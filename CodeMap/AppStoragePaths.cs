using System;
using System.IO;

namespace CodeMap;

internal static class AppStoragePaths
{
    private static readonly string s_localAppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeMap");

    public static string LocalAppDataRoot => s_localAppDataRoot;

    public static string LogDirectoryPath => Path.Combine(s_localAppDataRoot, "logs");

    public static string LogFilePath => Path.Combine(LogDirectoryPath, "codemap.log");

    public static string CaptureDirectoryPath => Path.Combine(LogDirectoryPath, "captures");

    public static string WebView2UserDataDirectoryPath => Path.Combine(s_localAppDataRoot, "webview2");

    public static string WebView2CleanupMarkerFilePath => Path.Combine(s_localAppDataRoot, "webview2-cleanup.pending");

    public static string RecentSolutionsFilePath => Path.Combine(s_localAppDataRoot, "recent-solutions.json");

    public static string SolutionViewStatesFilePath => Path.Combine(s_localAppDataRoot, "solution-view-states.json");

    public static string AppPreferencesFilePath => Path.Combine(s_localAppDataRoot, "app-preferences.json");

    public static string AnalysisCacheDatabasePath => Path.Combine(s_localAppDataRoot, "analysis-cache.db");
}
