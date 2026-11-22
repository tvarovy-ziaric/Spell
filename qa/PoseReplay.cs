using Spell.Client.Preflight;

internal static class PoseReplay
{
    internal static void Run(string path)
    {
        var clock = new PoseTimeline();
        long acceptedFixes = 0, sequence = 0, lastFix = 0, lastCamera = 0;
        foreach (string line in File.ReadLines(path).Skip(1))
        {
            var v = line.Split(',').Select(long.Parse).ToArray();
            if (v[1] > 0 && clock.ObserveGnss(v[1])) { lastFix = v[1]; acceptedFixes++; }
            if (v[2] > 0) { clock.ObserveVisual(v[2], v[0], v[3] == 1); lastCamera = Math.Max(lastCamera, v[2]); }
            bool emitted = clock.TryPublish(v[0], out var sample);
            if (emitted != (v[4] == 1) || (emitted && (sample.Sequence != ++sequence ||
                sample.LastGnssUs != lastFix || sample.LastCameraUs != lastCamera ||
                sample.HasRecentTracking != (v[5] == 1))))
                throw new Exception("Sensor timing replay failed at " + v[0]);
        }
        if (acceptedFixes != 2 || sequence != 16) throw new Exception("Incorrect replay counts.");
        Console.WriteLine($"PASS {sequence} publications, {acceptedFixes} distinct GNSS fixes, stale camera and ten-second gap");
    }
}
