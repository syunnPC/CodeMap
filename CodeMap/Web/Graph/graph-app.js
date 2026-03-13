"use strict";
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
const translations = {
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
const symbolKindNames = {
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
    cy: null,
    layoutIndex: 0,
    lastPayload: null,
    includeProjects: true,
    includeDocuments: true,
    includePackages: true,
    includeSymbols: false,
    includeAssemblies: true,
    includeNativeDependencies: true,
    includeDocumentDependencies: true,
    includeSymbolDependencies: true,
    symbolKindVisibility: {},
    searchQuery: "",
    searchMatches: [],
    selectedNodeId: null,
    pendingFocusRequest: null,
    isDependencyMapMode: false,
    isImpactAnalysisMode: false,
    showCyclesOnly: false,
    dependencyMapDirection: "both",
    locale: resolveLocale(),
    hostTheme: "system",
    panelWidth: DEFAULT_PANEL_WIDTH,
    mobilePanelHeight: DEFAULT_MOBILE_PANEL_HEIGHT,
    graphPerformanceMode: "normal",
    hasSearchHighlightClasses: false,
    pinnedNodes: [],
    hiddenNodes: [],
    hiddenNodeIds: new Set(),
    focusedModePositions: null
};
const inspectorEl = document.getElementById("nodeInspector");
const layoutButton = document.getElementById("layoutButton");
const fitButton = document.getElementById("fitButton");
const dependencyMapModeToggle = document.getElementById("dependencyMapModeToggle");
const impactAnalysisModeToggle = document.getElementById("impactAnalysisModeToggle");
const showCyclesOnlyToggle = document.getElementById("showCyclesOnlyToggle");
const dependencyMapDirectionSelect = document.getElementById("dependencyMapDirectionSelect");
const showProjectsToggle = document.getElementById("showProjectsToggle");
const showDocumentsToggle = document.getElementById("showDocumentsToggle");
const showPackagesToggle = document.getElementById("showPackagesToggle");
const showSymbolsToggle = document.getElementById("showSymbolsToggle");
const showAssembliesToggle = document.getElementById("showAssembliesToggle");
const showNativeDependenciesToggle = document.getElementById("showNativeDependenciesToggle");
const showDocumentDependenciesToggle = document.getElementById("showDocumentDependenciesToggle");
const showSymbolDependenciesToggle = document.getElementById("showSymbolDependenciesToggle");
const searchInput = document.getElementById("searchInput");
const searchResultText = document.getElementById("searchResultText");
const focusSearchButton = document.getElementById("focusSearchButton");
const clearSearchButton = document.getElementById("clearSearchButton");
const pinSelectedButton = document.getElementById("pinSelectedButton");
const unpinAllButton = document.getElementById("unpinAllButton");
const hideSelectedButton = document.getElementById("hideSelectedButton");
const exportViewButton = document.getElementById("exportViewButton");
const importViewButton = document.getElementById("importViewButton");
const copyShareButton = document.getElementById("copyShareButton");
const importViewInput = document.getElementById("importViewInput");
const symbolTypeFiltersEl = document.getElementById("symbolTypeFilters");
const graphContainer = document.getElementById("graph");
const nodeHoverCardEl = document.getElementById("nodeHoverCard");
const edgeHoverCardEl = document.getElementById("edgeHoverCard");
const workspaceEl = document.getElementById("workspace");
const panelSplitterEl = document.getElementById("panelSplitter");
const mobilePanelSplitterEl = document.getElementById("mobilePanelSplitter");
let activePointerId = null;
let mobileActivePointerId = null;
let liveResizePreviewFrame = null;
let pendingSearchHighlightFrame = null;
let lastViewportWidth = 0;
let lastViewportHeight = 0;
const impactNodeCountCache = new Map();
const panelSplitterPointerState = {
    get: () => activePointerId,
    set: (pointerId) => {
        activePointerId = pointerId;
    }
};
const mobilePanelSplitterPointerState = {
    get: () => mobileActivePointerId,
    set: (pointerId) => {
        mobileActivePointerId = pointerId;
    }
};
const panelSplitterHandlers = {
    move: onSplitterPointerMove,
    up: onSplitterPointerUp,
    cancel: onSplitterPointerCancel,
    lost: onSplitterPointerLostCapture
};
const mobilePanelSplitterHandlers = {
    move: onMobileSplitterPointerMove,
    up: onMobileSplitterPointerUp,
    cancel: onMobileSplitterPointerCancel,
    lost: onMobileSplitterPointerLostCapture
};
installGlobalErrorForwarding();
function main() {
    if (window.__codeMapGraphInitialized) {
        return;
    }
    window.__codeMapGraphInitialized = true;
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
    state.cy.on("tap", "node", (event) => {
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
    state.cy.on("tap", (event) => {
        if (event.target !== state.cy) {
            return;
        }
        clearSelection(true);
    });
    state.cy.on("mouseover", "node", (event) => {
        hideEdgeHoverCard();
        showNodeHoverCard(event.target, event.originalEvent);
    });
    state.cy.on("mousemove", "node", (event) => {
        moveNodeHoverCard(event.originalEvent);
    });
    state.cy.on("mouseout", "node", () => {
        hideNodeHoverCard();
    });
    state.cy.on("mouseover", "edge", (event) => {
        hideNodeHoverCard();
        showEdgeHoverCard(event.target, event.originalEvent);
    });
    state.cy.on("mousemove", "edge", (event) => {
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
function setupSearchControls() {
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
function registerHostShortcutBridges() {
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
function scheduleApplySearchHighlights() {
    cancelScheduledSearchHighlights();
    pendingSearchHighlightFrame = window.setTimeout(() => {
        pendingSearchHighlightFrame = null;
        applySearchHighlights();
    }, SEARCH_INPUT_DEBOUNCE_MS);
}
function cancelScheduledSearchHighlights() {
    if (pendingSearchHighlightFrame === null) {
        return;
    }
    window.clearTimeout(pendingSearchHighlightFrame);
    pendingSearchHighlightFrame = null;
}
function onHostMessage(event) {
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
            nodeCount: Array.isArray(incoming.data?.nodes)
                ? (incoming.data.nodes?.length ?? 0)
                : 0,
            edgeCount: Array.isArray(incoming.data?.edges)
                ? (incoming.data.edges?.length ?? 0)
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
function renderGraph(payload) {
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
function rebuildGraphFromPayload() {
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
function rerenderGraphFromState(fitViewport = true) {
    applyGraphVisibilityFromState({ fitViewport });
}
function applyGraphVisibilityFromState(options) {
    if (!state.cy) {
        return;
    }
    applyBaseVisibilityClasses();
    if (options.fitViewport) {
        fitGraphViewport(56);
    }
    if (state.selectedNodeId &&
        (state.cy.getElementById(state.selectedNodeId).empty() ||
            state.cy.getElementById(state.selectedNodeId).hasClass("state-hidden"))) {
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
function applyBaseVisibilityClasses() {
    if (!state.cy) {
        return;
    }
    const visibleNodeIds = new Set();
    state.cy.startBatch();
    state.cy.nodes().forEach((node) => {
        const isVisible = shouldNodeBeVisible(node.data());
        node.toggleClass("state-hidden", !isVisible);
        if (isVisible) {
            visibleNodeIds.add(node.id());
        }
    });
    state.cy.edges().forEach((edge) => {
        const isVisible = shouldEdgeBeVisible(edge.data(), visibleNodeIds);
        edge.toggleClass("state-hidden", !isVisible);
    });
    state.cy.endBatch();
}
function applyGraphPerformanceModeFromCurrentVisibility() {
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
function resolveGraphPerformanceMode(visibleNodeCount, visibleEdgeCount) {
    if (visibleNodeCount >= MASSIVE_GRAPH_NODE_THRESHOLD ||
        visibleEdgeCount >= MASSIVE_GRAPH_EDGE_THRESHOLD) {
        return "massive";
    }
    if (visibleNodeCount >= LARGE_GRAPH_NODE_THRESHOLD ||
        visibleEdgeCount >= LARGE_GRAPH_EDGE_THRESHOLD) {
        return "large";
    }
    return "normal";
}
function focusNodeFromHost(request) {
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
function clearSelection(notifyHost) {
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
function hideSelectedNode() {
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
function notifyHiddenNodesChanged() {
    postHostMessage({
        type: "hidden-nodes-changed",
        hiddenNodes: state.hiddenNodes
    });
}
function applyLocaleFromHost(locale) {
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
function tryApplyPendingFocusRequest() {
    const pendingRequest = state.pendingFocusRequest;
    if (!pendingRequest) {
        return;
    }
    state.pendingFocusRequest = null;
    focusNodeFromHost(pendingRequest);
}
function resolveNodeForFocus(request) {
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
    const exactMatches = state.cy.nodes().filter((node) => String(node.data("label") ?? "") === request.label);
    if (exactMatches.length > 0) {
        return exactMatches[0];
    }
    const prefixMatches = state.cy
        .nodes()
        .filter((node) => String(node.data("label") ?? "").startsWith(`${request.label}:`));
    if (prefixMatches.length > 0) {
        return prefixMatches[0];
    }
    return directMatch;
}
function ensureNodeVisibilityForFocus(nodeId, label) {
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
function resolveNodePayloadForFocus(nodeId, label) {
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
function setToggleState(toggle, stateKey, value) {
    if (state[stateKey] === value) {
        return false;
    }
    state[stateKey] = value;
    if (toggle) {
        toggle.checked = value;
    }
    return true;
}
function applyViewStateFromHost(viewState) {
    let shouldRerender = false;
    let shouldResizeViewport = false;
    shouldRerender = applyToggleFromViewState(viewState.includeProjects, showProjectsToggle, "includeProjects") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includeDocuments, showDocumentsToggle, "includeDocuments") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includePackages, showPackagesToggle, "includePackages") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includeSymbols, showSymbolsToggle, "includeSymbols") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includeAssemblies, showAssembliesToggle, "includeAssemblies") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includeNativeDependencies, showNativeDependenciesToggle, "includeNativeDependencies") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includeDocumentDependencies, showDocumentDependenciesToggle, "includeDocumentDependencies") || shouldRerender;
    shouldRerender = applyToggleFromViewState(viewState.includeSymbolDependencies, showSymbolDependenciesToggle, "includeSymbolDependencies") || shouldRerender;
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
    if (typeof viewState.dependencyMapDirection === "string" &&
        isDependencyMapDirection(viewState.dependencyMapDirection) &&
        state.dependencyMapDirection !== viewState.dependencyMapDirection) {
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
function applyToggleFromViewState(value, toggle, stateKey) {
    if (typeof value !== "boolean") {
        return false;
    }
    return setToggleState(toggle, stateKey, value);
}
function buildGraphElements(payload) {
    const elements = [];
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
function shouldNodeBeVisible(nodeData) {
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
function shouldEdgeBeVisible(edgeData, visibleNodeIds) {
    const kind = String(edgeData.kind ?? "");
    if (kind === "contains-symbol" && !state.includeSymbols) {
        return false;
    }
    if (kind === "document-reference" && !state.includeDocumentDependencies) {
        return false;
    }
    if ((kind === "symbol-reference" ||
        kind === "symbol-call" ||
        kind === "symbol-inheritance" ||
        kind === "symbol-implementation" ||
        kind === "symbol-creation") &&
        (!state.includeSymbols || !state.includeSymbolDependencies)) {
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
function syncSymbolKindVisibility(payload) {
    const nextVisibility = {};
    const kinds = collectSortedSymbolKinds(payload);
    for (const kind of kinds) {
        if (kind in nextVisibility) {
            continue;
        }
        nextVisibility[kind] = state.symbolKindVisibility[kind] ?? true;
    }
    state.symbolKindVisibility = nextVisibility;
}
function renderSymbolTypeFilters(payload) {
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
function resolveSymbolKindLabel(kind) {
    const localized = symbolKindNames[state.locale]?.[kind];
    if (localized) {
        return localized;
    }
    if (kind.trim().length === 0) {
        return symbolKindNames[state.locale]?.Unknown ?? kind;
    }
    return kind.replace(/Declaration$/u, "");
}
function collectSortedSymbolKinds(payload) {
    return [...new Set(payload.nodes
            .filter((node) => node.group === "symbol")
            .map((node) => node.symbolKind ?? "Unknown"))].sort((left, right) => left.localeCompare(right));
}
function applySearchHighlights() {
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
        .filter((node) => isSearchTargetGroup(node.data("group")));
    const matches = allTargets.filter((node) => {
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
    state.searchMatches = matches.map((node) => node.id());
    updateSearchResultText(matches.length);
}
function focusSearchMatches() {
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
function updateSearchResultText(matchCount) {
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
function applyDependencyMapForSelection() {
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
    const dependencySeedNodes = collectDependencyMapSeedNodes(selectedNode, selectionSeedNodes, state.dependencyMapDirection);
    const dependencyEdges = getDependencyEdgesForSeedNodes(dependencySeedNodes, state.dependencyMapDirection);
    const neighborhood = buildSelectionNeighborhood(selectedNode, dependencySeedNodes, dependencyEdges);
    state.cy.elements().addClass("filtered-out");
    revealFocusedNeighborhood(neighborhood);
    neighborhood.removeClass("filtered-out");
    applyGraphPerformanceModeFromCurrentVisibility();
    applyDependencyMapLayout(selectedNode, dependencySeedNodes, dependencyEdges, neighborhood);
    fitGraphViewportToCollection(neighborhood, 88);
    forceGraphRender();
}
function setDependencyMapMode(nextValue) {
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
function setImpactAnalysisMode(nextValue) {
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
function applyImpactAnalysisForSelection() {
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
    const neighborhood = collectImpactNeighborhood(selectedNode, selectionSeedNodes, state.dependencyMapDirection, {
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
function collectImpactNeighborhood(selectedNode, selectionSeedNodes, direction, edgeQueryOptions = {}) {
    if (!state.cy) {
        return selectedNode;
    }
    const visitedNodes = new Set();
    const visitedEdges = state.cy.collection();
    const queue = [];
    let queueIndex = 0;
    selectionSeedNodes.forEach((seedNode) => {
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
        edges.forEach((edge) => {
            visitedEdges.merge(edge);
            const endpoints = edge.connectedNodes();
            endpoints.forEach((candidate) => {
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
function getImpactNodeCount(selectedNode, direction) {
    if (!state.cy) {
        return 0;
    }
    const cacheKey = `${direction}:${selectedNode.id()}`;
    const cached = impactNodeCountCache.get(cacheKey);
    if (typeof cached === "number") {
        return cached;
    }
    const selectionSeedNodes = getSelectionSeedNodes(selectedNode);
    const visitedNodes = new Set();
    const queue = [];
    let queueIndex = 0;
    selectionSeedNodes.forEach((seedNode) => {
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
        edges.forEach((edge) => {
            edge.connectedNodes().forEach((candidate) => {
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
function getDependencyEdgesByDirection(selectedNode, direction, options = {}) {
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
function filterDependencyEdges(edges, options) {
    return edges.filter((edge) => (isDependencyEdgeKind(String(edge.data("kind") ?? "")) &&
        isDependencyEdgeQueryable(edge, options)));
}
function isDependencyEdgeQueryable(edge, options) {
    if (!options.includeStateHidden && edge.hasClass("state-hidden") && !edge.hasClass("focus-visible")) {
        return false;
    }
    if (!options.includeFilteredOut && edge.hasClass("filtered-out")) {
        return false;
    }
    return true;
}
function clearDependencyMapClasses(updatePerformanceMode = true) {
    if (!state.cy) {
        return;
    }
    state.cy.elements().removeClass("focus-visible");
    state.cy.elements().removeClass("filtered-out");
    if (updatePerformanceMode) {
        applyGraphPerformanceModeFromCurrentVisibility();
    }
}
function captureFocusedModePositionSnapshotIfNeeded() {
    if (!state.cy || state.focusedModePositions) {
        return;
    }
    const positions = new Map();
    state.cy.nodes().not(".state-hidden").forEach((node) => {
        const position = node.position();
        positions.set(String(node.id()), {
            x: Number(position.x ?? 0),
            y: Number(position.y ?? 0)
        });
    });
    state.focusedModePositions = positions;
}
function restoreFocusedModePositionSnapshotIfNeeded() {
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
function discardFocusedModePositionSnapshot() {
    state.focusedModePositions = null;
}
function revealFocusedNeighborhood(neighborhood) {
    if (!state.cy || !neighborhood) {
        return;
    }
    neighborhood.addClass("focus-visible");
}
function pinSelectedNode() {
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
function clearPinnedNodes() {
    if (!state.cy) {
        state.pinnedNodes = [];
        return;
    }
    state.cy.nodes(".pinned").forEach((node) => {
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
function capturePinnedNodeState() {
    if (!state.cy) {
        return normalizePinnedNodes(state.pinnedNodes);
    }
    const pinnedNodes = [];
    state.cy.nodes(".pinned").forEach((node) => {
        pinnedNodes.push({
            nodeId: node.id(),
            x: Number(node.position("x") ?? 0),
            y: Number(node.position("y") ?? 0)
        });
    });
    state.pinnedNodes = normalizePinnedNodes(pinnedNodes);
    return state.pinnedNodes;
}
function applyPinnedNodeState(pinnedNodes) {
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
function normalizePinnedNodes(rawPinnedNodes) {
    if (!Array.isArray(rawPinnedNodes)) {
        return [];
    }
    const normalized = [];
    const seen = new Set();
    for (const rawPinnedNode of rawPinnedNodes) {
        if (typeof rawPinnedNode !== "object" || rawPinnedNode === null) {
            continue;
        }
        const candidate = rawPinnedNode;
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
function normalizeHiddenNodes(rawHiddenNodes) {
    if (!Array.isArray(rawHiddenNodes)) {
        return [];
    }
    const normalized = [];
    const seen = new Set();
    for (const rawHiddenNode of rawHiddenNodes) {
        if (typeof rawHiddenNode !== "object" || rawHiddenNode === null) {
            continue;
        }
        const candidate = rawHiddenNode;
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
function setHiddenNodes(hiddenNodes) {
    state.hiddenNodes = hiddenNodes;
    state.hiddenNodeIds = new Set(hiddenNodes.map((hiddenNode) => hiddenNode.nodeId));
}
function upsertPinnedNodeState(pinnedNodes, nextPinnedNode) {
    const normalized = normalizePinnedNodes(pinnedNodes);
    const updated = normalized.filter((candidate) => candidate.nodeId !== nextPinnedNode.nodeId);
    updated.push(nextPinnedNode);
    return updated.sort((left, right) => left.nodeId.localeCompare(right.nodeId));
}
function createCurrentViewState() {
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
function exportCurrentViewState() {
    const payload = JSON.stringify(createCurrentViewState(), null, 2);
    const blob = new Blob([payload], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `codemap-view-${new Date().toISOString().replace(/[:.]/g, "-")}.json`;
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 0);
}
async function importViewStateFromFile(file) {
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
async function copyCurrentViewStateToClipboard() {
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
function fitGraphViewport(padding) {
    if (!state.cy) {
        return;
    }
    const elements = state.cy.elements().not(".state-hidden").not(".filtered-out");
    if (elements.length === 0) {
        return;
    }
    fitGraphViewportToCollection(elements, padding);
}
function resolveNodeReferenceStats(node) {
    const incomingEdges = node.incomers("edge").filter((edge) => isDependencyEdgeKind(String(edge.data("kind") ?? "")));
    const outgoingEdges = node.outgoers("edge").filter((edge) => isDependencyEdgeKind(String(edge.data("kind") ?? "")));
    return {
        incomingEdges,
        outgoingEdges,
        incomingWeight: sumEdgeWeights(incomingEdges),
        outgoingWeight: sumEdgeWeights(outgoingEdges)
    };
}
function resolveInspectorReferenceStats(node) {
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
function getSelectionSeedNodes(selectedNode) {
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
function collectDependencyMapSeedNodes(selectedNode, selectionSeedNodes, direction, options = {}) {
    if (!state.cy) {
        return selectionSeedNodes;
    }
    let relevantSeedNodes = state.cy.collection().union(selectedNode);
    selectionSeedNodes.forEach((seedNode) => {
        if (seedNode.id() === selectedNode.id()) {
            return;
        }
        if (getDependencyEdgesByDirection(seedNode, direction, options).length > 0) {
            relevantSeedNodes = relevantSeedNodes.union(seedNode);
        }
    });
    return relevantSeedNodes;
}
function getContainedSymbolNodes(documentNode) {
    return documentNode.outgoers("edge[kind = 'contains-symbol']").targets();
}
function getOwnerDocumentNodes(symbolNode) {
    return symbolNode.incomers("edge[kind = 'contains-symbol']").sources();
}
function getOwnerProjectNodes(documentNode) {
    return documentNode.incomers("edge[kind = 'contains-document']").sources();
}
function getDependencyEdgesForSeedNodes(seedNodes, direction, options = {}) {
    if (!state.cy) {
        return seedNodes;
    }
    let edges = state.cy.collection();
    seedNodes.forEach((seedNode) => {
        edges = edges.union(getDependencyEdgesByDirection(seedNode, direction, options));
    });
    return edges;
}
function buildSelectionNeighborhood(selectedNode, selectionSeedNodes, dependencyEdges) {
    if (!state.cy) {
        return selectedNode;
    }
    let neighborhood = selectionSeedNodes.union(dependencyEdges).union(dependencyEdges.connectedNodes());
    neighborhood = neighborhood.union(collectOwnershipContext(neighborhood.nodes()));
    neighborhood = neighborhood.union(selectedNode);
    return neighborhood;
}
function collectOwnershipContext(nodes) {
    if (!state.cy) {
        return nodes;
    }
    let context = state.cy.collection();
    nodes.forEach((node) => {
        const group = String(node.data("group") ?? "");
        if (group === "symbol") {
            const ownershipEdges = node
                .incomers("edge[kind = 'contains-symbol']")
                .filter((edge) => !edge.hasClass("state-hidden"));
            const documentNodes = ownershipEdges
                .sources()
                .filter((documentNode) => !documentNode.hasClass("state-hidden"));
            context = context.union(ownershipEdges).union(documentNodes);
            documentNodes.forEach((documentNode) => {
                if (!state.includeProjects) {
                    return;
                }
                const projectEdges = documentNode
                    .incomers("edge[kind = 'contains-document']")
                    .filter((edge) => !edge.hasClass("state-hidden"));
                const projectNodes = projectEdges
                    .sources()
                    .filter((projectNode) => !projectNode.hasClass("state-hidden"));
                context = context.union(projectEdges).union(projectNodes);
            });
            return;
        }
        if (group === "document" && state.includeProjects) {
            const projectEdges = node
                .incomers("edge[kind = 'contains-document']")
                .filter((edge) => !edge.hasClass("state-hidden"));
            const projectNodes = projectEdges
                .sources()
                .filter((projectNode) => !projectNode.hasClass("state-hidden"));
            context = context.union(projectEdges).union(projectNodes);
        }
    });
    return context;
}
function sumEdgeWeights(edges) {
    let total = 0;
    edges.forEach((edge) => {
        total += Math.max(1, Number(edge.data("weight") ?? 1));
    });
    return total;
}
function resolveNodeTypeLabel(data) {
    const group = String(data.group ?? "");
    if (group === "symbol") {
        return resolveSymbolKindLabel(String(data.symbolKind ?? "Unknown"));
    }
    return resolveGroupLabel(group);
}
function resolveNodeDisplayFileName(data) {
    if (typeof data.fileName === "string" && data.fileName.trim().length > 0) {
        return data.fileName;
    }
    return t("tooltip.unavailable");
}
function resolveDisplayPath(filePath, lineNumber) {
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
function localizeConfidence(value) {
    if (typeof value !== "string" || value.trim().length === 0) {
        return t("tooltip.unavailable");
    }
    const key = `confidence.${value}`;
    const localized = t(key);
    return localized === key ? value : localized;
}
function localizeReferenceKind(value) {
    if (typeof value !== "string" || value.trim().length === 0) {
        return t("tooltip.unavailable");
    }
    const key = `edge.referenceKind.${value}`;
    const localized = t(key);
    return localized === key ? value : localized;
}
function resolveEdgeKindLabel(kind) {
    const key = `edge.kind.${kind}`;
    const localized = t(key);
    return localized === key ? kind : localized;
}
function resolveEdgeDescription(kind) {
    const key = `edge.description.${kind}`;
    const localized = t(key);
    return localized === key ? kind : localized;
}
function localizeYesNo(value) {
    return value ? t("state.yes") : t("state.no");
}
function showNodeHoverCard(node, originalEvent) {
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
function moveNodeHoverCard(originalEvent) {
    moveHoverCard(nodeHoverCardEl, originalEvent);
}
function hideNodeHoverCard() {
    hideHoverCard(nodeHoverCardEl);
}
function showEdgeHoverCard(edge, originalEvent) {
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
        renderHoverField(t("edge.location"), resolveDisplayPath(sampleFilePath, Number.isFinite(sampleLineNumber) && sampleLineNumber > 0 ? sampleLineNumber : null)),
        `<div class="edge-hover-section-label">${escapeHtml(t("edge.example"))}</div>`,
        exampleHtml
    ].join(""), originalEvent);
}
function moveEdgeHoverCard(originalEvent) {
    moveHoverCard(edgeHoverCardEl, originalEvent);
}
function hideEdgeHoverCard() {
    hideHoverCard(edgeHoverCardEl);
}
function showHoverCard(cardEl, contentHtml, originalEvent) {
    if (!cardEl) {
        return;
    }
    cardEl.innerHTML = contentHtml;
    cardEl.hidden = false;
    moveHoverCard(cardEl, originalEvent);
}
function moveHoverCard(cardEl, originalEvent) {
    if (!cardEl || cardEl.hidden) {
        return;
    }
    positionHoverCard(cardEl, originalEvent);
}
function hideHoverCard(cardEl) {
    if (!cardEl) {
        return;
    }
    cardEl.hidden = true;
}
function positionHoverCard(cardEl, originalEvent) {
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
function renderHoverField(label, value) {
    return [
        "<div class=\"node-hover-row\">",
        `<span class="node-hover-label">${escapeHtml(label)}</span>`,
        `<span class="node-hover-value">${escapeHtml(value)}</span>`,
        "</div>"
    ].join("");
}
function updateInspector(node) {
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
    inspectorEl.querySelectorAll(".inspector-ref-button").forEach((button) => {
        button.addEventListener("click", () => {
            const nodeId = button.dataset.nodeId;
            if (!nodeId) {
                return;
            }
            focusInspectorReferenceNode(nodeId);
        });
    });
}
function buildReferenceItems(edges, direction) {
    const entries = [];
    const seen = new Set();
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
function resetInspector() {
    if (!inspectorEl) {
        return;
    }
    inspectorEl.innerHTML = `<div class="inspector-empty">${escapeHtml(t("inspector.empty"))}</div>`;
}
function renderInspectorField(label, value) {
    return [
        "<div class=\"inspector-field\">",
        `<div class="inspector-field-label">${escapeHtml(label)}</div>`,
        `<div class="inspector-field-value">${escapeHtml(value)}</div>`,
        "</div>"
    ].join("");
}
function renderInspectorMetric(label, value) {
    return [
        "<div class=\"inspector-metric\">",
        `<div class="inspector-metric-label">${escapeHtml(label)}</div>`,
        `<div class="inspector-metric-value">${escapeHtml(String(value))}</div>`,
        "</div>"
    ].join("");
}
function renderReferenceList(items) {
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
function focusInspectorReferenceNode(nodeId) {
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
function escapeHtml(value) {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}
function isSearchTargetGroup(group) {
    return group === "document" || group === "symbol";
}
function isExternalDependencyGroup(group) {
    return group === "package" || group === "assembly" || group === "dll";
}
function isDependencyEdgeKind(kind) {
    return (kind === "document-reference" ||
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
function resolveGroupLabel(group) {
    return t(`group.${group}`);
}
function isDependencyMapDirection(value) {
    return value === "incoming" || value === "outgoing" || value === "both";
}
function resolveLocale() {
    const rawLang = document.documentElement.lang?.trim().toLowerCase() ?? "";
    if (rawLang.startsWith("en")) {
        return "en";
    }
    return "ja";
}
function t(key) {
    const localeTable = translations[state.locale] ?? translations.ja;
    return localeTable[key] ?? translations.ja[key] ?? key;
}
function formatTemplate(template, ...args) {
    let output = template;
    for (let index = 0; index < args.length; index += 1) {
        output = output.replace(`{${index}}`, args[index]);
    }
    return output;
}
function applyLocalizedText() {
    document.documentElement.lang = state.locale;
    document.title = t("page.title");
    const localizedElements = document.querySelectorAll("[data-i18n]");
    for (const element of localizedElements) {
        const key = element.dataset.i18n;
        if (!key) {
            continue;
        }
        element.textContent = t(key);
    }
    const localizedPlaceholderElements = document.querySelectorAll("[data-i18n-placeholder]");
    for (const element of localizedPlaceholderElements) {
        const key = element.dataset.i18nPlaceholder;
        if (!key) {
            continue;
        }
        if (element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement) {
            element.placeholder = t(key);
        }
    }
    const localizedAriaLabelElements = document.querySelectorAll("[data-i18n-aria-label]");
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
function applyHostTheme(theme) {
    state.hostTheme = theme;
    if (theme === "system") {
        document.documentElement.removeAttribute("data-host-theme");
        return;
    }
    document.documentElement.setAttribute("data-host-theme", theme);
}
function initializePanelWidth() {
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
function initializeMobilePanelHeight() {
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
function initializePanelSplitter() {
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
function initializeMobilePanelSplitter() {
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
function onSplitterPointerDown(event) {
    beginSplitterDrag(event, panelSplitterEl, panelSplitterPointerState, !isCompactLayout(), panelSplitterHandlers, (pointerEvent) => {
        updatePanelWidthFromPointer(pointerEvent.clientX);
    });
}
function onSplitterPointerMove(event) {
    continueSplitterDrag(event, activePointerId, !isCompactLayout(), (pointerEvent) => {
        updatePanelWidthFromPointer(pointerEvent.clientX);
    });
}
function onSplitterPointerUp(event) {
    if (event.pointerId !== activePointerId) {
        return;
    }
    finishSplitterDrag();
}
function onSplitterPointerCancel(event) {
    if (event.pointerId !== activePointerId) {
        return;
    }
    finishSplitterDrag();
}
function onSplitterPointerLostCapture(event) {
    if (event.pointerId !== activePointerId) {
        return;
    }
    finishSplitterDrag();
}
function onMobileSplitterPointerDown(event) {
    beginSplitterDrag(event, mobilePanelSplitterEl, mobilePanelSplitterPointerState, isCompactLayout(), mobilePanelSplitterHandlers, (pointerEvent) => {
        updateMobilePanelHeightFromPointer(pointerEvent.clientY);
    });
}
function onMobileSplitterPointerMove(event) {
    continueSplitterDrag(event, mobileActivePointerId, isCompactLayout(), (pointerEvent) => {
        updateMobilePanelHeightFromPointer(pointerEvent.clientY);
    });
}
function onMobileSplitterPointerUp(event) {
    if (event.pointerId !== mobileActivePointerId) {
        return;
    }
    finishMobileSplitterDrag();
}
function onMobileSplitterPointerCancel(event) {
    if (event.pointerId !== mobileActivePointerId) {
        return;
    }
    finishMobileSplitterDrag();
}
function onMobileSplitterPointerLostCapture(event) {
    if (event.pointerId !== mobileActivePointerId) {
        return;
    }
    finishMobileSplitterDrag();
}
function finishSplitterDrag() {
    finishSplitterDragSession(panelSplitterEl, panelSplitterPointerState, panelSplitterHandlers, persistPanelWidth);
}
function finishMobileSplitterDrag() {
    finishSplitterDragSession(mobilePanelSplitterEl, mobilePanelSplitterPointerState, mobilePanelSplitterHandlers, persistMobilePanelHeight);
}
function beginSplitterDrag(event, element, pointerState, isEnabled, handlers, updateFromPointer) {
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
function continueSplitterDrag(event, activeId, isEnabled, updateFromPointer) {
    if (event.pointerId !== activeId || !isEnabled) {
        return;
    }
    updateFromPointer(event);
    requestLiveResizePreview();
}
function finishSplitterDragSession(element, pointerState, handlers, persistSize) {
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
function updatePanelWidthFromPointer(pointerX) {
    if (!workspaceEl) {
        return;
    }
    const workspaceRect = workspaceEl.getBoundingClientRect();
    const nextWidth = workspaceRect.right - pointerX;
    setPanelWidth(nextWidth);
}
function setPanelWidth(panelWidth) {
    if (!workspaceEl || !Number.isFinite(panelWidth)) {
        return;
    }
    const workspaceWidth = workspaceEl.getBoundingClientRect().width;
    const dynamicMax = Math.max(MIN_PANEL_WIDTH, Math.min(MAX_PANEL_WIDTH, workspaceWidth * 0.6));
    const clampedWidth = Math.round(Math.max(MIN_PANEL_WIDTH, Math.min(dynamicMax, panelWidth)));
    state.panelWidth = clampedWidth;
    document.documentElement.style.setProperty("--panel-width", `${clampedWidth}px`);
}
function updateMobilePanelHeightFromPointer(pointerY) {
    if (!workspaceEl) {
        return;
    }
    const workspaceRect = workspaceEl.getBoundingClientRect();
    const nextHeight = workspaceRect.bottom - pointerY;
    setMobilePanelHeight(nextHeight);
}
function setMobilePanelHeight(panelHeight) {
    if (!workspaceEl || !Number.isFinite(panelHeight)) {
        return;
    }
    const workspaceHeight = workspaceEl.getBoundingClientRect().height;
    if (workspaceHeight <= 1) {
        return;
    }
    const dynamicMax = Math.max(MIN_MOBILE_PANEL_HEIGHT, workspaceHeight - MIN_MOBILE_GRAPH_HEIGHT - MOBILE_SPLITTER_HEIGHT);
    const clampedHeight = Math.round(Math.max(MIN_MOBILE_PANEL_HEIGHT, Math.min(dynamicMax, panelHeight)));
    state.mobilePanelHeight = clampedHeight;
    document.documentElement.style.setProperty("--mobile-panel-height", `${clampedHeight}px`);
}
function requestLiveResizePreview() {
    if (liveResizePreviewFrame !== null) {
        return;
    }
    liveResizePreviewFrame = window.requestAnimationFrame(() => {
        liveResizePreviewFrame = null;
        resizeGraphViewportPreservingTransform(false, true);
    });
}
function resizeGraphViewportPreservingTransform(force = false, suppressForceRender = false) {
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
function cancelLiveResizePreview() {
    if (liveResizePreviewFrame === null) {
        return;
    }
    window.cancelAnimationFrame(liveResizePreviewFrame);
    liveResizePreviewFrame = null;
}
function persistPanelWidth() {
    try {
        window.localStorage.setItem(PANEL_WIDTH_STORAGE_KEY, String(state.panelWidth));
    }
    catch {
        // Ignore storage failures in restricted environments.
    }
}
function persistMobilePanelHeight() {
    try {
        window.localStorage.setItem(MOBILE_PANEL_HEIGHT_STORAGE_KEY, String(state.mobilePanelHeight));
    }
    catch {
        // Ignore storage failures in restricted environments.
    }
}
function isCompactLayout() {
    return window.matchMedia("(max-width: 920px)").matches;
}
function resolveDefaultMobilePanelHeight() {
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
function postHostMessage(message) {
    const webview = getHostWebView();
    if (!webview) {
        return;
    }
    webview.postMessage(message);
}
function installGlobalErrorForwarding() {
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
function parseMessage(raw) {
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
function parseFocusNodeRequest(raw) {
    if (typeof raw !== "object" || raw === null) {
        return null;
    }
    const candidate = raw;
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
function parseViewStateRequest(raw) {
    if (typeof raw !== "object" || raw === null) {
        return null;
    }
    const candidate = raw;
    const viewState = {};
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
function parseLocale(raw) {
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
    const candidate = raw;
    return parseLocale(candidate.locale);
}
function parseHostTheme(raw) {
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
    const candidate = raw;
    return parseHostTheme(candidate.theme);
}
function parseSearchQuery(raw) {
    if (typeof raw === "string") {
        return raw.trim();
    }
    if (typeof raw !== "object" || raw === null) {
        return null;
    }
    const candidate = raw;
    if (typeof candidate.query !== "string") {
        return null;
    }
    return candidate.query.trim();
}
function normalizeHostMessage(raw) {
    if (typeof raw !== "object" || raw === null) {
        return null;
    }
    const candidate = raw;
    if (typeof candidate.type !== "string") {
        return null;
    }
    if (candidate.type !== "render-graph") {
        return candidate;
    }
    if (!isGraphPayload(candidate.data)) {
        return null;
    }
    return {
        ...candidate,
        data: candidate.data
    };
}
function isGraphPayload(raw) {
    if (typeof raw !== "object" || raw === null) {
        return false;
    }
    const payload = raw;
    return Array.isArray(payload.nodes) && Array.isArray(payload.edges) && typeof payload.stats === "object" && payload.stats !== null;
}
function postGraphRendered() {
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
function getStoredPanelWidth() {
    try {
        return window.localStorage.getItem(PANEL_WIDTH_STORAGE_KEY);
    }
    catch {
        return null;
    }
}
function getStoredMobilePanelHeight() {
    try {
        return window.localStorage.getItem(MOBILE_PANEL_HEIGHT_STORAGE_KEY);
    }
    catch {
        return null;
    }
}
function getHostWebView() {
    return window.chrome?.webview;
}
main();
const layoutSequence = ["semantic-flow", "breadthfirst", "cose", "concentric", "grid"];
function cycleLayoutAndApply() {
    state.layoutIndex = (state.layoutIndex + 1) % layoutSequence.length;
    applyLayout(layoutSequence[state.layoutIndex]);
}
function applyPreferredLayoutForPayload(payload) {
    const metrics = collectLayoutSelectionMetrics(payload);
    const preferredLayout = resolvePreferredLayoutName(metrics);
    applyPreferredLayout(preferredLayout, metrics);
}
function collectLayoutSelectionMetrics(payload) {
    const visibleNodeIds = new Set();
    let symbolNodeCount = 0;
    for (const node of payload.nodes) {
        if (!shouldNodeBeVisible(node)) {
            continue;
        }
        visibleNodeIds.add(node.id);
        if (node.group === "symbol") {
            symbolNodeCount += 1;
        }
    }
    const adjacency = new Map();
    for (const nodeId of visibleNodeIds) {
        adjacency.set(nodeId, new Set());
    }
    let visibleEdgeCount = 0;
    const connectedNodeIds = new Set();
    for (const edge of payload.edges) {
        if (!shouldEdgeBeVisible(edge, visibleNodeIds)) {
            continue;
        }
        visibleEdgeCount += 1;
        connectedNodeIds.add(edge.source);
        connectedNodeIds.add(edge.target);
        adjacency.get(edge.source)?.add(edge.target);
        adjacency.get(edge.target)?.add(edge.source);
    }
    let connectedComponentCount = 0;
    let largestComponentSize = 0;
    const visited = new Set();
    for (const nodeId of visibleNodeIds) {
        if (visited.has(nodeId)) {
            continue;
        }
        connectedComponentCount += 1;
        let componentSize = 0;
        const stack = [nodeId];
        visited.add(nodeId);
        while (stack.length > 0) {
            const current = stack.pop();
            componentSize += 1;
            for (const next of adjacency.get(current) ?? []) {
                if (visited.has(next)) {
                    continue;
                }
                visited.add(next);
                stack.push(next);
            }
        }
        largestComponentSize = Math.max(largestComponentSize, componentSize);
    }
    const visibleNodeCount = visibleNodeIds.size;
    const isolatedNodeCount = Math.max(0, visibleNodeCount - connectedNodeIds.size);
    const averageDegree = visibleNodeCount > 0
        ? (visibleEdgeCount * 2) / visibleNodeCount
        : 0;
    return {
        visibleNodeCount,
        visibleEdgeCount,
        symbolNodeCount,
        isolatedNodeCount,
        connectedComponentCount,
        largestComponentSize,
        averageDegree
    };
}
function fitGraphViewportToCollection(elements, padding) {
    if (!state.cy || !elements || elements.length === 0) {
        return;
    }
    state.cy.resize();
    state.cy.fit(elements, padding);
}
function resolveMinimumReadableZoom(visibleNodeCount) {
    if (!state.cy) {
        return 0.56;
    }
    const nodeCount = Math.max(0, visibleNodeCount ?? state.cy.nodes().length);
    if (state.includeSymbols) {
        if (nodeCount >= 3000) {
            return 0.12;
        }
        if (nodeCount >= 1800) {
            return 0.16;
        }
        if (nodeCount >= 900) {
            return 0.24;
        }
        if (nodeCount >= 280) {
            return 0.42;
        }
        return 0.64;
    }
    if (nodeCount >= 1200) {
        return 0.32;
    }
    if (nodeCount >= 400) {
        return 0.52;
    }
    if (nodeCount >= 72) {
        return 0.72;
    }
    return 1.0;
}
function ensureReadableZoom(minimumZoom) {
    if (!state.cy) {
        return;
    }
    const zoom = state.cy.zoom();
    if (zoom >= minimumZoom) {
        return;
    }
    state.cy.zoom({
        level: minimumZoom,
        renderedPosition: {
            x: state.cy.width() / 2,
            y: state.cy.height() / 2
        }
    });
}
function forceGraphRender() {
    if (!state.cy || typeof state.cy.forceRender !== "function") {
        return;
    }
    state.cy.forceRender();
    window.requestAnimationFrame(() => {
        state.cy?.forceRender();
    });
}
function resolvePreferredLayoutName(metrics) {
    if (metrics.visibleEdgeCount === 0) {
        return "structured-massive";
    }
    const isolatedRatio = metrics.visibleNodeCount > 0
        ? metrics.isolatedNodeCount / metrics.visibleNodeCount
        : 0;
    const giantComponentRatio = metrics.visibleNodeCount > 0
        ? metrics.largestComponentSize / metrics.visibleNodeCount
        : 0;
    if (metrics.visibleNodeCount <= 160 &&
        metrics.averageDegree <= 2.2 &&
        giantComponentRatio >= 0.75) {
        return "breadthfirst";
    }
    if (metrics.connectedComponentCount >= 16 &&
        giantComponentRatio < 0.45) {
        return "structured-massive";
    }
    if (metrics.visibleNodeCount >= 2200 &&
        isolatedRatio >= 0.7) {
        return "structured-massive";
    }
    if (metrics.visibleNodeCount >= 900 &&
        giantComponentRatio >= 0.6) {
        return "cose";
    }
    return "semantic-flow";
}
function applyPreferredLayout(layoutName, metrics) {
    if (!state.cy) {
        return;
    }
    syncLayoutIndex(layoutName);
    const visibleElements = state.cy.elements().not(".state-hidden").not(".filtered-out");
    if (visibleElements.length === 0) {
        return;
    }
    if (layoutName === "semantic-flow") {
        applySemanticFlowLayout(metrics);
        finalizeAppliedLayout(visibleElements, metrics);
        return;
    }
    if (layoutName === "structured-massive") {
        applyStructuredMassiveLayout(metrics);
        finalizeAppliedLayout(visibleElements, metrics);
        return;
    }
    applyLayout(layoutName);
}
function applyLayout(layoutName) {
    if (!state.cy) {
        return;
    }
    const visibleElements = state.cy.elements().not(".state-hidden").not(".filtered-out");
    if (visibleElements.length === 0) {
        return;
    }
    const metrics = state.lastPayload
        ? collectLayoutSelectionMetrics(state.lastPayload)
        : collectVisibleGraphMetricsFromCy();
    if (layoutName === "semantic-flow") {
        applySemanticFlowLayout(metrics);
        finalizeAppliedLayout(visibleElements, metrics);
        return;
    }
    if (layoutName === "structured-massive") {
        applyStructuredMassiveLayout(metrics);
        finalizeAppliedLayout(visibleElements, metrics);
        return;
    }
    runCytoscapeLayout(visibleElements, resolveCytoscapeLayoutOptions(layoutName, metrics), metrics);
}
function collectVisibleGraphMetricsFromCy() {
    if (!state.cy) {
        return {
            visibleNodeCount: 0,
            visibleEdgeCount: 0,
            symbolNodeCount: 0,
            isolatedNodeCount: 0,
            connectedComponentCount: 0,
            largestComponentSize: 0,
            averageDegree: 0
        };
    }
    const visibleNodes = state.cy.nodes().not(".state-hidden").not(".filtered-out");
    const visibleEdges = state.cy.edges().not(".state-hidden").not(".filtered-out");
    const visibleNodeIds = new Set();
    let symbolNodeCount = 0;
    visibleNodes.forEach((node) => {
        visibleNodeIds.add(String(node.id()));
        if (String(node.data("group") ?? "") === "symbol") {
            symbolNodeCount += 1;
        }
    });
    const adjacency = new Map();
    for (const nodeId of visibleNodeIds) {
        adjacency.set(nodeId, new Set());
    }
    const connectedNodeIds = new Set();
    visibleEdges.forEach((edge) => {
        const sourceId = String(edge.source().id());
        const targetId = String(edge.target().id());
        if (!visibleNodeIds.has(sourceId) || !visibleNodeIds.has(targetId)) {
            return;
        }
        connectedNodeIds.add(sourceId);
        connectedNodeIds.add(targetId);
        adjacency.get(sourceId)?.add(targetId);
        adjacency.get(targetId)?.add(sourceId);
    });
    let connectedComponentCount = 0;
    let largestComponentSize = 0;
    const visited = new Set();
    for (const nodeId of visibleNodeIds) {
        if (visited.has(nodeId)) {
            continue;
        }
        connectedComponentCount += 1;
        let componentSize = 0;
        const stack = [nodeId];
        visited.add(nodeId);
        while (stack.length > 0) {
            const current = stack.pop();
            componentSize += 1;
            for (const next of adjacency.get(current) ?? []) {
                if (visited.has(next)) {
                    continue;
                }
                visited.add(next);
                stack.push(next);
            }
        }
        largestComponentSize = Math.max(largestComponentSize, componentSize);
    }
    const visibleNodeCount = visibleNodes.length;
    const visibleEdgeCount = visibleEdges.length;
    const isolatedNodeCount = Math.max(0, visibleNodeCount - connectedNodeIds.size);
    const averageDegree = visibleNodeCount > 0
        ? (visibleEdgeCount * 2) / visibleNodeCount
        : 0;
    return {
        visibleNodeCount,
        visibleEdgeCount,
        symbolNodeCount,
        isolatedNodeCount,
        connectedComponentCount,
        largestComponentSize,
        averageDegree
    };
}
function resolveCytoscapeLayoutOptions(layoutName, metrics) {
    switch (layoutName) {
        case "grid":
            return {
                name: "grid",
                animate: false,
                fit: false,
                avoidOverlap: true,
                avoidOverlapPadding: metrics.visibleNodeCount >= 800 ? 24 : 16,
                nodeDimensionsIncludeLabels: true,
                spacingFactor: metrics.visibleNodeCount >= 800 ? 1.18 : 1.32
            };
        case "breadthfirst":
            return {
                name: "breadthfirst",
                animate: false,
                fit: false,
                directed: true,
                avoidOverlap: true,
                nodeDimensionsIncludeLabels: true,
                spacingFactor: metrics.visibleNodeCount >= 120 ? 1.4 : 1.75
            };
        case "concentric":
            return {
                name: "concentric",
                animate: false,
                fit: false,
                avoidOverlap: true,
                nodeDimensionsIncludeLabels: true,
                minNodeSpacing: metrics.visibleNodeCount >= 400 ? 18 : 28,
                spacingFactor: 1.12,
                concentric: (node) => {
                    const group = String(node.data("group") ?? "");
                    switch (group) {
                        case "project":
                            return 4;
                        case "document":
                            return 3;
                        case "symbol":
                            return 2;
                        default:
                            return 1;
                    }
                },
                levelWidth: () => 1
            };
        default:
            return {
                name: "cose",
                animate: false,
                fit: false,
                randomize: false,
                componentSpacing: metrics.visibleNodeCount >= 1000 ? 160 : 96,
                nodeDimensionsIncludeLabels: true,
                nodeRepulsion: (node) => {
                    const group = String(node.data("group") ?? "");
                    switch (group) {
                        case "project":
                            return 26000;
                        case "document":
                            return 16000;
                        case "symbol":
                            return state.includeSymbols ? 4200 : 7000;
                        default:
                            return 11000;
                    }
                },
                idealEdgeLength: (edge) => {
                    const kind = String(edge.data("kind") ?? "");
                    switch (kind) {
                        case "contains-document":
                            return 180;
                        case "contains-symbol":
                            return state.includeSymbols ? 96 : 72;
                        case "project-reference":
                            return 220;
                        case "document-reference":
                            return 160;
                        case "symbol-call":
                            return 120;
                        case "symbol-inheritance":
                        case "symbol-implementation":
                            return 140;
                        default:
                            return 100;
                    }
                },
                edgeElasticity: (edge) => {
                    const kind = String(edge.data("kind") ?? "");
                    switch (kind) {
                        case "contains-document":
                        case "contains-symbol":
                            return 0.9;
                        case "project-reference":
                            return 0.45;
                        default:
                            return 0.6;
                    }
                },
                numIter: metrics.visibleNodeCount >= 1000 ? 650 : 1000
            };
    }
}
function runCytoscapeLayout(elements, options, metrics) {
    if (!state.cy || !elements || elements.length === 0) {
        return;
    }
    const layout = elements.layout({
        ...options,
        stop: () => {
            finalizeAppliedLayout(elements, metrics);
        }
    });
    layout.run();
}
function finalizeAppliedLayout(elements, metrics) {
    if (!state.cy) {
        return;
    }
    separateOverlappingNodeCollisions();
    if (metrics.visibleNodeCount <= 260) {
        separateOverlappingHighLevelNodes();
    }
    fitGraphViewportToCollection(elements, resolveViewportPadding(metrics));
    ensureReadableZoom(resolveMinimumReadableZoom(metrics.visibleNodeCount));
    forceGraphRender();
}
function resolveViewportPadding(metrics) {
    if (metrics.visibleNodeCount >= 1400) {
        return 120;
    }
    if (metrics.visibleNodeCount >= 500) {
        return 84;
    }
    return 56;
}
function applyStructuredMassiveLayout(metrics) {
    if (!state.cy) {
        return;
    }
    const visibleNodes = state.cy.nodes().filter((node) => shouldNodeBeVisible(node.data()));
    const projectNodes = visibleNodes.filter((node) => String(node.data("group") ?? "") === "project");
    const documentNodes = visibleNodes.filter((node) => String(node.data("group") ?? "") === "document");
    const symbolNodes = visibleNodes.filter((node) => String(node.data("group") ?? "") === "symbol");
    const externalNodes = visibleNodes.filter((node) => isExternalDependencyGroup(String(node.data("group") ?? "")));
    state.cy.startBatch();
    layoutHorizontalStrip(projectNodes, -360, 260, metrics.visibleNodeCount >= 1200 ? 4 : 6, 120);
    layoutHorizontalStrip(documentNodes, -120, 188, metrics.visibleNodeCount >= 1200 ? 6 : 8, 92);
    layoutExternalDependencyStrip(externalNodes, metrics);
    layoutSymbolSections(symbolNodes, metrics);
    state.cy.endBatch();
}
function layoutHorizontalStrip(nodes, y, columnGap, maxColumns = 6, rowGap = 88) {
    if (!nodes || nodes.length === 0) {
        return;
    }
    const ordered = nodes.toArray().sort(compareNodeLabels);
    const rows = chunkPlacements(ordered, Math.max(1, maxColumns));
    const rowOffset = (rows.length - 1) / 2;
    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const row = rows[rowIndex];
        const xPositions = buildCenteredAxisPositions(row.length, columnGap);
        const rowY = y + (rowIndex - rowOffset) * rowGap;
        for (let columnIndex = 0; columnIndex < row.length; columnIndex += 1) {
            positionNodePreservingLock(row[columnIndex], xPositions[columnIndex], rowY);
        }
    }
}
function layoutExternalDependencyStrip(nodes, metrics) {
    if (!nodes || nodes.length === 0) {
        return;
    }
    const ordered = nodes.toArray().sort(compareNodeLabels);
    const rowGap = 84;
    const columnGap = 164;
    const maxColumns = metrics.visibleNodeCount >= 1200 ? 8 : 6;
    const chunks = chunkPlacements(ordered, maxColumns);
    for (let rowIndex = 0; rowIndex < chunks.length; rowIndex += 1) {
        const row = chunks[rowIndex];
        const xPositions = buildCenteredAxisPositions(row.length, columnGap);
        const y = 360 + rowIndex * rowGap;
        for (let columnIndex = 0; columnIndex < row.length; columnIndex += 1) {
            positionNodePreservingLock(row[columnIndex], xPositions[columnIndex], y);
        }
    }
}
function separateOverlappingExternalDependencyNodes() {
    if (!state.cy) {
        return;
    }
    const externalNodes = state.cy
        .nodes()
        .not(".state-hidden")
        .not(".filtered-out")
        .not(".pinned")
        .filter((node) => isExternalDependencyGroup(String(node.data("group") ?? "")));
    if (externalNodes.length < 2) {
        return;
    }
    const collisionGroups = new Map();
    externalNodes.forEach((node) => {
        const position = node.position();
        const x = Number(position.x ?? 0);
        const y = Number(position.y ?? 0);
        const key = `${Math.round(x)}:${Math.round(y)}`;
        const group = collisionGroups.get(key);
        if (group) {
            group.push(node);
            return;
        }
        collisionGroups.set(key, [node]);
    });
    let hasCollisions = false;
    state.cy.startBatch();
    for (const group of collisionGroups.values()) {
        if (group.length < 2) {
            continue;
        }
        hasCollisions = true;
        const orderedNodes = group.sort(compareExternalDependencyNodes);
        const anchor = orderedNodes[0].position();
        const rows = chunkPlacements(orderedNodes, 3);
        const rowOffset = (rows.length - 1) / 2;
        for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
            const row = rows[rowIndex];
            const xPositions = buildCenteredAxisPositions(row.length, 172);
            const y = Number(anchor.y ?? 0) + (rowIndex - rowOffset) * 70;
            for (let columnIndex = 0; columnIndex < row.length; columnIndex += 1) {
                positionNodePreservingLock(row[columnIndex], Number(anchor.x ?? 0) + xPositions[columnIndex], y);
            }
        }
    }
    state.cy.endBatch();
    if (hasCollisions) {
        forceGraphRender();
    }
}
function separateOverlappingCollapsedNodes() {
    if (!state.cy) {
        return;
    }
    const movableNodes = state.cy
        .nodes()
        .not(".state-hidden")
        .not(".filtered-out")
        .not(".pinned")
        .filter((node) => !isExternalDependencyGroup(String(node.data("group") ?? "")));
    if (movableNodes.length < 2) {
        return;
    }
    const collisionGroups = new Map();
    movableNodes.forEach((node) => {
        const position = node.position();
        const x = Number(position.x ?? 0);
        const y = Number(position.y ?? 0);
        const key = `${Math.round(x)}:${Math.round(y)}`;
        const group = collisionGroups.get(key);
        if (group) {
            group.push(node);
            return;
        }
        collisionGroups.set(key, [node]);
    });
    let hasCollisions = false;
    state.cy.startBatch();
    for (const group of collisionGroups.values()) {
        if (group.length < 2) {
            continue;
        }
        hasCollisions = true;
        const orderedNodes = group.sort(compareNodeLabels);
        const anchor = orderedNodes[0].position();
        const columnCount = Math.max(2, Math.min(16, Math.ceil(Math.sqrt(orderedNodes.length))));
        const rows = chunkPlacements(orderedNodes, columnCount);
        const rowOffset = (rows.length - 1) / 2;
        const columnGap = orderedNodes.length >= 80 ? 94 : 128;
        const rowGap = orderedNodes.length >= 80 ? 54 : 72;
        for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
            const row = rows[rowIndex];
            const xPositions = buildCenteredAxisPositions(row.length, columnGap);
            const y = Number(anchor.y ?? 0) + (rowIndex - rowOffset) * rowGap;
            for (let columnIndex = 0; columnIndex < row.length; columnIndex += 1) {
                positionNodePreservingLock(row[columnIndex], Number(anchor.x ?? 0) + xPositions[columnIndex], y);
            }
        }
    }
    state.cy.endBatch();
    if (hasCollisions) {
        forceGraphRender();
    }
}
function separateOverlappingDocumentNodes() {
    if (!state.cy) {
        return;
    }
    const documentNodes = state.cy
        .nodes()
        .not(".state-hidden")
        .not(".filtered-out")
        .not(".pinned")
        .filter((node) => String(node.data("group") ?? "") === "document");
    if (documentNodes.length < 2) {
        return;
    }
    const collisionGroups = new Map();
    documentNodes.forEach((node) => {
        const position = node.position();
        const x = Number(position.x ?? 0);
        const y = Number(position.y ?? 0);
        const key = `${Math.round(x)}:${Math.round(y)}`;
        const group = collisionGroups.get(key);
        if (group) {
            group.push(node);
            return;
        }
        collisionGroups.set(key, [node]);
    });
    let hasCollisions = false;
    state.cy.startBatch();
    for (const group of collisionGroups.values()) {
        if (group.length < 2) {
            continue;
        }
        hasCollisions = true;
        const orderedNodes = group.sort(compareNodeLabels);
        const anchor = orderedNodes[0].position();
        const columnCount = Math.max(2, Math.min(8, Math.ceil(Math.sqrt(orderedNodes.length))));
        const rows = chunkPlacements(orderedNodes, columnCount);
        const rowOffset = (rows.length - 1) / 2;
        const columnGap = 172;
        const rowGap = 66;
        for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
            const row = rows[rowIndex];
            const xPositions = buildCenteredAxisPositions(row.length, columnGap);
            const y = Number(anchor.y ?? 0) + (rowIndex - rowOffset) * rowGap;
            for (let columnIndex = 0; columnIndex < row.length; columnIndex += 1) {
                positionNodePreservingLock(row[columnIndex], Number(anchor.x ?? 0) + xPositions[columnIndex], y);
            }
        }
    }
    state.cy.endBatch();
    if (hasCollisions) {
        forceGraphRender();
    }
}
function separateOverlappingNodeCollisions() {
    separateOverlappingExternalDependencyNodes();
    separateOverlappingCollapsedNodes();
    separateOverlappingDocumentNodes();
}
function separateOverlappingHighLevelNodes() {
    if (!state.cy) {
        return;
    }
    const nodes = state.cy
        .nodes()
        .not(".state-hidden")
        .not(".filtered-out")
        .not(".pinned")
        .filter((node) => {
        const group = String(node.data("group") ?? "");
        return group === "project" || group === "document";
    })
        .toArray();
    if (nodes.length < 2) {
        return;
    }
    let movedAny = false;
    for (let pass = 0; pass < 6; pass += 1) {
        let movedThisPass = false;
        for (let i = 0; i < nodes.length; i += 1) {
            for (let j = i + 1; j < nodes.length; j += 1) {
                const left = nodes[i];
                const right = nodes[j];
                const a = left.boundingBox({ includeLabels: true });
                const b = right.boundingBox({ includeLabels: true });
                const overlapX = Math.min(a.x2, b.x2) - Math.max(a.x1, b.x1);
                const overlapY = Math.min(a.y2, b.y2) - Math.max(a.y1, b.y1);
                if (overlapX <= 0 || overlapY <= 0) {
                    continue;
                }
                movedThisPass = true;
                movedAny = true;
                const leftPos = left.position();
                const rightPos = right.position();
                if (overlapX < overlapY) {
                    const delta = overlapX / 2 + 24;
                    positionNodePreservingLock(left, Number(leftPos.x ?? 0) - delta, Number(leftPos.y ?? 0));
                    positionNodePreservingLock(right, Number(rightPos.x ?? 0) + delta, Number(rightPos.y ?? 0));
                }
                else {
                    const delta = overlapY / 2 + 20;
                    positionNodePreservingLock(left, Number(leftPos.x ?? 0), Number(leftPos.y ?? 0) - delta);
                    positionNodePreservingLock(right, Number(rightPos.x ?? 0), Number(rightPos.y ?? 0) + delta);
                }
            }
        }
        if (!movedThisPass) {
            break;
        }
    }
    if (movedAny) {
        forceGraphRender();
    }
}
function layoutSymbolSections(nodes, metrics) {
    if (!nodes || nodes.length === 0) {
        return;
    }
    const groups = new Map();
    nodes.forEach((node) => {
        const symbolKind = String(node.data("symbolKind") ?? "Unknown");
        const bucket = groups.get(symbolKind);
        if (bucket) {
            bucket.push(node);
            return;
        }
        groups.set(symbolKind, [node]);
    });
    const sectionGapX = 96;
    const sectionGapY = 96;
    const cellWidth = metrics.visibleNodeCount >= 1400 ? 124 : 136;
    const cellHeight = 46;
    const maxCanvasWidth = metrics.visibleNodeCount >= 1400 ? 2200 : 1800;
    let cursorX = -maxCanvasWidth / 2;
    let cursorY = 32;
    let currentRowHeight = 0;
    const orderedGroups = [...groups.entries()]
        .sort((left, right) => left[0].localeCompare(right[0]));
    for (const [, groupNodes] of orderedGroups) {
        const orderedNodes = groupNodes.sort(compareNodeLabels);
        const rowCount = resolveSectionRowCount(orderedNodes.length);
        const columnCount = Math.ceil(orderedNodes.length / rowCount);
        const sectionWidth = Math.max(160, columnCount * cellWidth);
        const sectionHeight = Math.max(72, rowCount * cellHeight);
        if (cursorX + sectionWidth > maxCanvasWidth / 2) {
            cursorX = -maxCanvasWidth / 2;
            cursorY += currentRowHeight + sectionGapY;
            currentRowHeight = 0;
        }
        for (let index = 0; index < orderedNodes.length; index += 1) {
            const columnIndex = Math.floor(index / rowCount);
            const rowIndex = index % rowCount;
            const x = cursorX + columnIndex * cellWidth + cellWidth / 2;
            const y = cursorY + rowIndex * cellHeight + cellHeight / 2;
            positionNodePreservingLock(orderedNodes[index], x, y);
        }
        cursorX += sectionWidth + sectionGapX;
        currentRowHeight = Math.max(currentRowHeight, sectionHeight);
    }
}
function resolveSectionRowCount(nodeCount) {
    if (nodeCount <= 6) {
        return nodeCount;
    }
    if (nodeCount <= 24) {
        return 6;
    }
    if (nodeCount <= 96) {
        return 10;
    }
    return 14;
}
function compareNodeLabels(left, right) {
    const leftLabel = String(left.data("label") ?? "");
    const rightLabel = String(right.data("label") ?? "");
    return leftLabel.localeCompare(rightLabel);
}
function compareExternalDependencyNodes(left, right) {
    const groupComparison = resolveDependencyGroupRank(left) - resolveDependencyGroupRank(right);
    if (groupComparison !== 0) {
        return groupComparison;
    }
    return compareNodeLabels(left, right);
}
function applyDependencyMapLayout(selectedNode, dependencySeedNodes, dependencyEdges, neighborhood) {
    if (!state.cy) {
        return;
    }
    const neighborhoodNodes = neighborhood.nodes();
    const neighborhoodNodesById = buildDependencyNodeIndex(neighborhoodNodes);
    const ownership = buildOwnershipMaps();
    const positionedNodeIds = new Set();
    const placements = collectDependencyNeighborPlacements(dependencySeedNodes, dependencyEdges);
    const incomingOnly = placements
        .filter((item) => item.direction === "incoming")
        .sort(compareDependencyPlacement);
    const outgoingOnly = placements
        .filter((item) => item.direction === "outgoing")
        .sort(compareDependencyPlacement);
    const bidirectional = placements
        .filter((item) => item.direction === "both")
        .sort(compareDependencyPlacement);
    state.cy.startBatch();
    const centerLayout = layoutDependencyMapCenter(selectedNode, dependencySeedNodes, ownership, neighborhoodNodesById, positionedNodeIds);
    const incomingClusters = buildDependencyPlacementClusters(incomingOnly, ownership, neighborhoodNodesById, centerLayout.reservedNodeIds).sort(compareDependencyCluster);
    const outgoingClusters = buildDependencyPlacementClusters(outgoingOnly, ownership, neighborhoodNodesById, centerLayout.reservedNodeIds).sort(compareDependencyCluster);
    const bidirectionalClusters = buildDependencyPlacementClusters(bidirectional, ownership, neighborhoodNodesById, centerLayout.reservedNodeIds).sort(compareDependencyCluster);
    layoutDependencySide(incomingClusters, -1, positionedNodeIds);
    layoutDependencySide(outgoingClusters, 1, positionedNodeIds);
    layoutBidirectionalDependencies(bidirectionalClusters, positionedNodeIds);
    layoutRemainingDependencyNodes(neighborhoodNodes, positionedNodeIds);
    state.cy.endBatch();
    separateOverlappingNodeCollisions();
    neighborhood.nodes().forEach((node) => {
        if (node.id() === selectedNode.id()) {
            return;
        }
        node.unselect();
    });
}
function collectDependencyNeighborPlacements(selectionSeedNodes, dependencyEdges) {
    const placementsByNodeId = new Map();
    const seedNodeIds = buildDependencyNodeIdSet(selectionSeedNodes);
    dependencyEdges.forEach((edge) => {
        const sourceNode = edge.source();
        const targetNode = edge.target();
        const sourceIsSeed = seedNodeIds.has(sourceNode.id());
        const targetIsSeed = seedNodeIds.has(targetNode.id());
        if (sourceIsSeed === targetIsSeed) {
            return;
        }
        const isIncoming = targetIsSeed;
        const peerNode = isIncoming ? sourceNode : targetNode;
        const peerNodeId = peerNode.id();
        const nextDirection = isIncoming ? "incoming" : "outgoing";
        const weight = Number(edge.data("weight") ?? 1);
        const kind = String(edge.data("kind") ?? "reference");
        const existing = placementsByNodeId.get(peerNodeId);
        if (!existing) {
            placementsByNodeId.set(peerNodeId, {
                node: peerNode,
                direction: nextDirection,
                primaryKind: kind,
                totalWeight: weight
            });
            return;
        }
        placementsByNodeId.set(peerNodeId, {
            node: existing.node,
            direction: existing.direction === nextDirection ? existing.direction : "both",
            primaryKind: pickPreferredDependencyKind(existing.primaryKind, kind),
            totalWeight: existing.totalWeight + weight
        });
    });
    return [...placementsByNodeId.values()];
}
function buildDependencyNodeIdSet(nodes) {
    const nodeIds = new Set();
    nodes.forEach((node) => {
        nodeIds.add(String(node.id()));
    });
    return nodeIds;
}
function buildDependencyNodeIndex(nodes) {
    const index = new Map();
    nodes.forEach((node) => {
        index.set(String(node.id()), node);
    });
    return index;
}
function layoutDependencyMapCenter(selectedNode, dependencySeedNodes, ownership, neighborhoodNodesById, positionedNodeIds) {
    const reservedNodeIds = new Set();
    const contextNodes = [];
    const projectNode = resolveDependencyProjectNode(selectedNode, ownership, neighborhoodNodesById);
    if (projectNode && projectNode.id() !== selectedNode.id()) {
        contextNodes.push(projectNode);
    }
    const documentNode = resolveDependencyDocumentNode(selectedNode, ownership, neighborhoodNodesById);
    if (documentNode &&
        documentNode.id() !== selectedNode.id() &&
        !contextNodes.some((node) => node.id() === documentNode.id())) {
        contextNodes.push(documentNode);
    }
    let cursorY = -contextNodes.length * 76;
    for (const contextNode of contextNodes) {
        positionNodePreservingLock(contextNode, 0, cursorY);
        positionedNodeIds.add(contextNode.id());
        reservedNodeIds.add(contextNode.id());
        cursorY += 76;
    }
    positionNodePreservingLock(selectedNode, 0, 0);
    positionedNodeIds.add(selectedNode.id());
    reservedNodeIds.add(selectedNode.id());
    const extraSeedNodes = dependencySeedNodes
        .toArray()
        .filter((node) => !reservedNodeIds.has(node.id()))
        .sort(compareDependencyPeerNodes);
    if (extraSeedNodes.length > 0) {
        layoutDependencySeedNodes(extraSeedNodes, 132, positionedNodeIds);
    }
    return { reservedNodeIds };
}
function layoutDependencySeedNodes(seedNodes, startY, positionedNodeIds) {
    const columnCount = resolveDependencyCenterSeedColumnCount(seedNodes.length);
    const rows = chunkPlacements(seedNodes, columnCount);
    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const rowNodes = rows[rowIndex];
        const xPositions = buildCenteredAxisPositions(rowNodes.length, 156);
        const y = startY + rowIndex * 58;
        for (let columnIndex = 0; columnIndex < rowNodes.length; columnIndex += 1) {
            positionNodePreservingLock(rowNodes[columnIndex], xPositions[columnIndex], y);
            positionedNodeIds.add(rowNodes[columnIndex].id());
        }
    }
}
function buildDependencyPlacementClusters(placements, ownership, neighborhoodNodesById, reservedNodeIds) {
    const clustersByKey = new Map();
    for (const placement of placements) {
        const context = resolveDependencyPlacementContext(placement.node, ownership, neighborhoodNodesById, reservedNodeIds);
        const clusterKey = `${placement.direction}:${context.key}`;
        const existing = clustersByKey.get(clusterKey);
        if (!existing) {
            clustersByKey.set(clusterKey, {
                key: clusterKey,
                laneIndex: resolveDependencyClusterLaneIndex(placement.node),
                primaryKind: placement.primaryKind,
                totalWeight: placement.totalWeight,
                documentNode: context.documentNode,
                projectNode: context.projectNode,
                peerNodes: [placement.node]
            });
            continue;
        }
        if (!existing.peerNodes.some((node) => node.id() === placement.node.id())) {
            existing.peerNodes.push(placement.node);
        }
        existing.primaryKind = pickPreferredDependencyKind(existing.primaryKind, placement.primaryKind);
        existing.totalWeight += placement.totalWeight;
        if (!existing.documentNode && context.documentNode) {
            existing.documentNode = context.documentNode;
        }
        if (!existing.projectNode && context.projectNode) {
            existing.projectNode = context.projectNode;
        }
    }
    return [...clustersByKey.values()].map((cluster) => ({
        ...cluster,
        peerNodes: [...cluster.peerNodes].sort(compareDependencyPeerNodes)
    }));
}
function resolveDependencyPlacementContext(node, ownership, neighborhoodNodesById, reservedNodeIds) {
    const group = String(node.data("group") ?? "");
    if (group === "symbol") {
        const documentId = ownership.symbolToDocument.get(node.id()) ?? null;
        const projectId = documentId
            ? ownership.documentToProject.get(documentId) ?? null
            : null;
        return {
            key: `document:${documentId ?? node.id()}`,
            documentNode: resolveDependencyContextNode(documentId, neighborhoodNodesById, reservedNodeIds),
            projectNode: resolveDependencyContextNode(projectId, neighborhoodNodesById, reservedNodeIds)
        };
    }
    if (group === "document") {
        const projectId = ownership.documentToProject.get(node.id()) ?? null;
        return {
            key: `document:${node.id()}`,
            documentNode: null,
            projectNode: resolveDependencyContextNode(projectId, neighborhoodNodesById, reservedNodeIds)
        };
    }
    if (group === "project") {
        return {
            key: `project:${node.id()}`,
            documentNode: null,
            projectNode: null
        };
    }
    if (isExternalDependencyGroup(group)) {
        const projectId = ownership.externalToProject.get(node.id()) ?? null;
        return {
            key: `external:${projectId ?? "none"}:${group}`,
            documentNode: null,
            projectNode: resolveDependencyContextNode(projectId, neighborhoodNodesById, reservedNodeIds)
        };
    }
    return {
        key: `node:${node.id()}`,
        documentNode: null,
        projectNode: null
    };
}
function resolveDependencyContextNode(nodeId, neighborhoodNodesById, reservedNodeIds) {
    if (!nodeId || reservedNodeIds.has(nodeId)) {
        return null;
    }
    return neighborhoodNodesById.get(nodeId) ?? null;
}
function resolveDependencyDocumentNode(node, ownership, neighborhoodNodesById) {
    const group = String(node.data("group") ?? "");
    if (group === "document") {
        return node;
    }
    if (group !== "symbol") {
        return null;
    }
    const documentId = ownership.symbolToDocument.get(node.id()) ?? null;
    return documentId
        ? neighborhoodNodesById.get(documentId) ?? null
        : null;
}
function resolveDependencyProjectNode(node, ownership, neighborhoodNodesById) {
    const group = String(node.data("group") ?? "");
    if (group === "project") {
        return node;
    }
    if (group === "document") {
        const projectId = ownership.documentToProject.get(node.id()) ?? null;
        return projectId
            ? neighborhoodNodesById.get(projectId) ?? null
            : null;
    }
    if (group === "symbol") {
        const documentId = ownership.symbolToDocument.get(node.id()) ?? null;
        const projectId = documentId
            ? ownership.documentToProject.get(documentId) ?? null
            : null;
        return projectId
            ? neighborhoodNodesById.get(projectId) ?? null
            : null;
    }
    if (isExternalDependencyGroup(group)) {
        const projectId = ownership.externalToProject.get(node.id()) ?? null;
        return projectId
            ? neighborhoodNodesById.get(projectId) ?? null
            : null;
    }
    return null;
}
function layoutDependencySide(clusters, directionMultiplier, positionedNodeIds) {
    if (clusters.length === 0) {
        return;
    }
    const lanes = new Map();
    for (const cluster of clusters) {
        const laneIndex = cluster.laneIndex;
        const lane = lanes.get(laneIndex);
        if (lane) {
            lane.push(cluster);
            continue;
        }
        lanes.set(laneIndex, [cluster]);
    }
    const laneIndices = [...lanes.keys()].sort((left, right) => left - right);
    for (const laneIndex of laneIndices) {
        const laneClusters = lanes.get(laneIndex) ?? [];
        const columnCount = resolveDependencyLaneColumnCount(laneClusters.length);
        const columns = Array.from({ length: columnCount }, () => []);
        const columnHeights = new Array(columnCount).fill(0);
        for (const cluster of laneClusters) {
            const metrics = measureDependencyPlacementCluster(cluster);
            let selectedColumn = 0;
            let smallestHeight = columnHeights[0];
            for (let columnIndex = 1; columnIndex < columnCount; columnIndex += 1) {
                if (columnHeights[columnIndex] < smallestHeight) {
                    selectedColumn = columnIndex;
                    smallestHeight = columnHeights[columnIndex];
                }
            }
            columns[selectedColumn].push(cluster);
            columnHeights[selectedColumn] += metrics.height;
        }
        for (let columnIndex = 0; columnIndex < columns.length; columnIndex += 1) {
            const columnClusters = columns[columnIndex];
            if (columnClusters.length === 0) {
                continue;
            }
            const contentHeight = columnClusters.reduce((sum, cluster) => sum + measureDependencyPlacementCluster(cluster).height, 0) +
                Math.max(0, columnClusters.length - 1) * 30;
            let cursorY = -contentHeight / 2;
            const x = directionMultiplier * (320 + laneIndex * 220 + columnIndex * 170);
            for (const cluster of columnClusters) {
                const metrics = measureDependencyPlacementCluster(cluster);
                layoutDependencyCluster(cluster, metrics, x, cursorY, directionMultiplier * 52, positionedNodeIds);
                cursorY += metrics.height + 30;
            }
        }
    }
}
function layoutBidirectionalDependencies(clusters, positionedNodeIds) {
    if (clusters.length === 0) {
        return;
    }
    const itemsPerRow = resolveBidirectionalClusterColumnCount(clusters.length);
    const rowGap = 44;
    const columnGap = 48;
    const rows = chunkPlacements(clusters, itemsPerRow);
    const rowHeights = rows.map((row) => row.reduce((maxHeight, cluster) => Math.max(maxHeight, measureDependencyPlacementCluster(cluster).height), 0));
    const totalHeight = rowHeights.reduce((sum, height) => sum + height, 0) +
        Math.max(0, rowHeights.length - 1) * rowGap;
    let cursorY = -Math.max(220, totalHeight + 140);
    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const rowClusters = rows[rowIndex];
        const rowHeight = rowHeights[rowIndex];
        const rowWidth = rowClusters.reduce((sum, cluster) => sum + measureDependencyPlacementCluster(cluster).width, 0) +
            Math.max(0, rowClusters.length - 1) * columnGap;
        let cursorX = -rowWidth / 2;
        for (const cluster of rowClusters) {
            const metrics = measureDependencyPlacementCluster(cluster);
            layoutDependencyCluster(cluster, metrics, cursorX + metrics.width / 2, cursorY + (rowHeight - metrics.height) / 2, 0, positionedNodeIds);
            cursorX += metrics.width + columnGap;
        }
        cursorY += rowHeight + rowGap;
    }
}
function measureDependencyPlacementCluster(cluster) {
    const peerCount = cluster.peerNodes.length;
    const peerColumnCount = resolveDependencyClusterPeerColumnCount(peerCount);
    const peerRowCount = peerCount === 0
        ? 0
        : Math.ceil(peerCount / peerColumnCount);
    const peerWidth = peerCount === 0
        ? 0
        : 132 + Math.max(0, peerColumnCount - 1) * 148;
    const peerHeight = peerCount === 0
        ? 0
        : 44 + Math.max(0, peerRowCount - 1) * 56;
    const contextNodeCount = (cluster.projectNode ? 1 : 0) + (cluster.documentNode ? 1 : 0);
    const contextHeight = contextNodeCount * 72;
    const contextPeerGap = contextNodeCount > 0 && peerCount > 0 ? 16 : 0;
    return {
        width: Math.max(180, peerWidth + 56),
        height: Math.max(72, 18 + contextHeight + contextPeerGap + peerHeight + 18),
        peerColumnCount: Math.max(1, peerColumnCount),
        peerRowCount
    };
}
function layoutDependencyCluster(cluster, metrics, centerX, topY, peerBiasX, positionedNodeIds) {
    let cursorY = topY + 18;
    if (cluster.projectNode) {
        positionNodePreservingLock(cluster.projectNode, centerX, cursorY);
        positionedNodeIds.add(cluster.projectNode.id());
        cursorY += 72;
    }
    if (cluster.documentNode) {
        positionNodePreservingLock(cluster.documentNode, centerX, cursorY);
        positionedNodeIds.add(cluster.documentNode.id());
        cursorY += 72;
    }
    if ((cluster.projectNode || cluster.documentNode) && cluster.peerNodes.length > 0) {
        cursorY += 16;
    }
    const xPositions = buildCenteredAxisPositions(metrics.peerColumnCount, 148);
    const peerCenterX = centerX + peerBiasX;
    for (let index = 0; index < cluster.peerNodes.length; index += 1) {
        const columnIndex = index % metrics.peerColumnCount;
        const rowIndex = Math.floor(index / metrics.peerColumnCount);
        const node = cluster.peerNodes[index];
        positionNodePreservingLock(node, peerCenterX + xPositions[columnIndex], cursorY + rowIndex * 56);
        positionedNodeIds.add(node.id());
    }
}
function layoutRemainingDependencyNodes(nodes, positionedNodeIds) {
    const remainingNodes = nodes
        .toArray()
        .filter((node) => !positionedNodeIds.has(node.id()))
        .sort(compareDependencyPeerNodes);
    if (remainingNodes.length === 0) {
        return;
    }
    const columnCount = resolveDependencyCenterSeedColumnCount(remainingNodes.length);
    const rows = chunkPlacements(remainingNodes, columnCount);
    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const rowNodes = rows[rowIndex];
        const xPositions = buildCenteredAxisPositions(rowNodes.length, 164);
        const y = 224 + rowIndex * 64;
        for (let columnIndex = 0; columnIndex < rowNodes.length; columnIndex += 1) {
            positionNodePreservingLock(rowNodes[columnIndex], xPositions[columnIndex], y);
            positionedNodeIds.add(rowNodes[columnIndex].id());
        }
    }
}
function compareDependencyCluster(left, right) {
    const laneComparison = left.laneIndex - right.laneIndex;
    if (laneComparison !== 0) {
        return laneComparison;
    }
    const kindComparison = resolveDependencyKindRank(left.primaryKind) - resolveDependencyKindRank(right.primaryKind);
    if (kindComparison !== 0) {
        return kindComparison;
    }
    const weightComparison = right.totalWeight - left.totalWeight;
    if (weightComparison !== 0) {
        return weightComparison;
    }
    return resolveDependencyClusterLabel(left).localeCompare(resolveDependencyClusterLabel(right));
}
function resolveDependencyClusterLabel(cluster) {
    const anchorNode = cluster.documentNode ?? cluster.projectNode ?? cluster.peerNodes[0];
    return String(anchorNode?.data("label") ?? cluster.key);
}
function compareDependencyPeerNodes(left, right) {
    const groupComparison = resolveDependencyGroupRank(left) - resolveDependencyGroupRank(right);
    if (groupComparison !== 0) {
        return groupComparison;
    }
    return compareNodeLabels(left, right);
}
function resolveDependencyClusterLaneIndex(node) {
    const group = String(node.data("group") ?? "");
    if (group === "symbol") {
        return 0;
    }
    if (group === "document") {
        return 1;
    }
    if (group === "project") {
        return 2;
    }
    return 3;
}
function resolveDependencyCenterSeedColumnCount(nodeCount) {
    if (nodeCount <= 4) {
        return Math.max(1, nodeCount);
    }
    if (nodeCount <= 10) {
        return 2;
    }
    if (nodeCount <= 24) {
        return 3;
    }
    return 4;
}
function resolveDependencyClusterPeerColumnCount(peerCount) {
    if (peerCount <= 4) {
        return Math.max(1, peerCount);
    }
    if (peerCount <= 10) {
        return 2;
    }
    if (peerCount <= 20) {
        return 3;
    }
    return 4;
}
function resolveDependencyLaneColumnCount(clusterCount) {
    if (clusterCount <= 5) {
        return 1;
    }
    if (clusterCount <= 12) {
        return 2;
    }
    return 3;
}
function resolveBidirectionalClusterColumnCount(clusterCount) {
    if (clusterCount <= 2) {
        return Math.max(1, clusterCount);
    }
    if (clusterCount <= 6) {
        return 3;
    }
    return 4;
}
function chunkPlacements(items, chunkSize) {
    if (items.length === 0 || chunkSize <= 0) {
        return [];
    }
    const chunks = [];
    for (let index = 0; index < items.length; index += chunkSize) {
        chunks.push(items.slice(index, index + chunkSize));
    }
    return chunks;
}
function buildCenteredAxisPositions(count, gap) {
    if (count <= 0) {
        return [];
    }
    const positions = [];
    const offset = (count - 1) / 2;
    for (let index = 0; index < count; index += 1) {
        positions.push((index - offset) * gap);
    }
    return positions;
}
function positionNodePreservingLock(node, x, y) {
    const wasLocked = typeof node.locked === "function" ? Boolean(node.locked()) : false;
    if (wasLocked && typeof node.unlock === "function") {
        node.unlock();
    }
    node.position({ x, y });
    if (wasLocked && typeof node.lock === "function") {
        node.lock();
    }
}
function compareDependencyPlacement(left, right) {
    const groupComparison = resolveDependencyGroupRank(left.node) - resolveDependencyGroupRank(right.node);
    if (groupComparison !== 0) {
        return groupComparison;
    }
    const kindComparison = resolveDependencyKindRank(left.primaryKind) - resolveDependencyKindRank(right.primaryKind);
    if (kindComparison !== 0) {
        return kindComparison;
    }
    const weightComparison = right.totalWeight - left.totalWeight;
    if (weightComparison !== 0) {
        return weightComparison;
    }
    const leftLabel = String(left.node.data("label") ?? "");
    const rightLabel = String(right.node.data("label") ?? "");
    return leftLabel.localeCompare(rightLabel);
}
function resolveDependencyLaneIndex(placement) {
    const group = String(placement.node.data("group") ?? "");
    if (group === "symbol") {
        return 0;
    }
    if (group === "document" || group === "project") {
        return 1;
    }
    return 2;
}
function resolveDependencyGroupRank(node) {
    const group = String(node.data("group") ?? "");
    switch (group) {
        case "project":
            return 0;
        case "document":
            return 1;
        case "symbol":
            return 2;
        case "package":
            return 3;
        case "assembly":
            return 4;
        case "dll":
            return 5;
        default:
            return 6;
    }
}
function resolveDependencyKindRank(kind) {
    switch (kind) {
        case "symbol-call":
            return 0;
        case "symbol-inheritance":
        case "symbol-implementation":
            return 1;
        case "document-reference":
            return 2;
        case "project-reference":
            return 3;
        case "symbol-reference":
            return 4;
        case "symbol-creation":
            return 5;
        case "project-package":
            return 6;
        case "project-assembly":
            return 7;
        case "project-dll":
            return 8;
        default:
            return 9;
    }
}
function pickPreferredDependencyKind(currentKind, nextKind) {
    return resolveDependencyKindRank(nextKind) < resolveDependencyKindRank(currentKind)
        ? nextKind
        : currentKind;
}
function syncLayoutIndex(layoutName) {
    const nextIndex = layoutSequence.findIndex((candidate) => candidate === layoutName);
    if (nextIndex >= 0) {
        state.layoutIndex = nextIndex;
    }
}
function applySemanticFlowLayout(metrics) {
    if (!state.cy) {
        return;
    }
    const visibleNodes = state.cy.nodes().not(".state-hidden").not(".filtered-out");
    if (visibleNodes.length === 0) {
        return;
    }
    const visibleEdges = state.cy.edges().not(".state-hidden").not(".filtered-out");
    const ownership = buildOwnershipMaps();
    const visibleNodesById = new Map();
    visibleNodes.forEach((node) => {
        visibleNodesById.set(node.id(), node);
    });
    const projectIds = collectSemanticProjectIds(visibleNodes, ownership);
    if (projectIds.length === 0) {
        applyStructuredMassiveLayout(metrics);
        return;
    }
    const projectAdjacency = buildProjectDependencyGraph(visibleEdges, ownership);
    const documentAdjacency = buildDocumentDependencyGraph(visibleEdges, ownership);
    const reverseDocumentAdjacency = buildReverseAdjacency(documentAdjacency);
    const symbolAdjacency = buildSymbolDependencyGraph(visibleEdges);
    const reverseSymbolAdjacency = buildReverseAdjacency(symbolAdjacency);
    const projectLayers = computeProjectLayers(projectIds, projectAdjacency, ownership.projectLabels);
    const projectLayerById = buildLayerIndexMap(projectLayers);
    const clusterByProjectId = new Map();
    for (const layer of projectLayers) {
        for (const projectId of layer) {
            clusterByProjectId.set(projectId, buildSemanticProjectCluster(projectId, visibleNodesById, ownership, documentAdjacency, reverseDocumentAdjacency, symbolAdjacency, reverseSymbolAdjacency, projectLayerById));
        }
    }
    const layerWidths = projectLayers.map((layer) => {
        let maxWidth = 260;
        for (const projectId of layer) {
            const cluster = clusterByProjectId.get(projectId);
            if (cluster) {
                maxWidth = Math.max(maxWidth, cluster.width);
            }
        }
        return maxWidth;
    });
    const layerGap = state.includeSymbols ? 320 : 260;
    const layerHeightGap = state.includeSymbols ? 140 : 110;
    const totalWidth = layerWidths.reduce((sum, width) => sum + width, 0) +
        Math.max(0, projectLayers.length - 1) * layerGap;
    let cursorX = -totalWidth / 2;
    state.cy.startBatch();
    for (let layerIndex = 0; layerIndex < projectLayers.length; layerIndex += 1) {
        const layer = projectLayers[layerIndex];
        const layerCenterX = cursorX + layerWidths[layerIndex] / 2;
        cursorX += layerWidths[layerIndex] + layerGap;
        const totalLayerHeight = layer.reduce((sum, projectId) => sum + (clusterByProjectId.get(projectId)?.height ?? 0), 0) +
            Math.max(0, layer.length - 1) * layerHeightGap;
        let cursorY = -totalLayerHeight / 2;
        for (const projectId of layer) {
            const cluster = clusterByProjectId.get(projectId);
            if (!cluster) {
                continue;
            }
            applySemanticClusterPosition(cluster, layerCenterX, cursorY);
            cursorY += cluster.height + layerHeightGap;
        }
    }
    state.cy.endBatch();
}
function buildOwnershipMaps() {
    const ownership = {
        documentToProject: new Map(),
        symbolToDocument: new Map(),
        externalToProject: new Map(),
        projectToDocuments: new Map(),
        documentToSymbols: new Map(),
        projectLabels: new Map()
    };
    if (!state.cy) {
        return ownership;
    }
    state.cy.nodes().forEach((node) => {
        if (String(node.data("group") ?? "") === "project") {
            ownership.projectLabels.set(node.id(), String(node.data("label") ?? node.id()));
        }
    });
    state.cy.edges().forEach((edge) => {
        const kind = String(edge.data("kind") ?? "");
        const sourceId = String(edge.source().id());
        const targetId = String(edge.target().id());
        switch (kind) {
            case "contains-document":
                ownership.documentToProject.set(targetId, sourceId);
                appendOwnedValue(ownership.projectToDocuments, sourceId, targetId);
                break;
            case "contains-symbol":
                ownership.symbolToDocument.set(targetId, sourceId);
                appendOwnedValue(ownership.documentToSymbols, sourceId, targetId);
                break;
            case "project-package":
            case "project-assembly":
            case "project-dll":
                ownership.externalToProject.set(targetId, sourceId);
                break;
        }
    });
    return ownership;
}
function appendOwnedValue(map, key, value) {
    const current = map.get(key);
    if (current) {
        current.push(value);
        return;
    }
    map.set(key, [value]);
}
function collectSemanticProjectIds(visibleNodes, ownership) {
    const projectIds = new Set();
    visibleNodes.forEach((node) => {
        const group = String(node.data("group") ?? "");
        const projectId = resolveProjectScopeId(node.id(), group, ownership);
        if (!projectId) {
            return;
        }
        projectIds.add(projectId);
        ensureProjectLabel(ownership, projectId);
    });
    return [...projectIds].sort((left, right) => resolveProjectLabel(ownership, left).localeCompare(resolveProjectLabel(ownership, right)));
}
function resolveProjectScopeId(nodeId, group, ownership) {
    if (group === "project") {
        return nodeId;
    }
    if (group === "document") {
        return ownership.documentToProject.get(nodeId) ?? null;
    }
    if (group === "symbol") {
        const documentId = ownership.symbolToDocument.get(nodeId);
        return documentId
            ? ownership.documentToProject.get(documentId) ?? null
            : null;
    }
    if (isExternalDependencyGroup(group)) {
        return ownership.externalToProject.get(nodeId) ?? null;
    }
    return null;
}
function resolveDocumentScopeId(nodeId, group, ownership) {
    if (group === "document") {
        return nodeId;
    }
    if (group === "symbol") {
        return ownership.symbolToDocument.get(nodeId) ?? null;
    }
    return null;
}
function ensureProjectLabel(ownership, projectId) {
    if (ownership.projectLabels.has(projectId) || !state.cy) {
        return;
    }
    const projectNode = state.cy.getElementById(projectId);
    ownership.projectLabels.set(projectId, projectNode.empty()
        ? projectId
        : String(projectNode.data("label") ?? projectId));
}
function resolveProjectLabel(ownership, projectId) {
    ensureProjectLabel(ownership, projectId);
    return ownership.projectLabels.get(projectId) ?? projectId;
}
function buildProjectDependencyGraph(visibleEdges, ownership) {
    const adjacency = new Map();
    visibleEdges.forEach((edge) => {
        const kind = String(edge.data("kind") ?? "");
        if (kind !== "project-reference" && kind !== "document-reference" && !isSymbolDependencyKind(kind)) {
            return;
        }
        const sourceNode = edge.source();
        const targetNode = edge.target();
        const sourceProjectId = resolveProjectScopeId(String(sourceNode.id()), String(sourceNode.data("group") ?? ""), ownership);
        const targetProjectId = resolveProjectScopeId(String(targetNode.id()), String(targetNode.data("group") ?? ""), ownership);
        if (!sourceProjectId || !targetProjectId || sourceProjectId === targetProjectId) {
            return;
        }
        addAdjacencyWeight(adjacency, sourceProjectId, targetProjectId, resolveProjectAggregationWeight(kind, Number(edge.data("weight") ?? 1)));
    });
    return adjacency;
}
function buildDocumentDependencyGraph(visibleEdges, ownership) {
    const adjacency = new Map();
    visibleEdges.forEach((edge) => {
        const kind = String(edge.data("kind") ?? "");
        if (kind !== "document-reference" && !isSymbolDependencyKind(kind)) {
            return;
        }
        const sourceNode = edge.source();
        const targetNode = edge.target();
        const sourceDocumentId = resolveDocumentScopeId(String(sourceNode.id()), String(sourceNode.data("group") ?? ""), ownership);
        const targetDocumentId = resolveDocumentScopeId(String(targetNode.id()), String(targetNode.data("group") ?? ""), ownership);
        if (!sourceDocumentId || !targetDocumentId || sourceDocumentId === targetDocumentId) {
            return;
        }
        addAdjacencyWeight(adjacency, sourceDocumentId, targetDocumentId, resolveDocumentAggregationWeight(kind, Number(edge.data("weight") ?? 1)));
    });
    return adjacency;
}
function buildSymbolDependencyGraph(visibleEdges) {
    const adjacency = new Map();
    visibleEdges.forEach((edge) => {
        const kind = String(edge.data("kind") ?? "");
        if (!isSymbolDependencyKind(kind)) {
            return;
        }
        addAdjacencyWeight(adjacency, String(edge.source().id()), String(edge.target().id()), resolveSymbolAggregationWeight(kind, Number(edge.data("weight") ?? 1)));
    });
    return adjacency;
}
function addAdjacencyWeight(adjacency, sourceId, targetId, weight) {
    if (!Number.isFinite(weight) || weight <= 0) {
        return;
    }
    let targets = adjacency.get(sourceId);
    if (!targets) {
        targets = new Map();
        adjacency.set(sourceId, targets);
    }
    targets.set(targetId, (targets.get(targetId) ?? 0) + weight);
}
function buildReverseAdjacency(adjacency) {
    const reverse = new Map();
    for (const [sourceId, targets] of adjacency.entries()) {
        for (const [targetId, weight] of targets.entries()) {
            addAdjacencyWeight(reverse, targetId, sourceId, weight);
        }
        if (!reverse.has(sourceId)) {
            reverse.set(sourceId, new Map());
        }
    }
    return reverse;
}
function resolveProjectAggregationWeight(kind, weight) {
    switch (kind) {
        case "project-reference":
            return weight * 8;
        case "document-reference":
            return weight * 4.2;
        case "symbol-call":
            return weight * 3.2;
        case "symbol-inheritance":
        case "symbol-implementation":
            return weight * 3;
        case "symbol-creation":
            return weight * 2.4;
        case "symbol-reference":
            return weight * 2.1;
        default:
            return weight;
    }
}
function resolveDocumentAggregationWeight(kind, weight) {
    switch (kind) {
        case "document-reference":
            return weight * 3.4;
        case "symbol-call":
            return weight * 2.8;
        case "symbol-inheritance":
        case "symbol-implementation":
            return weight * 2.6;
        case "symbol-creation":
            return weight * 2.2;
        case "symbol-reference":
            return weight * 1.9;
        default:
            return weight;
    }
}
function resolveSymbolAggregationWeight(kind, weight) {
    switch (kind) {
        case "symbol-call":
            return weight * 2.6;
        case "symbol-inheritance":
        case "symbol-implementation":
            return weight * 2.4;
        case "symbol-creation":
            return weight * 2;
        default:
            return weight * 1.6;
    }
}
function computeProjectLayers(projectIds, adjacency, labels) {
    if (projectIds.length === 0) {
        return [];
    }
    const components = computeStronglyConnectedComponents(projectIds, adjacency);
    const componentMembers = new Map();
    const nodeToComponentId = new Map();
    const componentLabels = new Map();
    for (let index = 0; index < components.length; index += 1) {
        const componentId = `component:${index}`;
        const members = components[index];
        componentMembers.set(componentId, members);
        for (const projectId of members) {
            nodeToComponentId.set(projectId, componentId);
        }
        const componentLabel = [...members]
            .map((projectId) => labels.get(projectId) ?? projectId)
            .sort((left, right) => left.localeCompare(right))[0];
        componentLabels.set(componentId, componentLabel);
    }
    const componentAdjacency = new Map();
    for (const [sourceId, targets] of adjacency.entries()) {
        const sourceComponentId = nodeToComponentId.get(sourceId);
        if (!sourceComponentId) {
            continue;
        }
        for (const [targetId, weight] of targets.entries()) {
            const targetComponentId = nodeToComponentId.get(targetId);
            if (!targetComponentId || targetComponentId === sourceComponentId) {
                continue;
            }
            addAdjacencyWeight(componentAdjacency, sourceComponentId, targetComponentId, weight);
        }
    }
    const reverseComponentAdjacency = buildReverseAdjacency(componentAdjacency);
    const indegree = new Map();
    for (const componentId of componentMembers.keys()) {
        indegree.set(componentId, 0);
    }
    for (const targets of componentAdjacency.values()) {
        for (const targetId of targets.keys()) {
            indegree.set(targetId, (indegree.get(targetId) ?? 0) + 1);
        }
    }
    const frontier = [...componentMembers.keys()]
        .filter((componentId) => (indegree.get(componentId) ?? 0) === 0)
        .sort((left, right) => compareLayerMembersByLabel(left, right, componentLabels));
    const topologicalOrder = [];
    while (frontier.length > 0) {
        const current = frontier.shift();
        topologicalOrder.push(current);
        for (const targetId of componentAdjacency.get(current)?.keys() ?? []) {
            const nextInDegree = (indegree.get(targetId) ?? 0) - 1;
            indegree.set(targetId, nextInDegree);
            if (nextInDegree === 0) {
                frontier.push(targetId);
                frontier.sort((left, right) => compareLayerMembersByLabel(left, right, componentLabels));
            }
        }
    }
    if (topologicalOrder.length === 0) {
        topologicalOrder.push(...componentMembers.keys());
    }
    const layerByComponentId = new Map();
    for (const componentId of topologicalOrder) {
        let layerIndex = 0;
        for (const predecessorId of reverseComponentAdjacency.get(componentId)?.keys() ?? []) {
            layerIndex = Math.max(layerIndex, (layerByComponentId.get(predecessorId) ?? 0) + 1);
        }
        layerByComponentId.set(componentId, layerIndex);
    }
    const maxLayer = topologicalOrder.reduce((max, componentId) => Math.max(max, layerByComponentId.get(componentId) ?? 0), 0);
    const componentLayers = Array.from({ length: maxLayer + 1 }, () => []);
    for (const componentId of topologicalOrder) {
        componentLayers[layerByComponentId.get(componentId) ?? 0].push(componentId);
    }
    refineLayerOrdering(componentLayers, componentAdjacency, reverseComponentAdjacency, componentLabels);
    const projectLayers = componentLayers.map((componentLayer) => componentLayer.flatMap((componentId) => [...(componentMembers.get(componentId) ?? [])].sort((left, right) => compareProjectsWithinComponent(left, right, adjacency, labels))));
    if (projectLayers.length === 1 && projectLayers[0].length > 6) {
        return chunkPlacements(projectLayers[0], Math.ceil(Math.sqrt(projectLayers[0].length)));
    }
    return projectLayers;
}
function computeStronglyConnectedComponents(nodeIds, adjacency) {
    const indexByNodeId = new Map();
    const lowLinkByNodeId = new Map();
    const stack = [];
    const onStack = new Set();
    const components = [];
    let currentIndex = 0;
    const visit = (nodeId) => {
        indexByNodeId.set(nodeId, currentIndex);
        lowLinkByNodeId.set(nodeId, currentIndex);
        currentIndex += 1;
        stack.push(nodeId);
        onStack.add(nodeId);
        for (const targetId of adjacency.get(nodeId)?.keys() ?? []) {
            if (!indexByNodeId.has(targetId)) {
                visit(targetId);
                lowLinkByNodeId.set(nodeId, Math.min(lowLinkByNodeId.get(nodeId) ?? 0, lowLinkByNodeId.get(targetId) ?? 0));
            }
            else if (onStack.has(targetId)) {
                lowLinkByNodeId.set(nodeId, Math.min(lowLinkByNodeId.get(nodeId) ?? 0, indexByNodeId.get(targetId) ?? 0));
            }
        }
        if ((lowLinkByNodeId.get(nodeId) ?? -1) !== (indexByNodeId.get(nodeId) ?? -2)) {
            return;
        }
        const component = [];
        while (stack.length > 0) {
            const currentNodeId = stack.pop();
            onStack.delete(currentNodeId);
            component.push(currentNodeId);
            if (currentNodeId === nodeId) {
                break;
            }
        }
        components.push(component);
    };
    for (const nodeId of nodeIds) {
        if (!indexByNodeId.has(nodeId)) {
            visit(nodeId);
        }
    }
    return components;
}
function refineLayerOrdering(layers, adjacency, reverseAdjacency, labels) {
    if (layers.length <= 1) {
        return;
    }
    for (let iteration = 0; iteration < 3; iteration += 1) {
        let orderingIndex = buildOrderingIndexMap(layers);
        for (let layerIndex = 1; layerIndex < layers.length; layerIndex += 1) {
            layers[layerIndex].sort((left, right) => compareLayerMembers(left, right, reverseAdjacency, orderingIndex, labels));
        }
        orderingIndex = buildOrderingIndexMap(layers);
        for (let layerIndex = layers.length - 2; layerIndex >= 0; layerIndex -= 1) {
            layers[layerIndex].sort((left, right) => compareLayerMembers(left, right, adjacency, orderingIndex, labels));
        }
    }
}
function compareLayerMembers(leftId, rightId, adjacency, orderingIndex, labels) {
    const leftBarycenter = resolveAdjacencyBarycenter(leftId, adjacency, orderingIndex);
    const rightBarycenter = resolveAdjacencyBarycenter(rightId, adjacency, orderingIndex);
    if (leftBarycenter !== null && rightBarycenter !== null && leftBarycenter !== rightBarycenter) {
        return leftBarycenter - rightBarycenter;
    }
    if (leftBarycenter !== null && rightBarycenter === null) {
        return -1;
    }
    if (leftBarycenter === null && rightBarycenter !== null) {
        return 1;
    }
    return compareLayerMembersByLabel(leftId, rightId, labels);
}
function compareLayerMembersByLabel(leftId, rightId, labels) {
    return (labels.get(leftId) ?? leftId).localeCompare(labels.get(rightId) ?? rightId);
}
function resolveAdjacencyBarycenter(nodeId, adjacency, orderingIndex) {
    const neighbors = adjacency.get(nodeId);
    if (!neighbors || neighbors.size === 0) {
        return null;
    }
    let totalWeight = 0;
    let weightedIndex = 0;
    for (const [neighborId, weight] of neighbors.entries()) {
        const neighborIndex = orderingIndex.get(neighborId);
        if (neighborIndex === undefined) {
            continue;
        }
        totalWeight += weight;
        weightedIndex += neighborIndex * weight;
    }
    if (totalWeight <= 0) {
        return null;
    }
    return weightedIndex / totalWeight;
}
function buildOrderingIndexMap(layers) {
    const orderingIndex = new Map();
    for (const layer of layers) {
        for (let index = 0; index < layer.length; index += 1) {
            orderingIndex.set(layer[index], index);
        }
    }
    return orderingIndex;
}
function buildLayerIndexMap(layers) {
    const layerIndex = new Map();
    for (let index = 0; index < layers.length; index += 1) {
        for (const memberId of layers[index]) {
            layerIndex.set(memberId, index);
        }
    }
    return layerIndex;
}
function compareProjectsWithinComponent(leftProjectId, rightProjectId, adjacency, labels) {
    const leftWeight = resolveAdjacencyWeight(adjacency, leftProjectId);
    const rightWeight = resolveAdjacencyWeight(adjacency, rightProjectId);
    if (leftWeight !== rightWeight) {
        return rightWeight - leftWeight;
    }
    return (labels.get(leftProjectId) ?? leftProjectId).localeCompare(labels.get(rightProjectId) ?? rightProjectId);
}
function buildSemanticProjectCluster(projectId, visibleNodesById, ownership, documentAdjacency, reverseDocumentAdjacency, symbolAdjacency, reverseSymbolAdjacency, projectLayerById) {
    const positions = new Map();
    const projectNode = visibleNodesById.get(projectId) ?? null;
    let cursorY = 28;
    if (projectNode) {
        positions.set(projectId, { x: 0, y: cursorY });
        cursorY += 92;
    }
    const externalLayout = layoutSemanticExternalNodes(projectId, visibleNodesById, ownership, positions, cursorY);
    cursorY += externalLayout.height;
    if (externalLayout.height > 0) {
        cursorY += 28;
    }
    const documentLayouts = (ownership.projectToDocuments.get(projectId) ?? [])
        .filter((documentId) => visibleNodesById.has(documentId) ||
        (ownership.documentToSymbols.get(documentId) ?? []).some((symbolId) => visibleNodesById.has(symbolId)))
        .map((documentId) => buildSemanticDocumentLayout(documentId, visibleNodesById, ownership, documentAdjacency, reverseDocumentAdjacency, symbolAdjacency, reverseSymbolAdjacency, projectLayerById))
        .sort(compareSemanticDocumentLayouts);
    const maxDocumentWidth = documentLayouts.reduce((maxWidth, layout) => Math.max(maxWidth, layout.width), 132);
    const documentColumnCount = resolveProjectDocumentColumnCount(documentLayouts.length, maxDocumentWidth);
    const documentColumnGap = documentColumnCount <= 1
        ? maxDocumentWidth + 48
        : Math.max(220, maxDocumentWidth + 64);
    const documentColumnPositions = buildCenteredAxisPositions(documentColumnCount, documentColumnGap);
    const columnHeights = new Array(Math.max(1, documentColumnCount)).fill(0);
    for (const documentLayout of documentLayouts) {
        const preferredColumn = resolvePreferredColumnFromBias(documentLayout.bias, documentColumnCount);
        let selectedColumn = 0;
        let bestScore = Number.POSITIVE_INFINITY;
        for (let columnIndex = 0; columnIndex < documentColumnCount; columnIndex += 1) {
            const score = columnHeights[columnIndex] +
                Math.abs(columnIndex - preferredColumn) * (state.includeSymbols ? 170 : 130);
            if (score < bestScore) {
                bestScore = score;
                selectedColumn = columnIndex;
            }
        }
        const documentTopY = cursorY + columnHeights[selectedColumn];
        for (const [nodeId, localPosition] of documentLayout.positions.entries()) {
            positions.set(nodeId, {
                x: documentColumnPositions[selectedColumn] + localPosition.x,
                y: documentTopY + localPosition.y
            });
        }
        columnHeights[selectedColumn] += documentLayout.height + 44;
    }
    const documentSectionHeight = documentLayouts.length === 0 ? 0 : Math.max(...columnHeights) - 44;
    const documentSectionWidth = documentLayouts.length === 0
        ? 0
        : maxDocumentWidth + Math.max(0, documentColumnCount - 1) * documentColumnGap;
    return {
        projectId,
        projectNode,
        width: Math.max(240, externalLayout.width, documentSectionWidth),
        height: Math.max(cursorY + documentSectionHeight + 36, projectNode ? 92 : 36),
        positions
    };
}
function buildSemanticDocumentLayout(documentId, visibleNodesById, ownership, documentAdjacency, reverseDocumentAdjacency, symbolAdjacency, reverseSymbolAdjacency, projectLayerById) {
    const documentNode = visibleNodesById.get(documentId) ?? state.cy?.getElementById(documentId);
    const positions = new Map();
    const documentHeaderY = 24;
    positions.set(documentId, { x: 0, y: documentHeaderY });
    let cursorY = documentHeaderY + 76;
    let width = 132;
    const symbolIds = (ownership.documentToSymbols.get(documentId) ?? [])
        .filter((symbolId) => visibleNodesById.has(symbolId));
    const symbolLayerResolver = resolveSymbolLayerResolver(ownership, projectLayerById);
    const symbolGroups = new Map();
    for (const symbolId of symbolIds) {
        const symbolNode = visibleNodesById.get(symbolId);
        const symbolKind = String(symbolNode.data("symbolKind") ?? "Unknown");
        const entry = {
            node: symbolNode,
            bias: resolveFlowBias(symbolId, symbolAdjacency, reverseSymbolAdjacency, symbolLayerResolver),
            importance: resolveNodeImportance(symbolId, symbolAdjacency, reverseSymbolAdjacency)
        };
        const bucket = symbolGroups.get(symbolKind);
        if (bucket) {
            bucket.push(entry);
            continue;
        }
        symbolGroups.set(symbolKind, [entry]);
    }
    for (const [, entries] of [...symbolGroups.entries()].sort((left, right) => left[0].localeCompare(right[0]))) {
        entries.sort((left, right) => {
            const importanceComparison = right.importance - left.importance;
            if (importanceComparison !== 0) {
                return importanceComparison;
            }
            const biasComparison = left.bias - right.bias;
            if (biasComparison !== 0) {
                return biasComparison;
            }
            return compareNodeLabels(left.node, right.node);
        });
        const symbolColumnCount = resolveSymbolColumnCount(entries.length);
        const symbolColumnPositions = buildCenteredAxisPositions(symbolColumnCount, 148);
        width = Math.max(width, 132 + Math.max(0, symbolColumnCount - 1) * 148);
        for (let index = 0; index < entries.length; index += 1) {
            const columnIndex = index % symbolColumnCount;
            const rowIndex = Math.floor(index / symbolColumnCount);
            positions.set(entries[index].node.id(), {
                x: symbolColumnPositions[columnIndex],
                y: cursorY + rowIndex * 46
            });
        }
        cursorY += Math.ceil(entries.length / symbolColumnCount) * 46 + 18;
    }
    return {
        documentId,
        documentNode,
        width,
        height: Math.max(84, cursorY + 18),
        bias: resolveFlowBias(documentId, documentAdjacency, reverseDocumentAdjacency, resolveDocumentLayerResolver(ownership, projectLayerById)),
        importance: resolveNodeImportance(documentId, documentAdjacency, reverseDocumentAdjacency) + symbolIds.length * 0.35,
        positions
    };
}
function layoutSemanticExternalNodes(projectId, visibleNodesById, ownership, positions, startY) {
    const externalGroups = {
        package: [],
        assembly: [],
        dll: []
    };
    visibleNodesById.forEach((node, nodeId) => {
        const group = String(node.data("group") ?? "");
        if (!isExternalDependencyGroup(group)) {
            return;
        }
        if (ownership.externalToProject.get(nodeId) !== projectId) {
            return;
        }
        externalGroups[group].push(node);
    });
    let cursorY = startY;
    let maxWidth = 0;
    for (const group of ["package", "assembly", "dll"]) {
        const nodes = externalGroups[group].sort(compareNodeLabels);
        if (nodes.length === 0) {
            continue;
        }
        const rowSize = resolveExternalRowSize(nodes.length);
        const rows = chunkPlacements(nodes, rowSize);
        for (const row of rows) {
            const xPositions = buildCenteredAxisPositions(row.length, 156);
            maxWidth = Math.max(maxWidth, 140 + Math.max(0, row.length - 1) * 156);
            for (let index = 0; index < row.length; index += 1) {
                positions.set(row[index].id(), {
                    x: xPositions[index],
                    y: cursorY
                });
            }
            cursorY += 58;
        }
        cursorY += 16;
    }
    if (cursorY === startY) {
        return { width: 0, height: 0 };
    }
    return {
        width: maxWidth,
        height: cursorY - startY
    };
}
function resolveDocumentLayerResolver(ownership, projectLayerById) {
    return (documentId) => {
        const projectId = ownership.documentToProject.get(documentId);
        return projectId ? projectLayerById.get(projectId) ?? 0 : 0;
    };
}
function resolveSymbolLayerResolver(ownership, projectLayerById) {
    return (symbolId) => {
        const documentId = ownership.symbolToDocument.get(symbolId);
        const projectId = documentId
            ? ownership.documentToProject.get(documentId)
            : null;
        return projectId ? projectLayerById.get(projectId) ?? 0 : 0;
    };
}
function resolveFlowBias(nodeId, adjacency, reverseAdjacency, resolveLayer) {
    const currentLayer = resolveLayer(nodeId);
    let totalWeight = 0;
    let weightedDelta = 0;
    for (const [neighborId, weight] of adjacency.get(nodeId)?.entries() ?? []) {
        weightedDelta += (resolveLayer(neighborId) - currentLayer) * weight;
        totalWeight += weight;
    }
    for (const [neighborId, weight] of reverseAdjacency.get(nodeId)?.entries() ?? []) {
        weightedDelta += (resolveLayer(neighborId) - currentLayer) * weight;
        totalWeight += weight;
    }
    if (totalWeight <= 0) {
        return 0;
    }
    return Math.max(-1, Math.min(1, weightedDelta / totalWeight));
}
function resolveNodeImportance(nodeId, adjacency, reverseAdjacency) {
    return resolveAdjacencyWeight(adjacency, nodeId) + resolveAdjacencyWeight(reverseAdjacency, nodeId);
}
function resolveAdjacencyWeight(adjacency, nodeId) {
    let total = 0;
    for (const weight of adjacency.get(nodeId)?.values() ?? []) {
        total += weight;
    }
    return total;
}
function compareSemanticDocumentLayouts(left, right) {
    const importanceComparison = right.importance - left.importance;
    if (importanceComparison !== 0) {
        return importanceComparison;
    }
    const biasComparison = left.bias - right.bias;
    if (biasComparison !== 0) {
        return biasComparison;
    }
    return compareNodeLabels(left.documentNode, right.documentNode);
}
function resolveProjectDocumentColumnCount(documentCount, maxDocumentWidth) {
    if (documentCount <= 0) {
        return 1;
    }
    if (documentCount <= 4) {
        return 1;
    }
    if (documentCount <= 10) {
        return 2;
    }
    if (documentCount <= 20) {
        return state.includeSymbols || maxDocumentWidth > 220 ? 3 : 4;
    }
    if (documentCount <= 40) {
        return state.includeSymbols ? 4 : 5;
    }
    return state.includeSymbols ? 5 : 6;
}
function resolvePreferredColumnFromBias(bias, columnCount) {
    if (columnCount <= 1) {
        return 0;
    }
    const center = (columnCount - 1) / 2;
    return Math.max(0, Math.min(columnCount - 1, Math.round(center + bias * center)));
}
function resolveSymbolColumnCount(symbolCount) {
    if (symbolCount <= 10) {
        return 1;
    }
    if (symbolCount <= 28) {
        return 2;
    }
    if (symbolCount <= 54) {
        return 3;
    }
    return 4;
}
function resolveExternalRowSize(nodeCount) {
    if (nodeCount <= 4) {
        return nodeCount;
    }
    if (nodeCount <= 12) {
        return 4;
    }
    if (nodeCount <= 24) {
        return 5;
    }
    return 6;
}
function applySemanticClusterPosition(cluster, centerX, topY) {
    if (!state.cy) {
        return;
    }
    for (const [nodeId, position] of cluster.positions.entries()) {
        const node = state.cy.getElementById(nodeId);
        if (node.empty()) {
            continue;
        }
        positionNodePreservingLock(node, centerX + position.x, topY + position.y);
    }
}
function isSymbolDependencyKind(kind) {
    return kind === "symbol-reference" ||
        kind === "symbol-call" ||
        kind === "symbol-inheritance" ||
        kind === "symbol-implementation" ||
        kind === "symbol-creation";
}
