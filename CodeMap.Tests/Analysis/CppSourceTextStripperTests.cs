using CodeMap.Analysis;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class CppSourceTextStripperTests
{
    [Fact]
    public void StripCommentsAndStrings_RemovesCommentsAndMasksStringContents()
    {
        string[] lines =
        [
            "int value = 1; // inline comment",
            "const char* name = \"ab\\\\\\\"c\"; /* block */ int next = 2;"
        ];

        string[] stripped = CppSourceTextStripper.StripCommentsAndStrings(lines);

        Assert.Equal("int value = 1; ", stripped[0]);
        Assert.Contains("const char* name = ", stripped[1]);
        Assert.DoesNotContain("ab", stripped[1]);
        Assert.DoesNotContain("block", stripped[1]);
        Assert.Contains("int next = 2;", stripped[1]);
    }

    [Fact]
    public void StripCommentsPreservingStrings_RemovesCommentsAndKeepsStringContents()
    {
        string[] lines =
        [
            "const char* name = \"http://example\"; // comment",
            "int value = 3; /* block */ int next = 4;"
        ];

        string[] stripped = CppSourceTextStripper.StripCommentsPreservingStrings(lines);

        Assert.Equal("const char* name = \"http://example\"; ", stripped[0]);
        Assert.Equal("int value = 3;  int next = 4;", stripped[1]);
    }
}
