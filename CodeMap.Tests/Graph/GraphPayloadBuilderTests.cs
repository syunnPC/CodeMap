using System;
using System.Collections.Generic;
using System.Linq;
using CodeMap.Analysis;
using CodeMap.Graph;
using Xunit;

namespace CodeMap.Tests.Graph;

public sealed class GraphPayloadBuilderTests
{
    [Fact]
    public void Build_IncludeSymbolsFalse_DoesNotEmitSymbolNodesOrSymbolEdges()
    {
        SolutionAnalysisSnapshot snapshot = CreateSnapshot(symbolCount: 3);

        GraphPayload payload = GraphPayloadBuilder.Build(snapshot, includeSymbols: false);

        Assert.DoesNotContain(payload.Nodes, node => node.Group == "symbol");
        Assert.DoesNotContain(payload.Edges, edge => edge.Kind.StartsWith("symbol-", StringComparison.Ordinal));
        Assert.Equal(3, payload.Stats.SymbolCount);
    }

    [Fact]
    public void Build_MaxSymbolNodes_LimitsVisibleSymbolNodes()
    {
        SolutionAnalysisSnapshot snapshot = CreateSnapshot(symbolCount: 4);

        GraphPayload payload = GraphPayloadBuilder.Build(snapshot, maxSymbolNodes: 2, includeSymbols: true);

        Assert.Equal(2, payload.Nodes.Count(node => node.Group == "symbol"));
        Assert.Equal(2, payload.Edges.Count(edge => edge.Kind == "contains-symbol"));
    }

    [Fact]
    public void Build_ProjectCycleNodeIdFromProjectKey_MarksProjectNodeInCycle()
    {
        SolutionAnalysisSnapshot snapshot = CreateSnapshot(symbolCount: 2);
        string expectedProjectNodeId = GraphPayloadBuilder.BuildProjectNodeId(snapshot.Projects[0]);

        GraphPayload payload = GraphPayloadBuilder.Build(snapshot);

        GraphNodePayload projectNode = Assert.Single(payload.Nodes, node => node.Id == expectedProjectNodeId);
        Assert.True(projectNode.IsInCycle);
    }

    [Fact]
    public void BuildDependencyNodeId_SameInput_ReturnsSameId()
    {
        string first = GraphPayloadBuilder.BuildDependencyNodeId("package", "Newtonsoft.Json");
        string second = GraphPayloadBuilder.BuildDependencyNodeId("package", "Newtonsoft.Json");
        string third = GraphPayloadBuilder.BuildDependencyNodeId("package", "Newtonsoft.Json ");

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    private static SolutionAnalysisSnapshot CreateSnapshot(int symbolCount)
    {
        string workspacePath = @"C:\repo\CodeMap";
        string projectFilePath = @"C:\repo\CodeMap\App\App.csproj";
        string documentPath = @"C:\repo\CodeMap\App\Program.cs";
        string projectKey = AnalysisIdentity.BuildProjectKey("App", projectFilePath);

        List<SymbolAnalysisSummary> symbols = Enumerable.Range(1, symbolCount)
            .Select(index => new SymbolAnalysisSummary(
                Id: $"symbol:{index}",
                Kind: "MethodDeclaration",
                Name: $"Method{index}",
                DisplayName: $"Method{index}()",
                LineNumber: index))
            .ToList();

        DocumentAnalysisSummary document = new(
            Id: "document:program_cs",
            Name: "Program.cs",
            FilePath: documentPath,
            Symbols: symbols);

        ProjectAnalysisSummary project = new(
            Name: "App",
            Language: "C#",
            ProjectFilePath: projectFilePath,
            ProjectKey: projectKey,
            IsFolderBased: false,
            Documents: [document],
            ProjectReferences: Array.Empty<ProjectReferenceSummary>(),
            MetadataReferences: ["System.Runtime"],
            PackageReferences: ["Newtonsoft.Json"],
            NativeDependencies:
            [
                new NativeDependencySummary(
                    LibraryName: "kernel32.dll",
                    ImportKind: "LoadLibrary",
                    Confidence: "high",
                    SourceDocumentId: document.Id,
                    SourceSymbolId: symbols[0].Id,
                    ImportedSymbols: ["LoadLibraryA", "LoadLibraryW", "GetProcAddress"])
            ]);

        List<SymbolDependencySummary> symbolDependencies = [];
        List<DependencyCycleSummary> cycles =
        [
            new DependencyCycleSummary(
                GraphKind: "project",
                CycleId: "project-cycle-1",
                NodeIds: [projectKey],
                EdgeCount: 1)
        ];

        if (symbols.Count >= 2)
        {
            symbolDependencies.Add(new SymbolDependencySummary(
                SourceSymbolId: symbols[0].Id,
                TargetSymbolId: symbols[1].Id,
                ReferenceCount: 3,
                ReferenceKind: "call",
                Confidence: "high",
                SampleFilePath: documentPath,
                SampleLineNumber: 12,
                SampleSnippet: "Method2();"));

            cycles.Add(new DependencyCycleSummary(
                GraphKind: "symbol",
                CycleId: "symbol-cycle-1",
                NodeIds: [symbols[0].Id, symbols[1].Id],
                EdgeCount: 2));
        }

        return new SolutionAnalysisSnapshot(
            WorkspacePath: workspacePath,
            WorkspaceKind: "solution",
            AnalyzedAt: DateTimeOffset.UtcNow,
            Projects: [project],
            DocumentDependencies:
            [
                new DocumentDependencySummary(
                    SourceDocumentId: document.Id,
                    TargetDocumentId: document.Id,
                    ReferenceCount: 1,
                    SampleFilePath: documentPath,
                    SampleLineNumber: 1,
                    SampleSnippet: "using System;")
            ],
            SymbolDependencies: symbolDependencies,
            Cycles: cycles,
            Diagnostics: Array.Empty<string>());
    }
}
