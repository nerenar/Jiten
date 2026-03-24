using Jiten.Api.Dtos.Requests;

namespace Jiten.Api.Services;

public interface IDeckDownloadService
{
    Task<byte[]?> GenerateDownload(DeckDownloadRequest request, List<long> wordIds,
        string deckTitle, List<(int WordId, byte ReadingIndex, int Occurrences)> deckWords,
        List<int>? sentenceDeckIds);
}
