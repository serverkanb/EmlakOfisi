using EmlakOfisi.BusinessLayer.Helpers;

namespace EmlakOfisi.Tests;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("0532 111 22 33", "5321112233")]
    [InlineData("+90 (532) 111-22-33", "5321112233")]
    [InlineData("905321112233", "5321112233")]
    [InlineData("5321112233", "5321112233")]
    [InlineData("0 532 111 22 33", "5321112233")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("abc", "")]
    public void Normalize(string? input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("0532", true)]
    [InlineData("111 22", true)]
    [InlineData("12", false)]      // çok kısa
    [InlineData("Ali", false)]
    [InlineData("Ali 532", false)] // harf var -> isim araması
    public void LooksLikePhone(string input, bool expected)
    {
        Assert.Equal(expected, PhoneNormalizer.LooksLikePhone(input));
    }

    [Theory]
    [InlineData("0532", "532")]
    [InlineData("+90 532 111 2233", "5321112233")]
    [InlineData("111 22", "11122")]
    public void NormalizeForSearch(string input, string expected)
    {
        Assert.Equal(expected, PhoneNormalizer.NormalizeForSearch(input));
    }

    [Fact]
    public void ToWhatsApp_AddsCountryCode()
    {
        Assert.Equal("905321112233", PhoneNormalizer.ToWhatsApp("5321112233"));
    }
}
