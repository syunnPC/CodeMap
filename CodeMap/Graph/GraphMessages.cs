using CodeMap.ViewModels;

namespace CodeMap.Graph;

internal sealed record GraphControlMessage(string Type);

internal sealed record GraphLocalePayload(string Locale);

internal sealed record GraphLocaleMessage(
    string Type,
    GraphLocalePayload Data);

internal sealed record GraphThemePayload(string Theme);

internal sealed record GraphThemeMessage(
    string Type,
    GraphThemePayload Data);

internal sealed record GraphRenderMessage(
    string Type,
    GraphPayload Data);

internal sealed record GraphViewStateMessage(
    string Type,
    GraphViewState Data);

internal sealed record GraphFocusNodePayload(
    string NodeId,
    string Label,
    bool EnableDependencyMap,
    bool ForceVisible);

internal sealed record GraphFocusNodeMessage(
    string Type,
    GraphFocusNodePayload Data);

internal sealed record GraphSearchQueryPayload(string Query);

internal sealed record GraphSearchQueryMessage(
    string Type,
    GraphSearchQueryPayload Data);

internal sealed record GraphPerformanceMetricsModePayload(bool Enabled);

internal sealed record GraphPerformanceMetricsModeMessage(
    string Type,
    GraphPerformanceMetricsModePayload Data);
