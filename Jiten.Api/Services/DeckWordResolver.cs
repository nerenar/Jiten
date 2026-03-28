using System.Text.Json;
using Jiten.Api.Dtos;
using Jiten.Api.Helpers;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Core.Data.JMDict;
using Jiten.Core.Data.FSRS;
using Jiten.Core.Data.User;
using Microsoft.EntityFrameworkCore;

namespace Jiten.Api.Services;

public class DeckWordResolver(JitenDbContext context, UserDbContext userContext, ICurrentUserService currentUserService) : IDeckWordResolver
{
    public async Task<(List<DeckWord>? Words, IResult? Error)> ResolveDeckWords(DeckWordResolveRequest request)
    {
        var (deckId, deck, downloadType, order, minFrequency, maxFrequency,
            excludeMatureMasteredBlacklisted, excludeAllTrackedWords,
            targetPercentage, minOccurrences, maxOccurrences) = request;

        IQueryable<DeckWord> deckWordsQuery = context.DeckWords.AsNoTracking().Where(dw => dw.DeckId == deckId);

        List<DeckWord>? deckWordsRaw = null;

        switch (downloadType)
        {
            case DeckDownloadType.Full:
                break;

            case DeckDownloadType.TopGlobalFrequency:
                deckWordsQuery = deckWordsQuery.Where(dw => context.WordFormFrequencies
                                                                   .Any(wff => wff.WordId == dw.WordId &&
                                                                               wff.ReadingIndex == (short)dw.ReadingIndex &&
                                                                               wff.FrequencyRank >= minFrequency &&
                                                                               wff.FrequencyRank <= maxFrequency));
                break;

            case DeckDownloadType.TopDeckFrequency:
                deckWordsQuery = deckWordsQuery
                                 .OrderByDescending(dw => dw.Occurrences)
                                 .Skip(minFrequency)
                                 .Take(maxFrequency - minFrequency);
                break;

            case DeckDownloadType.TopChronological:
                deckWordsQuery = deckWordsQuery
                                 .OrderBy(dw => dw.DeckWordId)
                                 .Skip(minFrequency)
                                 .Take(maxFrequency - minFrequency);
                break;

            case DeckDownloadType.TargetCoverage:
                if (!currentUserService.IsAuthenticated)
                    return (null, Results.Unauthorized());

                if (targetPercentage is null or < 1 or > 100)
                    return (null, Results.BadRequest("Target percentage must be between 1 and 100"));

                var allDeckWordsForCoverage = await deckWordsQuery
                                                    .OrderByDescending(dw => dw.Occurrences)
                                                    .ToListAsync();

                var coverageWordKeys = allDeckWordsForCoverage
                                       .Select(dw => (dw.WordId, dw.ReadingIndex))
                                       .ToList();

                var coverageStates = await currentUserService.GetKnownWordsState(coverageWordKeys);

                var knownKeysSet = coverageStates
                                   .Where(kvp => kvp.Value.Any(s => s is KnownState.Mastered or KnownState.Blacklisted
                                                                   or KnownState.Mature))
                                   .Select(kvp => WordFormHelper.EncodeWordKey(kvp.Key.WordId, kvp.Key.ReadingIndex))
                                   .ToHashSet();

                int totalOccurrences = deck.WordCount;
                int knownOccurrences = allDeckWordsForCoverage
                                       .Where(dw => knownKeysSet.Contains(WordFormHelper.EncodeWordKey(dw.WordId, dw.ReadingIndex)))
                                       .Sum(dw => dw.Occurrences);

                double targetCoverage = targetPercentage.Value;

                var resultWords = new List<DeckWord>();
                int cumulativeOccurrences = knownOccurrences;

                foreach (var dw in allDeckWordsForCoverage)
                {
                    var key = WordFormHelper.EncodeWordKey(dw.WordId, dw.ReadingIndex);
                    if (knownKeysSet.Contains(key))
                        continue;

                    resultWords.Add(dw);
                    cumulativeOccurrences += dw.Occurrences;

                    double newCoverage = (double)cumulativeOccurrences / totalOccurrences * 100;
                    if (newCoverage >= targetCoverage)
                        break;
                }

                if (order == DeckOrder.Chronological)
                {
                    deckWordsRaw = resultWords.OrderBy(dw => dw.DeckWordId).ToList();
                }
                else if (order == DeckOrder.GlobalFrequency)
                {
                    var resultWordIds = resultWords.Select(dw => dw.WordId).Distinct().ToList();
                    var freqMap = await WordFormHelper.LoadWordFormFrequencies(context, resultWordIds);

                    deckWordsRaw = resultWords.OrderBy(dw =>
                                                           freqMap.TryGetValue((dw.WordId, (short)dw.ReadingIndex), out var wff)
                                                               ? wff.FrequencyRank
                                                               : int.MaxValue
                                                      ).ToList();
                }
                else
                {
                    deckWordsRaw = resultWords;
                }

                break;

            case DeckDownloadType.OccurrenceCount:
                if (minOccurrences.HasValue)
                    deckWordsQuery = deckWordsQuery.Where(dw => dw.Occurrences >= minOccurrences.Value);
                if (maxOccurrences.HasValue)
                    deckWordsQuery = deckWordsQuery.Where(dw => dw.Occurrences <= maxOccurrences.Value);
                break;

            default:
                return (null, Results.BadRequest());
        }

        if (deckWordsRaw == null)
        {
            switch (order)
            {
                case DeckOrder.Chronological:
                    deckWordsQuery = deckWordsQuery.OrderBy(dw => dw.DeckWordId);
                    break;

                case DeckOrder.GlobalFrequency:
                    deckWordsQuery = deckWordsQuery.OrderBy(dw => context.WordFormFrequencies
                                                                         .Where(wff => wff.WordId == dw.WordId &&
                                                                                       wff.ReadingIndex == (short)dw.ReadingIndex)
                                                                         .Select(wff => wff.FrequencyRank)
                                                                         .FirstOrDefault()
                                                           );
                    break;

                case DeckOrder.DeckFrequency:
                    deckWordsQuery = deckWordsQuery.OrderByDescending(dw => dw.Occurrences);
                    break;
                case DeckOrder.Random:
                    deckWordsRaw = await deckWordsQuery.ToListAsync();
                    var rng = Random.Shared;
                    for (var i = deckWordsRaw.Count - 1; i > 0; i--)
                    {
                        var j = rng.Next(i + 1);
                        (deckWordsRaw[i], deckWordsRaw[j]) = (deckWordsRaw[j], deckWordsRaw[i]);
                    }
                    break;
                default:
                    return (null, Results.BadRequest());
            }

            deckWordsRaw = await deckWordsQuery.ToListAsync();
        }

        if ((excludeMatureMasteredBlacklisted || excludeAllTrackedWords) && currentUserService.IsAuthenticated)
        {
            var wordKeys = deckWordsRaw.Select(dw => (dw.WordId, dw.ReadingIndex)).ToList();
            var knownStates = await currentUserService.GetKnownWordsState(wordKeys);

            deckWordsRaw = deckWordsRaw
                .Where(dw => !ShouldExcludeWord((dw.WordId, dw.ReadingIndex), knownStates,
                    excludeMatureMasteredBlacklisted, excludeAllTrackedWords))
                .ToList();
        }

        return (deckWordsRaw, null);
    }

