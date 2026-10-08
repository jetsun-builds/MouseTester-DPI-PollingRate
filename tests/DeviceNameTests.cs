using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using MouseTester;

class DeviceNameTests
{
    static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
    }

    static int Main(string[] args)
    {
        try
        {
            const string known = @"\\?\HID#VID_046D&PID_C09D&MI_00#device";
            Check(UsbDeviceNames.Resolve(known, "My actual mouse", "Different system name") == "My actual mouse", "Device product must take priority");
            Check(UsbDeviceNames.Resolve(known, "", "Specific system model") == "Specific system model", "Specific system name must precede database");
            string fallback = UsbDeviceNames.Resolve(known, "USB Optical Mouse", "HID-compliant mouse");
            Check(fallback.Contains("Logitech") && fallback.Contains("G102 LIGHTSYNC"), "Generic descriptors must fall back to embedded database");
            Check(UsbDeviceNames.Resolve(known.ToLowerInvariant(), null, null) == fallback, "IDs are case insensitive");
            Check(UsbDeviceNames.Resolve(@"HID#VID_FFFE&PID_FFFD", null, null) == "HID 鼠标（VID:FFFE PID:FFFD）", "Unknown device must show VID/PID");
            Check(UsbDeviceNames.Resolve(@"HID#VID_FFFE&PID_FFFD", "USB Mouse", "HID 鼠标") == "HID 鼠标（VID:FFFE PID:FFFD）", "Generic name cannot hide unknown IDs");
            Check(UsbDeviceNames.Resolve(null, null, null) == "HID 鼠标", "Missing metadata must be safe");
            Check(UsbDeviceNames.Resolve("VID_046D0&PID_C09D", null, null) == "HID 鼠标", "Do not truncate longer vendor IDs");
            Check(UsbDeviceNames.Resolve("VID_046D&PID_C09D0", null, null) == "HID 鼠标", "Do not truncate longer product IDs");
            Check(UsbDeviceNames.Resolve("VID_046D&PID_C52B", null, null).Contains("Receiver"), "Receiver ID must remain a receiver");
            Check(UsbDeviceNames.Resolve(known, "USB Receiver", null) == "USB Receiver", "Do not replace receiver's self-reported identity");
            Parallel.For(0, 100, i => Check(UsbDeviceNames.Resolve(known, null, null) == fallback, "Concurrent lookup failed"));
            using (var resource = typeof(UsbDeviceNames).Assembly.GetManifestResourceStream("MouseTester.UsbIds"))
                Check(resource != null && resource.Length > 600000, "Database must be embedded in EXE");
            using (var resource = typeof(UsbDeviceNames).Assembly.GetManifestResourceStream("MouseTester.Notices"))
                Check(resource != null && resource.Length > 1000, "Third-party notice must be embedded");
            Console.WriteLine("PASS: device/system/database priority, generic names, unknown IDs, case and boundary handling, receiver identity, concurrency and embedded resources.");
            if (args.Length > 0 && args[0] == "--devices")
            {
                var timer = Stopwatch.StartNew();
                var devices = MouseDevices.ResolveNames(MouseDevices.Enumerate());
                foreach (var device in devices) Console.WriteLine(device.Name);
                Console.WriteLine("Devices: " + devices.Count + "; resolved in " + timer.ElapsedMilliseconds + "ms");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
