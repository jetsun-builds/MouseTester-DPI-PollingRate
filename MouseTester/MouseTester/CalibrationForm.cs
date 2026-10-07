using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MouseTester
{
    public sealed class CalibrationForm : Form
    {
        private readonly RawInputSource source;
        private readonly NumericUpDown distance = new NumericUpDown { Minimum = 1, Maximum = 200, DecimalPlaces = 2, Value = 10m, Width = 100 };
        private readonly NumericUpDown nominal = new NumericUpDown { Minimum = 1, Maximum = 100000, Value = 6000, Width = 100 };
        private readonly CheckBox compareNominal = new CheckBox { Text = "对比标称 DPI（可选）", AutoSize = true, Checked = false };
        private readonly ComboBox axis = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        private readonly ComboBox reference = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
        private readonly RichTextBox rateText = new RichTextBox { ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 11), BorderStyle = BorderStyle.None, DetectUrls = false, BackColor = SystemColors.Control };
        private readonly RichTextBox resultText = new RichTextBox { ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", 11), BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, BackColor = SystemColors.Control };
        private readonly Font dpiHighlightFont = new Font("Microsoft YaHei UI", 16, FontStyle.Bold);
        private readonly Font rateHighlightFont = new Font("Microsoft YaHei UI", 13, FontStyle.Bold);
        private readonly Label deviceText = new Label { AutoSize = true };
        private readonly Label deviceCount = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        private readonly ComboBox deviceChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 470 };
        private readonly Button refreshDevices = new Button { Text = "刷新设备", AutoSize = true };
        private readonly ToolTip deviceTip = new ToolTip();
        private List<MouseDevice> devices = new List<MouseDevice>();
        private bool changingDeviceList, manualDevice;
        private bool namesLoading;
        private bool enumeratingDevices;
        private readonly Button start = new Button { Text = "开始测量 (F5)", AutoSize = true };
        private readonly Button stop = new Button { Text = "取消测量 (Esc)", AutoSize = true, Enabled = false };
        private readonly MotionView motionView = new MotionView();
        private readonly Button reset = new Button { Text = "重新选择鼠标", AutoSize = true };
        private readonly Button correctDistance = new Button { Text = "修正上次距离", AutoSize = true, Enabled = false };
        private readonly List<MotionSample> recent = new List<MotionSample>();
        private readonly List<MotionSample> captured = new List<MotionSample>();
        private readonly Timer refresh = new Timer { Interval = 250 };
        private IntPtr selected;
        private bool measuring;
        private readonly List<double> repeated = new List<double>();
        private long sumX, sumY;
        private double path, previousTime = double.NaN;
        private double savedCm, savedNominal, savedCpi;
        private int savedAxis;
        private string savedResult = "";
        private string savedDetails = "";
        private int seenInput, otherInput, absoluteInput, emptyInput;
        private long startedCounter;
        private IntPtr previousTrialDevice;

        public CalibrationForm(RawInputSource source)
        {
            this.source = source;
            Text = "Mouse Tester, 检测鼠标回报率、DPI";
            ClientSize = new Size(930, 905);
            MinimumSize = new Size(920, 885);
            Font = new Font("Microsoft YaHei UI", 10);
            KeyPreview = true;
            axis.Items.AddRange(new object[] { "直线合成 X/Y", "仅 X 轴", "仅 Y 轴" });
            axis.SelectedIndex = 0;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 7 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 225));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "回报率：连续移动鼠标 3–5 秒，查看下方绿色 Hz 结果。\nDPI：需在桌面上直线移动鼠标指定的实际距离。摆到尺子起点 → 按 F5 → 直线移动指定距离 → 停稳后按 F6。\n默认不开启标称对比；已知驱动 DPI 时，可勾选“对比标称 DPI”，查看偏差。\n移动时不需要按住鼠标左键；光标碰到屏幕边缘仍会继续计数。保持朝向固定，不抬起、不来回；无需刻意控制快慢，平稳移动即可。程序显示的轨迹仅用于观察速度和直线程度。\n注意：鼠标在鼠标垫/桌面上直线移动的实际距离，一定要与程序界面上设置的实际距离一致，例如 10 厘米，否则计算出的 DPI 将不准确。", AutoSize = false }, 0, 0);
            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill };
            settings.Controls.Add(new Label { Text = "实际距离 (cm)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            settings.Controls.Add(distance);
            settings.Controls.Add(compareNominal);
            settings.Controls.Add(nominal);
            nominal.Enabled = false;
            settings.Controls.Add(axis);
            reference.Items.AddRange(new object[] { "10cm（直尺）", "21cm（A4 纸短边）", "29.7cm（A4 纸长边）", "25.4cm（10 英寸）" });
            reference.SelectedIndex = 0;
            reference.SelectedIndexChanged += delegate { if (reference.SelectedIndex >= 0) distance.Value = new decimal[] { 10m, 21m, 29.7m, 25.4m }[reference.SelectedIndex]; };
            settings.Controls.Add(reference);
            root.Controls.Add(settings, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
            buttons.Controls.Add(start); buttons.Controls.Add(stop); buttons.Controls.Add(reset);
            buttons.Controls.Add(new Label { Text = "结束请按 F6", AutoSize = true, Padding = new Padding(10, 6, 10, 0) });
            var export = new Button { Text = "导出本次 DPI 数据", AutoSize = true };
            buttons.Controls.Add(export);
            buttons.Controls.Add(correctDistance);
            correctDistance.Click += delegate { CorrectLastDistance(); };
            root.Controls.Add(buttons, 0, 2);
            var devicePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            devicePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            devicePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var deviceControls = new FlowLayoutPanel { Dock = DockStyle.Fill };
            deviceControls.Controls.Add(deviceCount); deviceControls.Controls.Add(deviceChoice); deviceControls.Controls.Add(refreshDevices);
            devicePanel.Controls.Add(deviceControls, 0, 0);
            deviceText.AutoSize = false; deviceText.Dock = DockStyle.Fill; deviceText.AutoEllipsis = true;
            devicePanel.Controls.Add(deviceText, 0, 1);
            root.Controls.Add(devicePanel, 0, 3);
            root.Controls.Add(motionView, 0, 4);
            root.Controls.Add(rateText, 0, 5);
            root.Controls.Add(resultText, 0, 6);
            Controls.Add(root);
            start.Click += delegate { BeginMeasurement(); };
            stop.Click += delegate { CancelMeasurement(); };
            reset.Click += delegate { manualDevice = false; SelectDevice(IntPtr.Zero); changingDeviceList = true; deviceChoice.SelectedIndex = 0; changingDeviceList = false; };
            refreshDevices.Click += delegate { RefreshDevices(); };
            deviceChoice.SelectedIndexChanged += delegate {
                if (changingDeviceList || measuring || deviceChoice.SelectedIndex < 0) return;
                var device = deviceChoice.SelectedItem as MouseDevice;
                manualDevice = device != null;
                SelectDevice(device == null ? IntPtr.Zero : device.Handle);
            };
            export.Click += delegate { Export(); };
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.F5) { BeginMeasurement(); e.Handled = true; } if (e.KeyCode == Keys.F6) { EndMeasurement(); e.Handled = true; } if (e.KeyCode == Keys.Escape) { CancelMeasurement(); e.Handled = true; } };
            distance.ValueChanged += delegate { Recalculate(); };
            nominal.ValueChanged += delegate { Recalculate(); };
            compareNominal.CheckedChanged += delegate { nominal.Enabled = compareNominal.Checked && !measuring; Recalculate(); };
            axis.SelectedIndexChanged += delegate { Recalculate(); };
            source.RawMotion += Receive;
            source.RawDevicesChanged += OnDevicesChanged;
            resultText.TextChanged += delegate { HighlightResults(resultText); };
            rateText.TextChanged += delegate { HighlightResults(rateText); };
            refresh.Tick += delegate { UpdateRate(); };
            refresh.Start();
            deviceText.Text = "请只移动要测试的鼠标以锁定设备";
            RefreshDevices();
            resultText.Text = "当前为直接测量模式：不用填写 DPI 或回报率。\r\n回报率直接移动即可；DPI 测量只需要桌面实际移动距离（默认 10cm），F5 开始、F6 结束。\r\n若知道标称 DPI，可勾选上方对比项。上次距离填错时，修改距离后点击“修正上次距离”。\r\n改变鼠标 DPI 档位后，请重新选择鼠标清空旧统计。";
            FormClosed += delegate { refresh.Stop(); refresh.Dispose(); source.RawMotion -= Receive; source.RawDevicesChanged -= OnDevicesChanged; deviceTip.Dispose(); dpiHighlightFont.Dispose(); rateHighlightFont.Dispose(); };
        }

        private void OnDevicesChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(delegate { if (!IsDisposed) RefreshDevices(); }));
        }

        private void RefreshDevices()
        {
            if (enumeratingDevices) return;
            enumeratingDevices = true;
            deviceCount.Text = "正在读取鼠标设备…";
            var hwnd = Handle;
            Task.Run(delegate { return MouseDevices.Enumerate(); }).ContinueWith(task => {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(delegate {
                    enumeratingDevices = false;
                    if (IsDisposed) return;
                    if (task.IsFaulted) { deviceCount.Text = "设备枚举失败"; deviceTip.SetToolTip(deviceCount, task.Exception.GetBaseException().Message); return; }
                    ApplyDeviceList(task.Result);
                })); } catch (InvalidOperationException) { }
            });
        }

        private void ApplyDeviceList(List<MouseDevice> found)
        {
            try
            {
                devices = found;
                deviceCount.Text = "鼠标输入设备：" + devices.Count;
                changingDeviceList = true;
                deviceChoice.Items.Clear();
                deviceChoice.Items.Add("自动识别：移动要测试的鼠标");
                int index = 0;
                foreach (var d in devices)
                {
                    int added = deviceChoice.Items.Add(d);
                    if (manualDevice && d.Handle == selected) index = added;
                }
                deviceChoice.SelectedIndex = index;
                if (selected != IntPtr.Zero && !devices.Any(d => d.Handle == selected))
                {
                    if (measuring) CancelMeasurement();
                    manualDevice = false; SelectDevice(IntPtr.Zero);
                    deviceText.Text = "所选设备已断开，请重新选择或移动待测鼠标。";
                }
                else ShowSelectedDevice();
                deviceTip.SetToolTip(deviceCount, "Windows Raw Input 鼠标设备数量；可能包含触摸板、虚拟设备或同一物理鼠标的多个接口。");
                ResolveNamesInBackground();
            }
            catch (Exception ex) { deviceCount.Text = "设备枚举失败"; deviceTip.SetToolTip(deviceCount, ex.Message); }
            finally { changingDeviceList = false; }
        }

        private void ResolveNamesInBackground()
        {
            if (namesLoading || devices.Count == 0) return;
            namesLoading = true;
            var snapshot = devices.ToArray();
            Task.Run(delegate { return MouseDevices.ResolveNames(snapshot); }).ContinueWith(task => {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(delegate {
                    namesLoading = false;
                    if (IsDisposed || task.IsFaulted) return;
                    foreach (var d in devices)
                    {
                        var named = task.Result.FirstOrDefault(n => n.Handle == d.Handle && n.Path == d.Path);
                        if (named != null) d.Name = named.Name;
                    }
                    int index = deviceChoice.SelectedIndex;
                    changingDeviceList = true;
                    deviceChoice.Items.Clear(); deviceChoice.Items.Add("自动识别：移动要测试的鼠标");
                    foreach (var d in devices) deviceChoice.Items.Add(d);
                    deviceChoice.SelectedIndex = index >= 0 && index < deviceChoice.Items.Count ? index : 0;
                    changingDeviceList = false;
                    ShowSelectedDevice();
                })); } catch (InvalidOperationException) { }
            });
        }

        private void SelectDevice(IntPtr handle)
        {
            selected = handle;
            recent.Clear(); repeated.Clear(); captured.Clear(); previousTime = double.NaN;
            savedResult = ""; savedCpi = 0; sumX = sumY = 0; path = 0;
            savedDetails = ""; correctDistance.Enabled = false;
            UpdateMotion();
            resultText.Text = "设备已选择。回报率直接连续移动即可；DPI 测量先摆好鼠标，再按 F5。";
            ShowSelectedDevice();
        }

        private void ShowSelectedDevice()
        {
            var device = devices.FirstOrDefault(d => d.Handle == selected);
            deviceText.Text = selected == IntPtr.Zero ? "当前未选定：请移动要测试的鼠标，或从列表选择。" : "当前鼠标：" + (device == null ? "名称不可用 [0x" + selected.ToInt64().ToString("X") + "]" : device.ToString()) + (manualDevice ? "（手动选择）" : "（自动识别）");
            string path = device == null ? "" : device.Path;
            deviceTip.SetToolTip(deviceText, deviceText.Text + "\n设备路径：" + path);
            deviceTip.SetToolTip(deviceChoice, path);
        }

        private void HighlightResults(RichTextBox box)
        {
            box.SelectAll();
            box.SelectionFont = box.Font;
            box.SelectionColor = SystemColors.ControlText;
            box.SelectionBackColor = box.BackColor;
            HighlightMatches(box, @"(?:实测 DPI / CPI：|平均 DPI：)\s*[0-9]+(?:[.,][0-9]+)?", dpiHighlightFont, Color.RoyalBlue, Color.AliceBlue);
            HighlightMatches(box, @"(?:回报率估算（中位间隔）：|连续段事件率：|全窗口事件率：|本次事件率（含停顿）：)\s*[0-9]+(?:[.,][0-9]+)?\s*Hz", rateHighlightFont, Color.DarkGreen, Color.Honeydew);
            box.Select(0, 0);
        }

        private static void HighlightMatches(RichTextBox box, string pattern, Font font, Color color, Color background)
        {
            foreach (Match match in Regex.Matches(box.Text, pattern))
            {
                box.Select(match.Index, match.Length);
                box.SelectionFont = font;
                box.SelectionColor = color;
                box.SelectionBackColor = background;
            }
        }

        private void Receive(IntPtr device, int x, int y, long counter, bool relative, ushort buttons)
        {
            if (measuring) seenInput++;
            if (!relative || device == IntPtr.Zero) { if (measuring) absoluteInput++; return; }
            if (selected == IntPtr.Zero)
            {
                if (x == 0 && y == 0) return;
                selected = device;
                if (measuring && previousTrialDevice != device) repeated.Clear();
                ShowSelectedDevice();
            }
            if (device != selected) { if (measuring) otherInput++; return; }
            if (measuring && x == 0 && y == 0) emptyInput++;
            double ms = counter * 1000.0 / Stopwatch.Frequency;
            if (x != 0 || y != 0) recent.Add(new MotionSample(ms, x, y));
            if (measuring && (x != 0 || y != 0))
            {
                // Do not update controls per packet: avoid perturbing the arrival timestamps.
                captured.Add(new MotionSample(ms, x, y));
                sumX += x; sumY += y;
                path += Math.Sqrt((double)x * x + (double)y * y);
            }
            if (x != 0 || y != 0) previousTime = ms;
        }

        private void UpdateRate()
        {
            double now = Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
            if (measuring)
            {
                UpdateMotion();
                resultText.Text = string.Format("正在计数：X {0}    Y {1}    有效移动 {2}    其他设备输入 {4}\r\n移动桌面实际距离 {3:0.##}cm 后停稳按 F6。以桌面尺子终点为准。", sumX, sumY, captured.Count, savedCm, otherInput);
            }
            recent.RemoveAll(s => now - s.Ms > 2000);
            if (recent.Count < 200 || double.IsNaN(previousTime) || now - previousTime > 250 || recent[recent.Count - 1].Ms - recent[0].Ms < 500)
            {
                rateText.Text = "回报率：等待充分的连续移动数据（近 2 秒窗口）\r\n这是 Windows 原始移动事件的到达率估计，静止时不能判断硬件回报率。";
                return;
            }
            var r = Measurement.Rate(recent);
            rateText.Text = string.Format("回报率估算（中位间隔）：{0:F1} Hz    间隔：{1:F4} ms\r\n连续段平均事件率：{2:F1} Hz    全窗口平均事件率：{3:F1} Hz\r\nP99：{4:F4} ms    样本：{5}    >20ms 长间隔：{6}\r\n平均事件率受停顿影响；以上均为 Windows 到达时间估算，不能等同 USB 总线测量。", 1000 / r.MedianMs, r.MedianMs, r.MovingHz, r.AllHz, r.P99Ms, recent.Count, r.Gaps);
        }

        private void BeginMeasurement()
        {
            if (measuring) return;
            previousTrialDevice = selected;
            if (!manualDevice)
            {
                selected = IntPtr.Zero;
                recent.Clear(); previousTime = double.NaN;
                ShowSelectedDevice();
            }
            captured.Clear(); sumX = sumY = 0; path = 0;
            seenInput = otherInput = absoluteInput = emptyInput = 0;
            startedCounter = Stopwatch.GetTimestamp();
            savedResult = ""; savedCpi = 0;
            savedDetails = "";
            savedCm = (double)distance.Value; savedNominal = compareNominal.Checked ? (double)nominal.Value : 0; savedAxis = axis.SelectedIndex;
            measuring = true;
            UpdateMotion();
            SetRecording(true);
            resultText.Text = "正在记录：单向移动 " + savedCm.ToString("0.##") + "cm，停稳按 F6。不要移去点击按钮。";
        }

        private void SetRecording(bool recording)
        {
            distance.Enabled = compareNominal.Enabled = axis.Enabled = reference.Enabled = reset.Enabled = start.Enabled = !recording;
            deviceChoice.Enabled = refreshDevices.Enabled = !recording;
            correctDistance.Enabled = !recording && captured.Count >= 2 && savedResult.Length > 0;
            nominal.Enabled = !recording && compareNominal.Checked;
            stop.Enabled = recording;
        }

        private void EndMeasurement()
        {
            if (!measuring) return;
            measuring = false; SetRecording(false);
            if (captured.Count < 2)
            {
                UpdateMotion();
                string reason = otherInput > 0 ? "收到了其他设备输入。可能选中了另一只鼠标，请从设备列表选中正在移动的鼠标。" : absoluteInput > 0 ? "收到了绝对坐标或无设备标识输入，无法按标准相对鼠标方式测 CPI。" : seenInput == 0 ? "记录期间没有收到鼠标原始输入。请确认 F5 开始后持续移动，再按 F6。" : emptyInput > 0 ? "当前设备只有按钮/滚轮等零位移输入，没有足够的移动数据。" : "只收到一条有效移动，请延长移动时间后重测。";
                resultText.Text = string.Format("有效移动数据不足：{0}\r\n记录时长：{1:F2} 秒    收到输入：{2}    有效移动：{3}\r\n其他设备：{4}    绝对坐标/无标识：{5}    零位移：{6}\r\n{7}", reason, (Stopwatch.GetTimestamp() - startedCounter) / (double)Stopwatch.Frequency, seenInput, captured.Count, otherInput, absoluteInput, emptyInput, deviceText.Text);
                return;
            }
            ComputeResult();
        }

        private void UpdateMotion()
        {
            motionView.UpdateMotion(captured, !measuring && savedCpi > 0 ? savedCpi : savedNominal, !measuring && savedCpi > 0);
        }

        private void Recalculate()
        {
            reference.SelectedIndex = Array.IndexOf(new decimal[] { 10m, 21m, 29.7m, 25.4m }, distance.Value);
            repeated.Clear();
            if (measuring || savedDetails.Length == 0) return;
            // Editing controls configures the NEXT trial; it must not seed an average with old counts.
            savedResult = savedDetails + "\r\n参数已修改，仅用于下一次 F5 测量；重复统计已清空。\r\n上方是上次完成的结果。若上次距离填错，请点击“修正上次距离”。";
            resultText.Text = savedResult;
        }

        private void CorrectLastDistance()
        {
            if (measuring || captured.Count < 2 || savedDetails.Length == 0) return;
            repeated.Clear();
            savedCm = (double)distance.Value;
            // Only the actual distance is corrected. Preserve the captured axis/nominal/device.
            ComputeResult();
        }

        private void ComputeResult()
        {
            savedCpi = Measurement.Cpi(sumX, sumY, savedCm, savedAxis);
            UpdateMotion();
            double net = Math.Sqrt((double)sumX * sumX + (double)sumY * sumY);
            double ratio = net > 0 ? path / net : double.PositiveInfinity;
            bool axisBad = savedAxis == 1 ? Math.Abs((double)sumY) > Math.Abs((double)sumX) * 0.05 : savedAxis == 2 && Math.Abs((double)sumX) > Math.Abs((double)sumY) * 0.05;
            var trajectory = Measurement.Trajectory(captured);
            bool suspect = ratio > 1.05 || axisBad || savedCpi <= 0 || (trajectory.NetCounts > 0 && trajectory.MaxDeviationCounts / trajectory.NetCounts > 0.02);
            var r = Measurement.Rate(captured);
            string comparison = savedNominal > 0 ? string.Format("标称：{0:F0}    偏差：{1:+0.00;-0.00;0.00}%", savedNominal, (savedCpi / savedNominal - 1) * 100) : "直接测量（未设置标称 DPI）";
            savedResult = string.Format("实测 DPI / CPI：{0:F1}    {1}\r\n输入距离：{2:F2} cm    净计数 X：{3}    Y：{4}\r\n路径 / 净位移：{5:F3}    移动事件数：{6}\r\n回报率估算（中位间隔）：{7:F1} Hz    间隔：{8:F4} ms", savedCpi, comparison, savedCm, sumX, sumY, ratio, captured.Count, 1000 / r.MedianMs, r.MedianMs);
            resultText.Text = savedResult;
            savedResult += "\r\n以上结果以填写的实际桌面距离为前提；轨迹显示不是物理尺子。";
            savedDetails = savedResult;
            if (!suspect)
            {
                repeated.Add(savedCpi);
                double mean = repeated.Average();
                double spread = (repeated.Max() - repeated.Min()) / mean * 100;
                savedResult += string.Format("\r\n同设置有效测量：{0} 次    平均 DPI：{1:F1}    最大/最小跨度：{2:F2}%\r\n{3}", repeated.Count, mean, spread, repeated.Count < 3 ? "至少测 3 次再判断偏差。" : spread > 3 ? "重复结果波动较大：先检查端点、鼠标朝向和操作，再判断 DPI 偏差。" : "重复结果较一致；仍需确认尺子距离准确。");
            }
            else savedResult += "\r\n本次存在轨迹异常，未计入重复测量平均值。";
            resultText.Text = savedResult;
            correctDistance.Enabled = true;
        }

        private void CancelMeasurement()
        {
            if (!measuring) return;
            measuring = false;
            captured.Clear(); savedResult = ""; savedCpi = 0;
            savedDetails = ""; correctDistance.Enabled = false;
            UpdateMotion();
            SetRecording(false);
            resultText.Text = "本次测量已取消，未生成 DPI 结果。可以重新准备。";
        }

        private void Export()
        {
            if (measuring || savedResult.Length == 0) { MessageBox.Show("先完成一次 DPI 测量。"); return; }
            using (var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "dpi-measurement.csv" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var w = new StreamWriter(dialog.FileName, false, new System.Text.UTF8Encoding(true)))
                    {
                        w.WriteLine("# " + savedResult.Replace("\r\n", "\r\n# "));
                        w.WriteLine("Time_ms,X_count,Y_count");
                        double t0 = captured[0].Ms;
                        foreach (var s in captured) w.WriteLine((s.Ms - t0).ToString("F6", CultureInfo.InvariantCulture) + "," + s.X.ToString(CultureInfo.InvariantCulture) + "," + s.Y.ToString(CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception ex) { MessageBox.Show("导出失败：" + ex.Message); }
            }
        }
    }
}
