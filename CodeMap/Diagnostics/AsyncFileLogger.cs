using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CodeMap.Diagnostics;

internal sealed class AsyncFileLogger : IDisposable
{
    private readonly string _logFilePath;
    private readonly Channel<LogEntry> _channel;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource = new();
    private readonly Task _writerTask;
    private int _disposed;

    public AsyncFileLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        _channel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = Task.Run(WriteLoopAsync);
    }

    public void Log(string? message, bool isError)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        string normalized = string.IsNullOrEmpty(message)
            ? string.Empty
            : message.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] messageLines = normalized.Split('\n');
        if (messageLines.Length == 0)
        {
            messageLines = [string.Empty];
        }

        foreach (string messageLine in messageLines)
        {
            string line = isError
                ? $"[{DateTime.Now:HH:mm:ss}] [CodeMap][Error] {messageLine}"
                : $"[{DateTime.Now:HH:mm:ss}] [CodeMap] {messageLine}";

            // Keep console/debug output immediate while file I/O remains asynchronous.
            if (isError)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }

            System.Diagnostics.Debug.WriteLine(line);

            _ = _channel.Writer.TryWrite(new LogEntry(line));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _channel.Writer.TryComplete();
        _shutdownCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            _writerTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Logging shutdown should not affect app behavior.
        }
        finally
        {
            _shutdownCancellationTokenSource.Dispose();
        }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            string? directoryPath = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            await using FileStream stream = new(
                _logFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                4096,
                useAsync: true);
            await using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            await foreach (LogEntry entry in _channel.Reader.ReadAllAsync(_shutdownCancellationTokenSource.Token))
            {
                await writer.WriteLineAsync(entry.Line);
            }

            await writer.FlushAsync();
        }
        catch (OperationCanceledException)
        {
            // Shutdown timeout reached; discard remaining buffered logs.
        }
        catch
        {
            // Logging failures should not affect app behavior.
        }
    }

    private sealed record LogEntry(string Line);
}
