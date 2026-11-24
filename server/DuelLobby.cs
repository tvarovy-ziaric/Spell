using System.Security.Cryptography;
using System.Threading.Channels;
using Spell.Protocol.V1;

namespace Spell.Server;

public sealed record Seat(string Role, string Token);
public sealed record Invitation(string Code, DateTimeOffset ExpiresAt);

public sealed class DuelLobby(ArenaPackage arena, TimeProvider time)
{
    public const uint ArenaRevision = 1;
    public const string Preset = "diagnostic-v1";
    private readonly object gate = new();
    private readonly Dictionary<string, Player> players = new();
    private Invitation? invitation;
    private (string Code, string Attempt, Seat Seat)? lastJoin;
    private (string Attempt, Seat Seat, Invitation Original)? founderRequest;
    private Phase phase = Phase.Preparing;
    private ulong revision;

    private sealed class Player(string role)
    {
        public string Role { get; } = role;
        public Channel<ServerFrame>? Connection;
        public bool Prepared;
        public bool Tracking;
        public ulong LastSequence;
        public long LastDeviceUs;
        public long ReceivedAt;
        public double? MinClockOffsetUs;
        public bool Ready => Prepared && Tracking && Connection != null;
    }

    public (Seat Seat, Invitation Invitation)? Create(string? attempt = null)
    {
        lock (gate)
        {
            if (attempt != null && !Guid.TryParseExact(attempt, "N", out _)) return null;
            if (founderRequest is { } f && f.Attempt == attempt)
                return (f.Seat, invitation ?? f.Original);
            if (players.Count != 0) return null;
            var seat = Add("founder");
            var invite = NewInvitation();
            if (attempt != null) founderRequest = (attempt, seat, invite);
            return (seat, invite);
        }
    }

