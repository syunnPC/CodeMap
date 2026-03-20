using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeMap.ViewModels;

namespace CodeMap;

public sealed partial class MainWindow
{
    private sealed class JsonFileWriteCoordinator
    {
        private long _latestVersion;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public long AdvanceVersion()
        {
            return Interlocked.Increment(ref _latestVersion);
        }

        public long CurrentVersion => Interlocked.Read(ref _latestVersion);

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _gate.WaitAsync(cancellationToken);
        }

        public void Wait()
        {
            _gate.Wait();
        }

        public void Release()
        {
            _gate.Release();
        }
    }

    private readonly JsonFileWriteCoordinator _appPreferencesSaveCoordinator = new();
    private readonly JsonFileWriteCoordinator _recentSolutionsSaveCoordinator = new();
    private readonly JsonFileWriteCoordinator _solutionViewStatesSaveCoordinator = new();

    private static string? DiscoverDefaultSolutionPath()
    {
        DirectoryInfo? cursor = new(Environment.CurrentDirectory);

        for (int depth = 0; depth < 6 && cursor is not null; depth++)
        {
            FileInfo? slnx = cursor.GetFiles("*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (slnx is not null)
            {
                return slnx.FullName;
            }

            FileInfo? sln = cursor.GetFiles("*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (sln is not null)
            {
                return sln.FullName;
            }

            cursor = cursor.Parent;
        }

        return Directory.Exists(Environment.CurrentDirectory)
            ? Environment.CurrentDirectory
            : null;
    }

    private void LoadSolutionViewStates()
    {
        _solutionViewStates.Clear();

        try
        {
            if (!File.Exists(s_solutionViewStatesFilePath))
            {
                return;
            }

            string json = File.ReadAllText(s_solutionViewStatesFilePath);
            Dictionary<string, SolutionViewState>? items = JsonSerializer.Deserialize(
                json,
                typeof(Dictionary<string, SolutionViewState>),
                CodeMapJsonSerializerContext.Default) as Dictionary<string, SolutionViewState>;
            if (items is null)
            {
                return;
            }

            foreach ((string solutionPath, SolutionViewState state) in items)
            {
                if (string.IsNullOrWhiteSpace(solutionPath))
                {
                    continue;
                }

                string normalizedPath = NormalizeSolutionPath(solutionPath);
                _solutionViewStates[normalizedPath] = NormalizeSolutionViewState(state);
            }
        }
        catch (Exception ex)
        {
            AppendLocalizedDiagnostics("diag.persistence.solutionViewStateLoadFailed", ex.Message);
        }
    }

    private void SaveSolutionViewStates()
    {
        Dictionary<string, SolutionViewState> snapshot = _solutionViewStates.ToDictionary(
            entry => entry.Key,
            entry => NormalizeSolutionViewState(entry.Value),
            StringComparer.OrdinalIgnoreCase);
        ScheduleJsonFileWrite(
            ref _solutionViewStatesSaveCancellationTokenSource,
            _solutionViewStatesSaveCoordinator,
            s_solutionViewStatesFilePath,
            snapshot,
            T("diag.persistence.solutionViewStateSaveFailed"),
            ClearSolutionViewStatesSaveTokenSource);
    }

    private void LoadRecentSolutions()
    {
        _recentSolutions.Clear();

        try
        {
            if (!File.Exists(s_recentSolutionsFilePath))
            {
                return;
            }

            string json = File.ReadAllText(s_recentSolutionsFilePath);
            List<string>? items = JsonSerializer.Deserialize(
                json,
                typeof(List<string>),
                CodeMapJsonSerializerContext.Default) as List<string>;
            if (items is null)
            {
                return;
            }

            foreach (string item in items.Take(MaxRecentSolutions))
            {
                string normalizedPath = NormalizeSolutionPath(item);
                if (string.IsNullOrWhiteSpace(normalizedPath) ||
                    _recentSolutions.Any(existing => string.Equals(existing, normalizedPath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                _recentSolutions.Add(normalizedPath);
            }
        }
        catch (Exception ex)
        {
            AppendLocalizedDiagnostics("diag.persistence.recentWorkspaceLoadFailed", ex.Message);
        }
    }

    private void SaveRecentSolutions()
    {
        string[] snapshot = _recentSolutions
            .Select(NormalizeSolutionPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentSolutions)
            .ToArray();

        ScheduleJsonFileWrite(
            ref _recentSolutionsSaveCancellationTokenSource,
            _recentSolutionsSaveCoordinator,
            s_recentSolutionsFilePath,
            snapshot,
            T("diag.persistence.recentWorkspaceSaveFailed"),
            ClearRecentSolutionsSaveTokenSource);
    }

    private static void CancelPendingSave(ref CancellationTokenSource? cancellationTokenSource)
    {
        CancellationTokenSource? previous = Interlocked.Exchange(ref cancellationTokenSource, null);
        if (previous is null)
        {
            return;
        }

        RequestCancellation(previous);
    }

    private void ScheduleJsonFileWrite<T>(
        ref CancellationTokenSource? cancellationTokenSource,
        JsonFileWriteCoordinator coordinator,
        string filePath,
        T value,
        string failureMessage,
        Action<CancellationTokenSource> clearSaveTokenSource)
    {
        try
        {
            long scheduledVersion = coordinator.AdvanceVersion();
            CancellationTokenSource next = new();
            CancellationTokenSource? previous = Interlocked.Exchange(ref cancellationTokenSource, next);
            RequestCancellation(previous);
            _ = PersistJsonFileAsync(
                filePath,
                value,
                failureMessage,
                next,
                clearSaveTokenSource,
                coordinator,
                scheduledVersion);
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"{failureMessage}: {ex.Message}");
        }
    }

    private async Task PersistJsonFileAsync<T>(
        string filePath,
        T value,
        string failureMessage,
        CancellationTokenSource cancellationTokenSource,
        Action<CancellationTokenSource> clearSaveTokenSource,
        JsonFileWriteCoordinator coordinator,
        long scheduledVersion)
    {
        try
        {
            await Task.Delay(JsonFileWriteDebounceMilliseconds, cancellationTokenSource.Token).ConfigureAwait(false);
            if (cancellationTokenSource.IsCancellationRequested || scheduledVersion != coordinator.CurrentVersion)
            {
                return;
            }

            string json = JsonSerializer.Serialize(
                value,
                typeof(T),
                CodeMapJsonSerializerContext.Default);
            await WriteTextFileAtomicallyAsync(
                filePath,
                json,
                cancellationTokenSource.Token,
                coordinator,
                scheduledVersion).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (!_isWindowClosed)
            {
                AppendDiagnosticsLine($"{failureMessage}: {ex.Message}");
            }
        }
        finally
        {
            clearSaveTokenSource(cancellationTokenSource);
            cancellationTokenSource.Dispose();
        }
    }

    private void ClearRecentSolutionsSaveTokenSource(CancellationTokenSource cancellationTokenSource)
    {
        Interlocked.CompareExchange(ref _recentSolutionsSaveCancellationTokenSource, null, cancellationTokenSource);
    }

    private void ClearSolutionViewStatesSaveTokenSource(CancellationTokenSource cancellationTokenSource)
    {
        Interlocked.CompareExchange(ref _solutionViewStatesSaveCancellationTokenSource, null, cancellationTokenSource);
    }

    private static async Task WriteTextFileAtomicallyAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken,
        JsonFileWriteCoordinator coordinator,
        long scheduledVersion)
    {
        try
        {
            await coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scheduledVersion != coordinator.CurrentVersion)
                {
                    return;
                }

                await WriteTextFileAtomicallyCoreAsync(
                    filePath,
                    content,
                    cancellationToken,
                    static (path, text, token) => File.WriteAllTextAsync(path, text, token)).ConfigureAwait(false);
            }
            finally
            {
                coordinator.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IOException($"ファイル書き込みに失敗しました: {filePath}", ex);
        }
    }

    private void SaveRecentSolutionsImmediately()
    {
        long scheduledVersion = _recentSolutionsSaveCoordinator.AdvanceVersion();
        CancelPendingSave(ref _recentSolutionsSaveCancellationTokenSource);
        string[] snapshot = _recentSolutions
            .Select(NormalizeSolutionPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentSolutions)
            .ToArray();
        WriteJsonFileImmediately(
            s_recentSolutionsFilePath,
            snapshot,
            T("diag.persistence.recentWorkspaceSaveFailed"),
            _recentSolutionsSaveCoordinator,
            scheduledVersion);
    }

    private void SaveSolutionViewStatesImmediately()
    {
        long scheduledVersion = _solutionViewStatesSaveCoordinator.AdvanceVersion();
        CancelPendingSave(ref _solutionViewStatesSaveCancellationTokenSource);
        Dictionary<string, SolutionViewState> snapshot = _solutionViewStates.ToDictionary(
            entry => entry.Key,
            entry => NormalizeSolutionViewState(entry.Value),
            StringComparer.OrdinalIgnoreCase);
        WriteJsonFileImmediately(
            s_solutionViewStatesFilePath,
            snapshot,
            T("diag.persistence.solutionViewStateSaveFailed"),
            _solutionViewStatesSaveCoordinator,
            scheduledVersion);
    }

    private void WriteJsonFileImmediately<T>(
        string filePath,
        T value,
        string failureMessage,
        JsonFileWriteCoordinator coordinator,
        long scheduledVersion)
    {
        try
        {
            string json = JsonSerializer.Serialize(
                value,
                typeof(T),
                CodeMapJsonSerializerContext.Default);
            WriteTextFileAtomically(filePath, json, coordinator, scheduledVersion);
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"{failureMessage}: {ex.Message}");
        }
    }

    private static void WriteTextFileAtomically(
        string filePath,
        string content,
        JsonFileWriteCoordinator coordinator,
        long scheduledVersion)
    {
        try
        {
            coordinator.Wait();
            try
            {
                if (scheduledVersion != coordinator.CurrentVersion)
                {
                    return;
                }

                WriteTextFileAtomicallyCoreAsync(
                        filePath,
                        content,
                        CancellationToken.None,
                        static (path, text, _) =>
                        {
                            File.WriteAllText(path, text);
                            return Task.CompletedTask;
                        })
                    .GetAwaiter()
                    .GetResult();
            }
            finally
            {
                coordinator.Release();
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"ファイル書き込みに失敗しました: {filePath}", ex);
        }
    }

    private static async Task WriteTextFileAtomicallyCoreAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken,
        Func<string, string, CancellationToken, Task> writeContentAsync)
    {
        string? directoryPath = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await writeContentAsync(tempFilePath, content, cancellationToken).ConfigureAwait(false);
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static void RequestCancellation(CancellationTokenSource? cancellationTokenSource)
    {
        if (cancellationTokenSource is null)
        {
            return;
        }

        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
