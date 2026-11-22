using System;
using System.Numerics;

namespace Spell.Geometry
{
    public readonly struct MarkerPoint
    {
        public int Id { get; }
        public Vector3 PositionMeters { get; }

        public MarkerPoint(int id, Vector3 positionMeters)
        {
            Id = id;
            PositionMeters = positionMeters;
        }
    }

    public enum CalibrationStatus
    {
        Ready, InvalidInput, MarkerIdentityMismatch, InsufficientBaseline, MarkerResidualExceeded
    }

    // Rigid, gravity-aligned transform between an AR tracking frame and an agreed local arena.
    // All points are marker centres in metres. The arena has +Y up; its horizontal axes need
    // not align with north. Input observations must already share one tracking origin.
    public sealed class MarkerCalibration
    {
        readonly Vector3 trackingOrigin, arenaOrigin;
        readonly Vector3 trackingX, trackingUp, trackingZ;
        readonly Vector3 arenaX, arenaZ;

        public float MaxResidualMeters { get; private set; }

        MarkerCalibration(Vector3 trackingOrigin, Vector3 arenaOrigin,
            Vector3 trackingX, Vector3 trackingUp, Vector3 arenaX)
        {
            this.trackingOrigin = trackingOrigin;
            this.arenaOrigin = arenaOrigin;
            this.trackingX = trackingX;
            this.trackingUp = trackingUp;
            this.arenaX = arenaX;
            trackingZ = Vector3.Normalize(Vector3.Cross(trackingX, trackingUp));
            arenaZ = Vector3.Normalize(Vector3.Cross(arenaX, Vector3.UnitY));
        }

        public static CalibrationStatus Fit(MarkerPoint referenceA, MarkerPoint referenceB,
            MarkerPoint observation1, MarkerPoint observation2, Vector3 trackingGravityDown,
            float minimumHorizontalBaselineMeters, float maximumMarkerResidualMeters,
            out MarkerCalibration calibration)
        {
            calibration = null;
            if (referenceA.Id < 0 || referenceB.Id < 0 ||
                !Finite(referenceA.PositionMeters) || !Finite(referenceB.PositionMeters) ||
                !Finite(observation1.PositionMeters) || !Finite(observation2.PositionMeters) ||
                !Finite(minimumHorizontalBaselineMeters) || minimumHorizontalBaselineMeters <= 0 ||
                !Finite(maximumMarkerResidualMeters) || maximumMarkerResidualMeters < 0 ||
                !Unit(-trackingGravityDown, out var up))
                return CalibrationStatus.InvalidInput;

            if (referenceA.Id == referenceB.Id || observation1.Id == observation2.Id)
                return CalibrationStatus.MarkerIdentityMismatch;
            if (observation2.Id == referenceA.Id)
            {
                var swap = observation1;
                observation1 = observation2;
                observation2 = swap;
            }
            if (observation1.Id != referenceA.Id || observation2.Id != referenceB.Id)
                return CalibrationStatus.MarkerIdentityMismatch;

            var trackDelta = observation2.PositionMeters - observation1.PositionMeters;
            var arenaDelta = referenceB.PositionMeters - referenceA.PositionMeters;
            if (!Horizontal(trackDelta, up, minimumHorizontalBaselineMeters, out var trackingX) ||
                !Horizontal(arenaDelta, Vector3.UnitY, minimumHorizontalBaselineMeters, out var arenaX))
                return CalibrationStatus.InsufficientBaseline;

            // With orientation fixed by gravity and bearing, aligning midpoints minimizes
            // the sum of squared marker-centre residuals and gives both markers equal weight.
            var fit = new MarkerCalibration(observation1.PositionMeters + trackDelta * .5f,
                referenceA.PositionMeters + arenaDelta * .5f, trackingX, up, arenaX);
            float errorA = Vector3.Distance(fit.ToShared(observation1.PositionMeters), referenceA.PositionMeters);
            float errorB = Vector3.Distance(fit.ToShared(observation2.PositionMeters), referenceB.PositionMeters);
            fit.MaxResidualMeters = Math.Max(errorA, errorB);
            if (!Finite(fit.MaxResidualMeters) || fit.MaxResidualMeters > maximumMarkerResidualMeters)
                return CalibrationStatus.MarkerResidualExceeded;
            calibration = fit;
            return CalibrationStatus.Ready;
        }

        public Vector3 DirectionToShared(Vector3 direction)
        {
            return arenaX * Vector3.Dot(direction, trackingX) +
                   Vector3.UnitY * Vector3.Dot(direction, trackingUp) +
                   arenaZ * Vector3.Dot(direction, trackingZ);
        }

        public Vector3 ToShared(Vector3 trackingPoint)
        {
            return arenaOrigin + DirectionToShared(trackingPoint - trackingOrigin);
        }

        public Vector3 ToTracking(Vector3 arenaPoint)
        {
            var delta = arenaPoint - arenaOrigin;
            return trackingOrigin + trackingX * Vector3.Dot(delta, arenaX) +
                   trackingUp * delta.Y + trackingZ * Vector3.Dot(delta, arenaZ);
        }

        static bool Horizontal(Vector3 delta, Vector3 up, float requiredSpan, out Vector3 axis)
        {
            Vector3 planar = delta - up * Vector3.Dot(delta, up);
            return Unit(planar, out axis) && planar.Length() >= requiredSpan;
        }

        static bool Unit(Vector3 vector, out Vector3 normalized)
        {
            float length = vector.Length();
            if (!Finite(length) || length < 1e-6f)
            {
                normalized = default;
                return false;
            }
            normalized = vector / length;
            return true;
        }

        static bool Finite(Vector3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