    public async Task<HashSet<long>> GetStudyDeckWordKeys(List<int> deckIds)
    {
        var keys = await context.DeckWords
            .AsNoTracking()
            .Where(dw => deckIds.Contains(dw.DeckId))
            .Select(dw => ((long)dw.WordId << 8) | dw.ReadingIndex)
            .Distinct()
            .ToListAsync();

        return keys.ToHashSet();
    }

    public async Task<HashSet<long>> GetStaticDeckWordKeys(List<int> studyDeckIds)
    {
        var keys = await userContext.UserStudyDeckWords
            .AsNoTracking()
            .Where(w => studyDeckIds.Contains(w.UserStudyDeckId))
            .Select(w => ((long)w.WordId << 8) | (long)w.ReadingIndex)
            .Distinct()
            .ToListAsync();

        return keys.ToHashSet();
    }

    private IQueryable<JmDictWordFormFrequency> BuildGlobalFrequencyQuery(int? minFreq, int? maxFreq, string? posFilter)
    {
        var query = context.WordFormFrequencies.AsNoTracking();

        if (minFreq.HasValue)
            query = query.Where(wff => wff.FrequencyRank >= minFreq.Value);
        if (maxFreq.HasValue)
            query = query.Where(wff => wff.FrequencyRank <= maxFreq.Value);

        if (!string.IsNullOrEmpty(posFilter))
        {
            var posTags = JsonSerializer.Deserialize<string[]>(posFilter);
            if (posTags is { Length: > 0 })
            {
                var wordIdsWithPos = context.JMDictWords.AsNoTracking()
                    .Where(w => w.PartsOfSpeech.Any(p => posTags.Contains(p)));
                query = query.Where(wff => wordIdsWithPos.Any(w => w.WordId == wff.WordId));
            }
        }

        return query;
    }

