using System.Diagnostics;
using System.Text.RegularExpressions;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Middleware;

public class VisitorTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VisitorTrackingMiddleware> _logger;

    // Skip these paths from logging
    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/favicon.ico",
        "/robots.txt",
        "/sitemap.xml"
    };

    // Skip these extensions
    private static readonly HashSet<string> SkipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".eot", ".map"
    };

    public VisitorTrackingMiddleware(RequestDelegate next, ILogger<VisitorTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        var path = context.Request.Path.Value ?? "/";

        // Skip static files and certain paths
        if (ShouldSkipLogging(path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            try
            {
                var log = CreateVisitorLog(context, stopwatch.ElapsedMilliseconds);
                dbContext.VisitorLogs.Add(log);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging visitor");
            }
        }
    }

    private static bool ShouldSkipLogging(string path)
    {
        // Skip specific paths
        if (SkipPaths.Contains(path))
            return true;

        // Skip static files
        var extension = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(extension) && SkipExtensions.Contains(extension))
            return true;

        // Skip paths starting with /lib/, /css/, /js/, /images/
        if (path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/css/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/_", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private VisitorLog CreateVisitorLog(HttpContext context, long responseTimeMs)
    {
        var request = context.Request;
        var userAgent = request.Headers.UserAgent.ToString();
        var (browser, browserVersion, os, deviceType) = ParseUserAgent(userAgent);

        // Get real IP address (consider proxies)
        var ipAddress = GetClientIpAddress(context);

        // Get session ID
        var sessionId = context.Session?.Id;

        // Get user ID if authenticated
        string? userId = null;
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        }

        // Get language from Accept-Language header
        var language = request.Headers.AcceptLanguage.ToString().Split(',').FirstOrDefault()?.Split(';').FirstOrDefault();

        return new VisitorLog
        {
            IpAddress = ipAddress,
            UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
            Browser = browser,
            BrowserVersion = browserVersion,
            OperatingSystem = os,
            DeviceType = deviceType,
            PageUrl = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
            PagePath = request.Path.Value?.Length > 100 ? request.Path.Value[..100] : request.Path.Value,
            QueryString = request.QueryString.Value?.Length > 500 ? request.QueryString.Value[..500] : request.QueryString.Value,
            HttpMethod = request.Method,
            Referrer = request.Headers.Referer.ToString().Length > 500 
                ? request.Headers.Referer.ToString()[..500] 
                : request.Headers.Referer.ToString(),
            Language = language?.Length > 10 ? language[..10] : language,
            StatusCode = context.Response.StatusCode,
            ResponseTimeMs = responseTimeMs,
            SessionId = sessionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
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

    private static (string? browser, string? version, string? os, string? deviceType) ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return (null, null, null, null);

        string? browser = null;
        string? version = null;
        string? os = null;
        string? deviceType = "Desktop";

        // Detect device type
        if (Regex.IsMatch(userAgent, @"Mobile|Android|iPhone|iPad|iPod|webOS|BlackBerry|Opera Mini|IEMobile", RegexOptions.IgnoreCase))
        {
            deviceType = Regex.IsMatch(userAgent, @"iPad|Tablet", RegexOptions.IgnoreCase) ? "Tablet" : "Mobile";
        }

        // Detect OS
        if (userAgent.Contains("Windows NT 10", StringComparison.OrdinalIgnoreCase))
            os = "Windows 10/11";
        else if (userAgent.Contains("Windows NT 6.3", StringComparison.OrdinalIgnoreCase))
            os = "Windows 8.1";
        else if (userAgent.Contains("Windows NT 6.2", StringComparison.OrdinalIgnoreCase))
            os = "Windows 8";
        else if (userAgent.Contains("Windows NT 6.1", StringComparison.OrdinalIgnoreCase))
            os = "Windows 7";
        else if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            os = "Windows";
        else if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
            os = "macOS";
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            os = "iOS";
        else if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            os = "iPadOS";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            os = "Android";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            os = "Linux";

        // Detect browser
        var edgeMatch = Regex.Match(userAgent, @"Edg[eA]?/(\d+\.?\d*)", RegexOptions.IgnoreCase);
        if (edgeMatch.Success)
        {
            browser = "Edge";
            version = edgeMatch.Groups[1].Value;
        }
        else
        {
            var chromeMatch = Regex.Match(userAgent, @"Chrome/(\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (chromeMatch.Success && !userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            {
                browser = "Chrome";
                version = chromeMatch.Groups[1].Value;
            }
        }

        if (browser == null)
        {
            var firefoxMatch = Regex.Match(userAgent, @"Firefox/(\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (firefoxMatch.Success)
            {
                browser = "Firefox";
                version = firefoxMatch.Groups[1].Value;
            }
        }

        if (browser == null)
        {
            var safariMatch = Regex.Match(userAgent, @"Version/(\d+\.?\d*).*Safari", RegexOptions.IgnoreCase);
            if (safariMatch.Success)
            {
                browser = "Safari";
                version = safariMatch.Groups[1].Value;
            }
        }

        if (browser == null)
        {
            var operaMatch = Regex.Match(userAgent, @"OPR/(\d+\.?\d*)", RegexOptions.IgnoreCase);
            if (operaMatch.Success)
            {
                browser = "Opera";
                version = operaMatch.Groups[1].Value;
            }
        }

        if (browser == null && userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Bot";
            deviceType = "Bot";
        }

        return (
            browser?.Length > 100 ? browser[..100] : browser,
            version?.Length > 100 ? version[..100] : version,
            os?.Length > 100 ? os[..100] : os,
            deviceType
        );
    }
}

// Extension method for easy registration
public static class VisitorTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseVisitorTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<VisitorTrackingMiddleware>();
    }
}


