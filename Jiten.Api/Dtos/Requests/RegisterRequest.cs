using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class RegisterRequest
{
    [Required, MaxLength(50)]
    public string Username { get; set; }
    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; }
    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; }  
}