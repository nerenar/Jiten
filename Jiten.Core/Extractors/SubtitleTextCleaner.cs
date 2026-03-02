using System.Text.RegularExpressions;
using System.Linq;

namespace Jiten.Core;

public static class SubtitleTextCleaner
{
    private const string BracketSegmentPattern = @"(\([^\)]*\)|（[^）]*）|\[[^\]]*\]|【[^】]*】)";

    private static readonly Regex TagRegex = new(@"\{[^}]*\}|<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex BracketSegmentRegex = new(BracketSegmentPattern, RegexOptions.Compiled);
    private static readonly Regex OnlyBracketsRegex = new(@"^\s*(?:" + BracketSegmentPattern + @"\s*)+$", RegexOptions.Compiled);
    private static readonly Regex PrefixBracketRegex = new(@"^\s*" + BracketSegmentPattern + @"\s*", RegexOptions.Compiled);
    private static readonly Regex MusicOnlyRegex = new(@"^[\s♪～〜ー—…・･]+$", RegexOptions.Compiled);
    private static readonly Regex KanaOnlyRegex = new(@"^[\u3040-\u309F\u30A0-\u30FFー・･\s]+$", RegexOptions.Compiled);

    private static readonly string[] CueWords =
    [
        "BGM",
        "SE",
        "効果音",
        "拍手",
        "歓声",
        "ざわ",
        "ざわざわ",
        "笑",
        "泣",
        "息",
        "ため息",
        "鼻歌",
        "ドア",
        "足音",
        "電話",
        "着信",
        "通知",
        "メール",
        "チャイム",
        "鈴",
        "ベル",
        "雷",
        "雨",
        "風",
    ];

    private static readonly Regex CueRegex =
        new(string.Join("|", CueWords.Select(Regex.Escape)), RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Replace("\\N", "\n").Replace("\\n", "\n");
        return TagRegex.Replace(normalized, "");
    }

    public static string StripNonSpoken(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleanedLines = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (MusicOnlyRegex.IsMatch(line))
                continue;

            if (OnlyBracketsRegex.IsMatch(line))
                continue;

            // Remove leading speaker labels like "（柚子）"
            for (var i = 0; i < 3; i++)
            {
                var match = PrefixBracketRegex.Match(line);
                if (!match.Success)
                    break;

                line = line[match.Length..].TrimStart(' ', '　', '/', '／', '・', '-', '–', '—', ':', '：');
            }

            // Drop bracketed segments that look like SFX or non-spoken cues, or kana-only furigana.
            line = BracketSegmentRegex.Replace(line, match =>
            {
                if (CueRegex.IsMatch(match.Value))
                    return "";

                if (match.Value.Length >= 2)
                {
                    var inner = match.Value[1..^1].Trim();
                    if (!string.IsNullOrEmpty(inner) && KanaOnlyRegex.IsMatch(inner))
                        return "";
                }

                return match.Value;
            });

            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            cleanedLines.Add(line);
        }

        return string.Join("\n", cleanedLines);
    }
}
