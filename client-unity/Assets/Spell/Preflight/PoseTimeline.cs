namespace Spell.Client.Preflight
{
    // Camera and GNSS retain their original clock values. Visual freshness uses local receipt time.
    public readonly struct PoseTiming
    {
        public readonly long Sequence, EstimatedUs, LastGnssUs, LastCameraUs;
        public readonly bool HasRecentTracking;

        internal PoseTiming(long sequence, long now, long gnss, long frame, bool tracking)
        {
            Sequence = sequence;
            EstimatedUs = now;
            LastGnssUs = gnss;
            LastCameraUs = frame;
            HasRecentTracking = tracking;
        }
    }

    public sealed class PoseTimeline
    {
        private long lastFix, cameraFrame, visualReceipt, lastPublish, sequence;
        private bool tracking;

        public bool ObserveGnss(long fixUs)
        {
            if (fixUs <= lastFix) return false;
            lastFix = fixUs;
            return true;
        }

        public bool ObserveVisual(long frameUs, long receivedUs, bool isTracking)
        {
            if (frameUs <= cameraFrame || receivedUs <= 0 || receivedUs < visualReceipt) return false;
            cameraFrame = frameUs;
            visualReceipt = receivedUs;
            tracking = isTracking;
            return true;
        }

        public bool TryPublish(long nowUs, out PoseTiming sample)
        {
            sample = default;
            if (nowUs <= 0 || (sequence > 0 && nowUs - lastPublish < 100_000)) return false;
            lastPublish = nowUs;
            bool recent = tracking && nowUs >= visualReceipt && nowUs - visualReceipt < 800_000;
            sample = new PoseTiming(++sequence, nowUs, lastFix, cameraFrame, recent);
            return true;
        }
    }
}
