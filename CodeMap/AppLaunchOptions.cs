using System;

namespace CodeMap;

internal sealed record AppLaunchOptions(bool EnableConsoleDebugging, bool EnablePerformanceMetrics, bool EnableNativeLayout)
{
    private const string EnableConsoleDebuggingOptionName = "enable-console-debugging";
    private const string EnablePerformanceMetricsOptionName = "enable-performance-metrics";
    private const string EnableNativeLayoutOptionName = "enable-native-layout";

    public static AppLaunchOptions Current { get; private set; } = CreateDefault();

    public static void Initialize(string[]? args)
    {
        Current = Parse(args);
    }

    private static AppLaunchOptions Parse(string[]? args)
    {
        bool enableConsoleDebugging = GetDefaultConsoleDebugging();
        bool enablePerformanceMetrics = false;
        bool enableNativeLayout = true;

        if (args is not null)
        {
            foreach (string argument in args)
            {
                if (!TryParseBooleanOption(argument, out string optionName, out bool parsedValue))
                {
                    continue;
                }

                switch (optionName)
                {
                    case EnableConsoleDebuggingOptionName:
                        enableConsoleDebugging = parsedValue;
                        break;
                    case EnablePerformanceMetricsOptionName:
                        enablePerformanceMetrics = parsedValue;
                        break;
                    case EnableNativeLayoutOptionName:
                        enableNativeLayout = parsedValue;
                        break;
                }
            }
        }

        return new AppLaunchOptions(enableConsoleDebugging, enablePerformanceMetrics, enableNativeLayout);
    }

    private static bool TryParseBooleanOption(string? argument, out string optionName, out bool value)
    {
        optionName = string.Empty;
        value = false;
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        string trimmedArgument = argument.Trim();
        if (!trimmedArgument.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        int separatorIndex = trimmedArgument.IndexOf('=');
        if (separatorIndex <= 2 || separatorIndex == trimmedArgument.Length - 1)
        {
            return false;
        }

        optionName = trimmedArgument[2..separatorIndex].Trim().ToLowerInvariant();
        string rawValue = trimmedArgument[(separatorIndex + 1)..].Trim();
        return TryParseBooleanToken(rawValue, out value);
    }

    private static bool GetDefaultConsoleDebugging()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static AppLaunchOptions CreateDefault()
    {
        return new AppLaunchOptions(GetDefaultConsoleDebugging(), false, true);
    }

    private static bool TryParseBooleanToken(string rawValue, out bool value)
    {
        if (bool.TryParse(rawValue, out bool parsed))
        {
            value = parsed;
            return true;
        }

        switch (rawValue.Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "on":
                value = true;
                return true;
            case "0":
            case "no":
            case "off":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }
}
