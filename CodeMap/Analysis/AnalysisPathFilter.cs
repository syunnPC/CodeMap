using System;
using System.Collections.Generic;

namespace CodeMap.Analysis;

internal static class AnalysisPathFilter
{
    private static readonly HashSet<string> s_ignoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".vs",
        ".nuget",
        "bin",
        "obj",
        "debug",
        "release",
        "x64",
        "x86",
        "arm64",
        "node_modules",
        "packages",
        "third_party",
        "third-party",
        "external",
        "externals",
        "vendor",
        "vendors"
    };

    public static bool IsIgnoredDirectoryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalized = name.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        return s_ignoredDirectoryNames.Contains(normalized) ||
            normalized.StartsWith("cmake-build", StringComparison.OrdinalIgnoreCase);
    }
}
