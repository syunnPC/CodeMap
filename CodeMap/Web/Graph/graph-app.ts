
type GraphGroup = "project" | "document" | "symbol" | "package" | "assembly" | "dll";
type SearchTargetGroup = "document" | "symbol";
type DependencyMapDirection = "outgoing" | "incoming" | "both";

interface FocusNodeRequest {
  nodeId: string;
  label?: string;
  enableDependencyMap: boolean;
  forceVisible: boolean;
}

interface ViewStateRequest {
  includeProjects?: boolean;
  includeDocuments?: boolean;
  includePackages?: boolean;
  includeSymbols?: boolean;
  includeAssemblies?: boolean;
  includeNativeDependencies?: boolean;
  includeDocumentDependencies?: boolean;
  includeSymbolDependencies?: boolean;
  isDependencyMapMode?: boolean;
  isImpactAnalysisMode?: boolean;
  showCyclesOnly?: boolean;
  dependencyMapDirection?: DependencyMapDirection;
  panelWidth?: number;
  mobilePanelHeight?: number;
  pinnedNodes?: PinnedNodeViewState[];
  hiddenNodes?: HiddenNodeViewState[];
}

interface PinnedNodeViewState {
  nodeId: string;
  x: number;
  y: number;
}

interface HiddenNodeViewState {
  nodeId: string;
  label: string;
  group: string;
}

interface InspectorReferenceItem {
  nodeId: string;
  label: string;
  displayText: string;
}

interface GraphEdgeLike {
  data(name?: string): unknown;
  source(): any;
  target(): any;
}

interface GraphEdgeCollectionLike {
  forEach(callback: (edge: GraphEdgeLike) => void): void;
  filter(predicate: (edge: GraphEdgeLike) => boolean): GraphEdgeCollectionLike;
}

interface GraphNodePayload {
  id: string;
  label: string;
  group: GraphGroup;
  parentId?: string | null;
  symbolKind?: string | null;
  fileName?: string | null;
  isInCycle?: boolean;
}

interface GraphEdgePayload {
  id: string;
  source: string;
  target: string;
  kind: string;
  weight: number;
  referenceKind?: string | null;
  confidence?: string | null;
  sampleFilePath?: string | null;
  sampleLineNumber?: number | null;
  sampleSnippet?: string | null;
  isCycleEdge?: boolean;
}

interface GraphStatsPayload {
  projectCount: number;
  documentCount: number;
  symbolCount: number;
  nativeDependencyCount: number;
  documentDependencyCount: number;
  symbolDependencyCount: number;
  projectCycleCount: number;
  documentCycleCount: number;
  symbolCycleCount: number;
  edgeCount: number;
}

interface VisibleGraphMetrics {
  visibleNodeCount: number;
  visibleEdgeCount: number;
  symbolNodeCount: number;
  isolatedNodeCount: number;
}

interface NodeReferenceStats {
  incomingEdges: GraphEdgeCollectionLike;
  outgoingEdges: GraphEdgeCollectionLike;
  incomingWeight: number;
  outgoingWeight: number;
}

interface GraphPayload {
  nodes: GraphNodePayload[];
  edges: GraphEdgePayload[];
  stats: GraphStatsPayload;
}

type WeightedAdjacency = Map<string, Map<string, number>>;

interface LocalNodePosition {
  x: number;
  y: number;
}

interface OwnershipMaps {
  documentToProject: Map<string, string>;
  symbolToDocument: Map<string, string>;
  externalToProject: Map<string, string>;
  projectToDocuments: Map<string, string[]>;
  documentToSymbols: Map<string, string[]>;
  projectLabels: Map<string, string>;
}

interface SemanticDocumentLayout {
  documentId: string;
  documentNode: any;
  width: number;
  height: number;
  bias: number;
  importance: number;
  positions: Map<string, LocalNodePosition>;
}

interface SemanticProjectCluster {
  projectId: string;
  projectNode: any | null;
  width: number;
  height: number;
  positions: Map<string, LocalNodePosition>;
}

interface HostMessage<T = unknown> {
  type: string;
  data?: T;
  [key: string]: unknown;
}

interface WebViewBridge {
  addEventListener(type: "message", listener: (event: MessageEvent) => void): void;
  postMessage(message: unknown): void;
}

interface ChromiumWindow extends Window {
  __codeMapGraphInitialized?: boolean;
  chrome?: {
    webview?: WebViewBridge;
  };
}

declare const cytoscape: any;
const GRAPH_UI_VERSION = "graph-ui-20260313a";
const PANEL_WIDTH_STORAGE_KEY = "codemap.graph.panelWidth";
const MOBILE_PANEL_HEIGHT_STORAGE_KEY = "codemap.graph.mobilePanelHeight";
const DEFAULT_PANEL_WIDTH = 320;
const MIN_PANEL_WIDTH = 220;
const MAX_PANEL_WIDTH = 640;
const DEFAULT_MOBILE_PANEL_HEIGHT = 320;
const MIN_MOBILE_PANEL_HEIGHT = 180;
const MIN_MOBILE_GRAPH_HEIGHT = 220;
const MOBILE_SPLITTER_HEIGHT = 10;
const LARGE_GRAPH_NODE_THRESHOLD = 900;
const LARGE_GRAPH_EDGE_THRESHOLD = 2600;
const MASSIVE_GRAPH_NODE_THRESHOLD = 1800;
const MASSIVE_GRAPH_EDGE_THRESHOLD = 6200;
const SEARCH_INPUT_DEBOUNCE_MS = 120;

type Locale = "ja" | "en";
type GraphPerformanceMode = "normal" | "large" | "massive";
type HostTheme = "light" | "dark" | "system";

const translations: Record<Locale, Record<string, string>> = {
  ja: {
    "page.title": "CodeMap グラフ",
    "graph.ariaLabel": "依存関係グラフ",
    "panel.controls": "グラフ設定パネル",
    "panel.display": "表示",
    "panel.symbolTypes": "シンボル種別",
    "panel.legend": "凡例",
    "panel.inspector": "詳細",
    "toggle.projects": "プロジェクトを表示",
    "toggle.documents": "ドキュメントを表示",
    "toggle.packages": "パッケージを表示",
    "toggle.symbols": "シンボルを表示",
    "toggle.assemblies": "アセンブリを表示",
    "toggle.nativeDependencies": "実行時 DLL を表示",
    "toggle.documentDependencies": "ドキュメント依存を表示",
    "toggle.symbolDependencies": "シンボル依存を表示",
    "toggle.dependencyMapMode": "選択ノード依存マップモード",
    "toggle.impactAnalysisMode": "影響範囲解析モード",
    "toggle.showCyclesOnly": "循環依存のみ表示",
    "dependencyMap.direction.label": "依存方向",
    "dependencyMap.direction.outgoing": "参照先のみ",
    "dependencyMap.direction.incoming": "参照元のみ",
    "dependencyMap.direction.both": "参照元/参照先",
    "button.fit": "表示範囲に合わせる",
    "button.layout": "レイアウト再計算",
    "button.dependencyMap": "選択ノード依存マップ",
    "button.resetDependencyMap": "依存マップを解除",
    "button.pinSelected": "選択ノードを固定",
    "button.unpinAll": "固定を解除",
    "button.hideSelected": "選択ノードを非表示",
    "button.exportView": "表示状態を保存",
    "button.importView": "表示状態を読込",
    "button.copyShare": "共有JSONをコピー",
    "button.focusSearch": "一致へ移動",
    "button.clearSearch": "検索クリア",
    "search.placeholder": "ドキュメント/シンボルを検索...",
    "search.empty": "検索語を入力してください。",
    "search.none": "一致するノードはありません。",
    "search.count": "{0} 件一致",
    "symbolTypes.empty": "利用可能なシンボル種別はありません。",
    "inspector.empty": "ノードを選択すると詳細が表示されます。",
    "inspector.label": "名前",
    "inspector.group": "分類",
    "inspector.type": "種類",
    "inspector.file": "ファイル",
    "inspector.id": "ID",
    "inspector.symbolKind": "シンボル種別",
    "inspector.in": "被参照数",
    "inspector.out": "参照数",
    "inspector.referencesFrom": "参照元",
    "inspector.referencesTo": "参照先",
    "inspector.impact": "影響範囲",
    "inspector.cycles": "循環依存",
    "inspector.pinned": "固定",
    "legend.section.nodes": "ノード",
    "legend.section.references": "参照ライン",
    "legend.hintLabel": "ヒント",
    "legend.project": "プロジェクト",
    "legend.package": "パッケージ",
    "legend.assembly": "アセンブリ",
    "legend.dll": "DLL",
    "legend.projectDependency": "プロジェクト依存",
    "legend.externalDependency": "外部依存",
    "legend.document": "ドキュメント",
    "legend.symbol": "シンボル",
    "legend.documentDependency": "ドキュメント依存",
    "legend.symbolDependency": "シンボル依存",
    "legend.callDependency": "呼び出し",
    "legend.inheritanceDependency": "継承/実装",
    "legend.cycleDependency": "循環依存",
    "legend.note": "実線は直接参照、破線や点線は依存種別の違い、太い線は参照数の多さを示します。",
    "legend.hoverHint": "矢印にホバーすると参照の意味と実例を表示します。",
    "group.project": "プロジェクト",
    "group.document": "ドキュメント",
    "group.symbol": "シンボル",
    "group.package": "パッケージ",
    "group.assembly": "アセンブリ",
    "group.dll": "DLL",
    "state.yes": "あり",
    "state.no": "なし",
    "state.enabled": "有効",
    "state.disabled": "無効",
    "tooltip.type": "種類",
    "tooltip.file": "ファイル",
    "tooltip.incoming": "被参照数",
    "tooltip.outgoing": "参照数",
    "tooltip.unavailable": "該当なし",
    "edge.details": "参照ライン",
    "edge.kind": "ライン種別",
    "edge.description": "説明",
    "edge.from": "参照元",
    "edge.to": "参照先",
    "edge.references": "参照数",
    "edge.confidence": "確度",
    "edge.location": "実例位置",
    "edge.example": "実例",
    "edge.referenceKind": "参照分類",
    "edge.kind.contains-document": "プロジェクト包含",
    "edge.kind.contains-symbol": "ドキュメント包含",
    "edge.kind.project-reference": "プロジェクト参照",
    "edge.kind.project-package": "パッケージ参照",
    "edge.kind.project-assembly": "アセンブリ参照",
    "edge.kind.project-dll": "実行時 DLL 参照",
    "edge.kind.document-reference": "ドキュメント参照",
    "edge.kind.symbol-reference": "シンボル参照",
    "edge.kind.symbol-call": "関数呼び出し",
    "edge.kind.symbol-inheritance": "継承",
    "edge.kind.symbol-implementation": "実装",
    "edge.kind.symbol-creation": "生成",
    "edge.description.contains-document": "プロジェクトに属するドキュメントです。",
    "edge.description.contains-symbol": "ドキュメントに属するシンボルです。",
    "edge.description.project-reference": "プロジェクトが別プロジェクトを参照しています。",
    "edge.description.project-package": "プロジェクトがパッケージへ依存しています。",
    "edge.description.project-assembly": "プロジェクトがアセンブリ参照を持っています。",
    "edge.description.project-dll": "実行時に利用する DLL への依存です。",
    "edge.description.document-reference": "ファイル間の include / using / 型参照などを集約したドキュメント依存です。",
    "edge.description.symbol-reference": "シンボル間の一般参照です。",
    "edge.description.symbol-call": "関数やメソッドの呼び出しです。",
    "edge.description.symbol-inheritance": "継承関係です。",
    "edge.description.symbol-implementation": "実装関係です。",
    "edge.description.symbol-creation": "インスタンス生成やファクトリ生成です。",
    "edge.referenceKind.reference": "一般参照",
    "edge.referenceKind.call": "呼び出し",
    "edge.referenceKind.creation": "生成",
    "edge.referenceKind.inheritance": "継承",
    "edge.referenceKind.implementation": "実装",
    "edge.referenceKind.parameter": "パラメーター",
    "edge.referenceKind.field": "フィールド",
    "edge.referenceKind.return": "戻り値",
    "confidence.high": "高",
    "confidence.medium": "中",
    "confidence.low": "低",
    "splitter.ariaLabel": "サイドパネル幅を調整",
    "splitter.mobileAriaLabel": "グラフと詳細パネルの高さ比率を調整",
    "error.containerNotFound": "グラフ描画領域が見つかりません。",
    "error.cytoscapeNotAvailable": "Cytoscape.js の読み込みに失敗しました。",
    "error.dependencyMapNoSelection": "依存マップを作成するにはノードを選択してください。"
  },
  en: {
    "page.title": "CodeMap Graph",
    "graph.ariaLabel": "Dependency graph",
    "panel.controls": "Graph controls",
    "panel.display": "Display",
    "panel.symbolTypes": "Symbol Types",
    "panel.legend": "Legend",
    "panel.inspector": "Inspector",
    "toggle.projects": "Show projects",
    "toggle.documents": "Show documents",
    "toggle.packages": "Show packages",
    "toggle.symbols": "Show symbols",
    "toggle.assemblies": "Show assemblies",
    "toggle.nativeDependencies": "Show runtime DLLs",
    "toggle.documentDependencies": "Show document dependencies",
    "toggle.symbolDependencies": "Show symbol dependencies",
    "toggle.dependencyMapMode": "Selection dependency map mode",
    "toggle.impactAnalysisMode": "Impact analysis mode",
    "toggle.showCyclesOnly": "Show cycles only",
    "dependencyMap.direction.label": "Dependency direction",
    "dependencyMap.direction.outgoing": "Outgoing only",
    "dependencyMap.direction.incoming": "Incoming only",
    "dependencyMap.direction.both": "Incoming and outgoing",
    "button.fit": "Fit visible",
    "button.layout": "Re-layout",
    "button.dependencyMap": "Selected dependency map",
    "button.resetDependencyMap": "Reset dependency map",
    "button.pinSelected": "Pin selection",
    "button.unpinAll": "Clear pins",
    "button.hideSelected": "Hide selection",
    "button.exportView": "Save view",
    "button.importView": "Load view",
    "button.copyShare": "Copy share JSON",
    "button.focusSearch": "Focus matches",
    "button.clearSearch": "Clear search",
    "search.placeholder": "Search documents/symbols...",
    "search.empty": "Type a query.",
    "search.none": "No matching nodes.",
    "search.count": "{0} matches",
    "symbolTypes.empty": "No symbol types available.",
    "inspector.empty": "Select a node to view details.",
    "inspector.label": "Name",
    "inspector.group": "Group",
    "inspector.type": "Type",
    "inspector.file": "File",
    "inspector.id": "ID",
    "inspector.symbolKind": "Symbol kind",
    "inspector.in": "Referenced by",
    "inspector.out": "References",
    "inspector.referencesFrom": "Referenced from",
    "inspector.referencesTo": "References to",
    "inspector.impact": "Impact",
    "inspector.cycles": "Cycles",
    "inspector.pinned": "Pinned",
    "legend.section.nodes": "Nodes",
    "legend.section.references": "Reference lines",
    "legend.hintLabel": "Hint",
    "legend.project": "Project",
    "legend.package": "Package",
    "legend.assembly": "Assembly",
    "legend.dll": "DLL",
    "legend.projectDependency": "Project dependency",
    "legend.externalDependency": "External dependency",
    "legend.document": "Document",
    "legend.symbol": "Symbol",
    "legend.documentDependency": "Document dependency",
    "legend.symbolDependency": "Symbol dependency",
    "legend.callDependency": "Call",
    "legend.inheritanceDependency": "Inheritance / implementation",
    "legend.cycleDependency": "Cycle",
    "legend.note": "Solid lines show direct references, dashed lines show dependency types, and thicker lines indicate higher reference counts.",
    "legend.hoverHint": "Hover an arrow to see what the reference means and one concrete example.",
    "group.project": "Project",
    "group.document": "Document",
    "group.symbol": "Symbol",
    "group.package": "Package",
    "group.assembly": "Assembly",
    "group.dll": "DLL",
    "state.yes": "Yes",
    "state.no": "No",
    "state.enabled": "Enabled",
    "state.disabled": "Disabled",
    "tooltip.type": "Type",
    "tooltip.file": "File",
    "tooltip.incoming": "Referenced by",
    "tooltip.outgoing": "References",
    "tooltip.unavailable": "N/A",
    "edge.details": "Reference line",
    "edge.kind": "Line type",
    "edge.description": "Description",
    "edge.from": "From",
    "edge.to": "To",
    "edge.references": "References",
    "edge.confidence": "Confidence",
    "edge.location": "Example location",
    "edge.example": "Example",
    "edge.referenceKind": "Reference kind",
    "edge.kind.contains-document": "Project containment",
    "edge.kind.contains-symbol": "Document containment",
    "edge.kind.project-reference": "Project reference",
    "edge.kind.project-package": "Package reference",
    "edge.kind.project-assembly": "Assembly reference",
    "edge.kind.project-dll": "Runtime DLL reference",
    "edge.kind.document-reference": "Document reference",
    "edge.kind.symbol-reference": "Symbol reference",
    "edge.kind.symbol-call": "Function call",
    "edge.kind.symbol-inheritance": "Inheritance",
    "edge.kind.symbol-implementation": "Implementation",
    "edge.kind.symbol-creation": "Creation",
    "edge.description.contains-document": "This document belongs to the project.",
    "edge.description.contains-symbol": "This symbol belongs to the document.",
    "edge.description.project-reference": "This project references another project.",
    "edge.description.project-package": "This project depends on a package.",
    "edge.description.project-assembly": "This project has an assembly reference.",
    "edge.description.project-dll": "This is a runtime DLL dependency.",
    "edge.description.document-reference": "An aggregated document-level dependency such as include, using, or type usage across files.",
    "edge.description.symbol-reference": "A general symbol-to-symbol reference.",
    "edge.description.symbol-call": "A function or method call.",
    "edge.description.symbol-inheritance": "An inheritance relationship.",
    "edge.description.symbol-implementation": "An implementation relationship.",
    "edge.description.symbol-creation": "An object creation or factory-style creation.",
    "edge.referenceKind.reference": "General reference",
    "edge.referenceKind.call": "Call",
    "edge.referenceKind.creation": "Creation",
    "edge.referenceKind.inheritance": "Inheritance",
    "edge.referenceKind.implementation": "Implementation",
    "edge.referenceKind.parameter": "Parameter",
    "edge.referenceKind.field": "Field",
    "edge.referenceKind.return": "Return",
    "confidence.high": "High",
    "confidence.medium": "Medium",
    "confidence.low": "Low",
    "splitter.ariaLabel": "Resize side panel",
    "splitter.mobileAriaLabel": "Resize graph and detail panel ratio",
    "error.containerNotFound": "Graph container was not found.",
    "error.cytoscapeNotAvailable": "Failed to load Cytoscape.js.",
    "error.dependencyMapNoSelection": "Select a node before building a dependency map."
  }
};

