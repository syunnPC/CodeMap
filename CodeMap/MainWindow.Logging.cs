namespace CodeMap;

public sealed partial class MainWindow
{
    private static void LogInfo(string message)
    {
        WriteLogMessage(message, isError: false);
    }

    private static void LogError(string message)
    {
        WriteLogMessage(message, isError: true);
    }

    private static void WriteLogMessage(string message, bool isError)
    {
        try
        {
            s_asyncFileLogger.Value.Log(message, isError);
        }
        catch
        {
            // Logging failure should not affect app behavior.
        }
    }

    private static void DisposeLogger()
    {
        if (!s_asyncFileLogger.IsValueCreated)
        {
            return;
        }

        s_asyncFileLogger.Value.Dispose();
    }
}
