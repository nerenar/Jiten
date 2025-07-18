using System.Text.Json.Serialization;

namespace Jiten.Core.Data.Providers.Igdb;

public class IgdbAccessToken
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    public DateTime ExpirationTime { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpirationTime;
}
