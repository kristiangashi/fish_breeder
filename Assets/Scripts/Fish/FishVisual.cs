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
        // Swimming models: each model space has a spine (a row in the SpineAtlas), and every
        // renderer is bent along its space's spine. Materials are shared by every fish of a kind;
        // what differs per fish goes in a property block, so fish are drawn with GPU instancing.
        readonly List<SwimUnit> swimUnits = new List<SwimUnit>();
        readonly List<SwimRenderer> swimRenderers = new List<SwimRenderer>();
        Color geneColor = Color.white;
        Color tintNow = Color.white;
        // The low-poly copy drawn when the fish is small on screen (null if the model has none).
        Renderer farRenderer;
        LODGroup lodGroup;
        FishModelLibrary.Entry modelEntry;
        SwimSpine spine;
        float modelLength;
        float swimPhase;
        float stroke = 0.5f;
        // Mouth and fins of the big fish (see FishFinMotion): what the shader plays this frame.
        bool movesFins;
        Vector4 finMotion, finMotion2;
        float breathPhase, pectoralPhase;

        // The parts the fish is built from, for the illness overlays.
        List<FishParts.Piece> partPieces;
        Transform partSpace;
        Bounds partBounds;
        DiseaseDefinition illness;
        float illnessSeverity;
        int illnessStage = -1;
        GameObject illnessRoot;
        float sneezeTimer = 2f;

        /// <summary>Tail stroke when not swimming: a gentle scull for hovering fish, near zero for a resting one.</summary>
        public float IdleStroke { get; set; } = 0.12f;

        static readonly int SwimAxisId = Shader.PropertyToID("_SwimAxis");
        static readonly int SwimSideId = Shader.PropertyToID("_SwimSide");
        static readonly int SwimUpId = Shader.PropertyToID("_SwimUp");
        static readonly int RestCenterId = Shader.PropertyToID("_RestCenter");
        static readonly int GeneColorId = Shader.PropertyToID("_GeneColor");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
        static readonly int FinMotionId = Shader.PropertyToID("_FinMotion");
        static readonly int FinMotion2Id = Shader.PropertyToID("_FinMotion2");

        /// <summary>A model space with its own spine row.</summary>
        class SwimUnit
        {
            public Transform space;
            public int row = -1;
        }

        /// <summary>A renderer bent along a spine, with where its body lies in its own mesh space.</summary>
        class SwimRenderer
        {
            public Renderer renderer;
            public SwimUnit unit;
            public Vector4 axis, side, up, center;
            /// <summary>Illness spots and sores: their own colour, untouched by the gene colour or tint.</summary>
            public bool overlay;
            /// <summary>The full model of a big fish, whose mouth and fins move (the far copy and small fish don't).</summary>
            public bool finMotion;
        }

        static MaterialPropertyBlock block;

        /// <summary>Screen height share above which the full model is drawn, and below which the fish isn't drawn at all.</summary>
        const float NearShare = 0.025f, CullShare = 0.0035f;
        /// <summary>Grid cells along the body for the far model (see <see cref="MeshDecimator"/>).</summary>
        const int FarCells = 18;

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
            ReleaseSwim();
            modelEntry = null;
            spine = null;
            tailPivot = null;
            partPieces = null;
            partSpace = null;
            illnessRoot = null;
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
            else if (entry != null) BuildFromModel(entry, length, phenotype.bodyColor, !fryLook);
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
            var look = LookFor(pieces);
            var bounds = look.bounds;
            float scale = lengthMeters / Mathf.Max(bounds.size.z, 0.0001f);

            var space = new GameObject(bodyEntry.displayName).transform;
            space.SetParent(modelRoot, false);
            space.localScale = Vector3.one * scale;
            space.localPosition = -bounds.center * scale;
            partSpace = space;
            partBounds = bounds;
            geneColor = textured ? bodyColor : Color.white;

            // The bent body can reach a little outside the rest pose; keep it from being culled.
            var cullBounds = bounds;
            cullBounds.Expand(bounds.size.magnitude * 0.5f);
            var materials = new Material[look.entries.Count];
            for (int i = 0; i < materials.Length; i++) materials[i] = SharedMaterial(look.entries[i], textured);
            var unit = AddUnit(space);
            var near = AddSwimRenderer(MeshPart("Body", look.near, materials, space, cullBounds), unit, space, bounds, false);
            if (look.movesFins)
            {
                movesFins = near.finMotion = true;
                breathPhase = Random.value * Mathf.PI * 2f;
                pectoralPhase = Random.value * Mathf.PI * 2f;
            }
            if (look.far != null)
            {
                farRenderer = MeshPart("Body (far)", look.far, materials, space, cullBounds);
                // Too small to see a fish's shadow at this distance.
                farRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                AddSwimRenderer(farRenderer, unit, space, bounds, false);
            }
            tailPivot = null;
            StartSpine(bodyEntry, lengthMeters);
            UpdateLods();
        }

        /// <summary>One look (body, fins and tail combination) baked into a mesh, a submesh per kit it came from.</summary>
        class Look
        {
            public Mesh near, far;
            public Bounds bounds;
            /// <summary>The near mesh carries mouth and fin movement (a big fish's look).</summary>
            public bool movesFins;
            public readonly List<FishModelLibrary.Entry> entries = new List<FishModelLibrary.Entry>();
        }

        static readonly Dictionary<List<FishParts.Piece>, Look> looks = new Dictionary<List<FishParts.Piece>, Look>();

        /// <summary>
        /// The pieces of a look merged into one mesh (with a low-poly copy for far away), made once
        /// and shared by every fish that looks like this: one renderer per fish, and fish that match
        /// share a mesh, so they can be drawn together.
        /// </summary>
        static Look LookFor(List<FishParts.Piece> pieces)
        {
            if (looks.TryGetValue(pieces, out var known) && known.near != null) return known;
            var look = new Look { bounds = pieces[0].mesh.bounds };
            foreach (var piece in pieces) look.bounds.Encapsulate(piece.mesh.bounds);

            var groups = new List<Mesh>();
            foreach (var piece in pieces)
            {
                if (look.entries.Contains(piece.entry)) continue;
                look.entries.Add(piece.entry);
                var parts = new List<CombineInstance>();
                foreach (var other in pieces)
                {
                    if (other.entry != piece.entry) continue;
                    for (int sub = 0; sub < other.mesh.subMeshCount; sub++)
                        parts.Add(new CombineInstance { mesh = other.mesh, subMeshIndex = sub, transform = Matrix4x4.identity });
                }
                var group = new Mesh();
                group.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                group.CombineMeshes(parts.ToArray(), true, false);
                groups.Add(group);
            }
            var all = new CombineInstance[groups.Count];
            for (int i = 0; i < groups.Count; i++) all[i] = new CombineInstance { mesh = groups[i], transform = Matrix4x4.identity };
            int vertexCount = 0;
            foreach (var group in groups) vertexCount += group.vertexCount;
            look.near = new Mesh { name = pieces[0].entry.displayName + " look" };
            if (vertexCount > 65000) look.near.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            look.near.CombineMeshes(all, false, false);
            look.near.RecalculateBounds();
            look.movesFins = FishFinMotion.Write(look.near, pieces, look.entries);
            foreach (var group in groups) Destroy(group);
            look.far = MeshDecimator.Simplify(look.near, FarCells);
            looks[pieces] = look;
            return look;
        }

        static readonly Dictionary<FishModelLibrary.Entry, Material>[] sharedMaterials =
            { new Dictionary<FishModelLibrary.Entry, Material>(), new Dictionary<FishModelLibrary.Entry, Material>() };

        /// <summary>The swim material for a kit, shared by every fish wearing it (grey and untextured for fry).</summary>
        static Material SharedMaterial(FishModelLibrary.Entry entry, bool textured)
        {
            var cache = sharedMaterials[textured ? 1 : 0];
            if (cache.TryGetValue(entry, out var known) && known != null) return known;
            var material = new Material(SwimShader) { name = entry.displayName + (textured ? "" : " (fry)"), color = textured ? Color.white : FryGrey, enableInstancing = true };
            if (textured && entry.albedo != null) material.mainTexture = entry.albedo;
            if (entry.normalMap != null && material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", entry.normalMap);
            if (textured && entry.smoothnessMap != null && material.HasProperty("_SmoothnessMap")) material.SetTexture("_SmoothnessMap", entry.smoothnessMap);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", entry.smoothness);
            material.SetFloat(TintStrengthId, textured ? entry.geneTintStrength : 0f);
            cache[entry] = material;
            return material;
        }

        static readonly Dictionary<Color, Material> overlayMaterials = new Dictionary<Color, Material>();

        static Material OverlayMaterial(Color color, float gloss)
        {
            var key = new Color(color.r, color.g, color.b, gloss);
            if (overlayMaterials.TryGetValue(key, out var known) && known != null) return known;
            var material = new Material(SwimShader) { name = "Illness", color = color, enableInstancing = true };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", gloss);
            overlayMaterials[key] = material;
            return material;
        }

        static Renderer MeshPart(string name, Mesh mesh, Material[] materials, Transform parent, Bounds cullBounds)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.localBounds = cullBounds;
            return renderer;
        }

        SwimUnit AddUnit(Transform space)
        {
            var unit = new SwimUnit { space = space, row = SpineAtlas.Take() };
            swimUnits.Add(unit);
            return unit;
        }

        SwimUnit UnitFor(Transform space)
        {
            foreach (var unit in swimUnits) if (unit.space == space) return unit;
            return AddUnit(space);
        }

        /// <summary>Works out where the body lies in the renderer's mesh space (rest pose bounds <paramref name="b"/>) and hands it its spine.</summary>
        SwimRenderer AddSwimRenderer(Renderer renderer, SwimUnit unit, Transform space, Bounds b, bool overlay)
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
            var swim = new SwimRenderer
            {
                renderer = renderer,
                unit = unit,
                axis = new Vector4(axis.x, axis.y, axis.z, max),
                side = new Vector4(side.x, side.y, side.z, max - min),
                up = new Vector4(up.x, up.y, up.z, unit.row),
                center = b.center,
                overlay = overlay,
            };
            swimRenderers.Add(swim);
            ApplyBlock(swim);
            return swim;
        }

        void ApplyBlock(SwimRenderer swim)
        {
            if (swim.renderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetVector(SwimAxisId, swim.axis);
            block.SetVector(SwimSideId, swim.side);
            block.SetVector(SwimUpId, swim.up);
            block.SetVector(RestCenterId, swim.center);
            block.SetColor(GeneColorId, swim.overlay ? Color.white : geneColor);
            block.SetColor(TintId, swim.overlay ? Color.white : tintNow);
            if (swim.finMotion)
            {
                block.SetVector(FinMotionId, finMotion);
                block.SetVector(FinMotion2Id, finMotion2);
            }
            swim.renderer.SetPropertyBlock(block);
        }

        /// <summary>Full model up close, the low-poly copy further off, nothing when the fish is a speck.</summary>
        void UpdateLods()
        {
            if (farRenderer == null)
            {
                if (lodGroup != null) lodGroup.enabled = false;
                return;
            }
            if (lodGroup == null) lodGroup = gameObject.GetComponent<LODGroup>();
            if (lodGroup == null) lodGroup = gameObject.AddComponent<LODGroup>();
            var near = new List<Renderer>();
            foreach (var swim in swimRenderers)
                if (swim.renderer != null && swim.renderer != farRenderer) near.Add(swim.renderer);
            lodGroup.SetLODs(new[] { new LOD(NearShare, near.ToArray()), new LOD(CullShare, new[] { farRenderer }) });
            lodGroup.localReferencePoint = Vector3.zero;
            lodGroup.size = Mathf.Max(0.01f, modelLength) * 1.2f;
            lodGroup.enabled = true;
        }

        /// <summary>Gives back the fish's spine rows (it's being rebuilt or is gone).</summary>
        void ReleaseSwim()
        {
            foreach (var unit in swimUnits) SpineAtlas.Give(unit.row);
            swimUnits.Clear();
            swimRenderers.Clear();
            farRenderer = null;
            movesFins = false;
            finMotion = finMotion2 = Vector4.zero;
        }

        void OnDestroy() => ReleaseSwim();

        void StartSpine(FishModelLibrary.Entry entry, float lengthMeters)
        {
            modelLength = lengthMeters;
            spine = new SwimSpine
            {
                wavesPerBody = entry.wavesPerBody,
                tailTwistDegrees = entry.tailTwistDegrees,
            };
            ApplyBendStart(entry);
            ResetSpine();
        }

        /// <summary>
        /// Gives the spine the species' bend start (where its body starts to wiggle) and the bend limits, and returns
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
                // How far the body may bend, from the console's Swimming tab: stiff fish less.
                var limits = Core.Balance.Data.swimming;
                spine.maxBendDegrees = Mathf.Lerp(limits.maxBendFlexible, limits.maxBendStiff, stiffness);
                spine.maxPitchBendDegrees = limits.maxPitchBend;
                spine.sharpestBend = limits.sharpestBend;
                spine.frontBend = limits.frontBend;
                spine.bankTwist = limits.bankTwist;
            }
            return stiffness;
        }

        /// <summary>A Meshy model that isn't split into parts, at this length and tint; without <paramref name="textured"/> it's a plain colour (fry).</summary>
        void BuildFromModel(FishModelLibrary.Entry entry, float lengthMeters, Color bodyColor, bool textured)
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

            geneColor = textured ? bodyColor : Color.white;
            var material = SharedMaterial(entry, textured);
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null) continue;

                var slots = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;

                // The bent body can reach a little outside the rest pose; keep it from being culled.
                var cullBounds = filter.sharedMesh.bounds;
                cullBounds.Expand(cullBounds.size.magnitude * 0.5f);
                renderer.localBounds = cullBounds;
                AddSwimRenderer(renderer, UnitFor(filter.transform), filter.transform, filter.sharedMesh.bounds, false);
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
            var nose = transform.position + transform.forward * length * 0.5f;
            spine.Reset(nose, transform.forward, transform.up, length);
            // A straight body at rest until it first swims.
            foreach (var unit in swimUnits)
            {
                spine.Evaluate(nose, transform.forward, transform.up, length, 0f, 0f, 0f, 0f, 0f, 0f, unit.space);
                SpineAtlas.Write(unit.row, spine);
            }
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
            var cull = partBounds;
            cull.Expand(partBounds.size.magnitude * 0.5f);
            var renderer = MeshPart(mesh.name, mesh, new[] { OverlayMaterial(color, gloss) }, illnessRoot.transform, cull);
            AddSwimRenderer(renderer, UnitFor(partSpace), partSpace, partBounds, true);
            UpdateLods();
        }

        void ClearIllnessOverlay()
        {
            bool had = swimRenderers.RemoveAll(r => r.overlay) > 0;
            if (illnessRoot != null)
            {
                foreach (var filter in illnessRoot.GetComponentsInChildren<MeshFilter>()) Destroy(filter.sharedMesh);
                // Hidden now: Destroy waits for the end of the frame.
                illnessRoot.SetActive(false);
                Destroy(illnessRoot);
            }
            illnessRoot = null;
            if (had) UpdateLods();
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
            tintNow = tint;
            foreach (var swim in swimRenderers)
                if (!swim.overlay) ApplyBlock(swim);
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
            Perf.FishSpine.Begin();
            AnimateModel(bodyLengthsPerSecond, thrust, bankDegrees, turnDegreesPerSecond, deltaTime);
            Perf.FishSpine.End();
        }

        /// <summary>
        /// Big fish breathe and work their fins: the mouth gulps, slowly when resting and faster and wider
        /// when working hard; the pectorals scull steadily while hovering or turning and fold back when
        /// cruising; the dorsal, ventral and tail fins ripple with the body's beat, more with a harder stroke.
        /// </summary>
        void AnimateMouthAndFins(float effort, float thrust, float turning, float moving, float deltaTime)
        {
            float exertion = Mathf.Clamp01(0.5f * effort + 0.6f * thrust);
            breathPhase = Mathf.Repeat(breathPhase + deltaTime * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1.4f, exertion), Mathf.PI * 2f);
            float gulp = 0.5f - 0.5f * Mathf.Cos(breathPhase);
            float mouth = gulp * gulp * Mathf.Lerp(0.5f, 1f, exertion);

            float hover = 1f - moving;
            float scull = Mathf.Max(hover, turning);
            pectoralPhase = Mathf.Repeat(pectoralPhase + deltaTime * Mathf.PI * 2f * Mathf.Lerp(0.9f, 1.7f, scull), Mathf.PI * 2f);
            float pectorals = Mathf.Clamp01(0.3f + 0.6f * hover + 0.5f * turning) * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(effort - 0.8f));

            float fins = 0.35f + 0.65f * Mathf.Clamp01(stroke);
            finMotion = new Vector4(mouth, pectoralPhase, pectorals, swimPhase);
            finMotion2 = new Vector4(fins, Mathf.Clamp01(stroke), 0f, 0f);
            foreach (var swim in swimRenderers)
                if (swim.finMotion) ApplyBlock(swim);
        }

        void AnimateModel(float bodyLengthsPerSecond, float thrust, float bankDegrees, float turnDegreesPerSecond, float deltaTime)
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
                // Parts of one fish share a space: the spine is worked out once for them all.
                foreach (var unit in swimUnits)
                {
                    spine.Evaluate(nose, transform.forward, transform.up, length, swimPhase, stroke * modelEntry.strokeStrength,
                        bankDegrees, follow, curl, stiffen, unit.space);
                    SpineAtlas.Write(unit.row, spine);
                }
                if (movesFins) AnimateMouthAndFins(effort, thrust, turning, moving, deltaTime);
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
