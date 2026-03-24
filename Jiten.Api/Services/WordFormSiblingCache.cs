using Jiten.Core;
using Jiten.Core.Data.JMDict;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Services;

public class WordFormSiblingCache : IWordFormSiblingCache
{
    private readonly IDbContextFactory<JitenDbContext> _contextFactory;
    private readonly ILogger<WordFormSiblingCache> _logger;
    private volatile Dictionary<int, WordFormInfo> _wordForms = new();

    public WordFormSiblingCache(IDbContextFactory<JitenDbContext> contextFactory, ILogger<WordFormSiblingCache> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        TryLoadData();
    }

    public void Reload() => TryLoadData();

    private void TryLoadData()
    {
        try
        {
            LoadData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WordFormSiblingCache failed to load data from DB - serving with empty cache");
        }
    }

    private void LoadData()
    {
        var result = new Dictionary<int, WordFormInfo>();
        using var context = _contextFactory.CreateDbContext();

        var groups = context.WordForms
            .AsNoTracking()
            .Where(wf => !wf.IsSearchOnly)
            .Select(wf => new { wf.WordId, wf.ReadingIndex, wf.FormType })
            .ToList()
            .GroupBy(wf => wf.WordId);

        foreach (var group in groups)
        {
            var kanjiIndexes = group
                .Where(wf => wf.FormType == JmDictFormType.KanjiForm)
                .Select(wf => (byte)wf.ReadingIndex)
                .ToArray();
            var kanaIndexes = group
                .Where(wf => wf.FormType == JmDictFormType.KanaForm)
                .Select(wf => (byte)wf.ReadingIndex)
                .ToArray();

            if (kanjiIndexes.Length > 0 && kanaIndexes.Length > 0)
            {
                result[group.Key] = new WordFormInfo
                {
                    KanjiReadingIndexes = kanjiIndexes,
                    KanaReadingIndexes = kanaIndexes
                };
            }
        }

        _wordForms = result;
        _logger.LogInformation("WordFormSiblingCache loaded {Count} words with both kanji and kana forms", result.Count);
    }

    public byte[]? GetKanaIndexesForKanji(int wordId, byte kanjiReadingIndex)
    {
        if (!_wordForms.TryGetValue(wordId, out var info))
            return null;
        if (!info.KanjiReadingIndexes.Contains(kanjiReadingIndex))
            return null;
        return info.KanaReadingIndexes;
    }

    public byte[]? GetKanjiIndexesForKana(int wordId, byte kanaReadingIndex)
    {
        if (!_wordForms.TryGetValue(wordId, out var info))
            return null;
        if (!info.KanaReadingIndexes.Contains(kanaReadingIndex))
            return null;
        return info.KanjiReadingIndexes;
    }

    public WordFormInfo? GetWordFormInfo(int wordId)
    {
        return _wordForms.GetValueOrDefault(wordId);
    }
}
