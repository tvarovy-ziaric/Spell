using System.Text.Json;
namespace Spell.Server;

internal sealed record FieldSurvey(int Version, string ArenaSha256, double[] BoardA, double[] BoardB)
{
    public static FieldSurvey? Load(string? filename, ArenaPackage arena)
    {
        if (string.IsNullOrEmpty(filename)) return null;
        if (new FileInfo(filename).Length > 4096) throw new InvalidDataException("Survey file exceeds limit.");
        var value = JsonSerializer.Deserialize<FieldSurvey>(File.ReadAllBytes(filename), JsonSerializerOptions.Web);
        bool Valid(double[]? p) => p is { Length: 3 } && p.All(double.IsFinite) &&
            arena.Surface.IsInPlayArea(p[0], p[2]) && Math.Abs(p[1]) <= 10000;
        if (value == null || value.Version != 1 || value.ArenaSha256 != arena.Digest ||
            !Valid(value.BoardA) || !Valid(value.BoardB)) throw new InvalidDataException("Survey and arena differ.");
        double dx = value.BoardB[0]-value.BoardA[0], dz = value.BoardB[2]-value.BoardA[2];
        if (dx*dx + dz*dz < 1) throw new InvalidDataException("Surveyed baseline too short.");
        return value;
    }
}
