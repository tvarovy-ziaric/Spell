using System;
using UnityEngine;

namespace Spell.Client.Preflight
{
    public sealed class QrScanner : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass bridge;
        string request;
        Action<string> decoded, failed;
#endif
        public void Scan(Action<string> onDecoded, Action<string> onError)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (request != null) { onError?.Invoke("Skenovanie už prebieha."); return; }
            try
            {
                bridge ??= new AndroidJavaClass("site.arnavigator.spell.InvitationScanActivity");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                string id = Guid.NewGuid().ToString("N");
                if (!bridge.CallStatic<bool>("begin", activity, id))
                {
                    onError?.Invoke("Skenovanie momentálne nie je dostupné.");
                    return;
                }
                request = id;
                decoded = onDecoded;
                failed = onError;
            }
            catch (AndroidJavaException) { onError?.Invoke("Android nedokázal spustiť skener QR."); }
#else
            onError?.Invoke("Živé skenovanie QR je dostupné na telefóne s Androidom.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void Update()
        {
            if (request == null) return;
            string response;
            try { response = bridge.CallStatic<string>("take", request); }
            catch (AndroidJavaException)
            {
                response = "!Výsledok skenovania sa nepodarilo prečítať.";
                Abandon();
            }
            if (response == null) return;
            var success = decoded;
            var error = failed;
            request = null;
            decoded = failed = null;
            if (response.StartsWith("+", StringComparison.Ordinal)) success?.Invoke(response.Substring(1));
            else error?.Invoke(response.StartsWith("!", StringComparison.Ordinal) ? response.Substring(1) : null);
        }

        void Abandon()
        {
            try { bridge?.CallStatic("cancel", request); }
            catch (AndroidJavaException) { /* Android activity may already have gone away. */ }
        }
#endif
        public void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (request == null) return;
            Abandon();
            request = null;
            var callback = failed;
            failed = decoded = null;
            callback?.Invoke(null);
#endif
        }

        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (request != null) Abandon();
            bridge?.Dispose();
#endif
        }
    }
}
