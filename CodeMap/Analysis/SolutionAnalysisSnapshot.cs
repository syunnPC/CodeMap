using System;
using System.Collections.Generic;
using System.IO;

namespace CodeMap.Analysis;

public sealed record SolutionAnalysisSnapshot(
    string WorkspacePath,
    string WorkspaceKind,
    DateTimeOffset AnalyzedAt,
    IReadOnlyList<ProjectAnalysisSummary> Projects,
    IReadOnlyList<DocumentDependencySummary> DocumentDependencies,
    IReadOnlyList<SymbolDependencySummary> SymbolDependencies,
    IReadOnlyList<DependencyCycleSummary> Cycles,
    IReadOnlyList<string> Diagnostics);

public sealed record ProjectAnalysisSummary(
    string Name,
    string Language,
    string? ProjectFilePath,
    string ProjectKey,
    bool IsFolderBased,
    IReadOnlyList<DocumentAnalysisSummary> Documents,
    IReadOnlyList<ProjectReferenceSummary> ProjectReferences,
    IReadOnlyList<string> MetadataReferences,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<NativeDependencySummary> NativeDependencies);

public sealed record ProjectReferenceSummary(
    string TargetProjectKey,
    string DisplayName);

public sealed record DocumentAnalysisSummary(
    string Id,
    string Name,
    string? FilePath,
    IReadOnlyList<SymbolAnalysisSummary> Symbols);

public sealed record SymbolAnalysisSummary(
    string Id,
    string Kind,
    string Name,
    string DisplayName,
    int LineNumber);

public sealed record DocumentDependencySummary(
    string SourceDocumentId,
    string TargetDocumentId,
    int ReferenceCount,
    string? SampleFilePath = null,
    int? SampleLineNumber = null,
    string? SampleSnippet = null);

public sealed record SymbolDependencySummary(
    string SourceSymbolId,
    string TargetSymbolId,
    int ReferenceCount,
    string ReferenceKind = "reference",
    string Confidence = "high",
    string? SampleFilePath = null,
    int? SampleLineNumber = null,
    string? SampleSnippet = null);

public sealed record DependencyCycleSummary(
    string GraphKind,
    string CycleId,
    IReadOnlyList<string> NodeIds,
    int EdgeCount);

public sealed record NativeDependencySummary(
    string LibraryName,
    string ImportKind,
    string Confidence,
    string? SourceDocumentId,
    string? SourceSymbolId,
    IReadOnlyList<string> ImportedSymbols);

public static class AnalysisIdentity
{
    public static string BuildProjectKey(string projectName, string? projectFilePath, string? fallbackIdentity = null)
    {
        if (!string.IsNullOrWhiteSpace(projectFilePath))
        {
            try
            {
                string fullPath = Path.GetFullPath(projectFilePath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return OperatingSystem.IsWindows()
                    ? fullPath.ToUpperInvariant()
                    : fullPath;
            }
            catch
            {
                return projectFilePath;
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackIdentity))
        {
            return $"id:{fallbackIdentity.Trim()}";
        }

        string normalizedProjectName = string.IsNullOrWhiteSpace(projectName)
            ? "project"
            : projectName.Trim();
        return $"name:{normalizedProjectName}";
    }
}
