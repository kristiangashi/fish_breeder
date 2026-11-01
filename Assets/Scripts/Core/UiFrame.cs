using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishBreeder.Core
{
    /// <summary>
    /// Fills a UI Toolkit element from code that reads like the old OnGUI menus: every frame the
    /// view calls <see cref="Begin"/>, then Label, Button, BeginRow and so on in order, then
    /// <see cref="End"/>. Elements made last frame are kept and only their text, classes and state
    /// change, so hover, scroll positions and half-finished clicks survive; anything no longer
    /// asked for is removed. A Button returns true once, on the frame after it was clicked.
    ///
    /// Only content changes here. How things look (sizes, colours, spacing) comes from the classes,
    /// which live in Resources/UI/FishBreeder.uss; each call takes them as a space-separated list.
    /// Every call returns its element, for the odd per-element style (a column's width, a bar's colour).
    /// </summary>
    public class UiFrame
    {
        /// <summary>What the frame knows about each element it made.</summary>
        class Tag
        {
            public string kind;
            public string classes = "";
            public bool clicked, changed;
            public object value;
        }

        readonly Stack<VisualElement> open = new Stack<VisualElement>();
        readonly Stack<int> next = new Stack<int>();

        /// <summary>The element the last call made or reused.</summary>
        public VisualElement Last { get; private set; }

        /// <summary>The tooltip of whatever the pointer is over, or "".</summary>
        public string Tooltip => UiPanels.HoverTooltip;

        public void Begin(VisualElement container)
        {
            open.Clear();
            next.Clear();
            Push(container);
        }

        public void End()
        {
            while (open.Count > 0) Pop();
        }

        void Push(VisualElement element)
        {
            open.Push(element);
            next.Push(0);
        }

        /// <summary>Closes the innermost BeginX, dropping whatever it held last frame that wasn't asked for this time.</summary>
        public void Pop()
        {
            var element = open.Pop();
            int used = next.Pop();
            var into = element is ScrollView scroll ? scroll.contentContainer : element;
            while (into.childCount > used) into.RemoveAt(into.childCount - 1);
        }

        VisualElement Into => open.Peek() is ScrollView scroll ? scroll.contentContainer : open.Peek();

        /// <summary>The next element in order: last frame's if it's the same kind, otherwise a new one.</summary>
        T Next<T>(string kind, string classes, Func<T> make) where T : VisualElement
        {
            var into = Into;
            int index = next.Pop();
            next.Push(index + 1);
            T element = null;
            if (index < into.childCount)
            {
                var old = into[index];
                if (old is T same && old.userData is Tag t && t.kind == kind) element = same;
                else while (into.childCount > index) into.RemoveAt(into.childCount - 1);
            }
            if (element == null)
            {
                element = make();
                element.userData = new Tag { kind = kind };
                into.Add(element);
            }
            SetClasses(element, classes);
            Last = element;
            return element;
        }

        static Tag TagOf(VisualElement e) => (Tag)e.userData;

        static void SetClasses(VisualElement e, string classes)
        {
            classes = classes ?? "";
            var tag = TagOf(e);
            if (tag.classes == classes) return;
            foreach (var c in tag.classes.Split(' ')) if (c.Length > 0) e.RemoveFromClassList(c);
            foreach (var c in classes.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
            tag.classes = classes;
        }

        // ------------------------------------------------------------ text

        public Label Label(string text, string classes = "fb-text")
        {
            var label = Next("label", classes, () => new Label());
            label.text = text ?? "";
            return label;
        }

        /// <summary>A sunny section heading.</summary>
        public Label Section(string text) => Label(text, "fb-section");

        /// <summary>Words on the left (wrapping) and a value kept on the right.</summary>
        public void Line(string text, string value, string tooltip = null)
        {
            var row = BeginRow("fb-line");
            row.tooltip = tooltip ?? "";
            Label(text, "fb-text fb-grow");
            Label(value, "fb-text fb-value");
            Pop();
        }

        /// <summary>A small coloured tag with bold text (sex, age, NEW).</summary>
        public Label Badge(string text, Color color, string classes = "fb-badge")
        {
            var label = Label(text, classes);
            label.style.backgroundColor = color;
            return label;
        }

        // ------------------------------------------------------------ buttons and inputs

        /// <summary>True on the frame after it was clicked. A disabled button never reports a click.</summary>
        public bool Button(string text, string classes = "fb-button", bool enabled = true, string tooltip = null)
        {
            var button = ButtonElement(text, classes, enabled, tooltip);
            var tag = TagOf(button);
            bool clicked = tag.clicked && enabled;
            tag.clicked = false;
            return clicked;
        }

        public Button ButtonElement(string text, string classes = "fb-button", bool enabled = true, string tooltip = null, string kind = "button")
        {
            var button = Next(kind, classes, () =>
            {
                var b = new Button();
                b.clicked += () => TagOf(b).clicked = true;
                return b;
            });
            button.text = text ?? "";
            if (button.enabledSelf != enabled) button.SetEnabled(enabled);
            button.tooltip = tooltip ?? "";
            return button;
        }

        /// <summary>
        /// A button that holds other elements (a card you click). Fill it, then <see cref="Pop"/>.
        /// Like <see cref="Button"/>, true on the frame after a click.
        /// </summary>
        public bool BeginButton(string classes, bool enabled = true, string tooltip = null)
        {
            var button = ButtonElement("", classes, enabled, tooltip, "buttonbox");
            Push(button);
            var tag = TagOf(button);
            bool clicked = tag.clicked && enabled;
            tag.clicked = false;
            return clicked;
        }

        /// <summary>A row of tab buttons; returns the picked one. The picked tab gets "fb-tab--on".</summary>
        public int Tabs(int selected, IList<string> names, string classes = "fb-tabs", string tabClasses = "fb-button fb-tab")
        {
            BeginRow(classes);
            int picked = selected;
            for (int i = 0; i < names.Count; i++)
                if (Button(names[i], i == selected ? tabClasses + " fb-tab--on" : tabClasses)) picked = i;
            Pop();
            return picked;
        }

        public bool Toggle(bool value, string text, string classes = "fb-toggle", string tooltip = null, bool enabled = true)
        {
            var toggle = Next("toggle", classes, () =>
            {
                var t = new Toggle();
                t.RegisterValueChangedCallback(e => { var tag = TagOf(t); tag.changed = true; tag.value = e.newValue; });
                return t;
            });
            toggle.text = text ?? "";
            toggle.tooltip = tooltip ?? "";
            if (toggle.enabledSelf != enabled) toggle.SetEnabled(enabled);
            var tg = TagOf(toggle);
            if (tg.changed)
            {
                tg.changed = false;
                return (bool)tg.value;
            }
            toggle.SetValueWithoutNotify(value);
            return value;
        }

        public float Slider(float value, float min, float max, string classes = "fb-slider", bool enabled = true)
        {
            var slider = Next("slider", classes, () =>
            {
                var s = new Slider();
                s.RegisterValueChangedCallback(e => { var tag = TagOf(s); tag.changed = true; tag.value = e.newValue; });
                return s;
            });
            slider.lowValue = min;
            slider.highValue = max;
            if (slider.enabledSelf != enabled) slider.SetEnabled(enabled);
            var tg = TagOf(slider);
            if (tg.changed)
            {
                tg.changed = false;
                return (float)tg.value;
            }
            slider.SetValueWithoutNotify(value);
            return value;
        }

        public string TextField(string value, int maxLength = 40, string classes = "fb-field")
        {
            var field = Next("field", classes, () =>
            {
                var f = new TextField();
                f.RegisterValueChangedCallback(e => { var tag = TagOf(f); tag.changed = true; tag.value = e.newValue; });
                return f;
            });
            field.maxLength = maxLength;
            var tg = TagOf(field);
            if (tg.changed)
            {
                tg.changed = false;
                return (string)tg.value;
            }
            if (field.focusController?.focusedElement != field) field.SetValueWithoutNotify(value ?? "");
            return field.value;
        }

        // ------------------------------------------------------------ containers

        public VisualElement BeginRow(string classes = "fb-row") => Begin("row", classes);
        public VisualElement BeginColumn(string classes = "fb-column") => Begin("column", classes);
        public VisualElement BeginCard(bool selected = false, string classes = "fb-card fb-card--column") =>
            Begin("card", selected ? classes + " fb-card--selected" : classes);

        /// <summary>Any box, closed with <see cref="Pop"/>.</summary>
        public VisualElement Begin(string kind, string classes)
        {
            var box = Next("box:" + kind, classes, () => new VisualElement());
            Push(box);
            return box;
        }

        /// <summary>A vertical scroll list, closed with <see cref="Pop"/>.</summary>
        public ScrollView BeginScroll(string classes = "fb-scroll")
        {
            var scroll = Next("scroll", classes, () => new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden });
            Push(scroll);
            return scroll;
        }

        public VisualElement Space(float size)
        {
            var space = Next("space", "", () => new VisualElement { pickingMode = PickingMode.Ignore });
            space.style.width = size;
            space.style.height = size;
            space.style.flexShrink = 0f;
            return space;
        }

        /// <summary>Takes the room left over in a row or column (GUILayout.FlexibleSpace).</summary>
        public VisualElement Flex() => Next("flex", "fb-spacer", () => new VisualElement { pickingMode = PickingMode.Ignore });

        // ------------------------------------------------------------ bars and pictures

        /// <summary>A rounded bar filled to <paramref name="fill"/> (0..1) in <paramref name="color"/>.</summary>
        public VisualElement Bar(float fill, Color color, string classes = "fb-bar", string tooltip = null)
        {
            var bar = Next("bar", classes, () =>
            {
                var b = new VisualElement();
                b.Add(new VisualElement { pickingMode = PickingMode.Ignore });
                b[0].AddToClassList("fb-bar__fill");
                return b;
            });
            bar.tooltip = tooltip ?? "";
            var inside = bar[0];
            inside.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f);
            inside.style.backgroundColor = color;
            UiPanels.Show(inside, fill > 0f);
            return bar;
        }

        public Image Image(Texture texture, float width, float height, string classes = "fb-image", string tooltip = null)
        {
            var image = Next("image", classes, () => new Image { scaleMode = ScaleMode.ScaleToFit });
            image.image = texture;
            image.style.width = width;
            image.style.height = height;
            image.tooltip = tooltip ?? "";
            return image;
        }

        /// <summary>A button showing a picture (and optional text under it).</summary>
        public bool ImageButton(Texture texture, float size, string text = null, string classes = "fb-button fb-image-button", bool enabled = true, string tooltip = null)
        {
            var button = ButtonElement("", classes, enabled, tooltip, "imagebutton");
            if (button.childCount == 0 || !(button[0] is Image))
            {
                button.Clear();
                button.Add(new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore });
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("fb-image-button__text");
                button.Add(label);
            }
            var image = (Image)button[0];
            image.image = texture;
            image.style.width = size;
            image.style.height = size;
            var caption = (Label)button[1];
            caption.text = text ?? "";
            UiPanels.Show(caption, !string.IsNullOrEmpty(text));
            var tag = TagOf(button);
            bool clicked = tag.clicked && enabled;
            tag.clicked = false;
            return clicked;
        }

        public const float TempMin = 10f, TempMax = 34f;
        public const float PhMin = 5f, PhMax = 9.5f;

        static Texture2D heatScale, phScale;

        static Texture2D HeatScale => heatScale != null ? heatScale : heatScale = Stripe("HeatScale",
            new Color(0.2f, 0.35f, 0.95f), new Color(0.2f, 0.75f, 0.95f), new Color(0.35f, 0.85f, 0.4f), new Color(0.98f, 0.7f, 0.2f), new Color(0.9f, 0.2f, 0.15f));

        static Texture2D PhScale => phScale != null ? phScale : phScale = Stripe("PhScale",
            new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.8f, 0.25f), new Color(0.35f, 0.8f, 0.35f), new Color(0.2f, 0.65f, 0.75f), new Color(0.5f, 0.3f, 0.8f));

        /// <summary>Temperature scale with the water at <paramref name="celsius"/> and, when known, the range the fish likes.</summary>
        public VisualElement Temperature(float celsius, Vector2? liked) =>
            Gauge(HeatScale, TempMin, TempMax, celsius, liked, $"{celsius:0.#} C", "cold", "hot");

        /// <summary>pH scale: acidic (low) on the left, alkaline (high) on the right.</summary>
        public VisualElement Ph(float ph, Vector2? liked) =>
            Gauge(PhScale, PhMin, PhMax, ph, liked, $"pH {ph:0.0}", "acidic", "alkaline");

        /// <summary>A coloured scale with the liked range outlined and a marker where the value is now.</summary>
        VisualElement Gauge(Texture2D scale, float min, float max, float value, Vector2? liked, string valueText, string lowWord, string highWord)
        {
            var gauge = Next("gauge", "fb-gauge", () =>
            {
                var g = new VisualElement();
                var readout = new Label { name = "readout" };
                readout.AddToClassList("fb-gauge__readout");
                var track = new VisualElement { name = "track" };
                track.AddToClassList("fb-gauge__track");
                var band = new VisualElement { name = "band" };
                band.AddToClassList("fb-gauge__band");
                var marker = new VisualElement { name = "marker" };
                marker.AddToClassList("fb-gauge__marker");
                track.Add(band);
                track.Add(marker);
                var ends = new VisualElement();
                ends.AddToClassList("fb-gauge__ends");
                var low = new Label { name = "low" };
                var high = new Label { name = "high" };
                low.AddToClassList("fb-gauge__end");
                high.AddToClassList("fb-gauge__end");
                ends.Add(low);
                ends.Add(new VisualElement { style = { flexGrow = 1f } });
                ends.Add(high);
                g.Add(readout);
                g.Add(track);
                g.Add(ends);
                return g;
            });
            gauge.Q("track").style.backgroundImage = scale;
            float t = Mathf.InverseLerp(min, max, value);
            gauge.Q("marker").style.left = Length.Percent(t * 100f);
            var bandView = gauge.Q("band");
            UiPanels.Show(bandView, liked.HasValue);
            if (liked.HasValue)
            {
                float a = Mathf.InverseLerp(min, max, liked.Value.x), b = Mathf.InverseLerp(min, max, liked.Value.y);
                bandView.style.left = Length.Percent(a * 100f);
                bandView.style.width = Length.Percent(Mathf.Max(0.01f, b - a) * 100f);
            }
            bool inside = liked.HasValue && value >= liked.Value.x && value <= liked.Value.y;
            string color = !liked.HasValue ? "#ffffff" : inside ? "#9fe39f" : "#ff9a8a";
            var readoutView = gauge.Q<Label>("readout");
            readoutView.text = $"<color={color}><b>▼ {valueText}</b></color>";
            // Over the marker, but kept inside the gauge near its ends.
            readoutView.style.left = t < 0.25f ? 0f : t > 0.75f ? StyleKeyword.Auto : Length.Percent(t * 100f);
            readoutView.style.right = t > 0.75f ? 0f : StyleKeyword.Auto;
            readoutView.style.translate = t >= 0.25f && t <= 0.75f ? new Translate(Length.Percent(-50f), 0f) : new Translate(0f, 0f);
            gauge.Q<Label>("low").text = lowWord;
            gauge.Q<Label>("high").text = highWord;
            return gauge;
        }

        static Texture2D Stripe(string name, params Color[] stops)
        {
            const int width = 128;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < width; i++)
            {
                float t = i / (width - 1f) * (stops.Length - 1);
                int a = Mathf.Min(stops.Length - 2, Mathf.FloorToInt(t));
                texture.SetPixel(i, 0, Color.Lerp(stops[a], stops[a + 1], t - a));
            }
            texture.Apply();
            return texture;
        }
    }
}
