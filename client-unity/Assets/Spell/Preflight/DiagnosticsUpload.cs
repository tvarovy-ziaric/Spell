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
        public IPilotEventSink Events => journal;
        public void Pose(double east, double up, double north, double sigma) => journal?.Pose(east, up, north, sigma);
        // Both tester consent and an owner-approved build are required before enabling the precision path.
#if SPELL_APPROVED_DIAGNOSTIC_POSES
        const bool CaptureApproved = true;
#else
        const bool CaptureApproved = false;
#endif
        float nextFlush;
        void Awake()
        {
            Safe(() => journal = new PilotJournal(Path.Combine(Application.persistentDataPath, "diagnostic-counts-v1"), CaptureApproved));
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
                    TapePage[] pages;
                    try { data = PilotJournal.Read(file, out permit, out pages); }
                    catch (Exception e) when (e is IOException || e is FormatException || e is InvalidDataException)
                    { continue; }
                    using var counts = Post("/batch", permit, JsonUtility.ToJson(data));
                    yield return counts.SendWebRequest();
                    bool acknowledged = counts.responseCode == 204;
                    foreach (var page in pages)
                    {
                        if (!acknowledged || journal.Recording) { acknowledged = false; break; }
                        using var req = Post("/events", permit, JsonUtility.ToJson(page));
                        yield return req.SendWebRequest();
                        acknowledged = req.responseCode == 204;
                    }
                    if (acknowledged) Safe(() => File.Delete(file));
                }
                yield return new WaitForSecondsRealtime(10);
            }
        }
        static UnityWebRequest Post(string path, string permit, string json)
        {
            var req = new UnityWebRequest(GuestInvite.ServerOrigin + "/v1/diagnostics" + path, "POST");
            req.timeout = 10;
            req.redirectLimit = 0;
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Authorization", "Bearer " + permit);
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }
    }
}
