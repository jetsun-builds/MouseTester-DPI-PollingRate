using System;
using System.Collections.Generic;
using System.Linq;

namespace MouseTester
{
    public sealed class MotionSample
    {
        public double Ms;
        public int X, Y;
        public MotionSample(double ms, int x, int y) { Ms = ms; X = x; Y = y; }
    }

    public sealed class RateResult
    {
        public int Intervals, Gaps;
        public double MedianMs, P99Ms, AllHz, MovingHz;
    }

    public sealed class TrajectoryResult
    {
        public double NetCounts, PathCounts, MaxDeviationCounts;
    }

    public static class Measurement
    {
        public static TrajectoryResult Trajectory(IList<MotionSample> samples)
        {
            var result = new TrajectoryResult();
            double endX = samples.Sum(s => (double)s.X), endY = samples.Sum(s => (double)s.Y);
            result.NetCounts = Math.Sqrt(endX * endX + endY * endY);
            double x = 0, y = 0;
            foreach (var s in samples)
            {
                x += s.X; y += s.Y;
                result.PathCounts += Math.Sqrt((double)s.X * s.X + (double)s.Y * s.Y);
                if (result.NetCounts > 0) result.MaxDeviationCounts = Math.Max(result.MaxDeviationCounts, Math.Abs(x * endY - y * endX) / result.NetCounts);
            }
            return result;
        }

        // Counts per second over the last available motion interval window.
        public static double RecentSpeed(IList<MotionSample> samples, double windowMs)
        {
            if (samples.Count < 2 || windowMs <= 0) return 0;
            int last = samples.Count - 1, first = last;
            while (first > 0 && samples[last].Ms - samples[first].Ms < windowMs) first--;
            double dt = samples[last].Ms - samples[first].Ms;
            if (dt <= 0) return 0;
            double counts = 0;
            // First delta precedes the first timestamp: exclude it from this interval estimate.
            for (int i = first + 1; i <= last; i++) counts += Math.Sqrt((double)samples[i].X * samples[i].X + (double)samples[i].Y * samples[i].Y);
            return counts * 1000 / dt;
        }

        // Arrival-time estimates, not a USB bus measurement. Keep gap counts visible.
        public static RateResult Rate(IList<MotionSample> samples)
        {
            var intervals = new List<double>();
            for (int i = 1; i < samples.Count; i++)
            {
                double dt = samples[i].Ms - samples[i - 1].Ms;
                if (dt > 0) intervals.Add(dt);
            }
            var result = new RateResult { Intervals = intervals.Count };
            if (intervals.Count == 0) return result;
            var sorted = intervals.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            result.MedianMs = sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
            result.P99Ms = sorted[(int)Math.Ceiling(sorted.Count * 0.99) - 1];
            result.AllHz = 1000.0 * intervals.Count / intervals.Sum();
            var moving = intervals.Where(x => x <= 20).ToList();
            result.Gaps = intervals.Count - moving.Count;
            if (moving.Count > 0) result.MovingHz = 1000.0 * moving.Count / moving.Sum();
            return result;
        }

        public static double Cpi(long x, long y, double cm, int axis)
        {
            if (cm <= 0 || double.IsNaN(cm) || double.IsInfinity(cm)) throw new ArgumentOutOfRangeException("cm");
            double counts = axis == 1 ? Math.Abs((double)x) : axis == 2 ? Math.Abs((double)y) : Math.Sqrt((double)x * x + (double)y * y);
            return counts * 2.54 / cm;
        }
    }
}
