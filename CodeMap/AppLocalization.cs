using System;
using System.Collections.Generic;
using System.Globalization;

namespace CodeMap;

internal enum AppLocale
{
    Japanese = 0,
    English = 1
}

internal static class AppLocalization
{
    private static AppLocale s_currentLocale = ResolveSystemLocale();

    private static readonly IReadOnlyDictionary<string, string> s_japanese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["workspace.placeholder"] = "ワークスペース (.sln / .slnx / .vcxproj / .csproj / フォルダー) のパスを入力...",
        ["browse.tooltip"] = "ソリューション ファイル/フォルダーを開く、または最近開いたワークスペースを選択",
        ["browse.placeholder"] = "最近のワークスペースを選択...",
        ["browse.solutionFile"] = "ソリューション ファイルを開く...",
        ["browse.folder"] = "フォルダーを開く...",
        ["cache.tooltip.line1"] = "現在のワークスペースに保存されている最新の解析結果を読み込みます。",
        ["cache.tooltip.line2"] = "すぐに構造を確認したいときに使います。ショートカット: Ctrl+Shift+R",
        ["cache.button"] = "前回結果",
        ["analyze.tooltip"] = "ワークスペースの解析を開始 (Ctrl+Enter)",
        ["analyze.button"] = "解析",
        ["workspace.tree"] = "ツリー",
        ["workspace.symbols"] = "シンボル",
        ["workspace.splitTooltip"] = "分割表示の切り替え",
        ["workspace.searchPlaceholder"] = "ツリー/シンボルを検索...",
        ["workspace.pathTooltip"] = "ワークスペースを入力します。ショートカットでフォーカス: Ctrl+L",
        ["workspace.searchTooltip"] = "ツリー/シンボル検索。ショートカットでフォーカス: Ctrl+F",
        ["empty.explorer.title"] = "ワークスペースを開いてください",
        ["empty.explorer.description"] = "上部の入力欄にパスを指定し、解析または前回結果を実行してください。",
        ["empty.graph.title"] = "グラフを表示する準備ができていません",
        ["empty.graph.description"] = "ワークスペースを読み込むと、依存関係グラフがここに表示されます。",
        ["explorer.summary"] = "プロジェクト {0} / ファイル {1} / シンボル {2}",
        ["explorer.summary.filtered"] = "一致: プロジェクト {0} / ファイル {1} / シンボル {2}",
        ["graph.recovering"] = "グラフを復旧しています...",
        ["status.ready"] = "準備完了",
        ["button.log"] = "ログ",
        ["button.logTooltip"] = "診断ログを表示",
        ["button.settings"] = "設定",
        ["button.settingsTooltip"] = "設定とバージョン情報を表示",
        ["a11y.solutionPath"] = "ワークスペース パス入力",
        ["a11y.browseCombo"] = "最近のワークスペース選択",
        ["a11y.loadCacheButton"] = "前回結果を読み込む",
        ["a11y.analyzeButton"] = "ワークスペース解析を開始",
        ["a11y.treeTabButton"] = "ツリー タブ",
        ["a11y.symbolTabButton"] = "シンボル タブ",
        ["a11y.splitToggleButton"] = "分割表示切り替え",
        ["a11y.explorerSearch"] = "エクスプローラー検索入力",
        ["a11y.workspaceTree"] = "ワークスペース ツリー",
        ["a11y.workspaceSplitDivider"] = "ツリーとシンボルの分割バー",
        ["a11y.symbolList"] = "シンボル リスト",
        ["a11y.explorerPanelDivider"] = "エクスプローラー パネル分割バー",
        ["a11y.graphWebView"] = "依存関係グラフ",
        ["a11y.diagnosticsButton"] = "診断ログを表示",
        ["a11y.settingsButton"] = "設定を表示",
        ["diagnostics.none"] = "診断情報はありません。",
        ["workspace.kind.folder"] = "フォルダー",
        ["workspace.kind.solution"] = "ソリューション",
        ["workspace.kind.project"] = "プロジェクト",
        ["tree.section.files"] = "ファイル",
        ["tree.section.projectReferences"] = "プロジェクト参照",
        ["tree.section.packages"] = "パッケージ",
        ["tree.section.assemblies"] = "アセンブリ",
        ["tree.section.dllDependencies"] = "DLL 依存",
        ["tree.noMatches"] = "一致する項目はありません",
        ["tree.omitted"] = "... {0} 件を省略",
        ["nativeDependency.confidence.confirmed"] = "確定",
        ["nativeDependency.confidence.high"] = "高信頼",
        ["nativeDependency.more"] = "{0} ほか {1} 件",
        ["group.project"] = "プロジェクト",
        ["group.document"] = "ドキュメント",
        ["group.symbol"] = "シンボル",
        ["group.package"] = "パッケージ",
        ["group.assembly"] = "アセンブリ",
        ["group.dll"] = "DLL",
        ["status.initialCacheFallback"] = "キャッシュが利用できないため解析を実行しています...",
        ["status.initFailed"] = "初期化失敗: ログを確認してください。",
        ["status.initFailedRetry"] = "初期化に失敗しました。再試行してください。",
        ["status.graphRetry"] = "グラフの処理に失敗しました。再試行してください。",
        ["status.workspacePathRequired"] = "ワークスペース パスを指定してください。",
        ["status.workspaceNotFound"] = "ワークスペースが見つかりません: {0}",
        ["status.analyzing"] = "解析中...",
        ["status.analyzingDetail"] = "解析中... {0}",
        ["analysis.progress.preparingWorkspace"] = "解析準備: {0}",
        ["analysis.progress.loadingManagedSolution"] = "ソリューションを読み込み中: {0}",
        ["analysis.progress.managedProject"] = "C# プロジェクトを解析中: {0}",
        ["analysis.progress.managedDocument"] = "C# ファイルを解析中: {1} ({0})",
        ["analysis.progress.folderDocument"] = "C# ファイルを解析中: {0}",
        ["analysis.progress.discoverNativeProjects"] = "C/C++ プロジェクトを検出中...",
        ["analysis.progress.nativeProject"] = "C/C++ プロジェクトを解析中: {0}",
        ["analysis.progress.nativeDocument"] = "C/C++ ファイルを解析中: {1} ({0})",
        ["analysis.progress.finalizing"] = "解析結果を集計中...",
        ["status.analyzeComplete"] = "解析完了: プロジェクト {0} 件、ドキュメント {1} 件、シンボル {2} 件、スナップショット={3}",
        ["status.analyzeCanceled"] = "解析はキャンセルされました。",
        ["status.analyzeFailed"] = "解析失敗: {0}",
        ["status.cacheLoading"] = "キャッシュを読込中...",
        ["status.cacheNotFound"] = "保存済みキャッシュはありません。解析を実行してください。",
        ["status.cacheLoaded"] = "キャッシュ読込完了: プロジェクト {0} 件、ドキュメント {1} 件、シンボル {2} 件",
        ["status.cacheFailed"] = "キャッシュ読込失敗: {0}",
        ["status.localDataClearing"] = "ローカルデータをクリアしています...",
        ["status.localDataCleared"] = "ローカルデータを完全クリアしました。",
        ["status.localDataReanalyzingWithRestore"] = "キャッシュクリア後の再解析中です。解析完了後に、復元可能な表示状態を自動で復元します。",
        ["status.localDataClearFailed"] = "ローカルデータのクリアに失敗しました: {0}",
        ["status.graphInitializing"] = "グラフを初期化しています...",
        ["status.graphInitFailedRetrying"] = "グラフの初期化に失敗しました。再試行しています...",
        ["status.graphLoading"] = "グラフを読み込んでいます...",
        ["status.graphReady"] = "グラフ表示の準備完了",
        ["status.graphEngineRestarting"] = "グラフ エンジンを再起動しています...",
        ["status.graphReloading"] = "グラフを再読み込みしています...",
        ["status.graphRecovering"] = "グラフを復旧しています...",
        ["status.graphRecreating"] = "グラフ エンジンを再作成しています...",
        ["status.graphBuild"] = "グラフを構築しています...",
        ["status.graphBuildFailed"] = "グラフ構築に失敗しました。",
        ["status.nodeSelected"] = "ノード選択: {0}",
        ["status.nodeFocus"] = "依存マップ表示: {0}",
        ["status.nodeFocusFailed"] = "依存マップ遷移失敗: {0}",
        ["status.graphRendered"] = "グラフ描画完了: ノード {0} 件、エッジ {1} 件",
        ["status.graphRenderedSimple"] = "グラフ描画完了",
        ["diag.roslyn.analysisFailed"] = "警告: マネージド解析に失敗しました: {0}",
        ["diag.roslyn.semanticModelUnavailable"] = "警告: セマンティックモデルを取得できませんでした: {0}/{1}",
        ["diag.roslyn.folderNoFiles"] = "情報: フォルダー '{0}' に解析対象の C# ファイルはありません。",
        ["diag.roslyn.folderSymbolsExtracted"] = "情報: フォルダー解析で C# シンボルを抽出しました: documents={0}, symbols={1}",
        ["diag.roslyn.packageReferenceFailed"] = "警告: PackageReference の解析に失敗しました: {0} - {1}",
        ["diag.cpp.projectFailed"] = "警告: C/C++ プロジェクト解析に失敗しました: {0} - {1}",
        ["diag.cpp.inputMissing"] = "警告: C/C++ 解析入力が見つかりません: {0}",
        ["diag.cpp.slnxParseFailed"] = "警告: .slnx の解析に失敗しました: {0}",
        ["diag.cpp.unsupportedInputSkipped"] = "情報: C/C++ 解析の対象外入力のためスキップしました: {0}",
        ["diag.cpp.slnParseFailed"] = "警告: .sln の解析に失敗しました: {0}",
        ["diag.cpp.projectEvaluation"] = "情報: C/C++ プロジェクト評価構成: project={0}, configuration={1}, platform={2}",
        ["diag.cpp.projectNoFiles"] = "情報: C/C++ プロジェクトに解析可能なソース ファイルがありません: {0}",
        ["diag.cpp.symbolsExtracted"] = "情報: C/C++ シンボルを抽出しました: project={0}, documents={1}, symbols={2}, docDeps={3}, symbolDeps={4}",
        ["diag.cpp.folderNoFiles"] = "情報: フォルダー '{0}' に解析対象の C/C++ ファイルはありません。",
        ["diag.cpp.folderSymbolsExtracted"] = "情報: フォルダー解析で C/C++ シンボルを抽出しました: documents={0}, symbols={1}, docDeps={2}, symbolDeps={3}",
        ["diag.cpp.documentParseLimitReached"] = "警告: C/C++ ドキュメント解析が上限 {0:N0} 件に達したため打ち切られました: project={1}, parsed={2:N0}, pending={3:N0}",
        ["diag.cpp.libClangProbeSuccess"] = "情報: ClangSharp プローブ成功: project={0}, file={1}",
        ["diag.cpp.libClangProbeFailed"] = "警告: ClangSharp プローブ失敗: project={0}, file={1} - {2}",
        ["diag.cpp.libClangAstSummary"] = "情報: ClangSharp AST 解析: project={0}, astDocuments={1}, regexFallback={2}, fallbackReason={3}",
        ["diag.cpp.importSkipped"] = "情報: C/C++ import の読み込みをスキップしました: {0} - {1}",
        ["diag.cpp.compileCommandsLoaded"] = "情報: compile_commands.json を読み込みました: {0}",
        ["diag.cpp.compileCommandsFailed"] = "警告: compile_commands.json の解析に失敗しました: {0}",
        ["diag.cpp.largeInputWarning"] = "警告: C/C++ 解析対象ファイル数が多いため、解析に時間がかかる可能性があります: project={0}, files={1:N0}",
        ["diag.cpp.analysisDuration.warning"] = "警告: C/C++ 解析時間: project={0}, files={1:N0}, elapsed={2:0.0}s",
        ["diag.cpp.analysisDuration.info"] = "情報: C/C++ 解析時間: project={0}, files={1:N0}, elapsed={2:0.0}s",
        ["diag.cpp.enumerationLimitReached"] = "警告: C/C++ ファイル列挙が上限 {0:N0} 件に達したため打ち切られました: root={1}",
        ["dialog.close"] = "閉じる",
        ["dialog.cancel"] = "キャンセル",
        ["dialog.logs.title"] = "診断ログ",
        ["settings.title"] = "設定",
        ["settings.section.general"] = "全般",
        ["settings.language"] = "表示言語",
        ["settings.language.japanese"] = "日本語",
        ["settings.language.english"] = "English",
        ["settings.theme"] = "テーマ",
        ["settings.theme.system"] = "システム同期",
        ["settings.theme.light"] = "ライト",
        ["settings.theme.dark"] = "ダーク",
        ["settings.section.hiddenNodes"] = "非表示ノード",
        ["settings.hiddenNodes.workspace"] = "現在のワークスペース: {0}",
        ["settings.hiddenNodes.noWorkspace"] = "ワークスペースはまだ読み込まれていません。",
        ["settings.hiddenNodes.empty"] = "このワークスペースに非表示ノードはありません。",
        ["settings.hiddenNodes.restore"] = "復元",
        ["settings.hiddenNodes.restoreAll"] = "すべて復元",
        ["settings.section.dataManagement"] = "データ管理",
        ["settings.dataManagement.description"] = "解析キャッシュ、最近開いた履歴、表示状態、WebView2 データ、ログを削除します。",
        ["settings.dataManagement.clearAll"] = "ローカルデータを完全クリア",
        ["settings.dataManagement.confirm.title"] = "ローカルデータの完全クリア",
        ["settings.dataManagement.confirm.message"] = "解析キャッシュ、履歴、表示状態、WebView2 データ、ログを削除します。この操作は元に戻せません。",
        ["settings.dataManagement.confirm.messageWithReanalyze"] = "解析キャッシュ、履歴、表示状態、WebView2 データ、ログを削除します。この操作は元に戻せません。実行後、現在のワークスペースを自動で再解析し、復元可能な表示状態を再適用します。",
        ["settings.dataManagement.confirm.primary"] = "完全クリアを実行",
        ["settings.section.about"] = "About",
        ["settings.about.version"] = "アプリ バージョン",
        ["settings.about.buildNumber"] = "ビルド番号",
        ["settings.about.buildDate"] = "最終ビルド日",
        ["settings.about.components"] = "関連コンポーネント",
        ["settings.about.component.dotnet"] = ".NET ランタイム",
        ["settings.about.component.winappsdk"] = "Windows App SDK / WinUI",
        ["settings.about.component.webview2"] = "WebView2",
        ["settings.about.component.roslyn"] = "Roslyn",
        ["settings.about.component.sqlite"] = "Microsoft.Data.Sqlite",
        ["settings.about.component.graphUi"] = "グラフ UI",
        ["settings.hiddenNode.entry"] = "[{0}] {1}"
    };

    private static readonly IReadOnlyDictionary<string, string> s_english = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["workspace.placeholder"] = "Enter a workspace path (.sln / .slnx / .vcxproj / .csproj / folder)...",
        ["browse.tooltip"] = "Open a solution file or folder, or choose a recent workspace",
        ["browse.placeholder"] = "Choose a recent workspace...",
        ["browse.solutionFile"] = "Open solution file...",
        ["browse.folder"] = "Open folder...",
        ["cache.tooltip.line1"] = "Load the latest saved analysis result for the current workspace.",
        ["cache.tooltip.line2"] = "Use this when you want to inspect the structure immediately. Shortcut: Ctrl+Shift+R",
        ["cache.button"] = "Last result",
        ["analyze.tooltip"] = "Start analyzing the workspace (Ctrl+Enter)",
        ["analyze.button"] = "Analyze",
        ["workspace.tree"] = "Tree",
        ["workspace.symbols"] = "Symbols",
        ["workspace.splitTooltip"] = "Toggle split view",
        ["workspace.searchPlaceholder"] = "Search tree / symbols...",
        ["workspace.pathTooltip"] = "Enter a workspace path. Focus shortcut: Ctrl+L",
        ["workspace.searchTooltip"] = "Search tree and symbols. Focus shortcut: Ctrl+F",
        ["empty.explorer.title"] = "Open a workspace to begin",
        ["empty.explorer.description"] = "Enter a path above, then run Analyze or load the last snapshot.",
        ["empty.graph.title"] = "The graph is not ready yet",
        ["empty.graph.description"] = "The dependency graph appears here after a workspace is loaded.",
        ["explorer.summary"] = "Projects {0} / Files {1} / Symbols {2}",
        ["explorer.summary.filtered"] = "Matches: Projects {0} / Files {1} / Symbols {2}",
        ["graph.recovering"] = "Recovering graph...",
        ["status.ready"] = "Ready",
        ["button.log"] = "Log",
        ["button.logTooltip"] = "Show diagnostic log",
        ["button.settings"] = "Settings",
        ["button.settingsTooltip"] = "Show settings and version information",
        ["a11y.solutionPath"] = "Workspace path input",
        ["a11y.browseCombo"] = "Recent workspace selector",
        ["a11y.loadCacheButton"] = "Load cached snapshot",
        ["a11y.analyzeButton"] = "Analyze workspace",
        ["a11y.treeTabButton"] = "Tree tab",
        ["a11y.symbolTabButton"] = "Symbol tab",
        ["a11y.splitToggleButton"] = "Toggle split view",
        ["a11y.explorerSearch"] = "Explorer search input",
        ["a11y.workspaceTree"] = "Workspace tree",
        ["a11y.workspaceSplitDivider"] = "Tree and symbol splitter",
        ["a11y.symbolList"] = "Symbol list",
        ["a11y.explorerPanelDivider"] = "Explorer panel splitter",
        ["a11y.graphWebView"] = "Dependency graph",
        ["a11y.diagnosticsButton"] = "Show diagnostics",
        ["a11y.settingsButton"] = "Show settings",
        ["diagnostics.none"] = "No diagnostics are available.",
        ["workspace.kind.folder"] = "Folder",
        ["workspace.kind.solution"] = "Solution",
        ["workspace.kind.project"] = "Project",
        ["tree.section.files"] = "Files",
        ["tree.section.projectReferences"] = "Project References",
        ["tree.section.packages"] = "Packages",
        ["tree.section.assemblies"] = "Assemblies",
        ["tree.section.dllDependencies"] = "DLL Dependencies",
        ["tree.noMatches"] = "No matching items",
        ["tree.omitted"] = "... omitted {0} items",
        ["nativeDependency.confidence.confirmed"] = "Confirmed",
        ["nativeDependency.confidence.high"] = "High confidence",
        ["nativeDependency.more"] = "{0} and {1} more",
        ["group.project"] = "Project",
        ["group.document"] = "Document",
        ["group.symbol"] = "Symbol",
        ["group.package"] = "Package",
        ["group.assembly"] = "Assembly",
        ["group.dll"] = "DLL",
        ["status.initialCacheFallback"] = "Cache is unavailable, running analysis...",
        ["status.initFailed"] = "Initialization failed: check the log.",
        ["status.initFailedRetry"] = "Initialization failed. Please try again.",
        ["status.graphRetry"] = "The graph operation failed. Please try again.",
        ["status.workspacePathRequired"] = "Specify a workspace path.",
        ["status.workspaceNotFound"] = "Workspace not found: {0}",
        ["status.analyzing"] = "Analyzing...",
        ["status.analyzingDetail"] = "Analyzing... {0}",
        ["analysis.progress.preparingWorkspace"] = "Preparing analysis: {0}",
        ["analysis.progress.loadingManagedSolution"] = "Loading solution: {0}",
        ["analysis.progress.managedProject"] = "Analyzing C# project: {0}",
        ["analysis.progress.managedDocument"] = "Analyzing C# file: {1} ({0})",
        ["analysis.progress.folderDocument"] = "Analyzing C# file: {0}",
        ["analysis.progress.discoverNativeProjects"] = "Discovering C/C++ projects...",
        ["analysis.progress.nativeProject"] = "Analyzing C/C++ project: {0}",
        ["analysis.progress.nativeDocument"] = "Analyzing C/C++ file: {1} ({0})",
        ["analysis.progress.finalizing"] = "Finalizing analysis results...",
        ["status.analyzeComplete"] = "Analysis complete: projects {0}, documents {1}, symbols {2}, snapshot={3}",
        ["status.analyzeCanceled"] = "Analysis was canceled.",
        ["status.analyzeFailed"] = "Analysis failed: {0}",
        ["status.cacheLoading"] = "Loading cache...",
        ["status.cacheNotFound"] = "No saved cache is available. Run Analyze first.",
        ["status.cacheLoaded"] = "Cache loaded: projects {0}, documents {1}, symbols {2}",
        ["status.cacheFailed"] = "Cache load failed: {0}",
        ["status.localDataClearing"] = "Clearing local data...",
        ["status.localDataCleared"] = "Local data was fully cleared.",
        ["status.localDataReanalyzingWithRestore"] = "Reanalyzing after cache clear. When analysis finishes, the previous display state is restored when valid.",
        ["status.localDataClearFailed"] = "Failed to clear local data: {0}",
        ["status.graphInitializing"] = "Initializing graph...",
        ["status.graphInitFailedRetrying"] = "Graph initialization failed. Retrying...",
        ["status.graphLoading"] = "Loading graph...",
        ["status.graphReady"] = "Graph is ready",
        ["status.graphEngineRestarting"] = "Restarting graph engine...",
        ["status.graphReloading"] = "Reloading graph...",
        ["status.graphRecovering"] = "Recovering graph...",
        ["status.graphRecreating"] = "Recreating graph engine...",
        ["status.graphBuild"] = "Building graph...",
        ["status.graphBuildFailed"] = "Graph build failed.",
        ["status.nodeSelected"] = "Node selected: {0}",
        ["status.nodeFocus"] = "Dependency map: {0}",
        ["status.nodeFocusFailed"] = "Dependency map navigation failed: {0}",
        ["status.graphRendered"] = "Graph rendered: nodes {0}, edges {1}",
        ["status.graphRenderedSimple"] = "Graph rendered",
        ["diag.roslyn.analysisFailed"] = "Warning: managed analysis failed: {0}",
        ["diag.roslyn.semanticModelUnavailable"] = "Warning: semantic model was unavailable: {0}/{1}",
        ["diag.roslyn.folderNoFiles"] = "Info: folder '{0}' does not contain analyzable C# files.",
        ["diag.roslyn.folderSymbolsExtracted"] = "Info: extracted C# symbols from folder analysis: documents={0}, symbols={1}",
        ["diag.roslyn.packageReferenceFailed"] = "Warning: failed to parse PackageReference entries: {0} - {1}",
        ["diag.cpp.projectFailed"] = "Warning: C/C++ project analysis failed: {0} - {1}",
        ["diag.cpp.inputMissing"] = "Warning: C/C++ analysis input was not found: {0}",
        ["diag.cpp.slnxParseFailed"] = "Warning: failed to parse .slnx: {0}",
        ["diag.cpp.unsupportedInputSkipped"] = "Info: skipped unsupported C/C++ analysis input: {0}",
        ["diag.cpp.slnParseFailed"] = "Warning: failed to parse .sln: {0}",
        ["diag.cpp.projectEvaluation"] = "Info: evaluated C/C++ project configuration: project={0}, configuration={1}, platform={2}",
        ["diag.cpp.projectNoFiles"] = "Info: C/C++ project has no analyzable source files: {0}",
        ["diag.cpp.symbolsExtracted"] = "Info: extracted C/C++ symbols: project={0}, documents={1}, symbols={2}, docDeps={3}, symbolDeps={4}",
        ["diag.cpp.folderNoFiles"] = "Info: folder '{0}' does not contain analyzable C/C++ files.",
        ["diag.cpp.folderSymbolsExtracted"] = "Info: extracted C/C++ symbols from folder analysis: documents={0}, symbols={1}, docDeps={2}, symbolDeps={3}",
        ["diag.cpp.documentParseLimitReached"] = "Warning: C/C++ document parsing stopped at the {0:N0} item limit: project={1}, parsed={2:N0}, pending={3:N0}",
        ["diag.cpp.libClangProbeSuccess"] = "Info: ClangSharp probe succeeded: project={0}, file={1}",
        ["diag.cpp.libClangProbeFailed"] = "Warning: ClangSharp probe failed: project={0}, file={1} - {2}",
        ["diag.cpp.libClangAstSummary"] = "Info: ClangSharp AST analysis: project={0}, astDocuments={1}, regexFallback={2}, fallbackReason={3}",
        ["diag.cpp.importSkipped"] = "Info: skipped loading a C/C++ import: {0} - {1}",
        ["diag.cpp.compileCommandsLoaded"] = "Info: loaded compile_commands.json: {0}",
        ["diag.cpp.compileCommandsFailed"] = "Warning: failed to parse compile_commands.json: {0}",
        ["diag.cpp.largeInputWarning"] = "Warning: this C/C++ input is large and may take longer to analyze: project={0}, files={1:N0}",
        ["diag.cpp.analysisDuration.warning"] = "Warning: C/C++ analysis duration: project={0}, files={1:N0}, elapsed={2:0.0}s",
        ["diag.cpp.analysisDuration.info"] = "Info: C/C++ analysis duration: project={0}, files={1:N0}, elapsed={2:0.0}s",
        ["diag.cpp.enumerationLimitReached"] = "Warning: C/C++ file enumeration stopped at the {0:N0} item limit: root={1}",
        ["dialog.close"] = "Close",
        ["dialog.cancel"] = "Cancel",
        ["dialog.logs.title"] = "Diagnostic Log",
        ["settings.title"] = "Settings",
        ["settings.section.general"] = "General",
        ["settings.language"] = "Display language",
        ["settings.language.japanese"] = "Japanese",
        ["settings.language.english"] = "English",
        ["settings.theme"] = "Theme",
        ["settings.theme.system"] = "Follow system",
        ["settings.theme.light"] = "Light",
        ["settings.theme.dark"] = "Dark",
        ["settings.section.hiddenNodes"] = "Hidden Nodes",
        ["settings.hiddenNodes.workspace"] = "Current workspace: {0}",
        ["settings.hiddenNodes.noWorkspace"] = "No workspace is loaded yet.",
        ["settings.hiddenNodes.empty"] = "There are no hidden nodes for this workspace.",
        ["settings.hiddenNodes.restore"] = "Restore",
        ["settings.hiddenNodes.restoreAll"] = "Restore all",
        ["settings.section.dataManagement"] = "Data Management",
        ["settings.dataManagement.description"] = "Delete analysis cache, recent workspaces, view states, WebView2 data, and logs.",
        ["settings.dataManagement.clearAll"] = "Clear all local data",
        ["settings.dataManagement.confirm.title"] = "Clear all local data",
        ["settings.dataManagement.confirm.message"] = "This will delete analysis cache, history, view state, WebView2 data, and logs. This action cannot be undone.",
        ["settings.dataManagement.confirm.messageWithReanalyze"] = "This will delete analysis cache, history, view state, WebView2 data, and logs. This action cannot be undone. After clearing, the current workspace is analyzed again automatically and the previous display state is restored when valid.",
        ["settings.dataManagement.confirm.primary"] = "Clear now",
        ["settings.section.about"] = "About",
        ["settings.about.version"] = "App version",
        ["settings.about.buildNumber"] = "Build number",
        ["settings.about.buildDate"] = "Last build date",
        ["settings.about.components"] = "Related components",
        ["settings.about.component.dotnet"] = ".NET runtime",
        ["settings.about.component.winappsdk"] = "Windows App SDK / WinUI",
        ["settings.about.component.webview2"] = "WebView2",
        ["settings.about.component.roslyn"] = "Roslyn",
        ["settings.about.component.sqlite"] = "Microsoft.Data.Sqlite",
        ["settings.about.component.graphUi"] = "Graph UI",
        ["settings.hiddenNode.entry"] = "[{0}] {1}"
    };

    public static AppLocale ResolveSystemLocale()
    {
        return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase)
            ? AppLocale.Japanese
            : AppLocale.English;
    }

    public static AppLocale ResolvePreferredLocale(string? preference)
    {
        return preference?.Trim().ToLowerInvariant() switch
        {
            "ja" or "japanese" => AppLocale.Japanese,
            "en" or "english" => AppLocale.English,
            _ => ResolveSystemLocale()
        };
    }

    public static string ToCode(AppLocale locale)
    {
        return locale == AppLocale.English ? "en" : "ja";
    }

    public static void SetCurrentLocale(AppLocale locale)
    {
        s_currentLocale = locale;
    }

    public static string GetSymbolKindLabel(string kind)
    {
        return GetSymbolKindLabel(s_currentLocale, kind);
    }

    public static string GetSymbolKindLabel(AppLocale locale, string kind)
    {
        return locale switch
        {
            AppLocale.English => kind switch
            {
                "ClassDeclaration" => "Class",
                "StructDeclaration" => "Struct",
                "InterfaceDeclaration" => "Interface",
                "EnumDeclaration" => "Enum",
                "UnionDeclaration" => "Union",
                "RecordDeclaration" => "Record",
                "MethodDeclaration" => "Method",
                "FunctionDeclaration" => "Function",
                "ConstructorDeclaration" => "Constructor",
                "PropertyDeclaration" => "Property",
                "IndexerDeclaration" => "Indexer",
                "FieldDeclaration" => "Field",
                "EventDeclaration" => "Event",
                "DelegateDeclaration" => "Delegate",
                "TypeAliasDeclaration" => "Type Alias",
                "MacroDefinition" => "Macro",
                "MacroDefinitions" => "Macro",
                _ => kind
            },
            _ => kind switch
            {
                "ClassDeclaration" => "クラス",
                "StructDeclaration" => "構造体",
                "InterfaceDeclaration" => "インターフェイス",
                "EnumDeclaration" => "列挙型",
                "UnionDeclaration" => "共用体",
                "RecordDeclaration" => "レコード",
                "MethodDeclaration" => "メソッド",
                "FunctionDeclaration" => "関数",
                "ConstructorDeclaration" => "コンストラクター",
                "PropertyDeclaration" => "プロパティ",
                "IndexerDeclaration" => "インデクサー",
                "FieldDeclaration" => "フィールド",
                "EventDeclaration" => "イベント",
                "DelegateDeclaration" => "デリゲート",
                "TypeAliasDeclaration" => "型エイリアス",
                "MacroDefinition" => "マクロ",
                "MacroDefinitions" => "マクロ",
                _ => kind
            }
        };
    }

    public static string Get(AppLocale locale, string key, params object[] args)
    {
        IReadOnlyDictionary<string, string> table = GetTable(locale);
        if (!table.TryGetValue(key, out string? value))
        {
            value = s_japanese.TryGetValue(key, out string? fallback)
                ? fallback
                : key;
        }

        if (args.Length == 0)
        {
            return value;
        }

        return string.Format(CultureInfo.InvariantCulture, value, args);
    }

    public static string Get(string key, params object[] args)
    {
        return Get(s_currentLocale, key, args);
    }

    private static IReadOnlyDictionary<string, string> GetTable(AppLocale locale)
    {
        return locale == AppLocale.English
            ? s_english
            : s_japanese;
    }
}
