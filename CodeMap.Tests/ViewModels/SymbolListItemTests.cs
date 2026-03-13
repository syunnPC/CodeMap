using System.Text.RegularExpressions;
using CodeMap.ViewModels;
using Xunit;

namespace CodeMap.Tests.ViewModels;

public sealed class SymbolListItemTests
{
    [Fact]
    public void DisplayText_IncludesDisplayNameAndLocalizedKindLabel()
    {
        SymbolListItem item = new(
            symbolId: "symbol:1",
            projectName: "ProjectA",
            documentName: "File.cs",
            kind: "ClassDeclaration",
            symbolName: "MyType",
            symbolDisplayName: "MyType",
            lineNumber: 12);

        Assert.Equal("ProjectA / File.cs:12", item.DetailText);
        Assert.Matches(@"^MyType \[(Class|クラス)\]$", item.DisplayText);
    }

    [Fact]
    public void MatchesQuery_SearchesAcrossFieldsCaseInsensitive()
    {
        SymbolListItem item = new(
            symbolId: "symbol:2",
            projectName: "CoreProject",
            documentName: "Worker.cs",
            kind: "MethodDeclaration",
            symbolName: "Execute",
            symbolDisplayName: "Execute(CancellationToken)",
            lineNumber: 44);

        Assert.True(item.MatchesQuery("coreproject"));
        Assert.True(item.MatchesQuery("worker.cs"));
        Assert.True(item.MatchesQuery("execute"));
        Assert.False(item.MatchesQuery("not-found"));
    }

    [Fact]
    public void IconGlyph_UsesKnownKindMappingAndFallback()
    {
        SymbolListItem classItem = new(
            symbolId: "symbol:class",
            projectName: "P",
            documentName: "D.cs",
            kind: "ClassDeclaration",
            symbolName: "C",
            symbolDisplayName: "C",
            lineNumber: 1);
        SymbolListItem unknownItem = new(
            symbolId: "symbol:unknown",
            projectName: "P",
            documentName: "D.cs",
            kind: "UnknownKind",
            symbolName: "X",
            symbolDisplayName: "X",
            lineNumber: 1);

        Assert.Equal("\uE943", classItem.IconGlyph);
        Assert.Equal("\uE946", unknownItem.IconGlyph);
    }
}
