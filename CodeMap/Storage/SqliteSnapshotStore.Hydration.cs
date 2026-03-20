using CodeMap.Analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeMap.Storage;

public sealed partial class SqliteSnapshotStore
{
    private static Dictionary<string, List<CachedDocumentRecord>> BuildDocumentsByProject(
        IReadOnlyList<CachedDocumentRecord> documentRecords)
    {
        return BuildLookup(
            documentRecords,
            static record => record.ProjectKey,
            static projectDocuments => projectDocuments.Sort(static (left, right) =>
            {
                int nameCompare = StringComparer.OrdinalIgnoreCase.Compare(left.DocumentName, right.DocumentName);
                return nameCompare != 0
                    ? nameCompare
                    : StringComparer.OrdinalIgnoreCase.Compare(left.DocumentFilePath, right.DocumentFilePath);
            }));
    }

    private static Dictionary<string, List<CachedSymbolRecord>> BuildSymbolsByDocumentId(
        IReadOnlyList<CachedSymbolRecord> symbolRecords)
    {
        return BuildLookup(
            symbolRecords,
            static record => record.DocumentId,
            static documentSymbols => documentSymbols.Sort(static (left, right) =>
            {
                int lineCompare = left.LineNumber.CompareTo(right.LineNumber);
                return lineCompare != 0
                    ? lineCompare
                    : StringComparer.OrdinalIgnoreCase.Compare(left.SymbolDisplayName, right.SymbolDisplayName);
            }));
    }

    private static Dictionary<string, List<CachedDependencyRecord>> BuildDependenciesByProject(
        IReadOnlyList<CachedDependencyRecord> dependencyRecords)
    {
        return BuildLookup(dependencyRecords, static record => record.ProjectKey);
    }

    private static IReadOnlyList<ProjectReferenceSummary> BuildProjectReferences(
        IReadOnlyList<CachedDependencyRecord>? projectDependencies)
    {
        if (projectDependencies is null || projectDependencies.Count == 0)
        {
            return Array.Empty<ProjectReferenceSummary>();
        }

        Dictionary<string, ProjectReferenceSummary> referencesByTarget = new(StringComparer.OrdinalIgnoreCase);
        foreach (CachedDependencyRecord dependency in projectDependencies)
        {
            if (!string.Equals(dependency.DependencyKind, "project", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(dependency.DependencyName))
            {
                continue;
            }

            string targetProjectKey = dependency.DependencyName;
            string displayName = ResolveProjectReferenceDisplayName(targetProjectKey, dependency.DependencyOrigin);
            ProjectReferenceSummary candidate = new(targetProjectKey, displayName);

            if (!referencesByTarget.TryGetValue(targetProjectKey, out ProjectReferenceSummary? current))
            {
                referencesByTarget.Add(targetProjectKey, candidate);
                continue;
            }

            if (StringComparer.OrdinalIgnoreCase.Compare(candidate.DisplayName, current.DisplayName) < 0)
            {
                referencesByTarget[targetProjectKey] = candidate;
            }
        }

        if (referencesByTarget.Count == 0)
        {
            return Array.Empty<ProjectReferenceSummary>();
        }

        List<ProjectReferenceSummary> references = [.. referencesByTarget.Values];
        references.Sort(static (left, right) =>
        {
            int displayCompare = StringComparer.OrdinalIgnoreCase.Compare(left.DisplayName, right.DisplayName);
            return displayCompare != 0
                ? displayCompare
                : StringComparer.OrdinalIgnoreCase.Compare(left.TargetProjectKey, right.TargetProjectKey);
        });

        return [.. references];
    }

    private static IReadOnlyList<string> BuildDistinctDependencyNames(
        IReadOnlyList<CachedDependencyRecord>? projectDependencies,
        string dependencyKind)
    {
        if (projectDependencies is null || projectDependencies.Count == 0)
        {
            return Array.Empty<string>();
        }

        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        List<string> dependencyNames = [];
        foreach (CachedDependencyRecord dependency in projectDependencies)
        {
            if (!string.Equals(dependency.DependencyKind, dependencyKind, StringComparison.OrdinalIgnoreCase) ||
                !seenNames.Add(dependency.DependencyName))
            {
                continue;
            }

            dependencyNames.Add(dependency.DependencyName);
        }

        if (dependencyNames.Count == 0)
        {
            return Array.Empty<string>();
        }

        dependencyNames.Sort(StringComparer.OrdinalIgnoreCase);
        return [.. dependencyNames];
    }

    private static IReadOnlyList<NativeDependencySummary> BuildNativeDependencies(
        IReadOnlyList<CachedDependencyRecord>? projectDependencies)
    {
        if (projectDependencies is null || projectDependencies.Count == 0)
        {
            return Array.Empty<NativeDependencySummary>();
        }

        List<NativeDependencySummary> dependencies = [];
        foreach (CachedDependencyRecord dependency in projectDependencies)
        {
            if (!string.Equals(dependency.DependencyKind, "dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            dependencies.Add(new NativeDependencySummary(
                dependency.DependencyName,
                string.IsNullOrWhiteSpace(dependency.DependencyOrigin) ? "unknown" : dependency.DependencyOrigin,
                string.IsNullOrWhiteSpace(dependency.Confidence) ? "high" : dependency.Confidence,
                SourceDocumentId: null,
                SourceSymbolId: null,
                ParseImportedSymbols(dependency.ImportedSymbols)));
        }

        if (dependencies.Count == 0)
        {
            return Array.Empty<NativeDependencySummary>();
        }

        dependencies.Sort(static (left, right) =>
        {
            int nameCompare = StringComparer.OrdinalIgnoreCase.Compare(left.LibraryName, right.LibraryName);
            return nameCompare != 0
                ? nameCompare
                : StringComparer.OrdinalIgnoreCase.Compare(left.ImportKind, right.ImportKind);
        });

        return [.. dependencies];
    }

    private static IReadOnlyList<string> ParseImportedSymbols(string? importedSymbols)
    {
        if (string.IsNullOrWhiteSpace(importedSymbols))
        {
            return Array.Empty<string>();
        }

        return importedSymbols
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveProjectReferenceDisplayName(string targetProjectKey, string? storedDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(storedDisplayName))
        {
            return storedDisplayName;
        }

        if (string.IsNullOrWhiteSpace(targetProjectKey))
        {
            return string.Empty;
        }

        if (targetProjectKey.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            return targetProjectKey["name:".Length..];
        }

        if (targetProjectKey.StartsWith("id:", StringComparison.OrdinalIgnoreCase))
        {
            return targetProjectKey["id:".Length..];
        }

        string fileName = Path.GetFileNameWithoutExtension(targetProjectKey);
        return string.IsNullOrWhiteSpace(fileName)
            ? targetProjectKey
            : fileName;
    }

    private static Dictionary<string, List<TRecord>> BuildLookup<TRecord>(
        IReadOnlyList<TRecord> records,
        Func<TRecord, string> keySelector,
        Action<List<TRecord>>? sort = null)
    {
        Dictionary<string, List<TRecord>> lookup = new(StringComparer.Ordinal);
        foreach (TRecord record in records)
        {
            string key = keySelector(record);
            if (!lookup.TryGetValue(key, out List<TRecord>? values))
            {
                values = [];
                lookup.Add(key, values);
            }

            values.Add(record);
        }

        if (sort is null)
        {
            return lookup;
        }

        foreach (List<TRecord> values in lookup.Values)
        {
            sort(values);
        }

        return lookup;
    }
}
