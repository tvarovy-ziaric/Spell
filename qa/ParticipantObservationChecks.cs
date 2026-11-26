using System.Numerics;
using Google.Protobuf;
using Spell.Client.Preflight;
using Spell.Protocol.V1;

internal static class ParticipantObservationChecks
{
    public static void Run()
    {
        var session=Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var id=Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var wrong=Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        var stream=new ParticipantObservations(session,id);
        long t=10_000_000;
        int checks=0;
        void Check(bool ok,string label) { if (!ok) throw new Exception(label); checks++; }
        ParticipantCandidate Good() => new(id,true,Vector3.UnitX,.8);
        bool Next(params ParticipantCandidate[] candidates) => stream.Observe(1,session,++t,t,true,candidates);
        bool Read() => stream.TryRead(t,out _);
        Check(!Read(),"no input bypasses visual correction");
        Check(Next(Good()) && stream.TryRead(t,out var good) && good.Direction==Vector3.UnitX,"valid identified bearing");
        stream.TryRead(t,out var sample);
        var encoded=new VisualObservation { Subject=VisualObservation.Types.Subject.Participant,
            SubjectId=sample.ParticipantReference.ToString("N"),FrameUs=sample.FrameUs,
            Bearing=new Vector3M {East=sample.Direction.X,Up=sample.Direction.Y,North=sample.Direction.Z},
            Confidence=sample.Confidence,IdentityConfirmed=true };
        var wire=VisualObservation.Parser.ParseFrom(encoded.ToByteArray());
        Check(wire.SubjectId==id.ToString("N") && wire.Subject==VisualObservation.Types.Subject.Participant &&
            wire.Bearing.East==1 && wire.Confidence==.8 && wire.FrameUs==t && wire.IdentityConfirmed,"v1 wire round trip");
        Check(!Next(new ParticipantCandidate(wrong,true,Vector3.UnitX,.8)) && !Read(),"wrong identity");
        Check(!Next(new ParticipantCandidate(id,false,Vector3.UnitX,.8)) && !Read(),"unconfirmed identity");
        Check(!Next(Good(),Good()) && !Read(),"ambiguous multiple candidates");
        Check(!Next(new ParticipantCandidate(id,true,Vector3.Zero,.8)) && !Read(),"missing direction");
        Check(!Next(new ParticipantCandidate(id,true,new Vector3(float.NaN,0,0),.8)) && !Read(),"nonfinite direction");
        Check(!Next(new ParticipantCandidate(id,true,Vector3.UnitX,double.NaN)) && !Read(),"nonfinite confidence");
        Check(Next(Good()) && !stream.Observe(1,wrong,++t,t,true,new[]{Good()}) && Read(),"other session isolated");
        Check(!stream.Observe(1,session,t+1,t,true,new[]{Good()}) && !Read(),"future frame rejected");
        Check(Next(Good()) && !stream.Observe(1,session,t,t,true,new[]{Good()}),"duplicate ignored");
        Check(!stream.Observe(2,session,++t,t,true,new[]{Good()}) && !Read(),"future version rejected");
        Check(Next(Good()) && !stream.Observe(1,session,++t,t,false,new[]{Good()}) && !Read(),"limited tracking clears");
        Check(Next(Good()) && !Next() && !Read(),"missing candidate clears");
        Check(Next(Good()),"reacquire");
        stream.LoseTracking(t+3);
        Check(!stream.Observe(1,session,++t,t+4,true,new[]{Good()}) && !Read(),"late callback after loss");
        t+=10;
        Check(Next(Good()) && stream.TryRead(t+799999,out _) && !stream.TryRead(t+800000,out _),"freshness boundary");
        t+=2_000_000;
        Check(!stream.Observe(1,session,t-900000,t,true,new[]{Good()}) && !Read(),"stale capture even on fresh receipt");
        Check(Next(Good()) && !stream.TryRead(t-1,out _),"clock reversal invalidates sample");
        var timeline = new PoseTimeline(stream);
        t+=200000;
        Check(Next(Good()) && timeline.ObserveVisual(t,t,true) && timeline.TryPublish(t,out var timing) &&
            timing.ParticipantDirection?.ParticipantReference==id && timing.LastGnssUs==0,"sensor replay carries only fresh confirmed direction");
        t+=200000;
        Check(timeline.ObserveVisual(t,t,false) && timeline.TryPublish(t,out timing) && !timing.HasRecentTracking &&
            !timing.ParticipantDirection.HasValue && !Read(),"tracking loss revokes both sample and source");
        Console.WriteLine($"PASS {checks} participant reference, direction and loss checks");
    }
}
