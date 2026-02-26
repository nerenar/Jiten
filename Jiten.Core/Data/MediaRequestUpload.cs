namespace Jiten.Core.Data;

public class MediaRequestUpload
{
    public int Id { get; set; }
    public int MediaRequestCommentId { get; set; }
    public int MediaRequestId { get; set; }
    public required string FileName { get; set; }
    public required string StoragePath { get; set; }
    public long FileSize { get; set; }
    public int OriginalFileCount { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool AdminReviewed { get; set; }
    public string? AdminNote { get; set; }
    public bool FileDeleted { get; set; }

    public MediaRequestComment? Comment { get; set; }
    public MediaRequest? MediaRequest { get; set; }
}
