using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MouseTester
{
    // Hidden input sink; independent of the original UI and plotting library.
    public partial class RawInputSource : Form
    {
        internal event Action<IntPtr, int, int, long, bool, ushort> RawMotion;
        internal event Action RawDevicesChanged;

        public RawInputSource()
        {
            ShowInTaskbar = false;
            var hwnd = Handle;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterRawInputMouse(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x00FE)
            {
                var changed = RawDevicesChanged;
                if (changed != null) changed();
            }
            if (m.Msg == WM_INPUT)
            {
                RAWINPUT raw;
                uint size = (uint)Marshal.SizeOf(typeof(RAWINPUT));
                int bytes = GetRawInputData(m.LParam, RID_INPUT, out raw, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));
                if (bytes != -1 && raw.header.dwType == RIM_TYPEMOUSE)
                {
                    long counter;
                    QueryPerformanceCounter(out counter);
                    var handler = RawMotion;
                    if (handler != null)
                        handler(raw.header.hDevice, raw.data.mouse.lLastX, raw.data.mouse.lLastY, counter,
                            (raw.data.mouse.usFlags & 1) == 0, raw.data.mouse.buttonsStr.usButtonFlags);
                }
            }
            base.WndProc(ref m);
        }
    }
}
