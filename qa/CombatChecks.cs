using Google.Protobuf;
using Spell.Geometry;
using Spell.Protocol.V1;
using Spell.Server;

internal static class CombatChecks
{
    private sealed class Clock : TimeProvider
    {
        public long Now = 1_000_000;
        public override long TimestampFrequency => 1_000_000;
        public override long GetTimestamp() => Now;
    }

    private sealed class Pair
    {
        readonly Clock clock = new();
        readonly DuelLobby lobby;
        readonly string[] tokens;
        readonly System.Threading.Channels.Channel<ServerFrame>[] channels;
        readonly Ready ready;
        readonly PoseSample[] poses = new PoseSample[2];
        readonly Snapshot[] state = new Snapshot[2];

        public Pair(ArenaPackage arena, DuelRules? rules = null)
        {
            lobby = new DuelLobby(arena, clock);
            lobby.ConfigureRules("test", rules ?? DuelRules.Default);
            var created = lobby.Create()!.Value;
            tokens = [created.Seat.Token, lobby.Join(created.Invitation.Code)!.Token];
            channels = [lobby.Connect(tokens[0])!, lobby.Connect(tokens[1])!];
            ready = new Ready { ArenaRevision = 1, ArenaSha256 = arena.Digest, Preset = "test",
                LoadedInRam = true, Calibrated = true, CombatVersion = 1 };
            for (int i = 0; i < 2; i++)
            {
                Send(i, new ClientFrame { Ready = ready });
                poses[i] = new PoseSample { ArenaRevision = 1, Tracking = Tracking.Good,
                    Orientation = new Orientation { Y = Math.Sqrt(.5), W = Math.Sqrt(.5) },
                    Position = new Vector3M(), PositionSigmaM = .01, AngleSigmaDeg = .1 };
            }
            Move(0, 1, 105, 3);
            Move(1, 2.45, 105.1, 3);
        }

        public void Move(int seat, double east, double up, double north, double sigma = .01, long delayUs = 0)
        {
            var p = poses[seat];
            p.Sequence++;
            p.EstimatedUs = Math.Max(clock.Now - delayUs, p.EstimatedUs + 1);
            p.LastVisualUs = p.EstimatedUs;
            p.Position = new Vector3M { East = east, Up = up, North = north };
            p.PositionSigmaM = sigma;
            Send(seat, new() { Pose = p });
        }
        public void Cast(ulong id, ulong pose = 1) => Send(0, new ClientFrame {
            Cast = new CastFireball { CommandId = id, PoseSequence = pose } });
        public void Advance(long us) { clock.Now += us; lobby.Tick(); }
        public void Disconnect() => lobby.Disconnect(tokens[1], channels[1]);
        public void Reconnect()
        {
            channels[1] = lobby.Connect(tokens[1])!;
            Send(1, new ClientFrame { Ready = ready });
            Move(1, 2.45, 105.1, 3);
        }
        public Snapshot State(int seat = 0)
        {
            while (channels[seat].Reader.TryRead(out var frame)) if (frame.Snapshot != null) state[seat] = frame.Snapshot;
            return state[seat];
        }
        private void Send(int seat, ClientFrame frame) =>
            lobby.Receive(tokens[seat], channels[seat], ClientFrame.Parser.ParseFrom(frame.ToByteArray()));
    }

