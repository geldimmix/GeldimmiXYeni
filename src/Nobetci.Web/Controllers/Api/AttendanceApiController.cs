using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using Nobetci.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace Nobetci.Web.Controllers.Api;

/// <summary>
/// API for time attendance (mesai takip) - requires JWT authentication
/// </summary>
[ApiController]
[Route("api/v1/attendance")]
public class AttendanceApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AttendanceApiController> _logger;

    public AttendanceApiController(
        ApplicationDbContext context,
        IJwtService jwtService,
        ILogger<AttendanceApiController> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _logger = logger;
    }

    #region Authentication

    /// <summary>
    /// Get JWT token using API key
    /// POST /api/v1/attendance/auth
    /// </summary>
    [HttpPost("auth")]
    [AllowAnonymous]
    public async Task<IActionResult> Authenticate([FromBody] AuthRequest request)
    {
        if (string.IsNullOrEmpty(request.ApiKey))
        {
            return BadRequest(new ApiResponse { Success = false, Error = "API key is required" });
        }

        var (token, error) = await _jwtService.GenerateTokenAsync(request.ApiKey);

        if (token == null)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = error });
        }

        return Ok(new AuthResponse 
        { 
            Success = true, 
            Token = token,
            ExpiresIn = 3600 // 60 minutes
        });
    }

    #endregion

    #region Check-In / Check-Out

    /// <summary>
    /// Record employee check-in (giriş kaydı)
    /// POST /api/v1/attendance/checkin
    /// </summary>
    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        var (valid, organizationId, permissions) = await ValidateRequestAsync();
        if (!valid)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "Invalid or expired token" });
        }

        if (!permissions.Contains("attendance:write"))
        {
            return Forbid();
        }

        // Find employee
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.OrganizationId == organizationId && e.IsActive);

        if (employee == null)
        {
            return NotFound(new ApiResponse { Success = false, Error = "Employee not found" });
        }

        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Now);
        var time = request.Time ?? TimeOnly.FromDateTime(DateTime.Now);

        // Check if already checked in today
        var existingRecord = await _context.TimeAttendances
            .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.Date == date);

        if (existingRecord != null && existingRecord.CheckInTime.HasValue)
        {
            return BadRequest(new ApiResponse 
            { 
                Success = false, 
                Error = "Employee already checked in for this date",
                Data = new { existingCheckIn = existingRecord.CheckInTime.Value.ToString("HH:mm") }
            });
        }

        TimeAttendance attendance;
        if (existingRecord != null)
        {
            // Update existing record
            existingRecord.CheckInTime = time;
            existingRecord.Source = AttendanceSource.Api;
            existingRecord.SourceIdentifier = GetSourceIdentifier();
            existingRecord.CheckInLocation = request.Location;
            existingRecord.UpdatedAt = DateTime.UtcNow;
            attendance = existingRecord;
        }
        else
        {
            // Create new record
            attendance = new TimeAttendance
            {
                EmployeeId = request.EmployeeId,
                Date = date,
                CheckInTime = time,
                Source = AttendanceSource.Api,
                SourceIdentifier = GetSourceIdentifier(),
                CheckInLocation = request.Location,
                Notes = request.Notes
            };
            _context.TimeAttendances.Add(attendance);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Check-in recorded for employee {EmployeeId} at {Time}", request.EmployeeId, time);

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Check-in recorded successfully",
            Data = new
            {
                attendanceId = attendance.Id,
                employeeId = employee.Id,
                employeeName = employee.FullName,
                date = date.ToString("yyyy-MM-dd"),
                checkInTime = time.ToString("HH:mm:ss")
            }
        });
    }

    /// <summary>
    /// Record employee check-out (çıkış kaydı)
    /// POST /api/v1/attendance/checkout
    /// </summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request)
    {
        var (valid, organizationId, permissions) = await ValidateRequestAsync();
        if (!valid)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "Invalid or expired token" });
        }

        if (!permissions.Contains("attendance:write"))
        {
            return Forbid();
        }

        // Find employee
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.OrganizationId == organizationId && e.IsActive);

        if (employee == null)
        {
            return NotFound(new ApiResponse { Success = false, Error = "Employee not found" });
        }

        var date = request.Date ?? DateOnly.FromDateTime(DateTime.Now);
        var time = request.Time ?? TimeOnly.FromDateTime(DateTime.Now);

        // Find today's check-in record
        var attendance = await _context.TimeAttendances
            .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.Date == date);

        if (attendance == null)
        {
            // Create record with only check-out (unusual but allowed)
            attendance = new TimeAttendance
            {
                EmployeeId = request.EmployeeId,
                Date = date,
                CheckOutTime = time,
                CheckOutToNextDay = request.SpansNextDay,
                Source = AttendanceSource.Api,
                SourceIdentifier = GetSourceIdentifier(),
                CheckOutLocation = request.Location,
                Notes = request.Notes
            };
            _context.TimeAttendances.Add(attendance);
        }
        else
        {
            attendance.CheckOutTime = time;
            attendance.CheckOutToNextDay = request.SpansNextDay;
            attendance.CheckOutLocation = request.Location;
            attendance.UpdatedAt = DateTime.UtcNow;

            // Calculate worked hours
            if (attendance.CheckInTime.HasValue)
            {
                attendance.WorkedHours = CalculateWorkedHours(
                    attendance.CheckInTime.Value, 
                    time, 
                    attendance.CheckInFromPreviousDay,
                    request.SpansNextDay);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Check-out recorded for employee {EmployeeId} at {Time}", request.EmployeeId, time);

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Check-out recorded successfully",
            Data = new
            {
                attendanceId = attendance.Id,
                employeeId = employee.Id,
                employeeName = employee.FullName,
                date = date.ToString("yyyy-MM-dd"),
                checkInTime = attendance.CheckInTime?.ToString("HH:mm:ss"),
                checkOutTime = time.ToString("HH:mm:ss"),
                workedHours = attendance.WorkedHours
            }
        });
    }

    /// <summary>
    /// Record both check-in and check-out at once
    /// POST /api/v1/attendance/record
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordAttendance([FromBody] AttendanceRecordRequest request)
    {
        var (valid, organizationId, permissions) = await ValidateRequestAsync();
        if (!valid)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "Invalid or expired token" });
        }

        if (!permissions.Contains("attendance:write"))
        {
            return Forbid();
        }

        // Find employee
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeId && e.OrganizationId == organizationId && e.IsActive);

        if (employee == null)
        {
            return NotFound(new ApiResponse { Success = false, Error = "Employee not found" });
        }

        // Check for existing record
        var existing = await _context.TimeAttendances
            .FirstOrDefaultAsync(t => t.EmployeeId == request.EmployeeId && t.Date == request.Date);

        if (existing != null && !request.Overwrite)
        {
            return BadRequest(new ApiResponse 
            { 
                Success = false, 
                Error = "Record already exists for this date. Set overwrite=true to replace." 
            });
        }

        var attendance = existing ?? new TimeAttendance
        {
            EmployeeId = request.EmployeeId,
            Date = request.Date
        };

        attendance.CheckInTime = request.CheckInTime;
        attendance.CheckOutTime = request.CheckOutTime;
        attendance.CheckInFromPreviousDay = request.CheckInFromPreviousDay;
        attendance.CheckOutToNextDay = request.CheckOutToNextDay;
        attendance.Type = request.Type;
        attendance.Source = AttendanceSource.Api;
        attendance.SourceIdentifier = GetSourceIdentifier();
        attendance.Notes = request.Notes;
        attendance.UpdatedAt = DateTime.UtcNow;

        // Calculate worked hours
        if (request.CheckInTime.HasValue && request.CheckOutTime.HasValue)
        {
            attendance.WorkedHours = CalculateWorkedHours(
                request.CheckInTime.Value, 
                request.CheckOutTime.Value, 
                request.CheckInFromPreviousDay,
                request.CheckOutToNextDay);
        }

        if (existing == null)
        {
            _context.TimeAttendances.Add(attendance);
        }

        await _context.SaveChangesAsync();

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Attendance record saved",
            Data = new
            {
                attendanceId = attendance.Id,
                employeeId = employee.Id,
                employeeName = employee.FullName,
                date = request.Date.ToString("yyyy-MM-dd"),
                checkInTime = attendance.CheckInTime?.ToString("HH:mm:ss"),
                checkOutTime = attendance.CheckOutTime?.ToString("HH:mm:ss"),
                workedHours = attendance.WorkedHours
            }
        });
    }

    #endregion

    #region Query

    /// <summary>
    /// Get attendance records for an employee
    /// GET /api/v1/attendance/employee/{employeeId}?from=2026-01-01&to=2026-01-31
    /// </summary>
    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetEmployeeAttendance(int employeeId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var (valid, organizationId, permissions) = await ValidateRequestAsync();
        if (!valid)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "Invalid or expired token" });
        }

        if (!permissions.Contains("attendance:read") && !permissions.Contains("attendance:write"))
        {
            return Forbid();
        }

        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.OrganizationId == organizationId);

        if (employee == null)
        {
            return NotFound(new ApiResponse { Success = false, Error = "Employee not found" });
        }

        var fromDate = from ?? DateOnly.FromDateTime(DateTime.Now.AddDays(-30));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.Now);

        var records = await _context.TimeAttendances
            .Where(t => t.EmployeeId == employeeId && t.Date >= fromDate && t.Date <= toDate)
            .OrderByDescending(t => t.Date)
            .Select(t => new
            {
                id = t.Id,
                date = t.Date.ToString("yyyy-MM-dd"),
                checkInTime = t.CheckInTime.HasValue ? t.CheckInTime.Value.ToString("HH:mm:ss") : null,
                checkOutTime = t.CheckOutTime.HasValue ? t.CheckOutTime.Value.ToString("HH:mm:ss") : null,
                workedHours = t.WorkedHours,
                type = t.Type.ToString(),
                source = t.Source.ToString(),
                isApproved = t.IsApproved,
                notes = t.Notes
            })
            .ToListAsync();

        return Ok(new ApiResponse
        {
            Success = true,
            Data = new
            {
                employee = new { id = employee.Id, name = employee.FullName, title = employee.Title },
                from = fromDate.ToString("yyyy-MM-dd"),
                to = toDate.ToString("yyyy-MM-dd"),
                records = records
            }
        });
    }

    /// <summary>
    /// Get all employees with their today's attendance status
    /// GET /api/v1/attendance/today
    /// </summary>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayAttendance()
    {
        var (valid, organizationId, permissions) = await ValidateRequestAsync();
        if (!valid)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "Invalid or expired token" });
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organizationId && e.IsActive)
            .Select(e => new
            {
                id = e.Id,
                name = e.FullName,
                title = e.Title,
                attendance = _context.TimeAttendances
                    .Where(t => t.EmployeeId == e.Id && t.Date == today)
                    .Select(t => new
                    {
                        checkInTime = t.CheckInTime.HasValue ? t.CheckInTime.Value.ToString("HH:mm") : null,
                        checkOutTime = t.CheckOutTime.HasValue ? t.CheckOutTime.Value.ToString("HH:mm") : null,
                        workedHours = t.WorkedHours,
                        status = t.CheckOutTime.HasValue ? "completed" : (t.CheckInTime.HasValue ? "working" : "absent")
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new ApiResponse
        {
            Success = true,
            Data = new
            {
                date = today.ToString("yyyy-MM-dd"),
                employees = employees
            }
        });
    }

    /// <summary>
    /// Get list of employees (for device integration)
    /// GET /api/v1/attendance/employees
    /// </summary>
    [HttpGet("employees")]
    public async Task<IActionResult> GetEmployees()
    {
        var (valid, organizationId, permissions) = await ValidateRequestAsync();
        if (!valid)
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "Invalid or expired token" });
        }

        var employees = await _context.Employees
            .Where(e => e.OrganizationId == organizationId && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new
            {
                id = e.Id,
                name = e.FullName,
                title = e.Title,
                identityNo = e.IdentityNo
            })
            .ToListAsync();

        return Ok(new ApiResponse
        {
            Success = true,
            Data = new { employees }
        });
    }

    #endregion

    #region Helpers

    private async Task<(bool valid, int organizationId, string[] permissions)> ValidateRequestAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return (false, 0, Array.Empty<string>());
        }

        var token = authHeader.Substring("Bearer ".Length);
        return await _jwtService.ValidateTokenAsync(token);
    }

    private string GetSourceIdentifier()
    {
        return Request.Headers["X-Device-Id"].FirstOrDefault() 
            ?? Request.Headers["User-Agent"].FirstOrDefault()?.Substring(0, Math.Min(100, Request.Headers["User-Agent"].FirstOrDefault()?.Length ?? 0))
            ?? "API";
    }

    private static decimal CalculateWorkedHours(TimeOnly checkIn, TimeOnly checkOut, bool fromPreviousDay, bool toNextDay)
    {
        var checkInMinutes = checkIn.Hour * 60 + checkIn.Minute;
        var checkOutMinutes = checkOut.Hour * 60 + checkOut.Minute;

        if (fromPreviousDay)
        {
            checkInMinutes -= 24 * 60; // Subtract a day
        }

        if (toNextDay)
        {
            checkOutMinutes += 24 * 60; // Add a day
        }

        var totalMinutes = checkOutMinutes - checkInMinutes;
        return Math.Round(totalMinutes / 60m, 2);
    }

    #endregion
}

#region DTOs

public class AuthRequest
{
    [Required]
    public string ApiKey { get; set; } = string.Empty;
}

public class AuthResponse : ApiResponse
{
    public string? Token { get; set; }
    public int ExpiresIn { get; set; }
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public object? Data { get; set; }
}

public class CheckInRequest
{
    [Required]
    public int EmployeeId { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

public class CheckOutRequest
{
    [Required]
    public int EmployeeId { get; set; }
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public bool SpansNextDay { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceRecordRequest
{
    [Required]
    public int EmployeeId { get; set; }
    
    [Required]
    public DateOnly Date { get; set; }
    
    public TimeOnly? CheckInTime { get; set; }
    public TimeOnly? CheckOutTime { get; set; }
    public bool CheckInFromPreviousDay { get; set; }
    public bool CheckOutToNextDay { get; set; }
    public AttendanceType Type { get; set; } = AttendanceType.Normal;
    public string? Notes { get; set; }
    public bool Overwrite { get; set; }
}

#endregion

