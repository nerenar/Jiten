using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class SendNotificationRequest
{
    public string? UserId { get; set; }
    public bool SendToEveryone { get; set; }

    [Required]
    [StringLength(200)]
    public required string Title { get; set; }

    [Required]
    [StringLength(2000)]
    public required string Message { get; set; }

    [StringLength(500)]
    public string? LinkUrl { get; set; }
}
