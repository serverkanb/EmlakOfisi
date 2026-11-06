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
    [InlineData("5321112233", true)]
    [InlineData("0532 111 22 33", true)]
    [InlineData("905321112233", true)]
    [InlineData("+90 532 111 22 33", false)] // formda sadece rakam: "+" kabul edilmez
    [InlineData("0532a1112233", false)]      // harf var
    [InlineData("0532-111-22-33", false)]    // simge var
    [InlineData("0532 111 22 333", false)]  // 12 hane, 90 ile başlamıyor
    [InlineData("532 111 22 3", false)]     // 9 hane
    [InlineData("15321112233", false)]      // 11 hane, 0 ile başlamıyor
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid(string? input, bool expected)
    {
        Assert.Equal(expected, PhoneNormalizer.IsValid(input));
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
