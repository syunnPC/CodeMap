using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeMap.ViewModels;

namespace CodeMap;

public sealed partial class MainWindow
{
    private void RememberRecentSolution(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(solutionPath);
        }
        catch
        {
            normalizedPath = solutionPath.Trim();
        }

        for (int index = _recentSolutions.Count - 1; index >= 0; index--)
        {
            if (string.Equals(_recentSolutions[index], normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                _recentSolutions.RemoveAt(index);
            }
        }

        _recentSolutions.Insert(0, normalizedPath);
        while (_recentSolutions.Count > MaxRecentSolutions)
        {
            _recentSolutions.RemoveAt(_recentSolutions.Count - 1);
        }

        SaveRecentSolutions();
        RefreshBrowseSolutionOptions();
        SetBrowseSolutionSelection(normalizedPath);
    }

    private void PersistActiveExplorerViewState()
    {
        if (_isApplyingPersistedViewState)
        {
            return;
        }

        SolutionViewState? activeState = GetOrCreateActiveSolutionViewState();
        if (activeState is null)
        {
            return;
        }

        ExplorerViewState nextState = NormalizeExplorerViewState(new ExplorerViewState
        {
            ExplorerPanelWidth = _explorerPanelWidth,
            IsWorkspaceSplit = _isWorkspaceSplit,
            WorkspaceSplitRatio = _workspaceSplitRatio,
            ActiveWorkspaceTab = SymbolTabButton.IsChecked == true ? "symbol" : "tree"
        });

        if (activeState.Explorer == nextState)
        {
            return;
        }

        activeState.Explorer = nextState;
        SaveSolutionViewStates();
        TryRecordNavigationState();
    }

    private void PersistActiveGraphViewState(GraphViewState graphViewState)
    {
        SolutionViewState? activeState = GetOrCreateActiveSolutionViewState();
        if (activeState is null)
        {
            return;
        }

        GraphViewState normalizedState = NormalizeGraphViewState(graphViewState);
        if (GraphViewStatesEqual(activeState.Graph, normalizedState))
        {
            return;
        }

        bool hiddenNodesChanged = !ViewStateComparer.ReadOnlyListEqual(activeState.Graph.HiddenNodes, normalizedState.HiddenNodes);
        activeState.Graph = normalizedState;
        SaveSolutionViewStates();
        if (hiddenNodesChanged)
        {
            RefreshExplorerContent();
        }

        TryRecordNavigationState();
    }

    private void ApplyExplorerViewState(ExplorerViewState explorerState)
    {
        ExplorerViewState normalizedState = NormalizeExplorerViewState(explorerState);
        _isApplyingPersistedViewState = true;
        try
        {
            _explorerPanelWidth = normalizedState.ExplorerPanelWidth;
            _workspaceSplitRatio = normalizedState.WorkspaceSplitRatio;
            _isWorkspaceSplit = normalizedState.IsWorkspaceSplit;
            bool showTreeTab = !string.Equals(normalizedState.ActiveWorkspaceTab, "symbol", StringComparison.OrdinalIgnoreCase);
            TreeTabButton.IsChecked = showTreeTab;
            SymbolTabButton.IsChecked = !showTreeTab;
            SplitToggleButton.IsChecked = _isWorkspaceSplit;
            EnsureExplorerPanelWidthWithinBounds();
            UpdateWorkspaceLayout();
        }
        finally
        {
            _isApplyingPersistedViewState = false;
        }
    }

    private SolutionViewState? GetOrCreateActiveSolutionViewState()
    {
        if (string.IsNullOrWhiteSpace(_activeSolutionPath))
        {
            return null;
        }

        if (_solutionViewStates.TryGetValue(_activeSolutionPath, out SolutionViewState? existingState))
        {
            return existingState;
        }

        SolutionViewState createdState = CreateDefaultSolutionViewState();
        _solutionViewStates[_activeSolutionPath] = createdState;
        return createdState;
    }

    private static SolutionViewState CreateDefaultSolutionViewState()
    {
        return new SolutionViewState
        {
            Explorer = NormalizeExplorerViewState(new ExplorerViewState()),
            Graph = NormalizeGraphViewState(new GraphViewState())
        };
    }

    private static SolutionViewState NormalizeSolutionViewState(SolutionViewState? state)
    {
        SolutionViewState fallback = CreateDefaultSolutionViewState();
        if (state is null)
        {
            return fallback;
        }

        return new SolutionViewState
        {
            Explorer = NormalizeExplorerViewState(state.Explorer),
            Graph = NormalizeGraphViewState(state.Graph)
        };
    }

    private static ExplorerViewState NormalizeExplorerViewState(ExplorerViewState? explorerState)
    {
        ExplorerViewState state = explorerState ?? new ExplorerViewState();
        string activeTab = string.Equals(state.ActiveWorkspaceTab, "symbol", StringComparison.OrdinalIgnoreCase)
            ? "symbol"
            : "tree";
        double panelWidth = double.IsFinite(state.ExplorerPanelWidth)
            ? state.ExplorerPanelWidth
            : DefaultExplorerPanelWidth;
        double splitRatio = double.IsFinite(state.WorkspaceSplitRatio)
            ? state.WorkspaceSplitRatio
            : DefaultWorkspaceSplitRatio;

        return state with
        {
            ExplorerPanelWidth = Math.Max(MinExplorerPanelWidth, panelWidth),
            WorkspaceSplitRatio = Math.Clamp(splitRatio, MinWorkspaceSplitRatio, MaxWorkspaceSplitRatio),
            ActiveWorkspaceTab = activeTab
        };
    }

