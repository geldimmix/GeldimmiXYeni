using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Nobetci.Web.Data;
using Nobetci.Web.Data.Entities;

namespace Nobetci.Web.Services;

public interface IJwtService
{
    Task<(string? token, string? error)> GenerateTokenAsync(string apiKey);
    Task<(bool valid, int organizationId, string[] permissions)> ValidateTokenAsync(string token);
    string GenerateApiKey();
    string HashApiKey(string apiKey);
}

public class JwtService : IJwtService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtService> _logger;

    public JwtService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<JwtService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generate JWT token from API key
    /// </summary>
    public async Task<(string? token, string? error)> GenerateTokenAsync(string apiKey)
    {
        try
        {
            var keyHash = HashApiKey(apiKey);
            
            var apiKeyEntity = await _context.ApiKeys
                .Include(k => k.Organization)
                .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

            if (apiKeyEntity == null)
            {
                return (null, "Invalid API key");
            }

            if (apiKeyEntity.ExpiresAt.HasValue && apiKeyEntity.ExpiresAt.Value < DateTime.UtcNow)
            {
                return (null, "API key has expired");
            }

            // Update usage stats
            apiKeyEntity.LastUsedAt = DateTime.UtcNow;
            apiKeyEntity.UsageCount++;
            await _context.SaveChangesAsync();

            // Generate JWT
            var secret = _configuration["Jwt:Secret"] ?? "GeldimmiDefaultSecretKey2026!SuperSecure";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, apiKeyEntity.OrganizationId.ToString()),
                new("organization_id", apiKeyEntity.OrganizationId.ToString()),
                new("organization_name", apiKeyEntity.Organization.Name),
                new("api_key_id", apiKeyEntity.Id.ToString()),
                new("api_key_name", apiKeyEntity.Name)
            };

            // Add permissions as claims
            var permissions = apiKeyEntity.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission.Trim()));
            }

            var tokenExpiry = _configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "Geldimmi",
                audience: _configuration["Jwt:Audience"] ?? "GeldimmiApi",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(tokenExpiry),
                signingCredentials: credentials
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating JWT token");
            return (null, "Error generating token");
        }
    }

    /// <summary>
    /// Validate JWT token and extract claims
    /// </summary>
    public async Task<(bool valid, int organizationId, string[] permissions)> ValidateTokenAsync(string token)
    {
        try
        {
            var secret = _configuration["Jwt:Secret"] ?? "GeldimmiDefaultSecretKey2026!SuperSecure";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "Geldimmi",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "GeldimmiApi",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            
            var orgIdClaim = principal.FindFirst("organization_id")?.Value;
            var permissions = principal.FindAll("permission").Select(c => c.Value).ToArray();

            if (int.TryParse(orgIdClaim, out var organizationId))
            {
                return (true, organizationId, permissions);
            }

            return (false, 0, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return (false, 0, Array.Empty<string>());
        }
    }

    /// <summary>
    /// Generate a new random API key
    /// </summary>
    public string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return "gld_" + Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 40);
    }

    /// <summary>
    /// Hash API key for secure storage
    /// </summary>
    public string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

