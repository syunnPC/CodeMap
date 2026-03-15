using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CodeMap.Analysis;

public sealed class RoslynAnalysisService
{
    private static readonly object s_msBuildRegistrationLock = new();
    private static readonly Lazy<IReadOnlyList<MetadataReference>> s_looseCompilationReferences = new(CreateLooseCompilationReferences);
    private readonly CppAnalysisService _cppAnalysisService = new();

    private static string T(string key, params object[] args)
    {
        return global::CodeMap.AppLocalization.Get(key, args);
    }

    public Task<SolutionAnalysisSnapshot> AnalyzeSolutionAsync(string solutionPath, CancellationToken cancellationToken = default)
    {
        return AnalyzeWorkspaceAsync(solutionPath, progress: null, cancellationToken);
    }

    public Task<SolutionAnalysisSnapshot> AnalyzeWorkspaceAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        return AnalyzeWorkspaceAsync(workspacePath, progress: null, cancellationToken);
    }

    public async Task<SolutionAnalysisSnapshot> AnalyzeWorkspaceAsync(
        string workspacePath,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        string normalizedPath = Path.GetFullPath(workspacePath);
        ReportProgress(
            progress,
            new AnalysisProgressUpdate(AnalysisProgressStage.PreparingWorkspace, normalizedPath));
        string workspaceKind = ResolveWorkspaceKind(normalizedPath);
        ConcurrentQueue<string> diagnostics = [];

        List<ProjectAnalysisSummary> projects = [];
        List<ProjectDependencyExtractionResult> dependencyExtractionResults = [];
        HashSet<string> seenProjectFilePaths = new(StringComparer.OrdinalIgnoreCase);
        List<DocumentDependencySummary> cppDocumentDependencies = [];
        List<SymbolDependencySummary> cppSymbolDependencies = [];

        if (Directory.Exists(normalizedPath))
        {
            ProjectAnalysisResult? folderProjectAnalysis = await AnalyzeFolderProjectAsync(
                normalizedPath,
                diagnostics,
                progress,
                cancellationToken);

            if (folderProjectAnalysis is not null)
            {
                projects.Add(folderProjectAnalysis.Summary);
                dependencyExtractionResults.Add(folderProjectAnalysis.DependencyExtraction);
            }
        }
        else if (SupportsManagedAnalysis(normalizedPath))
        {
            try
            {
                EnsureMsBuildRegistered();
                ReportProgress(
                    progress,
                    new AnalysisProgressUpdate(
                        AnalysisProgressStage.LoadingManagedSolution,
                        normalizedPath,
                        DocumentPath: normalizedPath));

                using MSBuildWorkspace workspace = MSBuildWorkspace.Create();
                workspace.RegisterWorkspaceFailedHandler(eventArgs =>
                {
                    WorkspaceDiagnostic diagnostic = eventArgs.Diagnostic;
                    if (!string.IsNullOrWhiteSpace(diagnostic.Message))
                    {
                        diagnostics.Enqueue($"{diagnostic.Kind}: {diagnostic.Message}");
                    }
                });

                Solution solution = await OpenManagedSolutionAsync(workspace, normalizedPath, cancellationToken);
                foreach (Project project in solution.Projects.OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ReportProgress(
                        progress,
                        new AnalysisProgressUpdate(
                            AnalysisProgressStage.AnalyzingManagedProject,
                            normalizedPath,
                            project.Name,
                            project.FilePath));

                    ProjectAnalysisResult projectAnalysis = await AnalyzeProjectAsync(
                        project,
                        solution,
                        diagnostics,
                        normalizedPath,
                        progress,
                        cancellationToken);
                    projects.Add(projectAnalysis.Summary);
                    dependencyExtractionResults.Add(projectAnalysis.DependencyExtraction);

                    if (!string.IsNullOrWhiteSpace(projectAnalysis.Summary.ProjectFilePath))
                    {
                        seenProjectFilePaths.Add(Path.GetFullPath(projectAnalysis.Summary.ProjectFilePath));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                diagnostics.Enqueue(T("diag.roslyn.analysisFailed", ex.Message));
            }
        }

        ReportProgress(
            progress,
            new AnalysisProgressUpdate(AnalysisProgressStage.DiscoveringNativeProjects, normalizedPath));
        CppSolutionAnalysisResult cppResult = await _cppAnalysisService.AnalyzeAsync(
            normalizedPath,
            progress,
            cancellationToken);
        foreach (string cppDiagnostic in cppResult.Diagnostics)
        {
            diagnostics.Enqueue(cppDiagnostic);
        }

        foreach (CppProjectAnalysisResult cppProject in cppResult.Projects)
        {
            ProjectAnalysisSummary summary = cppProject.Summary;
            string? projectFilePath = summary.ProjectFilePath;
            if (!string.IsNullOrWhiteSpace(projectFilePath))
            {
                string normalizedProjectPath = Path.GetFullPath(projectFilePath);
                if (seenProjectFilePaths.Contains(normalizedProjectPath))
                {
                    continue;
                }

                seenProjectFilePaths.Add(normalizedProjectPath);
            }

            projects.Add(summary);
            cppDocumentDependencies.AddRange(cppProject.DocumentDependencies);
            cppSymbolDependencies.AddRange(cppProject.SymbolDependencies);
        }

        (IReadOnlyList<DocumentDependencySummary> managedDocumentDependencies, IReadOnlyList<SymbolDependencySummary> managedSymbolDependencies) =
            BuildDependencySummaries(dependencyExtractionResults);

        IReadOnlyList<DocumentDependencySummary> documentDependencies = MergeDocumentDependencies(
            managedDocumentDependencies,
            cppDocumentDependencies);
        IReadOnlyList<SymbolDependencySummary> symbolDependencies = MergeSymbolDependencies(
            managedSymbolDependencies,
            cppSymbolDependencies);
        IReadOnlyList<DependencyCycleSummary> cycles = BuildCycles(projects, documentDependencies, symbolDependencies);

        ReportProgress(
            progress,
            new AnalysisProgressUpdate(AnalysisProgressStage.Finalizing, normalizedPath));
        IReadOnlyList<string> diagnosticSnapshot = diagnostics.ToArray();

        return new SolutionAnalysisSnapshot(
            normalizedPath,
            workspaceKind,
            DateTimeOffset.UtcNow,
            projects,
            documentDependencies,
            symbolDependencies,
            cycles,
            diagnosticSnapshot);
    }

    private static async Task<ProjectAnalysisResult> AnalyzeProjectAsync(
        Project project,
        Solution solution,
        ConcurrentQueue<string> diagnostics,
        string workspacePath,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        List<DocumentAnalysisSummary> documentSummaries = [];
        List<DeclaredSymbolEdgeAnchor> declaredSymbols = [];
        List<SymbolReferenceObservation> referenceObservations = [];
        List<NativeDependencyObservation> nativeDependencyObservations = [];
        string projectKey = AnalysisIdentity.BuildProjectKey(project.Name, project.FilePath, project.Id.Id.ToString("N"));
        string projectIdentity = projectKey;

        foreach (Document document in project.Documents.OrderBy(document => document.Name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(
                progress,
                new AnalysisProgressUpdate(
                    AnalysisProgressStage.AnalyzingManagedDocument,
                    workspacePath,
                    project.Name,
                    document.FilePath ?? document.Name));

            if (!document.SupportsSyntaxTree)
            {
                continue;
            }

            string documentId = BuildDocumentId(projectIdentity, document.FilePath, document.Name);
            List<DeclaredSymbolEdgeAnchor> declaredSymbolsInDocument = [];
            IReadOnlyList<SymbolAnalysisSummary> symbols = Array.Empty<SymbolAnalysisSummary>();

            if (string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
            {
                SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken);
                SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken);

                if (root is not null && semanticModel is not null)
                {
                    symbols = ExtractDeclaredSymbols(
                        root,
                        semanticModel,
                        documentId,
                        declaredSymbolsInDocument,
                        cancellationToken);

                    IReadOnlyList<SymbolReferenceObservation> references = ExtractReferenceObservations(
                        root,
                        semanticModel,
                        documentId,
                        cancellationToken);

                    referenceObservations.AddRange(references);

                    IReadOnlyList<NativeDependencyObservation> nativeDependencies = ExtractManagedNativeDependencies(
                        root,
                        semanticModel,
                        documentId,
                        declaredSymbolsInDocument,
                        cancellationToken);
                    nativeDependencyObservations.AddRange(nativeDependencies);
                }
                else
                {
                    diagnostics.Enqueue(T("diag.roslyn.semanticModelUnavailable", project.Name, document.Name));
                }
            }

            declaredSymbols.AddRange(declaredSymbolsInDocument);

            documentSummaries.Add(new DocumentAnalysisSummary(
                documentId,
                document.Name,
                document.FilePath,
                symbols));
        }

        IReadOnlyList<ProjectReferenceSummary> projectReferences = GetProjectReferences(solution, project);
        IReadOnlyList<string> metadataReferences = await GetMetadataReferencesAsync(project, cancellationToken);
        IReadOnlyList<string> packageReferences = ReadPackageReferences(project.FilePath, diagnostics, project.Name);

        ProjectAnalysisSummary summary = new(
            project.Name,
            project.Language,
            project.FilePath,
            projectKey,
            IsFolderBased: false,
            documentSummaries,
            projectReferences,
            metadataReferences,
            packageReferences,
            AggregateNativeDependencies(nativeDependencyObservations));

        return new ProjectAnalysisResult(
            summary,
            new ProjectDependencyExtractionResult(declaredSymbols, referenceObservations));
    }

    private static async Task<ProjectAnalysisResult?> AnalyzeFolderProjectAsync(
        string folderPath,
        ConcurrentQueue<string> diagnostics,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> csharpFiles = EnumerateFiles(folderPath, ".cs");
        if (csharpFiles.Count == 0)
        {
            diagnostics.Enqueue(T("diag.roslyn.folderNoFiles", folderPath));
            return null;
        }

        List<SyntaxTree> syntaxTrees = [];
        foreach (string filePath in csharpFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReportProgress(
                progress,
                new AnalysisProgressUpdate(
                    AnalysisProgressStage.AnalyzingFolderDocument,
                    folderPath,
                    Path.GetFileName(folderPath),
                    filePath));
            string sourceText = await File.ReadAllTextAsync(filePath, cancellationToken);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                SourceText.From(sourceText, Encoding.UTF8),
                path: filePath,
                cancellationToken: cancellationToken);
            syntaxTrees.Add(syntaxTree);
        }

        IReadOnlyList<MetadataReference> looseReferences = GetLooseCompilationReferences();

        CSharpCompilation compilation = CSharpCompilation.Create(
            Path.GetFileName(folderPath),
            syntaxTrees,
            looseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        List<DocumentAnalysisSummary> documentSummaries = [];
        List<DeclaredSymbolEdgeAnchor> declaredSymbols = [];
        List<SymbolReferenceObservation> referenceObservations = [];
        List<NativeDependencyObservation> nativeDependencyObservations = [];
        string projectName = Path.GetFileName(folderPath);
        string projectKey = AnalysisIdentity.BuildProjectKey(projectName, folderPath);
        string projectIdentity = projectKey;

        foreach (SyntaxTree syntaxTree in syntaxTrees.OrderBy(tree => tree.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string documentPath = syntaxTree.FilePath;
            string documentName = Path.GetFileName(documentPath);
            string documentId = BuildDocumentId(projectIdentity, documentPath, documentName);
            SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
            SyntaxNode root = await syntaxTree.GetRootAsync(cancellationToken);
            List<DeclaredSymbolEdgeAnchor> declaredSymbolsInDocument = [];

            IReadOnlyList<SymbolAnalysisSummary> symbols = ExtractDeclaredSymbols(
                root,
                semanticModel,
                documentId,
                declaredSymbolsInDocument,
                cancellationToken);

            declaredSymbols.AddRange(declaredSymbolsInDocument);
            referenceObservations.AddRange(ExtractReferenceObservations(root, semanticModel, documentId, cancellationToken));
            nativeDependencyObservations.AddRange(ExtractManagedNativeDependencies(
                root,
                semanticModel,
                documentId,
                declaredSymbolsInDocument,
                cancellationToken));

            documentSummaries.Add(new DocumentAnalysisSummary(
                documentId,
                documentName,
                documentPath,
                symbols));
        }

        ProjectAnalysisSummary summary = new(
            projectName,
            LanguageNames.CSharp,
            folderPath,
            projectKey,
            IsFolderBased: true,
            documentSummaries,
            ProjectReferences: Array.Empty<ProjectReferenceSummary>(),
            MetadataReferences: looseReferences
                .OfType<PortableExecutableReference>()
                .Select(reference => reference.FilePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFileNameWithoutExtension(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            PackageReferences: Array.Empty<string>(),
            NativeDependencies: AggregateNativeDependencies(nativeDependencyObservations));

        diagnostics.Enqueue(T(
            "diag.roslyn.folderSymbolsExtracted",
            documentSummaries.Count,
            documentSummaries.Sum(document => document.Symbols.Count)));

        return new ProjectAnalysisResult(
            summary,
            new ProjectDependencyExtractionResult(declaredSymbols, referenceObservations));
    }

    private static IReadOnlyList<SymbolAnalysisSummary> ExtractDeclaredSymbols(
        SyntaxNode root,
        SemanticModel semanticModel,
        string documentId,
        ICollection<DeclaredSymbolEdgeAnchor> declaredSymbolAnchors,
        CancellationToken cancellationToken)
    {
        List<SymbolAnalysisSummary> symbols = [];
        HashSet<string> seenSymbolKeys = [];

        foreach (SyntaxNode node in root.DescendantNodes())
        {
            switch (node)
            {
                case BaseTypeDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case DelegateDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case MethodDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case ConstructorDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case PropertyDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case IndexerDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case EventDeclarationSyntax declaration:
                    TryAddDeclaredSymbol(
                        declaration,
                        semanticModel.GetDeclaredSymbol(declaration, cancellationToken),
                        declaration.Kind().ToString(),
                        documentId,
                        seenSymbolKeys,
                        symbols,
                        declaredSymbolAnchors,
                        cancellationToken);
                    break;
                case FieldDeclarationSyntax declaration:
                    foreach (VariableDeclaratorSyntax variable in declaration.Declaration.Variables)
                    {
                        TryAddDeclaredSymbol(
                            variable,
                            semanticModel.GetDeclaredSymbol(variable, cancellationToken),
                            declaration.Kind().ToString(),
                            documentId,
                            seenSymbolKeys,
                            symbols,
                            declaredSymbolAnchors,
                            cancellationToken);
                    }

                    break;
                case EventFieldDeclarationSyntax declaration:
                    foreach (VariableDeclaratorSyntax variable in declaration.Declaration.Variables)
                    {
                        TryAddDeclaredSymbol(
                            variable,
                            semanticModel.GetDeclaredSymbol(variable, cancellationToken),
                            "EventDeclaration",
                            documentId,
                            seenSymbolKeys,
                            symbols,
                            declaredSymbolAnchors,
                            cancellationToken);
                    }

                    break;
            }
        }

        return symbols
            .OrderBy(symbol => symbol.LineNumber)
            .ThenBy(symbol => symbol.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SymbolReferenceObservation> ExtractReferenceObservations(
        SyntaxNode root,
        SemanticModel semanticModel,
        string documentId,
        CancellationToken cancellationToken)
    {
        List<SymbolReferenceObservation> observations = [];
        SourceText sourceText = root.SyntaxTree.GetText(cancellationToken);
        string? sourceFilePath = string.IsNullOrWhiteSpace(root.SyntaxTree.FilePath)
            ? null
            : root.SyntaxTree.FilePath;

        foreach (SimpleNameSyntax simpleName in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(simpleName, cancellationToken);
            ISymbol? targetSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
            if (targetSymbol is null)
            {
                continue;
            }

            ISymbol resolvedTarget = targetSymbol.OriginalDefinition;
            if (!IsDependencyCandidate(resolvedTarget))
            {
                continue;
            }

            string targetSymbolKey = CreateSymbolIdentity(resolvedTarget);

            ISymbol? sourceSymbol = GetDependencySourceSymbol(semanticModel, simpleName.SpanStart, cancellationToken);
            string? sourceSymbolKey = sourceSymbol is null
                ? null
                : CreateSymbolIdentity(sourceSymbol.OriginalDefinition);
            int lineNumber = simpleName.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            observations.Add(new SymbolReferenceObservation(
                documentId,
                sourceSymbolKey,
                targetSymbolKey,
                ResolveManagedReferenceKind(simpleName, sourceSymbol, resolvedTarget),
                "high",
                sourceFilePath,
                lineNumber,
                ExtractLineSnippet(sourceText, lineNumber)));
        }

        return observations;
    }

    private static string ResolveManagedReferenceKind(SimpleNameSyntax simpleName, ISymbol? sourceSymbol, ISymbol targetSymbol)
    {
        SyntaxNode? parent = simpleName.Parent;
        if (parent is null)
        {
            return "reference";
        }

        if (
            parent is InvocationExpressionSyntax invocationExpression &&
            invocationExpression.Expression.DescendantNodesAndSelf().Any(node => ReferenceEquals(node, simpleName)))
        {
            return "call";
        }

        if (
            parent is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax ||
            parent.Parent is ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax)
        {
            return "creation";
        }

        if (parent is BaseTypeSyntax or PrimaryConstructorBaseTypeSyntax)
        {
            if (targetSymbol is INamedTypeSymbol targetNamedType && sourceSymbol is INamedTypeSymbol sourceNamedType)
            {
                if (targetNamedType.TypeKind == TypeKind.Interface)
                {
                    return sourceNamedType.TypeKind is TypeKind.Class or TypeKind.Struct
                        ? "implementation"
                        : "inheritance";
                }

                return "inheritance";
            }
        }

        return "reference";
    }

    private static void TryAddDeclaredSymbol(
        SyntaxNode declarationSyntax,
        ISymbol? symbol,
        string kind,
        string documentId,
        ISet<string> seenSymbolKeys,
        ICollection<SymbolAnalysisSummary> symbols,
        ICollection<DeclaredSymbolEdgeAnchor> declaredSymbolAnchors,
        CancellationToken cancellationToken)
    {
        if (symbol is null)
        {
            return;
        }

        ISymbol resolvedSymbol = symbol.OriginalDefinition;
        if (!IsDependencyCandidate(resolvedSymbol))
        {
            return;
        }

        string symbolKey = CreateSymbolIdentity(resolvedSymbol);
        if (!seenSymbolKeys.Add(symbolKey))
        {
            return;
        }

        string symbolName = resolvedSymbol.Name;
        string displayName = resolvedSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        string symbolId = BuildSymbolId(symbolName, symbolKey);
        int lineNumber = GetLineNumber(declarationSyntax);

        symbols.Add(new SymbolAnalysisSummary(
            symbolId,
            kind,
            symbolName,
            displayName,
            lineNumber));

        declaredSymbolAnchors.Add(new DeclaredSymbolEdgeAnchor(symbolKey, symbolId, documentId));
    }

    private static ISymbol? GetDependencySourceSymbol(
        SemanticModel semanticModel,
        int position,
        CancellationToken cancellationToken)
    {
        ISymbol? source = semanticModel.GetEnclosingSymbol(position, cancellationToken);

        while (source is not null)
        {
            ISymbol candidate = source.OriginalDefinition;
            if (IsDependencyCandidate(candidate))
            {
                return candidate;
            }

            source = source.ContainingSymbol;
        }

        return null;
    }

    private static bool IsDependencyCandidate(ISymbol symbol)
    {
        if (symbol.IsImplicitlyDeclared)
        {
            return false;
        }

        return symbol.Kind switch
        {
            SymbolKind.NamedType => true,
            SymbolKind.Method => true,
            SymbolKind.Property => true,
            SymbolKind.Field => true,
            SymbolKind.Event => true,
            _ => false
        };
    }

    private static (IReadOnlyList<DocumentDependencySummary> DocumentDependencies, IReadOnlyList<SymbolDependencySummary> SymbolDependencies)
        BuildDependencySummaries(IReadOnlyList<ProjectDependencyExtractionResult> dependencyResults)
    {
        Dictionary<string, List<DeclaredSymbolEdgeAnchor>> symbolLookup = new(StringComparer.Ordinal);

        foreach (ProjectDependencyExtractionResult result in dependencyResults)
        {
            foreach (DeclaredSymbolEdgeAnchor declaration in result.DeclaredSymbols)
            {
                if (!symbolLookup.TryGetValue(declaration.SymbolKey, out List<DeclaredSymbolEdgeAnchor>? declarations))
                {
                    declarations = [];
                    symbolLookup.Add(declaration.SymbolKey, declarations);
                }

                declarations.Add(declaration);
            }
        }

        Dictionary<(string Source, string Target), int> documentDependencyCounts = [];
        Dictionary<(string Source, string Target), AnalysisDependencySample> documentDependencySamples = [];
        Dictionary<(string Source, string Target, string ReferenceKind, string Confidence), int> symbolDependencyCounts = [];
        Dictionary<(string Source, string Target, string ReferenceKind, string Confidence), AnalysisDependencySample> symbolDependencySamples = [];

        foreach (ProjectDependencyExtractionResult result in dependencyResults)
        {
            foreach (SymbolReferenceObservation reference in result.ReferenceObservations)
            {
                if (
                    !symbolLookup.TryGetValue(reference.TargetSymbolKey, out List<DeclaredSymbolEdgeAnchor>? targetDeclarations) ||
                    targetDeclarations.Count == 0)
                {
                    continue;
                }

                DeclaredSymbolEdgeAnchor targetRepresentative = targetDeclarations[0];
                HashSet<string> targetDocumentIds = [];
                foreach (DeclaredSymbolEdgeAnchor targetDeclaration in targetDeclarations)
                {
                    if (!targetDocumentIds.Add(targetDeclaration.DocumentId))
                    {
                        continue;
                    }

                    if (!string.Equals(reference.SourceDocumentId, targetDeclaration.DocumentId, StringComparison.Ordinal))
                    {
                        (string Source, string Target) documentKey = (reference.SourceDocumentId, targetDeclaration.DocumentId);
                        IncrementCount(documentDependencyCounts, documentKey);
                        TrySetDependencySample(
                            documentDependencySamples,
                            documentKey,
                            reference.SampleFilePath,
                            reference.SampleLineNumber,
                            reference.SampleSnippet);
                    }
                }

                if (string.IsNullOrWhiteSpace(reference.SourceSymbolKey))
                {
                    continue;
                }

                if (
                    !symbolLookup.TryGetValue(reference.SourceSymbolKey, out List<DeclaredSymbolEdgeAnchor>? sourceDeclarations) ||
                    sourceDeclarations.Count == 0)
                {
                    continue;
                }

                DeclaredSymbolEdgeAnchor sourceDeclaration = ResolveSourceDeclaration(
                    sourceDeclarations,
                    reference.SourceDocumentId);

                if (string.Equals(sourceDeclaration.SymbolId, targetRepresentative.SymbolId, StringComparison.Ordinal))
                {
                    continue;
                }

                (string Source, string Target, string ReferenceKind, string Confidence) symbolKey =
                    (
                        sourceDeclaration.SymbolId,
                        targetRepresentative.SymbolId,
                        NormalizeReferenceKind(reference.ReferenceKind),
                        NormalizeConfidence(reference.Confidence));
                IncrementCount(symbolDependencyCounts, symbolKey);
                TrySetDependencySample(
                    symbolDependencySamples,
                    symbolKey,
                    reference.SampleFilePath,
                    reference.SampleLineNumber,
                    reference.SampleSnippet);
            }
        }

        IReadOnlyList<DocumentDependencySummary> documentDependencies = documentDependencyCounts
            .Select(entry => CreateDocumentDependencySummary(entry.Key, entry.Value, documentDependencySamples))
            .OrderByDescending(dependency => dependency.ReferenceCount)
            .ThenBy(dependency => dependency.SourceDocumentId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetDocumentId, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<SymbolDependencySummary> symbolDependencies = symbolDependencyCounts
            .Select(entry => CreateSymbolDependencySummary(entry.Key, entry.Value, symbolDependencySamples))
            .OrderByDescending(dependency => dependency.ReferenceCount)
            .ThenBy(dependency => dependency.SourceSymbolId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetSymbolId, StringComparer.Ordinal)
            .ToArray();

        return (documentDependencies, symbolDependencies);
    }

    private static void IncrementCount(
        IDictionary<(string Source, string Target), int> counts,
        (string Source, string Target) key)
    {
        IncrementCount(counts, key, 1);
    }

    private static void IncrementCount(
        IDictionary<(string Source, string Target), int> counts,
        (string Source, string Target) key,
        int incrementBy)
    {
        if (incrementBy <= 0)
        {
            return;
        }

        if (counts.TryGetValue(key, out int current))
        {
            counts[key] = current + incrementBy;
            return;
        }

        counts.Add(key, incrementBy);
    }

    private static void IncrementCount(
        IDictionary<(string Source, string Target, string ReferenceKind, string Confidence), int> counts,
        (string Source, string Target, string ReferenceKind, string Confidence) key)
    {
        IncrementCount(counts, key, 1);
    }

    private static void IncrementCount(
        IDictionary<(string Source, string Target, string ReferenceKind, string Confidence), int> counts,
        (string Source, string Target, string ReferenceKind, string Confidence) key,
        int incrementBy)
    {
        if (incrementBy <= 0)
        {
            return;
        }

        if (counts.TryGetValue(key, out int current))
        {
            counts[key] = current + incrementBy;
            return;
        }

        counts.Add(key, incrementBy);
    }

    private static IReadOnlyList<DependencyCycleSummary> BuildCycles(
        IReadOnlyList<ProjectAnalysisSummary> projects,
        IReadOnlyList<DocumentDependencySummary> documentDependencies,
        IReadOnlyList<SymbolDependencySummary> symbolDependencies)
    {
        List<DependencyCycleSummary> cycles = [];

        List<(string Source, string Target)> projectEdges = [];
        foreach (ProjectAnalysisSummary project in projects)
        {
            foreach (ProjectReferenceSummary reference in project.ProjectReferences)
            {
                projectEdges.Add((project.ProjectKey, reference.TargetProjectKey));
            }
        }

        cycles.AddRange(BuildCyclesForGraph("project", projectEdges));
        cycles.AddRange(BuildCyclesForGraph(
            "document",
            documentDependencies.Select(item => (item.SourceDocumentId, item.TargetDocumentId))));
        cycles.AddRange(BuildCyclesForGraph(
            "symbol",
            symbolDependencies.Select(item => (item.SourceSymbolId, item.TargetSymbolId))));

        return cycles;
    }

    private static IReadOnlyList<DependencyCycleSummary> BuildCyclesForGraph(
        string graphKind,
        IEnumerable<(string Source, string Target)> edges)
    {
        Dictionary<string, HashSet<string>> adjacency = new(StringComparer.Ordinal);
        Dictionary<(string Source, string Target), int> edgeMultiplicity = [];

        foreach ((string source, string target) in edges)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (!adjacency.TryGetValue(source, out HashSet<string>? next))
            {
                next = new HashSet<string>(StringComparer.Ordinal);
                adjacency.Add(source, next);
            }

            next.Add(target);
            if (!adjacency.ContainsKey(target))
            {
                adjacency.Add(target, new HashSet<string>(StringComparer.Ordinal));
            }

            if (edgeMultiplicity.TryGetValue((source, target), out int current))
            {
                edgeMultiplicity[(source, target)] = current + 1;
            }
            else
            {
                edgeMultiplicity.Add((source, target), 1);
            }
        }

        List<List<string>> stronglyConnectedComponents = FindStronglyConnectedComponents(adjacency);
        List<DependencyCycleSummary> cycles = [];
        int cycleOrdinal = 0;
        foreach (List<string> component in stronglyConnectedComponents)
        {
            bool isCycle = component.Count > 1 ||
                (component.Count == 1 && edgeMultiplicity.ContainsKey((component[0], component[0])));
            if (!isCycle)
            {
                continue;
            }

            HashSet<string> componentNodes = component.ToHashSet(StringComparer.Ordinal);
            int edgeCount = edgeMultiplicity
                .Where(entry => componentNodes.Contains(entry.Key.Source) && componentNodes.Contains(entry.Key.Target))
                .Sum(entry => entry.Value);

            string cycleId = $"{graphKind}-cycle-{++cycleOrdinal}";
            cycles.Add(new DependencyCycleSummary(
                graphKind,
                cycleId,
                component.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                edgeCount));
        }

        return cycles;
    }

    private static List<List<string>> FindStronglyConnectedComponents(IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        List<List<string>> components = [];
        Dictionary<string, int> indexByNode = new(StringComparer.Ordinal);
        Dictionary<string, int> lowLinkByNode = new(StringComparer.Ordinal);
        Stack<string> stack = new();
        HashSet<string> onStack = new(StringComparer.Ordinal);
        int nextIndex = 0;

        foreach (string node in adjacency.Keys.OrderBy(item => item, StringComparer.Ordinal))
        {
            if (!indexByNode.ContainsKey(node))
            {
                StrongConnect(node);
            }
        }

        return components;

        void StrongConnect(string node)
        {
            indexByNode[node] = nextIndex;
            lowLinkByNode[node] = nextIndex;
            nextIndex++;
            stack.Push(node);
            onStack.Add(node);

            if (adjacency.TryGetValue(node, out HashSet<string>? neighbors))
            {
                foreach (string neighbor in neighbors)
                {
                    if (!indexByNode.ContainsKey(neighbor))
                    {
                        StrongConnect(neighbor);
                        lowLinkByNode[node] = Math.Min(lowLinkByNode[node], lowLinkByNode[neighbor]);
                    }
                    else if (onStack.Contains(neighbor))
                    {
                        lowLinkByNode[node] = Math.Min(lowLinkByNode[node], indexByNode[neighbor]);
                    }
                }
            }

            if (lowLinkByNode[node] != indexByNode[node])
            {
                return;
            }

            List<string> component = [];
            while (stack.Count > 0)
            {
                string current = stack.Pop();
                onStack.Remove(current);
                component.Add(current);
                if (string.Equals(current, node, StringComparison.Ordinal))
                {
                    break;
                }
            }

            components.Add(component);
        }
    }

    private static string NormalizeReferenceKind(string? referenceKind)
    {
        if (string.IsNullOrWhiteSpace(referenceKind))
        {
            return "reference";
        }

        return referenceKind.Trim().ToLowerInvariant();
    }

    private static string NormalizeConfidence(string? confidence)
    {
        if (string.IsNullOrWhiteSpace(confidence))
        {
            return "high";
        }

        return confidence.Trim().ToLowerInvariant();
    }

    private static DeclaredSymbolEdgeAnchor ResolveSourceDeclaration(
        IReadOnlyList<DeclaredSymbolEdgeAnchor> declarations,
        string sourceDocumentId)
    {
        foreach (DeclaredSymbolEdgeAnchor declaration in declarations)
        {
            if (string.Equals(declaration.DocumentId, sourceDocumentId, StringComparison.Ordinal))
            {
                return declaration;
            }
        }

        return declarations[0];
    }

    private static async Task<Solution> OpenManagedSolutionAsync(
        MSBuildWorkspace workspace,
        string solutionOrProjectPath,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(solutionOrProjectPath);
        if (
            string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            return await workspace.OpenSolutionAsync(
                solutionOrProjectPath,
                progress: null,
                cancellationToken: cancellationToken);
        }

        Project project = await workspace.OpenProjectAsync(
            solutionOrProjectPath,
            progress: null,
            cancellationToken: cancellationToken);
        return project.Solution;
    }

    private static bool SupportsManagedAnalysis(string path)
    {
        string extension = Path.GetExtension(path);
        return
            string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".vbproj", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".fsproj", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DocumentDependencySummary> MergeDocumentDependencies(
        IReadOnlyList<DocumentDependencySummary> managedDocumentDependencies,
        IReadOnlyList<DocumentDependencySummary> cppDocumentDependencies)
    {
        if (cppDocumentDependencies.Count == 0)
        {
            return managedDocumentDependencies;
        }

        Dictionary<(string Source, string Target), int> mergedCounts = [];
        Dictionary<(string Source, string Target), AnalysisDependencySample> mergedSamples = [];
        foreach (DocumentDependencySummary dependency in managedDocumentDependencies)
        {
            (string Source, string Target) key = (dependency.SourceDocumentId, dependency.TargetDocumentId);
            IncrementCount(mergedCounts, key, dependency.ReferenceCount);
            TrySetDependencySample(mergedSamples, key, dependency.SampleFilePath, dependency.SampleLineNumber, dependency.SampleSnippet);
        }

        foreach (DocumentDependencySummary dependency in cppDocumentDependencies)
        {
            (string Source, string Target) key = (dependency.SourceDocumentId, dependency.TargetDocumentId);
            IncrementCount(mergedCounts, key, dependency.ReferenceCount);
            TrySetDependencySample(mergedSamples, key, dependency.SampleFilePath, dependency.SampleLineNumber, dependency.SampleSnippet);
        }

        return mergedCounts
            .Select(entry => CreateDocumentDependencySummary(entry.Key, entry.Value, mergedSamples))
            .OrderByDescending(dependency => dependency.ReferenceCount)
            .ThenBy(dependency => dependency.SourceDocumentId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetDocumentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SymbolDependencySummary> MergeSymbolDependencies(
        IReadOnlyList<SymbolDependencySummary> managedSymbolDependencies,
        IReadOnlyList<SymbolDependencySummary> cppSymbolDependencies)
    {
        if (cppSymbolDependencies.Count == 0)
        {
            return managedSymbolDependencies;
        }

        Dictionary<(string Source, string Target, string ReferenceKind, string Confidence), int> mergedCounts = [];
        Dictionary<(string Source, string Target, string ReferenceKind, string Confidence), AnalysisDependencySample> mergedSamples = [];
        foreach (SymbolDependencySummary dependency in managedSymbolDependencies)
        {
            (string Source, string Target, string ReferenceKind, string Confidence) key =
                (
                    dependency.SourceSymbolId,
                    dependency.TargetSymbolId,
                    NormalizeReferenceKind(dependency.ReferenceKind),
                    NormalizeConfidence(dependency.Confidence));
            IncrementCount(mergedCounts, key, dependency.ReferenceCount);
            TrySetDependencySample(mergedSamples, key, dependency.SampleFilePath, dependency.SampleLineNumber, dependency.SampleSnippet);
        }

        foreach (SymbolDependencySummary dependency in cppSymbolDependencies)
        {
            (string Source, string Target, string ReferenceKind, string Confidence) key =
                (
                    dependency.SourceSymbolId,
                    dependency.TargetSymbolId,
                    NormalizeReferenceKind(dependency.ReferenceKind),
                    NormalizeConfidence(dependency.Confidence));
            IncrementCount(mergedCounts, key, dependency.ReferenceCount);
            TrySetDependencySample(mergedSamples, key, dependency.SampleFilePath, dependency.SampleLineNumber, dependency.SampleSnippet);
        }

        return mergedCounts
            .Select(entry => CreateSymbolDependencySummary(entry.Key, entry.Value, mergedSamples))
            .OrderByDescending(dependency => dependency.ReferenceCount)
            .ThenBy(dependency => dependency.SourceSymbolId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.TargetSymbolId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void EnsureMsBuildRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (s_msBuildRegistrationLock)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return;
            }

            VisualStudioInstance? visualStudioInstance = MSBuildLocator.QueryVisualStudioInstances()
                .OrderByDescending(instance => instance.Version)
                .FirstOrDefault();

            if (visualStudioInstance is not null)
            {
                MSBuildLocator.RegisterInstance(visualStudioInstance);
                return;
            }

            MSBuildLocator.RegisterDefaults();
        }
    }

    private static IReadOnlyList<ProjectReferenceSummary> GetProjectReferences(Solution solution, Project project)
    {
        return project.ProjectReferences
            .Select(reference =>
            {
                Project? referencedProject = solution.GetProject(reference.ProjectId);
                string fallbackIdentity = reference.ProjectId.Id.ToString("N");
                string displayName = referencedProject?.Name ?? fallbackIdentity;
                string targetProjectKey = referencedProject is not null
                    ? AnalysisIdentity.BuildProjectKey(referencedProject.Name, referencedProject.FilePath, fallbackIdentity)
                    : AnalysisIdentity.BuildProjectKey(displayName, projectFilePath: null, fallbackIdentity: fallbackIdentity);

                return new ProjectReferenceSummary(targetProjectKey, displayName);
            })
            .GroupBy(reference => reference.TargetProjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(reference => reference.DisplayName, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(reference => reference.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.TargetProjectKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<string>> GetMetadataReferencesAsync(Project project, CancellationToken cancellationToken)
    {
        Compilation? compilation = await project.GetCompilationAsync(cancellationToken);
        if (compilation is null)
        {
            return Array.Empty<string>();
        }

        return compilation.References
            .OfType<PortableExecutableReference>()
            .Select(reference => reference.FilePath is not null
                ? Path.GetFileNameWithoutExtension(reference.FilePath)
                : reference.Display)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<MetadataReference> GetLooseCompilationReferences()
    {
        return s_looseCompilationReferences.Value;
    }

    private static IReadOnlyList<MetadataReference> CreateLooseCompilationReferences()
    {
        string? trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
        {
            return
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DllImportAttribute).Assembly.Location)
            ];
        }

        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    private static IReadOnlyList<NativeDependencyObservation> ExtractManagedNativeDependencies(
        SyntaxNode root,
        SemanticModel semanticModel,
        string documentId,
        IReadOnlyList<DeclaredSymbolEdgeAnchor> declaredSymbols,
        CancellationToken cancellationToken)
    {
        List<NativeDependencyObservation> observations = [];
        Dictionary<string, string> declaredSymbolIds = declaredSymbols.ToDictionary(
            item => item.SymbolKey,
            item => item.SymbolId,
            StringComparer.Ordinal);

        foreach (AttributeSyntax attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            INamedTypeSymbol? attributeType = semanticModel.GetTypeInfo(attribute, cancellationToken).Type as INamedTypeSymbol;
            if (attributeType is null)
            {
                continue;
            }

            string attributeDisplayName = attributeType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            string importKind = attributeDisplayName switch
            {
                "System.Runtime.InteropServices.DllImportAttribute" => "DllImport",
                "System.Runtime.InteropServices.LibraryImportAttribute" => "LibraryImport",
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(importKind))
            {
                continue;
            }

            string? libraryName = TryResolveAttributeStringArgument(attribute, semanticModel, cancellationToken);
            if (string.IsNullOrWhiteSpace(libraryName))
            {
                continue;
            }

            ISymbol? sourceSymbol = GetDependencySourceSymbol(semanticModel, attribute.SpanStart, cancellationToken);
            string? sourceSymbolId = null;
            string? importedSymbol = sourceSymbol?.Name;
            if (sourceSymbol is not null)
            {
                string symbolKey = CreateSymbolIdentity(sourceSymbol.OriginalDefinition);
                if (declaredSymbolIds.TryGetValue(symbolKey, out string? resolvedSymbolId))
                {
                    sourceSymbolId = resolvedSymbolId;
                }
            }

            string? entryPoint = TryResolveNamedAttributeArgument(attribute, semanticModel, "EntryPoint", cancellationToken);
            if (!string.IsNullOrWhiteSpace(entryPoint))
            {
                importedSymbol = entryPoint;
            }

            observations.Add(new NativeDependencyObservation(
                libraryName.Trim(),
                importKind,
                "confirmed",
                documentId,
                sourceSymbolId,
                importedSymbol));
        }

        return observations;
    }

    private static string? TryResolveAttributeStringArgument(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        AttributeArgumentSyntax? firstArgument = attribute.ArgumentList?.Arguments.FirstOrDefault();
        if (firstArgument is null)
        {
            return null;
        }

        Optional<object?> constant = semanticModel.GetConstantValue(firstArgument.Expression, cancellationToken);
        return constant.HasValue && constant.Value is string value && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string? TryResolveNamedAttributeArgument(
        AttributeSyntax attribute,
        SemanticModel semanticModel,
        string argumentName,
        CancellationToken cancellationToken)
    {
        if (attribute.ArgumentList is null)
        {
            return null;
        }

        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
        {
            if (!string.Equals(argument.NameEquals?.Name.Identifier.ValueText, argumentName, StringComparison.Ordinal))
            {
                continue;
            }

            Optional<object?> constant = semanticModel.GetConstantValue(argument.Expression, cancellationToken);
            return constant.HasValue && constant.Value is string value && !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }

        return null;
    }

    private static IReadOnlyList<NativeDependencySummary> AggregateNativeDependencies(
        IEnumerable<NativeDependencyObservation> observations)
    {
        return observations
            .GroupBy(
                observation => (
                    LibraryName: observation.LibraryName.ToLowerInvariant(),
                    ImportKind: observation.ImportKind.ToLowerInvariant(),
                    Confidence: observation.Confidence.ToLowerInvariant()))
            .Select(group => new NativeDependencySummary(
                group.First().LibraryName,
                group.First().ImportKind,
                group.First().Confidence,
                group.Select(item => item.SourceDocumentId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                group.Select(item => item.SourceSymbolId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                group.Select(item => item.ImportedSymbol)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(item => item.LibraryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ImportKind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> EnumerateFiles(string rootDirectory, string extension)
    {
        List<string> files = [];
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(rootDirectory);

        while (pendingDirectories.Count > 0)
        {
            string currentDirectory = pendingDirectories.Pop();
            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch
            {
                continue;
            }

            foreach (string subDirectory in subDirectories)
            {
                string name = Path.GetFileName(subDirectory);
                if (IsIgnoredDirectory(name))
                {
                    continue;
                }

                pendingDirectories.Push(subDirectory);
            }

            IEnumerable<string> currentFiles;
            try
            {
                currentFiles = Directory.EnumerateFiles(currentDirectory, $"*{extension}", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                continue;
            }

            foreach (string filePath in currentFiles)
            {
                files.Add(Path.GetFullPath(filePath));
            }
        }

        return files
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsIgnoredDirectory(string name)
    {
        return AnalysisPathFilter.IsIgnoredDirectoryName(name);
    }

    private static string ResolveWorkspaceKind(string path)
    {
        if (Directory.Exists(path))
        {
            return "folder";
        }

        string extension = Path.GetExtension(path);
        return
            string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
                ? "solution"
                : "project";
    }

    private static IReadOnlyList<string> ReadPackageReferences(
        string? projectFilePath,
        ConcurrentQueue<string> diagnostics,
        string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
        {
            return Array.Empty<string>();
        }

        try
        {
            XDocument document = XDocument.Load(projectFilePath);
            return document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal))
                .Select(element =>
                {
                    string? include = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value;
                    string? version = element.Attribute("Version")?.Value
                        ?? element.Elements().FirstOrDefault(versionElement =>
                            string.Equals(versionElement.Name.LocalName, "Version", StringComparison.Ordinal))?.Value;

                    if (string.IsNullOrWhiteSpace(include))
                    {
                        return null;
                    }

                    return string.IsNullOrWhiteSpace(version)
                        ? include
                        : $"{include} ({version})";
                })
                .Where(package => !string.IsNullOrWhiteSpace(package))
                .Select(package => package!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(package => package, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            diagnostics.Enqueue(T("diag.roslyn.packageReferenceFailed", projectName, ex.Message));
            return Array.Empty<string>();
        }
    }

    private static string BuildDocumentId(string projectIdentity, string? filePath, string documentName)
    {
        return AnalysisIdBuilder.BuildDocumentId(projectIdentity, filePath, documentName);
    }

    private static string BuildSymbolId(string symbolName, string symbolKey)
    {
        return AnalysisIdBuilder.BuildSymbolId(symbolName, symbolKey);
    }

    private static string CreateSymbolIdentity(ISymbol symbol)
    {
        string assemblyName = symbol.ContainingAssembly?.Identity.Name ?? "workspace";
        string displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return $"{assemblyName}|{symbol.Kind}|{displayName}";
    }

    private static int GetLineNumber(SyntaxNode syntaxNode)
    {
        return syntaxNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    private static string? ExtractLineSnippet(SourceText sourceText, int lineNumber)
    {
        return DependencySampleHelper.ExtractLineSnippet(sourceText, lineNumber);
    }

    private static string? NormalizeDependencySnippet(string? snippet)
    {
        return DependencySampleHelper.NormalizeSnippet(snippet);
    }

    private static void TrySetDependencySample<TKey>(
        IDictionary<TKey, AnalysisDependencySample> samples,
        TKey key,
        string? filePath,
        int? lineNumber,
        string? snippet)
        where TKey : notnull
    {
        DependencySampleHelper.TrySetSample(samples, key, filePath, lineNumber, snippet);
    }

    private static DocumentDependencySummary CreateDocumentDependencySummary(
        (string Source, string Target) key,
        int referenceCount,
        IReadOnlyDictionary<(string Source, string Target), AnalysisDependencySample> samples)
    {
        samples.TryGetValue(key, out AnalysisDependencySample sample);
        return new DocumentDependencySummary(
            key.Source,
            key.Target,
            referenceCount,
            sample.FilePath,
            sample.LineNumber,
            sample.Snippet);
    }

    private static SymbolDependencySummary CreateSymbolDependencySummary(
        (string Source, string Target, string ReferenceKind, string Confidence) key,
        int referenceCount,
        IReadOnlyDictionary<(string Source, string Target, string ReferenceKind, string Confidence), AnalysisDependencySample> samples)
    {
        samples.TryGetValue(key, out AnalysisDependencySample sample);
        return new SymbolDependencySummary(
            key.Source,
            key.Target,
            referenceCount,
            key.ReferenceKind,
            key.Confidence,
            sample.FilePath,
            sample.LineNumber,
            sample.Snippet);
    }

    private static void ReportProgress(IProgress<AnalysisProgressUpdate>? progress, AnalysisProgressUpdate update)
    {
        progress?.Report(update);
    }

    private sealed record ProjectAnalysisResult(
        ProjectAnalysisSummary Summary,
        ProjectDependencyExtractionResult DependencyExtraction);

    private sealed record ProjectDependencyExtractionResult(
        IReadOnlyList<DeclaredSymbolEdgeAnchor> DeclaredSymbols,
        IReadOnlyList<SymbolReferenceObservation> ReferenceObservations);

    private sealed record DeclaredSymbolEdgeAnchor(
        string SymbolKey,
        string SymbolId,
        string DocumentId);

    private sealed record SymbolReferenceObservation(
        string SourceDocumentId,
        string? SourceSymbolKey,
        string TargetSymbolKey,
        string ReferenceKind,
        string Confidence,
        string? SampleFilePath,
        int? SampleLineNumber,
        string? SampleSnippet);

    private sealed record NativeDependencyObservation(
        string LibraryName,
        string ImportKind,
        string Confidence,
        string SourceDocumentId,
        string? SourceSymbolId,
        string? ImportedSymbol);
}
