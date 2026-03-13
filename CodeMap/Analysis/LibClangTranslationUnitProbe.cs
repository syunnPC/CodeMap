using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ClangSharp.Interop;

namespace CodeMap.Analysis;

internal static unsafe class LibClangTranslationUnitProbe
{
    private const CXTranslationUnit_Flags DetailedPreprocessingRecord = CXTranslationUnit_Flags.CXTranslationUnit_DetailedPreprocessingRecord;
    private static readonly CXCursorVisitor s_cursorVisitor = VisitCursor;

    public static bool TryParse(
        string sourceFilePath,
        IEnumerable<string> includeDirectories,
        IReadOnlyList<string>? parseArguments,
        out string message)
    {
        message = string.Empty;

        if (!TryCreateTranslationUnit(
            sourceFilePath,
            includeDirectories,
            parseArguments,
            CXTranslationUnit_Flags.CXTranslationUnit_None,
            out CXIndex index,
            out CXTranslationUnit translationUnit,
            out string failureMessage))
        {
            DisposeTranslationUnitResources(index, translationUnit);
            message = failureMessage;
            return false;
        }

        try
        {
            message = $"parsed '{Path.GetFileName(sourceFilePath)}' with ClangSharp";
            return true;
        }
        finally
        {
            DisposeTranslationUnitResources(index, translationUnit);
        }
    }

    public static bool TryAnalyzeDocument(
        string sourceFilePath,
        IEnumerable<string> includeDirectories,
        IReadOnlyList<string>? parseArguments,
        out LibClangDocumentAnalysisResult result,
        out string message)
    {
        result = new LibClangDocumentAnalysisResult(
            Symbols: Array.Empty<LibClangSymbolObservation>(),
            References: Array.Empty<LibClangReferenceObservation>());
        message = string.Empty;

        if (!TryCreateTranslationUnit(
            sourceFilePath,
            includeDirectories,
            parseArguments,
            DetailedPreprocessingRecord,
            out CXIndex index,
            out CXTranslationUnit translationUnit,
            out string failureMessage))
        {
            DisposeTranslationUnitResources(index, translationUnit);
            message = failureMessage;
            return false;
        }

        try
        {
            string normalizedSourcePath = NormalizePath(sourceFilePath);
            AstTraversalContext context = new(normalizedSourcePath);
            GCHandle handle = GCHandle.Alloc(context);
            try
            {
                CXCursor rootCursor = translationUnit.Cursor;
                _ = rootCursor.VisitChildren(s_cursorVisitor, new CXClientData(GCHandle.ToIntPtr(handle)));
            }
            finally
            {
                handle.Free();
            }

            result = new LibClangDocumentAnalysisResult(
                context.GetSymbols(),
                context.GetReferences());
            message = $"analyzed '{Path.GetFileName(sourceFilePath)}' with ClangSharp AST";
            return true;
        }
        finally
        {
            DisposeTranslationUnitResources(index, translationUnit);
        }
    }

    private static void DisposeTranslationUnitResources(
        CXIndex index,
        CXTranslationUnit translationUnit)
    {
        if (translationUnit.Handle != IntPtr.Zero)
        {
            translationUnit.Dispose();
        }

        if (index.Handle != IntPtr.Zero)
        {
            index.Dispose();
        }
    }

    private static bool TryCreateTranslationUnit(
        string sourceFilePath,
        IEnumerable<string> includeDirectories,
        IReadOnlyList<string>? parseArguments,
        CXTranslationUnit_Flags options,
        out CXIndex index,
        out CXTranslationUnit translationUnit,
        out string message)
    {
        index = default;
        translationUnit = default;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            message = "source file was not found";
            return false;
        }

