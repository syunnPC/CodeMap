using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CodeMap.Diagnostics;

internal static partial class ConsoleBootstrap
{
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private static bool s_initialized;

    public static void EnsureConsole(bool isEnabled)
    {
        if (s_initialized)
        {
            return;
        }

        s_initialized = true;
        if (!isEnabled)
        {
            return;
        }

        try
        {
            _ = AttachConsole(AttachParentProcess) || AllocConsole();

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch
        {
            // Console allocation failure should never break app startup.
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint dwProcessId);
}
