using UnityEngine;

namespace FishBreeder.Shop
{
    public enum Mood { None, Happy, Grossed, Sad, Mad }

    /// <summary>Little face icons for customer thought bubbles, drawn into textures at startup (no art needed).</summary>
    public static class MoodIcons
    {
        const int Size = 64;
        static Texture2D happy, grossed, sad, mad, bubble;

        public static Texture2D Bubble => bubble != null ? bubble : bubble = MakeBubble();

        public static Texture2D Get(Mood mood)
        {
            switch (mood)
            {
                case Mood.Happy: return happy != null ? happy : happy = MakeFace(new Color(1f, 0.85f, 0.25f), Mood.Happy);
                case Mood.Grossed: return grossed != null ? grossed : grossed = MakeFace(new Color(0.6f, 0.82f, 0.35f), Mood.Grossed);
                case Mood.Sad: return sad != null ? sad : sad = MakeFace(new Color(0.55f, 0.75f, 1f), Mood.Sad);
                case Mood.Mad: return mad != null ? mad : mad = MakeFace(new Color(1f, 0.42f, 0.32f), Mood.Mad);
                default: return null;
            }
        }

        static Texture2D NewTexture()
        {
            return new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        }

        static Texture2D MakeFace(Color skin, Mood mood)
        {
            var texture = NewTexture();
            var ink = new Color(0.15f, 0.1f, 0.08f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                // -1..1 with +y up.
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                float r = p.magnitude;
                var c = Color.clear;
                if (r < 0.92f) c = skin;
                if (r >= 0.84f && r < 0.94f) c = ink;
                if (r < 0.84f)
                {
                    float feature = Features(p, mood);
                    if (feature > 0.5f) c = ink;
                    if (mood == Mood.Sad && Tear(p)) c = new Color(0.3f, 0.6f, 1f);
                    if (mood == Mood.Grossed && Tongue(p)) c = new Color(0.9f, 0.4f, 0.45f);
                }
                // Soft edge.
                if (r >= 0.92f && r < 0.96f) c = new Color(ink.r, ink.g, ink.b, (0.96f - r) / 0.04f);
                pixels[y * Size + x] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static float Features(Vector2 p, Mood mood)
        {
            const float line = 0.075f;
            var leftEye = new Vector2(-0.32f, 0.22f);
            var rightEye = new Vector2(0.32f, 0.22f);
            bool hit = false;

            switch (mood)
            {
                case Mood.Grossed:
                    // Squeezed-shut eyes: little > < shapes.
                    hit |= Segment(p, leftEye + new Vector2(-0.12f, 0.08f), leftEye, line) || Segment(p, leftEye + new Vector2(-0.12f, -0.08f), leftEye, line);
                    hit |= Segment(p, rightEye + new Vector2(0.12f, 0.08f), rightEye, line) || Segment(p, rightEye + new Vector2(0.12f, -0.08f), rightEye, line);
                    // Wavy mouth.
                    if (p.x > -0.4f && p.x < 0.4f && Mathf.Abs(p.y - (-0.35f + Mathf.Sin(p.x * 14f) * 0.06f)) < line) hit = true;
                    break;
                case Mood.Mad:
                    hit |= (p - leftEye - new Vector2(0f, -0.04f)).magnitude < 0.09f || (p - rightEye - new Vector2(0f, -0.04f)).magnitude < 0.09f;
                    hit |= Segment(p, new Vector2(-0.5f, 0.42f), new Vector2(-0.16f, 0.3f), line);
                    hit |= Segment(p, new Vector2(0.5f, 0.42f), new Vector2(0.16f, 0.3f), line);
                    hit |= Arc(p, new Vector2(0f, -0.62f), 0.36f, line, true);
                    break;
                default:
                    hit |= (p - leftEye).magnitude < 0.1f || (p - rightEye).magnitude < 0.1f;
                    if (mood == Mood.Happy) hit |= Arc(p, new Vector2(0f, -0.05f), 0.45f, line, false);
                    if (mood == Mood.Sad) hit |= Arc(p, new Vector2(0f, -0.65f), 0.36f, line, true);
                    break;
            }
            return hit ? 1f : 0f;
        }

        static bool Tear(Vector2 p) => (p - new Vector2(0.36f, -0.05f)).magnitude < 0.08f || (p.y > -0.05f && p.y < 0.08f && Mathf.Abs(p.x - 0.36f) < 0.08f * (0.08f - p.y) / 0.13f);

        static bool Tongue(Vector2 p) => (p - new Vector2(0.12f, -0.47f)).magnitude < 0.1f && p.y < -0.38f;

        static bool Segment(Vector2 p, Vector2 a, Vector2 b, float width)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude < width * 0.5f;
        }

        /// <summary>Lower half of a circle (a smile) or the upper half (a frown).</summary>
        static bool Arc(Vector2 p, Vector2 center, float radius, float width, bool upper)
        {
            var d = p - center;
            if (upper ? d.y < 0.05f : d.y > -0.05f) return false;
            if (Mathf.Abs(d.x) > radius * 0.85f) return false;
            return Mathf.Abs(d.magnitude - radius) < width * 0.5f;
        }

        static Texture2D MakeBubble()
        {
            var texture = NewTexture();
            var pixels = new Color[Size * Size];
            var white = new Color(1f, 1f, 1f, 0.95f);
            var edge = new Color(0.25f, 0.25f, 0.3f, 0.9f);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size * 2f - 1f, (y + 0.5f) / Size * 2f - 1f);
                var body = p - new Vector2(0f, 0.12f);
                float r = new Vector2(body.x, body.y * 1.15f).magnitude;
                var c = Color.clear;
                bool tail = p.y < -0.55f && p.y > -0.98f && Mathf.Abs(p.x + 0.15f + (p.y + 0.55f) * 0.3f) < (p.y + 0.98f) * 0.35f;
                if (r < 0.84f || tail) c = white;
                if (r >= 0.84f && r < 0.9f && !(p.y < -0.5f && Mathf.Abs(p.x + 0.15f) < 0.18f)) c = edge;
                pixels[y * Size + x] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
