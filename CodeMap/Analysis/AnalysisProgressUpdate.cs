namespace CodeMap.Analysis;

public enum AnalysisProgressStage
{
    PreparingWorkspace = 0,
    LoadingManagedSolution,
    AnalyzingManagedProject,
    AnalyzingManagedDocument,
    AnalyzingFolderDocument,
    DiscoveringNativeProjects,
    AnalyzingNativeProject,
    AnalyzingNativeDocument,
    Finalizing
}

public sealed record AnalysisProgressUpdate(
    AnalysisProgressStage Stage,
    string WorkspacePath,
    string? ProjectName = null,
    string? DocumentPath = null,
    string? Detail = null);
