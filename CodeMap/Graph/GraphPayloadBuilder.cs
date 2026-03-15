using CodeMap.Analysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CodeMap.Graph;

public static class GraphPayloadBuilder
{
    private const int DefaultMaxSymbolNodes = 3000;
    private readonly record struct DependencyNodeIdentity(string Prefix, string Identity);
    private readonly record struct EdgeIdentity(
        string Source,
        string Target,
        string Kind,
        string? ReferenceKind,
        string? Confidence);

    public static GraphPayload Build(
        SolutionAnalysisSnapshot snapshot,
        int maxSymbolNodes = DefaultMaxSymbolNodes,
        bool includeSymbols = true)
    {
        int maxVisibleSymbolNodes = includeSymbols
            ? Math.Max(0, maxSymbolNodes)
            : 0;
        bool includeSymbolGraph = maxVisibleSymbolNodes > 0;

        HashSet<string> nodeIds = new(StringComparer.Ordinal);
        HashSet<EdgeIdentity> edgeIdentities = [];
        HashSet<string> visibleDocumentNodeIds = new(StringComparer.Ordinal);
        HashSet<string>? visibleSymbolNodeIds = includeSymbolGraph
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;
        List<GraphNodePayload> nodes = [];
        List<GraphEdgePayload> edges = [];
        Dictionary<string, string> projectNodeIdsByKey = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<DependencyNodeIdentity, string> dependencyNodeIds = [];
        Dictionary<string, HashSet<string>> cycleIdsByNodeId = new(StringComparer.Ordinal);
        List<(ProjectAnalysisSummary Project, string NodeId)> projectEntries = new(snapshot.Projects.Count);

        foreach (ProjectAnalysisSummary project in snapshot.Projects)
        {
            string projectNodeId = BuildProjectNodeId(project, dependencyNodeIds);
            projectEntries.Add((project, projectNodeId));
            if (!projectNodeIdsByKey.ContainsKey(project.ProjectKey))
            {
                projectNodeIdsByKey.Add(project.ProjectKey, projectNodeId);
            }
        }

        foreach (DependencyCycleSummary cycle in snapshot.Cycles)
        {
            if (!includeSymbolGraph &&
                string.Equals(cycle.GraphKind, "symbol", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string cycleNodeId in cycle.NodeIds)
            {
                string resolvedNodeId = ResolveCycleNodeId(cycle.GraphKind, cycleNodeId, projectNodeIdsByKey);
                if (!cycleIdsByNodeId.TryGetValue(resolvedNodeId, out HashSet<string>? cycleIds))
                {
                    cycleIds = new HashSet<string>(StringComparer.Ordinal);
                    cycleIdsByNodeId.Add(resolvedNodeId, cycleIds);
                }

                cycleIds.Add(cycle.CycleId);
            }
        }

        int totalSymbolCount = 0;
        int visibleSymbolCount = 0;
        int totalDocumentCount = 0;
        int totalNativeDependencyCount = 0;

        foreach ((ProjectAnalysisSummary project, string projectNodeId) in projectEntries)
        {
            AddNode(
                nodes,
                nodeIds,
                new GraphNodePayload(
                    projectNodeId,
                    project.Name,
                    "project",
                    null,
                    null,
                    GetDisplayFileName(project.ProjectFilePath, project.Name),
                    cycleIdsByNodeId.ContainsKey(projectNodeId)));

            foreach (DocumentAnalysisSummary document in project.Documents)
            {
                totalDocumentCount++;
                string documentNodeId = document.Id;
                AddNode(
                    nodes,
                    nodeIds,
                    new GraphNodePayload(
                        documentNodeId,
                        document.Name,
                        "document",
                        null,
                        null,
                        GetDisplayFileName(document.FilePath, document.Name),
                        cycleIdsByNodeId.ContainsKey(documentNodeId)));
                visibleDocumentNodeIds.Add(documentNodeId);
                AddEdge(
                    edges,
                    edgeIdentities,
                    source: projectNodeId,
                    target: documentNodeId,
                    kind: "contains-document",
                    weight: 1,
                    isCycleEdge: IsCycleEdge(cycleIdsByNodeId, projectNodeId, documentNodeId));

                totalSymbolCount += document.Symbols.Count;
                if (!includeSymbolGraph)
                {
                    continue;
                }

                foreach (SymbolAnalysisSummary symbol in document.Symbols)
                {
                    if (visibleSymbolCount >= maxVisibleSymbolNodes)
                    {
                        continue;
                    }

                    visibleSymbolCount++;
                    string symbolLabel = string.IsNullOrWhiteSpace(symbol.DisplayName)
                        ? symbol.Name
                        : symbol.DisplayName;

                    AddNode(
                        nodes,
                        nodeIds,
                        new GraphNodePayload(
                            symbol.Id,
                            symbolLabel,
                            "symbol",
                            null,
                            symbol.Kind,
                            GetDisplayFileName(document.FilePath, document.Name),
                            cycleIdsByNodeId.ContainsKey(symbol.Id)));

                    AddEdge(
                        edges,
                        edgeIdentities,
                        source: documentNodeId,
                        target: symbol.Id,
                        kind: "contains-symbol",
                        weight: 1,
                        isCycleEdge: IsCycleEdge(cycleIdsByNodeId, documentNodeId, symbol.Id));

                    visibleSymbolNodeIds!.Add(symbol.Id);
                }
            }

            foreach (string packageReference in project.PackageReferences)
            {
                string packageNodeId = BuildDependencyNodeId("package", packageReference, dependencyNodeIds);
                AddNode(nodes, nodeIds, new GraphNodePayload(packageNodeId, packageReference, "package", null, null, null, false));
                AddEdge(
                    edges,
                    edgeIdentities,
                    source: projectNodeId,
                    target: packageNodeId,
                    kind: "project-package",
                    weight: 1);
            }

            foreach (string metadataReference in project.MetadataReferences)
            {
                string assemblyNodeId = BuildDependencyNodeId("assembly", metadataReference, dependencyNodeIds);
                AddNode(nodes, nodeIds, new GraphNodePayload(assemblyNodeId, metadataReference, "assembly", null, null, null, false));
                AddEdge(
                    edges,
                    edgeIdentities,
                    source: projectNodeId,
                    target: assemblyNodeId,
                    kind: "project-assembly",
                    weight: 1);
            }

            foreach (NativeDependencySummary nativeDependency in project.NativeDependencies)
            {
                totalNativeDependencyCount++;
                string dllNodeId = BuildDependencyNodeId("dll", nativeDependency.LibraryName, dependencyNodeIds);
                string dllLabel = BuildNativeDependencyLabel(nativeDependency);
                AddNode(nodes, nodeIds, new GraphNodePayload(dllNodeId, dllLabel, "dll", null, null, null, false));
                AddEdge(
                    edges,
                    edgeIdentities,
                    source: projectNodeId,
                    target: dllNodeId,
                    kind: "project-dll",
                    weight: Math.Max(1, nativeDependency.ImportedSymbols.Count),
                    referenceKind: nativeDependency.ImportKind,
                    confidence: nativeDependency.Confidence);
            }
        }

        foreach ((ProjectAnalysisSummary project, string sourceProjectNodeId) in projectEntries)
        {
            foreach (ProjectReferenceSummary projectReference in project.ProjectReferences)
            {
                if (!projectNodeIdsByKey.TryGetValue(projectReference.TargetProjectKey, out string? targetProjectNodeId))
                {
                    targetProjectNodeId = BuildProjectNodeId(projectReference.TargetProjectKey, dependencyNodeIds);
                    projectNodeIdsByKey[projectReference.TargetProjectKey] = targetProjectNodeId;
                    string projectReferenceLabel = string.IsNullOrWhiteSpace(projectReference.DisplayName)
                        ? projectReference.TargetProjectKey
                        : projectReference.DisplayName;
                    AddNode(
                        nodes,
                        nodeIds,
                        new GraphNodePayload(
                            targetProjectNodeId,
                            projectReferenceLabel,
                            "project",
                            null,
                            null,
                            projectReferenceLabel,
                            cycleIdsByNodeId.ContainsKey(targetProjectNodeId)));
                }

                AddEdge(
                    edges,
                    edgeIdentities,
                    source: sourceProjectNodeId,
                    target: targetProjectNodeId,
                    kind: "project-reference",
                    weight: 1,
                    isCycleEdge: IsCycleEdge(cycleIdsByNodeId, sourceProjectNodeId, targetProjectNodeId));
            }
        }

        foreach (DocumentDependencySummary dependency in snapshot.DocumentDependencies)
        {
            if (!visibleDocumentNodeIds.Contains(dependency.SourceDocumentId) ||
                !visibleDocumentNodeIds.Contains(dependency.TargetDocumentId))
            {
                continue;
            }

            AddEdge(
                edges,
                edgeIdentities,
                source: dependency.SourceDocumentId,
                target: dependency.TargetDocumentId,
                kind: "document-reference",
                weight: dependency.ReferenceCount,
                sampleFilePath: dependency.SampleFilePath,
                sampleLineNumber: dependency.SampleLineNumber,
                sampleSnippet: dependency.SampleSnippet,
                isCycleEdge: IsCycleEdge(cycleIdsByNodeId, dependency.SourceDocumentId, dependency.TargetDocumentId));
        }

        if (includeSymbolGraph && visibleSymbolNodeIds is not null)
        {
            foreach (SymbolDependencySummary dependency in snapshot.SymbolDependencies)
            {
                if (!visibleSymbolNodeIds.Contains(dependency.SourceSymbolId) ||
                    !visibleSymbolNodeIds.Contains(dependency.TargetSymbolId))
                {
                    continue;
                }

                AddEdge(
                    edges,
                    edgeIdentities,
                    source: dependency.SourceSymbolId,
                    target: dependency.TargetSymbolId,
                    kind: ResolveSymbolEdgeKind(dependency.ReferenceKind),
                    weight: dependency.ReferenceCount,
                    referenceKind: dependency.ReferenceKind,
                    confidence: dependency.Confidence,
                    sampleFilePath: dependency.SampleFilePath,
                    sampleLineNumber: dependency.SampleLineNumber,
                    sampleSnippet: dependency.SampleSnippet,
                    isCycleEdge: IsCycleEdge(cycleIdsByNodeId, dependency.SourceSymbolId, dependency.TargetSymbolId));
            }
        }

        int projectCycleCount = 0;
        int documentCycleCount = 0;
        int symbolCycleCount = 0;
        foreach (DependencyCycleSummary cycle in snapshot.Cycles)
        {
            if (string.Equals(cycle.GraphKind, "project", StringComparison.OrdinalIgnoreCase))
            {
                projectCycleCount++;
            }
            else if (string.Equals(cycle.GraphKind, "document", StringComparison.OrdinalIgnoreCase))
            {
                documentCycleCount++;
            }
            else if (string.Equals(cycle.GraphKind, "symbol", StringComparison.OrdinalIgnoreCase))
            {
                symbolCycleCount++;
            }
        }

        GraphStatsPayload stats = new(
            ProjectCount: snapshot.Projects.Count,
            DocumentCount: totalDocumentCount,
            SymbolCount: totalSymbolCount,
            NativeDependencyCount: totalNativeDependencyCount,
            DocumentDependencyCount: snapshot.DocumentDependencies.Count,
            SymbolDependencyCount: snapshot.SymbolDependencies.Count,
            ProjectCycleCount: projectCycleCount,
            DocumentCycleCount: documentCycleCount,
            SymbolCycleCount: symbolCycleCount,
            EdgeCount: edges.Count);

        return new GraphPayload(nodes, edges, stats);
    }

    private static void AddNode(
        ICollection<GraphNodePayload> nodes,
        ISet<string> nodeIds,
        GraphNodePayload node)
    {
        if (nodeIds.Add(node.Id))
        {
            nodes.Add(node);
        }
    }

    private static void AddEdge(
        ICollection<GraphEdgePayload> edges,
        ISet<EdgeIdentity> edgeIdentities,
        string source,
        string target,
        string kind,
        int weight,
        string? referenceKind = null,
        string? confidence = null,
        string? sampleFilePath = null,
        int? sampleLineNumber = null,
        string? sampleSnippet = null,
        bool isCycleEdge = false)
    {
        EdgeIdentity edgeIdentity = new(source, target, kind, referenceKind, confidence);
        if (!edgeIdentities.Add(edgeIdentity))
        {
            return;
        }

        string edgeId = string.IsNullOrWhiteSpace(referenceKind) && string.IsNullOrWhiteSpace(confidence)
            ? $"edge:{kind}:{source}->{target}"
            : $"edge:{kind}:{NormalizeIdentity(referenceKind ?? "none")}:{NormalizeIdentity(confidence ?? "none")}:{source}->{target}";
        edges.Add(new GraphEdgePayload(
            edgeId,
            source,
            target,
            kind,
            weight,
            referenceKind,
            confidence,
            isCycleEdge,
            sampleFilePath,
            sampleLineNumber,
            NormalizeSampleSnippet(sampleSnippet)));
    }

    public static string BuildProjectNodeId(ProjectAnalysisSummary project)
    {
        return BuildProjectNodeId(project.ProjectKey);
    }

    private static string BuildProjectNodeId(
        ProjectAnalysisSummary project,
        IDictionary<DependencyNodeIdentity, string> dependencyNodeIds)
    {
        return BuildProjectNodeId(project.ProjectKey, dependencyNodeIds);
    }

    public static string BuildProjectNodeId(string projectKey)
    {
        return BuildDependencyNodeId("project", projectKey);
    }

    private static string BuildProjectNodeId(
        string projectKey,
        IDictionary<DependencyNodeIdentity, string> dependencyNodeIds)
    {
        return BuildDependencyNodeId("project", projectKey, dependencyNodeIds);
    }

    public static string BuildDependencyNodeId(string prefix, string identity)
    {
        string normalized = NormalizeIdentity(identity);
        string hashToken = ComputeHashToken(identity);
        return $"{prefix}:{normalized}_{hashToken}";
    }

    private static string BuildDependencyNodeId(
        string prefix,
        string identity,
        IDictionary<DependencyNodeIdentity, string> dependencyNodeIds)
    {
        DependencyNodeIdentity cacheKey = new(prefix, identity);
        if (dependencyNodeIds.TryGetValue(cacheKey, out string? cachedNodeId))
        {
            return cachedNodeId;
        }

        string createdNodeId = BuildDependencyNodeId(prefix, identity);
        dependencyNodeIds.Add(cacheKey, createdNodeId);
        return createdNodeId;
    }

    private static string GetDisplayFileName(string? filePath, string fallbackLabel)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            string fileName = Path.GetFileName(filePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return fallbackLabel;
    }

    private static string NormalizeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty";
        }

