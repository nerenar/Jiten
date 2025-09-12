namespace Jiten.Core.Data.User;

public enum KnownState
{
    Unknown = 0,
    Learning = 1,
    Known = 2,
    Suspended = 3
}

public class UserKnownWord
{
    public string UserId { get; set; } = string.Empty;
    public int WordId { get; set; }
    public byte ReadingIndex { get; set; }
    public DateTime LearnedDate { get; set; } = DateTime.UtcNow;
    public KnownState KnownState { get; set; } = KnownState.Known;
}