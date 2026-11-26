using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Spell.Client.Preflight;

namespace Spell.Client.Diagnostics
{
    public sealed class DiagnosticsUpload : MonoBehaviour
    {
        PilotJournal journal;
        public IMeasurementSink Sink => journal;
        float nextFlush;
        void Awake()
        {
            Safe(() => journal = new PilotJournal(Path.Combine(Application.persistentDataPath, "diagnostic-counts-v1")));
            if (journal != null) StartCoroutine(Upload());
        }
        public void Begin(string permit) => Safe(() => journal?.Begin(permit));
        public void End() => Safe(() => journal?.End());
        void Update()
        {
            if (journal == null || !journal.Recording || Time.unscaledTime < nextFlush) return;
            nextFlush = Time.unscaledTime + 5;
            Safe(journal.Flush);
        }
        void OnApplicationPause(bool paused) { if (paused) Safe(() => journal?.Flush()); }
        void OnDestroy() => End();
        static void Safe(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Debug.LogWarning("Zápis diagnostických počítadiel zlyhal."); }
        }
        IEnumerator Upload()
        {
            while (true)
            {
                string[] pending = Array.Empty<string>();
                Safe(() => pending = journal.Pending(DateTime.UtcNow));
                foreach (var file in pending)
                {
                    if (journal.Recording) break;
                    string permit;
                    PilotCounters data;
                    try { data = PilotJournal.Read(file, out permit); }
                    catch (Exception e) when (e is IOException || e is FormatException || e is InvalidDataException)
                    { continue; }
                    using var req = new UnityWebRequest(GuestInvite.ServerOrigin + "/v1/diagnostics/batch", "POST");
                    req.timeout = 10;
                    req.redirectLimit = 0;
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Authorization", "Bearer " + permit);
                    req.SetRequestHeader("Content-Type", "application/json");
                    yield return req.SendWebRequest();
                    if (req.responseCode == 204) Safe(() => File.Delete(file));
                }
                yield return new WaitForSecondsRealtime(10);
            }
        }
    }
}
