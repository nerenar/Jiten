using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jiten.Api.Services;

public class ApiKeyService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(IConfiguration configuration, ILogger<ApiKeyService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateApiKey()
    {
        // Use 32 bytes for high entropy (256 bits)
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);

        // Base64url encoding without padding for URL safety
        var base64 = Convert.ToBase64String(bytes)
                            .TrimEnd('=')
                            .Replace('+', '-')
                            .Replace('/', '_');

        return $"ak_{base64}";
    }

    public string ComputeHash(string apiKey)
    {
        var pepper = GetPepper();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(apiKey));

        // Use Base64 instead of hex for shorter storage
        return Convert.ToBase64String(hashBytes);
    }

    private string GetPepper()
    {
        var pepper = _configuration["ApiKey:Pepper"] ??
                     _configuration["JwtSettings:Secret"];

        if (string.IsNullOrEmpty(pepper))
        {
            _logger.LogError("API key pepper/secret not configured");
            throw new InvalidOperationException("API key pepper/secret not configured");
        }

        if (pepper.Length < 32)
        {
            _logger.LogWarning("API key pepper is shorter than recommended (32 characters)");
        }

        return pepper;
    }

    public bool ValidateApiKeyFormat(string apiKey)
    {
        return !string.IsNullOrEmpty(apiKey) &&
               apiKey.StartsWith("ak_") &&
               apiKey.Length > 10;
    }
}