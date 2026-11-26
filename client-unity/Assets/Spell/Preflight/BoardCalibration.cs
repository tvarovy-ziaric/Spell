using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Spell.Geometry;
using Spell.Client.Visuals;
using XrVector = UnityEngine.Vector3;
using MetricVector = System.Numerics.Vector3;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.XR.ARCore;
#endif

namespace Spell.Client.Preflight
{
    public sealed class BoardCalibration : MonoBehaviour, IMarkerPoseSource
    {
        // The two diagnostic boards have a prescribed geometry, independent of GNSS or DMR.
        MetricVector referenceB = MetricVector.UnitX * 1.5f;
        OnlineArenaDownload online;
        const double ObservationLifetime = 12;
        readonly double[] observedAt = { -100, -100 };
        readonly MetricVector[] centres = new MetricVector[2];
        ARTrackedImageManager images;
        MarkerCalibration fit;
        GameObject midpoint;
        bool hasNewFrame;
        double lastFrameAt = -100;
        readonly MarkerObservations visualObservations = new MarkerObservations();
        public bool TryGetMarkerPose(int id, out Pose pose) =>
            visualObservations.TryRead(id, Time.realtimeSinceStartupAsDouble, out pose);

        public bool TryGetSharedFrame(out MarkerCalibration calibration)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            calibration = isActiveAndEnabled && ARSession.state == ARSessionState.SessionTracking &&
                now - lastFrameAt <= .8 && now - observedAt[0] <= ObservationLifetime &&
                now - observedAt[1] <= ObservationLifetime ? fit : null;
            return calibration != null;
        }
        public string Status { get; private set; } = "Terčíky čakajú na zapnutie kamery.";

        public void CameraFrame()
        {
            lastFrameAt = Time.realtimeSinceStartupAsDouble;
            hasNewFrame = true;
        }

        public void Begin(GameObject origin)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            online = GetComponent<OnlineArenaDownload>();
            var library = Resources.Load<XRReferenceImageLibrary>("SpellBoards");
            var provider = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRImageTrackingSubsystem>();
            if (library == null || library.count != 2 || provider == null)
            {
                Status = "Na tomto zariadení chýba knižnica alebo podpora sledovania terčíkov.";
                return;
            }

            images = origin.AddComponent<ARTrackedImageManager>();
            images.referenceLibrary = library;
            images.enabled = true;
            midpoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            midpoint.name = "Kontrolný bod medzi terčíkmi";
            midpoint.transform.localScale = XrVector.one * 0.12f;
            midpoint.transform.SetParent(transform, false);
            midpoint.SetActive(false);
            Status = "Zameraj oba terčíky podľa rozostavenia pre toto ihrisko.";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // UnityXRNativeImage_1, documented by the pinned ARCore provider.
        [StructLayout(LayoutKind.Sequential)]
        struct NativeImage { public int version; public IntPtr imageTrackable; }

        [DllImport("arcore_sdk_c")]
        static extern void ArAugmentedImage_getTrackingMethod(IntPtr session, IntPtr image, out int method);

        void LateUpdate()
        {
            if (images == null || midpoint == null) return;
            var expectedB = online?.Registration?.BoardBLocal ?? MetricVector.UnitX * 1.5f;
            if (referenceB != expectedB) { referenceB = expectedB; Clear(); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            if (ARSession.state != ARSessionState.SessionTracking || now - lastFrameAt > 0.8)
            {
                Clear();
                return;
            }
            var session = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRSessionSubsystem>() as ARCoreSessionSubsystem;
            IntPtr sessionPtr = session == null ? IntPtr.Zero : session.session.AsIntPtr();
            if (sessionPtr == IntPtr.Zero) { Clear(); return; }

            if (!hasNewFrame) return;
            hasNewFrame = false;
            visualObservations.BeginFrame(lastFrameAt);
            bool observed = false;
            foreach (var image in images.trackables)
            {
                int id = image.referenceImage.name switch { "board-0" => 0, "board-1" => 1, _ => -1 };
                if (id < 0 || image.trackingState != TrackingState.Tracking || image.nativePtr == IntPtr.Zero) continue;
                NativeImage native = Marshal.PtrToStructure<NativeImage>(image.nativePtr);
                if (native.version != 1 || native.imageTrackable == IntPtr.Zero) continue;
                ArAugmentedImage_getTrackingMethod(sessionPtr, native.imageTrackable, out int method);
                if (method != 1) continue; // FULL_TRACKING requires visual evidence in the current frame.
                var position = image.transform.position;
                visualObservations.Observe(id, new Pose(position, image.transform.rotation));
                centres[id] = new MetricVector(position.x, position.y, position.z);
                observedAt[id] = now;
                observed = true;
            }
            bool hasA = now - observedAt[0] <= ObservationLifetime;
            bool hasB = now - observedAt[1] <= ObservationLifetime;
            if (!hasA || !hasB)
            {
                fit = null;
                midpoint.SetActive(false);
                Status = hasA ? "Terčík A je zameraný. Zameraj B." : hasB
                    ? "Terčík B je zameraný. Zameraj A." : "Zameraj terčík A alebo B.";
                return;
            }
            if (!observed) return;
            var result = MarkerCalibration.Fit(new MarkerPoint(0, MetricVector.Zero),
                new MarkerPoint(1, referenceB),
                new MarkerPoint(0, centres[0]), new MarkerPoint(1, centres[1]), -MetricVector.UnitY,
                1.0f, 0.12f, out fit);
            midpoint.SetActive(result == CalibrationStatus.Ready);
            if (result == CalibrationStatus.Ready)
            {
                var point = fit.ToTracking(referenceB * 0.5f + MetricVector.UnitY * 0.25f);
                midpoint.transform.position = new XrVector(point.X, point.Y, point.Z);
                Status = $"Miestny rámec z dvoch terčíkov. Odchýlka zhody terčíkov {fit.MaxResidualMeters:F3} m. Kontrolný bod je 25 cm nad ich stredom.";
            }
            else Status = "Polohy terčíkov nesedia. Skontroluj osadenie voči rozmerom ihriska.";
        }
#endif

        void Clear()
        {
            visualObservations.BeginFrame(double.NegativeInfinity);
            fit = null;
            observedAt[0] = observedAt[1] = -100;
            hasNewFrame = false;
            if (midpoint != null)
            {
                midpoint.SetActive(false);
                Status = "Lokálne sledovanie prerušené. Zameraj oba terčíky znova.";
            }
        }

        void OnApplicationPause(bool paused) { if (paused) Clear(); }
        void OnDisable() => Clear();
    }
}
