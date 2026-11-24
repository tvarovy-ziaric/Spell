using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Google.Protobuf;
using Spell.Client.Preflight;
using Spell.Protocol.V1;
using Spell.Server;

var folder = args[0];
PoseReplay.Run(Path.Combine(folder, "timing.csv"));
var bytes = File.ReadAllBytes(Path.Combine(folder, "hill.bytes"));
var digest = File.ReadAllText(Path.Combine(folder, "hill.sha256")).Trim();
var surface = ArenaPreflight.Load(bytes, digest);
var serverArena = new ArenaPackage(bytes, digest);
LobbyChecks.Run(serverArena);
Assert(surface.TryHeight(3, 3, out var elevation) && elevation == 106 &&
       serverArena.Surface.TryHeight(3, 3, out var serverHeight) && elevation == serverHeight, "shared summit");
Assert(!surface.TryHeight(0, 0, out _), "fixture NoData");
var damaged = bytes.ToArray();
damaged[^1] ^= 1;
Reject(() => ArenaPreflight.Load(damaged, digest));
Reject(() => new ArenaPackage(damaged, digest));
var unsupported = bytes.ToArray();
unsupported[8] = 255;
var unknownHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(unsupported)).ToLowerInvariant();
Reject(() => ArenaPreflight.Load(unsupported, unknownHash));
Reject(() => new ArenaPackage(unsupported, unknownHash));
Console.WriteLine("PASS client preflight and server arena acceptance, corruption, unsupported version and NoData");

var poseClock = Stopwatch.StartNew();
using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
var ct = deadline.Token;
using var http = new HttpClient { BaseAddress = new Uri(args[1]) };
bool playerHosting = args.Skip(2).Contains("--player-hosting");
using var unauthorized = await http.PostAsync("v1/session", null, ct);
Assert(unauthorized.StatusCode == (playerHosting ? HttpStatusCode.BadRequest : HttpStatusCode.Unauthorized),
    "creation requires a unique attempt or private founder credential");
if (!playerHosting)
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("SPELL_FOUNDER_KEY"));
http.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
var created = await Post("v1/session", null);
var founder = created.GetProperty("player");
string founderToken = Token(founder);
Assert(GuestInvite.SupportsSeat("founder", founder.GetProperty("seat").GetProperty("role").GetString(), founderToken,
    founder.GetProperty("arenaRevision").GetInt32(), founder.GetProperty("preset").GetString(),
    founder.GetProperty("arenaSha256").GetString(), founder.GetProperty("arenaPath").GetString()), "mobile accepts founder");
var founderRetry = await Post("v1/session", null);
Assert(Token(founderRetry.GetProperty("player")) == founderToken, "founder recovered original seat");
http.DefaultRequestHeaders.Remove("Idempotency-Key");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", founderToken);
Assert(!(await http.GetFromJsonAsync<JsonElement>("v1/session", ct)).GetProperty("paired").GetBoolean(), "waiting for guest");
var invite = created.GetProperty("invitation");
Assert(Convert.FromBase64String(invite.GetProperty("qrPngBase64").GetString()!)[1] == 80, "QR image");
string payload = invite.GetProperty("payload").GetString()!;
Assert(GuestInvite.TryParse("https://localhost:5081", payload, out _, out var code), "mobile parses issued invite");
Assert(!GuestInvite.TryParse("http://localhost:5081", payload, out _, out _) &&
       !GuestInvite.TryParse("https://localhost:5081/moved", payload, out _, out _) &&
       !GuestInvite.TryParse("https://localhost:5081", payload + "/extra", out _, out _), "invalid mobile inputs");
http.DefaultRequestHeaders.Authorization = null;
string attempt = Guid.NewGuid().ToString("N");
var joined = await Post("v1/join", new { code, attempt });
string guestToken = Token(joined);
Assert(GuestInvite.Supports(joined.GetProperty("seat").GetProperty("role").GetString()!, guestToken,
    joined.GetProperty("arenaRevision").GetInt32(), joined.GetProperty("preset").GetString()!,
    joined.GetProperty("arenaSha256").GetString()!, joined.GetProperty("arenaPath").GetString()!),
    "mobile accepts exact server descriptor");
var afterLostResponse = await Post("v1/join", new { code, attempt });
Assert(Token(afterLostResponse) == guestToken, "same client recovered its original seat");
using var reuse = await http.PostAsJsonAsync("v1/join", new { code }, ct);
Assert(reuse.StatusCode == HttpStatusCode.Unauthorized, "one-time invitation");

foreach (var token in new[] { founderToken, guestToken })
{
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    Assert((await http.GetFromJsonAsync<JsonElement>("v1/session", ct)).GetProperty("paired").GetBoolean(), "both see pairing");
    var downloaded = await http.GetByteArrayAsync("v1/arena", ct);
    Assert(ArenaPreflight.Load(downloaded, digest).Sha256 == digest, "both clients preloaded");
}
using var a = await Connect(founderToken);
using var b = await Connect(guestToken);
await Until(a, s => s.Phase == Phase.Preparing);
await Until(b, s => s.Phase == Phase.Preparing);
var ready = new Ready { ArenaRevision = DuelLobby.ArenaRevision, ArenaSha256 = digest, Preset = DuelLobby.Preset,
    LoadedInRam = true, Calibrated = true };
