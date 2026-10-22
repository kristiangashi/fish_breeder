using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Builds and animates the 3D model for a fish: a Meshy model from the library when one
    /// matches its genes, otherwise the procedural placeholder.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishVisual : MonoBehaviour
    {
        /// <summary>Set by GameBootstrap; per-fish override below.</summary>
        public static FishModelLibrary DefaultLibrary;

        [SerializeField] FishModelLibrary modelLibrary;
        [SerializeField] float tailWagDegrees = 25f;
        [SerializeField] float tailWagFrequency = 9f;

        Transform modelRoot;
        Transform tailPivot;
        readonly List<Material> bodyMaterials = new List<Material>();
        readonly List<Color> bodyBaseColors = new List<Color>();
        float swimPhase;

        public void Build(Phenotype phenotype)
        {
            if (modelRoot != null) Destroy(modelRoot.gameObject);
            bodyMaterials.Clear();
            bodyBaseColors.Clear();

            modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(transform, false);

            var library = modelLibrary != null ? modelLibrary : DefaultLibrary;
            var entry = library != null ? library.Find(phenotype.finShape, phenotype.tailShape) : null;
            if (entry != null) BuildFromModel(entry, phenotype);
            else BuildPlaceholder(phenotype);

            var body = gameObject.GetComponent<CapsuleCollider>();
            if (body == null) body = gameObject.AddComponent<CapsuleCollider>();
            body.direction = 2; // Z
            body.height = phenotype.lengthMeters * 1.2f;
            body.radius = phenotype.lengthMeters * phenotype.heightRatio * 0.5f;
        }

        void BuildPlaceholder(Phenotype p)
        {
            float length = p.lengthMeters;
            float height = length * p.heightRatio;
            float width = height * 0.5f;

            var bodyMaterial = MaterialFactory.Create(Color.white, 0.6f);
            bodyMaterial.mainTexture = FishPatternTexture.Generate(p);
            var finMaterial = MaterialFactory.Create(p.finColor, 0.3f);
            TrackMaterial(bodyMaterial);
            TrackMaterial(finMaterial);

            Part("Body", PlaceholderFishMesh.Body(length, height, width), bodyMaterial, modelRoot, Vector3.zero, Quaternion.identity);

            const float tailT = 0.06f;
            tailPivot = new GameObject("TailPivot").transform;
            tailPivot.SetParent(modelRoot, false);
            tailPivot.localPosition = new Vector3(0f, 0f, Mathf.Lerp(-length * 0.5f, length * 0.5f, tailT));
            Part("Tail", PlaceholderFishMesh.Tail(p.tailShape, length * 0.45f * p.finSize), finMaterial, tailPivot, Vector3.zero, Quaternion.identity);

            float finLength = length * 0.35f;
            float finHeight = height * 0.6f * p.finSize;
            float backY = PlaceholderFishMesh.Profile(0.52f) * height * 0.5f * 0.9f;
            Part("DorsalFin", PlaceholderFishMesh.Fin(p.finShape, finLength, finHeight), finMaterial, modelRoot,
                new Vector3(0f, backY, length * 0.02f), Quaternion.identity);

            float bellyY = PlaceholderFishMesh.Profile(0.35f) * height * 0.5f * 0.85f;
            Part("AnalFin", PlaceholderFishMesh.Fin(p.finShape, finLength * 0.6f, finHeight * 0.5f), finMaterial, modelRoot,
                new Vector3(0f, -bellyY, -length * 0.15f), Quaternion.Euler(0f, 0f, 180f));

            var pectoral = PlaceholderFishMesh.Fin(p.finShape, finLength * 0.5f, finHeight * 0.55f);
            float sideX = PlaceholderFishMesh.Profile(0.68f) * width * 0.5f * 0.8f;
            Part("PectoralFinL", pectoral, finMaterial, modelRoot, new Vector3(-sideX, -height * 0.08f, length * 0.15f), Quaternion.Euler(0f, -20f, 110f));
            Part("PectoralFinR", pectoral, finMaterial, modelRoot, new Vector3(sideX, -height * 0.08f, length * 0.15f), Quaternion.Euler(0f, 20f, -110f));

            float eyeT = 0.84f;
            float eyeZ = Mathf.Lerp(-length * 0.5f, length * 0.5f, eyeT);
            float eyeX = PlaceholderFishMesh.Profile(eyeT) * width * 0.5f * 0.85f;
            float eyeSize = height * 0.16f;
            var eyeColor = new Color(0.05f, 0.05f, 0.05f);
            MaterialFactory.Decor(PrimitiveType.Sphere, "EyeL", modelRoot, new Vector3(-eyeX, height * 0.1f, eyeZ), Vector3.one * eyeSize, eyeColor);
            MaterialFactory.Decor(PrimitiveType.Sphere, "EyeR", modelRoot, new Vector3(eyeX, height * 0.1f, eyeZ), Vector3.one * eyeSize, eyeColor);
        }

        void BuildFromModel(FishModelLibrary.Entry entry, Phenotype p)
        {
            var instance = Instantiate(entry.prefab, modelRoot, false);
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);

            // Normalise size so the gene-driven length wins over whatever scale Meshy exported.
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float modelLength = Mathf.Max(bounds.size.z, 0.0001f);
                instance.transform.localScale *= p.lengthMeters / modelLength;
            }

            // Tint: works best when the Meshy texture is white/greyscale. The pattern
            // texture is not applied here yet (see Docs/MeshyPipeline.md, step 5).
            foreach (var r in renderers)
            {
                foreach (var material in r.materials)
                {
                    material.color = p.bodyColor;
                    TrackMaterial(material);
                }
            }

            tailPivot = string.IsNullOrEmpty(entry.tailTransformName) ? null : FindDeep(instance.transform, entry.tailTransformName);
        }

        static void Part(string name, Mesh mesh, Material material, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void TrackMaterial(Material material)
        {
            bodyMaterials.Add(material);
            bodyBaseColors.Add(material.color);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Multiplies all fish materials, used for disease symptoms.</summary>
        public void SetTint(Color tint)
        {
            for (int i = 0; i < bodyMaterials.Count; i++)
                bodyMaterials[i].color = bodyBaseColors[i] * tint;
        }

        /// <summary>Tail wag and body sway; <paramref name="effort"/> is roughly speed / cruise speed.</summary>
        public void Animate(float effort, float deltaTime)
        {
            if (modelRoot == null) return;
            effort = Mathf.Clamp(effort, 0.2f, 2f);
            swimPhase += deltaTime * tailWagFrequency * effort;
            float wave = Mathf.Sin(swimPhase);
            if (tailPivot != null)
                tailPivot.localRotation = Quaternion.Euler(0f, wave * tailWagDegrees * effort, 0f);
            modelRoot.localRotation = Quaternion.Euler(0f, -wave * 4f * effort, 0f);
        }
    }
}
