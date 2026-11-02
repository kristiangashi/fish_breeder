using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The pictures sold for the back glass of a tank (set in the balance console under Store &
    /// supplier). Each one is a texture in Resources/Backgrounds named after its id.
    /// </summary>
    public static class TankBackgrounds
    {
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        public static List<BackgroundBalance> All => Balance.Data.supplier.backgrounds ?? new List<BackgroundBalance>();

        public static BackgroundBalance Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var background in All)
                if (background != null && background.id == id) return background;
            return null;
        }

        /// <summary>The picture for a background id, or null.</summary>
        public static Texture2D Texture(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!textures.TryGetValue(id, out var texture))
            {
                texture = Resources.Load<Texture2D>("Backgrounds/" + id);
                if (texture != null) texture.wrapMode = TextureWrapMode.Clamp;
                textures[id] = texture;
            }
            return texture;
        }
    }
}
