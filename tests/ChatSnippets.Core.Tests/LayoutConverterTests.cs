using ChatSnippets.Core;
using Xunit;

namespace ChatSnippets.Core.Tests;

public class LayoutConverterTests
{
    [Theory]
    [InlineData("ghbdtn", "привет")]
    [InlineData("Ghbdtn", "Привет")]
    [InlineData("ghbdtn vbh", "привет мир")]
    [InlineData("ghbdtn? vbh/", "привет, мир.")]
    [InlineData("rfr ltkf&", "как дела?")]
    [InlineData("[jhjij", "хорошо")]
    [InlineData("`kl", "ёлд")]
    public void LatinTypedOnRussianMeaning_BecomesRussian(string input, string expected) =>
        Assert.Equal(expected, LayoutConverter.Convert(input));

    [Theory]
    [InlineData("руддщ", "hello")]
    [InlineData("Руддщ цщкдв", "Hello world")]
    [InlineData("пше сщььше", "git commit")]
    public void CyrillicTypedOnEnglishMeaning_BecomesLatin(string input, string expected) =>
        Assert.Equal(expected, LayoutConverter.Convert(input));

    [Theory]
    [InlineData("123 456")]
    [InlineData("")]
    [InlineData("   ")]
    public void NoLetters_IsUnchanged(string input) =>
        Assert.Equal(input, LayoutConverter.Convert(input));

    [Fact]
    public void Digits_AndNewlines_ArePreserved() =>
        Assert.Equal("привет 123\nмир", LayoutConverter.Convert("ghbdtn 123\nvbh"));

    [Fact]
    public void RoundTrip_ReturnsOriginal()
    {
        const string text = "ghbdtn? vbh/ Rfr ltkf& [jhjij";
        var once = LayoutConverter.Convert(text);
        Assert.Equal(text, LayoutConverter.Convert(once, LayoutDirection.ToLatin));
    }

    [Fact]
    public void DetectsDirectionByMajorityOfLetters()
    {
        Assert.Equal(LayoutDirection.ToRussian, LayoutConverter.Detect("abc дe"));
        Assert.Equal(LayoutDirection.ToLatin, LayoutConverter.Detect("ab где"));
    }
}
