using System;
using System.Collections.Generic;
using Spell.Geometry;
using UnityEngine;

namespace Spell.Client.Visuals
{
    // Draw the exact SW--NE triangulation used by ArenaSurface, using only the in-memory package.
    public sealed class TerrainDebugPatch : IDisposable
    {
        public const int RadiusCells=8;
        readonly VisualArt art;
        readonly Mesh mesh;
        ArenaSurface previous;
        int column, row;
        Vector3 origin;

        public TerrainDebugPatch()
        {
            art=new VisualArt("Mriežka výšok ihriska");
            mesh=new Mesh { name="Miestna sieť terénu" };
            var filter=art.Root.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;
            var renderer=art.Root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=art.Material;
            art.Colorize(renderer,new Color(.18f,.93f,.82f,.8f));
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows=false;
        }

        public void Update(ArenaSurface surface,ArenaRegistration registration,MarkerCalibration frame,Vector3 position)
        {
            if (surface==null || registration==null || frame==null || !VisualContract.Finite(position))
            { Hide(); return; }
            Vector3 local=registration.ToArena(frame,position);
            if (!surface.IsInPlayArea(local.x,local.z)) { Hide(); return; }
            int x=Mathf.FloorToInt(local.x), z=Mathf.FloorToInt(local.z);
            if (previous!=surface || x!=column || z!=row)
            {
                previous=surface; column=x; row=z;
                origin=Bake(surface,x,z,mesh);
            }
            Vector3 p=registration.ToTracking(frame,origin);
            Vector3 north=registration.ToTracking(frame,origin+Vector3.forward)-p;
            Vector3 up=registration.ToTracking(frame,origin+Vector3.up)-p;
            art.Root.SetPositionAndRotation(p,Quaternion.LookRotation(north,up));
            art.Root.gameObject.SetActive(mesh.GetIndexCount(0)>0);
        }

        public static Vector3 Bake(ArenaSurface surface,int x,int z, Mesh target)
        {
            int x0=Math.Max(0,x-RadiusCells),z0=Math.Max(0,z-RadiusCells);
            int x1=Math.Min(surface.Width-1,x+RadiusCells),z1=Math.Min(surface.Height-1,z+RadiusCells);
            target.Clear();
            if (x0>=x1 || z0>=z1) return Vector3.zero;
            int width=x1-x0+1,count=width*(z1-z0+1);
            var vertices=new Vector3[count];
            var valid=new bool[count];
            float y0=surface.TryHeight(x,z,out double height) ? (float)height : 0;
            for (int j=z0;j<=z1;j++)
                for (int i=x0;i<=x1;i++)
                {
                    int id=(j-z0)*width+i-x0;
                    valid[id]=surface.TryHeight(i,j,out height);
                    vertices[id]=new Vector3(i-x0,valid[id]?(float)height-y0:0,j-z0);
                }
            var indices=new List<int>();
            var edges=new HashSet<int>();
            void Edge(int a,int b)
            {
                if (!edges.Add(Math.Min(a,b)*count+Math.Max(a,b))) return;
                indices.Add(a); indices.Add(b);
            }
            void Triangle(int a,int b,int c)
            {
                if (!valid[a] || !valid[b] || !valid[c]) return;
                Edge(a,b); Edge(b,c); Edge(c,a);
            }
            for(int j=z0;j<z1;j++)
                for(int i=x0;i<x1;i++)
                {
                    int sw=(j-z0)*width+i-x0,ne=sw+width+1;
                    Triangle(sw,sw+width,ne);
                    Triangle(sw,ne,sw+1);
                }
            target.vertices=vertices;
            target.SetIndices(indices,MeshTopology.Lines,0);
            return new Vector3(x0,y0,z0);
        }
        public void Hide() => art.Hide();
        public void Dispose() { art.Dispose(); VisualArt.Release(mesh); }
    }
}
