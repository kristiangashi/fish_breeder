using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The visible water in a tank: a tinted see-through volume with surface highlights and
    /// moving caustics on the sand. Both follow the tank's water colour, quality and algae.
    /// </summary>
    public class TankWater : MonoBehaviour
    {
        Material volume, caustics;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ClarityId = Shader.PropertyToID("_Clarity");
        static readonly int StrengthId = Shader.PropertyToID("_Strength");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int MaxThicknessId = Shader.PropertyToID("_MaxThickness");

        public static TankWater Build(Transform root, Vector3 size)
        {
            var volumeShader = Shader.Find("FishBreeder/WaterVolume");
            var causticShader = Shader.Find("FishBreeder/Caustics");
            if (volumeShader == null && causticShader == null) return null;

            var water = root.gameObject.AddComponent<TankWater>();
            if (volumeShader != null)
            {
                float height = size.y * 0.94f;
                var box = MaterialFactory.Decor(PrimitiveType.Cube, "Water", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(size.x - 0.01f, height, size.z - 0.01f), Color.white);
                water.volume = new Material(volumeShader);
                // The longest look through the water is corner to corner; murk never builds past that.
                water.volume.SetFloat(MaxThicknessId, new Vector3(size.x, height, size.z).magnitude);
                box.GetComponent<Renderer>().sharedMaterial = water.volume;
                box.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (causticShader != null)
            {
                var quad = MaterialFactory.Decor(PrimitiveType.Quad, "Caustics", root, new Vector3(0f, 0.004f, 0f), new Vector3(size.x - 0.02f, size.z - 0.02f, 1f), Color.white);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                water.caustics = new Material(causticShader);
                quad.GetComponent<Renderer>().sharedMaterial = water.caustics;
                quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return water;
        }

        public void SetLook(Color color, float quality, float algae)
        {
            float clarity = Mathf.Clamp01(Mathf.Min(quality, 1f - algae * 0.8f));
            if (volume != null)
            {
                volume.SetColor(ColorId, color);
                volume.SetFloat(ClarityId, clarity);
            }
            if (caustics != null)
            {
                caustics.SetFloat(StrengthId, Mathf.Lerp(0.05f, 0.4f, clarity));
                caustics.SetColor(TintId, Color.Lerp(new Color(0.6f, 0.75f, 0.5f), new Color(0.75f, 0.92f, 1f), clarity));
            }
        }
    }

    /// <summary>
    /// Streams of bubbles from a bubbler's outlets: small spheres rising and looping, shared out
    /// over the outlets so each one keeps bubbling.
    /// </summary>
    public class TankBubbles : MonoBehaviour
    {
        Transform[] bubbles;
        Vector3[] from;
        float[] speeds;
        float height;

        /// <summary>Outlets are in the tank's visuals space; <paramref name="count"/> bubbles in all (at least one per outlet).</summary>
        public void Initialize(List<Vector3> outlets, int count, float waterHeight)
        {
            height = waterHeight * 0.92f;
            count = Mathf.Max(count, outlets.Count);
            bubbles = new Transform[count];
            from = new Vector3[count];
            speeds = new float[count];
            var material = MaterialFactory.Create(new Color(0.85f, 0.95f, 1f), 0.95f);
            for (int i = 0; i < count; i++)
            {
                from[i] = outlets[i % outlets.Count];
                // Spread out over the climb, so the streams start out steady.
                var start = from[i] + Vector3.up * ((height - from[i].y) * GameRandom.Value);
                var bubble = MaterialFactory.Decor(PrimitiveType.Sphere, "Bubble", transform, start, Vector3.one * GameRandom.Range(0.006f, 0.014f), Color.white);
                bubble.GetComponent<Renderer>().sharedMaterial = material;
                bubble.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bubbles[i] = bubble.transform;
                speeds[i] = GameRandom.Range(0.12f, 0.2f);
            }
        }

        // A broken bubbler (disabled) shows no bubbles.
        void OnEnable() => ShowBubbles(true);
        void OnDisable() => ShowBubbles(false);

        void ShowBubbles(bool show)
        {
            if (bubbles == null) return;
            foreach (var bubble in bubbles) if (bubble != null) bubble.gameObject.SetActive(show);
        }

        void Update()
        {
            if (bubbles == null) return;
            float t = Time.time;
            for (int i = 0; i < bubbles.Length; i++)
            {
                var p = bubbles[i].localPosition;
                p.y += speeds[i] * Time.deltaTime;
                if (p.y > height) p.y = from[i].y;
                float risen = Mathf.Clamp01((p.y - from[i].y) / Mathf.Max(0.01f, height - from[i].y));
                p.x = from[i].x + Mathf.Sin(t * 3f + i * 1.7f) * 0.008f * (risen + 0.2f);
                p.z = from[i].z + Mathf.Cos(t * 2.3f + i) * 0.006f * (risen + 0.2f);
                bubbles[i].localPosition = p;
            }
        }
    }
}
