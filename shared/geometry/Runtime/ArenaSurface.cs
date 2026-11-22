using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Spell.Geometry
{
    public enum TerrainIntersection { Clear, Hit, Unknown }

    // Horizontal queries use metre offsets from the southwest sample. Elevations are absolute.
    public sealed class ArenaSurface
    {
        public const int FormatVersion = 1;
        public const int GeometryVersion = 1;
        public const int MaxSide = 2048;
        const int MaxBytes = 18 * 1024 * 1024;
        readonly float[] samples;
        readonly byte[] valid;
        public string Sha256 { get; }
        public int Width { get; }
        public int Height { get; }
        public double StepMeters { get; }
        public double OriginEasting { get; }
        public double OriginNorthing { get; }
        public double West { get; }
        public double South { get; }
        public double East { get; }
        public double North { get; }
        public int HorizontalEpsg { get; }
        public int VerticalEpsg { get; }
        public string Dataset { get; }
        public string DatasetRevision { get; }
        public bool PlayableAreaValid { get; }
        public long ResidentSampleBytes => (long)samples.Length * sizeof(float) + valid.Length;

        public static ArenaSurface Load(byte[] bytes, string expectedSha256)
        {
            if (bytes == null || bytes.Length < 90 || bytes.Length > MaxBytes)
                throw new InvalidDataException("Arena package length is outside supported bounds.");
            if (expectedSha256 == null || expectedSha256.Length != 64)
                throw new InvalidDataException("A trusted SHA-256 digest is required.");
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Arena SHA-256 does not match the session.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
                return new ArenaSurface(reader, hash);
        }

        ArenaSurface(BinaryReader input, string hash)
        {
            Sha256 = hash;
            if (Encoding.ASCII.GetString(input.ReadBytes(8)) != "SPLARENA" ||
                input.ReadUInt16() != FormatVersion || input.ReadUInt16() != GeometryVersion)
                throw new InvalidDataException("Unsupported Spell arena or geometry version.");
            Width = input.ReadInt32();
            Height = input.ReadInt32();
            if (Width < 2 || Width > MaxSide || Height < 2 || Height > MaxSide)
                throw new InvalidDataException("Invalid grid dimensions.");
            HorizontalEpsg = input.ReadInt32();
            VerticalEpsg = input.ReadInt32();
            if (!(HorizontalEpsg == 0 && VerticalEpsg == 0) &&
                !(HorizontalEpsg == 8353 && VerticalEpsg == 8357))
                throw new InvalidDataException("Unsupported horizontal or vertical reference system.");
            OriginEasting = input.ReadDouble();
            OriginNorthing = input.ReadDouble();
            StepMeters = input.ReadDouble();
            West = input.ReadDouble();
            South = input.ReadDouble();
            East = input.ReadDouble();
            North = input.ReadDouble();
            if (!Finite(OriginEasting) || !Finite(OriginNorthing) || StepMeters != 1 ||
                !(0 <= West && West < East && East <= Width - 1 &&
                  0 <= South && South < North && North <= Height - 1))
                throw new InvalidDataException("Invalid grid spacing, origin or play boundary.");
            int datasetBytes = input.ReadUInt16(), revisionBytes = input.ReadUInt16();
            int count = Width * Height;
            int maskBytes = (count + 7) / 8;
            if (datasetBytes < 1 || datasetBytes > 256 || revisionBytes < 1 || revisionBytes > 256 ||
                input.BaseStream.Length - input.BaseStream.Position != datasetBytes + revisionBytes + count * 4 + maskBytes)
                throw new InvalidDataException("Truncated, oversized or inconsistent Spell arena.");
            var utf8 = new UTF8Encoding(false, true);
            try
            {
                Dataset = utf8.GetString(input.ReadBytes(datasetBytes));
                DatasetRevision = utf8.GetString(input.ReadBytes(revisionBytes));
            }
            catch (DecoderFallbackException ex) { throw new InvalidDataException("Invalid dataset identifier.", ex); }
            samples = new float[count];
            for (int i = 0; i < count; i++)
                samples[i] = input.ReadSingle();
            valid = input.ReadBytes(maskBytes);
            if (count % 8 != 0 && (valid[maskBytes - 1] >> (count % 8)) != 0)
                throw new InvalidDataException("Unexpected mask padding.");
            for (int i = 0; i < count; i++)
                if (!Finite(samples[i]) || (!Valid(i) && samples[i] != 0))
                    throw new InvalidDataException("Invalid elevation or nonzero NoData sample.");
            PlayableAreaValid = CheckPlayableArea();
        }

        bool Valid(int i) => (valid[i / 8] & (1 << (i % 8))) != 0;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        bool Covers(double e, double n) => e >= 0 && n >= 0 && e <= Width - 1 && n <= Height - 1;
        public bool IsInPlayArea(double e, double n) => West <= e && e <= East && South <= n && n <= North;

        bool CheckPlayableArea()
        {
            for (int row = (int)Math.Floor(South); row <= Math.Ceiling(North); row++)
                for (int col = (int)Math.Floor(West); col <= Math.Ceiling(East); col++)
                    if (!Valid(row * Width + col)) return false;
            return true;
        }

        public bool TryHeight(double eastMeters, double northMeters, out double elevationMeters)
        {
            elevationMeters = double.NaN;
            if (!Covers(eastMeters, northMeters)) return false;
            int x = Math.Min((int)eastMeters, Width - 2);
            int y = Math.Min((int)northMeters, Height - 2);
            double u = eastMeters - x, v = northMeters - y;
            int sw = y * Width + x, ne = sw + Width + 1;
            int side = u >= v ? sw + 1 : sw + Width;
            double wSw = 1 - Math.Max(u, v), wNe = Math.Min(u, v), wSide = Math.Abs(u - v);
            if ((wSw > 0 && !Valid(sw)) || (wNe > 0 && !Valid(ne)) || (wSide > 0 && !Valid(side)))
                return false;
            elevationMeters = wSw * samples[sw] + wSide * samples[side] + wNe * samples[ne];
            return true;
        }

        // Visit every crossed cell and triangle in parameter order. The terrain and segment
        // are linear on each interval, so a thin ridge cannot be skipped between endpoints.
        public TerrainIntersection IntersectSegment(double e0, double n0, double z0,
            double e1, double n1, double z1, out double fraction)
        {
            fraction = double.NaN;
            if (!Covers(e0, n0) || !Covers(e1, n1) || !Finite(z0) || !Finite(z1))
                return TerrainIntersection.Unknown;
            double de = e1 - e0, dn = n1 - n0, dz = z1 - z0;
            if (!TryHeight(e0, n0, out double h0)) return TerrainIntersection.Unknown;
            if (z0 <= h0) { fraction = 0; return TerrainIntersection.Hit; }
            int xLine = de > 0 ? (int)Math.Floor(e0) + 1 : (int)Math.Ceiling(e0) - 1;
            int yLine = dn > 0 ? (int)Math.Floor(n0) + 1 : (int)Math.Ceiling(n0) - 1;
            double t = 0;
            while (t < 1)
            {
                double tx = de == 0 ? double.PositiveInfinity : (xLine - e0) / de;
                double ty = dn == 0 ? double.PositiveInfinity : (yLine - n0) / dn;
                double next = Math.Min(1, Math.Min(tx, ty));
                double mid = (t + next) / 2;
                int cellX = Math.Min((int)(e0 + mid * de), Width - 2);
                int cellY = Math.Min((int)(n0 + mid * dn), Height - 2);
                if (de != dn)
                {
                    double diagonal = (cellX - cellY - (e0 - n0)) / (de - dn);
                    if (diagonal > t && diagonal < next)
                    {
                        var first = TriangleInterval(e0, n0, z0, de, dn, dz, t, diagonal, out fraction);
                        if (first != TerrainIntersection.Clear) return first;
                        t = diagonal;
                    }
                }
                var result = TriangleInterval(e0, n0, z0, de, dn, dz, t, next, out fraction);
                if (result != TerrainIntersection.Clear) return result;
                if (tx == next) xLine += Math.Sign(de);
                if (ty == next) yLine += Math.Sign(dn);
                t = next;
            }
            return TerrainIntersection.Clear;
        }

        TerrainIntersection TriangleInterval(double e, double n, double z, double de, double dn, double dz,
            double a, double b, out double fraction)
        {
            fraction = double.NaN;
            double mid = (a + b) / 2;
            if (!TryHeight(e + de * mid, n + dn * mid, out _) ||
                !TryHeight(e + de * a, n + dn * a, out double ha) ||
                !TryHeight(e + de * b, n + dn * b, out double hb))
                return TerrainIntersection.Unknown;
            double gapA = z + dz * a - ha, gapB = z + dz * b - hb;
            if (gapA > 0 && gapB > 0) return TerrainIntersection.Clear;
            fraction = gapA <= 0 ? a : a + (b - a) * gapA / (gapA - gapB);
            return TerrainIntersection.Hit;
        }
    }
}
