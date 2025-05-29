using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos;

public class LoginRequest
{
    [Required]
    public required string UsernameOrEmail { get; set; }
    [Required]
    public required string Password { get; set; }
}