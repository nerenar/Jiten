using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; set; }
    [Required, MinLength(10)]
    public required string NewPassword { get; set; }
}