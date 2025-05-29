using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class RefreshTokenRequest
{
    [Required]
    public required string AccessToken { get; set; }

    [Required]
    public required string RefreshToken { get; set; }
}