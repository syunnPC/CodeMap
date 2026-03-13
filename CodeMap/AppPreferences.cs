namespace CodeMap;

internal sealed class AppPreferences
{
    public const string SystemLocaleCode = "system";

    public string Locale { get; set; } = SystemLocaleCode;

    public string Theme { get; set; } = AppThemePreferenceResolver.SystemThemeCode;
}
