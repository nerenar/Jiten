using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class RegisterRequest
{
    [Required, MaxLength(20)]
    public string Username { get; set; }

    [Required, EmailAddress, MaxLength(100)]
    public string Email { get; set; }

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; }

    public string RecaptchaResponse { get; set; }
    public bool TosAccepted { get; set; }
    public bool ReceiveNewsletter { get; set; }
}