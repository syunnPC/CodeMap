using System;
using System.IO;
using CodeMap.Analysis;
using Xunit;

namespace CodeMap.Tests.Analysis;

public sealed class AnalysisIdentityTests
{
    [Fact]
    public void BuildProjectKey_ProjectFilePathProvided_ReturnsNormalizedFullPath()
    {
        string projectFilePath = Path.Combine("temp", "..", "src", "project.csproj");
        string expected = Path.GetFullPath(projectFilePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            expected = expected.ToUpperInvariant();
        }

        string actual = AnalysisIdentity.BuildProjectKey("ignored", projectFilePath);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildProjectKey_ProjectPathResolutionFails_ReturnsOriginalPath()
    {
        string invalidPath = $"invalid\0{Guid.NewGuid():N}.csproj";

        string actual = AnalysisIdentity.BuildProjectKey("ignored", invalidPath);

        Assert.Equal(invalidPath, actual);
    }

    [Fact]
    public void BuildProjectKey_FallbackIdentityProvided_ReturnsIdPrefix()
    {
        string actual = AnalysisIdentity.BuildProjectKey("project", projectFilePath: null, fallbackIdentity: "  custom-id  ");

        Assert.Equal("id:custom-id", actual);
    }

    [Fact]
    public void BuildProjectKey_FallbackIdentityMissing_UsesProjectName()
    {
        string actual = AnalysisIdentity.BuildProjectKey(" SampleProject ", projectFilePath: null, fallbackIdentity: " ");

        Assert.Equal("name:SampleProject", actual);
    }

    [Fact]
    public void BuildProjectKey_ProjectNameMissing_UsesProjectLiteral()
    {
        string actual = AnalysisIdentity.BuildProjectKey(" ", projectFilePath: null, fallbackIdentity: null);

        Assert.Equal("name:project", actual);
    }
}
