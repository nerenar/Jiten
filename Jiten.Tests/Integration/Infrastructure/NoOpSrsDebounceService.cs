using Jiten.Api.Services;

namespace Jiten.Parser.Tests.Integration.Infrastructure;

public class NoOpSrsDebounceService : ISrsDebounceService
{
    public bool TryAcquire(string userId, int wordId, byte readingIndex) => true;
}
