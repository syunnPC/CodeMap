using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CodeMap;

internal enum AppLocale
{
    Japanese = 0,
    English = 1
}

internal static class AppLocalization
{
    private const string LocalizationDirectoryName = "Localization";
    private static AppLocale s_currentLocale = ResolveSystemLocale();

    private static readonly Lazy<IReadOnlyDictionary<string, string>> s_japaneseTable = new(
        () => LoadLocaleTable(AppLocale.Japanese));

    private static readonly Lazy<IReadOnlyDictionary<string, string>> s_englishTable = new(
        () => LoadLocaleTable(AppLocale.English));

    private static readonly IReadOnlyDictionary<string, string> s_englishSymbolKindLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ClassDeclaration"] = "Class",
        ["StructDeclaration"] = "Struct",
        ["InterfaceDeclaration"] = "Interface",
        ["EnumDeclaration"] = "Enum",
        ["UnionDeclaration"] = "Union",
        ["RecordDeclaration"] = "Record",
        ["MethodDeclaration"] = "Method",
        ["FunctionDeclaration"] = "Function",
        ["ConstructorDeclaration"] = "Constructor",
        ["PropertyDeclaration"] = "Property",
        ["IndexerDeclaration"] = "Indexer",
        ["FieldDeclaration"] = "Field",
        ["EventDeclaration"] = "Event",
        ["DelegateDeclaration"] = "Delegate",
        ["TypeAliasDeclaration"] = "Type Alias",
        ["MacroDefinition"] = "Macro",
        ["MacroDefinitions"] = "Macro"
    };

    private static readonly IReadOnlyDictionary<string, string> s_japaneseSymbolKindLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ClassDeclaration"] = "クラス",
        ["StructDeclaration"] = "構造体",
        ["InterfaceDeclaration"] = "インターフェイス",
        ["EnumDeclaration"] = "列挙型",
        ["UnionDeclaration"] = "共用体",
        ["RecordDeclaration"] = "レコード",
        ["MethodDeclaration"] = "メソッド",
        ["FunctionDeclaration"] = "関数",
        ["ConstructorDeclaration"] = "コンストラクター",
        ["PropertyDeclaration"] = "プロパティ",
        ["IndexerDeclaration"] = "インデクサー",
        ["FieldDeclaration"] = "フィールド",
        ["EventDeclaration"] = "イベント",
        ["DelegateDeclaration"] = "デリゲート",
        ["TypeAliasDeclaration"] = "型エイリアス",
        ["MacroDefinition"] = "マクロ",
        ["MacroDefinitions"] = "マクロ"
    };

    public static AppLocale ResolveSystemLocale()
    {
        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase)
            ? AppLocale.Japanese
            : AppLocale.English;
    }

    public static AppLocale ResolvePreferredLocale(string? preference)
    {
        return preference?.Trim().ToLowerInvariant() switch
        {
            "ja" or "japanese" => AppLocale.Japanese,
            "en" or "english" => AppLocale.English,
            _ => ResolveSystemLocale()
        };
    }

    public static string ToCode(AppLocale locale)
    {
        return locale == AppLocale.English ? "en" : "ja";
    }

    public static void SetCurrentLocale(AppLocale locale)
    {
        s_currentLocale = locale;
    }

    public static string GetSymbolKindLabel(string kind)
    {
        return GetSymbolKindLabel(s_currentLocale, kind);
    }

    public static string GetSymbolKindLabel(AppLocale locale, string kind)
    {
        IReadOnlyDictionary<string, string> labels = locale == AppLocale.English
            ? s_englishSymbolKindLabels
            : s_japaneseSymbolKindLabels;
        return labels.TryGetValue(kind, out string? localized)
            ? localized
            : kind;
    }

    public static string Get(AppLocale locale, string key, params object[] args)
    {
        IReadOnlyDictionary<string, string> table = GetTable(locale);
        if (!table.TryGetValue(key, out string? value))
        {
            value = s_japaneseTable.Value.TryGetValue(key, out string? fallback)
                ? fallback
                : key;
        }

        if (args.Length == 0)
        {
            return value;
        }

        return string.Format(CultureInfo.InvariantCulture, value, args);
    }

    public static string Get(string key, params object[] args)
    {
        return Get(s_currentLocale, key, args);
    }

    private static IReadOnlyDictionary<string, string> GetTable(AppLocale locale)
    {
        return locale == AppLocale.English
            ? s_englishTable.Value
            : s_japaneseTable.Value;
    }

    private static IReadOnlyDictionary<string, string> LoadLocaleTable(AppLocale locale)
    {
        string localeCode = ToCode(locale);
        string fileName = $"{localeCode}.json";
        HashSet<string> inspectedPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string candidatePath in EnumerateCandidatePaths(fileName))
        {
            string normalizedPath = Path.GetFullPath(candidatePath);
            if (!inspectedPaths.Add(normalizedPath) || !File.Exists(normalizedPath))
            {
                continue;
            }

            return ParseLocaleTable(normalizedPath, localeCode);
        }

        throw new FileNotFoundException(
            $"Localization resource file '{fileName}' was not found under '{LocalizationDirectoryName}'.");
    }

    private static IReadOnlyDictionary<string, string> ParseLocaleTable(string filePath, string localeCode)
    {
        using FileStream stream = File.OpenRead(filePath);
        Dictionary<string, string?>? raw = JsonSerializer.Deserialize<Dictionary<string, string?>>(stream);
        if (raw is null || raw.Count == 0)
        {
            throw new InvalidDataException(
                $"Localization resource '{filePath}' for locale '{localeCode}' is empty or invalid.");
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach ((string key, string? value) in raw)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            normalized[key] = value ?? string.Empty;
        }

        if (normalized.Count == 0)
        {
            throw new InvalidDataException(
                $"Localization resource '{filePath}' for locale '{localeCode}' does not contain valid entries.");
        }

        return normalized;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string fileName)
    {
        yield return Path.Combine(AppContext.BaseDirectory, LocalizationDirectoryName, fileName);
        yield return Path.Combine(Environment.CurrentDirectory, LocalizationDirectoryName, fileName);
    }
}
