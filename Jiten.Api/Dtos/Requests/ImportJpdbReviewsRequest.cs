namespace Jiten.Api.Dtos.Requests;

public class ImportJpdbReviewsRequest
{
    public List<JpdbReviewCard> Cards { get; set; } = [];
}

public class JpdbReviewCard
{
    public int WordId { get; set; }
    public string Spelling { get; set; } = "";
    public List<JpdbReview> Reviews { get; set; } = [];
}

public class JpdbReview
{
    public long Timestamp { get; set; }
    public string Grade { get; set; } = "";
}
