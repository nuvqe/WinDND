using System.Diagnostics;
using System.Globalization;

namespace WinDND.Infrastructure;

internal sealed class EventViewerLogger : IAppLogger
{
    internal const string SourceName = "WinDND";
    private readonly object _sync = new();
    private readonly string _fallbackPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinDND",
        "WinDND.log");

    public void Information(string message, int eventId = 1000) =>
        Write(message, EventLogEntryType.Information, eventId);

    public void Error(string message, Exception? exception = null, int eventId = 2000)
    {
        string detail = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write(detail, EventLogEntryType.Error, eventId);
    }

    private void Write(string message, EventLogEntryType type, int eventId)
    {
        lock (_sync)
        {
            try
            {
                EventLog.WriteEntry(SourceName, message, type, eventId);
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.Security.SecurityException)
            {
                WriteFallback(message, type, eventId, ex);
            }
        }
    }

    private void WriteFallback(string message, EventLogEntryType type, int eventId, Exception eventLogError)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_fallbackPath)!);
            string line = string.Create(
                CultureInfo.InvariantCulture,
                $"{DateTimeOffset.Now:O} [{type}] {eventId}: {message}{Environment.NewLine}" +
                $"Event Viewer fallback reason: {eventLogError.Message}{Environment.NewLine}");
            File.AppendAllText(_fallbackPath, line);
        }
        catch
        {
            // Logging must never terminate the tray process.
        }
    }
}