    public async Task<GlobalDynamicResult> ResolveGlobalDynamicWords(int? minFreq, int? maxFreq, string? posFilter,
        bool excludeKana, bool excludeMatureMasteredBlacklisted, bool excludeAllTrackedWords)
    {
        var query = BuildGlobalFrequencyQuery(minFreq, maxFreq, posFilter);

        if (excludeKana)
            query = query.Where(wff => context.WordForms
                .Any(wf => wf.WordId == wff.WordId && wf.ReadingIndex == wff.ReadingIndex && wf.FormType != JmDictFormType.KanaForm));

        const int maxResults = 500_000;

        var excludedKeys = await BuildExcludedWordKeys(excludeMatureMasteredBlacklisted, excludeAllTrackedWords);

        var words = await query
            .OrderBy(wff => wff.FrequencyRank)
            .Take(maxResults + 1)
            .Select(wff => new ResolvedWord
            {
                WordId = wff.WordId,
                ReadingIndex = (byte)wff.ReadingIndex,
                Occurrences = 1,
                SortOrder = wff.FrequencyRank
            })
            .ToListAsync();

        var wasTruncated = words.Count > maxResults;
        if (wasTruncated)
            words = words.Take(maxResults).ToList();

        if (excludedKeys.Count > 0)
        {
            words = words
                .Where(w => !excludedKeys.Contains(WordFormHelper.EncodeWordKey(w.WordId, w.ReadingIndex)))
                .ToList();
        }

        return new GlobalDynamicResult(words, wasTruncated);
    }

    public async Task<List<ResolvedWord>> ResolveStaticDeckWords(int studyDeckId, int order,
        bool excludeMatureMasteredBlacklisted = false, bool excludeAllTrackedWords = false)
    {
        var baseQuery = userContext.UserStudyDeckWords
            .AsNoTracking()
            .Where(w => w.UserStudyDeckId == studyDeckId);

        if (order == (int)DeckOrder.GlobalFrequency)
        {
            var words = await baseQuery
                .Select(w => new ResolvedWord
                {
                    WordId = w.WordId,
                    ReadingIndex = (byte)w.ReadingIndex,
                    Occurrences = w.Occurrences,
                    SortOrder = w.SortOrder
                })
                .ToListAsync();

            if (words.Count == 0) return [];

            var wordIds = words.Select(w => w.WordId).Distinct().ToList();
            var freqMap = await WordFormHelper.LoadWordFormFrequencies(context, wordIds);

            words.Sort((a, b) =>
            {
                var rankA = freqMap.TryGetValue((a.WordId, a.ReadingIndex), out var fa) ? fa.FrequencyRank : int.MaxValue;
                var rankB = freqMap.TryGetValue((b.WordId, b.ReadingIndex), out var fb) ? fb.FrequencyRank : int.MaxValue;
                return rankA.CompareTo(rankB);
            });

            return FilterExcludedWords(words, await BuildExcludedWordKeys(excludeMatureMasteredBlacklisted, excludeAllTrackedWords));
        }

        if (order == (int)DeckOrder.Random)
        {
            var words = await baseQuery
                .Select(w => new ResolvedWord
                {
                    WordId = w.WordId,
                    ReadingIndex = (byte)w.ReadingIndex,
                    Occurrences = w.Occurrences,
                    SortOrder = w.SortOrder
                })
                .ToListAsync();

            words = FilterExcludedWords(words, await BuildExcludedWordKeys(excludeMatureMasteredBlacklisted, excludeAllTrackedWords));

            var rng = Random.Shared;
            for (var i = words.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (words[i], words[j]) = (words[j], words[i]);
            }
            return words;
        }

        IOrderedQueryable<UserStudyDeckWord> ordered = order == (int)DeckOrder.DeckFrequency
            ? baseQuery.OrderByDescending(w => w.Occurrences)
            : baseQuery.OrderBy(w => w.SortOrder);

        var result = await ordered
            .Select(w => new ResolvedWord
            {
                WordId = w.WordId,
                ReadingIndex = (byte)w.ReadingIndex,
                Occurrences = w.Occurrences,
                SortOrder = w.SortOrder
            })
            .ToListAsync();

        return FilterExcludedWords(result, await BuildExcludedWordKeys(excludeMatureMasteredBlacklisted, excludeAllTrackedWords));
    }

