namespace Jiten.Core.Data;

public enum PartOfSpeech
{
    Unknown = 0,
    Noun = 1,
    Verb = 2,
    IAdjective = 3,
    Adverb = 4,
    Particle = 5,
    Conjunction = 6,
    Auxiliary = 7,
    Adnominal = 8,
    Interjection = 9,
    Symbol = 10,
    Prefix = 11,
    Filler = 12,
    Name = 13,
    Pronoun = 14,
    NaAdjective = 15,
    Suffix = 16,
    CommonNoun = 17,
    SupplementarySymbol = 18,
    BlankSpace = 19,
    Expression = 20,
    NominalAdjective = 21,
    Numeral = 22,
    PrenounAdjectival = 23,
    Counter = 24,
    AdverbTo = 25,
    NounSuffix = 26
}

public enum PartOfSpeechSection
{
    None = 0,
    Amount = 1,
    Alphabet = 2,
    FullStop = 3,
    BlankSpace = 4,
    Suffix = 5,
    Pronoun = 6,
    Independant = 7,
    Dependant = 8,
    Filler = 9,
    Common = 10,
    SentenceEndingParticle = 11,
    Counter = 12,
    ParallelMarker = 13,
    BindingParticle = 14,
    PotentialAdverb = 15,
    CaseMarkingParticle = 16,
    IrregularConjunction = 17,
    ConjunctionParticle = 18,
    AuxiliaryVerbStem = 19,
    AdjectivalStem = 20,
    CompoundWord = 21,
    Quotation = 22,
    NounConjunction = 23,
    AdverbialParticle = 24,
    ConjunctiveParticleClass = 25,
    Adverbialization = 26,
    AdverbialParticleOrParallelMarkerOrSentenceEndingParticle = 27,
    AdnominalAdjective = 28,
    ProperNoun = 29,
    Special = 30,
    VerbConjunction = 31,
    PersonName = 32,
    FamilyName = 33,
    Organization = 34,
    NotAdjectiveStem = 35,
    Comma = 36,
    OpeningBracket = 37,
    ClosingBracket = 38,
    Region = 39,
    Country = 40,
    Numeral = 41,
    PossibleDependant = 42,
    CommonNoun = 43,
    SubstantiveAdjective = 44,
    PossibleCounterWord = 45,
    PossibleSuru = 46,
    Juntaijoushi = 47,
    PossibleNaAdjective = 48,
    VerbLike = 49,
    PossibleVerbSuruNoun = 50,
    Adjectival = 51,
    NaAdjectiveLike = 52,
    Name = 53,
    Letter = 54,
    PlaceName = 55,
    TaruAdjective = 56,
}

public static class PartOfSpeechExtension
{
    /// <summary>
    /// Converts a POS string (Sudachi or JMDict) to PartOfSpeech enum.
    /// Delegates to PosMapper.FromAny() for unified conversion logic.
    /// </summary>
    public static PartOfSpeech ToPartOfSpeech(this string pos) => PosMapper.FromAny(pos);

    public static List<PartOfSpeech> ToPartOfSpeech(this List<string> pos)
    {
        return pos.Select(p => p.ToPartOfSpeech()).ToList();
    }

    public static List<PartOfSpeechSection> ToPartOfSpeechSection(this List<string> pos)
    {
        return pos.Select(p => p.ToPartOfSpeechSection()).ToList();
    }

