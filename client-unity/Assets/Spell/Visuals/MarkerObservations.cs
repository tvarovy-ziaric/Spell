using UnityEngine;

namespace Spell.Client.Visuals
{
    // The localization adapter starts an empty frame and supplies FULL_TRACKING observations.
    // Missing markers expire on the very first new frame without evidence, including after pause.
    public sealed class MarkerObservations
    {
        readonly Pose[] poses = new Pose[2];
        readonly bool[] seen = new bool[2];
        double frameTime = double.NegativeInfinity;
        public void BeginFrame(double time)
        {
            frameTime = time;
            seen[0] = seen[1] = false;
        }
        public void Observe(int id, Pose pose)
        {
            if ((uint)id >= 2) return;
            seen[id] = VisualContract.Valid(pose);
            poses[id] = pose;
        }
        public bool TryRead(int id, double now, out Pose pose)
        {
            pose = default;
            double age = now-frameTime;
            if ((uint)id >= 2 || !seen[id] || !(age >= 0 && age <= .8)) return false;
            pose = poses[id];
            return true;
        }
    }
}