    public async Task<HashSet<long>> GetGlobalDynamicWordKeys(int? minFreq, int? maxFreq, string? posFilter)
    {
        var query = BuildGlobalFrequencyQuery(minFreq, maxFreq, posFilter);

        var keys = await query
            .Select(wff => ((long)wff.WordId << 8) | (byte)wff.ReadingIndex)
            .Distinct()
            .ToListAsync();

        return keys.ToHashSet();
    }

    public async Task<HashSet<long>> GetGlobalDynamicWordKeysForWordIds(int? minFreq, int? maxFreq, string? posFilter, List<int> wordIds)
    {
        if (wordIds.Count == 0) return [];

        var query = BuildGlobalFrequencyQuery(minFreq, maxFreq, posFilter)
            .Where(wff => wordIds.Contains(wff.WordId));

        var keys = await query
            .Select(wff => ((long)wff.WordId << 8) | (byte)wff.ReadingIndex)
            .Distinct()
            .ToListAsync();

        return keys.ToHashSet();
    }

    public async Task<(int Count, bool WasTruncated)> CountGlobalDynamicWords(int? minFreq, int? maxFreq, string? posFilter, bool excludeKana,
        bool excludeMatureMasteredBlacklisted = false, bool excludeAllTrackedWords = false)
    {
        var query = BuildGlobalFrequencyQuery(minFreq, maxFreq, posFilter);

        if (excludeKana)
            query = query.Where(wff => context.WordForms
                .Any(wf => wf.WordId == wff.WordId && wf.ReadingIndex == wff.ReadingIndex && wf.FormType != JmDictFormType.KanaForm));

        var excludedKeys = await BuildExcludedWordKeys(excludeMatureMasteredBlacklisted, excludeAllTrackedWords);

        if (excludedKeys.Count > 0)
        {
            const int maxResults = 500_000;
            var words = await query
                .OrderBy(wff => wff.FrequencyRank)
                .Take(maxResults + 1)
                .Select(wff => new { wff.WordId, ReadingIndex = (byte)wff.ReadingIndex })
                .ToListAsync();

            var wasTruncated = words.Count > maxResults;
            if (wasTruncated)
                words = words.Take(maxResults).ToList();

            var count = words.Count(w => !excludedKeys.Contains(WordFormHelper.EncodeWordKey(w.WordId, w.ReadingIndex)));
            return (count, wasTruncated);
        }

        return (await query.CountAsync(), false);
    }