    private static GraphViewState NormalizeGraphViewState(GraphViewState? graphViewState)
    {
        GraphViewState state = graphViewState ?? new GraphViewState();
        int panelWidth = state.PanelWidth > 0
            ? state.PanelWidth
            : DefaultGraphPanelWidth;
        int mobilePanelHeight = state.MobilePanelHeight > 0
            ? state.MobilePanelHeight
            : DefaultGraphMobilePanelHeight;
        IReadOnlyList<PinnedNodeViewState> pinnedNodes = NormalizePinnedNodes(state.PinnedNodes);
        IReadOnlyList<HiddenNodeViewState> hiddenNodes = NormalizeHiddenNodes(state.HiddenNodes);

        return state with
        {
            DependencyMapDirection = NormalizeDependencyMapDirection(state.DependencyMapDirection),
            PanelWidth = Math.Clamp(panelWidth, MinGraphSidebarPanelWidth, MaxGraphSidebarPanelWidth),
            MobilePanelHeight = Math.Clamp(mobilePanelHeight, MinGraphMobilePanelHeight, MaxGraphMobilePanelHeight),
            PinnedNodes = pinnedNodes,
            HiddenNodes = hiddenNodes
        };
    }

    private static bool GraphViewStatesEqual(GraphViewState? left, GraphViewState? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return
            left.IncludeProjects == right.IncludeProjects &&
            left.IncludeDocuments == right.IncludeDocuments &&
            left.IncludePackages == right.IncludePackages &&
            left.IncludeSymbols == right.IncludeSymbols &&
            left.IncludeAssemblies == right.IncludeAssemblies &&
            left.IncludeNativeDependencies == right.IncludeNativeDependencies &&
            left.IncludeDocumentDependencies == right.IncludeDocumentDependencies &&
            left.IncludeSymbolDependencies == right.IncludeSymbolDependencies &&
            left.IsDependencyMapMode == right.IsDependencyMapMode &&
            left.IsImpactAnalysisMode == right.IsImpactAnalysisMode &&
            left.ShowCyclesOnly == right.ShowCyclesOnly &&
            left.PanelWidth == right.PanelWidth &&
            left.MobilePanelHeight == right.MobilePanelHeight &&
            string.Equals(left.DependencyMapDirection, right.DependencyMapDirection, StringComparison.OrdinalIgnoreCase) &&
            ViewStateComparer.ReadOnlyListEqual(left.PinnedNodes, right.PinnedNodes) &&
            ViewStateComparer.ReadOnlyListEqual(left.HiddenNodes, right.HiddenNodes);
    }

    private static IReadOnlyList<PinnedNodeViewState> NormalizePinnedNodes(IReadOnlyList<PinnedNodeViewState>? pinnedNodes)
    {
        if (pinnedNodes is null || pinnedNodes.Count == 0)
        {
            return Array.Empty<PinnedNodeViewState>();
        }

        Dictionary<string, PinnedNodeViewState> normalized = new(StringComparer.Ordinal);
        foreach (PinnedNodeViewState pinnedNode in pinnedNodes)
        {
            if (string.IsNullOrWhiteSpace(pinnedNode.NodeId))
            {
                continue;
            }

            if (!double.IsFinite(pinnedNode.X) || !double.IsFinite(pinnedNode.Y))
            {
                continue;
            }

            normalized[pinnedNode.NodeId.Trim()] = pinnedNode with
            {
                NodeId = pinnedNode.NodeId.Trim()
            };
        }

        return normalized.Values
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<HiddenNodeViewState> NormalizeHiddenNodes(IReadOnlyList<HiddenNodeViewState>? hiddenNodes)
    {
        if (hiddenNodes is null || hiddenNodes.Count == 0)
        {
            return Array.Empty<HiddenNodeViewState>();
        }

        Dictionary<string, HiddenNodeViewState> normalized = new(StringComparer.Ordinal);
        foreach (HiddenNodeViewState hiddenNode in hiddenNodes)
        {
            if (string.IsNullOrWhiteSpace(hiddenNode.NodeId))
            {
                continue;
            }

            string nodeId = hiddenNode.NodeId.Trim();
            string label = string.IsNullOrWhiteSpace(hiddenNode.Label)
                ? nodeId
                : hiddenNode.Label.Trim();
            string group = string.IsNullOrWhiteSpace(hiddenNode.Group)
                ? string.Empty
                : hiddenNode.Group.Trim();
            normalized[nodeId] = new HiddenNodeViewState(nodeId, label, group);
        }

        return normalized.Values
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.NodeId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeDependencyMapDirection(string? direction)
    {
        if (string.Equals(direction, "incoming", StringComparison.OrdinalIgnoreCase))
        {
            return "incoming";
        }

        if (string.Equals(direction, "outgoing", StringComparison.OrdinalIgnoreCase))
        {
            return "outgoing";
        }

        return "both";
    }

    private static string NormalizeSolutionPath(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(solutionPath);
        }
        catch
        {
            return solutionPath.Trim();
        }
    }
}
