using System.Collections.Generic;

namespace CodeMap.Graph;

public sealed record GraphPayload(
    IReadOnlyList<GraphNodePayload> Nodes,
    IReadOnlyList<GraphEdgePayload> Edges,
    GraphStatsPayload Stats);

public sealed record GraphNodePayload(
    string Id,
    string Label,
    string Group,
    string? ParentId,
    string? SymbolKind,
    string? FileName = null,
    bool IsInCycle = false);

public sealed record GraphEdgePayload(
    string Id,
    string Source,
    string Target,
    string Kind,
    int Weight,
    string? ReferenceKind = null,
    string? Confidence = null,
    bool IsCycleEdge = false,
    string? SampleFilePath = null,
    int? SampleLineNumber = null,
    string? SampleSnippet = null);

public sealed record GraphStatsPayload(
    int ProjectCount,
    int DocumentCount,
    int SymbolCount,
    int NativeDependencyCount,
    int DocumentDependencyCount,
    int SymbolDependencyCount,
    int ProjectCycleCount,
    int DocumentCycleCount,
    int SymbolCycleCount,
    int EdgeCount);
