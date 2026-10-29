using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Draws the screen side of <see cref="Fx"/>: floating words, confetti and bubble bursts,
    /// celebration cards (milestones, new species), the OPEN! stamp and the day-change wash. Sits
    /// above the other screens. In watch mode only the day wash shows; cards wait until you're back.
    /// </summary>
    public class FxOverlay : MonoBehaviour
    {
        const float CardIn = 0.45f, CardHold = 3.6f, CardOut = 0.4f;
        const float DayLength = 2.4f;

        GUIStyle floatStyle, cardTitle, cardKicker, cardText, stampStyle, dayTitle, daySub;
        Rect cardRect;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = Fx.Floaters.Count - 1; i >= 0; i--)
            {
                var floater = Fx.Floaters[i];
                floater.age += dt;
                if (floater.age >= floater.life) Fx.Floaters.RemoveAt(i);
            }
            for (int i = Fx.Particles.Count - 1; i >= 0; i--)
            {
                var p = Fx.Particles[i];
                p.age += dt;
                if (p.bubble) p.vel = new Vector2(p.vel.x * (1f - 2f * dt) + Mathf.Sin((p.age + i) * 6f) * 20f * dt, Mathf.Lerp(p.vel.y, -90f, 2f * dt));
                else p.vel += new Vector2(0f, 420f * dt);
                p.pos += p.vel * dt;
                if (p.age >= p.life) Fx.Particles.RemoveAt(i);
            }
            if (Fx.StampAge >= 0f)
            {
                Fx.StampAge += dt;
                if (Fx.StampAge > 1.8f) Fx.StampAge = -1f;
            }
            if (Fx.DayAge >= 0f)
            {
                Fx.DayAge += dt;
                if (Fx.DayAge > DayLength) Fx.DayAge = -1f;
            }

            if (Fx.Current == null && Fx.Queue.Count > 0 && !Fx.Hold && !ViewMode.Watching && Fx.DayAge < 0f)
                Fx.Current = Fx.Queue.Dequeue();
            if (Fx.Current != null && !ViewMode.Watching)
            {
                Fx.Current.age += dt;
                if (Fx.Current.age >= CardIn + CardHold + CardOut) FinishCard();
            }
        }

        /// <summary>A card is done: for a new species, open the journal on it (after the last of a run of them).</summary>
        void FinishCard()
        {
            var card = Fx.Current;
            Fx.Current = null;
            if (card.species == null) return;
            foreach (var next in Fx.Queue)
                if (next.species != null) return;
            JournalView.Open(card.species.id);
        }

        void OnGUI()
        {
            UiTheme.Apply();
            GUI.depth = -20;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            EnsureStyles();

            if (!ViewMode.Watching)
            {
                var cam = Camera.main;
                ConvertBursts(cam, scale, height);
                if (Event.current.type == EventType.Repaint)
                {
                    DrawFloaters(cam, scale, height);
                    DrawParticles();
                }
                DrawCard(width);
                if (Event.current.type == EventType.Repaint) DrawStamp(width, height);
            }
            else Fx.Bursts.Clear();
            if (Event.current.type == EventType.Repaint) DrawDay(width, height);
        }

        void ConvertBursts(Camera cam, float scale, float height)
        {
            if (Fx.Bursts.Count == 0) return;
            foreach (var burst in Fx.Bursts)
            {
                if (cam == null) break;
                var screen = cam.WorldToScreenPoint(burst.world);
                if (screen.z <= 0f) continue;
                Fx.ScreenBurst(new Vector2(screen.x / scale, height - screen.y / scale), burst.color, burst.count, burst.bubbles, 0.7f);
            }
            Fx.Bursts.Clear();
        }

        void DrawFloaters(Camera cam, float scale, float height)
        {
            if (cam == null) return;
            var old = GUI.color;
            foreach (var floater in Fx.Floaters)
            {
                var screen = cam.WorldToScreenPoint(floater.world);
                if (screen.z <= 0f) continue;
                float t = floater.age / floater.life;
                float pop = 1f + 0.7f * Mathf.Clamp01(1f - floater.age * 6f);
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                var gui = new Vector2(screen.x / scale, height - screen.y / scale - floater.age * 34f);
                floatStyle.fontSize = Mathf.RoundToInt(floater.size * pop);
                var content = new GUIContent(floater.text);
                var size = floatStyle.CalcSize(content);
                var rect = new Rect(gui.x - size.x * 0.5f, gui.y - size.y * 0.5f, size.x, size.y);
                GUI.color = new Color(0f, 0.08f, 0.12f, alpha * 0.7f);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), content, floatStyle);
                GUI.color = new Color(floater.color.r, floater.color.g, floater.color.b, alpha);
                GUI.Label(rect, content, floatStyle);
            }
            GUI.color = old;
        }

        void DrawParticles()
        {
            var old = GUI.color;
            foreach (var p in Fx.Particles)
            {
                float t = p.age / p.life;
                var color = p.color;
                color.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                GUI.color = color;
                float s = p.size * (p.bubble ? Mathf.Lerp(0.6f, 1.1f, t) : 1f);
                GUI.DrawTexture(new Rect(p.pos.x - s * 0.5f, p.pos.y - s * 0.5f, s, s), p.bubble ? UiTheme.Bubble : UiTheme.Circle);
            }
            GUI.color = old;
        }

        void DrawCard(float width)
        {
            var card = Fx.Current;
            if (card == null) return;
            // The card grows to fit its text (research cards say a lot).
            const float w = 520f;
            float textW = w - 14f - 112f - 14f - 14f;
            var title = new GUIContent(card.title);
            var subtitle = new GUIContent(card.subtitle);
            var reward = new GUIContent($"<b><color={UiTheme.LeafHex}>{card.reward}</color></b>");
            float titleH = Mathf.Max(30f, cardTitle.CalcHeight(title, textW));
            float subtitleH = cardText.CalcHeight(subtitle, textW);
            float rewardH = cardText.CalcHeight(reward, textW);
            float h = Mathf.Max(118f, 12f + 16f + titleH + subtitleH + 4f + rewardH + 14f);
            float t = card.age;
            float y;
            if (t < CardIn) y = Mathf.LerpUnclamped(-h - 20f, 96f, EaseOutBack(t / CardIn));
            else if (t < CardIn + CardHold) y = 96f;
            else y = Mathf.Lerp(96f, -h - 20f, (t - CardIn - CardHold) / CardOut);
            cardRect = new Rect((width - w) * 0.5f, y, w, h);

            if (!card.burst && t > CardIn * 0.6f)
            {
                card.burst = true;
                Fx.ScreenBurst(cardRect.center, null, 40, false, 1.4f);
                Fx.ScreenBurst(cardRect.center, new Color(0.85f, 0.97f, 1f), 14, true);
            }

            GUI.Box(cardRect, GUIContent.none, UiStyles.Window);
            // A soft glow stripe in the card's accent colour along the top.
            GUI.DrawTexture(new Rect(cardRect.x + 12f, cardRect.y + 4f, cardRect.width - 24f, 3f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, card.accent, 0f, 2f);

            var iconRect = new Rect(cardRect.x + 14f, cardRect.y + 14f, 112f, h - 28f);
            GUI.DrawTexture(iconRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.05f, 0.42f, 0.5f, 0.9f), 0f, 12f);
            var icon = card.species != null ? ItemPreviews.Fish(card.species) : null;
            if (icon != null)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 3f) * 3f;
                GUI.DrawTexture(new Rect(iconRect.x + 4f, iconRect.y + 4f + bob, iconRect.width - 8f, iconRect.height - 8f), icon, ScaleMode.ScaleToFit);
            }
            else
            {
                float spin = Mathf.Sin(Time.unscaledTime * 2.5f) * 8f;
                var pivot = iconRect.center;
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(spin, pivot * Mathf.Max(1f, Screen.height / 900f));
                GUI.Label(iconRect, $"<size=46><color={UiTheme.SunHex}>★</color></size>", cardTitle);
                GUI.matrix = matrix;
            }

            float x = iconRect.xMax + 14f;
            cardKicker.normal.textColor = card.accent;
            float ty = cardRect.y + 12f;
            UiCheck.Label("Celebration card: kicker", new Rect(x, ty, textW, 18f), new GUIContent(card.kicker), cardKicker);
            ty += 16f;
            UiCheck.Label("Celebration card: title", new Rect(x, ty, textW, titleH), title, cardTitle);
            ty += titleH;
            UiCheck.Label("Celebration card: text", new Rect(x, ty, textW, subtitleH), subtitle, cardText);
            ty += subtitleH + 4f;
            UiCheck.Label("Celebration card: reward", new Rect(x, ty, textW, rewardH), reward, cardText);

            var e = Event.current;
            if (e.type == EventType.MouseDown && cardRect.Contains(e.mousePosition) && !UiLayout.Editing)
            {
                e.Use();
                Fx.Current = null;
                if (card.species != null) JournalView.Open(card.species.id);
            }
        }

        void DrawStamp(float width, float height)
        {
            if (Fx.StampAge < 0f) return;
            float t = Fx.StampAge;
            float size = t < 0.22f ? Mathf.Lerp(3f, 1f, t / 0.22f) : 1f + Mathf.Sin(Mathf.Clamp01((t - 0.22f) / 0.2f) * Mathf.PI) * 0.08f;
            float alpha = t < 1.3f ? Mathf.Clamp01(t * 8f) : 1f - (t - 1.3f) / 0.5f;
            var center = new Vector2(width * 0.5f, height * 0.42f);
            var matrix = GUI.matrix;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUIUtility.ScaleAroundPivot(new Vector2(size, size), center * scale);
            GUIUtility.RotateAroundPivot(-8f, center * scale);
            var rect = new Rect(center.x - 220f, center.y - 60f, 440f, 120f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.35f);
            GUI.Label(new Rect(rect.x + 5f, rect.y + 5f, rect.width, rect.height), Fx.StampText, stampStyle);
            GUI.color = new Color(Fx.StampColor.r, Fx.StampColor.g, Fx.StampColor.b, alpha);
            GUI.Label(rect, Fx.StampText, stampStyle);
            GUI.color = old;
            GUI.matrix = matrix;
            if (Fx.StampFresh)
            {
                Fx.StampFresh = false;
                Fx.ScreenBurst(center, null, 36, false, 1.6f);
            }
        }

        void DrawDay(float width, float height)
        {
            if (Fx.DayAge < 0f) return;
            float t = Fx.DayAge;
            float alpha = t < 0.35f ? t / 0.35f : t > DayLength - 0.6f ? (DayLength - t) / 0.6f : 1f;
            alpha = Mathf.Clamp01(alpha);
            var old = GUI.color;
            UiTheme.Gradient(new Rect(0f, 0f, width, height), new Color(0.05f, 0.38f, 0.52f, 0.92f * alpha), new Color(0.01f, 0.1f, 0.2f, 0.96f * alpha));

            // The sun comes up over the horizon while the day changes.
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.4f));
            float sunSize = 120f;
            var sun = new Rect(width * 0.5f - sunSize * 0.5f, height * 0.62f - rise * 140f, sunSize, sunSize);
            GUI.color = new Color(1f, 0.85f, 0.4f, alpha * 0.35f);
            GUI.DrawTexture(new Rect(sun.x - 40f, sun.y - 40f, sun.width + 80f, sun.height + 80f), UiTheme.Circle);
            GUI.color = new Color(1f, 0.82f, 0.38f, alpha);
            GUI.DrawTexture(sun, UiTheme.Circle);
            GUI.color = new Color(0.02f, 0.2f, 0.3f, alpha);
            GUI.DrawTexture(new Rect(0f, height * 0.62f + sunSize * 0.5f, width, height), Texture2D.whiteTexture);

            // Rising bubbles across the screen.
            for (int i = 0; i < 18; i++)
            {
                float bx = (i * 97.3f) % width;
                float by = height - ((t * (60f + i * 7f) + i * 53f) % (height + 40f));
                float bs = 8f + (i * 13 % 7) * 3f;
                GUI.color = new Color(0.85f, 0.97f, 1f, alpha * 0.8f);
                GUI.DrawTexture(new Rect(bx, by, bs, bs), UiTheme.Bubble);
            }

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, height * 0.3f, width, 70f), Fx.DayTitle, dayTitle);
            GUI.Label(new Rect(0f, height * 0.3f + 70f, width, 30f), Fx.DaySubtitle, daySub);
            GUI.color = old;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        void EnsureStyles()
        {
            if (floatStyle != null) return;
            var skin = UiTheme.Skin;
            floatStyle = new GUIStyle(skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, richText = true };
            floatStyle.normal.textColor = Color.white;
            cardKicker = new GUIStyle(skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, wordWrap = false };
            cardTitle = new GUIStyle(skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true, richText = true, alignment = TextAnchor.MiddleLeft };
            cardTitle.normal.textColor = UiTheme.Sun;
            cardText = new GUIStyle(skin.label) { fontSize = 13, wordWrap = true, richText = true };
            stampStyle = new GUIStyle(skin.label) { fontSize = 84, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            stampStyle.normal.textColor = Color.white;
            dayTitle = new GUIStyle(skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            dayTitle.normal.textColor = UiTheme.Sand;
            daySub = new GUIStyle(skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            daySub.normal.textColor = UiTheme.Aqua;
        }
    }
}
