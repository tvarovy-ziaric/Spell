using System;
using System.IO;
using Spell.Client.Visuals;
using Spell.Geometry;
using UnityEngine;
using V3 = System.Numerics.Vector3;

namespace Spell.Editor
{
    // Use Unity batch -executeMethod Spell.Editor.VisualContractChecks.Run.
    // Exercise the actual Unity modules, material resource and runtime module switch.
    public static class VisualContractChecks
    {
        static int passed;
        static void Check(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("FAIL: "+check);
            passed++;
        }
        static bool Close(Vector3 a, Vector3 b) => Vector3.Distance(a,b) < .0001f;
        sealed class Tracking : IMarkerPoseSource
        {
            public double Time;
            public readonly MarkerObservations Frames = new MarkerObservations();
            public bool TryGetMarkerPose(int id,out Pose pose) => Frames.TryRead(id,Time,out pose);
        }
        sealed class FutureTheme : IMarkerVisualTheme
        {
            public int ApiVersion => 2;
            public void Present(int id,Pose pose,float seconds) => throw new InvalidOperationException();
            public void Hide(int id) {}
            public void Dispose() {}
        }
        public static void Run()
        {
            passed=0;
            var tracking=new Tracking();
            var origin=new Pose(new Vector3(1,2,3),Quaternion.Euler(12,27,35));
            var budget=new VisualBudget(true);
            tracking.Frames.BeginFrame(0);
            tracking.Frames.Observe(0, origin);
            using (var controller=new MarkerPresentation(tracking,new Inferno(budget)))
            {
                controller.Draw(0);
                var torch=GameObject.Find("Inferno 0");
                Check(torch && Close(torch.transform.position,origin.position), "known pose anchors Inferno");
                Check(Quaternion.Angle(torch.transform.rotation,origin.rotation*Quaternion.Euler(90,0,0))<.01f,"image basis rotation");
                Vector3 flame=torch.transform.Find("Flame").localScale;
                controller.Draw(.25f);
                Check(!Close(torch.transform.Find("Flame").localScale,flame),"flame animates independently of pose");
                controller.Select(new Necro(budget));
                controller.Draw(.25f);
                var skull=GameObject.Find("Necro 0");
                Check(!torch && skull && skull.transform.Find("Cranium") && skull.transform.Find("Smoke"),"swap marker theme without recapturing");
                Check(Close(skull.transform.position,origin.position) && tracking.TryGetMarkerPose(0,out var actual) &&
                    actual.rotation==origin.rotation,"localization source unchanged");
                try { controller.Select(new FutureTheme()); throw new Exception("future contract accepted"); }
                catch (ArgumentException) { Check(true,"future theme rejected"); }
                tracking.Time=.81;
                controller.Draw(.81f);
                Check(!skull.activeSelf,"lost camera frame hides marker");
                tracking.Time=1;
                tracking.Frames.BeginFrame(1);
                tracking.Frames.Observe(1,origin);
                controller.Draw(1);
                Check(!skull.activeSelf && GameObject.Find("Necro 1"),"new frame hides unobserved board");
                tracking.Frames.Observe(1,new Pose(new Vector3(float.NaN,0,0),Quaternion.identity));
                controller.Draw(1);
                Check(!GameObject.Find("Necro 1"),"malformed pose hidden");
                tracking.Frames.Observe(1,new Pose(Vector3.zero,default));
                Check(!tracking.TryGetMarkerPose(1,out _) && !tracking.TryGetMarkerPose(9,out _),"invalid rotation and board ID");
            }

            var app=new GameObject("Visual contract harness");
            try
            {
                var view=app.AddComponent<DuelVisuals>();
                app.SendMessage("Awake"); // Edit-mode batch has no game-loop initialization.
                view.SetCameraOpen(true);
                view.ShowFireball(14,Vector3.zero,Vector3.right*4,20,22);
                view.ShowFireball(14,Vector3.zero,Vector3.right*4,20,22);
                view.DrawFlights(19);
                Check(view.VisibleFlights==1 && !GameObject.Find("Fireball · Ember"),"dedupe and defer future flight");
                view.DrawFlights(21);
                var ember=GameObject.Find("Fireball · Ember");
                Check(ember && Close(ember.transform.position,new Vector3(2,0,0)),"Ember replay midpoint");
                try
                {
                    view.SelectFireball(new VisualModule<IFireballRenderer>("v2",2,_ => throw new Exception("incompatible factory invoked")));
                    throw new Exception("future renderer contract accepted");
                }
                catch (ArgumentException) { Check(true,"future Fireball contract rejected before factory invocation"); }
                view.SelectFireball(VisualModules.Rune); view.DrawFlights(21);
                var rune=GameObject.Find("Fireball · Rune");
                Check(!ember && rune && Close(rune.transform.position,new Vector3(2,0,0)) && rune.transform.Find("Orbiting facet"),"Rune exact same replay");
                view.ConfirmImpact(14,Vector3.right*4,22);
                view.DrawFlights(22.14);
                Check(Close(rune.transform.position,Vector3.right*4),"confirmed impact stays at server coordinate");
                view.DrawFlights(23);
                Check(view.VisibleFlights==0 && !rune,"completed effect retired");
                view.ShowFireball(15,Vector3.zero,Vector3.up,100,90);
                Check(view.VisibleFlights==0,"bad event time rejected");
                view.ShowFireball(16,Vector3.one,Vector3.up,100,110);
                view.ClearFlights(); view.DrawFlights(101);
                Check(view.VisibleFlights==0,"paused/reset clears stale motion");
                view.ApplyThermalStatus(4);
                Check(view.FrameLimit==30 && VisualBudget.Auto(0,0).SparkCount < VisualBudget.Auto(8192,2048).SparkCount,"thermal and low-memory visual profile");
                TestProjection(view);
            }
            finally { UnityEngine.Object.DestroyImmediate(app); }
            Debug.Log($"PASS visual contracts: {passed} Unity checks, version {VisualContract.Version}. Physical optical alignment, appearance and FPS require Android.");
        }

