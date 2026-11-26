using System;
using UnityEngine;

namespace Spell.Client.Visuals
{
    public static class VisualContract
    {
        public const int Version = 1;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
        public static bool Valid(Pose pose)
        {
            var q = pose.rotation;
            return Finite(pose.position) && Mathf.Abs(Quaternion.Dot(q, q) - 1) < .005f;
        }
    }

    // All poses are Unity world metres. Sources return a value copy; trackable Transforms stay private.
    public interface IMarkerPoseSource
    {
        bool TryGetMarkerPose(int id, out Pose pose);
    }

    public interface IMarkerVisualTheme : IDisposable
    {
        int ApiVersion { get; }
        void Present(int boardId, Pose pose, float seconds);
        void Hide(int boardId);
    }

    public interface IFireballRenderer : IDisposable
    {
        int ApiVersion { get; }
        void Present(Vector3 worldPosition, Vector3 travelDirection, float seconds, float impactProgress);
        void Hide();
    }

    public readonly struct VisualBudget
    {
        public readonly int FramesPerSecond;
        public readonly int SparkCount;
        public VisualBudget(bool economical)
        {
            FramesPerSecond = economical ? 30 : 60;
            SparkCount = economical ? 3 : 7;
        }
        // Unknown capability follows the lower-cost profile until measured on real hardware.
        public static VisualBudget Auto(int ramMb, int graphicsMb) => new VisualBudget(ramMb < 4096 || graphicsMb < 1024);
    }

    public sealed class MarkerPresentation : IDisposable
    {
        readonly IMarkerPoseSource source;
        IMarkerVisualTheme theme;
        public MarkerPresentation(IMarkerPoseSource source, IMarkerVisualTheme theme)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            Select(theme);
        }
        public void Select(IMarkerVisualTheme next)
        {
            if (next == null || next.ApiVersion != VisualContract.Version)
                throw new ArgumentException("Unsupported marker theme contract.");
            if (ReferenceEquals(theme, next)) return;
            theme?.Dispose();
            theme = next;
        }
        public void Draw(float seconds)
        {
            for (int id = 0; id < 2; ++id)
            {
                if (source.TryGetMarkerPose(id, out Pose pose) && VisualContract.Valid(pose))
                    theme.Present(id, pose, seconds);
                else theme.Hide(id);
            }
        }
        public void Dispose() => theme?.Dispose();
    }
}
