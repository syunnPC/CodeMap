namespace CodeMap.ViewModels;

using System;
using CodeMap;

public sealed class SymbolListItem
{
    private const string ClassGlyph = "\uE943";
    private const string StructGlyph = "\uEA86";
    private const string InterfaceGlyph = "\uE8AB";
    private const string EnumGlyph = "\uE8EF";
    private const string MethodGlyph = "\uE7C1";
    private const string ConstructorGlyph = "\uE710";
    private const string FieldGlyph = "\uE71A";
    private const string DefaultGlyph = "\uE946";

    public SymbolListItem(
        string symbolId,
        string projectName,
        string documentName,
        string kind,
        string symbolName,
        string symbolDisplayName,
        int lineNumber)
    {
        SymbolId = symbolId;
        ProjectName = projectName;
        DocumentName = documentName;
        Kind = kind;
        SymbolName = symbolName;
        SymbolDisplayName = symbolDisplayName;
        LineNumber = lineNumber;
    }

    public string SymbolId { get; }

    public string ProjectName { get; }

    public string DocumentName { get; }

    public string Kind { get; }

    public string SymbolName { get; }

    public string SymbolDisplayName { get; }

    public int LineNumber { get; }

    public string DisplayText => $"{SymbolDisplayName} [{GetKindLabel(Kind)}]";

    public string DetailText => $"{ProjectName} / {DocumentName}:{LineNumber}";

    public string IconGlyph => Kind switch
    {
        "ClassDeclaration" => ClassGlyph,
        "StructDeclaration" => StructGlyph,
        "InterfaceDeclaration" => InterfaceGlyph,
        "EnumDeclaration" => EnumGlyph,
        "UnionDeclaration" => StructGlyph,
        "MethodDeclaration" => MethodGlyph,
        "FunctionDeclaration" => MethodGlyph,
        "ConstructorDeclaration" => ConstructorGlyph,
        "PropertyDeclaration" => MethodGlyph,
        "FieldDeclaration" => FieldGlyph,
        "EventDeclaration" => StructGlyph,
        "DelegateDeclaration" => InterfaceGlyph,
        "TypeAliasDeclaration" => InterfaceGlyph,
        "MacroDefinition" => FieldGlyph,
        _ => DefaultGlyph
    };

    public bool MatchesQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return
            ContainsIgnoreCase(SymbolDisplayName, query) ||
            ContainsIgnoreCase(SymbolName, query) ||
            ContainsIgnoreCase(ProjectName, query) ||
            ContainsIgnoreCase(DocumentName, query) ||
            ContainsIgnoreCase(GetKindLabel(Kind), query) ||
            ContainsIgnoreCase(Kind, query);
    }

    private static bool ContainsIgnoreCase(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetKindLabel(string kind)
    {
        return AppLocalization.GetSymbolKindLabel(kind);
    }
}
