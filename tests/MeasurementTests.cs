using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MouseTester;

class MeasurementTests
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Range { public int Min, Max; }
    [StructLayout(LayoutKind.Sequential)] struct FormatRange { public IntPtr Hdc, Target; public Rect Area, Page; public Range Characters; }
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, ref FormatRange range);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, IntPtr lparam);
    static void RenderRichText(RichTextBox box, Form root, Bitmap bitmap)
    {
        // RichEdit does not support DrawToBitmap; use its native print formatter for QA.
        Point origin = root.PointToClient(box.PointToScreen(Point.Empty));
        origin.Offset(SystemInformation.FrameBorderSize.Width, SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.FillRectangle(new SolidBrush(box.BackColor), origin.X, origin.Y, box.ClientSize.Width, box.ClientSize.Height);
            double factor = 1440.0 / graphics.DpiX;
            var area = new Rect { Left = (int)(origin.X * factor), Top = (int)(origin.Y * factor), Right = (int)((origin.X + box.ClientSize.Width) * factor), Bottom = (int)((origin.Y + box.ClientSize.Height) * factor) };
            IntPtr hdc = graphics.GetHdc();
            try
            {
                var range = new FormatRange { Hdc = hdc, Target = hdc, Area = area, Page = new Rect { Right = (int)(bitmap.Width * factor), Bottom = (int)(bitmap.Height * factor) }, Characters = new Range { Min = 0, Max = -1 } };
                SendMessage(box.Handle, 0x439, new IntPtr(1), ref range);
            }
            finally { graphics.ReleaseHdc(hdc); SendMessage(box.Handle, 0x439, IntPtr.Zero, IntPtr.Zero); }
        }
    }
    static void Near(double actual, double expected, string name)
    {
        if (Math.Abs(actual - expected) > 0.000001) throw new Exception(name + ": " + actual);
    }
    static void Key(Control form, Keys key)
    {
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { new KeyEventArgs(key) });
    }
    [STAThread]
    static void Main(string[] args) { RunTests(args); }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static void RunTests(string[] args)
    {
        if (args.Length > 0 && args[0] == "--startup") { Startup(); return; }
        Near(Measurement.Cpi(60000, 0, 25.4, 0), 6000, "6000 DPI at 10 inches");
        Near(Measurement.Cpi(-60000, 0, 25.4, 1), 6000, "reverse X direction");
        Near(Measurement.Cpi(30000, 40000, 25.4, 0), 5000, "diagonal net displacement");
        Near(Measurement.Cpi(123, -60000, 25.4, 2), 6000, "Y only");
        bool rejected = false;
        try { Measurement.Cpi(1, 1, 0, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
        if (!rejected) throw new Exception("zero distance accepted");
        var samples = new List<MotionSample>();
        for (int i = 0; i <= 1000; i++) samples.Add(new MotionSample(i, 1, 0));
        var r = Measurement.Rate(samples);
        Near(r.AllHz, 1000, "1000 Hz all"); Near(r.MovingHz, 1000, "1000 Hz moving"); Near(r.MedianMs, 1, "median"); Near(r.P99Ms, 1, "P99");
        samples.Add(new MotionSample(1500, 1, 0));
        r = Measurement.Rate(samples);
        Near(r.AllHz, 1000.0 * 1001 / 1500, "pause included in full rate");
        Near(r.MovingHz, 1000, "pause excluded in moving rate");
        if (r.Gaps != 1) throw new Exception("gap count");
        Near(Measurement.Rate(new List<MotionSample>()).AllHz, 0, "empty");
        var straight = new List<MotionSample> { new MotionSample(0, 0, 0), new MotionSample(1000, 100, 100), new MotionSample(2000, 100, 100) };
        var geometry = Measurement.Trajectory(straight);
        Near(geometry.NetCounts, Math.Sqrt(80000), "diagonal net");
        Near(geometry.PathCounts / geometry.NetCounts, 1, "straight path ratio");
        Near(geometry.MaxDeviationCounts, 0, "diagonal is straight");
        Near(Measurement.RecentSpeed(straight, 150), Math.Sqrt(20000), "speed counts per second");
        var bent = new List<MotionSample> { new MotionSample(0, 0, 0), new MotionSample(100, 50, 100), new MotionSample(200, 50, -100) };
        Near(Measurement.Trajectory(bent).MaxDeviationCounts, 100, "bend deviation from chord");
        var returned = new List<MotionSample> { new MotionSample(0, 100, 0), new MotionSample(100, -100, 0) };
        Near(Measurement.Trajectory(returned).NetCounts, 0, "return to start");
        Near(Measurement.RecentSpeed(new List<MotionSample>(), 150), 0, "empty speed");
        var header = typeof(RawInputSource).GetNestedType("RAWINPUTHEADER", BindingFlags.NonPublic);
        if (Marshal.SizeOf(header) != (IntPtr.Size == 8 ? 24 : 16)) throw new Exception("RAWINPUTHEADER pointer size");
        Application.EnableVisualStyles();
        using (var original = new RawInputSource())
        using (var calibration = new CalibrationForm(original))
        {
            calibration.StartPosition = FormStartPosition.Manual;
            calibration.Location = new Point(-20000, -20000);
            calibration.ShowInTaskbar = false;
            calibration.Show();
            Application.DoEvents();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var fakeDevices = new List<MouseDevice> { new MouseDevice { Handle = new IntPtr(123), Name = "测试鼠标 A", Path = "test-A" }, new MouseDevice { Handle = new IntPtr(456), Name = "测试鼠标 B", Path = "test-B" } };
            typeof(CalibrationForm).GetMethod("ApplyDeviceList", flags).Invoke(calibration, new object[] { new List<MouseDevice>(fakeDevices) { new MouseDevice { Handle = new IntPtr(789), Name = "合成输入", Path = "" } } });
            var receive = typeof(CalibrationForm).GetMethod("Receive", flags);
            receive.Invoke(calibration, new object[] { new IntPtr(789), 99999, 0, Stopwatch.GetTimestamp(), true, (ushort)0 });
            if ((IntPtr)typeof(CalibrationForm).GetField("selected", flags).GetValue(calibration) != IntPtr.Zero) throw new Exception("synthetic device won auto selection");
            if (((ComboBox)typeof(CalibrationForm).GetField("deviceChoice", flags).GetValue(calibration)).Items.Count != 3) throw new Exception("synthetic device visible");
            var distance = (NumericUpDown)typeof(CalibrationForm).GetField("distance", flags).GetValue(calibration);
            var compare = (CheckBox)typeof(CalibrationForm).GetField("compareNominal", flags).GetValue(calibration);
            var nominal = (NumericUpDown)typeof(CalibrationForm).GetField("nominal", flags).GetValue(calibration);
            if (compare.Checked || nominal.Enabled) throw new Exception("default mode requires nominal DPI");
            distance.Value = 25.4m;
            long t0 = Stopwatch.GetTimestamp();
            receive.Invoke(calibration, new object[] { new IntPtr(123), 1, 0, t0, true, (ushort)0 });
            Key(calibration, Keys.F5);
            Near((double)typeof(CalibrationForm).GetField("savedNominal", flags).GetValue(calibration), 0, "unknown nominal mode");
            if (!(bool)typeof(CalibrationForm).GetField("measuring", flags).GetValue(calibration)) throw new Exception("F5 did not immediately start");
            receive.Invoke(calibration, new object[] { new IntPtr(123), 60, 0, t0 + Stopwatch.Frequency / 1000, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(456), 90000, 0, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), 90000, 0, t0, false, (ushort)0 });
            for (int i = 2; i < 1000; i++) receive.Invoke(calibration, new object[] { new IntPtr(123), 60, 0, t0 + i * Stopwatch.Frequency / 1000, true, (ushort)0 });
            // Mouse button release no longer ends the measurement.
            receive.Invoke(calibration, new object[] { new IntPtr(123), 60, 0, t0 + Stopwatch.Frequency, true, (ushort)2 });
            if (!(bool)typeof(CalibrationForm).GetField("measuring", flags).GetValue(calibration)) throw new Exception("button release unexpectedly stopped measurement");
            Key(calibration, Keys.F6);
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 6000, "form workflow with device isolation and absolute rejection");
            if (((string)typeof(CalibrationForm).GetField("savedResult", flags).GetValue(calibration)).Contains("偏差：")) throw new Exception("unknown DPI shows nominal deviation");
            var highlighted = (RichTextBox)typeof(CalibrationForm).GetField("resultText", flags).GetValue(calibration);
            highlighted.Select(highlighted.Text.IndexOf("6000.0"), 6);
            if (highlighted.SelectionColor.ToArgb() != Color.RoyalBlue.ToArgb() || !highlighted.SelectionFont.Bold || highlighted.SelectionFont.Size < 15) throw new Exception("DPI highlight missing");
            int rateIndex = highlighted.Text.IndexOf("1000.0 Hz");
            if (rateIndex < 0) throw new Exception("result rate missing");
            highlighted.Select(rateIndex, 9);
            if (highlighted.SelectionColor.ToArgb() != Color.DarkGreen.ToArgb() || !highlighted.SelectionFont.Bold) throw new Exception("rate highlight missing");
            highlighted.Select(0, 0);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 90000, 0, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 6000, "post-F6 movement excluded");
            calibration.PerformLayout();
            using (var bmp = new Bitmap(calibration.Width, calibration.Height))
            {
                calibration.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                RenderRichText(highlighted, calibration, bmp);
                if (args.Length > 0) bmp.Save(args[0]);
            }
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 0, 0, t0, true, (ushort)1 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), 999, 0, t0, true, (ushort)0 });
            Key(calibration, Keys.Escape);
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 0, "cancel discards result");
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), -30000, 0, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), -30000, 0, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 6000, "keyboard mode reverse direction");
            var trials = (List<double>)typeof(CalibrationForm).GetField("repeated", flags).GetValue(calibration);
            if (trials.Count != 2) throw new Exception("repeat count");
            distance.Value = 10;
            if (trials.Count != 0) throw new Exception("editing next distance seeded old counts into history");
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 6000, "editing distance must preserve last result");
            typeof(CalibrationForm).GetMethod("CorrectLastDistance", flags).Invoke(calibration, null);
            if (trials.Count != 1) throw new Exception("explicit correction history");
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 15240, "actual-distance correction reuses raw counts");
            distance.Value = 25.4m;
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 30000, 5000, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), 30000, -5000, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            if (trials.Count != 0) throw new Exception("bent trajectory entered valid-trial average");
            compare.Checked = true;
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 6000, "optional nominal does not change measured DPI");
            if (!nominal.Enabled || trials.Count != 0) throw new Exception("optional comparison configuration failed");
            compare.Checked = false;
            if (nominal.Enabled || ((string)typeof(CalibrationForm).GetField("savedResult", flags).GetValue(calibration)).Contains("偏差：")) throw new Exception("disable comparison failed");
            typeof(CalibrationForm).GetMethod("ApplyDeviceList", flags).Invoke(calibration, new object[] { fakeDevices });
            var choices = (ComboBox)typeof(CalibrationForm).GetField("deviceChoice", flags).GetValue(calibration);
            if (choices.Items.Count != 3) throw new Exception("two-device list missing");
            choices.SelectedIndex = 2;
            if (!((Label)typeof(CalibrationForm).GetField("deviceText", flags).GetValue(calibration)).Text.Contains("测试鼠标 B")) throw new Exception("selected mouse name missing");
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 30000, 0, t0, true, (ushort)0 });
            Key(calibration, Keys.F6);
            if (!highlighted.Text.Contains("其他设备输入") || !highlighted.Text.Contains("有效移动：0")) throw new Exception("wrong-device diagnostic missing");
            choices.SelectedIndex = 0;
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(456), 30000, 0, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(456), 30000, 0, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), 6000, "automatic new mouse per F5");
            Key(calibration, Keys.F5);
            Key(calibration, Keys.F6);
            if (!highlighted.Text.Contains("没有收到鼠标原始输入")) throw new Exception("no-input diagnostic missing");

            // Regression for the user's 19cm -> 36cm screenshots.
            distance.Value = 19; compare.Checked = true; nominal.Value = 6000;
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 22922, 137, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), 22923, 137, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            double firstDpi = Measurement.Cpi(45845, 274, 19, 0);
            Near(trials[0], firstDpi, "19cm first trial");
            distance.Value = 36; nominal.Value = 6400;
            if (trials.Count != 0) throw new Exception("19->36 edits retained a ghost trial");
            Near((double)typeof(CalibrationForm).GetField("savedCpi", flags).GetValue(calibration), firstDpi, "old 19cm result changed without correction");
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 45560, -2875, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), 45560, -2876, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            double secondDpi = Measurement.Cpi(91120, -5751, 36, 0);
            if (trials.Count != 1) throw new Exception("first 36cm measurement counted twice");
            Near(trials[0], secondDpi, "36cm average equals its only valid trial");
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(123), 45560, -2875, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(123), 45560, -2876, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            if (trials.Count != 2) throw new Exception("same-setting repeat failed");
            Key(calibration, Keys.F5);
            receive.Invoke(calibration, new object[] { new IntPtr(456), 45560, -2875, t0, true, (ushort)0 });
            receive.Invoke(calibration, new object[] { new IntPtr(456), 45560, -2876, t0 + Stopwatch.Frequency, true, (ushort)0 });
            Key(calibration, Keys.F6);
            if (trials.Count != 1) throw new Exception("different mouse mixed into average");
        }
        Console.WriteLine("PASS: two-device list/names/manual selection, auto selection per F5, no-input/wrong-device diagnostics, default unknown-DPI mode, highlights, keyboard workflow, input isolation, distance correction, trajectory/speed/CPI/rate math.");
    }

    static void Startup()
    {
        // Run before creating any controls: WinForms rendering defaults are process-wide.
        bool startupChecked = false;
        EventHandler inspect = null;
        inspect = delegate {
                Application.Idle -= inspect;
                int visible = 0; CalibrationForm main = null; RawInputSource input = null;
                foreach (Form form in Application.OpenForms) { if (form.Visible) visible++; if (form is CalibrationForm) main = (CalibrationForm)form; }
                if (main != null) input = (RawInputSource)typeof(CalibrationForm).GetField("source", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main);
                if (visible != 1 || main == null || main.Text != "Mouse Tester, 检测鼠标回报率、DPI" || main.Owner != null || input == null || input.Visible || !input.IsHandleCreated) throw new Exception("single-window startup failed");
                startupChecked = true;
                main.Close();
        };
        Application.Idle += inspect;
        typeof(CalibrationForm).Assembly.GetType("MouseTester.Program").GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        if (!startupChecked || Application.OpenForms.Count != 0) throw new Exception("application close lifecycle failed");
        Console.WriteLine("PASS: single visible window and exact title, hidden Raw Input handle, close/dispose lifecycle.");
    }
}


