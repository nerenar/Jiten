namespace Jiten.Api.Dtos;

public class AuthenticatorResponse
{
    public required string SharedKey { get; set; }
    public required string AuthenticatorUri { get; set; }
    public IEnumerable<string>? RecoveryCodes { get; set; }
}