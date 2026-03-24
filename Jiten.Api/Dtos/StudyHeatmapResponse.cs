namespace Jiten.Api.Dtos;

public class StudyHeatmapResponse
{
    public int Year { get; set; }
    public List<HeatmapDayDto> Days { get; set; } = new();
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalReviewDays { get; set; }
    public int TotalReviews { get; set; }
}

public class HeatmapDayDto
{
    public DateOnly Date { get; set; }
    public int ReviewCount { get; set; }
    public int CorrectCount { get; set; }
}
