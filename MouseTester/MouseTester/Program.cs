using System;
using System.Windows.Forms;

namespace MouseTester
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var inputSource = new RawInputSource())
                Application.Run(new CalibrationForm(inputSource));
        }
    }
}
