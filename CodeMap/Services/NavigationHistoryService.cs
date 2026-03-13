using System;
using System.Collections.Generic;
using CodeMap.ViewModels;

namespace CodeMap.Services;

internal sealed class NavigationHistoryService
{
    private const int MaxHistoryEntries = 80;

    private readonly List<ViewNavigationState> _history = [];
    private int _historyIndex = -1;
    private ViewNavigationState? _lastRecordedState;
    private ViewNavigationState? _pendingRestoreState;
    private bool _isRestoring;

    public bool IsRestoring => _isRestoring;

    public ViewNavigationState? PendingRestoreState => _pendingRestoreState;

    public bool TryRecord(ViewNavigationState? currentState)
    {
        if (currentState is null)
        {
            return false;
        }

        if (_isRestoring)
        {
            if (_pendingRestoreState is not null &&
                StatesEqual(_pendingRestoreState, currentState))
            {
                _lastRecordedState = currentState;
                _pendingRestoreState = null;
                _isRestoring = false;
            }

            return false;
        }

        if (_lastRecordedState is not null &&
            StatesEqual(_lastRecordedState, currentState))
        {
            return false;
        }

        if (_historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(
                _historyIndex + 1,
                _history.Count - (_historyIndex + 1));
        }

        _history.Add(currentState);
        if (_history.Count > MaxHistoryEntries)
        {
            int overflowCount = _history.Count - MaxHistoryEntries;
            _history.RemoveRange(0, overflowCount);
        }

        _historyIndex = _history.Count - 1;
        _lastRecordedState = currentState;
        return true;
    }

    public ViewNavigationState? TryNavigate(int delta)
    {
        int nextIndex = _historyIndex + delta;
        if (nextIndex < 0 || nextIndex >= _history.Count)
        {
            return null;
        }

        ViewNavigationState targetState = _history[nextIndex];
        _historyIndex = nextIndex;
        _pendingRestoreState = targetState;
        _isRestoring = true;
        return targetState;
    }

    public void Reset()
    {
        _history.Clear();
        _historyIndex = -1;
        _lastRecordedState = null;
        _pendingRestoreState = null;
        _isRestoring = false;
    }

    public void CancelRestore()
    {
        _isRestoring = false;
        _pendingRestoreState = null;
    }

    public static NavigationGraphState CreateNavigableGraphState(GraphViewState graphViewState)
    {
        return new NavigationGraphState(
            graphViewState.IncludeProjects,
            graphViewState.IncludeDocuments,
            graphViewState.IncludePackages,
            graphViewState.IncludeSymbols,
            graphViewState.IncludeAssemblies,
            graphViewState.IncludeNativeDependencies,
            graphViewState.IncludeDocumentDependencies,
            graphViewState.IncludeSymbolDependencies,
            graphViewState.IsDependencyMapMode,
            graphViewState.IsImpactAnalysisMode,
            graphViewState.ShowCyclesOnly,
            graphViewState.DependencyMapDirection,
            graphViewState.HiddenNodes);
    }

    public static GraphViewState MergeGraphViewState(
        NavigationGraphState navigationState,
        GraphViewState? baseState)
    {
        GraphViewState source = baseState ?? new GraphViewState();
        return source with
        {
            IncludeProjects = navigationState.IncludeProjects,
            IncludeDocuments = navigationState.IncludeDocuments,
            IncludePackages = navigationState.IncludePackages,
            IncludeSymbols = navigationState.IncludeSymbols,
            IncludeAssemblies = navigationState.IncludeAssemblies,
            IncludeNativeDependencies = navigationState.IncludeNativeDependencies,
            IncludeDocumentDependencies = navigationState.IncludeDocumentDependencies,
            IncludeSymbolDependencies = navigationState.IncludeSymbolDependencies,
            IsDependencyMapMode = navigationState.IsDependencyMapMode,
            IsImpactAnalysisMode = navigationState.IsImpactAnalysisMode,
            ShowCyclesOnly = navigationState.ShowCyclesOnly,
            DependencyMapDirection = navigationState.DependencyMapDirection,
            HiddenNodes = navigationState.HiddenNodes
        };
    }

    private static bool StatesEqual(ViewNavigationState left, ViewNavigationState right)
    {
        return
            string.Equals(left.WorkspacePath, right.WorkspacePath, StringComparison.OrdinalIgnoreCase) &&
            left.Explorer == right.Explorer &&
            NavigationGraphStatesEqual(left.Graph, right.Graph) &&
            left.SelectionTarget == right.SelectionTarget;
    }

    private static bool NavigationGraphStatesEqual(NavigationGraphState left, NavigationGraphState right)
    {
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
            string.Equals(left.DependencyMapDirection, right.DependencyMapDirection, StringComparison.OrdinalIgnoreCase) &&
            HiddenNodesEqual(left.HiddenNodes, right.HiddenNodes);
    }

    private static bool HiddenNodesEqual(
        IReadOnlyList<HiddenNodeViewState>? left,
        IReadOnlyList<HiddenNodeViewState>? right)
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
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
