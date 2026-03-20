using CodeMap.Analysis;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeMap.Storage;

public sealed partial class SqliteSnapshotStore : IDisposable
{
    private const int MaxSnapshotsToKeep = 20;
    private const int CurrentSnapshotFormatVersion = 5;
    private readonly string _databasePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifecycleLock = new();
    private bool _schemaInitialized;
    private bool _ftsEnabled;
    private int _activeOperationCount;
    private bool _disposeRequested;
    private bool _isDisposed;

    public SqliteSnapshotStore(string? databasePath = null)
    {
        _databasePath = databasePath ?? AppStoragePaths.AnalysisCacheDatabasePath;
    }

    public void Dispose()
    {
        bool disposeGate = false;
        lock (_lifecycleLock)
        {
            if (_disposeRequested)
            {
                return;
            }

            _disposeRequested = true;
            if (_activeOperationCount == 0 && !_isDisposed)
            {
                _isDisposed = true;
                disposeGate = true;
            }
        }

        if (disposeGate)
        {
            _gate.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public async Task<SnapshotPersistenceResult> SaveSnapshotAsync(
        SolutionAnalysisSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteExclusiveAsync(async ct =>
        {
            string? databaseDirectoryPath = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrWhiteSpace(databaseDirectoryPath))
            {
                Directory.CreateDirectory(databaseDirectoryPath);
            }

            SqliteConnectionStringBuilder connectionStringBuilder = new()
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            await using SqliteConnection connection = new(connectionStringBuilder.ToString());
            await connection.OpenAsync(ct);
            await EnsureSchemaAsync(connection, ct);

            await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);
            long snapshotId = await InsertSnapshotAsync(connection, transaction, snapshot, ct);
            using SnapshotInsertCommands insertCommands = CreateSnapshotInsertCommands(connection, transaction, _ftsEnabled);

            foreach (ProjectAnalysisSummary project in snapshot.Projects)
            {
                string projectKey = project.ProjectKey;
                await InsertProjectAsync(insertCommands.ProjectCommand, snapshotId, projectKey, project, ct);

                foreach (ProjectReferenceSummary projectReference in project.ProjectReferences)
                {
                    await InsertDependencyAsync(
                        insertCommands.DependencyCommand,
                        snapshotId,
                        projectKey,
                        project.Name,
                        "project",
                        projectReference.TargetProjectKey,
                        dependencyOrigin: projectReference.DisplayName,
                        confidence: null,
                        importedSymbols: null,
                        ct);
                }

                foreach (string packageReference in project.PackageReferences)
                {
                    await InsertDependencyAsync(
                        insertCommands.DependencyCommand,
                        snapshotId,
                        projectKey,
                        project.Name,
                        "package",
                        packageReference,
                        dependencyOrigin: null,
                        confidence: null,
                        importedSymbols: null,
                        ct);
                }

                foreach (string metadataReference in project.MetadataReferences)
                {
                    await InsertDependencyAsync(
                        insertCommands.DependencyCommand,
                        snapshotId,
                        projectKey,
                        project.Name,
                        "assembly",
                        metadataReference,
                        dependencyOrigin: null,
                        confidence: null,
                        importedSymbols: null,
                        ct);
                }

                foreach (NativeDependencySummary nativeDependency in project.NativeDependencies)
                {
                    await InsertDependencyAsync(
                        insertCommands.DependencyCommand,
                        snapshotId,
                        projectKey,
                        project.Name,
                        "dll",
                        nativeDependency.LibraryName,
                        nativeDependency.ImportKind,
                        nativeDependency.Confidence,
                        nativeDependency.ImportedSymbols.Count == 0
                            ? null
                            : string.Join(", ", nativeDependency.ImportedSymbols),
                        ct);
                }

                foreach (DocumentAnalysisSummary document in project.Documents)
                {
                    await InsertDocumentAsync(
                        insertCommands.DocumentCommand,
                        snapshotId,
                        projectKey,
                        project.Name,
                        document,
                        ct);

                    foreach (SymbolAnalysisSummary symbol in document.Symbols)
                    {
                        await InsertSymbolAsync(
                            insertCommands.SymbolCommand,
                            insertCommands.SymbolFtsCommand,
                            snapshotId,
                            projectKey,
                            project.Name,
                            document,
                            symbol,
                            ct);
                    }
                }
            }

            foreach (DocumentDependencySummary dependency in snapshot.DocumentDependencies)
            {
                await InsertDocumentDependencyAsync(
                    insertCommands.DocumentDependencyCommand,
                    snapshotId,
                    dependency,
                    ct);
            }

            foreach (SymbolDependencySummary dependency in snapshot.SymbolDependencies)
            {
                await InsertSymbolDependencyAsync(
                    insertCommands.SymbolDependencyCommand,
                    snapshotId,
                    dependency,
                    ct);
            }

            foreach (DependencyCycleSummary cycle in snapshot.Cycles)
            {
                await InsertCycleAsync(
                    insertCommands.CycleCommand,
                    insertCommands.CycleNodeCommand,
                    snapshotId,
                    cycle,
                    ct);
            }

            for (int index = 0; index < snapshot.Diagnostics.Count; index++)
            {
                await InsertDiagnosticAsync(
                    insertCommands.DiagnosticCommand,
                    snapshotId,
                    index,
                    snapshot.Diagnostics[index],
                    ct);
            }

            await DeleteObsoleteSnapshotsAsync(connection, transaction, ct);
            await transaction.CommitAsync(ct);
            return new SnapshotPersistenceResult(snapshotId, _databasePath);
        }, cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteExclusiveAsync(ct =>
        {
            _schemaInitialized = false;
            _ftsEnabled = false;
            SqliteConnection.ClearAllPools();
            DeleteFileIfExists(_databasePath);
            DeleteFileIfExists($"{_databasePath}-wal");
            DeleteFileIfExists($"{_databasePath}-shm");
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public async Task<SolutionAnalysisSnapshot?> TryLoadLatestSnapshotAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return null;
        }

        return await ExecuteExclusiveAsync(async ct =>
        {
            if (!File.Exists(_databasePath))
            {
                return null;
            }

            SqliteConnectionStringBuilder connectionStringBuilder = new()
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Private
            };

            await using SqliteConnection connection = new(connectionStringBuilder.ToString());
            await connection.OpenAsync(ct);

            if (!await TableExistsAsync(connection, "snapshots", ct))
            {
                return null;
            }

            string resolvedWorkspacePath = Path.GetFullPath(workspacePath);
            string normalizedWorkspacePath = NormalizeWorkspacePathForStorage(resolvedWorkspacePath);
            HashSet<string> snapshotColumns = await LoadColumnNamesAsync(connection, "snapshots", ct);
            bool hasSnapshotFormatVersionColumn = snapshotColumns.Contains("snapshot_format_version");

            CachedSnapshotHeader? header = await LoadSnapshotHeaderAsync(
                connection,
                normalizedWorkspacePath,
                resolvedWorkspacePath,
                hasSnapshotFormatVersionColumn,
                ct);
            if (header is null)
            {
                return null;
            }

            if (header.SnapshotFormatVersion != CurrentSnapshotFormatVersion)
            {
                return null;
            }

            IReadOnlyList<CachedProjectRecord> projectRecords = await LoadProjectRecordsAsync(
                connection,
                header.SnapshotId,
                ct);
            IReadOnlyList<CachedSymbolRecord> symbolRecords = await LoadSymbolRecordsAsync(
                connection,
                header.SnapshotId,
                ct);
            IReadOnlyList<CachedDocumentRecord> documentRecords = await LoadDocumentRecordsAsync(
                connection,
                header.SnapshotId,
                symbolRecords,
                ct);
            IReadOnlyList<CachedDependencyRecord> dependencyRecords = await LoadDependencyRecordsAsync(
                connection,
                header.SnapshotId,
                ct);
            IReadOnlyList<DocumentDependencySummary> documentDependencies = await LoadDocumentDependenciesAsync(
                connection,
                header.SnapshotId,
                ct);
            IReadOnlyList<SymbolDependencySummary> symbolDependencies = await LoadSymbolDependenciesAsync(
                connection,
                header.SnapshotId,
                ct);
            IReadOnlyList<DependencyCycleSummary> cycles = await LoadCyclesAsync(
                connection,
                header.SnapshotId,
                ct);
            IReadOnlyList<string> diagnostics = await LoadDiagnosticsAsync(
                connection,
                header.SnapshotId,
                ct);

            Dictionary<string, List<CachedDocumentRecord>> documentsByProject = BuildDocumentsByProject(documentRecords);
            Dictionary<string, List<CachedSymbolRecord>> symbolsByDocumentId = BuildSymbolsByDocumentId(symbolRecords);
            Dictionary<string, List<CachedDependencyRecord>> dependenciesByProject = BuildDependenciesByProject(dependencyRecords);

            List<ProjectAnalysisSummary> projects = new(projectRecords.Count);
            foreach (CachedProjectRecord projectRecord in projectRecords)
            {
                dependenciesByProject.TryGetValue(projectRecord.ProjectKey, out List<CachedDependencyRecord>? projectDependencies);
                documentsByProject.TryGetValue(projectRecord.ProjectKey, out List<CachedDocumentRecord>? projectDocuments);

                DocumentAnalysisSummary[] documents;
                if (projectDocuments is null || projectDocuments.Count == 0)
                {
                    documents = [];
                }
                else
                {
                    documents = new DocumentAnalysisSummary[projectDocuments.Count];
                    for (int documentIndex = 0; documentIndex < projectDocuments.Count; documentIndex++)
                    {
                        CachedDocumentRecord documentRecord = projectDocuments[documentIndex];
                        symbolsByDocumentId.TryGetValue(documentRecord.DocumentId, out List<CachedSymbolRecord>? documentSymbols);

                        SymbolAnalysisSummary[] symbols;
                        if (documentSymbols is null || documentSymbols.Count == 0)
                        {
                            symbols = [];
                        }
                        else
                        {
                            symbols = new SymbolAnalysisSummary[documentSymbols.Count];
                            for (int symbolIndex = 0; symbolIndex < documentSymbols.Count; symbolIndex++)
                            {
                                CachedSymbolRecord symbol = documentSymbols[symbolIndex];
                                symbols[symbolIndex] = new SymbolAnalysisSummary(
                                    symbol.SymbolId,
                                    symbol.SymbolKind,
                                    symbol.SymbolName,
                                    symbol.SymbolDisplayName,
                                    symbol.LineNumber);
                            }
                        }

                        documents[documentIndex] = new DocumentAnalysisSummary(
                            documentRecord.DocumentId,
                            documentRecord.DocumentName,
                            documentRecord.DocumentFilePath,
                            symbols);
                    }
                }

                IReadOnlyList<ProjectReferenceSummary> projectReferences = BuildProjectReferences(projectDependencies);
                IReadOnlyList<string> packageReferences = BuildDistinctDependencyNames(projectDependencies, "package");
                IReadOnlyList<string> metadataReferences = BuildDistinctDependencyNames(projectDependencies, "assembly");
                IReadOnlyList<NativeDependencySummary> nativeDependencies = BuildNativeDependencies(projectDependencies);

                projects.Add(new ProjectAnalysisSummary(
                    projectRecord.ProjectName,
                    projectRecord.Language,
                    projectRecord.ProjectFilePath,
                    projectRecord.ProjectKey,
                    projectRecord.IsFolderBased,
                    documents,
                    projectReferences,
                    metadataReferences,
                    packageReferences,
                    nativeDependencies));
            }

            return new SolutionAnalysisSnapshot(
                resolvedWorkspacePath,
                header.WorkspaceKind,
                header.AnalyzedAt,
                projects,
                documentDependencies,
                symbolDependencies,
                cycles,
                diagnostics);
        }, cancellationToken);
    }

    private void BeginOperation()
    {
        lock (_lifecycleLock)
        {
            if (_disposeRequested)
            {
                throw new ObjectDisposedException(nameof(SqliteSnapshotStore));
            }

            _activeOperationCount++;
        }
    }

    private void EndOperation()
    {
        bool disposeGate = false;
        lock (_lifecycleLock)
        {
            _activeOperationCount--;
            if (_disposeRequested && _activeOperationCount == 0 && !_isDisposed)
            {
                _isDisposed = true;
                disposeGate = true;
            }
        }

        if (disposeGate)
        {
            _gate.Dispose();
        }
    }

    private async Task ExecuteExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        await ExecuteExclusiveAsync(
            async ct =>
            {
                await operation(ct);
                return true;
            },
            cancellationToken);
    }

