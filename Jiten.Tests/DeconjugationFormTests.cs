using FluentAssertions;
using Jiten.Parser;
using Xunit;

namespace Jiten.Tests;

public class DeconjugationFormTests
{
    [Fact]
    public void Constructor_ShouldSnapshotInputCollections()
    {
        var tags = new List<string> { "tag-a" };
        var seen = new HashSet<string>(StringComparer.Ordinal) { "seen-a" };
        var process = new List<string> { "step-a" };

        var form = new DeconjugationForm("text", "original", tags, seen, process);

        tags.Add("tag-b");
        seen.Add("seen-b");
        process.Add("step-b");

        form.Tags.Should().Equal("tag-a");
        form.SeenText.Should().BeEquivalentTo(new[] { "seen-a" });
        form.Process.Should().Equal("step-a");
    }

    [Fact]
    public void Equality_ShouldTreatSeenTextAsSetAndKeepHashStable()
    {
        var first = new DeconjugationForm(
            "text",
            "original",
            ["tag-a"],
            ["seen-a", "seen-b"],
            ["step-a"]);

        var second = new DeconjugationForm(
            "text",
            "original",
            ["tag-a"],
            ["seen-b", "seen-a"],
            ["step-a"]);

        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }
}
