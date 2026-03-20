using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeMap.Analysis;
using CodeMap.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CodeMap.Tests.Storage;

public sealed class SqliteSnapshotStoreTests
{
    [Fact]
    public async Task SaveAndLoadLatestSnapshot_RoundTripsCoreData()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);
        SolutionAnalysisSnapshot snapshot = CreateSnapshot(workspace.RootPath);

        SnapshotPersistenceResult result = await store.SaveSnapshotAsync(snapshot);
        Assert.True(result.SnapshotId > 0);

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(snapshot.WorkspacePath);
        Assert.NotNull(loaded);

        Assert.Equal(snapshot.WorkspacePath, loaded!.WorkspacePath, ignoreCase: true);
        Assert.Equal(snapshot.WorkspaceKind, loaded.WorkspaceKind);
        Assert.Single(loaded.Projects);
        Assert.Single(loaded.DocumentDependencies);
        Assert.Single(loaded.SymbolDependencies);
        Assert.Single(loaded.Cycles);
        Assert.NotEmpty(loaded.Diagnostics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" \t ")]
    public async Task TryLoadLatestSnapshotAsync_WorkspacePathIsBlank_ReturnsNull(string? workspacePath)
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspacePath!);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_DatabaseFileMissing_ReturnsNull()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_SnapshotsTableMissing_ReturnsNull()
    {
        using TemporaryWorkspace workspace = new();
        await CreateEmptyDatabaseFileAsync(workspace.DatabasePath);

        using SqliteSnapshotStore store = new(workspace.DatabasePath);
        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_UnknownWorkspace_ReturnsNull()
    {
        using TemporaryWorkspace workspace = new();
        string otherWorkspacePath = Path.Combine(workspace.RootPath, "other");
        Directory.CreateDirectory(otherWorkspacePath);

        using SqliteSnapshotStore store = new(workspace.DatabasePath);
        await store.SaveSnapshotAsync(CreateSnapshot(workspace.RootPath));

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(otherWorkspacePath);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_LoadsMostRecentSnapshotForWorkspace()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        DateTimeOffset firstAnalyzedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        DateTimeOffset secondAnalyzedAt = DateTimeOffset.UtcNow;

        await store.SaveSnapshotAsync(CreateSnapshot(
            workspace.RootPath,
            analyzedAt: firstAnalyzedAt,
            diagnostic: "old"));
        await store.SaveSnapshotAsync(CreateSnapshot(
            workspace.RootPath,
            analyzedAt: secondAnalyzedAt,
            diagnostic: "new"));

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);

        Assert.NotNull(loaded);
        Assert.Equal(secondAnalyzedAt, loaded!.AnalyzedAt);
        Assert.Equal("new", Assert.Single(loaded.Diagnostics));
    }

    [Fact]
    public async Task SaveSnapshotAsync_OnlyKeepsLatestTwentySnapshots()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        long lastSnapshotId = 0;
        for (int index = 0; index < 25; index++)
        {
            SnapshotPersistenceResult result = await store.SaveSnapshotAsync(CreateSnapshot(
                workspace.RootPath,
                analyzedAt: DateTimeOffset.UtcNow.AddMinutes(index),
                diagnostic: $"diag-{index}"));
            lastSnapshotId = result.SnapshotId;
        }

        (long count, long minSnapshotId, long maxSnapshotId) = await ReadSnapshotIdStatsAsync(workspace.DatabasePath);
        Assert.Equal(20, count);
        Assert.Equal(lastSnapshotId, maxSnapshotId);
        Assert.Equal(lastSnapshotId - 19, minSnapshotId);

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        Assert.NotNull(loaded);
        Assert.Equal("diag-24", Assert.Single(loaded!.Diagnostics));
    }

    [Fact]
    public async Task SaveSnapshotAsync_PrunesPerWorkspaceWithoutDeletingOtherWorkspaceCaches()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);
        string otherWorkspacePath = Path.Combine(workspace.RootPath, "other-workspace");
        Directory.CreateDirectory(otherWorkspacePath);

        await store.SaveSnapshotAsync(CreateSnapshot(otherWorkspacePath, diagnostic: "other"));

        for (int index = 0; index < 25; index++)
        {
            await store.SaveSnapshotAsync(CreateSnapshot(
                workspace.RootPath,
                analyzedAt: DateTimeOffset.UtcNow.AddMinutes(index),
                diagnostic: $"diag-{index}"));
        }

        SolutionAnalysisSnapshot? primaryLoaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        SolutionAnalysisSnapshot? otherLoaded = await store.TryLoadLatestSnapshotAsync(otherWorkspacePath);
        (long count, _, _) = await ReadSnapshotIdStatsAsync(workspace.DatabasePath);

        Assert.NotNull(primaryLoaded);
        Assert.NotNull(otherLoaded);
        Assert.Equal("diag-24", Assert.Single(primaryLoaded!.Diagnostics));
        Assert.Equal("other", Assert.Single(otherLoaded!.Diagnostics));
        Assert.Equal(21, count);
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_FormatVersionMismatch_ReturnsNull()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        SnapshotPersistenceResult result = await store.SaveSnapshotAsync(CreateSnapshot(workspace.RootPath));
        await SetSnapshotFormatVersionAsync(workspace.DatabasePath, result.SnapshotId, 4);

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_NormalizesWorkspacePath()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        string normalizedWorkspacePath = workspace.RootPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string storedWorkspacePath = $"{normalizedWorkspacePath}{Path.DirectorySeparatorChar}";
        await store.SaveSnapshotAsync(CreateSnapshot(storedWorkspacePath, diagnostic: "normalized"));

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(normalizedWorkspacePath);

        Assert.NotNull(loaded);
        Assert.Equal(Path.GetFullPath(normalizedWorkspacePath), loaded!.WorkspacePath, ignoreCase: true);
        Assert.Equal("normalized", Assert.Single(loaded.Diagnostics));
    }

    [Fact]
    public async Task SaveAndLoadLatestSnapshot_NormalizesNativeImportedSymbols()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        await store.SaveSnapshotAsync(CreateSnapshot(
            workspace.RootPath,
            importedSymbols: ["zeta", "alpha", "beta"]));

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        Assert.NotNull(loaded);

        NativeDependencySummary nativeDependency = Assert.Single(Assert.Single(loaded!.Projects).NativeDependencies);
        Assert.Equal(new[] { "alpha", "beta", "zeta" }, nativeDependency.ImportedSymbols);
    }

    [Fact]
    public async Task SaveAndLoadLatestSnapshot_DeduplicatesProjectReferencesByTarget()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);

        await store.SaveSnapshotAsync(CreateSnapshot(
            workspace.RootPath,
            projectReferences:
            [
                new ProjectReferenceSummary("name:target", "zeta"),
                new ProjectReferenceSummary("name:target", "alpha")
            ]));

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        Assert.NotNull(loaded);

        ProjectReferenceSummary projectReference = Assert.Single(Assert.Single(loaded!.Projects).ProjectReferences);
        Assert.Equal("name:target", projectReference.TargetProjectKey, ignoreCase: true);
        Assert.Equal("alpha", projectReference.DisplayName);
    }

    [Fact]
    public async Task ClearAllAsync_RemovesSavedSnapshotAndDatabaseFiles()
    {
        using TemporaryWorkspace workspace = new();
        using SqliteSnapshotStore store = new(workspace.DatabasePath);
        await store.SaveSnapshotAsync(CreateSnapshot(workspace.RootPath));

        await store.ClearAllAsync();

        SolutionAnalysisSnapshot? loaded = await store.TryLoadLatestSnapshotAsync(workspace.RootPath);
        Assert.Null(loaded);
        Assert.False(File.Exists(workspace.DatabasePath));
    }

    [Fact]
    public async Task ClearAllAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using TemporaryWorkspace workspace = new();
        SqliteSnapshotStore store = new(workspace.DatabasePath);
        store.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => store.ClearAllAsync());
    }

    [Fact]
    public async Task SaveSnapshotAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using TemporaryWorkspace workspace = new();
        SqliteSnapshotStore store = new(workspace.DatabasePath);
        store.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => store.SaveSnapshotAsync(CreateSnapshot(workspace.RootPath)));
    }

    [Fact]
    public async Task TryLoadLatestSnapshotAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using TemporaryWorkspace workspace = new();
        SqliteSnapshotStore store = new(workspace.DatabasePath);
        store.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => store.TryLoadLatestSnapshotAsync(workspace.RootPath));
    }

    private static SolutionAnalysisSnapshot CreateSnapshot(
        string workspaceRoot,
        DateTimeOffset? analyzedAt = null,
        string diagnostic = "diagnostic",
        IReadOnlyList<string>? importedSymbols = null,
        IReadOnlyList<ProjectReferenceSummary>? projectReferences = null)
    {
        string projectDirectory = Path.Combine(workspaceRoot, "project");
        string projectFilePath = Path.Combine(projectDirectory, "project.vcxproj");
        string documentPath = Path.Combine(projectDirectory, "main.c");
        string projectKey = AnalysisIdentity.BuildProjectKey("project", projectFilePath);
        string documentId = "document:main_c";
        string symbolId = "symbol:main";

        SymbolAnalysisSummary symbol = new(
            symbolId,
            "FunctionDeclaration",
            "main",
            "main",
            1);
        DocumentAnalysisSummary document = new(
            documentId,
            "main.c",
            documentPath,
            [symbol]);
        ProjectAnalysisSummary project = new(
            "project",
            "C/C++",
            projectFilePath,
            projectKey,
            IsFolderBased: false,
            [document],
            ProjectReferences: projectReferences ?? Array.Empty<ProjectReferenceSummary>(),
            MetadataReferences: Array.Empty<string>(),
            PackageReferences: Array.Empty<string>(),
            NativeDependencies:
            [
                new NativeDependencySummary(
                    "kernel32.dll",
                    "LoadLibrary",
                    "high",
                    documentId,
                    symbolId,
                    importedSymbols ?? ["GetProcAddress"])
            ]);

        return new SolutionAnalysisSnapshot(
            workspaceRoot,
            "folder",
            analyzedAt ?? DateTimeOffset.UtcNow,
            [project],
            [
                new DocumentDependencySummary(
                    documentId,
                    documentId,
                    1,
                    documentPath,
                    1,
                    "#include \"main.h\"")
            ],
            [
                new SymbolDependencySummary(
                    symbolId,
                    symbolId,
                    1,
                    "call",
                    "high",
                    documentPath,
                    1,
                    "main();")
            ],
            [
                new DependencyCycleSummary(
                    "symbol",
                    "cycle-1",
                    [symbolId],
                    1)
            ],
            [diagnostic]);
    }

    private static async Task CreateEmptyDatabaseFileAsync(string databasePath)
    {
        string? databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };

        await using SqliteConnection connection = new(connectionStringBuilder.ToString());
        await connection.OpenAsync();
    }

    private static async Task SetSnapshotFormatVersionAsync(string databasePath, long snapshotId, int formatVersion)
    {
        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private
        };

        await using SqliteConnection connection = new(connectionStringBuilder.ToString());
        await connection.OpenAsync();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE snapshots
            SET snapshot_format_version = $snapshotFormatVersion
            WHERE snapshot_id = $snapshotId;
            """;
        command.Parameters.AddWithValue("$snapshotFormatVersion", formatVersion);
        command.Parameters.AddWithValue("$snapshotId", snapshotId);
        _ = await command.ExecuteNonQueryAsync();
    }

    private static async Task<(long Count, long MinSnapshotId, long MaxSnapshotId)> ReadSnapshotIdStatsAsync(string databasePath)
    {
        SqliteConnectionStringBuilder connectionStringBuilder = new()
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };

        await using SqliteConnection connection = new(connectionStringBuilder.ToString());
        await connection.OpenAsync();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                COUNT(*),
                COALESCE(MIN(snapshot_id), 0),
                COALESCE(MAX(snapshot_id), 0)
            FROM snapshots;
            """;

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        return (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CodeMapTests", Guid.NewGuid().ToString("N"));
            DatabasePath = Path.Combine(RootPath, "snapshot-cache.db");
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string DatabasePath { get; }

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
