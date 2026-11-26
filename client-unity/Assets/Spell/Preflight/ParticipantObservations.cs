using System;
using System.Numerics;

namespace Spell.Client.Preflight
{
    // Confirmed direction only. The paired participant reference is an ephemeral game identifier.
    public readonly struct ParticipantBearing
    {
        public const int ContractVersion = 1;
        public readonly Guid ParticipantReference;
        public readonly long FrameUs;
        public readonly Vector3 Direction;
        public readonly double Confidence;
        internal ParticipantBearing(Guid participantReference,long frameUs,Vector3 direction,double confidence)
        {
            ParticipantReference=participantReference; FrameUs=frameUs; Direction=direction; Confidence=confidence;
        }
    }

    public readonly struct ParticipantCandidate
    {
        public readonly Guid Reference;
        public readonly bool Confirmed;
        public readonly Vector3 Bearing;
        public readonly double Confidence;
        public ParticipantCandidate(Guid reference,bool confirmed,Vector3 bearing,double confidence)
        {
            Reference=reference; Confirmed=confirmed; Bearing=bearing; Confidence=confidence;
        }
    }

    // Use the same monotonic clock for frame and receipt. Rebind on each new opted-in session.
    public sealed class ParticipantObservations
    {
        const long FreshnessUs=800_000;
        readonly Guid session, reference;
        long lastFrame, lastReceipt;
        ParticipantBearing bearing;
        bool present;
        public ParticipantObservations(Guid sessionId,Guid optedInParticipantReference)
        {
            if (sessionId==Guid.Empty || optedInParticipantReference==Guid.Empty)
                throw new ArgumentException("A session and opted-in participant reference are required.");
            session=sessionId; reference=optedInParticipantReference;
        }

        public bool Observe(int version,Guid sessionId,long frameUs,long nowUs,bool cameraTracking,
            ReadOnlySpan<ParticipantCandidate> candidates)
        {
            if (sessionId!=session) return false;
            if (version!=ParticipantBearing.ContractVersion || frameUs<=0 || nowUs<=0 ||
                nowUs<lastReceipt || frameUs>nowUs)
            { present=false; return false; }
            if (frameUs<=lastFrame) return false;
            lastFrame=frameUs; lastReceipt=nowUs;
            present=false;
            if (!cameraTracking || nowUs-frameUs>=FreshnessUs || candidates.Length!=1) return false;
            var candidate=candidates[0];
            if (!candidate.Confirmed || candidate.Reference!=reference || !(candidate.Confidence>0 && candidate.Confidence<=1) ||
                !(Math.Abs(candidate.Bearing.LengthSquared()-1)<.001f)) return false;
            bearing=new ParticipantBearing(reference,frameUs,Vector3.Normalize(candidate.Bearing),candidate.Confidence);
            present=true;
            return true;
        }

        public bool TryRead(long nowUs,out ParticipantBearing sample)
        {
            if (nowUs<lastReceipt || nowUs-lastFrame>=FreshnessUs) present=false;
            sample=present ? bearing : default;
            return present;
        }

        // Explicit loss invalidates the previous sample. A replay of the lost frame cannot restore it.
        public void LoseTracking(long nowUs)
        {
            present=false;
            lastReceipt=Math.Max(lastReceipt,nowUs);
            lastFrame=Math.Max(lastFrame,nowUs);
        }
    }
}
