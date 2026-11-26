using System;

namespace Spell.Geometry
{
    public enum FlightEndKind { Unknown, Range, Terrain, Boundary }

    public readonly struct FireballPath
    {
        public readonly FlightEndKind EndKind;
        public readonly double EndEast, EndUp, EndNorth, Distance;

        FireballPath(FlightEndKind endKind, double e, double u, double n, double distance)
        {
            EndKind = endKind;
            EndEast = e;
            EndUp = u;
            EndNorth = n;
            Distance = distance;
        }

        public static FireballPath Trace(ArenaSurface arena, double east, double up, double north,
            double dirEast, double dirUp, double dirNorth, double range)
        {
            if (arena == null || !arena.IsInPlayArea(east, north) || !Finite(up) ||
                !Finite(range) || range <= 0) return default;
            double norm = Math.Sqrt(dirEast * dirEast + dirUp * dirUp + dirNorth * dirNorth);
            if (!Finite(norm) || norm < 1e-9) return default;
            dirEast /= norm;
            dirUp /= norm;
            dirNorth /= norm;
            double boundary = Math.Min(ToEdge(east, dirEast, arena.West, arena.East),
                ToEdge(north, dirNorth, arena.South, arena.North));
            double limit = Math.Min(range, boundary);
            // Clamp floating-point roundoff at a boundary before querying the grid.
            double e = Math.Max(arena.West, Math.Min(arena.East, east + dirEast * limit));
            double n = Math.Max(arena.South, Math.Min(arena.North, north + dirNorth * limit));
            double u = up + dirUp * limit;
            var hit = arena.IntersectSegment(east, north, up, e, n, u, out double fraction);
            if (hit == TerrainIntersection.Unknown) return default;
            if (hit == TerrainIntersection.Hit && (range < boundary || limit * (1 - fraction) > 1e-9))
                return new FireballPath(FlightEndKind.Terrain, east + (e - east) * fraction,
                    up + (u - up) * fraction, north + (n - north) * fraction, limit * fraction);
            return new FireballPath(boundary <= range ? FlightEndKind.Boundary : FlightEndKind.Range, e, u, n, limit);
        }

        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        static double ToEdge(double start, double direction, double min, double max)
        {
            if (direction > 0) return (max - start) / direction;
            if (direction < 0) return (min - start) / direction;
            return double.PositiveInfinity;
        }
    }
}
