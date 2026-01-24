using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;
using System.Text;

namespace Nobetci.Web.Controllers;

/// <summary>
/// API Controller for external work hours (attendance) tracking
/// Uses Basic Authentication with user-defined credentials
/// </summary>
[Route("api/v1/workhours")]
[ApiController]
[AllowAnonymous]
public class WorkHoursApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkHoursApiController> _logger;

    public WorkHoursApiController(ApplicationDbContext context, ILogger<WorkHoursApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Record employee check-in or check-out
    /// POST /api/v1/workhours/record
    /// </summary>
    [HttpPost("record")]
    public async Task<IActionResult> RecordAttendance([FromBody] AttendanceRecordRequest request)
    {
        try
        {
            // 1. Validate Basic Auth
            var credential = await ValidateBasicAuthAsync();
            if (credential == null)
            {
                return Unauthorized(new ApiErrorResponse
                {
                    Success = false,
                    Error = "Unauthorized",
                    Message = "Invalid API credentials"
                });
            }

            // 2. Check monthly limit
            await ResetMonthlyCounterIfNeeded(credential);
            
            if (credential.CurrentMonthRequests >= credential.MonthlyRequestLimit)
            {
                return StatusCode(429, new ApiErrorResponse
                {
                    Success = false,
                    Error = "RateLimitExceeded",
                    Message = $"Monthly request limit ({credential.MonthlyRequestLimit}) exceeded. Resets on {credential.MonthlyResetDate:yyyy-MM-dd}"
                });
            }

            // 3. Validate request
            if (string.IsNullOrEmpty(request.IdentityNo))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    Error = "ValidationError",
                    Message = "IdentityNo (sicil numarası) is required"
                });
            }

            if (!request.Date.HasValue)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    Error = "ValidationError",
                    Message = "Date is required (format: YYYY-MM-DD)"
                });
            }

            if (!request.Time.HasValue)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    Error = "ValidationError",
                    Message = "Time is required (format: HH:mm)"
                });
            }

            if (string.IsNullOrEmpty(request.Action) || 
                (request.Action.ToLower() != "checkin" && request.Action.ToLower() != "checkout"))
            {
                return BadRequest(new ApiErrorResponse
                {
                    Success = false,
                    Error = "ValidationError",
                    Message = "Action must be 'checkin' or 'checkout'"
                });
            }

            // 4. Find employee by identity number
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.OrganizationId == credential.OrganizationId && 
                                         e.IdentityNo == request.IdentityNo);

            if (employee == null)
            {
                return NotFound(new ApiErrorResponse
                {
                    Success = false,
                    Error = "EmployeeNotFound",
                    Message = $"Employee with IdentityNo '{request.IdentityNo}' not found"
                });
            }

            // 5. Find or create attendance record for the date
            var date = DateOnly.FromDateTime(request.Date.Value);
            var time = TimeOnly.FromTimeSpan(request.Time.Value);
            var isCheckIn = request.Action.ToLower() == "checkin";

            var attendance = await _context.TimeAttendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Date == date);

            if (attendance == null)
            {
                attendance = new TimeAttendance
                {
                    EmployeeId = employee.Id,
                    Date = date,
                    Source = AttendanceSource.Api,
                    SourceIdentifier = credential.ApiUsername,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.TimeAttendances.Add(attendance);
            }
            else
            {
                attendance.UpdatedAt = DateTime.UtcNow;
            }

            // 6. Set check-in or check-out time
            if (isCheckIn)
            {
                attendance.CheckInTime = time;
            }
            else
            {
                attendance.CheckOutTime = time;
            }

            // 7. Calculate worked hours if both times exist
            if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
            {
                var checkIn = attendance.CheckInTime.Value;
                var checkOut = attendance.CheckOutTime.Value;
                
                TimeSpan duration;
                if (checkOut < checkIn)
                {
                    // Checkout is next day
                    attendance.CheckOutToNextDay = true;
                    duration = TimeSpan.FromHours(24) - checkIn.ToTimeSpan() + checkOut.ToTimeSpan();
                }
                else
                {
                    duration = checkOut.ToTimeSpan() - checkIn.ToTimeSpan();
                }
                
                attendance.WorkedHours = (decimal)duration.TotalHours;
            }

            // 8. Update request counter
            credential.CurrentMonthRequests++;
            credential.TotalRequests++;
            credential.LastUsedAt = DateTime.UtcNow;
            credential.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "API Attendance recorded: {IdentityNo}, {Date}, {Action}, {Time} by {ApiUser}",
                request.IdentityNo, date, request.Action, time, credential.ApiUsername);

            return Ok(new ApiSuccessResponse
            {
                Success = true,
                Message = $"{(isCheckIn ? "Check-in" : "Check-out")} recorded successfully",
                Data = new
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName,
                    Date = date.ToString("yyyy-MM-dd"),
                    Action = request.Action,
                    Time = time.ToString("HH:mm"),
                    CheckInTime = attendance.CheckInTime?.ToString("HH:mm"),
                    CheckOutTime = attendance.CheckOutTime?.ToString("HH:mm"),
                    WorkedHours = attendance.WorkedHours,
                    RemainingRequests = credential.MonthlyRequestLimit - credential.CurrentMonthRequests
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording attendance via API");
            return StatusCode(500, new ApiErrorResponse
            {
                Success = false,
                Error = "InternalError",
                Message = "An error occurred while processing your request"
            });
        }
    }

    /// <summary>
    /// Get API usage statistics
    /// GET /api/v1/workhours/status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var credential = await ValidateBasicAuthAsync();
            if (credential == null)
            {
                return Unauthorized(new ApiErrorResponse
                {
                    Success = false,
                    Error = "Unauthorized",
                    Message = "Invalid API credentials"
                });
            }

            await ResetMonthlyCounterIfNeeded(credential);
            await _context.SaveChangesAsync();

            return Ok(new ApiSuccessResponse
            {
                Success = true,
                Message = "API status retrieved successfully",
                Data = new
                {
                    IsActive = credential.IsActive,
                    MonthlyLimit = credential.MonthlyRequestLimit,
                    UsedThisMonth = credential.CurrentMonthRequests,
                    RemainingThisMonth = Math.Max(0, credential.MonthlyRequestLimit - credential.CurrentMonthRequests),
                    ResetDate = credential.MonthlyResetDate.ToString("yyyy-MM-dd"),
                    TotalRequestsAllTime = credential.TotalRequests,
                    LastUsed = credential.LastUsedAt?.ToString("yyyy-MM-dd HH:mm:ss")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API status");
            return StatusCode(500, new ApiErrorResponse
            {
                Success = false,
                Error = "InternalError",
                Message = "An error occurred while processing your request"
            });
        }
    }

    /// <summary>
    /// Validate Basic Authentication header and return the credential if valid
    /// </summary>
    private async Task<UserApiCredential?> ValidateBasicAuthAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return null;

        var authValue = authHeader.ToString();
        if (!authValue.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            var base64Credentials = authValue.Substring(6);
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(base64Credentials));
            var parts = credentials.Split(':', 2);

            if (parts.Length != 2)
                return null;

            var username = parts[0];
            var password = parts[1];

            var credential = await _context.UserApiCredentials
                .FirstOrDefaultAsync(c => c.ApiUsername == username && c.IsActive);

            if (credential == null)
                return null;

            // Verify password using BCrypt
            if (!BCrypt.Net.BCrypt.Verify(password, credential.ApiPasswordHash))
                return null;

            return credential;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reset monthly counter if we're in a new month
    /// </summary>
    private async Task ResetMonthlyCounterIfNeeded(UserApiCredential credential)
    {
        if (DateTime.UtcNow >= credential.MonthlyResetDate)
        {
            credential.CurrentMonthRequests = 0;
            
            // Set next reset date to first day of next month
            var now = DateTime.UtcNow;
            credential.MonthlyResetDate = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            credential.UpdatedAt = DateTime.UtcNow;
        }
    }
}

/// <summary>
/// Request model for recording attendance
/// </summary>
public class AttendanceRecordRequest
{
    /// <summary>
    /// Employee identity number (sicil numarası)
    /// </summary>
    public string? IdentityNo { get; set; }
    
    /// <summary>
    /// Date of the record (format: YYYY-MM-DD)
    /// </summary>
    public DateTime? Date { get; set; }
    
    /// <summary>
    /// Action type: "checkin" or "checkout"
    /// </summary>
    public string? Action { get; set; }
    
    /// <summary>
    /// Time of the action (format: HH:mm)
    /// </summary>
    public TimeSpan? Time { get; set; }
}

public class ApiSuccessResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class ApiErrorResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

