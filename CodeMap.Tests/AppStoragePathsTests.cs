using System;
using System.IO;
using CodeMap;
using Xunit;

namespace CodeMap.Tests;

public sealed class AppStoragePathsTests
{
    [Fact]
    public void LocalAppDataRoot_IsCodeMapDirectoryUnderLocalAppData()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeMap");

        Assert.Equal(expected, AppStoragePaths.LocalAppDataRoot);
    }

    [Fact]
    public void DerivedPaths_AreComposedFromLocalRoot()
    {
        string root = AppStoragePaths.LocalAppDataRoot;

        Assert.Equal(Path.Combine(root, "logs"), AppStoragePaths.LogDirectoryPath);
        Assert.Equal(Path.Combine(root, "logs", "codemap.log"), AppStoragePaths.LogFilePath);
        Assert.Equal(Path.Combine(root, "logs", "captures"), AppStoragePaths.CaptureDirectoryPath);
        Assert.Equal(Path.Combine(root, "webview2"), AppStoragePaths.WebView2UserDataDirectoryPath);
        Assert.Equal(Path.Combine(root, "webview2-cleanup.pending"), AppStoragePaths.WebView2CleanupMarkerFilePath);
        Assert.Equal(Path.Combine(root, "recent-solutions.json"), AppStoragePaths.RecentSolutionsFilePath);
        Assert.Equal(Path.Combine(root, "solution-view-states.json"), AppStoragePaths.SolutionViewStatesFilePath);
        Assert.Equal(Path.Combine(root, "app-preferences.json"), AppStoragePaths.AppPreferencesFilePath);
        Assert.Equal(Path.Combine(root, "analysis-cache.db"), AppStoragePaths.AnalysisCacheDatabasePath);
    }

    [Fact]
    public void AllStoragePaths_AreAbsolute()
    {
        string[] paths =
        [
            AppStoragePaths.LocalAppDataRoot,
            AppStoragePaths.LogDirectoryPath,
            AppStoragePaths.LogFilePath,
            AppStoragePaths.CaptureDirectoryPath,
            AppStoragePaths.WebView2UserDataDirectoryPath,
            AppStoragePaths.WebView2CleanupMarkerFilePath,
            AppStoragePaths.RecentSolutionsFilePath,
            AppStoragePaths.SolutionViewStatesFilePath,
            AppStoragePaths.AppPreferencesFilePath,
            AppStoragePaths.AnalysisCacheDatabasePath
        ];

        foreach (string path in paths)
        {
            Assert.True(Path.IsPathRooted(path), $"Path should be absolute: {path}");
        }
    }
}
