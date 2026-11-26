using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Spell.Geometry;
using Spell.Client.Visuals;

namespace Spell.Client.Preflight
{
    public sealed class ArenaDiagnostics : MonoBehaviour
    {
        ArenaSurface arena;
        string result = "Načítavam skúšobné ihrisko…";
        DeviceDiagnostics device;
        DuelVisuals visuals;
        bool showVisualSettings;
        OnlineArenaDownload online;
        GUIStyle title;
        GUIStyle label;
        bool showCamera;
        Vector2 scroll;

        IEnumerator Start()
        {
            device = gameObject.AddComponent<DeviceDiagnostics>();
            online = gameObject.AddComponent<OnlineArenaDownload>();
            visuals = gameObject.AddComponent<DuelVisuals>();
            byte[] content = null;
            string digest = null;
            using (var payload = UnityWebRequest.Get(FixtureUri("hill.bytes")))
            {
                yield return payload.SendWebRequest();
                if (!Succeeded(payload)) yield break;
                content = payload.downloadHandler.data;
            }
            using (var checksum = UnityWebRequest.Get(FixtureUri("hill.sha256")))
            {
                yield return checksum.SendWebRequest();
                if (!Succeeded(checksum)) yield break;
                digest = checksum.downloadHandler.text.Trim();
            }

            try
            {
                arena = ArenaPreflight.Load(content, digest);
                result = $"Ihrisko je v pamäti. Mriežka {arena.Width} × {arena.Height}, krok {arena.StepMeters:0.##} m.";
                if (arena.TryHeight(3, 3, out double elevation))
                    result += $" Kontrolný bod na skúšobnom kopci má výšku {elevation:0.##} m.";
            }
            catch (InvalidDataException error)
            {
                result = "Príprava ihriska zlyhala. " + error.Message;
            }
            catch (OutOfMemoryException)
            {
                result = "Pre ihrisko chýba voľná pamäť. Vyber menšiu plochu.";
            }
        }

        bool Succeeded(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success) return true;
            result = "Súbor ihriska sa nepodarilo načítať. " + request.error;
            return false;
        }

        static string FixtureUri(string name)
        {
#if UNITY_EDITOR
            return new Uri(Path.GetFullPath(Path.Combine(Application.dataPath, "../../contracts/fixtures", name))).AbsoluteUri;
#else
            return Application.streamingAssetsPath + "/fixture/" + name;
#endif
        }

        void OnGUI()
        {
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            Rect safe = Screen.safeArea;
            float scale = Mathf.Max(1f, Mathf.Min(safe.width / 360f, safe.height / 500f));
            Matrix4x4 original = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax), Quaternion.identity, Vector3.one * scale);
            if (showCamera && device != null && device.Running)
            {
                visuals.DrawSight(safe.width / scale, safe.height / scale);
                if (GUI.Button(new Rect(12, safe.height / scale - 58, 210, 46), "Zobraziť diagnostiku"))
                { showCamera = false; visuals.SetCameraOpen(false); }
                GUI.matrix = original;
                return;
            }
            GUILayout.BeginArea(new Rect(12, 12, safe.width / scale - 24, safe.height / scale - 24), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Spell · Hra pre dvoch", title);
            online?.Draw(label, safe.width / scale - 24);
            GUILayout.Space(20);
            GUILayout.Label("Kontrola ihriska", label);
            GUILayout.Label(result, label);
            if (device != null)
            {
                GUILayout.Space(12);
                GUILayout.Label(device.Status, label);
                device.DrawFieldFixes(label);
                if (device.Running && GUILayout.Button("Zobraziť kameru", GUILayout.MinHeight(46)))
                { showCamera = true; visuals.SetCameraOpen(true); }
                if (!device.Running && GUILayout.Button("Skontrolovať senzory", GUILayout.MinHeight(46)))
                    device.Begin();
            }
            if (GUILayout.Button("Nastavenia grafiky", GUILayout.MinHeight(44)))
                showVisualSettings = !showVisualSettings;
            if (showVisualSettings) visuals?.DrawSettings();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = original;
        }
    }
}