    private async Task<T> ExecuteExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        BeginOperation();
        try
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        if (_schemaInitialized)
        {
            return;
        }

        if (await RequiresSnapshotSchemaResetAsync(connection, cancellationToken))
        {
            await ResetSnapshotSchemaAsync(connection, cancellationToken);
        }

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshots (
                snapshot_id INTEGER PRIMARY KEY AUTOINCREMENT,
                solution_path TEXT NOT NULL,
                workspace_kind TEXT NOT NULL DEFAULT 'workspace',
                snapshot_format_version INTEGER NOT NULL DEFAULT 1,
                analyzed_at_utc TEXT NOT NULL,
                project_count INTEGER NOT NULL,
                document_count INTEGER NOT NULL,
                symbol_count INTEGER NOT NULL
            );
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_projects (
                snapshot_id INTEGER NOT NULL,
                project_key TEXT NOT NULL,
                project_name TEXT NOT NULL,
                language TEXT NOT NULL,
                project_file_path TEXT NULL,
                is_folder_based INTEGER NOT NULL DEFAULT 0,
                document_count INTEGER NOT NULL,
                symbol_count INTEGER NOT NULL,
                PRIMARY KEY (snapshot_id, project_key),
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_projects_project_name
                ON snapshot_projects(snapshot_id, project_name);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_documents (
                snapshot_id INTEGER NOT NULL,
                project_key TEXT NOT NULL,
                project_name TEXT NOT NULL,
                document_id TEXT NOT NULL,
                document_name TEXT NOT NULL,
                document_file_path TEXT NULL,
                PRIMARY KEY (snapshot_id, document_id),
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_documents_snapshot_id
                ON snapshot_documents(snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_documents_project_key
                ON snapshot_documents(snapshot_id, project_key);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_diagnostics (
                snapshot_id INTEGER NOT NULL,
                ordinal INTEGER NOT NULL,
                message TEXT NOT NULL,
                PRIMARY KEY (snapshot_id, ordinal),
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_diagnostics_snapshot_id
                ON snapshot_diagnostics(snapshot_id);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_dependencies (
                snapshot_id INTEGER NOT NULL,
                project_key TEXT NOT NULL,
                project_name TEXT NOT NULL,
                dependency_kind TEXT NOT NULL,
                dependency_name TEXT NOT NULL,
                dependency_origin TEXT NULL,
                confidence TEXT NULL,
                imported_symbols TEXT NULL,
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_dependencies_snapshot_id
                ON snapshot_dependencies(snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_dependencies_project_key
                ON snapshot_dependencies(snapshot_id, project_key);
            """,
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshots",
            "workspace_kind",
            "ALTER TABLE snapshots ADD COLUMN workspace_kind TEXT NOT NULL DEFAULT 'workspace';",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshots",
            "snapshot_format_version",
            "ALTER TABLE snapshots ADD COLUMN snapshot_format_version INTEGER NOT NULL DEFAULT 1;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_projects",
            "project_key",
            "ALTER TABLE snapshot_projects ADD COLUMN project_key TEXT NOT NULL DEFAULT '';",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_projects",
            "is_folder_based",
            "ALTER TABLE snapshot_projects ADD COLUMN is_folder_based INTEGER NOT NULL DEFAULT 0;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_dependencies",
            "project_key",
            "ALTER TABLE snapshot_dependencies ADD COLUMN project_key TEXT NOT NULL DEFAULT '';",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_dependencies",
            "dependency_origin",
            "ALTER TABLE snapshot_dependencies ADD COLUMN dependency_origin TEXT NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_dependencies",
            "confidence",
            "ALTER TABLE snapshot_dependencies ADD COLUMN confidence TEXT NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_dependencies",
            "imported_symbols",
            "ALTER TABLE snapshot_dependencies ADD COLUMN imported_symbols TEXT NULL;",
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_symbols (
                snapshot_id INTEGER NOT NULL,
                symbol_id TEXT NOT NULL,
                project_key TEXT NOT NULL,
                project_name TEXT NOT NULL,
                document_id TEXT NULL,
                document_name TEXT NOT NULL,
                document_file_path TEXT NULL,
                symbol_kind TEXT NOT NULL,
                symbol_name TEXT NOT NULL,
                display_name TEXT NOT NULL,
                line_number INTEGER NOT NULL,
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_symbols_snapshot_id
                ON snapshot_symbols(snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_symbols_symbol_id
                ON snapshot_symbols(symbol_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_symbols_project_key
                ON snapshot_symbols(snapshot_id, project_key);
            """,
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbols",
            "project_key",
            "ALTER TABLE snapshot_symbols ADD COLUMN project_key TEXT NOT NULL DEFAULT '';",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbols",
            "symbol_id",
            "ALTER TABLE snapshot_symbols ADD COLUMN symbol_id TEXT;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbols",
            "document_id",
            "ALTER TABLE snapshot_symbols ADD COLUMN document_id TEXT;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbols",
            "document_file_path",
            "ALTER TABLE snapshot_symbols ADD COLUMN document_file_path TEXT;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbols",
            "display_name",
            "ALTER TABLE snapshot_symbols ADD COLUMN display_name TEXT;",
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_document_dependencies (
                snapshot_id INTEGER NOT NULL,
                source_document_id TEXT NOT NULL,
                target_document_id TEXT NOT NULL,
                reference_count INTEGER NOT NULL,
                sample_file_path TEXT NULL,
                sample_line_number INTEGER NULL,
                sample_snippet TEXT NULL,
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_document_dependencies_snapshot_id
                ON snapshot_document_dependencies(snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_document_dependencies_lookup
                ON snapshot_document_dependencies(snapshot_id, source_document_id, target_document_id);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_symbol_dependencies (
                snapshot_id INTEGER NOT NULL,
                source_symbol_id TEXT NOT NULL,
                target_symbol_id TEXT NOT NULL,
                reference_count INTEGER NOT NULL,
                reference_kind TEXT NOT NULL DEFAULT 'reference',
                confidence TEXT NOT NULL DEFAULT 'high',
                sample_file_path TEXT NULL,
                sample_line_number INTEGER NULL,
                sample_snippet TEXT NULL,
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_symbol_dependencies_snapshot_id
                ON snapshot_symbol_dependencies(snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_symbol_dependencies_lookup
                ON snapshot_symbol_dependencies(snapshot_id, source_symbol_id, target_symbol_id);
            """,
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_document_dependencies",
            "sample_file_path",
            "ALTER TABLE snapshot_document_dependencies ADD COLUMN sample_file_path TEXT NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_document_dependencies",
            "sample_line_number",
            "ALTER TABLE snapshot_document_dependencies ADD COLUMN sample_line_number INTEGER NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_document_dependencies",
            "sample_snippet",
            "ALTER TABLE snapshot_document_dependencies ADD COLUMN sample_snippet TEXT NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbol_dependencies",
            "reference_kind",
            "ALTER TABLE snapshot_symbol_dependencies ADD COLUMN reference_kind TEXT NOT NULL DEFAULT 'reference';",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbol_dependencies",
            "confidence",
            "ALTER TABLE snapshot_symbol_dependencies ADD COLUMN confidence TEXT NOT NULL DEFAULT 'high';",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbol_dependencies",
            "sample_file_path",
            "ALTER TABLE snapshot_symbol_dependencies ADD COLUMN sample_file_path TEXT NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbol_dependencies",
            "sample_line_number",
            "ALTER TABLE snapshot_symbol_dependencies ADD COLUMN sample_line_number INTEGER NULL;",
            cancellationToken);

        await EnsureColumnExistsAsync(
            connection,
            "snapshot_symbol_dependencies",
            "sample_snippet",
            "ALTER TABLE snapshot_symbol_dependencies ADD COLUMN sample_snippet TEXT NULL;",
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_cycles (
                snapshot_id INTEGER NOT NULL,
                cycle_id TEXT NOT NULL,
                graph_kind TEXT NOT NULL,
                edge_count INTEGER NOT NULL,
                node_count INTEGER NOT NULL,
                PRIMARY KEY (snapshot_id, cycle_id),
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_cycles_snapshot_id
                ON snapshot_cycles(snapshot_id);
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS snapshot_cycle_nodes (
                snapshot_id INTEGER NOT NULL,
                cycle_id TEXT NOT NULL,
                graph_kind TEXT NOT NULL,
                node_id TEXT NOT NULL,
                sort_order INTEGER NOT NULL,
                FOREIGN KEY (snapshot_id) REFERENCES snapshots(snapshot_id)
            );
            CREATE INDEX IF NOT EXISTS idx_snapshot_cycle_nodes_snapshot_id
                ON snapshot_cycle_nodes(snapshot_id);
            """,
            cancellationToken);

        try
        {
            await ExecuteNonQueryAsync(
                connection,
                """
                CREATE VIRTUAL TABLE IF NOT EXISTS snapshot_symbol_fts USING fts5(
                    snapshot_id UNINDEXED,
                    project_name,
                    document_name,
                    symbol_kind,
                    symbol_name
                );
                """,
                cancellationToken);
            _ftsEnabled = true;
        }
        catch (SqliteException)
        {
            _ftsEnabled = false;
        }

        _schemaInitialized = true;
    }

    private static SnapshotInsertCommands CreateSnapshotInsertCommands(
        SqliteConnection connection,
        SqliteTransaction transaction,
        bool ftsEnabled)
    {
        SqliteCommand projectCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_projects (
                snapshot_id,
                project_key,
                project_name,
                language,
                project_file_path,
                is_folder_based,
                document_count,
                symbol_count
            ) VALUES (
                $snapshotId,
                $projectKey,
                $projectName,
                $language,
                $projectFilePath,
                $isFolderBased,
                $documentCount,
                $symbolCount
            );
            """,
            ("$snapshotId", 0L),
            ("$projectKey", string.Empty),
            ("$projectName", string.Empty),
            ("$language", string.Empty),
            ("$projectFilePath", DBNull.Value),
            ("$isFolderBased", 0),
            ("$documentCount", 0),
            ("$symbolCount", 0));

        SqliteCommand dependencyCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_dependencies (
                snapshot_id,
                project_key,
                project_name,
                dependency_kind,
                dependency_name,
                dependency_origin,
                confidence,
                imported_symbols
            ) VALUES (
                $snapshotId,
                $projectKey,
                $projectName,
                $dependencyKind,
                $dependencyName,
                $dependencyOrigin,
                $confidence,
                $importedSymbols
            );
            """,
            ("$snapshotId", 0L),
            ("$projectKey", string.Empty),
            ("$projectName", string.Empty),
            ("$dependencyKind", string.Empty),
            ("$dependencyName", string.Empty),
            ("$dependencyOrigin", DBNull.Value),
            ("$confidence", DBNull.Value),
            ("$importedSymbols", DBNull.Value));

        SqliteCommand documentCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_documents (
                snapshot_id,
                project_key,
                project_name,
                document_id,
                document_name,
                document_file_path
            ) VALUES (
                $snapshotId,
                $projectKey,
                $projectName,
                $documentId,
                $documentName,
                $documentFilePath
            );
            """,
            ("$snapshotId", 0L),
            ("$projectKey", string.Empty),
            ("$projectName", string.Empty),
            ("$documentId", string.Empty),
            ("$documentName", string.Empty),
            ("$documentFilePath", DBNull.Value));

        SqliteCommand symbolCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_symbols (
                snapshot_id,
                symbol_id,
                project_key,
                project_name,
                document_id,
                document_name,
                document_file_path,
                symbol_kind,
                symbol_name,
                display_name,
                line_number
            ) VALUES (
                $snapshotId,
                $symbolId,
                $projectKey,
                $projectName,
                $documentId,
                $documentName,
                $documentFilePath,
                $symbolKind,
                $symbolName,
                $displayName,
                $lineNumber
            );
            """,
            ("$snapshotId", 0L),
            ("$symbolId", string.Empty),
            ("$projectKey", string.Empty),
            ("$projectName", string.Empty),
            ("$documentId", string.Empty),
            ("$documentName", string.Empty),
            ("$documentFilePath", DBNull.Value),
            ("$symbolKind", string.Empty),
            ("$symbolName", string.Empty),
            ("$displayName", string.Empty),
            ("$lineNumber", 0));

        SqliteCommand? symbolFtsCommand = null;
        if (ftsEnabled)
        {
            symbolFtsCommand = CreateParameterizedCommand(
                connection,
                transaction,
                """
                INSERT INTO snapshot_symbol_fts (
                    snapshot_id,
                    project_name,
                    document_name,
                    symbol_kind,
                    symbol_name
                ) VALUES (
                    $snapshotId,
                    $projectName,
                    $documentName,
                    $symbolKind,
                    $symbolName
                );
                """,
                ("$snapshotId", 0L),
                ("$projectName", string.Empty),
                ("$documentName", string.Empty),
                ("$symbolKind", string.Empty),
                ("$symbolName", string.Empty));
        }

        SqliteCommand diagnosticCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_diagnostics (
                snapshot_id,
                ordinal,
                message
            ) VALUES (
                $snapshotId,
                $ordinal,
                $message
            );
            """,
            ("$snapshotId", 0L),
            ("$ordinal", 0),
            ("$message", string.Empty));

        SqliteCommand documentDependencyCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_document_dependencies (
                snapshot_id,
                source_document_id,
                target_document_id,
                reference_count,
                sample_file_path,
                sample_line_number,
                sample_snippet
            ) VALUES (
                $snapshotId,
                $sourceDocumentId,
                $targetDocumentId,
                $referenceCount,
                $sampleFilePath,
                $sampleLineNumber,
                $sampleSnippet
            );
            """,
            ("$snapshotId", 0L),
            ("$sourceDocumentId", string.Empty),
            ("$targetDocumentId", string.Empty),
            ("$referenceCount", 0),
            ("$sampleFilePath", DBNull.Value),
            ("$sampleLineNumber", DBNull.Value),
            ("$sampleSnippet", DBNull.Value));

        SqliteCommand symbolDependencyCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_symbol_dependencies (
                snapshot_id,
                source_symbol_id,
                target_symbol_id,
                reference_count,
                reference_kind,
                confidence,
                sample_file_path,
                sample_line_number,
                sample_snippet
            ) VALUES (
                $snapshotId,
                $sourceSymbolId,
                $targetSymbolId,
                $referenceCount,
                $referenceKind,
                $confidence,
                $sampleFilePath,
                $sampleLineNumber,
                $sampleSnippet
            );
            """,
            ("$snapshotId", 0L),
            ("$sourceSymbolId", string.Empty),
            ("$targetSymbolId", string.Empty),
            ("$referenceCount", 0),
            ("$referenceKind", string.Empty),
            ("$confidence", string.Empty),
            ("$sampleFilePath", DBNull.Value),
            ("$sampleLineNumber", DBNull.Value),
            ("$sampleSnippet", DBNull.Value));

        SqliteCommand cycleCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_cycles (
                snapshot_id,
                cycle_id,
                graph_kind,
                edge_count,
                node_count
            ) VALUES (
                $snapshotId,
                $cycleId,
                $graphKind,
                $edgeCount,
                $nodeCount
            );
            """,
            ("$snapshotId", 0L),
            ("$cycleId", string.Empty),
            ("$graphKind", string.Empty),
            ("$edgeCount", 0),
            ("$nodeCount", 0));

        SqliteCommand cycleNodeCommand = CreateParameterizedCommand(
            connection,
            transaction,
            """
            INSERT INTO snapshot_cycle_nodes (
                snapshot_id,
                cycle_id,
                graph_kind,
                node_id,
                sort_order
            ) VALUES (
                $snapshotId,
                $cycleId,
                $graphKind,
                $nodeId,
                $sortOrder
            );
            """,
            ("$snapshotId", 0L),
            ("$cycleId", string.Empty),
            ("$graphKind", string.Empty),
            ("$nodeId", string.Empty),
            ("$sortOrder", 0));

        return new SnapshotInsertCommands(
            projectCommand,
            dependencyCommand,
            documentCommand,
            symbolCommand,
            symbolFtsCommand,
            diagnosticCommand,
            documentDependencyCommand,
            symbolDependencyCommand,
            cycleCommand,
            cycleNodeCommand);
    }

    private static async Task<long> InsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SolutionAnalysisSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO snapshots (
                solution_path,
                workspace_kind,
                snapshot_format_version,
                analyzed_at_utc,
                project_count,
                document_count,
                symbol_count
            ) VALUES (
                $solutionPath,
                $workspaceKind,
                $snapshotFormatVersion,
                $analyzedAtUtc,
                $projectCount,
                $documentCount,
                $symbolCount
            );

            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$solutionPath", NormalizeWorkspacePathForStorage(snapshot.WorkspacePath));
        command.Parameters.AddWithValue("$workspaceKind", snapshot.WorkspaceKind);
        command.Parameters.AddWithValue("$snapshotFormatVersion", CurrentSnapshotFormatVersion);
        command.Parameters.AddWithValue("$analyzedAtUtc", snapshot.AnalyzedAt.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$projectCount", snapshot.Projects.Count);
        command.Parameters.AddWithValue("$documentCount", snapshot.Projects.Sum(project => project.Documents.Count));
        command.Parameters.AddWithValue("$symbolCount", snapshot.Projects.Sum(project => project.Documents.Sum(document => document.Symbols.Count)));

        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is long id ? id : Convert.ToInt64(scalar);
    }

    private static async Task InsertProjectAsync(
        SqliteCommand command,
        long snapshotId,
        string projectKey,
        ProjectAnalysisSummary project,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            command,
            snapshotId,
            projectKey,
            project.Name,
            project.Language,
            project.ProjectFilePath,
            project.IsFolderBased ? 1 : 0,
            project.Documents.Count,
            project.Documents.Sum(document => document.Symbols.Count));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDependencyAsync(
        SqliteCommand command,
        long snapshotId,
        string projectKey,
        string projectName,
        string dependencyKind,
        string dependencyName,
        string? dependencyOrigin,
        string? confidence,
        string? importedSymbols,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            command,
            snapshotId,
            projectKey,
            projectName,
            dependencyKind,
            dependencyName,
            dependencyOrigin,
            confidence,
            importedSymbols);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDocumentAsync(
        SqliteCommand command,
        long snapshotId,
        string projectKey,
        string projectName,
        DocumentAnalysisSummary document,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            command,
            snapshotId,
            projectKey,
            projectName,
            document.Id,
            document.Name,
            document.FilePath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSymbolAsync(
        SqliteCommand symbolCommand,
        SqliteCommand? symbolFtsCommand,
        long snapshotId,
        string projectKey,
        string projectName,
        DocumentAnalysisSummary document,
        SymbolAnalysisSummary symbol,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            symbolCommand,
            snapshotId,
            symbol.Id,
            projectKey,
            projectName,
            document.Id,
            document.Name,
            document.FilePath,
            symbol.Kind,
            symbol.Name,
            symbol.DisplayName,
            symbol.LineNumber);
        await symbolCommand.ExecuteNonQueryAsync(cancellationToken);

        if (symbolFtsCommand is null)
        {
            return;
        }

        SetParameterValues(
            symbolFtsCommand,
            snapshotId,
            projectName,
            document.Name,
            symbol.Kind,
            symbol.Name);
        await symbolFtsCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDiagnosticAsync(
        SqliteCommand command,
        long snapshotId,
        int ordinal,
        string message,
        CancellationToken cancellationToken)
    {
        SetParameterValues(command, snapshotId, ordinal, message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertDocumentDependencyAsync(
        SqliteCommand command,
        long snapshotId,
        DocumentDependencySummary dependency,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            command,
            snapshotId,
            dependency.SourceDocumentId,
            dependency.TargetDocumentId,
            dependency.ReferenceCount,
            dependency.SampleFilePath,
            dependency.SampleLineNumber,
            dependency.SampleSnippet);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertSymbolDependencyAsync(
        SqliteCommand command,
        long snapshotId,
        SymbolDependencySummary dependency,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            command,
            snapshotId,
            dependency.SourceSymbolId,
            dependency.TargetSymbolId,
            dependency.ReferenceCount,
            dependency.ReferenceKind,
            dependency.Confidence,
            dependency.SampleFilePath,
            dependency.SampleLineNumber,
            dependency.SampleSnippet);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertCycleAsync(
        SqliteCommand cycleCommand,
        SqliteCommand cycleNodeCommand,
        long snapshotId,
        DependencyCycleSummary cycle,
        CancellationToken cancellationToken)
    {
        SetParameterValues(
            cycleCommand,
            snapshotId,
            cycle.CycleId,
            cycle.GraphKind,
            cycle.EdgeCount,
            cycle.NodeIds.Count);
        await cycleCommand.ExecuteNonQueryAsync(cancellationToken);

        for (int index = 0; index < cycle.NodeIds.Count; index++)
        {
            SetParameterValues(
                cycleNodeCommand,
                snapshotId,
                cycle.CycleId,
                cycle.GraphKind,
                cycle.NodeIds[index],
                index);
            await cycleNodeCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task DeleteObsoleteSnapshotsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT snapshot_id
            FROM snapshots AS snapshot
            WHERE (
                SELECT COUNT(*)
                FROM snapshots AS newer
                WHERE newer.solution_path = snapshot.solution_path
                  AND newer.snapshot_id > snapshot.snapshot_id
            ) >= $keepCount
            ORDER BY snapshot_id DESC;
            """;
        command.Parameters.AddWithValue("$keepCount", MaxSnapshotsToKeep);

        long[] obsoleteSnapshotIds;
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            var collectedSnapshotIds = new System.Collections.Generic.List<long>();
            while (await reader.ReadAsync(cancellationToken))
            {
                collectedSnapshotIds.Add(reader.GetInt64(0));
            }

            obsoleteSnapshotIds = [.. collectedSnapshotIds];
        }

        if (obsoleteSnapshotIds.Length == 0)
        {
            return;
        }

        string snapshotIdList = string.Join(", ", obsoleteSnapshotIds);
        string[] dependentTables =
        [
            "snapshot_projects",
            "snapshot_documents",
            "snapshot_dependencies",
            "snapshot_symbols",
            "snapshot_document_dependencies",
            "snapshot_symbol_dependencies",
            "snapshot_cycles",
            "snapshot_cycle_nodes",
            "snapshot_diagnostics"
        ];

        foreach (string tableName in dependentTables)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                $"DELETE FROM {tableName} WHERE snapshot_id IN ({snapshotIdList});",
                cancellationToken);
        }

        if (_ftsEnabled)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                $"DELETE FROM snapshot_symbol_fts WHERE snapshot_id IN ({snapshotIdList});",
                cancellationToken);
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            $"DELETE FROM snapshots WHERE snapshot_id IN ({snapshotIdList});",
            cancellationToken);
    }

    private static async Task<CachedSnapshotHeader?> LoadSnapshotHeaderAsync(
        SqliteConnection connection,
        string workspacePath,
        string resolvedWorkspacePath,
        bool hasSnapshotFormatVersionColumn,
        CancellationToken cancellationToken)
    {
        string solutionPathPredicate = OperatingSystem.IsWindows()
            ? "(solution_path = $solutionPath OR solution_path = $legacySolutionPath COLLATE NOCASE)"
            : "solution_path = $solutionPath";
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                snapshot_id,
                solution_path,
                workspace_kind,
                {(hasSnapshotFormatVersionColumn ? "snapshot_format_version" : "1 AS snapshot_format_version")},
                analyzed_at_utc
            FROM snapshots
            WHERE {solutionPathPredicate}
            {(hasSnapshotFormatVersionColumn ? "AND snapshot_format_version = $snapshotFormatVersion" : string.Empty)}
            ORDER BY snapshot_id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$solutionPath", workspacePath);
        if (OperatingSystem.IsWindows())
        {
            command.Parameters.AddWithValue("$legacySolutionPath", resolvedWorkspacePath);
        }

        if (hasSnapshotFormatVersionColumn)
        {
            command.Parameters.AddWithValue("$snapshotFormatVersion", CurrentSnapshotFormatVersion);
        }

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        long snapshotId = reader.GetInt64(0);
        string storedWorkspacePath = reader.GetString(1);
        string workspaceKind = reader.IsDBNull(2) ? "workspace" : reader.GetString(2);
        int snapshotFormatVersion = reader.IsDBNull(3) ? 1 : reader.GetInt32(3);
        string analyzedAtRaw = reader.GetString(4);
        DateTimeOffset analyzedAt = DateTimeOffset.TryParse(analyzedAtRaw, out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

        return new CachedSnapshotHeader(snapshotId, storedWorkspacePath, workspaceKind, snapshotFormatVersion, analyzedAt);
    }

    private static async Task<IReadOnlyList<CachedProjectRecord>> LoadProjectRecordsAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT project_key, project_name, language, project_file_path, is_folder_based
            FROM snapshot_projects
            WHERE snapshot_id = $snapshotId
            ORDER BY project_name, project_key;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<CachedProjectRecord> records = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new CachedProjectRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                GetNullableString(reader, 3),
                reader.GetInt64(4) != 0));
        }

        return records;
    }

    private static async Task<IReadOnlyList<CachedDocumentRecord>> LoadDocumentRecordsAsync(
        SqliteConnection connection,
        long snapshotId,
        IReadOnlyList<CachedSymbolRecord> symbolRecords,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "snapshot_documents", cancellationToken))
        {
            HashSet<(string ProjectKey, string ProjectName, string DocumentId, string DocumentName, string? DocumentFilePath)> seenDocuments = [];
            List<CachedDocumentRecord> legacyRecords = [];
            foreach (CachedSymbolRecord record in symbolRecords)
            {
                var key = (
                    record.ProjectKey,
                    record.ProjectName,
                    record.DocumentId,
                    record.DocumentName,
                    record.DocumentFilePath);
                if (!seenDocuments.Add(key))
                {
                    continue;
                }

                legacyRecords.Add(new CachedDocumentRecord(
                    record.ProjectKey,
                    record.ProjectName,
                    record.DocumentId,
                    record.DocumentName,
                    record.DocumentFilePath));
            }

            legacyRecords.Sort(static (left, right) =>
            {
                int projectNameCompare = StringComparer.OrdinalIgnoreCase.Compare(left.ProjectName, right.ProjectName);
                if (projectNameCompare != 0)
                {
                    return projectNameCompare;
                }

                int projectKeyCompare = StringComparer.Ordinal.Compare(left.ProjectKey, right.ProjectKey);
                if (projectKeyCompare != 0)
                {
                    return projectKeyCompare;
                }

                int documentNameCompare = StringComparer.OrdinalIgnoreCase.Compare(left.DocumentName, right.DocumentName);
                return documentNameCompare != 0
                    ? documentNameCompare
                    : StringComparer.OrdinalIgnoreCase.Compare(left.DocumentFilePath, right.DocumentFilePath);
            });

            return legacyRecords;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT project_key, project_name, document_id, document_name, document_file_path
            FROM snapshot_documents
            WHERE snapshot_id = $snapshotId
            ORDER BY project_name, project_key, document_name, document_file_path;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<CachedDocumentRecord> records = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new CachedDocumentRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                GetNullableString(reader, 4)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<CachedSymbolRecord>> LoadSymbolRecordsAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        HashSet<string> columns = await LoadColumnNamesAsync(connection, "snapshot_symbols", cancellationToken);
        bool hasDocumentId = columns.Contains("document_id");
        bool hasDocumentFilePath = columns.Contains("document_file_path");
        bool hasDisplayName = columns.Contains("display_name");
        bool hasProjectKey = columns.Contains("project_key");

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                symbol_id,
                {(hasProjectKey ? "project_key" : "project_name AS project_key")},
                project_name,
                {(hasDocumentId ? "document_id" : "NULL AS document_id")},
                document_name,
                {(hasDocumentFilePath ? "document_file_path" : "NULL AS document_file_path")},
                symbol_kind,
                symbol_name,
                {(hasDisplayName ? "display_name" : "symbol_name AS display_name")},
                line_number
            FROM snapshot_symbols
            WHERE snapshot_id = $snapshotId
            ORDER BY project_name, {(hasProjectKey ? "project_key" : "project_name")}, document_name, line_number, {(hasDisplayName ? "display_name" : "symbol_name")};
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<CachedSymbolRecord> records = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string documentId = GetNullableString(reader, 3) ?? BuildLegacyDocumentId(
                reader.GetString(2),
                reader.GetString(4),
                GetNullableString(reader, 5));

            records.Add(new CachedSymbolRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                documentId,
                reader.GetString(4),
                GetNullableString(reader, 5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<CachedDependencyRecord>> LoadDependencyRecordsAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT project_key, project_name, dependency_kind, dependency_name, dependency_origin, confidence, imported_symbols
            FROM snapshot_dependencies
            WHERE snapshot_id = $snapshotId
            ORDER BY project_name, project_key, dependency_kind, dependency_name;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<CachedDependencyRecord> records = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new CachedDependencyRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                GetNullableString(reader, 4),
                GetNullableString(reader, 5),
                GetNullableString(reader, 6)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<DocumentDependencySummary>> LoadDocumentDependenciesAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        HashSet<string> columns = await LoadColumnNamesAsync(
            connection,
            "snapshot_document_dependencies",
            cancellationToken);
        bool hasSampleFilePath = columns.Contains("sample_file_path");
        bool hasSampleLineNumber = columns.Contains("sample_line_number");
        bool hasSampleSnippet = columns.Contains("sample_snippet");

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT source_document_id, target_document_id, reference_count,
                   {(hasSampleFilePath ? "sample_file_path" : "NULL AS sample_file_path")},
                   {(hasSampleLineNumber ? "sample_line_number" : "NULL AS sample_line_number")},
                   {(hasSampleSnippet ? "sample_snippet" : "NULL AS sample_snippet")}
            FROM snapshot_document_dependencies
            WHERE snapshot_id = $snapshotId
            ORDER BY source_document_id, target_document_id;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<DocumentDependencySummary> dependencies = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dependencies.Add(new DocumentDependencySummary(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return dependencies;
    }

    private static async Task<IReadOnlyList<SymbolDependencySummary>> LoadSymbolDependenciesAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        HashSet<string> columns = await LoadColumnNamesAsync(
            connection,
            "snapshot_symbol_dependencies",
            cancellationToken);
        bool hasSampleFilePath = columns.Contains("sample_file_path");
        bool hasSampleLineNumber = columns.Contains("sample_line_number");
        bool hasSampleSnippet = columns.Contains("sample_snippet");

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT source_symbol_id, target_symbol_id, reference_count, reference_kind, confidence,
                   {(hasSampleFilePath ? "sample_file_path" : "NULL AS sample_file_path")},
                   {(hasSampleLineNumber ? "sample_line_number" : "NULL AS sample_line_number")},
                   {(hasSampleSnippet ? "sample_snippet" : "NULL AS sample_snippet")}
            FROM snapshot_symbol_dependencies
            WHERE snapshot_id = $snapshotId
            ORDER BY source_symbol_id, target_symbol_id;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<SymbolDependencySummary> dependencies = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            dependencies.Add(new SymbolDependencySummary(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? "reference" : reader.GetString(3),
                reader.IsDBNull(4) ? "high" : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return dependencies;
    }

    private static async Task<IReadOnlyList<DependencyCycleSummary>> LoadCyclesAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> nodeIdsByCycleId = await LoadCycleNodesByCycleIdAsync(
            connection,
            snapshotId,
            cancellationToken);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT cycle_id, graph_kind, edge_count, node_count
            FROM snapshot_cycles
            WHERE snapshot_id = $snapshotId
            ORDER BY cycle_id;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<DependencyCycleSummary> cycles = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string cycleId = reader.GetString(0);
            string graphKind = reader.GetString(1);
            int edgeCount = reader.GetInt32(2);
            IReadOnlyList<string> nodeIds = nodeIdsByCycleId.TryGetValue(cycleId, out IReadOnlyList<string>? resolvedNodeIds)
                ? resolvedNodeIds
                : Array.Empty<string>();

            cycles.Add(new DependencyCycleSummary(
                graphKind,
                cycleId,
                nodeIds,
                edgeCount));
        }

        return cycles;
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadCycleNodesByCycleIdAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT cycle_id, node_id
            FROM snapshot_cycle_nodes
            WHERE snapshot_id = $snapshotId
            ORDER BY cycle_id, sort_order;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        Dictionary<string, List<string>> nodeIdsByCycleId = new(StringComparer.Ordinal);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string cycleId = reader.GetString(0);
            if (!nodeIdsByCycleId.TryGetValue(cycleId, out List<string>? nodeIds))
            {
                nodeIds = [];
                nodeIdsByCycleId.Add(cycleId, nodeIds);
            }

            nodeIds.Add(reader.GetString(1));
        }

        Dictionary<string, IReadOnlyList<string>> readonlyNodeIdsByCycleId = new(nodeIdsByCycleId.Count, StringComparer.Ordinal);
        foreach ((string cycleId, List<string> nodeIds) in nodeIdsByCycleId)
        {
            readonlyNodeIdsByCycleId.Add(cycleId, nodeIds);
        }

        return readonlyNodeIdsByCycleId;
    }

    private static async Task<IReadOnlyList<string>> LoadDiagnosticsAsync(
        SqliteConnection connection,
        long snapshotId,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "snapshot_diagnostics", cancellationToken))
        {
            return Array.Empty<string>();
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT message
            FROM snapshot_diagnostics
            WHERE snapshot_id = $snapshotId
            ORDER BY ordinal;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        List<string> diagnostics = [];
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            diagnostics.Add(reader.GetString(0));
        }

        return diagnostics;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT 1
            FROM sqlite_master
            WHERE type IN ('table', 'view') AND name = $tableName
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);

        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is not null && scalar != DBNull.Value;
    }

    private static async Task<HashSet<string>> LoadColumnNamesAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        if (!await TableExistsAsync(connection, tableName, cancellationToken))
        {
            return columns;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task<bool> RequiresSnapshotSchemaResetAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "snapshot_projects", cancellationToken))
        {
            return false;
        }

        HashSet<string> projectColumns = await LoadColumnNamesAsync(connection, "snapshot_projects", cancellationToken);
        if (!projectColumns.Contains("project_key"))
        {
            return true;
        }

        HashSet<string> documentColumns = await LoadColumnNamesAsync(connection, "snapshot_documents", cancellationToken);
        if (documentColumns.Count > 0 && !documentColumns.Contains("project_key"))
        {
            return true;
        }

        HashSet<string> dependencyColumns = await LoadColumnNamesAsync(connection, "snapshot_dependencies", cancellationToken);
        if (dependencyColumns.Count > 0 && !dependencyColumns.Contains("project_key"))
        {
            return true;
        }

        HashSet<string> symbolColumns = await LoadColumnNamesAsync(connection, "snapshot_symbols", cancellationToken);
        return symbolColumns.Count > 0 && !symbolColumns.Contains("project_key");
    }

    private static async Task ResetSnapshotSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            DROP TABLE IF EXISTS snapshot_symbol_fts;
            DROP TABLE IF EXISTS snapshot_cycle_nodes;
            DROP TABLE IF EXISTS snapshot_cycles;
            DROP TABLE IF EXISTS snapshot_symbol_dependencies;
            DROP TABLE IF EXISTS snapshot_document_dependencies;
            DROP TABLE IF EXISTS snapshot_symbols;
            DROP TABLE IF EXISTS snapshot_dependencies;
            DROP TABLE IF EXISTS snapshot_diagnostics;
            DROP TABLE IF EXISTS snapshot_documents;
            DROP TABLE IF EXISTS snapshot_projects;
            DROP TABLE IF EXISTS snapshots;
            """,
            cancellationToken);
    }

    private static string? GetNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static void DeleteFileIfExists(string filePath)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(40);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(40);
            }
        }
    }

    private static string NormalizeWorkspacePathForStorage(string workspacePath)
    {
        return PathNormalization.NormalizeDirectoryLikePath(workspacePath, uppercaseWindows: true);
    }

    private static string BuildLegacyDocumentId(string projectName, string documentName, string? documentFilePath)
    {
        string identity = !string.IsNullOrWhiteSpace(documentFilePath)
            ? $"{projectName}|{Path.GetFullPath(documentFilePath)}"
            : $"{projectName}/{documentName}";

        return $"document:{NormalizeToken(identity)}";
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        char[] normalized = value
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        return new string(normalized);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqliteCommand CreateParameterizedCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        params (string Name, object? DefaultValue)[] parameters)
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach ((string name, object? defaultValue) in parameters)
        {
            command.Parameters.AddWithValue(name, defaultValue ?? DBNull.Value);
        }

        return command;
    }

    private static void SetParameterValues(SqliteCommand command, params object?[] values)
    {
        if (command.Parameters.Count != values.Length)
        {
            throw new InvalidOperationException("The number of values must match the prepared command parameters.");
        }

        for (int index = 0; index < values.Length; index++)
        {
            command.Parameters[index].Value = values[index] ?? DBNull.Value;
        }
    }

    private static async Task EnsureColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string alterSql,
        CancellationToken cancellationToken)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        bool columnExists = false;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string existingColumnName = reader.GetString(1);
            if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                columnExists = true;
                break;
            }
        }

