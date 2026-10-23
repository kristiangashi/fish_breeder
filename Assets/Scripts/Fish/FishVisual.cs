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
        readonly List<Material> swimMaterials = new List<Material>();
        readonly List<Transform> swimMeshes = new List<Transform>();
        FishModelLibrary.Entry modelEntry;
        SwimSpine spine;
        float modelLength;
        float swimPhase;
        float stroke = 0.5f;

        /// <summary>Tail stroke when not swimming: a gentle scull for hovering fish, near zero for a resting one.</summary>
        public float IdleStroke { get; set; } = 0.12f;

        static readonly int SpineEnabledId = Shader.PropertyToID("_SpineEnabled");
        static readonly int SpinePosId = Shader.PropertyToID("_SpinePos");
        static readonly int SpineFwdId = Shader.PropertyToID("_SpineFwd");
        static readonly int SpineSideId = Shader.PropertyToID("_SpineSide");
        static readonly int SpineUpId = Shader.PropertyToID("_SpineUp");
        static readonly int BodyAxisId = Shader.PropertyToID("_BodyAxis");
        static readonly int SideAxisId = Shader.PropertyToID("_SideAxis");
        static readonly int UpAxisId = Shader.PropertyToID("_UpAxis");
        static readonly int RestCenterId = Shader.PropertyToID("_RestCenter");
        static readonly int NoseOffsetId = Shader.PropertyToID("_NoseOffset");
        static readonly int BodyLengthId = Shader.PropertyToID("_BodyLength");

        public void Build(Phenotype phenotype)
        {
            if (modelRoot != null) Destroy(modelRoot.gameObject);
            bodyMaterials.Clear();
            bodyBaseColors.Clear();
            swimMaterials.Clear();
            swimMeshes.Clear();
            modelEntry = null;
            spine = null;
            tailPivot = null;

            modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(transform, false);

            var library = modelLibrary != null ? modelLibrary : DefaultLibrary;
            var entry = library != null ? library.Find(phenotype.species.id, phenotype.finShape, phenotype.tailShape) : null;
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
            modelEntry = entry;
            var instance = Instantiate(entry.model, modelRoot, false);
            instance.name = entry.displayName;
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
            var filters = instance.GetComponentsInChildren<MeshFilter>();
            var model = instance.transform;

            // Turn the model so its head points along the fish's +Z, whatever way it was exported.
            var turn = Quaternion.Euler(0f, DetectHeadYaw(filters), 0f);
            model.localRotation = turn * model.localRotation;
            model.localPosition = turn * model.localPosition;

            // Scale so the gene-driven length wins, then centre the body on the fish's pivot.
            var bounds = BoundsInModelRoot(filters);
            float restLength = Mathf.Max(bounds.size.z, 0.0001f);
            float scale = p.lengthMeters / restLength;
            model.localScale *= scale;
            model.localPosition = model.localPosition * scale - bounds.center * scale;

            var shader = SwimShader;
            var tint = Color.Lerp(Color.white, p.bodyColor, entry.geneTintStrength);
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null) continue;

                var material = new Material(shader) { name = entry.displayName, color = tint };
                if (entry.albedo != null) material.mainTexture = entry.albedo;
                if (entry.normalMap != null && material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", entry.normalMap);
                if (entry.smoothnessMap != null && material.HasProperty("_SmoothnessMap")) material.SetTexture("_SmoothnessMap", entry.smoothnessMap);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", entry.smoothness);
                ConfigureSwim(material, filter);

                var slots = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
                TrackMaterial(material);
                swimMaterials.Add(material);
                swimMeshes.Add(filter.transform);

                // The bent body can reach a little outside the rest pose; keep it from being culled.
                var cullBounds = filter.sharedMesh.bounds;
                cullBounds.Expand(cullBounds.size.magnitude * 0.5f);
                renderer.localBounds = cullBounds;
            }
            tailPivot = null;

            modelLength = p.lengthMeters;
            spine = new SwimSpine
            {
                wavesPerBody = entry.wavesPerBody,
                tailTwistDegrees = entry.tailTwistDegrees,
            };
            float length = modelLength * transform.lossyScale.x;
            spine.Reset(transform.position + transform.forward * length * 0.5f, transform.forward, transform.up, length);
        }

        static Shader swimShader;

        static Shader SwimShader
        {
            get
            {
                if (swimShader == null) swimShader = Resources.Load<Shader>("Shaders/FishSwim");
                return swimShader != null && swimShader.isSupported ? swimShader : MaterialFactory.Lit;
            }
        }

        /// <summary>Tells the swim shader how this mesh lies at rest: its axes, centre and length.</summary>
        void ConfigureSwim(Material material, MeshFilter filter)
        {
            var axis = filter.transform.InverseTransformDirection(modelRoot.forward).normalized;
            var side = filter.transform.InverseTransformDirection(modelRoot.right).normalized;
            var up = filter.transform.InverseTransformDirection(modelRoot.up).normalized;
            var b = filter.sharedMesh.bounds;
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                float along = Vector3.Dot(corner - b.center, axis);
                min = Mathf.Min(min, along);
                max = Mathf.Max(max, along);
            }
            material.SetVector(BodyAxisId, axis);
            material.SetVector(SideAxisId, side);
            material.SetVector(UpAxisId, up);
            material.SetVector(RestCenterId, b.center);
            material.SetFloat(NoseOffsetId, max);
            material.SetFloat(BodyLengthId, max - min);
            material.SetFloat(SpineEnabledId, 0f);
        }

        /// <summary>
        /// Finds the yaw that points the head along +Z. The body runs along the longer horizontal
        /// axis, and the head is the end that is widest side to side (tail fins are flat).
        /// </summary>
        float DetectHeadYaw(MeshFilter[] filters)
        {
            var points = new List<Vector3>();
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"[FishBreeder] {mesh.name} is not readable, so its head direction can't be detected. Enable Read/Write in its import settings.");
                    return 0f;
                }
                foreach (var v in mesh.vertices)
                    points.Add(modelRoot.InverseTransformPoint(filter.transform.TransformPoint(v)));
            }
            if (points.Count == 0) return 0f;

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            bool alongX = bounds.size.x > bounds.size.z;

            float min = alongX ? bounds.min.x : bounds.min.z;
            float length = alongX ? bounds.size.x : bounds.size.z;
            float lowMin = float.MaxValue, lowMax = float.MinValue, highMin = float.MaxValue, highMax = float.MinValue;
            foreach (var point in points)
            {
                float t = ((alongX ? point.x : point.z) - min) / length;
                float lateral = alongX ? point.z : point.x;
                if (t < 0.2f) { lowMin = Mathf.Min(lowMin, lateral); lowMax = Mathf.Max(lowMax, lateral); }
                else if (t > 0.8f) { highMin = Mathf.Min(highMin, lateral); highMax = Mathf.Max(highMax, lateral); }
            }
            bool headAtHighEnd = (highMax - highMin) > (lowMax - lowMin);

            if (alongX) return headAtHighEnd ? -90f : 90f;
            return headAtHighEnd ? 0f : 180f;
        }

        Bounds BoundsInModelRoot(MeshFilter[] filters)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = modelRoot.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
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

        /// <summary>
        /// Drives the swim cycle each frame.
        /// <paramref name="bodyLengthsPerSecond"/> is the swimming speed, <paramref name="thrust"/> (0..1) how
        /// hard the fish is accelerating, <paramref name="bankDegrees"/> its roll into the current turn and
        /// <paramref name="turnDegreesPerSecond"/> how fast it is turning.
        /// Fish beat their tail harder and faster when speeding up or turning, glide with gentle
        /// strokes once at speed (burst and coast), and barely scull the tail when hovering.
        /// At speed the body follows the head's path; when hovering, turning on the spot curls it into a C.
        /// </summary>
        public void Animate(float bodyLengthsPerSecond, float thrust, float bankDegrees, float turnDegreesPerSecond, float deltaTime)
        {
            if (modelRoot == null || deltaTime <= 0f) return;
            float effort = Mathf.Clamp(bodyLengthsPerSecond, 0f, 2f);
            thrust = Mathf.Clamp01(thrust);
            float turning = Mathf.Clamp01(Mathf.Abs(turnDegreesPerSecond) / 90f);

            if (spine != null)
            {
                float targetStroke = Mathf.Clamp(IdleStroke + 0.45f * effort + 0.6f * thrust + 0.3f * turning, 0.02f, 1.3f);
                stroke = Mathf.MoveTowards(stroke, targetStroke, deltaTime * 1.2f);
                float beats = modelEntry.tailBeatsPerSecond * Mathf.Lerp(0.35f, 1.4f, Mathf.Clamp01(0.5f * effort + 0.6f * thrust));
                swimPhase = Mathf.Repeat(swimPhase + deltaTime * Mathf.PI * 2f * beats, Mathf.PI * 2f);

                // Moving: the body trails the head. Slow: it stays straight and bends from the turn instead.
                float moving = Mathf.Clamp01(effort / 0.6f);
                float follow = modelEntry.pathFollow * moving;
                float curl = Mathf.Clamp(turnDegreesPerSecond / 90f, -1f, 1f) * 0.12f * (1f - moving);

                float length = modelLength * transform.lossyScale.x;
                var nose = transform.position + transform.forward * length * 0.5f;
                spine.Record(nose, transform.forward, transform.up, length);
                for (int i = 0; i < swimMaterials.Count; i++)
                {
                    spine.Evaluate(nose, transform.forward, transform.up, length, swimPhase, stroke * modelEntry.strokeStrength,
                        bankDegrees, follow, curl, swimMeshes[i]);
                    var material = swimMaterials[i];
                    material.SetVectorArray(SpinePosId, spine.Positions);
                    material.SetVectorArray(SpineFwdId, spine.Forwards);
                    material.SetVectorArray(SpineSideId, spine.Sides);
                    material.SetVectorArray(SpineUpId, spine.Ups);
                    material.SetFloat(SpineEnabledId, 1f);
                }
                return;
            }

            // Placeholder fish: wag the separate tail and sway the body.
            swimPhase = Mathf.Repeat(swimPhase + deltaTime * tailWagFrequency * Mathf.Lerp(0.35f, 1.4f, effort * 0.6f), Mathf.PI * 2f);
            float strength = Mathf.Clamp(0.35f + 0.65f * effort, 0.35f, 1.4f);
            if (tailPivot != null)
                tailPivot.localRotation = Quaternion.Euler(0f, Mathf.Sin(swimPhase) * tailWagDegrees * strength, 0f);
            modelRoot.localRotation = Quaternion.Euler(0f, -Mathf.Cos(swimPhase) * 2.5f * strength, bankDegrees);
        }
    }
}
