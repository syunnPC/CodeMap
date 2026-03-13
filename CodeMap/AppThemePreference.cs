namespace CodeMap;

internal enum AppThemePreference
{
    System = 0,
    Light = 1,
    Dark = 2
}

internal static class AppThemePreferenceResolver
{
    public const string SystemThemeCode = "system";

    public static AppThemePreference ResolvePreferredTheme(string? preference)
    {
        return preference?.Trim().ToLowerInvariant() switch
        {
            "light" => AppThemePreference.Light,
            "dark" => AppThemePreference.Dark,
            "default" or SystemThemeCode => AppThemePreference.System,
            _ => AppThemePreference.System
        };
    }

    public static string ToCode(AppThemePreference themePreference)
    {
        return themePreference switch
        {
            AppThemePreference.Light => "light",
            AppThemePreference.Dark => "dark",
            _ => SystemThemeCode
        };
    }
}
