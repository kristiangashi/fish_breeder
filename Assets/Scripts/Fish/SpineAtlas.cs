using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Every swimming fish's spine in one float texture, one row per fish (16 samples of position,
    /// forward, side and up), so all fish of a kind can share one material and be drawn with GPU
    /// instancing: the FishSwim shader finds its row from a per-fish instanced number instead of
    /// a per-fish material. Uploaded once a frame, just before the cameras draw.
    /// </summary>
    public static class SpineAtlas
    {
        public const int Width = SwimSpine.SampleCount * 4;
        static readonly int TextureId = Shader.PropertyToID("_SpineTex");
        static readonly int SizeId = Shader.PropertyToID("_SpineTexSize");

        static Texture2D texture;
        static Color[] pixels = new Color[0];
        static int rows;
        static readonly Stack<int> free = new Stack<int>();
        static int used;
        static bool dirty;

        /// <summary>Takes a row for a new spine.</summary>
        public static int Take()
        {
            if (uploader == null)
            {
                var go = new GameObject("Spine atlas");
                Object.DontDestroyOnLoad(go);
                uploader = go.AddComponent<Uploader>();
            }
            if (free.Count > 0) return free.Pop();
            if (used >= rows) Grow(Mathf.Max(256, rows * 2));
            return used++;
        }

        /// <summary>Hands a row back when its fish is gone or rebuilt.</summary>
        public static void Give(int row)
        {
            if (row >= 0 && row < used) free.Push(row);
        }

        public static void Write(int row, SwimSpine spine)
        {
            if (row < 0 || row >= rows) return;
            int start = row * Width;
            for (int i = 0; i < SwimSpine.SampleCount; i++)
            {
                int at = start + i * 4;
                pixels[at] = spine.Positions[i];
                pixels[at + 1] = spine.Forwards[i];
                pixels[at + 2] = spine.Sides[i];
                pixels[at + 3] = spine.Ups[i];
            }
            dirty = true;
        }

        static void Grow(int newRows)
        {
            var bigger = new Color[Width * newRows];
            System.Array.Copy(pixels, bigger, pixels.Length);
            pixels = bigger;
            rows = newRows;
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(Width, rows, TextureFormat.RGBAFloat, false, true)
            {
                name = "Fish spines",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            Shader.SetGlobalTexture(TextureId, texture);
            Shader.SetGlobalVector(SizeId, new Vector4(1f / Width, 1f / rows, Width, rows));
            dirty = true;
        }

        /// <summary>Starts empty at every Play, even when the editor keeps static fields between plays.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            if (texture != null) Object.Destroy(texture);
            texture = null;
            pixels = new Color[0];
            rows = used = 0;
            free.Clear();
            dirty = false;
            uploader = null;
        }

        static Uploader uploader;

        static void Upload()
        {
            if (!dirty || texture == null) return;
            dirty = false;
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            // A scene reload can clear globals; cheap to set again.
            Shader.SetGlobalTexture(TextureId, texture);
        }

        /// <summary>Runs after every fish has moved (late in the frame) and before rendering.</summary>
        class Uploader : MonoBehaviour
        {
            void LateUpdate() => Upload();
        }
    }
}
