using System;
using System.IO;
using Spell.Client.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace Spell.Editor
{
    // Run separately with a graphics device. A successful code-only test is insufficient here.
    public static class VisualPixelChecks
    {
        public static void Run()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Pixel checks require a graphics device.");
            var cameraObject=new GameObject("VFX test camera");
            var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;
            camera.orthographic=true;
            camera.nearClipPlane=.01f;
            camera.farClipPlane=3;
            var target=new RenderTexture(256,256,24);
            camera.targetTexture=target;
            var previous=RenderTexture.active;
            // Place the synthetic capture outside the current scene's normal content.
            var anchor=new Vector3(-30,40,-20);
            var pose=new Pose(anchor,Quaternion.Euler(-90,0,0));
            try
            {
                var budget=new VisualBudget(false);
                camera.orthographicSize=.22f;
                camera.transform.SetPositionAndRotation(anchor+new Vector3(0,.17f,-1),Quaternion.identity);
                Color32[] inferno,necro,ember,rune;
                using (var a=new Inferno(budget)) { a.Present(0,pose,.46f); inferno=Read(camera,"inferno"); }
                using (var b=new Necro(budget)) { b.Present(0,pose,.46f); necro=Read(camera,"necro"); }
                Distinct(inferno,necro);
                camera.orthographicSize=.19f;
                camera.transform.position=anchor+Vector3.back;
                using (var a=new EmberFireball(budget)) { a.Present(anchor,Vector3.right,.2f,-1); ember=Read(camera,"ember"); }
                using (var b=new RuneFireball(budget)) { b.Present(anchor,Vector3.right,.2f,-1); rune=Read(camera,"rune"); }
                Distinct(ember,rune);
            }
            finally
            {
                RenderTexture.active=previous;
                camera.targetTexture=null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
            Debug.Log("PASS visual raster checks: four nonempty rendered models and two distinct swappable pairs.");
        }
        static Color32[] Read(Camera camera,string style)
        {
            camera.Render();
            RenderTexture.active=camera.targetTexture;
            var texture=new Texture2D(256,256,TextureFormat.RGB24,false);
            try
            {
                texture.ReadPixels(new Rect(0,0,256,256),0,0); texture.Apply();
                var pixels=texture.GetPixels32();
                int colored=0;
                foreach (var p in pixels) if (p.r>30 || p.g>30 || p.b>30) colored++;
                if (colored<100 || colored>60000) throw new InvalidOperationException("FAIL empty/obscured model: "+style);
                string dir=Environment.GetEnvironmentVariable("SPELL_VISUAL_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir,style+".png"),texture.EncodeToPNG());
                }
                return pixels;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        static void Distinct(Color32[] a, Color32[] b)
        {
            int differences=0;
            for(int i=0;i<a.Length;i++)
                if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>28) differences++;
            if(differences<300) throw new InvalidOperationException("FAIL indistinguishable plugin outputs.");
        }
    }
}
