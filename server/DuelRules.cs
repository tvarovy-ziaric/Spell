using System.Text.Json.Serialization;

namespace Spell.Server;

// Synthetic pilot values. Distances are metres, speed is metres per second.
public sealed record DuelRules(
    [property: JsonRequired] int HitPoints, [property: JsonRequired] int Damage, [property: JsonRequired] int AuraPenalty,
    [property: JsonRequired] double Speed, [property: JsonRequired] double Range, [property: JsonRequired] double Radius,
    [property: JsonRequired] double AuraWarning, [property: JsonRequired] double AuraCollision, [property: JsonRequired] bool FireballEnabled)
{
    public static DuelRules Default { get; } = new(100, 25, 0, 10, 20, 1, 1.5, 0.5, true);
    public bool IsValid() => HitPoints is >= 1 and <= 10000 && Damage is >= 0 and <= 10000 &&
        AuraPenalty is >= 0 and <= 10000 && Speed is >= 0.1 and <= 100 &&
        Range is >= 0.1 and <= 300 && Radius is >= 0 and <= 30 &&
        AuraWarning is >= 0.1 and <= 30 && AuraCollision is >= 0.1 && AuraCollision <= AuraWarning;
}
