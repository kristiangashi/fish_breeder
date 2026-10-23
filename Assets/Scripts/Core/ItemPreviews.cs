using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Little pictures for the supplier shop. Things that have a 3D design (fish and decorations)
    /// are photographed at runtime by a hidden studio camera; everything else gets a simple drawn
    /// filler icon until it has a design of its own. Pictures are made once and cached.
    /// </summary>
    public static class ItemPreviews
    {
        public const int Size = 128;
        /// <summary>Layer the studio photographs on, so nothing else in the scene gets in the shot.</summary>
        const int StudioLayer = 31;

        class Job
        {
            public string key;
            public Func<GameObject> make;
            public bool sideView;
        }

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Job> pending = new Dictionary<string, Job>();
        static Studio studio;

        /// <summary>A decoration model, front three-quarter view. Null until the photo is ready.</summary>
        public static Texture2D Decoration(string id)
        {
            return Photo("decor:" + id, false, () =>
            {
                var prefab = Resources.Load<GameObject>("Decor/" + id);
                if (prefab == null) return null;
                var piece = UnityEngine.Object.Instantiate(prefab);
                piece.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                TankDecor.Dress(piece, out _, false);
                return piece;
            });
        }

        /// <summary>A species' model from the side. Null until the photo is ready (or if it has no model).</summary>
        public static Texture2D Fish(Genetics.Species species)
        {
            if (species == null) return null;
            return Photo("fish:" + species.id, true, () =>
            {
                var library = FishVisual.DefaultLibrary;
                var entry = library != null ? library.Find(species.id, default, default) : null;
                if (entry == null || entry.model == null) return null;
                var model = UnityEngine.Object.Instantiate(entry.model);
                var material = new Material(MaterialFactory.Lit);
                if (entry.albedo != null) material.mainTexture = entry.albedo;
                if (entry.normalMap != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", entry.normalMap);
                    material.EnableKeyword("_NORMALMAP");
                }
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var slots = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
                return model;
            });
        }

        static Texture2D Photo(string key, bool sideView, Func<GameObject> make)
        {
            if (cache.TryGetValue(key, out var texture)) return texture;
            if (!pending.ContainsKey(key)) pending[key] = new Job { key = key, make = make, sideView = sideView };
            if (studio == null) studio = new GameObject("Preview studio").AddComponent<Studio>();
            return null;
        }

        /// <summary>Takes the waiting photos, a couple per frame, after the frame's own rendering.</summary>
        class Studio : MonoBehaviour
        {
            Camera cam;
            RenderTexture target;

            void Awake()
            {
                transform.position = new Vector3(0f, -500f, 0f);
                cam = new GameObject("Studio camera").AddComponent<Camera>();
                cam.transform.SetParent(transform, false);
                cam.enabled = false;
                cam.cullingMask = 1 << StudioLayer;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.fieldOfView = 25f;
                target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = target;

                var light = new GameObject("Studio light").AddComponent<Light>();
                light.transform.SetParent(transform, false);
                light.type = LightType.Directional;
                light.cullingMask = 1 << StudioLayer;
                light.intensity = 0.9f;
                light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            }

            void LateUpdate()
            {
                int budget = 2;
                foreach (var key in new List<string>(pending.Keys))
                {
                    if (budget-- <= 0) break;
                    var job = pending[key];
                    pending.Remove(key);
                    cache[key] = Take(job);
                }
            }

            Texture2D Take(Job job)
            {
                GameObject subject = null;
                try { subject = job.make(); }
                catch (Exception e) { Debug.LogWarning($"[FishBreeder] Preview {job.key} failed: {e.Message}"); }
                if (subject == null) return null;

                subject.transform.position = transform.position;
                foreach (var t in subject.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = StudioLayer;
                var renderers = subject.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    DestroyImmediate(subject);
                    return null;
                }
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                // Fish are photographed side-on: look across whichever way the body runs.
                var look = Vector3.forward;
                if (job.sideView) look = bounds.size.x >= bounds.size.z ? Vector3.forward : Vector3.right;
                var direction = (look + Vector3.down * (job.sideView ? 0.15f : 0.35f)).normalized;
                float radius = bounds.extents.magnitude;
                float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                cam.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2f);
                cam.farClipPlane = distance + radius * 2f;
                cam.transform.position = bounds.center - direction * distance;
                cam.transform.rotation = Quaternion.LookRotation(direction);
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var photo = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                photo.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                photo.Apply();
                RenderTexture.active = previous;
                DestroyImmediate(subject);
                return photo;
            }

            void OnDestroy()
            {
                if (target != null) target.Release();
            }
        }

        // ------------------------------------------------------------ drawn fillers

        /// <summary>An egg, plain or speckled, in the given shell colour.</summary>
        public static Texture2D Egg(Color color, bool speckled)
        {
            string key = $"egg:{ColorUtility.ToHtmlStringRGB(color)}:{speckled}";
            if (cache.TryGetValue(key, out var texture)) return texture;
            var canvas = new Canvas();
            var dark = Color.Lerp(color, Color.black, 0.35f);
            canvas.Ellipse(64f, 60f, 38f, 50f, dark);
            canvas.Ellipse(63f, 61f, 35f, 47f, color);
            canvas.Ellipse(50f, 80f, 10f, 14f, Color.Lerp(color, Color.white, 0.6f));
            if (speckled)
            {
                var spot = Color.Lerp(color, Color.black, 0.45f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * 2.39996f, r = 8f + (i * 7.3f) % 26f;
                    canvas.Circle(63f + Mathf.Cos(a) * r * 0.75f, 58f + Mathf.Sin(a) * r, 2.2f + (i % 3), spot);
                }
            }
            return cache[key] = canvas.ToTexture();
        }

        /// <summary>Shell texture for speckled eggs in the tank.</summary>
        public static Texture2D Speckles(Color color)
        {
            string key = $"speckles:{ColorUtility.ToHtmlStringRGB(color)}";
            if (cache.TryGetValue(key, out var texture)) return texture;
            var canvas = new Canvas();
            canvas.Rect(0f, 0f, Size, Size, Color.white);
            var spot = Color.Lerp(Color.white, Color.black, 0.45f);
            for (int i = 0; i < 60; i++) canvas.Circle((i * 37.7f) % Size, (i * 59.3f) % Size, 2f + i % 4, spot);
            return cache[key] = canvas.ToTexture();
        }

        /// <summary>A box of eggs: three eggs in a little carton.</summary>
        public static Texture2D EggBox(Color color, bool speckled)
        {
            string key = $"eggbox:{ColorUtility.ToHtmlStringRGB(color)}:{speckled}";
            if (cache.TryGetValue(key, out var texture)) return texture;
            var canvas = new Canvas();
            var shell = color;
            var spot = Color.Lerp(color, Color.black, 0.45f);
            foreach (var x in new[] { 36f, 64f, 92f })
            {
                canvas.Ellipse(x, 62f, 15f, 21f, Color.Lerp(shell, Color.black, 0.3f));
                canvas.Ellipse(x, 63f, 13.5f, 19.5f, shell);
                if (speckled)
                    for (int i = 0; i < 5; i++) canvas.Circle(x - 5f + (i * 5.3f) % 11f, 56f + (i * 7.1f) % 16f, 1.8f, spot);
            }
            var carton = new Color(0.72f, 0.6f, 0.45f);
            canvas.Rect(14f, 26f, 100f, 30f, Color.Lerp(carton, Color.black, 0.25f));
            canvas.Rect(16f, 28f, 96f, 26f, carton);
            return cache[key] = canvas.ToTexture();
        }

        /// <summary>A bag of water with little grey fry in it, the bag's band in the tier colour.</summary>
        public static Texture2D FryBox(Color color)
        {
            string key = $"frybox:{ColorUtility.ToHtmlStringRGB(color)}";
            if (cache.TryGetValue(key, out var texture)) return texture;
            var canvas = new Canvas();
            var water = new Color(0.55f, 0.75f, 0.92f, 0.9f);
            canvas.Ellipse(64f, 52f, 40f, 38f, Color.Lerp(water, Color.black, 0.2f));
            canvas.Ellipse(64f, 52f, 37f, 35f, water);
            canvas.Rect(54f, 86f, 20f, 16f, Color.Lerp(water, Color.white, 0.4f));
            canvas.Rect(48f, 96f, 32f, 9f, color);
            var fry = new Color(0.45f, 0.47f, 0.5f);
            for (int i = 0; i < 5; i++)
            {
                float x = 42f + (i * 19f) % 44f, y = 34f + (i * 13f) % 32f;
                canvas.Ellipse(x, y, 6f, 2.6f, fry);
                canvas.Ellipse(x - 7f, y, 2.5f, 2.2f, fry);
            }
            return cache[key] = canvas.ToTexture();
        }

        public enum Icon { Filter, Heater, Bubbler, Conditioner, Medicine, Food, Tank, Salt, Repair }

        /// <summary>A placeholder drawing for things that don't have a 3D design yet.</summary>
        public static Texture2D Filler(Icon icon, Color tint)
        {
            string key = $"icon:{icon}:{ColorUtility.ToHtmlStringRGB(tint)}";
            if (cache.TryGetValue(key, out var texture)) return texture;
            var canvas = new Canvas();
            var dark = new Color(0.15f, 0.16f, 0.18f);
            var light = Color.Lerp(tint, Color.white, 0.45f);
            switch (icon)
            {
                case Icon.Filter:
                    canvas.Rect(30f, 70f, 68f, 36f, dark);
                    canvas.Rect(34f, 74f, 60f, 28f, tint);
                    canvas.Rect(70f, 18f, 10f, 56f, dark);
                    for (int i = 0; i < 4; i++) canvas.Rect(38f + i * 14f, 80f, 8f, 16f, light);
                    break;
                case Icon.Heater:
                    canvas.Rect(54f, 14f, 20f, 92f, dark);
                    canvas.Rect(57f, 18f, 14f, 70f, tint);
                    canvas.Circle(64f, 100f, 9f, light);
                    break;
                case Icon.Bubbler:
                    canvas.Rect(34f, 14f, 60f, 10f, dark);
                    for (int i = 0; i < 7; i++) canvas.Circle(50f + (i % 3) * 13f + (i % 2) * 4f, 34f + i * 12f, 5f + i * 0.6f, i % 2 == 0 ? tint : light);
                    break;
                case Icon.Conditioner:
                    canvas.Rect(44f, 14f, 40f, 70f, tint);
                    canvas.Rect(54f, 84f, 20f, 18f, dark);
                    canvas.Rect(48f, 36f, 32f, 24f, light);
                    break;
                case Icon.Medicine:
                    canvas.Rect(40f, 18f, 48f, 72f, Color.white);
                    canvas.Rect(42f, 90f, 44f, 14f, tint);
                    canvas.Rect(58f, 36f, 12f, 36f, tint);
                    canvas.Rect(46f, 48f, 36f, 12f, tint);
                    break;
                case Icon.Food:
                    canvas.Rect(30f, 18f, 68f, 64f, tint);
                    canvas.Rect(26f, 82f, 76f, 14f, dark);
                    for (int i = 0; i < 9; i++) canvas.Circle(40f + (i * 17f) % 48f, 34f + (i * 11f) % 36f, 4f, light);
                    break;
                case Icon.Tank:
                    canvas.Rect(16f, 30f, 96f, 66f, dark);
                    canvas.Rect(20f, 34f, 88f, 54f, tint);
                    canvas.Rect(20f, 34f, 88f, 10f, Color.Lerp(tint, new Color(0.85f, 0.75f, 0.55f), 0.8f));
                    canvas.Rect(26f, 20f, 76f, 10f, Color.Lerp(dark, Color.white, 0.3f));
                    break;
                case Icon.Repair:
                    // A spanner.
                    canvas.Circle(40f, 88f, 20f, tint);
                    canvas.Circle(40f, 88f, 9f, new Color(0.2f, 0.24f, 0.3f));
                    canvas.Rect(32f, 92f, 16f, 18f, new Color(0.2f, 0.24f, 0.3f));
                    for (int i = 0; i < 12; i++) canvas.Circle(48f + i * 5f, 80f - i * 5f, 7f, tint);
                    canvas.Rect(78f, 18f, 30f, 12f, dark);
                    canvas.Rect(86f, 30f, 14f, 22f, light);
                    break;
                case Icon.Salt:
                    canvas.Rect(40f, 16f, 48f, 76f, tint);
                    canvas.Rect(46f, 92f, 36f, 14f, dark);
                    for (int i = 0; i < 6; i++) canvas.Rect(48f + (i % 3) * 11f, 34f + (i / 3) * 14f, 6f, 6f, Color.white);
                    break;
            }
            return cache[key] = canvas.ToTexture();
        }

        /// <summary>A tiny software painter for the filler icons. (0, 0) is the bottom left.</summary>
        class Canvas
        {
            readonly Color[] pixels = new Color[Size * Size];

            public void Rect(float x, float y, float w, float h, Color color)
            {
                for (int py = Mathf.Max(0, (int)y); py < Mathf.Min(Size, (int)(y + h)); py++)
                for (int px = Mathf.Max(0, (int)x); px < Mathf.Min(Size, (int)(x + w)); px++)
                    pixels[py * Size + px] = color;
            }

            public void Circle(float cx, float cy, float r, Color color) => Ellipse(cx, cy, r, r, color);

            /// <summary>Filled ellipse with a soft (anti-aliased) edge.</summary>
            public void Ellipse(float cx, float cy, float rx, float ry, Color color)
            {
                for (int py = Mathf.Max(0, (int)(cy - ry - 1)); py < Mathf.Min(Size, (int)(cy + ry + 2)); py++)
                for (int px = Mathf.Max(0, (int)(cx - rx - 1)); px < Mathf.Min(Size, (int)(cx + rx + 2)); px++)
                {
                    float dx = (px + 0.5f - cx) / rx, dy = (py + 0.5f - cy) / ry;
                    float edge = (1f - Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Min(rx, ry);
                    float coverage = Mathf.Clamp01(edge + 0.5f);
                    if (coverage <= 0f) continue;
                    int i = py * Size + px;
                    var under = pixels[i];
                    var blended = Color.Lerp(under, color, coverage);
                    blended.a = Mathf.Max(under.a, coverage * color.a);
                    pixels[i] = blended;
                }
            }

            public Texture2D ToTexture()
            {
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }
        }
    }
}
