using System.Collections.Generic;
using System.IO;
using CodeMap.Analysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class AnalysisUtilitiesTests
{
    [Fact]
    public void BuildDocumentId_FilePathProvided_UsesNormalizedFullPathIdentity()
    {
        string projectIdentity = "project-key";
        string filePath = Path.Combine(".", "src", "..", "Sample.cs");
        string sourceIdentity = $"{projectIdentity}|{Path.GetFullPath(filePath)}";
        string expectedHash = AnalysisIdBuilder.ComputeHashToken(sourceIdentity);

        string documentId = AnalysisIdBuilder.BuildDocumentId(projectIdentity, filePath, "Sample.cs");

        Assert.StartsWith("document:", documentId);
        Assert.EndsWith(expectedHash, documentId);
    }

    [Fact]
    public void BuildSymbolId_UsesNormalizedNameAndHash()
    {
        string symbolId = AnalysisIdBuilder.BuildSymbolId("My.Type", "assembly|type|My.Type");

        Assert.StartsWith("symbol:my_type_", symbolId);
        Assert.Equal($"symbol:my_type_{AnalysisIdBuilder.ComputeHashToken("assembly|type|My.Type")}", symbolId);
    }

    [Fact]
    public void Normalize_WhitespaceValue_ReturnsEmptyToken()
    {
        Assert.Equal("empty_00000000", AnalysisIdBuilder.Normalize(" \t "));
    }

    [Fact]
    public void NormalizeIdentifierFragment_RemovesUnsupportedCharacters()
    {
        string fragment = AnalysisIdBuilder.NormalizeIdentifierFragment("  *My.Type+Name*  ");

        Assert.Equal("my_typename", fragment);
    }

    [Fact]
    public void ExtractLineSnippet_FromSourceTextAndList_UsesSameNormalization()
    {
        SourceText sourceText = SourceText.From("line1\n    line2 with spaces    \nline3");
        string[] lines = ["line1", "    line2 with spaces    ", "line3"];

        Assert.Equal("line2 with spaces", DependencySampleHelper.ExtractLineSnippet(sourceText, 2));
        Assert.Equal("line2 with spaces", DependencySampleHelper.ExtractLineSnippet(lines, 2));
        Assert.Null(DependencySampleHelper.ExtractLineSnippet(sourceText, 99));
        Assert.Null(DependencySampleHelper.ExtractLineSnippet(lines, 0));
    }

    [Fact]
    public void TrySetSample_IgnoresEmptyAndDuplicateEntries()
    {
        Dictionary<string, AnalysisDependencySample> samples = new();

        DependencySampleHelper.TrySetSample(samples, "edge", null, null, null);
        Assert.Empty(samples);

        DependencySampleHelper.TrySetSample(samples, "edge", @"C:\repo\file.cs", 42, "  sample();  ");
        Assert.Single(samples);
        AnalysisDependencySample sample = samples["edge"];
        Assert.Equal(@"C:\repo\file.cs", sample.FilePath);
        Assert.Equal(42, sample.LineNumber);
        Assert.Equal("sample();", sample.Snippet);

        DependencySampleHelper.TrySetSample(samples, "edge", @"C:\repo\other.cs", 7, "other();");
        sample = samples["edge"];
        Assert.Equal(@"C:\repo\file.cs", sample.FilePath);
        Assert.Equal(42, sample.LineNumber);
        Assert.Equal("sample();", sample.Snippet);
    }
}
