using UnityEngine;
using UnityEngine.Rendering;

namespace FishBreeder.Core
{
    /// <summary>
    /// Creates runtime materials that work in both the Built-in pipeline and URP.
    /// Note: for player builds, make sure the shader is included (any material asset using it is enough).
    /// </summary>
    public static class MaterialFactory
    {
        static Shader lit;

        public static Shader Lit
        {
            get
            {
                if (lit != null) return lit;
                if (GraphicsSettings.currentRenderPipeline != null)
                    lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null)
                    lit = Shader.Find("Standard");
                return lit;
            }
        }

        public static Material Create(Color color, float smoothness = 0.4f)
        {
            var material = new Material(Lit) { color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            return material;
        }

        /// <summary>Primitive with its collider removed, for decoration.</summary>
        public static GameObject Decor(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Create(color, 0.2f);
            return go;
        }

        /// <summary>
        /// Primitive that fish swim around. Uses a convex mesh collider so squashed or
        /// stretched shapes are matched exactly instead of by a too-big sphere or capsule.
        /// </summary>
        public static GameObject Obstacle(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Create(color, 0.2f);
            if (type != PrimitiveType.Cube)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var meshCollider = go.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                meshCollider.convex = true;
            }
            go.AddComponent<FishBreeder.TankObstacle>();
            return go;
        }
    }
}
