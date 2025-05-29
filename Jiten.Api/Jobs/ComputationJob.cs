using Jiten.Core;
using Jiten.Core.Data;

namespace Jiten.Api.Jobs;

public class ComputationJob(JitenDbContext context)
{
    public async Task RecomputeFrequencies()
    {
        await JitenHelper.ComputeFrequencies(context.DbOptions);
    }

    public async Task RecomputeDifficulties()
    {
        foreach (MediaType mediaType in Enum.GetValues(typeof(MediaType)))
        {
            await JitenHelper.ComputeDifficulty(context.DbOptions, false, mediaType);
        }
    }
}