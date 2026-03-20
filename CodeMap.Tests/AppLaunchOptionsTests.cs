using CodeMap;
using Xunit;

namespace CodeMap.Tests;

public sealed class AppLaunchOptionsTests
{
    [Theory]
    [InlineData("--enable-console-debugging=true", true)]
    [InlineData("--enable-console-debugging=FALSE", false)]
    [InlineData("--enable-console-debugging=1", true)]
    [InlineData("--enable-console-debugging=0", false)]
    [InlineData("--enable-console-debugging=yes", true)]
    [InlineData("--ENABLE-CONSOLE-DEBUGGING=off", false)]
    public void Initialize_ParsesBooleanOptionTokens(string argument, bool expected)
    {
        AppLaunchOptions.Initialize([argument]);

        Assert.Equal(expected, AppLaunchOptions.Current.EnableConsoleDebugging);
    }

    [Fact]
    public void Initialize_InvalidTokenAndUnknownOption_UsesDefaultValue()
    {
        AppLaunchOptions.Initialize(["--other-option=true", "--enable-console-debugging=maybe"]);

        Assert.Equal(GetDefaultConsoleDebugging(), AppLaunchOptions.Current.EnableConsoleDebugging);
    }

    [Fact]
    public void Initialize_MultipleOptions_LastValidValueWins()
    {
        AppLaunchOptions.Initialize(
        [
            "--enable-console-debugging=false",
            "--enable-console-debugging=invalid",
            "--enable-console-debugging=on"
        ]);

        Assert.True(AppLaunchOptions.Current.EnableConsoleDebugging);
    }

    [Fact]
    public void Initialize_NullArgs_UsesDefaultValue()
    {
        AppLaunchOptions.Initialize(args: null);

        Assert.Equal(GetDefaultConsoleDebugging(), AppLaunchOptions.Current.EnableConsoleDebugging);
    }

    [Theory]
    [InlineData("--enable-performance-metrics=true", true, true)]
    [InlineData("--enable-performance-metrics=off", false, true)]
    [InlineData("--enable-native-layout=false", false, false)]
    [InlineData("--enable-native-layout=1", false, true)]
    public void Initialize_ParsesAdditionalBooleanFlags(
        string argument,
        bool expectedPerformanceMetrics,
        bool expectedNativeLayout)
    {
        AppLaunchOptions.Initialize([argument]);

        Assert.Equal(expectedPerformanceMetrics, AppLaunchOptions.Current.EnablePerformanceMetrics);
        Assert.Equal(expectedNativeLayout, AppLaunchOptions.Current.EnableNativeLayout);
    }

    [Fact]
    public void Initialize_MixedBooleanFlags_LastValidValueWinsPerOption()
    {
        AppLaunchOptions.Initialize(
        [
            "--enable-performance-metrics=true",
            "--enable-native-layout=false",
            "--enable-performance-metrics=invalid",
            "--enable-performance-metrics=off",
            "--enable-native-layout=on"
        ]);

        Assert.False(AppLaunchOptions.Current.EnablePerformanceMetrics);
        Assert.True(AppLaunchOptions.Current.EnableNativeLayout);
    }

    private static bool GetDefaultConsoleDebugging()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}
