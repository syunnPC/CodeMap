using CodeMap.Analysis;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class AnalysisPathFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" \t ")]
    public void IsIgnoredDirectoryName_NullOrWhitespace_ReturnsFalse(string? name)
    {
        Assert.False(AnalysisPathFilter.IsIgnoredDirectoryName(name));
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData(".nuget")]
    [InlineData("bin")]
    [InlineData("Bin")]
    [InlineData("OBJ")]
    [InlineData("packages")]
    [InlineData("external")]
    [InlineData("externals")]
    [InlineData("vendors")]
    [InlineData("x64")]
    [InlineData("arm64")]
    [InlineData("Third_Party")]
    [InlineData("third-party")]
    [InlineData("VENDOR")]
    public void IsIgnoredDirectoryName_KnownNames_AreCaseInsensitive(string name)
    {
        Assert.True(AnalysisPathFilter.IsIgnoredDirectoryName(name));
    }

    [Theory]
    [InlineData("cmake-build")]
    [InlineData("cmake-build-debug")]
    [InlineData("CMAKE-BUILD-Release")]
    public void IsIgnoredDirectoryName_CmakeBuildPrefix_IsCaseInsensitive(string name)
    {
        Assert.True(AnalysisPathFilter.IsIgnoredDirectoryName(name));
    }

    [Theory]
    [InlineData("  bin  ")]
    [InlineData(" \tthird-party  ")]
    [InlineData("  .git")]
    public void IsIgnoredDirectoryName_TrimmedKnownName_ReturnsTrue(string name)
    {
        Assert.True(AnalysisPathFilter.IsIgnoredDirectoryName(name));
    }

    [Fact]
    public void IsIgnoredDirectoryName_UnknownName_ReturnsFalse()
    {
        Assert.False(AnalysisPathFilter.IsIgnoredDirectoryName("src"));
    }

    [Theory]
    [InlineData("cmake_build_debug")]
    [InlineData("vendorized")]
    [InlineData("package")]
    public void IsIgnoredDirectoryName_SimilarButUnknownName_ReturnsFalse(string name)
    {
        Assert.False(AnalysisPathFilter.IsIgnoredDirectoryName(name));
    }
}
