#nullable enable
using Spell.Protocol.V1;

namespace Spell.Client.Preflight
{
    // A transient snapshot only. Authentication and session scope are provided by the duel socket.
    public sealed class RemoteSeatReader
    {
        ArenaSeatPose? latest;
        long pingSentUs, offsetUpperUs, synchronizedAtUs;
        bool synchronized;
        public void Reset()
        {
            latest = null;
            pingSentUs = synchronizedAtUs = 0;
            synchronized = false;
        }

        public void Ping(long nowUs) => pingSentUs = nowUs;
        public void Pong(long echoUs, long serverUs, long nowUs)
        {
            if (echoUs != pingSentUs || echoUs <= 0 || nowUs < echoUs || nowUs - echoUs >= 800_000 ||
                serverUs <= 0) return;
            // Server processed the ping after it was sent. This gives an UPPER bound on server now.
            offsetUpperUs = serverUs - echoUs;
            synchronizedAtUs = nowUs;
            synchronized = true;
            pingSentUs = 0;
        }

        public void Replace(Snapshot snapshot, bool localFounder)
        {
            latest = null;
            var combat = snapshot?.Combat;
            if (snapshot?.Phase != Phase.Playing || combat?.Version != 1 || combat.Finished) return;
            var p = localFounder ? combat.GuestPose : combat.FounderPose;
            if (p?.Position == null || p.Orientation == null || p.Sequence == 0 ||
                p.EstimatedServerUs <= 0 || p.EstimatedServerUs > combat.ServerUs ||
                combat.ServerUs - p.EstimatedServerUs >= 800_000) return;
            latest = p;
        }

        public bool Fresh(long nowUs)
        {
            if (!synchronized || latest == null || nowUs < synchronizedAtUs || nowUs - synchronizedAtUs > 10_000_000)
                return false;
            // Allow 1 ms of clock-rate drift per elapsed second since synchronization.
            double age = (double)nowUs + offsetUpperUs - latest.EstimatedServerUs +
                (nowUs - synchronizedAtUs) * .001;
            return age >= 0 && age < 800_000;
        }

        public bool TryRead(long nowUs, out ArenaSeatPose pose)
        {
            pose = Fresh(nowUs) ? latest!.Clone() : null!;
            return pose != null;
        }
    }
}
