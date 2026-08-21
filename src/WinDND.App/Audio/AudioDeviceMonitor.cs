using System.Runtime.InteropServices;

namespace WinDND.Audio;

internal sealed class AudioDeviceMonitor : IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly NotificationClient _client;
    private readonly System.Threading.Timer _debounceTimer;
    private bool _disposed;

    public AudioDeviceMonitor(Action endpointsChanged)
    {
        _debounceTimer = new System.Threading.Timer(_ => endpointsChanged());
        _client = new NotificationClient(QueueChange);
        _enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
        _enumerator.RegisterEndpointNotificationCallback(_client);
    }

    private void QueueChange()
    {
        if (!_disposed)
        {
            _debounceTimer.Change(TimeSpan.FromMilliseconds(750), Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer.Dispose();
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_client);
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException)
        {
            // The Windows audio service can invalidate the enumerator during shutdown.
        }
        finally
        {
            try
            {
                if (Marshal.IsComObject(_enumerator))
                {
                    _ = Marshal.ReleaseComObject(_enumerator);
                }
            }
            catch (InvalidComObjectException)
            {
                // The RCW was already disconnected; there is nothing left to release.
            }
        }
    }

    private sealed class NotificationClient(Action changed) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, uint newState)
        {
            changed();
            return 0;
        }

        public int OnDeviceAdded(string deviceId)
        {
            changed();
            return 0;
        }

        public int OnDeviceRemoved(string deviceId)
        {
            changed();
            return 0;
        }

        public int OnDefaultDeviceChanged(EDataFlow flow, int role, string? defaultDeviceId)
        {
            if (flow is EDataFlow.Render or EDataFlow.Capture or EDataFlow.All)
            {
                changed();
            }

            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }
}
