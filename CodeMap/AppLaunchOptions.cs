using System;

namespace CodeMap;

internal sealed record AppLaunchOptions(bool EnableConsoleDebugging, bool EnablePerformanceMetrics)
{
    private const string EnableConsoleDebuggingOptionName = "enable-console-debugging";
    private const string EnablePerformanceMetricsOptionName = "enable-performance-metrics";

    public static AppLaunchOptions Current { get; private set; } = CreateDefault();

    public static void Initialize(string[]? args)
    {
        Current = Parse(args);
    }

    private static AppLaunchOptions Parse(string[]? args)
    {
        bool enableConsoleDebugging = GetDefaultConsoleDebugging();
        bool enablePerformanceMetrics = false;

        if (args is not null)
        {
            foreach (string argument in args)
            {
                if (TryParseBooleanOption(argument, EnableConsoleDebuggingOptionName, out bool parsedValue))
                {
                    enableConsoleDebugging = parsedValue;
                }

                if (TryParseBooleanOption(argument, EnablePerformanceMetricsOptionName, out parsedValue))
                {
                    enablePerformanceMetrics = parsedValue;
                }
            }
        }

        return new AppLaunchOptions(enableConsoleDebugging, enablePerformanceMetrics);
    }

    private static bool TryParseBooleanOption(string? argument, string optionName, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        string prefix = $"--{optionName}=";
        if (!argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rawValue = argument[prefix.Length..].Trim();
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
        return new AppLaunchOptions(GetDefaultConsoleDebugging(), false);
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
