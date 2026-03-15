using CodeMap.Analysis;
using CodeMap.Diagnostics;
using CodeMap.Graph;
using CodeMap.Services;
using CodeMap.Storage;
using CodeMap.ViewModels;
using Microsoft.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace CodeMap;

public sealed partial class MainWindow : Window
{
    private enum GraphRecoveryMode
    {
        ReloadCurrentControl,
        RecreateControl
    }

    private enum GraphPayloadCompleteness
    {
        None = 0,
        StructureOnly = 1,
        Full = 2
    }

    private enum WorkspaceOperation
    {
        None = 0,
        LoadingCache = 1,
        Analyzing = 2
    }

    private enum SnapshotLoadResult
    {
        Loaded = 0,
        CacheMissing = 1,
        Superseded = 2,
        Failed = 3
    }

    private const string GraphUiVersion = "graph-ui-20260313a";

    private string BrowseFileOptionLabel => T("browse.solutionFile");

    private string BrowseFolderOptionLabel => T("browse.folder");

    private readonly RoslynAnalysisService _analysisService = new();
    private readonly SqliteSnapshotStore _snapshotStore = new();
    private readonly ObservableCollection<SymbolListItem> _symbols = [];
    private readonly List<SymbolListItem> _allSymbolItems = [];
    private readonly ObservableCollection<string> _recentSolutions = [];
    private readonly ObservableCollection<string> _browseSolutionOptions = [];
    private readonly List<string> _analysisDiagnostics = [];
    private readonly List<string> _graphDiagnostics = [];
    private readonly NavigationHistoryService _navigationHistoryService = new();
    private readonly AppPreferences _appPreferences = new();
    private string _diagnosticsText = string.Empty;
    private bool _diagnosticsDirty = true;
    private bool _isWorkspaceSplit;
    private bool _isDraggingWorkspaceSplitDivider;
    private bool _isDraggingExplorerPanelSplitDivider;
    private double _explorerPanelWidth = DefaultExplorerPanelWidth;
    private double _workspaceSplitRatio = DefaultWorkspaceSplitRatio;
    private static readonly string s_logFilePath = AppStoragePaths.LogFilePath;
    private static readonly Lazy<AsyncFileLogger> s_asyncFileLogger = new(() => new AsyncFileLogger(s_logFilePath));
    private static readonly string s_captureDirectoryPath = AppStoragePaths.CaptureDirectoryPath;
    private static readonly string s_webView2UserDataDirectoryPath = AppStoragePaths.WebView2UserDataDirectoryPath;
    private static readonly string s_webView2CleanupMarkerFilePath = AppStoragePaths.WebView2CleanupMarkerFilePath;
    private static readonly string s_recentSolutionsFilePath = AppStoragePaths.RecentSolutionsFilePath;
    private static readonly string s_solutionViewStatesFilePath = AppStoragePaths.SolutionViewStatesFilePath;
    private static readonly string s_appPreferencesFilePath = AppStoragePaths.AppPreferencesFilePath;
    private const string WebView2BrowserArguments = "";
    private static readonly bool s_enableGraphPreviewCapture = IsFeatureEnabled("CODEMAP_GRAPH_CAPTURE");
    private static readonly bool s_enableVerboseGraphRenderDiagnostics = IsFeatureEnabled("CODEMAP_GRAPH_VERBOSE");

    private CancellationTokenSource? _analysisCancellationTokenSource;
    private bool _isGraphFrontendReady;
    private bool _isGraphContentRendered;
    private bool _isRefreshingGraphSurface;
    private bool _isRecoveringGraphFrontend;
    private bool _isWaitingForGraphBrowserProcessExit;
    private bool _isCacheClearReanalysisInProgress;
    private bool _isUpdatingBrowseSolutionSelection;
    private bool _isApplyingPersistedViewState;
    private bool _isWindowClosed;
    private bool _isGraphRecoveryOverlayRequested;
    private int _analysisSessionId;
    private int _activeAnalyzeOperationCount;
    private int _activeCacheLoadOperationCount;
    private AppLocale _currentLocale;
    private AppThemePreference _currentThemePreference;
    private string? _pendingGraphPayloadJson;
    private string? _lastGraphPayloadJson;
    private string? _pendingGraphLocaleCode;
    private string? _pendingGraphTheme;
    private string? _pendingGraphSearchQuery;
    private string? _activeSolutionPath;
    private string? _analysisProgressDetail;
    private string? _graphRecoveryOverlayMessage;
    private string _workspaceSearchQuery = string.Empty;
    private SolutionAnalysisSnapshot? _currentSnapshot;
    private GraphViewState? _pendingGraphViewState;
    private bool _pendingGraphSelectionClear;
    private GraphSelectionTarget? _pendingGraphFocusTarget;
    private GraphSelectionTarget? _lastRequestedGraphFocusTarget;
    private GraphSelectionTarget? _currentSelectionTarget;
    private readonly Dictionary<string, SolutionViewState> _solutionViewStates = new(StringComparer.OrdinalIgnoreCase);
    private CoreWebView2Environment? _graphWebViewEnvironment;
    private CancellationTokenSource? _appPreferencesSaveCancellationTokenSource;
    private CancellationTokenSource? _recentSolutionsSaveCancellationTokenSource;
    private CancellationTokenSource? _solutionViewStatesSaveCancellationTokenSource;
    private CancellationTokenSource? _graphFrontendRetryCancellationTokenSource;
    private CancellationTokenSource? _snapshotLoadCancellationTokenSource;
    private int _graphRenderRequestVersion;
    private int _snapshotLoadRequestVersion;
    private int _graphFrontendRetryAttemptCount;
    private GraphPayloadCompleteness _graphPayloadCompleteness;
    private bool _isFullGraphPayloadBuildQueued;
    private static readonly HashSet<char> s_invalidFileNameChars = [.. Path.GetInvalidFileNameChars()];

