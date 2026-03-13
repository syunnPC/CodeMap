using System;
using System.Collections.Generic;
using CodeMap.Services;
using CodeMap.ViewModels;
using Xunit;

namespace CodeMap.Tests.Services;

public sealed class NavigationHistoryServiceTests
{
    [Fact]
    public void TryRecord_NullState_ReturnsFalse()
    {
        NavigationHistoryService service = new();

        Assert.False(service.TryRecord(null));
    }

    [Fact]
    public void TryRecord_SameStateTwice_OnlyRecordsOnce()
    {
        NavigationHistoryService service = new();
        ViewNavigationState state = CreateState(@"C:\workspace", "node-1");

        Assert.True(service.TryRecord(state));
        Assert.False(service.TryRecord(state));
    }

    [Fact]
    public void TryNavigate_NoHistory_ReturnsNull()
    {
        NavigationHistoryService service = new();

        Assert.Null(service.TryNavigate(-1));
        Assert.Null(service.TryNavigate(1));
    }

    [Fact]
    public void TryRecord_WhenRestoreStateApplied_ClearsRestoreFlags()
    {
        NavigationHistoryService service = new();
        ViewNavigationState first = CreateState(@"C:\workspace", "node-1");
        ViewNavigationState second = CreateState(@"C:\workspace", "node-2");
        _ = service.TryRecord(first);
        _ = service.TryRecord(second);

        ViewNavigationState? restored = service.TryNavigate(-1);
        Assert.NotNull(restored);
        Assert.True(service.IsRestoring);
        Assert.Equal(first, restored);
        Assert.Equal(first, service.PendingRestoreState);

        Assert.False(service.TryRecord(restored));
        Assert.False(service.IsRestoring);
        Assert.Null(service.PendingRestoreState);
    }

    [Fact]
    public void TryRecord_WhenRestoringDifferentState_KeepsRestorePending()
    {
        NavigationHistoryService service = new();
        ViewNavigationState first = CreateState(@"C:\workspace", "node-1");
        ViewNavigationState second = CreateState(@"C:\workspace", "node-2");
        _ = service.TryRecord(first);
        _ = service.TryRecord(second);

        _ = service.TryNavigate(-1);

        Assert.False(service.TryRecord(CreateState(@"C:\workspace", "node-3")));
        Assert.True(service.IsRestoring);
        Assert.NotNull(service.PendingRestoreState);

        service.CancelRestore();
        Assert.False(service.IsRestoring);
        Assert.Null(service.PendingRestoreState);
    }

    [Fact]
    public void TryRecord_AfterBackNavigation_RemovesForwardHistory()
    {
        NavigationHistoryService service = new();
        ViewNavigationState stateA = CreateState(@"C:\workspace", "A");
        ViewNavigationState stateB = CreateState(@"C:\workspace", "B");
        ViewNavigationState stateC = CreateState(@"C:\workspace", "C");
        ViewNavigationState stateD = CreateState(@"C:\workspace", "D");
        _ = service.TryRecord(stateA);
        _ = service.TryRecord(stateB);
        _ = service.TryRecord(stateC);

        ViewNavigationState? backToB = service.TryNavigate(-1);
        Assert.NotNull(backToB);
        Assert.Equal("B", backToB!.SelectionTarget!.NodeId);
        Assert.False(service.TryRecord(backToB));

        Assert.True(service.TryRecord(stateD));

        ViewNavigationState? previous = service.TryNavigate(-1);
        Assert.NotNull(previous);
        Assert.Equal("B", previous!.SelectionTarget!.NodeId);
    }

    [Fact]
    public void TryRecord_MoreThanMaxHistoryEntries_DropsOldestEntries()
    {
        NavigationHistoryService service = new();

        for (int index = 0; index < 85; index++)
        {
            Assert.True(service.TryRecord(CreateState(@"C:\workspace", $"node-{index}")));
        }

        ViewNavigationState? oldestRemaining = service.TryNavigate(-79);
        Assert.NotNull(oldestRemaining);
        Assert.Equal("node-5", oldestRemaining!.SelectionTarget!.NodeId);
        Assert.False(service.TryRecord(oldestRemaining));
        Assert.Null(service.TryNavigate(-1));
    }

    [Fact]
    public void Reset_ClearsHistoryAndRestoreState()
    {
        NavigationHistoryService service = new();
        _ = service.TryRecord(CreateState(@"C:\workspace", "A"));
        _ = service.TryRecord(CreateState(@"C:\workspace", "B"));
        _ = service.TryNavigate(-1);
        Assert.True(service.IsRestoring);

        service.Reset();

        Assert.False(service.IsRestoring);
        Assert.Null(service.PendingRestoreState);
        Assert.Null(service.TryNavigate(-1));
        Assert.True(service.TryRecord(CreateState(@"C:\workspace", "C")));
    }

