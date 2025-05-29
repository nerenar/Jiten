using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }
}