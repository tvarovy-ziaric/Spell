using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Spell.Client.Preflight
{
    public sealed class DeviceDiagnostics : MonoBehaviour
    {
        public bool Running { get; private set; }
        public PoseTiming Timing {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
                return timing;
#else
                return default;
#endif
            }
        }
        public string Status { get; private set; } = "Diagnostika zistí stav ARCore, kamery a GNSS. Po hre sa odošlú diagnostické počítadlá bez presnej polohy.";
        Camera view;
        public Pose ViewPose => new Pose(view.transform.position, view.transform.rotation);
        GameObject trackingOrigin;
        BoardCalibration boards;
        ARCameraManager cameraManager;
        Spell.Client.Diagnostics.IMeasurementSink sink;
        long lastImageNs;
        long frameCount;
        double nextDisplay;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass gnss;
        string hardware;
        double thermalCheckAt;
        PermissionCallbacks permissionCallbacks;
        PoseTimeline timeline = new PoseTimeline();
        PoseTiming timing;
        int pendingPermissions;
#endif

        void Awake()
        {
            var rig = new GameObject("Lokálny priestor kamery");
            trackingOrigin = rig;
            rig.SetActive(false);
            var origin = rig.AddComponent<XROrigin>();
            var offset = new GameObject("Tracking origin");
            offset.transform.SetParent(rig.transform, false);
            var cameraObject = new GameObject("AR kamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset.transform, false);
            view = cameraObject.AddComponent<Camera>();
            view.nearClipPlane = 0.1f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.045f, 0.06f, 0.08f);
            origin.Camera = view;
            origin.CameraFloorOffsetObject = offset;
            origin.CameraYOffset = 0;
            rig.SetActive(true);
            rig.transform.SetParent(transform, false);
        }

        public void Begin()
        {
            if (Running) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Running = true;
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera) ||
                !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                permissionCallbacks = new PermissionCallbacks();
                var requested = new System.Collections.Generic.List<string>();
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera)) requested.Add(Permission.Camera);
                if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                {
                    requested.Add(Permission.CoarseLocation);
                    requested.Add(Permission.FineLocation);
                }
                pendingPermissions = requested.Count;
                permissionCallbacks.PermissionGranted += PermissionChanged;
                permissionCallbacks.PermissionDenied += PermissionChanged;
                Permission.RequestUserPermissions(requested.ToArray(), permissionCallbacks);
                StartCoroutine(WaitForPermission());
            }
            else StartCoroutine(StartSession());
#else
            Status = "Kontrola kamery a GNSS vyžaduje fyzický Android s ARCore.";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void PermissionChanged(string permission) => pendingPermissions--;

        IEnumerator WaitForPermission()
        {
            Status = "Čakám na rozhodnutie o prístupe ku kamere a polohe.";
            // Android may deliver callbacks for an already granted permission before showing the next dialog.
            while (pendingPermissions > 0 || !Application.isFocused) yield return null;
            permissionCallbacks = null;
            yield return null;
            yield return StartSession();
        }

        IEnumerator StartSession()
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Status = "Na sledovanie pohybu povoľ prístup ku kamere.";
                Running = false;
                yield break;
            }
            yield return ARSession.CheckAvailability();
            if (ARSession.state != ARSessionState.Ready && ARSession.state != ARSessionState.SessionTracking &&
                ARSession.state != ARSessionState.SessionInitializing)
            {
                Status = ARSession.state == ARSessionState.NeedsInstall
                    ? "Google Play Services pre AR vyžaduje inštaláciu alebo aktualizáciu."
                    : "ARCore na tomto zariadení nie je dostupný.";
                Running = false;
                yield break;
            }

            cameraManager = view.gameObject.AddComponent<ARCameraManager>();
            cameraManager.frameReceived += OnFrame;
            view.gameObject.AddComponent<ARCameraBackground>();
            gameObject.AddComponent<ARInputManager>();
            gameObject.AddComponent<ARSession>();
            sink = GetComponent<Spell.Client.Diagnostics.DiagnosticsUpload>()?.Sink;
            boards = gameObject.AddComponent<BoardCalibration>();
            boards.Begin(trackingOrigin);
            gnss = new AndroidJavaClass("site.arnavigator.spell.GnssProbe");
            StartLocation();
            Application.onBeforeRender += UpdateViewPose;
        }

        void StartLocation()
        {
            if (gnss == null || !Permission.HasUserAuthorizedPermission(Permission.FineLocation)) return;
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            gnss.CallStatic("start", activity);
        }

        void OnApplicationPause(bool paused)
        {
            if (gnss == null) return;
            if (Time.unscaledTimeAsDouble >= thermalCheckAt)
            {
                thermalCheckAt=Time.unscaledTimeAsDouble+5;
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                GetComponent<Spell.Client.Visuals.DuelVisuals>()?.ApplyThermalStatus(gnss.CallStatic<int>("thermalStatus",activity));
            }
            if (paused) gnss.CallStatic("stop");
            else
            {
                timeline = new PoseTimeline();
                timing = default;
                frameCount = 0;
                lastImageNs = 0;
                StartLocation();
            }
        }
#endif

        void UpdateViewPose()
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
                view.transform.SetLocalPositionAndRotation(position, rotation);
        }

        void OnFrame(ARCameraFrameEventArgs frame)
        {
            if (frame.timestampNs.HasValue && frame.timestampNs.Value > lastImageNs)
            {
                lastImageNs = frame.timestampNs.Value;
                frameCount++;
                sink?.Record(Spell.Client.Diagnostics.Measure.VisualFrame);
                boards?.CameraFrame();
#if UNITY_ANDROID && !UNITY_EDITOR
                if (gnss != null) timeline.ObserveVisual(lastImageNs / 1000, gnss.CallStatic<long>("elapsedUs"),
                    ARSession.state == ARSessionState.SessionTracking);
#endif
            }
        }

        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (gnss == null) return;
            long nowUs = gnss.CallStatic<long>("elapsedUs");
            long fixUs = gnss.CallStatic<long>("lastFixUs");
            if (timeline.ObserveGnss(fixUs)) sink?.Record(Spell.Client.Diagnostics.Measure.GnssFix);
            if (timeline.TryPublish(nowUs, out PoseTiming next))
            {
                timing = next;
                sink?.Record(Spell.Client.Diagnostics.Measure.Estimate);
            }
            if (Time.unscaledTimeAsDouble < nextDisplay) return;
            nextDisplay = Time.unscaledTimeAsDouble + 0.25;
            string gps = gnss.CallStatic<string>("status");
            if (hardware == null)
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                hardware = gnss.CallStatic<string>("capabilities", activity);
            }
            var occlusion = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XROcclusionSubsystem>();
            string depth = occlusion?.subsystemDescriptor.environmentDepthImageSupported.ToString() ?? "nedostupná";
            string pose = view.transform.localPosition.ToString("F2");
            string support = timing.HasRecentTracking ? "čerstvé" : "neisté";
            Status = $"ARCore: {ARSession.state}\nSnímky kamery: {frameCount}\n{hardware}\nHĺbka: {depth}\nLokálna poloha (m): {pose}\n{gps}\nOdhady: {timing.Sequence}, sledovanie {support}.\n{boards.Status}";
#endif
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= UpdateViewPose;
            if (cameraManager != null) cameraManager.frameReceived -= OnFrame;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (gnss != null)
            {
                gnss.CallStatic("stop");
                gnss.Dispose();
            }
#endif
        }
    }
}
