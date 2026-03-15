using System;
using System.Collections.Generic;

namespace CodeMap.ViewModels;

internal sealed class SolutionViewState
{
    public ExplorerViewState Explorer { get; set; } = new();

    public GraphViewState Graph { get; set; } = new();
}

internal sealed record ExplorerViewState
{
    public double ExplorerPanelWidth { get; init; } = 300;

    public bool IsWorkspaceSplit { get; init; }

    public double WorkspaceSplitRatio { get; init; } = 0.5;

    public string ActiveWorkspaceTab { get; init; } = "tree";
}

internal sealed record GraphViewState
{
    public bool IncludeProjects { get; init; } = true;

    public bool IncludeDocuments { get; init; } = true;

    public bool IncludePackages { get; init; } = true;

    public bool IncludeSymbols { get; init; }

    public bool IncludeAssemblies { get; init; } = true;

    public bool IncludeNativeDependencies { get; init; } = true;

    public bool IncludeDocumentDependencies { get; init; } = true;

    public bool IncludeSymbolDependencies { get; init; } = true;

    public bool IsDependencyMapMode { get; init; }

    public bool IsImpactAnalysisMode { get; init; }

    public bool ShowCyclesOnly { get; init; }

    public string DependencyMapDirection { get; init; } = "both";

    public int PanelWidth { get; init; } = 320;

    public int MobilePanelHeight { get; init; } = 320;

    public IReadOnlyList<PinnedNodeViewState> PinnedNodes { get; init; } = Array.Empty<PinnedNodeViewState>();

    public IReadOnlyList<HiddenNodeViewState> HiddenNodes { get; init; } = Array.Empty<HiddenNodeViewState>();
}

internal sealed record PinnedNodeViewState(
    string NodeId,
    double X,
    double Y);

internal sealed record HiddenNodeViewState(
    string NodeId,
    string Label,
    string Group);

internal sealed record NavigationGraphState(
    bool IncludeProjects,
    bool IncludeDocuments,
    bool IncludePackages,
    bool IncludeSymbols,
    bool IncludeAssemblies,
    bool IncludeNativeDependencies,
    bool IncludeDocumentDependencies,
    bool IncludeSymbolDependencies,
    bool IsDependencyMapMode,
    bool IsImpactAnalysisMode,
    bool ShowCyclesOnly,
    string DependencyMapDirection,
    IReadOnlyList<HiddenNodeViewState> HiddenNodes);

internal sealed record ViewNavigationState(
    string WorkspacePath,
    ExplorerViewState Explorer,
    NavigationGraphState Graph,
    GraphSelectionTarget? SelectionTarget);

internal sealed record GraphSelectionTarget(
    string NodeId,
    string DisplayLabel);

internal sealed record WorkspaceTreeNodeContent(
    string Text,
    GraphSelectionTarget? SelectionTarget)
{
    public override string ToString()
    {
        return Text;
    }
}

internal static class ViewStateComparer
{
    public static bool ReadOnlyListEqual<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        where T : IEquatable<T>
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}
