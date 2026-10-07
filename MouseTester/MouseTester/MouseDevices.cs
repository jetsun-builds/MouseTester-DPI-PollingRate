using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MouseTester
{
    public sealed class MouseDevice
    {
        public IntPtr Handle;
        public string Name, Path;
        public override string ToString() { return Name + " [0x" + Handle.ToInt64().ToString("X") + "]"; }
    }

    public static class MouseDevices
    {
        [StructLayout(LayoutKind.Sequential)] struct DeviceList { public IntPtr Handle; public uint Type; }
        [StructLayout(LayoutKind.Sequential)] struct InterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct DeviceInfo { public int Size; public Guid Class; public int DevInst; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint GetRawInputDeviceList(IntPtr data, ref uint count, uint size);
        [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);
        [DllImport("setupapi.dll", SetLastError = true)] static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr guid, IntPtr parent);
        [DllImport("setupapi.dll", EntryPoint = "SetupDiOpenDeviceInterfaceW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiOpenDeviceInterface(IntPtr set, string path, uint flags, ref InterfaceData data);
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, ref DeviceInfo info);
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDeviceRegistryPropertyW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref DeviceInfo info, uint property, out uint type, byte[] buffer, uint size, out uint required);
        [DllImport("setupapi.dll", EntryPoint = "SetupDiGetDevicePropertyW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceProperty(IntPtr set, ref DeviceInfo info, ref PropertyKey key, out uint type, byte[] buffer, uint size, out uint required, uint flags);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        public static List<MouseDevice> Enumerate()
        {
            // Retry when hot-plug changes the required list size between the two calls.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint count = 0, size = (uint)Marshal.SizeOf(typeof(DeviceList));
                if (GetRawInputDeviceList(IntPtr.Zero, ref count, size) == uint.MaxValue) throw new Win32Exception();
                if (count == 0) return new List<MouseDevice>();
                IntPtr buffer = Marshal.AllocHGlobal(checked((int)(count * size)));
                try
                {
                    uint found = GetRawInputDeviceList(buffer, ref count, size);
                    if (found == uint.MaxValue) { if (Marshal.GetLastWin32Error() == 122) continue; throw new Win32Exception(); }
                    var devices = new List<MouseDevice>();
                    for (int i = 0; i < found; i++)
                    {
                        var item = (DeviceList)Marshal.PtrToStructure(IntPtr.Add(buffer, checked(i * (int)size)), typeof(DeviceList));
                        if (item.Type != 0 || item.Handle == IntPtr.Zero) continue;
                        uint length = 0;
                        string path = "";
                        if (GetRawInputDeviceInfo(item.Handle, 0x20000007, null, ref length) != uint.MaxValue && length > 0)
                        {
                            var text = new StringBuilder(checked((int)length + 1));
                            if (GetRawInputDeviceInfo(item.Handle, 0x20000007, text, ref length) != uint.MaxValue) path = text.ToString();
                        }
                        var id = Regex.Match(path, @"VID_[0-9A-F]{4}.*?PID_[0-9A-F]{4}", RegexOptions.IgnoreCase);
                        devices.Add(new MouseDevice { Handle = item.Handle, Path = path, Name = id.Success ? "HID 鼠标（" + id.Value.Replace("&", " ") + "）" : "HID 鼠标" });
                    }
                    return devices;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            throw new InvalidOperationException("设备列表正在变化，请刷新重试。");
        }

        public static List<MouseDevice> ResolveNames(IEnumerable<MouseDevice> devices)
        {
            var named = new List<MouseDevice>();
            foreach (var d in devices)
            {
                string name;
                try { name = FriendlyName(d.Path); } catch { name = d.Name; }
                named.Add(new MouseDevice { Handle = d.Handle, Path = d.Path, Name = string.IsNullOrWhiteSpace(name) || name == "HID 鼠标" ? d.Name : name });
            }
            return named;
        }

        private static string FriendlyName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "鼠标（名称不可用）";
            IntPtr set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
            if (set == new IntPtr(-1)) return "HID 鼠标";
            try
            {
                var data = new InterfaceData { Size = Marshal.SizeOf(typeof(InterfaceData)) };
                if (!SetupDiOpenDeviceInterface(set, path, 0, ref data)) return "HID 鼠标";
                var info = new DeviceInfo { Size = Marshal.SizeOf(typeof(DeviceInfo)) };
                uint required;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, ref info);
                if (required == 0) return "HID 鼠标";
                IntPtr detail = Marshal.AllocHGlobal(checked((int)required));
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, ref info)) return "HID 鼠标";
                    byte[] buffer = new byte[8192]; uint type, bytes;
                    var key = new PropertyKey { Format = new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2"), Id = 4 };
                    if (SetupDiGetDeviceProperty(set, ref info, ref key, out type, buffer, (uint)buffer.Length, out bytes, 0) && type == 0x12) return Encoding.Unicode.GetString(buffer, 0, (int)bytes).TrimEnd('\0');
                    foreach (uint property in new uint[] { 12, 0 })
                        if (SetupDiGetDeviceRegistryProperty(set, ref info, property, out type, buffer, (uint)buffer.Length, out bytes)) return Encoding.Unicode.GetString(buffer, 0, (int)bytes).TrimEnd('\0');
                    return "HID 鼠标";
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }
}
