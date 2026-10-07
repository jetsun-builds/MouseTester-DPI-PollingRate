using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace MouseTester
{
    public sealed class MotionView : Control
    {
        private IList<MotionSample> samples = new List<MotionSample>();
        private double scaleCpi = 6000;
        private bool finished;
        public MotionView()
        {
            DoubleBuffered = true; Dock = DockStyle.Fill; BackColor = Color.White;
        }
        public void UpdateMotion(IList<MotionSample> data, double cpi, bool done)
        {
            // Copy on the UI thread; raw-input callbacks never paint or calculate statistics.
            samples = data.ToArray(); scaleCpi = cpi; finished = done;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawString("移动轨迹：蓝色较慢 → 绿色 → 橙色较快；灰色虚线连接起点和终点", Font, Brushes.Black, 12, 8);
            if (samples.Count < 2)
            {
                g.DrawString("将鼠标放在桌面尺子起点，按 F5；沿直线移动后停稳按 F6。", Font, Brushes.DimGray, 12, 48);
                g.DrawLine(Pens.LightGray, 35, 95, Math.Max(40, Width - 35), 95);
                return;
            }
            var stats = Measurement.Trajectory(samples);
            double unitFactor = scaleCpi > 0 ? 2.54 / scaleCpi : 1;
            double averageCountsSpeed = stats.PathCounts * 1000 / Math.Max(1, samples[samples.Count - 1].Ms - samples[0].Ms);
            var points = new List<PointF> { new PointF(0, 0) };
            var times = new List<double> { samples[0].Ms };
            var paths = new List<double> { 0 };
            double x = 0, y = 0, path = 0;
            // Preserve all samples in the numerical quality calculation. Downsample only drawing.
            int stride = Math.Max(1, (samples.Count + 649) / 650);
            for (int i = 0; i < samples.Count; i++)
            {
                x += samples[i].X; y += samples[i].Y;
                path += Math.Sqrt((double)samples[i].X * samples[i].X + (double)samples[i].Y * samples[i].Y);
                if (i % stride == 0 || i == samples.Count - 1)
                { points.Add(new PointF((float)x, (float)y)); times.Add(samples[i].Ms); paths.Add(path); }
            }
            float minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            float minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
            double spanX = Math.Max(1, maxX - minX), spanY = Math.Max(1, maxY - minY);
            float left = 30, top = 48, areaW = Math.Max(10, Width - 60), areaH = Math.Max(10, Height - 135);
            double fit = Math.Min(areaW / spanX, areaH / spanY);
            float ox = left + (areaW - (float)(spanX * fit)) / 2;
            float oy = top + (areaH - (float)(spanY * fit)) / 2;
            var screen = points.Select(p => new PointF(ox + (float)((p.X - minX) * fit), oy + (float)((p.Y - minY) * fit))).ToList();
            using (var dashed = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dash }) g.DrawLine(dashed, screen[0], screen[screen.Count - 1]);
            for (int i = 1; i < screen.Count; i++)
            {
                // Average over at least 50ms to reduce raw event-arrival jitter.
                int j = i - 1;
                while (j > 0 && times[i] - times[j] < 50) j--;
                double dt = times[i] - times[j];
                double speed = dt > 0 ? (paths[i] - paths[j]) * unitFactor / (dt / 1000) : 0;
                double low = scaleCpi > 0 ? 5 : averageCountsSpeed * 0.7, high = scaleCpi > 0 ? 15 : averageCountsSpeed * 1.4;
                using (var pen = new Pen(speed < low ? Color.RoyalBlue : speed < high ? Color.SeaGreen : Color.DarkOrange, 2)) g.DrawLine(pen, screen[i - 1], screen[i]);
            }
            var first = screen[0]; var last = screen[screen.Count - 1];
            g.FillEllipse(Brushes.RoyalBlue, first.X - 4, first.Y - 4, 8, 8);
            g.FillEllipse(Brushes.DarkOrange, last.X - 4, last.Y - 4, 8, 8);
            double recentSpeed = Measurement.RecentSpeed(samples, 150) * unitFactor;
            if (!finished && Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency - samples[samples.Count - 1].Ms > 150) recentSpeed = 0;
            string quality = stats.NetCounts > 0 ? string.Format("路径/净位移 {0:F3}（越接近 1 越直）    最大横向偏离 {1:F2}%", stats.PathCounts / stats.NetCounts, stats.MaxDeviationCounts / stats.NetCounts * 100) : "无净位移，可能来回移动";
            g.DrawString(quality, Font, Brushes.Black, 12, Height - 72);
            g.DrawString(string.Format("{0} {1:F1} {2}    {3}", finished ? "末段平均速度" : "当前速度", recentSpeed, scaleCpi > 0 ? "cm/s" : "计数/秒", scaleCpi > 0 ? "颜色：<5 / 5–15 / >=15 cm/s" : "颜色表示本次相对速度，结束后换算 cm/s"), Font, Brushes.Black, 12, Height - 48);
            g.DrawString(finished ? "速度按本次实测 DPI 换算；轨迹自动等比例缩放，屏幕尺寸不是桌面厘米。" : scaleCpi > 0 ? "速度暂按标称 DPI 估算；轨迹自动等比例缩放，屏幕尺寸不是桌面厘米。" : "未知 DPI 时先显示原始计数速度；轨迹不依赖 DPI，屏幕尺寸不是桌面厘米。", Font, Brushes.DimGray, 12, Height - 25);
        }
    }
}