await Send(a, new() { Ready = ready });
await Send(a, new() { Pose = Pose(1) });
await Until(b, s => s.FounderReady && !s.GuestReady);
var wrong = ready.Clone();
wrong.ArenaSha256 = new string('0', 64);
await Send(b, new() { Ready = wrong });
await Send(b, new() { Pose = Pose(1) });
// Ping acts as a barrier for prior messages on this socket.
await Send(b, new() { Ping = new Ping { ClientUs = 420 } });
var pong = await Read(b);
while (pong.Snapshot != null) pong = await Read(b);
Assert(pong.Pong?.ClientUs == 420 && pong.Pong.ServerUs > 0, "hash mismatch held start and time ping worked");
await Send(a, new() { Pose = Pose(2) });
await Send(b, new() { Ready = ready });
var started = await Until(b, s => s.Phase == Phase.Playing);
await Until(a, s => s.Revision == started.Revision && s.Phase == Phase.Playing);
using var duringPlay = await http.GetAsync("v1/arena", ct);
Assert(duringPlay.StatusCode == HttpStatusCode.Forbidden, "arena download closed during play");
await Send(b, new() { Ready = ready });
await Send(b, new() { Pose = Pose(2, Tracking.Limited) });
await Until(b, s => s.Phase == Phase.Paused && !s.GuestReady);
await Send(a, new() { Pose = Pose(3) });
await Send(b, new() { Pose = Pose(3) });
var resumed = await Until(b, s => s.Phase == Phase.Playing);
await Until(a, s => s.Revision == resumed.Revision && s.Phase == Phase.Playing);
await b.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "reconnect", ct);
await Until(a, s => s.Revision > resumed.Revision && s.Phase == Phase.Paused && !s.GuestReady);
using var rejoined = await Connect(guestToken);
await Until(rejoined, s => s.Phase == Phase.Paused && !s.GuestReady);
await Send(a, new() { Pose = Pose(4) });
await Send(rejoined, new() { Ready = ready });
await Send(rejoined, new() { Pose = Pose(1) });
await Until(rejoined, s => s.Phase == Phase.Playing);
await Until(rejoined, s => s.Phase == Phase.Paused);
using var closed = await http.DeleteAsync("v1/session", ct);
Assert(closed.StatusCode == HttpStatusCode.NoContent, "guest can leave and end the duel");
using var ended = await http.GetAsync("v1/session", ct);
Assert(ended.StatusCode == HttpStatusCode.Unauthorized, "ended seats revoked");
Console.WriteLine("PASS two WebSocket clients, one-time QR, paired state, readiness, ping, quality loss, reconnect, tracking timeout and teardown");

string Token(JsonElement player) => player.GetProperty("seat").GetProperty("token").GetString()!;
async Task<JsonElement> Post(string path, object? data)
{
    using var response = await http.PostAsync(path, data == null ? null : JsonContent.Create(data), ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<JsonElement>(ct);
}
async Task<ClientWebSocket> Connect(string token)
{
    var ws = new ClientWebSocket();
    ws.Options.SetRequestHeader("Authorization", "Bearer " + token);
    var endpoint = new UriBuilder(new Uri(http.BaseAddress!, "v1/events"));
    endpoint.Scheme = endpoint.Scheme == "https" ? "wss" : "ws";
    await ws.ConnectAsync(endpoint.Uri, ct);
    return ws;
}
async Task Send(ClientWebSocket ws, ClientFrame frame) =>
    await ws.SendAsync(frame.ToByteArray().AsMemory(), WebSocketMessageType.Binary, true, ct);
async Task<ServerFrame> Read(ClientWebSocket ws)
{
    var buffer = new byte[2048];
    int used = 0;
    ValueWebSocketReceiveResult part;
    do
    {
        part = await ws.ReceiveAsync(buffer.AsMemory(used), ct);
        if (part.MessageType != WebSocketMessageType.Binary) throw new IOException("Unexpected websocket close.");
        used += part.Count;
    } while (!part.EndOfMessage);
    return ServerFrame.Parser.ParseFrom(buffer, 0, used);
}
async Task<Snapshot> Until(ClientWebSocket ws, Predicate<Snapshot> matches)
{
    while (true)
        if ((await Read(ws)).Snapshot is { } snapshot && matches(snapshot)) return snapshot;
}
PoseSample Pose(ulong sequence, Tracking quality = Tracking.Good)
{
    long stamp = poseClock.Elapsed.Ticks / 10;
    return new PoseSample { Sequence = sequence, ArenaRevision = 1, EstimatedUs = stamp, LastVisualUs = stamp,
        Tracking = quality, PositionSigmaM = 0.25, AngleSigmaDeg = 2,
        Position = new Vector3M { East = 3, North = 3 }, Orientation = new Orientation { W = 1 } };
}
static void Reject(Action action)
{
    try { action(); } catch (InvalidDataException) { return; }
    throw new Exception("Invalid arena accepted");
}
static void Assert(bool condition, string label)
{
    if (!condition) throw new Exception("Failed: " + label);
}
