namespace Jiten.Api.Dtos;

public class DeckCoverageResponse
{
    /// <summary>
    /// The deck ID
    /// </summary>
    public int DeckId { get; set; }
    
    /// <summary>
    /// Total word count in the deck
    /// </summary>
    public int TotalWordCount { get; set; }

    /// <summary>
    /// Total occurrences of the known words in the deck
    /// </summary>
    public int KnownWordsOccurrences { get; set; }
    
    /// <summary>
    /// Number of unique words that are in the deck known
    /// </summary>
    public int KnownUniqueWordCount { get; set; }
    
    /// <summary>
    /// Number of unique words in the deck
    /// </summary>
    public int UniqueWordCount { get; set; }
    
    /// <summary>
    /// Percentage of the words covered by known words
    /// </summary>
    public double KnownWordPercentage { get; set; }
    
    /// <summary>
    /// Percentage of the unique words covered by the known words
    /// </summary>
    public double KnownUniqueWordPercentage { get; set; }
}
