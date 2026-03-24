using System.ComponentModel.DataAnnotations;
using Jiten.Core.Data.FSRS;

namespace Jiten.Api.Dtos.Requests;

public class SrsReviewRequest
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public FsrsRating Rating { get; set; }
    [Range(0, 60_000)]
    public int? ReviewDuration { get; set; }
    public string? SessionId { get; set; }
    [MaxLength(64)]
    public string? ClientRequestId { get; set; }
}