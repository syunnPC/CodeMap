using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CodeMap.Analysis;

public sealed class CppAnalysisService
{
    private static readonly Regex s_slnProjectPattern = new(
        @"^\s*Project\([^)]*\)\s*=\s*""[^""]+"",\s*""(?<path>[^""]+)""\s*,",
        RegexOptions.Compiled);

    private static readonly Regex s_includePattern = new(
        @"^\s*#\s*include\s*([<""])\s*([^""><]+?)\s*[>""]",
        RegexOptions.Compiled);

    private static readonly Regex s_typeDeclarationPattern = new(
        @"^\s*(?:class|struct|union|enum(?:\s+class)?)\s+(?<name>[A-Za-z_]\w*)\b",
        RegexOptions.Compiled);

    private static readonly Regex s_namespaceDeclarationPattern = new(
        @"^\s*namespace\s+(?<name>[A-Za-z_]\w*(?:::[A-Za-z_]\w*)*)\b",
        RegexOptions.Compiled);

    private static readonly Regex s_usingAliasPattern = new(
        @"^\s*using\s+(?<name>[A-Za-z_]\w*)\s*=",
        RegexOptions.Compiled);

    private static readonly Regex s_macroDefinitionPattern = new(
        @"^\s*#\s*define\s+(?<name>[A-Za-z_]\w*)\b",
        RegexOptions.Compiled);

    private static readonly Regex s_functionPattern = new(
        @"^\s*(?:template\s*<[^;{>]+>\s*)?(?:[A-Za-z_][\w:\<\>\*&~\s]*\s+)?(?<name>(?:[A-Za-z_]\w*::)*~?[A-Za-z_]\w*)\s*\([^;{}]*\)\s*(?:const\b|noexcept\b|override\b|final\b|->\s*[^;{]+|\s)*(?<terminator>\{|;)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex s_loadLibraryPattern = new(
        @"(?<api>LoadLibrary(?:Ex)?[AW]?)\s*\(\s*(?:L)?""(?<dll>[^""]+?\.dll)""",
        RegexOptions.Compiled);

    private static readonly Regex s_getProcAddressPattern = new(
        @"GetProcAddress\s*\(\s*(?<handle>[A-Za-z_]\w*)\s*,\s*""(?<proc>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex s_loadLibraryAssignmentPattern = new(
        @"(?:(?<handle>[A-Za-z_]\w*)\s*=\s*)?(?<api>LoadLibrary(?:Ex)?[AW]?)\s*\(\s*(?:L)?""(?<dll>[^""]+?\.dll)""",
        RegexOptions.Compiled);

    private static readonly Regex s_referenceTokenPattern = new(
        @"(?<![A-Za-z0-9_~:])(?<name>(?:[A-Za-z_]\w*::)+~?[A-Za-z_]\w*|~?[A-Za-z_]\w*)(?![A-Za-z0-9_~:])",
        RegexOptions.Compiled);

    private static readonly Regex s_msBuildPropertyPattern = new(
        @"\$\((?<name>[^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex s_msBuildItemMetadataPattern = new(
        @"%\((?<name>[^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex s_commandTokenPattern = new(
        "\"(?:\\\\.|[^\"])*\"|[^\\s]+",
        RegexOptions.Compiled);

