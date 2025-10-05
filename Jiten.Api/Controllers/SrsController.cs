using Jiten.Api.Dtos.Requests;
using Jiten.Api.Services;
using Jiten.Core;
using Jiten.Core.Data.FSRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Jiten.Api.Controllers;

[ApiController]
[Route("api/srs")]
[Authorize]
public class SrsController(JitenDbContext context, UserDbContext userContext, ICurrentUserService currentUserService) : ControllerBase
{
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
    public async Task<IResult> Review(SrsReviewRequest request)
    {
        if (!currentUserService.IsAuthenticated)
            return Results.Unauthorized();

        var card = await userContext.FsrsCards.FirstOrDefaultAsync(c => c.UserId == currentUserService.UserId &&
                                                                        c.WordId == request.WordId &&
                                                                        c.ReadingIndex == request.ReadingIndex);

        // TODO: customize the scheduler to the user
        var scheduler = new FsrsScheduler();
        if (card == null)
        {
            card = new FsrsCard(currentUserService.UserId!, request.WordId, request.ReadingIndex);
        }

        var cardAndLog = scheduler.ReviewCard(card, request.Rating, DateTime.UtcNow);

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
        await userContext.SaveChangesAsync();

        return Results.Ok();
    }
}