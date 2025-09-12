namespace Jiten.Api.Dtos;

public class GoogleUserInfo
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
    public bool VerifiedEmail { get; set; }
}