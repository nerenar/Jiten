using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Helpers;
using Jiten.Api.Integrations;
using Jiten.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/vocabulary")]
[EnableRateLimiting("fixed")]
public class VocabularyController(JitenDbContext context) : ControllerBase
{
    [HttpGet("{wordId}/{readingIndex}")]
    [ResponseCache(Duration = 3600)]
    public async Task<IResult> GetWord(int wordId, int readingIndex)
    {
        var word = await context.JMDictWords.AsNoTracking()
                                .Include(w => w.Definitions)
                                .FirstOrDefaultAsync(w => w.WordId == wordId);

        if (word == null)
            return Results.NotFound();

        var frequency = context.JmDictWordFrequencies.AsNoTracking().First(f => f.WordId == word.WordId);

        var usedInMediaByType = await context.DeckWords.AsNoTracking()
                                             .Where(dw => dw.WordId == wordId && dw.ReadingIndex == readingIndex)
                                             .Join(
                                                   context.Decks.AsNoTracking()
                                                          .Where(d => d.ParentDeckId == null)
                                                          .Select(d => new { d.DeckId, d.MediaType }),
                                                   dw => dw.DeckId,
                                                   d => d.DeckId,
                                                   (dw, d) => d.MediaType
                                                  )
                                             .GroupBy(mediaType => mediaType)
                                             .Select(g => new { MediaType = g.Key, Count = g.Count() })
                                             .ToDictionaryAsync(x => (int)x.MediaType, x => x.Count);

        var mainReading = new ReadingDto()
                          {
                              Text = word.ReadingsFurigana[readingIndex], ReadingIndex = readingIndex,
                              ReadingType = word.ReadingTypes[readingIndex], FrequencyRank = frequency.ReadingsFrequencyRank[readingIndex],
                              FrequencyPercentage = frequency.ReadingsFrequencyPercentage[readingIndex].ZeroIfNaN(),
                              UsedInMediaAmount = frequency.ReadingsUsedInMediaAmount[readingIndex],
                              UsedInMediaAmountByType = usedInMediaByType
                          };

        List<ReadingDto> alternativeReadings = word.Readings
                                                   .Select((r, i) => new ReadingDto
                                                                     {
                                                                         Text = r, ReadingIndex = i, ReadingType = word.ReadingTypes[i],
                                                                         FrequencyRank =
                                                                             frequency.ReadingsFrequencyRank[i],
                                                                         FrequencyPercentage =
                                                                             frequency.ReadingsFrequencyPercentage[i].ZeroIfNaN(),
                                                                         UsedInMediaAmount = frequency.ReadingsUsedInMediaAmount[i]
                                                                     })
                                                   .ToList();

        return Results.Ok(new WordDto
                          {
                              WordId = word.WordId, MainReading = mainReading, AlternativeReadings = alternativeReadings,
                              Definitions = word.Definitions.ToDefinitionDtos(), PartsOfSpeech = word.PartsOfSpeech,
                              PitchAccents = word.PitchAccents
                          });
    }

    [HttpGet("parse")]
    public async Task<IResult> Parse(string text)
    {
        if (text.Length > 500)
            return Results.BadRequest("Text is too long");

        var parsedWords = await Parser.Parser.ParseText(context, text);

        // We want both parsed words and unparsed ones
        var allWords = new List<DeckWordDto>();

        var wordsWithPositions = new List<(DeckWordDto Word, int Position)>();
        int currentPosition = 0;

        foreach (var word in parsedWords)
        {
            int position = text.IndexOf(word.OriginalText, currentPosition, StringComparison.Ordinal);
            if (position >= 0)
            {
                wordsWithPositions.Add((new DeckWordDto(word), position));
                currentPosition = position + word.OriginalText.Length;
            }
        }

        currentPosition = 0;
        foreach (var (word, position) in wordsWithPositions)
        {
            // If there's a gap before this word, add it as an unparsed word
            if (position > currentPosition)
            {
                string gap = text.Substring(currentPosition, position - currentPosition);
                allWords.Add(new DeckWordDto(gap));
            }

            allWords.Add(word);

            currentPosition = position + word.OriginalText.Length;
        }

        if (currentPosition < text.Length)
        {
            string gap = text.Substring(currentPosition);
            allWords.Add(new DeckWordDto(gap));
        }

        return Results.Ok(allWords);
    }


