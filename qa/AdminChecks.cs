using System.Security.Cryptography;
using Spell.Server;

internal static class AdminChecks
{
    public static void Run()
    {
        var rules = DuelRules.Default;
        if (!rules.IsValid() || (rules with { Range = double.NaN }).IsValid() ||
            (rules with { Speed = double.PositiveInfinity }).IsValid() || (rules with { Radius = -1 }).IsValid() ||
            (rules with { AuraWarning = .1, AuraCollision = .2 }).IsValid() || (rules with { HitPoints = 0 }).IsValid())
            throw new Exception("Invalid preset rules validation.");
        byte[] salt = new byte[16];
        var encoded = Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(
            Rfc2898DeriveBytes.Pbkdf2("synthetic", salt, 210000, HashAlgorithmName.SHA256, 32));
        if (!AdminStore.PasswordMatches("synthetic", encoded) || AdminStore.PasswordMatches("wrong", encoded) ||
            AdminStore.PasswordMatches("a", "broken") || AdminStore.PasswordMatches("a", "abc:xyz"))
            throw new Exception("Private founder password checks failed.");
        Console.WriteLine("PASS founder password and preset bounds");
    }
}
