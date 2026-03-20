using System;
using CodeMap.ViewModels;
using Xunit;

namespace CodeMap.Tests.MainWindow;

public sealed class ViewStatePersistenceTests
{
    [Fact]
    public void NormalizeExplorerViewState_ClampsAndNormalizesValues()
    {
        ExplorerViewState normalized = CodeMap.MainWindow.TestNormalizeExplorerViewState(new ExplorerViewState
        {
            ExplorerPanelWidth = double.NaN,
            WorkspaceSplitRatio = 2.5,
            ActiveWorkspaceTab = "unknown"
        });

        Assert.Equal(300, normalized.ExplorerPanelWidth);
        Assert.Equal(0.8, normalized.WorkspaceSplitRatio);
        Assert.Equal("tree", normalized.ActiveWorkspaceTab);
    }

    [Fact]
    public void NormalizeGraphViewState_NormalizesDirectionAndNodeCollections()
    {
        GraphViewState normalized = CodeMap.MainWindow.TestNormalizeGraphViewState(new GraphViewState
        {
            DependencyMapDirection = "invalid",
            PanelWidth = -10,
            MobilePanelHeight = 99999,
            PinnedNodes =
            [
                new PinnedNodeViewState(" node-a ", 1, 2),
                new PinnedNodeViewState("node-a", 3, 4),
                new PinnedNodeViewState("node-b", double.NaN, 10)
            ],
            HiddenNodes =
            [
                new HiddenNodeViewState(" ", "ignored", "symbol"),
                new HiddenNodeViewState("node-x", " ", "  "),
                new HiddenNodeViewState("node-x", "Node X", "document")
            ]
        });

        Assert.Equal("both", normalized.DependencyMapDirection);
        Assert.Equal(320, normalized.PanelWidth);
        Assert.Equal(1600, normalized.MobilePanelHeight);

        PinnedNodeViewState pinned = Assert.Single(normalized.PinnedNodes);
        Assert.Equal("node-a", pinned.NodeId);
        Assert.Equal(3, pinned.X);
        Assert.Equal(4, pinned.Y);

        HiddenNodeViewState hidden = Assert.Single(normalized.HiddenNodes);
        Assert.Equal("node-x", hidden.NodeId);
        Assert.Equal("Node X", hidden.Label);
        Assert.Equal("document", hidden.Group);
    }

    [Fact]
    public void GraphViewStatesEqual_ReflectsPropertyDifferences()
    {
        GraphViewState baseline = CodeMap.MainWindow.TestNormalizeGraphViewState(new GraphViewState
        {
            IncludeSymbols = true,
            HiddenNodes = [new HiddenNodeViewState("node-1", "Node 1", "symbol")]
        });
        GraphViewState same = baseline with { };
        GraphViewState changed = baseline with { IncludeSymbols = false };

        Assert.True(CodeMap.MainWindow.TestGraphViewStatesEqual(baseline, same));
        Assert.False(CodeMap.MainWindow.TestGraphViewStatesEqual(baseline, changed));
    }

    [Fact]
    public void NormalizeSolutionPath_BlankReturnsEmpty_AndDirectoryPathsAreCanonicalized()
    {
        Assert.Equal(string.Empty, CodeMap.MainWindow.TestNormalizeSolutionPath(" \t "));

        string relative = ".";
        string normalized = CodeMap.MainWindow.TestNormalizeSolutionPath(relative);

        Assert.Equal(System.IO.Path.GetFullPath(relative), normalized);

        string directoryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "CodeMapViewStateTests",
            Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directoryPath);
        try
        {
            string withTrailingSeparator = directoryPath + System.IO.Path.DirectorySeparatorChar;
            Assert.Equal(
                CodeMap.MainWindow.TestNormalizeSolutionPath(directoryPath),
                CodeMap.MainWindow.TestNormalizeSolutionPath(withTrailingSeparator));
        }
        finally
        {
            if (System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
