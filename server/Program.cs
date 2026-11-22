using QRCoder;
using Spell.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 4096);
var path = builder.Configuration["arena"] ?? throw new ArgumentException("Supply --arena <file> and --digest <trusted SHA-256>.");
var digest = builder.Configuration["digest"] ?? throw new ArgumentException("Missing --digest.");
if (new FileInfo(path).Length > 64 * 1024 * 1024) throw new InvalidDataException("Arena exceeds the pilot size limit.");
var arena = new ArenaPackage(File.ReadAllBytes(path), digest);
var founderKey = Environment.GetEnvironmentVariable("SPELL_FOUNDER_KEY");
if (string.IsNullOrWhiteSpace(founderKey) || founderKey.Length < 32)
    throw new ArgumentException("Set SPELL_FOUNDER_KEY to a private random key of at least 32 characters.");
var lobby = new DuelLobby(arena, TimeProvider.System);
var app = builder.Build();
app.UseWebSockets();
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
app.MapPost("/v1/session", (HttpContext ctx) =>
{
    if (Bearer(ctx) != founderKey) return Results.Unauthorized();
    var key = ctx.Request.Headers["Idempotency-Key"].ToString();
    var session = lobby.Create(key.Length == 0 ? null : key);
    return session is { } s ? Results.Ok(new { player = Describe(s.Seat), invitation = DescribeInvite(s.Invitation) })
        : Results.Conflict();
});
app.MapPost("/v1/invite", (HttpContext ctx) => lobby.Renew(Bearer(ctx)) is { } i
    ? Results.Ok(DescribeInvite(i)) : Results.Unauthorized());
app.MapPost("/v1/join", (JoinRequest req) => lobby.Join(req.Code, req.Attempt) is { } seat
    ? Results.Ok(Describe(seat)) : Results.Unauthorized());
app.MapGet("/v1/arena", (HttpContext ctx) => lobby.MayDownload(Bearer(ctx))
    ? Results.Bytes(arena.Bytes, "application/octet-stream") : Results.StatusCode(403));
app.MapGet("/v1/events", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
    string token = Bearer(ctx);
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
    try { while (await timer.WaitForNextTickAsync(stop)) lobby.Tick(); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
}
record JoinRequest(string Code, string? Attempt);
