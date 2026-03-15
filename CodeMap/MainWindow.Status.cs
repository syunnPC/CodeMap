using Microsoft.UI.Xaml;
using System.Collections.Generic;

namespace CodeMap;

public sealed partial class MainWindow
{
    private enum StatusSeverity
    {
        Information = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    private enum StatusCode
    {
        Ready = 0,
        InitialCacheFallback,
        InitFailed,
        InitFailedRetry,
        GraphRetry,
        WorkspacePathRequired,
        WorkspaceNotFound,
        Analyzing,
        AnalyzeComplete,
        AnalyzeCanceled,
        AnalyzeFailed,
        CacheLoading,
        CacheNotFound,
        CacheLoaded,
        CacheFailed,
        GraphInitializing,
        GraphInitFailedRetrying,
        GraphLoading,
        GraphReady,
        GraphEngineRestarting,
        GraphReloading,
        GraphRecovering,
        GraphRecreating,
        GraphBuild,
        GraphBuildFailed,
        NodeSelected,
        NodeFocus,
        NodeFocusFailed,
        GraphRendered,
        GraphRenderedSimple
    }

    private static readonly IReadOnlyDictionary<StatusCode, StatusDefinition> s_statusDefinitions =
        new Dictionary<StatusCode, StatusDefinition>
        {
            [StatusCode.Ready] = new("status.ready", StatusSeverity.Success),
            [StatusCode.InitialCacheFallback] = new("status.initialCacheFallback", StatusSeverity.Warning),
            [StatusCode.InitFailed] = new("status.initFailed", StatusSeverity.Error),
            [StatusCode.InitFailedRetry] = new("status.initFailedRetry", StatusSeverity.Error),
            [StatusCode.GraphRetry] = new("status.graphRetry", StatusSeverity.Error),
            [StatusCode.WorkspacePathRequired] = new("status.workspacePathRequired", StatusSeverity.Warning),
            [StatusCode.WorkspaceNotFound] = new("status.workspaceNotFound", StatusSeverity.Error),
            [StatusCode.Analyzing] = new("status.analyzing", StatusSeverity.Warning),
            [StatusCode.AnalyzeComplete] = new("status.analyzeComplete", StatusSeverity.Success),
            [StatusCode.AnalyzeCanceled] = new("status.analyzeCanceled", StatusSeverity.Warning),
            [StatusCode.AnalyzeFailed] = new("status.analyzeFailed", StatusSeverity.Error),
            [StatusCode.CacheLoading] = new("status.cacheLoading", StatusSeverity.Warning),
            [StatusCode.CacheNotFound] = new("status.cacheNotFound", StatusSeverity.Warning),
            [StatusCode.CacheLoaded] = new("status.cacheLoaded", StatusSeverity.Success),
            [StatusCode.CacheFailed] = new("status.cacheFailed", StatusSeverity.Error),
            [StatusCode.GraphInitializing] = new("status.graphInitializing", StatusSeverity.Warning),
            [StatusCode.GraphInitFailedRetrying] = new("status.graphInitFailedRetrying", StatusSeverity.Warning),
            [StatusCode.GraphLoading] = new("status.graphLoading", StatusSeverity.Warning),
            [StatusCode.GraphReady] = new("status.graphReady", StatusSeverity.Success),
            [StatusCode.GraphEngineRestarting] = new("status.graphEngineRestarting", StatusSeverity.Warning),
            [StatusCode.GraphReloading] = new("status.graphReloading", StatusSeverity.Warning),
            [StatusCode.GraphRecovering] = new("status.graphRecovering", StatusSeverity.Warning),
            [StatusCode.GraphRecreating] = new("status.graphRecreating", StatusSeverity.Warning),
            [StatusCode.GraphBuild] = new("status.graphBuild", StatusSeverity.Warning),
            [StatusCode.GraphBuildFailed] = new("status.graphBuildFailed", StatusSeverity.Error),
            [StatusCode.NodeSelected] = new("status.nodeSelected", StatusSeverity.Information),
            [StatusCode.NodeFocus] = new("status.nodeFocus", StatusSeverity.Information),
            [StatusCode.NodeFocusFailed] = new("status.nodeFocusFailed", StatusSeverity.Error),
            [StatusCode.GraphRendered] = new("status.graphRendered", StatusSeverity.Success),
            [StatusCode.GraphRenderedSimple] = new("status.graphRenderedSimple", StatusSeverity.Success)
        };

    private void StartWorkspaceOperation(WorkspaceOperation operation)
    {
        if (_isWindowClosed)
        {
            return;
        }

        switch (operation)
        {
            case WorkspaceOperation.LoadingCache:
                _activeCacheLoadOperationCount++;
                break;
            case WorkspaceOperation.Analyzing:
                _activeAnalyzeOperationCount++;
                break;
            default:
                return;
        }

        UpdateWorkspaceOperationVisualState();
    }

