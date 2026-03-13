using CodeMap;
using Xunit;

namespace CodeMap.Tests;

public sealed class AppLocalizationTests
{
    [Theory]
    [InlineData("ja", "ja")]
    [InlineData("japanese", "ja")]
    [InlineData("en", "en")]
    [InlineData("english", "en")]
    public void ResolvePreferredLocale_KnownCodes_ReturnsExpectedLocale(string preference, string expectedCode)
    {
        AppLocale resolved = AppLocalization.ResolvePreferredLocale(preference);
        Assert.Equal(expectedCode, AppLocalization.ToCode(resolved));
    }

    [Fact]
    public void ResolvePreferredLocale_UnknownCode_FallsBackToSystemLocale()
    {
        AppLocale expected = AppLocalization.ResolveSystemLocale();

        Assert.Equal(expected, AppLocalization.ResolvePreferredLocale("xx-unknown"));
    }

    [Fact]
    public void ToCode_MapsLocaleToExpectedCode()
    {
        Assert.Equal("ja", AppLocalization.ToCode(AppLocale.Japanese));
        Assert.Equal("en", AppLocalization.ToCode(AppLocale.English));
    }

    [Fact]
    public void Get_MissingKey_ReturnsKey()
    {
        string value = AppLocalization.Get(AppLocale.English, "unknown.localization.key");

        Assert.Equal("unknown.localization.key", value);
    }

    [Fact]
    public void Get_WithArguments_FormatsString()
    {
        string value = AppLocalization.Get(AppLocale.English, "status.workspaceNotFound", @"C:\workspace");

        Assert.Contains(@"C:\workspace", value);
    }

    [Fact]
    public void GetSymbolKindLabel_ReturnsLocalizedLabels()
    {
        Assert.Equal("Class", AppLocalization.GetSymbolKindLabel(AppLocale.English, "ClassDeclaration"));
        Assert.Equal("クラス", AppLocalization.GetSymbolKindLabel(AppLocale.Japanese, "ClassDeclaration"));
        Assert.Equal("UnknownKind", AppLocalization.GetSymbolKindLabel(AppLocale.English, "UnknownKind"));
    }
}
