using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CodeMap.Analysis;

internal static class AnalysisIdBuilder
{
    public static string BuildDocumentId(string projectIdentity, string? filePath, string documentName)
    {
        string identity = !string.IsNullOrWhiteSpace(filePath)
            ? $"{projectIdentity}|{Path.GetFullPath(filePath)}"
            : $"{projectIdentity}/{documentName}";

        return $"document:{Normalize(identity)}";
    }

    public static string BuildSymbolId(string symbolName, string symbolKey)
    {
        string nameToken = string.IsNullOrWhiteSpace(symbolName)
            ? "symbol"
            : NormalizeIdentifierFragment(symbolName);

        return $"symbol:{nameToken}_{ComputeHashToken(symbolKey)}";
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "empty_00000000";
        }

        StringBuilder builder = new(value.Length + 9);
        foreach (char ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        if (builder.Length == 0)
        {
            builder.Append("empty");
        }

        builder.Append('_');
        builder.Append(ComputeHashToken(value));
        return builder.ToString();
    }

    public static string NormalizeIdentifierFragment(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (ch is '_' or '.')
            {
                builder.Append('_');
            }
        }

        if (builder.Length == 0)
        {
            builder.Append("symbol");
        }

        return builder.ToString();
    }

    public static string ComputeHashToken(string value)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes.AsSpan(0, 4)).ToLowerInvariant();
    }
}

internal static class DependencySampleHelper
{
    public static string? ExtractLineSnippet(SourceText sourceText, int lineNumber)
    {
        if (lineNumber <= 0 || lineNumber > sourceText.Lines.Count)
        {
            return null;
        }

        return NormalizeSnippet(sourceText.Lines[lineNumber - 1].ToString());
    }

    public static string? ExtractLineSnippet(IReadOnlyList<string> lines, int lineNumber)
    {
        if (lineNumber <= 0 || lineNumber > lines.Count)
        {
            return null;
        }

        return NormalizeSnippet(lines[lineNumber - 1]);
    }

    public static string? NormalizeSnippet(string? snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
        {
            return null;
        }

        string trimmed = snippet.Trim();
        return trimmed.Length <= 180
            ? trimmed
            : $"{trimmed[..177]}...";
    }

    public static void TrySetSample<TKey>(
        IDictionary<TKey, AnalysisDependencySample> samples,
        TKey key,
        string? filePath,
        int? lineNumber,
        string? snippet)
        where TKey : notnull
    {
        if (samples.ContainsKey(key))
        {
            return;
        }

        string? normalizedFilePath = string.IsNullOrWhiteSpace(filePath)
            ? null
            : filePath;
        int? normalizedLineNumber = lineNumber > 0
            ? lineNumber
            : null;
        string? normalizedSnippet = NormalizeSnippet(snippet);
        if (normalizedFilePath is null && normalizedLineNumber is null && normalizedSnippet is null)
        {
            return;
        }

        samples[key] = new AnalysisDependencySample(normalizedFilePath, normalizedLineNumber, normalizedSnippet);
    }
}

internal readonly record struct AnalysisDependencySample(
    string? FilePath,
    int? LineNumber,
    string? Snippet);
