using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

namespace Spell.Server;

internal static class DiagnosticTape
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    internal sealed record Item(int Ms, int Kind, int Value, int Detail, int EastCm, int UpCm, int NorthCm)
    {
        public bool Valid(bool poses)
        {
            if (Ms is < 0 or > 604800000 || Kind is < 1 or > 9) return false;
            if (Kind == 9) return poses && Value is >= 0 and <= 100000 && Detail == 0 &&
                EastCm is >= -1000000 and <= 1000000 && UpCm is >= -1000000 and <= 1000000 &&
                NorthCm is >= -1000000 and <= 1000000;
            if (EastCm != 0 || UpCm != 0 || NorthCm != 0) return false;
            return Kind switch
            {
                1 or 2 or 4 => Value is 0 or 1 && Detail == 0,
                3 => Value is >= 0 and <= 3 && Detail is >= 1 and <= 240,
                5 => Value is >= 0 and <= 10000 && Detail is >= 0 and <= 10000,
                6 or 7 => Value > 0 && Detail == 0,
                8 => Value is >= 0 and <= 3 && Detail == 0,
                _ => false
            };
        }
    }
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    internal sealed record Page(int Version, int PageNumber, int Pages, int Dropped, Item[]? Events)
    {
        public bool Valid(bool poses)
        {
            if (Version != 1 || Pages is < 1 or > 22 || PageNumber < 0 || PageNumber >= Pages || Dropped < 0 ||
                Events == null || Events.Length is < 1 or > 12 || (PageNumber < Pages-1 && Events.Length != 12) ||
                PageNumber*12 + Events.Length > 256) return false;
            int previous = -1;
            foreach (var item in Events)
            {
                if (item == null || !item.Valid(poses) || item.Ms < previous) return false;
                previous = item.Ms;
            }
            return true;
        }
    }

    public static void Map(WebApplication app, string? connection, ITimeLimitedDataProtector permits)
    {
        bool allowPose = app.Configuration.GetValue<bool>("approved-diagnostic-poses");
        app.MapPost("/v1/diagnostics/events", async (HttpContext ctx, Page page, CancellationToken stop) =>
        {
            if (string.IsNullOrWhiteSpace(connection)) return Results.StatusCode(503);
            var header = ctx.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return Results.Unauthorized();
            string seat;
            try { seat = permits.Unprotect(header[7..]); }
            catch (CryptographicException) { return Results.Unauthorized(); }
            if (!page.Valid(allowPose)) return Results.BadRequest();
            var events = page.Events!;
            await using var db = new NpgsqlConnection(connection);
            await db.OpenAsync(stop);
            await using var tx = await db.BeginTransactionAsync(stop);
            // Serialize pages for this seat only. A recovered client may upload an acknowledged page again.
            await using var parent = new NpgsqlCommand("SELECT 1 FROM spell_diagnostic_batches WHERE seat_hash=$1 FOR UPDATE", db, tx);
            parent.Parameters.AddWithValue(seat);
            if (await parent.ExecuteScalarAsync(stop) == null) return Results.Conflict();
            if (page.PageNumber > 0)
            {
                await using var prior = new NpgsqlCommand("""
                    SELECT 1 FROM spell_diagnostic_events WHERE seat_hash=$1 AND page=$2 AND pages=$3 AND dropped=$4 AND last_ms<=$5
                    """, db, tx);
                prior.Parameters.AddWithValue(seat); prior.Parameters.AddWithValue(page.PageNumber-1);
                prior.Parameters.AddWithValue(page.Pages); prior.Parameters.AddWithValue(page.Dropped); prior.Parameters.AddWithValue(events[0].Ms);
                if (await prior.ExecuteScalarAsync(stop) == null) return Results.Conflict();
            }
            var data = JsonSerializer.Serialize(events);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO spell_diagnostic_events(seat_hash,page,pages,dropped,last_ms,events)
                VALUES($1,$2,$3,$4,$5,$6::jsonb) ON CONFLICT DO NOTHING
                """, db, tx);
            cmd.Parameters.AddWithValue(seat); cmd.Parameters.AddWithValue(page.PageNumber);
            cmd.Parameters.AddWithValue(page.Pages); cmd.Parameters.AddWithValue(page.Dropped);
            cmd.Parameters.AddWithValue(events[^1].Ms); cmd.Parameters.AddWithValue(data);
            await cmd.ExecuteNonQueryAsync(stop);
            await using var match = new NpgsqlCommand("""
                SELECT 1 FROM spell_diagnostic_events WHERE seat_hash=$1 AND page=$2 AND pages=$3 AND dropped=$4 AND events=$5::jsonb
                """, db, tx);
            match.Parameters.AddWithValue(seat); match.Parameters.AddWithValue(page.PageNumber);
            match.Parameters.AddWithValue(page.Pages); match.Parameters.AddWithValue(page.Dropped); match.Parameters.AddWithValue(data);
            if (await match.ExecuteScalarAsync(stop) == null) return Results.Conflict();
            await tx.CommitAsync(stop);
            return Results.NoContent();
        });
    }
}
