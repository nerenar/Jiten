using System.Text;
using System.Text.RegularExpressions;
using AnkiNet;
using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Enums;
using Jiten.Api.Helpers;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WanaKanaShaapu;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/media-deck")]
[EnableRateLimiting("fixed")]
public class MediaDeckController(JitenDbContext context) : ControllerBase
{
    [HttpGet("get-media-decks-id")]
    [ResponseCache(Duration = 60 * 60 * 24)]
    public async Task<List<int>> GetMediaDecksId()
    {
        return await context.Decks.AsNoTracking().Where(d => d.ParentDeckId == null).Select(d => d.DeckId).ToListAsync();
    }

    [HttpGet("get-media-decks")]
    [ResponseCache(Duration = 300,
                   VaryByQueryKeys = ["offset", "mediaType", "wordId", "readingIndex", "titleFilter", "sortBy", "sortOrder"])]
    public async Task<PaginatedResponse<List<DeckDto>>> GetMediaDecks(int? offset = 0, MediaType? mediaType = null,
                                                                      int wordId = 0, int readingIndex = 0, string? titleFilter = "",
                                                                      string? sortBy = "",
                                                                      SortOrder sortOrder = SortOrder.Ascending)
    {
        int pageSize = 50;

        var query = context.Decks.AsNoTracking();

        if (!string.IsNullOrEmpty(titleFilter))
        {
            FormattableString sql = $"""
                                     SELECT *
                                     FROM jiten."Decks"
                                     WHERE "ParentDeckId" IS NULL AND
                                     ("OriginalTitle" &@~ {titleFilter} OR 
                                      "RomajiTitle" &@~ {titleFilter} OR REPLACE("RomajiTitle", ' ', '') &@~ {titleFilter} OR 
                                      "EnglishTitle" &@~ {titleFilter})
                                     ORDER BY pgroonga_score(tableoid, ctid) DESC
                                     """;


            query = context.Set<Deck>().FromSqlInterpolated(sql);
        }
        else
        {
            query = query.Where(d => d.ParentDeckId == null);
        }

        query = query.Include(d => d.Children);

        if (mediaType != null)
            query = query.Where(d => d.MediaType == mediaType);

        if (wordId != 0)
        {
            // Execute this query first and materialize the results
            var deckIds = await context.DeckWords
                                       .Where(dw => dw.WordId == wordId && dw.ReadingIndex == readingIndex)
                                       .Select(dw => dw.DeckId)
                                       .Distinct()
                                       .ToListAsync(); // Get the IDs in memory first

            // Then use the in-memory collection for filtering
            query = query.Where(d => deckIds.Contains(d.DeckId));
        }

        if (string.IsNullOrEmpty(sortBy))
            sortBy = "title";

        query = sortBy switch
        {
            "difficulty" => sortOrder == SortOrder.Ascending
                ? query.Where(d => d.Difficulty != -1).OrderBy(d => d.Difficulty)
                : query.Where(d => d.Difficulty != -1).OrderByDescending(d => d.Difficulty),
            "charCount" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.CharacterCount)
                : query.OrderByDescending(d => d.CharacterCount),
            "sentenceLength" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.CharacterCount / (d.SentenceCount + 1)).Where(d => d.SentenceCount != 0)
                : query.OrderByDescending(d => d.CharacterCount / (d.SentenceCount + 1)).Where(d => d.SentenceCount != 0),
            "dialoguePercentage" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.DialoguePercentage).Where(d => d.DialoguePercentage != 0 && d.DialoguePercentage != 100)
                : query.OrderByDescending(d => d.DialoguePercentage).Where(d => d.DialoguePercentage != 0 && d.DialoguePercentage != 100),
            "wordCount" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.WordCount)
                : query.OrderByDescending(d => d.WordCount),
            "uKanji" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.UniqueKanjiCount)
                : query.OrderByDescending(d => d.UniqueKanjiCount),
            "uWordCount" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.UniqueWordCount)
                : query.OrderByDescending(d => d.UniqueWordCount),
            "uKanjiOnce" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.UniqueKanjiUsedOnceCount)
                : query.OrderByDescending(d => d.UniqueKanjiUsedOnceCount),
            "filter" => query.OrderBy(_ => 1), // Dummy ordering to avoid efcore warning, pgroonga_score handles the actual sort
            "releaseDate" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.ReleaseDate)
                : query.OrderByDescending(d => d.ReleaseDate),
            _ => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.RomajiTitle)
                : query.OrderByDescending(d => d.RomajiTitle),
        };

        query = query.Include(d => d.Links);

        var totalCount = query.Count();

        List<DeckDto> dtos;

        if (wordId != 0)
        {
            var projectedQuery = query.Select(d => new
                                                   {
                                                       Deck = d, Occurrences = d.DeckWords
                                                                                .Where(dw => dw.WordId == wordId &&
                                                                                           dw.ReadingIndex == readingIndex)
                                                                                .Select(dw => (int?)dw.Occurrences)
                                                                                .FirstOrDefault() ?? 0
                                                   });

            if (sortBy == "occurrences")
            {
                projectedQuery = sortOrder == SortOrder.Ascending
                    ? projectedQuery.OrderBy(p => p.Occurrences)
                    : projectedQuery.OrderByDescending(p => p.Occurrences);
            }

            var paginatedResults = await projectedQuery
                                         .Skip(offset ?? 0)
                                         .Take(pageSize)
                                         .AsSplitQuery()
                                         .ToListAsync();

            dtos = paginatedResults
                   .Select(r => new DeckDto(r.Deck, r.Occurrences))
                   .ToList();
        }
        else
        {
            var paginatedDecks = await query
                                       .Skip(offset ?? 0)
                                       .Take(pageSize)
                                       .AsSplitQuery()
                                       .ToListAsync(); // Execute the query against the database

            dtos = new List<DeckDto>();
            foreach (var deck in paginatedDecks)
            {
                dtos.Add(new DeckDto(deck));
            }
        }

        return new PaginatedResponse<List<DeckDto>>(dtos, totalCount, pageSize, offset ?? 0);
    }


    [HttpGet("{id}/vocabulary")]
    [ResponseCache(Duration = 600, VaryByQueryKeys = ["id", "sortBy", "sortOrder", "offset"])]
    public PaginatedResponse<DeckVocabularyListDto?> GetVocabulary(int id, string? sortBy = "", SortOrder sortOrder = SortOrder.Ascending,
                                                                   int? offset = 0)
    {
        int pageSize = 100;

        var deck = context.Decks.AsNoTracking().FirstOrDefault(d => d.DeckId == id);

        if (deck == null)
            return new PaginatedResponse<DeckVocabularyListDto?>(null, 0, pageSize, offset ?? 0);

        var parentDeck = context.Decks.AsNoTracking().FirstOrDefault(d => d.DeckId == deck.ParentDeckId);
        var parentDeckDto = parentDeck != null ? new DeckDto(parentDeck) : null;

        var query = context.DeckWords.AsNoTracking().Where(dw => dw.DeckId == id);

        query = sortBy switch
        {
            "globalFreq" => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => context.JmDictWordFrequencies
                                            .Where(f => f.WordId == d.WordId)
                                            .Select(f => f.ReadingsFrequencyRank[d.ReadingIndex])
                                            .FirstOrDefault()).ThenBy(d => d.DeckWordId)
                : query.OrderByDescending(d => context.JmDictWordFrequencies
                                                      .Where(f => f.WordId == d.WordId)
                                                      .Select(f => f.ReadingsFrequencyRank[d.ReadingIndex])
                                                      .FirstOrDefault()).ThenBy(d => d.DeckWordId),
            "deckFreq" => sortOrder == SortOrder.Ascending
                ? query.OrderByDescending(d => d.Occurrences).ThenBy(d => d.DeckWordId)
                : query.OrderBy(d => d.Occurrences).ThenBy(d => d.DeckWordId),
            "chrono" or _ => sortOrder == SortOrder.Ascending
                ? query.OrderBy(d => d.DeckWordId)
                : query.OrderByDescending(d => d.DeckWordId),
        };

        int totalCount = query.Count(dw => dw.DeckId == id);

        var deckWordsList = query.Skip(offset ?? 0)
                                 .Take(pageSize)
                                 .ToList();

        var wordIds = deckWordsList.Select(dw => dw.WordId).ToList();

        var jmdictWords = context.JMDictWords.AsNoTracking()
                                 .Where(w => wordIds.Contains(w.WordId))
                                 .Include(w => w.Definitions)
                                 .ToList();

        var words = deckWordsList.Select(dw => new { dw, jmDictWord = jmdictWords.FirstOrDefault(w => w.WordId == dw.WordId) })
                                 .OrderBy(dw => wordIds.IndexOf(dw.dw.WordId))
                                 .ToList();

        var frequencies = context.JmDictWordFrequencies.AsNoTracking().Where(f => wordIds.Contains(f.WordId)).ToList();

        DeckVocabularyListDto dto = new() { ParentDeck = parentDeckDto, Deck = deck, Words = new() };

        foreach (var word in words)
        {
            if (word.jmDictWord == null)
            {
                continue;
            }

            var reading = word.jmDictWord.ReadingsFurigana[word.dw.ReadingIndex];

            List<ReadingDto> alternativeReadings = word.jmDictWord.ReadingsFurigana
                                                       .Select((r, i) => new ReadingDto
                                                                         {
                                                                             Text = r, ReadingIndex = i,
                                                                             ReadingType = word.jmDictWord.ReadingTypes[i], FrequencyRank =
                                                                                 frequencies.First(f => f.WordId == word.dw.WordId)
                                                                                            .ReadingsFrequencyRank[i],
                                                                             FrequencyPercentage =
                                                                                 frequencies.First(f => f.WordId == word.dw.WordId)
                                                                                            .ReadingsFrequencyPercentage[i]
                                                                         })
                                                       .ToList();

            // Remove current
            alternativeReadings.RemoveAt(word.dw.ReadingIndex);

            var frequency = frequencies.First(f => f.WordId == word.dw.WordId);

            var mainReading = new ReadingDto()
                              {
                                  Text = reading, ReadingIndex = word.dw.ReadingIndex,
                                  ReadingType = word.jmDictWord.ReadingTypes[word.dw.ReadingIndex],
                                  FrequencyRank = frequency.ReadingsFrequencyRank[word.dw.ReadingIndex],
                                  FrequencyPercentage = frequency.ReadingsFrequencyPercentage[word.dw.ReadingIndex]
                              };

            var wordDto = new WordDto
                          {
                              WordId = word.jmDictWord.WordId, MainReading = mainReading, AlternativeReadings = alternativeReadings,
                              PartsOfSpeech = word.jmDictWord.PartsOfSpeech.ToHumanReadablePartsOfSpeech(),
                              Definitions = word.jmDictWord.Definitions.ToDefinitionDtos(), Occurrences = word.dw.Occurrences,
                              PitchAccents = word.jmDictWord.PitchAccents
                          };

            dto.Words.Add(wordDto);
        }

        return new PaginatedResponse<DeckVocabularyListDto?>(dto, totalCount, pageSize, offset ?? 0);
    }

    [HttpGet("{id}/detail")]
    [ResponseCache(Duration = 600, VaryByQueryKeys = ["id", "offset"])]
    public PaginatedResponse<DeckDetailDto?> GetMediaDeckDetail(int id, int? offset = 0)
    {
        int pageSize = 25;

        var deck = context.Decks.AsNoTracking().Include(d => d.Children).Include(d => d.Links).FirstOrDefault(d => d.DeckId == id);

        if (deck == null)
            return new PaginatedResponse<DeckDetailDto?>(null, 0, pageSize, offset ?? 0);

        var parentDeck = context.Decks.AsNoTracking().FirstOrDefault(d => d.DeckId == deck.ParentDeckId);

        var subDecks = context.Decks.AsNoTracking()
                              .Where(d => d.ParentDeckId == id);
        int totalCount = subDecks.Count();

        subDecks = subDecks
                   .OrderBy(dw => dw.DeckOrder)
                   .Skip(offset ?? 0)
                   .Take(pageSize);

        var mainDeckDto = new DeckDto(deck);
        List<DeckDto> subDeckDtos = new();

        foreach (var subDeck in subDecks)
            subDeckDtos.Add(new DeckDto(subDeck));

        var parentDeckDto = parentDeck != null ? new DeckDto(parentDeck) : null;

        var dto = new DeckDetailDto { ParentDeck = parentDeckDto, MainDeck = mainDeckDto, SubDecks = subDeckDtos };

        return new PaginatedResponse<DeckDetailDto?>(dto, totalCount, pageSize, offset ?? 0);
    }

    [HttpPost("{id}/download")]
    [EnableRateLimiting("download")]
    public async Task<IResult> DownloadDeck(int id, [FromBody] DeckDownloadRequest request)
    {
        var deck = await context.Decks
                                .AsNoTracking()
                                .Include(d => d.Children)
                                .FirstOrDefaultAsync(d => d.DeckId == id);

        if (deck == null)
        {
            return Results.NotFound();
        }

        IQueryable<DeckWord> deckWordsQuery = context.DeckWords.AsNoTracking().Where(dw => dw.DeckId == id);

        if (request.ExcludeKnownWords && request.KnownWordIds != null)
            deckWordsQuery = deckWordsQuery.Where(dw => !request.KnownWordIds.Contains(dw.WordId));

        switch (request.DownloadType)
        {
            case DeckDownloadType.Full:
                break;

            case DeckDownloadType.TopGlobalFrequency:
                // Get the words between frequency rank minFrequency and maxFrequency
                deckWordsQuery = deckWordsQuery.Where(dw => context.JmDictWordFrequencies
                                                                   .Any(f => f.WordId == dw.WordId &&
                                                                             f.ReadingsFrequencyRank[dw.ReadingIndex] >=
                                                                             request.MinFrequency &&
                                                                             f.ReadingsFrequencyRank[dw.ReadingIndex] <=
                                                                             request.MaxFrequency));
                break;

            case DeckDownloadType.TopDeckFrequency:
                deckWordsQuery = deckWordsQuery
                                 .OrderByDescending(dw => dw.Occurrences)
                                 .Skip(request.MinFrequency)
                                 .Take(request.MaxFrequency - request.MinFrequency);
                break;

            case DeckDownloadType.TopChronological:
                deckWordsQuery = deckWordsQuery
                                 .OrderBy(dw => dw.DeckWordId)
                                 .Skip(request.MinFrequency)
                                 .Take(request.MaxFrequency - request.MinFrequency);
                break;
            default:
                return Results.BadRequest();
        }

        switch (request.Order)
        {
            case DeckOrder.Chronological:
                deckWordsQuery = deckWordsQuery.OrderBy(dw => dw.DeckWordId);
                break;

            case DeckOrder.GlobalFrequency:
                deckWordsQuery = deckWordsQuery.OrderBy(dw => context.JmDictWordFrequencies
                                                                     .Where(f => f.WordId == dw.WordId)
                                                                     .Select(f => f.ReadingsFrequencyRank[dw.ReadingIndex])
                                                                     .FirstOrDefault()
                                                       );
                break;

            case DeckOrder.DeckFrequency:
                deckWordsQuery = deckWordsQuery.OrderByDescending(dw => dw.Occurrences);
                break;
            default:
                return Results.BadRequest();
        }

        var wordIds = await deckWordsQuery.Select(dw => dw.WordId).ToListAsync();
        var deckWords = await deckWordsQuery.Select(dw => new { dw.WordId, dw.ReadingIndex, dw.Occurrences }).ToListAsync();

        var jmdictWords = await context.JMDictWords.AsNoTracking()
                                       .Include(w => w.Definitions)
                                       .Where(w => wordIds.Contains(w.WordId))
                                       .ToDictionaryAsync(w => w.WordId);
        var frequencies = context.JmDictWordFrequencies.Where(f => wordIds.Contains(f.WordId))
                                 .ToDictionary(f => f.WordId, f => f);


        var deckIds = new List<int> { id };

        // If this deck has children, use sentences from the children instead
        if (deck?.Children.Any() == true)
        {
            deckIds = deck.Children.Select(c => c.DeckId).ToList();
        }

        var exampleSentences = await context.ExampleSentences
                                            .AsNoTracking()
                                            .Where(es => deckIds.Contains(es.DeckId))
                                            .Include(es => es.Words.Where(w => wordIds.Contains(w.WordId)))
                                            .ToListAsync();

        var wordToSentencesMap = new Dictionary<(int WordId, byte ReadingIndex), List<(string Text, byte Position, byte Length)>>();

        foreach (var sentence in exampleSentences)
        {
            foreach (var word in sentence.Words.Where(w => wordIds.Contains(w.WordId)))
            {
                var key = (word.WordId, word.ReadingIndex);
                if (!wordToSentencesMap.ContainsKey(key))
                    wordToSentencesMap[key] = new List<(string, byte, byte)>();

                // If this word already has a sentence and we're only collecting one per word, skip
                if (wordToSentencesMap[key].Count > 0)
                    continue;

                wordToSentencesMap[key].Add((sentence.Text, word.Position, word.Length));
            }
        }

        switch (request.Format)
        {
            case DeckFormat.Anki:

                // Lapis template from https://github.com/donkuri/lapis/tree/main
                var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "lapis.apkg");
                var template = await AnkiFileReader.ReadFromFileAsync(templatePath);
                var noteTypeTemplate = template.NoteTypes.First();

                var collection = new AnkiCollection();
                var noteTypeId = collection.CreateNoteType(noteTypeTemplate);
                var deckId = collection.CreateDeck(deck.OriginalTitle);

                foreach (var word in deckWords)
                {
                    string expression = jmdictWords[word.WordId].Readings[word.ReadingIndex];

                    if (request.ExcludeKana && WanaKana.IsKana(expression))
                        continue;


                    // Need a space before the kanjis for lapis
                    string kanjiPatternPart = @"\p{IsCJKUnifiedIdeographs}";
                    string lookaheadPattern = $@"(?=(?:{kanjiPatternPart})*\[.*?\])";
                    string precedingKanjiLookbehind = $@"\p{{IsCJKUnifiedIdeographs}}{lookaheadPattern}";
                    string pattern = $"(?<!\\])(?<!{precedingKanjiLookbehind})({kanjiPatternPart}){lookaheadPattern}";
                    string expressionFurigana = Regex.Replace(jmdictWords[word.WordId].ReadingsFurigana[word.ReadingIndex], pattern, " $1");
                    // Very unoptimized, might have to rework
                    string expressionReading = string.Join("", jmdictWords[word.WordId].ReadingsFurigana[word.ReadingIndex]
                                                                                       .Where(c => WanaKana.IsKana(c.ToString()))
                                                                                       .Select(c => c.ToString()));
                    string expressionAudio = "";
                    string selectionText = "";
                    string mainDefinition = "<ul>" +
                                            string.Join("", jmdictWords[word.WordId].Definitions
                                                                                    .SelectMany(d => d.EnglishMeanings)
                                                                                    .Select(meaning => $"<li>{meaning}</li>")) +
                                            "</ul>";
                    string definitionPicture = "";
                    string sentence = "";

                    if (!request.ExcludeExampleSentences &&
                        wordToSentencesMap.TryGetValue((word.WordId, word.ReadingIndex), out var sentences) && sentences.Count > 0)
                    {
                        var exampleSentence = sentences.First();
                        int position = exampleSentence.Position;
                        int length = exampleSentence.Length;

                        string originalText = exampleSentence.Text;
                        if (position >= 0 && position + length <= originalText.Length)
                        {
                            sentence = originalText.Substring(0, position) +
                                       "<span style=\"font-weight: 700; color: rgb(168, 85, 247);\">" +
                                       originalText.Substring(position, length) + "</span>" +
                                       originalText.Substring(position + length);
                        }
                    }

                    string sentenceFurigana = "";
                    string sentenceAudio = "";
                    string picture = "";
                    // This is where to add extra definitions, such as J-J
                    string glossary = "";
                    string hint = "";
                    string isWordAndSentenceCard = "";
                    string isClickCard = "";
                    string isSentenceCard = "";
                    string pitchPosition = "";
                    string pitchCategories = "";
                    string frequency =
                        $"<ul><li>Jiten: {word.Occurrences} occurrences ; #{frequencies[word.WordId].ReadingsFrequencyRank[word.ReadingIndex]} global rank</li></ul>";
                    string freqSort = $"{frequencies[word.WordId].ReadingsFrequencyRank[word.ReadingIndex]}";
                    string occurrences = $"{word.Occurrences}";
                    string miscInfo = $"From {deck.OriginalTitle} - generated by Jiten.moe";

                    if (jmdictWords[word.WordId].PitchAccents != null)
                        pitchPosition = string.Join(",", jmdictWords[word.WordId].PitchAccents!.Select(p => p.ToString()));

                    collection.CreateNote(deckId, noteTypeId,
                                          expression, expressionFurigana,
                                          expressionReading, expressionAudio, selectionText, mainDefinition, definitionPicture,
                                          sentence, sentenceFurigana,
                                          sentenceAudio, picture, glossary, hint,
                                          isWordAndSentenceCard, isClickCard, isSentenceCard,
                                          pitchPosition, pitchCategories,
                                          frequency, freqSort, occurrences,
                                          miscInfo
                                         );
                }


                var stream = new MemoryStream();

                await AnkiFileWriter.WriteToStreamAsync(stream, collection);
                var bytes = stream.ToArray();

                return Results.File(bytes, "application/x-binary", $"{deck.OriginalTitle}.apkg");

            case DeckFormat.Csv:
                StringBuilder sb = new StringBuilder();

                sb.AppendLine($"\"Reading\",\"ReadingFurigana\",\"ReadingKana\",\"Occurences\",\"ReadingFrequency\",\"PitchPositions\",\"Definitions\"");

                foreach (var word in deckWords)
                {
                    string reading = jmdictWords[word.WordId].Readings[word.ReadingIndex];

                    if (request.ExcludeKana && WanaKana.IsKana(reading))
                        continue;


                    string readingFurigana = jmdictWords[word.WordId].ReadingsFurigana[word.ReadingIndex];
                    string pitchPositions = "";

                    if (jmdictWords[word.WordId].PitchAccents != null)
                        pitchPositions = string.Join(",", jmdictWords[word.WordId].PitchAccents!.Select(p => p.ToString()));

                    // Very unoptimized, might have to rework
                    string readingKana = string.Join("", jmdictWords[word.WordId].ReadingsFurigana[word.ReadingIndex]
                                                                                 .Where(c => WanaKana.IsKana(c.ToString()))
                                                                                 .Select(c => c.ToString()));
                    string definitions = string.Join(",", jmdictWords[word.WordId].Definitions
                                                                                  .SelectMany(d => d.EnglishMeanings)
                                                                                  .Select(m => m.Replace("\"", "\"\"")));
                    var occurrences = word.Occurrences;
                    var readingFrequency = frequencies[word.WordId].ReadingsFrequencyRank[word.ReadingIndex];

                    sb.AppendLine($"\"{reading}\",\"{readingFurigana}\",\"{readingKana}\",\"{occurrences}\",\"{readingFrequency}\",\"{pitchPositions}\",\"{definitions}\"");
                }

                return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"{deck.OriginalTitle}.csv");
            case DeckFormat.Txt:
                StringBuilder txtSb = new StringBuilder();
                foreach (var word in deckWords)
                {
                    string reading = jmdictWords[word.WordId].Readings[word.ReadingIndex];
                    if (request.ExcludeKana && WanaKana.IsKana(reading))
                        continue;

                    txtSb.AppendLine(reading);
                }

                return Results.File(Encoding.UTF8.GetBytes(txtSb.ToString()), "text/plain", $"{deck.OriginalTitle}.txt");

            case DeckFormat.TxtRepeated:
                StringBuilder txtRepeatedSb = new StringBuilder();
                foreach (var word in deckWords)
                {
                    string reading = jmdictWords[word.WordId].Readings[word.ReadingIndex];
                    if (request.ExcludeKana && WanaKana.IsKana(reading))
                        continue;

                    for (int i = 0; i < word.Occurrences; i++)
                        txtRepeatedSb.AppendLine(reading);
                }

                return Results.File(Encoding.UTF8.GetBytes(txtRepeatedSb.ToString()), "text/plain", $"{deck.OriginalTitle}.txt");

            default:
                return Results.BadRequest();
        }
    }

    [HttpPost("{id}/coverage")]
    public async Task<ActionResult<DeckCoverageResponse>> GetCoverage(int id, [FromBody] List<int>? wordIds)
    {
        if (wordIds == null || !wordIds.Any())
        {
            return BadRequest("Please provide a valid list of words");
        }

        var deck = await context.Decks.AsNoTracking()
                                .FirstOrDefaultAsync(d => d.DeckId == id);

        if (deck == null)
        {
            return NotFound($"Deck with ID {id} not found");
        }

        var deckWords = await context.DeckWords.AsNoTracking()
                                     .Where(dw => dw.DeckId == id)
                                     .ToListAsync();

        var knownUniqueWords = deckWords
                               .Where(dw => wordIds.Contains(dw.WordId)).ToList();

        int knownWordsOccurrences = knownUniqueWords
            .Sum(dw => dw.Occurrences);

        knownUniqueWords = knownUniqueWords.DistinctBy(w => w.WordId).ToList();

        double knownWordPercentage = Math.Round((double)knownWordsOccurrences / deck.WordCount * 100, 2);

        double knownUniqueWordPercentage = Math.Round((double)knownUniqueWords.Count() / deck.UniqueWordCount * 100, 2);

        var response = new DeckCoverageResponse
                       {
                           DeckId = id, TotalWordCount = deck.WordCount, KnownUniqueWordCount = knownUniqueWords.Count(),
                           UniqueWordCount = deck.UniqueWordCount, KnownWordsOccurrences = knownWordsOccurrences,
                           KnownWordPercentage = knownWordPercentage, KnownUniqueWordPercentage = knownUniqueWordPercentage
                       };

        return Ok(response);
    }

    [HttpGet("decks-count")]
    [ResponseCache(Duration = 600)]
    public IResult GetDecksCountByMediaType()
    {
        Dictionary<int, int> decksCount = context.Decks.AsNoTracking()
                                                 .Where(d => d.ParentDeckId == null)
                                                 .GroupBy(d => d.MediaType)
                                                 .ToDictionary(g => (int)g.Key, g => g.Count());

        return Results.Ok(decksCount);
    }

    [HttpGet("{id}/vocabulary-count-frequency")]
    public IResult GetVocabularyCountByMediaFrequencyRange(int id, int minFrequency, int maxFrequency)
    {
        var query = context.DeckWords.AsNoTracking()
                           .Where(dw => dw.DeckId == id &&
                                        context.JmDictWordFrequencies
                                               .Any(f => f.WordId == dw.WordId &&
                                                         f.ReadingsFrequencyRank[dw.ReadingIndex] >= minFrequency &&
                                                         f.ReadingsFrequencyRank[dw.ReadingIndex] <= maxFrequency));

        var count = query.Count();

        return Results.Ok(count);
    }

    /// <summary>
    /// Gets decks from sliding 30-day windows based on offset for display in the update log
    /// </summary>
    /// <param name="offset">Window offset: 0 = last 30 days, 1 = days 30-60 ago, 2 = days 60-90 ago, etc.</param>
    /// <returns>Deck information for the specified 30-day window</returns>
    [HttpGet("media-update-log")]
    [ResponseCache(Duration = 60 * 10, VaryByQueryKeys = ["offset"])]
    public async Task<PaginatedResponse<List<DeckDto>>> GetDecksForUpdateLog(int? offset = 0)
    {
        int offsetValue = offset ?? 0;
        var endDate = DateTimeOffset.UtcNow.AddDays(-30 * offsetValue);
        var startDate = endDate.AddDays(-30);

        var query = context.Decks.AsNoTracking()
                           .Where(d => d.ParentDeckId == null &&
                                       d.CreationDate >= startDate &&
                                       d.CreationDate < endDate)
                           .OrderByDescending(d => d.CreationDate);

        int totalCount = await query.CountAsync();

        var decks = await query.ToListAsync();

        var dtos = decks.Select(d => new DeckDto
                                     {
                                         DeckId = d.DeckId, CreationDate = d.CreationDate, OriginalTitle = d.OriginalTitle,
                                         RomajiTitle = d.RomajiTitle!, EnglishTitle = d.EnglishTitle!, MediaType = d.MediaType
                                     }).ToList();

        return new PaginatedResponse<List<DeckDto>>(dtos, totalCount, decks.Count, offsetValue);
    }

    [HttpGet("by-link-id/{linkType}/{id}")]
    [ResponseCache(Duration = 600, VaryByQueryKeys = ["id"])]
    public async Task<List<int>> GetMediaDeckIdsByLinkId(LinkType linkType, string id)
    {
        var links = await context.Decks
                                 .Include(d => d.Links)
                                 .Where(d => d.Links.Any(l => l.LinkType == linkType))
                                 .SelectMany(d => d.Links.Where(l => l.LinkType == linkType)
                                                   .Select(l => new { DeckId = d.DeckId, Url = l.Url }))
                                 .ToListAsync();

        var result = new List<int>();

        foreach (var link in links)
        {
            // Remove trailing slash if present
            var url = link.Url.TrimEnd('/');

            // Get the last part of the URL (after the last slash)
            var lastSlashIndex = url.LastIndexOf('/');
            if (lastSlashIndex == -1)
                continue;

            var urlId = url.Substring(lastSlashIndex + 1);

            // If the extracted ID matches the provided ID, add the deck ID to the result
            if (urlId.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(link.DeckId);
            }
        }

        return result;
    }
}