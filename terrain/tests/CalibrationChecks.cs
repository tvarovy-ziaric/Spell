using System;
using System.Numerics;
using Spell.Geometry;

internal static class CalibrationChecks
{
    public static int Run()
    {
        int count = 0;
        var a = new MarkerPoint(12, new Vector3(1, 1.2f, 2));
        var b = new MarkerPoint(27, new Vector3(4, 1.5f, 6));
        var target = new Vector3(3.5f, 0.1f, -1.75f);
        var down = -Vector3.UnitY;

        void Check(bool pass, string label)
        {
            if (!pass) throw new Exception(label);
            count++;
        }

        foreach (var orientation in new[] { Quaternion.Identity,
            Quaternion.CreateFromYawPitchRoll(.97f, -.31f, .12f),
            Quaternion.CreateFromYawPitchRoll(-1.81f, .28f, -.79f) })
        {
            var translation = new Vector3(-3, 8, 25);
            Vector3 Observe(Vector3 p) => Vector3.Transform(p, orientation) + translation;
            var oa = new MarkerPoint(a.Id, Observe(a.PositionMeters));
            var ob = new MarkerPoint(b.Id, Observe(b.PositionMeters));
            var gravity = Vector3.Transform(down * 9.81f, orientation);
            Check(MarkerCalibration.Fit(a, b, ob, oa, gravity, 1, .01f, out var fit)
                  == CalibrationStatus.Ready, "two marker IDs in reversed observation order");
            Check(fit.MaxResidualMeters < .0001f, "nominal residual");
            Check(Vector3.Distance(fit.ToShared(Observe(target)), target) < .0001f, "third point in arena frame");
            Check(Vector3.Distance(fit.ToTracking(target), Observe(target)) < .0001f, "third point in tracking frame");
            Check(Vector3.Distance(fit.DirectionToShared(Vector3.Transform(Vector3.UnitZ, orientation)), Vector3.UnitZ)
                  < .0001f, "direction ignores translation");
        }
        bool Rejected(MarkerPoint obA, MarkerPoint obB, Vector3 gravity, CalibrationStatus reason)
        {
            var result = MarkerCalibration.Fit(a, b, obA, obB, gravity, 1, .1f, out var fit);
            return result == reason && fit == null;
        }
        Check(Rejected(a, a, down, CalibrationStatus.MarkerIdentityMismatch), "duplicate observation");
        Check(Rejected(a, new MarkerPoint(32, b.PositionMeters), down, CalibrationStatus.MarkerIdentityMismatch),
              "unrecognised identity");
        Check(Rejected(a, new MarkerPoint(b.Id, a.PositionMeters), down, CalibrationStatus.InsufficientBaseline),
              "coincident centres");
        Check(Rejected(a, new MarkerPoint(b.Id, a.PositionMeters + Vector3.UnitY), down,
              CalibrationStatus.InsufficientBaseline), "purely vertical baseline");
        Check(Rejected(a, new MarkerPoint(b.Id, b.PositionMeters + new Vector3(3, 0, 4)),
              down, CalibrationStatus.MarkerResidualExceeded), "observed baseline too long");
        Check(Rejected(a, new MarkerPoint(b.Id, b.PositionMeters + Vector3.UnitY),
              down, CalibrationStatus.MarkerResidualExceeded), "wrong height difference");
        Check(Rejected(a, b, Vector3.Zero, CalibrationStatus.InvalidInput), "missing gravity");
        Check(Rejected(a, new MarkerPoint(b.Id, new Vector3(float.NaN, 2, 3)), down, CalibrationStatus.InvalidInput),
              "nonfinite centre");
        Check(Spell.Client.Preflight.FieldReadout.Format(null, Vector3.Zero).Contains("nedostupný"),
            "lost calibration suppresses stale numeric field readout");
        MarkerCalibration.Fit(a, b, a, b, down, 1, .1f, out var identity);
        Check(Spell.Client.Preflight.FieldReadout.Format(identity, new Vector3(1, 2, 3)).Contains("1.000, 2.000, 3.000"),
            "field readout uses marker coordinates and invariant metres");
        Check(Spell.Client.Preflight.FieldReadout.Format(identity, new Vector3(float.NaN, 2, 3)).Contains("nedostupná"),
            "invalid camera sample has no numeric readout");
        Console.WriteLine($"{count} marker calibration checks passed.");
        return count;
    }
}
