using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CodeMap.Analysis;

public sealed partial class CppAnalysisService
{
    internal static IReadOnlyList<string> TestLoadProjectDocumentPaths(
        string projectPath,
        string workspaceRootPath,
        string? solutionPath = null)
    {
        XDocument projectDocument = XDocument.Load(projectPath);
        string projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project directory not found: {projectPath}");
        string projectName = ResolveProjectName(projectDocument, projectPath);
        ProjectXmlDocument rootProjectDocument = new(
            Path.GetFullPath(projectPath),
            projectDirectory,
            projectDocument);
        MsBuildEvaluationContext evaluationContext = CreateEvaluationContext(
            projectDocument,
            [rootProjectDocument],
            projectPath,
            workspaceRootPath,
            projectName,
            solutionPath);
        List<string> diagnostics = [];
        return LoadProjectDocuments(rootProjectDocument, evaluationContext, diagnostics)
            .Select(document => document.FilePath)
            .ToArray();
    }

    internal static bool TestEvaluateMsBuildCondition(
        string condition,
        string projectPath,
        string workspaceRootPath,
        string? solutionPath = null)
    {
        XDocument projectDocument =
            File.Exists(projectPath)
                ? XDocument.Load(projectPath)
                : new XDocument(new XElement("Project"));
        string projectDirectory = Path.GetDirectoryName(projectPath) ?? workspaceRootPath;
        string projectName = ResolveProjectName(projectDocument, projectPath);
        ProjectXmlDocument rootProjectDocument = new(
            Path.GetFullPath(projectPath),
            projectDirectory,
            projectDocument);
        MsBuildEvaluationContext evaluationContext = CreateEvaluationContext(
            projectDocument,
            [rootProjectDocument],
            projectPath,
            workspaceRootPath,
            projectName,
            solutionPath);
        return EvaluateMsBuildCondition(condition, evaluationContext, projectDirectory);
    }

    internal static bool TestTryResolveEvaluationProperty(
        string projectPath,
        string workspaceRootPath,
        string propertyName,
        out string? propertyValue,
        string? solutionPath = null)
    {
        XDocument projectDocument =
            File.Exists(projectPath)
                ? XDocument.Load(projectPath)
                : new XDocument(new XElement("Project"));
        string projectDirectory = Path.GetDirectoryName(projectPath) ?? workspaceRootPath;
        string projectName = ResolveProjectName(projectDocument, projectPath);
        ProjectXmlDocument rootProjectDocument = new(
            Path.GetFullPath(projectPath),
            projectDirectory,
            projectDocument);
        MsBuildEvaluationContext evaluationContext = CreateEvaluationContext(
            projectDocument,
            [rootProjectDocument],
            projectPath,
            workspaceRootPath,
            projectName,
            solutionPath);
        bool found = evaluationContext.TryResolveProperty(propertyName, out string resolvedValue);
        propertyValue = found ? resolvedValue : null;
        return found;
    }
}