    public static PartOfSpeechSection ToPartOfSpeechSection(this string pos)
    {
        return pos switch
        {
            "*" => PartOfSpeechSection.None,
            "数" => PartOfSpeechSection.Amount,
            "アルファベット" => PartOfSpeechSection.Alphabet,
            "句点" => PartOfSpeechSection.FullStop,
            "空白" => PartOfSpeechSection.BlankSpace,
            "接尾" or "suf" => PartOfSpeechSection.Suffix,
            "代名詞" or "pn" => PartOfSpeechSection.Pronoun,
            "自立" => PartOfSpeechSection.Independant,
            "フィラー" => PartOfSpeechSection.Filler,
            "一般" => PartOfSpeechSection.Common,
            "非自立" => PartOfSpeechSection.Dependant,
            "終助詞" => PartOfSpeechSection.SentenceEndingParticle,
            "助数詞" or "ctr" => PartOfSpeechSection.Counter,
            "並立助詞" => PartOfSpeechSection.ParallelMarker,
            "係助詞" => PartOfSpeechSection.BindingParticle,
            "副詞可能" => PartOfSpeechSection.PotentialAdverb,
            "格助詞" => PartOfSpeechSection.CaseMarkingParticle,
            "サ変接続" => PartOfSpeechSection.IrregularConjunction,
            "接続助詞" => PartOfSpeechSection.ConjunctionParticle,
            "助動詞語幹" => PartOfSpeechSection.AuxiliaryVerbStem,
            "形容動詞語幹" => PartOfSpeechSection.AdjectivalStem,
            "連語" => PartOfSpeechSection.CompoundWord,
            "引用" => PartOfSpeechSection.Quotation,
            "名詞接続" => PartOfSpeechSection.NounConjunction,
            "副助詞" => PartOfSpeechSection.AdverbialParticle,
            "助詞類接続" => PartOfSpeechSection.ConjunctiveParticleClass,
            "副詞化" => PartOfSpeechSection.Adverbialization,
            "副助詞／並立助詞／終助詞" => PartOfSpeechSection.AdverbialParticleOrParallelMarkerOrSentenceEndingParticle,
            "連体化" => PartOfSpeechSection.AdnominalAdjective,
            "固有名詞" => PartOfSpeechSection.ProperNoun,
            "特殊" => PartOfSpeechSection.Special,
            "動詞接続" => PartOfSpeechSection.VerbConjunction,
            "人名" => PartOfSpeechSection.PersonName,
            "姓" => PartOfSpeechSection.FamilyName,
            "組織" => PartOfSpeechSection.Organization,
            "ナイ形容詞語幹" => PartOfSpeechSection.NotAdjectiveStem,
            "読点" => PartOfSpeechSection.Comma,
            "括弧開" => PartOfSpeechSection.OpeningBracket,
            "括弧閉" => PartOfSpeechSection.ClosingBracket,
            "地域" => PartOfSpeechSection.Region,
            "国" => PartOfSpeechSection.Country,
            "数詞" or "num" => PartOfSpeechSection.Numeral,
            "非自立可能" => PartOfSpeechSection.PossibleDependant,
            "普通名詞" => PartOfSpeechSection.CommonNoun,
            "名詞的" => PartOfSpeechSection.SubstantiveAdjective,
            "助数詞可能" => PartOfSpeechSection.PossibleCounterWord,
            "サ変可能" => PartOfSpeechSection.PossibleSuru,
            "準体助詞" => PartOfSpeechSection.Juntaijoushi,
            "形状詞可能" => PartOfSpeechSection.PossibleNaAdjective,
            "動詞的" => PartOfSpeechSection.VerbLike,
            "サ変形状詞可能" => PartOfSpeechSection.PossibleVerbSuruNoun,
            "形容詞的" => PartOfSpeechSection.Adjectival,
            "名"  or "company" or "given" or "place" or "person" or "product" or "ship" or "surname" or "unclass" or "name-fem" or "name-masc" or "station"
                or "group" or "char" or "creat" or "dei" or "doc" or "ev" or "fem" or "fict" or "leg" or "masc" or "myth" or "obj"
                or "organization" or "oth" or "relig" or "serv" or "ship" or "surname" or "work" or "unc" => PartOfSpeechSection.Name,
            "文字" => PartOfSpeechSection.Letter,
            "形状詞的" => PartOfSpeechSection.NaAdjectiveLike,
            "地名" => PartOfSpeechSection.PlaceName,
            "タリ" => PartOfSpeechSection.TaruAdjective,
            // _ => throw new ArgumentException($"Invalid part of speech section : {pos}")
            _ => PartOfSpeechSection.None
        };
    }
}