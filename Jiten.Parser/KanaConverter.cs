using System.Collections.Concurrent;
using WanaKanaShaapu;

namespace Jiten.Parser;

internal static class KanaConverter
{
    private static readonly DefaultOptions LongVowelConversion = new() { ConvertLongVowelMark = true };
    private static readonly DefaultOptions NoLongVowelConversion = new() { ConvertLongVowelMark = false };

    private static readonly ConcurrentDictionary<(string Text, bool ConvertLongVowelMark), string> _cache = new();

    public static string ToHiragana(string text) => ToHiragana(text, convertLongVowelMark: true);

    public static string ToHiragana(string text, bool convertLongVowelMark)
    {
        return _cache.GetOrAdd((text, convertLongVowelMark), static key =>
            WanaKana.ToHiragana(key.Text, key.ConvertLongVowelMark ? LongVowelConversion : NoLongVowelConversion));
    }
}
