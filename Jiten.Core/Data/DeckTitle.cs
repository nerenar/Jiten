namespace Jiten.Core.Data;

public class DeckTitle
{
        public int DeckTitleId { get; set; }
        public int DeckId { get; set; }

        public string Title { get; set; } = null!;
        public DeckTitleType TitleType { get; set; }

        public Deck Deck { get; set; } = null!;
}