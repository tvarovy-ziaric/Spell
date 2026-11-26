using System;
using System.IO;
#if UNITY_5_3_OR_NEWER
using System.Collections;
using System.Text;
using Spell.Geometry;
using UnityEngine;
using UnityEngine.Networking;
#endif

namespace Spell.Client.Preflight
{
#nullable enable
    public static class GuestInvite
    {
        public const string ServerOrigin = "https://spell.arnavigator.site";
        public const int MaxArenaBytes = 18 * 1024 * 1024;
        const string Prefix = "spell://join/v1/";

        public static bool TryParse(string? serverOrigin, string? invitation, out Uri? origin, out string? code)
        {
            origin = null;
            code = null;
            string? input = serverOrigin?.Trim();
            if (input == null || input.Length == 0 || input.Length > 1024 ||
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                input.IndexOfAny(new[] { '#', '?', '@', '\\', '\r', '\n' }) >= 0 ||
                !Uri.TryCreate(input, UriKind.Absolute, out var candidate) ||
                candidate.Scheme != Uri.UriSchemeHttps || candidate.Host.Length == 0 ||
                candidate.AbsolutePath != "/" || candidate.UserInfo.Length != 0)
                return false;
            int slash = input.IndexOf('/', "https://".Length);
            if (slash >= 0 && slash != input.Length - 1) return false;
            string? payload = invitation?.Trim();
            if (payload == null || !payload.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string value = payload.Substring(Prefix.Length);
            if (!Hex(value, 32)) return false;
            code = value.ToUpperInvariant();
            origin = new Uri(candidate.GetLeftPart(UriPartial.Authority));
            return true;
        }

        public static bool Supports(string? role, string? token, int revision, string? preset, string? sha, string? path)
        {
            return SupportsSeat("guest", role, token, revision, preset, sha, path);
        }

        public static bool SupportsSeat(string expectedRole, string? role, string? token, int revision, string? preset, string? sha, string? path)
        {
            return (role == "guest" || role == "founder") && role == expectedRole && Hex(token, 64) && revision == 1 &&
                preset == "diagnostic-v1" && Hex(sha, 64) && path == "/v1/arena";
        }

        public static string NewAttempt()
        {
            var bytes = new byte[16];
            using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            bytes[7] = (byte)((bytes[7] & 15) | 64);
            bytes[8] = (byte)((bytes[8] & 63) | 128);
            return new Guid(bytes).ToString("N");
        }

        static bool Hex(string? value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char c in value)
                if (!(c >= '0' && c <= '9' || c >= 'A' && c <= 'F' || c >= 'a' && c <= 'f')) return false;
            return true;
        }
    }

#nullable restore
#if UNITY_5_3_OR_NEWER
    public sealed class OnlineArenaDownload : MonoBehaviour
    {
        [Serializable] sealed class Seat
        {
            public string role = "";
            public string token = "";
        }
        [Serializable] sealed class Ticket
        {
            public Seat seat = null;
            public string diagnosticPermit;
            public int arenaRevision = 0;
            public string arenaSha256 = "";
            public string preset = "";
            public string arenaPath = "";
        }
        [Serializable] sealed class Invitation
        {
            public string payload = "";
            public string qrPngBase64 = "";
        }
        [Serializable] sealed class Created
        {
            public Ticket player = null;
            public Invitation invitation = null;
        }
        [Serializable] sealed class Pairing { public bool paired = false; }
        [Serializable] sealed class Join { public string code; public string attempt; }

        // Enforce the limit while receiving, including chunked replies with no Content-Length.
        // The byte buffer is allocated only after the server begins to send content.
        sealed class LimitedBody : DownloadHandlerScript
        {
            readonly int limit;
            readonly MemoryStream buffer = new MemoryStream();
            public bool Rejected { get; private set; }

            public LimitedBody(int limit) : base(new byte[32768]) { this.limit = limit; }

            protected override void ReceiveContentLengthHeader(ulong length)
            {
                if (length > (ulong)limit) Rejected = true;
            }

            protected override bool ReceiveData(byte[] data, int length)
            {
                if (Rejected || length < 0 || length > limit - buffer.Length ||
                    (length > 0 && (data == null || length > data.Length)))
                {
                    Rejected = true;
                    return false;
                }
                if (length > 0) buffer.Write(data, 0, length);
                return true;
            }

