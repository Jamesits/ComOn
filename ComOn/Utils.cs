using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ComOn
{
    public static class Utils
    {
        public static string GetFullPath(string fileName)
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        public static Version GetVersion()
        {
            return typeof(Utils).Assembly.GetName().Version;
        }
    }
    // https://www.pinvoke.net/default.aspx/Structures.DEV_BROADCAST_DEVICEINTERFACE
    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_DEVICE_INTERFACE
    {
        public int Size;
        public int DeviceType;
        public int Reserved;
        public Guid ClassGuid;
        public short Name;
    }

    public class Win32Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr RegisterDeviceNotification(
            IntPtr hRecipient,
            IntPtr notificationFilter,
            uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint UnregisterDeviceNotification(IntPtr hHandle);
    }

    public class UsbEventRegistration : IDisposable
    {
        private const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        private static readonly Guid s_guidDevInterfaceUsbDevice = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");

        private readonly IntPtr _windowHandle;
        private IntPtr _notificationHandle = IntPtr.Zero;

        public bool IsRegistered => _notificationHandle != IntPtr.Zero;

        public UsbEventRegistration(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public void Register()
        {
            var dbdi = new DEV_BROADCAST_DEVICE_INTERFACE
            {
                DeviceType = DBT_DEVTYP_DEVICEINTERFACE,
                Reserved = 0,
                ClassGuid = s_guidDevInterfaceUsbDevice,
                Name = 0,
            };
            dbdi.Size = Marshal.SizeOf(dbdi);

            IntPtr buffer = Marshal.AllocHGlobal(dbdi.Size);
            Marshal.StructureToPtr(dbdi, buffer, true);
            _notificationHandle = Win32Native.RegisterDeviceNotification(
                _windowHandle,
                buffer,
                0);
        }

        // Call on window unload.
        public void Dispose()
        {
            Win32Native.UnregisterDeviceNotification(_notificationHandle);
        }
    }
}
