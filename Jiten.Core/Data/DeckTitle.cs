namespace Jiten.Core.Data;

public class DeckTitle
{
        public int DeckTitleId { get; set; }
        public int DeckId { get; set; }

        public string Title { get; set; } = null!;
        public DeckTitleType TitleType { get; set; }

        /// <summary>
        /// Generated column - Title with spaces removed for flexible search matching.
        /// This is computed by the database (GENERATED ALWAYS AS).
        /// </summary>
        public string? TitleNoSpaces { get; set; }

        public Deck Deck { get; set; } = null!;
}