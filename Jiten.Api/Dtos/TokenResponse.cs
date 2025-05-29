namespace Jiten.Api.Dtos;

public class TokenResponse
{
    public required string AccessToken { get; set; }
    public DateTime AccessTokenExpiration { get; set; }
    public required string RefreshToken { get; set; }
}