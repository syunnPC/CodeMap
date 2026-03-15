namespace CodeMap;

public sealed partial class MainWindow
{
    private const int MaxTreeItemsPerSection = 96;
    private const int MaxRecentSolutions = 20;
    private const int MaxDiagnosticsEntries = 2000;

    private const double WorkspaceSplitDividerHeight = 14;
    private const double ExplorerPanelSplitDividerWidth = 12;
    private const double MinExplorerPanelWidth = 240;
    private const double MinGraphPanelWidth = 420;
    private const double MinWorkspaceSplitRatio = 0.2;
    private const double MaxWorkspaceSplitRatio = 0.8;

    private const int DefaultGraphPanelWidth = 320;
    private const int MinGraphSidebarPanelWidth = 220;
    private const int MaxGraphSidebarPanelWidth = 640;
    private const int DefaultGraphMobilePanelHeight = 320;
    private const int MinGraphMobilePanelHeight = 180;
    private const int MaxGraphMobilePanelHeight = 1600;
    private const int MaxGraphFrontendRetryAttempts = 3;

    private const int DefaultExplorerPanelWidth = 300;
    private const double DefaultWorkspaceSplitRatio = 0.5;
    private const int JsonFileWriteDebounceMilliseconds = 250;

    private const int GraphWebViewMinimumHeight = 360;
    private const int GraphSurfaceCollapsedHeightThreshold = 64;
    private const int GraphSurfaceFallbackMinHeight = 420;
    private const int GraphSurfaceFallbackMaxHeight = 1024;
    private const double GraphSurfaceFallbackHeightRatio = 0.68;
    private const double WorkspaceSplitMinimumPaneRatio = 0.18;
    private const int WorkspaceSplitMinimumPaneMinHeight = 72;
    private const int WorkspaceSplitMinimumPaneMaxHeight = 180;
    private const int GraphFrontendRetryBaseDelayMilliseconds = 400;
    private const int GraphFrontendRecoveryDelayMilliseconds = 250;

    private const int SettingsDialogMaxHeight = 640;
    private const int SettingsDialogMaxWidth = 720;
    private const int SettingsDialogRootSpacing = 18;
    private const int SettingsSectionSpacing = 8;
    private const int SettingsComboMinWidth = 220;
    private const int SettingsHiddenNodesRowColumnSpacing = 12;
    private const int SettingsSectionHeaderFontSize = 15;
    private const int SettingsRowColumnSpacing = 16;
    private const int SettingsRowLabelColumnWidth = 220;
    private const int DiagnosticsDialogMaxHeight = 480;
    private const double DiagnosticsDialogFontSize = 12.5;

    private const string GraphHostName = "codemap.local";
    private const string GraphHostOrigin = "https://codemap.local";
}
