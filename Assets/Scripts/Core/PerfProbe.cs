using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FishBreeder.Genetics;
using FishBreeder.Store;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>Profiler markers round the game's own per-frame work, so the Profiler window (and <see cref="PerfProbe"/>) can tell them apart.</summary>
    public static class Perf
    {
        public static readonly ProfilerMarker FishBrain = new ProfilerMarker("FishBreeder.FishBrain");
        public static readonly ProfilerMarker FishSpine = new ProfilerMarker("FishBreeder.FishSpine");
        public static readonly ProfilerMarker FishProbes = new ProfilerMarker("FishBreeder.FishProbes");
        public static readonly ProfilerMarker TankMates = new ProfilerMarker("FishBreeder.TankMates");
    }

    /// <summary>
    /// Performance test (editor and development builds). F11 packs the store: in dev mode it opens
    /// every room and buys a tank for every empty spot, then fills every tank to its hard limit with
    /// grown fish of mixed species. F12 measures: about six seconds in the view you're in, then (in
    /// the store view) six seconds inside the fullest tank, and writes "[Perf]" lines to the log.
    /// Nothing is saved, so a packed store is gone at the next Play.
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        const float MeasureSeconds = 6f;
        const float SettleSeconds = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (!DevMode.Available || FindAnyObjectByType<PerfProbe>() != null) return;
            var go = new GameObject("PerfProbe") { hideFlags = HideFlags.DontSave };
            go.AddComponent<PerfProbe>();
        }

        bool running;

        void Update()
        {
            if (running) return;
            if (Input.GetKeyDown(KeyCode.F11)) Pack();
            if (Input.GetKeyDown(KeyCode.F12)) StartCoroutine(Measure());
        }

        static StoreLayout Store => FindAnyObjectByType<StoreLayout>();

        static void Pack()
        {
            var store = Store;
            if (store == null) return;
            if (DevMode.On)
            {
                foreach (var section in store.Sections)
                    if (!section.unlocked) store.TryUnlock(section);
                foreach (var slot in store.Slots)
                {
                    if (slot.tank != null || !slot.IsOpen) continue;
                    var role = slot.backRoom ? TankRole.Breeder : TankRole.Display;
                    if (!store.CanBuyTank(slot, role, out _)) role = slot.backRoom ? TankRole.Quarantine : TankRole.ForSale;
                    if (store.CanBuyTank(slot, role, out _)) store.TryBuyTank(slot, role);
                }
            }
            else GameLog.Add("Perf test: dev mode is off (F10), so only the tanks you have are filled.");

            var decorations = Balance.Data.supplier.decorations;
            int fish = 0, decor = 0;
            foreach (var storeTank in store.Tanks)
            {
                var tank = storeTank.Tank;
                // A realistic dressing: about half the decoration spots used.
                for (int i = tank.Decor.Count; i < tank.DecorationSlots / 2 && decorations.Count > 0; i++)
                    if (tank.Decor.Add(decorations[GameRandom.Range(0, decorations.Count)].id)) decor++;
                var all = Species.All;
                while (!tank.IsFull)
                {
                    var species = all[GameRandom.Range(0, all.Count)];
                    FishFactory.Spawn(Genome.CreateRandom(species), 1, 1f, tank, tank.RandomFreePoint(0.1f));
                    fish++;
                }
            }
            string line = $"[Perf] Packed {store.Tanks.Count} tanks: added {fish} fish and {decor} decorations ({CountFish()} fish in all). F12 measures.";
            Debug.Log(line);
            GameLog.Add(line);
        }

        static int CountFish()
        {
            var store = Store;
            int count = 0;
            if (store != null) foreach (var t in store.Tanks) count += t.Tank.Residents.Count;
            return count;
        }

        IEnumerator Measure()
        {
            running = true;
            GameLog.Add("Perf test: measuring, keep still for a few seconds...");
            yield return Sample(Tank.Focused != null ? $"tank view ({Tank.Focused.Title})" : "store view");

            // From the store, also look inside the fullest tank.
            var view = FindAnyObjectByType<StoreView>();
            var store = Store;
            if (Tank.Focused == null && view != null && store != null && store.Tanks.Count > 0)
            {
                StoreTank fullest = null;
                foreach (var t in store.Tanks)
                    if (fullest == null || t.Tank.Residents.Count > fullest.Tank.Residents.Count) fullest = t;
                var enter = typeof(StoreView).GetMethod("EnterTank", BindingFlags.Instance | BindingFlags.NonPublic);
                var exit = typeof(StoreView).GetMethod("ExitTank", BindingFlags.Instance | BindingFlags.NonPublic);
                if (enter != null && exit != null)
                {
                    enter.Invoke(view, new object[] { fullest });
                    yield return new WaitForSecondsRealtime(SettleSeconds);
                    yield return Sample($"tank view ({fullest.Tank.Title}, {fullest.Tank.Residents.Count} fish)");
                    exit.Invoke(view, null);
                }
            }
            GameLog.Add("Perf test done: see the [Perf] lines in the console.");
            running = false;
        }

        static readonly string[] Counters =
        {
            "Batches Count", "SetPass Calls Count", "Draw Calls Count", "Triangles Count", "Vertices Count",
            "Shadow Casters Count", "Dynamic Batched Draw Calls Count", "Static Batched Draw Calls Count", "Instanced Batched Draw Calls Count",
        };

        static readonly string[] Markers =
        {
            "FishBreeder.FishBrain", "FishBreeder.FishProbes", "FishBreeder.FishSpine", "FishBreeder.TankMates",
            "BehaviourUpdate", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "FixedUpdate.PhysicsFixedUpdate",
            "Physics.SyncColliderTransform", "Physics.Simulate", "PostLateUpdate.UpdateAllRenderers",
            "Camera.Render", "Shadows.RenderShadowmap", "Render.OpaqueGeometry", "Render.TransparentGeometry",
            "Culling", "ParticleSystem.Update", "GUI.Repaint", "PostLateUpdate.PlayerUpdateCanvases",
            "Gfx.WaitForPresentOnGfxThread", "Gfx.WaitForGfxCommandsFromMainThread",
        };

        static IEnumerator Sample(string label)
        {
            var recorders = new List<KeyValuePair<string, ProfilerRecorder>>();
            var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
            var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            foreach (var name in Counters) recorders.Add(new KeyValuePair<string, ProfilerRecorder>(name, ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 1)));
            var markers = new List<KeyValuePair<string, ProfilerRecorder>>();
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var byName = new Dictionary<string, ProfilerRecorderHandle>();
            foreach (var h in handles)
            {
                var name = ProfilerRecorderHandle.GetDescription(h).Name;
                if (name != null && !byName.ContainsKey(name)) byName[name] = h;
            }
            foreach (var name in Markers)
            {
                var recorder = byName.TryGetValue(name, out var handle)
                    ? new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default)
                    : default(ProfilerRecorder);
                if (recorder.Valid) recorder.Start();
                markers.Add(new KeyValuePair<string, ProfilerRecorder>(name, recorder));
            }

            yield return null;
            yield return null;
            var frames = new List<float>();
            double mainSum = 0, renderSum = 0, gcSum = 0;
            var counterSums = new double[Counters.Length];
            var markerSums = new double[Markers.Length];
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < MeasureSeconds)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000f);
                mainSum += Last(main) * 1e-6;
                renderSum += Last(render) * 1e-6;
                gcSum += Last(gc);
                for (int i = 0; i < recorders.Count; i++) counterSums[i] += Last(recorders[i].Value);
                for (int i = 0; i < markers.Count; i++) markerSums[i] += Last(markers[i].Value) * 1e-6;
            }
            int n = Mathf.Max(1, frames.Count);
            frames.Sort();
            float avg = 0f;
            foreach (var f in frames) avg += f;
            avg /= n;

            var sb = new StringBuilder();
            sb.Append($"[Perf] {label}: {CountFish()} fish, {n} frames, {avg:0.0} ms avg ({1000f / Mathf.Max(avg, 0.01f):0} fps), p95 {frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * 0.95f))]:0.0} ms");
            sb.Append(main.Valid ? "" : " (no main thread timer)");
            sb.Append($" | main thread {mainSum / n:0.0} ms, render thread {renderSum / n:0.0} ms, GC {gcSum / n / 1024:0.0} KB/frame");
            sb.Append($" | renderers on {CountRenderers()}, {Screen.width}x{Screen.height}");
            sb.Append("\n[Perf]   counters:");
            for (int i = 0; i < recorders.Count; i++)
                sb.Append(recorders[i].Value.Valid ? $" {Short(Counters[i])} {counterSums[i] / n:0}," : $" {Short(Counters[i])} n/a,");
            sb.Append("\n[Perf]   ms/frame:");
            for (int i = 0; i < markers.Count; i++)
                sb.Append(markers[i].Value.Valid ? $" {Markers[i]} {markerSums[i] / n:0.00}," : $" {Markers[i]} n/a,");
            sb.Append("\n[Perf]   seen by the camera:").Append(Breakdown());
            Debug.Log(sb.ToString());
            GameLog.Add($"Perf ({label}): {avg:0.0} ms a frame, main thread {mainSum / n:0.0} ms.");

            if (main.Valid) main.Dispose();
            if (render.Valid) render.Dispose();
            if (gc.Valid) gc.Dispose();
            foreach (var r in recorders) if (r.Value.Valid) r.Value.Dispose();
            foreach (var r in markers) if (r.Value.Valid) r.Value.Dispose();
        }

        static double Last(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : 0;

        static string Short(string counter) => counter.Replace(" Count", "").Replace(" Calls", "");

        /// <summary>Renderers and triangles the camera saw last frame, by what they belong to.</summary>
        static string Breakdown()
        {
            var groups = new SortedDictionary<string, long[]>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.isVisible) continue;
                string group;
                if (r.GetComponentInParent<Fish>() != null) group = "fish";
                else if (r.GetComponentInParent<TankDecor>() != null) group = "tank/" + TankPart(r.transform);
                else group = r.transform.root.name;
                long tris = 0;
                var filter = r.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
                if (mesh != null)
                    for (int i = 0; i < mesh.subMeshCount; i++) tris += mesh.GetIndexCount(i) / 3;
                if (!groups.TryGetValue(group, out var g)) groups[group] = g = new long[3];
                g[0]++;
                g[1] += tris;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) g[2]++;
            }
            var sb = new StringBuilder();
            foreach (var g in groups) sb.Append($" {g.Key}: {g.Value[0]} renderers {g.Value[1] / 1000}k tris {g.Value[2]} cast shadows;");
            sb.Append($" shadows {QualitySettings.shadows} to {QualitySettings.shadowDistance:0} m");
            return sb.ToString();
        }

        /// <summary>The tank's own piece a renderer is in: two levels down from the tank, e.g. "Visuals/Decor".</summary>
        static string TankPart(Transform t)
        {
            var tank = t.GetComponentInParent<TankDecor>().transform;
            var path = new List<string>();
            for (var p = t; p != null && p != tank; p = p.parent) path.Insert(0, p.name);
            return path.Count > 1 ? path[0] + "/" + path[1] : path.Count == 1 ? path[0] : "self";
        }

        static int CountRenderers()
        {
            int on = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && r.gameObject.activeInHierarchy) on++;
            return on;
        }
    }
}
