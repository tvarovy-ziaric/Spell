#nullable enable
namespace Spell.Client.Preflight
{
    // Camera and GNSS retain their original clock values. Visual freshness uses local receipt time.
    public readonly struct PoseTiming
    {
        public readonly long Sequence, EstimatedUs, LastGnssUs, LastCameraUs, VisualReceiptUs;
        public readonly bool HasRecentTracking;
        public readonly ParticipantBearing? ParticipantDirection;

        internal PoseTiming(long sequence, long now, long gnss, long frame, long receipt, bool tracking, ParticipantBearing? participant)
        {
            Sequence = sequence;
            EstimatedUs = now;
            LastGnssUs = gnss;
            LastCameraUs = frame;
            VisualReceiptUs = receipt;
            HasRecentTracking = tracking;
            ParticipantDirection = participant;
        }
    }

    public sealed class PoseTimeline
    {
        private long lastFix, cameraFrame, visualReceipt, lastPublish, sequence;
        private bool tracking;
        readonly ParticipantObservations? participant;
        public PoseTimeline(ParticipantObservations? participant = null) { this.participant = participant; }

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
            if (!tracking) participant?.LoseTracking(receivedUs);
            return true;
        }

        public bool TryPublish(long nowUs, out PoseTiming sample)
        {
            sample = default;
            if (nowUs <= 0 || (sequence > 0 && nowUs - lastPublish < 100_000)) return false;
            lastPublish = nowUs;
            bool recent = tracking && nowUs >= visualReceipt && nowUs - visualReceipt < 800_000;
            ParticipantBearing? direction = null;
            if (recent && participant != null && participant.TryRead(nowUs, out var current))
                direction = current;
            sample = new PoseTiming(++sequence, nowUs, lastFix, cameraFrame, visualReceipt, recent, direction);
            return true;
        }
    }
}
