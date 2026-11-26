using System;
using System.Numerics;
using Spell.Geometry;

namespace Spell.Client.Preflight
{
    public static class FieldReadout
    {
        public static string Format(MarkerCalibration fit, Vector3 camera)
        {
            if (fit == null) return "M2 · spoločný rámec nedostupný.";
            var point = fit.ToShared(camera);
            if (!IsFinite(point.X) || !IsFinite(point.Y) || !IsFinite(point.Z)) return "M2 · poloha nedostupná.";
            return FormattableString.Invariant(
                $"M2 · spoločný rámec (X, Y, Z) v m: {point.X:F3}, {point.Y:F3}, {point.Z:F3}. Odchýlka terčíkov: {fit.MaxResidualMeters:F3} m.");
        }

        static bool IsFinite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }
}
