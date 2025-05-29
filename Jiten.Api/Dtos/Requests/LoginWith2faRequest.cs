using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class LoginWith2faRequest
{
    [Required]
    public required string UserId { get; set; }
    [Required]
    public required string TwoFactorCode { get; set; }
    public bool RememberMachine { get; set; }
}