    public async Task<(int Count, HashSet<long> WordKeys)> CountDeckWords(DeckWordResolveRequest request, bool excludeKana)
    {
        var (deckId, deck, downloadType, order, minFrequency, maxFrequency,
            excludeMatureMasteredBlacklisted, excludeAllTrackedWords,
            targetPercentage, minOccurrences, maxOccurrences) = request;

        IQueryable<DeckWord> query = context.DeckWords.AsNoTracking().Where(dw => dw.DeckId == deckId);

        switch (downloadType)
        {
            case DeckDownloadType.Full:
                break;
            case DeckDownloadType.TopGlobalFrequency:
                query = query.Where(dw => context.WordFormFrequencies
                    .Any(wff => wff.WordId == dw.WordId &&
                                wff.ReadingIndex == (short)dw.ReadingIndex &&
                                wff.FrequencyRank >= minFrequency &&
                                wff.FrequencyRank <= maxFrequency));
                break;
            case DeckDownloadType.TopDeckFrequency:
                query = query.OrderByDescending(dw => dw.Occurrences)
                             .Skip(minFrequency)
                             .Take(maxFrequency - minFrequency);
                break;
            case DeckDownloadType.TopChronological:
                query = query.OrderBy(dw => dw.DeckWordId)
                             .Skip(minFrequency)
                             .Take(maxFrequency - minFrequency);
                break;
            case DeckDownloadType.OccurrenceCount:
                if (minOccurrences.HasValue)
                    query = query.Where(dw => dw.Occurrences >= minOccurrences.Value);
                if (maxOccurrences.HasValue)
                    query = query.Where(dw => dw.Occurrences <= maxOccurrences.Value);
                break;
            default:
                return (0, []);
        }

        if (excludeKana)
            query = query.Where(dw => context.WordForms
                .Any(wf => wf.WordId == dw.WordId && wf.ReadingIndex == (short)dw.ReadingIndex && wf.FormType != JmDictFormType.KanaForm));

        var pairs = await query
            .Select(dw => new { dw.WordId, dw.ReadingIndex })
            .ToListAsync();

        if ((excludeMatureMasteredBlacklisted || excludeAllTrackedWords) && currentUserService.IsAuthenticated)
        {
            var wordKeys = pairs.Select(p => (p.WordId, p.ReadingIndex)).ToList();
            var knownStates = await currentUserService.GetKnownWordsState(wordKeys);

            pairs = pairs
                .Where(p => !ShouldExcludeWord((p.WordId, p.ReadingIndex), knownStates,
                    excludeMatureMasteredBlacklisted, excludeAllTrackedWords))
                .ToList();
        }

        var keySet = pairs.Select(p => WordFormHelper.EncodeWordKey(p.WordId, p.ReadingIndex)).ToHashSet();
        return (keySet.Count, keySet);
    }

    private static bool ShouldExcludeWord(
        (int WordId, byte ReadingIndex) key,
        Dictionary<(int WordId, byte ReadingIndex), List<KnownState>> knownStates,
        bool excludeMatureMasteredBlacklisted,
        bool excludeAllTrackedWords)
    {
        if (!knownStates.TryGetValue(key, out var states))
            return false;
        if (excludeAllTrackedWords && states.Any(s => s != KnownState.New))
            return true;
        if (excludeMatureMasteredBlacklisted &&
            states.Any(s => s is KnownState.Mastered or KnownState.Blacklisted or KnownState.Mature))
            return true;
        return false;
    }

    public async Task<(int Count, HashSet<long> WordKeys)> CountTargetCoverageWords(int deckId, Deck deck, float targetPercentage, bool excludeKana)
    {
        if (!currentUserService.IsAuthenticated)
            return (0, []);

        var allDeckWords = await context.DeckWords.AsNoTracking()
            .Where(dw => dw.DeckId == deckId)
            .OrderByDescending(dw => dw.Occurrences)
            .ToListAsync();

        var coverageWordKeys = allDeckWords
            .Select(dw => (dw.WordId, dw.ReadingIndex))
            .ToList();

        var coverageStates = await currentUserService.GetKnownWordsState(coverageWordKeys);

        var knownKeysSet = coverageStates
            .Where(kvp => kvp.Value.Any(s => s is KnownState.Mastered or KnownState.Blacklisted or KnownState.Mature))
            .Select(kvp => WordFormHelper.EncodeWordKey(kvp.Key.WordId, kvp.Key.ReadingIndex))
            .ToHashSet();

        int totalOccurrences = deck.WordCount;
        int cumulativeOccurrences = 0;
        var resultKeys = new HashSet<long>();

        foreach (var dw in allDeckWords)
        {
            var key = WordFormHelper.EncodeWordKey(dw.WordId, dw.ReadingIndex);
            resultKeys.Add(key);
            cumulativeOccurrences += dw.Occurrences;

            double coverage = (double)cumulativeOccurrences / totalOccurrences * 100;
            if (coverage >= targetPercentage)
                break;
        }

        if (excludeKana)
        {
            var wordIds = allDeckWords
                .Where(dw => resultKeys.Contains(WordFormHelper.EncodeWordKey(dw.WordId, dw.ReadingIndex)))
                .Select(dw => dw.WordId)
                .Distinct();
            var kanaFormKeys = await WordFormHelper.GetKanaFormKeys(context, wordIds);
            if (kanaFormKeys.Count > 0)
                resultKeys.RemoveWhere(k => kanaFormKeys.Contains(k));
        }

        return (resultKeys.Count, resultKeys);
    }

