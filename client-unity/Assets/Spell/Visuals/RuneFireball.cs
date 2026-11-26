using UnityEngine;

namespace Spell.Client.Visuals
{
    // Second implementation uses orbiting crystalline shards rather than the ember/tail mesh set.
    public sealed class RuneFireball : IFireballRenderer
    {
        readonly VisualArt art = new VisualArt("Fireball · Rune");
        readonly Transform[] shards;
        readonly Transform centre;
        public int ApiVersion => VisualContract.Version;
        public RuneFireball(VisualBudget budget)
        {
            centre = art.Part("Rune seed",PrimitiveType.Cube,Vector3.zero,Vector3.one*.085f,new Color(.85f,.96f,1));
            shards = new Transform[budget.SparkCount];
            for (int i=0; i<shards.Length; i++)
                shards[i] = art.Part("Orbiting facet",PrimitiveType.Cube,Vector3.zero,Vector3.one,new Color(.25f,.64f,1));
        }
        public void Present(Vector3 position, Vector3 dir, float t, float impactProgress)
        {
            art.Root.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.forward,dir));
            art.Root.gameObject.SetActive(true);
            float burst=Mathf.Max(0,impactProgress);
            centre.localRotation=Quaternion.Euler(t*87,t*121,45);
            centre.localScale=Vector3.one*(.085f*(1-burst));
            for (int i=0;i<shards.Length;i++)
            {
                float angle = t*6 + i*2*Mathf.PI/shards.Length;
                float radius = .09f + burst*.34f;
                shards[i].localPosition = new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,.04f*Mathf.Sin(2*angle));
                shards[i].localRotation = Quaternion.Euler(30,angle*Mathf.Rad2Deg,angle*Mathf.Rad2Deg);
                shards[i].localScale = new Vector3(.022f,.056f,.025f)*(1-burst);
            }
        }
        public void Hide() => art.Hide();
        public void Dispose() => art.Dispose();
    }
}
