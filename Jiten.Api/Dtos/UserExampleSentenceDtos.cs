namespace Jiten.Api.Dtos;

public class UserExampleSentenceDto
{
    public int UserExampleSentenceId { get; set; }
    public string Text { get; set; } = "";
    public string? Source { get; set; }
    public byte SortOrder { get; set; }
}

public class UpsertUserExampleSentenceRequest
{
    public required string Text { get; set; }
    public string? Source { get; set; }
}

public class FavouriteExampleSentenceRequest
{
    public required string Text { get; set; }
    public string? Source { get; set; }
}
