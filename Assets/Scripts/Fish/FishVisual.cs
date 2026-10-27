using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using FishBreeder.Health;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Builds and animates the 3D model for a fish: a Meshy model from the library when one
    /// matches its genes, otherwise the procedural placeholder. Models split into parts are put
    /// together by <see cref="FishParts"/>, so a cross-bred fish wears the fins and tail of another species.
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

        // The parts the fish is built from, for the illness overlays.
        List<FishParts.Piece> partPieces;
        Transform partSpace;
        Bounds partBounds;
        DiseaseDefinition illness;
        float illnessSeverity;
        int illnessStage = -1;
        GameObject illnessRoot;
        readonly List<Material> illnessMaterials = new List<Material>();
        float sneezeTimer = 2f;

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

        /// <summary>Length of the model every fry wears: a tiny grey fish, whatever it grows into.</summary>
        public const float FryLength = 0.045f;
        /// <summary>Species whose model is used, plain grey, for all fry.</summary>
        const string FryModelSpecies = "ZebraDanio";
        static readonly Color FryGrey = new Color(0.62f, 0.64f, 0.66f);

        /// <summary>Builds the fish's model. <paramref name="fryLook"/> gives it the plain grey fry body instead.</summary>
        public void Build(Phenotype phenotype, bool fryLook = false)
        {
            if (modelRoot != null) Destroy(modelRoot.gameObject);
            bodyMaterials.Clear();
            bodyBaseColors.Clear();
            swimMaterials.Clear();
            swimMeshes.Clear();
            modelEntry = null;
            spine = null;
            tailPivot = null;
            partPieces = null;
            partSpace = null;
            illnessRoot = null;
            illnessMaterials.Clear();
            illnessStage = -1;

            modelRoot = new GameObject("Model").transform;
            modelRoot.SetParent(transform, false);

            var library = modelLibrary != null ? modelLibrary : DefaultLibrary;
            string speciesId = fryLook ? FryModelSpecies : phenotype.species.id;
            var entry = library != null ? library.Find(speciesId, phenotype.finShape, phenotype.tailShape) : null;
            float length = fryLook ? FryLength : phenotype.lengthMeters;
            List<FishParts.Piece> pieces = null;
            if (entry != null)
            {
                var fins = fryLook ? entry : library.Find(phenotype.finSpecies.id, phenotype.finShape, phenotype.tailShape);
                var tail = fryLook ? entry : library.Find(phenotype.tailSpecies.id, phenotype.finShape, phenotype.tailShape);
                pieces = FishParts.Assemble(entry, fins, tail);
            }
            if (pieces != null) BuildFromParts(entry, pieces, length, phenotype.bodyColor, !fryLook);
            else if (entry != null) BuildFromModel(entry, length, fryLook ? FryGrey : Color.Lerp(Color.white, phenotype.bodyColor, entry.geneTintStrength), !fryLook);
            else
            {
                BuildPlaceholder(phenotype);
                if (fryLook) modelRoot.localScale = Vector3.one * (FryLength / Mathf.Max(0.01f, phenotype.lengthMeters));
            }

            var body = gameObject.GetComponent<CapsuleCollider>();
            if (body == null) body = gameObject.AddComponent<CapsuleCollider>();
            body.direction = 2; // Z
            body.height = length * 1.2f;
            body.radius = length * phenotype.heightRatio * 0.5f;
            ShowIllness(illness, illnessSeverity);
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

        /// <summary>
        /// A fish put together from parts (its own, or a cross-bred mix), at this length. Each part
        /// wears its own model's texture tinted by the body colour gene; without <paramref name="textured"/>
        /// it's plain fry grey. All parts share one space, so the swim shader bends them as one body.
        /// </summary>
        void BuildFromParts(FishModelLibrary.Entry bodyEntry, List<FishParts.Piece> pieces, float lengthMeters, Color bodyColor, bool textured)
        {
            modelEntry = bodyEntry;
            partPieces = pieces;
            var bounds = pieces[0].mesh.bounds;
            foreach (var piece in pieces) bounds.Encapsulate(piece.mesh.bounds);
            float scale = lengthMeters / Mathf.Max(bounds.size.z, 0.0001f);

            var space = new GameObject(bodyEntry.displayName).transform;
            space.SetParent(modelRoot, false);
            space.localScale = Vector3.one * scale;
            space.localPosition = -bounds.center * scale;
            partSpace = space;
            partBounds = bounds;

            var cullBounds = bounds;
            cullBounds.Expand(bounds.size.magnitude * 0.5f);
            var shader = SwimShader;
            var materials = new Dictionary<FishModelLibrary.Entry, Material>();
            foreach (var piece in pieces)
            {
                if (!materials.TryGetValue(piece.entry, out var material))
                {
                    var entry = piece.entry;
                    var tint = textured ? Color.Lerp(Color.white, bodyColor, entry.geneTintStrength) : FryGrey;
                    material = new Material(shader) { name = entry.displayName, color = tint };
                    if (textured && entry.albedo != null) material.mainTexture = entry.albedo;
                    if (entry.normalMap != null && material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", entry.normalMap);
                    if (textured && entry.smoothnessMap != null && material.HasProperty("_SmoothnessMap")) material.SetTexture("_SmoothnessMap", entry.smoothnessMap);
                    if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", entry.smoothness);
                    ConfigureSwim(material, space, bounds);
                    materials[entry] = material;
                    TrackMaterial(material);
                    swimMaterials.Add(material);
                    swimMeshes.Add(space);
                }

                var go = new GameObject(piece.mesh.name);
                go.transform.SetParent(space, false);
                go.AddComponent<MeshFilter>().sharedMesh = piece.mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // The bent body can reach a little outside the rest pose; keep it from being culled.
                renderer.localBounds = cullBounds;
            }
            tailPivot = null;
            StartSpine(bodyEntry, lengthMeters);
        }

        void StartSpine(FishModelLibrary.Entry entry, float lengthMeters)
        {
            modelLength = lengthMeters;
            spine = new SwimSpine
            {
                wavesPerBody = entry.wavesPerBody,
                tailTwistDegrees = entry.tailTwistDegrees,
            };
            ApplyBendStart(entry);
            float length = modelLength * transform.lossyScale.x;
            spine.Reset(transform.position + transform.forward * length * 0.5f, transform.forward, transform.up, length);
        }

        /// <summary>
        /// Gives the spine the species' bend start (where its body starts to wiggle) and returns
        /// how stiff that makes the fish (0 = snakes from the head, 1 = only the back half swings),
        /// which tones down its kicks and curls. Models without a species keep their library stiffness.
        /// </summary>
        float ApplyBendStart(FishModelLibrary.Entry entry)
        {
            var species = Genetics.Species.Find(entry.species);
            float bendStart = species != null ? Mathf.Clamp01(species.bendStart) : 0.1f + entry.bodyStiffness * 0.45f;
            float stiffness = Mathf.Clamp01((bendStart - 0.1f) / 0.45f);
            if (spine != null)
            {
                spine.bendStart = bendStart;
                spine.stiffness = stiffness;
            }
            return stiffness;
        }

        /// <summary>A Meshy model that isn't split into parts, at this length and tint; without <paramref name="textured"/> it's a plain colour (fry).</summary>
        void BuildFromModel(FishModelLibrary.Entry entry, float lengthMeters, Color tint, bool textured)
        {
            modelEntry = entry;
            var instance = Instantiate(entry.model, modelRoot, false);
            instance.name = entry.displayName;
            foreach (var c in instance.GetComponentsInChildren<Collider>()) Destroy(c);
            var filters = instance.GetComponentsInChildren<MeshFilter>();
            var model = instance.transform;

            // Turn the model so its head points along the fish's +Z, whatever way it was exported.
            FishParts.HideFills(instance);
            var turn = Quaternion.Euler(0f, DetectHeadYaw(filters), 0f);
            model.localRotation = turn * model.localRotation;
            model.localPosition = turn * model.localPosition;

            // Scale so the gene-driven length wins, then centre the body on the fish's pivot.
            var bounds = BoundsInModelRoot(filters);
            float restLength = Mathf.Max(bounds.size.z, 0.0001f);
            float scale = lengthMeters / restLength;
            model.localScale *= scale;
            model.localPosition = model.localPosition * scale - bounds.center * scale;

            var shader = SwimShader;
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null) continue;

                var material = new Material(shader) { name = entry.displayName, color = tint };
                if (textured && entry.albedo != null) material.mainTexture = entry.albedo;
                if (entry.normalMap != null && material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", entry.normalMap);
                if (textured && entry.smoothnessMap != null && material.HasProperty("_SmoothnessMap")) material.SetTexture("_SmoothnessMap", entry.smoothnessMap);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", entry.smoothness);
                ConfigureSwim(material, filter.transform, filter.sharedMesh.bounds);

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
            StartSpine(entry, lengthMeters);
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

        /// <summary>
        /// Tells the swim shader how the fish lies at rest in <paramref name="space"/>'s local space:
        /// its axes, centre and length (<paramref name="b"/>, the bounds of the whole fish in that space).
        /// </summary>
        void ConfigureSwim(Material material, Transform space, Bounds b)
        {
            var axis = space.InverseTransformDirection(modelRoot.forward).normalized;
            var side = space.InverseTransformDirection(modelRoot.right).normalized;
            var up = space.InverseTransformDirection(modelRoot.up).normalized;
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

        /// <summary>Finds the yaw that points the head along +Z (see <see cref="FishParts.HeadYaw"/>).</summary>
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
            return FishParts.HeadYaw(points);
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

        /// <summary>Straightens the body along the current heading, e.g. after the fish is moved to another tank.</summary>
        public void ResetSpine()
        {
            if (spine == null) return;
            float length = modelLength * transform.lossyScale.x;
            spine.Reset(transform.position + transform.forward * length * 0.5f, transform.forward, transform.up, length);
        }

        /// <summary>
        /// Shows an illness (null for none) at this severity: the disease's body tint, plus a
        /// cartoony look of what it does to real fish (see <see cref="FishSymptoms"/>):
        /// white spots, ragged fins, floating over on its side, or a pale feverish shiver with sneezes.
        /// </summary>
        public void ShowIllness(DiseaseDefinition disease, float severity)
        {
            var previous = illness;
            illness = disease;
            illnessSeverity = severity;
            SetTint(disease != null ? Color.Lerp(Color.white, disease.symptomTint, severity) : Color.white);
            // Rebuild the overlay only when the illness or its stage changes (spots get more numerous).
            int stage = disease == null ? -1 : Mathf.Clamp(Mathf.CeilToInt(severity * 4f), 1, 4);
            if (disease == previous && stage == illnessStage && (illnessRoot != null || stage < 0 || partSpace == null)) return;
            illnessStage = stage;
            ClearIllnessOverlay();
            if (disease == null || partPieces == null || partSpace == null) return;

            float t = stage / 4f;
            int seed = GetInstanceID();
            switch (disease.Look)
            {
                case SymptomLook.Spots:
                    AddOverlay(FishSymptoms.Spots(partPieces, Mathf.RoundToInt(Mathf.Lerp(14f, 70f, t)), 0.014f, seed), new Color(1f, 1f, 0.97f), 0.9f);
                    break;
                case SymptomLook.FinRot:
                    FishSymptoms.FinRot(partPieces, Mathf.RoundToInt(Mathf.Lerp(16f, 48f, t)), Mathf.Lerp(0.012f, 0.02f, t), seed, out var edges, out var sores);
                    AddOverlay(edges, new Color(0.92f, 0.86f, 0.74f), 0.3f);
                    AddOverlay(sores, new Color(0.9f, 0.32f, 0.3f), 0.6f);
                    break;
            }
        }

        void AddOverlay(Mesh mesh, Color color, float gloss)
        {
            if (mesh == null || mesh.vertexCount == 0) return;
            if (illnessRoot == null)
            {
                illnessRoot = new GameObject("Illness");
                illnessRoot.transform.SetParent(partSpace, false);
            }
            var go = new GameObject(mesh.name);
            go.transform.SetParent(illnessRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(SwimShader) { name = mesh.name, color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", gloss);
            ConfigureSwim(material, partSpace, partBounds);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var cull = partBounds;
            cull.Expand(partBounds.size.magnitude * 0.5f);
            renderer.localBounds = cull;
            illnessMaterials.Add(material);
            swimMaterials.Add(material);
            swimMeshes.Add(partSpace);
        }

        void ClearIllnessOverlay()
        {
            foreach (var material in illnessMaterials)
            {
                int i = swimMaterials.IndexOf(material);
                if (i >= 0)
                {
                    swimMaterials.RemoveAt(i);
                    swimMeshes.RemoveAt(i);
                }
                Destroy(material);
            }
            illnessMaterials.Clear();
            if (illnessRoot != null)
            {
                foreach (var filter in illnessRoot.GetComponentsInChildren<MeshFilter>()) Destroy(filter.sharedMesh);
                Destroy(illnessRoot);
            }
            illnessRoot = null;
        }

        /// <summary>
        /// Extra roll from illness this frame: a fish with swim bladder trouble floats over on its
        /// side (further the worse it is); itchy or feverish fish shiver now and then.
        /// </summary>
        float IllnessRoll(float deltaTime)
        {
            if (illness == null) return 0f;
            float t = Mathf.Max(illnessSeverity, 0.25f);
            switch (illness.Look)
            {
                case SymptomLook.Listing:
                    return (25f + 65f * t) + Mathf.Sin(Time.time * 1.3f) * 6f;
                case SymptomLook.Fever:
                {
                    sneezeTimer -= deltaTime;
                    if (sneezeTimer <= 0f)
                    {
                        sneezeTimer = Random.Range(2.5f, 5f);
                        var fish = GetComponent<Fish>();
                        float length = modelLength * transform.lossyScale.x;
                        if (fish != null && fish.Tank != null)
                            Fx.Bubbles(transform.position + transform.forward * length * 0.5f, 3, fish.Tank.SurfaceY, 0.01f);
                    }
                    // A feverish shiver in bursts.
                    float burst = Mathf.Max(0f, Mathf.Sin(Time.time * 0.9f + GetInstanceID()));
                    return Mathf.Sin(Time.time * 38f) * 7f * t * burst * burst;
                }
                case SymptomLook.Spots:
                {
                    // White spot itches: every so often the fish twitches as if scratching.
                    float burst = Mathf.Max(0f, Mathf.Sin(Time.time * 0.6f + GetInstanceID()) - 0.85f) / 0.15f;
                    return Mathf.Sin(Time.time * 30f) * 12f * burst;
                }
                default:
                    return 0f;
            }
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
            bankDegrees += IllnessRoll(deltaTime);
            float effort = Mathf.Clamp(bodyLengthsPerSecond, 0f, 2f);
            thrust = Mathf.Clamp01(thrust);
            float turning = Mathf.Clamp01(Mathf.Abs(turnDegreesPerSecond) / 90f);

            if (spine != null)
            {
                // Moving: the body trails the head. Slow: it stays straight and bends from the turn instead.
                float moving = Mathf.Clamp01(effort / 0.6f);
                // Setting off or turning from still is where a stiff, deep body must not snake: it gets a
                // smaller kick and keeps its front straight. Cruising is left alone.
                // Read every frame so a change on the console's Swimming tab shows straight away.
                float stiffness = ApplyBendStart(modelEntry);
                float startle = 1f - moving;
                float kick = (0.6f * thrust + 0.3f * turning) * Mathf.Lerp(1f, 0.55f, stiffness * startle);
                float targetStroke = Mathf.Clamp(IdleStroke + 0.45f * effort + kick, 0.02f, 1.3f);
                stroke = Mathf.MoveTowards(stroke, targetStroke, deltaTime * 1.2f);
                float beats = modelEntry.tailBeatsPerSecond * Mathf.Lerp(0.35f, 1.4f, Mathf.Clamp01(0.5f * effort + 0.6f * thrust));
                swimPhase = Mathf.Repeat(swimPhase + deltaTime * Mathf.PI * 2f * beats, Mathf.PI * 2f);

                float follow = modelEntry.pathFollow * moving * Mathf.Lerp(1f, 0.6f, stiffness * startle);
                float curl = Mathf.Clamp(turnDegreesPerSecond / 90f, -1f, 1f) * Mathf.Lerp(0.12f, 0.08f, stiffness) * startle;
                float stiffen = Mathf.Lerp(0.35f, 1f, startle);

                float length = modelLength * transform.lossyScale.x;
                var nose = transform.position + transform.forward * length * 0.5f;
                spine.Record(nose, transform.forward, transform.up, length);
                // While hanging about, forget the arc the nose drew, more so for stiff fish.
                spine.Relax(nose, transform.forward, deltaTime * Mathf.Lerp(1.5f, 4f, stiffness) * startle * startle);
                for (int i = 0; i < swimMaterials.Count; i++)
                {
                    // Parts of one fish share a space: the spine only needs working out once for them.
                    if (i == 0 || swimMeshes[i] != swimMeshes[i - 1])
                        spine.Evaluate(nose, transform.forward, transform.up, length, swimPhase, stroke * modelEntry.strokeStrength,
                            bankDegrees, follow, curl, stiffen, swimMeshes[i]);
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