    private void FinishWorkspaceOperation(WorkspaceOperation operation)
    {
        if (_isWindowClosed)
        {
            return;
        }

        switch (operation)
        {
            case WorkspaceOperation.LoadingCache:
                if (_activeCacheLoadOperationCount > 0)
                {
                    _activeCacheLoadOperationCount--;
                }

                break;
            case WorkspaceOperation.Analyzing:
                if (_activeAnalyzeOperationCount > 0)
                {
                    _activeAnalyzeOperationCount--;
                }

                break;
            default:
                return;
        }

        UpdateWorkspaceOperationVisualState();
    }

    private void UpdateWorkspaceOperationVisualState()
    {
        if (_isWindowClosed)
        {
            return;
        }

        bool isLoadingCache = _activeCacheLoadOperationCount > 0;
        LoadCacheButtonIcon.Visibility = isLoadingCache ? Visibility.Collapsed : Visibility.Visible;
        LoadCacheButtonProgressRing.IsActive = isLoadingCache;
        LoadCacheButtonProgressRing.Visibility = isLoadingCache ? Visibility.Visible : Visibility.Collapsed;

        bool isAnalyzing = _activeAnalyzeOperationCount > 0;
        AnalyzeButtonIcon.Visibility = isAnalyzing ? Visibility.Collapsed : Visibility.Visible;
        AnalyzeButtonProgressRing.IsActive = isAnalyzing;
        AnalyzeButtonProgressRing.Visibility = isAnalyzing ? Visibility.Visible : Visibility.Collapsed;

        UpdateGraphRecoveryOverlayVisualState();
    }

    private void UpdateStatus(StatusCode statusCode, params object[] args)
    {
        if (ShouldSuppressStatusCodeWhileAnalyzing(statusCode))
        {
            return;
        }

        UpdateStatus(
            T(GetStatusLocalizationKey(statusCode), args),
            GetStatusSeverity(statusCode));
    }

    private void UpdateStatus(
        string statusMessage,
        StatusSeverity severity = StatusSeverity.Information,
        bool suppressLog = false)
    {
        if (_isWindowClosed)
        {
            return;
        }

        StatusTextBlock.Text = statusMessage;
        UpdateStatusIcon(severity);
        if (!suppressLog)
        {
            LogInfo($"Status: {statusMessage}");
        }
    }

    private static string GetStatusLocalizationKey(StatusCode statusCode)
    {
        return ResolveStatusDefinition(statusCode).LocalizationKey;
    }

    private static StatusSeverity GetStatusSeverity(StatusCode statusCode)
    {
        return ResolveStatusDefinition(statusCode).Severity;
    }

    private const string ErrorIconGlyph = "\uEA39";
    private const string WarningIconGlyph = "\uE895";
    private const string SuccessIconGlyph = "\uE73E";
    private const string InfoIconGlyph = "\uE946";

    private static readonly Microsoft.UI.Color ErrorFallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 205, 76, 89);
    private static readonly Microsoft.UI.Color WarningFallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 214, 171, 45);
    private static readonly Microsoft.UI.Color SuccessFallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 72, 163, 102);
    private static readonly Microsoft.UI.Color NeutralFallbackColor = Microsoft.UI.ColorHelper.FromArgb(255, 142, 142, 142);

    private void UpdateStatusIcon(StatusSeverity severity)
    {
        (string glyph, string brushKey, Microsoft.UI.Color fallbackColor) = severity switch
        {
            StatusSeverity.Error => (ErrorIconGlyph, "SystemFillColorCriticalBrush", ErrorFallbackColor),
            StatusSeverity.Warning => (WarningIconGlyph, "SystemFillColorCautionBrush", WarningFallbackColor),
            StatusSeverity.Success => (SuccessIconGlyph, "SystemFillColorSuccessBrush", SuccessFallbackColor),
            _ => (InfoIconGlyph, "SystemFillColorNeutralBrush", NeutralFallbackColor),
        };

        StatusIcon.Glyph = glyph;
        StatusIcon.Foreground = GetThemeBrush(brushKey, fallbackColor);
    }

    private static StatusDefinition ResolveStatusDefinition(StatusCode statusCode)
    {
        return s_statusDefinitions.TryGetValue(statusCode, out StatusDefinition? definition)
            ? definition
            : new StatusDefinition("status.ready", StatusSeverity.Information);
    }

    private bool ShouldSuppressStatusCodeWhileAnalyzing(StatusCode statusCode)
    {
        if (_activeAnalyzeOperationCount <= 0)
        {
            return false;
        }

        return statusCode switch
        {
            StatusCode.Analyzing => false,
            StatusCode.AnalyzeComplete => false,
            StatusCode.AnalyzeCanceled => false,
            StatusCode.AnalyzeFailed => false,
            StatusCode.GraphRetry => false,
            StatusCode.InitFailed => false,
            StatusCode.InitFailedRetry => false,
            _ => true
        };
    }

    private sealed record StatusDefinition(string LocalizationKey, StatusSeverity Severity);
}
