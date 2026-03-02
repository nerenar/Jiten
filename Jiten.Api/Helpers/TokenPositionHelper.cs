namespace Jiten.Api.Helpers;

/// <summary>
/// Helpers for mapping parsed tokens back onto source text, tolerating
/// expressive ー characters that the parser's vowel elongation repair strips
/// (e.g., source "なーい" → parsed token "ない").
/// </summary>
public static class TokenPositionHelper
{
    /// <summary>
    /// Finds a token in the source text. Falls back to a fuzzy match that skips ー
    /// in the source when the exact match fails.
    /// Returns (position, sourceLength) where sourceLength accounts for any skipped ー.
    /// </summary>
    public static (int position, int sourceLength) FindTokenInSource(string source, string token, int startFrom)
    {
        int pos = source.IndexOf(token, startFrom, StringComparison.Ordinal);
        if (pos >= 0) return (pos, token.Length);

        for (int i = startFrom; i <= source.Length - token.Length; i++)
        {
            int si = i, ti = 0;
            while (si < source.Length && ti < token.Length)
            {
                if (source[si] == token[ti]) { si++; ti++; }
                else if (source[si] == 'ー') { si++; }
                else break;
            }

            if (ti == token.Length) return (i, si - i);
        }

        return (-1, 0);
    }
}
