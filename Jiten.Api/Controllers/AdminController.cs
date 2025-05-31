using Hangfire;
using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Jobs;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize("RequiresAdmin")]
public class AdminController(IConfiguration config, HttpClient httpClient, IBackgroundJobClient backgroundJobs, JitenDbContext dbContext)
    : ControllerBase
{
    [HttpGet("search-media")]
    public async Task<IResult> SearchMedia(string provider, string query, string? author)
    {
        return Results.Ok(provider switch
        {
            "AnilistManga" => await MetadataProviderHelper.AnilistMangaApi(query),
            "AnilistNovel" => await MetadataProviderHelper.AnilistNovelApi(query),
            "GoogleBooks" =>
                await MetadataProviderHelper.GoogleBooksApi(query + (!string.IsNullOrEmpty(author) ? $"+inauthor:{author}" : "")),
            "Vndb" => await MetadataProviderHelper.VndbApi(query),
            _ => new List<Metadata>()
        });
    }

    [HttpPost("add-deck")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(254857600)]
    public async Task<IActionResult> AddMediaDeck([FromForm] AddMediaRequest model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            string path = Path.Join(config["StaticFilesPath"], "tmp", Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            string? coverImagePathOrUrl = Path.Join(path, "cover.jpg");
            Metadata metadata = new()
                                {
                                    OriginalTitle = model.OriginalTitle.Trim(), RomajiTitle = model.RomajiTitle?.Trim(),
                                    EnglishTitle = model.EnglishTitle?.Trim(), Image = coverImagePathOrUrl, Links = new List<Link>()
                                };

            // Parse links from form data
            for (int i = 0; i < Request.Form.Keys.Count; i++)
            {
                string urlKey = $"links[{i}].url";
                string linkTypeKey = $"links[{i}].linkType";

                if (Request.Form.TryGetValue(urlKey, out var urlValue) &&
                    Request.Form.TryGetValue(linkTypeKey, out var linkTypeValue) &&
                    !string.IsNullOrEmpty(urlValue) &&
                    !string.IsNullOrEmpty(linkTypeValue) &&
                    Enum.TryParse<Core.Data.LinkType>(linkTypeValue, out var linkType))
                {
                    metadata.Links.Add(new Core.Data.Link { Url = urlValue.ToString(), LinkType = linkType });
                }
            }

            if (model.CoverImage is { Length: > 0 })
            {
                await using var stream = new FileStream(coverImagePathOrUrl, FileMode.Create);
                await model.CoverImage.CopyToAsync(stream);
            }
            else
            {
                // If there's no cover image uploaded, then it should be an URL instead
                if (Request.Form.TryGetValue("coverImage", out var coverImageUrlValue) && !string.IsNullOrEmpty(coverImageUrlValue))
                {
                    var imageUrl = coverImageUrlValue.ToString();
                    try
                    {
                        var response = await httpClient.GetAsync(imageUrl);
                        response.EnsureSuccessStatusCode();

                        await using var imageStream = await response.Content.ReadAsStreamAsync();
                        await using var fileStream = new FileStream(coverImagePathOrUrl, FileMode.Create);
                        await imageStream.CopyToAsync(fileStream);
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new ArgumentException($"Unable to download cover image from URL: {ex.Message}", ex);
                    }
                }
                else
                {
                    return BadRequest("No cover image or URL provided.");
                }
            }

            if (model.File is { Length: > 0 } && (model.Subdecks == null || !model.Subdecks.Any(sd => sd.File is { Length: > 0 })))
            {
                var mainFilePath = Path.Join(path, $"{Guid.NewGuid()}{Path.GetExtension(model.File.FileName)}");
                await using var stream = new FileStream(mainFilePath, FileMode.Create);
                await model.File.CopyToAsync(stream);

                metadata.FilePath = mainFilePath;
            }
            else if (model.Subdecks != null && model.Subdecks.Any(sd => sd.File is { Length: > 0 }))
            {
                metadata.Children = new List<Metadata>();

                foreach (var subdeck in model.Subdecks)
                {
                    if (subdeck.File is { Length: > 0 })
                    {
                        var subdeckFilePath = Path.Join(path, $"{Guid.NewGuid()}{Path.GetExtension(subdeck.File.FileName)}");

                        await using var stream = new FileStream(subdeckFilePath, FileMode.Create);
                        await subdeck.File.CopyToAsync(stream);

                        var subdeckMetadata = new Metadata { OriginalTitle = subdeck.OriginalTitle, FilePath = subdeckFilePath };

                        metadata.Children.Add(subdeckMetadata);
                    }
                }
            }
            else
            {
                // Return error if no files provided
                return BadRequest("No media files provided. Please upload at least one file.");
            }

            backgroundJobs.Enqueue<ParseJob>(job => job.Parse(metadata, model.MediaType, bool.Parse(config["StoreRawText"] ?? "false")));

            return Ok(new
                      {
                          Message = "Media added successfully.", Title = model.OriginalTitle, Path = path,
                          SubdeckCount = metadata.Children?.Count ?? 0
                      });
        }
        catch (ArgumentException argEx)
        {
            return BadRequest(new { Message = $"Invalid input: {argEx.Message}" });
        }
        catch (IOException ioEx)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An error occurred while saving files." });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                              new { Message = "An unexpected error occurred while processing your request." });
        }
    }

    [HttpGet("deck/{id}")]
    public async Task<IActionResult> GetDeck(int id)
    {
        var deck = dbContext.Decks.AsNoTracking()
                            .Include(d => d.Children)
                            .Include(d => d.Links)
                            .FirstOrDefault(d => d.DeckId == id);

        if (deck == null)
            return NotFound(new { Message = $"No deck found with ID {id}." });

        var subDecks = dbContext.Decks.AsNoTracking().Where(d => d.ParentDeckId == id);

        subDecks = subDecks
            .OrderBy(dw => dw.DeckOrder);

        var mainDeckDto = new DeckDto(deck);
        List<DeckDto> subDeckDtos = new();

        foreach (var subDeck in subDecks)
            subDeckDtos.Add(new DeckDto(subDeck));

        var dto = new DeckDetailDto { MainDeck = mainDeckDto, SubDecks = subDeckDtos };

        return Ok(dto);
    }

    [HttpPost("update-deck")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(254857600)]
    public async Task<IActionResult> UpdateMediaDeck([FromForm] UpdateMediaRequest model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var deck = await dbContext.Decks
                                  .Include(d => d.Links)
                                  .Include(d => d.RawText)
                                  .Include(d => d.Children)
                                  .ThenInclude(deck => deck.RawText)
                                  .FirstOrDefaultAsync(d => d.DeckId == model.DeckId);

        if (deck == null)
            return NotFound(new { Message = $"No deck found with ID {model.DeckId}." });

        // Update basic properties
        deck.MediaType = model.MediaType;
        deck.OriginalTitle = model.OriginalTitle.Trim();
        deck.RomajiTitle = model.RomajiTitle?.Trim();
        deck.EnglishTitle = model.EnglishTitle?.Trim();

        // Update cover image if provided
        if (model.CoverImage is { Length: > 0 })
        {
            using var memoryStream = new MemoryStream();
            await model.CoverImage.CopyToAsync(memoryStream);
            var cover = memoryStream.ToArray();

            var coverUrl = await BunnyCdnHelper.UploadFile(cover, $"{deck.DeckId}/cover.jpg");
            deck.CoverName = coverUrl;
        }

        string path = Path.Join(config["StaticFilesPath"], "tmp", Guid.NewGuid().ToString());
        Directory.CreateDirectory(path);

        // Update text if provided
        if (model.File is { Length: > 0 })
            deck.RawText!.RawText = await GetTextFromFile(model.File);


        // Update links
        if (model.Links.Any())
        {
            var existingLinkIds = deck.Links.Select(l => l.LinkId).ToHashSet();
            var newLinkIds = model.Links.Where(l => l.LinkId > 0).Select(l => l.LinkId).ToHashSet();

            // Remove links that are no longer present
            var linksToRemove = deck.Links.Where(l => !newLinkIds.Contains(l.LinkId));
            dbContext.RemoveRange(linksToRemove);

            // Update existing links and add new ones
            foreach (var link in model.Links)
            {
                if (link.LinkId > 0 && existingLinkIds.Contains(link.LinkId))
                {
                    var existingLink = deck.Links.First(l => l.LinkId == link.LinkId);
                    existingLink.Url = link.Url;
                    existingLink.LinkType = link.LinkType;
                }
                else
                {
                    deck.Links.Add(link);
                }
            }
        }

        // Update subdecks if provided
        if (model.Subdecks != null && model.Subdecks.Count != 0)
        {
            var existingSubdeckIds = deck.Children.Select(d => d.DeckId).ToHashSet();
            var newSubdeckIds = model.Subdecks.Where(d => d.DeckId > 0).Select(d => d.DeckId).ToHashSet();

            // Remove subdecks that are no longer present
            var subdecksToRemove = deck.Children.Where(d => !newSubdeckIds.Contains(d.DeckId));
            dbContext.RemoveRange(subdecksToRemove);

            // Update existing subdecks and add new ones 
            foreach (var subdeck in model.Subdecks)
            {
                if (subdeck.DeckId > 0 && existingSubdeckIds.Contains(subdeck.DeckId))
                {
                    var existingSubdeck = deck.Children.First(d => d.DeckId == subdeck.DeckId);
                    existingSubdeck.OriginalTitle = subdeck.OriginalTitle.Trim();
                    existingSubdeck.DeckOrder = subdeck.DeckOrder;

                    if (subdeck.File is { Length: > 0 })
                        existingSubdeck.RawText!.RawText = await GetTextFromFile(subdeck.File);
                }
                else
                {
                    var newDeck = new Deck
                                  {
                                      MediaType = deck.MediaType, OriginalTitle = subdeck.OriginalTitle.Trim(),
                                      DeckOrder = subdeck.DeckOrder,
                                  };

                    if (subdeck.File is { Length: > 0 })
                        newDeck.RawText = new DeckRawText(await GetTextFromFile(subdeck.File));

                    deck.Children.Add(newDeck);
                }
            }
        }

        deck.LastUpdate = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        if (model.Reparse)
            backgroundJobs.Enqueue<ReparseJob>(job => job.Reparse(deck.DeckId));

        return Ok(new { Message = $"Media deck {deck.DeckId} updated successfully" });

        async Task<string> GetTextFromFile(IFormFile file)
        {
            var fileExtension = Path.GetExtension(file.FileName);
            var filePath = Path.Join(path, $"{Guid.NewGuid()}{fileExtension}");
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            stream.Close();

            string text;
            if (fileExtension == ".epub")
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
                text = await System.IO.File.ReadAllTextAsync(filePath);
            }

            return text;
        }
    }

    [HttpPost("reparse-media-by-type/{mediaType}")]
    public async Task<IActionResult> ReparseMediaByType(MediaType mediaType)
    {
        var mediaToReparse = await dbContext.Decks.AsNoTracking()
                                            .Where(d => d.MediaType == mediaType && d.ParentDeck == null)
                                            .ToListAsync();

        if (!mediaToReparse.Any())
            return NotFound(new { Message = $"No media found of type {mediaType}" });

        int count = 0;
        foreach (var deck in mediaToReparse)
        {
            backgroundJobs.Enqueue<ReparseJob>(job => job.Reparse(deck.DeckId));
            count++;
        }

        return Ok(new { Message = $"Reparsing {count} media items of type {mediaType}", Count = count });
    }

    [HttpPost("recompute-frequencies")]
    public IActionResult RecomputeFrequencies()
    {
        backgroundJobs.Enqueue<ComputationJob>(job => job.RecomputeFrequencies());

        return Ok(new { Message = "Recomputing frequencies job has been queued" });
    }

    [HttpPost("recompute-difficulties")]
    public IActionResult RecomputeDifficulties()
    {
        backgroundJobs.Enqueue<ComputationJob>(job => job.RecomputeDifficulties());

        return Ok(new { Message = "Recomputing difficulties job has been queued" });
    }

    [HttpGet("issues")]
    public async Task<IActionResult> GetIssues()
    {
        var decks = await dbContext.Decks.AsNoTracking().Include(d => d.Links).ToListAsync();
        var issues = new IssuesDto();
        
        // If the original title is equal to the english title, then it probably means the original title is an english name too, so we don't need a romaji title
        issues.MissingRomajiTitles = decks.Where(d => d.ParentDeckId == null).Where(d => string.IsNullOrEmpty(d.RomajiTitle) && d.OriginalTitle != d.EnglishTitle).Select(d => d.DeckId).ToList();
        issues.ZeroCharacters = decks.Where(d => d.CharacterCount == 0).Select(d => d.DeckId).ToList();
        issues.MissingLinks = decks.Where(d => d.ParentDeckId == null).Where(d => d.Links.Count == 0).Select(d => d.DeckId).ToList();

        return Ok(issues);
    }
}