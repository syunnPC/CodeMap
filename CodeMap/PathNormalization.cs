using System;
using System.IO;

namespace CodeMap;

internal static class PathNormalization
{
    public static string NormalizeDirectoryLikePath(
        string? path,
        bool uppercaseWindows = false,
        bool catchAllExceptions = false)
    {
        string candidate = path?.Trim() ?? string.Empty;
        if (candidate.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            candidate = Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (catchAllExceptions || ex is ArgumentException)
        {
            return FinalizeNormalizedPath(candidate, uppercaseWindows);
        }

        return FinalizeNormalizedPath(candidate, uppercaseWindows);
    }

    private static string FinalizeNormalizedPath(string candidate, bool uppercaseWindows)
    {
        string root = Path.GetPathRoot(candidate) ?? string.Empty;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(candidate, root, comparison))
        {
            candidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return uppercaseWindows && OperatingSystem.IsWindows()
            ? candidate.ToUpperInvariant()
            : candidate;
    }
}
