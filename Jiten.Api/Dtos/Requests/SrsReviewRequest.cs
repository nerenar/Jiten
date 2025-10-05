using Jiten.Core.Data.FSRS;

namespace Jiten.Api.Dtos.Requests;

public class SrsReviewRequest
{
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public FsrsRating Rating { get; set; }
}