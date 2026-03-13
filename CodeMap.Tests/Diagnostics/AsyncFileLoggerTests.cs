using System;
using System.IO;
using System.Threading;
using CodeMap.Diagnostics;
using Xunit;

namespace CodeMap.Tests.Diagnostics;

public sealed class AsyncFileLoggerTests
{
    [Fact]
    public void Log_WritesNormalizedLinesWithErrorMarker()
    {
        using TemporaryLogDirectory temp = new();
        string logFilePath = Path.Combine(temp.RootPath, "codemap.log");

        using (AsyncFileLogger logger = new(logFilePath))
        {
            logger.Log("line1\r\nline2\rline3", isError: false);
            logger.Log("boom", isError: true);
        }

        string[] lines = File.ReadAllLines(logFilePath);
        Assert.Equal(4, lines.Length);
        Assert.Contains(lines, line => line.Contains("[CodeMap] line1", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[CodeMap] line2", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[CodeMap] line3", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("[CodeMap][Error] boom", StringComparison.Ordinal));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_AndFurtherLogCallsAreIgnored()
    {
        using TemporaryLogDirectory temp = new();
        string logFilePath = Path.Combine(temp.RootPath, "codemap.log");

        AsyncFileLogger logger = new(logFilePath);
        logger.Log("before-dispose", isError: false);
        logger.Dispose();
        logger.Dispose();

        long lengthAfterDispose = new FileInfo(logFilePath).Length;
        logger.Log("after-dispose", isError: false);

        Assert.Equal(lengthAfterDispose, new FileInfo(logFilePath).Length);
    }

    private sealed class TemporaryLogDirectory : IDisposable
    {
        public TemporaryLogDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "CodeMapLoggerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            TryDeleteDirectory(RootPath);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }
}
