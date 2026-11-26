using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Spell.Client.Visuals
{
    // Each module owns a single material and its own unparented display hierarchy.
    public sealed class VisualArt : IDisposable
    {
        public readonly Transform Root;
        public readonly Material Material;
        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        static readonly int Tint = Shader.PropertyToID("_Tint");

        public VisualArt(string label)
        {
            var shader = Resources.Load<Shader>("SpellVisual");
            if (!shader) throw new InvalidOperationException("SpellVisual shader unavailable.");
            Material = new Material(shader);
            Root = new GameObject(label).transform;
            Root.gameObject.SetActive(false);
        }

        public Transform Part(string label, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = label;
            part.transform.SetParent(Root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Release(collider); }
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Colorize(renderer, color);
            return part.transform;
        }

        public void Colorize(Renderer renderer, Color color)
        {
            properties.SetColor(Tint, color);
            renderer.SetPropertyBlock(properties);
        }

        public void Hide() => Root.gameObject.SetActive(false);
        public void Dispose()
        {
            Hide();
            Release(Root.gameObject);
            Release(Material);
        }
        public static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
