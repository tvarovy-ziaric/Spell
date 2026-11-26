using UnityEngine;

namespace Spell.Client.Visuals
{
    public sealed class EmberFireball : IFireballRenderer
    {
        readonly VisualArt art = new VisualArt("Fireball · Ember");
        readonly Transform[] tongues;
        readonly Transform core;
        public int ApiVersion => VisualContract.Version;
        public EmberFireball(VisualBudget budget)
        {
            core = art.Part("Hot core", PrimitiveType.Sphere, Vector3.zero, Vector3.one*.12f, new Color(1,.86f,.26f));
            tongues = new Transform[budget.SparkCount];
            for (int i=0; i<tongues.Length; i++)
                tongues[i] = art.Part("Ember tail", PrimitiveType.Sphere, Vector3.zero, Vector3.one, new Color(1,.22f,.02f,.82f));
        }
        public void Present(Vector3 p, Vector3 dir, float t, float impactProgress)
        {
            art.Root.SetPositionAndRotation(p, Quaternion.FromToRotation(Vector3.forward, dir));
            art.Root.gameObject.SetActive(true);
            float expansion = impactProgress < 0 ? 1 : 1 + 5*impactProgress;
            core.localScale = Vector3.one*.12f*expansion*(impactProgress < 0 ? 1 : 1-impactProgress);
            for (int i=0; i<tongues.Length; i++)
            {
                float phase = i*2.4f + t*4;
                float u = (i+1f)/(tongues.Length+1);
                float r = impactProgress < 0 ? .03f : .04f + .35f*impactProgress;
                tongues[i].localPosition = new Vector3(Mathf.Cos(phase)*r, Mathf.Sin(phase)*r, impactProgress < 0 ? -.23f*u : r*Mathf.Sin(phase*2));
                tongues[i].localScale = new Vector3(.06f,.06f,.11f)*(1-u*.6f)*(1-Mathf.Max(0,impactProgress));
            }
        }
        public void Hide() => art.Hide();
        public void Dispose() => art.Dispose();
    }
}
