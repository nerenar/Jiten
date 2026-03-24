using System.ComponentModel.DataAnnotations;

namespace Jiten.Api.Dtos.Requests;

public class AddDeckWordRequest
{
    public int WordId { get; set; }
    public short ReadingIndex { get; set; }
    [Range(1, int.MaxValue)]
    public int Occurrences { get; set; } = 1;
}

public class BatchAddDeckWordsRequest
{
    public List<AddDeckWordRequest> Words { get; set; } = new();
}

public class UpdateDeckWordRequest
{
    [Range(1, int.MaxValue)]
    public int Occurrences { get; set; }
}
