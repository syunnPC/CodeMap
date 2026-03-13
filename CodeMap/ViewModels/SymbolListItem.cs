namespace CodeMap.ViewModels;

using System;
using CodeMap;

public sealed class SymbolListItem
{
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
        "ClassDeclaration" => "\uE943",
        "StructDeclaration" => "\uEA86",
        "InterfaceDeclaration" => "\uE8AB",
        "EnumDeclaration" => "\uE8EF",
        "UnionDeclaration" => "\uEA86",
        "MethodDeclaration" => "\uE7C1",
        "FunctionDeclaration" => "\uE7C1",
        "ConstructorDeclaration" => "\uE710",
        "PropertyDeclaration" => "\uE7C1",
        "FieldDeclaration" => "\uE71A",
        "EventDeclaration" => "\uEA86",
        "DelegateDeclaration" => "\uE8AB",
        "TypeAliasDeclaration" => "\uE8AB",
        "MacroDefinition" => "\uE71A",
        _ => "\uE946"
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
