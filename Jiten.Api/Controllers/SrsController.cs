using System.Globalization;
using Jiten.Api.Dtos;
using Jiten.Api.Dtos.Requests;
using Jiten.Api.Helpers;
using Jiten.Api.Jobs;
using Jiten.Api.Services;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.FSRS;
using Jiten.Core.Data.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/srs")]
[Authorize]
public class SrsController(
    JitenDbContext context,
    UserDbContext userContext,
    ICurrentUserService currentUserService,
    ISrsDebounceService debounceService,
    IStudySessionService sessionService,
    IWordFormSiblingCache wordFormCache,
    SrsRecomputeJob recomputeJob,
    ILogger<SrsController> logger) : ControllerBase
{
    [HttpPost("undo-review")]
    [SwaggerOperation(Summary = "Undo the last review for a card",
                      Description = "Removes the most recent review log and reconstructs the card state by replaying remaining reviews.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IResult> UndoReview(UndoReviewRequest request)
    {
        var userId = currentUserService.UserId;
        if (userId == null) return Results.Unauthorized();

        if (!debounceService.TryAcquire(userId, request.WordId, request.ReadingIndex))
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

        await using var transaction = await userContext.Database.BeginTransactionAsync();

        var card = await userContext.FsrsCards
            .Include(c => c.ReviewLogs)
            .FirstOrDefaultAsync(c => c.UserId == userId
                                      && c.WordId == request.WordId
                                      && c.ReadingIndex == request.ReadingIndex);

        if (card == null)
            return Results.NotFound("Card not found");

        var lastLog = card.ReviewLogs
            .OrderByDescending(l => l.ReviewDateTime)
            .FirstOrDefault();

        if (lastLog == null)
            return Results.BadRequest("No review to undo");

        userContext.FsrsReviewLogs.Remove(lastLog);

        var remainingLogs = card.ReviewLogs
            .Where(l => l.ReviewLogId != lastLog.ReviewLogId)
            .OrderBy(l => l.ReviewDateTime)
            .ToList();

        var cardDeleted = false;

        if (remainingLogs.Count == 0)
        {
            userContext.FsrsCards.Remove(card);
            cardDeleted = true;
        }
        else
        {
            var userSettings = await LoadUserSettings(userId);
            var parameters = GetParameters(userSettings);
            var desiredRetention = GetDesiredRetention(userSettings);
            var scheduler = new FsrsScheduler(desiredRetention: desiredRetention, parameters: parameters, enableFuzzing: false);

            var replayCard = new FsrsCard(userId, card.WordId, card.ReadingIndex);
            foreach (var log in remainingLogs)
            {
                var reviewDt = DateTime.SpecifyKind(log.ReviewDateTime, DateTimeKind.Utc);
                var result = scheduler.ReviewCard(replayCard, log.Rating, reviewDt);
                replayCard = result.UpdatedCard;
            }

            card.State = replayCard.State;
            card.Step = replayCard.Step;
            card.Stability = replayCard.Stability;
            card.Difficulty = replayCard.Difficulty;
            card.Due = replayCard.Due;
            card.LastReview = replayCard.LastReview;
        }

        await CoverageDirtyHelper.MarkCoverageDirty(userContext, userId);
        await userContext.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation("User undid SRS review: WordId={WordId}, ReadingIndex={ReadingIndex}, CardDeleted={CardDeleted}",
                              request.WordId, request.ReadingIndex, cardDeleted);

        return Results.Json(new { success = true, cardDeleted });
    }

    /// <summary>
    /// Rate (review) a word using the FSRS scheduler.
    /// </summary>
    /// <param name="request">A request containing the word to review and a rating</param>
    /// <returns>Status.</returns>
    [HttpPost("review")]
    [SwaggerOperation(Summary = "Review a FSRS card",
                      Description = "Rate (review) a word using the FSRS scheduler.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IResult> Review(SrsReviewRequest request)
    {
        var userId = currentUserService.UserId;
        if (userId == null) return Results.Unauthorized();

        if (!Enum.IsDefined(request.Rating))
            return Results.BadRequest($"Invalid rating: {(int)request.Rating}. Must be 1 (Again), 2 (Hard), 3 (Good), or 4 (Easy).");

        var hasIdempotency = !string.IsNullOrEmpty(request.SessionId) && !string.IsNullOrEmpty(request.ClientRequestId);

        if (hasIdempotency)
        {
            if (!await sessionService.ValidateSessionAsync(request.SessionId!, userId))
                return Results.Unauthorized();

            var cached = await sessionService.GetCachedReviewResultAsync(request.SessionId!, request.ClientRequestId!);
            if (cached != null)
                return Results.Content(cached, "application/json");
        }

        if (!debounceService.TryAcquire(userId, request.WordId, request.ReadingIndex))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        await using var transaction = await userContext.Database.BeginTransactionAsync();

        var card = await userContext.FsrsCards.FirstOrDefaultAsync(c => c.UserId == userId &&
                                                                        c.WordId == request.WordId &&
                                                                        c.ReadingIndex == request.ReadingIndex);

        var userSettings = await LoadUserSettings(userId);
        var parameters = GetParameters(userSettings);
        var desiredRetention = GetDesiredRetention(userSettings);
        var scheduler = new FsrsScheduler(desiredRetention: desiredRetention, parameters: parameters);
        if (card == null)
        {
            card = new FsrsCard(userId, request.WordId, request.ReadingIndex);
        }

        var cardAndLog = scheduler.ReviewCard(card, request.Rating, DateTime.UtcNow, request.ReviewDuration);

        if (card.CardId == 0)
        {
            await userContext.FsrsCards.AddAsync(cardAndLog.UpdatedCard);
            await userContext.SaveChangesAsync();

            cardAndLog.ReviewLog.CardId = cardAndLog.UpdatedCard.CardId;
        }
        else
        {
            userContext.Entry(card).CurrentValues.SetValues(cardAndLog.UpdatedCard);
            cardAndLog.ReviewLog.CardId = card.CardId;
        }

        await userContext.FsrsReviewLogs.AddAsync(cardAndLog.ReviewLog);
        await CoverageDirtyHelper.MarkCoverageDirty(userContext, userId);
        await userContext.SaveChangesAsync();
        await transaction.CommitAsync();

        var previewScheduler = new FsrsScheduler(desiredRetention: desiredRetention, parameters: parameters, enableFuzzing: false);
        var intervals = previewScheduler.PreviewIntervals(cardAndLog.UpdatedCard, DateTime.UtcNow);

        var resultObj = new
        {
            success = true,
            nextDue = cardAndLog.UpdatedCard.Due,
            newState = (int)cardAndLog.UpdatedCard.State,
            stability = cardAndLog.UpdatedCard.Stability,
            difficulty = cardAndLog.UpdatedCard.Difficulty,
            intervalPreview = new
            {
                againSeconds = (int)intervals[FsrsRating.Again].TotalSeconds,
                hardSeconds = (int)intervals[FsrsRating.Hard].TotalSeconds,
                goodSeconds = (int)intervals[FsrsRating.Good].TotalSeconds,
                easySeconds = (int)intervals[FsrsRating.Easy].TotalSeconds,
            }
        };

        if (hasIdempotency)
        {
            var resultJson = System.Text.Json.JsonSerializer.Serialize(resultObj);
            _ = sessionService.StoreCachedReviewResultAsync(request.SessionId!, request.ClientRequestId!, resultJson);
        }

        logger.LogInformation("User reviewed SRS card: WordId={WordId}, ReadingIndex={ReadingIndex}, Rating={Rating}, NewState={NewState}",
                              request.WordId, request.ReadingIndex, request.Rating, cardAndLog.UpdatedCard.State);
        return Results.Json(resultObj);
    }

    [HttpGet("review-history/{wordId}/{readingIndex}")]
    [SwaggerOperation(Summary = "Get review history for a word")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IResult> GetReviewHistory(int wordId, byte readingIndex)
    {
        var userId = currentUserService.UserId;
        if (userId == null) return Results.Unauthorized();

        var card = await userContext.FsrsCards
            .Include(c => c.ReviewLogs)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId
                                      && c.WordId == wordId
                                      && c.ReadingIndex == readingIndex);

        if (card == null)
            return Results.Ok(new ReviewHistoryDto());

        return Results.Ok(new ReviewHistoryDto
        {
            Card = new CardStateDto
            {
                State = (int)card.State,
                Stability = card.Stability,
                Difficulty = card.Difficulty,
                Due = card.Due,
                LastReview = card.LastReview,
                CreatedAt = card.CreatedAt,
            },
            Reviews = card.ReviewLogs
                .OrderByDescending(l => l.ReviewDateTime)
                .Select(l => new ReviewLogDto
                {
                    Rating = (int)l.Rating,
                    ReviewDateTime = l.ReviewDateTime,
                    ReviewDuration = l.ReviewDuration,
                })
                .ToList()
        });
    }

    [HttpGet("review-history")]
    [SwaggerOperation(Summary = "Get paginated review history across all cards")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IResult> GetRecentReviews([FromQuery] int offset = 0, [FromQuery] int limit = 100)
    {
        var userId = currentUserService.UserId;
        if (userId == null) return Results.Unauthorized();

        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, 100);

        var totalCount = await userContext.FsrsReviewLogs
            .Where(l => l.Card.UserId == userId)
            .CountAsync();

        var logs = await userContext.FsrsReviewLogs
            .AsNoTracking()
            .Where(l => l.Card.UserId == userId)
            .OrderByDescending(l => l.ReviewDateTime)
            .Skip(offset)
            .Take(limit)
            .Select(l => new
            {
                l.Card.WordId,
                l.Card.ReadingIndex,
                l.Rating,
                l.ReviewDateTime,
                l.ReviewDuration,
                CardState = (int)l.Card.State
            })
            .ToListAsync();

        var wordIds = logs.Select(l => l.WordId).Distinct().ToList();
        var formDict = await WordFormHelper.LoadWordForms(context, wordIds);

        var reviews = logs.Select(l =>
        {
            var form = formDict.GetValueOrDefault((l.WordId, l.ReadingIndex));
            return new RecentReviewDto
            {
                WordId = l.WordId,
                ReadingIndex = l.ReadingIndex,
                WordText = form?.RubyText ?? form?.Text ?? "",
                Rating = (int)l.Rating,
                ReviewDateTime = l.ReviewDateTime,
                ReviewDuration = l.ReviewDuration,
                CardState = l.CardState
            };
        }).ToList();

        return Results.Ok(new PaginatedResponse<List<RecentReviewDto>>(reviews, totalCount, limit, offset));
    }

    [HttpGet("settings")]
    [SwaggerOperation(Summary = "Get FSRS settings",
                      Description = "Get the user's FSRS settings.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IResult> GetParameters()
    {
        var userId = currentUserService.UserId;
        if (userId == null)
        {
            return Results.Unauthorized();
        }

        var userSettings = await LoadUserSettings(userId);
        var parameters = GetParameters(userSettings);
        var desiredRetention = GetDesiredRetention(userSettings);
        var isDefault = IsSettingsDefault(parameters, desiredRetention);
        var response = new FsrsParametersResponse
        {
            Parameters = SerializeParametersCsv(parameters),
            IsDefault = isDefault,
            DesiredRetention = desiredRetention
        };

        return Results.Ok(response);
    }

    [HttpPut("settings")]
    [SwaggerOperation(Summary = "Update FSRS settings",
                      Description = "Update the user's FSRS settings.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IResult> UpdateParameters(UpdateFsrsParametersRequest request)
    {
        var userId = currentUserService.UserId;
        if (userId == null)
        {
            return Results.Unauthorized();
        }

        var settings = await userContext.UserFsrsSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        var currentParameters = GetParameters(settings);
        var currentDesiredRetention = GetDesiredRetention(settings);

        var nextParameters = currentParameters;
        var nextDesiredRetention = currentDesiredRetention;

        if (request.Parameters != null)
        {
            if (string.IsNullOrWhiteSpace(request.Parameters))
            {
                nextParameters = FsrsConstants.DefaultParameters;
            }
            else if (!TryParseParametersCsv(request.Parameters, out var parsedParameters, out var error))
            {
                return Results.BadRequest(error);
            }
            else
            {
                nextParameters = parsedParameters;
            }
        }

        if (request.DesiredRetention.HasValue)
        {
            if (!IsDesiredRetentionValid(request.DesiredRetention.Value))
            {
                return Results.BadRequest("Desired retention must be between 0 and 1.");
            }

            nextDesiredRetention = request.DesiredRetention.Value;
        }

        var parametersAreDefault = AreParametersDefault(nextParameters);
        var desiredRetentionIsDefault = IsDesiredRetentionDefault(nextDesiredRetention);
        var shouldRemoveSettings = parametersAreDefault && desiredRetentionIsDefault;

        if (shouldRemoveSettings)
        {
            if (settings != null)
            {
                settings.Parameters = Array.Empty<double>();
                settings.DesiredRetention = null;
                await userContext.SaveChangesAsync();
            }
        }
        else
        {
            if (settings == null)
            {
                settings = new UserFsrsSettings { UserId = userId };
                userContext.UserFsrsSettings.Add(settings);
            }

            settings.Parameters = nextParameters;
            settings.DesiredRetention = desiredRetentionIsDefault ? null : nextDesiredRetention;
            await userContext.SaveChangesAsync();
        }

        return Results.Ok(new FsrsParametersResponse
        {
            Parameters = SerializeParametersCsv(nextParameters),
            IsDefault = shouldRemoveSettings,
            DesiredRetention = nextDesiredRetention
        });
    }

    [HttpPost("settings/recompute")]
    [SwaggerOperation(Summary = "Recompute FSRS scheduling",
                      Description = "Recompute scheduling for all cards using the stored settings (or defaults).")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IResult> RecomputeParameters()
    {
        var userId = currentUserService.UserId;
        if (userId == null)
        {
            return Results.Unauthorized();
        }

        var userSettings = await LoadUserSettings(userId);
        var parameters = GetParameters(userSettings);
        var desiredRetention = GetDesiredRetention(userSettings);
        await recomputeJob.RecomputeUserSrs(userId, parameters, desiredRetention);

        return Results.Ok(new { success = true });
    }

    [HttpPost("settings/recompute-batch")]
    [EnableRateLimiting("compute")]
    [SwaggerOperation(Summary = "Recompute FSRS scheduling batch",
                      Description = "Recompute scheduling in batches for progress tracking.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IResult> RecomputeParametersBatch([FromQuery] long lastCardId = 0, [FromQuery] int batchSize = 500)
    {
        var userId = currentUserService.UserId;
        if (userId == null)
        {
            return Results.Unauthorized();
        }

        if (batchSize < 1 || batchSize > 2000)
        {
            batchSize = 500;
        }

        var userSettings = await LoadUserSettings(userId);
        var parameters = GetParameters(userSettings);
        var desiredRetention = GetDesiredRetention(userSettings);
        var result = await recomputeJob.RecomputeUserSrsBatch(userId, parameters, desiredRetention, lastCardId, batchSize);

        return Results.Ok(result);
    }

    [HttpPost("set-vocabulary-state")]
    [SwaggerOperation(Summary = "Set vocabulary state",
                      Description = "Set vocabulary state to neverForget or blacklist.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IResult> SetVocabularyState(SetVocabularyStateRequest request)
    {
        var userId = currentUserService.UserId;
        if (userId == null) return Results.Unauthorized();

        if (!debounceService.TryAcquire(userId, request.WordId, request.ReadingIndex))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var card = await userContext.FsrsCards.FirstOrDefaultAsync(c =>
                                                                       c.UserId == userId &&
                                                                       c.WordId == request.WordId &&
                                                                       c.ReadingIndex == request.ReadingIndex);

        switch (request.State)
        {
            case "neverForget-add":
                if (card == null)
                {
                    card = new FsrsCard(userId, request.WordId, request.ReadingIndex,
                                        due: DateTime.UtcNow,
                                        lastReview: DateTime.UtcNow,
                                        state: FsrsState.Mastered);
                    await userContext.FsrsCards.AddAsync(card);
                }
                else
                {
                    card.State = FsrsState.Mastered;
                }

                await WordFormHelper.RemoveRedundantKanaSrsCards(userContext, wordFormCache, userId, request.WordId, request.ReadingIndex);

                break;

            case "neverForget-remove":
                if (card != null && card.State == FsrsState.Mastered)
                    RestoreCardState(card);
                break;

            case "blacklist-add":
                if (card == null)
                {
                    card = new FsrsCard(userId, request.WordId, request.ReadingIndex,
                                        state: FsrsState.Blacklisted);
                    await userContext.FsrsCards.AddAsync(card);
                }
                else
                {
                    card.State = FsrsState.Blacklisted;
                }

                break;

            case "blacklist-remove":
                if (card != null && card.State == FsrsState.Blacklisted)
                {
                    RestoreCardState(card);
                }
                else if (card == null)
                {
                    // Check if blacklisted via WordSet (two queries — separate DbContexts)
                    var blacklistedSetIds = await userContext.UserWordSetStates
                        .Where(uwss => uwss.UserId == userId && uwss.State == WordSetStateType.Blacklisted)
                        .Select(uwss => uwss.SetId)
                        .ToListAsync();

                    var isSetBlacklisted = blacklistedSetIds.Count > 0 &&
                        await context.WordSetMembers
                            .AnyAsync(wsm => blacklistedSetIds.Contains(wsm.SetId) &&
                                             wsm.WordId == request.WordId &&
                                             wsm.ReadingIndex == request.ReadingIndex);

                    if (isSetBlacklisted)
                    {
                        // Create FsrsCard to override set blacklist
                        card = new FsrsCard(userId, request.WordId, request.ReadingIndex,
                                            state: FsrsState.Learning);
                        await userContext.FsrsCards.AddAsync(card);
                    }
                }
                break;

            case "forget-add":
                if (card != null)
                {
                    userContext.FsrsCards.Remove(card);
                }

                break;

            case "suspend-add":
                if (card != null && card.State != FsrsState.Suspended)
                {
                    card.State = FsrsState.Suspended;
                }

                break;

            case "suspend-remove":
                if (card != null && card.State == FsrsState.Suspended)
                {
                    RestoreCardState(card);
                }

                break;

            default:
                return Results.BadRequest($"Invalid state: {request.State}");
        }

        await CoverageDirtyHelper.MarkCoverageDirty(userContext, userId);
        await userContext.SaveChangesAsync();

        logger.LogInformation("User set vocabulary state: WordId={WordId}, ReadingIndex={ReadingIndex}, State={State}",
                              request.WordId, request.ReadingIndex, request.State);
        return Results.Json(new { success = true });

        void RestoreCardState(FsrsCard card)
        {
            if (card.Stability is > 0)
            {
                card.State = FsrsState.Review;
            }
            else
            {
                card.State = FsrsState.Learning;
                card.Step = 0;
            }

            card.Due = DateTime.UtcNow;
        }
    }

    private async Task<UserFsrsSettings?> LoadUserSettings(string userId)
    {
        return await userContext.UserFsrsSettings.AsNoTracking()
                                                 .FirstOrDefaultAsync(s => s.UserId == userId);
    }

    private static bool AreParametersDefault(double[] parameters)
    {
        if (parameters.Length != FsrsConstants.DefaultParameters.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (Math.Abs(parameters[i] - FsrsConstants.DefaultParameters[i]) > 1e-6)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDesiredRetentionDefault(double desiredRetention)
    {
        return Math.Abs(desiredRetention - FsrsConstants.DefaultDesiredRetention) < 1e-6;
    }

    private static bool IsSettingsDefault(double[] parameters, double desiredRetention)
    {
        return AreParametersDefault(parameters) && IsDesiredRetentionDefault(desiredRetention);
    }

    private static bool IsDesiredRetentionValid(double desiredRetention)
    {
        return desiredRetention > 0 && desiredRetention < 1 && !double.IsNaN(desiredRetention) && !double.IsInfinity(desiredRetention);
    }

    private static double[] GetParameters(UserFsrsSettings? settings)
    {
        return TryGetStoredParameters(settings, out var parameters) ? parameters : FsrsConstants.DefaultParameters;
    }

    private static double GetDesiredRetention(UserFsrsSettings? settings)
    {
        if (settings?.DesiredRetention is double desiredRetention && IsDesiredRetentionValid(desiredRetention))
        {
            return desiredRetention;
        }

        return FsrsConstants.DefaultDesiredRetention;
    }

    private static bool TryGetStoredParameters(UserFsrsSettings? settings, out double[] parameters)
    {
        parameters = Array.Empty<double>();
        if (settings == null)
        {
            return false;
        }

        var stored = settings.GetParametersOnce();
        if (stored.Length != FsrsConstants.DefaultParameters.Length)
        {
            return false;
        }

        if (stored.Any(value => double.IsNaN(value) || double.IsInfinity(value)))
        {
            return false;
        }

        parameters = stored;
        return true;
    }

    private static bool TryParseParametersCsv(string? csv, out double[] parameters, out string error)
    {
        parameters = Array.Empty<double>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(csv))
        {
            error = "Parameters are required.";
            return false;
        }

        var parts = csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var expectedCount = FsrsConstants.DefaultParameters.Length;
        if (parts.Length != expectedCount)
        {
            error = $"Parameters must contain {expectedCount} comma-separated values.";
            return false;
        }

        var parsed = new double[expectedCount];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                double.IsNaN(value) || double.IsInfinity(value))
            {
                error = $"Invalid parameter at position {i + 1}.";
                return false;
            }

            parsed[i] = value;
        }

        parameters = parsed;
        return true;
    }

    private static string SerializeParametersCsv(double[] parameters)
    {
        return string.Join(", ", parameters.Select(value => value.ToString("0.####", CultureInfo.InvariantCulture)));
    }
}
