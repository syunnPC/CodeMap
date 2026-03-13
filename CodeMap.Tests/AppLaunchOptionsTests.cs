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

    private static bool GetDefaultConsoleDebugging()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}