    [Fact]
    public void CreateNavigableGraphState_CopiesGraphFlagsAndHiddenNodes()
    {
        HiddenNodeViewState[] hiddenNodes = [new HiddenNodeViewState("node-1", "Node 1", "symbol")];
        GraphViewState graphViewState = new()
        {
            IncludeProjects = false,
            IncludeDocuments = false,
            IncludePackages = true,
            IncludeSymbols = true,
            IncludeAssemblies = false,
            IncludeNativeDependencies = false,
            IncludeDocumentDependencies = true,
            IncludeSymbolDependencies = false,
            IsDependencyMapMode = true,
            IsImpactAnalysisMode = true,
            ShowCyclesOnly = true,
            DependencyMapDirection = "incoming",
            HiddenNodes = hiddenNodes
        };

        NavigationGraphState actual = NavigationHistoryService.CreateNavigableGraphState(graphViewState);

        Assert.False(actual.IncludeProjects);
        Assert.False(actual.IncludeDocuments);
        Assert.True(actual.IncludePackages);
        Assert.True(actual.IncludeSymbols);
        Assert.False(actual.IncludeAssemblies);
        Assert.False(actual.IncludeNativeDependencies);
        Assert.True(actual.IncludeDocumentDependencies);
        Assert.False(actual.IncludeSymbolDependencies);
        Assert.True(actual.IsDependencyMapMode);
        Assert.True(actual.IsImpactAnalysisMode);
        Assert.True(actual.ShowCyclesOnly);
        Assert.Equal("incoming", actual.DependencyMapDirection);
        Assert.Equal(hiddenNodes, actual.HiddenNodes);
    }

    [Fact]
    public void MergeGraphViewState_UsesNavigationStateAndPreservesLayoutFields()
    {
        PinnedNodeViewState[] pinnedNodes = [new PinnedNodeViewState("pin", 10, 20)];
        HiddenNodeViewState[] hiddenNodes = [new HiddenNodeViewState("node-2", "Node 2", "document")];
        GraphViewState baseState = new()
        {
            PanelWidth = 480,
            MobilePanelHeight = 420,
            PinnedNodes = pinnedNodes,
            HiddenNodes = Array.Empty<HiddenNodeViewState>()
        };
        NavigationGraphState navigationState = new(
            IncludeProjects: false,
            IncludeDocuments: false,
            IncludePackages: true,
            IncludeSymbols: true,
            IncludeAssemblies: false,
            IncludeNativeDependencies: true,
            IncludeDocumentDependencies: false,
            IncludeSymbolDependencies: true,
            IsDependencyMapMode: true,
            IsImpactAnalysisMode: false,
            ShowCyclesOnly: true,
            DependencyMapDirection: "outgoing",
            HiddenNodes: hiddenNodes);

        GraphViewState merged = NavigationHistoryService.MergeGraphViewState(navigationState, baseState);

        Assert.Equal(480, merged.PanelWidth);
        Assert.Equal(420, merged.MobilePanelHeight);
        Assert.Equal(pinnedNodes, merged.PinnedNodes);
        Assert.Equal(hiddenNodes, merged.HiddenNodes);
        Assert.False(merged.IncludeProjects);
        Assert.False(merged.IncludeDocuments);
        Assert.True(merged.IncludePackages);
        Assert.True(merged.IncludeSymbols);
        Assert.False(merged.IncludeAssemblies);
        Assert.True(merged.IncludeNativeDependencies);
        Assert.False(merged.IncludeDocumentDependencies);
        Assert.True(merged.IncludeSymbolDependencies);
        Assert.True(merged.IsDependencyMapMode);
        Assert.False(merged.IsImpactAnalysisMode);
        Assert.True(merged.ShowCyclesOnly);
        Assert.Equal("outgoing", merged.DependencyMapDirection);
    }

    [Fact]
    public void MergeGraphViewState_BaseStateNull_UsesDefaultsForLayoutFields()
    {
        NavigationGraphState navigationState = new(
            IncludeProjects: true,
            IncludeDocuments: false,
            IncludePackages: true,
            IncludeSymbols: false,
            IncludeAssemblies: true,
            IncludeNativeDependencies: false,
            IncludeDocumentDependencies: true,
            IncludeSymbolDependencies: false,
            IsDependencyMapMode: false,
            IsImpactAnalysisMode: true,
            ShowCyclesOnly: false,
            DependencyMapDirection: "incoming",
            HiddenNodes: Array.Empty<HiddenNodeViewState>());

        GraphViewState merged = NavigationHistoryService.MergeGraphViewState(navigationState, baseState: null);

        Assert.Equal(320, merged.PanelWidth);
        Assert.Equal(320, merged.MobilePanelHeight);
        Assert.Empty(merged.PinnedNodes);
        Assert.Equal("incoming", merged.DependencyMapDirection);
        Assert.True(merged.IsImpactAnalysisMode);
    }

    private static ViewNavigationState CreateState(
        string workspacePath,
        string selectionNodeId,
        string dependencyMapDirection = "both",
        IReadOnlyList<HiddenNodeViewState>? hiddenNodes = null)
    {
        GraphViewState graphViewState = new()
        {
            IncludeProjects = true,
            IncludeDocuments = true,
            IncludePackages = true,
            IncludeSymbols = true,
            IncludeAssemblies = true,
            IncludeNativeDependencies = true,
            IncludeDocumentDependencies = true,
            IncludeSymbolDependencies = true,
            IsDependencyMapMode = false,
            IsImpactAnalysisMode = false,
            ShowCyclesOnly = false,
            DependencyMapDirection = dependencyMapDirection,
            HiddenNodes = hiddenNodes ?? Array.Empty<HiddenNodeViewState>()
        };

        return new ViewNavigationState(
            workspacePath,
            new ExplorerViewState
            {
                ExplorerPanelWidth = 300,
                IsWorkspaceSplit = false,
                WorkspaceSplitRatio = 0.5,
                ActiveWorkspaceTab = "tree"
            },
            NavigationHistoryService.CreateNavigableGraphState(graphViewState),
            new GraphSelectionTarget(selectionNodeId, selectionNodeId));
    }
}
