using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace Spell.Server;

// Store only pilot counters. No coordinates, participant identity, access tokens or camera frames.
internal static class DiagnosticBatches
{
    internal sealed record Counts(int Version, int GnssFixes, int VisualFrames, int Estimates,
        int SentPoses, int TerrainBytes, int TerrainRequests, int PeakThermalStatus)
    {
        public bool Valid => Version == 1 && GnssFixes is >= 0 and <= 50_000_000 &&
            VisualFrames is >= 0 and <= 50_000_000 && Estimates is >= 0 and <= 50_000_000 &&
            SentPoses >= 0 && SentPoses <= Estimates && TerrainBytes >= 0 && TerrainRequests >= 0 &&
            PeakThermalStatus is >= -1 and <= 6;
    }

    static ITimeLimitedDataProtector Protector(WebApplication app) => app.Services.GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("Spell.diagnostics.1").ToTimeLimitedDataProtector();

    internal static string? Permit(WebApplication app, string? connection, string token) =>
        string.IsNullOrWhiteSpace(connection) ? null : Protector(app).Protect(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), TimeSpan.FromHours(48));

    public static void MapDiagnosticBatches(this WebApplication app, string? connection)
    {
        var permits = Protector(app);
        DiagnosticTape.Map(app, connection, permits);

        app.MapPost("/v1/diagnostics/batch", async (HttpContext ctx, Counts counts, CancellationToken stop) =>
        {
            if (string.IsNullOrWhiteSpace(connection)) return Results.StatusCode(503);
            string key;
            try { key = permits.Unprotect(Bearer(ctx)); }
            catch (CryptographicException) { return Results.Unauthorized(); }
            if (!counts.Valid) return Results.BadRequest();
            string data = System.Text.Json.JsonSerializer.Serialize(counts);
            await using var db = new NpgsqlConnection(connection);
            await db.OpenAsync(stop);
            await using var insert = new NpgsqlCommand(
                "INSERT INTO spell_diagnostic_batches (seat_hash, counters) VALUES ($1,$2::jsonb) ON CONFLICT DO NOTHING", db);
            insert.Parameters.AddWithValue(key);
            insert.Parameters.AddWithValue(data);
            await insert.ExecuteNonQueryAsync(stop);
            await using var match = new NpgsqlCommand(
                "SELECT counters = $2::jsonb FROM spell_diagnostic_batches WHERE seat_hash=$1", db);
            match.Parameters.AddWithValue(key);
            match.Parameters.AddWithValue(data);
            return await match.ExecuteScalarAsync(stop) is true ? Results.NoContent() : Results.Conflict();
        });
    }

    public static async Task MaintainAsync(string connection, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var db = new NpgsqlConnection(connection);
                await db.OpenAsync(stop);
                await using var command = new NpgsqlCommand(
                    "CREATE TABLE IF NOT EXISTS spell_diagnostic_batches (seat_hash text PRIMARY KEY, counters jsonb NOT NULL, received_at timestamptz NOT NULL DEFAULT now()); " +
                    "CREATE TABLE IF NOT EXISTS spell_diagnostic_events (seat_hash text REFERENCES spell_diagnostic_batches(seat_hash) ON DELETE CASCADE, page int NOT NULL, pages int NOT NULL, dropped int NOT NULL, last_ms int NOT NULL, events jsonb NOT NULL, PRIMARY KEY(seat_hash,page)); " +
                    "DELETE FROM spell_diagnostic_batches WHERE received_at < now() - interval '7 days'", db);
                await command.ExecuteNonQueryAsync(stop);
            }
            catch (NpgsqlException) when (!stop.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stop);
                continue;
            }
            await Task.Delay(TimeSpan.FromHours(1), stop);
        }
    }

    static string Bearer(HttpContext ctx)
    {
        string header = ctx.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : "";
    }
}