            public byte[] Bytes() => buffer.ToArray();
        }

        enum Job { None, Create, Scan, Join, Download, Renew, Leave }

        static readonly Uri Server = new Uri(GuestInvite.ServerOrigin);
        QrScanner scanner;
        Texture2D qr;
        Ticket ticket;
        Job queued, retry;
        bool busy, scanning, paired, polling;
        string code, hostAttempt, joinAttempt;
        string status = "", connectionNotice = "";
        float nextPoll;
        long playTerrainBytes;
        int playTerrainRequests;
        internal string SeatToken => ticket?.seat?.token;
        internal string PresetId => ticket?.preset;
        internal bool IsFounder => IsHost;
        internal MobileRules Rules => ticket?.rules ?? defaultRules;
        readonly MobileRules defaultRules = new MobileRules();
        public ArenaSurface Arena { get; private set; }

        Spell.Client.Diagnostics.DiagnosticsUpload journal;
        void Awake()
        {
            scanner = gameObject.AddComponent<QrScanner>();
            journal = gameObject.AddComponent<Spell.Client.Diagnostics.DiagnosticsUpload>();
            gameObject.AddComponent<MobileDuel>();
        }

        public void Draw(GUIStyle label, float availableWidth)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && !busy && !scanning && queued == Job.None;
            if (ticket == null)
            {
                if (GUILayout.Button("Vytvoriť hru", GUILayout.MinHeight(48))) queued = Job.Create;
                if (GUILayout.Button("Naskenovať QR kód", GUILayout.MinHeight(48))) queued = Job.Scan;
            }
            else
            {
                if (IsHost && !paired)
                {
                    GUILayout.Label("Ukáž QR kód druhému hráčovi.", label);
                    if (qr != null)
                    {
                        float side = Mathf.Min(280f, Mathf.Max(80f, availableWidth - 50f));
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        Rect area = GUILayoutUtility.GetRect(side, side, GUILayout.Width(side), GUILayout.Height(side));
                        GUI.DrawTexture(area, qr, ScaleMode.ScaleToFit, false);
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    if (GUILayout.Button("Obnoviť QR kód", GUILayout.MinHeight(44))) queued = Job.Renew;
                }
                if (paired) GUILayout.Label("Obaja hráči sú v hre.", label);
                var duel = GetComponent<MobileDuel>();
                if (duel != null && duel.Status.Length > 0) GUILayout.Label(duel.Status, label);
                if (duel != null && duel.HasStarted) GUILayout.Label($"Terén od štartu: {playTerrainRequests} požiadaviek, {playTerrainBytes} B. Sieť RTT {duel.RoundTripMs:0} ms.", label);
            }
            if (status.Length > 0) GUILayout.Label(status, label);
            if (connectionNotice.Length > 0) GUILayout.Label(connectionNotice, label);
            if (retry != Job.None && retry != Job.Create && retry != Job.Renew && retry != Job.Leave &&
                GUILayout.Button("Skúsiť znova", GUILayout.MinHeight(44))) queued = retry;
            if (ticket != null && GUILayout.Button("Opustiť hru", GUILayout.MinHeight(44))) queued = Job.Leave;
            GUI.enabled = previous;
        }

        bool IsHost => ticket?.seat.role == "founder";

        void Update()
        {
            if (busy || scanning) return;
            Job job = queued;
            queued = Job.None;
            if (job == Job.Scan)
            {
                retry = Job.None;
                status = "";
                scanning = true;
                scanner.Scan(Decoded, error => { scanning = false; status = error ?? ""; });
            }
            else if (job != Job.None)
            {
                busy = true;
                retry = job;
                StartCoroutine(Run(job));
            }
            else if (ticket != null && !polling && Time.unscaledTime >= nextPoll)
            {
                polling = true;
                StartCoroutine(CheckPairing(ticket));
            }
        }

        void Decoded(string payload)
        {
            scanning = false;
            if (!GuestInvite.TryParse(GuestInvite.ServerOrigin, payload, out _, out var scanned))
            {
                status = "Tento QR kód nepatrí do hry Spell. Naskenuj kód z telefónu prvého hráča.";
                return;
            }
            if (code != scanned)
            {
                code = scanned;
                joinAttempt = GuestInvite.NewAttempt();
            }
            queued = Job.Join;
        }

