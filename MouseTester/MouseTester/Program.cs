using System;
using System.Windows.Forms;

namespace MouseTester
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            StartupLog.Mark("Main; CLR " + Environment.Version + "; " + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var inputSource = new RawInputSource())
            {
                StartupLog.Mark("Raw Input source ready");
                Application.Run(new CalibrationForm(inputSource));
            }
            StartupLog.Mark("Application closed");
            StartupLog.Flush();
        }
    }
}
