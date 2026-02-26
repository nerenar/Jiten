using Jiten.Core.Data;

namespace Jiten.Parser;

public partial class MorphologicalAnalyser
{
    private static HashSet<(string, string, string, PartOfSpeech?)> SpecialCases3 =
    [
        ("な", "の", "で", PartOfSpeech.Expression),
        ("で", "は", "ない", PartOfSpeech.Expression),
        ("それ", "で", "も", PartOfSpeech.Conjunction),
        ("なく", "なっ", "た", PartOfSpeech.Verb),
        ("さ", "せ", "て", PartOfSpeech.Verb),
        ("ほう", "が", "いい", PartOfSpeech.Expression),
        ("に", "とっ", "て", PartOfSpeech.Expression),
        ("に", "つい", "て", PartOfSpeech.Expression),
        ("いそう", "に", "ない", PartOfSpeech.Expression),
        ("か", "の", "ように", PartOfSpeech.Expression),
    ];

    private static readonly HashSet<string> AuxiliaryVerbs =
    [
        "続ける",
        "始める",
        "終わる",
        "終える",
        "出す",
        "かける",
        "いたす",
        "いただく",
        "頂く",
        "する",
    ];

    private static readonly HashSet<string> TeFormSubsidiaryVerbs =
    [
        "あげる", "上げる",
        "くれる", "呉れる",
        "もらう", "貰う",
        "やる",
        "さしあげる", "差し上げる",
        "くださる", "下さる",
        "おく", "置く",
        "みる", "見る",
    ];

    // Te-form auxiliary verbs with full deconjugator support (both dict form and conjugated forms)
    // Used in 3-token combining: verb + て/で + aux
    // Only include verbs that have deconjugator rules so combined tokens get proper conjugation info
    private static readonly HashSet<string> TeFormAuxChainVerbs =
    [
        "いる", "居る",
        "ある", "有る",
        "おく", "置く",
        "しまう", "仕舞う",
        "いく", "行く",
        "くる", "来る",
        "みる", "見る",
    ];

    private static readonly Dictionary<string, string> AuxiliaryVerbStems = new()
    {
        { "続ける", "続け" }, { "始める", "始め" }, { "終わる", "終わ" },
        { "終える", "終え" }, { "出す", "出" }, { "かける", "かけ" },
    };

    private static HashSet<(string, string, PartOfSpeech?)> SpecialCases2 =
    [
        ("じゃ", "ない", PartOfSpeech.Expression),
        ("だ", "けど", PartOfSpeech.Conjunction),
        ("だ", "が", PartOfSpeech.Conjunction),
        ("で", "さえ", PartOfSpeech.Expression),
        ("で", "すら", PartOfSpeech.Expression),
        ("と", "いう", PartOfSpeech.Expression),
        ("と", "か", PartOfSpeech.Conjunction),
        ("だ", "から", PartOfSpeech.Conjunction),
        ("これ", "まで", PartOfSpeech.Expression),
        ("それ", "も", PartOfSpeech.Conjunction),
        ("それ", "だけ", PartOfSpeech.Noun),
        ("くせ", "に", PartOfSpeech.Conjunction),
        ("なの", "に", PartOfSpeech.Conjunction),
        ("なの", "で", PartOfSpeech.Conjunction),
        ("の", "で", PartOfSpeech.Particle),
        ("誰", "も", PartOfSpeech.Expression),
        ("誰", "か", PartOfSpeech.Expression),
        ("すぐ", "に", PartOfSpeech.Adverb),
        ("なん", "か", PartOfSpeech.Particle),
        ("なん", "て", PartOfSpeech.Particle),
        ("だっ", "た", PartOfSpeech.Expression),
        ("だっ", "たら", PartOfSpeech.Conjunction),
        ("よう", "に", PartOfSpeech.Expression),
        ("ん", "です", PartOfSpeech.Expression),
        ("ん", "だ", PartOfSpeech.Expression),
        ("です", "か", PartOfSpeech.Expression),
        ("し", "て", PartOfSpeech.Verb),
        ("し", "ちゃ", PartOfSpeech.Verb),
        ("何", "の", PartOfSpeech.Pronoun),
        ("カッコ", "いい", PartOfSpeech.IAdjective),
        ("か", "な", PartOfSpeech.Particle),
        ("よう", "です", PartOfSpeech.Expression),
        ("何も", "かも", PartOfSpeech.Expression),
        ("に", "とって", PartOfSpeech.Expression),
        ("何と", "も", PartOfSpeech.Adverb),
        ("なくて", "も", PartOfSpeech.Expression),
        ("なんに", "も", PartOfSpeech.Adverb),
        ("なし", "で", PartOfSpeech.Expression),
        ("なん", "で", PartOfSpeech.Adverb),
        ("に", "ついて", PartOfSpeech.Expression),
        ("だ", "って", PartOfSpeech.Conjunction),
        ("どこ", "か", PartOfSpeech.Pronoun),
        ("急", "に", PartOfSpeech.Adverb),
        ("と", "ても", PartOfSpeech.Adverb),
        ("で", "も", PartOfSpeech.Conjunction),
        ("多", "き", PartOfSpeech.IAdjective),
        ("ぶっ", "た", PartOfSpeech.Suffix),
        ("に", "よる", PartOfSpeech.Expression),
        ("に", "より", PartOfSpeech.Expression),
        ("とっく", "に", PartOfSpeech.Adverb),
        ("おい", "で", PartOfSpeech.Expression),
        ("どうして", "も", PartOfSpeech.Adverb),
        ("か", "も", PartOfSpeech.Particle),
        ("かも", "しれない", PartOfSpeech.Expression),
        ("何に", "も", PartOfSpeech.Adverb),
        ("ま", "だ", PartOfSpeech.Adverb),
        ("いっしょ", "に", PartOfSpeech.Adverb),
        ("と", "言った", PartOfSpeech.Conjunction),
        ("その", "時", PartOfSpeech.Expression),
        ("そう", "いえば", PartOfSpeech.Expression),
        ("出来", "なさそう", PartOfSpeech.Verb),
    ];

    private readonly HashSet<char> _sentenceEnders = ['。', '！', '？', '」'];

    private static readonly string _stopToken = "|";
    private static readonly string _batchDelimiter = "|||";

    private static readonly HashSet<string> NCompoundSuffixes =
        ["だ", "です", "じゃ", "なら", "ても", "でも", "だろ", "だろう", "だって", "だけど", "だけ", "だが", "だし", "だから"];

    private static readonly HashSet<string> DaCompoundSuffixes =
        ["が", "けど", "けれど", "けれども", "から", "し", "って"];

    private static readonly HashSet<string> VerbIndicatingAuxiliaries =
        ["られる", "れる", "せる", "させる"];

    private static readonly string[] GodanVerbEndings = ["る", "す", "つ", "く", "ぐ", "む", "ぶ", "ぬ", "う"];
}