        if (!columnExists)
        {
            await ExecuteNonQueryAsync(connection, alterSql, cancellationToken);
        }
    }

    private sealed class SnapshotInsertCommands : IDisposable
    {
        public SnapshotInsertCommands(
            SqliteCommand projectCommand,
            SqliteCommand dependencyCommand,
            SqliteCommand documentCommand,
            SqliteCommand symbolCommand,
            SqliteCommand? symbolFtsCommand,
            SqliteCommand diagnosticCommand,
            SqliteCommand documentDependencyCommand,
            SqliteCommand symbolDependencyCommand,
            SqliteCommand cycleCommand,
            SqliteCommand cycleNodeCommand)
        {
            ProjectCommand = projectCommand;
            DependencyCommand = dependencyCommand;
            DocumentCommand = documentCommand;
            SymbolCommand = symbolCommand;
            SymbolFtsCommand = symbolFtsCommand;
            DiagnosticCommand = diagnosticCommand;
            DocumentDependencyCommand = documentDependencyCommand;
            SymbolDependencyCommand = symbolDependencyCommand;
            CycleCommand = cycleCommand;
            CycleNodeCommand = cycleNodeCommand;
        }

        public SqliteCommand ProjectCommand { get; }

        public SqliteCommand DependencyCommand { get; }

        public SqliteCommand DocumentCommand { get; }

        public SqliteCommand SymbolCommand { get; }

        public SqliteCommand? SymbolFtsCommand { get; }

        public SqliteCommand DiagnosticCommand { get; }

        public SqliteCommand DocumentDependencyCommand { get; }

        public SqliteCommand SymbolDependencyCommand { get; }

        public SqliteCommand CycleCommand { get; }

        public SqliteCommand CycleNodeCommand { get; }

        public void Dispose()
        {
            CycleNodeCommand.Dispose();
            CycleCommand.Dispose();
            SymbolDependencyCommand.Dispose();
            DocumentDependencyCommand.Dispose();
            DiagnosticCommand.Dispose();
            SymbolFtsCommand?.Dispose();
            SymbolCommand.Dispose();
            DocumentCommand.Dispose();
            DependencyCommand.Dispose();
            ProjectCommand.Dispose();
        }
    }

    private sealed record CachedSnapshotHeader(
        long SnapshotId,
        string WorkspacePath,
        string WorkspaceKind,
        int SnapshotFormatVersion,
        DateTimeOffset AnalyzedAt);

    private sealed record CachedProjectRecord(
        string ProjectKey,
        string ProjectName,
        string Language,
        string? ProjectFilePath,
        bool IsFolderBased);

    private sealed record CachedDocumentRecord(
        string ProjectKey,
        string ProjectName,
        string DocumentId,
        string DocumentName,
        string? DocumentFilePath);

    private sealed record CachedSymbolRecord(
        string SymbolId,
        string ProjectKey,
        string ProjectName,
        string DocumentId,
        string DocumentName,
        string? DocumentFilePath,
        string SymbolKind,
        string SymbolName,
        string SymbolDisplayName,
        int LineNumber);

    private sealed record CachedDependencyRecord(
        string ProjectKey,
        string ProjectName,
        string DependencyKind,
        string DependencyName,
        string? DependencyOrigin,
        string? Confidence,
        string? ImportedSymbols);
}
