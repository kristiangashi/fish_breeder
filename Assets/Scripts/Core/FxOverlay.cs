using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// Draws the screen side of <see cref="Fx"/>: floating words, confetti and bubble bursts,
    /// celebration cards (milestones, new species), the OPEN! stamp, the day-change wash and the
    /// "Game saved" note after each save. Sits
    /// above the other screens. In watch mode only the day wash shows; cards wait until you're back.
    /// UI Toolkit, on its own panel (FxOverlay.uxml, styled in FishBreeder.uss); positions are in
    /// the panel's units (the screen is 900 tall), the same units <see cref="Fx.ScreenBurst"/> takes.
    /// </summary>
    public class FxOverlay : MonoBehaviour
    {
        const float CardIn = 0.45f, CardHold = 3.6f, CardOut = 0.4f;
        const float DayLength = 2.4f;

        static FxOverlay instance;

        UIDocument document;
        VisualElement root, dayView, particlesView, floatersView, card, cardStripe, sun, sunGlow, horizon;
        Label cardKicker, cardTitle, cardText, cardReward, cardStar, stamp, stampShadow, dayTitle, daySubtitle, saved;
        Image cardImage;
        Texture2D dayTexture;
        readonly UiFrame particles = new UiFrame(), floaters = new UiFrame(), bubbles = new UiFrame();

        /// <summary>The mouse is over the celebration card (a click there is the card's, not the store's).</summary>
        public static bool MouseOverCard => instance != null && instance.card != null && UiPanels.Showing(instance.card)
            && instance.card.worldBound.Contains(UiPanels.ScreenToPanel(instance.root, Input.mousePosition));

        void Awake()
        {
            instance = this;
            document = UiPanels.Open("Effects", "FxOverlay", transform, 50);
            root = document.rootVisualElement;
            dayView = root.Q("day");
            sunGlow = root.Q("sun-glow");
            sun = root.Q("sun");
            horizon = root.Q("horizon");
            dayTitle = root.Q<Label>("day-title");
            daySubtitle = root.Q<Label>("day-subtitle");
            particlesView = root.Q("particles");
            floatersView = root.Q("floaters");
            card = root.Q("card");
            cardStripe = root.Q("card-stripe");
            cardImage = root.Q<Image>("card-image");
            cardStar = root.Q<Label>("card-star");
            cardKicker = root.Q<Label>("card-kicker");
            cardTitle = root.Q<Label>("card-title");
            cardText = root.Q<Label>("card-text");
            cardReward = root.Q<Label>("card-reward");
            stamp = root.Q<Label>("stamp");
            stampShadow = root.Q<Label>("stamp-shadow");
            saved = root.Q<Label>("saved");
            sun.style.backgroundImage = UiTheme.Circle;
            sunGlow.style.backgroundImage = UiTheme.Circle;
            dayTexture = UiPanels.Gradient(new Color(0.05f, 0.38f, 0.52f, 0.92f), new Color(0.01f, 0.1f, 0.2f, 0.96f));
            dayView.style.backgroundImage = dayTexture;
            // A click on the card closes it (and for a new fish opens the journal on it).
            card.RegisterCallback<PointerDownEvent>(_ =>
            {
                var current = Fx.Current;
                if (current == null) return;
                Fx.Current = null;
                if (current.species != null) JournalView.Open(current.species.id);
            });
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (document != null) Destroy(document.gameObject);
            if (dayTexture != null) Destroy(dayTexture);
        }

        void OnDisable() => UiPanels.Show(root, false);

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
            var finished = Fx.Current;
            Fx.Current = null;
            if (finished.species == null) return;
            foreach (var next in Fx.Queue)
                if (next.species != null) return;
            JournalView.Open(finished.species.id);
        }

        Vector2 Size => root.panel != null ? root.panel.visualTree.layout.size : new Vector2(1600f, 900f);

        void LateUpdate()
        {
            UiPanels.Show(root, true);
            bool watching = ViewMode.Watching;
            if (watching) Fx.Bursts.Clear();
            else ConvertBursts();
            particles.Begin(particlesView);
            if (!watching) DrawParticles();
            particles.End();
            floaters.Begin(floatersView);
            if (!watching) DrawFloaters();
            floaters.End();
            DrawCard(!watching);
            DrawStamp(!watching);
            DrawDay();
            DrawSaved();
        }

        /// <summary>"Game saved" in the bottom right corner for a moment after every save, fading out.</summary>
        void DrawSaved()
        {
            float age = SaveGame.LastSaved < 0f ? 99f : Time.unscaledTime - SaveGame.LastSaved;
            UiPanels.Show(saved, age <= 2.5f);
            saved.style.opacity = Mathf.Clamp01((2.5f - age) / 0.6f);
        }

        void ConvertBursts()
        {
            var cam = Camera.main;
            foreach (var burst in Fx.Bursts)
            {
                if (cam == null) break;
                var screen = cam.WorldToScreenPoint(burst.world);
                if (screen.z <= 0f) continue;
                Fx.ScreenBurst(UiPanels.ScreenToPanel(root, screen), burst.color, burst.count, burst.bubbles, 0.7f);
            }
            Fx.Bursts.Clear();
        }

        void DrawFloaters()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var floater in Fx.Floaters)
            {
                var screen = cam.WorldToScreenPoint(floater.world);
                float t = floater.age / floater.life;
                float pop = 1f + 0.7f * Mathf.Clamp01(1f - floater.age * 6f);
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                var at = UiPanels.ScreenToPanel(root, screen);
                var label = floaters.Label(floater.text, "fb-floater");
                UiPanels.Show(label, screen.z > 0f);
                label.style.left = at.x;
                label.style.top = at.y - floater.age * 34f;
                label.style.fontSize = Mathf.RoundToInt(floater.size * pop);
                label.style.color = new Color(floater.color.r, floater.color.g, floater.color.b, 1f);
                label.style.opacity = alpha;
            }
        }

        void DrawParticles()
        {
            foreach (var p in Fx.Particles)
            {
                float t = p.age / p.life;
                var color = p.color;
                color.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                float s = p.size * (p.bubble ? Mathf.Lerp(0.6f, 1.1f, t) : 1f);
                var dot = particles.Begin("dot", "fb-particle");
                particles.Pop();
                dot.style.backgroundImage = p.bubble ? UiTheme.Bubble : UiTheme.Circle;
                dot.style.unityBackgroundImageTintColor = color;
                dot.style.left = p.pos.x - s * 0.5f;
                dot.style.top = p.pos.y - s * 0.5f;
                dot.style.width = s;
                dot.style.height = s;
            }
        }

        void DrawCard(bool show)
        {
            var current = Fx.Current;
            UiPanels.Show(card, show && current != null);
            if (!show || current == null) return;
            cardKicker.text = current.kicker;
            cardKicker.style.color = current.accent;
            cardStripe.style.backgroundColor = current.accent;
            cardTitle.text = current.title;
            cardText.text = current.subtitle;
            cardReward.text = $"<b><color={UiTheme.LeafHex}>{current.reward}</color></b>";
            var icon = current.species != null ? ItemPreviews.Fish(current.species) : null;
            cardImage.image = icon;
            UiPanels.Show(cardImage, icon != null);
            UiPanels.Show(cardStar, icon == null);
            if (icon != null) cardImage.style.translate = new Translate(0f, Mathf.Sin(Time.unscaledTime * 3f) * 3f);
            else cardStar.style.rotate = new Rotate(Mathf.Sin(Time.unscaledTime * 2.5f) * 8f);

            // Drops in from the top, waits, and goes back up. The card grows to fit its text.
            float h = card.resolvedStyle.height;
            if (float.IsNaN(h) || h < 118f) h = 118f;
            float t = current.age, y;
            if (t < CardIn) y = Mathf.LerpUnclamped(-h - 20f, 96f, EaseOutBack(t / CardIn));
            else if (t < CardIn + CardHold) y = 96f;
            else y = Mathf.Lerp(96f, -h - 20f, (t - CardIn - CardHold) / CardOut);
            card.style.top = y;
            // Sliding in or out it's meant to be partly off the top: the UI check only looks once it's still.
            card.EnableInClassList(UiToolkitCheck.SkipClass, t < CardIn || t > CardIn + CardHold);

            if (!current.burst && t > CardIn * 0.6f)
            {
                current.burst = true;
                var center = new Vector2(Size.x * 0.5f, 96f + h * 0.5f);
                Fx.ScreenBurst(center, null, 40, false, 1.4f);
                Fx.ScreenBurst(center, new Color(0.85f, 0.97f, 1f), 14, true);
            }
        }

        void DrawStamp(bool show)
        {
            bool on = show && Fx.StampAge >= 0f;
            UiPanels.Show(stamp, on);
            UiPanels.Show(stampShadow, on);
            if (!on) return;
            float t = Fx.StampAge;
            float size = t < 0.22f ? Mathf.Lerp(3f, 1f, t / 0.22f) : 1f + Mathf.Sin(Mathf.Clamp01((t - 0.22f) / 0.2f) * Mathf.PI) * 0.08f;
            float alpha = t < 1.3f ? Mathf.Clamp01(t * 8f) : 1f - (t - 1.3f) / 0.5f;
            foreach (var label in new[] { stampShadow, stamp })
            {
                label.text = Fx.StampText;
                label.style.scale = new Scale(new Vector3(size, size, 1f));
            }
            stamp.style.color = new Color(Fx.StampColor.r, Fx.StampColor.g, Fx.StampColor.b, alpha);
            stampShadow.style.color = new Color(0f, 0f, 0f, alpha * 0.35f);
            if (Fx.StampFresh)
            {
                Fx.StampFresh = false;
                Fx.ScreenBurst(new Vector2(Size.x * 0.5f, Size.y * 0.42f), null, 36, false, 1.6f);
            }
        }

        /// <summary>The new day washes over the screen: the sun comes up over the horizon, bubbles rise, and the day's name shows.</summary>
        void DrawDay()
        {
            bool on = Fx.DayAge >= 0f;
            UiPanels.Show(dayView, on);
            bubbles.Begin(dayView.Q("day-bubbles"));
            if (on)
            {
                var size = Size;
                float t = Fx.DayAge;
                float alpha = t < 0.35f ? t / 0.35f : t > DayLength - 0.6f ? (DayLength - t) / 0.6f : 1f;
                dayView.style.opacity = Mathf.Clamp01(alpha);
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.4f));
                const float sunSize = 120f;
                float sunTop = size.y * 0.62f - rise * 140f;
                sun.style.left = size.x * 0.5f - sunSize * 0.5f;
                sun.style.top = sunTop;
                sunGlow.style.left = size.x * 0.5f - sunSize * 0.5f - 40f;
                sunGlow.style.top = sunTop - 40f;
                horizon.style.top = size.y * 0.62f + sunSize * 0.5f;
                for (int i = 0; i < 18; i++)
                {
                    float bx = (i * 97.3f) % size.x;
                    float by = size.y - ((t * (60f + i * 7f) + i * 53f) % (size.y + 40f));
                    float bs = 8f + (i * 13 % 7) * 3f;
                    var bubble = bubbles.Begin("bubble", "fb-day-bubble");
                    bubbles.Pop();
                    bubble.style.backgroundImage = UiTheme.Bubble;
                    bubble.style.left = bx;
                    bubble.style.top = by;
                    bubble.style.width = bs;
                    bubble.style.height = bs;
                }
                dayTitle.text = Fx.DayTitle;
                daySubtitle.text = Fx.DaySubtitle;
            }
            bubbles.End();
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
