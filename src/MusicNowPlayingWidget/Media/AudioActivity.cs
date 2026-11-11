using System.Runtime.InteropServices;

namespace MusicNowPlayingWidget.Media;

/// Which processes are making sound right now, from the Windows audio sessions of every active output device
/// (players often play to an audio interface or virtual mixer rather than the default device).
public static class AudioActivity
{
    const float Silence = 0.0001f;

    /// Process IDs with sound in any output device's sessions; null when Windows audio can't be read.
    public static HashSet<int>? AudiblePids()
    {
        var pids = new HashSet<int>();
        var com = new List<object>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            com.Add(enumerator);
            Check(enumerator.EnumAudioEndpoints(0 /* eRender */, 1 /* DEVICE_STATE_ACTIVE */, out var devices));
            com.Add(devices);
            Check(devices.GetCount(out var count));
            for (uint d = 0; d < count; d++)
            {
                if (devices.Item(d, out var device) != 0) continue;
                com.Add(device);
                var iid = typeof(IAudioSessionManager2).GUID;
                if (device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var managerObj) != 0) continue;
                com.Add(managerObj);
                if (((IAudioSessionManager2)managerObj).GetSessionEnumerator(out var sessions) != 0) continue;
                com.Add(sessions);
                if (sessions.GetCount(out var n) != 0) continue;
                for (var i = 0; i < n; i++)
                {
                    if (sessions.GetSession(i, out var control) != 0) continue;
                    com.Add(control);
                    if (control.GetProcessId(out var pid) != 0 || pid == 0) continue; // 0: system sounds
                    if (((IAudioMeterInformation)control).GetPeakValue(out var peak) == 0 && peak > Silence) pids.Add((int)pid);
                }
            }
            return pids;
        }
        catch { return null; } // no audio service, or a device vanished mid-read: caller falls back to titles
        finally
        {
            foreach (var o in com) Marshal.ReleaseComObject(o);
        }
    }

    static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, out IntPtr control); // IAudioSessionManager
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, out IntPtr volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        // IAudioSessionControl, only to keep the vtable order
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(IntPtr name, IntPtr context);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, IntPtr context);
        [PreserveSig] int GetGroupingParam(out Guid param);
        [PreserveSig] int SetGroupingParam(IntPtr param, IntPtr context);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr client);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr client);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint pid);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
    }
}