const symbolKindNames: Record<Locale, Record<string, string>> = {
  ja: {
    ClassDeclaration: "クラス",
    StructDeclaration: "構造体",
    UnionDeclaration: "共用体",
    InterfaceDeclaration: "インターフェイス",
    EnumDeclaration: "列挙型",
    RecordDeclaration: "レコード",
    MethodDeclaration: "メソッド",
    FunctionDeclaration: "関数",
    ConstructorDeclaration: "コンストラクター",
    PropertyDeclaration: "プロパティ",
    IndexerDeclaration: "インデクサー",
    FieldDeclaration: "フィールド",
    EventDeclaration: "イベント",
    DelegateDeclaration: "デリゲート",
    TypeAliasDeclaration: "型エイリアス",
    MacroDefinition: "マクロ",
    MacroDefinitions: "マクロ",
    Unknown: "不明"
  },
  en: {
    ClassDeclaration: "Class",
    StructDeclaration: "Struct",
    UnionDeclaration: "Union",
    InterfaceDeclaration: "Interface",
    EnumDeclaration: "Enum",
    RecordDeclaration: "Record",
    MethodDeclaration: "Method",
    FunctionDeclaration: "Function",
    ConstructorDeclaration: "Constructor",
    PropertyDeclaration: "Property",
    IndexerDeclaration: "Indexer",
    FieldDeclaration: "Field",
    EventDeclaration: "Event",
    DelegateDeclaration: "Delegate",
    TypeAliasDeclaration: "Type alias",
    MacroDefinition: "Macro",
    MacroDefinitions: "Macro",
    Unknown: "Unknown"
  }
};

const state = {
  cy: null as any,
  layoutIndex: 0,
  lastPayload: null as GraphPayload | null,
  includeProjects: true,
  includeDocuments: true,
  includePackages: true,
  includeSymbols: false,
  includeAssemblies: true,
  includeNativeDependencies: true,
  includeDocumentDependencies: true,
  includeSymbolDependencies: true,
  symbolKindVisibility: {} as Record<string, boolean>,
  searchQuery: "",
  searchMatches: [] as string[],
  selectedNodeId: null as string | null,
  pendingFocusRequest: null as FocusNodeRequest | null,
  isDependencyMapMode: false,
  isImpactAnalysisMode: false,
  showCyclesOnly: false,
  dependencyMapDirection: "both" as DependencyMapDirection,
  locale: resolveLocale(),
  hostTheme: "system" as HostTheme,
  panelWidth: DEFAULT_PANEL_WIDTH,
  mobilePanelHeight: DEFAULT_MOBILE_PANEL_HEIGHT,
  graphPerformanceMode: "normal" as GraphPerformanceMode,
  hasSearchHighlightClasses: false,
  pinnedNodes: [] as PinnedNodeViewState[],
  hiddenNodes: [] as HiddenNodeViewState[],
  hiddenNodeIds: new Set<string>(),
  focusedModePositions: null as Map<string, LocalNodePosition> | null
};

const inspectorEl = document.getElementById("nodeInspector");
const layoutButton = document.getElementById("layoutButton");
const fitButton = document.getElementById("fitButton");
const dependencyMapModeToggle = document.getElementById("dependencyMapModeToggle") as HTMLInputElement | null;
const impactAnalysisModeToggle = document.getElementById("impactAnalysisModeToggle") as HTMLInputElement | null;
const showCyclesOnlyToggle = document.getElementById("showCyclesOnlyToggle") as HTMLInputElement | null;
const dependencyMapDirectionSelect = document.getElementById("dependencyMapDirectionSelect") as HTMLSelectElement | null;
const showProjectsToggle = document.getElementById("showProjectsToggle") as HTMLInputElement | null;
const showDocumentsToggle = document.getElementById("showDocumentsToggle") as HTMLInputElement | null;
const showPackagesToggle = document.getElementById("showPackagesToggle") as HTMLInputElement | null;
const showSymbolsToggle = document.getElementById("showSymbolsToggle") as HTMLInputElement | null;
const showAssembliesToggle = document.getElementById("showAssembliesToggle") as HTMLInputElement | null;
const showNativeDependenciesToggle = document.getElementById("showNativeDependenciesToggle") as HTMLInputElement | null;
const showDocumentDependenciesToggle = document.getElementById("showDocumentDependenciesToggle") as HTMLInputElement | null;
const showSymbolDependenciesToggle = document.getElementById("showSymbolDependenciesToggle") as HTMLInputElement | null;
const searchInput = document.getElementById("searchInput") as HTMLInputElement | null;
const searchResultText = document.getElementById("searchResultText");
const focusSearchButton = document.getElementById("focusSearchButton");
const clearSearchButton = document.getElementById("clearSearchButton");
const pinSelectedButton = document.getElementById("pinSelectedButton");
const unpinAllButton = document.getElementById("unpinAllButton");
const hideSelectedButton = document.getElementById("hideSelectedButton");
const exportViewButton = document.getElementById("exportViewButton");
const importViewButton = document.getElementById("importViewButton");
const copyShareButton = document.getElementById("copyShareButton");
const importViewInput = document.getElementById("importViewInput") as HTMLInputElement | null;
const symbolTypeFiltersEl = document.getElementById("symbolTypeFilters");
const graphContainer = document.getElementById("graph");
const nodeHoverCardEl = document.getElementById("nodeHoverCard");
const edgeHoverCardEl = document.getElementById("edgeHoverCard");
const workspaceEl = document.getElementById("workspace");
const panelSplitterEl = document.getElementById("panelSplitter") as HTMLDivElement | null;
const mobilePanelSplitterEl = document.getElementById("mobilePanelSplitter") as HTMLDivElement | null;
let activePointerId: number | null = null;
let mobileActivePointerId: number | null = null;
let liveResizePreviewFrame: number | null = null;
let pendingSearchHighlightFrame: number | null = null;
let lastViewportWidth = 0;
let lastViewportHeight = 0;
const impactNodeCountCache = new Map<string, number>();

interface SplitterPointerState {
  get(): number | null;
  set(pointerId: number | null): void;
}

interface SplitterPointerHandlers {
  move: (event: PointerEvent) => void;
  up: (event: PointerEvent) => void;
  cancel: (event: PointerEvent) => void;
  lost: (event: PointerEvent) => void;
}

const panelSplitterPointerState: SplitterPointerState = {
  get: () => activePointerId,
  set: (pointerId) => {
    activePointerId = pointerId;
  }
};

const mobilePanelSplitterPointerState: SplitterPointerState = {
  get: () => mobileActivePointerId,
  set: (pointerId) => {
    mobileActivePointerId = pointerId;
  }
};

const panelSplitterHandlers: SplitterPointerHandlers = {
  move: onSplitterPointerMove,
  up: onSplitterPointerUp,
  cancel: onSplitterPointerCancel,
  lost: onSplitterPointerLostCapture
};

const mobilePanelSplitterHandlers: SplitterPointerHandlers = {
  move: onMobileSplitterPointerMove,
  up: onMobileSplitterPointerUp,
  cancel: onMobileSplitterPointerCancel,
  lost: onMobileSplitterPointerLostCapture
};

installGlobalErrorForwarding();

