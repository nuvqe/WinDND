using System.Runtime.InteropServices;
using WinDND.Core.Models;

namespace WinDND.Audio;

internal sealed class AudioController
{
    private const uint DeviceStateActive = 0x1;
    private static readonly Guid EndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid EventContext = new("04B31C46-6009-48CE-A65A-1EAB459A55B9");
    private readonly object _sync = new();
    private Dictionary<string, AudioEndpointState>? _outputSnapshot;
    private Dictionary<string, AudioEndpointState>? _microphoneSnapshot;

    public bool AreAllMuted()
    {
        lock (_sync)
        {
            IReadOnlyList<AudioEndpointState> states = ReadStates(EDataFlow.Render);
            return states.Count > 0 && states.All(state => state.Muted);
        }
    }

    public int VerifyAccess()
    {
        lock (_sync)
        {
            int count = ReadStates(EDataFlow.Render).Count;
            EnsureEndpoints(count);
            return count;
        }
    }

    public bool ToggleManualMute()
    {
        lock (_sync)
        {
            IReadOnlyList<AudioEndpointState> current = ReadStates(EDataFlow.Render);
            EnsureEndpoints(current.Count);

            if (!current.All(state => state.Muted))
            {
                _outputSnapshot = current.ToDictionary(state => state.Id, StringComparer.Ordinal);
                ForEachEndpoint(EDataFlow.Render, (_, volume) => SetMute(volume, true));
                return true;
            }

            Dictionary<string, AudioEndpointState>? snapshot = _outputSnapshot;
            ForEachEndpoint(EDataFlow.Render, (id, volume) =>
            {
                if (snapshot is not null && snapshot.TryGetValue(id, out AudioEndpointState? previous))
                {
                    SetVolume(volume, previous.Volume);
                    SetMute(volume, previous.Muted);
                }
                else
                {
                    SetMute(volume, false);
                }
            });
            _outputSnapshot = null;
            return false;
        }
    }

    public int VerifyMicrophoneAccess(MicrophoneScope scope)
    {
        lock (_sync)
        {
            int count = ReadMicrophoneStates(scope).Count;
            EnsureEndpoints(count, "audio input");
            return count;
        }
    }

    public bool AreAllMicrophonesMuted(MicrophoneScope scope)
    {
        lock (_sync)
        {
            IReadOnlyList<AudioEndpointState> states = ReadMicrophoneStates(scope);
            return states.Count > 0 && states.All(state => state.Muted);
        }
    }

    public bool ToggleManualMicrophoneMute(MicrophoneScope scope)
    {
        lock (_sync)
        {
            IReadOnlyList<AudioEndpointState> current = ReadMicrophoneStates(scope);
            EnsureEndpoints(current.Count, "audio input");

            if (!current.All(state => state.Muted))
            {
                _microphoneSnapshot = current.ToDictionary(state => state.Id, StringComparer.Ordinal);
                ForEachMicrophone(scope, (_, volume) => SetMute(volume, true));
                return true;
            }

            Dictionary<string, AudioEndpointState>? snapshot = _microphoneSnapshot;
            ForEachMicrophone(scope, (id, volume) =>
            {
                bool muted = snapshot is not null && snapshot.TryGetValue(id, out AudioEndpointState? previous)
                    ? previous.Muted
                    : false;
                SetMute(volume, muted);
            });
            _microphoneSnapshot = null;
            return false;
        }
    }

    public void Apply(SoundAction action)
    {
        lock (_sync)
        {
            int count = 0;
            ForEachEndpoint(EDataFlow.Render, (_, volume) =>
            {
                count++;
                switch (action.Kind)
                {
                    case SoundActionKind.Mute:
                        SetMute(volume, true);
                        break;
                    case SoundActionKind.Unmute:
                        SetMute(volume, false);
                        break;
                    case SoundActionKind.SetVolume:
                        SetVolume(volume, action.Volume / 100f);
                        SetMute(volume, false);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported sound action.");
                }
            });
            EnsureEndpoints(count);
            _outputSnapshot = null;
        }
    }

    private static void EnsureEndpoints(int count, string endpointType = "audio output")
    {
        if (count == 0)
        {
            throw new InvalidOperationException($"Windows has no active {endpointType} endpoint.");
        }
    }

    private static void SetMute(IAudioEndpointVolume volume, bool muted)
    {
        Guid eventContext = EventContext;
        volume.SetMute(muted, ref eventContext);
    }

    private static void SetVolume(IAudioEndpointVolume volume, float level)
    {
        Guid eventContext = EventContext;
        volume.SetMasterVolumeLevelScalar(level, ref eventContext);
    }

    private static IReadOnlyList<AudioEndpointState> ReadStates(EDataFlow flow)
    {
        List<AudioEndpointState> result = [];
        ForEachEndpoint(flow, (id, volume) =>
        {
            volume.GetMute(out bool muted);
            volume.GetMasterVolumeLevelScalar(out float level);
            result.Add(new AudioEndpointState(id, muted, level));
        });
        return result;
    }

    private static IReadOnlyList<AudioEndpointState> ReadMicrophoneStates(MicrophoneScope scope)
    {
        List<AudioEndpointState> result = [];
        ForEachMicrophone(scope, (id, volume) =>
        {
            volume.GetMute(out bool muted);
            volume.GetMasterVolumeLevelScalar(out float level);
            result.Add(new AudioEndpointState(id, muted, level));
        });
        return result;
    }

    private static void ForEachMicrophone(MicrophoneScope scope, Action<string, IAudioEndpointVolume> action)
    {
        if (scope == MicrophoneScope.DefaultCommunications)
        {
            ForDefaultEndpoint(EDataFlow.Capture, ERole.Communications, action);
            return;
        }

        ForEachEndpoint(EDataFlow.Capture, action);
    }

    private static void ForEachEndpoint(EDataFlow flow, Action<string, IAudioEndpointVolume> action)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
            enumerator.EnumAudioEndpoints(flow, DeviceStateActive, out collection);
            collection.GetCount(out uint count);

            for (uint index = 0; index < count; index++)
            {
                IMMDevice? device = null;
                IAudioEndpointVolume? volume = null;
                try
                {
                    collection.Item(index, out device);
                    device.GetId(out string id);
                    Guid interfaceId = EndpointVolumeId;
                    device.Activate(ref interfaceId, ClsCtx.All, IntPtr.Zero, out object instance);
                    volume = (IAudioEndpointVolume)instance;
                    action(id, volume);
                }
                finally
                {
                    Release(volume);
                    Release(device);
                }
            }
        }
        finally
        {
            Release(collection);
            Release(enumerator);
        }
    }

    private static void ForDefaultEndpoint(
        EDataFlow flow,
        ERole role,
        Action<string, IAudioEndpointVolume> action)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? volume = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(flow, role, out device);
            device.GetId(out string id);
            Guid interfaceId = EndpointVolumeId;
            device.Activate(ref interfaceId, ClsCtx.All, IntPtr.Zero, out object instance);
            volume = (IAudioEndpointVolume)instance;
            action(id, volume);
        }
        finally
        {
            Release(volume);
            Release(device);
            Release(enumerator);
        }
    }

    private static void Release(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            _ = Marshal.ReleaseComObject(instance);
        }
    }
}
