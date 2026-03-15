using System.Collections.Generic;
using System.Text;

namespace CodeMap.Analysis;

internal static class CppSourceTextStripper
{
    public static string[] StripCommentsAndStrings(IReadOnlyList<string> rawLines)
    {
        return StripComments(rawLines, preserveStrings: false);
    }

    public static string[] StripCommentsPreservingStrings(IReadOnlyList<string> rawLines)
    {
        return StripComments(rawLines, preserveStrings: true);
    }

    private static string[] StripComments(IReadOnlyList<string> rawLines, bool preserveStrings)
    {
        bool inBlockComment = false;
        string[] strippedLines = new string[rawLines.Count];

        for (int lineIndex = 0; lineIndex < rawLines.Count; lineIndex++)
        {
            string line = rawLines[lineIndex];
            StringBuilder builder = new(line.Length);
            bool inString = false;
            char stringDelimiter = '\0';

            for (int index = 0; index < line.Length; index++)
            {
                char current = line[index];
                char next = index + 1 < line.Length ? line[index + 1] : '\0';

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        index++;
                    }

                    continue;
                }

                if (inString)
                {
                    if (preserveStrings)
                    {
                        builder.Append(current);
                        if (current == '\\' && index + 1 < line.Length)
                        {
                            builder.Append(line[index + 1]);
                            index++;
                            continue;
                        }
                    }
                    else if (current == '\\' && index + 1 < line.Length)
                    {
                        builder.Append(' ');
                        builder.Append(' ');
                        index++;
                        continue;
                    }

                    if (current == stringDelimiter)
                    {
                        inString = false;
                        stringDelimiter = '\0';
                    }

                    if (!preserveStrings)
                    {
                        builder.Append(' ');
                    }

                    continue;
                }

                if (current == '/' && next == '/')
                {
                    break;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    index++;
                    continue;
                }

                if (current == '"' || current == '\'')
                {
                    inString = true;
                    stringDelimiter = current;
                    builder.Append(preserveStrings ? current : ' ');
                    continue;
                }

                builder.Append(current);
            }

            strippedLines[lineIndex] = builder.ToString();
        }

        return strippedLines;
    }
}