    public async Task<(int Count, HashSet<long> WordKeys)> CountStaticDeckWords(int studyDeckId, bool excludeKana,
        bool excludeMatureMasteredBlacklisted = false, bool excludeAllTrackedWords = false)
    {
        IQueryable<UserStudyDeckWord> query = userContext.UserStudyDeckWords
            .AsNoTracking()
            .Where(w => w.UserStudyDeckId == studyDeckId);

        if (excludeKana)
            query = query.Where(w => context.WordForms
                .Any(wf => wf.WordId == w.WordId && wf.ReadingIndex == w.ReadingIndex && wf.FormType != JmDictFormType.KanaForm));

        var pairs = await query
            .Select(w => new { w.WordId, w.ReadingIndex })
            .ToListAsync();

        var keySet = pairs.Select(p => WordFormHelper.EncodeWordKey(p.WordId, p.ReadingIndex)).ToHashSet();

        var excludedKeys = await BuildExcludedWordKeys(excludeMatureMasteredBlacklisted, excludeAllTrackedWords);
        if (excludedKeys.Count > 0)
            keySet.ExceptWith(excludedKeys);

        return (keySet.Count, keySet);
    }

    private static List<ResolvedWord> FilterExcludedWords(List<ResolvedWord> words, HashSet<long> excludedKeys)
    {
        if (excludedKeys.Count == 0) return words;
        return words.Where(w => !excludedKeys.Contains(WordFormHelper.EncodeWordKey(w.WordId, w.ReadingIndex))).ToList();
    }

    private async Task<HashSet<long>> BuildExcludedWordKeys(bool excludeMatureMasteredBlacklisted, bool excludeAllTrackedWords)
    {
        if ((!excludeMatureMasteredBlacklisted && !excludeAllTrackedWords) || !currentUserService.IsAuthenticated)
            return [];

        var userId = currentUserService.UserId!;
        var excluded = new HashSet<long>();

        IQueryable<FsrsCard> cardQuery = userContext.FsrsCards.AsNoTracking()
            .Where(c => c.UserId == userId);

        if (excludeMatureMasteredBlacklisted && !excludeAllTrackedWords)
        {
            cardQuery = cardQuery.Where(c =>
                c.State == FsrsState.Mastered ||
                c.State == FsrsState.Blacklisted ||
                (c.LastReview != null && c.Due >= c.LastReview.Value.AddDays(21)));
        }

        var cards = await cardQuery
            .Select(c => new { c.WordId, c.ReadingIndex })
            .ToListAsync();

        foreach (var c in cards)
            excluded.Add(WordFormHelper.EncodeWordKey(c.WordId, (byte)c.ReadingIndex));

        var setStatesQuery = userContext.UserWordSetStates
            .AsNoTracking()
            .Where(uwss => uwss.UserId == userId);

        if (excludeMatureMasteredBlacklisted && !excludeAllTrackedWords)
            setStatesQuery = setStatesQuery.Where(s => s.State == WordSetStateType.Mastered || s.State == WordSetStateType.Blacklisted);

        var relevantSetIds = await setStatesQuery
            .Select(s => s.SetId)
            .ToListAsync();

        if (relevantSetIds.Count > 0)
        {
            var members = await context.WordSetMembers
                .AsNoTracking()
                .Where(wsm => relevantSetIds.Contains(wsm.SetId))
                .Select(wsm => new { wsm.WordId, wsm.ReadingIndex })
                .ToListAsync();

            foreach (var m in members)
            {
                excluded.Add(WordFormHelper.EncodeWordKey(m.WordId, m.ReadingIndex));
            }
        }

        return excluded;
    }
}
