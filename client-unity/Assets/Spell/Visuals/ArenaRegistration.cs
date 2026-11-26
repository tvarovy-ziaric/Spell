using System;
using Spell.Geometry;
using UnityEngine;
using MetricVector = System.Numerics.Vector3;

namespace Spell.Client.Visuals
{
    // The marker-local origin is board A's centre. Elevation must come from the surveyed centres.
    public sealed class ArenaRegistration
    {
        const string FixtureDigest = "fc787c4ed750620ebb12500e6926382eede4134016a1bb3fb2da334c33935546";
        [Serializable] public sealed class Survey
        {
            public int version;
            public string arenaSha256;
            // [east offset, vertical datum elevation, north offset], all metres.
            public float[] boardA;
            public float[] boardB;
        }
        readonly MarkerCalibration survey;
        public MetricVector BoardBLocal { get; }
        ArenaRegistration(MarkerCalibration fit, MetricVector boardBLocal) { survey=fit; BoardBLocal=boardBLocal; }

        public static ArenaRegistration ForArena(ArenaSurface terrain)
        {
            if (terrain == null) return null;
            if (terrain.Sha256 == FixtureDigest)
                return FromSurvey(terrain,new Survey { version=1,arenaSha256=FixtureDigest,
                    boardA=new float[]{2,105,2},boardB=new float[]{3.5f,105,2} });
            var file=Resources.Load<TextAsset>("SpellSurvey");
            if (!file) return null;
            try { return FromSurvey(terrain,JsonUtility.FromJson<Survey>(file.text)); }
            catch (ArgumentException) { return null; }
        }

        public static ArenaRegistration FromSurvey(ArenaSurface terrain, Survey data)
        {
            if (terrain == null || data?.version != 1 || data.arenaSha256 != terrain.Sha256 ||
                data.boardA?.Length != 3 || data.boardB?.Length != 3) return null;
            var a = new MetricVector(data.boardA[0],data.boardA[1],data.boardA[2]);
            var b = new MetricVector(data.boardB[0],data.boardB[1],data.boardB[2]);
            if (!terrain.IsInPlayArea(a.X,a.Z) || !terrain.IsInPlayArea(b.X,b.Z)) return null;
            var delta=b-a;
            var boardBLocal=new MetricVector((float)Math.Sqrt(delta.X*delta.X+delta.Z*delta.Z),delta.Y,0);
            var status=MarkerCalibration.Fit(new MarkerPoint(0,a), new MarkerPoint(1,b),
                new MarkerPoint(0,MetricVector.Zero),new MarkerPoint(1,boardBLocal),
                -MetricVector.UnitY,1f,.12f,out var fit);
            return status==CalibrationStatus.Ready ? new ArenaRegistration(fit,boardBLocal) : null;
        }

        static MetricVector Metric(Vector3 v) => new MetricVector(v.x,v.y,v.z);
        static Vector3 UnityVector(MetricVector v) => new Vector3(v.X,v.Y,v.Z);
        public Vector3 ToArena(MarkerCalibration frame,Vector3 point) =>
            UnityVector(survey.ToShared(frame.ToShared(Metric(point))));
        public Vector3 DirectionToArena(MarkerCalibration frame,Vector3 direction) =>
            UnityVector(survey.DirectionToShared(frame.DirectionToShared(Metric(direction))));
        public Vector3 ToTracking(MarkerCalibration frame,Vector3 point) =>
            UnityVector(frame.ToTracking(survey.ToTracking(Metric(point))));
    }
}
