using CodeMap.Analysis;
using CodeMap.Graph;
using CodeMap.Services;
using CodeMap.ViewModels;
using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CodeMap;

public sealed partial class MainWindow
{
    private static readonly Microsoft.UI.Color SecondaryTextFallbackColor =
        Microsoft.UI.ColorHelper.FromArgb(255, 160, 160, 160);

    private async void OnShowSettingsClicked(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = RootLayout.XamlRoot
        };

        ScrollViewer scrollViewer = new()
        {
            MaxHeight = 640
        };

        StackPanel root = new()
        {
            Spacing = 18,
            MaxWidth = 720
        };
        scrollViewer.Content = root;
        dialog.Content = scrollViewer;

        void Rebuild()
        {
            dialog.Title = T("settings.title");
            dialog.CloseButtonText = T("dialog.close");
            root.Children.Clear();
            root.Children.Add(BuildGeneralSettingsSection(Rebuild));
            root.Children.Add(BuildHiddenNodesSection(Rebuild));
            root.Children.Add(BuildDataManagementSection(Rebuild, dialog));
            root.Children.Add(BuildAboutSection());
        }

        Rebuild();
        await dialog.ShowAsync();
    }

    private UIElement BuildGeneralSettingsSection(Action rebuild)
    {
        StackPanel section = CreateSettingsSection(T("settings.section.general"));
        ComboBox languageComboBox = new()
        {
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        languageComboBox.Items.Add(new ComboBoxItem
        {
            Content = T("settings.language.japanese"),
            Tag = AppLocale.Japanese
        });
        languageComboBox.Items.Add(new ComboBoxItem
        {
            Content = T("settings.language.english"),
            Tag = AppLocale.English
        });
        languageComboBox.SelectedIndex = _currentLocale == AppLocale.Japanese ? 0 : 1;
        languageComboBox.SelectionChanged += (_, _) =>
        {
            if (languageComboBox.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Tag is not AppLocale selectedLocale)
            {
                return;
            }

            if (selectedLocale == _currentLocale)
            {
                return;
            }

            SetCurrentLocale(selectedLocale);
            rebuild();
        };

        ComboBox themeComboBox = new()
        {
            MinWidth = 220,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        themeComboBox.Items.Add(new ComboBoxItem
        {
            Content = T("settings.theme.system"),
            Tag = AppThemePreference.System
        });
        themeComboBox.Items.Add(new ComboBoxItem
        {
            Content = T("settings.theme.light"),
            Tag = AppThemePreference.Light
        });
        themeComboBox.Items.Add(new ComboBoxItem
        {
            Content = T("settings.theme.dark"),
            Tag = AppThemePreference.Dark
        });
        themeComboBox.SelectedIndex = _currentThemePreference switch
        {
            AppThemePreference.Light => 1,
            AppThemePreference.Dark => 2,
            _ => 0
        };
        themeComboBox.SelectionChanged += (_, _) =>
        {
            if (themeComboBox.SelectedItem is not ComboBoxItem selectedItem ||
                selectedItem.Tag is not AppThemePreference selectedThemePreference)
            {
                return;
            }

            SetCurrentThemePreference(selectedThemePreference);
        };

        section.Children.Add(CreateSettingsRow(T("settings.language"), languageComboBox));
        section.Children.Add(CreateSettingsRow(T("settings.theme"), themeComboBox));
        return section;
    }

    private UIElement BuildHiddenNodesSection(Action rebuild)
    {
        StackPanel section = CreateSettingsSection(T("settings.section.hiddenNodes"));
        if (string.IsNullOrWhiteSpace(_activeSolutionPath))
        {
            section.Children.Add(new TextBlock
            {
                Text = T("settings.hiddenNodes.noWorkspace"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetThemeBrush(
                    "TextFillColorSecondaryBrush",
                    SecondaryTextFallbackColor)
            });
            return section;
        }

        section.Children.Add(new TextBlock
        {
            Text = T("settings.hiddenNodes.workspace", _activeSolutionPath),
            TextWrapping = TextWrapping.WrapWholeWords
        });

        IReadOnlyList<HiddenNodeViewState> hiddenNodes = GetActiveGraphViewState()?.HiddenNodes ?? Array.Empty<HiddenNodeViewState>();
        if (hiddenNodes.Count == 0)
        {
            section.Children.Add(new TextBlock
            {
                Text = T("settings.hiddenNodes.empty"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = GetThemeBrush(
                    "TextFillColorSecondaryBrush",
                    SecondaryTextFallbackColor)
            });
            return section;
        }

        Button restoreAllButton = new()
        {
            Content = T("settings.hiddenNodes.restoreAll"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        restoreAllButton.Click += (_, _) =>
        {
            RestoreHiddenNodes(Array.Empty<string>());
            rebuild();
        };
        section.Children.Add(restoreAllButton);

        StackPanel itemsPanel = new()
        {
            Spacing = 8
        };

        foreach (HiddenNodeViewState hiddenNode in hiddenNodes)
        {
            Grid row = new()
            {
                ColumnSpacing = 12
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = new()
            {
                Text = T(
                    "settings.hiddenNode.entry",
                    ResolveGroupLabel(hiddenNode.Group),
                    hiddenNode.Label),
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button restoreButton = new()
            {
                Content = T("settings.hiddenNodes.restore"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            restoreButton.SetValue(Grid.ColumnProperty, 1);
            restoreButton.Click += (_, _) =>
            {
                RestoreHiddenNodes(new[] { hiddenNode.NodeId });
                rebuild();
            };

            row.Children.Add(label);
            row.Children.Add(restoreButton);
            itemsPanel.Children.Add(row);
        }

        section.Children.Add(itemsPanel);
        return section;
    }

    private UIElement BuildDataManagementSection(Action rebuild, ContentDialog settingsDialog)
    {
        StackPanel section = CreateSettingsSection(T("settings.section.dataManagement"));
        section.Children.Add(new TextBlock
        {
            Text = T("settings.dataManagement.description"),
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = GetThemeBrush(
                "TextFillColorSecondaryBrush",
                SecondaryTextFallbackColor)
        });

        Button clearAllButton = new()
        {
            Content = T("settings.dataManagement.clearAll"),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        clearAllButton.Click += async (_, _) =>
        {
            clearAllButton.IsEnabled = false;
            try
            {
                bool cleared = await TryClearLocalDataAsync(settingsDialog);
                if (cleared)
                {
                    rebuild();
                }
            }
            finally
            {
                clearAllButton.IsEnabled = true;
            }
        };
        section.Children.Add(clearAllButton);

        return section;
    }

    private UIElement BuildAboutSection()
    {
        StackPanel section = CreateSettingsSection(T("settings.section.about"));
        foreach ((string label, string value) in BuildAboutRows())
        {
            section.Children.Add(CreateSettingsRow(label, new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true
            }));
        }

        return section;
    }

    private StackPanel CreateSettingsSection(string title)
    {
        StackPanel section = new()
        {
            Spacing = 8
        };
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15
        });
        return section;
    }

    private UIElement CreateSettingsRow(string label, UIElement valueElement)
    {
        Grid row = new()
        {
            ColumnSpacing = 16
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetThemeBrush(
                "TextFillColorSecondaryBrush",
                SecondaryTextFallbackColor)
        });

        if (valueElement is FrameworkElement frameworkElement)
        {
            frameworkElement.SetValue(Grid.ColumnProperty, 1);
        }

        row.Children.Add(valueElement);
        return row;
    }

    private IReadOnlyList<(string Label, string Value)> BuildAboutRows()
    {
        Assembly assembly = typeof(MainWindow).Assembly;
        string assemblyPath = assembly.Location;
        Version? assemblyVersion = assembly.GetName().Version;
        FileVersionInfo? fileVersionInfo = !string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath)
            ? FileVersionInfo.GetVersionInfo(assemblyPath)
            : null;
        DateTime buildDateLocal = !string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath)
            ? File.GetLastWriteTime(assemblyPath)
            : DateTime.Now;
        string appVersion = fileVersionInfo is null || string.IsNullOrWhiteSpace(fileVersionInfo.ProductVersion)
            ? assemblyVersion?.ToString() ?? "n/a"
            : fileVersionInfo.ProductVersion;
        string buildNumber = fileVersionInfo is null || string.IsNullOrWhiteSpace(fileVersionInfo.FileVersion)
            ? buildDateLocal.ToString("yyyyMMdd.HHmm", CultureInfo.InvariantCulture)
            : fileVersionInfo.FileVersion;

        string webView2Version;
        try
        {
            webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch
        {
            webView2Version = "n/a";
        }

        return new (string Label, string Value)[]
        {
            (T("settings.about.version"), appVersion),
            (T("settings.about.buildNumber"), buildNumber),
            (T("settings.about.buildDate"), buildDateLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            (T("settings.about.component.dotnet"), Environment.Version.ToString()),
            (T("settings.about.component.winappsdk"), typeof(Application).Assembly.GetName().Version?.ToString() ?? "n/a"),
            (T("settings.about.component.webview2"), webView2Version),
            (T("settings.about.component.roslyn"), typeof(Compilation).Assembly.GetName().Version?.ToString() ?? "n/a"),
            (T("settings.about.component.sqlite"), typeof(SqliteConnection).Assembly.GetName().Version?.ToString() ?? "n/a"),
            (T("settings.about.component.graphUi"), GraphUiVersion)
        };
    }

    private string ResolveGroupLabel(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            return T("group.symbol");
        }

        string key = $"group.{group.Trim().ToLowerInvariant()}";
        string resolved = T(key);
        return string.Equals(resolved, key, StringComparison.Ordinal)
            ? group
            : resolved;
    }

    private void RestoreHiddenNodes(IReadOnlyCollection<string>? nodeIdsToRestore)
    {
        SolutionViewState? activeState = GetOrCreateActiveSolutionViewState();
        if (activeState is null)
        {
            return;
        }

        GraphViewState currentGraphState = NormalizeGraphViewState(activeState.Graph);
        IReadOnlyList<HiddenNodeViewState> nextHiddenNodes = nodeIdsToRestore is null || nodeIdsToRestore.Count == 0
            ? Array.Empty<HiddenNodeViewState>()
            : currentGraphState.HiddenNodes
                .Where(hiddenNode => !nodeIdsToRestore.Contains(hiddenNode.NodeId, StringComparer.Ordinal))
                .ToArray();
        GraphViewState nextState = NormalizeGraphViewState(currentGraphState with
        {
            HiddenNodes = nextHiddenNodes
        });

        activeState.Graph = nextState;
        _pendingGraphViewState = nextState;
        SaveSolutionViewStates();
        RefreshExplorerContent();
        FlushPendingGraphViewState();
        TryRecordNavigationState();
    }

    private async Task<bool> TryClearLocalDataAsync(ContentDialog? settingsDialog = null)
    {
        settingsDialog?.Hide();
        string? workspacePathToReload = ResolveWorkspacePathToReloadAfterClear();
        string confirmMessageKey = string.IsNullOrWhiteSpace(workspacePathToReload)
            ? "settings.dataManagement.confirm.message"
            : "settings.dataManagement.confirm.messageWithReanalyze";

        ContentDialog confirmationDialog = new()
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = T("settings.dataManagement.confirm.title"),
            Content = new TextBlock
            {
                Text = T(confirmMessageKey),
                TextWrapping = TextWrapping.WrapWholeWords
            },
            PrimaryButtonText = T("settings.dataManagement.confirm.primary"),
            CloseButtonText = T("dialog.cancel"),
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await confirmationDialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        UpdateStatus(T("status.localDataClearing"), StatusSeverity.Warning);

        try
        {
            await ClearLocalDataAsync(workspacePathToReload);
            UpdateStatus(T("status.localDataCleared"), StatusSeverity.Success);
            return true;
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"ローカルデータの完全クリアに失敗しました: {ex}");
            UpdateStatus(T("status.localDataClearFailed", ex.Message), StatusSeverity.Error);
            return false;
        }
    }

    private async Task ClearLocalDataAsync(string? workspacePathToReloadAfterClear)
    {
        string normalizedWorkspacePathToReload = NormalizeSolutionPath(workspacePathToReloadAfterClear ?? string.Empty);
        string? workspacePathToReload =
            !string.IsNullOrWhiteSpace(normalizedWorkspacePathToReload) &&
            (File.Exists(normalizedWorkspacePathToReload) || Directory.Exists(normalizedWorkspacePathToReload))
                ? normalizedWorkspacePathToReload
                : null;
        ClearReanalysisDisplayState? restoreState = CaptureDisplayStateForClear(workspacePathToReload);

        InvalidateActiveAnalysisSession();
        Interlocked.Increment(ref _snapshotLoadRequestVersion);
        Interlocked.Increment(ref _graphRenderRequestVersion);
        CancelSnapshotLoadRequest();
        CancelPendingSave(ref _recentSolutionsSaveCancellationTokenSource);
        CancelPendingSave(ref _solutionViewStatesSaveCancellationTokenSource);

        await ClearGraphBrowserDataAsync();
        await _snapshotStore.ClearAllAsync();

        TryDeleteFileIfExists(s_recentSolutionsFilePath, "最近開いたワークスペース");
        TryDeleteFileIfExists(s_solutionViewStatesFilePath, "表示状態");
        TryDeleteDirectoryIfExists(s_captureDirectoryPath, "グラフ キャプチャ");
        MarkWebView2DataCleanupPending();
        TryClearLogFile(s_logFilePath, "診断ログ");

        _activeCacheLoadOperationCount = 0;
        _activeAnalyzeOperationCount = 0;
        UpdateWorkspaceOperationVisualState();

        _currentSnapshot = null;
        _activeSolutionPath = null;
        _workspaceSearchQuery = string.Empty;
        _recentSolutions.Clear();
        _solutionViewStates.Clear();
        _analysisDiagnostics.Clear();
        _graphDiagnostics.Clear();
        _diagnosticsText = string.Empty;
        _diagnosticsDirty = true;
        _navigationHistoryService.Reset();
        _pendingGraphViewState = null;
        _pendingGraphFocusTarget = null;
        _lastRequestedGraphFocusTarget = null;
        _currentSelectionTarget = null;
        _pendingGraphSelectionClear = true;
        _pendingGraphPayloadJson = null;
        _lastGraphPayloadJson = null;
        _pendingGraphSearchQuery = string.Empty;
        _graphPayloadCompleteness = GraphPayloadCompleteness.None;
        _isFullGraphPayloadBuildQueued = false;

        SolutionPathTextBox.Text = workspacePathToReload ?? string.Empty;
        if (!string.IsNullOrEmpty(ExplorerSearchTextBox.Text))
        {
            ExplorerSearchTextBox.Text = string.Empty;
        }
        else
        {
            QueueGraphSearchQuery(string.Empty);
        }

        RefreshBrowseSolutionOptions();
        SetBrowseSolutionSelection(null);
        SetGraphRecoveryOverlay(isVisible: false);
        RefreshExplorerContent();
        UpdateWorkspaceEmptyStateVisibility();

        if (!string.IsNullOrWhiteSpace(workspacePathToReload))
        {
            SetCacheClearReanalysisDisplayState(isActive: true);
            try
            {
                await AnalyzeCurrentWorkspaceAsync();
                TryRestoreDisplayStateAfterClear(restoreState);
            }
            finally
            {
                SetCacheClearReanalysisDisplayState(isActive: false);
            }
        }
    }

    private string? ResolveWorkspacePathToReloadAfterClear()
    {
        if (_currentSnapshot is null)
        {
            return null;
        }

        string activeWorkspacePath = NormalizeSolutionPath(_activeSolutionPath ?? string.Empty);
        string snapshotWorkspacePath = NormalizeSolutionPath(_currentSnapshot.WorkspacePath);
        if (string.IsNullOrWhiteSpace(activeWorkspacePath) ||
            !string.Equals(activeWorkspacePath, snapshotWorkspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (File.Exists(activeWorkspacePath) || Directory.Exists(activeWorkspacePath))
        {
            return activeWorkspacePath;
        }

        return null;
    }

    private ClearReanalysisDisplayState? CaptureDisplayStateForClear(string? workspacePathToReload)
    {
        if (string.IsNullOrWhiteSpace(workspacePathToReload) ||
            _currentSnapshot is null ||
            string.IsNullOrWhiteSpace(_activeSolutionPath) ||
            !string.Equals(_activeSolutionPath, workspacePathToReload, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        ExplorerViewState explorerState = CreateCurrentExplorerViewState();
        GraphViewState graphState = NormalizeGraphViewState(GetActiveGraphViewState() ?? new GraphViewState());
        string searchQuery = _workspaceSearchQuery;
        GraphSelectionTarget? selectionTarget = _currentSelectionTarget;

        return new ClearReanalysisDisplayState(
            workspacePathToReload,
            explorerState,
            graphState,
            searchQuery,
            selectionTarget);
    }

    private void TryRestoreDisplayStateAfterClear(ClearReanalysisDisplayState? restoreState)
    {
        if (restoreState is null ||
            _currentSnapshot is null ||
            string.IsNullOrWhiteSpace(_activeSolutionPath) ||
            !string.Equals(_activeSolutionPath, restoreState.WorkspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        HashSet<string> validNodeIds = BuildSnapshotNodeIds(_currentSnapshot);
        ExplorerViewState explorerState = NormalizeExplorerViewState(restoreState.ExplorerState);
        GraphViewState normalizedGraphState = NormalizeGraphViewState(restoreState.GraphState);
        GraphViewState graphState = NormalizeGraphViewState(normalizedGraphState with
        {
            PinnedNodes = normalizedGraphState.PinnedNodes
                .Where(pinnedNode => validNodeIds.Contains(pinnedNode.NodeId))
                .ToArray(),
            HiddenNodes = normalizedGraphState.HiddenNodes
                .Where(hiddenNode => validNodeIds.Contains(hiddenNode.NodeId))
                .ToArray()
        });

        GraphSelectionTarget? selectionTarget =
            restoreState.SelectionTarget is GraphSelectionTarget candidateTarget &&
            validNodeIds.Contains(candidateTarget.NodeId)
                ? candidateTarget
                : null;

        ApplyExplorerViewState(explorerState);

        SolutionViewState? activeState = GetOrCreateActiveSolutionViewState();
        if (activeState is not null)
        {
            activeState.Explorer = explorerState;
            activeState.Graph = graphState;
            _pendingGraphViewState = graphState;
            SaveSolutionViewStates();
        }
        else
        {
            _pendingGraphViewState = graphState;
        }

        string restoredSearchQuery = restoreState.SearchQuery;
        if (!string.Equals(ExplorerSearchTextBox.Text, restoredSearchQuery, StringComparison.Ordinal))
        {
            ExplorerSearchTextBox.Text = restoredSearchQuery;
        }
        else
        {
            _workspaceSearchQuery = restoredSearchQuery;
            RefreshExplorerContent();
            QueueGraphSearchQuery(_workspaceSearchQuery);
        }

        _currentSelectionTarget = selectionTarget;
        _pendingGraphFocusTarget = selectionTarget;
        _pendingGraphSelectionClear = selectionTarget is null;

        FlushPendingGraphViewState();
        FlushPendingGraphFocusTarget();
        FlushPendingGraphSelectionClear();
        TryRecordNavigationState();
    }

    private static HashSet<string> BuildSnapshotNodeIds(SolutionAnalysisSnapshot snapshot)
    {
        HashSet<string> nodeIds = new(StringComparer.Ordinal);

        foreach (ProjectAnalysisSummary project in snapshot.Projects)
        {
            nodeIds.Add(GraphPayloadBuilder.BuildProjectNodeId(project));

            foreach (DocumentAnalysisSummary document in project.Documents)
            {
                if (!string.IsNullOrWhiteSpace(document.Id))
                {
                    nodeIds.Add(document.Id);
                }

                foreach (SymbolAnalysisSummary symbol in document.Symbols)
                {
                    if (!string.IsNullOrWhiteSpace(symbol.Id))
                    {
                        nodeIds.Add(symbol.Id);
                    }
                }
            }

            foreach (string packageReference in project.PackageReferences)
            {
                if (!string.IsNullOrWhiteSpace(packageReference))
                {
                    nodeIds.Add(GraphPayloadBuilder.BuildDependencyNodeId("package", packageReference));
                }
            }

            foreach (string metadataReference in project.MetadataReferences)
            {
                if (!string.IsNullOrWhiteSpace(metadataReference))
                {
                    nodeIds.Add(GraphPayloadBuilder.BuildDependencyNodeId("assembly", metadataReference));
                }
            }

            foreach (NativeDependencySummary nativeDependency in project.NativeDependencies)
            {
                if (!string.IsNullOrWhiteSpace(nativeDependency.LibraryName))
                {
                    nodeIds.Add(GraphPayloadBuilder.BuildDependencyNodeId("dll", nativeDependency.LibraryName));
                }
            }

            foreach (ProjectReferenceSummary projectReference in project.ProjectReferences)
            {
                if (!string.IsNullOrWhiteSpace(projectReference.TargetProjectKey))
                {
                    nodeIds.Add(GraphPayloadBuilder.BuildProjectNodeId(projectReference.TargetProjectKey));
                }
            }
        }

        foreach (DependencyCycleSummary cycle in snapshot.Cycles)
        {
            foreach (string cycleNodeId in cycle.NodeIds)
            {
                if (string.IsNullOrWhiteSpace(cycleNodeId))
                {
                    continue;
                }

                string nodeId = string.Equals(cycle.GraphKind, "project", StringComparison.OrdinalIgnoreCase)
                    ? GraphPayloadBuilder.BuildProjectNodeId(cycleNodeId)
                    : cycleNodeId;
                nodeIds.Add(nodeId);
            }
        }

        return nodeIds;
    }

    private void SetCacheClearReanalysisDisplayState(bool isActive)
    {
        _isCacheClearReanalysisInProgress = isActive;
        GraphWebView.IsHitTestVisible = !isActive;
        GraphWebView.Opacity = isActive ? 0 : 1;

        if (isActive)
        {
            SetGraphRecoveryOverlay(isVisible: true, T("status.localDataReanalyzingWithRestore"));
            UpdateStatus(T("status.localDataReanalyzingWithRestore"), StatusSeverity.Warning);
        }
        else
        {
            if (_currentSnapshot is null || _isGraphContentRendered)
            {
                SetGraphRecoveryOverlay(isVisible: false);
            }
            else
            {
                SetGraphRecoveryOverlay(isVisible: true, T("status.graphLoading"));
            }
        }
    }

    private async Task ClearGraphBrowserDataAsync()
    {
        if (GraphWebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await GraphWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.clearBrowserCache", "{}");
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"WebView2 キャッシュのクリアに失敗しました: {ex.Message}");
        }

        try
        {
            await GraphWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Network.clearBrowserCookies", "{}");
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"WebView2 Cookie のクリアに失敗しました: {ex.Message}");
        }

        try
        {
            await GraphWebView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Storage.clearDataForOrigin",
                "{\"origin\":\"https://codemap.local\",\"storageTypes\":\"all\"}");
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"WebView2 ストレージのクリアに失敗しました: {ex.Message}");
        }
    }

    private void MarkWebView2DataCleanupPending()
    {
        try
        {
            Directory.CreateDirectory(AppStoragePaths.LocalAppDataRoot);
            File.WriteAllText(
                s_webView2CleanupMarkerFilePath,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"WebView2 データ削除予約に失敗しました: {ex.Message}");
        }
    }

    private void TryApplyPendingWebView2DataCleanup()
    {
        try
        {
            if (!File.Exists(s_webView2CleanupMarkerFilePath))
            {
                return;
            }

            TryDeleteDirectoryIfExists(s_webView2UserDataDirectoryPath, "WebView2 データ");
            if (Directory.Exists(s_webView2UserDataDirectoryPath))
            {
                AppendDiagnosticsLine("予約済みの WebView2 データ削除を完了できなかったため、次回起動時に再試行します。");
                return;
            }

            File.Delete(s_webView2CleanupMarkerFilePath);
            AppendDiagnosticsLine("予約済みの WebView2 データ削除を完了しました。");
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"予約済みの WebView2 データ削除に失敗しました: {ex.Message}");
        }
    }

    private void TryDeleteFileIfExists(string filePath, string label)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"{label}の削除に失敗しました: {ex.Message}");
        }
    }

    private void TryDeleteDirectoryIfExists(string directoryPath, string label)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"{label}の削除に失敗しました: {ex.Message}");
        }
    }

    private void TryClearLogFile(string logFilePath, string label)
    {
        try
        {
            if (File.Exists(logFilePath))
            {
                File.WriteAllText(logFilePath, string.Empty);
            }
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"{label}のクリアに失敗しました: {ex.Message}");
        }
    }

    private sealed record ClearReanalysisDisplayState(
        string WorkspacePath,
        ExplorerViewState ExplorerState,
        GraphViewState GraphState,
        string SearchQuery,
        GraphSelectionTarget? SelectionTarget);

    private async void OnShowDiagnosticsClicked(object sender, RoutedEventArgs e)
    {
        RenderDiagnostics();

        ScrollViewer scrollViewer = new()
        {
            MaxHeight = 480,
            Content = new TextBlock
            {
                Text = _diagnosticsText,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Foreground = GetThemeBrush(
                    "TextFillColorSecondaryBrush",
                    Microsoft.UI.ColorHelper.FromArgb(255, 180, 180, 180))
            }
        };

        ContentDialog dialog = new()
        {
            Title = T("dialog.logs.title"),
            Content = scrollViewer,
            CloseButtonText = T("dialog.close"),
            XamlRoot = Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}