        try
        {
            List<string> arguments = BuildParseArguments(sourceFilePath, includeDirectories, parseArguments);
            index = CXIndex.Create();
            if (index.Handle == IntPtr.Zero)
            {
                message = "clang_createIndex returned null";
                return false;
            }

            CXErrorCode errorCode = CXTranslationUnit.TryParse(
                index,
                sourceFilePath,
                CollectionsMarshal.AsSpan(arguments),
                [],
                options,
                out translationUnit);

            if (errorCode != CXErrorCode.CXError_Success || translationUnit.Handle == IntPtr.Zero)
            {
                message = errorCode == CXErrorCode.CXError_Success
                    ? "clang_parseTranslationUnit returned null"
                    : $"clang_parseTranslationUnit failed: {errorCode}";
                return false;
            }

            return true;
        }
        catch (DllNotFoundException ex)
        {
            message = ex.Message;
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            message = ex.Message;
            return false;
        }
    }

    private static unsafe CXChildVisitResult VisitCursor(
        CXCursor cursor,
        CXCursor parent,
        void* clientData)
    {
        if (clientData is null)
        {
            return CXChildVisitResult.CXChildVisit_Recurse;
        }

        GCHandle handle = GCHandle.FromIntPtr((IntPtr)clientData);
        if (handle.Target is AstTraversalContext context)
        {
            context.Visit(cursor, parent);
        }

        return CXChildVisitResult.CXChildVisit_Recurse;
    }

    private static List<string> BuildParseArguments(
        string sourceFilePath,
        IEnumerable<string> includeDirectories,
        IReadOnlyList<string>? parseArguments)
    {
        List<string> arguments = ["-fsyntax-only"];
        if (parseArguments is not null && parseArguments.Count > 0)
        {
            arguments.AddRange(parseArguments.Where(argument => !string.IsNullOrWhiteSpace(argument)));
        }
        else
        {
            arguments.Add($"-std={ResolveDefaultLanguageStandard(sourceFilePath)}");
            arguments.Add("-x");
            arguments.Add(ResolveDefaultLanguage(sourceFilePath));
        }

        foreach (string includeDirectory in includeDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            arguments.Add("-I");
            arguments.Add(includeDirectory);
        }

        return arguments;
    }

    private static string ResolveDefaultLanguage(string sourceFilePath)
    {
        string extension = Path.GetExtension(sourceFilePath);
        return extension switch
        {
            ".c" => "c",
            ".h" => "c++-header",
            _ => "c++"
        };
    }

    private static string ResolveDefaultLanguageStandard(string sourceFilePath)
    {
        string extension = Path.GetExtension(sourceFilePath);
        return string.Equals(extension, ".c", StringComparison.OrdinalIgnoreCase)
            ? "c17"
            : "c++20";
    }

    private static bool TryMapSymbolKind(string cursorKind, out string symbolKind)
    {
        symbolKind = cursorKind switch
        {
            "ClassDecl" or "ClassTemplate" => "ClassDeclaration",
            "StructDecl" => "StructDeclaration",
            "UnionDecl" => "UnionDeclaration",
            "EnumDecl" => "EnumDeclaration",
            "FunctionDecl" or "FunctionTemplate" => "FunctionDeclaration",
            "CXXMethod" or "Destructor" => "MethodDeclaration",
            "Constructor" => "ConstructorDeclaration",
            "TypedefDecl" or "TypeAliasDecl" or "TypeAliasTemplateDecl" => "TypeAliasDeclaration",
            "MacroDefinition" => "MacroDefinition",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(symbolKind);
    }

    private static string ResolveReferenceKind(string cursorKind)
    {
        if (string.Equals(cursorKind, "CallExpr", StringComparison.Ordinal))
        {
            return "call";
        }

        if (string.Equals(cursorKind, "CXXBaseSpecifier", StringComparison.Ordinal) ||
            string.Equals(cursorKind, "BaseSpecifier", StringComparison.Ordinal))
        {
            return "inheritance";
        }

        if (string.Equals(cursorKind, "MemberRefExpr", StringComparison.Ordinal))
        {
            return "field";
        }

        return "reference";
    }

    private static string BuildQualifiedName(CXCursor cursor, string symbolName)
    {
        if (string.IsNullOrWhiteSpace(symbolName))
        {
            return string.Empty;
        }

        if (symbolName.Contains("::", StringComparison.Ordinal))
        {
            return symbolName;
        }

        List<string> segments = [symbolName];
        CXCursor parent = cursor.SemanticParent;
        while (!IsNullCursor(parent))
        {
            string parentKind = GetCursorKindName(parent);
            if (IsScopeCursorKind(parentKind))
            {
                string parentName = GetCursorSpelling(parent);
                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    segments.Add(parentName);
                }
            }

            parent = parent.SemanticParent;
        }

        if (segments.Count == 1)
        {
            return symbolName;
        }

        segments.Reverse();
        return string.Join("::", segments);
    }

    private static string BuildNamespaceScopePath(CXCursor cursor)
    {
        List<string> namespaceSegments = [];
        CXCursor parent = cursor.SemanticParent;
        while (!IsNullCursor(parent))
        {
            string parentKind = GetCursorKindName(parent);
            if (string.Equals(parentKind, "Namespace", StringComparison.Ordinal))
            {
                string parentName = GetCursorSpelling(parent);
                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    namespaceSegments.Add(parentName);
                }
            }

            parent = parent.SemanticParent;
        }

        if (namespaceSegments.Count == 0)
        {
            return string.Empty;
        }

        namespaceSegments.Reverse();
        return string.Join("::", namespaceSegments);
    }

    private static string? ResolveEnclosingSymbolUsr(CXCursor cursor)
    {
        CXCursor current = cursor;
        while (!IsNullCursor(current))
        {
            if (TryMapSymbolKind(GetCursorKindName(current), out _))
            {
                string candidateUsr = GetCursorUsr(current);
                if (!string.IsNullOrWhiteSpace(candidateUsr))
                {
                    return candidateUsr;
                }
            }

            current = current.SemanticParent;
        }

        return null;
    }

    private static bool TryGetCursorSourcePosition(
        CXCursor cursor,
        out string filePath,
        out int lineNumber)
    {
        filePath = string.Empty;
        lineNumber = 0;

        CXSourceLocation location = cursor.Location;
        location.GetExpansionLocation(
            out CXFile file,
            out uint line,
            out _,
            out _);
        if (file.Handle == IntPtr.Zero || line == 0)
        {
            return false;
        }

        string rawFilePath = GetClangString(file.Name);
        if (string.IsNullOrWhiteSpace(rawFilePath))
        {
            return false;
        }

        filePath = NormalizePath(rawFilePath);
        lineNumber = (int)line;
        return true;
    }

    private static string GetCursorSpelling(CXCursor cursor)
    {
        return GetClangString(cursor.Spelling);
    }

    private static string GetCursorDisplayName(CXCursor cursor)
    {
        return GetClangString(cursor.DisplayName);
    }

    private static string GetCursorUsr(CXCursor cursor)
    {
        return GetClangString(cursor.Usr);
    }

    private static string GetCursorKindName(CXCursor cursor)
    {
        return GetClangString(cursor.KindSpelling);
    }

    private static bool IsScopeCursorKind(string cursorKind)
    {
        return cursorKind switch
        {
            "Namespace" or
            "ClassDecl" or
            "ClassTemplate" or
            "StructDecl" or
            "UnionDecl" or
            "EnumDecl" => true,
            _ => false
        };
    }

    private static bool IsNullCursor(CXCursor cursor)
    {
        return cursor.IsNull;
    }

    private static string ExtractSimpleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int separatorIndex = value.LastIndexOf("::", StringComparison.Ordinal);
        string simpleName = separatorIndex >= 0
            ? value[(separatorIndex + 2)..]
            : value;
        return simpleName.Trim();
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Trim();
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(left, right, comparison);
    }

    private static string GetClangString(CXString value)
    {
        try
        {
            return value.ToString();
        }
        finally
        {
            value.Dispose();
        }
    }

    private sealed class AstTraversalContext
    {
        private readonly string _sourceFilePath;
        private readonly List<LibClangSymbolObservation> _symbols = [];
        private readonly List<LibClangReferenceObservation> _references = [];
        private readonly HashSet<string> _symbolKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _referenceKeys = new(StringComparer.Ordinal);

        public AstTraversalContext(string sourceFilePath)
        {
            _sourceFilePath = sourceFilePath;
        }

        public void Visit(CXCursor cursor, CXCursor parent)
        {
            _ = parent;

            if (!TryGetCursorSourcePosition(cursor, out string filePath, out int lineNumber) ||
                !PathsEqual(filePath, _sourceFilePath))
            {
                return;
            }

            string cursorKind = GetCursorKindName(cursor);
            if (TryMapSymbolKind(cursorKind, out string symbolKind))
            {
                CollectSymbol(cursor, symbolKind, lineNumber);
            }

            CollectReference(cursor, cursorKind, lineNumber);
        }

        public IReadOnlyList<LibClangSymbolObservation> GetSymbols()
        {
            return _symbols
                .OrderBy(symbol => symbol.LineNumber)
                .ThenBy(symbol => symbol.QualifiedName, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<LibClangReferenceObservation> GetReferences()
        {
            return _references
                .OrderBy(reference => reference.LineNumber)
                .ThenBy(reference => reference.TargetName, StringComparer.Ordinal)
                .ToArray();
        }

        private void CollectSymbol(CXCursor cursor, string symbolKind, int lineNumber)
        {
            string cursorName = GetCursorSpelling(cursor);
            if (string.IsNullOrWhiteSpace(cursorName))
            {
                cursorName = GetCursorDisplayName(cursor);
            }

            if (string.IsNullOrWhiteSpace(cursorName) &&
                string.Equals(GetCursorKindName(cursor), "Constructor", StringComparison.Ordinal))
            {
                CXCursor semanticParent = cursor.SemanticParent;
                if (!IsNullCursor(semanticParent))
                {
                    cursorName = GetCursorSpelling(semanticParent);
                }
            }

            string symbolName = ExtractSimpleName(cursorName);
            if (string.IsNullOrWhiteSpace(symbolName))
            {
                return;
            }

            string qualifiedName = BuildQualifiedName(cursor, symbolName);
            string namespaceScopePath = BuildNamespaceScopePath(cursor);
            string usr = GetCursorUsr(cursor);
            bool isDefinition = cursor.IsDefinition;
            string symbolKey = $"{usr}|{lineNumber}|{symbolKind}|{qualifiedName}";
            if (!_symbolKeys.Add(symbolKey))
            {
                return;
            }

            _symbols.Add(new LibClangSymbolObservation(
                usr,
                symbolKind,
                symbolName,
                string.IsNullOrWhiteSpace(qualifiedName) ? symbolName : qualifiedName,
                namespaceScopePath,
                lineNumber,
                isDefinition));
        }

        private void CollectReference(CXCursor cursor, string cursorKind, int lineNumber)
        {
            CXCursor referenced = cursor.Referenced;
            if (IsNullCursor(referenced))
            {
                return;
            }

            string targetUsr = GetCursorUsr(referenced);
            string targetName = GetCursorSpelling(referenced);
            if (string.IsNullOrWhiteSpace(targetName))
            {
                targetName = GetCursorDisplayName(referenced);
            }

            if (string.IsNullOrWhiteSpace(targetName))
            {
                return;
            }

            string qualifiedTargetName = BuildQualifiedName(referenced, ExtractSimpleName(targetName));
            string sourceUsr = ResolveEnclosingSymbolUsr(cursor) ?? string.Empty;
            string resolvedTargetName = string.IsNullOrWhiteSpace(qualifiedTargetName)
                ? targetName
                : qualifiedTargetName;
            string referenceKind = ResolveReferenceKind(cursorKind);
            string referenceKey = $"{sourceUsr}|{targetUsr}|{resolvedTargetName}|{lineNumber}|{referenceKind}";
            if (!_referenceKeys.Add(referenceKey))
            {
                return;
            }

            _references.Add(new LibClangReferenceObservation(
                string.IsNullOrWhiteSpace(sourceUsr) ? null : sourceUsr,
                string.IsNullOrWhiteSpace(targetUsr) ? null : targetUsr,
                resolvedTargetName,
                lineNumber,
                referenceKind));
        }
    }
}

internal sealed record LibClangDocumentAnalysisResult(
    IReadOnlyList<LibClangSymbolObservation> Symbols,
    IReadOnlyList<LibClangReferenceObservation> References);

internal sealed record LibClangSymbolObservation(
    string? Usr,
    string Kind,
    string Name,
    string QualifiedName,
    string NamespaceScopePath,
    int LineNumber,
    bool IsDefinition);

internal sealed record LibClangReferenceObservation(
    string? SourceUsr,
    string? TargetUsr,
    string TargetName,
    int LineNumber,
    string ReferenceKind);