function main(): void {
  if ((window as ChromiumWindow).__codeMapGraphInitialized) {
    return;
  }

  (window as ChromiumWindow).__codeMapGraphInitialized = true;
  applyHostTheme("system");
  applyLocalizedText();
  initializePanelWidth();
  initializePanelSplitter();
  initializeMobilePanelHeight();
  initializeMobilePanelSplitter();

  if (!graphContainer || !inspectorEl) {
    postHostMessage({
      type: "graph-error",
      message: t("error.containerNotFound")
    });
    return;
  }

  if (typeof cytoscape !== "function") {
    inspectorEl.textContent = t("error.cytoscapeNotAvailable");
    postHostMessage({
      type: "graph-error",
      message: "cytoscape is not available"
    });
    return;
  }

  state.cy = cytoscape({
    container: graphContainer,
    elements: [],
    minZoom: 0.3,
    maxZoom: 3,
    wheelSensitivity: 0.18,
    motionBlur: false,
    textureOnViewport: false,
    hideLabelsOnViewport: true,
    hideEdgesOnViewport: false,
    pixelRatio: 1,
    renderer: {
      name: "canvas"
    },
    layout: {
      name: "grid",
      fit: true,
      padding: 36
    },
    style: [
      {
        selector: "node",
        style: {
          "label": "data(label)",
          "font-size": 12,
          "font-family": "'Segoe UI Variable Text', 'Segoe UI', sans-serif",
          "min-zoomed-font-size": 0,
          "text-wrap": "ellipsis",
          "text-max-width": 220,
          "text-valign": "center",
          "text-halign": "center",
          "font-weight": 600,
          "color": "#17212e",
          "text-opacity": 1,
          "text-outline-width": 0
        }
      },
      {
        selector: "node[group = 'project']",
        style: {
          "shape": "roundrectangle",
          "background-color": "#76b9ff",
          "width": 132,
          "height": 52,
          "color": "#0f233b",
          "font-weight": 700,
          "font-size": 13,
          "text-max-width": 122,
          "border-width": 2,
          "border-color": "#d6ebff"
        }
      },
      {
        selector: "node[group = 'document']",
        style: {
          "shape": "roundrectangle",
          "background-color": "#6cd4a8",
          "width": 132,
          "height": 44,
          "color": "#123325",
          "font-size": 12,
          "text-max-width": 120,
          "border-width": 2,
          "border-color": "#ccf3e2"
        }
      },
      {
        selector: "node[group = 'package']",
        style: {
          "shape": "roundrectangle",
          "background-color": "#b4a0ff",
          "width": 132,
          "height": 36,
          "font-size": 11,
          "text-max-width": 120,
          "color": "#291f4d",
          "font-weight": 600,
          "border-width": 2,
          "border-color": "#dcd2ff"
        }
      },
      {
        selector: "node[group = 'assembly']",
        style: {
          "shape": "roundrectangle",
          "background-color": "#f28b96",
          "width": 132,
          "height": 36,
          "font-size": 11,
          "text-max-width": 120,
          "color": "#4a1d24",
          "font-weight": 600,
          "border-width": 2,
          "border-color": "#f8cad0"
        }
      },
      {
        selector: "node[group = 'dll']",
        style: {
          "shape": "roundrectangle",
          "background-color": "#ffcf73",
          "width": 140,
          "height": 38,
          "font-size": 11,
          "text-max-width": 128,
          "color": "#4b2f02",
          "font-weight": 700,
          "border-width": 2,
          "border-color": "#ffe4b1"
        }
      },
      {
        selector: "node[group = 'symbol']",
        style: {
          "shape": "roundrectangle",
          "background-color": "#f5b870",
          "width": 132,
          "height": 40,
          "font-size": 11,
          "text-max-width": 120,
          "color": "#3b2509",
          "font-weight": 700,
          "border-width": 2,
          "border-color": "#f8d7aa"
        }
      },
      {
        selector: "edge",
        style: {
          "curve-style": "bezier",
          "opacity": 0.9,
          "line-color": "#6b7d96",
          "width": 2.4,
          "line-cap": "round",
          "target-arrow-shape": "triangle",
          "target-arrow-color": "#6b7d96",
          "arrow-scale": 1.1
        }
      },
      {
        selector: "edge[kind = 'contains-document']",
        style: {
          "line-color": "#7ba9de",
          "target-arrow-color": "#7ba9de",
          "line-style": "solid",
          "opacity": 0.42,
          "width": 1.6
        }
      },
      {
        selector: "edge[kind = 'project-reference']",
        style: {
          "line-color": "#76b9ff",
          "target-arrow-color": "#76b9ff",
          "line-style": "solid",
          "opacity": 0.95,
          "width": 2.8
        }
      },
      {
        selector: "edge[kind = 'project-package']",
        style: {
          "line-color": "#b4a0ff",
          "target-arrow-color": "#b4a0ff",
          "line-style": "dashed",
          "line-dash-pattern": [14, 10],
          "opacity": 0.96,
          "width": 2.6
        }
      },
      {
        selector: "edge[kind = 'project-assembly']",
        style: {
          "line-color": "#f28b96",
          "target-arrow-color": "#f28b96",
          "line-style": "dashed",
          "line-dash-pattern": [6, 9],
          "opacity": 0.96,
          "width": 2.8
        }
      },
      {
        selector: "edge[kind = 'project-dll']",
        style: {
          "line-color": "#ffcf73",
          "target-arrow-color": "#ffcf73",
          "line-style": "dotted",
          "line-dash-pattern": [2, 9],
          "opacity": 0.98,
          "width": "mapData(weight, 1, 20, 2.8, 7.4)"
        }
      },
      {
        selector: "edge[kind = 'contains-symbol']",
        style: {
          "line-color": "#f0c58b",
          "target-arrow-color": "#f0c58b",
          "line-style": "solid",
          "opacity": 0.28,
          "width": 1.4
        }
      },
      {
        selector: "edge[kind = 'document-reference']",
        style: {
          "line-color": "#6cd4a8",
          "target-arrow-color": "#6cd4a8",
          "line-style": "solid",
          "opacity": 0.94,
          "width": "mapData(weight, 1, 20, 2.8, 8)"
        }
      },
      {
        selector: "edge[kind = 'symbol-reference']",
        style: {
          "line-color": "#f5b870",
          "target-arrow-color": "#f5b870",
          "line-style": "dashed",
          "line-dash-pattern": [12, 8],
          "opacity": 0.95,
          "width": "mapData(weight, 1, 20, 2.2, 5.4)"
        }
      },
      {
        selector: "edge[kind = 'symbol-call']",
        style: {
          "line-color": "#ffd166",
          "target-arrow-color": "#ffd166",
          "line-style": "solid",
          "opacity": 0.98,
          "width": "mapData(weight, 1, 20, 3.4, 8.2)"
        }
      },
      {
        selector: "edge[kind = 'symbol-inheritance'], edge[kind = 'symbol-implementation']",
        style: {
          "line-color": "#9fb4ff",
          "target-arrow-color": "#9fb4ff",
          "line-style": "dashed",
          "line-dash-pattern": [18, 10],
          "opacity": 0.96,
          "width": "mapData(weight, 1, 20, 2.8, 6.4)"
        }
      },
      {
        selector: "edge[kind = 'symbol-creation']",
        style: {
          "line-color": "#7fe2c7",
          "target-arrow-color": "#7fe2c7",
          "line-style": "dotted",
          "line-dash-pattern": [4, 8],
          "opacity": 0.96,
          "width": "mapData(weight, 1, 20, 2.4, 5.8)"
        }
      },
      {
        selector: "node[isInCycle = 1]",
        style: {
          "border-width": 4,
          "border-color": "#ff6b6b"
        }
      },
      {
        selector: "edge[isCycleEdge = 1]",
        style: {
          "line-color": "#ff6b6b",
          "target-arrow-color": "#ff6b6b"
        }
      },
      {
        selector: "node.pinned",
        style: {
          "overlay-opacity": 0,
          "border-style": "double",
          "border-width": 5
        }
      },
      {
        selector: "node.search-match",
        style: {
          "border-width": 5,
          "border-color": "#e8f0ff"
        }
      },
      {
        selector: "node.search-dim",
        style: {
          "opacity": 0.2,
          "text-opacity": 0.2
        }
      },
      {
        selector: "edge.search-dim",
        style: {
          "opacity": 0.08
        }
      },
      {
        selector: "edge.performance-large",
        style: {
          "curve-style": "haystack",
          "target-arrow-shape": "none",
          "opacity": 0.62
        }
      },
      {
        selector: "edge.performance-massive",
        style: {
          "curve-style": "haystack",
          "target-arrow-shape": "none",
          "line-style": "solid",
          "line-dash-pattern": [1, 0],
          "width": 1.4,
          "opacity": 0.42,
          "events": "no"
        }
      },
      {
        selector: "node.performance-massive",
        style: {
          "font-size": 10,
          "text-max-width": 96
        }
      },
      {
        selector: ".filtered-out",
        style: {
          "display": "none"
        }
      },
      {
        selector: ".state-hidden",
        style: {
          "display": "none"
        }
      },
      {
        selector: ".focus-visible",
        style: {
          "display": "element"
        }
      },
      {
        selector: ":selected",
        style: {
          "border-width": 5,
          "border-color": "#ffffff",
          "border-opacity": 1
        }
      }
    ]
  });

  state.cy.on("tap", "node", (event: any) => {
    const node = event.target;
    hideNodeHoverCard();
    state.selectedNodeId = node.id();

    if (state.isImpactAnalysisMode) {
      applyImpactAnalysisForSelection();
    }
    else if (state.isDependencyMapMode) {
      applyDependencyMapForSelection();
    }

    updateInspector(node);

    postHostMessage({
      type: "node-selected",
      id: node.id(),
      label: node.data("label"),
      group: node.data("group")
    });
  });

  state.cy.on("tap", (event: any) => {
    if (event.target !== state.cy) {
      return;
    }

    clearSelection(true);
  });

  state.cy.on("mouseover", "node", (event: any) => {
    hideEdgeHoverCard();
    showNodeHoverCard(event.target, event.originalEvent);
  });

  state.cy.on("mousemove", "node", (event: any) => {
    moveNodeHoverCard(event.originalEvent);
  });

  state.cy.on("mouseout", "node", () => {
    hideNodeHoverCard();
  });

  state.cy.on("mouseover", "edge", (event: any) => {
    hideNodeHoverCard();
    showEdgeHoverCard(event.target, event.originalEvent);
  });

  state.cy.on("mousemove", "edge", (event: any) => {
    moveEdgeHoverCard(event.originalEvent);
  });

  state.cy.on("mouseout", "edge", () => {
    hideEdgeHoverCard();
  });

  state.cy.on("drag pan zoom", () => {
    hideNodeHoverCard();
    hideEdgeHoverCard();
  });

  layoutButton?.addEventListener("click", () => {
    cycleLayoutAndApply();
    fitGraphViewport(56);
    ensureReadableZoom(resolveMinimumReadableZoom());
    forceGraphRender();
  });

  fitButton?.addEventListener("click", () => {
    fitGraphViewport(56);
  });

  if (dependencyMapModeToggle) {
    dependencyMapModeToggle.checked = state.isDependencyMapMode;
    dependencyMapModeToggle.addEventListener("change", () => {
      setDependencyMapMode(dependencyMapModeToggle.checked);
      if (state.isDependencyMapMode) {
        applyDependencyMapForSelection();
      }
      else {
        clearDependencyMapClasses();
        forceGraphRender();
      }

      postGraphRendered();
    });
  }

  if (impactAnalysisModeToggle) {
    impactAnalysisModeToggle.checked = state.isImpactAnalysisMode;
    impactAnalysisModeToggle.addEventListener("change", () => {
      setImpactAnalysisMode(impactAnalysisModeToggle.checked);
      if (state.isImpactAnalysisMode) {
        applyImpactAnalysisForSelection();
      }
      else {
        clearDependencyMapClasses();
        forceGraphRender();
      }

      postGraphRendered();
    });
  }

  if (showCyclesOnlyToggle) {
    showCyclesOnlyToggle.checked = state.showCyclesOnly;
    showCyclesOnlyToggle.addEventListener("change", () => {
      state.showCyclesOnly = showCyclesOnlyToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (dependencyMapDirectionSelect) {
    dependencyMapDirectionSelect.value = state.dependencyMapDirection;
    dependencyMapDirectionSelect.addEventListener("change", () => {
      if (isDependencyMapDirection(dependencyMapDirectionSelect.value)) {
        state.dependencyMapDirection = dependencyMapDirectionSelect.value;
      }
      else {
        state.dependencyMapDirection = "both";
        dependencyMapDirectionSelect.value = "both";
      }

      if (state.isDependencyMapMode) {
        applyDependencyMapForSelection();
      }
      else if (state.isImpactAnalysisMode) {
        applyImpactAnalysisForSelection();
      }

      postGraphRendered();
    });
  }

  if (showDocumentsToggle) {
    showDocumentsToggle.checked = state.includeDocuments;
    showDocumentsToggle.addEventListener("change", () => {
      state.includeDocuments = showDocumentsToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showProjectsToggle) {
    showProjectsToggle.checked = state.includeProjects;
    showProjectsToggle.addEventListener("change", () => {
      state.includeProjects = showProjectsToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showPackagesToggle) {
    showPackagesToggle.checked = state.includePackages;
    showPackagesToggle.addEventListener("change", () => {
      state.includePackages = showPackagesToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showSymbolsToggle) {
    showSymbolsToggle.checked = state.includeSymbols;
    showSymbolsToggle.addEventListener("change", () => {
      state.includeSymbols = showSymbolsToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showAssembliesToggle) {
    showAssembliesToggle.checked = state.includeAssemblies;
    showAssembliesToggle.addEventListener("change", () => {
      state.includeAssemblies = showAssembliesToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showNativeDependenciesToggle) {
    showNativeDependenciesToggle.checked = state.includeNativeDependencies;
    showNativeDependenciesToggle.addEventListener("change", () => {
      state.includeNativeDependencies = showNativeDependenciesToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showDocumentDependenciesToggle) {
    showDocumentDependenciesToggle.checked = state.includeDocumentDependencies;
    showDocumentDependenciesToggle.addEventListener("change", () => {
      state.includeDocumentDependencies = showDocumentDependenciesToggle.checked;
      rerenderGraphFromState();
    });
  }

  if (showSymbolDependenciesToggle) {
    showSymbolDependenciesToggle.checked = state.includeSymbolDependencies;
    showSymbolDependenciesToggle.addEventListener("change", () => {
      state.includeSymbolDependencies = showSymbolDependenciesToggle.checked;
      rerenderGraphFromState();
    });
  }

  pinSelectedButton?.addEventListener("click", () => {
    pinSelectedNode();
  });

  unpinAllButton?.addEventListener("click", () => {
    clearPinnedNodes();
  });

  hideSelectedButton?.addEventListener("click", () => {
    hideSelectedNode();
  });

  exportViewButton?.addEventListener("click", () => {
    exportCurrentViewState();
  });

  importViewButton?.addEventListener("click", () => {
    importViewInput?.click();
  });

  importViewInput?.addEventListener("change", () => {
    const selectedFile = importViewInput.files?.[0];
    if (selectedFile) {
      void importViewStateFromFile(selectedFile);
    }

    importViewInput.value = "";
  });

  copyShareButton?.addEventListener("click", () => {
    void copyCurrentViewStateToClipboard();
  });

  setupSearchControls();
  registerHostShortcutBridges();

  const webview = getHostWebView();
  if (webview) {
    webview.addEventListener("message", onHostMessage);
  }

  postHostMessage({
    type: "graph-ui-version",
    version: GRAPH_UI_VERSION,
    locale: state.locale,
    hasDependencyMapModeToggle: !!dependencyMapModeToggle,
    hasDependencyMapDirectionSelect: !!dependencyMapDirectionSelect,
    hasProjectsToggle: !!showProjectsToggle,
    hasDocumentsToggle: !!showDocumentsToggle,
    hasPackagesToggle: !!showPackagesToggle,
    hasSymbolsToggle: !!showSymbolsToggle,
    hasAssembliesToggle: !!showAssembliesToggle,
    hasDocumentDependenciesToggle: !!showDocumentDependenciesToggle,
    hasSymbolDependenciesToggle: !!showSymbolDependenciesToggle,
    hasImpactAnalysisModeToggle: !!impactAnalysisModeToggle,
    hasShowCyclesOnlyToggle: !!showCyclesOnlyToggle,
    hasSymbolTypeFilters: !!symbolTypeFiltersEl,
    hasSearchInput: !!searchInput,
    hostTheme: state.hostTheme,
    location: window.location.href
  });
  postHostMessage({ type: "graph-ready" });
}

function setupSearchControls(): void {
  if (searchInput) {
    searchInput.addEventListener("input", () => {
      state.searchQuery = searchInput.value.trim();
      scheduleApplySearchHighlights();
    });
  }

  focusSearchButton?.addEventListener("click", () => {
    focusSearchMatches();
  });

  clearSearchButton?.addEventListener("click", () => {
    if (searchInput) {
      searchInput.value = "";
    }

    state.searchQuery = "";
    cancelScheduledSearchHighlights();
    applySearchHighlights();
  });

  updateSearchResultText();
}

function registerHostShortcutBridges(): void {
  window.addEventListener("keydown", (event) => {
    if (!event.ctrlKey || event.altKey || event.metaKey || event.shiftKey) {
      return;
    }

    if (event.key.toLowerCase() !== "f") {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    postHostMessage({ type: "focus-explorer-search" });
  });
}

function scheduleApplySearchHighlights(): void {
  cancelScheduledSearchHighlights();
  pendingSearchHighlightFrame = window.setTimeout(() => {
    pendingSearchHighlightFrame = null;
    applySearchHighlights();
  }, SEARCH_INPUT_DEBOUNCE_MS);
}

function cancelScheduledSearchHighlights(): void {
  if (pendingSearchHighlightFrame === null) {
    return;
  }

  window.clearTimeout(pendingSearchHighlightFrame);
  pendingSearchHighlightFrame = null;
}

function onHostMessage(event: MessageEvent): void {
  try {
    const incoming = parseMessage(event.data);
    if (!incoming) {
      postHostMessage({
        type: "graph-error",
        message: "Host message parse returned null"
      });
      return;
    }

    postHostMessage({
      type: "graph-message-received",
      nodeCount: Array.isArray((incoming.data as { nodes?: unknown[] } | undefined)?.nodes)
        ? ((incoming.data as { nodes?: unknown[] }).nodes?.length ?? 0)
        : 0,
      edgeCount: Array.isArray((incoming.data as { edges?: unknown[] } | undefined)?.edges)
        ? ((incoming.data as { edges?: unknown[] }).edges?.length ?? 0)
        : 0
    });

    if (incoming.type === "focus-node") {
      const focusRequest = parseFocusNodeRequest(incoming.data);
      if (!focusRequest) {
        postHostMessage({
          type: "node-focus-failed",
          reason: "invalid-focus-request"
        });
        return;
      }

      focusNodeFromHost(focusRequest);
      return;
    }

    if (incoming.type === "apply-view-state") {
      const viewStateRequest = parseViewStateRequest(incoming.data);
      if (!viewStateRequest) {
        postHostMessage({
          type: "graph-error",
          message: "invalid apply-view-state payload"
        });
        return;
      }

      applyViewStateFromHost(viewStateRequest);
      return;
    }

    if (incoming.type === "set-locale") {
      const locale = parseLocale(incoming.data);
      if (!locale) {
        postHostMessage({
          type: "graph-error",
          message: "invalid set-locale payload"
        });
        return;
      }

      applyLocaleFromHost(locale);
      return;
    }

    if (incoming.type === "set-theme") {
      const theme = parseHostTheme(incoming.data);
      if (!theme) {
        postHostMessage({
          type: "graph-error",
          message: "invalid set-theme payload"
        });
        return;
      }

      applyHostTheme(theme);
      return;
    }

    if (incoming.type === "clear-selection") {
      clearSelection(false);
      return;
    }

    if (incoming.type === "set-search-query") {
      const searchQuery = parseSearchQuery(incoming.data);
      if (searchQuery === null) {
        postHostMessage({
          type: "graph-error",
          message: "invalid set-search-query payload"
        });
        return;
      }

      state.searchQuery = searchQuery;
      if (searchInput) {
        searchInput.value = searchQuery;
      }
      cancelScheduledSearchHighlights();
      applySearchHighlights();
      return;
    }

    if (incoming.type === "render-graph" && incoming.data) {
      renderGraph(incoming.data);
    }
  }
  catch (error) {
    postHostMessage({
      type: "graph-error",
      message: String(error)
    });
  }
}

function renderGraph(payload: GraphPayload): void {
  try {
    if (!state.cy) {
      postHostMessage({
        type: "graph-error",
        message: "state.cy is null"
      });
      return;
    }

    state.lastPayload = payload;
    impactNodeCountCache.clear();
    syncSymbolKindVisibility(payload);
    renderSymbolTypeFilters(payload);
    rebuildGraphFromPayload();
  }
  catch (error) {
    postHostMessage({
      type: "graph-error",
      message: String(error)
    });
  }
}

function rebuildGraphFromPayload(): void {
  if (!state.cy || !state.lastPayload) {
    return;
  }

  const payload = state.lastPayload;
  discardFocusedModePositionSnapshot();
  const pinnedNodeSnapshot = capturePinnedNodeState();
  const elements = buildGraphElements(payload);

  state.cy.startBatch();
  state.cy.elements().remove();
  state.cy.add(elements);
  state.cy.endBatch();

  applyBaseVisibilityClasses();
  applyPreferredLayoutForPayload(payload);
  applyPinnedNodeState(pinnedNodeSnapshot);
  applyGraphVisibilityFromState({ fitViewport: true });
}

function rerenderGraphFromState(fitViewport = true): void {
  applyGraphVisibilityFromState({ fitViewport });
}

function applyGraphVisibilityFromState(options: { fitViewport: boolean }): void {
  if (!state.cy) {
    return;
  }

  applyBaseVisibilityClasses();

  if (options.fitViewport) {
    fitGraphViewport(56);
  }

  if (
    state.selectedNodeId &&
    (state.cy.getElementById(state.selectedNodeId).empty() ||
    state.cy.getElementById(state.selectedNodeId).hasClass("state-hidden")))
  {
    clearSelection(true);
  }

  applySearchHighlights();

  if (state.isImpactAnalysisMode) {
    applyImpactAnalysisForSelection();
  }
  else if (state.isDependencyMapMode) {
    applyDependencyMapForSelection();
  }
  else {
    clearDependencyMapClasses();
  }

  applyGraphPerformanceModeFromCurrentVisibility();
  const visibleNodeCount = state.cy
    .nodes()
    .not(".state-hidden")
    .not(".filtered-out")
    .length;
  ensureReadableZoom(resolveMinimumReadableZoom(visibleNodeCount));

  if (state.selectedNodeId) {
    const selected = state.cy.getElementById(state.selectedNodeId);
    if (!selected.empty()) {
      updateInspector(selected);
    }
    else {
      resetInspector();
    }
  }
  else {
    resetInspector();
  }

  tryApplyPendingFocusRequest();
  forceGraphRender();
  postGraphRendered();
}

function applyBaseVisibilityClasses(): void {
  if (!state.cy) {
    return;
  }

  const visibleNodeIds = new Set<string>();

  state.cy.startBatch();
  state.cy.nodes().forEach((node: any) => {
    const isVisible = shouldNodeBeVisible(node.data());
    node.toggleClass("state-hidden", !isVisible);
    if (isVisible) {
      visibleNodeIds.add(node.id());
    }
  });

  state.cy.edges().forEach((edge: any) => {
    const isVisible = shouldEdgeBeVisible(edge.data(), visibleNodeIds);
    edge.toggleClass("state-hidden", !isVisible);
  });
  state.cy.endBatch();
}

function applyGraphPerformanceModeFromCurrentVisibility(): void {
  if (!state.cy) {
    return;
  }

  const visibleNodeCount = state.cy.nodes().not(".state-hidden").not(".filtered-out").length;
  const visibleEdgeCount = state.cy.edges().not(".state-hidden").not(".filtered-out").length;
  const nextMode = resolveGraphPerformanceMode(visibleNodeCount, visibleEdgeCount);
  if (state.graphPerformanceMode === nextMode) {
    return;
  }

  state.graphPerformanceMode = nextMode;
  const useLargeMode = nextMode !== "normal";
  const useMassiveMode = nextMode === "massive";

  state.cy.startBatch();
  state.cy.edges().toggleClass("performance-large", useLargeMode);
  state.cy.edges().toggleClass("performance-massive", useMassiveMode);
  state.cy.nodes().toggleClass("performance-massive", useMassiveMode);
  state.cy.endBatch();

  if (useMassiveMode) {
    hideEdgeHoverCard();
  }
}

function resolveGraphPerformanceMode(visibleNodeCount: number, visibleEdgeCount: number): GraphPerformanceMode {
  if (
    visibleNodeCount >= MASSIVE_GRAPH_NODE_THRESHOLD ||
    visibleEdgeCount >= MASSIVE_GRAPH_EDGE_THRESHOLD)
  {
    return "massive";
  }

  if (
    visibleNodeCount >= LARGE_GRAPH_NODE_THRESHOLD ||
    visibleEdgeCount >= LARGE_GRAPH_EDGE_THRESHOLD)
  {
    return "large";
  }

  return "normal";
}

function focusNodeFromHost(request: FocusNodeRequest): void {
  if (!state.cy) {
    state.pendingFocusRequest = request;
    return;
  }

  if (request.forceVisible && ensureNodeVisibilityForFocus(request.nodeId, request.label)) {
    state.pendingFocusRequest = request;
    rerenderGraphFromState();
    return;
  }

  const node = resolveNodeForFocus(request);
  if (node.empty()) {
    state.pendingFocusRequest = request;
    postHostMessage({
      type: "node-focus-failed",
      nodeId: request.nodeId,
      reason: "node-not-found"
    });
    return;
  }

  state.pendingFocusRequest = null;
  state.cy.elements().unselect();
  node.select();
  state.selectedNodeId = node.id();
  setDependencyMapMode(request.enableDependencyMap);

  if (state.isImpactAnalysisMode) {
    applyImpactAnalysisForSelection();
  }
  else if (state.isDependencyMapMode) {
    applyDependencyMapForSelection();
  }
  else {
    clearDependencyMapClasses();
    fitGraphViewportToCollection(node, 72);
    forceGraphRender();
  }

  updateInspector(node);
  postGraphRendered();

  postHostMessage({
    type: "node-focused",
    nodeId: node.id(),
    label: String(node.data("label") ?? ""),
    group: String(node.data("group") ?? "")
  });
}

function clearSelection(notifyHost: boolean): void {
  hideNodeHoverCard();
  state.selectedNodeId = null;
  state.cy?.elements().unselect();
  if (state.isDependencyMapMode || state.isImpactAnalysisMode) {
    clearDependencyMapClasses();
    forceGraphRender();
  }

  resetInspector();
  if (notifyHost) {
    postHostMessage({ type: "selection-cleared" });
  }
}

function hideSelectedNode(): void {
  if (!state.cy || !state.selectedNodeId) {
    return;
  }

  const selectedNode = state.cy.getElementById(state.selectedNodeId);
  if (selectedNode.empty()) {
    return;
  }

  setHiddenNodes(normalizeHiddenNodes([
    ...state.hiddenNodes,
    {
      nodeId: selectedNode.id(),
      label: String(selectedNode.data("label") ?? selectedNode.id()),
      group: String(selectedNode.data("group") ?? "")
    }
  ]));

  clearSelection(true);
  rerenderGraphFromState();
  notifyHiddenNodesChanged();
}

function notifyHiddenNodesChanged(): void {
  postHostMessage({
    type: "hidden-nodes-changed",
    hiddenNodes: state.hiddenNodes
  });
}

function applyLocaleFromHost(locale: Locale): void {
  if (state.locale === locale) {
    return;
  }

  state.locale = locale;
  applyLocalizedText();
  if (state.lastPayload) {
    renderSymbolTypeFilters(state.lastPayload);
  }

  if (state.selectedNodeId && state.cy) {
    const selectedNode = state.cy.getElementById(state.selectedNodeId);
    if (!selectedNode.empty()) {
      updateInspector(selectedNode);
    }
    else {
      resetInspector();
    }
  }
  else {
    resetInspector();
  }

  hideNodeHoverCard();
  hideEdgeHoverCard();
  updateSearchResultText(state.searchMatches.length);
}

function tryApplyPendingFocusRequest(): void {
  const pendingRequest = state.pendingFocusRequest;
  if (!pendingRequest) {
    return;
  }

  state.pendingFocusRequest = null;
  focusNodeFromHost(pendingRequest);
}

function resolveNodeForFocus(request: FocusNodeRequest): any {
  if (!state.cy) {
    return null;
  }

  const directMatch = state.cy.getElementById(request.nodeId);
  if (!directMatch.empty()) {
    return directMatch;
  }

  if (!request.label) {
    return directMatch;
  }

  const exactMatches = state.cy.nodes().filter((node: any) => String(node.data("label") ?? "") === request.label);
  if (exactMatches.length > 0) {
    return exactMatches[0];
  }

  const prefixMatches = state.cy
    .nodes()
    .filter((node: any) => String(node.data("label") ?? "").startsWith(`${request.label}:`));
  if (prefixMatches.length > 0) {
    return prefixMatches[0];
  }

  return directMatch;
}

function ensureNodeVisibilityForFocus(nodeId: string, label?: string): boolean {
  const node = resolveNodePayloadForFocus(nodeId, label);
  if (!node) {
    return false;
  }

  let changed = false;
  if (state.hiddenNodeIds.has(node.id)) {
    setHiddenNodes(state.hiddenNodes.filter((hiddenNode) => hiddenNode.nodeId !== node.id));
    notifyHiddenNodesChanged();
    changed = true;
  }

  switch (node.group) {
    case "project":
      changed = setToggleState(showProjectsToggle, "includeProjects", true) || changed;
      break;
    case "document":
      changed = setToggleState(showDocumentsToggle, "includeDocuments", true) || changed;
      break;
    case "package":
      changed = setToggleState(showPackagesToggle, "includePackages", true) || changed;
      break;
    case "assembly":
      changed = setToggleState(showAssembliesToggle, "includeAssemblies", true) || changed;
      break;
    case "dll":
      changed = setToggleState(showNativeDependenciesToggle, "includeNativeDependencies", true) || changed;
      break;
    case "symbol":
      changed = setToggleState(showSymbolsToggle, "includeSymbols", true) || changed;
      {
        const symbolKind = node.symbolKind ?? "Unknown";
        if (state.symbolKindVisibility[symbolKind] === false) {
          state.symbolKindVisibility[symbolKind] = true;
          changed = true;
        }
      }
      break;
  }

  if (state.showCyclesOnly && !node.isInCycle) {
    state.showCyclesOnly = false;
    if (showCyclesOnlyToggle) {
      showCyclesOnlyToggle.checked = false;
    }

    changed = true;
  }

  changed = setToggleState(showDocumentDependenciesToggle, "includeDocumentDependencies", true) || changed;
  changed = setToggleState(showSymbolDependenciesToggle, "includeSymbolDependencies", true) || changed;

  return changed;
}

function resolveNodePayloadForFocus(nodeId: string, label?: string): GraphNodePayload | undefined {
  const payloadNodes = state.lastPayload?.nodes;
  if (!payloadNodes || payloadNodes.length === 0) {
    return undefined;
  }

  const byId = payloadNodes.find((candidate) => candidate.id === nodeId);
  if (byId) {
    return byId;
  }

  if (!label) {
    return undefined;
  }

  const byExactLabel = payloadNodes.find((candidate) => candidate.label === label);
  if (byExactLabel) {
    return byExactLabel;
  }

  return payloadNodes.find((candidate) => candidate.label.startsWith(`${label}:`));
}

function setToggleState(
  toggle: HTMLInputElement | null,
  stateKey:
    | "includeProjects"
    | "includeDocuments"
    | "includePackages"
    | "includeSymbols"
    | "includeAssemblies"
    | "includeNativeDependencies"
    | "includeDocumentDependencies"
    | "includeSymbolDependencies",
  value: boolean): boolean
{
  if (state[stateKey] === value) {
    return false;
  }

  state[stateKey] = value;
  if (toggle) {
    toggle.checked = value;
  }

  return true;
}

function applyViewStateFromHost(viewState: ViewStateRequest): void {
  let shouldRerender = false;
  let shouldResizeViewport = false;

  shouldRerender = applyToggleFromViewState(viewState.includeProjects, showProjectsToggle, "includeProjects") || shouldRerender;
  shouldRerender = applyToggleFromViewState(viewState.includeDocuments, showDocumentsToggle, "includeDocuments") || shouldRerender;
  shouldRerender = applyToggleFromViewState(viewState.includePackages, showPackagesToggle, "includePackages") || shouldRerender;
  shouldRerender = applyToggleFromViewState(viewState.includeSymbols, showSymbolsToggle, "includeSymbols") || shouldRerender;
  shouldRerender = applyToggleFromViewState(viewState.includeAssemblies, showAssembliesToggle, "includeAssemblies") || shouldRerender;
  shouldRerender = applyToggleFromViewState(
    viewState.includeNativeDependencies,
    showNativeDependenciesToggle,
    "includeNativeDependencies") || shouldRerender;
  shouldRerender = applyToggleFromViewState(
    viewState.includeDocumentDependencies,
    showDocumentDependenciesToggle,
    "includeDocumentDependencies") || shouldRerender;
  shouldRerender = applyToggleFromViewState(
    viewState.includeSymbolDependencies,
    showSymbolDependenciesToggle,
    "includeSymbolDependencies") || shouldRerender;

  if (typeof viewState.isDependencyMapMode === "boolean" && state.isDependencyMapMode !== viewState.isDependencyMapMode) {
    setDependencyMapMode(viewState.isDependencyMapMode);
    shouldRerender = true;
  }

  if (typeof viewState.isImpactAnalysisMode === "boolean" && state.isImpactAnalysisMode !== viewState.isImpactAnalysisMode) {
    setImpactAnalysisMode(viewState.isImpactAnalysisMode);
    shouldRerender = true;
  }

  if (typeof viewState.showCyclesOnly === "boolean" && state.showCyclesOnly !== viewState.showCyclesOnly) {
    state.showCyclesOnly = viewState.showCyclesOnly;
    if (showCyclesOnlyToggle) {
      showCyclesOnlyToggle.checked = state.showCyclesOnly;
    }

    shouldRerender = true;
  }

  if (
    typeof viewState.dependencyMapDirection === "string" &&
    isDependencyMapDirection(viewState.dependencyMapDirection) &&
    state.dependencyMapDirection !== viewState.dependencyMapDirection)
  {
    state.dependencyMapDirection = viewState.dependencyMapDirection;
    if (dependencyMapDirectionSelect) {
      dependencyMapDirectionSelect.value = viewState.dependencyMapDirection;
    }

    shouldRerender = true;
  }

  if (typeof viewState.panelWidth === "number" && Number.isFinite(viewState.panelWidth)) {
    const previousWidth = state.panelWidth;
    setPanelWidth(viewState.panelWidth);
    if (state.panelWidth !== previousWidth) {
      persistPanelWidth();
      shouldResizeViewport = true;
    }
  }

  if (typeof viewState.mobilePanelHeight === "number" && Number.isFinite(viewState.mobilePanelHeight)) {
    const previousHeight = state.mobilePanelHeight;
    setMobilePanelHeight(viewState.mobilePanelHeight);
    if (state.mobilePanelHeight !== previousHeight) {
      persistMobilePanelHeight();
      shouldResizeViewport = true;
    }
  }

  if (Array.isArray(viewState.pinnedNodes)) {
    state.pinnedNodes = normalizePinnedNodes(viewState.pinnedNodes);
    shouldRerender = true;
  }

  if (Array.isArray(viewState.hiddenNodes)) {
    setHiddenNodes(normalizeHiddenNodes(viewState.hiddenNodes));
    shouldRerender = true;
  }

  if (shouldRerender && state.lastPayload) {
    rerenderGraphFromState();
    return;
  }

  if (shouldResizeViewport && state.cy) {
    resizeGraphViewportPreservingTransform();
    postGraphRendered();
  }
}

function applyToggleFromViewState(
  value: boolean | undefined,
  toggle: HTMLInputElement | null,
  stateKey:
    | "includeProjects"
    | "includeDocuments"
    | "includePackages"
    | "includeSymbols"
    | "includeAssemblies"
    | "includeNativeDependencies"
    | "includeDocumentDependencies"
    | "includeSymbolDependencies"): boolean
{
  if (typeof value !== "boolean") {
    return false;
  }

  return setToggleState(toggle, stateKey, value);
}

function buildGraphElements(payload: GraphPayload): any[]
{
  const elements: any[] = [];

  for (const node of payload.nodes) {
    elements.push({
      data: {
        id: node.id,
        label: node.label,
        group: node.group,
        symbolKind: node.symbolKind ?? null,
        fileName: node.fileName ?? null,
        isInCycle: node.isInCycle === true ? 1 : 0
      }
    });
  }

  for (const edge of payload.edges) {
    elements.push({
      data: {
        id: edge.id,
        source: edge.source,
        target: edge.target,
        kind: edge.kind,
        weight: edge.weight,
        referenceKind: edge.referenceKind ?? null,
        confidence: edge.confidence ?? null,
        sampleFilePath: edge.sampleFilePath ?? null,
        sampleLineNumber: edge.sampleLineNumber ?? null,
        sampleSnippet: edge.sampleSnippet ?? null,
        isCycleEdge: edge.isCycleEdge === true ? 1 : 0
      }
    });
  }

  return elements;
}

function shouldNodeBeVisible(nodeData: { id?: string; group?: string; symbolKind?: string | null; isInCycle?: number | boolean }): boolean {
  const nodeId = String(nodeData.id ?? "");
  if (nodeId.length > 0 && state.hiddenNodeIds.has(nodeId)) {
    return false;
  }

  const group = String(nodeData.group ?? "");
  if (group === "project" && !state.includeProjects) {
    return false;
  }

  if (group === "document" && !state.includeDocuments) {
    return false;
  }

  if (group === "package" && !state.includePackages) {
    return false;
  }

  if (group === "assembly" && !state.includeAssemblies) {
    return false;
  }

  if (group === "dll" && !state.includeNativeDependencies) {
    return false;
  }

  if (group === "symbol") {
    if (!state.includeSymbols) {
      return false;
    }

    const symbolKind = nodeData.symbolKind ?? "Unknown";
    if (state.symbolKindVisibility[symbolKind] === false) {
      return false;
    }
  }

  if (state.showCyclesOnly && !Boolean(nodeData.isInCycle)) {
    return false;
  }

  return true;
}

function shouldEdgeBeVisible(
  edgeData: { kind?: string; source?: string; target?: string; isCycleEdge?: number | boolean },
  visibleNodeIds: ReadonlySet<string>): boolean
{
  const kind = String(edgeData.kind ?? "");

  if (kind === "contains-symbol" && !state.includeSymbols) {
    return false;
  }

  if (kind === "document-reference" && !state.includeDocumentDependencies) {
    return false;
  }

  if (
    (kind === "symbol-reference" ||
    kind === "symbol-call" ||
    kind === "symbol-inheritance" ||
    kind === "symbol-implementation" ||
    kind === "symbol-creation") &&
    (!state.includeSymbols || !state.includeSymbolDependencies))
  {
    return false;
  }

  if (kind === "project-assembly" && !state.includeAssemblies) {
    return false;
  }

  if (kind === "project-dll" && !state.includeNativeDependencies) {
    return false;
  }

  if (state.showCyclesOnly && !Boolean(edgeData.isCycleEdge)) {
    return false;
  }

  const source = String(edgeData.source ?? "");
  const target = String(edgeData.target ?? "");
  return visibleNodeIds.has(source) && visibleNodeIds.has(target);
}

function syncSymbolKindVisibility(payload: GraphPayload): void {
  const nextVisibility: Record<string, boolean> = {};
  const kinds = collectSortedSymbolKinds(payload);

  for (const kind of kinds) {
    if (kind in nextVisibility) {
      continue;
    }

    nextVisibility[kind] = state.symbolKindVisibility[kind] ?? true;
  }

  state.symbolKindVisibility = nextVisibility;
}

function renderSymbolTypeFilters(payload: GraphPayload): void {
  if (!symbolTypeFiltersEl) {
    return;
  }

  symbolTypeFiltersEl.innerHTML = "";

  const kinds = collectSortedSymbolKinds(payload);

  if (kinds.length === 0) {
    const empty = document.createElement("div");
    empty.className = "search-result";
    empty.textContent = t("symbolTypes.empty");
    symbolTypeFiltersEl.appendChild(empty);
    return;
  }

  for (const kind of kinds) {
    const row = document.createElement("label");
    row.className = "symbol-type-row";

    const input = document.createElement("input");
    input.type = "checkbox";
    input.checked = state.symbolKindVisibility[kind] !== false;
    input.addEventListener("change", () => {
      state.symbolKindVisibility[kind] = input.checked;
      rerenderGraphFromState();
    });

    const text = document.createElement("span");
    text.textContent = resolveSymbolKindLabel(kind);

    row.appendChild(input);
    row.appendChild(text);
    symbolTypeFiltersEl.appendChild(row);
  }
}

function resolveSymbolKindLabel(kind: string): string {
  const localized = symbolKindNames[state.locale]?.[kind];
  if (localized) {
    return localized;
  }

  if (kind.trim().length === 0) {
    return symbolKindNames[state.locale]?.Unknown ?? kind;
  }

  return kind.replace(/Declaration$/u, "");
}

function collectSortedSymbolKinds(payload: GraphPayload): string[] {
  return [...new Set(
    payload.nodes
      .filter((node) => node.group === "symbol")
      .map((node) => node.symbolKind ?? "Unknown")
  )].sort((left, right) => left.localeCompare(right));
}

function applySearchHighlights(): void {
  if (!state.cy) {
    return;
  }

  const query = state.searchQuery.trim().toLocaleLowerCase();
  if (query.length === 0) {
    if (state.hasSearchHighlightClasses) {
      state.cy.nodes().removeClass("search-match");
      state.cy.nodes().removeClass("search-dim");
      state.cy.edges().removeClass("search-dim");
      state.hasSearchHighlightClasses = false;
    }

    state.searchMatches = [];
    updateSearchResultText();
    return;
  }

  state.cy.nodes().removeClass("search-match");
  state.cy.nodes().removeClass("search-dim");
  state.cy.edges().removeClass("search-dim");
  state.searchMatches = [];
  state.hasSearchHighlightClasses = false;

  const allTargets = state.cy
    .nodes()
    .not(".state-hidden")
    .filter((node: any) => isSearchTargetGroup(node.data("group")));
  const matches = allTargets.filter((node: any) => {
    const label = String(node.data("label") ?? "").toLocaleLowerCase();
    return label.includes(query);
  });

  if (matches.length === 0) {
    updateSearchResultText(0);
    return;
  }

  // In focused map modes, keep labels readable and avoid dimming neighbor nodes.
  const shouldDimNonMatches = !state.isDependencyMapMode && !state.isImpactAnalysisMode;
  if (shouldDimNonMatches) {
    allTargets.not(matches).addClass("search-dim");
  }

  matches.addClass("search-match");

  if (shouldDimNonMatches) {
    const connectedEdges = matches.connectedEdges();
    state.cy.edges().not(".state-hidden").not(connectedEdges).addClass("search-dim");
  }

  state.hasSearchHighlightClasses = true;
  state.searchMatches = matches.map((node: any) => node.id());
  updateSearchResultText(matches.length);
}

function focusSearchMatches(): void {
  if (!state.cy || state.searchMatches.length === 0) {
    return;
  }

  let focusCollection = state.cy.collection();

  for (const nodeId of state.searchMatches) {
    const node = state.cy.getElementById(nodeId);
    if (!node.empty()) {
      focusCollection = focusCollection.union(node);
    }
  }

  if (focusCollection.length === 0) {
    return;
  }

  fitGraphViewportToCollection(focusCollection, 64);

  const firstNode = focusCollection[0];
  if (!firstNode) {
    return;
  }

  state.cy.elements().unselect();
  firstNode.select();
  state.selectedNodeId = firstNode.id();
  updateInspector(firstNode);
}

function updateSearchResultText(matchCount?: number): void {
  if (!searchResultText) {
    return;
  }

  const query = state.searchQuery.trim();
  if (query.length === 0) {
    searchResultText.textContent = t("search.empty");
    return;
  }

  if (typeof matchCount === "number" && matchCount > 0) {
    searchResultText.textContent = formatTemplate(t("search.count"), String(matchCount));
    return;
  }

  searchResultText.textContent = t("search.none");
}

function applyDependencyMapForSelection(): void {
  if (!state.cy) {
    return;
  }

  captureFocusedModePositionSnapshotIfNeeded();
  clearDependencyMapClasses(false);

  const selectedNodeId = state.selectedNodeId;
  if (!selectedNodeId) {
    return;
  }

  const selectedNode = state.cy.getElementById(selectedNodeId);
  if (selectedNode.empty()) {
    return;
  }

  const selectionSeedNodes = getSelectionSeedNodes(selectedNode);
  const dependencySeedNodes = collectDependencyMapSeedNodes(
    selectedNode,
    selectionSeedNodes,
    state.dependencyMapDirection);
  const dependencyEdges = getDependencyEdgesForSeedNodes(
    dependencySeedNodes,
    state.dependencyMapDirection);
  const neighborhood = buildSelectionNeighborhood(selectedNode, dependencySeedNodes, dependencyEdges);

  state.cy.elements().addClass("filtered-out");
  revealFocusedNeighborhood(neighborhood);
  neighborhood.removeClass("filtered-out");
  applyGraphPerformanceModeFromCurrentVisibility();

  applyDependencyMapLayout(selectedNode, dependencySeedNodes, dependencyEdges, neighborhood);
  fitGraphViewportToCollection(neighborhood, 88);
  forceGraphRender();
}

function setDependencyMapMode(nextValue: boolean): void {
  state.isDependencyMapMode = nextValue;
  if (nextValue) {
    captureFocusedModePositionSnapshotIfNeeded();
    state.isImpactAnalysisMode = false;
    if (impactAnalysisModeToggle) {
      impactAnalysisModeToggle.checked = false;
    }
  }
  else if (!state.isImpactAnalysisMode) {
    restoreFocusedModePositionSnapshotIfNeeded();
  }

  if (dependencyMapModeToggle && dependencyMapModeToggle.checked !== nextValue) {
    dependencyMapModeToggle.checked = nextValue;
  }

  applySearchHighlights();
}

function setImpactAnalysisMode(nextValue: boolean): void {
  state.isImpactAnalysisMode = nextValue;
  if (nextValue) {
    captureFocusedModePositionSnapshotIfNeeded();
    state.isDependencyMapMode = false;
    if (dependencyMapModeToggle) {
      dependencyMapModeToggle.checked = false;
    }
  }
  else if (!state.isDependencyMapMode) {
    restoreFocusedModePositionSnapshotIfNeeded();
  }

  if (impactAnalysisModeToggle && impactAnalysisModeToggle.checked !== nextValue) {
    impactAnalysisModeToggle.checked = nextValue;
  }

  applySearchHighlights();
}

function applyImpactAnalysisForSelection(): void {
  if (!state.cy) {
    return;
  }

  captureFocusedModePositionSnapshotIfNeeded();
  clearDependencyMapClasses(false);

  const selectedNodeId = state.selectedNodeId;
  if (!selectedNodeId) {
    return;
  }

  const selectedNode = state.cy.getElementById(selectedNodeId);
  if (selectedNode.empty()) {
    return;
  }

  const selectionSeedNodes = getSelectionSeedNodes(selectedNode);
  const neighborhood = collectImpactNeighborhood(
    selectedNode,
    selectionSeedNodes,
    state.dependencyMapDirection,
    {
      includeStateHidden: true,
      includeFilteredOut: true
    });
  state.cy.elements().addClass("filtered-out");
  revealFocusedNeighborhood(neighborhood);
  neighborhood.removeClass("filtered-out");
  applyGraphPerformanceModeFromCurrentVisibility();

  fitGraphViewportToCollection(neighborhood, 72);
  forceGraphRender();
}

function collectImpactNeighborhood(
  selectedNode: any,
  selectionSeedNodes: any,
  direction: DependencyMapDirection,
  edgeQueryOptions: DependencyEdgeQueryOptions = {}): any
{
  if (!state.cy) {
    return selectedNode;
  }

  const visitedNodes = new Set<string>();
  const visitedEdges = state.cy.collection();
  const queue: any[] = [];
  let queueIndex = 0;

  selectionSeedNodes.forEach((seedNode: any) => {
    const seedId = seedNode.id();
    if (visitedNodes.has(seedId)) {
      return;
    }

    visitedNodes.add(seedId);
    queue.push(seedNode);
  });

  while (queueIndex < queue.length) {
    const current = queue[queueIndex++];
    const edges = getDependencyEdgesByDirection(current, direction, edgeQueryOptions);
    edges.forEach((edge: any) => {
      visitedEdges.merge(edge);
      const endpoints = edge.connectedNodes();
      endpoints.forEach((candidate: any) => {
        const candidateId = candidate.id();
        if (visitedNodes.has(candidateId)) {
          return;
        }

        visitedNodes.add(candidateId);
        queue.push(candidate);
      });
    });
  }

  const impactedNodes = state.cy.collection();
  for (const nodeId of visitedNodes) {
    impactedNodes.merge(state.cy.getElementById(nodeId));
  }

  return buildSelectionNeighborhood(selectedNode, impactedNodes, visitedEdges);
}

function getImpactNodeCount(selectedNode: any, direction: DependencyMapDirection): number {
  if (!state.cy) {
    return 0;
  }

  const cacheKey = `${direction}:${selectedNode.id()}`;
  const cached = impactNodeCountCache.get(cacheKey);
  if (typeof cached === "number") {
    return cached;
  }

  const selectionSeedNodes = getSelectionSeedNodes(selectedNode);
  const visitedNodes = new Set<string>();
  const queue: any[] = [];
  let queueIndex = 0;

  selectionSeedNodes.forEach((seedNode: any) => {
    const seedId = seedNode.id();
    if (visitedNodes.has(seedId)) {
      return;
    }

    visitedNodes.add(seedId);
    queue.push(seedNode);
  });

  while (queueIndex < queue.length) {
    const current = queue[queueIndex++];
    const edges = getDependencyEdgesByDirection(current, direction);
    edges.forEach((edge: any) => {
      edge.connectedNodes().forEach((candidate: any) => {
        const candidateId = candidate.id();
        if (visitedNodes.has(candidateId)) {
          return;
        }

        visitedNodes.add(candidateId);
        queue.push(candidate);
      });
    });
  }

  const impactNodeCount = Math.max(0, visitedNodes.size - selectionSeedNodes.length);
  impactNodeCountCache.set(cacheKey, impactNodeCount);
  return impactNodeCount;
}

function getDependencyEdgesByDirection(
  selectedNode: any,
  direction: DependencyMapDirection,
  options: DependencyEdgeQueryOptions = {}): any
{
  const incoming = filterDependencyEdges(selectedNode.incomers("edge"), options);
  const outgoing = filterDependencyEdges(selectedNode.outgoers("edge"), options);

  if (direction === "incoming") {
    return incoming;
  }

  if (direction === "outgoing") {
    return outgoing;
  }

  return incoming.union(outgoing);
}

type DependencyNeighborDirection = "incoming" | "outgoing" | "both";

interface DependencyNeighborPlacement {
  node: any;
  direction: DependencyNeighborDirection;
  primaryKind: string;
  totalWeight: number;
}

interface DependencyPlacementCluster {
  key: string;
  laneIndex: number;
  primaryKind: string;
  totalWeight: number;
  documentNode: any | null;
  projectNode: any | null;
  peerNodes: any[];
}

interface DependencyPlacementClusterMetrics {
  width: number;
  height: number;
  peerColumnCount: number;
  peerRowCount: number;
}

interface DependencyCenterLayoutResult {
  reservedNodeIds: Set<string>;
}

interface DependencyEdgeQueryOptions {
  includeStateHidden?: boolean;
  includeFilteredOut?: boolean;
}

function filterDependencyEdges(edges: any, options: DependencyEdgeQueryOptions): any {
  return edges.filter((edge: any) => (
    isDependencyEdgeKind(String(edge.data("kind") ?? "")) &&
    isDependencyEdgeQueryable(edge, options)
  ));
}

function isDependencyEdgeQueryable(edge: any, options: DependencyEdgeQueryOptions): boolean {
  if (!options.includeStateHidden && edge.hasClass("state-hidden") && !edge.hasClass("focus-visible")) {
    return false;
  }

  if (!options.includeFilteredOut && edge.hasClass("filtered-out")) {
    return false;
  }

  return true;
}

function clearDependencyMapClasses(updatePerformanceMode = true): void {
  if (!state.cy) {
    return;
  }

  state.cy.elements().removeClass("focus-visible");
  state.cy.elements().removeClass("filtered-out");
  if (updatePerformanceMode) {
    applyGraphPerformanceModeFromCurrentVisibility();
  }
}

function captureFocusedModePositionSnapshotIfNeeded(): void {
  if (!state.cy || state.focusedModePositions) {
    return;
  }

  const positions = new Map<string, LocalNodePosition>();
  state.cy.nodes().not(".state-hidden").forEach((node: any) => {
    const position = node.position();
    positions.set(String(node.id()), {
      x: Number(position.x ?? 0),
      y: Number(position.y ?? 0)
    });
  });

  state.focusedModePositions = positions;
}

function restoreFocusedModePositionSnapshotIfNeeded(): void {
  if (!state.cy || !state.focusedModePositions) {
    return;
  }

  const snapshot = state.focusedModePositions;
  state.focusedModePositions = null;
  if (snapshot.size === 0) {
    return;
  }

  state.cy.startBatch();
  for (const [nodeId, position] of snapshot.entries()) {
    const node = state.cy.getElementById(nodeId);
    if (node.empty()) {
      continue;
    }

    positionNodePreservingLock(node, position.x, position.y);
  }

  state.cy.endBatch();
  forceGraphRender();
}

function discardFocusedModePositionSnapshot(): void {
  state.focusedModePositions = null;
}

function revealFocusedNeighborhood(neighborhood: any): void {
  if (!state.cy || !neighborhood) {
    return;
  }

  neighborhood.addClass("focus-visible");
}

function pinSelectedNode(): void {
  if (!state.cy || !state.selectedNodeId) {
    return;
  }

  const node = state.cy.getElementById(state.selectedNodeId);
  if (node.empty()) {
    return;
  }

  node.lock();
  node.addClass("pinned");
  state.pinnedNodes = upsertPinnedNodeState(state.pinnedNodes, {
    nodeId: node.id(),
    x: Number(node.position("x") ?? 0),
    y: Number(node.position("y") ?? 0)
  });
  updateInspector(node);
  postGraphRendered();
}

function clearPinnedNodes(): void {
  if (!state.cy) {
    state.pinnedNodes = [];
    return;
  }

  state.cy.nodes(".pinned").forEach((node: any) => {
    node.removeClass("pinned");
    node.unlock();
  });

  state.pinnedNodes = [];
  if (state.selectedNodeId) {
    const selected = state.cy.getElementById(state.selectedNodeId);
    if (!selected.empty()) {
      updateInspector(selected);
    }
  }

  postGraphRendered();
}

function capturePinnedNodeState(): PinnedNodeViewState[] {
  if (!state.cy) {
    return normalizePinnedNodes(state.pinnedNodes);
  }

  const pinnedNodes: PinnedNodeViewState[] = [];
  state.cy.nodes(".pinned").forEach((node: any) => {
    pinnedNodes.push({
      nodeId: node.id(),
      x: Number(node.position("x") ?? 0),
      y: Number(node.position("y") ?? 0)
    });
  });

  state.pinnedNodes = normalizePinnedNodes(pinnedNodes);
  return state.pinnedNodes;
}

function applyPinnedNodeState(pinnedNodes: PinnedNodeViewState[]): void {
  if (!state.cy) {
    state.pinnedNodes = normalizePinnedNodes(pinnedNodes);
    return;
  }

  const normalized = normalizePinnedNodes(pinnedNodes);
  state.pinnedNodes = normalized;
  state.cy.nodes().removeClass("pinned").unlock();
  for (const pinnedNode of normalized) {
    const node = state.cy.getElementById(pinnedNode.nodeId);
    if (node.empty()) {
      continue;
    }

    node.position({ x: pinnedNode.x, y: pinnedNode.y });
    node.lock();
    node.addClass("pinned");
  }
}

function normalizePinnedNodes(rawPinnedNodes: unknown): PinnedNodeViewState[] {
  if (!Array.isArray(rawPinnedNodes)) {
    return [];
  }

  const normalized: PinnedNodeViewState[] = [];
  const seen = new Set<string>();
  for (const rawPinnedNode of rawPinnedNodes) {
    if (typeof rawPinnedNode !== "object" || rawPinnedNode === null) {
      continue;
    }

    const candidate = rawPinnedNode as { nodeId?: unknown; x?: unknown; y?: unknown };
    if (typeof candidate.nodeId !== "string" || candidate.nodeId.trim().length === 0) {
      continue;
    }

    const nodeId = candidate.nodeId.trim();
    if (seen.has(nodeId)) {
      continue;
    }

    const x = typeof candidate.x === "number" && Number.isFinite(candidate.x) ? candidate.x : 0;
    const y = typeof candidate.y === "number" && Number.isFinite(candidate.y) ? candidate.y : 0;
    normalized.push({ nodeId, x, y });
    seen.add(nodeId);
  }

  return normalized;
}

function normalizeHiddenNodes(rawHiddenNodes: unknown): HiddenNodeViewState[] {
  if (!Array.isArray(rawHiddenNodes)) {
    return [];
  }

  const normalized: HiddenNodeViewState[] = [];
  const seen = new Set<string>();
  for (const rawHiddenNode of rawHiddenNodes) {
    if (typeof rawHiddenNode !== "object" || rawHiddenNode === null) {
      continue;
    }

    const candidate = rawHiddenNode as { nodeId?: unknown; label?: unknown; group?: unknown };
    if (typeof candidate.nodeId !== "string" || candidate.nodeId.trim().length === 0) {
      continue;
    }

    const nodeId = candidate.nodeId.trim();
    if (seen.has(nodeId)) {
      continue;
    }

    const label = typeof candidate.label === "string" && candidate.label.trim().length > 0
      ? candidate.label.trim()
      : nodeId;
    const group = typeof candidate.group === "string"
      ? candidate.group.trim()
      : "";
    normalized.push({ nodeId, label, group });
    seen.add(nodeId);
  }

  return normalized.sort((left, right) => left.label.localeCompare(right.label));
}

function setHiddenNodes(hiddenNodes: HiddenNodeViewState[]): void {
  state.hiddenNodes = hiddenNodes;
  state.hiddenNodeIds = new Set(hiddenNodes.map((hiddenNode) => hiddenNode.nodeId));
}

function upsertPinnedNodeState(
  pinnedNodes: PinnedNodeViewState[],
  nextPinnedNode: PinnedNodeViewState): PinnedNodeViewState[]
{
  const normalized = normalizePinnedNodes(pinnedNodes);
  const updated = normalized.filter((candidate) => candidate.nodeId !== nextPinnedNode.nodeId);
  updated.push(nextPinnedNode);
  return updated.sort((left, right) => left.nodeId.localeCompare(right.nodeId));
}

function createCurrentViewState(): ViewStateRequest {
  return {
    includeProjects: state.includeProjects,
    includeDocuments: state.includeDocuments,
    includePackages: state.includePackages,
    includeSymbols: state.includeSymbols,
    includeAssemblies: state.includeAssemblies,
    includeNativeDependencies: state.includeNativeDependencies,
    includeDocumentDependencies: state.includeDocumentDependencies,
    includeSymbolDependencies: state.includeSymbolDependencies,
    isDependencyMapMode: state.isDependencyMapMode,
    isImpactAnalysisMode: state.isImpactAnalysisMode,
    showCyclesOnly: state.showCyclesOnly,
    dependencyMapDirection: state.dependencyMapDirection,
    panelWidth: state.panelWidth,
    mobilePanelHeight: state.mobilePanelHeight,
    pinnedNodes: capturePinnedNodeState(),
    hiddenNodes: state.hiddenNodes
  };
}

function exportCurrentViewState(): void {
  const payload = JSON.stringify(createCurrentViewState(), null, 2);
  const blob = new Blob([payload], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `codemap-view-${new Date().toISOString().replace(/[:.]/g, "-")}.json`;
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

async function importViewStateFromFile(file: File): Promise<void> {
  const text = await file.text();
  const parsed = parseViewStateRequest(JSON.parse(text));
  if (!parsed) {
    postHostMessage({
      type: "graph-error",
      message: "invalid saved view state"
    });
    return;
  }

  applyViewStateFromHost(parsed);
}

async function copyCurrentViewStateToClipboard(): Promise<void> {
  const payload = JSON.stringify(createCurrentViewState(), null, 2);
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(payload);
    return;
  }

  const textArea = document.createElement("textarea");
  textArea.value = payload;
  document.body.appendChild(textArea);
  textArea.select();
  document.execCommand("copy");
  textArea.remove();
}

function fitGraphViewport(padding: number): void {
  if (!state.cy) {
    return;
  }

  const elements = state.cy.elements().not(".state-hidden").not(".filtered-out");
  if (elements.length === 0) {
    return;
  }

  fitGraphViewportToCollection(elements, padding);
}

function resolveNodeReferenceStats(node: any): NodeReferenceStats {
  const incomingEdges = node.incomers("edge").filter((edge: any) => isDependencyEdgeKind(String(edge.data("kind") ?? "")));
  const outgoingEdges = node.outgoers("edge").filter((edge: any) => isDependencyEdgeKind(String(edge.data("kind") ?? "")));
  return {
    incomingEdges,
    outgoingEdges,
    incomingWeight: sumEdgeWeights(incomingEdges),
    outgoingWeight: sumEdgeWeights(outgoingEdges)
  };
}

function resolveInspectorReferenceStats(node: any): NodeReferenceStats {
  const selectionSeedNodes = getSelectionSeedNodes(node);
  const incomingEdges = getDependencyEdgesForSeedNodes(selectionSeedNodes, "incoming");
  const outgoingEdges = getDependencyEdgesForSeedNodes(selectionSeedNodes, "outgoing");
  return {
    incomingEdges,
    outgoingEdges,
    incomingWeight: sumEdgeWeights(incomingEdges),
    outgoingWeight: sumEdgeWeights(outgoingEdges)
  };
}

function getSelectionSeedNodes(selectedNode: any): any {
  if (!state.cy) {
    return selectedNode;
  }

  let seedNodes = state.cy.collection().union(selectedNode);
  const group = String(selectedNode.data("group") ?? "");
  if (group === "document" && state.includeSymbols && state.includeSymbolDependencies) {
    seedNodes = seedNodes.union(getContainedSymbolNodes(selectedNode));
  }
  else if (group === "symbol" && state.includeDocuments && state.includeDocumentDependencies) {
    seedNodes = seedNodes.union(getOwnerDocumentNodes(selectedNode));
  }

  return seedNodes;
}

function collectDependencyMapSeedNodes(
  selectedNode: any,
  selectionSeedNodes: any,
  direction: DependencyMapDirection,
  options: DependencyEdgeQueryOptions = {}): any
{
  if (!state.cy) {
    return selectionSeedNodes;
  }

  let relevantSeedNodes = state.cy.collection().union(selectedNode);
  selectionSeedNodes.forEach((seedNode: any) => {
    if (seedNode.id() === selectedNode.id()) {
      return;
    }

    if (getDependencyEdgesByDirection(seedNode, direction, options).length > 0) {
      relevantSeedNodes = relevantSeedNodes.union(seedNode);
    }
  });

  return relevantSeedNodes;
}

function getContainedSymbolNodes(documentNode: any): any {
  return documentNode.outgoers("edge[kind = 'contains-symbol']").targets();
}

function getOwnerDocumentNodes(symbolNode: any): any {
  return symbolNode.incomers("edge[kind = 'contains-symbol']").sources();
}

function getOwnerProjectNodes(documentNode: any): any {
  return documentNode.incomers("edge[kind = 'contains-document']").sources();
}

function getDependencyEdgesForSeedNodes(
  seedNodes: any,
  direction: DependencyMapDirection,
  options: DependencyEdgeQueryOptions = {}): any
{
  if (!state.cy) {
    return seedNodes;
  }

  let edges = state.cy.collection();
  seedNodes.forEach((seedNode: any) => {
    edges = edges.union(getDependencyEdgesByDirection(seedNode, direction, options));
  });
  return edges;
}

function buildSelectionNeighborhood(selectedNode: any, selectionSeedNodes: any, dependencyEdges: any): any {
  if (!state.cy) {
    return selectedNode;
  }

  let neighborhood = selectionSeedNodes.union(dependencyEdges).union(dependencyEdges.connectedNodes());
  neighborhood = neighborhood.union(collectOwnershipContext(neighborhood.nodes()));
  neighborhood = neighborhood.union(selectedNode);
  return neighborhood;
}

function collectOwnershipContext(nodes: any): any {
  if (!state.cy) {
    return nodes;
  }

  let context = state.cy.collection();
  nodes.forEach((node: any) => {
    const group = String(node.data("group") ?? "");
    if (group === "symbol") {
      const ownershipEdges = node
        .incomers("edge[kind = 'contains-symbol']")
        .filter((edge: any) => !edge.hasClass("state-hidden"));
      const documentNodes = ownershipEdges
        .sources()
        .filter((documentNode: any) => !documentNode.hasClass("state-hidden"));
      context = context.union(ownershipEdges).union(documentNodes);
      documentNodes.forEach((documentNode: any) => {
        if (!state.includeProjects) {
          return;
        }

        const projectEdges = documentNode
          .incomers("edge[kind = 'contains-document']")
          .filter((edge: any) => !edge.hasClass("state-hidden"));
        const projectNodes = projectEdges
          .sources()
          .filter((projectNode: any) => !projectNode.hasClass("state-hidden"));
        context = context.union(projectEdges).union(projectNodes);
      });
      return;
    }

    if (group === "document" && state.includeProjects) {
      const projectEdges = node
        .incomers("edge[kind = 'contains-document']")
        .filter((edge: any) => !edge.hasClass("state-hidden"));
      const projectNodes = projectEdges
        .sources()
        .filter((projectNode: any) => !projectNode.hasClass("state-hidden"));
      context = context.union(projectEdges).union(projectNodes);
    }
  });

  return context;
}

function sumEdgeWeights(edges: GraphEdgeCollectionLike): number {
  let total = 0;
  edges.forEach((edge) => {
    total += Math.max(1, Number(edge.data("weight") ?? 1));
  });

  return total;
}

function resolveNodeTypeLabel(data: { group?: string; symbolKind?: string | null }): string {
  const group = String(data.group ?? "");
  if (group === "symbol") {
    return resolveSymbolKindLabel(String(data.symbolKind ?? "Unknown"));
  }

  return resolveGroupLabel(group);
}

function resolveNodeDisplayFileName(data: { fileName?: string | null }): string {
  if (typeof data.fileName === "string" && data.fileName.trim().length > 0) {
    return data.fileName;
  }

  return t("tooltip.unavailable");
}

function resolveDisplayPath(filePath?: string | null, lineNumber?: number | null): string {
  if (typeof filePath !== "string" || filePath.trim().length === 0) {
    return t("tooltip.unavailable");
  }

  const segments = filePath.split(/[\\/]/u);
  const fileName = segments[segments.length - 1] || filePath;
  if (typeof lineNumber === "number" && Number.isFinite(lineNumber) && lineNumber > 0) {
    return `${fileName}:${Math.trunc(lineNumber)}`;
  }

  return fileName;
}

function localizeConfidence(value?: string | null): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    return t("tooltip.unavailable");
  }

  const key = `confidence.${value}`;
  const localized = t(key);
  return localized === key ? value : localized;
}

function localizeReferenceKind(value?: string | null): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    return t("tooltip.unavailable");
  }

  const key = `edge.referenceKind.${value}`;
  const localized = t(key);
  return localized === key ? value : localized;
}

function resolveEdgeKindLabel(kind: string): string {
  const key = `edge.kind.${kind}`;
  const localized = t(key);
  return localized === key ? kind : localized;
}

function resolveEdgeDescription(kind: string): string {
  const key = `edge.description.${kind}`;
  const localized = t(key);
  return localized === key ? kind : localized;
}

function localizeYesNo(value: boolean): string {
  return value ? t("state.yes") : t("state.no");
}

function showNodeHoverCard(node: any, originalEvent?: MouseEvent): void {
  const data = node.data();
  const referenceStats = resolveNodeReferenceStats(node);
  showHoverCard(nodeHoverCardEl, [
    `<div class="node-hover-title">${escapeHtml(String(data.label ?? ""))}</div>`,
    renderHoverField(t("tooltip.type"), resolveNodeTypeLabel(data)),
    renderHoverField(t("tooltip.file"), resolveNodeDisplayFileName(data)),
    renderHoverField(t("tooltip.incoming"), String(referenceStats.incomingWeight)),
    renderHoverField(t("tooltip.outgoing"), String(referenceStats.outgoingWeight))
  ].join(""), originalEvent);
}

function moveNodeHoverCard(originalEvent?: MouseEvent): void {
  moveHoverCard(nodeHoverCardEl, originalEvent);
}

function hideNodeHoverCard(): void {
  hideHoverCard(nodeHoverCardEl);
}

function showEdgeHoverCard(edge: any, originalEvent?: MouseEvent): void {
  const data = edge.data();
  const sourceLabel = String(edge.source().data("label") ?? "");
  const targetLabel = String(edge.target().data("label") ?? "");
  const kind = String(data.kind ?? "");
  const sampleFilePath = typeof data.sampleFilePath === "string" ? data.sampleFilePath : null;
  const sampleLineNumber = Number(data.sampleLineNumber ?? 0);
  const sampleSnippet = typeof data.sampleSnippet === "string" && data.sampleSnippet.trim().length > 0
    ? data.sampleSnippet
    : null;
  const referenceKind = typeof data.referenceKind === "string" ? data.referenceKind : null;
  const exampleHtml = sampleSnippet
    ? `<pre class="edge-hover-code">${escapeHtml(sampleSnippet)}</pre>`
    : `<div class="edge-hover-empty">${escapeHtml(t("tooltip.unavailable"))}</div>`;

  showHoverCard(edgeHoverCardEl, [
    `<div class="node-hover-title">${escapeHtml(t("edge.details"))}</div>`,
    `<div class="edge-hover-badges"><span class="edge-hover-badge">${escapeHtml(resolveEdgeKindLabel(kind))}</span></div>`,
    renderHoverField(t("edge.kind"), resolveEdgeKindLabel(kind)),
    renderHoverField(t("edge.description"), resolveEdgeDescription(kind)),
    renderHoverField(t("edge.from"), sourceLabel),
    renderHoverField(t("edge.to"), targetLabel),
    renderHoverField(t("edge.references"), String(Math.max(1, Number(data.weight ?? 1)))),
    renderHoverField(t("edge.referenceKind"), localizeReferenceKind(referenceKind)),
    renderHoverField(t("edge.confidence"), localizeConfidence(typeof data.confidence === "string" ? data.confidence : null)),
    renderHoverField(
      t("edge.location"),
      resolveDisplayPath(sampleFilePath, Number.isFinite(sampleLineNumber) && sampleLineNumber > 0 ? sampleLineNumber : null)),
    `<div class="edge-hover-section-label">${escapeHtml(t("edge.example"))}</div>`,
    exampleHtml
  ].join(""), originalEvent);
}

function moveEdgeHoverCard(originalEvent?: MouseEvent): void {
  moveHoverCard(edgeHoverCardEl, originalEvent);
}

function hideEdgeHoverCard(): void {
  hideHoverCard(edgeHoverCardEl);
}

function showHoverCard(cardEl: HTMLElement | null, contentHtml: string, originalEvent?: MouseEvent): void {
  if (!cardEl) {
    return;
  }

  cardEl.innerHTML = contentHtml;
  cardEl.hidden = false;
  moveHoverCard(cardEl, originalEvent);
}

function moveHoverCard(cardEl: HTMLElement | null, originalEvent?: MouseEvent): void {
  if (!cardEl || cardEl.hidden) {
    return;
  }

  positionHoverCard(cardEl, originalEvent);
}

function hideHoverCard(cardEl: HTMLElement | null): void {
  if (!cardEl) {
    return;
  }

  cardEl.hidden = true;
}

function positionHoverCard(cardEl: HTMLElement, originalEvent?: MouseEvent): void {
  if (!graphContainer || !originalEvent) {
    return;
  }

  const containerRect = graphContainer.getBoundingClientRect();
  const cardRect = cardEl.getBoundingClientRect();
  const pointerOffset = 16;
  let left = originalEvent.clientX - containerRect.left + pointerOffset;
  let top = originalEvent.clientY - containerRect.top + pointerOffset;

  if (left + cardRect.width > containerRect.width - 8) {
    left = Math.max(8, containerRect.width - cardRect.width - 8);
  }

  if (top + cardRect.height > containerRect.height - 8) {
    top = Math.max(8, originalEvent.clientY - containerRect.top - cardRect.height - pointerOffset);
  }

  cardEl.style.left = `${Math.round(left)}px`;
  cardEl.style.top = `${Math.round(top)}px`;
}

function renderHoverField(label: string, value: string): string {
  return [
    "<div class=\"node-hover-row\">",
    `<span class="node-hover-label">${escapeHtml(label)}</span>`,
    `<span class="node-hover-value">${escapeHtml(value)}</span>`,
    "</div>"
  ].join("");
}

function updateInspector(node: any): void {
  if (!inspectorEl) {
    return;
  }

  const data = node.data();
  const referenceStats = resolveInspectorReferenceStats(node);
  const incomingRefs = buildReferenceItems(referenceStats.incomingEdges, "incoming");
  const outgoingRefs = buildReferenceItems(referenceStats.outgoingEdges, "outgoing");
  const impactNodeCount = getImpactNodeCount(node, state.dependencyMapDirection);
  const cycleLabel = localizeYesNo(Boolean(data.isInCycle));
  const pinnedLabel = localizeYesNo(node.hasClass("pinned"));

  const detailRows = [
    renderInspectorField(t("inspector.label"), String(data.label ?? "")),
    renderInspectorField(t("inspector.group"), resolveGroupLabel(String(data.group ?? ""))),
    renderInspectorField(t("inspector.type"), resolveNodeTypeLabel(data)),
    renderInspectorField(t("inspector.file"), resolveNodeDisplayFileName(data)),
    renderInspectorField(t("inspector.id"), String(data.id ?? ""))
  ];

  if (typeof data.symbolKind === "string" && data.symbolKind.length > 0) {
    detailRows.push(renderInspectorField(t("inspector.symbolKind"), resolveSymbolKindLabel(data.symbolKind)));
  }

  inspectorEl.innerHTML = [
    "<section class=\"inspector-section\">",
    `<h3 class="inspector-section-title">${escapeHtml(t("panel.inspector"))}</h3>`,
    `<div class="inspector-field-grid">${detailRows.join("")}</div>`,
    "</section>",
    "<section class=\"inspector-section\">",
    `<div class="inspector-metrics">`,
    renderInspectorMetric(t("inspector.in"), referenceStats.incomingWeight),
    renderInspectorMetric(t("inspector.out"), referenceStats.outgoingWeight),
    renderInspectorMetric(t("inspector.impact"), impactNodeCount),
    renderInspectorMetric(t("inspector.cycles"), cycleLabel),
    renderInspectorMetric(t("inspector.pinned"), pinnedLabel),
    "</div>",
    "</section>",
    "<section class=\"inspector-section\">",
    `<h3 class="inspector-section-title">${escapeHtml(t("inspector.referencesFrom"))}</h3>`,
    renderReferenceList(incomingRefs),
    "</section>",
    "<section class=\"inspector-section\">",
    `<h3 class="inspector-section-title">${escapeHtml(t("inspector.referencesTo"))}</h3>`,
    renderReferenceList(outgoingRefs),
    "</section>"
  ].join("");

  inspectorEl.querySelectorAll<HTMLButtonElement>(".inspector-ref-button").forEach((button) => {
    button.addEventListener("click", () => {
      const nodeId = button.dataset.nodeId;
      if (!nodeId) {
        return;
      }

      focusInspectorReferenceNode(nodeId);
    });
  });
}

function buildReferenceItems(edges: GraphEdgeCollectionLike, direction: "incoming" | "outgoing"): InspectorReferenceItem[] {
  const entries: InspectorReferenceItem[] = [];
  const seen = new Set<string>();

  edges.forEach((edge) => {
    if (entries.length >= 12) {
      return;
    }

    const peer = direction === "incoming"
      ? edge.source()
      : edge.target();

    const label = String(peer.data("label") ?? "");
    const groupLabel = resolveGroupLabel(String(peer.data("group") ?? ""));
    const weight = Number(edge.data("weight") ?? 1);
    const entryCore = weight > 1
      ? `${label} (${weight})`
      : label;
    const entry = `[${groupLabel}] ${entryCore}`;
    const nodeId = String(peer.id?.() ?? "");

    if (nodeId.length === 0 || seen.has(nodeId)) {
      return;
    }

    seen.add(nodeId);
    entries.push({
      nodeId,
      label,
      displayText: entry
    });
  });

  return entries;
}

function resetInspector(): void {
  if (!inspectorEl) {
    return;
  }

  inspectorEl.innerHTML = `<div class="inspector-empty">${escapeHtml(t("inspector.empty"))}</div>`;
}

function renderInspectorField(label: string, value: string): string {
  return [
    "<div class=\"inspector-field\">",
    `<div class="inspector-field-label">${escapeHtml(label)}</div>`,
    `<div class="inspector-field-value">${escapeHtml(value)}</div>`,
    "</div>"
  ].join("");
}

function renderInspectorMetric(label: string, value: string | number): string {
  return [
    "<div class=\"inspector-metric\">",
    `<div class="inspector-metric-label">${escapeHtml(label)}</div>`,
    `<div class="inspector-metric-value">${escapeHtml(String(value))}</div>`,
    "</div>"
  ].join("");
}

function renderReferenceList(items: InspectorReferenceItem[]): string {
  if (items.length === 0) {
    return `<div class="inspector-ref-empty">-</div>`;
  }

  return [
    "<ul class=\"inspector-ref-list\">",
    ...items.map((item) => [
      "<li class=\"inspector-ref-item\">",
      `<button class="inspector-ref-button" type="button" data-node-id="${escapeHtml(item.nodeId)}">`,
      escapeHtml(item.displayText),
      "</button>",
      "</li>"
    ].join("")),
    "</ul>"
  ].join("");
}

function focusInspectorReferenceNode(nodeId: string): void {
  if (!state.cy) {
    return;
  }

  const node = state.cy.getElementById(nodeId);
  if (node.empty()) {
    return;
  }

  state.cy.elements().unselect();
  node.select();
  state.selectedNodeId = nodeId;
  updateInspector(node);

  if (state.isImpactAnalysisMode) {
    applyImpactAnalysisForSelection();
  }
  else if (state.isDependencyMapMode) {
    applyDependencyMapForSelection();
  }
  else {
    fitGraphViewportToCollection(node.union(node.connectedEdges().connectedNodes()), 72);
    forceGraphRender();
  }
}

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function isSearchTargetGroup(group: string): group is SearchTargetGroup {
  return group === "document" || group === "symbol";
}

function isExternalDependencyGroup(group: string): group is "package" | "assembly" | "dll" {
  return group === "package" || group === "assembly" || group === "dll";
}

function isDependencyEdgeKind(kind: string): boolean {
  return (
    kind === "document-reference" ||
    kind === "symbol-reference" ||
    kind === "symbol-call" ||
    kind === "symbol-inheritance" ||
    kind === "symbol-implementation" ||
    kind === "symbol-creation" ||
    kind === "project-reference" ||
    kind === "project-package" ||
    kind === "project-assembly" ||
    kind === "project-dll");
}

function resolveGroupLabel(group: string): string {
  return t(`group.${group}`);
}

function isDependencyMapDirection(value: string): value is DependencyMapDirection {
  return value === "incoming" || value === "outgoing" || value === "both";
}

function resolveLocale(): Locale {
  const rawLang = document.documentElement.lang?.trim().toLowerCase() ?? "";
  if (rawLang.startsWith("en")) {
    return "en";
  }

  return "ja";
}

function t(key: string): string {
  const localeTable = translations[state.locale] ?? translations.ja;
  return localeTable[key] ?? translations.ja[key] ?? key;
}

function formatTemplate(template: string, ...args: string[]): string {
  let output = template;
  for (let index = 0; index < args.length; index += 1) {
    output = output.replace(`{${index}}`, args[index]);
  }

  return output;
}

function applyLocalizedText(): void {
  document.documentElement.lang = state.locale;
  document.title = t("page.title");

  const localizedElements = document.querySelectorAll<HTMLElement>("[data-i18n]");
  for (const element of localizedElements) {
    const key = element.dataset.i18n;
    if (!key) {
      continue;
    }

    element.textContent = t(key);
  }

  const localizedPlaceholderElements = document.querySelectorAll<HTMLElement>("[data-i18n-placeholder]");
  for (const element of localizedPlaceholderElements) {
    const key = element.dataset.i18nPlaceholder;
    if (!key) {
      continue;
    }

    if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
      element.placeholder = t(key);
    }
  }

  const localizedAriaLabelElements = document.querySelectorAll<HTMLElement>("[data-i18n-aria-label]");
  for (const element of localizedAriaLabelElements) {
    const key = element.dataset.i18nAriaLabel;
    if (!key) {
      continue;
    }

    element.setAttribute("aria-label", t(key));
  }

  panelSplitterEl?.setAttribute("aria-label", t("splitter.ariaLabel"));
  mobilePanelSplitterEl?.setAttribute("aria-label", t("splitter.mobileAriaLabel"));
  graphContainer?.setAttribute("aria-label", t("graph.ariaLabel"));
}

function applyHostTheme(theme: HostTheme): void {
  state.hostTheme = theme;
  if (theme === "system") {
    document.documentElement.removeAttribute("data-host-theme");
    return;
  }

  document.documentElement.setAttribute("data-host-theme", theme);
}

function initializePanelWidth(): void {
  const storedWidth = Number.parseFloat(getStoredPanelWidth() ?? "");
  setPanelWidth(Number.isFinite(storedWidth) ? storedWidth : state.panelWidth);

  window.addEventListener("resize", () => {
    if (isCompactLayout()) {
      return;
    }

    setPanelWidth(state.panelWidth);
    resizeGraphViewportPreservingTransform();
  });
}

function initializeMobilePanelHeight(): void {
  const storedHeight = Number.parseFloat(getStoredMobilePanelHeight() ?? "");
  const fallbackHeight = resolveDefaultMobilePanelHeight();
  setMobilePanelHeight(Number.isFinite(storedHeight) ? storedHeight : fallbackHeight);

  window.addEventListener("resize", () => {
    if (!isCompactLayout()) {
      return;
    }

    setMobilePanelHeight(state.mobilePanelHeight);
    resizeGraphViewportPreservingTransform();
  });
}

function initializePanelSplitter(): void {
  if (!panelSplitterEl) {
    return;
  }

  panelSplitterEl.addEventListener("pointerdown", onSplitterPointerDown);
  panelSplitterEl.addEventListener("dblclick", () => {
    setPanelWidth(DEFAULT_PANEL_WIDTH);
    persistPanelWidth();
    resizeGraphViewportPreservingTransform();
  });
}

function initializeMobilePanelSplitter(): void {
  if (!mobilePanelSplitterEl) {
    return;
  }

  mobilePanelSplitterEl.addEventListener("pointerdown", onMobileSplitterPointerDown);
  mobilePanelSplitterEl.addEventListener("dblclick", () => {
    setMobilePanelHeight(resolveDefaultMobilePanelHeight());
    persistMobilePanelHeight();
    resizeGraphViewportPreservingTransform();
  });
}

function onSplitterPointerDown(event: PointerEvent): void {
  beginSplitterDrag(
    event,
    panelSplitterEl,
    panelSplitterPointerState,
    !isCompactLayout(),
    panelSplitterHandlers,
    (pointerEvent) => {
      updatePanelWidthFromPointer(pointerEvent.clientX);
    });
}

function onSplitterPointerMove(event: PointerEvent): void {
  continueSplitterDrag(event, activePointerId, !isCompactLayout(), (pointerEvent) => {
    updatePanelWidthFromPointer(pointerEvent.clientX);
  });
}

function onSplitterPointerUp(event: PointerEvent): void {
  if (event.pointerId !== activePointerId) {
    return;
  }

  finishSplitterDrag();
}

function onSplitterPointerCancel(event: PointerEvent): void {
  if (event.pointerId !== activePointerId) {
    return;
  }

  finishSplitterDrag();
}

function onSplitterPointerLostCapture(event: PointerEvent): void {
  if (event.pointerId !== activePointerId) {
    return;
  }

  finishSplitterDrag();
}

function onMobileSplitterPointerDown(event: PointerEvent): void {
  beginSplitterDrag(
    event,
    mobilePanelSplitterEl,
    mobilePanelSplitterPointerState,
    isCompactLayout(),
    mobilePanelSplitterHandlers,
    (pointerEvent) => {
      updateMobilePanelHeightFromPointer(pointerEvent.clientY);
    });
}

function onMobileSplitterPointerMove(event: PointerEvent): void {
  continueSplitterDrag(event, mobileActivePointerId, isCompactLayout(), (pointerEvent) => {
    updateMobilePanelHeightFromPointer(pointerEvent.clientY);
  });
}

function onMobileSplitterPointerUp(event: PointerEvent): void {
  if (event.pointerId !== mobileActivePointerId) {
    return;
  }

  finishMobileSplitterDrag();
}

function onMobileSplitterPointerCancel(event: PointerEvent): void {
  if (event.pointerId !== mobileActivePointerId) {
    return;
  }

  finishMobileSplitterDrag();
}

function onMobileSplitterPointerLostCapture(event: PointerEvent): void {
  if (event.pointerId !== mobileActivePointerId) {
    return;
  }

  finishMobileSplitterDrag();
}

function finishSplitterDrag(): void {
  finishSplitterDragSession(
    panelSplitterEl,
    panelSplitterPointerState,
    panelSplitterHandlers,
    persistPanelWidth);
}

function finishMobileSplitterDrag(): void {
  finishSplitterDragSession(
    mobilePanelSplitterEl,
    mobilePanelSplitterPointerState,
    mobilePanelSplitterHandlers,
    persistMobilePanelHeight);
}

function beginSplitterDrag(
  event: PointerEvent,
  element: HTMLDivElement | null,
  pointerState: SplitterPointerState,
  isEnabled: boolean,
  handlers: SplitterPointerHandlers,
  updateFromPointer: (event: PointerEvent) => void): void {
  if (!element || !isEnabled) {
    return;
  }

  pointerState.set(event.pointerId);
  element.setPointerCapture(event.pointerId);
  element.classList.add("is-dragging");
  element.addEventListener("pointermove", handlers.move);
  element.addEventListener("pointerup", handlers.up);
  element.addEventListener("pointercancel", handlers.cancel);
  element.addEventListener("lostpointercapture", handlers.lost);
  updateFromPointer(event);
  requestLiveResizePreview();
  event.preventDefault();
}

function continueSplitterDrag(
  event: PointerEvent,
  activeId: number | null,
  isEnabled: boolean,
  updateFromPointer: (event: PointerEvent) => void): void {
  if (event.pointerId !== activeId || !isEnabled) {
    return;
  }

  updateFromPointer(event);
  requestLiveResizePreview();
}

function finishSplitterDragSession(
  element: HTMLDivElement | null,
  pointerState: SplitterPointerState,
  handlers: SplitterPointerHandlers,
  persistSize: () => void): void {
  if (!element) {
    return;
  }

  const pointerId = pointerState.get();
  pointerState.set(null);
  if (pointerId !== null && element.hasPointerCapture(pointerId)) {
    element.releasePointerCapture(pointerId);
  }

  element.classList.remove("is-dragging");
  element.removeEventListener("pointermove", handlers.move);
  element.removeEventListener("pointerup", handlers.up);
  element.removeEventListener("pointercancel", handlers.cancel);
  element.removeEventListener("lostpointercapture", handlers.lost);
  cancelLiveResizePreview();
  persistSize();

  if (!state.cy) {
    return;
  }

  resizeGraphViewportPreservingTransform(true);
}

function updatePanelWidthFromPointer(pointerX: number): void {
  if (!workspaceEl) {
    return;
  }

  const workspaceRect = workspaceEl.getBoundingClientRect();
  const nextWidth = workspaceRect.right - pointerX;
  setPanelWidth(nextWidth);
}

function setPanelWidth(panelWidth: number): void {
  if (!workspaceEl || !Number.isFinite(panelWidth)) {
    return;
  }

  const workspaceWidth = workspaceEl.getBoundingClientRect().width;
  const dynamicMax = Math.max(MIN_PANEL_WIDTH, Math.min(MAX_PANEL_WIDTH, workspaceWidth * 0.6));
  const clampedWidth = Math.round(Math.max(MIN_PANEL_WIDTH, Math.min(dynamicMax, panelWidth)));
  state.panelWidth = clampedWidth;
  document.documentElement.style.setProperty("--panel-width", `${clampedWidth}px`);
}

function updateMobilePanelHeightFromPointer(pointerY: number): void {
  if (!workspaceEl) {
    return;
  }

  const workspaceRect = workspaceEl.getBoundingClientRect();
  const nextHeight = workspaceRect.bottom - pointerY;
  setMobilePanelHeight(nextHeight);
}

function setMobilePanelHeight(panelHeight: number): void {
  if (!workspaceEl || !Number.isFinite(panelHeight)) {
    return;
  }

  const workspaceHeight = workspaceEl.getBoundingClientRect().height;
  if (workspaceHeight <= 1) {
    return;
  }

  const dynamicMax = Math.max(
    MIN_MOBILE_PANEL_HEIGHT,
    workspaceHeight - MIN_MOBILE_GRAPH_HEIGHT - MOBILE_SPLITTER_HEIGHT);
  const clampedHeight = Math.round(Math.max(MIN_MOBILE_PANEL_HEIGHT, Math.min(dynamicMax, panelHeight)));
  state.mobilePanelHeight = clampedHeight;
  document.documentElement.style.setProperty("--mobile-panel-height", `${clampedHeight}px`);
}

function requestLiveResizePreview(): void {
  if (liveResizePreviewFrame !== null) {
    return;
  }

  liveResizePreviewFrame = window.requestAnimationFrame(() => {
    liveResizePreviewFrame = null;
    resizeGraphViewportPreservingTransform(false, true);
  });
}

function resizeGraphViewportPreservingTransform(force = false, suppressForceRender = false): void {
  if (!state.cy || !graphContainer) {
    return;
  }

  const graphBounds = graphContainer.getBoundingClientRect();
  const nextWidth = Math.round(graphBounds.width);
  const nextHeight = Math.round(graphBounds.height);
  if (nextWidth <= 1 || nextHeight <= 1) {
    return;
  }

  if (!force && nextWidth === lastViewportWidth && nextHeight === lastViewportHeight) {
    return;
  }

  lastViewportWidth = nextWidth;
  lastViewportHeight = nextHeight;
  const pan = state.cy.pan();
  const zoom = state.cy.zoom();
  state.cy.resize();
  state.cy.zoom(zoom);
  state.cy.pan(pan);
  if (!suppressForceRender) {
    forceGraphRender();
  }
}

function cancelLiveResizePreview(): void {
  if (liveResizePreviewFrame === null) {
    return;
  }

  window.cancelAnimationFrame(liveResizePreviewFrame);
  liveResizePreviewFrame = null;
}

function persistPanelWidth(): void {
  try {
    window.localStorage.setItem(PANEL_WIDTH_STORAGE_KEY, String(state.panelWidth));
  }
  catch {
    // Ignore storage failures in restricted environments.
  }
}

function persistMobilePanelHeight(): void {
  try {
    window.localStorage.setItem(MOBILE_PANEL_HEIGHT_STORAGE_KEY, String(state.mobilePanelHeight));
  }
  catch {
    // Ignore storage failures in restricted environments.
  }
}

function isCompactLayout(): boolean {
  return window.matchMedia("(max-width: 920px)").matches;
}

function resolveDefaultMobilePanelHeight(): number {
  if (!workspaceEl) {
    return DEFAULT_MOBILE_PANEL_HEIGHT;
  }

  const workspaceHeight = workspaceEl.getBoundingClientRect().height;
  if (workspaceHeight <= 1) {
    return DEFAULT_MOBILE_PANEL_HEIGHT;
  }

  const computedHeight = Math.round(workspaceHeight * 0.42);
  return Math.max(MIN_MOBILE_PANEL_HEIGHT, computedHeight);
}

function postHostMessage(message: HostMessage<unknown>): void {
  const webview = getHostWebView();
  if (!webview) {
    return;
  }

  webview.postMessage(message);
}

function installGlobalErrorForwarding(): void {
  window.addEventListener("error", (event) => {
    postHostMessage({
      type: "graph-error",
      message: event.message
    });
  });

  window.addEventListener("unhandledrejection", (event) => {
    postHostMessage({
      type: "graph-error",
      message: String(event.reason ?? "unhandledrejection")
    });
  });
}

function parseMessage(raw: unknown): HostMessage<GraphPayload> | null {
  if (typeof raw === "string") {
    try {
      return normalizeHostMessage(JSON.parse(raw));
    }
    catch {
      return null;
    }
  }

  return normalizeHostMessage(raw);
}

function parseFocusNodeRequest(raw: unknown): FocusNodeRequest | null {
  if (typeof raw !== "object" || raw === null) {
    return null;
  }

  const candidate = raw as
    {
      nodeId?: unknown;
      label?: unknown;
      enableDependencyMap?: unknown;
      forceVisible?: unknown;
    };

  if (typeof candidate.nodeId !== "string" || candidate.nodeId.trim().length === 0) {
    return null;
  }

  return {
    nodeId: candidate.nodeId,
    label: typeof candidate.label === "string" && candidate.label.trim().length > 0
      ? candidate.label.trim()
      : undefined,
    enableDependencyMap: typeof candidate.enableDependencyMap === "boolean"
      ? candidate.enableDependencyMap
      : true,
    forceVisible: typeof candidate.forceVisible === "boolean"
      ? candidate.forceVisible
      : true
  };
}

function parseViewStateRequest(raw: unknown): ViewStateRequest | null {
  if (typeof raw !== "object" || raw === null) {
    return null;
  }

  const candidate = raw as Record<string, unknown>;
  const viewState: ViewStateRequest = {};

  if (typeof candidate.includeProjects === "boolean") {
    viewState.includeProjects = candidate.includeProjects;
  }

  if (typeof candidate.includeDocuments === "boolean") {
    viewState.includeDocuments = candidate.includeDocuments;
  }

  if (typeof candidate.includePackages === "boolean") {
    viewState.includePackages = candidate.includePackages;
  }

  if (typeof candidate.includeSymbols === "boolean") {
    viewState.includeSymbols = candidate.includeSymbols;
  }

  if (typeof candidate.includeAssemblies === "boolean") {
    viewState.includeAssemblies = candidate.includeAssemblies;
  }

  if (typeof candidate.includeNativeDependencies === "boolean") {
    viewState.includeNativeDependencies = candidate.includeNativeDependencies;
  }

  if (typeof candidate.includeDocumentDependencies === "boolean") {
    viewState.includeDocumentDependencies = candidate.includeDocumentDependencies;
  }

  if (typeof candidate.includeSymbolDependencies === "boolean") {
    viewState.includeSymbolDependencies = candidate.includeSymbolDependencies;
  }

  if (typeof candidate.isDependencyMapMode === "boolean") {
    viewState.isDependencyMapMode = candidate.isDependencyMapMode;
  }

  if (typeof candidate.isImpactAnalysisMode === "boolean") {
    viewState.isImpactAnalysisMode = candidate.isImpactAnalysisMode;
  }

  if (typeof candidate.showCyclesOnly === "boolean") {
    viewState.showCyclesOnly = candidate.showCyclesOnly;
  }

  if (typeof candidate.dependencyMapDirection === "string" && isDependencyMapDirection(candidate.dependencyMapDirection)) {
    viewState.dependencyMapDirection = candidate.dependencyMapDirection;
  }

  if (typeof candidate.panelWidth === "number" && Number.isFinite(candidate.panelWidth)) {
    viewState.panelWidth = candidate.panelWidth;
  }

  if (typeof candidate.mobilePanelHeight === "number" && Number.isFinite(candidate.mobilePanelHeight)) {
    viewState.mobilePanelHeight = candidate.mobilePanelHeight;
  }

  if (Array.isArray(candidate.pinnedNodes)) {
    viewState.pinnedNodes = normalizePinnedNodes(candidate.pinnedNodes);
  }

  if (Array.isArray(candidate.hiddenNodes)) {
    viewState.hiddenNodes = normalizeHiddenNodes(candidate.hiddenNodes);
  }

  return viewState;
}

function parseLocale(raw: unknown): Locale | null {
  if (typeof raw === "string") {
    const normalized = raw.trim().toLowerCase();
    if (normalized.startsWith("ja")) {
      return "ja";
    }

    if (normalized.startsWith("en")) {
      return "en";
    }

    return null;
  }

  if (typeof raw !== "object" || raw === null) {
    return null;
  }

  const candidate = raw as { locale?: unknown };
  return parseLocale(candidate.locale);
}

function parseHostTheme(raw: unknown): HostTheme | null {
  if (typeof raw === "string") {
    const normalized = raw.trim().toLowerCase();
    if (normalized === "light") {
      return "light";
    }

    if (normalized === "dark") {
      return "dark";
    }

    if (normalized === "system" || normalized === "default") {
      return "system";
    }

    return null;
  }

  if (typeof raw !== "object" || raw === null) {
    return null;
  }

  const candidate = raw as { theme?: unknown };
  return parseHostTheme(candidate.theme);
}

function parseSearchQuery(raw: unknown): string | null {
  if (typeof raw === "string") {
    return raw.trim();
  }

  if (typeof raw !== "object" || raw === null) {
    return null;
  }

  const candidate = raw as { query?: unknown };
  if (typeof candidate.query !== "string") {
    return null;
  }

  return candidate.query.trim();
}

function normalizeHostMessage(raw: unknown): HostMessage<GraphPayload> | null {
  if (typeof raw !== "object" || raw === null) {
    return null;
  }

  const candidate = raw as { type?: unknown; data?: unknown };
  if (typeof candidate.type !== "string") {
    return null;
  }

  if (candidate.type !== "render-graph") {
    return candidate as HostMessage<GraphPayload>;
  }

  if (!isGraphPayload(candidate.data)) {
    return null;
  }

  return {
    ...candidate,
    data: candidate.data
  } as HostMessage<GraphPayload>;
}

function isGraphPayload(raw: unknown): raw is GraphPayload {
  if (typeof raw !== "object" || raw === null) {
    return false;
  }

  const payload = raw as { nodes?: unknown; edges?: unknown; stats?: unknown };
  return Array.isArray(payload.nodes) && Array.isArray(payload.edges) && typeof payload.stats === "object" && payload.stats !== null;
}

function postGraphRendered(): void {
  if (!state.cy) {
    return;
  }

  const containerWidth = Math.round(graphContainer?.getBoundingClientRect().width ?? 0);
  const containerHeight = Math.round(graphContainer?.getBoundingClientRect().height ?? 0);
  const visibleElements = state.cy.elements().not(".state-hidden").not(".filtered-out");
  const renderedNodes = state.cy.nodes().not(".state-hidden").not(".filtered-out").length;
  const renderedEdges = state.cy.edges().not(".state-hidden").not(".filtered-out").length;
  const extent = visibleElements.length > 0
    ? visibleElements.boundingBox()
    : { x1: 0, y1: 0, x2: 0, y2: 0 };
  postHostMessage({
    type: "graph-rendered",
    frameTag: Date.now().toString(),
    includeProjects: state.includeProjects,
    includeDocuments: state.includeDocuments,
    includePackages: state.includePackages,
    includeSymbols: state.includeSymbols,
    includeAssemblies: state.includeAssemblies,
    includeNativeDependencies: state.includeNativeDependencies,
    includeDocumentDependencies: state.includeDocumentDependencies,
    includeSymbolDependencies: state.includeSymbolDependencies,
    isDependencyMapMode: state.isDependencyMapMode,
    isImpactAnalysisMode: state.isImpactAnalysisMode,
    showCyclesOnly: state.showCyclesOnly,
    dependencyMapDirection: state.dependencyMapDirection,
    graphPerformanceMode: state.graphPerformanceMode,
    panelWidth: state.panelWidth,
    mobilePanelHeight: state.mobilePanelHeight,
    pinnedNodes: capturePinnedNodeState(),
    hiddenNodes: state.hiddenNodes,
    searchQuery: state.searchQuery,
    searchMatchCount: state.searchMatches.length,
    renderedNodeCount: renderedNodes,
    renderedEdgeCount: renderedEdges,
    containerWidth,
    containerHeight,
    zoom: Number(state.cy.zoom()).toFixed(3),
    boundsX1: Math.round(extent.x1 ?? 0),
    boundsY1: Math.round(extent.y1 ?? 0),
    boundsX2: Math.round(extent.x2 ?? 0),
    boundsY2: Math.round(extent.y2 ?? 0)
  });
}

function getStoredPanelWidth(): string | null {
  try {
    return window.localStorage.getItem(PANEL_WIDTH_STORAGE_KEY);
  }
  catch {
    return null;
  }
}

function getStoredMobilePanelHeight(): string | null {
  try {
    return window.localStorage.getItem(MOBILE_PANEL_HEIGHT_STORAGE_KEY);
  }
  catch {
    return null;
  }
}

function getHostWebView(): WebViewBridge | undefined {
  return (window as ChromiumWindow).chrome?.webview;
}

main();
