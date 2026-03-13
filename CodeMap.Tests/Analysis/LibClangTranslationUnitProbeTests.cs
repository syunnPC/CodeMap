using CodeMap.Analysis;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class LibClangTranslationUnitProbeTests
{
    [Fact]
    public void TryParse_SourceFileMissing_ReturnsFalseWithMessage()
    {
        bool result = LibClangTranslationUnitProbe.TryParse(
            sourceFilePath: @"C:\not-found\missing.cpp",
            includeDirectories: [],
            parseArguments: null,
            out string message);

        Assert.False(result);
        Assert.Equal("source file was not found", message);
    }

    [Fact]
    public void TryAnalyzeDocument_SourceFileMissing_ReturnsFalseWithMessageAndEmptyResult()
    {
        bool result = LibClangTranslationUnitProbe.TryAnalyzeDocument(
            sourceFilePath: @"C:\not-found\missing.cpp",
            includeDirectories: [],
            parseArguments: null,
            out LibClangDocumentAnalysisResult analysisResult,
            out string message);

        Assert.False(result);
        Assert.Equal("source file was not found", message);
        Assert.Empty(analysisResult.Symbols);
        Assert.Empty(analysisResult.References);
    }
}