        StringBuilder builder = new(value.Length);
        foreach (char ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        if (builder.Length == 0)
        {
            return "empty";
        }

        return builder.ToString();
    }

    private static string ComputeHashToken(string value) => AnalysisIdBuilder.ComputeHashToken(value);

    private static string BuildNativeDependencyLabel(NativeDependencySummary dependency)
    {
        string importKindLabel = dependency.ImportKind switch
        {
            "LoadLibrary" => "LoadLibrary",
            "GetProcAddress" => "GetProcAddress",
            "DllImport" => "DllImport",
            "LibraryImport" => "LibraryImport",
            _ => dependency.ImportKind
        };

        if (dependency.ImportedSymbols.Count == 0)
        {
            return dependency.LibraryName;
        }

        string importedSymbolLabel = string.Join(", ", dependency.ImportedSymbols.Take(2));
        if (dependency.ImportedSymbols.Count > 2)
        {
            importedSymbolLabel = global::CodeMap.AppLocalization.Get(
                "nativeDependency.more",
                importedSymbolLabel,
                dependency.ImportedSymbols.Count - 2);
        }

        return $"{dependency.LibraryName} [{importKindLabel}: {importedSymbolLabel}]";
    }

    private static string ResolveCycleNodeId(
        string graphKind,
        string cycleNodeId,
        IReadOnlyDictionary<string, string> projectNodeIdsByKey)
    {
        if (string.Equals(graphKind, "project", StringComparison.OrdinalIgnoreCase) &&
            projectNodeIdsByKey.TryGetValue(cycleNodeId, out string? projectNodeId))
        {
            return projectNodeId;
        }

        return string.Equals(graphKind, "project", StringComparison.OrdinalIgnoreCase)
            ? BuildProjectNodeId(cycleNodeId)
            : cycleNodeId;
    }

    private static bool IsCycleEdge(
        IReadOnlyDictionary<string, HashSet<string>> cycleIdsByNodeId,
        string sourceNodeId,
        string targetNodeId)
    {
        if (!cycleIdsByNodeId.TryGetValue(sourceNodeId, out HashSet<string>? sourceCycleIds) ||
            !cycleIdsByNodeId.TryGetValue(targetNodeId, out HashSet<string>? targetCycleIds))
        {
            return false;
        }

        return sourceCycleIds.Overlaps(targetCycleIds);
    }

    private static string ResolveSymbolEdgeKind(string referenceKind)
    {
        return referenceKind switch
        {
            "call" => "symbol-call",
            "inheritance" => "symbol-inheritance",
            "implementation" => "symbol-implementation",
            "creation" => "symbol-creation",
            _ => "symbol-reference"
        };
    }

    private static string? NormalizeSampleSnippet(string? sampleSnippet) => DependencySampleHelper.NormalizeSnippet(sampleSnippet);
}
