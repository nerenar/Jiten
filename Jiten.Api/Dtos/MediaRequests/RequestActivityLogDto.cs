using Jiten.Core.Data;

namespace Jiten.Api.Dtos;

public class RequestActivityLogDto
{
    public long Id { get; set; }
    public int? MediaRequestId { get; set; }
    public string? RequestTitle { get; set; }
    public required string UserId { get; set; }
    public string? UserName { get; set; }
    public string? TargetUserId { get; set; }
    public RequestAction Action { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
}
