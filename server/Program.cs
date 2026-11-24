using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;
using Spell.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 4096);
var path = builder.Configuration["arena"] ?? throw new ArgumentException("Supply --arena <file> and --digest <trusted SHA-256>.");
var digest = builder.Configuration["digest"] ?? throw new ArgumentException("Missing --digest.");
if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Arena exceeds the pilot size limit.");
var arena = new ArenaPackage(File.ReadAllBytes(path), digest);
var founderKey = Environment.GetEnvironmentVariable("SPELL_FOUNDER_KEY");
var playerHosting = builder.Configuration.GetValue<bool>("player-hosting");
if (!playerHosting && (string.IsNullOrWhiteSpace(founderKey) || founderKey.Length < 32))
    throw new ArgumentException("Set SPELL_FOUNDER_KEY to a private random key of at least 32 characters.");
var sessions = new DuelSessions(arena, TimeProvider.System);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("player-create", ctx =>
    {
        var address = ctx.Connection.RemoteIpAddress;
        if (address != null && IPAddress.IsLoopback(address) &&
            IPAddress.TryParse(ctx.Request.Headers["CF-Connecting-IP"], out var client)) address = client;
        return RateLimitPartition.GetFixedWindowLimiter(address?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) });
    });
});
var app = builder.Build();
app.UseWebSockets();
app.UseRateLimiter();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    await next(ctx);
});
string Bearer(HttpContext ctx)
{
    var header = ctx.Request.Headers.Authorization.ToString();
    return header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : "";
}
object Describe(Seat seat) => new { seat, arenaRevision = DuelLobby.ArenaRevision, arenaSha256 = arena.Digest,
    preset = DuelLobby.Preset, arenaPath = "/v1/arena", socketPath = "/v1/events" };
object DescribeInvite(Invitation invite)
{
    var payload = "spell://join/v1/" + invite.Code;
    using var data = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.M);
    using var qr = new PngByteQRCode(data);
    return new { payload, invite.ExpiresAt, qrPngBase64 = Convert.ToBase64String(qr.GetGraphic(4)) };
}
app.MapGet("/healthz", () => Results.Text("ok"));
var createRoute = app.MapPost("/v1/session", (HttpContext ctx) =>
{
    if (!playerHosting && Bearer(ctx) != founderKey) return Results.Unauthorized();
    var key = ctx.Request.Headers["Idempotency-Key"].ToString();
    if (playerHosting && !Guid.TryParseExact(key, "N", out _)) return Results.BadRequest();
    var session = sessions.Create(key.Length == 0 ? null : key);
    return session is { } s ? Results.Ok(new { player = Describe(s.Seat), invitation = DescribeInvite(s.Invitation) })
        : Results.Conflict();
});
if (playerHosting) createRoute.RequireRateLimiting("player-create");
app.MapGet("/v1/session", (HttpContext ctx) => sessions.Find(Bearer(ctx)) is { } room
    ? Results.Ok(new { paired = room.Paired }) : Results.Unauthorized());
app.MapDelete("/v1/session", (HttpContext ctx) => sessions.End(Bearer(ctx))
    ? Results.NoContent() : Results.Unauthorized());
app.MapPost("/v1/invite", (HttpContext ctx) => sessions.Find(Bearer(ctx))?.Renew(Bearer(ctx)) is { } i
    ? Results.Ok(DescribeInvite(i)) : Results.Unauthorized());
app.MapPost("/v1/join", (JoinRequest req) => sessions.Join(req.Code, req.Attempt) is { } seat
    ? Results.Ok(Describe(seat)) : Results.Unauthorized());
app.MapGet("/v1/arena", (HttpContext ctx) => sessions.Find(Bearer(ctx))?.MayDownload(Bearer(ctx)) == true
    ? Results.Bytes(arena.Bytes, "application/octet-stream") : Results.StatusCode(403));
app.MapGet("/v1/events", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    string token = Bearer(ctx);
    var lobby = sessions.Find(token);
    if (lobby == null) { ctx.Response.StatusCode = 403; return; }
    var channel = lobby.Connect(token);
    if (channel == null) { ctx.Response.StatusCode = 403; return; }
    try
    {
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        await DuelSocket.Run(ws, channel, f => lobby.Receive(token, channel, f), ctx.RequestAborted);
    }
    finally { lobby.Disconnect(token, channel); }
});
var ticking = Tick(app.Lifetime.ApplicationStopping);
await app.RunAsync();
await ticking;

async Task Tick(CancellationToken stop)
{
    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
    try { while (await timer.WaitForNextTickAsync(stop)) sessions.Tick(); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
}
record JoinRequest(string Code, string? Attempt);
