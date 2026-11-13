using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Algae on the inside of the glass: a quad just inside each of the four panes, drawn by the
    /// AlgaeGlass shader, growing in from the edges and corners as the tank's algae stage climbs
    /// (how far at each stage is on the console's Algae & cleaning tab). The water itself never
    /// turns green. A new stage creeps in (or scrubs off) over a second or two.
    /// </summary>
    public class TankAlgaeGlass : MonoBehaviour
    {
        static readonly int CoverId = Shader.PropertyToID("_Cover");
        static readonly int SizeId = Shader.PropertyToID("_Size");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        Material[] panes;
        Renderer[] renderers;
        float cover, target;

        /// <summary>Seconds to grow or scrub from no algae to all of the glass.</summary>
        const float FullChangeSeconds = 2.5f;

        public static TankAlgaeGlass Build(Transform root, Vector3 size, float baseDepth, bool wallAquarium, float startCover)
        {
            var shader = Shader.Find("FishBreeder/AlgaeGlass");
            if (shader == null) return null;
            var glass = root.gameObject.AddComponent<TankAlgaeGlass>();
            // From just under the substrate's top to the waterline (algae only grows under water).
            float bottom = 0f, top = size.y * 0.94f, height = top - bottom, inset = 0.004f;
            float midY = (top + bottom) * 0.5f;
            var specs = new[]
            {
                // name, centre, yaw, width
                ("AlgaeFront", new Vector3(0f, midY, -size.z * 0.5f + inset), 0f, size.x),
                ("AlgaeBack", new Vector3(0f, midY, size.z * 0.5f - inset), 180f, size.x),
                ("AlgaeLeft", new Vector3(-size.x * 0.5f + inset, midY, 0f), 90f, size.z),
                ("AlgaeRight", new Vector3(size.x * 0.5f - inset, midY, 0f), -90f, size.z),
            };
            glass.panes = new Material[specs.Length];
            glass.renderers = new Renderer[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var (name, centre, yaw, width) = specs[i];
                var quad = MaterialFactory.Decor(PrimitiveType.Quad, name, root, centre, new Vector3(width - 0.006f, height, 1f), Color.white);
                quad.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var material = new Material(shader);
                material.SetVector(SizeId, new Vector4(width, height, 0f, 0f));
                // Each pane (and each tank) gets its own pattern.
                material.SetFloat(SeedId, i * 3.17f + UnityEngine.Random.Range(0f, 50f));
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                glass.panes[i] = material;
                glass.renderers[i] = r;
            }
            glass.cover = glass.target = startCover;
            glass.Apply();
            return glass;
        }

        /// <summary>How much of the glass the algae should cover (0..1); it creeps there.</summary>
        public void SetCover(float value) => target = Mathf.Clamp01(value);

        /// <summary>Jumps straight to the target (for screenshots and tests).</summary>
        public void Snap()
        {
            cover = target;
            Apply();
        }

        void Update()
        {
            if (Mathf.Approximately(cover, target)) return;
            cover = Mathf.MoveTowards(cover, target, Time.unscaledDeltaTime / FullChangeSeconds);
            Apply();
        }

        void Apply()
        {
            if (panes == null) return;
            for (int i = 0; i < panes.Length; i++)
            {
                if (panes[i] != null) panes[i].SetFloat(CoverId, cover);
                // Clean glass draws nothing at all.
                if (renderers[i] != null) renderers[i].enabled = cover > 0.001f;
            }
        }
    }
}