    internal static void Run(ArenaPackage arena)
    {
        var s = arena.Surface;
        var t = FireballPath.Trace(s, 1, 105, 3, 1, 0, 0, 20);
        Check(t.EndKind == FlightEndKind.Terrain && t.Distance == 1.5 && t.EndEast == 2.5, "ridge intersection");
        t = FireballPath.Trace(s, 1, 107, 3, 1, 0, 0, 2);
        Check(t.EndKind == FlightEndKind.Range && t.EndEast == 3, "range");
        t = FireballPath.Trace(s, 1, 107, 3, 1, 0, 0, 40);
        Check(t.EndKind == FlightEndKind.Boundary && t.Distance == 4, "play boundary before package edge");
        Check(FireballPath.Trace(s, 0, 107, 3, 0, 0, 0, 20).EndKind == FlightEndKind.Unknown, "outside");
        Check(FireballPath.Trace(s, 1, 107, 3, double.NaN, 0, 0, 20).EndKind == FlightEndKind.Unknown, "NaN");
        Check(FireballPath.Trace(s, 1, 107, 3, 0, 0, 0, 20).EndKind == FlightEndKind.Unknown, "zero");
        var pair = new Pair(arena);
        Check(pair.State().Phase == Phase.Playing, "two versions negotiated");
        var firstState = pair.State();
        var remote = new Spell.Client.Preflight.RemoteSeatReader();
        remote.Replace(firstState, true);
        Check(!remote.Fresh(100), "read requires monotonic sync");
        remote.Ping(100); remote.Pong(100, firstState.Combat.ServerUs, 200);
        Check(remote.TryRead(201, out var other) && other.Position.East == 2.45 && other.Sequence == 1,
            "founder reads only guest arena pose");
        other.Position.East = 999;
        Check(remote.TryRead(202, out other) && other.Position.East == 2.45, "consumer cannot alter authoritative state");
        Check(!remote.Fresh(800200), "delayed snapshot expires locally");
        remote.Replace(firstState, false);
        Check(remote.TryRead(203, out other) && other.Position.East == 1, "guest reads founder");
        remote.Replace(new Snapshot { Phase=Phase.Paused, Combat=firstState.Combat }, true);
        Check(!remote.Fresh(205), "pause revokes a previous sample");
        remote.Reset(); remote.Replace(firstState,true);
        Check(!remote.Fresh(206), "reconnect invalidates synchronization");
        Check(FireballPath.Trace(s,1,107,3,1,0,0,4).EndKind==FlightEndKind.Boundary,
            "range exactly at edge cannot damage");
        Check(FireballPath.Trace(s,1,106,1,4,-6,0,20).EndKind==FlightEndKind.Boundary,
            "terrain contact exactly at edge cannot damage");
        pair.Cast(1);
        var cast = pair.State().Combat.Flights.Single();
        Check(cast.Kind == "terrain" && Math.Abs(cast.DurationUs - 150000) <= 1, "duration from authoritative path, speed");
        pair.Cast(1);
        pair.Advance(100_000);
        Check(pair.State().Combat is { GuestHp: 100, Flights.Count: 1 }, "no early damage");
        pair.Advance(100_000);
        Check(pair.State().Combat.Impacts.Single().Result == "hit" && pair.State(1).Combat.GuestHp == 75, "shared impact HP");
        pair.Cast(1);
        Check(pair.State().Combat.GuestHp == 75 && pair.State().Combat.Flights.Count == 0, "duplicate");
        pair.Cast(2, 99);
        Check(pair.State().Combat.Flights.Count == 0, "wrong pose id");
        pair.Move(1, 2.8, 105.7, 3, .5);
        pair.Move(0, 1, 105, 3);
        pair.Cast(3, 2);
        pair.Advance(200_000);
        Check(pair.State().Combat.Impacts.Last().Result == "disputed" && pair.State().Combat.GuestHp == 75, "uncertain preserves HP");
        pair.Move(0, 1, 105, 3);
        pair.Cast(4, 3);
        pair.Disconnect();
        Check(pair.State().Phase == Phase.Paused && pair.State().Combat.Impacts.Last().Result == "interrupted", "interrupt");
        pair.Advance(200_000);
        pair.Reconnect();
        Check(pair.State(1).Combat.GuestHp == 75 && pair.State().Phase == Phase.Playing, "reconnect snapshot");
        pair.Cast(4, 3);
        Check(pair.State().Combat.Flights.Count == 0, "retry after reconnect");
        pair.Move(1, 7, 105, 3);
        Check(pair.State().Combat.Safety == "boundary" && pair.State().Phase == Phase.Paused, "outside hold");
        var delayed = new Pair(arena);
        delayed.Advance(700_000);
        delayed.Move(0, 1, 105, 3);
        delayed.Move(1, 2.5, 105.1, 3, delayUs: 650_000);
        delayed.Advance(100_000);
        Check(delayed.State().Combat.GuestPose.Position.East == 2.5, "movement published even without active flight");
        delayed.Advance(51_000);
        Check(delayed.State().Phase == Phase.Paused && delayed.State().Combat.GuestPose == null,
            "ingress delay counts towards expiration; peer pose removed");
        delayed.Move(1, 2.5, double.MaxValue, 3);
        Check(delayed.State().Combat.GuestPose == null, "value not representable on mobile rejected");
        pair = new Pair(arena, DuelRules.Default with { AuraPenalty = 20, Radius = 3 });
        pair.Move(1, 4, 105, 3);
        Check(pair.State().Combat.Sight == "blocked", "ridge hides opponent");
        pair.Cast(1);
        pair.Advance(200_000);
        Check(pair.State().Combat.Impacts.Last().Result == "occluded" && pair.State().Combat.GuestHp == 100, "ridge screens effect");
        pair.Move(1, 1.6, 105, 3, .25);
        Check(pair.State().Combat.Safety == "uncertain" && pair.State().Combat.GuestHp == 100, "uncertain aura");
        pair.Move(1, 1.2, 105, 3);
        Check(pair.State().Combat.Safety == "collision" && pair.State().Combat.GuestHp == 80, "confirmed collision");
        pair.Advance(100_000);
        Check(pair.State().Combat.FounderHp == 80, "aura applied once");
        pair.Move(1, 3, 105, 3);
        Check(pair.State().Phase == Phase.Playing, "aura clear resumes");
        pair = new Pair(arena, DuelRules.Default with { HitPoints = 1 });
        pair.Cast(1); pair.Advance(200_000);
        Check(pair.State().Combat is { GuestHp: 0, Finished: true } && pair.State().Phase != Phase.Playing, "terminal HP");
        pair = new Pair(arena, DuelRules.Default with { FireballEnabled = false });
        pair.Cast(1);
        Check(pair.State().Combat.Flights.Count == 0, "disabled preset");
        Console.WriteLine("PASS shared flight predictor, referee, dedup/reconnect, terrain/disputed impacts, aura, bounds, terminal HP");
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("Combat check failed: " + label);
    }
}