    public MainWindow()
    {
        InitializeComponent();
        LoadAppPreferences();
        _currentLocale = AppLocalization.ResolvePreferredLocale(_appPreferences.Locale);
        AppLocalization.SetCurrentLocale(_currentLocale);
        _currentThemePreference = AppThemePreferenceResolver.ResolvePreferredTheme(_appPreferences.Theme);
        SetupTitleBar();
        ApplyLocalizedUiText(overwriteStatusWithReady: true);
        UpdateWorkspaceOperationVisualState();
        UpdateWorkspaceEmptyStateVisibility();
        SymbolsListView.ItemsSource = _symbols;
        BrowseSolutionComboBox.ItemsSource = _browseSolutionOptions;
        WorkspaceTreeView.AddHandler(UIElement.DoubleTappedEvent, new DoubleTappedEventHandler(OnWorkspaceTreeDoubleTapped), true);
        RootLayout.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnRootLayoutPointerPressed), true);
        RootLayout.Loaded += OnLoaded;
        RootLayout.SizeChanged += OnRootLayoutSizeChanged;
        ApplyCurrentThemePreference(savePreference: false);
        RootLayout.ActualThemeChanged += OnRootLayoutActualThemeChanged;
        GraphWebView.SizeChanged += OnGraphWebViewSizeChanged;
        Closed += OnClosed;
    }

    private void SetupTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    private string T(string key, params object[] args)
    {
        return AppLocalization.Get(_currentLocale, key, args);
    }

    private void LoadAppPreferences()
    {
        try
        {
            if (!File.Exists(s_appPreferencesFilePath))
            {
                return;
            }

            string json = File.ReadAllText(s_appPreferencesFilePath);
            AppPreferences? loaded = JsonSerializer.Deserialize(
                json,
                typeof(AppPreferences),
                CodeMapJsonSerializerContext.Default) as AppPreferences;
            if (loaded is null)
            {
                return;
            }

            _appPreferences.Locale = loaded.Locale;
            _appPreferences.Theme = loaded.Theme;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load app preferences: {ex.Message}");
        }
    }

    private void SaveAppPreferences()
    {
        ScheduleJsonFileWrite(
            ref _appPreferencesSaveCancellationTokenSource,
            s_appPreferencesFilePath,
            _appPreferences,
            "アプリ設定の保存に失敗",
            ClearAppPreferencesSaveTokenSource);
    }

    private void SaveAppPreferencesImmediately()
    {
        CancelPendingSave(ref _appPreferencesSaveCancellationTokenSource);
        WriteJsonFileImmediately(s_appPreferencesFilePath, _appPreferences, "アプリ設定の保存に失敗");
    }

    private void ClearAppPreferencesSaveTokenSource(CancellationTokenSource cancellationTokenSource)
    {
        Interlocked.CompareExchange(ref _appPreferencesSaveCancellationTokenSource, null, cancellationTokenSource);
    }

    private void ApplyLocalizedUiText(bool overwriteStatusWithReady = false)
    {
        SolutionPathTextBox.PlaceholderText = T("workspace.placeholder");
        BrowseSolutionComboBox.PlaceholderText = T("browse.placeholder");
        ToolTipService.SetToolTip(BrowseSolutionComboBox, T("browse.tooltip"));
        LoadCacheTooltipLine1TextBlock.Text = T("cache.tooltip.line1");
        LoadCacheTooltipLine2TextBlock.Text = T("cache.tooltip.line2");
        LoadCacheButtonTextBlock.Text = T("cache.button");
        ToolTipService.SetToolTip(AnalyzeButton, T("analyze.tooltip"));
        AnalyzeButtonTextBlock.Text = T("analyze.button");
        TreeTabTextBlock.Text = T("workspace.tree");
        SymbolTabTextBlock.Text = T("workspace.symbols");
        ToolTipService.SetToolTip(SplitToggleButton, T("workspace.splitTooltip"));
        ExplorerSearchTextBox.PlaceholderText = T("workspace.searchPlaceholder");
        ToolTipService.SetToolTip(SolutionPathTextBox, T("workspace.pathTooltip"));
        ToolTipService.SetToolTip(ExplorerSearchTextBox, T("workspace.searchTooltip"));
        ExplorerEmptyStateTitleTextBlock.Text = T("empty.explorer.title");
        ExplorerEmptyStateDescriptionTextBlock.Text = T("empty.explorer.description");
        GraphEmptyStateTitleTextBlock.Text = T("empty.graph.title");
        GraphEmptyStateDescriptionTextBlock.Text = T("empty.graph.description");
        ToolTipService.SetToolTip(ShowDiagnosticsButton, T("button.logTooltip"));
        DiagnosticsButtonTextBlock.Text = T("button.log");
        ToolTipService.SetToolTip(ShowSettingsButton, T("button.settingsTooltip"));
        SettingsButtonTextBlock.Text = T("button.settings");
        AutomationProperties.SetName(SolutionPathTextBox, T("a11y.solutionPath"));
        AutomationProperties.SetName(BrowseSolutionComboBox, T("a11y.browseCombo"));
        AutomationProperties.SetName(LoadCacheButton, T("a11y.loadCacheButton"));
        AutomationProperties.SetName(AnalyzeButton, T("a11y.analyzeButton"));
        AutomationProperties.SetName(TreeTabButton, T("a11y.treeTabButton"));
        AutomationProperties.SetName(SymbolTabButton, T("a11y.symbolTabButton"));
        AutomationProperties.SetName(SplitToggleButton, T("a11y.splitToggleButton"));
        AutomationProperties.SetName(ExplorerSearchTextBox, T("a11y.explorerSearch"));
        AutomationProperties.SetName(WorkspaceTreeView, T("a11y.workspaceTree"));
        AutomationProperties.SetName(WorkspaceSplitDivider, T("a11y.workspaceSplitDivider"));
        AutomationProperties.SetName(SymbolsListView, T("a11y.symbolList"));
        AutomationProperties.SetName(ExplorerPanelSplitDivider, T("a11y.explorerPanelDivider"));
        AutomationProperties.SetName(GraphWebView, T("a11y.graphWebView"));
        AutomationProperties.SetName(ShowDiagnosticsButton, T("a11y.diagnosticsButton"));
        AutomationProperties.SetName(ShowSettingsButton, T("a11y.settingsButton"));

        if (overwriteStatusWithReady || string.IsNullOrWhiteSpace(StatusTextBlock.Text))
        {
            UpdateStatus(StatusCode.Ready);
        }

        UpdateExplorerSearchSummary(
            visibleProjectCount: _currentSnapshot?.Projects.Count ?? 0,
            visibleDocumentCount: _currentSnapshot?.Projects.Sum(project => project.Documents.Count) ?? 0,
            visibleSymbolCount: _allSymbolItems.Count,
            isFiltering: !string.IsNullOrWhiteSpace(_workspaceSearchQuery));

        RefreshBrowseSolutionOptions();
        if (_currentSnapshot is not null)
        {
            RefreshExplorerContent();
        }

        UpdateGraphRecoveryOverlayVisualState();
        QueueGraphLocaleUpdate();
    }

    private void SetCurrentLocale(AppLocale locale)
    {
        if (_currentLocale == locale)
        {
            return;
        }

        _currentLocale = locale;
        AppLocalization.SetCurrentLocale(locale);
        _appPreferences.Locale = AppLocalization.ToCode(locale);
        _diagnosticsDirty = true;
        SaveAppPreferences();
        ApplyLocalizedUiText();
        TryRecordNavigationState();
    }

    private void SetCurrentThemePreference(AppThemePreference themePreference)
    {
        if (_currentThemePreference == themePreference)
        {
            return;
        }

        _currentThemePreference = themePreference;
        ApplyCurrentThemePreference(savePreference: true);
    }

    private void ApplyCurrentThemePreference(bool savePreference)
    {
        RootLayout.RequestedTheme = _currentThemePreference switch
        {
            AppThemePreference.Light => ElementTheme.Light,
            AppThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        _appPreferences.Theme = AppThemePreferenceResolver.ToCode(_currentThemePreference);
        if (savePreference)
        {
            SaveAppPreferences();
        }

        QueueGraphThemeUpdate();
    }

    private void QueueGraphLocaleUpdate()
    {
        _pendingGraphLocaleCode = AppLocalization.ToCode(_currentLocale);
        FlushPendingGraphLocale();
    }

    private void QueueGraphThemeUpdate()
    {
        string themeCode = ResolveGraphThemeCode();
        GraphWebView.DefaultBackgroundColor = ResolveGraphWebViewBackgroundColor(themeCode);
        _pendingGraphTheme = themeCode;
        FlushPendingGraphTheme();
    }

    private void FlushPendingGraphLocale()
    {
        if (!_isGraphFrontendReady || GraphWebView.CoreWebView2 is null || string.IsNullOrWhiteSpace(_pendingGraphLocaleCode))
        {
            return;
        }

        try
        {
            GraphLocaleMessage message = new(
                "set-locale",
                new GraphLocalePayload(_pendingGraphLocaleCode));
            string messageJson = JsonSerializer.Serialize(
                message,
                CodeMapJsonSerializerContext.Default.GraphLocaleMessage);

            GraphWebView.CoreWebView2.PostWebMessageAsJson(messageJson);
            _pendingGraphLocaleCode = null;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ ロケール送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private void FlushPendingGraphTheme()
    {
        if (!_isGraphFrontendReady || GraphWebView.CoreWebView2 is null || string.IsNullOrWhiteSpace(_pendingGraphTheme))
        {
            return;
        }

        try
        {
            GraphThemeMessage message = new(
                "set-theme",
                new GraphThemePayload(_pendingGraphTheme));
            string messageJson = JsonSerializer.Serialize(
                message,
                CodeMapJsonSerializerContext.Default.GraphThemeMessage);

            GraphWebView.CoreWebView2.PostWebMessageAsJson(messageJson);
            _pendingGraphTheme = null;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ テーマ送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private string ResolveGraphThemeCode()
    {
        if (_currentThemePreference == AppThemePreference.Light)
        {
            return "light";
        }

        if (_currentThemePreference == AppThemePreference.Dark)
        {
            return "dark";
        }

        ElementTheme actualTheme = RootLayout.ActualTheme;
        return actualTheme switch
        {
            ElementTheme.Light => "light",
            ElementTheme.Dark => "dark",
            _ => Application.Current.RequestedTheme == ApplicationTheme.Light
                ? "light"
                : "dark"
        };
    }

    private static Windows.UI.Color ResolveGraphWebViewBackgroundColor(string themeCode)
    {
        return string.Equals(themeCode, "light", StringComparison.OrdinalIgnoreCase)
            ? Microsoft.UI.ColorHelper.FromArgb(255, 246, 248, 251)
            : Microsoft.UI.ColorHelper.FromArgb(255, 30, 33, 38);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureExplorerPanelWidthWithinBounds();
            LoadSolutionViewStates();
            LoadRecentSolutions();
            RefreshBrowseSolutionOptions();
            if (_recentSolutions.Count > 0)
            {
                SolutionPathTextBox.Text = _recentSolutions[0];
                SetBrowseSolutionSelection(_recentSolutions[0]);
            }
            else
            {
                string? defaultSolutionPath = DiscoverDefaultSolutionPath();
                SolutionPathTextBox.Text = defaultSolutionPath ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(defaultSolutionPath))
                {
                    RememberRecentSolution(defaultSolutionPath);
                }
            }

            LogInfo($"Log file: {s_logFilePath}");
            LogInfo($"Graph preview capture: {(s_enableGraphPreviewCapture ? "enabled" : "disabled")}");
            LogInfo($"Graph verbose diagnostics: {(s_enableVerboseGraphRenderDiagnostics ? "enabled" : "disabled")}");
            TryApplyPendingWebView2DataCleanup();
            await InitializeGraphFrontendAsync();

            if (!string.IsNullOrWhiteSpace(SolutionPathTextBox.Text))
            {
                SnapshotLoadResult snapshotLoadResult = await TryLoadCachedSnapshotAsync(
                    SolutionPathTextBox.Text,
                    suppressMissingStatus: true);
                if (snapshotLoadResult == SnapshotLoadResult.CacheMissing)
                {
                    string solutionPath = NormalizeSolutionPath(SolutionPathTextBox.Text);
                    if (File.Exists(solutionPath) || Directory.Exists(solutionPath))
                    {
                        UpdateStatus(StatusCode.InitialCacheFallback);
                        await AnalyzeSolutionAsync(solutionPath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"初期化に失敗しました: {ex.Message}");
            UpdateStatus(StatusCode.InitFailed);
            SetGraphRecoveryOverlay(isVisible: true, T("status.initFailedRetry"));
            LogError($"Window initialization failed: {ex}");
        }
    }

    private void RefreshBrowseSolutionOptions()
    {
        string? selectedPath = BrowseSolutionComboBox.SelectedItem as string;
        _browseSolutionOptions.Clear();
        _browseSolutionOptions.Add(BrowseFileOptionLabel);
        _browseSolutionOptions.Add(BrowseFolderOptionLabel);
        foreach (string solutionPath in _recentSolutions)
        {
            _browseSolutionOptions.Add(solutionPath);
        }

        if (!string.IsNullOrWhiteSpace(selectedPath) &&
            _browseSolutionOptions.Any(option => string.Equals(option, selectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            SetBrowseSolutionSelection(selectedPath);
            return;
        }

        SetBrowseSolutionSelection(null);
    }

    private void SetBrowseSolutionSelection(string? selectedOption)
    {
        _isUpdatingBrowseSolutionSelection = true;
        try
        {
            BrowseSolutionComboBox.SelectedItem = selectedOption;
        }
        finally
        {
            _isUpdatingBrowseSolutionSelection = false;
        }
    }

    private async void OnBrowseSolutionSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingBrowseSolutionSelection)
        {
            return;
        }

        if (BrowseSolutionComboBox.SelectedItem is not string selectedOption || string.IsNullOrWhiteSpace(selectedOption))
        {
            return;
        }

        if (string.Equals(selectedOption, BrowseFileOptionLabel, StringComparison.Ordinal))
        {
            BrowseSolutionComboBox.SelectedIndex = -1;
            await BrowseSolutionFileAsync();
            return;
        }

        if (string.Equals(selectedOption, BrowseFolderOptionLabel, StringComparison.Ordinal))
        {
            BrowseSolutionComboBox.SelectedIndex = -1;
            await BrowseFolderAsync();
            return;
        }

        await OpenWorkspaceFromPathAsync(selectedOption, suppressMissingStatus: false);
    }

    private async Task BrowseSolutionFileAsync()
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        picker.FileTypeFilter.Add(".sln");
        picker.FileTypeFilter.Add(".slnx");
        picker.FileTypeFilter.Add(".vcxproj");
        picker.FileTypeFilter.Add(".csproj");

        nint windowHandle = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, windowHandle);

        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await OpenWorkspaceFromPathAsync(file.Path, suppressMissingStatus: false);
        }
    }

    private async Task BrowseFolderAsync()
    {
        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        picker.FileTypeFilter.Add("*");

        nint windowHandle = WindowNative.GetWindowHandle(this);
        InitializeWithWindow.Initialize(picker, windowHandle);

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            await OpenWorkspaceFromPathAsync(folder.Path, suppressMissingStatus: false);
        }
    }

    private async Task LoadCachedSnapshotForCurrentWorkspaceAsync()
    {
        string rawWorkspacePath = SolutionPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawWorkspacePath))
        {
            UpdateStatus(StatusCode.WorkspacePathRequired);
            return;
        }

        _ = await TryLoadCachedSnapshotAsync(rawWorkspacePath, suppressMissingStatus: false);
    }

    private async Task OpenWorkspaceFromPathAsync(string workspacePath, bool suppressMissingStatus)
    {
        string normalizedPath = NormalizeSolutionPath(workspacePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return;
        }

        SolutionPathTextBox.Text = normalizedPath;
        RememberRecentSolution(normalizedPath);

        SnapshotLoadResult snapshotLoadResult = await TryLoadCachedSnapshotAsync(
            normalizedPath,
            suppressMissingStatus: suppressMissingStatus);
        if (snapshotLoadResult == SnapshotLoadResult.CacheMissing &&
            (File.Exists(normalizedPath) || Directory.Exists(normalizedPath)))
        {
            await AnalyzeSolutionAsync(normalizedPath);
        }
    }

    private async Task AnalyzeCurrentWorkspaceAsync()
    {
        string rawSolutionPath = SolutionPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawSolutionPath))
        {
            UpdateStatus(StatusCode.WorkspacePathRequired);
            return;
        }

        string solutionPath = NormalizeSolutionPath(rawSolutionPath);
        if (!File.Exists(solutionPath) && !Directory.Exists(solutionPath))
        {
            UpdateStatus(StatusCode.WorkspaceNotFound, solutionPath);
            return;
        }

        SetActiveSolutionContext(solutionPath);
        RememberRecentSolution(solutionPath);
        await AnalyzeSolutionAsync(solutionPath);
    }

    private async void OnLoadCachedSnapshotClicked(object sender, RoutedEventArgs e)
    {
        await LoadCachedSnapshotForCurrentWorkspaceAsync();
    }

    private async void OnAnalyzeClicked(object sender, RoutedEventArgs e)
    {
        await AnalyzeCurrentWorkspaceAsync();
    }

    private async void OnAnalyzeKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AnalyzeCurrentWorkspaceAsync();
    }

    private async void OnLoadCacheKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await LoadCachedSnapshotForCurrentWorkspaceAsync();
    }

    private void OnFocusExplorerSearchKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        FocusExplorerSearchBox();
    }

    private void OnFocusWorkspacePathKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = SolutionPathTextBox.Focus(FocusState.Keyboard);
        SolutionPathTextBox.SelectAll();
    }

    private void OnNavigateBackKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateHistory(-1);
    }

    private void OnNavigateForwardKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NavigateHistory(1);
    }

    private void FocusExplorerSearchBox()
    {
        _ = ExplorerSearchTextBox.Focus(FocusState.Keyboard);
        ExplorerSearchTextBox.SelectAll();
    }

    private async Task AnalyzeSolutionAsync(string solutionPath)
    {
        string normalizedSolutionPath = NormalizeSolutionPath(solutionPath);
        (int sessionId, CancellationTokenSource analysisTokenSource) = BeginAnalysisSession();
        CancellationToken analysisToken = analysisTokenSource.Token;
        Interlocked.Increment(ref _snapshotLoadRequestVersion);
        CancelSnapshotLoadRequest();
        ClearAnalysisProgressDetail();

        StartWorkspaceOperation(WorkspaceOperation.Analyzing);
        UpdateStatus(StatusCode.Analyzing);
        LogInfo($"Analyze start: {normalizedSolutionPath}");

        IProgress<AnalysisProgressUpdate> progress = new Progress<AnalysisProgressUpdate>(update =>
        {
            if (!IsCurrentAnalysisSession(sessionId, normalizedSolutionPath))
            {
                return;
            }

            UpdateAnalysisProgressDisplay(update);
        });

        try
        {
            SolutionAnalysisSnapshot snapshot = await Task.Run(
                () => _analysisService.AnalyzeWorkspaceAsync(
                    normalizedSolutionPath,
                    progress,
                    analysisToken),
                analysisToken);

            if (!IsCurrentAnalysisSession(sessionId, normalizedSolutionPath))
            {
                return;
            }

            await ApplySnapshotAsync(snapshot, analysisToken);

            SnapshotPersistenceResult persistenceResult = await Task.Run(
                () => _snapshotStore.SaveSnapshotAsync(
                    snapshot,
                    analysisToken),
                analysisToken);

            if (!IsCurrentAnalysisSession(sessionId, normalizedSolutionPath))
            {
                return;
            }

            int projectCount = snapshot.Projects.Count;
            int documentCount = snapshot.Projects.Sum(project => project.Documents.Count);
            int symbolCount = _allSymbolItems.Count;

            UpdateStatus(
                StatusCode.AnalyzeComplete,
                projectCount,
                documentCount,
                symbolCount,
                persistenceResult.SnapshotId);
            LogInfo(
                $"Analyze complete: projects={projectCount}, documents={documentCount}, symbols={symbolCount}, snapshot={persistenceResult.SnapshotId}");
        }
        catch (OperationCanceledException) when (analysisToken.IsCancellationRequested)
        {
            if (!IsCurrentAnalysisSession(sessionId, normalizedSolutionPath))
            {
                return;
            }

            UpdateStatus(StatusCode.AnalyzeCanceled);
            LogInfo("Analyze canceled.");
        }
        catch (Exception ex)
        {
            if (!IsCurrentAnalysisSession(sessionId, normalizedSolutionPath))
            {
                LogError($"Analyze failed in stale session: {ex}");
                return;
            }

            UpdateStatus(StatusCode.AnalyzeFailed, ex.Message);
            AppendDiagnosticsLine(ex.ToString());
            LogError($"Analyze failed: {ex}");
        }
        finally
        {
            FinishWorkspaceOperation(WorkspaceOperation.Analyzing);
            if (_activeAnalyzeOperationCount == 0)
            {
                ClearAnalysisProgressDetail();
            }

            CleanupAnalysisSession(analysisTokenSource);
        }
    }

    private async Task<SnapshotLoadResult> TryLoadCachedSnapshotAsync(string workspacePath, bool suppressMissingStatus)
    {
        string normalizedPath = NormalizeSolutionPath(workspacePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return SnapshotLoadResult.Failed;
        }

        (int requestVersion, CancellationTokenSource loadRequestTokenSource) = BeginSnapshotLoadRequest();

        SetActiveSolutionContext(normalizedPath);
        RememberRecentSolution(normalizedPath);

        StartWorkspaceOperation(WorkspaceOperation.LoadingCache);
        if (!suppressMissingStatus)
        {
            UpdateStatus(StatusCode.CacheLoading);
        }

        LogInfo($"Load snapshot cache start: {normalizedPath}");

        try
        {
            SolutionAnalysisSnapshot? snapshot = await Task.Run(
                () => _snapshotStore.TryLoadLatestSnapshotAsync(
                    normalizedPath,
                    loadRequestTokenSource.Token),
                loadRequestTokenSource.Token);
            if (!IsCurrentSnapshotLoadRequest(requestVersion, normalizedPath))
            {
                LogInfo($"Load snapshot cache ignored due to newer request: {normalizedPath}");
                return SnapshotLoadResult.Superseded;
            }

            if (snapshot is null)
            {
                if (!suppressMissingStatus)
                {
                    UpdateStatus(StatusCode.CacheNotFound);
                }

                LogInfo("Load snapshot cache: not found.");
                return SnapshotLoadResult.CacheMissing;
            }

            await ApplySnapshotAsync(snapshot, loadRequestTokenSource.Token);

            int projectCount = snapshot.Projects.Count;
            int documentCount = snapshot.Projects.Sum(project => project.Documents.Count);
            int symbolCount = _allSymbolItems.Count;

            UpdateStatus(StatusCode.CacheLoaded, projectCount, documentCount, symbolCount);
            LogInfo($"Load snapshot cache complete: projects={projectCount}, documents={documentCount}, symbols={symbolCount}");
            return SnapshotLoadResult.Loaded;
        }
        catch (OperationCanceledException) when (loadRequestTokenSource.IsCancellationRequested)
        {
            if (IsCurrentSnapshotLoadRequest(requestVersion, normalizedPath))
            {
                LogInfo("Load snapshot cache canceled.");
            }

            return SnapshotLoadResult.Superseded;
        }
        catch (Exception ex)
        {
            if (!IsCurrentSnapshotLoadRequest(requestVersion, normalizedPath))
            {
                LogError($"Load snapshot cache failed in stale request: {ex}");
                return SnapshotLoadResult.Superseded;
            }

            if (!suppressMissingStatus)
            {
                UpdateStatus(StatusCode.CacheFailed, ex.Message);
            }

            AppendDiagnosticsLine($"キャッシュ読込に失敗しました: {ex}");
            LogError($"Load snapshot cache failed: {ex}");
            return SnapshotLoadResult.Failed;
        }
        finally
        {
            FinishWorkspaceOperation(WorkspaceOperation.LoadingCache);
            CleanupSnapshotLoadRequest(loadRequestTokenSource);
        }
    }

    private void SetActiveSolutionContext(string solutionPath)
    {
        string normalizedPath = NormalizeSolutionPath(solutionPath);
        bool workspaceChanged = !string.Equals(_activeSolutionPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
        InvalidateActiveAnalysisSession();
        _activeSolutionPath = normalizedPath;
        _currentSelectionTarget = null;
        _pendingGraphSelectionClear = true;
        _pendingGraphFocusTarget = null;
        _lastRequestedGraphFocusTarget = null;

        if (workspaceChanged)
        {
            ResetWorkspacePresentationForContextSwitch();
        }

        if (!_solutionViewStates.TryGetValue(normalizedPath, out SolutionViewState? persistedState))
        {
            persistedState = CreateDefaultSolutionViewState();
            _solutionViewStates[normalizedPath] = persistedState;
            SaveSolutionViewStates();
        }

        ApplyExplorerViewState(persistedState.Explorer);
        _pendingGraphViewState = persistedState.Graph;
    }

    private void ResetWorkspacePresentationForContextSwitch()
    {
        if (_isWindowClosed)
        {
            return;
        }

        Interlocked.Increment(ref _graphRenderRequestVersion);
        _currentSnapshot = null;
        _allSymbolItems.Clear();
        _graphPayloadCompleteness = GraphPayloadCompleteness.None;
        _isFullGraphPayloadBuildQueued = false;
        _isGraphContentRendered = false;
        _pendingGraphPayloadJson = null;
        _lastGraphPayloadJson = null;
        RefreshExplorerContent();

        GraphRenderMessage emptyGraphMessage = new(
            "render-graph",
            new GraphPayload(
                Array.Empty<GraphNodePayload>(),
                Array.Empty<GraphEdgePayload>(),
                new GraphStatsPayload(
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0)));
        string emptyGraphMessageJson = JsonSerializer.Serialize(
            emptyGraphMessage,
            CodeMapJsonSerializerContext.Default.GraphRenderMessage);
        _pendingGraphPayloadJson = emptyGraphMessageJson;
        _lastGraphPayloadJson = emptyGraphMessageJson;
        _pendingGraphSearchQuery = _workspaceSearchQuery;
        FlushPendingGraphPayload();
        FlushPendingGraphSearchQuery();
    }

    private (int SessionId, CancellationTokenSource TokenSource) BeginAnalysisSession()
    {
        CancellationTokenSource nextTokenSource = new();
        CancellationTokenSource? previousTokenSource = Interlocked.Exchange(ref _analysisCancellationTokenSource, nextTokenSource);
        if (previousTokenSource is not null)
        {
            previousTokenSource.Cancel();
            previousTokenSource.Dispose();
        }

        int sessionId = Interlocked.Increment(ref _analysisSessionId);
        return (sessionId, nextTokenSource);
    }

    private bool IsCurrentAnalysisSession(int sessionId, string solutionPath)
    {
        return
            !_isWindowClosed &&
            sessionId == Volatile.Read(ref _analysisSessionId) &&
            string.Equals(_activeSolutionPath, solutionPath, StringComparison.OrdinalIgnoreCase);
    }

    private void InvalidateActiveAnalysisSession()
    {
        Interlocked.Increment(ref _analysisSessionId);
        CancellationTokenSource? activeSession = Interlocked.Exchange(ref _analysisCancellationTokenSource, null);
        if (activeSession is null)
        {
            return;
        }

        activeSession.Cancel();
        activeSession.Dispose();
        LogInfo("Canceled active analysis because the workspace context changed.");
    }

    private void CleanupAnalysisSession(CancellationTokenSource completedTokenSource)
    {
        _ = Interlocked.CompareExchange(
            ref _analysisCancellationTokenSource,
            null,
            completedTokenSource);
        completedTokenSource.Dispose();
    }

    private (int RequestVersion, CancellationTokenSource TokenSource) BeginSnapshotLoadRequest()
    {
        CancellationTokenSource nextTokenSource = new();
        CancellationTokenSource? previousTokenSource = Interlocked.Exchange(ref _snapshotLoadCancellationTokenSource, nextTokenSource);
        if (previousTokenSource is not null)
        {
            try
            {
                previousTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        int requestVersion = Interlocked.Increment(ref _snapshotLoadRequestVersion);
        return (requestVersion, nextTokenSource);
    }

    private void CleanupSnapshotLoadRequest(CancellationTokenSource completedTokenSource)
    {
        _ = Interlocked.CompareExchange(
            ref _snapshotLoadCancellationTokenSource,
            null,
            completedTokenSource);
        completedTokenSource.Dispose();
    }

    private void CancelSnapshotLoadRequest()
    {
        CancellationTokenSource? activeRequest = Interlocked.Exchange(ref _snapshotLoadCancellationTokenSource, null);
        if (activeRequest is null)
        {
            return;
        }

        try
        {
            activeRequest.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private bool IsCurrentSnapshotLoadRequest(int requestVersion, string solutionPath)
    {
        return
            !_isWindowClosed &&
            requestVersion == Volatile.Read(ref _snapshotLoadRequestVersion) &&
            string.Equals(_activeSolutionPath, solutionPath, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplySnapshotAsync(
        SolutionAnalysisSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        _currentSnapshot = snapshot;
        PopulateDiagnostics(snapshot);
        QueueGraphRender(snapshot);

        IReadOnlyList<SymbolListItem> symbolItems = await BuildSymbolItemsAsync(snapshot, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _allSymbolItems.Clear();
        _allSymbolItems.AddRange(symbolItems);

        RefreshExplorerContent();
        UpdateWorkspaceEmptyStateVisibility();
    }

    private static Task<IReadOnlyList<SymbolListItem>> BuildSymbolItemsAsync(
        SolutionAnalysisSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<SymbolListItem>>(() =>
        {
            int estimatedSymbolCount = 0;
            foreach (ProjectAnalysisSummary project in snapshot.Projects)
            {
                foreach (DocumentAnalysisSummary document in project.Documents)
                {
                    estimatedSymbolCount += document.Symbols.Count;
                }
            }

            List<SymbolListItem> symbolItems = estimatedSymbolCount > 0
                ? new List<SymbolListItem>(estimatedSymbolCount)
                : [];

            foreach (ProjectAnalysisSummary project in snapshot.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (DocumentAnalysisSummary document in project.Documents)
                {
                    foreach (SymbolAnalysisSummary symbol in document.Symbols)
                    {
                        symbolItems.Add(new SymbolListItem(
                            symbol.Id,
                            project.Name,
                            document.Name,
                            symbol.Kind,
                            symbol.Name,
                            symbol.DisplayName,
                            symbol.LineNumber));
                    }
                }
            }

            return symbolItems;
        }, cancellationToken);
    }

    private void RefreshExplorerContent()
    {
        if (_currentSnapshot is null)
        {
            WorkspaceTreeView.RootNodes.Clear();
            _allSymbolItems.Clear();
            _symbols.Clear();
            UpdateExplorerSearchSummary(0, 0, 0, isFiltering: false);
            UpdateWorkspaceEmptyStateVisibility();
            return;
        }

        (int visibleProjects, int visibleDocuments) = PopulateWorkspaceTree(_currentSnapshot, _workspaceSearchQuery);
        int visibleSymbols = PopulateSymbolList(_workspaceSearchQuery);
        UpdateExplorerSearchSummary(
            visibleProjects,
            visibleDocuments,
            visibleSymbols,
            !string.IsNullOrWhiteSpace(_workspaceSearchQuery));
        UpdateWorkspaceEmptyStateVisibility();
    }

    private (int VisibleProjectCount, int VisibleDocumentCount) PopulateWorkspaceTree(
        SolutionAnalysisSnapshot snapshot,
        string query)
    {
        WorkspaceTreeView.RootNodes.Clear();
        HashSet<string> hiddenNodeIds = GetHiddenNodeIds();

        bool isFiltering = !string.IsNullOrWhiteSpace(query);
        string workspaceLabel = GetWorkspaceRootLabel(snapshot);
        bool workspaceMatches = MatchesQuery(workspaceLabel, query);

        TreeViewNode solutionNode = new()
        {
            Content = workspaceLabel,
            IsExpanded = true
        };

        Dictionary<string, string> projectNodeIdsByKey = new(StringComparer.OrdinalIgnoreCase);
        foreach (ProjectAnalysisSummary project in snapshot.Projects)
        {
            if (!projectNodeIdsByKey.ContainsKey(project.ProjectKey))
            {
                projectNodeIdsByKey.Add(project.ProjectKey, GraphPayloadBuilder.BuildProjectNodeId(project));
            }
        }

        int visibleProjectCount = 0;
        int visibleDocumentCount = 0;
        foreach (ProjectAnalysisSummary project in snapshot.Projects)
        {
            string projectNodeId = GraphPayloadBuilder.BuildProjectNodeId(project);
            if (hiddenNodeIds.Contains(projectNodeId))
            {
                continue;
            }

            bool projectMatches =
                MatchesQuery(project.Name, query) ||
                MatchesQuery(project.Language, query);
            bool includeAllProjectItems = !isFiltering || workspaceMatches || projectMatches;

            TreeViewNode projectNode = new()
            {
                Content = new WorkspaceTreeNodeContent(
                    $"{project.Name} [{project.Language}]",
                    new GraphSelectionTarget(projectNodeId, project.Name)),
                IsExpanded = true
            };

            int projectVisibleDocumentCount = AppendTreeChildren(
                projectNode,
                T("tree.section.files"),
                project.Documents,
                document => $"{document.Name} ({document.Symbols.Count})",
                document => new GraphSelectionTarget(document.Id, document.Name),
                query,
                includeAllProjectItems,
                alwaysShowAllItems: true,
                hiddenNodeIds: hiddenNodeIds);

            AppendTreeChildren(
                projectNode,
                T("tree.section.projectReferences"),
                project.ProjectReferences,
                projectReference => projectReference.DisplayName,
                projectReference =>
                {
                    string referenceNodeId = projectNodeIdsByKey.TryGetValue(projectReference.TargetProjectKey, out string? resolvedNodeId)
                        ? resolvedNodeId
                        : GraphPayloadBuilder.BuildProjectNodeId(projectReference.TargetProjectKey);
                    string displayLabel = string.IsNullOrWhiteSpace(projectReference.DisplayName)
                        ? projectReference.TargetProjectKey
                        : projectReference.DisplayName;
                    return new GraphSelectionTarget(referenceNodeId, displayLabel);
                },
                query,
                includeAllProjectItems,
                hiddenNodeIds: hiddenNodeIds);

            AppendTreeChildren(
                projectNode,
                T("tree.section.packages"),
                project.PackageReferences,
                packageReference => packageReference,
                packageReference => new GraphSelectionTarget(
                    GraphPayloadBuilder.BuildDependencyNodeId("package", packageReference),
                    packageReference),
                query,
                includeAllProjectItems,
                hiddenNodeIds: hiddenNodeIds);

            AppendTreeChildren(
                projectNode,
                T("tree.section.assemblies"),
                project.MetadataReferences,
                metadataReference => metadataReference,
                metadataReference => new GraphSelectionTarget(
                    GraphPayloadBuilder.BuildDependencyNodeId("assembly", metadataReference),
                    metadataReference),
                query,
                includeAllProjectItems,
                hiddenNodeIds: hiddenNodeIds);

            AppendTreeChildren(
                projectNode,
                T("tree.section.dllDependencies"),
                project.NativeDependencies,
                FormatNativeDependencyLabel,
                nativeDependency => new GraphSelectionTarget(
                    GraphPayloadBuilder.BuildDependencyNodeId("dll", nativeDependency.LibraryName),
                    FormatNativeDependencyLabel(nativeDependency)),
                query,
                includeAllProjectItems,
                hiddenNodeIds: hiddenNodeIds);

            if (includeAllProjectItems || projectNode.Children.Count > 0)
            {
                solutionNode.Children.Add(projectNode);
                visibleProjectCount++;
                visibleDocumentCount += projectVisibleDocumentCount;
            }
        }

        if (solutionNode.Children.Count == 0 && isFiltering && !workspaceMatches)
        {
            solutionNode.Children.Add(new TreeViewNode
            {
                Content = T("tree.noMatches")
            });
        }

        WorkspaceTreeView.RootNodes.Add(solutionNode);
        return (visibleProjectCount, visibleDocumentCount);
    }

    private int AppendTreeChildren<TItem>(
        TreeViewNode parentNode,
        string sectionTitle,
        IReadOnlyList<TItem> values,
        Func<TItem, string> labelSelector,
        Func<TItem, GraphSelectionTarget>? targetSelector,
        string query,
        bool includeAllItems,
        bool alwaysShowAllItems = false,
        IReadOnlySet<string>? hiddenNodeIds = null)
    {
        bool isFiltering = !string.IsNullOrWhiteSpace(query);
        List<(string Label, GraphSelectionTarget? Target)> visibleItems = [];
        foreach (TItem value in values)
        {
            GraphSelectionTarget? target = targetSelector?.Invoke(value);
            if (target is not null && hiddenNodeIds?.Contains(target.NodeId) == true)
            {
                continue;
            }

            string label = labelSelector(value);
            if (!includeAllItems && !MatchesQuery(label, query))
            {
                continue;
            }

            visibleItems.Add((label, target));
        }

        if (isFiltering && !includeAllItems && visibleItems.Count == 0)
        {
            return 0;
        }

        TreeViewNode sectionNode = new()
        {
            Content = BuildTreeSectionHeader(sectionTitle, visibleItems.Count, values.Count, isFiltering),
            IsExpanded = false
        };

        int maxItems = alwaysShowAllItems || isFiltering
            ? int.MaxValue
            : MaxTreeItemsPerSection;

        int appendedCount = 0;
        foreach ((string label, GraphSelectionTarget? target) in visibleItems)
        {
            if (appendedCount >= maxItems)
            {
                break;
            }

            sectionNode.Children.Add(new TreeViewNode
            {
                Content = new WorkspaceTreeNodeContent(label, target)
            });

            appendedCount++;
        }

        if (visibleItems.Count > maxItems)
        {
            int omittedCount = visibleItems.Count - maxItems;
            sectionNode.Children.Add(new TreeViewNode
            {
                Content = T("tree.omitted", omittedCount)
            });
        }

        parentNode.Children.Add(sectionNode);
        return visibleItems.Count;
    }

    private string BuildTreeSectionHeader(string title, int visibleCount, int totalCount, bool isFiltering)
    {
        return isFiltering && visibleCount != totalCount
            ? $"{title} ({visibleCount} / {totalCount})"
            : $"{title} ({totalCount})";
    }

    private static bool MatchesQuery(string value, string query)
    {
        return
            string.IsNullOrWhiteSpace(query) ||
            value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private string GetWorkspaceRootLabel(SolutionAnalysisSnapshot snapshot)
    {
        string fileName = Path.GetFileName(snapshot.WorkspacePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = snapshot.WorkspacePath;
        }

        string kindLabel = snapshot.WorkspaceKind switch
        {
            "folder" => T("workspace.kind.folder"),
            "solution" => T("workspace.kind.solution"),
            _ => T("workspace.kind.project")
        };

        return $"{fileName} [{kindLabel}]";
    }

    private string FormatNativeDependencyLabel(NativeDependencySummary dependency)
    {
        string confidenceLabel = dependency.Confidence switch
        {
            "confirmed" => T("nativeDependency.confidence.confirmed"),
            "high" => T("nativeDependency.confidence.high"),
            _ => dependency.Confidence
        };

        if (dependency.ImportedSymbols.Count == 0)
        {
            return $"{dependency.LibraryName} [{dependency.ImportKind}/{confidenceLabel}]";
        }

        string importedSymbols = string.Join(", ", dependency.ImportedSymbols.Take(2));
        if (dependency.ImportedSymbols.Count > 2)
        {
            importedSymbols = T("nativeDependency.more", importedSymbols, dependency.ImportedSymbols.Count - 2);
        }

        return $"{dependency.LibraryName} [{dependency.ImportKind}/{confidenceLabel}] {importedSymbols}";
    }

    private int PopulateSymbolList(string query)
    {
        _symbols.Clear();
        HashSet<string> hiddenNodeIds = GetHiddenNodeIds();

        foreach (SymbolListItem item in _allSymbolItems)
        {
            if (!hiddenNodeIds.Contains(item.SymbolId) && item.MatchesQuery(query))
            {
                _symbols.Add(item);
            }
        }

        return _symbols.Count;
    }

    private HashSet<string> GetHiddenNodeIds()
    {
        GraphViewState? activeGraphState = GetActiveGraphViewState();
        return activeGraphState?.HiddenNodes is { Count: > 0 } hiddenNodes
            ? hiddenNodes.Select(item => item.NodeId).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    private void UpdateExplorerSearchSummary(
        int visibleProjectCount,
        int visibleDocumentCount,
        int visibleSymbolCount,
        bool isFiltering)
    {
        ExplorerSearchSummaryTextBlock.Text = isFiltering
            ? T("explorer.summary.filtered", visibleProjectCount, visibleDocumentCount, visibleSymbolCount)
            : T("explorer.summary", visibleProjectCount, visibleDocumentCount, visibleSymbolCount);
    }

    private void UpdateWorkspaceEmptyStateVisibility()
    {
        bool hasSnapshot = _currentSnapshot is not null;
        ExplorerEmptyStateOverlay.Visibility = hasSnapshot
            ? Visibility.Collapsed
            : Visibility.Visible;

        bool hideGraphEmptyOverlay = hasSnapshot || GraphRecoveryOverlay.Visibility == Visibility.Visible;
        GraphEmptyStateOverlay.Visibility = hideGraphEmptyOverlay
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnExplorerSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _workspaceSearchQuery = ExplorerSearchTextBox.Text.Trim();
        RefreshExplorerContent();
        QueueGraphSearchQuery(_workspaceSearchQuery);
    }

    private void OnWorkspaceTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        TreeViewNode? selectedNode = sender.SelectedNodes.LastOrDefault();
        if (TryGetSelectionTargetFromTreeNode(selectedNode) is not GraphSelectionTarget target)
        {
            return;
        }

        RequestDependencyMapForTarget(target);
    }

    private void OnWorkspaceTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        GraphSelectionTarget? target = args.InvokedItem switch
        {
            WorkspaceTreeNodeContent nodeContent => nodeContent.SelectionTarget,
            TreeViewNode invokedNode => TryGetSelectionTargetFromTreeNode(invokedNode),
            _ => TryGetSelectionTargetFromTreeNode(sender.SelectedNodes.LastOrDefault())
        };

        if (target is null)
        {
            return;
        }

        RequestDependencyMapForTarget(target);
    }

    private void OnWorkspaceTreeDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        TreeViewNode? selectedNode = WorkspaceTreeView.SelectedNodes.LastOrDefault();
        if (TryGetSelectionTargetFromTreeNode(selectedNode) is not GraphSelectionTarget target)
        {
            return;
        }

        RequestDependencyMapForTarget(target);
        e.Handled = true;
    }

    private void OnSymbolsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SymbolsListView.SelectedItem is not SymbolListItem selectedSymbol)
        {
            return;
        }

        RequestDependencyMapForTarget(new GraphSelectionTarget(
            selectedSymbol.SymbolId,
            selectedSymbol.SymbolDisplayName));
    }

    private static GraphSelectionTarget? TryGetSelectionTargetFromTreeNode(TreeViewNode? node)
    {
        if (
            node?.Content is WorkspaceTreeNodeContent nodeContent &&
            nodeContent.SelectionTarget is GraphSelectionTarget target)
        {
            return target;
        }

        return null;
    }

    private void PopulateDiagnostics(SolutionAnalysisSnapshot snapshot)
    {
        _analysisDiagnostics.Clear();
        foreach (string diagnostic in snapshot.Diagnostics.TakeLast(MaxDiagnosticsEntries))
        {
            _analysisDiagnostics.Add(diagnostic);
        }

        if (snapshot.Diagnostics.Count == 0)
        {
            LogInfo("解析診断: なし");
        }
        else
        {
            foreach (string diagnostic in snapshot.Diagnostics)
            {
                LogInfo($"解析診断: {diagnostic}");
            }
        }

        _diagnosticsDirty = true;
        RenderDiagnostics();
    }

    private void RenderDiagnostics()
    {
        if (!_diagnosticsDirty)
        {
            return;
        }

        StringBuilder builder = new();

        foreach (string diagnostic in _analysisDiagnostics)
        {
            builder.AppendLine(diagnostic);
        }

        foreach (string graphDiagnostic in _graphDiagnostics)
        {
            builder.AppendLine(graphDiagnostic);
        }

        _diagnosticsText = builder.Length == 0
            ? T("diagnostics.none")
            : builder.ToString();
        _diagnosticsDirty = false;
    }

    private void UpdateAnalysisProgressDisplay(AnalysisProgressUpdate update)
    {
        if (_isCacheClearReanalysisInProgress)
        {
            return;
        }

        string detail = BuildAnalysisProgressDetail(update);
        if (string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        if (string.Equals(_analysisProgressDetail, detail, StringComparison.Ordinal))
        {
            return;
        }

        _analysisProgressDetail = detail;
        if (_activeAnalyzeOperationCount > 0)
        {
            UpdateStatus(
                T("status.analyzingDetail", detail),
                GetStatusSeverity(StatusCode.Analyzing),
                suppressLog: true);
        }

        UpdateGraphRecoveryOverlayVisualState();
    }

    private void ClearAnalysisProgressDetail()
    {
        _analysisProgressDetail = null;
        UpdateGraphRecoveryOverlayVisualState();
    }

    private string BuildAnalysisProgressDetail(AnalysisProgressUpdate update)
    {
        string workspaceName = GetDisplayName(update.WorkspacePath, fallback: T("workspace.kind.project"));
        string projectName = GetDisplayName(update.ProjectName, fallback: workspaceName);
        string documentName = GetDisplayName(update.DocumentPath, fallback: update.DocumentPath ?? workspaceName);

        return update.Stage switch
        {
            AnalysisProgressStage.PreparingWorkspace => T("analysis.progress.preparingWorkspace", workspaceName),
            AnalysisProgressStage.LoadingManagedSolution => T("analysis.progress.loadingManagedSolution", workspaceName),
            AnalysisProgressStage.AnalyzingManagedProject => T("analysis.progress.managedProject", projectName),
            AnalysisProgressStage.AnalyzingManagedDocument => T("analysis.progress.managedDocument", projectName, documentName),
            AnalysisProgressStage.AnalyzingFolderDocument => T("analysis.progress.folderDocument", documentName),
            AnalysisProgressStage.DiscoveringNativeProjects => T("analysis.progress.discoverNativeProjects"),
            AnalysisProgressStage.AnalyzingNativeProject => T("analysis.progress.nativeProject", projectName),
            AnalysisProgressStage.AnalyzingNativeDocument => T("analysis.progress.nativeDocument", projectName, documentName),
            AnalysisProgressStage.Finalizing => T("analysis.progress.finalizing"),
            _ => T("analysis.progress.finalizing")
        };
    }

    private static string GetDisplayName(string? pathOrName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return fallback;
        }

        string normalized = pathOrName.Trim();
        try
        {
            string fileName = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(fileName)
                ? normalized
                : fileName;
        }
        catch
        {
            return normalized;
        }
    }

    private void SetGraphRecoveryOverlay(bool isVisible, string? message = null)
    {
        _isGraphRecoveryOverlayRequested = isVisible;
        if (!string.IsNullOrWhiteSpace(message))
        {
            _graphRecoveryOverlayMessage = message;
        }
        else if (!isVisible)
        {
            _graphRecoveryOverlayMessage = null;
        }

        UpdateGraphRecoveryOverlayVisualState();
    }

    private void UpdateGraphRecoveryOverlayVisualState()
    {
        bool isAnalyzing = _activeAnalyzeOperationCount > 0;
        bool showOverlay = _isCacheClearReanalysisInProgress || _isGraphRecoveryOverlayRequested || isAnalyzing;
        GraphRecoveryOverlay.Visibility = showOverlay
            ? Visibility.Visible
            : Visibility.Collapsed;
        GraphRecoveryProgressRing.IsActive = showOverlay;

        if (showOverlay)
        {
            GraphRecoveryTextBlock.Text = ResolveGraphRecoveryOverlayMessage(isAnalyzing);
        }

        UpdateWorkspaceEmptyStateVisibility();
    }

    private string ResolveGraphRecoveryOverlayMessage(bool isAnalyzing)
    {
        if (_isCacheClearReanalysisInProgress)
        {
            return T("status.localDataReanalyzingWithRestore");
        }

        if (isAnalyzing)
        {
            return string.IsNullOrWhiteSpace(_analysisProgressDetail)
                ? T("status.analyzing")
                : T("status.analyzingDetail", _analysisProgressDetail);
        }

        if (!string.IsNullOrWhiteSpace(_graphRecoveryOverlayMessage))
        {
            return _graphRecoveryOverlayMessage;
        }

        return T("graph.recovering");
    }

    private void ReplaceGraphEnvironment(CoreWebView2Environment environment)
    {
        if (ReferenceEquals(_graphWebViewEnvironment, environment))
        {
            return;
        }

        if (_graphWebViewEnvironment is not null)
        {
            _graphWebViewEnvironment.BrowserProcessExited -= OnGraphBrowserProcessExited;
        }

        _graphWebViewEnvironment = environment;
        _graphWebViewEnvironment.BrowserProcessExited += OnGraphBrowserProcessExited;
    }

    private void DetachGraphWebViewHandlers(WebView2 webView)
    {
        webView.SizeChanged -= OnGraphWebViewSizeChanged;
        webView.NavigationCompleted -= OnGraphNavigationCompleted;
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        webView.CoreWebView2.WebMessageReceived -= OnGraphWebMessageReceived;
        webView.CoreWebView2.NavigationStarting -= OnGraphNavigationStarting;
        webView.CoreWebView2.ProcessFailed -= OnGraphWebProcessFailed;
    }

    private void RecreateGraphWebViewControl()
    {
        if (_isWindowClosed)
        {
            return;
        }

        WebView2 previousWebView = GraphWebView;
        DetachGraphWebViewHandlers(previousWebView);
        previousWebView.Visibility = Visibility.Collapsed;

        if (GraphPanelClipHost.Children.Contains(previousWebView))
        {
            GraphPanelClipHost.Children.Remove(previousWebView);
        }

        if (previousWebView is IDisposable disposablePreviousWebView)
        {
            disposablePreviousWebView.Dispose();
        }

        WebView2 replacementWebView = new()
        {
            MinHeight = GraphWebViewMinimumHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        replacementWebView.DefaultBackgroundColor = ResolveGraphWebViewBackgroundColor(ResolveGraphThemeCode());
        AutomationProperties.SetName(replacementWebView, T("a11y.graphWebView"));
        replacementWebView.SizeChanged += OnGraphWebViewSizeChanged;
        GraphPanelClipHost.Children.Insert(0, replacementWebView);
        GraphWebView = replacementWebView;
    }

    private async Task InitializeGraphFrontendAsync()
    {
        if (_isWindowClosed)
        {
            return;
        }

        try
        {
            SetGraphRecoveryOverlay(isVisible: true, T("status.graphInitializing"));
            _isGraphContentRendered = false;
            GraphWebView.DefaultBackgroundColor = ResolveGraphWebViewBackgroundColor(ResolveGraphThemeCode());
            Directory.CreateDirectory(s_webView2UserDataDirectoryPath);

            CoreWebView2EnvironmentOptions environmentOptions = new();
            if (!string.IsNullOrWhiteSpace(WebView2BrowserArguments))
            {
                environmentOptions.AdditionalBrowserArguments = WebView2BrowserArguments;
            }

            CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: null,
                userDataFolder: s_webView2UserDataDirectoryPath,
                options: environmentOptions);
            if (_isWindowClosed)
            {
                return;
            }

            ReplaceGraphEnvironment(environment);

            await GraphWebView.EnsureCoreWebView2Async(environment);
            if (_isWindowClosed)
            {
                return;
            }

            LogInfo($"WebView2 を初期化しました: 引数={WebView2BrowserArguments}");

            if (GraphWebView.CoreWebView2 is null)
            {
                throw new InvalidOperationException("CoreWebView2 が初期化されませんでした。");
            }

            GraphWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            _isGraphFrontendReady = false;
            GraphWebView.CoreWebView2.WebMessageReceived -= OnGraphWebMessageReceived;
            GraphWebView.CoreWebView2.NavigationStarting -= OnGraphNavigationStarting;
            GraphWebView.CoreWebView2.ProcessFailed -= OnGraphWebProcessFailed;
            GraphWebView.NavigationCompleted -= OnGraphNavigationCompleted;
            GraphWebView.CoreWebView2.WebMessageReceived += OnGraphWebMessageReceived;
            GraphWebView.CoreWebView2.NavigationStarting += OnGraphNavigationStarting;
            GraphWebView.CoreWebView2.ProcessFailed += OnGraphWebProcessFailed;
            GraphWebView.NavigationCompleted += OnGraphNavigationCompleted;
            _isWaitingForGraphBrowserProcessExit = false;

            string webRootPath = Path.Combine(AppContext.BaseDirectory, "Web");
            string graphPagePath = Path.Combine(webRootPath, "Graph", "index.html");
            if (!Directory.Exists(webRootPath))
            {
                throw new DirectoryNotFoundException($"Web root が見つかりません: {webRootPath}");
            }

            if (!File.Exists(graphPagePath))
            {
                throw new FileNotFoundException("グラフ フロントエンドのファイルが見つかりません。", graphPagePath);
            }

            GraphWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                GraphHostName,
                webRootPath,
                CoreWebView2HostResourceAccessKind.Allow);

            string cacheToken = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            Uri graphUri = new($"{GraphHostOrigin}/Graph/index.html?v={cacheToken}");
            LogInfo($"グラフ ソースを設定しました: {graphUri}");
            GraphWebView.Source = graphUri;
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"WebView2 の初期化に失敗しました: {ex.Message}");
            LogError($"WebView2 initialization failed: {ex}");
            ScheduleGraphFrontendRetry(GraphRecoveryMode.RecreateControl, StatusCode.GraphInitFailedRetrying);
        }
    }

    private void OnGraphNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        LogInfo($"グラフ フロントエンドのナビゲーション開始: {args.Uri}");
    }

    private void OnGraphNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        string webErrorStatus = args.WebErrorStatus.ToString();
        bool isSuccess = args.IsSuccess;

        if (DispatcherQueue is not null && !DispatcherQueue.HasThreadAccess)
        {
            bool enqueued = DispatcherQueue.TryEnqueue(() =>
            {
                HandleGraphNavigationCompleted(isSuccess, webErrorStatus);
            });

            if (!enqueued)
            {
                LogError("Failed to dispatch Graph navigation completion to UI thread.");
            }

            return;
        }

        HandleGraphNavigationCompleted(isSuccess, webErrorStatus);
    }

    private void HandleGraphNavigationCompleted(bool isSuccess, string webErrorStatus)
    {
        if (_isWindowClosed)
        {
            return;
        }

        if (!isSuccess)
        {
            AppendDiagnosticsLine($"グラフ フロントエンドのナビゲーションに失敗しました: {webErrorStatus}");
            ScheduleGraphFrontendRetry(GraphRecoveryMode.RecreateControl, StatusCode.GraphInitFailedRetrying);
            return;
        }

        ResetGraphFrontendRetryState();
        LogInfo("グラフ フロントエンドのナビゲーションが完了しました。");
        _ = EnsureGraphSurfaceVisibleAsync();
    }

    private void OnGraphWebProcessFailed(CoreWebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        if (_isWindowClosed)
        {
            return;
        }

        string processFailureMessage = $"グラフ Web プロセスが異常終了しました: 種別={args.ProcessFailedKind}, 理由={args.Reason}";
        string processDescription = args.ProcessDescription ?? string.Empty;
        CoreWebView2ProcessFailedKind failedKind = args.ProcessFailedKind;

        if (DispatcherQueue is not null && !DispatcherQueue.HasThreadAccess)
        {
            bool enqueued = DispatcherQueue.TryEnqueue(() =>
            {
                HandleGraphWebProcessFailed(failedKind, processFailureMessage, processDescription);
            });

            if (!enqueued)
            {
                LogError("Failed to dispatch Graph process failure to UI thread.");
            }

            return;
        }

        HandleGraphWebProcessFailed(failedKind, processFailureMessage, processDescription);
    }

    private void HandleGraphWebProcessFailed(
        CoreWebView2ProcessFailedKind failedKind,
        string processFailureMessage,
        string processDescription)
    {
        if (_isWindowClosed)
        {
            return;
        }

        AppendDiagnosticsLine(processFailureMessage);
        if (!string.IsNullOrWhiteSpace(processDescription))
        {
            AppendDiagnosticsLine($"グラフ Web プロセス詳細: {processDescription}");
        }

        _isGraphFrontendReady = false;
        _isGraphContentRendered = false;
        switch (failedKind)
        {
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                _isWaitingForGraphBrowserProcessExit = true;
                UpdateStatus(StatusCode.GraphEngineRestarting);
                SetGraphRecoveryOverlay(isVisible: true, T("status.graphEngineRestarting"));
                break;

            case CoreWebView2ProcessFailedKind.RenderProcessExited:
            case CoreWebView2ProcessFailedKind.FrameRenderProcessExited:
                UpdateStatus(StatusCode.GraphReloading);
                SetGraphRecoveryOverlay(isVisible: true, T("status.graphReloading"));
                _ = RecoverGraphFrontendAsync(GraphRecoveryMode.ReloadCurrentControl);
                break;

            default:
                UpdateStatus(StatusCode.GraphRecovering);
                SetGraphRecoveryOverlay(isVisible: true, T("status.graphRecovering"));
                _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
                break;
        }
    }

    private void OnGraphBrowserProcessExited(CoreWebView2Environment sender, CoreWebView2BrowserProcessExitedEventArgs args)
    {
        if (_isWindowClosed)
        {
            return;
        }

        string message = $"グラフ Browser プロセスが終了しました: kind={args.BrowserProcessExitKind}";
        if (DispatcherQueue is not null && !DispatcherQueue.HasThreadAccess)
        {
            bool enqueued = DispatcherQueue.TryEnqueue(() =>
            {
                HandleGraphBrowserProcessExited(message);
            });

            if (!enqueued)
            {
                LogError("Failed to dispatch Graph browser process exit to UI thread.");
            }

            return;
        }

        HandleGraphBrowserProcessExited(message);
    }

    private void HandleGraphBrowserProcessExited(string message)
    {
        if (_isWindowClosed)
        {
            return;
        }

        AppendDiagnosticsLine(message);
        if (!_isWaitingForGraphBrowserProcessExit)
        {
            return;
        }

        _isWaitingForGraphBrowserProcessExit = false;
        UpdateStatus(StatusCode.GraphRecreating);
        SetGraphRecoveryOverlay(isVisible: true, T("status.graphRecreating"));
        _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
    }

    private async void OnRootLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            EnsureExplorerPanelWidthWithinBounds();
            await EnsureGraphSurfaceVisibleAsync();
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"レイアウト変更の処理に失敗しました: {ex.Message}");
        }
    }

    private void OnRootLayoutActualThemeChanged(FrameworkElement sender, object args)
    {
        QueueGraphThemeUpdate();
    }

    private async void OnGraphWebViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            if (e.NewSize.Height < GraphSurfaceCollapsedHeightThreshold)
            {
                await EnsureGraphSurfaceVisibleAsync();
            }
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"グラフ領域サイズ変更の処理に失敗しました: {ex.Message}");
        }
    }

    private async Task EnsureGraphSurfaceVisibleAsync()
    {
        if (_isWindowClosed || GraphWebView.CoreWebView2 is null || _isWaitingForGraphBrowserProcessExit)
        {
            return;
        }

        if (_isRefreshingGraphSurface)
        {
            return;
        }

        _isRefreshingGraphSurface = true;
        try
        {
            bool hasActiveOverlay =
                GraphRecoveryOverlay.Visibility == Visibility.Visible ||
                GraphEmptyStateOverlay.Visibility == Visibility.Visible;
            if (hasActiveOverlay)
            {
                if (!double.IsNaN(GraphWebView.Height))
                {
                    GraphWebView.Height = double.NaN;
                }

                GraphWebView.Visibility = Visibility.Visible;
                GraphWebView.UpdateLayout();
                return;
            }

            double fallbackHeight = Math.Max(
                GraphSurfaceFallbackMinHeight,
                Math.Min(GraphSurfaceFallbackMaxHeight, RootLayout.ActualHeight * GraphSurfaceFallbackHeightRatio));
            bool forcedHeightApplied = false;
            if (GraphWebView.ActualHeight < GraphSurfaceCollapsedHeightThreshold)
            {
                GraphWebView.Height = fallbackHeight;
                forcedHeightApplied = true;
            }
            else if (!double.IsNaN(GraphWebView.Height))
            {
                GraphWebView.Height = double.NaN;
            }

            GraphWebView.Visibility = Visibility.Visible;
            GraphWebView.UpdateLayout();
            await GraphWebView.CoreWebView2.ExecuteScriptAsync("window.dispatchEvent(new Event('resize'));");
            LogInfo(
                $"グラフ描画領域の再調整を要求しました: size={GraphWebView.ActualWidth:0}x{GraphWebView.ActualHeight:0}, root={RootLayout.ActualWidth:0}x{RootLayout.ActualHeight:0}, forcedHeight={(forcedHeightApplied ? fallbackHeight.ToString("0") : "auto")}");
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"グラフ描画領域の再調整に失敗しました: {ex.Message}");
        }
        finally
        {
            _isRefreshingGraphSurface = false;
        }
    }

    private void OnGraphWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        string webMessageJson;
        try
        {
            webMessageJson = args.WebMessageAsJson;
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"グラフ メッセージの読み取りに失敗しました: {ex.Message}");
            return;
        }

        if (DispatcherQueue is not null && !DispatcherQueue.HasThreadAccess)
        {
            bool enqueued = DispatcherQueue.TryEnqueue(() =>
            {
                HandleGraphWebMessageReceived(webMessageJson);
            });

            if (!enqueued)
            {
                LogError("Failed to dispatch Graph web message to UI thread.");
            }

            return;
        }

        HandleGraphWebMessageReceived(webMessageJson);
    }

    private void HandleGraphWebMessageReceived(string webMessageJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(webMessageJson);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("type", out JsonElement typeProperty))
            {
                return;
            }

            string? messageType = typeProperty.GetString();
            switch (messageType)
            {
                case "focus-explorer-search":
                    FocusExplorerSearchBox();
                    break;

                case "graph-ui-version":
                    {
                        string version = TryGetString(root, "version");
                        string hasDependencyMapModeToggle = TryGetString(root, "hasDependencyMapModeToggle");
                        string hasDependencyMapDirectionSelect = TryGetString(root, "hasDependencyMapDirectionSelect");
                        string hasProjectsToggle = TryGetString(root, "hasProjectsToggle");
                        string hasDocumentsToggle = TryGetString(root, "hasDocumentsToggle");
                        string hasPackagesToggle = TryGetString(root, "hasPackagesToggle");
                        string hasSymbolsToggle = TryGetString(root, "hasSymbolsToggle");
                        string hasAssembliesToggle = TryGetString(root, "hasAssembliesToggle");
                        string hasDocumentDependenciesToggle = TryGetString(root, "hasDocumentDependenciesToggle");
                        string hasSymbolDependenciesToggle = TryGetString(root, "hasSymbolDependenciesToggle");
                        string hasImpactAnalysisModeToggle = TryGetString(root, "hasImpactAnalysisModeToggle");
                        string hasShowCyclesOnlyToggle = TryGetString(root, "hasShowCyclesOnlyToggle");
                        string hasSymbolTypeFilters = TryGetString(root, "hasSymbolTypeFilters");
                        string hasSearchInput = TryGetString(root, "hasSearchInput");
                        string location = TryGetString(root, "location");
                        AppendDiagnosticsLine(
                            $"Graph UI version: {version}, dependencyMapModeToggle={hasDependencyMapModeToggle}, impactModeToggle={hasImpactAnalysisModeToggle}, showCyclesOnlyToggle={hasShowCyclesOnlyToggle}, dependencyMapDirectionSelect={hasDependencyMapDirectionSelect}, projectsToggle={hasProjectsToggle}, documentsToggle={hasDocumentsToggle}, packagesToggle={hasPackagesToggle}, symbolsToggle={hasSymbolsToggle}, assembliesToggle={hasAssembliesToggle}, docDepsToggle={hasDocumentDependenciesToggle}, symbolDepsToggle={hasSymbolDependenciesToggle}, symbolTypeFilters={hasSymbolTypeFilters}, searchInput={hasSearchInput}, location={location}");
                        break;
                    }

                case "graph-ready":
                    ResetGraphFrontendRetryState();
                    _isGraphFrontendReady = true;
                    _isGraphContentRendered = false;
                    if (string.IsNullOrWhiteSpace(_pendingGraphPayloadJson) && !string.IsNullOrWhiteSpace(_lastGraphPayloadJson))
                    {
                        _pendingGraphPayloadJson = _lastGraphPayloadJson;
                    }

                    LogInfo("Graph frontend ready message received.");
                    _pendingGraphSearchQuery = _workspaceSearchQuery;
                    QueueGraphThemeUpdate();
                    bool hasGraphPayloadToRender = !string.IsNullOrWhiteSpace(_pendingGraphPayloadJson);
                    if (hasGraphPayloadToRender)
                    {
                        UpdateStatus(StatusCode.GraphLoading);
                        SetGraphRecoveryOverlay(isVisible: true, T("status.graphLoading"));
                    }
                    else
                    {
                        _isGraphContentRendered = true;
                        SetGraphRecoveryOverlay(isVisible: false);
                        UpdateStatus(StatusCode.GraphReady);
                    }

                    FlushPendingGraphLocale();
                    FlushPendingGraphTheme();
                    FlushPendingGraphViewState();
                    FlushPendingGraphPayload();
                    FlushPendingGraphSearchQuery();
                    _ = EnsureGraphSurfaceVisibleAsync();
                    break;

                case "node-selected":
                    {
                        string nodeId = TryGetString(root, "id");
                        string label = TryGetString(root, "label");
                        if (!string.IsNullOrWhiteSpace(nodeId))
                        {
                            _currentSelectionTarget = new GraphSelectionTarget(
                                nodeId,
                                string.IsNullOrWhiteSpace(label) ? nodeId : label);
                        }

                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            UpdateStatus(StatusCode.NodeSelected, label);
                            LogInfo($"Graph node selected: {label}");
                        }

                        TryRecordNavigationState();
                        break;
                    }

                case "selection-cleared":
                    {
                        _currentSelectionTarget = null;
                        TryRecordNavigationState();
                        break;
                    }

                case "node-focused":
                    {
                        string targetNodeId = TryGetString(root, "nodeId");
                        string focusedLabel = TryGetString(root, "label");
                        _lastRequestedGraphFocusTarget = null;
                        if (!string.IsNullOrWhiteSpace(targetNodeId))
                        {
                            _currentSelectionTarget = new GraphSelectionTarget(
                                targetNodeId,
                                string.IsNullOrWhiteSpace(focusedLabel) ? targetNodeId : focusedLabel);
                        }

                        if (!string.IsNullOrWhiteSpace(focusedLabel))
                        {
                            UpdateStatus(StatusCode.NodeFocus, focusedLabel);
                        }

                        TryRecordNavigationState();
                    }
                    break;

                case "node-focus-failed":
                    {
                        string targetNodeId = TryGetString(root, "nodeId");
                        string reason = TryGetString(root, "reason");
                        UpdateStatus(StatusCode.NodeFocusFailed, reason);
                        AppendDiagnosticsLine($"Node focus failed: nodeId={targetNodeId}, reason={reason}");

                        if (_graphPayloadCompleteness != GraphPayloadCompleteness.Full &&
                            string.Equals(reason, "node-not-found", StringComparison.OrdinalIgnoreCase) &&
                            IsSymbolNodeId(targetNodeId))
                        {
                            _pendingGraphFocusTarget = _lastRequestedGraphFocusTarget is GraphSelectionTarget requestedTarget &&
                                string.Equals(requestedTarget.NodeId, targetNodeId, StringComparison.Ordinal)
                                ? requestedTarget
                                : new GraphSelectionTarget(targetNodeId, targetNodeId);
                            RequestFullGraphPayloadIfNeeded("focus-symbol");
                        }

                        break;
                    }

                case "hidden-nodes-changed":
                    {
                        GraphViewState nextState = NormalizeGraphViewState(new GraphViewState
                        {
                            IncludeProjects = GetActiveGraphViewState()?.IncludeProjects ?? true,
                            IncludeDocuments = GetActiveGraphViewState()?.IncludeDocuments ?? true,
                            IncludePackages = GetActiveGraphViewState()?.IncludePackages ?? true,
                            IncludeSymbols = GetActiveGraphViewState()?.IncludeSymbols ?? false,
                            IncludeAssemblies = GetActiveGraphViewState()?.IncludeAssemblies ?? true,
                            IncludeNativeDependencies = GetActiveGraphViewState()?.IncludeNativeDependencies ?? true,
                            IncludeDocumentDependencies = GetActiveGraphViewState()?.IncludeDocumentDependencies ?? true,
                            IncludeSymbolDependencies = GetActiveGraphViewState()?.IncludeSymbolDependencies ?? true,
                            IsDependencyMapMode = GetActiveGraphViewState()?.IsDependencyMapMode ?? false,
                            IsImpactAnalysisMode = GetActiveGraphViewState()?.IsImpactAnalysisMode ?? false,
                            ShowCyclesOnly = GetActiveGraphViewState()?.ShowCyclesOnly ?? false,
                            DependencyMapDirection = GetActiveGraphViewState()?.DependencyMapDirection ?? "both",
                            PanelWidth = GetActiveGraphViewState()?.PanelWidth ?? DefaultGraphPanelWidth,
                            MobilePanelHeight = GetActiveGraphViewState()?.MobilePanelHeight ?? DefaultGraphMobilePanelHeight,
                            PinnedNodes = GetActiveGraphViewState()?.PinnedNodes ?? Array.Empty<PinnedNodeViewState>(),
                            HiddenNodes = TryGetHiddenNodes(root)
                        });
                        PersistActiveGraphViewState(nextState);
                        TryRecordNavigationState();
                        break;
                    }

                case "graph-error":
                    if (root.TryGetProperty("message", out JsonElement errorMessageProperty))
                    {
                        string? errorMessage = errorMessageProperty.GetString();
                        if (!string.IsNullOrWhiteSpace(errorMessage))
                        {
                        AppendDiagnosticsLine($"グラフ エラー: {errorMessage}");
                        }
                    }
                    break;

                case "graph-message-received":
                    {
                        int receivedNodes = TryGetInt(root, "nodeCount");
                        int receivedEdges = TryGetInt(root, "edgeCount");
                        AppendDiagnosticsLine($"グラフ メッセージを Web 側が受信しました: nodes={receivedNodes}, edges={receivedEdges}");
                        break;
                    }

                case "graph-rendered":
                    {
                        string frameTag = TryGetString(root, "frameTag");
                        bool includeProjects = TryGetBool(root, "includeProjects");
                        bool includeDocuments = TryGetBool(root, "includeDocuments");
                        bool includePackages = TryGetBool(root, "includePackages");
                        bool includeSymbols = TryGetBool(root, "includeSymbols");
                        bool includeAssemblies = TryGetBool(root, "includeAssemblies");
                        bool includeNativeDependencies = TryGetBool(root, "includeNativeDependencies");
                        bool includeDocumentDependencies = TryGetBool(root, "includeDocumentDependencies");
                        bool includeSymbolDependencies = TryGetBool(root, "includeSymbolDependencies");
                        bool isDependencyMapMode = TryGetBool(root, "isDependencyMapMode");
                        bool isImpactAnalysisMode = TryGetBool(root, "isImpactAnalysisMode");
                        bool showCyclesOnly = TryGetBool(root, "showCyclesOnly");
                        string dependencyMapDirection = TryGetString(root, "dependencyMapDirection");
                        int searchMatchCount = TryGetInt(root, "searchMatchCount");
                        int renderedNodes = TryGetInt(root, "renderedNodeCount");
                        int renderedEdges = TryGetInt(root, "renderedEdgeCount");
                        int containerWidth = TryGetInt(root, "containerWidth");
                        int containerHeight = TryGetInt(root, "containerHeight");
                        int panelWidth = TryGetInt(root, "panelWidth");
                        int mobilePanelHeight = TryGetInt(root, "mobilePanelHeight");
                        string zoom = TryGetString(root, "zoom");
                        GraphViewState graphViewState = NormalizeGraphViewState(new GraphViewState
                        {
                            IncludeProjects = includeProjects,
                            IncludeDocuments = includeDocuments,
                            IncludePackages = includePackages,
                            IncludeSymbols = includeSymbols,
                            IncludeAssemblies = includeAssemblies,
                            IncludeNativeDependencies = includeNativeDependencies,
                            IncludeDocumentDependencies = includeDocumentDependencies,
                            IncludeSymbolDependencies = includeSymbolDependencies,
                            IsDependencyMapMode = isDependencyMapMode,
                            IsImpactAnalysisMode = isImpactAnalysisMode,
                            ShowCyclesOnly = showCyclesOnly,
                            DependencyMapDirection = dependencyMapDirection,
                            PanelWidth = panelWidth,
                            MobilePanelHeight = mobilePanelHeight,
                            PinnedNodes = TryGetPinnedNodes(root),
                            HiddenNodes = TryGetHiddenNodes(root)
                        });
                        _isGraphContentRendered = true;
                        SetGraphRecoveryOverlay(isVisible: false);
                        if (renderedNodes > 0)
                        {
                            UpdateStatus(StatusCode.GraphRendered, renderedNodes, renderedEdges);
                        }
                        else
                        {
                            UpdateStatus(StatusCode.GraphRenderedSimple);
                        }
                        PersistActiveGraphViewState(graphViewState);
                        AppendDiagnosticsLine(
                            $"Graph rendered by web[{frameTag}]: nodes={renderedNodes}, edges={renderedEdges}, includeProjects={includeProjects}, includeDocuments={includeDocuments}, includePackages={includePackages}, includeSymbols={includeSymbols}, includeAssemblies={includeAssemblies}, includeNativeDependencies={includeNativeDependencies}, docDeps={includeDocumentDependencies}, symbolDeps={includeSymbolDependencies}, mapMode={isDependencyMapMode}, impactMode={isImpactAnalysisMode}, showCyclesOnly={showCyclesOnly}, mapDirection={graphViewState.DependencyMapDirection}, pinnedNodes={graphViewState.PinnedNodes.Count}, panelWidth={graphViewState.PanelWidth}, mobilePanelHeight={graphViewState.MobilePanelHeight}, searchMatches={searchMatchCount}, size={containerWidth}x{containerHeight}, zoom={zoom}");
                        if (s_enableVerboseGraphRenderDiagnostics)
                        {
                            int boundsX1 = TryGetInt(root, "boundsX1");
                            int boundsY1 = TryGetInt(root, "boundsY1");
                            int boundsX2 = TryGetInt(root, "boundsX2");
                            int boundsY2 = TryGetInt(root, "boundsY2");
                            AppendDiagnosticsLine($"グラフ境界: ({boundsX1},{boundsY1})-({boundsX2},{boundsY2})");
                        }
                        if (GraphWebView.ActualHeight < GraphSurfaceCollapsedHeightThreshold)
                        {
                            _ = EnsureGraphSurfaceVisibleAsync();
                        }
                        if (s_enableGraphPreviewCapture)
                        {
                            _ = CaptureGraphPreviewAsync(frameTag);
                        }

                        if (includeSymbols)
                        {
                            RequestFullGraphPayloadIfNeeded("include-symbols");
                        }

                        FlushPendingGraphFocusTarget();
                        FlushPendingGraphSelectionClear();
                        TryRecordNavigationState();
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"グラフ メッセージの解析に失敗しました: {ex.Message}");
        }
    }

    private void QueueGraphRender(SolutionAnalysisSnapshot snapshot)
    {
        int requestVersion = Interlocked.Increment(ref _graphRenderRequestVersion);
        bool includeSymbolsByDefault = _pendingGraphViewState?.IncludeSymbols == true;
        GraphPayloadCompleteness initialCompleteness = includeSymbolsByDefault
            ? GraphPayloadCompleteness.Full
            : GraphPayloadCompleteness.StructureOnly;
        _graphPayloadCompleteness = GraphPayloadCompleteness.None;
        _isFullGraphPayloadBuildQueued = initialCompleteness == GraphPayloadCompleteness.Full;
        _lastRequestedGraphFocusTarget = null;
        _isGraphContentRendered = false;
        UpdateStatus(StatusCode.GraphBuild);
        SetGraphRecoveryOverlay(isVisible: true, T("status.graphBuild"));
        _ = BuildGraphPayloadAsync(snapshot, requestVersion, initialCompleteness);
    }

    private async Task BuildGraphPayloadAsync(
        SolutionAnalysisSnapshot snapshot,
        int requestVersion,
        GraphPayloadCompleteness completeness)
    {
        try
        {
            (GraphPayload Payload, string MessageJson, long BuildMilliseconds, long SerializeMilliseconds, int MessageBytes) graphPayload = await Task.Run(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                GraphPayload payload = completeness == GraphPayloadCompleteness.Full
                    ? GraphPayloadBuilder.Build(snapshot)
                    : GraphPayloadBuilder.Build(snapshot, includeSymbols: false);
                long buildMilliseconds = stopwatch.ElapsedMilliseconds;

                stopwatch.Restart();
                GraphRenderMessage message = new("render-graph", payload);
                string messageJson = JsonSerializer.Serialize(
                    message,
                    CodeMapJsonSerializerContext.Default.GraphRenderMessage);
                long serializeMilliseconds = stopwatch.ElapsedMilliseconds;
                int messageBytes = Encoding.UTF8.GetByteCount(messageJson);
                return (payload, messageJson, buildMilliseconds, serializeMilliseconds, messageBytes);
            });

            if (_isWindowClosed || requestVersion != Volatile.Read(ref _graphRenderRequestVersion))
            {
                return;
            }

            if (completeness < _graphPayloadCompleteness)
            {
                return;
            }

            _graphPayloadCompleteness = completeness;
            if (completeness == GraphPayloadCompleteness.Full)
            {
                _isFullGraphPayloadBuildQueued = false;
            }

            string modeLabel = completeness == GraphPayloadCompleteness.Full ? "full" : "structure-only";
            AppendDiagnosticsLine(
                $"グラフ ペイロードをキューに追加しました: mode={modeLabel}, nodes={graphPayload.Payload.Nodes.Count}, edges={graphPayload.Payload.Edges.Count}, bytes={graphPayload.MessageBytes}, buildMs={graphPayload.BuildMilliseconds}, serializeMs={graphPayload.SerializeMilliseconds}");
            _lastGraphPayloadJson = graphPayload.MessageJson;
            _pendingGraphPayloadJson = graphPayload.MessageJson;
            _pendingGraphSearchQuery = _workspaceSearchQuery;
            FlushPendingGraphViewState();
            FlushPendingGraphPayload();
            FlushPendingGraphSearchQuery();
        }
        catch (Exception ex)
        {
            if (_isWindowClosed || requestVersion != Volatile.Read(ref _graphRenderRequestVersion))
            {
                return;
            }

            if (completeness == GraphPayloadCompleteness.Full)
            {
                _isFullGraphPayloadBuildQueued = false;
            }

            AppendDiagnosticsLine($"グラフ ペイロードの構築に失敗しました: {ex.Message}");
            UpdateStatus(StatusCode.GraphBuildFailed);
            SetGraphRecoveryOverlay(isVisible: true, T("status.graphRetry"));
            LogError($"Graph payload build failed: {ex}");
        }
    }

    private void RequestFullGraphPayloadIfNeeded(string reason)
    {
        if (_isWindowClosed ||
            _currentSnapshot is null ||
            _graphPayloadCompleteness == GraphPayloadCompleteness.Full ||
            _isFullGraphPayloadBuildQueued)
        {
            return;
        }

        int requestVersion = Volatile.Read(ref _graphRenderRequestVersion);
        _isFullGraphPayloadBuildQueued = true;
        AppendDiagnosticsLine($"フル グラフ ペイロードを要求しました: reason={reason}");
        _ = BuildGraphPayloadAsync(_currentSnapshot, requestVersion, GraphPayloadCompleteness.Full);
    }

    private static bool IsSymbolNodeId(string nodeId)
    {
        return nodeId.StartsWith("symbol:", StringComparison.Ordinal);
    }

    private void FlushPendingGraphPayload()
    {
        if (!_isGraphFrontendReady || GraphWebView.CoreWebView2 is null || string.IsNullOrWhiteSpace(_pendingGraphPayloadJson))
        {
            return;
        }

        try
        {
            GraphWebView.CoreWebView2.PostWebMessageAsJson(_pendingGraphPayloadJson);
            LogInfo("Graph payload posted to web frontend.");
            _isGraphContentRendered = false;
            _pendingGraphPayloadJson = null;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ ペイロードの送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private void FlushPendingGraphViewState()
    {
        if (!_isGraphFrontendReady || GraphWebView.CoreWebView2 is null || _pendingGraphViewState is null)
        {
            return;
        }

        try
        {
            GraphViewStateMessage message = new("apply-view-state", _pendingGraphViewState);
            string messageJson = JsonSerializer.Serialize(
                message,
                CodeMapJsonSerializerContext.Default.GraphViewStateMessage);

            GraphWebView.CoreWebView2.PostWebMessageAsJson(messageJson);
            LogInfo("Graph view state posted to web frontend.");
            _pendingGraphViewState = null;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ表示状態の送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private void RequestDependencyMapForTarget(GraphSelectionTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.NodeId))
        {
            return;
        }

        _currentSelectionTarget = target;
        _pendingGraphSelectionClear = false;
        _pendingGraphFocusTarget = target;
        FlushPendingGraphFocusTarget();
        TryRecordNavigationState();
    }

    private void FlushPendingGraphFocusTarget()
    {
        if (
            !_isGraphFrontendReady ||
            !_isGraphContentRendered ||
            GraphWebView.CoreWebView2 is null ||
            _pendingGraphFocusTarget is null)
        {
            return;
        }

        GraphSelectionTarget target = _pendingGraphFocusTarget;
        bool enableDependencyMap = GetActiveGraphViewState()?.IsImpactAnalysisMode != true;

        try
        {
            GraphFocusNodeMessage message = new(
                "focus-node",
                new GraphFocusNodePayload(
                    target.NodeId,
                    target.DisplayLabel,
                    enableDependencyMap,
                    true));
            string messageJson = JsonSerializer.Serialize(
                message,
                CodeMapJsonSerializerContext.Default.GraphFocusNodeMessage);

            GraphWebView.CoreWebView2.PostWebMessageAsJson(messageJson);
            LogInfo($"Graph focus requested: nodeId={target.NodeId}, label={target.DisplayLabel}");
            _lastRequestedGraphFocusTarget = target;
            _pendingGraphFocusTarget = null;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ フォーカス要求の送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private void QueueGraphSearchQuery(string query)
    {
        _pendingGraphSearchQuery = query;
        FlushPendingGraphSearchQuery();
    }

    private void FlushPendingGraphSearchQuery()
    {
        if (!_isGraphFrontendReady || GraphWebView.CoreWebView2 is null || _pendingGraphSearchQuery is null)
        {
            return;
        }

        try
        {
            GraphSearchQueryMessage message = new(
                "set-search-query",
                new GraphSearchQueryPayload(_pendingGraphSearchQuery));
            string messageJson = JsonSerializer.Serialize(
                message,
                CodeMapJsonSerializerContext.Default.GraphSearchQueryMessage);

            GraphWebView.CoreWebView2.PostWebMessageAsJson(messageJson);
            _pendingGraphSearchQuery = null;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ検索条件の送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private void FlushPendingGraphSelectionClear()
    {
        if (!_isGraphFrontendReady ||
            !_isGraphContentRendered ||
            GraphWebView.CoreWebView2 is null ||
            !_pendingGraphSelectionClear)
        {
            return;
        }

        try
        {
            GraphControlMessage message = new("clear-selection");
            string messageJson = JsonSerializer.Serialize(
                message,
                CodeMapJsonSerializerContext.Default.GraphControlMessage);

            GraphWebView.CoreWebView2.PostWebMessageAsJson(messageJson);
            _pendingGraphSelectionClear = false;
        }
        catch (Exception ex)
        {
            _isGraphFrontendReady = false;
            AppendDiagnosticsLine($"グラフ選択解除の送信に失敗しました: {ex.Message}");
            _ = RecoverGraphFrontendAsync(GraphRecoveryMode.RecreateControl);
        }
    }

    private GraphViewState? GetActiveGraphViewState()
    {
        if (!string.IsNullOrWhiteSpace(_activeSolutionPath) &&
            _solutionViewStates.TryGetValue(_activeSolutionPath, out SolutionViewState? activeState))
        {
            return activeState.Graph;
        }

        return _pendingGraphViewState;
    }

    private ExplorerViewState CreateCurrentExplorerViewState()
    {
        return NormalizeExplorerViewState(new ExplorerViewState
        {
            ExplorerPanelWidth = _explorerPanelWidth,
            IsWorkspaceSplit = _isWorkspaceSplit,
            WorkspaceSplitRatio = _workspaceSplitRatio,
            ActiveWorkspaceTab = SymbolTabButton.IsChecked == true ? "symbol" : "tree"
        });
    }

    private ViewNavigationState? CreateCurrentNavigationState()
    {
        if (string.IsNullOrWhiteSpace(_activeSolutionPath))
        {
            return null;
        }

        GraphViewState graphViewState = NormalizeGraphViewState(GetActiveGraphViewState() ?? new GraphViewState());
        return new ViewNavigationState(
            _activeSolutionPath,
            CreateCurrentExplorerViewState(),
            NavigationHistoryService.CreateNavigableGraphState(graphViewState),
            _currentSelectionTarget);
    }



    private void TryRecordNavigationState()
    {
        _navigationHistoryService.TryRecord(CreateCurrentNavigationState());
    }

    private void OnRootLayoutPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Microsoft.UI.Input.PointerUpdateKind updateKind = e.GetCurrentPoint(RootLayout).Properties.PointerUpdateKind;
        if (updateKind == Microsoft.UI.Input.PointerUpdateKind.XButton1Pressed)
        {
            NavigateHistory(-1);
            e.Handled = true;
            return;
        }

        if (updateKind == Microsoft.UI.Input.PointerUpdateKind.XButton2Pressed)
        {
            NavigateHistory(1);
            e.Handled = true;
        }
    }

    private void NavigateHistory(int delta)
    {
        ViewNavigationState? targetState = _navigationHistoryService.TryNavigate(delta);
        if (targetState is null)
        {
            return;
        }

        _ = RestoreNavigationStateAsync(targetState);
    }

    private async Task RestoreNavigationStateAsync(ViewNavigationState navigationState)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(navigationState.WorkspacePath) &&
                !string.Equals(_activeSolutionPath, navigationState.WorkspacePath, StringComparison.OrdinalIgnoreCase))
            {
                SolutionPathTextBox.Text = navigationState.WorkspacePath;
                SnapshotLoadResult snapshotLoadResult = await TryLoadCachedSnapshotAsync(
                    navigationState.WorkspacePath,
                    suppressMissingStatus: true);
                if (snapshotLoadResult == SnapshotLoadResult.CacheMissing &&
                    (File.Exists(navigationState.WorkspacePath) || Directory.Exists(navigationState.WorkspacePath)))
                {
                    await AnalyzeSolutionAsync(navigationState.WorkspacePath);
                }
            }

            ApplyExplorerViewState(navigationState.Explorer);

            SolutionViewState? activeState = GetOrCreateActiveSolutionViewState();
            if (activeState is not null)
            {
                activeState.Explorer = navigationState.Explorer;
                activeState.Graph = NavigationHistoryService.MergeGraphViewState(navigationState.Graph, activeState.Graph);
                _pendingGraphViewState = activeState.Graph;
                SaveSolutionViewStates();
            }
            else
            {
                _pendingGraphViewState = NavigationHistoryService.MergeGraphViewState(navigationState.Graph, null);
            }

            _currentSelectionTarget = navigationState.SelectionTarget;
            _pendingGraphFocusTarget = navigationState.SelectionTarget;
            _pendingGraphSelectionClear = navigationState.SelectionTarget is null;
            FlushPendingGraphViewState();
            FlushPendingGraphFocusTarget();
            FlushPendingGraphSelectionClear();
            TryRecordNavigationState();
        }
        catch (Exception ex)
        {
            _navigationHistoryService.CancelRestore();
            AppendDiagnosticsLine($"履歴の復元に失敗しました: {ex.Message}");
        }
    }

    private void AppendDiagnosticsLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        AppendCapped(_graphDiagnostics, line);
        LogInfo(line);
        _diagnosticsDirty = true;
    }

    private static void AppendCapped(ICollection<string> target, string value)
    {
        target.Add(value);
        if (target.Count <= MaxDiagnosticsEntries)
        {
            return;
        }

        if (target is List<string> list)
        {
            list.RemoveRange(0, list.Count - MaxDiagnosticsEntries);
            return;
        }

        while (target.Count > MaxDiagnosticsEntries)
        {
            target.Remove(target.First());
        }
    }

    private void OnWorkspaceTabChanged(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, TreeTabButton))
        {
            TreeTabButton.IsChecked = true;
            SymbolTabButton.IsChecked = false;
        }
        else
        {
            TreeTabButton.IsChecked = false;
            SymbolTabButton.IsChecked = true;
        }

        if (!_isWorkspaceSplit)
        {
            UpdateWorkspaceLayout();
        }

        PersistActiveExplorerViewState();
    }

    private void OnSplitToggleClicked(object sender, RoutedEventArgs e)
    {
        _isWorkspaceSplit = SplitToggleButton.IsChecked == true;
        UpdateWorkspaceLayout();
        PersistActiveExplorerViewState();
    }

    private void UpdateWorkspaceLayout()
    {
        if (_isWorkspaceSplit)
        {
            ApplyWorkspaceSplitRatio(_workspaceSplitRatio);
            WorkspaceTabSelector.Visibility = Visibility.Collapsed;
            WorkspaceSplitRow.Height = new GridLength(WorkspaceSplitDividerHeight);
            WorkspaceSplitDivider.Visibility = Visibility.Visible;
        }
        else
        {
            _isDraggingWorkspaceSplitDivider = false;
            WorkspaceTabSelector.Visibility = Visibility.Visible;
            WorkspaceSplitRow.Height = new GridLength(0);
            WorkspaceSplitDivider.Visibility = Visibility.Collapsed;
            bool showTree = TreeTabButton.IsChecked == true;
            TreeContentRow.Height = showTree
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
            SymbolContentRow.Height = showTree
                ? new GridLength(0)
                : new GridLength(1, GridUnitType.Star);
        }
    }

    private void OnWorkspaceSplitDividerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!_isWorkspaceSplit)
        {
            return;
        }

        _isDraggingWorkspaceSplitDivider = true;
        WorkspaceSplitDivider.CapturePointer(e.Pointer);
        UpdateWorkspaceSplitRatioFromPointer(e);
        e.Handled = true;
    }

    private void OnWorkspaceSplitDividerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isWorkspaceSplit || !_isDraggingWorkspaceSplitDivider)
        {
            return;
        }

        UpdateWorkspaceSplitRatioFromPointer(e);
        e.Handled = true;
    }

    private void OnWorkspaceSplitDividerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingWorkspaceSplitDivider)
        {
            return;
        }

        UpdateWorkspaceSplitRatioFromPointer(e);
        _isDraggingWorkspaceSplitDivider = false;
        WorkspaceSplitDivider.ReleasePointerCapture(e.Pointer);
        PersistActiveExplorerViewState();
        e.Handled = true;
    }

    private void OnWorkspaceSplitDividerPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingWorkspaceSplitDivider = false;
        PersistActiveExplorerViewState();
    }

    private void OnExplorerPanelSplitDividerPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingExplorerPanelSplitDivider = true;
        ExplorerPanelSplitDivider.CapturePointer(e.Pointer);
        UpdateExplorerPanelWidthFromPointer(e);
        e.Handled = true;
    }

    private void OnExplorerPanelSplitDividerPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingExplorerPanelSplitDivider)
        {
            return;
        }

        UpdateExplorerPanelWidthFromPointer(e);
        e.Handled = true;
    }

    private void OnExplorerPanelSplitDividerPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingExplorerPanelSplitDivider)
        {
            return;
        }

        UpdateExplorerPanelWidthFromPointer(e);
        _isDraggingExplorerPanelSplitDivider = false;
        ExplorerPanelSplitDivider.ReleasePointerCapture(e.Pointer);
        PersistActiveExplorerViewState();
        e.Handled = true;
    }

    private void OnExplorerPanelSplitDividerPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingExplorerPanelSplitDivider = false;
        PersistActiveExplorerViewState();
    }

    private void UpdateExplorerPanelWidthFromPointer(PointerRoutedEventArgs e)
    {
        double totalWidth = MainContentGrid.ActualWidth;
        if (totalWidth <= 1)
        {
            return;
        }

        double maxExplorerWidth = Math.Max(
            MinExplorerPanelWidth,
            totalWidth - ExplorerPanelSplitDividerWidth - MinGraphPanelWidth);
        double pointerX = e.GetCurrentPoint(MainContentGrid).Position.X;
        double targetWidth = Math.Clamp(
            pointerX - (ExplorerPanelSplitDividerWidth * 0.5),
            MinExplorerPanelWidth,
            maxExplorerWidth);
        SetExplorerPanelWidth(targetWidth);
    }

    private void EnsureExplorerPanelWidthWithinBounds()
    {
        double totalWidth = MainContentGrid.ActualWidth;
        if (totalWidth <= 1)
        {
            return;
        }

        double maxExplorerWidth = Math.Max(
            MinExplorerPanelWidth,
            totalWidth - ExplorerPanelSplitDividerWidth - MinGraphPanelWidth);
        double clampedWidth = Math.Clamp(_explorerPanelWidth, MinExplorerPanelWidth, maxExplorerWidth);
        SetExplorerPanelWidth(clampedWidth);
    }

    private void SetExplorerPanelWidth(double width)
    {
        _explorerPanelWidth = width;
        ExplorerPanelColumn.Width = new GridLength(width);
    }

    private void UpdateWorkspaceSplitRatioFromPointer(PointerRoutedEventArgs e)
    {
        if (!_isWorkspaceSplit)
        {
            return;
        }

        double splitterHeight = WorkspaceSplitDivider.ActualHeight;
        double availableHeight = ExplorerContentGrid.ActualHeight - splitterHeight;
        if (availableHeight <= 1)
        {
            return;
        }

        double minimumPaneHeight = Math.Clamp(
            availableHeight * WorkspaceSplitMinimumPaneRatio,
            WorkspaceSplitMinimumPaneMinHeight,
            WorkspaceSplitMinimumPaneMaxHeight);
        double pointerY = e.GetCurrentPoint(ExplorerContentGrid).Position.Y;
        double clampedTreeHeight = Math.Clamp(
            pointerY,
            minimumPaneHeight,
            Math.Max(minimumPaneHeight, availableHeight - minimumPaneHeight));
        double ratio = clampedTreeHeight / availableHeight;
        ApplyWorkspaceSplitRatio(ratio);
    }

    private void ApplyWorkspaceSplitRatio(double ratio)
    {
        double clampedRatio = Math.Clamp(ratio, MinWorkspaceSplitRatio, MaxWorkspaceSplitRatio);
        _workspaceSplitRatio = clampedRatio;
        TreeContentRow.Height = new GridLength(clampedRatio, GridUnitType.Star);
        SymbolContentRow.Height = new GridLength(1 - clampedRatio, GridUnitType.Star);
    }

    private void ScheduleGraphFrontendRetry(GraphRecoveryMode recoveryMode, StatusCode statusCode)
    {
        if (_isWindowClosed)
        {
            return;
        }

        int attempt = Interlocked.Increment(ref _graphFrontendRetryAttemptCount);
        if (attempt > MaxGraphFrontendRetryAttempts)
        {
            AppendDiagnosticsLine($"グラフ フロントエンドの再試行回数が上限に達しました: attempts={attempt - 1}");
            SetGraphRecoveryOverlay(isVisible: true, T(GetStatusLocalizationKey(StatusCode.GraphRetry)));
            UpdateStatus(StatusCode.GraphRetry);
            return;
        }

        CancellationTokenSource retryTokenSource = new();
        CancellationTokenSource? previous = Interlocked.Exchange(ref _graphFrontendRetryCancellationTokenSource, retryTokenSource);
        previous?.Cancel();
        previous?.Dispose();

        string statusMessage = T(GetStatusLocalizationKey(statusCode));
        SetGraphRecoveryOverlay(isVisible: true, statusMessage);
        UpdateStatus(statusCode);
        _ = RetryGraphFrontendAsync(recoveryMode, attempt, retryTokenSource);
    }

    private async Task RetryGraphFrontendAsync(
        GraphRecoveryMode recoveryMode,
        int attempt,
        CancellationTokenSource retryTokenSource)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(GraphFrontendRetryBaseDelayMilliseconds * attempt),
                retryTokenSource.Token);
            if (_isWindowClosed || retryTokenSource.IsCancellationRequested)
            {
                return;
            }

            await RecoverGraphFrontendAsync(recoveryMode);
        }
        catch (OperationCanceledException) when (retryTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"グラフ フロントエンドの再試行に失敗しました: {ex.Message}");
            LogError($"Graph frontend retry failed: {ex}");
        }
        finally
        {
            _ = Interlocked.CompareExchange(
                ref _graphFrontendRetryCancellationTokenSource,
                null,
                retryTokenSource);
            retryTokenSource.Dispose();
        }
    }

    private void ResetGraphFrontendRetryState()
    {
        Interlocked.Exchange(ref _graphFrontendRetryAttemptCount, 0);
        CancelPendingSave(ref _graphFrontendRetryCancellationTokenSource);
    }

    private async Task RecoverGraphFrontendAsync(GraphRecoveryMode recoveryMode)
    {
        if (_isWindowClosed || _isRecoveringGraphFrontend)
        {
            return;
        }

        _isRecoveringGraphFrontend = true;
        try
        {
            await Task.Delay(GraphFrontendRecoveryDelayMilliseconds);
            if (_isWindowClosed)
            {
                return;
            }

            _isGraphFrontendReady = false;
            _isGraphContentRendered = false;

            if (recoveryMode == GraphRecoveryMode.ReloadCurrentControl && GraphWebView.CoreWebView2 is not null)
            {
                GraphWebView.CoreWebView2.Reload();
                await EnsureGraphSurfaceVisibleAsync();
                LogInfo("Graph frontend reload requested.");
                return;
            }

            RecreateGraphWebViewControl();
            await InitializeGraphFrontendAsync();
            FlushPendingGraphLocale();
            FlushPendingGraphViewState();
            FlushPendingGraphPayload();
            FlushPendingGraphFocusTarget();
            FlushPendingGraphSelectionClear();
            FlushPendingGraphSearchQuery();
            await EnsureGraphSurfaceVisibleAsync();
            LogInfo("Graph frontend recreation requested.");
        }
        catch (Exception ex)
        {
            AppendDiagnosticsLine($"グラフ フロントエンドの復旧に失敗しました: {ex.Message}");
            SetGraphRecoveryOverlay(isVisible: true, T("status.graphRetry"));
        }
        finally
        {
            _isRecoveringGraphFrontend = false;
        }
    }

    private async Task CaptureGraphPreviewAsync(string frameTag)
    {
        if (GraphWebView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(s_captureDirectoryPath);
            string safeTag = MakeSafeFileToken(string.IsNullOrWhiteSpace(frameTag) ? "untagged" : frameTag);
            string capturePath = Path.Combine(
                s_captureDirectoryPath,
                $"graph-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{safeTag}.png");

            string captureResult = await GraphWebView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Page.captureScreenshot",
                "{\"format\":\"png\",\"fromSurface\":true}");

            using JsonDocument document = JsonDocument.Parse(captureResult);
            if (!document.RootElement.TryGetProperty("data", out JsonElement base64Element))
            {
                LogError("Graph preview capture failed: capture result does not contain data.");
                return;
            }

            string? base64 = base64Element.GetString();
            if (string.IsNullOrWhiteSpace(base64))
            {
                LogError("Graph preview capture failed: data was empty.");
                return;
            }

            byte[] imageBytes = Convert.FromBase64String(base64);
            await File.WriteAllBytesAsync(capturePath, imageBytes);
            LogInfo($"Graph preview captured: {capturePath} ({imageBytes.Length} bytes)");
        }
        catch (Exception ex)
        {
            LogError($"Graph preview capture failed: {ex.Message}");
        }
    }

    private static string MakeSafeFileToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "untagged";
        }

        StringBuilder builder = new(raw.Length);

        foreach (char ch in raw)
        {
            char normalized = char.IsWhiteSpace(ch) ? '_' : ch;
            builder.Append(s_invalidFileNameChars.Contains(normalized) ? '_' : normalized);
        }

        return builder.ToString();
    }

    private static bool IsFeatureEnabled(string environmentVariableName)
    {
        string? value = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static Microsoft.UI.Xaml.Media.Brush GetThemeBrush(string resourceKey, Windows.UI.Color fallbackColor)
    {
        if (
            Application.Current.Resources.TryGetValue(resourceKey, out object? resourceValue) &&
            resourceValue is Microsoft.UI.Xaml.Media.Brush resourceBrush)
        {
            return resourceBrush;
        }

        return new Microsoft.UI.Xaml.Media.SolidColorBrush(fallbackColor);
    }

    private static bool TryGetBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out bool boolValue) => boolValue,
            JsonValueKind.Number when value.TryGetInt32(out int intValue) => intValue != 0,
            _ => false
        };
    }

    private static int TryGetInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out int intValue) => intValue,
            JsonValueKind.String when int.TryParse(value.GetString(), out int intValue) => intValue,
            _ => 0
        };
    }

    private static string TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static IReadOnlyList<PinnedNodeViewState> TryGetPinnedNodes(JsonElement root)
    {
        if (!root.TryGetProperty("pinnedNodes", out JsonElement pinnedNodesElement) ||
            pinnedNodesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PinnedNodeViewState>();
        }

        List<PinnedNodeViewState> pinnedNodes = [];
        foreach (JsonElement pinnedNodeElement in pinnedNodesElement.EnumerateArray())
        {
            if (pinnedNodeElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string nodeId = TryGetString(pinnedNodeElement, "nodeId");
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                continue;
            }

            double x = pinnedNodeElement.TryGetProperty("x", out JsonElement xElement) && xElement.TryGetDouble(out double xValue)
                ? xValue
                : 0;
            double y = pinnedNodeElement.TryGetProperty("y", out JsonElement yElement) && yElement.TryGetDouble(out double yValue)
                ? yValue
                : 0;

            pinnedNodes.Add(new PinnedNodeViewState(nodeId.Trim(), x, y));
        }

        return NormalizePinnedNodes(pinnedNodes);
    }

    private static IReadOnlyList<HiddenNodeViewState> TryGetHiddenNodes(JsonElement root)
    {
        if (!root.TryGetProperty("hiddenNodes", out JsonElement hiddenNodesElement) ||
            hiddenNodesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HiddenNodeViewState>();
        }

        List<HiddenNodeViewState> hiddenNodes = [];
        foreach (JsonElement hiddenNodeElement in hiddenNodesElement.EnumerateArray())
        {
            if (hiddenNodeElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string nodeId = TryGetString(hiddenNodeElement, "nodeId");
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                continue;
            }

            hiddenNodes.Add(new HiddenNodeViewState(
                nodeId.Trim(),
                TryGetString(hiddenNodeElement, "label"),
                TryGetString(hiddenNodeElement, "group")));
        }

        return NormalizeHiddenNodes(hiddenNodes);
    }



    private void OnClosed(object sender, WindowEventArgs args)
    {
        _isWindowClosed = true;
        Interlocked.Increment(ref _analysisSessionId);
        Interlocked.Increment(ref _graphRenderRequestVersion);
        Interlocked.Increment(ref _snapshotLoadRequestVersion);
        _isWaitingForGraphBrowserProcessExit = false;
        SaveAppPreferencesImmediately();
        SaveRecentSolutionsImmediately();
        SaveSolutionViewStatesImmediately();
        _pendingGraphViewState = null;
        _pendingGraphFocusTarget = null;
        _lastRequestedGraphFocusTarget = null;
        _pendingGraphTheme = null;
        _isFullGraphPayloadBuildQueued = false;
        _graphPayloadCompleteness = GraphPayloadCompleteness.None;
        RootLayout.Loaded -= OnLoaded;
        RootLayout.SizeChanged -= OnRootLayoutSizeChanged;
        RootLayout.ActualThemeChanged -= OnRootLayoutActualThemeChanged;
        DetachGraphWebViewHandlers(GraphWebView);
        if (GraphPanelClipHost.Children.Contains(GraphWebView))
        {
            GraphPanelClipHost.Children.Remove(GraphWebView);
        }

        if (GraphWebView is IDisposable disposableGraphWebView)
        {
            disposableGraphWebView.Dispose();
        }

        CancellationTokenSource? analysisTokenSource = Interlocked.Exchange(ref _analysisCancellationTokenSource, null);
        analysisTokenSource?.Cancel();
        analysisTokenSource?.Dispose();
        CancelSnapshotLoadRequest();
        CancelPendingSave(ref _appPreferencesSaveCancellationTokenSource);
        CancelPendingSave(ref _recentSolutionsSaveCancellationTokenSource);
        CancelPendingSave(ref _solutionViewStatesSaveCancellationTokenSource);
        CancelPendingSave(ref _graphFrontendRetryCancellationTokenSource);
        _snapshotStore.Dispose();

        if (_graphWebViewEnvironment is not null)
        {
            _graphWebViewEnvironment.BrowserProcessExited -= OnGraphBrowserProcessExited;
            _graphWebViewEnvironment = null;
        }

        DisposeLogger();
    }
}
