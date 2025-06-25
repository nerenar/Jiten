using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Jobs;

public class ParseJob(JitenDbContext context)
{
    public async Task Parse(Metadata metadata, MediaType deckType, bool storeRawText = false)
    {
        Deck deck = new();
        string filePath = metadata.FilePath;

        if (!string.IsNullOrEmpty(metadata.FilePath))
        {
            if (!File.Exists(metadata.FilePath))
            {
                throw new FileNotFoundException($"File {filePath} not found.");
            }

            string text = "";
            if (Path.GetExtension(filePath).ToLower() == ".epub")
            {
                var extractor = new EbookExtractor();
                text = await extractor.ExtractTextFromEbook(filePath);

                if (string.IsNullOrEmpty(text))
                {
                    throw new Exception("No text found in the ebook.");
                }
            }
            else
            {
                text = await File.ReadAllTextAsync(filePath);
            }

            deck = await Parser.Parser.ParseTextToDeck(context, text, storeRawText, true, deckType);
        }

        // Process children recursively
        int deckOrder = 0;
        foreach (var child in metadata.Children)
        {
            var childDeck = await ParseChild(child, deck, deckType, ++deckOrder, storeRawText);
            deck.Children.Add(childDeck);
        }

        await deck.AddChildDeckWords(context);

        deck.OriginalTitle = metadata.OriginalTitle;
        deck.MediaType = deckType;

        if (deckType is MediaType.Manga or MediaType.Anime or MediaType.Movie or MediaType.Drama)
            deck.SentenceCount = 0;

        deck.RomajiTitle = metadata.RomajiTitle;
        deck.EnglishTitle = metadata.EnglishTitle;
        deck.Links = metadata.Links;
        deck.CoverName = metadata.Image ?? "nocover.jpg";
        deck.CreationDate = DateTimeOffset.UtcNow;
        deck.LastUpdate = DateTime.UtcNow;

        foreach (var link in deck.Links)
        {
            link.Deck = deck;
        }

        var coverImage = await File.ReadAllBytesAsync(metadata.Image ?? throw new Exception("No cover image found."));
        ;

        // Insert the deck into the database
        await JitenHelper.InsertDeck(context.DbOptions, deck, coverImage ?? [], false);
    }

    private async Task<Deck> ParseChild(Metadata metadata, Deck parentDeck, MediaType deckType, int deckOrder, bool storeRawText)
    {
        Deck deck = new();
        string filePath = metadata.FilePath;

        if (!string.IsNullOrEmpty(metadata.FilePath))
        {
            if (!File.Exists(metadata.FilePath))
            {
                throw new FileNotFoundException($"File {filePath} not found.");
            }

            string text = "";
            if (Path.GetExtension(filePath).ToLower() == ".epub")
            {
                var extractor = new EbookExtractor();
                text = await extractor.ExtractTextFromEbook(filePath);

                if (string.IsNullOrEmpty(text))
                {
                    throw new Exception("No text found in the ebook.");
                }
            }
            else
            {
                text = await File.ReadAllTextAsync(filePath);
            }

            deck = await Parser.Parser.ParseTextToDeck(context, text, storeRawText, true, deckType);
            deck.ParentDeck = parentDeck;
            deck.DeckOrder = deckOrder;
            deck.OriginalTitle = metadata.OriginalTitle;
            deck.MediaType = deckType;

            if (deckType is MediaType.Manga or MediaType.Anime or MediaType.Movie or MediaType.Drama)
                deck.SentenceCount = 0;
        }

        // Process children recursively
        int childDeckOrder = 0;
        foreach (var child in metadata.Children)
        {
            var childDeck = await ParseChild(child, deck, deckType, ++childDeckOrder, storeRawText);
            deck.Children.Add(childDeck);
        }

        return deck;
    }
}