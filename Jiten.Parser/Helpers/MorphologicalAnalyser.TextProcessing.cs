using System.Text;
using System.Text.RegularExpressions;
using Jiten.Core.Data;
using Jiten.Core.Utils;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private void PreprocessText(ref string text, bool preserveStopToken)
    {
        text = text.Replace("<", " ");
        text = text.Replace(">", " ");
        text = text.ToFullWidthDigits();
        text = Regex.Replace(text,
                             "[^\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\uFF21-\uFF3A\uFF41-\uFF5A\uFF10-\uFF19\u3005\u3001-\u3003\u3008-\u3011\u3014-\u301F\uFF01-\uFF0F\uFF1A-\uFF1F\uFF3B-\uFF3F\uFF5B-\uFF60\uFF62-\uFF65．\\n…\u3000―\u2500()。！？「」）|]",
                             "");

        if (!preserveStopToken)
            text = text.Replace(_stopToken, "");

        // Force spaces and line breaks with some characters so sudachi doesn't try to include them as part of a word
        text = Regex.Replace(text, "「", "\n「 ");
        text = Regex.Replace(text, "」", " 」\n");
        text = Regex.Replace(text, "〈", " \n〈 ");
        text = Regex.Replace(text, "〉", " 〉\n");
        text = Regex.Replace(text, "\n（", " （");
        text = Regex.Replace(text, "）", " ）\n");
        text = Regex.Replace(text, "《", " \n《 ");
        text = Regex.Replace(text, "》", " 》\n");
        text = Regex.Replace(text, "“", " \n“ ");
        text = Regex.Replace(text, "”", " ”\n");
        text = Regex.Replace(text, "―", " ― ");
        text = Regex.Replace(text, "。", "\n。\n");
        text = Regex.Replace(text, "！", "\n！\n");
        text = Regex.Replace(text, "？", "\n？\n");


        // Normalise tilde characters to chōon mark when used as vowel elongation after kana
        // e.g., ヤバ～ → ヤバー, すご〜 → すごー
        text = Regex.Replace(text, @"(?<=[\u3040-\u309F\u30A0-\u30FF])[～〜]+", "ー");

        // Normalise multiple long-vowel marks to a single one (preserves elongation but not emphasis degree)
        text = Regex.Replace(text, "ー{2,}", "ー");

        // Split up words that are parsed together in sudachi when they don't exist in jmdict
        text = Regex.Replace(text, "垣間見", $"垣間{_stopToken}見");
        // Split はやめる → は + やめる only when NOT preceded by を (which indicates 速める "to quicken")
        text = Regex.Replace(text, "(?<!を)はやめ", $"は{_stopToken}やめ");
        text = Regex.Replace(text, "もやる", $"も{_stopToken}やる");
        text = Regex.Replace(text, "(?<!が)はやる", $"は{_stopToken}やる");
        text = Regex.Replace(text, "べや", $"べ{_stopToken}や");
        text = Regex.Replace(text, "はいい", $"は{_stopToken}いい");
        text = Regex.Replace(text, "元国王", $"元{_stopToken}国王");

        text = Regex.Replace(text, "なんだろう", $"なん{_stopToken}だろう");
        text = Regex.Replace(text, "一人静かに", $"一人{_stopToken}静かに");

        // Fix Sudachi misparsing いやあんま as いやあん + ま instead of いや + あんま
        text = Regex.Replace(text, "いやあんま", $"いや{_stopToken}あんま");

        // Fix Sudachi misparsing この手紙 as この手 (konote - this kind) + 紙 (suffix)
        // Should be この + 手紙 (tegami - letter)
        text = Regex.Replace(text, "この手紙", $"この{_stopToken}手紙");

        // Fix Sudachi misparsing 少女の手 as 少 (prefix) + 女の手 (expression)
        // Should be 少女 (girl) + の + 手 (hand)
        text = Regex.Replace(text, "少女の手", $"少女{_stopToken}の手");

        // Fix Sudachi misparsing 外出/家出 + ない forms as compound noun + adjective
        // Should be 外/家 + 出ない (verb negative) in colloquial speech
        text = Regex.Replace(text, "(外|家)出(ない|なかった|なく)", $"$1{_stopToken}出$2");

        // Normalise emphatic ぶっち → ぶち (colloquial gemination)
        // e.g., ぶっち切れる → ぶち切れる ("to become enraged")
        text = Regex.Replace(text, "ぶっち切", "ぶち切");

        // Fix emphatic っ/ッ at clause boundaries causing Sudachi to misparse
        // e.g., 止まらないっ！ → Sudachi sees ないっ as な + いっ (行く te-form)
        // Insert stop token before っ/ッ when followed by punctuation, whitespace, or end of string
        // Require hiragana before っ/ッ — emphatic っ follows verb/adj conjugations (always hiragana)
        // This avoids breaking katakana interjections like フッ, チッ
        // Exclude うわ before っ/ッ — it's the interjection うわっ, not emphatic stress
        text = Regex.Replace(text, @"(?<=.\p{IsHiragana})(?<!うわ)([っッ])(?=[！!？?。、,\s]|$)", $"{_stopToken}$1");

        // Fix Sudachi misparsing 水魔法 as 水魔 (water demon) + 法 (law)
        // Should be 水 (water) + 魔法 (magic)
        text = Regex.Replace(text, "水魔法", $"水{_stopToken}魔法");

        // Fix Sudachi misparsing 不適応 as 不適 + 応 instead of 不 (prefix) + 適応
        text = Regex.Replace(text, "不適応", $"不{_stopToken}適応");

        // Fix Sudachi misparsing 首落とさ as 首落(ち) + と + さ(する)
        // Sudachi greedily matches 首落 as compound noun 首落ち (beheading)
        // Should be 首 (neck) + 落とす (to drop) in its conjugated forms
        text = Regex.Replace(text, "首落と", $"首{_stopToken}落と");

        // Fix Sudachi merging ホント with following short katakana nouns (e.g., ホントバカ → ホント + バカ)
        text = Regex.Replace(text, "ホント(バカ|ダメ|マジ|クソ|アホ)", $"ホント{_stopToken}$1");

        // Fix Sudachi parsing バカバカ as a single adverb — should be バカ + バカ (e.g., バカバカ言う)
        text = Regex.Replace(text, "バカバカ", $"バカ{_stopToken}バカ");

        // Fix Sudachi parsing 事大 as じだい (subserviency) — should be 事 + 大X (大好き, 大声, 大人, etc.)
        text = Regex.Replace(text, "事大", $"事{_stopToken}大");

        // Always split 日間 → 日 + 間 — 日間 as "daytime" (にっかん) is archaic;
        // in modern text it's virtually always N日間 (counter + period suffix)
        text = text.Replace("日間", $"日{_stopToken}間");

        // Split 何本 → 何 + 本 so it parses as "how many" + counter
        // Sudachi treats 何本 as surname ナニモト or single noun ナンボン; JMDict only has the surname
        text = Regex.Replace(text, "何本", $"何{_stopToken}本");

        // Normalise emphatic katakana within hiragana words (common literary device)
        // e.g., ふウむ → ふうむ, まサか → まさか
        text = Regex.Replace(text, @"(?<=[\u3041-\u3096])[\u30A1-\u30F6](?=[\u3041-\u3096])",
            m => ((char)(m.Value[0] - 0x60)).ToString());

        // Normalise katakana imperative 来イ → 来い (emphatic manga/game dialogue style)
        text = Regex.Replace(text, "来イ", "来い");

        // Normalise colloquial ねえ → ない in fixed expressions that Sudachi doesn't recognise
        // e.g., とんでもねえ → とんでもない, しょうがねえ → しょうがない
        text = Regex.Replace(text, "とんでもねえ", "とんでもない");
        text = Regex.Replace(text, "しょうがねえ", "しょうがない");

        // Split colloquial っしょ (sentence-final contraction of でしょう) from preceding word
        // e.g., サボりすぎっしょー → サボりすぎ|っしょ — so Sudachi can parse the verb correctly
        // JMDict has っしょ as WordId 2271410 (colloquial auxiliary)
        // Exclude いっしょ (一緒/一生) with negative lookbehind; strip trailing ー/う elongation
        text = Regex.Replace(text, @"(?<!い)っしょ[ーう]?(?=[\s\n]|$)", $"{_stopToken}っしょ");

        // Strip mid-sentence ellipsis to preserve Sudachi context (e.g., ここ……からだよね → ここからだよね)
        // Only when 2+ CJK chars precede the ellipsis; a single char is likely stuttering (e.g., ち……千尋, す……すみません)
        text = Regex.Replace(text, @"(?<=[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]{2})…+(?=[^\r\n…])", "");

        // Replace line ending ellipsis with a sentence ender to be able to flatten later
        text = text.Replace("…\r", "。\r").Replace("…\n", "。\n");
    }

    private List<SentenceInfo> SplitIntoSentences(string text, List<WordInfo> wordInfos)
    {
        // Normalise text - remove line breaks for consistent sentence boundaries
        text = text.Replace("\r", "").Replace("\n", "");

        // Phase 1: Build sentences AND track their start positions in the normalised text
        // This allows O(1) sentence lookup by position instead of repeated IndexOf calls
        var sentenceData = new List<(SentenceInfo info, int startPos)>();
        var sb = new StringBuilder();
        bool seenEnder = false;
        int sentenceStartPos = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            sb.Append(current);

            if (_sentenceEnders.Contains(current))
            {
                seenEnder = true;
                continue;
            }

            if (seenEnder)
            {
                if (_sentenceEnders.Contains(current))
                    continue;

                // Flush sentence (without the last character which belongs to next)
                var sentenceText = sb.ToString(0, sb.Length - 1);
                sentenceData.Add((new SentenceInfo(sentenceText), sentenceStartPos));

                // Next sentence starts at current character position
                sentenceStartPos = i;
                sb.Clear();
                sb.Append(current);
                seenEnder = false;
            }
        }

        if (sb.Length > 0)
        {
            sentenceData.Add((new SentenceInfo(sb.ToString()), sentenceStartPos));
        }

        if (sentenceData.Count == 0)
            return [];

        // Phase 2: Assign words using linear position tracking
        // Instead of O(n*m) repeated IndexOf per sentence, we do O(n+m):
        // - One IndexOf per word in the global text (O(n) total across all words)
        // - O(1) sentence lookup per word using position boundaries
        int globalPos = 0;
        int sentenceIdx = 0;

        foreach (var word in wordInfos)
        {
            if (string.IsNullOrEmpty(word.Text) || word.PartOfSpeech == PartOfSpeech.BlankSpace)
                continue;

            // Find word in the global text starting from current position
            int wordPos = text.IndexOf(word.Text, globalPos, StringComparison.Ordinal);
            if (wordPos < 0)
                continue;

            // Advance to the correct sentence based on word position
            while (sentenceIdx < sentenceData.Count - 1)
            {
                int nextSentenceStart = sentenceData[sentenceIdx + 1].startPos;
                if (wordPos < nextSentenceStart)
                    break;
                sentenceIdx++;
            }

            var (sentence, sentenceStart) = sentenceData[sentenceIdx];
            int sentenceEnd = sentenceStart + sentence.Text.Length;
            int wordEnd = wordPos + word.Text.Length;

            // Handle words that span sentence boundaries - merge sentences
            while (wordEnd > sentenceEnd && sentenceIdx + 1 < sentenceData.Count)
            {
                var nextSentence = sentenceData[sentenceIdx + 1].info;
                sentence.Text += nextSentence.Text;
                sentenceData.RemoveAt(sentenceIdx + 1);
                sentenceEnd = sentenceStart + sentence.Text.Length;
            }

            // Calculate position within the sentence and add word
            int posInSentence = wordPos - sentenceStart;
            sentence.Words.Add((word, posInSentence, word.Text.Length));

            globalPos = wordEnd;
        }

        return sentenceData.Select(s => s.info).ToList();
    }
}