        static void TestProjection(DuelVisuals visuals)
        {
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../contracts/fixtures"));
            var arena=ArenaSurface.Load(File.ReadAllBytes(Path.Combine(dir,"hill.bytes")),File.ReadAllText(Path.Combine(dir,"hill.sha256")).Trim());
            var projection=ArenaRegistration.ForArena(arena);
            var mesh = new Mesh();
            try
            {
                Vector3 zero=TerrainDebugPatch.Bake(arena,2,2,mesh);
                var indices=mesh.GetIndices(0);
                Check(mesh.vertexCount==49 && indices.Length>0 && Array.IndexOf(indices,0)<0,"mesh uses 7x7 RAM grid and omits NoData triangles");
                Check(Close(zero+mesh.vertices[24],new Vector3(3,106,3)),"debug mesh reproduces the fixture summit");
                using var patch=new TerrainDebugPatch();
                MarkerCalibration.Fit(new MarkerPoint(0,V3.Zero),new MarkerPoint(1,V3.UnitX*1.5f),
                    new MarkerPoint(0,V3.Zero),new MarkerPoint(1,V3.UnitX*1.5f),-V3.UnitY,1,.12f,out var syntheticFrame);
                patch.Update(arena,projection,syntheticFrame,Vector3.zero);
                Check(GameObject.Find("Mriežka výšok ihriska"),"trustworthy frame shows exact surface");
                patch.Update(arena,projection,null,Vector3.zero);
                Check(!GameObject.Find("Mriežka výšok ihriska"),"lost registration immediately hides debug terrain");
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            Check(projection!=null && ArenaRegistration.FromSurvey(arena,new ArenaRegistration.Survey{version=2})==null,"fail closed on unsupported survey");
            var testSurvey=new ArenaRegistration.Survey{version=1,arenaSha256=arena.Sha256,boardA=new[]{2f,102,2},boardB=new[]{2f,102,3.5f}};
            var rotated=ArenaRegistration.FromSurvey(arena,testSurvey);
            testSurvey.arenaSha256="wrong";
            Check(rotated!=null && ArenaRegistration.FromSurvey(arena,testSurvey)==null,"survey identity bound to exact package");
            testSurvey.arenaSha256=arena.Sha256; testSurvey.boardB=testSurvey.boardA;
            Check(ArenaRegistration.FromSurvey(arena,testSurvey)==null,"degenerate survey");
            MarkerCalibration.Fit(new MarkerPoint(0,V3.Zero),new MarkerPoint(1,V3.UnitX*1.5f),
                new MarkerPoint(0,new V3(8,1,2)),new MarkerPoint(1,new V3(8,1,3.5f)),
                -V3.UnitY,1,.12f,out var fit);
            var a=new Vector3(8,1,2);
            Check(Close(projection.ToArena(fit,a),new Vector3(2,105,2)),"synthetic board maps to exact diagnostic fixture");
            Check(Close(rotated.DirectionToArena(fit,Vector3.forward),Vector3.forward),"survey bearing rotates horizontal axes");
            var testPoint=new Vector3(8.5f,2,2.3f);
            Check(Close(testPoint,rotated.ToTracking(fit,rotated.ToArena(fit,testPoint))),"survey/AR round trip");
            var camera=new Pose(a+Vector3.up*10,Quaternion.FromToRotation(Vector3.forward,Vector3.down));
            Check(visuals.Predict(arena,projection,fit,camera,30,.1f) && visuals.HasAim,"shared first terrain intersection drives aim");
            Check(!visuals.Predict(arena,null,fit,camera,30,.1f) && !visuals.HasAim,"missing georeference hides prediction");
            camera.position+=Vector3.forward*1000;
            Check(!visuals.Predict(arena,projection,fit,camera,30,.1f),"outside play area cannot aim");
        }
    }
}