    private Invitation NewInvitation()
    {
        invitation = new(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)), time.GetUtcNow().AddMinutes(5));
        return invitation;
    }

    public Invitation? Renew(string token)
    {
        lock (gate)
        {
            return players.Count == 1 && players.TryGetValue(token, out var player) && player.Role == "founder"
                ? NewInvitation() : null;
        }
    }

    public Seat? Join(string code, string? attempt = null)
    {
        lock (gate)
        {
            if (attempt != null && !Guid.TryParseExact(attempt, "N", out _)) return null;
            if (lastJoin is { } previous && previous.Code == code && previous.Attempt == attempt)
                return previous.Seat;
            if (invitation == null || invitation.Code != code || time.GetUtcNow() >= invitation.ExpiresAt)
                return null;
            invitation = null;
            var seat = Add("guest");
            if (attempt != null) lastJoin = (code, attempt, seat);
            return seat;
        }
    }

    private Seat Add(string role)
    {
        var seat = new Seat(role, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        players.Add(seat.Token, new Player(role));
        return seat;
    }

    public bool MayDownload(string token)
    {
        lock (gate) return players.ContainsKey(token) && phase != Phase.Playing;
    }

    public bool HasSeat(string token) { lock (gate) return players.ContainsKey(token); }
    public bool Paired { get { lock (gate) return players.Count == 2; } }
    public bool HasConnection { get { lock (gate) return players.Values.Any(p => p.Connection != null); } }

    public void Close()
    {
        lock (gate)
        {
            foreach (var player in players.Values) player.Connection?.Writer.TryComplete();
            players.Clear();
            invitation = null;
            lastJoin = null;
            founderRequest = null;
        }
    }

    public Channel<ServerFrame>? Connect(string token)
    {
        lock (gate)
        {
            if (!players.TryGetValue(token, out var player) || player.Connection != null) return null;
            var channel = Channel.CreateBounded<ServerFrame>(new BoundedChannelOptions(16)
                { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
            player.Connection = channel;
            channel.Writer.TryWrite(State());
            return channel;
        }
    }

    public void Disconnect(string token, Channel<ServerFrame> connection)
    {
        lock (gate)
        {
            if (!players.TryGetValue(token, out var p) || p.Connection != connection) return;
            bool wasReady = p.Ready;
            p.Connection = null;
            p.Prepared = p.Tracking = false;
            p.LastSequence = 0;
            p.LastDeviceUs = 0;
            p.MinClockOffsetUs = null;
            connection.Writer.TryComplete();
            Update(wasReady);
        }
    }

    public void Receive(string token, Channel<ServerFrame> connection, ClientFrame frame)
    {
        lock (gate)
        {
            if (!players.TryGetValue(token, out var p) || p.Connection != connection) return;
            bool wasReady = p.Ready;
            if (frame.Ping != null)
            {
                connection.Writer.TryWrite(new ServerFrame { Pong = new Pong { ClientUs = frame.Ping.ClientUs,
                    ServerUs = time.GetElapsedTime(0, time.GetTimestamp()).Ticks / 10 } });
                return;
            }
            if (frame.Ready is { } ready)
                p.Prepared = ready.LoadedInRam && ready.Calibrated && ready.ArenaRevision == ArenaRevision
                    && ready.ArenaSha256 == arena.Digest && ready.Preset == Preset;
            if (frame.Pose is { } pose && pose.Sequence > p.LastSequence && pose.EstimatedUs > p.LastDeviceUs)
            {
                p.LastSequence = pose.Sequence;
                p.LastDeviceUs = pose.EstimatedUs;
                p.ReceivedAt = time.GetTimestamp();
                // Minimum observed arrival offset cancels the arbitrary origin of the device clock.
                // A queued old packet cannot regain readiness after a transport stall.
                double arrivalUs = time.GetElapsedTime(0, p.ReceivedAt).TotalMicroseconds;
                double offsetUs = arrivalUs - pose.EstimatedUs;
                p.MinClockOffsetUs = Math.Min(p.MinClockOffsetUs ?? offsetUs, offsetUs);
                p.Tracking = offsetUs - p.MinClockOffsetUs.Value < 800_000 && PoseUsable(pose);
            }
            bool expired = ExpireTracking();
            Update(expired || wasReady != p.Ready);
        }
    }

    // A diagnostic quality gate; field accuracy and pilot thresholds are measured at M2.
    private bool PoseUsable(PoseSample s)
    {
        if (s.ArenaRevision != ArenaRevision || s.Tracking != Tracking.Good || s.Position == null ||
            s.Orientation == null || !s.HasLastVisualUs || s.LastVisualUs <= 0 || s.LastVisualUs > s.EstimatedUs ||
            s.EstimatedUs - s.LastVisualUs > 1_000_000 ||
            (s.HasLastGnssUs && (s.LastGnssUs <= 0 || s.LastGnssUs > s.EstimatedUs)) ||
            !s.HasPositionSigmaM || !s.HasAngleSigmaDeg ||
            !double.IsFinite(s.PositionSigmaM) || s.PositionSigmaM is < 0 or > 3 ||
            !double.IsFinite(s.AngleSigmaDeg) || s.AngleSigmaDeg is < 0 or > 10) return false;
        var q = s.Orientation;
        var norm = q.X*q.X + q.Y*q.Y + q.Z*q.Z + q.W*q.W;
        return norm is >= 0.99 and <= 1.01 && double.IsFinite(s.Position.Up) &&
            arena.Surface.IsInPlayArea(s.Position.East, s.Position.North);
    }

    public void Tick()
    {
        lock (gate) Update(ExpireTracking());
    }

    private bool ExpireTracking()
    {
        bool changed = false;
        foreach (var p in players.Values)
        {
            if (!p.Tracking || time.GetElapsedTime(p.ReceivedAt) < TimeSpan.FromMilliseconds(800)) continue;
            changed |= p.Ready;
            p.Tracking = false;
        }
        return changed;
    }

    private void Update(bool changed)
    {
        var next = players.Count == 2 && players.Values.All(p => p.Ready)
            ? Phase.Playing : phase == Phase.Preparing ? Phase.Preparing : Phase.Paused;
        if (next == phase && !changed) return;
        phase = next;
        revision++;
        var frame = State();
        foreach (var p in players.Values) p.Connection?.Writer.TryWrite(frame);
    }

    private ServerFrame State() => new() { Snapshot = new Snapshot { Phase = phase, Revision = revision,
        FounderReady = players.Values.Any(p => p.Role == "founder" && p.Ready),
        GuestReady = players.Values.Any(p => p.Role == "guest" && p.Ready) } };
}
