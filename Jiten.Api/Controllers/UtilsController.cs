using System.Text;
using Jiten.Core;
using Jiten.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using WanaKanaShaapu;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/utils")]
public class UtilsController(IDbContextFactory<JitenDbContext> contextFactory) : ControllerBase
{
    [HttpPost("romanize")]
    [AllowAnonymous]
    [EnableRateLimiting("fixed")]
    public async Task<IResult> Romanize([FromBody] RomanizeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required" });

        if (request.Title.Length > 100)
            return Results.BadRequest(new { error = "Title must be 100 characters or fewer" });

        var parsedWords = await Parser.Parser.ParseText(contextFactory, request.Title);

        var parts = new List<string>();
        foreach (var word in parsedWords)
        {
            if (!WanaKana.IsJapanese(word.OriginalText))
            {
                parts.Add(word.OriginalText);
                continue;
            }

            var reading = !string.IsNullOrEmpty(word.SudachiReading) ? word.SudachiReading : word.OriginalText;
            var romaji = WanaKana.ToRomaji(reading);
            if (string.IsNullOrEmpty(romaji)) continue;

            bool isParticle = word.PartsOfSpeech.Contains(PartOfSpeech.Particle);
            if (isParticle)
            {
                if (romaji == "wo") romaji = "o";
                else if (romaji == "ha") romaji = "wa";
                else if (romaji == "he") romaji = "e";
            }

            parts.Add(isParticle ? romaji : char.ToUpperInvariant(romaji[0]) + romaji[1..]);
        }

        return Results.Ok(new { romaji = ToHalfWidth(string.Join(" ", parts)) });
    }

    private static string ToHalfWidth(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c >= '\uFF01' && c <= '\uFF5E')
                sb.Append((char)(c - 0xFEE0));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}

public record RomanizeRequest(string Title);
