using System;
using UnityEngine;

namespace Spell.Client.Visuals
{
    public abstract class MarkerTheme : IMarkerVisualTheme
    {
        readonly VisualArt[] art = new VisualArt[2];
        protected readonly VisualBudget Budget;
        protected MarkerTheme(VisualBudget budget) { Budget = budget; }
        public int ApiVersion => VisualContract.Version;
        protected abstract void Build(int id, VisualArt model);
        protected abstract void Animate(int id, float seconds);
        public void Present(int boardId, Pose pose, float seconds)
        {
            if (boardId < 0 || boardId >= art.Length) return;
            if (!VisualContract.Valid(pose)) { Hide(boardId); return; }
            if (art[boardId] == null)
            {
                art[boardId] = new VisualArt(GetType().Name + " " + boardId);
                Build(boardId, art[boardId]);
            }
            var root = art[boardId].Root;
            // AR image +Y is its outward normal. Sculptures have their face towards local -Z.
            root.SetPositionAndRotation(pose.position, pose.rotation * Quaternion.Euler(90, 0, 0));
            root.gameObject.SetActive(true);
            Animate(boardId, seconds + boardId * .31f);
        }
        public void Hide(int boardId)
        {
            if (boardId >= 0 && boardId < art.Length) art[boardId]?.Hide();
        }
        public void Dispose() { foreach (var item in art) item?.Dispose(); }
    }

    public sealed class Inferno : MarkerTheme
    {
        readonly Transform[][] sparks = new Transform[2][];
        readonly Transform[][] fire = new Transform[2][];
        public Inferno(VisualBudget budget) : base(budget) {}
        protected override void Build(int id, VisualArt a)
        {
            a.Part("Brass mounting", PrimitiveType.Cylinder, new Vector3(0,.022f,-.032f), new Vector3(.064f,.012f,.064f), new Color(.7f,.38f,.09f));
            a.Part("Charred handle", PrimitiveType.Cylinder, new Vector3(0,.065f,-.046f), new Vector3(.024f,.05f,.024f), new Color(.32f,.16f,.07f));
            a.Part("Brazier", PrimitiveType.Cylinder, new Vector3(0,.12f,-.046f), new Vector3(.074f,.018f,.074f), new Color(.91f,.47f,.09f));
            a.Part("Amber glow", PrimitiveType.Sphere, new Vector3(0,.17f,-.045f), new Vector3(.12f,.14f,.12f), new Color(1f,.34f,.03f,.16f));
            fire[id] = new Transform[3];
            for (int i=0; i<3; i++)
                fire[id][i] = a.Part("Flame", PrimitiveType.Sphere, Vector3.zero, Vector3.one, Color.Lerp(new Color(1,.16f,.015f), new Color(1,1,.28f), i / 2f));
            sparks[id] = new Transform[Budget.SparkCount];
            for (int i=0; i<sparks[id].Length; i++)
                sparks[id][i] = a.Part("Ember", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .007f, new Color(1,.7f,.1f));
        }
        protected override void Animate(int id, float t)
        {
            for (int i=0; i<3; i++)
            {
                float phase = t * (5 + i) + i * 2;
                var flame = fire[id][i];
                flame.localPosition = new Vector3((i-1)*.016f + .008f*Mathf.Sin(phase),.16f+i*.006f,-.047f-i*.008f);
                flame.localScale = new Vector3(.038f,.066f+.024f*Mathf.Sin(phase),.035f);
                flame.localRotation = Quaternion.Euler(0,0,12*Mathf.Cos(phase));
            }
            for (int i=0; i<sparks[id].Length; i++)
            {
                float h = Mathf.Repeat(t*.75f + i*.37f,1);
                sparks[id][i].localPosition = new Vector3(.024f*Mathf.Sin(t*3+i),.18f+h*.12f,-.045f+.02f*Mathf.Cos(i+t));
                sparks[id][i].localScale = Vector3.one*(1-h)*.009f;
            }
        }
    }

    public sealed class Necro : MarkerTheme
    {
        readonly Transform[][] wisps = new Transform[2][];
        readonly Transform[][] smoke = new Transform[2][];
        public Necro(VisualBudget budget) : base(budget) {}
        protected override void Build(int id, VisualArt a)
        {
            var ivory = new Color(.87f,.89f,.64f);
            a.Part("Cranium", PrimitiveType.Sphere, new Vector3(0,.15f,-.065f), new Vector3(.117f,.139f,.107f), ivory);
            a.Part("Jaw", PrimitiveType.Sphere, new Vector3(0,.098f,-.083f), new Vector3(.074f,.064f,.053f), ivory);
            for (int i=-1; i<=1; i+=2)
            {
                a.Part("Eye socket", PrimitiveType.Sphere, new Vector3(i*.024f,.147f,-.108f), new Vector3(.041f,.035f,.018f), new Color(.035f,.051f,.025f));
                a.Part("Soul fire", PrimitiveType.Sphere, new Vector3(i*.024f,.145f,-.118f), Vector3.one*.013f, new Color(.3f,1,.04f));
            }
            a.Part("Nasal cavity", PrimitiveType.Sphere, new Vector3(0,.12f,-.111f), new Vector3(.015f,.023f,.009f), Color.black);
            for (int i=-1; i<=1; i++)
                a.Part("Tooth", PrimitiveType.Cube, new Vector3(i*.015f,.090f,-.111f), new Vector3(.012f,.019f,.01f), ivory);
            wisps[id]=new Transform[3];
            smoke[id]=new Transform[Budget.SparkCount];
            for (int i=0; i<3; i++)
                wisps[id][i]=a.Part("Green flame",PrimitiveType.Sphere,Vector3.zero,Vector3.one,new Color(.18f,.99f,.17f,.83f));
            for (int i=0; i<smoke[id].Length;i++)
                smoke[id][i]=a.Part("Smoke",PrimitiveType.Sphere,Vector3.zero,Vector3.one,new Color(.31f,.49f,.38f,.12f));
        }
        protected override void Animate(int id, float t)
        {
            for (int i=0; i<3;i++)
            {
                float p=t*4+i*2;
                wisps[id][i].localPosition=new Vector3((i-1)*.034f+.008f*Mathf.Cos(p),.204f,-.06f);
                wisps[id][i].localScale=new Vector3(.028f,.064f+.02f*Mathf.Sin(p),.033f);
                wisps[id][i].localRotation=Quaternion.Euler(0,0,16*Mathf.Sin(p));
            }
            for (int i=0; i<smoke[id].Length;i++)
            {
                float h=Mathf.Repeat(t*.32f+(float)i/smoke[id].Length,1);
                smoke[id][i].localPosition=new Vector3(.039f*Mathf.Sin(i+t+h*3),.22f+.13f*h,-.07f);
                smoke[id][i].localScale=Vector3.one*.07f*Mathf.Sin(Mathf.PI*h);
            }
        }
    }
}
