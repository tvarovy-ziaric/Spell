using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace Spell.Server;

public sealed record AdminConfig(string? Postgres, string? PasswordHash)
{
    public static AdminConfig Load()
    {
        var path = Environment.GetEnvironmentVariable("SPELL_ADMIN_CONFIG") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spell", "server", "admin.config.json");
        var config = File.Exists(path) ? JsonSerializer.Deserialize<AdminConfig>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
        return new(Environment.GetEnvironmentVariable("SPELL_POSTGRES") ?? config?.Postgres,
            Environment.GetEnvironmentVariable("SPELL_ADMIN_PASSWORD_HASH") ?? config?.PasswordHash);
    }
}

public sealed record SavedPreset(long Sequence, string Id, DateTime CreatedAt, DuelRules Rules);
public sealed record Login(string? Password, string? DeviceSecret);
public sealed record PresetInput(int Version, Guid RequestId, DuelRules? Rules);

public sealed class AdminStore(NpgsqlDataSource db, string encodedPassword)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    const string Projection = "seq, id, created_at, rules::text";
    static byte[] Digest(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
    static bool IsSecret(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static bool PasswordMatches(string? password, string encoded)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 256) return false;
        var parts = encoded.Split(':');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            return salt.Length == 16 && expected.Length == 32 && CryptographicOperations.FixedTimeEquals(
                expected, Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32));
        }
        catch (FormatException) { return false; }
    }
    readonly SemaphoreSlim schemaLock = new(1);
    volatile bool initialized;
    public async Task Initialize()
    {
        if (initialized) return;
        await schemaLock.WaitAsync();
        try
        {
            if (initialized) return;
            await CreateSchema();
            initialized = true;
        }
        finally { schemaLock.Release(); }
    }
    async Task CreateSchema()
    {
        await using var cmd = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS spell_founder_grants (
                digest bytea PRIMARY KEY, owner bytea NOT NULL, expires_at timestamptz NOT NULL);
            CREATE INDEX IF NOT EXISTS spell_grant_expiry ON spell_founder_grants(expires_at);
            CREATE TABLE IF NOT EXISTS spell_presets (
                seq bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, id uuid NOT NULL UNIQUE,
                owner bytea NOT NULL, request_id uuid NOT NULL, created_at timestamptz NOT NULL DEFAULT now(),
                rules jsonb NOT NULL, UNIQUE(owner,request_id));
            CREATE INDEX IF NOT EXISTS spell_preset_history ON spell_presets(owner,seq DESC);
            """);
        await cmd.ExecuteNonQueryAsync();
    }
    public async Task<string?> Grant(Login login)
    {
        if (!IsSecret(login.DeviceSecret) || !PasswordMatches(login.Password, encodedPassword)) return null;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await using var clean = db.CreateCommand("DELETE FROM spell_founder_grants WHERE expires_at <= now()");
        await clean.ExecuteNonQueryAsync();
        await using var cmd = db.CreateCommand("INSERT INTO spell_founder_grants VALUES ($1,$2,now()+interval '30 days')");
        cmd.Parameters.Add(new() { Value = Digest(token) });
        cmd.Parameters.Add(new() { Value = Digest(login.DeviceSecret!.ToUpperInvariant()) });
        await cmd.ExecuteNonQueryAsync();
        return token;
    }
    public async Task<byte[]?> Owner(string token)
    {
        if (!IsSecret(token)) return null;
        await using var cmd = db.CreateCommand("SELECT owner FROM spell_founder_grants WHERE digest=$1 AND expires_at>now()");
        cmd.Parameters.Add(new() { Value = Digest(token) });
        return await cmd.ExecuteScalarAsync() as byte[];
    }
    public async Task Revoke(string token)
    {
        if (!IsSecret(token)) return;
        await using var cmd = db.CreateCommand("DELETE FROM spell_founder_grants WHERE digest=$1");
        cmd.Parameters.Add(new() { Value = Digest(token) });
        await cmd.ExecuteNonQueryAsync();
    }
    static SavedPreset Read(NpgsqlDataReader row) => new(row.GetInt64(0),
        "preset-v1-" + row.GetGuid(1).ToString("N"), row.GetDateTime(2),
        JsonSerializer.Deserialize<DuelRules>(row.GetString(3), Json)!);
    public async Task<SavedPreset?> Save(byte[] owner, PresetInput input)
    {
        await using var insert = db.CreateCommand("""
            INSERT INTO spell_presets(id,owner,request_id,rules) VALUES ($1,$2,$3,$4)
            ON CONFLICT(owner,request_id) DO NOTHING
            """);
        insert.Parameters.Add(new() { Value = Guid.NewGuid() });
        insert.Parameters.Add(new() { Value = owner });
        insert.Parameters.Add(new() { Value = input.RequestId });
        insert.Parameters.Add(new() { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = JsonSerializer.Serialize(input.Rules, Json) });
        await insert.ExecuteNonQueryAsync();
        await using var cmd = db.CreateCommand($"SELECT {Projection} FROM spell_presets WHERE owner=$1 AND request_id=$2");
        cmd.Parameters.Add(new() { Value = owner });
        cmd.Parameters.Add(new() { Value = input.RequestId });
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var saved = Read(reader);
        return saved.Rules == input.Rules ? saved : null;
    }
    public async Task<List<SavedPreset>> Page(byte[] owner, long before)
    {
        await using var cmd = db.CreateCommand($"SELECT {Projection} FROM spell_presets WHERE owner=$1 AND seq<$2 ORDER BY seq DESC LIMIT 11");
        cmd.Parameters.Add(new() { Value = owner });
        cmd.Parameters.Add(new() { Value = before });
        await using var reader = await cmd.ExecuteReaderAsync();
        var page = new List<SavedPreset>();
        while (await reader.ReadAsync()) page.Add(Read(reader));
        return page;
    }
    public async Task<SavedPreset?> ById(byte[] owner, string id)
    {
        if (!id.StartsWith("preset-v1-", StringComparison.Ordinal) || !Guid.TryParseExact(id[10..], "N", out var parsed)) return null;
        await using var cmd = db.CreateCommand($"SELECT {Projection} FROM spell_presets WHERE owner=$1 AND id=$2");
        cmd.Parameters.Add(new() { Value = owner });
        cmd.Parameters.Add(new() { Value = parsed });
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }
}
