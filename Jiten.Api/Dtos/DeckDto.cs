using Jiten.Core.Data;

namespace Jiten.Api.Dtos;

public class DeckDto
{
    public int DeckId { get; set; }
    public DateTimeOffset CreationDate { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string CoverName { get; set; } = "nocover.jpg";
    public MediaType MediaType { get; set; } = new();
    public string OriginalTitle { get; set; } = "Unknown";
    public string RomajiTitle { get; set; } = "";
    public string EnglishTitle { get; set; } = "";
    public string Description { get; set; } = "";
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
    public int UniqueWordCount { get; set; }
    public int UniqueWordUsedOnceCount { get; set; }
    public int UniqueKanjiCount { get; set; }
    public int UniqueKanjiUsedOnceCount { get; set; }
    public int Difficulty { get; set; }
    public float DifficultyRaw { get; set; }
    public int SentenceCount { get; set; }
    public float AverageSentenceLength { get; set; }
    public int? ParentDeckId { get; set; }
    public List<Link> Links { get; set; } = new List<Link>();
    public int ChildrenDeckCount { get; set; }
    public int SelectedWordOccurrences { get; set; }
    public float DialoguePercentage { get; set; }

    public DeckDto()
    {
    }

    public DeckDto(Deck deck, int occurrences)
    {
        DeckId = deck.DeckId;
        CreationDate = deck.CreationDate;
        ReleaseDate = deck.ReleaseDate.ToDateTime(new TimeOnly());
        CoverName = deck.CoverName;
        MediaType = deck.MediaType;
        OriginalTitle = deck.OriginalTitle;
        RomajiTitle = deck.RomajiTitle!;
        EnglishTitle = deck.EnglishTitle!;
        Description = deck.Description ?? "";
        CharacterCount = deck.CharacterCount;
        WordCount = deck.WordCount;
        UniqueWordCount = deck.UniqueWordCount;
        UniqueWordUsedOnceCount = deck.UniqueWordUsedOnceCount;
        UniqueKanjiCount = deck.UniqueKanjiCount;
        UniqueKanjiUsedOnceCount = deck.UniqueKanjiUsedOnceCount;
        Difficulty = MapDifficulty(deck.Difficulty);
        DifficultyRaw = deck.Difficulty;
        SentenceCount = deck.SentenceCount;
        AverageSentenceLength = deck.AverageSentenceLength;
        ParentDeckId = deck.ParentDeckId;
        Links = deck.Links;
        ChildrenDeckCount = deck.Children.Count;
        SelectedWordOccurrences = occurrences;
        DialoguePercentage = deck.DialoguePercentage;
    }

    public DeckDto(Deck deck)
    {
        DeckId = deck.DeckId;
        CreationDate = deck.CreationDate;
        ReleaseDate = deck.ReleaseDate.ToDateTime(new TimeOnly());
        CoverName = deck.CoverName;
        MediaType = deck.MediaType;
        OriginalTitle = deck.OriginalTitle;
        RomajiTitle = deck.RomajiTitle!;
        EnglishTitle = deck.EnglishTitle!;
        Description = deck.Description ?? "";
        CharacterCount = deck.CharacterCount;
        WordCount = deck.WordCount;
        UniqueWordCount = deck.UniqueWordCount;
        UniqueWordUsedOnceCount = deck.UniqueWordUsedOnceCount;
        UniqueKanjiCount = deck.UniqueKanjiCount;
        UniqueKanjiUsedOnceCount = deck.UniqueKanjiUsedOnceCount;
        Difficulty = MapDifficulty(deck.Difficulty);
        DifficultyRaw = deck.Difficulty;
        SentenceCount = deck.SentenceCount;
        AverageSentenceLength = deck.AverageSentenceLength;
        ParentDeckId = deck.ParentDeckId;
        Links = deck.Links;
        ChildrenDeckCount = deck.Children.Count;
        DialoguePercentage = deck.DialoguePercentage;
    }

    /// <summary>
    /// Remap the difficulty to an int while taking into account the biases of the model
    /// This is subject to change with a different training
    /// </summary>
    /// <param name="difficulty"></param>
    /// <returns></returns>
    private int MapDifficulty(float difficulty)
    {
        if (difficulty < 1.05)
            return 0;
        
        if (difficulty < 1.75)
            return 1;

        if (difficulty < 2.55)
            return 2;

        if (difficulty < 3.55)
            return 3;

        if (difficulty < 4)
            return 4;

        return 5;
    }
}