    private static readonly HashSet<string> s_cppExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".c", ".cc", ".cpp", ".cxx", ".c++",
        ".h", ".hh", ".hpp", ".hxx", ".inl", ".ipp", ".ixx", ".tpp"
    };

    private static readonly HashSet<string> s_cppKeywords = new(StringComparer.Ordinal)
    {
        "alignas", "alignof", "and", "and_eq", "asm", "auto", "bitand", "bitor", "bool", "break", "case", "catch",
        "char", "char8_t", "char16_t", "char32_t", "class", "compl", "concept", "const", "consteval", "constexpr", "constinit",
        "const_cast", "continue", "co_await", "co_return", "co_yield", "decltype", "default", "delete", "do", "double", "dynamic_cast",
        "else", "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "if", "inline", "int", "long",
        "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr", "operator", "or", "or_eq", "private", "protected",
        "public", "register", "reinterpret_cast", "requires", "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast",
        "struct", "switch", "template", "this", "thread_local", "throw", "true", "try", "typedef", "typeid", "typename", "union", "unsigned",
        "using", "virtual", "void", "volatile", "wchar_t", "while", "xor", "xor_eq"
    };

    private static readonly HashSet<string> s_disallowedFunctionNames = new(StringComparer.Ordinal)
    {
        "if", "for", "while", "switch", "catch", "return", "sizeof", "alignof", "decltype", "new", "delete"
    };

    private const int MaxEnumeratedCppFiles = 250000;
    private const int MaxParsedCppDocuments = 50000;
    private const int LargeCppFileCountWarningThreshold = 5000;
    private static readonly TimeSpan s_largeCppAnalysisDurationWarningThreshold = TimeSpan.FromSeconds(20);

    private static string T(string key, params object[] args)
    {
        return global::CodeMap.AppLocalization.Get(key, args);
    }

    private static string ResolveLibClangFailureMessage(string rawFailureCode)
    {
        if (string.IsNullOrWhiteSpace(rawFailureCode))
        {
            return "-";
        }

        if (string.Equals(rawFailureCode, "source-file-not-found", StringComparison.Ordinal))
        {
            return T("diag.cpp.libClangFailure.sourceFileNotFound");
        }

        if (string.Equals(rawFailureCode, "create-index-failed", StringComparison.Ordinal))
        {
            return T("diag.cpp.libClangFailure.createIndexFailed");
        }

        if (string.Equals(rawFailureCode, "parse-translation-unit-null", StringComparison.Ordinal))
        {
            return T("diag.cpp.libClangFailure.translationUnitNull");
        }

        if (string.Equals(rawFailureCode, "dll-not-found", StringComparison.Ordinal))
        {
            return T("diag.cpp.libClangFailure.dllNotFound");
        }

        if (string.Equals(rawFailureCode, "entrypoint-not-found", StringComparison.Ordinal))
        {
            return T("diag.cpp.libClangFailure.entryPointNotFound");
        }

        const string parseFailurePrefix = "parse-translation-unit-failed:";
        if (rawFailureCode.StartsWith(parseFailurePrefix, StringComparison.Ordinal))
        {
            return T("diag.cpp.libClangFailure.translationUnitParseFailed", rawFailureCode[parseFailurePrefix.Length..]);
        }

        return rawFailureCode;
    }

    public Task<CppSolutionAnalysisResult> AnalyzeAsync(
        string solutionOrProjectPath,
        CancellationToken cancellationToken = default)
    {
        return AnalyzeAsync(solutionOrProjectPath, progress: null, cancellationToken);
    }

    public async Task<CppSolutionAnalysisResult> AnalyzeAsync(
        string solutionOrProjectPath,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionOrProjectPath);

        string normalizedInputPath = Path.GetFullPath(solutionOrProjectPath);
        ReportProgress(
            progress,
            new AnalysisProgressUpdate(AnalysisProgressStage.DiscoveringNativeProjects, normalizedInputPath));
        List<string> diagnostics = [];
        string workspaceRootPath = ResolveWorkspaceRootPath(normalizedInputPath);
        if (Directory.Exists(normalizedInputPath))
        {
            CppProjectAnalysisResult folderResult = await AnalyzeFolderAsync(
                normalizedInputPath,
                diagnostics,
                normalizedInputPath,
                progress,
                cancellationToken);
            return new CppSolutionAnalysisResult([folderResult], diagnostics);
        }

        IReadOnlyList<string> projectPaths = DiscoverProjectPaths(normalizedInputPath, diagnostics);

        List<CppProjectAnalysisResult> projects = [];
        foreach (string projectPath in projectPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ReportProgress(
                    progress,
                    new AnalysisProgressUpdate(
                        AnalysisProgressStage.AnalyzingNativeProject,
                        normalizedInputPath,
                        Path.GetFileNameWithoutExtension(projectPath),
                        projectPath));
                CppProjectAnalysisResult result = await AnalyzeProjectAsync(
                    projectPath,
                    workspaceRootPath,
                    diagnostics,
                    normalizedInputPath,
                    progress,
                    cancellationToken);
                projects.Add(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                diagnostics.Add(T("diag.cpp.projectFailed", projectPath, ex.Message));
            }
        }

        return new CppSolutionAnalysisResult(projects, diagnostics);
    }

    private static IReadOnlyList<string> DiscoverProjectPaths(string inputPath, ICollection<string> diagnostics)
    {
        string extension = Path.GetExtension(inputPath);
        if (string.Equals(extension, ".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(inputPath) ? [inputPath] : Array.Empty<string>();
        }

        if (!File.Exists(inputPath))
        {
            diagnostics.Add(T("diag.cpp.inputMissing", inputPath));
            return Array.Empty<string>();
        }

        if (string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                string solutionDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty;
                XDocument slnx = XDocument.Load(inputPath);
                return slnx
                    .Descendants()
                    .Where(element => string.Equals(element.Name.LocalName, "Project", StringComparison.Ordinal))
                    .Select(element => element.Attribute("Path")?.Value)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => ResolvePath(solutionDirectory, path!))
                    .Where(path => path is not null && path.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
                    .Select(path => path!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                diagnostics.Add(T("diag.cpp.slnxParseFailed", ex.Message));
                return Array.Empty<string>();
            }
        }

        if (!string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(T("diag.cpp.unsupportedInputSkipped", inputPath));
            return Array.Empty<string>();
        }

        try
        {
            string solutionDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty;
            List<string> projectPaths = [];
            foreach (string line in File.ReadLines(inputPath))
            {
                Match match = s_slnProjectPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                string? path = ResolvePath(solutionDirectory, match.Groups["path"].Value);
                if (path is null || !path.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                projectPaths.Add(path);
            }

            return projectPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (Exception ex)
        {
            diagnostics.Add(T("diag.cpp.slnParseFailed", ex.Message));
            return Array.Empty<string>();
        }
    }

    private static async Task<CppProjectAnalysisResult> AnalyzeProjectAsync(
        string projectPath,
        string workspaceRootPath,
        ICollection<string> diagnostics,
        string workspacePath,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project directory not found: {projectPath}");
        XDocument projectDocument = XDocument.Load(projectPath);

        string projectName = ResolveProjectName(projectDocument, projectPath);
        ProjectXmlDocument rootProjectDocument = new(
            Path.GetFullPath(projectPath),
            projectDirectory,
            projectDocument);
        MsBuildEvaluationContext importResolutionContext = CreateEvaluationContext(
            projectDocument,
            [rootProjectDocument],
            projectPath,
            workspaceRootPath,
            projectName);
        IReadOnlyList<ProjectXmlDocument> projectDocuments = LoadProjectDocuments(
            rootProjectDocument,
            importResolutionContext,
            diagnostics);
        MsBuildEvaluationContext evaluationContext = CreateEvaluationContext(
            projectDocument,
            projectDocuments,
            projectPath,
            workspaceRootPath,
            projectName);
        diagnostics.Add(T(
            "diag.cpp.projectEvaluation",
            projectName,
            evaluationContext.Configuration,
            evaluationContext.Platform));

        IReadOnlyList<string> projectFiles = CollectProjectFiles(projectDocuments, evaluationContext, diagnostics);
        List<string> includeDirectories = CollectIncludeDirectories(projectDocuments, evaluationContext);
        CompileCommandData compileCommandData = ReadCompileCommandData(
            projectDirectory,
            workspaceRootPath,
            projectFiles,
            diagnostics);
        AppendUniqueDirectories(
            includeDirectories,
            compileCommandData.IncludeDirectories);
        AppendUniqueDirectories(
            includeDirectories,
            projectFiles
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (projectFiles.Count == 0)
        {
            diagnostics.Add(T("diag.cpp.projectNoFiles", projectName));
        }
        else
        {
            ProbeWithLibClang(
                projectName,
                projectFiles,
                includeDirectories,
                compileCommandData.ParseArgumentsBySourceFile,
                diagnostics);
        }

        string projectKey = AnalysisIdentity.BuildProjectKey(projectName, projectPath);
        WarnIfLargeCppInput(projectName, projectFiles.Count, diagnostics);

        DateTimeOffset analysisStartedAt = DateTimeOffset.UtcNow;
        ProjectParseArtifacts artifacts = await AnalyzeDocumentsAsync(
            projectKey,
            projectName,
            workspaceRootPath,
            projectFiles,
            includeDirectories,
            compileCommandData.ParseArgumentsBySourceFile,
            diagnostics,
            workspacePath,
            progress,
            cancellationToken);
        TimeSpan analysisDuration = DateTimeOffset.UtcNow - analysisStartedAt;

        IReadOnlyList<NativeDependencySummary> projectLevelNativeDependencies = ReadProjectLevelNativeDependencies(
            projectDocuments,
            evaluationContext);

        ProjectAnalysisSummary summary = new(
            projectName,
            "C/C++",
            projectPath,
            projectKey,
            IsFolderBased: false,
            artifacts.Documents,
            ReadProjectReferences(projectDocuments, evaluationContext),
            ReadMetadataReferences(projectDocuments, evaluationContext),
            ReadPackageReferences(projectDocuments, evaluationContext),
            AggregateNativeDependencies(projectLevelNativeDependencies.Concat(artifacts.NativeDependencies)));

        diagnostics.Add(T(
            "diag.cpp.symbolsExtracted",
            projectName,
            artifacts.Documents.Count,
            artifacts.SymbolCount,
            artifacts.DocumentDependencies.Count,
            artifacts.SymbolDependencies.Count));
        ReportCppAnalysisDuration(projectName, projectFiles.Count, analysisDuration, diagnostics);

        return new CppProjectAnalysisResult(summary, artifacts.DocumentDependencies, artifacts.SymbolDependencies);
    }

    private static async Task<CppProjectAnalysisResult> AnalyzeFolderAsync(
        string folderPath,
        ICollection<string> diagnostics,
        string workspacePath,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> projectFiles = EnumerateCppFiles(folderPath, diagnostics)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        CompileCommandData compileCommandData = ReadCompileCommandData(
            folderPath,
            folderPath,
            projectFiles,
            diagnostics);
        List<string> includeDirectories = projectFiles
            .Select(filePath => Path.GetDirectoryName(filePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        AppendUniqueDirectories(includeDirectories, compileCommandData.IncludeDirectories);
        AppendUniqueDirectories(includeDirectories, [folderPath]);

        string projectName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = folderPath;
        }

        string projectKey = AnalysisIdentity.BuildProjectKey(projectName, folderPath);

        if (projectFiles.Count == 0)
        {
            diagnostics.Add(T("diag.cpp.folderNoFiles", folderPath));
        }
        else
        {
            ProbeWithLibClang(
                projectName,
                projectFiles,
                includeDirectories,
                compileCommandData.ParseArgumentsBySourceFile,
                diagnostics);
        }

        WarnIfLargeCppInput(projectName, projectFiles.Count, diagnostics);

        DateTimeOffset analysisStartedAt = DateTimeOffset.UtcNow;
        ProjectParseArtifacts artifacts = await AnalyzeDocumentsAsync(
            projectKey,
            projectName,
            folderPath,
            projectFiles,
            includeDirectories,
            compileCommandData.ParseArgumentsBySourceFile,
            diagnostics,
            workspacePath,
            progress,
            cancellationToken);
        TimeSpan analysisDuration = DateTimeOffset.UtcNow - analysisStartedAt;

        ProjectAnalysisSummary summary = new(
            projectName,
            "C/C++",
            folderPath,
            projectKey,
            IsFolderBased: true,
            artifacts.Documents,
            ProjectReferences: Array.Empty<ProjectReferenceSummary>(),
            MetadataReferences: Array.Empty<string>(),
            PackageReferences: Array.Empty<string>(),
            NativeDependencies: artifacts.NativeDependencies);

        diagnostics.Add(T(
            "diag.cpp.folderSymbolsExtracted",
            artifacts.Documents.Count,
            artifacts.SymbolCount,
            artifacts.DocumentDependencies.Count,
            artifacts.SymbolDependencies.Count));
        ReportCppAnalysisDuration(projectName, projectFiles.Count, analysisDuration, diagnostics);

        return new CppProjectAnalysisResult(summary, artifacts.DocumentDependencies, artifacts.SymbolDependencies);
    }

    private static async Task<ProjectParseArtifacts> AnalyzeDocumentsAsync(
        string projectIdentity,
        string projectName,
        string workspaceRootPath,
        IReadOnlyList<string> projectFiles,
        IReadOnlyList<string> includeDirectories,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parseArgumentsBySourceFile,
        ICollection<string> diagnostics,
        string workspacePath,
        IProgress<AnalysisProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        Dictionary<string, DocumentDescriptor> documentsByPath = new(StringComparer.OrdinalIgnoreCase);
        foreach (string projectFile in projectFiles)
        {
            AddDocument(documentsByPath, projectIdentity, projectFile);
        }

        Dictionary<(string Source, string Target), int> documentDependencyCounts = [];
        Dictionary<(string Source, string Target), AnalysisDependencySample> documentDependencySamples = [];
        HashSet<(string Source, string Target)> includeDocumentDependencyPairs = [];
        List<DocumentParseContext> parsedDocuments = [];
        List<NativeDependencySummary> nativeDependencySummaries = [];
        int astDrivenDocumentCount = 0;
        int regexFallbackDocumentCount = 0;
        string? firstAstFailureMessage = null;

        Queue<DocumentDescriptor> pendingDocuments = new(
            documentsByPath.Values
                .OrderBy(document => document.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(document => document.FilePath, StringComparer.OrdinalIgnoreCase));

        HashSet<string> parsedDocumentPaths = new(StringComparer.OrdinalIgnoreCase);
        while (pendingDocuments.Count > 0 && parsedDocuments.Count < MaxParsedCppDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DocumentDescriptor sourceDocument = pendingDocuments.Dequeue();
            if (!parsedDocumentPaths.Add(sourceDocument.FilePath) || !File.Exists(sourceDocument.FilePath))
            {
                continue;
            }

            ReportProgress(
                progress,
                new AnalysisProgressUpdate(
                    AnalysisProgressStage.AnalyzingNativeDocument,
                    workspacePath,
                    projectName,
                    sourceDocument.FilePath));

            string[] rawLines = await File.ReadAllLinesAsync(sourceDocument.FilePath, cancellationToken);
            string[] codeLines = CppSourceTextStripper.StripCommentsAndStrings(rawLines);
            string[] rawLinesWithoutComments = CppSourceTextStripper.StripCommentsPreservingStrings(rawLines);
            IReadOnlyList<IncludeDirective> includeDirectives = ReadIncludeDirectives(rawLinesWithoutComments, rawLines);
            IReadOnlyList<string>? parseArguments = ResolveParseArgumentsForFile(
                sourceDocument.FilePath,
                parseArgumentsBySourceFile);
            bool isAstDriven = LibClangTranslationUnitProbe.TryAnalyzeDocument(
                sourceDocument.FilePath,
                includeDirectories,
                parseArguments,
                out LibClangDocumentAnalysisResult astResult,
                out string astFailureMessage);
            IReadOnlyList<CppDeclaredSymbol> declaredSymbols = isAstDriven
                ? ExtractDeclaredSymbolsFromAst(sourceDocument, projectIdentity, astResult.Symbols)
                : ExtractDeclaredSymbols(sourceDocument, projectIdentity, codeLines);
            IReadOnlyList<AstReferenceObservation> astReferences = isAstDriven
                ? ConvertAstReferences(astResult.References, sourceDocument.FilePath, rawLines)
                : Array.Empty<AstReferenceObservation>();

            if (isAstDriven)
            {
                astDrivenDocumentCount++;
            }
            else
            {
                regexFallbackDocumentCount++;
                firstAstFailureMessage ??= astFailureMessage;
            }

            parsedDocuments.Add(new DocumentParseContext(
                sourceDocument,
                codeLines,
                rawLines,
                declaredSymbols,
                astReferences,
                isAstDriven));
            nativeDependencySummaries.AddRange(ExtractNativeDependencies(
                sourceDocument.DocumentId,
                declaredSymbols,
                rawLinesWithoutComments));

            foreach (IncludeDirective includeDirective in includeDirectives)
            {
                string? resolvedPath = ResolveIncludePath(
                    sourceDocument.FilePath,
                    includeDirective,
                    includeDirectories);
                if (resolvedPath is null)
                {
                    continue;
                }

                if (!documentsByPath.TryGetValue(resolvedPath, out DocumentDescriptor? targetDocument))
                {
                    if (!IsPathUnderRoot(resolvedPath, workspaceRootPath) || !IsCppFile(resolvedPath))
                    {
                        continue;
                    }

                    targetDocument = AddDocument(documentsByPath, projectIdentity, resolvedPath);
                    pendingDocuments.Enqueue(targetDocument);
                }

                if (!string.Equals(sourceDocument.DocumentId, targetDocument.DocumentId, StringComparison.Ordinal))
                {
                    (string Source, string Target) dependencyKey = (sourceDocument.DocumentId, targetDocument.DocumentId);
                    includeDocumentDependencyPairs.Add(dependencyKey);
                    TrySetDependencySample(
                        documentDependencySamples,
                        dependencyKey,
                        sourceDocument.FilePath,
                        includeDirective.LineNumber,
                        includeDirective.Snippet);
                }
            }
        }

        if (pendingDocuments.Count > 0)
        {
            diagnostics.Add(T(
                "diag.cpp.documentParseLimitReached",
                MaxParsedCppDocuments,
                projectName,
                parsedDocuments.Count,
                pendingDocuments.Count));
        }

        diagnostics.Add(T(
            "diag.cpp.libClangAstSummary",
            projectName,
            astDrivenDocumentCount,
            regexFallbackDocumentCount,
            ResolveLibClangFailureMessage(firstAstFailureMessage ?? "-")));

        List<CppDeclaredSymbol> allDeclaredSymbols = parsedDocuments
            .SelectMany(context => context.DeclaredSymbols)
            .ToList();
        Dictionary<string, IReadOnlyList<SymbolAnalysisSummary>> symbolsByDocumentId = parsedDocuments
            .GroupBy(context => context.Document.DocumentId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SymbolAnalysisSummary>)group
                    .SelectMany(context => context.DeclaredSymbols)
                    .Select(symbol => symbol.Summary)
                    .OrderBy(symbol => symbol.LineNumber)
                    .ThenBy(symbol => symbol.Name, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        CppSymbolLookup symbolLookup = BuildSymbolLookup(allDeclaredSymbols);
        Dictionary<(string Source, string Target), int> symbolDependencyCounts = [];
        Dictionary<(string Source, string Target), Dictionary<string, int>> symbolDependencyKinds = [];
        Dictionary<(string Source, string Target), AnalysisDependencySample> symbolDependencySamples = [];
        foreach (DocumentParseContext parsedDocument in parsedDocuments)
        {
            if (parsedDocument.IsAstDriven)
            {
                ExtractAstSymbolDependencies(
                    parsedDocument,
                    symbolLookup,
                    symbolDependencyCounts,
                    symbolDependencyKinds,
                    symbolDependencySamples);
            }
            else
            {
                ExtractDeclarationDependencies(
                    parsedDocument,
                    symbolLookup,
                    symbolDependencyCounts,
                    symbolDependencyKinds,
                    symbolDependencySamples);
                ExtractSymbolDependencies(
                    parsedDocument,
                    symbolLookup,
                    symbolDependencyCounts,
                    symbolDependencyKinds,
                    symbolDependencySamples);
            }
        }

        foreach ((string Source, string Target) includeDependency in includeDocumentDependencyPairs)
        {
            // C/C++ のドキュメント依存は include 関係を基準に扱う。
            // シンボル参照由来の依存は symbolDependencies 側で可視化する。
            IncrementCount(documentDependencyCounts, includeDependency, amount: 1);
        }

        IReadOnlyList<DocumentAnalysisSummary> documents = documentsByPath.Values
            .OrderBy(document => document.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(document => document.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(document =>
            {
                IReadOnlyList<SymbolAnalysisSummary> symbols = symbolsByDocumentId.TryGetValue(
                    document.DocumentId,
                    out IReadOnlyList<SymbolAnalysisSummary>? resolvedSymbols)
                    ? resolvedSymbols
                    : Array.Empty<SymbolAnalysisSummary>();

                return new DocumentAnalysisSummary(
                    document.DocumentId,
                    document.Name,
                    document.FilePath,
                    symbols);
            })
            .ToArray();

        IReadOnlyList<DocumentDependencySummary> documentDependencies = documentDependencyCounts
            .Select(entry =>
            {
                documentDependencySamples.TryGetValue(entry.Key, out AnalysisDependencySample sample);
                return new DocumentDependencySummary(
                    entry.Key.Source,
                    entry.Key.Target,
                    entry.Value,
                    sample.FilePath,
                    sample.LineNumber,
                    sample.Snippet);
            })
            .OrderByDescending(item => item.ReferenceCount)
            .ThenBy(item => item.SourceDocumentId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetDocumentId, StringComparer.Ordinal)
            .ToArray();

        IReadOnlyList<SymbolDependencySummary> symbolDependencies = symbolDependencyCounts
            .Select(entry =>
            {
                symbolDependencySamples.TryGetValue(entry.Key, out AnalysisDependencySample sample);
                return new SymbolDependencySummary(
                    entry.Key.Source,
                    entry.Key.Target,
                    entry.Value,
                    ResolveDominantReferenceKind(symbolDependencyKinds, entry.Key),
                    "medium",
                    sample.FilePath,
                    sample.LineNumber,
                    sample.Snippet);
            })
            .OrderByDescending(item => item.ReferenceCount)
            .ThenBy(item => item.SourceSymbolId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetSymbolId, StringComparer.Ordinal)
            .ToArray();

        return new ProjectParseArtifacts(
            documents,
            documentDependencies,
            symbolDependencies,
            AggregateNativeDependencies(nativeDependencySummaries),
            allDeclaredSymbols.Count);
    }

    private static IReadOnlyList<string> CollectProjectFiles(
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        MsBuildEvaluationContext evaluationContext,
        ICollection<string> diagnostics)
    {
        HashSet<string> files = new(StringComparer.OrdinalIgnoreCase);

        foreach (ProjectXmlDocument projectDocument in projectDocuments)
        {
            foreach (XElement element in projectDocument.Document.Descendants())
            {
                if (!IsElementActive(element, evaluationContext, projectDocument.BaseDirectory))
                {
                    continue;
                }

                if (
                    !string.Equals(element.Name.LocalName, "ClCompile", StringComparison.Ordinal) &&
                    !string.Equals(element.Name.LocalName, "ClInclude", StringComparison.Ordinal) &&
                    !string.Equals(element.Name.LocalName, "None", StringComparison.Ordinal))
                {
                    continue;
                }

                string? includePath = element.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(includePath) || includePath.IndexOfAny(['*', '?']) >= 0)
                {
                    continue;
                }

                string? resolved = ResolvePath(projectDocument.BaseDirectory, includePath, evaluationContext);
                if (resolved is null || !File.Exists(resolved) || !IsCppFile(resolved))
                {
                    continue;
                }

                files.Add(resolved);
            }
        }

        if (files.Count > 0)
        {
            return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        foreach (string file in EnumerateCppFiles(evaluationContext.ProjectDirectory, diagnostics))
        {
            files.Add(file);
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void ProbeWithLibClang(
        string projectName,
        IReadOnlyList<string> projectFiles,
        IReadOnlyList<string> includeDirectories,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parseArgumentsBySourceFile,
        ICollection<string> diagnostics)
    {
        string? probeFile = projectFiles
            .FirstOrDefault(path => string.Equals(Path.GetExtension(path), ".cpp", StringComparison.OrdinalIgnoreCase))
            ?? projectFiles.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(probeFile))
        {
            return;
        }

        IReadOnlyList<string>? parseArguments = ResolveParseArgumentsForFile(probeFile, parseArgumentsBySourceFile);
        bool parsed = LibClangTranslationUnitProbe.TryParse(
            probeFile,
            includeDirectories,
            parseArguments,
            out string message);
        diagnostics.Add(parsed
            ? T("diag.cpp.libClangProbeSuccess", projectName, Path.GetFileName(probeFile))
            : T("diag.cpp.libClangProbeFailed", projectName, Path.GetFileName(probeFile), ResolveLibClangFailureMessage(message)));
    }

    private static IReadOnlyList<string>? ResolveParseArgumentsForFile(
        string sourceFilePath,
        IReadOnlyDictionary<string, IReadOnlyList<string>> parseArgumentsBySourceFile)
    {
        if (parseArgumentsBySourceFile.TryGetValue(sourceFilePath, out IReadOnlyList<string>? parseArguments) &&
            parseArguments.Count > 0)
        {
            return parseArguments;
        }

        return null;
    }

    private static List<string> CollectIncludeDirectories(
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        MsBuildEvaluationContext evaluationContext)
    {
        List<string> includeDirectories = [];
        HashSet<string> seenDirectories = new(StringComparer.OrdinalIgnoreCase);
        foreach (ProjectXmlDocument projectDocument in projectDocuments)
        {
            foreach (XElement element in projectDocument.Document.Descendants())
            {
                if (!IsElementActive(element, evaluationContext, projectDocument.BaseDirectory))
                {
                    continue;
                }

                if (
                    !string.Equals(element.Name.LocalName, "AdditionalIncludeDirectories", StringComparison.Ordinal) &&
                    !string.Equals(element.Name.LocalName, "IncludePath", StringComparison.Ordinal) &&
                    !string.Equals(element.Name.LocalName, "ExternalIncludePath", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string value in SplitMsBuildList(element.Value))
                {
                    string? normalized = NormalizeDirectory(value, projectDocument.BaseDirectory, evaluationContext);
                    if (normalized is not null && seenDirectories.Add(normalized))
                    {
                        includeDirectories.Add(normalized);
                    }
                }
            }
        }

        return includeDirectories;
    }

    private static IReadOnlyList<ProjectXmlDocument> LoadProjectDocuments(
        ProjectXmlDocument rootDocument,
        MsBuildEvaluationContext evaluationContext,
        ICollection<string> diagnostics)
    {
        List<ProjectXmlDocument> documents = [];
        HashSet<string> loadedPaths = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> queuedPaths = new(StringComparer.OrdinalIgnoreCase);
        Queue<ProjectXmlDocument> pendingDocuments = new();

        pendingDocuments.Enqueue(rootDocument);
        queuedPaths.Add(rootDocument.FilePath);

        while (pendingDocuments.Count > 0)
        {
            ProjectXmlDocument current = pendingDocuments.Dequeue();
            if (!loadedPaths.Add(current.FilePath))
            {
                continue;
            }

            documents.Add(current);

            foreach (XElement importElement in current.Document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "Import", StringComparison.Ordinal)))
            {
                if (!IsElementActive(importElement, evaluationContext, current.BaseDirectory))
                {
                    continue;
                }

                string? importProject = importElement.Attribute("Project")?.Value;
                if (string.IsNullOrWhiteSpace(importProject) || importProject.IndexOfAny(['*', '?']) >= 0)
                {
                    continue;
                }

                string? resolvedImportPath = ResolvePath(current.BaseDirectory, importProject, evaluationContext);
                if (resolvedImportPath is null || !File.Exists(resolvedImportPath) || !queuedPaths.Add(resolvedImportPath))
                {
                    continue;
                }

                try
                {
                    XDocument importedDocument = XDocument.Load(resolvedImportPath);
                    pendingDocuments.Enqueue(new ProjectXmlDocument(
                        resolvedImportPath,
                        Path.GetDirectoryName(resolvedImportPath) ?? current.BaseDirectory,
                        importedDocument));
                }
                catch (Exception ex)
                {
                    diagnostics.Add(T("diag.cpp.importSkipped", resolvedImportPath, ex.Message));
                }
            }
        }

        return documents;
    }

    private static CompileCommandData ReadCompileCommandData(
        string projectDirectory,
        string workspaceRootPath,
        IReadOnlyList<string> projectFiles,
        ICollection<string> diagnostics)
    {
        string? compileCommandsPath =
            new[] { Path.Combine(projectDirectory, "compile_commands.json"), Path.Combine(workspaceRootPath, "compile_commands.json") }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);

        if (compileCommandsPath is null)
        {
            return CompileCommandData.Empty;
        }

        try
        {
            List<string> includeDirectories = [];
            HashSet<string> seenIncludeDirectories = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IReadOnlyList<string>> parseArgumentsBySourceFile = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> projectFileSet = projectFiles
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            using FileStream stream = File.OpenRead(compileCommandsPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return CompileCommandData.Empty;
            }

            foreach (JsonElement entry in document.RootElement.EnumerateArray())
            {
                string workingDirectory = ResolveCompileCommandWorkingDirectory(entry, workspaceRootPath);
                IReadOnlyList<string> tokens = ReadCommandTokens(entry);
                string? sourceFilePath = ResolveCompileCommandSourceFilePath(entry, tokens, workingDirectory);
                if (!IsRelevantCompileCommandEntry(sourceFilePath, projectDirectory, projectFileSet))
                {
                    continue;
                }

                IReadOnlyList<string> parseArguments = BuildParseArgumentsFromCompileCommand(
                    tokens,
                    workingDirectory,
                    sourceFilePath,
                    includeDirectories,
                    seenIncludeDirectories);
                if (!string.IsNullOrWhiteSpace(sourceFilePath) &&
                    (!parseArgumentsBySourceFile.TryGetValue(sourceFilePath, out IReadOnlyList<string>? existing) ||
                        parseArguments.Count > existing.Count))
                {
                    parseArgumentsBySourceFile[sourceFilePath] = parseArguments;
                }
            }

            diagnostics.Add(T("diag.cpp.compileCommandsLoaded", compileCommandsPath));
            return new CompileCommandData(
                includeDirectories.ToArray(),
                parseArgumentsBySourceFile,
                compileCommandsPath);
        }
        catch (Exception ex)
        {
            diagnostics.Add(T("diag.cpp.compileCommandsFailed", ex.Message));
            return CompileCommandData.Empty;
        }
    }

    private static void AppendUniqueDirectories(List<string> includeDirectories, IEnumerable<string> candidates)
    {
        HashSet<string> seen = includeDirectories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!Directory.Exists(normalizedPath) || !seen.Add(normalizedPath))
            {
                continue;
            }

            includeDirectories.Add(normalizedPath);
        }
    }

    private static string ResolveCompileCommandWorkingDirectory(JsonElement entry, string fallbackDirectory)
    {
        string candidate = fallbackDirectory;
        if (entry.TryGetProperty("directory", out JsonElement directoryElement) &&
            directoryElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(directoryElement.GetString()))
        {
            candidate = directoryElement.GetString()!;
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch //Argument, ArgumentNull, PathTooLong, Security, NotSupported Exceptions
        {
            return fallbackDirectory;
        }
    }

    private static string? ResolveCompileCommandSourceFilePath(
        JsonElement entry,
        IReadOnlyList<string> tokens,
        string workingDirectory)
    {
        if (entry.TryGetProperty("file", out JsonElement fileElement) &&
            fileElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(fileElement.GetString()))
        {
            string? resolvedFilePath = ResolvePath(workingDirectory, fileElement.GetString()!);
            if (resolvedFilePath is not null)
            {
                return resolvedFilePath;
            }
        }

        for (int index = tokens.Count - 1; index >= 0; index--)
        {
            string token = tokens[index];
            if (!TryResolveCompileCommandPath(token, workingDirectory, out string? candidatePath))
            {
                continue;
            }

            if (IsCppFile(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static bool IsRelevantCompileCommandEntry(
        string? sourceFilePath,
        string projectDirectory,
        IReadOnlySet<string> projectFiles)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return true;
        }

        return
            projectFiles.Contains(sourceFilePath) ||
            IsPathUnderRoot(sourceFilePath, projectDirectory);
    }

    private static IReadOnlyList<string> BuildParseArgumentsFromCompileCommand(
        IReadOnlyList<string> tokens,
        string workingDirectory,
        string? sourceFilePath,
        List<string> includeDirectories,
        HashSet<string> seenIncludeDirectories)
    {
        List<string> parseArguments =
        [
            $"-std={ResolveDefaultLanguageStandard(sourceFilePath)}",
            "-x",
            ResolveDefaultLanguage(sourceFilePath)
        ];

        for (int index = GetCompileCommandOptionStartIndex(tokens); index < tokens.Count; index++)
        {
            string token = tokens[index];
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (TrySkipCompileCommandToken(tokens, ref index, workingDirectory, sourceFilePath))
            {
                continue;
            }

            if (TryReadCompileCommandIncludeArgument(tokens, ref index, workingDirectory, out string includeOption, out string? includePath))
            {
                if (!string.IsNullOrWhiteSpace(includePath))
                {
                    parseArguments.Add(includeOption);
                    parseArguments.Add(includePath);

                    if (TryNormalizeAbsolutePath(includePath, out string normalizedIncludePath) &&
                        seenIncludeDirectories.Add(normalizedIncludePath))
                    {
                        includeDirectories.Add(normalizedIncludePath);
                    }
                }

                continue;
            }

            if (TryReadCompileCommandMacroArgument(tokens, ref index, out string? macroArgument))
            {
                if (!string.IsNullOrWhiteSpace(macroArgument))
                {
                    parseArguments.Add(macroArgument);
                }

                continue;
            }

            if (TryReadCompileCommandForcedIncludeArgument(tokens, ref index, workingDirectory, out string? forcedIncludePath))
            {
                if (!string.IsNullOrWhiteSpace(forcedIncludePath))
                {
                    parseArguments.Add("-include");
                    parseArguments.Add(forcedIncludePath);
                }

                continue;
            }

            if (TryReadCompileCommandLanguageArgument(tokens, ref index, sourceFilePath, workingDirectory, out string? language))
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    parseArguments.Add("-x");
                    parseArguments.Add(language);
                }

                continue;
            }

            if (TryReadCompileCommandLanguageStandardArgument(tokens, ref index, out string? languageStandard))
            {
                if (!string.IsNullOrWhiteSpace(languageStandard))
                {
                    parseArguments.Add($"-std={languageStandard}");
                }

                continue;
            }

            if (TryReadCompileCommandTargetArgument(tokens, ref index, workingDirectory, out string? targetArgument))
            {
                if (!string.IsNullOrWhiteSpace(targetArgument))
                {
                    parseArguments.Add(targetArgument);
                }

                continue;
            }

            if (TryReadClangCompatiblePassthrough(token, out string? passthroughArgument))
            {
                parseArguments.Add(passthroughArgument);
            }
        }

        return parseArguments
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToArray();
    }

    private static int GetCompileCommandOptionStartIndex(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0;
        }

        string firstToken = tokens[0];
        return IsCompilerExecutableToken(firstToken) ? 1 : 0;
    }

    private static bool IsCompilerExecutableToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string candidate = token.Trim();
        if (candidate.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        string fileName = Path.GetFileName(candidate).ToLowerInvariant();
        if (fileName is "cl" or "cl.exe" or "clang" or "clang.exe" or "clang++" or "clang++.exe" or "gcc" or "g++")
        {
            return true;
        }

        return fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySkipCompileCommandToken(
        IReadOnlyList<string> tokens,
        ref int index,
        string workingDirectory,
        string? sourceFilePath)
    {
        string token = tokens[index];
        if (token.StartsWith("/Tc", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("/Tp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(token, "-c", StringComparison.Ordinal) ||
            string.Equals(token, "/c", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "-Winvalid-pch", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(token, "-o", StringComparison.Ordinal) ||
            string.Equals(token, "/Fo", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "/Fd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "/Fp", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "/Fa", StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 < tokens.Count)
            {
                index++;
            }

            return true;
        }

        if (token.StartsWith("-o", StringComparison.Ordinal) ||
            token.StartsWith("/Fo", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("/Fd", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("/Fp", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("/Fa", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryResolveCompileCommandPath(token, workingDirectory, out string? candidatePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sourceFilePath) &&
            string.Equals(candidatePath, sourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsCppFile(candidatePath);
    }

    private static bool TryReadCompileCommandIncludeArgument(
        IReadOnlyList<string> tokens,
        ref int index,
        string workingDirectory,
        out string includeOption,
        out string? includePath)
    {
        includeOption = "-I";
        includePath = null;

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-I", StringComparison.Ordinal, out string? includeDirectory) ||
            TryReadCompileCommandOptionValue(tokens, ref index, "/I", StringComparison.OrdinalIgnoreCase, out includeDirectory))
        {
            includeOption = "-I";
            includePath = ResolveCompileCommandPath(includeDirectory, workingDirectory);
            return true;
        }

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-isystem", StringComparison.Ordinal, out includeDirectory) ||
            TryReadCompileCommandOptionValue(tokens, ref index, "-imsvc", StringComparison.Ordinal, out includeDirectory) ||
            TryReadCompileCommandOptionValue(tokens, ref index, "/external:I", StringComparison.OrdinalIgnoreCase, out includeDirectory))
        {
            includeOption = "-isystem";
            includePath = ResolveCompileCommandPath(includeDirectory, workingDirectory);
            return true;
        }

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-iquote", StringComparison.Ordinal, out includeDirectory))
        {
            includeOption = "-iquote";
            includePath = ResolveCompileCommandPath(includeDirectory, workingDirectory);
            return true;
        }

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-idirafter", StringComparison.Ordinal, out includeDirectory))
        {
            includeOption = "-idirafter";
            includePath = ResolveCompileCommandPath(includeDirectory, workingDirectory);
            return true;
        }

        return false;
    }

    private static bool TryReadCompileCommandMacroArgument(
        IReadOnlyList<string> tokens,
        ref int index,
        out string? macroArgument)
    {
        macroArgument = null;

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-D", StringComparison.Ordinal, out string? definition) ||
            TryReadCompileCommandOptionValue(tokens, ref index, "/D", StringComparison.OrdinalIgnoreCase, out definition))
        {
            if (!string.IsNullOrWhiteSpace(definition))
            {
                macroArgument = $"-D{definition}";
            }

            return true;
        }

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-U", StringComparison.Ordinal, out string? undefinedSymbol) ||
            TryReadCompileCommandOptionValue(tokens, ref index, "/U", StringComparison.OrdinalIgnoreCase, out undefinedSymbol))
        {
            if (!string.IsNullOrWhiteSpace(undefinedSymbol))
            {
                macroArgument = $"-U{undefinedSymbol}";
            }

            return true;
        }

        return false;
    }

    private static bool TryReadCompileCommandForcedIncludeArgument(
        IReadOnlyList<string> tokens,
        ref int index,
        string workingDirectory,
        out string? forcedIncludePath)
    {
        forcedIncludePath = null;
        if (!TryReadCompileCommandOptionValue(tokens, ref index, "-include", StringComparison.Ordinal, out string? includePath) &&
            !TryReadCompileCommandOptionValue(tokens, ref index, "/FI", StringComparison.OrdinalIgnoreCase, out includePath))
        {
            return false;
        }

        forcedIncludePath = ResolveCompileCommandPath(includePath, workingDirectory);
        return true;
    }

    private static bool TryReadCompileCommandLanguageArgument(
        IReadOnlyList<string> tokens,
        ref int index,
        string? sourceFilePath,
        string workingDirectory,
        out string? language)
    {
        language = null;
        string token = tokens[index];

        if (TryReadCompileCommandOptionValue(tokens, ref index, "-x", StringComparison.Ordinal, out string? languageValue))
        {
            language = NormalizeCompileCommandLanguage(languageValue);
            return true;
        }

        if (string.Equals(token, "/TP", StringComparison.OrdinalIgnoreCase))
        {
            language = "c++";
            return true;
        }

        if (string.Equals(token, "/TC", StringComparison.OrdinalIgnoreCase))
        {
            language = "c";
            return true;
        }

        if (token.StartsWith("/Tp", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
        {
            if (TryResolveCompileCommandPath(token[3..], workingDirectory, out string? compileAsSource) &&
                (string.IsNullOrWhiteSpace(sourceFilePath) ||
                    string.Equals(compileAsSource, sourceFilePath, StringComparison.OrdinalIgnoreCase)))
            {
                language = "c++";
            }

            return true;
        }

        if (token.StartsWith("/Tc", StringComparison.OrdinalIgnoreCase) && token.Length > 3)
        {
            if (TryResolveCompileCommandPath(token[3..], workingDirectory, out string? compileAsSource) &&
                (string.IsNullOrWhiteSpace(sourceFilePath) ||
                    string.Equals(compileAsSource, sourceFilePath, StringComparison.OrdinalIgnoreCase)))
            {
                language = "c";
            }

            return true;
        }

        return false;
    }

    private static bool TryReadCompileCommandLanguageStandardArgument(
        IReadOnlyList<string> tokens,
        ref int index,
        out string? languageStandard)
    {
        languageStandard = null;
        string token = tokens[index];

        string? rawStandard = null;
        if (string.Equals(token, "-std", StringComparison.Ordinal) && index + 1 < tokens.Count)
        {
            rawStandard = tokens[++index];
        }
        else if (token.StartsWith("-std=", StringComparison.Ordinal) && token.Length > "-std=".Length)
        {
            rawStandard = token["-std=".Length..];
        }
        else if (token.StartsWith("/std:", StringComparison.OrdinalIgnoreCase) && token.Length > "/std:".Length)
        {
            rawStandard = token["/std:".Length..];
        }

        if (rawStandard is null)
        {
            return false;
        }

        if (TryResolveCompileCommandLanguageStandard(rawStandard, out string resolvedStandard))
        {
            languageStandard = resolvedStandard;
        }

        return true;
    }

    private static bool TryReadCompileCommandTargetArgument(
        IReadOnlyList<string> tokens,
        ref int index,
        string workingDirectory,
        out string? targetArgument)
    {
        targetArgument = null;
        string token = tokens[index];

        if (string.Equals(token, "-target", StringComparison.Ordinal) && index + 1 < tokens.Count)
        {
            targetArgument = $"--target={tokens[++index]}";
            return true;
        }

        if (token.StartsWith("--target=", StringComparison.Ordinal) ||
            token.StartsWith("-target=", StringComparison.Ordinal))
        {
            targetArgument = token.StartsWith("--target=", StringComparison.Ordinal)
                ? token
                : $"--target={token["-target=".Length..]}";
            return true;
        }

        if (string.Equals(token, "--target", StringComparison.Ordinal) && index + 1 < tokens.Count)
        {
            targetArgument = $"--target={tokens[++index]}";
            return true;
        }

        if (string.Equals(token, "--sysroot", StringComparison.Ordinal) && index + 1 < tokens.Count)
        {
            targetArgument = $"--sysroot={ResolveCompileCommandPath(tokens[++index], workingDirectory)}";
            return true;
        }

        if (token.StartsWith("--sysroot=", StringComparison.Ordinal))
        {
            targetArgument = $"--sysroot={ResolveCompileCommandPath(token["--sysroot=".Length..], workingDirectory)}";
            return true;
        }

        if (string.Equals(token, "-isysroot", StringComparison.Ordinal) && index + 1 < tokens.Count)
        {
            targetArgument = $"--sysroot={ResolveCompileCommandPath(tokens[++index], workingDirectory)}";
            return true;
        }

        return false;
    }

    private static bool TryReadClangCompatiblePassthrough(string token, out string passthroughArgument)
    {
        passthroughArgument = string.Empty;

        if (string.Equals(token, "-nostdinc", StringComparison.Ordinal) ||
            string.Equals(token, "-nostdinc++", StringComparison.Ordinal) ||
            string.Equals(token, "-fdeclspec", StringComparison.Ordinal) ||
            string.Equals(token, "-fdelayed-template-parsing", StringComparison.Ordinal) ||
            token.StartsWith("-fms-", StringComparison.Ordinal))
        {
            passthroughArgument = token;
            return true;
        }

        return false;
    }

    private static bool TryReadCompileCommandOptionValue(
        IReadOnlyList<string> tokens,
        ref int index,
        string option,
        StringComparison comparison,
        out string? value)
    {
        value = null;
        string token = tokens[index];
        if (string.Equals(token, option, comparison))
        {
            if (index + 1 < tokens.Count)
            {
                value = tokens[++index];
            }

            return true;
        }

        if (token.StartsWith(option, comparison) && token.Length > option.Length)
        {
            value = token[option.Length..];
            return true;
        }

        return false;
    }

    private static string? NormalizeCompileCommandLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        string normalized = language.Trim().ToLowerInvariant();
        if (normalized.Contains("c++", StringComparison.Ordinal))
        {
            return normalized.Contains("header", StringComparison.Ordinal)
                ? "c++-header"
                : "c++";
        }

        if (normalized.Contains("objective-c", StringComparison.Ordinal))
        {
            return normalized.Contains("++", StringComparison.Ordinal)
                ? "objective-c++"
                : "objective-c";
        }

        if (normalized.Contains("header", StringComparison.Ordinal))
        {
            return "c-header";
        }

        return normalized.Contains('c')
            ? "c"
            : null;
    }

    private static bool TryResolveCompileCommandLanguageStandard(string rawStandard, out string resolvedStandard)
    {
        string normalized = rawStandard.Trim().ToLowerInvariant();
        resolvedStandard = normalized switch
        {
            "c11" or "gnu11" => "c11",
            "c17" or "gnu17" or "c18" or "gnu18" => "c17",
            "c23" or "gnu23" or "c2x" or "gnu2x" => "c23",
            "c++14" or "gnu++14" => "c++14",
            "c++17" or "gnu++17" => "c++17",
            "c++20" or "gnu++20" => "c++20",
            "c++23" or "gnu++23" or "c++2b" or "gnu++2b" or "c++latest" => "c++23",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(resolvedStandard);
    }

    private static string ResolveDefaultLanguage(string? sourceFilePath)
    {
        string extension = Path.GetExtension(sourceFilePath ?? string.Empty);
        return extension switch
        {
            ".c" => "c",
            ".h" => "c++-header",
            _ => "c++"
        };
    }

    private static string ResolveDefaultLanguageStandard(string? sourceFilePath)
    {
        string extension = Path.GetExtension(sourceFilePath ?? string.Empty);
        return string.Equals(extension, ".c", StringComparison.OrdinalIgnoreCase)
            ? "c17"
            : "c++20";
    }

    private static string ResolveCompileCommandPath(string? value, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string candidate = value.Trim().Trim('"');
        if (candidate.Length == 0)
        {
            return string.Empty;
        }

        string? resolved = ResolvePath(workingDirectory, candidate);
        return resolved ?? candidate;
    }

    private static bool TryResolveCompileCommandPath(string value, string workingDirectory, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string candidate = value.Trim().Trim('"');
        if (candidate.Length == 0)
        {
            return false;
        }

        if (candidate.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        if (candidate.StartsWith("/", StringComparison.Ordinal) &&
            candidate.Length > 1 &&
            char.IsLetter(candidate[1]) &&
            (candidate.Length == 2 || char.IsLetter(candidate[2]) || candidate[2] == ':'))
        {
            return false;
        }

        string? resolved = ResolvePath(workingDirectory, candidate);
        if (resolved is null)
        {
            return false;
        }

        resolvedPath = resolved;
        return true;
    }

    private static bool TryNormalizeAbsolutePath(string value, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ReadCommandTokens(JsonElement entry)
    {
        if (entry.TryGetProperty("arguments", out JsonElement argumentsElement) && argumentsElement.ValueKind == JsonValueKind.Array)
        {
            return argumentsElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();
        }

        if (!entry.TryGetProperty("command", out JsonElement commandElement) || commandElement.ValueKind != JsonValueKind.String)
        {
            return Array.Empty<string>();
        }

        string? command = commandElement.GetString();
        return string.IsNullOrWhiteSpace(command)
            ? Array.Empty<string>()
            : TokenizeCommandLine(command);
    }

    private static IReadOnlyList<IncludeDirective> ReadIncludeDirectives(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> rawLines)
    {
        List<IncludeDirective> directives = [];
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string line = lines[lineIndex];
            Match match = s_includePattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string includeTarget = match.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(includeTarget))
            {
                continue;
            }

            directives.Add(new IncludeDirective(
                includeTarget,
                string.Equals(match.Groups[1].Value, "\"", StringComparison.Ordinal),
                lineIndex + 1,
                ExtractLineSnippet(rawLines, lineIndex + 1)));
        }

        return directives;
    }

    private static IReadOnlyList<CppDeclaredSymbol> ExtractDeclaredSymbols(
        DocumentDescriptor sourceDocument,
        string projectIdentity,
        IReadOnlyList<string> lines)
    {
        List<CppDeclaredSymbol> declaredSymbols = [];
        List<CppScopeFrame> scopeStack = [];
        CppPendingScope? pendingScope = null;
        int braceDepth = 0;

        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            string line = lines[lineIndex];
            int lineNumber = lineIndex + 1;
            if (string.IsNullOrWhiteSpace(line))
            {
                UpdateScopeState(line, scopeStack, ref pendingScope, ref braceDepth);
                continue;
            }

            string trimmed = line.Trim();
            if (trimmed.StartsWith("typedef", StringComparison.Ordinal))
            {
                UpdateScopeState(line, scopeStack, ref pendingScope, ref braceDepth);
                continue;
            }

            string currentScopePath = GetScopePath(scopeStack, includeTypes: true);
            string currentNamespaceScopePath = GetScopePath(scopeStack, includeTypes: false);
            string currentTypeScopePath = GetTypeScopePath(scopeStack);

            Match namespaceMatch = s_namespaceDeclarationPattern.Match(line);
            if (namespaceMatch.Success)
            {
                pendingScope = new CppPendingScope(
                    BuildScopeSegments(namespaceMatch.Groups["name"].Value, "namespace"));
            }

            Match macroMatch = s_macroDefinitionPattern.Match(line);
            if (macroMatch.Success)
            {
                AddSymbol(
                    declaredSymbols,
                    sourceDocument,
                    projectIdentity,
                    lineNumber,
                    "MacroDefinition",
                    macroMatch.Groups["name"].Value,
                    macroMatch.Groups["name"].Value,
                    isDefinition: false,
                    namespaceScopePath: currentNamespaceScopePath);
                UpdateScopeState(line, scopeStack, ref pendingScope, ref braceDepth);
                continue;
            }

            Match usingMatch = s_usingAliasPattern.Match(line);
            if (usingMatch.Success)
            {
                string name = usingMatch.Groups["name"].Value;
                string qualifiedAliasName = QualifyName(currentScopePath, name);
                AddSymbol(
                    declaredSymbols,
                    sourceDocument,
                    projectIdentity,
                    lineNumber,
                    "TypeAliasDeclaration",
                    name,
                    qualifiedAliasName,
                    isDefinition: false,
                    namespaceScopePath: currentNamespaceScopePath);
            }

            Match typeMatch = s_typeDeclarationPattern.Match(line);
            if (typeMatch.Success)
            {
                string declaredName = typeMatch.Groups["name"].Value;
                string qualifiedDeclaredName = QualifyName(currentScopePath, declaredName);
                string kindToken = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
                string typeKind = kindToken switch
                {
                    "class" => "ClassDeclaration",
                    "struct" => "StructDeclaration",
                    "union" => "UnionDeclaration",
                    _ => "EnumDeclaration"
                };

                AddSymbol(
                    declaredSymbols,
                    sourceDocument,
                    projectIdentity,
                    lineNumber,
                    typeKind,
                    declaredName,
                    qualifiedDeclaredName,
                    isDefinition: false,
                    namespaceScopePath: currentNamespaceScopePath);

                if (CanOpenScope(trimmed))
                {
                    pendingScope = new CppPendingScope(
                        [new CppScopeSegment(declaredName, "type")]);
                }
            }

            Match functionMatch = s_functionPattern.Match(line);
            if (functionMatch.Success)
            {
                string rawQualifiedName = functionMatch.Groups["name"].Value;
                if (!string.IsNullOrWhiteSpace(rawQualifiedName))
                {
                    string functionName = ExtractSimpleName(rawQualifiedName);
                    if (!string.IsNullOrWhiteSpace(functionName) && !s_disallowedFunctionNames.Contains(functionName))
                    {
                        string qualifiedFunctionName = rawQualifiedName.Contains("::", StringComparison.Ordinal)
                            ? QualifyName(currentNamespaceScopePath, rawQualifiedName)
                            : QualifyName(currentScopePath, rawQualifiedName);
                        string functionKind = DetermineFunctionKind(rawQualifiedName, currentTypeScopePath);
                        bool isDefinition = string.Equals(functionMatch.Groups["terminator"].Value, "{", StringComparison.Ordinal);

                        AddSymbol(
                            declaredSymbols,
                            sourceDocument,
                            projectIdentity,
                            lineNumber,
                            functionKind,
                            functionName,
                            qualifiedFunctionName,
                            isDefinition,
                            namespaceScopePath: currentNamespaceScopePath);
                    }
                }
            }

            UpdateScopeState(line, scopeStack, ref pendingScope, ref braceDepth);
        }

        return declaredSymbols
            .OrderBy(symbol => symbol.Summary.LineNumber)
            .ThenBy(symbol => symbol.QualifiedName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CppDeclaredSymbol> ExtractDeclaredSymbolsFromAst(
        DocumentDescriptor sourceDocument,
        string projectIdentity,
        IReadOnlyList<LibClangSymbolObservation> symbols)
    {
        List<CppDeclaredSymbol> declaredSymbols = [];
        HashSet<string> seenSymbols = new(StringComparer.Ordinal);
        foreach (LibClangSymbolObservation symbol in symbols)
        {
            if (string.IsNullOrWhiteSpace(symbol.Name))
            {
                continue;
            }

            int lineNumber = symbol.LineNumber;
            if (lineNumber <= 0)
            {
                continue;
            }

            string displayName = string.IsNullOrWhiteSpace(symbol.QualifiedName)
                ? symbol.Name
                : symbol.QualifiedName;
            string dedupeKey = $"{lineNumber}|{symbol.Kind}|{displayName}|{symbol.Usr}";
            if (!seenSymbols.Add(dedupeKey))
            {
                continue;
            }

            AddSymbol(
                declaredSymbols,
                sourceDocument,
                projectIdentity,
                lineNumber,
                symbol.Kind,
                symbol.Name,
                displayName,
                symbol.IsDefinition,
                symbol.NamespaceScopePath,
                symbol.Usr);
        }

        return declaredSymbols
            .OrderBy(symbol => symbol.Summary.LineNumber)
            .ThenBy(symbol => symbol.QualifiedName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<AstReferenceObservation> ConvertAstReferences(
        IReadOnlyList<LibClangReferenceObservation> references,
        string sourceFilePath,
        IReadOnlyList<string> rawLines)
    {
        if (references.Count == 0)
        {
            return Array.Empty<AstReferenceObservation>();
        }

        List<AstReferenceObservation> observations = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (LibClangReferenceObservation reference in references)
        {
            if (reference.LineNumber <= 0)
            {
                continue;
            }

            string targetName = string.IsNullOrWhiteSpace(reference.TargetName)
                ? string.Empty
                : reference.TargetName.Trim();
            string normalizedKind = NormalizeAstReferenceKind(reference.ReferenceKind);
            string dedupeKey = $"{reference.SourceUsr}|{reference.TargetUsr}|{targetName}|{reference.LineNumber}|{normalizedKind}";
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            observations.Add(new AstReferenceObservation(
                reference.SourceUsr,
                reference.TargetUsr,
                targetName,
                reference.LineNumber,
                normalizedKind,
                sourceFilePath,
                ExtractLineSnippet(rawLines, reference.LineNumber)));
        }

        return observations;
    }

    private static void AddSymbol(
        ICollection<CppDeclaredSymbol> symbols,
        DocumentDescriptor sourceDocument,
        string projectIdentity,
        int lineNumber,
        string kind,
        string name,
        string displayName,
        bool isDefinition,
        string namespaceScopePath,
        string? astUsr = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string symbolKey = $"cpp|{projectIdentity}|{sourceDocument.FilePath}|{lineNumber}|{kind}|{displayName}";
        string symbolId = BuildSymbolId(name, symbolKey);

        SymbolAnalysisSummary summary = new(
            symbolId,
            kind,
            name,
            displayName,
            lineNumber);

        symbols.Add(new CppDeclaredSymbol(
            symbolKey,
            sourceDocument.DocumentId,
            summary,
            isDefinition,
            displayName,
            GetContainingScopePath(displayName),
            namespaceScopePath,
            astUsr));
    }

    private static CppSymbolLookup BuildSymbolLookup(
        IReadOnlyList<CppDeclaredSymbol> symbols)
    {
        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> bySimpleName = symbols
            .GroupBy(symbol => symbol.Summary.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CppDeclaredSymbol>)group
                    .OrderBy(symbol => symbol.DocumentId, StringComparer.Ordinal)
                    .ThenBy(symbol => symbol.Summary.LineNumber)
                    .ToArray(),
                StringComparer.Ordinal);

        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> byQualifiedName = symbols
            .GroupBy(symbol => symbol.QualifiedName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CppDeclaredSymbol>)group
                    .OrderBy(symbol => symbol.DocumentId, StringComparer.Ordinal)
                    .ThenBy(symbol => symbol.Summary.LineNumber)
                    .ToArray(),
                StringComparer.Ordinal);

        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> byAstUsr = symbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol.AstUsr))
            .GroupBy(symbol => symbol.AstUsr!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CppDeclaredSymbol>)group
                    .OrderBy(symbol => symbol.DocumentId, StringComparer.Ordinal)
                    .ThenBy(symbol => symbol.Summary.LineNumber)
                    .ToArray(),
                StringComparer.Ordinal);

        return new CppSymbolLookup(bySimpleName, byQualifiedName, byAstUsr);
    }

    private static void ExtractAstSymbolDependencies(
        DocumentParseContext parseContext,
        CppSymbolLookup symbolLookup,
        IDictionary<(string Source, string Target), int> symbolDependencyCounts,
        IDictionary<(string Source, string Target), Dictionary<string, int>> symbolDependencyKinds,
        IDictionary<(string Source, string Target), AnalysisDependencySample> symbolDependencySamples)
    {
        if (parseContext.AstReferences.Count == 0 || parseContext.DeclaredSymbols.Count == 0)
        {
            return;
        }

        IReadOnlyList<CppDeclaredSymbol> sourceSymbols = parseContext.DeclaredSymbols
            .Where(symbol => symbol.IsDefinition && IsSourceSymbolKind(symbol.Summary.Kind))
            .OrderBy(symbol => symbol.Summary.LineNumber)
            .ToArray();
        IReadOnlyList<CppSourceSymbolRange> sourceSymbolRanges = BuildSourceSymbolRanges(
            sourceSymbols,
            parseContext.CodeLines.Count,
            parseContext.CodeLines);

        foreach (AstReferenceObservation reference in parseContext.AstReferences)
        {
            CppDeclaredSymbol? sourceSymbol = ResolveAstSourceSymbol(
                parseContext,
                symbolLookup,
                sourceSymbolRanges,
                reference);
            if (sourceSymbol is null)
            {
                continue;
            }

            CppDeclaredSymbol? targetSymbol = ResolveAstTargetSymbol(
                sourceSymbol,
                parseContext.Document.DocumentId,
                symbolLookup,
                reference);
            if (targetSymbol is null ||
                string.Equals(sourceSymbol.Summary.Id, targetSymbol.Summary.Id, StringComparison.Ordinal))
            {
                continue;
            }

            (string Source, string Target) dependencyKey = (sourceSymbol.Summary.Id, targetSymbol.Summary.Id);
            IncrementCount(symbolDependencyCounts, dependencyKey);
            IncrementReferenceKind(
                symbolDependencyKinds,
                dependencyKey,
                reference.ReferenceKind);
            TrySetDependencySample(
                symbolDependencySamples,
                dependencyKey,
                reference.SampleFilePath,
                reference.LineNumber,
                reference.SampleSnippet);
        }
    }

    private static CppDeclaredSymbol? ResolveAstSourceSymbol(
        DocumentParseContext parseContext,
        CppSymbolLookup symbolLookup,
        IReadOnlyList<CppSourceSymbolRange> sourceSymbolRanges,
        AstReferenceObservation reference)
    {
        CppDeclaredSymbol? sourceByUsr = ResolveByAstUsr(
            symbolLookup.ByAstUsr,
            reference.SourceUsr,
            parseContext.Document.DocumentId,
            sourceSymbolKeyToExclude: null,
            reference.LineNumber);
        if (sourceByUsr is not null && IsDeclarationDependencySourceSymbolKind(sourceByUsr))
        {
            return sourceByUsr;
        }

        CppSourceSymbolRange? sourceRange = ResolveSourceSymbol(sourceSymbolRanges, reference.LineNumber);
        if (sourceRange is not null)
        {
            return sourceRange.Symbol;
        }

        return parseContext.DeclaredSymbols
            .Where(IsDeclarationDependencySourceSymbolKind)
            .Where(symbol => symbol.Summary.LineNumber <= reference.LineNumber)
            .OrderByDescending(symbol => symbol.Summary.LineNumber)
            .FirstOrDefault();
    }

    private static CppDeclaredSymbol? ResolveAstTargetSymbol(
        CppDeclaredSymbol sourceSymbol,
        string sourceDocumentId,
        CppSymbolLookup symbolLookup,
        AstReferenceObservation reference)
    {
        CppDeclaredSymbol? targetByUsr = ResolveByAstUsr(
            symbolLookup.ByAstUsr,
            reference.TargetUsr,
            sourceDocumentId,
            sourceSymbol.Key,
            reference.LineNumber);
        if (targetByUsr is not null)
        {
            return targetByUsr;
        }

        if (string.IsNullOrWhiteSpace(reference.TargetName))
        {
            return null;
        }

        return ResolveTargetSymbol(
            reference.TargetName,
            sourceSymbol,
            sourceDocumentId,
            reference.LineNumber,
            symbolLookup);
    }

    private static CppDeclaredSymbol? ResolveByAstUsr(
        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> byAstUsr,
        string? astUsr,
        string sourceDocumentId,
        string? sourceSymbolKeyToExclude,
        int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(astUsr) || !byAstUsr.TryGetValue(astUsr, out IReadOnlyList<CppDeclaredSymbol>? candidates))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(sourceSymbolKeyToExclude))
        {
            return candidates
                .Where(candidate => string.Equals(candidate.DocumentId, sourceDocumentId, StringComparison.Ordinal))
                .OrderByDescending(candidate => candidate.Summary.LineNumber <= lineNumber)
                .ThenByDescending(candidate => candidate.Summary.LineNumber)
                .FirstOrDefault()
                ?? candidates.FirstOrDefault();
        }

        return ResolvePreferredTargetSymbol(candidates, sourceDocumentId, sourceSymbolKeyToExclude, lineNumber);
    }

    private static string NormalizeAstReferenceKind(string? referenceKind)
    {
        if (string.Equals(referenceKind, "call", StringComparison.OrdinalIgnoreCase))
        {
            return "call";
        }

        if (string.Equals(referenceKind, "inheritance", StringComparison.OrdinalIgnoreCase))
        {
            return "inheritance";
        }

        if (string.Equals(referenceKind, "parameter", StringComparison.OrdinalIgnoreCase))
        {
            return "parameter";
        }

        if (string.Equals(referenceKind, "field", StringComparison.OrdinalIgnoreCase))
        {
            return "field";
        }

        if (string.Equals(referenceKind, "return", StringComparison.OrdinalIgnoreCase))
        {
            return "return";
        }

        return "reference";
    }

    private static string? ExtractLineSnippet(IReadOnlyList<string> lines, int lineNumber)
    {
        return DependencySampleHelper.ExtractLineSnippet(lines, lineNumber);
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

    private static void ExtractSymbolDependencies(
        DocumentParseContext parseContext,
        CppSymbolLookup symbolLookup,
        IDictionary<(string Source, string Target), int> symbolDependencyCounts,
        IDictionary<(string Source, string Target), Dictionary<string, int>> symbolDependencyKinds,
        IDictionary<(string Source, string Target), AnalysisDependencySample> symbolDependencySamples)
    {
        IReadOnlyList<CppDeclaredSymbol> sourceSymbols = parseContext.DeclaredSymbols
            .Where(symbol => symbol.IsDefinition && IsSourceSymbolKind(symbol.Summary.Kind))
            .OrderBy(symbol => symbol.Summary.LineNumber)
            .ToArray();

        if (sourceSymbols.Count == 0)
        {
            return;
        }

        IReadOnlyList<CppSourceSymbolRange> sourceSymbolRanges = BuildSourceSymbolRanges(
            sourceSymbols,
            parseContext.CodeLines.Count,
            parseContext.CodeLines);

        Dictionary<int, HashSet<string>> declarationNamesByLine = parseContext.DeclaredSymbols
            .GroupBy(symbol => symbol.Summary.LineNumber)
            .ToDictionary(
                group => group.Key,
                group => group.Select(symbol => symbol.Summary.Name).ToHashSet(StringComparer.Ordinal));

        for (int lineIndex = 0; lineIndex < parseContext.CodeLines.Count; lineIndex++)
        {
            int lineNumber = lineIndex + 1;
            string line = parseContext.CodeLines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            CppSourceSymbolRange? sourceRange = ResolveSourceSymbol(sourceSymbolRanges, lineNumber);
            if (sourceRange is null)
            {
                continue;
            }

            CppDeclaredSymbol sourceSymbol = sourceRange.Symbol;

            declarationNamesByLine.TryGetValue(lineNumber, out HashSet<string>? declarationNames);
            foreach (Match match in s_referenceTokenPattern.Matches(line))
            {
                string token = match.Groups["name"].Value;
                string simpleName = ExtractSimpleName(token);
                if (string.IsNullOrWhiteSpace(simpleName) || s_cppKeywords.Contains(simpleName))
                {
                    continue;
                }

                if (declarationNames is not null && declarationNames.Contains(simpleName))
                {
                    continue;
                }

                CppDeclaredSymbol? targetSymbol = ResolveTargetSymbol(
                    token,
                    sourceSymbol,
                    parseContext.Document.DocumentId,
                    lineNumber,
                    symbolLookup);
                if (targetSymbol is null)
                {
                    continue;
                }

                if (string.Equals(sourceSymbol.Summary.Id, targetSymbol.Summary.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                (string Source, string Target) dependencyKey = (sourceSymbol.Summary.Id, targetSymbol.Summary.Id);
                IncrementCount(symbolDependencyCounts, dependencyKey);
                IncrementReferenceKind(
                    symbolDependencyKinds,
                    dependencyKey,
                    ResolveReferenceKind(line, match.Index, token.Length));
                TrySetDependencySample(
                    symbolDependencySamples,
                    dependencyKey,
                    parseContext.Document.FilePath,
                    lineNumber,
                    ExtractLineSnippet(parseContext.RawLines, lineNumber));
            }
        }
    }

    private static void ExtractDeclarationDependencies(
        DocumentParseContext parseContext,
        CppSymbolLookup symbolLookup,
        IDictionary<(string Source, string Target), int> symbolDependencyCounts,
        IDictionary<(string Source, string Target), Dictionary<string, int>> symbolDependencyKinds,
        IDictionary<(string Source, string Target), AnalysisDependencySample> symbolDependencySamples)
    {
        Dictionary<int, HashSet<string>> declarationNamesByLine = parseContext.DeclaredSymbols
            .GroupBy(symbol => symbol.Summary.LineNumber)
            .ToDictionary(
                group => group.Key,
                group => group.Select(symbol => symbol.Summary.Name).ToHashSet(StringComparer.Ordinal));

        foreach (CppDeclaredSymbol sourceSymbol in parseContext.DeclaredSymbols.Where(IsDeclarationDependencySourceSymbolKind))
        {
            int lineNumber = sourceSymbol.Summary.LineNumber;
            string declarationText = BuildDeclarationText(parseContext.CodeLines, lineNumber);
            if (string.IsNullOrWhiteSpace(declarationText))
            {
                continue;
            }

            declarationNamesByLine.TryGetValue(lineNumber, out HashSet<string>? declarationNames);
            foreach (Match match in s_referenceTokenPattern.Matches(declarationText))
            {
                string token = match.Groups["name"].Value;
                string simpleName = ExtractSimpleName(token);
                if (string.IsNullOrWhiteSpace(simpleName) ||
                    s_cppKeywords.Contains(simpleName) ||
                    string.Equals(simpleName, sourceSymbol.Summary.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (declarationNames is not null && declarationNames.Contains(simpleName))
                {
                    continue;
                }

                CppDeclaredSymbol? targetSymbol = ResolveTargetSymbol(
                    token,
                    sourceSymbol,
                    parseContext.Document.DocumentId,
                    lineNumber,
                    symbolLookup);
                if (targetSymbol is null ||
                    string.Equals(sourceSymbol.Summary.Id, targetSymbol.Summary.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                (string Source, string Target) dependencyKey = (sourceSymbol.Summary.Id, targetSymbol.Summary.Id);
                IncrementCount(symbolDependencyCounts, dependencyKey);
                IncrementReferenceKind(
                    symbolDependencyKinds,
                    dependencyKey,
                    ResolveDeclarationReferenceKind(declarationText, sourceSymbol.Summary.Kind, match.Index));
                TrySetDependencySample(
                    symbolDependencySamples,
                    dependencyKey,
                    parseContext.Document.FilePath,
                    lineNumber,
                    ExtractLineSnippet(parseContext.RawLines, lineNumber));
            }
        }
    }

    private static bool IsSourceSymbolKind(string kind)
    {
        return
            string.Equals(kind, "MethodDeclaration", StringComparison.Ordinal) ||
            string.Equals(kind, "FunctionDeclaration", StringComparison.Ordinal) ||
            string.Equals(kind, "ConstructorDeclaration", StringComparison.Ordinal);
    }

    private static bool IsDeclarationDependencySourceSymbolKind(CppDeclaredSymbol symbol)
    {
        return symbol.Summary.Kind switch
        {
            "ClassDeclaration" or
            "StructDeclaration" or
            "UnionDeclaration" or
            "EnumDeclaration" or
            "TypeAliasDeclaration" or
            "MethodDeclaration" or
            "FunctionDeclaration" or
            "ConstructorDeclaration" => true,
            _ => false
        };
    }

    private static string BuildDeclarationText(IReadOnlyList<string> lines, int lineNumber)
    {
        if (lineNumber <= 0 || lineNumber > lines.Count)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        for (int index = lineNumber - 1; index < Math.Min(lines.Count, lineNumber + 7); index++)
        {
            string line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(line);

            int terminatorIndex = line.IndexOfAny(['{', ';']);
            if (terminatorIndex >= 0)
            {
                break;
            }
        }

        string declarationText = builder.ToString();
        int declarationTerminatorIndex = declarationText.IndexOfAny(['{', ';']);
        return declarationTerminatorIndex >= 0
            ? declarationText[..declarationTerminatorIndex]
            : declarationText;
    }

    private static IReadOnlyList<CppSourceSymbolRange> BuildSourceSymbolRanges(
        IReadOnlyList<CppDeclaredSymbol> sourceSymbols,
        int totalLineCount,
        IReadOnlyList<string> codeLines)
    {
        List<CppSourceSymbolRange> ranges = [];
        for (int index = 0; index < sourceSymbols.Count; index++)
        {
            CppDeclaredSymbol symbol = sourceSymbols[index];
            int startLine = FindFunctionBodyStartLine(codeLines, symbol.Summary.LineNumber);
            int endLine = FindFunctionBodyEndLine(codeLines, startLine);
            if (endLine < startLine)
            {
                int nextLine = index + 1 < sourceSymbols.Count
                    ? sourceSymbols[index + 1].Summary.LineNumber - 1
                    : totalLineCount;
                endLine = Math.Min(totalLineCount, Math.Max(startLine, nextLine));
            }

            ranges.Add(new CppSourceSymbolRange(symbol, startLine, endLine));
        }

        return ranges;
    }

    private static CppSourceSymbolRange? ResolveSourceSymbol(IReadOnlyList<CppSourceSymbolRange> sourceSymbols, int lineNumber)
    {
        int lowerBound = 0;
        int upperBound = sourceSymbols.Count - 1;
        int candidateIndex = -1;
        while (lowerBound <= upperBound)
        {
            int middle = lowerBound + ((upperBound - lowerBound) / 2);
            if (sourceSymbols[middle].StartLine <= lineNumber)
            {
                candidateIndex = middle;
                lowerBound = middle + 1;
            }
            else
            {
                upperBound = middle - 1;
            }
        }

        if (candidateIndex < 0)
        {
            return null;
        }

        for (int index = candidateIndex; index >= 0; index--)
        {
            CppSourceSymbolRange candidate = sourceSymbols[index];
            if (lineNumber >= candidate.StartLine && lineNumber <= candidate.EndLine)
            {
                return candidate;
            }
        }

        return null;
    }

    private static CppDeclaredSymbol? ResolveTargetSymbol(
        string token,
        CppDeclaredSymbol sourceSymbol,
        string sourceDocumentId,
        int lineNumber,
        CppSymbolLookup symbolLookup)
    {
        if (token.Contains("::", StringComparison.Ordinal))
        {
            foreach (string qualifiedLookupName in EnumerateQualifiedLookupNames(sourceSymbol.NamespaceScopePath, token))
            {
                if (TryResolveTargetSymbolFromLookup(
                    symbolLookup.ByQualifiedName,
                    qualifiedLookupName,
                    sourceDocumentId,
                    sourceSymbol.Key,
                    lineNumber,
                    out CppDeclaredSymbol? resolved))
                {
                    return resolved;
                }
            }
        }
        else
        {
            foreach (string scopedLookupName in EnumerateSimpleLookupNames(sourceSymbol.ContainingScopePath, token))
            {
                if (TryResolveTargetSymbolFromLookup(
                    symbolLookup.ByQualifiedName,
                    scopedLookupName,
                    sourceDocumentId,
                    sourceSymbol.Key,
                    lineNumber,
                    out CppDeclaredSymbol? resolved))
                {
                    return resolved;
                }
            }
        }

        return symbolLookup.BySimpleName.TryGetValue(ExtractSimpleName(token), out IReadOnlyList<CppDeclaredSymbol>? candidates)
            ? ResolvePreferredTargetSymbol(candidates, sourceDocumentId, sourceSymbol.Key, lineNumber)
            : null;
    }

    private static bool TryResolveTargetSymbolFromLookup(
        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> symbolLookup,
        string lookupName,
        string sourceDocumentId,
        string sourceSymbolKey,
        int lineNumber,
        out CppDeclaredSymbol? resolved)
    {
        resolved = null;
        if (!symbolLookup.TryGetValue(lookupName, out IReadOnlyList<CppDeclaredSymbol>? candidates))
        {
            return false;
        }

        resolved = ResolvePreferredTargetSymbol(candidates, sourceDocumentId, sourceSymbolKey, lineNumber);
        return resolved is not null;
    }

    private static CppDeclaredSymbol? ResolvePreferredTargetSymbol(
        IReadOnlyList<CppDeclaredSymbol> candidates,
        string sourceDocumentId,
        string sourceSymbolKey,
        int lineNumber)
    {
        CppDeclaredSymbol? sameDocumentPreferred = candidates
            .Where(candidate =>
                string.Equals(candidate.DocumentId, sourceDocumentId, StringComparison.Ordinal) &&
                !string.Equals(candidate.Key, sourceSymbolKey, StringComparison.Ordinal) &&
                candidate.IsDefinition &&
                candidate.Summary.LineNumber <= lineNumber)
            .OrderByDescending(candidate => candidate.Summary.LineNumber)
            .FirstOrDefault();

        if (sameDocumentPreferred is not null)
        {
            return sameDocumentPreferred;
        }

        CppDeclaredSymbol? sameDocumentAny = candidates
            .Where(candidate =>
                string.Equals(candidate.DocumentId, sourceDocumentId, StringComparison.Ordinal) &&
                !string.Equals(candidate.Key, sourceSymbolKey, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Summary.LineNumber)
            .FirstOrDefault();

        if (sameDocumentAny is not null)
        {
            return sameDocumentAny;
        }

        CppDeclaredSymbol? definitionAny = candidates
            .Where(candidate =>
                !string.Equals(candidate.Key, sourceSymbolKey, StringComparison.Ordinal) &&
                candidate.IsDefinition)
            .OrderBy(candidate => candidate.DocumentId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Summary.LineNumber)
            .FirstOrDefault();

        if (definitionAny is not null)
        {
            return definitionAny;
        }

        return candidates.FirstOrDefault(candidate => !string.Equals(candidate.Key, sourceSymbolKey, StringComparison.Ordinal));
    }

    private static int FindFunctionBodyStartLine(IReadOnlyList<string> lines, int declarationLineNumber)
    {
        for (int index = Math.Max(0, declarationLineNumber - 1); index < Math.Min(lines.Count, declarationLineNumber + 12); index++)
        {
            if (lines[index].Contains('{', StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        return declarationLineNumber;
    }

    private static int FindFunctionBodyEndLine(IReadOnlyList<string> lines, int startLineNumber)
    {
        int braceDepth = 0;
        bool sawOpeningBrace = false;

        for (int index = Math.Max(0, startLineNumber - 1); index < lines.Count; index++)
        {
            foreach (char current in lines[index])
            {
                if (current == '{')
                {
                    braceDepth++;
                    sawOpeningBrace = true;
                }
                else if (current == '}')
                {
                    braceDepth--;
                    if (sawOpeningBrace && braceDepth <= 0)
                    {
                        return index + 1;
                    }
                }
            }
        }

        return startLineNumber;
    }

    private static string ResolveReferenceKind(string line, int matchIndex, int matchLength)
    {
        string prefix = matchIndex > 0
            ? line[..matchIndex]
            : string.Empty;
        if (prefix.TrimEnd().EndsWith("new", StringComparison.Ordinal))
        {
            return "creation";
        }

        string suffix = matchIndex + matchLength < line.Length
            ? line[(matchIndex + matchLength)..].TrimStart()
            : string.Empty;
        if (suffix.StartsWith("(", StringComparison.Ordinal))
        {
            return "call";
        }

        if (
            prefix.Contains(':', StringComparison.Ordinal) &&
            (
                prefix.Contains("class", StringComparison.Ordinal) ||
                prefix.Contains("struct", StringComparison.Ordinal) ||
                prefix.Contains("public", StringComparison.Ordinal) ||
                prefix.Contains("protected", StringComparison.Ordinal) ||
                prefix.Contains("private", StringComparison.Ordinal)))
        {
            return "inheritance";
        }

        return "reference";
    }

    private static string ResolveDeclarationReferenceKind(string declarationText, string sourceKind, int matchIndex)
    {
        if (matchIndex <= 0)
        {
            return "reference";
        }

        string prefix = declarationText[..matchIndex];
        bool isTypeDeclaration =
            string.Equals(sourceKind, "ClassDeclaration", StringComparison.Ordinal) ||
            string.Equals(sourceKind, "StructDeclaration", StringComparison.Ordinal) ||
            string.Equals(sourceKind, "UnionDeclaration", StringComparison.Ordinal);
        if (isTypeDeclaration &&
            prefix.Contains(':', StringComparison.Ordinal) &&
            (prefix.Contains("class", StringComparison.Ordinal) || prefix.Contains("struct", StringComparison.Ordinal)))
        {
            return "inheritance";
        }

        return "reference";
    }

    private static string ExtractSimpleName(string qualifiedName)
    {
        int delimiter = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return delimiter >= 0
            ? qualifiedName[(delimiter + 2)..]
            : qualifiedName;
    }

    private static string DetermineFunctionKind(string rawQualifiedName, string currentTypeScopePath)
    {
        bool isMethod = rawQualifiedName.Contains("::", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(currentTypeScopePath);
        if (!isMethod)
        {
            return "FunctionDeclaration";
        }

        string functionName = ExtractSimpleName(rawQualifiedName);
        string containingTypeName = ResolveContainingTypeName(rawQualifiedName, currentTypeScopePath);
        if (
            !string.IsNullOrWhiteSpace(containingTypeName) &&
            !functionName.StartsWith("~", StringComparison.Ordinal) &&
            string.Equals(functionName, containingTypeName, StringComparison.Ordinal))
        {
            return "ConstructorDeclaration";
        }

        return "MethodDeclaration";
    }

    private static string ResolveContainingTypeName(string rawQualifiedName, string currentTypeScopePath)
    {
        if (rawQualifiedName.Contains("::", StringComparison.Ordinal))
        {
            string containingScopePath = GetContainingScopePath(rawQualifiedName);
            if (!string.IsNullOrWhiteSpace(containingScopePath))
            {
                return ExtractSimpleName(containingScopePath);
            }
        }

        return string.IsNullOrWhiteSpace(currentTypeScopePath)
            ? string.Empty
            : ExtractSimpleName(currentTypeScopePath);
    }

    private static string GetContainingScopePath(string qualifiedName)
    {
        int delimiter = qualifiedName.LastIndexOf("::", StringComparison.Ordinal);
        return delimiter >= 0
            ? qualifiedName[..delimiter]
            : string.Empty;
    }

    private static string QualifyName(string scopePath, string name)
    {
        if (string.IsNullOrWhiteSpace(scopePath))
        {
            return name;
        }

        string prefix = $"{scopePath}::";
        return name.StartsWith(prefix, StringComparison.Ordinal)
            ? name
            : $"{prefix}{name}";
    }

    private static IReadOnlyList<CppScopeSegment> BuildScopeSegments(string qualifiedName, string kind)
    {
        return qualifiedName
            .Split("::", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => new CppScopeSegment(segment, kind))
            .ToArray();
    }

    private static string GetScopePath(IReadOnlyList<CppScopeFrame> scopeStack, bool includeTypes)
    {
        IEnumerable<string> scopeSegments = includeTypes
            ? scopeStack.Select(frame => frame.Segment)
            : scopeStack
                .Where(frame => string.Equals(frame.Kind, "namespace", StringComparison.Ordinal))
                .Select(frame => frame.Segment);

        return string.Join("::", scopeSegments);
    }

    private static string GetTypeScopePath(IReadOnlyList<CppScopeFrame> scopeStack)
    {
        return string.Join("::", scopeStack
            .Where(frame => string.Equals(frame.Kind, "type", StringComparison.Ordinal))
            .Select(frame => frame.Segment));
    }

    private static bool CanOpenScope(string trimmedLine)
    {
        return
            !string.IsNullOrWhiteSpace(trimmedLine) &&
            !trimmedLine.EndsWith(";", StringComparison.Ordinal);
    }

    private static void UpdateScopeState(
        string line,
        IList<CppScopeFrame> scopeStack,
        ref CppPendingScope? pendingScope,
        ref int braceDepth)
    {
        foreach (char current in line)
        {
            if (current == '{')
            {
                braceDepth++;
                if (pendingScope is not null)
                {
                    foreach (CppScopeSegment segment in pendingScope.Segments)
                    {
                        scopeStack.Add(new CppScopeFrame(segment.Segment, segment.Kind, braceDepth));
                    }

                    pendingScope = null;
                }

                continue;
            }

            if (current != '}')
            {
                continue;
            }

            while (scopeStack.Count > 0 && scopeStack[^1].CloseDepth == braceDepth)
            {
                scopeStack.RemoveAt(scopeStack.Count - 1);
            }

            if (braceDepth > 0)
            {
                braceDepth--;
            }
        }

        if (pendingScope is not null &&
            line.Contains(';', StringComparison.Ordinal) &&
            !line.Contains('{', StringComparison.Ordinal))
        {
            pendingScope = null;
        }
    }

    private static IEnumerable<string> EnumerateSimpleLookupNames(string containingScopePath, string simpleName)
    {
        return EnumerateLookupNames(containingScopePath, simpleName);
    }

    private static IEnumerable<string> EnumerateQualifiedLookupNames(string namespaceScopePath, string qualifiedName)
    {
        return EnumerateLookupNames(namespaceScopePath, qualifiedName);
    }

    private static IEnumerable<string> EnumerateLookupNames(string scopePath, string symbolName)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string scopePrefix in EnumerateScopePrefixes(scopePath))
        {
            string candidate = QualifyName(scopePrefix, symbolName);
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        if (seen.Add(symbolName))
        {
            yield return symbolName;
        }
    }

    private static IEnumerable<string> EnumerateScopePrefixes(string scopePath)
    {
        if (string.IsNullOrWhiteSpace(scopePath))
        {
            yield break;
        }

        string[] parts = scopePath.Split("::", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int length = parts.Length; length > 0; length--)
        {
            yield return string.Join("::", parts.Take(length));
        }
    }

    private static string? ResolveIncludePath(
        string sourceFilePath,
        IncludeDirective includeDirective,
        IReadOnlyList<string> includeDirectories)
    {
        string target = includeDirective.Target.Replace('/', Path.DirectorySeparatorChar).Trim();
        if (string.IsNullOrWhiteSpace(target) || ContainsMsBuildVariable(target))
        {
            return null;
        }

        if (Path.IsPathRooted(target))
        {
            return File.Exists(target) ? Path.GetFullPath(target) : null;
        }

        List<string> lookupDirectories = [];
        if (includeDirective.IsQuoted)
        {
            lookupDirectories.Add(Path.GetDirectoryName(sourceFilePath) ?? string.Empty);
        }

        lookupDirectories.AddRange(includeDirectories);
        foreach (string lookupDirectory in lookupDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(lookupDirectory))
            {
                continue;
            }

            string candidatePath;
            try
            {
                candidatePath = Path.GetFullPath(Path.Combine(lookupDirectory, target));
            }
            catch
            {
                continue;
            }

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static IReadOnlyList<ProjectReferenceSummary> ReadProjectReferences(
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        MsBuildEvaluationContext evaluationContext)
    {
        return projectDocuments
            .SelectMany(projectDocument => projectDocument.Document.Descendants()
                .Where(element =>
                    string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal) &&
                    IsElementActive(element, evaluationContext, projectDocument.BaseDirectory))
                .Select(element => ResolvePath(
                    projectDocument.BaseDirectory,
                    element.Attribute("Include")?.Value ?? string.Empty,
                    evaluationContext)))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
            {
                string resolvedPath = path!;
                string displayName = Path.GetFileNameWithoutExtension(resolvedPath);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = resolvedPath;
                }

                return new ProjectReferenceSummary(
                    AnalysisIdentity.BuildProjectKey(displayName, resolvedPath),
                    displayName);
            })
            .GroupBy(reference => reference.TargetProjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(reference => reference.DisplayName, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(reference => reference.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => reference.TargetProjectKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadPackageReferences(
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        MsBuildEvaluationContext evaluationContext)
    {
        return projectDocuments
            .SelectMany(projectDocument => projectDocument.Document.Descendants()
                .Where(element =>
                    string.Equals(element.Name.LocalName, "PackageReference", StringComparison.Ordinal) &&
                    IsElementActive(element, evaluationContext, projectDocument.BaseDirectory)))
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
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadMetadataReferences(
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        MsBuildEvaluationContext evaluationContext)
    {
        HashSet<string> references = new(StringComparer.OrdinalIgnoreCase);
        foreach (XElement element in projectDocuments.SelectMany(projectDocument => projectDocument.Document.Descendants()))
        {
            if (!string.Equals(element.Name.LocalName, "Reference", StringComparison.Ordinal))
            {
                continue;
            }

            ProjectXmlDocument? ownerDocument = projectDocuments.FirstOrDefault(projectDocument =>
                ReferenceEquals(projectDocument.Document, element.Document));
            string baseDirectory = ownerDocument?.BaseDirectory ?? evaluationContext.ProjectDirectory;
            if (!IsElementActive(element, evaluationContext, baseDirectory))
            {
                continue;
            }

            string? include = element.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            references.Add(include.Trim());
        }

        return references.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyList<NativeDependencySummary> ReadProjectLevelNativeDependencies(
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        MsBuildEvaluationContext evaluationContext)
    {
        List<NativeDependencySummary> dependencies = [];
        foreach (ProjectXmlDocument projectDocument in projectDocuments)
        {
            foreach (XElement element in projectDocument.Document.Descendants())
            {
                if (!IsElementActive(element, evaluationContext, projectDocument.BaseDirectory))
                {
                    continue;
                }

                string importKind = element.Name.LocalName switch
                {
                    "DelayLoadDLLs" => "DelayLoad",
                    "AdditionalDependencies" => "LinkerInput",
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(importKind))
                {
                    continue;
                }

                foreach (string token in SplitMsBuildList(element.Value))
                {
                    string trimmed = token.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(trimmed) || ContainsMsBuildVariable(trimmed))
                    {
                        continue;
                    }

                    string fileName = Path.GetFileName(trimmed);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        continue;
                    }

                    string extension = Path.GetExtension(fileName);
                    if (string.Equals(importKind, "DelayLoad", StringComparison.OrdinalIgnoreCase))
                    {
                        dependencies.Add(new NativeDependencySummary(
                            fileName,
                            importKind,
                            "high",
                            SourceDocumentId: null,
                            SourceSymbolId: null,
                            ImportedSymbols: Array.Empty<string>()));
                        continue;
                    }

                    if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    dependencies.Add(new NativeDependencySummary(
                        fileName,
                        importKind,
                        "high",
                        SourceDocumentId: null,
                        SourceSymbolId: null,
                        ImportedSymbols: Array.Empty<string>()));
                }
            }
        }

        return AggregateNativeDependencies(dependencies);
    }

    private static void WarnIfLargeCppInput(string projectName, int fileCount, ICollection<string> diagnostics)
    {
        if (fileCount < LargeCppFileCountWarningThreshold)
        {
            return;
        }

        diagnostics.Add(T("diag.cpp.largeInputWarning", projectName, fileCount));
    }

    private static void ReportCppAnalysisDuration(
        string projectName,
        int fileCount,
        TimeSpan duration,
        ICollection<string> diagnostics)
    {
        if (fileCount < LargeCppFileCountWarningThreshold && duration < s_largeCppAnalysisDurationWarningThreshold)
        {
            return;
        }

        diagnostics.Add(T(
            duration >= s_largeCppAnalysisDurationWarningThreshold
                ? "diag.cpp.analysisDuration.warning"
                : "diag.cpp.analysisDuration.info",
            projectName,
            fileCount,
            duration.TotalSeconds));
    }

    private static bool IsElementActive(
        XElement element,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory)
    {
        for (XElement? current = element; current is not null; current = current.Parent)
        {
            string? condition = current.Attribute("Condition")?.Value;
            if (!evaluationContext.EvaluateCondition(condition, baseDirectory))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluateMsBuildCondition(
        string? condition,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return true;
        }

        string expanded = ExpandMsBuildProperties(condition, evaluationContext);
        return EvaluateConditionExpression(expanded.Trim(), evaluationContext, baseDirectory);
    }

    private static bool EvaluateConditionExpression(
        string expression,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory)
    {
        string normalized = StripWrappingParentheses(expression);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (TrySplitCondition(normalized, "Or", out IReadOnlyList<string>? orTerms))
        {
            return orTerms!.Any(term => EvaluateConditionExpression(term, evaluationContext, baseDirectory));
        }

        if (TrySplitCondition(normalized, "And", out IReadOnlyList<string>? andTerms))
        {
            return andTerms!.All(term => EvaluateConditionExpression(term, evaluationContext, baseDirectory));
        }

        if (normalized.StartsWith("!", StringComparison.Ordinal))
        {
            return !EvaluateConditionExpression(normalized[1..].Trim(), evaluationContext, baseDirectory);
        }

        if (TryEvaluateConditionFunction(normalized, "Exists", evaluationContext, baseDirectory, out bool existsResult))
        {
            return existsResult;
        }

        if (TryEvaluateConditionFunction(normalized, "HasTrailingSlash", evaluationContext, baseDirectory, out bool hasTrailingSlashResult))
        {
            return hasTrailingSlashResult;
        }

        if (TryParseConditionComparison(normalized, out string? left, out string? op, out string? right))
        {
            return EvaluateConditionComparison(left, op, right, evaluationContext, baseDirectory);
        }

        return EvaluateConditionTruthiness(normalized, evaluationContext, baseDirectory);
    }

    private static string StripWrappingParentheses(string expression)
    {
        string normalized = expression.Trim();
        while (
            normalized.Length >= 2 &&
            normalized[0] == '(' &&
            normalized[^1] == ')' &&
            IsConditionWrappedByParentheses(normalized))
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized;
    }

    private static bool IsConditionWrappedByParentheses(string expression)
    {
        int depth = 0;
        char quote = '\0';
        for (int index = 0; index < expression.Length; index++)
        {
            char current = expression[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current != ')')
            {
                continue;
            }

            depth--;
            if (depth == 0 && index < expression.Length - 1)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private static bool TrySplitCondition(string expression, string keyword, out IReadOnlyList<string>? segments)
    {
        List<string> parts = [];
        int depth = 0;
        char quote = '\0';
        int segmentStart = 0;

        for (int index = 0; index <= expression.Length - keyword.Length; index++)
        {
            char current = expression[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current == ')')
            {
                depth--;
                continue;
            }

            if (depth != 0 ||
                !expression.AsSpan(index).StartsWith(keyword, StringComparison.OrdinalIgnoreCase) ||
                !IsConditionKeywordBoundary(expression, index - 1) ||
                !IsConditionKeywordBoundary(expression, index + keyword.Length))
            {
                continue;
            }

            string segment = expression[segmentStart..index].Trim();
            if (!string.IsNullOrWhiteSpace(segment))
            {
                parts.Add(segment);
            }

            index += keyword.Length - 1;
            segmentStart = index + 1;
        }

        if (parts.Count == 0)
        {
            segments = null;
            return false;
        }

        string tail = expression[segmentStart..].Trim();
        if (!string.IsNullOrWhiteSpace(tail))
        {
            parts.Add(tail);
        }

        segments = parts;
        return true;
    }

    private static bool IsConditionKeywordBoundary(string expression, int index)
    {
        if (index < 0 || index >= expression.Length)
        {
            return true;
        }

        return !char.IsLetterOrDigit(expression[index]) && expression[index] != '_';
    }

    private static bool TryEvaluateConditionFunction(
        string expression,
        string functionName,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory,
        out bool result)
    {
        result = false;
        if (!expression.StartsWith(functionName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string remainder = expression[functionName.Length..].Trim();
        if (!remainder.StartsWith("(", StringComparison.Ordinal) || !remainder.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        string argument = remainder[1..^1].Trim();
        string value = NormalizeConditionValue(argument, evaluationContext, baseDirectory);
        if (string.Equals(functionName, "Exists", StringComparison.OrdinalIgnoreCase))
        {
            string? resolvedPath = ResolveConditionPath(value, baseDirectory);
            result = resolvedPath is not null && (File.Exists(resolvedPath) || Directory.Exists(resolvedPath));
            return true;
        }

        if (string.Equals(functionName, "HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
        {
            result =
                value.EndsWith(Path.DirectorySeparatorChar) ||
                value.EndsWith(Path.AltDirectorySeparatorChar);
            return true;
        }

        return false;
    }

    private static string? ResolveConditionPath(string value, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Path.IsPathRooted(value)
                ? Path.GetFullPath(value)
                : Path.GetFullPath(Path.Combine(baseDirectory, value));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool TryParseConditionComparison(
        string expression,
        out string? left,
        out string? op,
        out string? right)
    {
        left = null;
        op = null;
        right = null;

        foreach (string candidateOperator in new[] { "==", "!=", ">=", "<=", ">", "<" })
        {
            int operatorIndex = FindTopLevelOperator(expression, candidateOperator);
            if (operatorIndex < 0)
            {
                continue;
            }

            left = expression[..operatorIndex].Trim();
            op = candidateOperator;
            right = expression[(operatorIndex + candidateOperator.Length)..].Trim();
            return true;
        }

        return false;
    }

    private static int FindTopLevelOperator(string expression, string op)
    {
        int depth = 0;
        char quote = '\0';
        for (int index = 0; index <= expression.Length - op.Length; index++)
        {
            char current = expression[index];
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == '(')
            {
                depth++;
                continue;
            }

            if (current == ')')
            {
                depth--;
                continue;
            }

            if (depth == 0 && expression.AsSpan(index).StartsWith(op, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool EvaluateConditionComparison(
        string? left,
        string? op,
        string? right,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory)
    {
        string leftValue = NormalizeConditionValue(left, evaluationContext, baseDirectory);
        string rightValue = NormalizeConditionValue(right, evaluationContext, baseDirectory);

        if (double.TryParse(leftValue, out double leftNumber) &&
            double.TryParse(rightValue, out double rightNumber))
        {
            return op switch
            {
                "==" => leftNumber == rightNumber,
                "!=" => leftNumber != rightNumber,
                ">=" => leftNumber >= rightNumber,
                "<=" => leftNumber <= rightNumber,
                ">" => leftNumber > rightNumber,
                "<" => leftNumber < rightNumber,
                _ => false
            };
        }

        int comparison = string.Compare(leftValue, rightValue, StringComparison.OrdinalIgnoreCase);
        return op switch
        {
            "==" => comparison == 0,
            "!=" => comparison != 0,
            ">=" => comparison >= 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            "<" => comparison < 0,
            _ => false
        };
    }

    private static bool EvaluateConditionTruthiness(
        string expression,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory)
    {
        string value = NormalizeConditionValue(expression, evaluationContext, baseDirectory);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return
            !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeConditionValue(
        string? expression,
        MsBuildEvaluationContext evaluationContext,
        string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        string expanded = ExpandMsBuildProperties(expression, evaluationContext).Trim();
        string unquoted = UnquoteConditionValue(expanded);
        return string.Equals(unquoted, ".", StringComparison.Ordinal)
            ? baseDirectory
            : unquoted;
    }

    private static string UnquoteConditionValue(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '\'' && trimmed[^1] == '\'') || (trimmed[0] == '"' && trimmed[^1] == '"')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static IReadOnlyList<NativeDependencySummary> ExtractNativeDependencies(
        string documentId,
        IReadOnlyList<CppDeclaredSymbol> declaredSymbols,
        IReadOnlyList<string> rawLines)
    {
        List<NativeDependencySummary> observations = [];
        Dictionary<string, string> loadLibraryHandles = new(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<CppSourceSymbolRange> sourceSymbolRanges = BuildSourceSymbolRanges(
            declaredSymbols.Where(symbol => symbol.IsDefinition && IsSourceSymbolKind(symbol.Summary.Kind))
                .OrderBy(symbol => symbol.Summary.LineNumber)
                .ToArray(),
            rawLines.Count,
            rawLines);

        for (int index = 0; index < rawLines.Count; index++)
        {
            string line = rawLines[index];
            int lineNumber = index + 1;
            CppSourceSymbolRange? sourceRange = ResolveSourceSymbol(sourceSymbolRanges, lineNumber);
            string? sourceSymbolId = sourceRange?.Symbol.Summary.Id;

            Match assignmentMatch = s_loadLibraryAssignmentPattern.Match(line);
            if (assignmentMatch.Success)
            {
                string libraryName = assignmentMatch.Groups["dll"].Value.Trim();
                string? handleName = assignmentMatch.Groups["handle"].Success
                    ? assignmentMatch.Groups["handle"].Value.Trim()
                    : null;
                if (!string.IsNullOrWhiteSpace(handleName))
                {
                    loadLibraryHandles[handleName] = libraryName;
                }

                observations.Add(new NativeDependencySummary(
                    libraryName,
                    "LoadLibrary",
                    "high",
                    documentId,
                    sourceSymbolId,
                    Array.Empty<string>()));
            }

            foreach (Match procMatch in s_getProcAddressPattern.Matches(line))
            {
                string handleName = procMatch.Groups["handle"].Value.Trim();
                string importedSymbol = procMatch.Groups["proc"].Value.Trim();
                if (!loadLibraryHandles.TryGetValue(handleName, out string? libraryName))
                {
                    continue;
                }

                observations.Add(new NativeDependencySummary(
                    libraryName,
                    "GetProcAddress",
                    "high",
                    documentId,
                    sourceSymbolId,
                    [importedSymbol]));
            }
        }

        return observations;
    }

    private static IReadOnlyList<NativeDependencySummary> AggregateNativeDependencies(
        IEnumerable<NativeDependencySummary> observations)
    {
        return observations
            .GroupBy(item => (
                LibraryName: item.LibraryName.ToLowerInvariant(),
                ImportKind: item.ImportKind.ToLowerInvariant(),
                Confidence: item.Confidence.ToLowerInvariant()))
            .Select(group => new NativeDependencySummary(
                group.First().LibraryName,
                group.First().ImportKind,
                group.First().Confidence,
                group.Select(item => item.SourceDocumentId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                group.Select(item => item.SourceSymbolId).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                group.SelectMany(item => item.ImportedSymbols)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .OrderBy(item => item.LibraryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ImportKind, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> EnumerateCppFiles(string rootDirectory, ICollection<string>? diagnostics = null)
    {
        Stack<string> pendingDirectories = new();
        pendingDirectories.Push(rootDirectory);
        HashSet<string> discoveredFiles = new(StringComparer.OrdinalIgnoreCase);
        int counter = 0;
        bool reachedLimit = false;

        while (pendingDirectories.Count > 0)
        {
            string currentDirectory = pendingDirectories.Pop();

            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(currentDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
                currentFiles = Directory.EnumerateFiles(currentDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string filePath in currentFiles)
            {
                if (!IsCppFile(filePath))
                {
                    continue;
                }

                counter++;
                if (counter > MaxEnumeratedCppFiles)
                {
                    reachedLimit = true;
                    break;
                }

                discoveredFiles.Add(Path.GetFullPath(filePath));
            }

            if (reachedLimit)
            {
                break;
            }
        }

        if (reachedLimit)
        {
            diagnostics?.Add(T("diag.cpp.enumerationLimitReached", MaxEnumeratedCppFiles, rootDirectory));
        }

        return discoveredFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsIgnoredDirectory(string name)
    {
        return AnalysisPathFilter.IsIgnoredDirectoryName(name);
    }

    private static DocumentDescriptor AddDocument(
        IDictionary<string, DocumentDescriptor> documentsByPath,
        string projectIdentity,
        string filePath)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (documentsByPath.TryGetValue(fullPath, out DocumentDescriptor? existing))
        {
            return existing;
        }

        string fileName = Path.GetFileName(fullPath);
        DocumentDescriptor descriptor = new(
            BuildDocumentId(projectIdentity, fullPath, fileName),
            fileName,
            fullPath);
        documentsByPath.Add(fullPath, descriptor);
        return descriptor;
    }

    private static bool IsCppFile(string filePath)
    {
        return s_cppExtensions.Contains(Path.GetExtension(filePath));
    }

    private static string ResolveProjectName(XDocument projectDocument, string projectPath)
    {
        string? projectName = projectDocument.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "ProjectName", StringComparison.Ordinal))
            ?.Value
            ?.Trim();

        return string.IsNullOrWhiteSpace(projectName)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : projectName;
    }

    private static string ResolveWorkspaceRootPath(string inputPath)
    {
        if (string.Equals(Path.GetExtension(inputPath), ".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory();
        }

        return Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory();
    }

    private static bool IsPathUnderRoot(string candidatePath, string rootPath)
    {
        string normalizedRoot = EnsureDirectorySeparatorSuffix(Path.GetFullPath(rootPath));
        string normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureDirectorySeparatorSuffix(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return path;
        }

        return $"{path}{Path.DirectorySeparatorChar}";
    }

    private static string? ResolvePath(string baseDirectory, string value)
    {
        return ResolvePath(baseDirectory, value, evaluationContext: null);
    }

    private static string? ResolvePath(string baseDirectory, string value, MsBuildEvaluationContext? evaluationContext)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = ExpandMsBuildProperties(value.Trim().Trim('"'), evaluationContext)
            .Replace('/', Path.DirectorySeparatorChar);
        if (ContainsMsBuildVariable(normalized))
        {
            return null;
        }

        try
        {
            return Path.IsPathRooted(normalized)
                ? Path.GetFullPath(normalized)
                : Path.GetFullPath(Path.Combine(baseDirectory, normalized));
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeDirectory(string path, string baseDirectory)
    {
        return NormalizeDirectory(path, baseDirectory, evaluationContext: null);
    }

    private static string? NormalizeDirectory(string path, string baseDirectory, MsBuildEvaluationContext? evaluationContext)
    {
        string? resolved = ResolvePath(baseDirectory, path, evaluationContext);
        if (string.IsNullOrWhiteSpace(resolved) || !Directory.Exists(resolved))
        {
            return null;
        }

        return resolved;
    }

    private static IEnumerable<string> SplitMsBuildList(string value)
    {
        return value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }

    private static bool ContainsMsBuildVariable(string value)
    {
        return value.Contains("$(", StringComparison.Ordinal) || value.Contains("%(", StringComparison.Ordinal);
    }

    private static (string Configuration, string Platform) ResolveBuildConfiguration(XDocument projectDocument)
    {
        string? preferredConfiguration =
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("Configuration"),
                ReadUnconditionedProjectProperty(projectDocument, "Configuration")) ??
            "Debug";
        string? preferredPlatform =
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("Platform"),
                ReadUnconditionedProjectProperty(projectDocument, "Platform")) ??
            "x64";

        IReadOnlyList<(string Configuration, string Platform)> candidates = projectDocument
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectConfiguration", StringComparison.Ordinal))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value =>
            {
                string[] parts = value!.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Length >= 2
                    ? (Configuration: parts[0], Platform: parts[1])
                    : default;
            })
            .Where(candidate =>
                !string.IsNullOrWhiteSpace(candidate.Configuration) &&
                !string.IsNullOrWhiteSpace(candidate.Platform))
            .Distinct()
            .ToArray();

        if (candidates.Count == 0)
        {
            return (preferredConfiguration, preferredPlatform);
        }

        return candidates
            .OrderByDescending(candidate => ScoreBuildConfigurationCandidate(candidate, preferredConfiguration, preferredPlatform))
            .ThenBy(candidate => candidate.Configuration, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Platform, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static int ScoreBuildConfigurationCandidate(
        (string Configuration, string Platform) candidate,
        string preferredConfiguration,
        string preferredPlatform)
    {
        int score = 0;
        if (string.Equals(candidate.Configuration, preferredConfiguration, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (PlatformsEqual(candidate.Platform, preferredPlatform))
        {
            score += 75;
        }

        if (string.Equals(candidate.Configuration, "Debug", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (PlatformsEqual(candidate.Platform, "x64"))
        {
            score += 10;
        }

        if (string.Equals(candidate.Configuration, "Release", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    private static string? ReadUnconditionedProjectProperty(XDocument projectDocument, string propertyName)
    {
        return projectDocument.Root?
            .Elements()
            .Where(element =>
                string.Equals(element.Name.LocalName, "PropertyGroup", StringComparison.Ordinal) &&
                element.Attribute("Condition") is null)
            .Elements()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, propertyName, StringComparison.Ordinal) &&
                element.Attribute("Condition") is null)
            ?.Value
            ?.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static bool PlatformsEqual(string left, string right)
    {
        return string.Equals(NormalizePlatformName(left), NormalizePlatformName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePlatformName(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return string.Empty;
        }

        return platform.Trim() switch
        {
            "Win32" => "x86",
            _ => platform.Trim().ToLowerInvariant()
        };
    }

    private static MsBuildEvaluationContext CreateEvaluationContext(
        XDocument projectDocument,
        IReadOnlyList<ProjectXmlDocument> projectDocuments,
        string projectPath,
        string workspaceRootPath,
        string projectName)
    {
        string projectDirectory = Path.GetDirectoryName(projectPath) ?? workspaceRootPath;
        (string configuration, string platform) = ResolveBuildConfiguration(projectDocument);

        MsBuildEvaluationContext evaluationContext = new(projectName, projectPath, projectDirectory, workspaceRootPath, configuration, platform);
        evaluationContext.ApplyProjectProperties(projectDocuments);
        return evaluationContext;
    }

    private static string ExpandMsBuildProperties(string value, MsBuildEvaluationContext? evaluationContext)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string expanded = s_msBuildItemMetadataPattern.Replace(value, string.Empty);
        if (evaluationContext is null)
        {
            return expanded.Trim();
        }

        for (int iteration = 0; iteration < 4; iteration++)
        {
            string next = s_msBuildPropertyPattern.Replace(expanded, match =>
            {
                string propertyName = match.Groups["name"].Value.Trim();
                return evaluationContext.TryResolveProperty(propertyName, out string? resolvedValue)
                    ? resolvedValue
                    : match.Value;
            });

            if (string.Equals(next, expanded, StringComparison.Ordinal))
            {
                break;
            }

            expanded = next;
        }

        return expanded.Trim();
    }

    private static IReadOnlyList<string> TokenizeCommandLine(string command)
    {
        return s_commandTokenPattern.Matches(command)
            .Select(match => match.Value.Trim())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token =>
            {
                string trimmed = token.Trim();
                if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
                {
                    return trimmed[1..^1];
                }

                return trimmed;
            })
            .ToArray();
    }

    private static void IncrementCount(
        IDictionary<(string Source, string Target), int> counts,
        (string Source, string Target) key)
    {
        if (counts.TryGetValue(key, out int current))
        {
            counts[key] = current + 1;
            return;
        }

        counts.Add(key, 1);
    }

    private static void IncrementCount(
        IDictionary<(string Source, string Target), int> counts,
        (string Source, string Target) key,
        int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (counts.TryGetValue(key, out int current))
        {
            counts[key] = current + amount;
            return;
        }

        counts.Add(key, amount);
    }

    private static void IncrementReferenceKind(
        IDictionary<(string Source, string Target), Dictionary<string, int>> counts,
        (string Source, string Target) key,
        string referenceKind)
    {
        if (!counts.TryGetValue(key, out Dictionary<string, int>? perKindCounts))
        {
            perKindCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            counts.Add(key, perKindCounts);
        }

        if (perKindCounts.TryGetValue(referenceKind, out int current))
        {
            perKindCounts[referenceKind] = current + 1;
            return;
        }

        perKindCounts.Add(referenceKind, 1);
    }

    private static string ResolveDominantReferenceKind(
        IReadOnlyDictionary<(string Source, string Target), Dictionary<string, int>> counts,
        (string Source, string Target) key)
    {
        if (!counts.TryGetValue(key, out Dictionary<string, int>? perKindCounts) || perKindCounts.Count == 0)
        {
            return "reference";
        }

        return perKindCounts
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Key)
            .First();
    }

    private static string BuildDocumentId(string projectIdentity, string? filePath, string documentName)
    {
        return AnalysisIdBuilder.BuildDocumentId(projectIdentity, filePath, documentName);
    }

    private static string BuildSymbolId(string symbolName, string symbolKey)
    {
        return AnalysisIdBuilder.BuildSymbolId(symbolName, symbolKey);
    }

    private sealed record IncludeDirective(
        string Target,
        bool IsQuoted,
        int LineNumber,
        string? Snippet);

    private sealed record DocumentDescriptor(string DocumentId, string Name, string FilePath);

    private sealed record CppScopeSegment(string Segment, string Kind);

    private sealed record CppPendingScope(IReadOnlyList<CppScopeSegment> Segments);

    private sealed record CppScopeFrame(string Segment, string Kind, int CloseDepth);

    private sealed record CppDeclaredSymbol(
        string Key,
        string DocumentId,
        SymbolAnalysisSummary Summary,
        bool IsDefinition,
        string QualifiedName,
        string ContainingScopePath,
        string NamespaceScopePath,
        string? AstUsr);

    private sealed record CppSymbolLookup(
        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> BySimpleName,
        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> ByQualifiedName,
        IReadOnlyDictionary<string, IReadOnlyList<CppDeclaredSymbol>> ByAstUsr);

    private sealed record DocumentParseContext(
        DocumentDescriptor Document,
        IReadOnlyList<string> CodeLines,
        IReadOnlyList<string> RawLines,
        IReadOnlyList<CppDeclaredSymbol> DeclaredSymbols,
        IReadOnlyList<AstReferenceObservation> AstReferences,
        bool IsAstDriven);

    private sealed record CppSourceSymbolRange(
        CppDeclaredSymbol Symbol,
        int StartLine,
        int EndLine);

    private sealed record AstReferenceObservation(
        string? SourceUsr,
        string? TargetUsr,
        string TargetName,
        int LineNumber,
        string ReferenceKind,
        string? SampleFilePath,
        string? SampleSnippet);

    private sealed record ProjectXmlDocument(
        string FilePath,
        string BaseDirectory,
        XDocument Document);

    private sealed class MsBuildEvaluationContext
    {
        private readonly Dictionary<string, string> _properties;

        public MsBuildEvaluationContext(
            string projectName,
            string projectPath,
            string projectDirectory,
            string workspaceRootPath,
            string configuration,
            string platform)
        {
            ProjectDirectory = projectDirectory;
            Configuration = configuration;
            Platform = platform;

            string normalizedProjectDirectory = EnsureDirectorySeparatorSuffix(Path.GetFullPath(projectDirectory));
            string normalizedWorkspaceRootPath = EnsureDirectorySeparatorSuffix(Path.GetFullPath(workspaceRootPath));

            _properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ProjectDir"] = normalizedProjectDirectory,
                ["MSBuildProjectDirectory"] = Path.GetFullPath(projectDirectory),
                ["ProjectPath"] = Path.GetFullPath(projectPath),
                ["ProjectFileName"] = Path.GetFileName(projectPath),
                ["ProjectName"] = projectName,
                ["SolutionDir"] = normalizedWorkspaceRootPath,
                ["SolutionPath"] = Path.Combine(normalizedWorkspaceRootPath, Path.GetFileName(projectPath)),
                ["Configuration"] = configuration,
                ["Platform"] = platform
            };
        }

        public string ProjectDirectory { get; }

        public string Configuration { get; }

        public string Platform { get; }

        public void ApplyProjectProperties(IEnumerable<ProjectXmlDocument> projectDocuments)
        {
            foreach (ProjectXmlDocument projectDocument in projectDocuments)
            {
                foreach (XElement propertyElement in projectDocument.Document.Descendants()
                    .Where(element => element.Parent is not null &&
                        string.Equals(element.Parent.Name.LocalName, "PropertyGroup", StringComparison.Ordinal)))
                {
                    if (!IsElementActive(propertyElement, this, projectDocument.BaseDirectory))
                    {
                        continue;
                    }

                    string propertyName = propertyElement.Name.LocalName;
                    string propertyValue = propertyElement.Value.Trim();
                    if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(propertyValue))
                    {
                        continue;
                    }

                    string expandedValue = ExpandMsBuildProperties(propertyValue, this);
                    if (ContainsMsBuildVariable(expandedValue))
                    {
                        continue;
                    }

                    _properties[propertyName] = NormalizePropertyValue(
                        propertyName,
                        expandedValue,
                        projectDocument.BaseDirectory);
                }
            }
        }

        public bool TryResolveProperty(string propertyName, out string resolvedValue)
        {
            if (_properties.TryGetValue(propertyName, out resolvedValue!))
            {
                return true;
            }

            string? environmentValue = Environment.GetEnvironmentVariable(propertyName);
            if (!string.IsNullOrWhiteSpace(environmentValue))
            {
                resolvedValue = environmentValue;
                return true;
            }

            resolvedValue = string.Empty;
            return false;
        }

        public bool EvaluateCondition(string? condition, string baseDirectory)
        {
            return EvaluateMsBuildCondition(condition, this, baseDirectory);
        }

        private static string NormalizePropertyValue(string propertyName, string propertyValue, string baseDirectory)
        {
            string normalizedValue = propertyValue.Trim().Replace('/', Path.DirectorySeparatorChar);
            if (!LooksLikePath(propertyName, normalizedValue))
            {
                return normalizedValue;
            }

            try
            {
                string fullPath = Path.IsPathRooted(normalizedValue)
                    ? Path.GetFullPath(normalizedValue)
                    : Path.GetFullPath(Path.Combine(baseDirectory, normalizedValue));

                if (propertyName.EndsWith("Dir", StringComparison.OrdinalIgnoreCase) ||
                    normalizedValue.EndsWith(Path.DirectorySeparatorChar) ||
                    normalizedValue.EndsWith(Path.AltDirectorySeparatorChar))
                {
                    return EnsureDirectorySeparatorSuffix(fullPath);
                }

                return fullPath;
            }
            catch (ArgumentException)
            {
                return normalizedValue;
            }
        }

        private static bool LooksLikePath(string propertyName, string propertyValue)
        {
            if (propertyValue.IndexOf(';') >= 0)
            {
                return false;
            }

            if (propertyName.EndsWith("Dir", StringComparison.OrdinalIgnoreCase) ||
                propertyName.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return propertyValue.Contains(Path.DirectorySeparatorChar) ||
                propertyValue.Contains(Path.AltDirectorySeparatorChar) ||
                propertyValue.StartsWith(".", StringComparison.Ordinal);
        }
    }

    private static void ReportProgress(IProgress<AnalysisProgressUpdate>? progress, AnalysisProgressUpdate update)
    {
        progress?.Report(update);
    }

    private sealed record CompileCommandData(
        IReadOnlyList<string> IncludeDirectories,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ParseArgumentsBySourceFile,
        string? CompileCommandsPath)
    {
        public static CompileCommandData Empty { get; } = new(
            Array.Empty<string>(),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
            CompileCommandsPath: null);
    }

    private sealed record ProjectParseArtifacts(
        IReadOnlyList<DocumentAnalysisSummary> Documents,
        IReadOnlyList<DocumentDependencySummary> DocumentDependencies,
        IReadOnlyList<SymbolDependencySummary> SymbolDependencies,
        IReadOnlyList<NativeDependencySummary> NativeDependencies,
        int SymbolCount);
}

public sealed record CppSolutionAnalysisResult(
    IReadOnlyList<CppProjectAnalysisResult> Projects,
    IReadOnlyList<string> Diagnostics);

public sealed record CppProjectAnalysisResult(
    ProjectAnalysisSummary Summary,
    IReadOnlyList<DocumentDependencySummary> DocumentDependencies,
    IReadOnlyList<SymbolDependencySummary> SymbolDependencies);
