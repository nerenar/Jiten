using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Services;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Core.Data.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/reader")]
[Authorize]
public class ReaderController(JitenDbContext context, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpPost("ping")]
    public IResult Ping()
    {
        return Results.Ok(new { success = true });
    }

    /// <summary>
    /// Parses the provided text and returns a sequence of parsed and unparsed segments as deck words.
    /// </summary>
    /// <param name="text">Text to parse. Max length 500 characters.</param>
    /// <returns>List of parsed and unparsed segments preserving original order.</returns>
    [HttpPost("parse")]
    [SwaggerOperation(Summary = "Parse text into words",
                      Description = "Parses the provided text and returns parsed words and any gaps as separate items, preserving order.")]
    // [ProducesResponseType(typeof(List<DeckWordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Parse(ReaderParseRequest request)
    {
        if (string.Join("", request.Text).Length > 17000)
            return Results.BadRequest("Text is too long");

        List<List<ReaderToken>> allTokens = new();
        List<ReaderWord> allWords = new();
        List<List<DeckWord>> parsedParagraphs = new();

        foreach (var paragraph in request.Text)
        {
            var parsedWords = await Parser.Parser.ParseText(context, paragraph);
            parsedParagraphs.Add(parsedWords);
        }

        var wordIds = parsedParagraphs.SelectMany(p => p).Select(w => w.WordId).ToList();
        var jmdictWords = await context.JMDictWords.Where(w => wordIds.Contains(w.WordId)).Include(w => w.Definitions).ToListAsync();
        

        for (var i = 0; i < parsedParagraphs.Count; i++)
        {
            List<DeckWord>? parsedWords = parsedParagraphs[i];
            List<ReaderToken> tokens = new();
            int currentPosition = 0;

            var knownStates = await currentUserService.GetKnownWordsState(parsedWords.Select(dw => (dw.WordId, dw.ReadingIndex)).ToList());
            
            foreach (var word in parsedWords)
            {
                int position = request.Text[i].IndexOf(word.OriginalText, currentPosition, StringComparison.Ordinal);
                if (position >= 0)
                {
                    tokens.Add(new ReaderToken
                               {
                                   WordId = word.WordId, ReadingIndex = word.ReadingIndex, Start = position,
                                   End = position + word.OriginalText.Length, Length = word.OriginalText.Length
                               });
                    var jmdictWord = jmdictWords.First(jw => jw.WordId == word.WordId);
                    knownStates.TryGetValue((word.WordId,word.ReadingIndex), out var knownState);
                    var readerWord = new ReaderWord()
                                     {
                                         WordId = word.WordId, ReadingIndex = word.ReadingIndex, Spelling = word.OriginalText, Reading =
                                             jmdictWord.ReadingsFurigana[word.ReadingIndex],
                                         PartsOfSpeech = jmdictWord.PartsOfSpeech.ToHumanReadablePartsOfSpeech(),
                                         MeaningsChunks = jmdictWord.Definitions.Where(d => d.EnglishMeanings.Count > 0).Select(d => d.EnglishMeanings).ToList(),
                                         MeaningsPartOfSpeech = jmdictWord.Definitions.SelectMany(d => d.PartsOfSpeech).ToList() ?? [""],
                                         FrequencyRank = 0, KnownState = knownState,
                                     };
                    allWords.Add(readerWord);

                    currentPosition = position + word.OriginalText.Length;
                }
            }

            allTokens.Add(tokens);
        }

        return Results.Ok(new { tokens = allTokens, vocabulary = allWords });
    }
}