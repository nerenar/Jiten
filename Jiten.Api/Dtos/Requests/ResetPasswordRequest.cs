using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; set; }
    [Required]
    public required string Token { get; set; } 
    [Required, MinLength(10)]
    public required string NewPassword { get; set; }

}