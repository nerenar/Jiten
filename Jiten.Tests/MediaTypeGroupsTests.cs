using FluentAssertions;
using Jiten.Core.Data;

namespace Jiten.Tests;

public class MediaTypeGroupsTests
{
    [Fact]
    public void AreComparable_SameType_ReturnsTrue()
    {
        MediaTypeGroups.AreComparable(MediaType.Novel, MediaType.Novel).Should().BeTrue();
    }

    [Fact]
    public void AreComparable_SameGroup_ReturnsTrue()
    {
        MediaTypeGroups.AreComparable(MediaType.Novel, MediaType.WebNovel).Should().BeTrue();
    }

    [Fact]
    public void AreComparable_CrossTextGroups_ReturnsTrue()
    {
        MediaTypeGroups.AreComparable(MediaType.Manga, MediaType.Novel).Should().BeTrue();
    }

    [Fact]
    public void AreComparable_TextVsAudioVisual_ReturnsFalse()
    {
        MediaTypeGroups.AreComparable(MediaType.Novel, MediaType.Anime).Should().BeFalse();
    }

    [Fact]
    public void AreComparable_AudioVisualTypes_ReturnsTrue()
    {
        MediaTypeGroups.AreComparable(MediaType.Anime, MediaType.Drama).Should().BeTrue();
    }

    [Fact]
    public void GetComparisonWeight_SameType_Returns1()
    {
        MediaTypeGroups.GetComparisonWeight(MediaType.Anime, MediaType.Anime).Should().Be(1.0m);
    }

    [Fact]
    public void GetComparisonWeight_SameGroupDifferentType_Returns07()
    {
        MediaTypeGroups.GetComparisonWeight(MediaType.Novel, MediaType.WebNovel).Should().Be(0.7m);
    }

    [Fact]
    public void GetComparisonWeight_AudioWithOtherAV_Returns03()
    {
        MediaTypeGroups.GetComparisonWeight(MediaType.Audio, MediaType.Anime).Should().Be(0.3m);
    }

    [Fact]
    public void GetComparisonWeight_CrossTextGroups_Returns05()
    {
        MediaTypeGroups.GetComparisonWeight(MediaType.Manga, MediaType.Novel).Should().Be(0.5m);
    }

    [Fact]
    public void GetComparisonWeight_NonFictionCross_Returns03()
    {
        MediaTypeGroups.GetComparisonWeight(MediaType.NonFiction, MediaType.Novel).Should().Be(0.3m);
    }

    [Fact]
    public void GetComparisonWeight_TextVsAV_Returns0()
    {
        MediaTypeGroups.GetComparisonWeight(MediaType.Novel, MediaType.Anime).Should().Be(0m);
    }
}
