using Spell.Protocol.V1;
using Spell.Server;

internal static class LobbyChecks
{
    private sealed class Clock : TimeProvider
    {
        public long Us = 1;
        public override long TimestampFrequency => 1_000_000;
        public override long GetTimestamp() => Us;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(Us * 10);
    }

    internal static void Run(ArenaPackage arena)
    {
        var time = new Clock();
        var lobby = new DuelLobby(arena, time);
        var created = lobby.Create()!.Value;
        time.Us += 300_000_000;
        if (lobby.Join(created.Invitation.Code) != null) throw new Exception("Expired code accepted.");
        var invite = lobby.Renew(created.Seat.Token)!;
        var guests = Enumerable.Range(0, 4).AsParallel().Select(_ => lobby.Join(invite.Code)).OfType<Seat>().ToList();
        if (guests.Count != 1) throw new Exception("Concurrent join consumed more than one seat.");

        string token = created.Seat.Token;
        var channel = lobby.Connect(token)!;
        if (lobby.Connect(token) != null) throw new Exception("Duplicate connection accepted.");
        var ready = new Ready { ArenaRevision = 1, ArenaSha256 = arena.Digest, Preset = DuelLobby.Preset,
            LoadedInRam = true, Calibrated = true };
        lobby.Receive(token, channel, new ClientFrame { Ready = ready });
        var s = new PoseSample { Sequence = 1, EstimatedUs = 100_000, LastVisualUs = 100_000,
            Tracking = Tracking.Good, Position = new Vector3M { East = 3, North = 3 },
            Orientation = new Orientation { W = 1 }, ArenaRevision = 1, PositionSigmaM = 0.2, AngleSigmaDeg = 1 };
        bool lastReady = false;
        void Expect(bool value)
        {
            while (channel.Reader.TryRead(out var f)) if (f.Snapshot != null) lastReady = f.Snapshot.FounderReady;
            if (lastReady != value) throw new Exception($"Unexpected readiness at sequence {s.Sequence}.");
        }
        void Push(bool expected)
        {
            lobby.Receive(token, channel, new ClientFrame { Pose = s });
            Expect(expected);
            s.Sequence++;
            s.EstimatedUs += 100_000;
            s.LastVisualUs = s.EstimatedUs;
        }
        Push(true);
        s.PositionSigmaM = 500; Push(false);
        s.PositionSigmaM = 0.2; Push(true);
        s.AngleSigmaDeg = double.NaN; Push(false);
        s.AngleSigmaDeg = 1; s.Position.East = 500; Push(false);
        s.Position.East = 3; Push(true);
        s.LastGnssUs = s.EstimatedUs + 1; Push(false);
        s.ClearLastGnssUs(); Push(true);
        ready.Calibrated = false;
        lobby.Receive(token, channel, new ClientFrame { Ready = ready }); Expect(false);
        ready.Calibrated = true;
        ready.LoadedInRam = false;
        lobby.Receive(token, channel, new ClientFrame { Ready = ready }); Expect(false);
        ready.LoadedInRam = true;
        lobby.Receive(token, channel, new ClientFrame { Ready = ready }); Expect(true);
        time.Us += 2_000_000;
        lobby.Tick(); Expect(false);
        Push(false); // Queued frame has a new sequence but was captured before the stall.
        s.EstimatedUs += 2_000_000;
        s.LastVisualUs = s.EstimatedUs;
        Push(true);
        time.Us += 800_000;
        lobby.Tick(); Expect(false);
        s.Sequence = 1;
        Push(false);
        Console.WriteLine("PASS expired and concurrent invites, arena boundary, uncertainty, stale pose and readiness revocation");
    }
}
