using System;
using System.IO;
using System.Security.Cryptography;
using Spell.Geometry;

class Program
{
    static int count;
    static void Check(bool result, string label)
    {
        if (!result) throw new Exception(label);
        count++;
    }

    static void Reject(byte[] bytes, string checksum, string label)
    {
        try { ArenaSurface.Load(bytes, checksum); }
        catch (InvalidDataException) { count++; return; }
        throw new Exception(label);
    }

    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static int Main(string[] args)
    {
        var bytes = File.ReadAllBytes(args[0]);
        var sha = File.ReadAllText(Path.ChangeExtension(args[0], ".sha256")).Trim();
        var s = ArenaSurface.Load(bytes, sha);
        Check(s.Sha256 == sha && s.Dataset == "spell-synthetic-hill" && s.DatasetRevision == "1", "identity");
        Check(s.Width == 7 && s.Height == 7 && s.StepMeters == 1, "dimensions");
        Check(s.PlayableAreaValid && s.IsInPlayArea(1, 5) && !s.IsInPlayArea(.999, 2), "play boundary");
        Check(s.TryHeight(3, 3, out var z) && z == 106, "peak");
        Check(s.TryHeight(2.75, 3.25, out z) && z == 105, "slope");
        Check(s.TryHeight(1.25, 1.75, out z) && z == 100.5, "southwest-northeast triangulation");
        Check(s.TryHeight(6, 6, out z) && z == 100, "outer sample");
        Check(!s.TryHeight(0, 0, out z) && double.IsNaN(z), "NoData");
        Check(s.TryHeight(1, 1, out z) && z == 100, "ignore zero-weight NoData");
        Check(!s.TryHeight(0.1, 0.1, out z), "NoData interpolation");
        Check(!s.TryHeight(-0.1, 1, out z) && !s.TryHeight(double.NaN, 1, out z), "outside");
        Check(s.IntersectSegment(1, 3, 105, 5, 3, 105, out var t) == TerrainIntersection.Hit && t == .375, "ridge");
        Check(s.IntersectSegment(5, 3, 105, 1, 3, 105, out t) == TerrainIntersection.Hit && t == .375, "reverse ridge");
        Check(s.IntersectSegment(1, 3, 107, 5, 3, 107, out t) == TerrainIntersection.Clear, "above ridge");
        Check(s.IntersectSegment(2, 1, 100.5, 1, 2, 100.5, out t) == TerrainIntersection.Hit && t == .25,
              "diagonal ridge between endpoints in a single cell");
        Check(s.IntersectSegment(3, 3, 110, 3, 3, 102, out t) == TerrainIntersection.Hit && t == .5, "vertical");
        Check(s.IntersectSegment(3, 3, 106, 3, 3, 106, out t) == TerrainIntersection.Hit && t == 0, "touch");
        Check(s.IntersectSegment(.1, .1, 110, 3, 3, 110, out t) == TerrainIntersection.Unknown, "unknown span");
        Check(s.IntersectSegment(1, 3, 107, 9, 3, 107, out t) == TerrainIntersection.Unknown, "outside segment");
        var corrupt = (byte[])bytes.Clone();
        corrupt[8] = 9;
        Reject(corrupt, sha, "corruption");
        Reject(corrupt, Hash(corrupt), "unsupported version");
        Reject(bytes, "", "missing digest");
        var truncated = bytes[..^1];
        Reject(truncated, Hash(truncated), "truncation");
        var oversized = (byte[])bytes.Clone();
        oversized[12] = 255;
        oversized[13] = 255;
        Reject(oversized, Hash(oversized), "oversized dimensions");
        // Clear the peak in both the elevation payload and its mask.
        var hole = (byte[])bytes.Clone();
        int countSamples = s.Width * s.Height;
        int maskStart = hole.Length - (countSamples + 7) / 8;
        Array.Clear(hole, maskStart - 4 * countSamples + 24 * 4, 4);
        hole[maskStart + 24 / 8] &= 0xfe;
        Check(!ArenaSurface.Load(hole, Hash(hole)).PlayableAreaValid, "NoData in play area");
        Console.WriteLine($"{count} geometry checks passed.");
        CalibrationChecks.Run();
        return 0;
    }
}