        IEnumerator Run(Job job)
        {
            using (var work = Requests(job))
            {
                try
                {
                    while (true)
                    {
                        object wait;
                        try
                        {
                            if (!work.MoveNext()) break;
                            wait = work.Current;
                        }
                        catch (OutOfMemoryException) { status = "Na načítanie ihriska chýba pamäť."; break; }
                        catch (Exception) { status = "Príprava hry zlyhala. Skús to znova."; break; }
                        yield return wait;
                    }
                }
                finally { busy = false; }
            }
        }

        System.Collections.Generic.IEnumerator<object> Requests(Job job)
        {
            if (job == Job.Leave)
            {
                status = "Opúšťam hru…";
                using var request = Request("/v1/session", "DELETE", new LimitedBody(2048));
                request.SetRequestHeader("Authorization", "Bearer " + ticket.seat.token);
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success && request.responseCode != 401 &&
                    request.responseCode != 403 && request.responseCode != 404)
                {
                    status = "Server nepotvrdil ukončenie hry. Skús ju opustiť znova.";
                    yield break;
                }
                ClearRoom();
                status = "Hra bola ukončená.";
                yield break;
            }

            if (job == Job.Create || job == Job.Join || job == Job.Renew)
            {
                bool host = job != Job.Join;
                status = job == Job.Renew ? "Obnovujem QR kód…" : host ? "Vytváram hru…" : "Pripájam sa k hre…";
                var body = new LimitedBody(64 * 1024);
                string path = job == Job.Renew ? "/v1/invite" : host ? "/v1/session" : "/v1/join";
                using (var request = Request(path, "POST", body))
                {
                    if (job == Job.Create)
                    {
                        hostAttempt ??= GuestInvite.NewAttempt();
                        request.SetRequestHeader("Idempotency-Key", hostAttempt);
                    }
                    else if (job == Job.Renew) request.SetRequestHeader("Authorization", "Bearer " + ticket.seat.token);
                    else
                    {
                        var join = new Join { code = code, attempt = joinAttempt };
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(join)));
                        request.SetRequestHeader("Content-Type", "application/json");
                    }
                    yield return request.SendWebRequest();
                    if (!Success(request, body, job)) yield break;
                    string json = Encoding.UTF8.GetString(body.Bytes());
                    if (job == Job.Renew)
                    {
                        SetQr(JsonUtility.FromJson<Invitation>(json));
                        retry = Job.None;
                        status = Arena == null ? "QR kód bol obnovený." : "Ihrisko je pripravené v telefóne.";
                        if (Arena == null) queued = Job.Download;
                        yield break;
                    }
                    var created = host ? JsonUtility.FromJson<Created>(json) : null;
                    var reply = host ? created?.player : JsonUtility.FromJson<Ticket>(json);
                    if (!GuestInvite.SupportsSeat(host ? "founder" : "guest", reply?.seat?.role, reply?.seat?.token,
                        reply?.arenaRevision ?? 0, reply?.preset, reply?.arenaSha256, reply?.arenaPath))
                    {
                        status = "Server vrátil nepodporované údaje hry.";
                        yield break;
                    }
                    if (host) SetQr(created.invitation);
                    ticket = reply;
                    journal.Begin(reply.diagnosticPermit);
                    paired = !host;
                    code = joinAttempt = null;
                    nextPoll = Time.unscaledTime;
                }
            }

            status = "Načítavam ihrisko do telefónu…";
            retry = Job.Download;
            var arenaBody = new LimitedBody(GuestInvite.MaxArenaBytes);
            using (var request = Request(ticket.arenaPath, "GET", arenaBody))
            {
                if (GetComponent<MobileDuel>().HasStarted) playTerrainRequests++;
                journal.Sink?.Record(Spell.Client.Diagnostics.Measure.TerrainRequest);
                request.timeout = 40;
                request.SetRequestHeader("Authorization", "Bearer " + ticket.seat.token);
                yield return request.SendWebRequest();
                if (GetComponent<MobileDuel>().HasStarted) playTerrainBytes += (long)request.downloadedBytes;
                journal.Sink?.Record(Spell.Client.Diagnostics.Measure.TerrainBytes, (int)Math.Min((ulong)int.MaxValue, request.downloadedBytes));
                if (!Success(request, arenaBody, Job.Download)) yield break;
                try { Arena = ArenaPreflight.Load(arenaBody.Bytes(), ticket.arenaSha256); }
                catch (InvalidDataException)
                {
                    status = "Ihrisko neprešlo kontrolou obsahu alebo platnosti dát.";
                    yield break;
                }
                status = $"Ihrisko je v pamäti telefónu. Mriežka {Arena.Width} × {Arena.Height}.";
                retry = Job.None;
            }
        }

        static UnityWebRequest Request(string path, string method, LimitedBody body)
        {
            return new UnityWebRequest(new Uri(Server, path), method)
            {
                downloadHandler = body,
                redirectLimit = 0,
                timeout = 15
            };
        }

        IEnumerator CheckPairing(Ticket current)
        {
            try
            {
                var body = new LimitedBody(2048);
                using var request = Request("/v1/session", "GET", body);
                request.SetRequestHeader("Authorization", "Bearer " + current.seat.token);
                yield return request.SendWebRequest();
                if (ticket != current || busy || queued != Job.None) yield break;
                if (request.responseCode == 401 || request.responseCode == 403 || request.responseCode == 404)
                {
                    ClearRoom();
                    status = "Hra sa skončila. Môžeš vytvoriť novú alebo naskenovať ďalší QR kód.";
                    yield break;
                }
                connectionNotice = "Overujem spojenie s hrou…";
                if (request.result != UnityWebRequest.Result.Success || body.Rejected) yield break;
                var state = JsonUtility.FromJson<Pairing>(Encoding.UTF8.GetString(body.Bytes()));
                if (state == null) yield break;
                paired = state.paired;
                connectionNotice = "";
                if (paired) ClearQr();
            }
            finally
            {
                nextPoll = Time.unscaledTime + (paired ? 8f : 2f);
                polling = false;
            }
        }

        void SetQr(Invitation invitation)
        {
            if (invitation == null || !GuestInvite.TryParse(GuestInvite.ServerOrigin, invitation.payload, out _, out _))
                throw new FormatException();
            byte[] png = Convert.FromBase64String(invitation.qrPngBase64);
            // Bound dimensions before handing server data to Unity's image decoder.
            if (png.Length < 33 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71 ||
                png[16] != 0 || png[17] != 0 || png[18] > 3 || png[20] != 0 || png[21] != 0 ||
                png[18] != png[22] || png[19] != png[23])
                throw new FormatException();
            var next = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            if (!ImageConversion.LoadImage(next, png, true))
            {
                Destroy(next);
                throw new FormatException();
            }
            ClearQr();
            qr = next;
        }

        bool Success(UnityWebRequest request, LimitedBody body, Job job)
        {
            if (!body.Rejected && request.result == UnityWebRequest.Result.Success && request.responseCode == 200) return true;
            if (body.Rejected) status = "Odpoveď prekročila povolenú veľkosť.";
            else if (job == Job.Join && request.responseCode == 401)
            {
                status = "QR kód už neplatí. Požiadaj prvého hráča o nový a naskenuj ho.";
                retry = Job.None;
            }
            else if (job == Job.Create && request.responseCode == 429) status = "Server je vyťažený. Skús vytvoriť hru o chvíľu.";
            else if (job == Job.Renew && request.responseCode == 401) status = "QR kód už nemožno obnoviť. Overujem obsadenie hry.";
            else if (job == Job.Download && request.responseCode == 403) status = "Server v tomto stave nepovolil stiahnutie ihriska.";
            else status = "Spojenie s herným serverom zlyhalo. Skontroluj internet a skús to znova.";
            return false;
        }

        void ClearRoom()
        {
            journal.End();
            playTerrainBytes = playTerrainRequests = 0;
            ClearQr();
            ticket = null;
            Arena = null;
            paired = false;
            hostAttempt = joinAttempt = code = null;
            connectionNotice = "";
            retry = Job.None;
        }

        void ClearQr()
        {
            if (qr != null) Destroy(qr);
            qr = null;
        }

        void OnDestroy() => ClearQr();
    }
#endif
}
