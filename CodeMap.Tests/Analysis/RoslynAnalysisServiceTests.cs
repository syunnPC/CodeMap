using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeMap.Analysis;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class RoslynAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeWorkspaceAsync_WorkspacePathIsBlank_ThrowsArgumentException()
    {
        RoslynAnalysisService service = new();

        await Assert.ThrowsAsync<ArgumentException>(() => service.AnalyzeWorkspaceAsync(" "));
    }

    [Fact]
    public async Task AnalyzeWorkspaceAsync_ExistingFolderWithCSharpFile_ReturnsManagedProject()
    {
        RoslynAnalysisService service = new();
        using TemporaryWorkspace workspace = new();
        string sourceFilePath = Path.Combine(workspace.RootPath, "Sample.cs");
        await File.WriteAllTextAsync(
            sourceFilePath,
            """
            namespace Demo;
            public sealed class Sample
            {
                public int Add(int a, int b) => a + b;
            }
            """);

        SolutionAnalysisSnapshot snapshot = await service.AnalyzeWorkspaceAsync(
            workspace.RootPath,
            CancellationToken.None);

        Assert.Equal("folder", snapshot.WorkspaceKind);
        Assert.Equal(Path.GetFullPath(workspace.RootPath), snapshot.WorkspacePath, ignoreCase: true);

        ProjectAnalysisSummary managedProject = Assert.Single(
            snapshot.Projects,
            project => project.Language == LanguageNames.CSharp);
        Assert.True(managedProject.IsFolderBased);
        Assert.NotEmpty(managedProject.Documents);
        Assert.Contains(managedProject.Documents, document => string.Equals(
            Path.GetFullPath(document.FilePath ?? string.Empty),
            Path.GetFullPath(sourceFilePath),
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeWorkspaceAsync_NonExistingProjectPath_ReturnsProjectKindSnapshotWithoutProjects()
    {
        RoslynAnalysisService service = new();
        string workspacePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.input");

        SolutionAnalysisSnapshot snapshot = await service.AnalyzeWorkspaceAsync(workspacePath);

        Assert.Equal("project", snapshot.WorkspaceKind);
        Assert.Equal(Path.GetFullPath(workspacePath), snapshot.WorkspacePath, ignoreCase: true);
        Assert.Empty(snapshot.Projects);
        Assert.NotEmpty(snapshot.Diagnostics);
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CodeMapRoslynAnalysisTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            TryDeleteDirectory(RootPath);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
