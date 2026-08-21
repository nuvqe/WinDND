using WinDND.Audio;
using WinDND.Core.Models;
using WinDND.Infrastructure;
using WinDND.UI;

namespace WinDND;

internal static class Program
{
    private const string MutexName = "Local\\Nuvqe.WinDND.SingleInstance";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--check-audio", StringComparison.OrdinalIgnoreCase))
        {
            return CheckAudio();
        }

        if (args.Length == 1 && args[0].Equals("--check-monitor-lifecycle", StringComparison.OrdinalIgnoreCase))
        {
            return CheckMonitorLifecycle();
        }

        if (args.Length == 1 && args[0].Equals("--check-microphone", StringComparison.OrdinalIgnoreCase))
        {
            return CheckMicrophone();
        }

        using Mutex mutex = new(initiallyOwned: true, MutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        EventViewerLogger logger = new();
        Application.ThreadException += (_, args) => logger.Error("Unhandled UI error.", args.Exception, 2001);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            logger.Error("Unhandled application error.", args.ExceptionObject as Exception, 2002);

        using TrayApplicationContext context = new(logger);
        Application.Run(context);
        return 0;
    }

    private static int CheckAudio()
    {
        EventViewerLogger logger = new();
        try
        {
            int endpointCount = new AudioController().VerifyAccess();
            logger.Information($"Audio diagnostic succeeded for {endpointCount} active output endpoint(s).", 1400);
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error("Audio diagnostic failed.", ex, 2400);
            return 1;
        }
    }

    private static int CheckMonitorLifecycle()
    {
        EventViewerLogger logger = new();
        try
        {
            using AudioDeviceMonitor monitor = new(() => { });
            int endpointCount = new AudioController().VerifyAccess();
            logger.Information(
                $"Audio monitor lifecycle diagnostic succeeded for {endpointCount} active output endpoint(s).",
                1401);
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error("Audio monitor lifecycle diagnostic failed.", ex, 2401);
            return 1;
        }
    }

    private static int CheckMicrophone()
    {
        EventViewerLogger logger = new();
        try
        {
            AudioController audio = new();
            int endpointCount = audio.VerifyMicrophoneAccess(MicrophoneScope.AllActive);
            bool muted = audio.AreAllMicrophonesMuted(MicrophoneScope.AllActive);
            logger.Information(
                $"Microphone diagnostic succeeded for {endpointCount} active input endpoint(s); all muted: {muted}.",
                1402);
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error("Microphone diagnostic failed.", ex, 2402);
            return 1;
        }
    }
}
