using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class EnableAuthenticatorRequest
{
    [Required]
    public required string VerificationCode { get; set; }
}