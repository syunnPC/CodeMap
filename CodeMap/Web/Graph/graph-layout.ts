const layoutSequence = ["semantic-flow", "breadthfirst", "cose", "concentric", "grid"] as const;

function cycleLayoutAndApply(): void {
  state.layoutIndex = (state.layoutIndex + 1) % layoutSequence.length;
  applyLayout(layoutSequence[state.layoutIndex]);
}

type LayoutSelectionMetrics = VisibleGraphMetrics & {
  connectedComponentCount: number;
  largestComponentSize: number;
  averageDegree: number;
};

function applyPreferredLayoutForPayload(
  payload: GraphPayload,
  performanceSample: GraphRenderPerformanceSample | null = null): void
{
  const layoutStartedAt = performance.now();
  const metrics = state.cy
    ? collectVisibleGraphMetricsFromCy()
    : collectLayoutSelectionMetrics(payload);
  const preferredLayout = resolvePreferredLayoutName(metrics);
  if (performanceSample) {
    performanceSample.layoutName = preferredLayout;
  }

  applyPreferredLayout(preferredLayout, metrics, performanceSample);
  if (performanceSample) {
    performanceSample.layoutMs = performance.now() - layoutStartedAt;
  }
}

function collectLayoutSelectionMetrics(payload: GraphPayload): LayoutSelectionMetrics {
  const visibleNodeIds = new Set<string>();
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

  const adjacency = new Map<string, Set<string>>();
  for (const nodeId of visibleNodeIds) {
    adjacency.set(nodeId, new Set<string>());
  }

  let visibleEdgeCount = 0;
  const connectedNodeIds = new Set<string>();
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
  const visited = new Set<string>();

  for (const nodeId of visibleNodeIds) {
    if (visited.has(nodeId)) {
      continue;
    }

    connectedComponentCount += 1;
    let componentSize = 0;
    const stack = [nodeId];
    visited.add(nodeId);

    while (stack.length > 0) {
      const current = stack.pop()!;
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

function fitGraphViewportToCollection(elements: any, padding: number): boolean {
  if (!state.cy || !elements || elements.length === 0) {
    return false;
  }

  const viewportBounds = graphContainer?.getBoundingClientRect();
  const viewportWidth = Math.round(viewportBounds?.width ?? state.cy.width());
  const viewportHeight = Math.round(viewportBounds?.height ?? state.cy.height());
  if (viewportWidth <= 1 || viewportHeight <= 1) {
    state.pendingViewportFitPadding = padding;
    return false;
  }

  state.pendingViewportFitPadding = null;
  state.cy.resize();
  state.cy.fit(elements, padding);
  return true;
}

function resolveMinimumReadableZoom(visibleNodeCount?: number): number {
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

function ensureReadableZoom(minimumZoom: number): void {
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

function forceGraphRender(): void {
  if (!state.cy || typeof state.cy.forceRender !== "function") {
    return;
  }

  state.cy.forceRender();
  window.requestAnimationFrame(() => {
    state.cy?.forceRender();
  });
}

function resolvePreferredLayoutName(metrics: LayoutSelectionMetrics): string {
  if (metrics.visibleEdgeCount === 0) {
    return "structured-massive";
  }

  const isolatedRatio = metrics.visibleNodeCount > 0
    ? metrics.isolatedNodeCount / metrics.visibleNodeCount
    : 0;

  const giantComponentRatio = metrics.visibleNodeCount > 0
    ? metrics.largestComponentSize / metrics.visibleNodeCount
    : 0;

  if (
    metrics.visibleNodeCount <= 160 &&
    metrics.averageDegree <= 2.2 &&
    giantComponentRatio >= 0.75
  ) {
    return "breadthfirst";
  }

  if (
    metrics.connectedComponentCount >= 16 &&
    giantComponentRatio < 0.45
  ) {
    return "structured-massive";
  }

  if (
    metrics.visibleNodeCount >= 2200 &&
    isolatedRatio >= 0.7
  ) {
    return "structured-massive";
  }

  if (
    metrics.visibleNodeCount >= 900 &&
    giantComponentRatio >= 0.6
  ) {
    return "cose";
  }

  return "semantic-flow";
}

function applyPreferredLayout(
  layoutName: string,
  metrics: VisibleGraphMetrics,
  performanceSample: GraphRenderPerformanceSample | null = null): void
{
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
    finalizeAppliedLayout(visibleElements, metrics, performanceSample);
    return;
  }

  if (layoutName === "structured-massive") {
    applyStructuredMassiveLayout(metrics);
    finalizeAppliedLayout(visibleElements, metrics, performanceSample);
    return;
  }

  applyLayout(layoutName, performanceSample);
}

function applyLayout(layoutName: string, performanceSample: GraphRenderPerformanceSample | null = null): void {
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
    finalizeAppliedLayout(visibleElements, metrics, performanceSample);
    return;
  }

  if (layoutName === "structured-massive") {
    applyStructuredMassiveLayout(metrics);
    finalizeAppliedLayout(visibleElements, metrics, performanceSample);
    return;
  }

  runCytoscapeLayout(visibleElements, resolveCytoscapeLayoutOptions(layoutName, metrics), metrics, performanceSample);
}

function collectVisibleGraphMetricsFromCy(): LayoutSelectionMetrics {
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

  const visibleNodeIds = new Set<string>();
  let symbolNodeCount = 0;
  visibleNodes.forEach((node: any) => {
    visibleNodeIds.add(String(node.id()));
    if (String(node.data("group") ?? "") === "symbol") {
      symbolNodeCount += 1;
    }
  });

  const adjacency = new Map<string, Set<string>>();
  for (const nodeId of visibleNodeIds) {
    adjacency.set(nodeId, new Set<string>());
  }

  const connectedNodeIds = new Set<string>();
  visibleEdges.forEach((edge: any) => {
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
  const visited = new Set<string>();

  for (const nodeId of visibleNodeIds) {
    if (visited.has(nodeId)) {
      continue;
    }

    connectedComponentCount += 1;
    let componentSize = 0;
    const stack = [nodeId];
    visited.add(nodeId);

    while (stack.length > 0) {
      const current = stack.pop()!;
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

function resolveCytoscapeLayoutOptions(layoutName: string, metrics: VisibleGraphMetrics): any {
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
        concentric: (node: any) => {
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
        nodeRepulsion: (node: any) => {
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
        idealEdgeLength: (edge: any) => {
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
        edgeElasticity: (edge: any) => {
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

function runCytoscapeLayout(
  elements: any,
  options: any,
  metrics: VisibleGraphMetrics,
  performanceSample: GraphRenderPerformanceSample | null = null): void
{
  if (!state.cy || !elements || elements.length === 0) {
    return;
  }

  if (performanceSample) {
    performanceSample.awaitingAsyncLayout = true;
    performanceSample.asyncLayoutStartedAt = performance.now();
  }

  const layout = elements.layout({
    ...options,
    stop: () => {
      if (performanceSample && performanceSample.asyncLayoutStartedAt !== null) {
        performanceSample.asyncLayoutMs = performance.now() - performanceSample.asyncLayoutStartedAt;
      }

      finalizeAppliedLayout(elements, metrics, performanceSample);
      if (performanceSample) {
        performanceSample.awaitingAsyncLayout = false;
        performanceSample.totalUntilLayoutStopMs = performance.now() - performanceSample.startedAt;
        postGraphPerformanceMetrics(performanceSample, "layout-stop");
      }
    }
  });

  layout.run();
}

function finalizeAppliedLayout(
  elements: any,
  metrics: VisibleGraphMetrics,
  performanceSample: GraphRenderPerformanceSample | null = null): void
{
  if (!state.cy) {
    return;
  }

  const finalizeStartedAt = performance.now();
  separateOverlappingNodeCollisions();
  if (metrics.visibleNodeCount <= 260) {
    separateOverlappingHighLevelNodes();
  }

  fitGraphViewportToCollection(elements, resolveViewportPadding(metrics));
  ensureReadableZoom(resolveMinimumReadableZoom(metrics.visibleNodeCount));
  forceGraphRender();
  if (performanceSample) {
    performanceSample.finalizeMs = performance.now() - finalizeStartedAt;
    if (!performanceSample.awaitingAsyncLayout) {
      performanceSample.totalUntilLayoutStopMs = performance.now() - performanceSample.startedAt;
    }
  }
}

function resolveViewportPadding(metrics: VisibleGraphMetrics): number {
  if (metrics.visibleNodeCount >= 1400) {
    return 120;
  }

  if (metrics.visibleNodeCount >= 500) {
    return 84;
  }

  return 56;
}

function applyStructuredMassiveLayout(metrics: VisibleGraphMetrics): void {
  if (!state.cy) {
    return;
  }

  const visibleNodes = state.cy.nodes().filter((node: any) => shouldNodeBeVisible(node.data()));
  const projectNodes = visibleNodes.filter((node: any) => String(node.data("group") ?? "") === "project");
  const documentNodes = visibleNodes.filter((node: any) => String(node.data("group") ?? "") === "document");
  const symbolNodes = visibleNodes.filter((node: any) => String(node.data("group") ?? "") === "symbol");
  const externalNodes = visibleNodes.filter((node: any) => isExternalDependencyGroup(String(node.data("group") ?? "")));

  state.cy.startBatch();
  layoutHorizontalStrip(projectNodes, -360, 260, metrics.visibleNodeCount >= 1200 ? 4 : 6, 120);
  layoutHorizontalStrip(documentNodes, -120, 188, metrics.visibleNodeCount >= 1200 ? 6 : 8, 92);
  layoutExternalDependencyStrip(externalNodes, metrics);
  layoutSymbolSections(symbolNodes, metrics);
  state.cy.endBatch();
}

function layoutHorizontalStrip(
  nodes: any,
  y: number,
  columnGap: number,
  maxColumns = 6,
  rowGap = 88): void
{
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

function layoutExternalDependencyStrip(nodes: any, metrics: VisibleGraphMetrics): void {
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

interface OverlapSeparationOptions {
  nodes: any;
  comparator: (left: any, right: any) => number;
  resolveColumnCount: (nodeCount: number) => number;
  columnGap: number | ((nodeCount: number) => number);
  rowGap: number | ((nodeCount: number) => number);
}

function separateOverlappingNodes(options: OverlapSeparationOptions): void {
  if (!state.cy) {
    return;
  }

  const targetNodes = options.nodes;
  if (targetNodes.length < 2) {
    return;
  }

  const collisionGroups = new Map<string, any[]>();
  targetNodes.forEach((node: any) => {
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
    const orderedNodes = group.sort(options.comparator);
    const anchor = orderedNodes[0].position();
    const columnCount = options.resolveColumnCount(orderedNodes.length);
    const rows = chunkPlacements(orderedNodes, columnCount);
    const rowOffset = (rows.length - 1) / 2;
    const columnGap = typeof options.columnGap === "function" ? options.columnGap(orderedNodes.length) : options.columnGap;
    const rowGap = typeof options.rowGap === "function" ? options.rowGap(orderedNodes.length) : options.rowGap;

    for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
      const row = rows[rowIndex];
      const xPositions = buildCenteredAxisPositions(row.length, columnGap);
      const y = Number(anchor.y ?? 0) + (rowIndex - rowOffset) * rowGap;

      for (let columnIndex = 0; columnIndex < row.length; columnIndex += 1) {
        positionNodePreservingLock(
          row[columnIndex],
          Number(anchor.x ?? 0) + xPositions[columnIndex],
          y);
      }
    }
  }
  state.cy.endBatch();

  if (hasCollisions) {
    forceGraphRender();
  }
}

function separateOverlappingExternalDependencyNodes(): void {
  if (!state.cy) {
    return;
  }

  separateOverlappingNodes({
    nodes: state.cy.nodes().not(".state-hidden").not(".filtered-out").not(".pinned")
      .filter((node: any) => isExternalDependencyGroup(String(node.data("group") ?? ""))),
    comparator: compareExternalDependencyNodes,
    resolveColumnCount: () => 3,
    columnGap: 172,
    rowGap: 70,
  });
}

function separateOverlappingCollapsedNodes(): void {
  if (!state.cy) {
    return;
  }

  separateOverlappingNodes({
    nodes: state.cy.nodes().not(".state-hidden").not(".filtered-out").not(".pinned")
      .filter((node: any) => !isExternalDependencyGroup(String(node.data("group") ?? ""))),
    comparator: compareNodeLabels,
    resolveColumnCount: (count) => Math.max(2, Math.min(16, Math.ceil(Math.sqrt(count)))),
    columnGap: (count) => count >= 80 ? 94 : 128,
    rowGap: (count) => count >= 80 ? 54 : 72,
  });
}

function separateOverlappingDocumentNodes(): void {
  if (!state.cy) {
    return;
  }

  separateOverlappingNodes({
    nodes: state.cy.nodes().not(".state-hidden").not(".filtered-out").not(".pinned")
      .filter((node: any) => String(node.data("group") ?? "") === "document"),
    comparator: compareNodeLabels,
    resolveColumnCount: (count) => Math.max(2, Math.min(8, Math.ceil(Math.sqrt(count)))),
    columnGap: 172,
    rowGap: 66,
  });
}

function separateOverlappingNodeCollisions(): void {
  separateOverlappingExternalDependencyNodes();
  separateOverlappingCollapsedNodes();
  separateOverlappingDocumentNodes();
}

function separateOverlappingHighLevelNodes(): void {
  if (!state.cy) {
    return;
  }

  const nodes = state.cy
    .nodes()
    .not(".state-hidden")
    .not(".filtered-out")
    .not(".pinned")
    .filter((node: any) => {
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

function layoutSymbolSections(nodes: any, metrics: VisibleGraphMetrics): void {
  if (!nodes || nodes.length === 0) {
    return;
  }

  const groups = new Map<string, any[]>();
  nodes.forEach((node: any) => {
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

function resolveSectionRowCount(nodeCount: number): number {
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

function compareNodeLabels(left: any, right: any): number {
  const leftLabel = String(left.data("label") ?? "");
  const rightLabel = String(right.data("label") ?? "");
  return leftLabel.localeCompare(rightLabel);
}

function compareExternalDependencyNodes(left: any, right: any): number {
  const groupComparison = resolveDependencyGroupRank(left) - resolveDependencyGroupRank(right);
  if (groupComparison !== 0) {
    return groupComparison;
  }

  return compareNodeLabels(left, right);
}

function applyDependencyMapLayout(selectedNode: any, dependencySeedNodes: any, dependencyEdges: any, neighborhood: any): void {
  if (!state.cy) {
    return;
  }

  const neighborhoodNodes = neighborhood.nodes();
  const neighborhoodNodesById = buildDependencyNodeIndex(neighborhoodNodes);
  const ownership = buildOwnershipMaps();
  const positionedNodeIds = new Set<string>();
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
  const centerLayout = layoutDependencyMapCenter(
    selectedNode,
    dependencySeedNodes,
    ownership,
    neighborhoodNodesById,
    positionedNodeIds);
  const incomingClusters = buildDependencyPlacementClusters(
    incomingOnly,
    ownership,
    neighborhoodNodesById,
    centerLayout.reservedNodeIds).sort(compareDependencyCluster);
  const outgoingClusters = buildDependencyPlacementClusters(
    outgoingOnly,
    ownership,
    neighborhoodNodesById,
    centerLayout.reservedNodeIds).sort(compareDependencyCluster);
  const bidirectionalClusters = buildDependencyPlacementClusters(
    bidirectional,
    ownership,
    neighborhoodNodesById,
    centerLayout.reservedNodeIds).sort(compareDependencyCluster);
  layoutDependencySide(incomingClusters, -1, positionedNodeIds);
  layoutDependencySide(outgoingClusters, 1, positionedNodeIds);
  layoutBidirectionalDependencies(bidirectionalClusters, positionedNodeIds);
  layoutRemainingDependencyNodes(neighborhoodNodes, positionedNodeIds);
  state.cy.endBatch();
  separateOverlappingNodeCollisions();

  neighborhood.nodes().forEach((node: any) => {
    if (node.id() === selectedNode.id()) {
      return;
    }

    node.unselect();
  });
}

function collectDependencyNeighborPlacements(selectionSeedNodes: any, dependencyEdges: any): DependencyNeighborPlacement[] {
  const placementsByNodeId = new Map<string, DependencyNeighborPlacement>();
  const seedNodeIds = buildDependencyNodeIdSet(selectionSeedNodes);

  dependencyEdges.forEach((edge: any) => {
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
    const nextDirection: DependencyNeighborDirection = isIncoming ? "incoming" : "outgoing";
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

function buildDependencyNodeIdSet(nodes: any): Set<string> {
  const nodeIds = new Set<string>();
  nodes.forEach((node: any) => {
    nodeIds.add(String(node.id()));
  });
  return nodeIds;
}

function buildDependencyNodeIndex(nodes: any): Map<string, any> {
  const index = new Map<string, any>();
  nodes.forEach((node: any) => {
    index.set(String(node.id()), node);
  });
  return index;
}

function layoutDependencyMapCenter(
  selectedNode: any,
  dependencySeedNodes: any,
  ownership: OwnershipMaps,
  neighborhoodNodesById: Map<string, any>,
  positionedNodeIds: Set<string>): DependencyCenterLayoutResult
{
  const reservedNodeIds = new Set<string>();
  const contextNodes: any[] = [];
  const projectNode = resolveDependencyProjectNode(selectedNode, ownership, neighborhoodNodesById);
  if (projectNode && projectNode.id() !== selectedNode.id()) {
    contextNodes.push(projectNode);
  }

  const documentNode = resolveDependencyDocumentNode(selectedNode, ownership, neighborhoodNodesById);
  if (documentNode &&
    documentNode.id() !== selectedNode.id() &&
    !contextNodes.some((node) => node.id() === documentNode.id()))
  {
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

  const extraSeedNodes: any[] = dependencySeedNodes
    .toArray()
    .filter((node: any) => !reservedNodeIds.has(node.id()))
    .sort(compareDependencyPeerNodes);
  if (extraSeedNodes.length > 0) {
    layoutDependencySeedNodes(extraSeedNodes, 132, positionedNodeIds);
  }

  return { reservedNodeIds };
}

function layoutDependencySeedNodes(seedNodes: any[], startY: number, positionedNodeIds: Set<string>): void {
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

function buildDependencyPlacementClusters(
  placements: DependencyNeighborPlacement[],
  ownership: OwnershipMaps,
  neighborhoodNodesById: Map<string, any>,
  reservedNodeIds: Set<string>): DependencyPlacementCluster[]
{
  const clustersByKey = new Map<string, DependencyPlacementCluster>();
  for (const placement of placements) {
    const context = resolveDependencyPlacementContext(
      placement.node,
      ownership,
      neighborhoodNodesById,
      reservedNodeIds);
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

function resolveDependencyPlacementContext(
  node: any,
  ownership: OwnershipMaps,
  neighborhoodNodesById: Map<string, any>,
  reservedNodeIds: Set<string>): { key: string; documentNode: any | null; projectNode: any | null }
{
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

function resolveDependencyContextNode(
  nodeId: string | null,
  neighborhoodNodesById: Map<string, any>,
  reservedNodeIds: Set<string>): any | null
{
  if (!nodeId || reservedNodeIds.has(nodeId)) {
    return null;
  }

  return neighborhoodNodesById.get(nodeId) ?? null;
}

function resolveDependencyDocumentNode(
  node: any,
  ownership: OwnershipMaps,
  neighborhoodNodesById: Map<string, any>): any | null
{
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

function resolveDependencyProjectNode(
  node: any,
  ownership: OwnershipMaps,
  neighborhoodNodesById: Map<string, any>): any | null
{
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

function layoutDependencySide(
  clusters: DependencyPlacementCluster[],
  directionMultiplier: -1 | 1,
  positionedNodeIds: Set<string>): void
{
  if (clusters.length === 0) {
    return;
  }

  const lanes = new Map<number, DependencyPlacementCluster[]>();
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
    const columns = Array.from({ length: columnCount }, () => [] as DependencyPlacementCluster[]);
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

function layoutBidirectionalDependencies(
  clusters: DependencyPlacementCluster[],
  positionedNodeIds: Set<string>): void
{
  if (clusters.length === 0) {
    return;
  }

  const itemsPerRow = resolveBidirectionalClusterColumnCount(clusters.length);
  const rowGap = 44;
  const columnGap = 48;
  const rows = chunkPlacements(clusters, itemsPerRow);
  const rowHeights = rows.map((row) => row.reduce(
    (maxHeight, cluster) => Math.max(maxHeight, measureDependencyPlacementCluster(cluster).height),
    0));
  const totalHeight = rowHeights.reduce((sum, height) => sum + height, 0) +
    Math.max(0, rowHeights.length - 1) * rowGap;
  let cursorY = -Math.max(220, totalHeight + 140);
  for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
    const rowClusters = rows[rowIndex];
    const rowHeight = rowHeights[rowIndex];
    const rowWidth = rowClusters.reduce(
      (sum, cluster) => sum + measureDependencyPlacementCluster(cluster).width,
      0) +
      Math.max(0, rowClusters.length - 1) * columnGap;
    let cursorX = -rowWidth / 2;
    for (const cluster of rowClusters) {
      const metrics = measureDependencyPlacementCluster(cluster);
      layoutDependencyCluster(
        cluster,
        metrics,
        cursorX + metrics.width / 2,
        cursorY + (rowHeight - metrics.height) / 2,
        0,
        positionedNodeIds);
      cursorX += metrics.width + columnGap;
    }
    cursorY += rowHeight + rowGap;
  }
}

function measureDependencyPlacementCluster(cluster: DependencyPlacementCluster): DependencyPlacementClusterMetrics {
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

function layoutDependencyCluster(
  cluster: DependencyPlacementCluster,
  metrics: DependencyPlacementClusterMetrics,
  centerX: number,
  topY: number,
  peerBiasX: number,
  positionedNodeIds: Set<string>): void
{
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
    positionNodePreservingLock(
      node,
      peerCenterX + xPositions[columnIndex],
      cursorY + rowIndex * 56);
    positionedNodeIds.add(node.id());
  }
}

function layoutRemainingDependencyNodes(nodes: any, positionedNodeIds: Set<string>): void {
  const remainingNodes: any[] = nodes
    .toArray()
    .filter((node: any) => !positionedNodeIds.has(node.id()))
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

function compareDependencyCluster(left: DependencyPlacementCluster, right: DependencyPlacementCluster): number {
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

function resolveDependencyClusterLabel(cluster: DependencyPlacementCluster): string {
  const anchorNode = cluster.documentNode ?? cluster.projectNode ?? cluster.peerNodes[0];
  return String(anchorNode?.data("label") ?? cluster.key);
}

function compareDependencyPeerNodes(left: any, right: any): number {
  const groupComparison = resolveDependencyGroupRank(left) - resolveDependencyGroupRank(right);
  if (groupComparison !== 0) {
    return groupComparison;
  }

  return compareNodeLabels(left, right);
}

function resolveDependencyClusterLaneIndex(node: any): number {
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

function resolveDependencyCenterSeedColumnCount(nodeCount: number): number {
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

function resolveDependencyClusterPeerColumnCount(peerCount: number): number {
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

function resolveDependencyLaneColumnCount(clusterCount: number): number {
  if (clusterCount <= 5) {
    return 1;
  }

  if (clusterCount <= 12) {
    return 2;
  }

  return 3;
}

function resolveBidirectionalClusterColumnCount(clusterCount: number): number {
  if (clusterCount <= 2) {
    return Math.max(1, clusterCount);
  }

  if (clusterCount <= 6) {
    return 3;
  }

  return 4;
}

function chunkPlacements<T>(items: T[], chunkSize: number): T[][] {
  if (items.length === 0 || chunkSize <= 0) {
    return [];
  }

  const chunks: T[][] = [];
  for (let index = 0; index < items.length; index += chunkSize) {
    chunks.push(items.slice(index, index + chunkSize));
  }

  return chunks;
}

function buildCenteredAxisPositions(count: number, gap: number): number[] {
  if (count <= 0) {
    return [];
  }

  const positions: number[] = [];
  const offset = (count - 1) / 2;
  for (let index = 0; index < count; index += 1) {
    positions.push((index - offset) * gap);
  }

  return positions;
}

function positionNodePreservingLock(node: any, x: number, y: number): void {
  const wasLocked = typeof node.locked === "function" ? Boolean(node.locked()) : false;
  if (wasLocked && typeof node.unlock === "function") {
    node.unlock();
  }

  node.position({ x, y });

  if (wasLocked && typeof node.lock === "function") {
    node.lock();
  }
}

function compareDependencyPlacement(left: DependencyNeighborPlacement, right: DependencyNeighborPlacement): number {
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

function resolveDependencyLaneIndex(placement: DependencyNeighborPlacement): number {
  const group = String(placement.node.data("group") ?? "");
  if (group === "symbol") {
    return 0;
  }

  if (group === "document" || group === "project") {
    return 1;
  }

  return 2;
}

function resolveDependencyGroupRank(node: any): number {
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

function resolveDependencyKindRank(kind: string): number {
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

function pickPreferredDependencyKind(currentKind: string, nextKind: string): string {
  return resolveDependencyKindRank(nextKind) < resolveDependencyKindRank(currentKind)
    ? nextKind
    : currentKind;
}

function syncLayoutIndex(layoutName: string): void {
  const nextIndex = layoutSequence.findIndex((candidate) => candidate === layoutName);
  if (nextIndex >= 0) {
    state.layoutIndex = nextIndex;
  }
}

function applySemanticFlowLayout(metrics: VisibleGraphMetrics): void {
  if (!state.cy) {
    return;
  }

  const visibleNodes = state.cy.nodes().not(".state-hidden").not(".filtered-out");
  if (visibleNodes.length === 0) {
    return;
  }

  const visibleEdges = state.cy.edges().not(".state-hidden").not(".filtered-out");
  const ownership = buildOwnershipMaps();
  const visibleNodesById = new Map<string, any>();
  visibleNodes.forEach((node: any) => {
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

  const clusterByProjectId = new Map<string, SemanticProjectCluster>();
  for (const layer of projectLayers) {
    for (const projectId of layer) {
      clusterByProjectId.set(
        projectId,
        buildSemanticProjectCluster(
          projectId,
          visibleNodesById,
          ownership,
          documentAdjacency,
          reverseDocumentAdjacency,
          symbolAdjacency,
          reverseSymbolAdjacency,
          projectLayerById));
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

function buildOwnershipMaps(): OwnershipMaps {
  const ownership: OwnershipMaps = {
    documentToProject: new Map<string, string>(),
    symbolToDocument: new Map<string, string>(),
    externalToProject: new Map<string, string>(),
    projectToDocuments: new Map<string, string[]>(),
    documentToSymbols: new Map<string, string[]>(),
    projectLabels: new Map<string, string>()
  };

  if (!state.cy) {
    return ownership;
  }

  state.cy.nodes().forEach((node: any) => {
    if (String(node.data("group") ?? "") === "project") {
      ownership.projectLabels.set(node.id(), String(node.data("label") ?? node.id()));
    }
  });

  state.cy.edges().forEach((edge: any) => {
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

function appendOwnedValue(map: Map<string, string[]>, key: string, value: string): void {
  const current = map.get(key);
  if (current) {
    current.push(value);
    return;
  }

  map.set(key, [value]);
}

function collectSemanticProjectIds(visibleNodes: any, ownership: OwnershipMaps): string[] {
  const projectIds = new Set<string>();
  visibleNodes.forEach((node: any) => {
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

function resolveProjectScopeId(nodeId: string, group: string, ownership: OwnershipMaps): string | null {
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

function resolveDocumentScopeId(nodeId: string, group: string, ownership: OwnershipMaps): string | null {
  if (group === "document") {
    return nodeId;
  }

  if (group === "symbol") {
    return ownership.symbolToDocument.get(nodeId) ?? null;
  }

  return null;
}

function ensureProjectLabel(ownership: OwnershipMaps, projectId: string): void {
  if (ownership.projectLabels.has(projectId) || !state.cy) {
    return;
  }

  const projectNode = state.cy.getElementById(projectId);
  ownership.projectLabels.set(projectId, projectNode.empty()
    ? projectId
    : String(projectNode.data("label") ?? projectId));
}

function resolveProjectLabel(ownership: OwnershipMaps, projectId: string): string {
  ensureProjectLabel(ownership, projectId);
  return ownership.projectLabels.get(projectId) ?? projectId;
}

function buildProjectDependencyGraph(visibleEdges: any, ownership: OwnershipMaps): WeightedAdjacency {
  const adjacency: WeightedAdjacency = new Map();
  visibleEdges.forEach((edge: any) => {
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

function buildDocumentDependencyGraph(visibleEdges: any, ownership: OwnershipMaps): WeightedAdjacency {
  const adjacency: WeightedAdjacency = new Map();
  visibleEdges.forEach((edge: any) => {
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

function buildSymbolDependencyGraph(visibleEdges: any): WeightedAdjacency {
  const adjacency: WeightedAdjacency = new Map();
  visibleEdges.forEach((edge: any) => {
    const kind = String(edge.data("kind") ?? "");
    if (!isSymbolDependencyKind(kind)) {
      return;
    }

    addAdjacencyWeight(
      adjacency,
      String(edge.source().id()),
      String(edge.target().id()),
      resolveSymbolAggregationWeight(kind, Number(edge.data("weight") ?? 1)));
  });

  return adjacency;
}

function addAdjacencyWeight(adjacency: WeightedAdjacency, sourceId: string, targetId: string, weight: number): void {
  if (!Number.isFinite(weight) || weight <= 0) {
    return;
  }

  let targets = adjacency.get(sourceId);
  if (!targets) {
    targets = new Map<string, number>();
    adjacency.set(sourceId, targets);
  }

  targets.set(targetId, (targets.get(targetId) ?? 0) + weight);
}

function buildReverseAdjacency(adjacency: WeightedAdjacency): WeightedAdjacency {
  const reverse: WeightedAdjacency = new Map();
  for (const [sourceId, targets] of adjacency.entries()) {
    for (const [targetId, weight] of targets.entries()) {
      addAdjacencyWeight(reverse, targetId, sourceId, weight);
    }

    if (!reverse.has(sourceId)) {
      reverse.set(sourceId, new Map<string, number>());
    }
  }

  return reverse;
}

function resolveProjectAggregationWeight(kind: string, weight: number): number {
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

function resolveDocumentAggregationWeight(kind: string, weight: number): number {
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

function resolveSymbolAggregationWeight(kind: string, weight: number): number {
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

function computeProjectLayers(projectIds: string[], adjacency: WeightedAdjacency, labels: Map<string, string>): string[][] {
  if (projectIds.length === 0) {
    return [];
  }

  const components = computeStronglyConnectedComponents(projectIds, adjacency);
  const componentMembers = new Map<string, string[]>();
  const nodeToComponentId = new Map<string, string>();
  const componentLabels = new Map<string, string>();

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

  const componentAdjacency: WeightedAdjacency = new Map();
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
  const indegree = new Map<string, number>();
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
  const topologicalOrder: string[] = [];

  while (frontier.length > 0) {
    const current = frontier.shift()!;
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

  const layerByComponentId = new Map<string, number>();
  for (const componentId of topologicalOrder) {
    let layerIndex = 0;
    for (const predecessorId of reverseComponentAdjacency.get(componentId)?.keys() ?? []) {
      layerIndex = Math.max(layerIndex, (layerByComponentId.get(predecessorId) ?? 0) + 1);
    }

    layerByComponentId.set(componentId, layerIndex);
  }

  const maxLayer = topologicalOrder.reduce((max, componentId) => Math.max(max, layerByComponentId.get(componentId) ?? 0), 0);
  const componentLayers = Array.from({ length: maxLayer + 1 }, () => [] as string[]);
  for (const componentId of topologicalOrder) {
    componentLayers[layerByComponentId.get(componentId) ?? 0].push(componentId);
  }

  refineLayerOrdering(componentLayers, componentAdjacency, reverseComponentAdjacency, componentLabels);
  const projectLayers = componentLayers.map((componentLayer) =>
    componentLayer.flatMap((componentId) =>
      [...(componentMembers.get(componentId) ?? [])].sort((left, right) =>
        compareProjectsWithinComponent(left, right, adjacency, labels))));

  if (projectLayers.length === 1 && projectLayers[0].length > 6) {
    return chunkPlacements(projectLayers[0], Math.ceil(Math.sqrt(projectLayers[0].length)));
  }

  return projectLayers;
}

function computeStronglyConnectedComponents(nodeIds: string[], adjacency: WeightedAdjacency): string[][] {
  const indexByNodeId = new Map<string, number>();
  const lowLinkByNodeId = new Map<string, number>();
  const stack: string[] = [];
  const onStack = new Set<string>();
  const components: string[][] = [];
  let currentIndex = 0;

  const visit = (nodeId: string): void => {
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

    const component: string[] = [];
    while (stack.length > 0) {
      const currentNodeId = stack.pop()!;
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

function refineLayerOrdering(
  layers: string[][],
  adjacency: WeightedAdjacency,
  reverseAdjacency: WeightedAdjacency,
  labels: Map<string, string>): void
{
  if (layers.length <= 1) {
    return;
  }

  for (let iteration = 0; iteration < 3; iteration += 1) {
    let orderingIndex = buildOrderingIndexMap(layers);
    for (let layerIndex = 1; layerIndex < layers.length; layerIndex += 1) {
      layers[layerIndex].sort((left, right) =>
        compareLayerMembers(left, right, reverseAdjacency, orderingIndex, labels));
    }

    orderingIndex = buildOrderingIndexMap(layers);
    for (let layerIndex = layers.length - 2; layerIndex >= 0; layerIndex -= 1) {
      layers[layerIndex].sort((left, right) =>
        compareLayerMembers(left, right, adjacency, orderingIndex, labels));
    }
  }
}

function compareLayerMembers(
  leftId: string,
  rightId: string,
  adjacency: WeightedAdjacency,
  orderingIndex: Map<string, number>,
  labels: Map<string, string>): number
{
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

function compareLayerMembersByLabel(leftId: string, rightId: string, labels: Map<string, string>): number {
  return (labels.get(leftId) ?? leftId).localeCompare(labels.get(rightId) ?? rightId);
}

function resolveAdjacencyBarycenter(nodeId: string, adjacency: WeightedAdjacency, orderingIndex: Map<string, number>): number | null {
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

function buildOrderingIndexMap(layers: string[][]): Map<string, number> {
  const orderingIndex = new Map<string, number>();
  for (const layer of layers) {
    for (let index = 0; index < layer.length; index += 1) {
      orderingIndex.set(layer[index], index);
    }
  }

  return orderingIndex;
}

function buildLayerIndexMap(layers: string[][]): Map<string, number> {
  const layerIndex = new Map<string, number>();
  for (let index = 0; index < layers.length; index += 1) {
    for (const memberId of layers[index]) {
      layerIndex.set(memberId, index);
    }
  }

  return layerIndex;
}

function compareProjectsWithinComponent(
  leftProjectId: string,
  rightProjectId: string,
  adjacency: WeightedAdjacency,
  labels: Map<string, string>): number
{
  const leftWeight = resolveAdjacencyWeight(adjacency, leftProjectId);
  const rightWeight = resolveAdjacencyWeight(adjacency, rightProjectId);
  if (leftWeight !== rightWeight) {
    return rightWeight - leftWeight;
  }

  return (labels.get(leftProjectId) ?? leftProjectId).localeCompare(labels.get(rightProjectId) ?? rightProjectId);
}

function buildSemanticProjectCluster(
  projectId: string,
  visibleNodesById: Map<string, any>,
  ownership: OwnershipMaps,
  documentAdjacency: WeightedAdjacency,
  reverseDocumentAdjacency: WeightedAdjacency,
  symbolAdjacency: WeightedAdjacency,
  reverseSymbolAdjacency: WeightedAdjacency,
  projectLayerById: Map<string, number>): SemanticProjectCluster
{
  const positions = new Map<string, LocalNodePosition>();
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
    .filter((documentId) =>
      visibleNodesById.has(documentId) ||
      (ownership.documentToSymbols.get(documentId) ?? []).some((symbolId) => visibleNodesById.has(symbolId)))
    .map((documentId) =>
      buildSemanticDocumentLayout(
        documentId,
        visibleNodesById,
        ownership,
        documentAdjacency,
        reverseDocumentAdjacency,
        symbolAdjacency,
        reverseSymbolAdjacency,
        projectLayerById))
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

function buildSemanticDocumentLayout(
  documentId: string,
  visibleNodesById: Map<string, any>,
  ownership: OwnershipMaps,
  documentAdjacency: WeightedAdjacency,
  reverseDocumentAdjacency: WeightedAdjacency,
  symbolAdjacency: WeightedAdjacency,
  reverseSymbolAdjacency: WeightedAdjacency,
  projectLayerById: Map<string, number>): SemanticDocumentLayout
{
  const documentNode = visibleNodesById.get(documentId) ?? state.cy?.getElementById(documentId);
  const positions = new Map<string, LocalNodePosition>();
  const documentHeaderY = 24;
  positions.set(documentId, { x: 0, y: documentHeaderY });

  let cursorY = documentHeaderY + 76;
  let width = 132;
  const symbolIds = (ownership.documentToSymbols.get(documentId) ?? [])
    .filter((symbolId) => visibleNodesById.has(symbolId));
  const symbolLayerResolver = resolveSymbolLayerResolver(ownership, projectLayerById);
  const symbolGroups = new Map<string, Array<{ node: any; bias: number; importance: number }>>();

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
    bias: resolveFlowBias(
      documentId,
      documentAdjacency,
      reverseDocumentAdjacency,
      resolveDocumentLayerResolver(ownership, projectLayerById)),
    importance: resolveNodeImportance(documentId, documentAdjacency, reverseDocumentAdjacency) + symbolIds.length * 0.35,
    positions
  };
}

function layoutSemanticExternalNodes(
  projectId: string,
  visibleNodesById: Map<string, any>,
  ownership: OwnershipMaps,
  positions: Map<string, LocalNodePosition>,
  startY: number): { width: number; height: number }
{
  const externalGroups: Record<"package" | "assembly" | "dll", any[]> = {
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
  for (const group of ["package", "assembly", "dll"] as const) {
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

function resolveDocumentLayerResolver(
  ownership: OwnershipMaps,
  projectLayerById: Map<string, number>): (nodeId: string) => number
{
  return (documentId: string) => {
    const projectId = ownership.documentToProject.get(documentId);
    return projectId ? projectLayerById.get(projectId) ?? 0 : 0;
  };
}

function resolveSymbolLayerResolver(
  ownership: OwnershipMaps,
  projectLayerById: Map<string, number>): (nodeId: string) => number
{
  return (symbolId: string) => {
    const documentId = ownership.symbolToDocument.get(symbolId);
    const projectId = documentId
      ? ownership.documentToProject.get(documentId)
      : null;
    return projectId ? projectLayerById.get(projectId) ?? 0 : 0;
  };
}

function resolveFlowBias(
  nodeId: string,
  adjacency: WeightedAdjacency,
  reverseAdjacency: WeightedAdjacency,
  resolveLayer: (nodeId: string) => number): number
{
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

function resolveNodeImportance(nodeId: string, adjacency: WeightedAdjacency, reverseAdjacency: WeightedAdjacency): number {
  return resolveAdjacencyWeight(adjacency, nodeId) + resolveAdjacencyWeight(reverseAdjacency, nodeId);
}

function resolveAdjacencyWeight(adjacency: WeightedAdjacency, nodeId: string): number {
  let total = 0;
  for (const weight of adjacency.get(nodeId)?.values() ?? []) {
    total += weight;
  }

  return total;
}

function compareSemanticDocumentLayouts(left: SemanticDocumentLayout, right: SemanticDocumentLayout): number {
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

function resolveProjectDocumentColumnCount(documentCount: number, maxDocumentWidth: number): number {
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

function resolvePreferredColumnFromBias(bias: number, columnCount: number): number {
  if (columnCount <= 1) {
    return 0;
  }

  const center = (columnCount - 1) / 2;
  return Math.max(0, Math.min(columnCount - 1, Math.round(center + bias * center)));
}

function resolveSymbolColumnCount(symbolCount: number): number {
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

function resolveExternalRowSize(nodeCount: number): number {
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

function applySemanticClusterPosition(cluster: SemanticProjectCluster, centerX: number, topY: number): void {
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

function isSymbolDependencyKind(kind: string): boolean {
  return kind === "symbol-reference" ||
    kind === "symbol-call" ||
    kind === "symbol-inheritance" ||
    kind === "symbol-implementation" ||
    kind === "symbol-creation";
}
