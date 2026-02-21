using FluentAssertions;
using Jiten.Parser;

namespace Jiten.Tests;

public class DeconjugatorTests
{
    [Theory]
    [InlineData("終わってしまった", "終わる")]
    [InlineData("わからない", "わかる")]
    [InlineData("みて", "みる")]
    [InlineData("作る", "作る")]
    [InlineData("なかった", "ない")]
    [InlineData("選びださねば", "選びだす")]
    public void Deconjugation_ShouldContainExpectedDictionaryForm(string text, string expectedBase)
    {
        var deconjugator = new Deconjugator();

        var forms = deconjugator.Deconjugate(text);

        forms.Select(f => f.Text).Should().Contain(expectedBase);
    }

    [Fact]
    public void Deconjugation_ShouldBeDeterministicForSameInput()
    {
        var deconjugator = new Deconjugator();
        const string input = "終わってしまった";

        var first = deconjugator.Deconjugate(input);
        var second = deconjugator.Deconjugate(input);

        first.Select(FormatForm).Should().Equal(second.Select(FormatForm));
    }

    [Fact]
    public void Deconjugation_ShouldNotContainDuplicateForms()
    {
        var deconjugator = new Deconjugator();
        const string input = "終わってしまった";

        var forms = deconjugator.Deconjugate(input);
        var keys = forms.Select(FormatForm).ToList();

        keys.Distinct(StringComparer.Ordinal).Count().Should().Be(keys.Count);
    }

    [Theory]
    [InlineData("終わってしまった", 200)]
    [InlineData("食べさせられなかった", 250)]
    [InlineData("読ませられなかった", 250)]
    public void Deconjugation_ShouldRemainBoundedForComplexInputs(string input, int maxForms)
    {
        var deconjugator = new Deconjugator();

        var forms = deconjugator.Deconjugate(input);

        forms.Count.Should().BeLessOrEqualTo(maxForms);
    }

    [Fact]
    public void Cache_ShouldEvict_WhenCapacityIsExceeded()
    {
        var deconjugator = new Deconjugator(maxCacheEntries: 3, evictionBatchSize: 1);
        deconjugator.ClearCacheForTesting();

        _ = deconjugator.Deconjugate("わからない");
        _ = deconjugator.Deconjugate("みて");
        _ = deconjugator.Deconjugate("なかった");
        _ = deconjugator.Deconjugate("終わってしまった");

        var stats = deconjugator.GetCacheStats();
        stats.Count.Should().BeLessOrEqualTo(3);
        stats.Evictions.Should().BeGreaterThan(0);
        stats.Stores.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Cache_ShouldTrackHitsAndMisses()
    {
        var deconjugator = new Deconjugator(maxCacheEntries: 10, evictionBatchSize: 2);
        deconjugator.ClearCacheForTesting();

        _ = deconjugator.Deconjugate("わからない"); // miss + store
        _ = deconjugator.Deconjugate("わからない"); // hit

        var stats = deconjugator.GetCacheStats();
        stats.Hits.Should().BeGreaterThan(0);
        stats.Misses.Should().BeGreaterThan(0);
    }

    private static string FormatForm(DeconjugationForm form)
    {
        var tags = string.Join(",", form.Tags);
        var process = string.Join(",", form.Process);
        var seen = string.Join(",", form.SeenText.OrderBy(x => x, StringComparer.Ordinal));
        return $"{form.Text}|{form.OriginalText}|{tags}|{process}|{seen}";
    }
}
