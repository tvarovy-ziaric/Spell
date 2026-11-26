namespace Spell.Server;

internal static class AdminRoutes
{
    public static void MapFounderAdmin(this WebApplication app, AdminStore? store, Func<HttpContext, string> bearer)
    {
        var group = app.MapGroup("/v1/admin");
        group.MapPost("/grant", async (Login login) =>
        {
            if (store == null) return Results.StatusCode(503);
            var token = await store.Grant(login);
            return token != null ? Results.Ok(new { version = 1, token }) : Results.Unauthorized();
        }).RequireRateLimiting("player-create");
        group.MapDelete("/grant", async (HttpContext ctx) =>
        {
            if (store == null) return Results.StatusCode(503);
            await store.Revoke(bearer(ctx));
            return Results.NoContent();
        });
        group.MapGet("/presets", async (HttpContext ctx, long? before) =>
        {
            if (store == null) return Results.StatusCode(503);
            if (await store.Owner(bearer(ctx)) is not { } owner) return Results.Unauthorized();
            if (before is <= 0) return Results.BadRequest();
            var page = await store.Page(owner, before ?? long.MaxValue);
            var items = page.Take(10).ToArray();
            return Results.Ok(new { version = 1, items, nextBefore = page.Count > 10 ? items[^1].Sequence : 0 });
        });
        group.MapPost("/presets", async (HttpContext ctx, PresetInput input) =>
        {
            if (store == null) return Results.StatusCode(503);
            if (await store.Owner(bearer(ctx)) is not { } owner) return Results.Unauthorized();
            if (input.Version != 1 || input.RequestId == Guid.Empty || input.Rules?.IsValid() != true) return Results.BadRequest();
            var saved = await store.Save(owner, input);
            return saved == null ? Results.Conflict() : Results.Ok(saved);
        });
    }
}
