using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Spell.Client.Preflight
{
    [Serializable]
    public sealed class MobileRules
    {
        public int hitPoints = 100, damage = 25, auraPenalty = 0;
        public double speed = 10, range = 20, radius = 1, auraWarning = 1.5, auraCollision = 0.5;
        public bool fireballEnabled = true;
    }

    public sealed class FounderAdmin : MonoBehaviour
    {
        [Serializable] sealed class Login { public string password, deviceSecret; }
        [Serializable] sealed class Grant { public int version; public string token; }
        [Serializable] sealed class Input { public int version = 1; public string requestId; public MobileRules rules; }
        [Serializable] sealed class Saved { public string id, createdAt; public MobileRules rules; }
        [Serializable] sealed class Page { public int version; public Saved[] items; public long nextBefore; }

        public string Selected { get; private set; }
        public bool Busy { get; private set; }
        string token, password = "", note, pendingSave, pendingJson;
        bool open, remember = true, initialized, enabledSpell = true;
        Page page;
        readonly string[] labels = { "Životy", "Poškodenie", "Postih aury", "Rýchlosť (m/s)", "Dosah (m)", "Polomer účinku (m)", "Varovanie aury (m)", "Kolízia aury (m)" };
        readonly string[] values = new string[8];

        void Awake() => Edit(new MobileRules());

        void Edit(MobileRules rules)
        {
            double[] numbers = { rules.hitPoints, rules.damage, rules.auraPenalty, rules.speed, rules.range, rules.radius, rules.auraWarning, rules.auraCollision };
            for (int i = 0; i < values.Length; i++) values[i] = numbers[i].ToString("G", CultureInfo.InvariantCulture);
            enabledSpell = rules.fireballEnabled;
        }

        public void Apply(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(Selected) && !string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Spell-Preset", Selected);
                request.SetRequestHeader("Spell-Grant", token);
            }
        }

        public void Draw(GUIStyle label)
        {
            if (GUILayout.Button(open ? "Zatvoriť nastavenia" : "Nastavenia zakladateľa", GUILayout.MinHeight(42)))
            {
                open = !open;
                if (open && !initialized) LoadGrant();
            }
            if (!open) return;
            bool previous = GUI.enabled;
            GUI.enabled = previous && !Busy;
            if (string.IsNullOrEmpty(token))
            {
                GUILayout.Label("Heslo zakladateľa", label);
                password = GUILayout.PasswordField(password, '•', 256);
                remember = GUILayout.Toggle(remember, "Zapamätať oprávnenie na tomto telefóne");
                if (GUILayout.Button("Odomknúť", GUILayout.MinHeight(42)) && password.Length > 0) LoginNow();
            }
            else
            {
                if (GUILayout.Button("História pravidiel", GUILayout.MinHeight(42))) History(0);
                if (page?.items != null)
                {
                    foreach (var item in page.items)
                    {
                        if (item.rules != null && GUILayout.Button(FormatDate(item.createdAt) + " · " + item.rules.hitPoints + " HP"))
                            Select(item);
                    }
                    if (page.nextBefore > 0 && GUILayout.Button("Staršie pravidlá")) History(page.nextBefore);
                }
                for (int i = 0; i < values.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(labels[i], label, GUILayout.ExpandWidth(true));
                    values[i] = GUILayout.TextField(values[i], 12, GUILayout.Width(72));
                    GUILayout.EndHorizontal();
                }
                enabledSpell = GUILayout.Toggle(enabledSpell, "Povoliť Fireball");
                if (GUILayout.Button("Uložiť a vybrať pravidlá", GUILayout.MinHeight(42))) Save();
                if (GUILayout.Button("Použiť základné pravidlá")) { Selected = null; Edit(new MobileRules()); note = "Vybrané základné pravidlá."; }
                if (GUILayout.Button("Odhlásiť správu pravidiel")) Send("DELETE", "/grant", null, _ => { Forget(); note = "Odhlásené."; });
            }
            if (!string.IsNullOrEmpty(note)) GUILayout.Label(note, label);
            GUI.enabled = previous;
        }

        static string FormatDate(string text) => DateTime.TryParse(text, out var date) ? date.ToLocalTime().ToString("g") : text;
        void Select(Saved item)
        {
            if (item?.rules == null || !Guid.TryParseExact(item.id?.Replace("preset-v1-", ""), "N", out _))
                throw new FormatException();
            Selected = item.id;
            Edit(item.rules);
            note = "Vybrané pravidlá z " + FormatDate(item.createdAt) + ".";
        }

        void LoadGrant()
        {
            try { token = Vault("grant"); initialized = true; if (!string.IsNullOrEmpty(token)) History(0); }
            catch (Exception) { note = "Úložisko oprávnení sa nepodarilo otvoriť."; }
        }
        string Vault(string method, string value = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var vault = new AndroidJavaClass("site.arnavigator.spell.FounderVault");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            if (value == null) return vault.CallStatic<string>(method, activity);
            vault.CallStatic(method, activity, value);
            return null;
#else
            throw new PlatformNotSupportedException();
#endif
        }
        void Forget()
        {
            token = Selected = null;
            page = null;
            Edit(new MobileRules());
            Vault("remember", "");
        }
        void LoginNow()
        {
            try
            {
                var body = JsonUtility.ToJson(new Login { password = password, deviceSecret = Vault("identity") });
                password = "";
                Send("POST", "/grant", body, text =>
                {
                    var response = JsonUtility.FromJson<Grant>(text);
                    if (response?.version != 1 || response.token?.Length != 64) throw new FormatException();
                    token = response.token;
                    Vault("remember", remember ? token : "");
                    History(0);
                });
            }
            catch (Exception) { note = "Bezpečná identita telefónu je nedostupná."; }
        }
        void History(long before) => Send("GET", "/presets" + (before > 0 ? "?before=" + before : ""), null, text =>
        {
            var result = JsonUtility.FromJson<Page>(text);
            if (result?.version != 1 || result.items == null || result.items.Length > 10) throw new FormatException();
            page = result;
            note = page.items.Length == 0 ? "História je zatiaľ prázdna." : "Vyber uložené pravidlá.";
        });
        void Save()
        {
            var rules = new MobileRules { fireballEnabled = enabledSpell };
            double[] n = new double[8];
            for (int i = 0; i < n.Length; i++)
            {
                if (!double.TryParse(values[i].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out n[i]) ||
                    double.IsInfinity(n[i]) || double.IsNaN(n[i]) || (i < 3 && (n[i] != Math.Truncate(n[i]) || n[i] < 0 || n[i] > 10000)))
                { note = "Skontroluj hodnotu pre " + labels[i] + "."; return; }
            }
            rules.hitPoints = (int)n[0]; rules.damage = (int)n[1]; rules.auraPenalty = (int)n[2];
            rules.speed = n[3]; rules.range = n[4]; rules.radius = n[5]; rules.auraWarning = n[6]; rules.auraCollision = n[7];
            string json = JsonUtility.ToJson(rules);
            if (pendingSave == null || pendingJson != json) { pendingJson = json; pendingSave = Guid.NewGuid().ToString(); }
            Send("POST", "/presets", JsonUtility.ToJson(new Input { requestId = pendingSave, rules = rules }), text =>
            {
                Select(JsonUtility.FromJson<Saved>(text));
                pendingSave = null;
            });
        }

        void Send(string method, string path, string body, Action<string> done) =>
            StartCoroutine(Request(method, path, body, done));

        IEnumerator Request(string method, string path, string body, Action<string> done)
        {
            Busy = true;
            note = "";
            using var request = new UnityWebRequest(GuestInvite.ServerOrigin + "/v1/admin" + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 15;
            request.redirectLimit = 0;
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return request.SendWebRequest();
            Busy = false;
            try
            {
                if (request.result == UnityWebRequest.Result.Success) done(request.downloadHandler.text);
                else if (request.responseCode == 401) { Forget(); note = "Oprávnenie je neplatné. Zadaj heslo zakladateľa."; }
                else if (request.responseCode == 400) note = "Server odmietol hodnoty pravidiel. Skontroluj ich rozsah.";
                else if (request.responseCode == 429) note = "Priveľa pokusov. Skús to o minútu.";
                else note = "Správa pravidiel je nedostupná. Skús to znova.";
            }
            catch (Exception) { note = "Odpoveď alebo bezpečné úložisko sa nepodarilo spracovať."; }
        }
    }
}
