using Spell.Geometry;
using Spell.Protocol.V1;

namespace Spell.Server;

// Owned by one DuelLobby under its existing lock. Client events never supply an origin, impact or HP.
internal sealed class CombatReferee(ArenaSurface surface, DuelRules rules)
{
    private sealed record Flight(FireballFlight Wire, double ErrorM);
    private readonly List<Flight> flights = [];
    private readonly Queue<Impact> impacts = new();
    private readonly int[] hp = [rules.HitPoints, rules.HitPoints];
    private readonly ulong[] lastCommand = [0, 0];
    private readonly ulong[] castPose = [0, 0];
    private ulong nextId;
    private long gameUs, lastFlightSnapshotUs;
    private bool collisionLatched;
    internal bool Finished => hp[0] == 0 || hp[1] == 0;
    internal string Safety { get; private set; } = "tracking";
    private string sight = "unknown";

    internal bool Evaluate(PoseSample? a, PoseSample? b, string unavailable, bool started)
    {
        string previous = Safety, previousSight = sight;
        if (a == null || b == null)
        {
            Safety = unavailable;
            sight = "unknown";
        }
        else
        {
            double e = a.Position.East - b.Position.East, n = a.Position.North - b.Position.North;
            double distance = Math.Sqrt(e * e + n * n);
            double error = a.PositionSigmaM + b.PositionSigmaM;
            Safety = distance + error <= rules.AuraCollision ? "collision"
                : distance - error <= rules.AuraCollision ? "uncertain"
                : distance - error <= rules.AuraWarning ? "warning" : "clear";
            sight = Visibility(a.Position, b.Position);
            if (Safety == "collision" && !collisionLatched && !Finished && started)
            {
                hp[0] = Math.Max(0, hp[0] - rules.AuraPenalty);
                hp[1] = Math.Max(0, hp[1] - rules.AuraPenalty);
                collisionLatched = true;
            }
            if (distance - error > rules.AuraCollision) collisionLatched = false;
        }
        return Safety != previous || sight != previousSight;
    }

    internal bool Cast(string owner, PoseSample? pose, CastFireball cmd, bool playing)
    {
        int index = Index(owner);
        if (cmd.CommandId <= lastCommand[index]) return false;
        lastCommand[index] = cmd.CommandId;
        if (!playing || Finished || !rules.FireballEnabled || pose == null || cmd.PoseSequence != pose.Sequence ||
            castPose[index] == pose.Sequence || flights.Count >= 32) return true;
        var q = pose.Orientation;
        var p = pose.Position;
        // Rotate the local +Z forward axis into the shared east/up/north frame.
        var path = FireballPath.Trace(surface, p.East, p.Up, p.North,
            2 * (q.X * q.Z + q.W * q.Y), 2 * (q.Y * q.Z - q.W * q.X),
            1 - 2 * (q.X * q.X + q.Y * q.Y), rules.Range);
        if (path.EndKind == FlightEndKind.Unknown || path.Distance <= 0) return true;
        castPose[index] = pose.Sequence;
        var flight = new FireballFlight
        {
            Id = ++nextId, Owner = owner, Start = p.Clone(),
            End = new Vector3M { East = path.EndEast, Up = path.EndUp, North = path.EndNorth },
            DurationUs = Math.Max(1, (long)Math.Ceiling(path.Distance / rules.Speed * 1e6)), StartGameUs = gameUs,
            Kind = path.EndKind.ToString().ToLowerInvariant()
        };
        double uncertainty = pose.PositionSigmaM + 2 * path.Distance * Math.Sin(pose.AngleSigmaDeg * Math.PI / 360);
        flights.Add(new Flight(flight, uncertainty));
        return true;
    }

    internal bool Interrupt()
    {
        foreach (var f in flights) Remember(f.Wire, "interrupted", gameUs);
        bool changed = flights.Count != 0;
        flights.Clear();
        return changed;
    }

    internal bool Advance(long elapsedUs, PoseSample a, PoseSample b)
    {
        gameUs += elapsedUs;
        if (flights.Count == 0) return false;
        // Evaluate simultaneous impacts as a group; both seats may lose their last HP.
        bool changed = false;
        foreach (var group in flights.Where(f => EndTime(f) <= gameUs).OrderBy(EndTime)
                     .GroupBy(EndTime).ToArray())
        {
            changed = true;
            foreach (var flight in group)
            {
                int target = 1 - Index(flight.Wire.Owner);
                string outcome = ImpactResult(flight, target == 0 ? a : b);
                if (outcome == "hit") hp[target] = Math.Max(0, hp[target] - rules.Damage);
                Remember(flight.Wire, outcome, group.Key);
                flights.Remove(flight);
            }
            if (Finished) { Interrupt(); break; }
        }
        if (!changed && gameUs - lastFlightSnapshotUs < 50_000) return false;
        lastFlightSnapshotUs = gameUs;
        return true;
    }

    private string ImpactResult(Flight flight, PoseSample target)
    {
        if (flight.Wire.Kind == "boundary") return "boundary";
        var p = target.Position;
        var end = flight.Wire.End;
        double e = end.East - p.East, u = end.Up - p.Up, n = end.North - p.North;
        double distance = Math.Sqrt(e*e + u*u + n*n), uncertainty = flight.ErrorM + target.PositionSigmaM;
        if (distance - uncertainty > rules.Radius) return "miss";
        if (distance + uncertainty > rules.Radius) return "disputed";
        // Begin a millimetre above the contact triangle to ignore the impact's own surface contact.
        var origin = end.Clone();
        origin.Up += 0.001;
        return Visibility(origin, p) switch { "clear" => "hit", "blocked" => "occluded", _ => "disputed" };
    }

    private string Visibility(Vector3M a, Vector3M b) =>
        surface.IntersectSegment(a.East, a.North, a.Up, b.East, b.North, b.Up, out _) switch
        {
            TerrainIntersection.Clear => "clear",
            TerrainIntersection.Hit => "blocked",
            _ => "unknown"
        };
    private void Remember(FireballFlight flight, string result, long when)
    {
        if (impacts.Count == 8) impacts.Dequeue();
        impacts.Enqueue(new Impact { Id = flight.Id, Position = flight.End.Clone(), GameUs = when, Result = result });
    }
    private static long EndTime(Flight f) => f.Wire.StartGameUs + f.Wire.DurationUs;
    private static int Index(string role) => role == "founder" ? 0 : 1;
    internal void ResetPoseSequence(string role) => castPose[Index(role)] = 0;

    internal CombatSnapshot State()
    {
        var result = new CombatSnapshot { Version = 1, GameUs = gameUs, FounderHp = hp[0], GuestHp = hp[1],
            Safety = Safety, Sight = sight, Finished = Finished, FounderCommand = lastCommand[0], GuestCommand = lastCommand[1] };
        result.Flights.Add(flights.Select(f => f.Wire));
        result.Impacts.Add(impacts);
        return result;
    }
}
