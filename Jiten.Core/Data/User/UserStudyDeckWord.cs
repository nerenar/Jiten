namespace Jiten.Core.Data.User;

public class UserStudyDeckWord
{
    public int UserStudyDeckId { get; set; }
    public int WordId { get; set; }
    public short ReadingIndex { get; set; }
    public int SortOrder { get; set; }
    public int Occurrences { get; set; } = 1;

    public UserStudyDeck StudyDeck { get; set; } = default!;
}
