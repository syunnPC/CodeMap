using System.Collections.Generic;
using System.Text.Json.Serialization;
using CodeMap.Graph;
using CodeMap.ViewModels;

namespace CodeMap;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppPreferences))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Dictionary<string, SolutionViewState>))]
[JsonSerializable(typeof(GraphPayload))]
[JsonSerializable(typeof(GraphControlMessage))]
[JsonSerializable(typeof(GraphLocaleMessage))]
[JsonSerializable(typeof(GraphThemeMessage))]
[JsonSerializable(typeof(GraphRenderMessage))]
[JsonSerializable(typeof(GraphViewStateMessage))]
[JsonSerializable(typeof(GraphFocusNodeMessage))]
[JsonSerializable(typeof(GraphSearchQueryMessage))]
[JsonSerializable(typeof(GraphPerformanceMetricsModeMessage))]
internal sealed partial class CodeMapJsonSerializerContext : JsonSerializerContext
{
}
