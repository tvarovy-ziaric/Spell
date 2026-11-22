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
            return role == "guest" && Hex(token, 64) && revision == 1 &&
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
            public int arenaRevision = 0;
            public string arenaSha256 = "";
            public string preset = "";
            public string arenaPath = "";
        }
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

        [SerializeField] string serverOrigin = "";
        string invitation = "";
        string status = "";
        Uri activeOrigin;
        string activeCode, attempt;
        Ticket ticket;
        bool queued, busy;
        public ArenaSurface Arena { get; private set; }

        public void Draw()
        {
            GUILayout.Space(12);
            GUILayout.Label("Online ihrisko pre hosťa");
            bool previous = GUI.enabled;
            if (ticket == null)
            {
                GUI.enabled = previous && !busy && !queued;
                GUILayout.Label("HTTPS adresa herného servera");
                serverOrigin = GUILayout.TextField(serverOrigin, 1024);
                GUILayout.Label("Pozvánka spell://join/v1/…");
                invitation = GUILayout.TextField(invitation, 128);
            }
            GUI.enabled = previous && !busy && !queued && Arena == null;
            if (GUILayout.Button(ticket == null ? "Pripojiť sa a načítať ihrisko" : "Zopakovať prenos ihriska",
                GUILayout.MinHeight(44))) queued = true;
            GUI.enabled = previous;
            if (status.Length > 0) GUILayout.Label(status);
            if (ticket != null && !busy && !queued &&
                GUILayout.Button("Použiť inú pozvánku", GUILayout.MinHeight(40)))
            {
                Arena = null;
                ticket = null;
                activeOrigin = null;
                activeCode = attempt = null;
                invitation = status = "";
            }
        }

        void Update()
        {
            if (!queued || busy) return;
            queued = false;
            if (ticket == null)
            {
                if (!GuestInvite.TryParse(serverOrigin, invitation, out var origin, out var code))
                {
                    status = "Skontroluj HTTPS adresu servera a celú pozvánku.";
                    return;
                }
                if (origin != activeOrigin || code != activeCode)
                {
                    activeOrigin = origin;
                    activeCode = code;
                    attempt = GuestInvite.NewAttempt();
                }
            }
            busy = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            using (var work = Requests())
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
                        catch (Exception) { status = "Príprava online ihriska zlyhala. Skontroluj pripojenie a reláciu."; break; }
                        yield return wait;
                    }
                }
                finally { busy = false; }
            }
        }

        System.Collections.Generic.IEnumerator<object> Requests()
        {
            if (ticket == null)
            {
                status = "Overujem pozvánku…";
                var body = new LimitedBody(16 * 1024);
                using (var request = new UnityWebRequest(new Uri(activeOrigin, "/v1/join"), "POST"))
                {
                    request.downloadHandler = body;
                    request.redirectLimit = 0;
                    request.timeout = 15;
                    var json = JsonUtility.ToJson(new Join { code = activeCode, attempt = attempt });
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                    yield return request.SendWebRequest();
                    if (!Success(request, body)) yield break;
                    var reply = JsonUtility.FromJson<Ticket>(Encoding.UTF8.GetString(body.Bytes()));
                    if (!GuestInvite.Supports(reply?.seat?.role, reply?.seat?.token, reply?.arenaRevision ?? 0,
                        reply?.preset, reply?.arenaSha256, reply?.arenaPath))
                    {
                        status = "Server vrátil nepodporovanú revíziu, pravidlá alebo údaje relácie.";
                        yield break;
                    }
                    ticket = reply;
                    invitation = activeCode = "";
                }
            }
            status = "Hosť je pripojený. Načítavam celé ihrisko…";
            var arenaBody = new LimitedBody(GuestInvite.MaxArenaBytes);
            using (var request = new UnityWebRequest(new Uri(activeOrigin, "/v1/arena"), "GET"))
            {
                request.downloadHandler = arenaBody;
                request.redirectLimit = 0;
                request.timeout = 40;
                request.SetRequestHeader("Authorization", "Bearer " + ticket.seat.token);
                yield return request.SendWebRequest();
                if (!Success(request, arenaBody)) yield break;
                var bytes = arenaBody.Bytes();
                try { Arena = ArenaPreflight.Load(bytes, ticket.arenaSha256); }
                catch (InvalidDataException)
                {
                    status = "Ihrisko neprešlo kontrolou obsahu alebo platnosti dát.";
                    yield break;
                }
                status = $"Online ihrisko je v RAM. {Arena.Width} × {Arena.Height} vzoriek, {bytes.Length} B. " +
                    $"Revízia {ticket.arenaRevision}, pravidlá {ticket.preset}.";
            }
        }

        bool Success(UnityWebRequest request, LimitedBody body)
        {
            if (!body.Rejected && request.result == UnityWebRequest.Result.Success &&
                request.responseCode == 200) return true;
            if (body.Rejected) status = "Odpoveď prekročila povolenú veľkosť.";
            else if (request.responseCode == 401) status = "Server odmietol pozvánku. Skontroluj platnosť kódu.";
            else if (request.responseCode == 403) status = "Server v tomto stave nepovolil stiahnutie ihriska.";
            else if (request.responseCode >= 300 && request.responseCode < 400) status = "Server požiadal o nepovolené presmerovanie.";
            else status = "Prenos zlyhal. Môžeš ho zopakovať.";
            return false;
        }
    }
#endif
}
