using System.Text.Json;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Services;

public interface IVisitorLogService
{
    Task LogActionAsync(string action, object? details = null);
}

public class VisitorLogService : IVisitorLogService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VisitorLogService> _logger;

    public VisitorLogService(
        ApplicationDbContext dbContext, 
        IHttpContextAccessor httpContextAccessor,
        ILogger<VisitorLogService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogActionAsync(string action, object? details = null)
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return;

            var log = new VisitorLog
            {
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                PageUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}",
                PagePath = context.Request.Path.Value,
                HttpMethod = context.Request.Method,
                SessionId = context.Session?.Id,
                UserId = context.User?.Identity?.IsAuthenticated == true 
                    ? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                    : null,
                Action = action.Length > 100 ? action[..100] : action,
                ActionDetails = details != null 
                    ? JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = false })
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            // Truncate ActionDetails if too long
            if (log.ActionDetails?.Length > 1000)
                log.ActionDetails = log.ActionDetails[..1000];

            _dbContext.VisitorLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging action: {Action}", action);
        }
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
                return ip.Length > 45 ? ip[..45] : ip;
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
            return realIp.Length > 45 ? realIp[..45] : realIp;

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return remoteIp?.Length > 45 ? remoteIp[..45] : remoteIp ?? "unknown";
    }
}

