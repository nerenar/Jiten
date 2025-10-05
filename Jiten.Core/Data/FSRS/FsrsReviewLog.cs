namespace Jiten.Core.Data.FSRS;

/// <summary>
/// Represents a record of a card review session
/// </summary>
public class FsrsReviewLog
{
    public long ReviewLogId { get; set; }
    
    /// <summary>
    /// ID of the card that was reviewed
    /// </summary>
    public long CardId { get; set; }

    /// <summary>
    /// Rating given during the review
    /// </summary>
    public FsrsRating Rating { get; }

    /// <summary>
    /// When the review took place
    /// </summary>
    public DateTime ReviewDateTime { get; }

    /// <summary>
    /// How long the review took in milliseconds (optional)
    /// </summary>
    public int? ReviewDuration { get; }

    public FsrsReviewLog()
    {
    }
    
    /// <summary>
    /// Creates a new review log entry
    /// </summary>
    public FsrsReviewLog(
        long cardId,
        FsrsRating rating,
        DateTime reviewDateTime,
        int? reviewDuration = null)
    {
        CardId = cardId;
        Rating = rating;
        ReviewDateTime = reviewDateTime;
        ReviewDuration = reviewDuration;
    }
}