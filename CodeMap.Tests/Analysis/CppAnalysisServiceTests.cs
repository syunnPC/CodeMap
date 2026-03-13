using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeMap.Analysis;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class CppAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_WorkspacePathIsBlank_ThrowsArgumentException()
    {
        CppAnalysisService service = new();

        await Assert.ThrowsAsync<ArgumentException>(() => service.AnalyzeAsync(" "));
    }

    [Fact]
    public async Task AnalyzeAsync_InputFileMissing_ReturnsNoProjectsWithDiagnostics()
    {
        CppAnalysisService service = new();
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.sln");

        CppSolutionAnalysisResult result = await service.AnalyzeAsync(missingPath);

        Assert.Empty(result.Projects);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task AnalyzeAsync_UnsupportedExistingFile_ReturnsNoProjectsWithDiagnostics()
    {
        CppAnalysisService service = new();
        using TemporaryWorkspace workspace = new();
        string inputFilePath = Path.Combine(workspace.RootPath, "input.txt");
        await File.WriteAllTextAsync(inputFilePath, "not a solution");

        CppSolutionAnalysisResult result = await service.AnalyzeAsync(inputFilePath);

        Assert.Empty(result.Projects);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public async Task AnalyzeAsync_FolderWithCppFile_ReturnsFolderBasedProject()
    {
        CppAnalysisService service = new();
        using TemporaryWorkspace workspace = new();
        string sourceFilePath = Path.Combine(workspace.RootPath, "main.cpp");
        await File.WriteAllTextAsync(
            sourceFilePath,
            """
            #include <vector>
            int Add(int a, int b) { return a + b; }
            int main() { return Add(1, 2); }
            """);

        CppSolutionAnalysisResult result = await service.AnalyzeAsync(workspace.RootPath, CancellationToken.None);

        CppProjectAnalysisResult project = Assert.Single(result.Projects);
        Assert.True(project.Summary.IsFolderBased);
        Assert.Equal("C/C++", project.Summary.Language);
        DocumentAnalysisSummary document = Assert.Single(project.Summary.Documents);
        Assert.Equal(Path.GetFullPath(sourceFilePath), Path.GetFullPath(document.FilePath ?? string.Empty), ignoreCase: true);
        Assert.False(string.IsNullOrWhiteSpace(document.Id));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CodeMapCppAnalysisTests", Guid.NewGuid().ToString("N"));
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