    [HttpGet("vocabulary-list-frequency/{minFrequency}/{maxFrequency}")]
    public IResult GetVocabularyByMediaFrequencyRange(int minFrequency, int maxFrequency)
    {
        var query = context.JmDictWordFrequencies.Where(f => f.FrequencyRank >= minFrequency && f.FrequencyRank <= maxFrequency);

        return Results.Ok(query.Select(f => f.WordId).ToList());
    }

    [HttpPost("vocabulary-from-anki-txt")]
    public async Task<IResult> ParseAnkiTxt(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest("File is empty or not provided");

        using var reader = new StreamReader(file.OpenReadStream());
        var lineCount = 0;
        var validWords = new List<string>();

        while (await reader.ReadLineAsync() is { } line)
        {
            lineCount++;

            if (lineCount > 50000)
                return Results.BadRequest("File has more than 50,000 lines");

            // Skip comments
            if (line.StartsWith("#"))
                continue;

            // Find the first word that ends with a tab
            var tabIndex = line.IndexOf('\t');

            if (tabIndex <= 0)
                tabIndex = line.IndexOf(',');

            if (tabIndex <= 0) continue;

            var word = line.Substring(0, tabIndex);

            // Skip words longer than 25 characters
            if (word.Length <= 25)
            {
                validWords.Add(word);
            }
        }

        var combinedText = string.Join(Environment.NewLine, validWords);
        var parsedWords = await Parser.Parser.ParseText(context, combinedText);
        var wordIds = parsedWords.Select(w => w.WordId).ToList();

        return Results.Ok(wordIds);
    }

    [HttpPost("{wordId}/{readingIndex}/random-example-sentences")]
    public async Task<List<ExampleSentenceDto>> GetRandomExampleSentences(int wordId, int readingIndex, [FromBody] List<int> alreadyLoaded)
    {
        return await context.ExampleSentenceWords
                            .AsNoTracking()
                            .Where(w => w.WordId == wordId && w.ReadingIndex == readingIndex)
                            .OrderBy(_ => EF.Functions.Random())
                            .Take(3)
                            .Join(
                                  context.ExampleSentences.AsNoTracking()
                                         .Where(s => !alreadyLoaded.Contains(s.DeckId)),
                                  w => w.ExampleSentenceId,
                                  s => s.SentenceId,
                                  (word, sentence) => new { Word = word, Sentence = sentence }
                                 )
                            .Join(
                                  context.Decks.AsNoTracking(),
                                  joined => joined.Sentence.DeckId,
                                  d => d.DeckId,
                                  (joined, deck) => new { joined, deck }
                                 )
                            .GroupJoin(
                                       context.Decks.AsNoTracking(),
                                       j => j.deck.ParentDeckId,
                                       pd => pd.DeckId,
                                       (j, parentDecks) => new ExampleSentenceDto
                                                           {
                                                               Text = j.joined.Sentence.Text, WordPosition = j.joined.Word.Position,
                                                               WordLength = j.joined.Word.Length, SourceDeck = j.deck,
                                                               SourceDeckParent = parentDecks.FirstOrDefault()
                                                           }
                                      )
                            .ToListAsync();
    }

    [HttpPost("import-from-jpdb")]
    public async Task<IResult> ImportFromJpdb([FromBody] JpdbImportRequest request)
    {
        if (string.IsNullOrEmpty(request.ApiKey))
            return Results.BadRequest("API key is required");

        try
        {
            using var client = new JpdbApiClient(request.ApiKey);
            var vocabularyIds = await client.GetFilteredVocabularyIds(
                                                                      request.BlacklistedAsKnown,
                                                                      request.DueAsKnown,
                                                                      request.SuspendedAsKnown);

            return Results.Ok(vocabularyIds);
        }
        catch (Exception ex)
        {
            return Results.BadRequest($"Error importing from JPDB: {ex.Message}");
        }
    }
}