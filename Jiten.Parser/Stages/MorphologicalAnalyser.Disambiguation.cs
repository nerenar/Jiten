using Jiten.Core.Data;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private List<WordInfo> FilterMisparse(List<WordInfo> wordInfos)
    {
        for (int i = wordInfos.Count - 1; i >= 0; i--)
        {
            var word = wordInfos[i];
            if (word.Text is "なん" or "フン" or "ふん")
                word.PartOfSpeech = PartOfSpeech.Prefix;

            if (word.Text == "そう")
                word.PartOfSpeech = PartOfSpeech.Adverb;

            if (word.Text == "おい")
                word.PartOfSpeech = PartOfSpeech.Interjection;

            if (word is { Text: "つ", PartOfSpeech: PartOfSpeech.Suffix })
                word.PartOfSpeech = PartOfSpeech.Counter;

            // Sudachi tags counter suffixes (e.g. 頭/とう, 匹, 本) with 助数詞 in POS detail
            if (word is { PartOfSpeech: PartOfSpeech.Suffix } &&
                word.HasPartOfSpeechSection(PartOfSpeechSection.Counter))
                word.PartOfSpeech = PartOfSpeech.Counter;

            // 人 after a numeral should be the counter にん, not the suffix じん
            if (word is { Text: "人", PartOfSpeech: PartOfSpeech.Suffix } &&
                i > 0 && (wordInfos[i - 1].PartOfSpeech == PartOfSpeech.Numeral ||
                          wordInfos[i - 1].HasPartOfSpeechSection(PartOfSpeechSection.Numeral)))
                word.PartOfSpeech = PartOfSpeech.Counter;

            // 家 followed by a case particle should be the noun いえ, not the suffix け
            if (word is { Text: "家", PartOfSpeech: PartOfSpeech.Suffix } &&
                i + 1 < wordInfos.Count &&
                wordInfos[i + 1] is { PartOfSpeech: PartOfSpeech.Particle, Text: "から" or "を" or "が" or "に" or "で" or "へ" or "の" or "は" or "も" })
                word.PartOfSpeech = PartOfSpeech.Noun;

            if (word is { Text: "山", PartOfSpeech: PartOfSpeech.Suffix })
                word.PartOfSpeech = PartOfSpeech.Noun;

            if (word is { Text: "だろう" or "だろ", PartOfSpeech: PartOfSpeech.Auxiliary })
            {
                word.PartOfSpeech = PartOfSpeech.Expression;
                word.DictionaryForm = word.Text;
            }

            if (word.Text == "だあ")
            {
                word.Text = "だ";
                word.DictionaryForm = "です";
                word.PartOfSpeech = PartOfSpeech.Auxiliary;
            }
            else if (word.Text == "だー")
            {
                word.DictionaryForm = "です";
                word.PartOfSpeech = PartOfSpeech.Auxiliary;
            }

            // いかんせん (如何せん): prevent resegmentation into いかん + せん
            if (word.Text == "いかんせん")
                word.PreMatchedWordId = 1919420;

            // Standalone prefix-tagged せん that wasn't combined by CombinePrefixes
            // is the Kansai-ben negative of する (= しない), not the numeral prefix 千
            if (word is { Text: "せん", PartOfSpeech: PartOfSpeech.Prefix })
            {
                word.PartOfSpeech = PartOfSpeech.Expression;
                word.PreMatchedWordId = 2844926;
            }

            // セン in katakana not preceded by a numeral → 線 (line), not 千 (thousand)
            if (word.Text == "セン")
            {
                var prev = i > 0 ? wordInfos[i - 1] : null;
                if (prev is not { PartOfSpeech: PartOfSpeech.Numeral })
                    word.PreMatchedWordId = 1391780;
            }

            // ノリ in katakana → 乗り (riding/enthusiasm/vibe, nf07), not 海苔 (seaweed, nf38)
            if (word.Text == "ノリ")
                word.PreMatchedWordId = 1354720;
        }

        return wordInfos;
    }

    /// <summary>
    /// Fixes Sudachi reading disambiguations for kanji homographs using contextual cues.
    /// E.g. 表 before へ/に (directional) when not preceded by a noun → おもて not ひょう.
    /// </summary>
    private List<WordInfo> FixReadingAmbiguity(List<WordInfo> wordInfos)
    {
        for (int i = 0; i < wordInfos.Count; i++)
        {
            var word = wordInfos[i];

            // 表 (ヒョウ) → オモテ when followed by directional particle and not preceded by a noun
            // e.g. 表へ出る (go outside) vs メニュー表 (menu chart)
            if (word is { Text: "表", Reading: "ヒョウ" } &&
                i + 1 < wordInfos.Count && wordInfos[i + 1].Text is "へ" or "に" &&
                (i == 0 || wordInfos[i - 1].PartOfSpeech != PartOfSpeech.Noun))
            {
                word.Reading = "オモテ";
            }

            // 何 (ナン) → ナニ before を/が/も or at end of sentence
            if (word is { Text: "何", Reading: "ナン" })
            {
                var next = i + 1 < wordInfos.Count ? wordInfos[i + 1] : null;
                if (next == null || next.Text is "を" or "が" or "も")
                    word.Reading = "ナニ";
            }

            // 一日/１日 → イチニチ unless preceded by a month (X月一日 = date → keep ツイタチ)
            if (word is { Reading: "ツイタチ", Text: "一日" or "１日" or "1日" })
            {
                var prev = i > 0 ? wordInfos[i - 1] : null;
                if (prev == null || !prev.Text.EndsWith('月'))
                    word.Reading = "イチニチ";
            }

            // 禍 (カ) → ワザワイ when standalone — カ reading only used in compounds (コロナ禍, 戦禍, 禍根)
            if (word is { Text: "禍", Reading: "カ" })
                word.Reading = "ワザワイ";

            // 私 (シ) → ワタシ when standalone — シ reading only in compounds (私的, 私立, 私用)
            if (word is { Text: "私", Reading: "シ" })
            {
                word.Reading = "ワタシ";
                word.PartOfSpeech = PartOfSpeech.Pronoun;
            }

            // 寒気 (カンキ cold air) → サムケ (chills) when followed by が + する
            // e.g. 寒気がする/寒気がした (to have chills) vs 寒気が南下する (cold air moves south)
            if (word is { Text: "寒気", Reading: "カンキ" } &&
                i + 2 < wordInfos.Count && wordInfos[i + 1].Text == "が" &&
                wordInfos[i + 2].DictionaryForm == "する")
            {
                word.Reading = "サムケ";
            }

            // 後 (ゴ) → アト when followed by a numeral/何 — adverbial "more/remaining"
            // e.g. 後何年 (how many more years), 後少し (a little more)
            if (word is { Text: "後", Reading: "ゴ" } &&
                i + 1 < wordInfos.Count &&
                (wordInfos[i + 1].PartOfSpeech == PartOfSpeech.Numeral ||
                 wordInfos[i + 1].HasPartOfSpeechSection(PartOfSpeechSection.Numeral)))
            {
                word.Reading = "アト";
            }

            // 次 (ジ) standalone prefix → ツギ noun — ジ reading only in compounds (次回, 次期, 次男)
            if (word is { Text: "次", Reading: "ジ", PartOfSpeech: PartOfSpeech.Prefix })
            {
                word.Reading = "ツギ";
                word.PartOfSpeech = PartOfSpeech.CommonNoun;
            }

            // 何時 (ナンドキ) → ナンジ — ナンドキ is archaic; modern usage is ナンジ (what time) or いつ (when)
            if (word is { Text: "何時", Reading: "ナンドキ" })
                word.Reading = "ナンジ";

            // 長 as suffix (チョウ) means "chief/head" — JMDict only has this as n (1429740), not suf.
            // Reclassify so the parser matches ちょう instead of なが (2647210, pref/suf "long").
            if (word is { Text: "長", Reading: "チョウ", PartOfSpeech: PartOfSpeech.Suffix })
                word.PartOfSpeech = PartOfSpeech.Noun;

            // 隙 (ヒマ) → スキ — ヒマ reading is obsolete; modern standalone 隙 is always すき
            if (word is { Text: "隙", Reading: "ヒマ" })
                word.Reading = "スキ";

            // 弄* (イラ*) → イジ* — いらう is archaic; modern 弄る is always いじる
            if (word.DictionaryForm == "弄う")
            {
                word.DictionaryForm = "弄る";
                word.NormalizedForm = "弄る";
                word.Reading = word.Reading!.Replace("イラ", "イジ");
            }

            // 角 (カド) — Sudachi always gives カド but standalone 角 has three common readings:
            //   かど (corner): 角を曲がる, 建物の角
            //   つの (horn):   鬼の角, 角が生えている
            //   かく (angle):  三角形の角, 角が90度
            if (word is { Text: "角", Reading: "カド" })
            {
                var next = i + 1 < wordInfos.Count ? wordInfos[i + 1] : null;
                var prev = i > 0 ? wordInfos[i - 1] : null;
                var next2 = i + 2 < wordInfos.Count ? wordInfos[i + 2] : null;
                var prev2 = i >= 2 ? wordInfos[i - 2] : null;

                // つの: 角が生え… / 角が折れ… (only horns grow/break off)
                bool isHornVerb = next is { Text: "が" or "を" } && next2 != null &&
                                  next2.DictionaryForm is "生える" or "生やす" or "折れる" or "折る"
                                      or "研ぐ" or "磨く";

                // つの: creature/demon + の + 角
                bool afterCreature = prev is { Text: "の" } && prev2 != null &&
                                     IsHornBearerWord(prev2.Text);

                // つの: 頭/額/おでこ + に/の + 角
                bool afterHead = prev is { Text: "に" or "の" } && prev2 != null &&
                                 prev2.Text is "頭" or "額" or "おでこ";

                // かく: geometry word + の + 角 (三角形の角, 多角形の角)
                bool afterGeometry = prev is { Text: "の" } && prev2 != null &&
                                     (prev2.Text.EndsWith("角形") || prev2.Text.EndsWith("多角"));

                // かく: 角 + が/は/も + degree/equality (角が90度, 角は等しい)
                var next3 = i + 3 < wordInfos.Count ? wordInfos[i + 3] : null;
                bool beforeDegree = next is { Text: "が" or "は" or "も" } && next2 != null &&
                                    (next2.Text.Contains('度') || next2.DictionaryForm is "等しい"
                                     || ((next2.PartOfSpeech == PartOfSpeech.Numeral
                                         || next2.HasPartOfSpeechSection(PartOfSpeechSection.Numeral))
                                        && next3 is { Text: "度" }));

                if (isHornVerb || afterCreature || afterHead)
                    word.Reading = "ツノ";
                else if (afterGeometry || beforeDegree)
                    word.Reading = "カク";
            }

            // 額 (ガク) → ヒタイ (forehead) when standalone
            // がく reading primarily in compounds (金額, 総額, 月額, 高額) parsed as single tokens.
            // Exception: 額にして (amounting to) is genuine standalone がく.
            if (word is { Text: "額", Reading: "ガク" })
            {
                bool isAmountExpression = i + 2 < wordInfos.Count &&
                    wordInfos[i + 1].Text == "に" && wordInfos[i + 2].DictionaryForm == "する";
                if (!isAmountExpression)
                    word.Reading = "ヒタイ";
            }

            // 様 disambiguation: さま (honorific suffix, 1545790) vs よう (appearance/manner, 1605840)
            // Sudachi reading reliably distinguishes: サマ → honorific, ヨウ → manner
            if (word is { Text: "様", Reading: "サマ" })
                word.PreMatchedWordId = 1545790;

            // Kana よう as 形状詞/助動詞語幹 → 様/manner (1605840), not 陽/positive (1605845)
            if (word is { Text: "よう", Reading: "ヨウ", DictionaryForm: "よう" })
                word.PreMatchedWordId = 1605840;

            // 事 (ジ) → コト when Sudachi misclassified as suffix after verb/expression
            // ジ reading only occurs in kango compounds (仕事, 用事, 無事); those are parsed as single tokens.
            // When 事 is orphaned (after a non-noun), it is the nominalizer こと.
            if (word is { Text: "事", Reading: "ジ", WasReclassifiedFromSuffix: true })
                word.Reading = "コト";

            // たった in time-elapsed context → 経つ (1251100), not 断つ/立つ.
            // When preceded by a time-unit noun (年/月/日/週/間), the intended meaning is
            // "X time has passed" (経つ), not "to cut" (断つ) or "to stand" (立つ).
            if (word.Text == "たった" &&
                word.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary or PartOfSpeech.Unknown)
            {
                var prev = i > 0 ? wordInfos[i - 1] : null;
                if (prev != null && prev.PartOfSpeech != PartOfSpeech.SupplementarySymbol)
                {
                    bool precedingIsTimeUnit = prev.Text.EndsWith('年') || prev.Text.EndsWith('月')
                                              || prev.Text.EndsWith('日') || prev.Text.EndsWith('週')
                                              || prev.Text.EndsWith('間');
                    if (precedingIsTimeUnit)
                    {
                        word.PreMatchedWordId = 1251100;
                        word.DictionaryForm = "たつ";
                        word.PreMatchedConjugations = ["past"];
                    }
                }
            }

            // あの: Sudachi sometimes misclassifies as 感動詞 (filler) when it's prenominal,
            // and as 連体詞 when it's actually a filler interjection.
            // Strategy: override 感動詞→PrenounAdjectival always (Sudachi filler detection unreliable),
            // then 連体詞→Interjection only when clearly not modifying a noun.
            if (word.Text == "あの")
            {
                if (word.PartOfSpeech == PartOfSpeech.Interjection)
                {
                    word.PartOfSpeech = PartOfSpeech.PrenounAdjectival;
                }
                else if (word.PartOfSpeech == PartOfSpeech.PrenounAdjectival)
                {
                    var next = i + 1 < wordInfos.Count ? wordInfos[i + 1] : null;
                    bool nextIsNoun = next is { PartOfSpeech: PartOfSpeech.Noun or PartOfSpeech.Pronoun
                        or PartOfSpeech.NaAdjective or PartOfSpeech.Counter or PartOfSpeech.Numeral
                    };
                    if (!nextIsNoun)
                        word.PartOfSpeech = PartOfSpeech.Interjection;
                }
            }

        }

        return wordInfos;
    }

    private static bool IsHornBearerWord(string text) => text is
        "鬼" or "牛" or "鹿" or "羊" or "山羊" or "馬" or "竜" or "龍"
        or "悪魔" or "怪物" or "獣" or "魔物" or "魔族" or "動物"
        or "トナカイ" or "ドラゴン" or "モンスター" or "ユニコーン" or "サイ"
        or "カブトムシ" or "クワガタ" or "虫" or "デーモン";
}
