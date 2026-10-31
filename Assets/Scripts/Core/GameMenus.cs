using System.Collections.Generic;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishBreeder.Core
{
    /// <summary>
    /// Which game the scene builds when it loads. The main menu and the pause menu pick a save (or
    /// a new one) and reload the scene; <see cref="GameBootstrap"/> then builds that game, or shows
    /// the main menu when nothing was picked. Batch-mode runs (automated tests) skip the menu and
    /// carry on with the latest save, as before menus existed.
    /// </summary>
    public static class GameSession
    {
        static int startSlot = -1;
        static bool startNew;

        /// <summary>The game to build now, if one was picked. False means: show the main menu.</summary>
        public static bool TakeStart(out int slot, out bool isNew)
        {
            slot = startSlot;
            isNew = startNew;
            startSlot = -1;
            if (slot > 0) return true;
            if (!Application.isBatchMode) return false;
            var latest = SaveGame.Latest();
            isNew = latest == null;
            slot = latest != null ? latest.slot : SaveGame.FreeSlot();
            return true;
        }

        /// <summary>Plays a save, or starts a new game in a free slot.</summary>
        public static void Play(int slot, bool isNew)
        {
            startSlot = slot;
            startNew = isNew;
            Reload();
        }

        public static void NewGame() => Play(SaveGame.FreeSlot(), true);

        public static void ToMainMenu()
        {
            startSlot = -1;
            Reload();
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Reload()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else SceneManager.LoadScene(scene.name);
        }
    }

    /// <summary>The pieces both menus share: the list of saves and the Settings window.</summary>
    public static class MenuScreens
    {
        static int confirmDelete = -1;
        static Vector2 scroll;
        static List<SaveSlot> slots;

        /// <summary>Re-reads the save folder the next time the list is drawn.</summary>
        public static void RefreshSaves()
        {
            slots = null;
            confirmDelete = -1;
        }

        public static string Describe(SaveSlot slot)
        {
            if (!slot.readable) return $"<b>Save {slot.slot}</b>   <color=#ffb0a0>can't be read</color>";
            string levels = slot.levels.Length >= 2 ? $"   Fishkeeping {slot.levels[0]}, Shopkeeping {slot.levels[1]}" : "";
            return $"<b>Save {slot.slot}</b>   Day {slot.day}   {Economy.Format(slot.money)}{levels}\n<size=11><color=#9fb4bc>Saved {slot.written:d MMM yyyy, HH:mm}</color></size>";
        }

        /// <summary>
        /// Every save with Load and Delete buttons, newest first. Returns the slot picked to load, or 0.
        /// Delete asks once more before it removes anything. <paramref name="playing"/> is the slot
        /// being played, which can't be deleted.
        /// </summary>
        public static int DrawSaves(float height, int playing = 0)
        {
            if (slots == null) slots = SaveGame.Slots();
            if (slots.Count == 0)
            {
                Ui.Label("<i>No saves yet.</i>", Rich());
                return 0;
            }
            int picked = 0;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            foreach (var slot in slots)
            {
                GUILayout.BeginHorizontal(slot.slot == playing ? UiTheme.CardSelected : UiTheme.Card);
                Ui.NamedLabel("Save line", Describe(slot) + (slot.slot == playing ? $"  <color={UiTheme.SunHex}>(playing)</color>" : ""), Rich());
                GUILayout.FlexibleSpace();
                GUI.enabled = slot.readable;
                if (Ui.Button("Load", UiTheme.Primary, GUILayout.Width(70f), GUILayout.Height(34f))) picked = slot.slot;
                GUI.enabled = slot.slot != playing;
                if (confirmDelete == slot.slot)
                {
                    if (Ui.Button("Delete for good", GUILayout.Width(118f), GUILayout.Height(34f)))
                    {
                        SaveGame.Delete(slot.slot);
                        RefreshSaves();
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                else if (Ui.Button("Delete", GUILayout.Width(70f), GUILayout.Height(34f))) confirmDelete = slot.slot;
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            return picked;
        }

        /// <summary>Screen, graphics and autosave settings; each change applies and is kept right away.</summary>
        public static void DrawSettings()
        {
            Ui.Label("Display", UiTheme.Heading);
            if (Application.isEditor) Ui.Label("<size=11><i>Screen mode and size only change in a built game; the editor's Game view sizes itself.</i></size>", Rich());
            int mode = Choice("Screen mode", GameSettings.Fullscreen ? 1 : 0, new[] { "Windowed", "Fullscreen" });
            if (mode != (GameSettings.Fullscreen ? 1 : 0)) GameSettings.Fullscreen = mode == 1;

            var sizes = GameSettings.Resolutions();
            int current = Mathf.Max(0, sizes.IndexOf(GameSettings.Resolution));
            var names = new string[sizes.Count];
            for (int i = 0; i < sizes.Count; i++) names[i] = $"{sizes[i].x} × {sizes[i].y}";
            int size = Choice("Resolution", current, names);
            if (size != current) GameSettings.Resolution = sizes[size];

            GUILayout.Space(8f);
            Ui.Label("Graphics", UiTheme.Heading);
            int quality = Choice("Quality", GameSettings.Quality, QualitySettings.names);
            if (quality != GameSettings.Quality) GameSettings.Quality = quality;
            int vsync = Choice("VSync", GameSettings.VSync ? 1 : 0, new[] { "Off", "On" });
            if (vsync != (GameSettings.VSync ? 1 : 0)) GameSettings.VSync = vsync == 1;
            GUI.enabled = !GameSettings.VSync;
            var caps = GameSettings.FrameCapChoices;
            var capNames = new string[caps.Length];
            for (int i = 0; i < caps.Length; i++) capNames[i] = caps[i] > 0 ? $"{caps[i]} fps" : "No limit";
            int cap = Mathf.Max(0, System.Array.IndexOf(caps, GameSettings.FrameCap));
            int newCap = Choice("Frame rate limit", cap, capNames);
            if (newCap != cap) GameSettings.FrameCap = caps[newCap];
            GUI.enabled = true;

            GUILayout.Space(8f);
            Ui.Label("Saving", UiTheme.Heading);
            var minutes = GameSettings.AutosaveChoices;
            var minuteNames = new string[minutes.Length];
            for (int i = 0; i < minutes.Length; i++) minuteNames[i] = minutes[i] > 0 ? $"Every {minutes[i]} minutes" : "Off";
            int auto = Mathf.Max(0, System.Array.IndexOf(minutes, GameSettings.AutosaveMinutes));
            int newAuto = Choice("Autosave", auto, minuteNames);
            if (newAuto != auto) GameSettings.AutosaveMinutes = minutes[newAuto];
            Ui.Label(GameSettings.Autosave
                ? "<size=11><i>The game also saves whenever the day moves on and when you quit.</i></size>"
                : "<size=11><i>Only the pause menu's Save buttons save. Quitting the window without them loses your progress.</i></size>", Rich());
        }

        /// <summary>A row with a label and the value between ◀ and ▶ buttons that step through the choices.</summary>
        static int Choice(string label, int index, string[] choices)
        {
            int count = Mathf.Max(1, choices.Length);
            index = Mathf.Clamp(index, 0, count - 1);
            GUILayout.BeginHorizontal();
            Ui.Label(label, GUILayout.Width(150f));
            if (Ui.Button("◀", GUILayout.Width(34f))) index = (index - 1 + count) % count;
            Ui.NamedLabel(label + " value", choices.Length > 0 ? choices[index] : "-", Centered(), GUILayout.Width(180f));
            if (Ui.Button("▶", GUILayout.Width(34f))) index = (index + 1) % count;
            GUILayout.EndHorizontal();
            return index;
        }

        /// <summary>Starts a menu frame: theme, scaling for big screens, and the dimmed backdrop.</summary>
        public static Vector2 Begin(float dim)
        {
            UiTheme.Apply();
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var size = new Vector2(Screen.width / scale, Screen.height / scale);
            if (dim > 0f) GUI.DrawTexture(new Rect(0f, 0f, size.x, size.y), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.01f, 0.06f, 0.1f, dim), 0f, 0f);
            return size;
        }

        /// <summary>Menus are modal: clicks and scrolls stop here instead of reaching the game behind.</summary>
        public static void Swallow()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
        }

        static GUIStyle rich, centered;
        public static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };
        static GUIStyle Centered() => centered != null ? centered : centered = new GUIStyle(UiTheme.Skin.label) { alignment = TextAnchor.MiddleCenter };
    }

    /// <summary>
    /// The title screen: Continue (the save played last), New Game, Load Game, Settings and Quit.
    /// Shown when the game starts; the pause menu comes back here.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        enum Page { Main, Load, Settings }

        Page page;
        SaveSlot latest;
        readonly List<Vector4> bubbles = new List<Vector4>();

        void Start()
        {
            latest = SaveGame.Latest();
            MenuScreens.RefreshSaves();
            for (int i = 0; i < 40; i++) bubbles.Add(NewBubble(Random.value));
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && page != Page.Main) Back();
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                b.y -= b.w * Time.unscaledDeltaTime;
                bubbles[i] = b.y < -0.05f ? NewBubble(1.05f) : b;
            }
        }

        static Vector4 NewBubble(float y) => new Vector4(Random.value, y, Random.Range(3f, 14f), Random.Range(0.02f, 0.08f));

        void Back()
        {
            page = Page.Main;
            latest = SaveGame.Latest();
        }

        void OnGUI()
        {
            var size = MenuScreens.Begin(0f);
            DrawBackdrop(size);

            var titleStyle = new GUIStyle(UiTheme.Title) { fontSize = 52, alignment = TextAnchor.MiddleCenter };
            var subStyle = new GUIStyle(MenuScreens.Rich()) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            Ui.Label(new Rect(0f, size.y * 0.12f, size.x, 70f), "Fish Breeder", titleStyle);
            Ui.Label(new Rect(0f, size.y * 0.12f + 66f, size.x, 26f), "<color=#9fe6e0>Breed, raise and sell fish in your own little shop</color>", subStyle);

            float top = size.y * 0.12f + 120f;
            if (page == Page.Main) DrawMain(size, top);
            else if (page == Page.Load) DrawLoad(size, top);
            else DrawSettings(size, top);

            var small = new GUIStyle(MenuScreens.Rich()) { alignment = TextAnchor.LowerRight, fontSize = 11 };
            string where = Application.isEditor ? "Editor saves" : "Saves";
            Ui.Label(new Rect(0f, size.y - 26f, size.x - 12f, 20f), $"<color=#6f8f99>{where}: {SaveGame.Folder}   v{Application.version}</color>", small);
        }

        void DrawBackdrop(Vector2 size)
        {
            // Deep water: darker towards the bottom, with bubbles rising past.
            const int bands = 24;
            for (int i = 0; i < bands; i++)
            {
                var color = Color.Lerp(UiTheme.Lagoon, UiTheme.Abyss, i / (float)(bands - 1));
                GUI.DrawTexture(new Rect(0f, size.y * i / bands, size.x, size.y / bands + 1f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 0f);
            }
            foreach (var b in bubbles)
            {
                var rect = new Rect(b.x * size.x, b.y * size.y, b.z, b.z);
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.7f, 0.95f, 1f, 0.18f), 0f, b.z * 0.5f);
            }
        }

        void DrawMain(Vector2 size, float top)
        {
            float w = 320f;
            var area = new Rect((size.x - w) * 0.5f, top, w, 360f);
            GUILayout.BeginArea(area);
            var tall = GUILayout.Height(44f);
            GUI.enabled = latest != null;
            string continueText = latest != null ? $"Continue  <size=12>(Save {latest.slot}, day {latest.day})</size>" : "Continue";
            if (Ui.Button(continueText, UiTheme.Primary, tall)) GameSession.Play(latest.slot, false);
            GUI.enabled = true;
            GUILayout.Space(6f);
            if (Ui.Button("New Game", tall)) GameSession.NewGame();
            GUILayout.Space(6f);
            if (Ui.Button("Load Game", tall))
            {
                MenuScreens.RefreshSaves();
                page = Page.Load;
            }
            GUILayout.Space(6f);
            if (Ui.Button("Settings", tall)) page = Page.Settings;
            GUILayout.Space(6f);
            if (Ui.Button("Quit", tall)) GameSession.Quit();
            GUILayout.EndArea();
        }

        void DrawLoad(Vector2 size, float top)
        {
            float w = Mathf.Min(640f, size.x - 40f), h = Mathf.Min(460f, size.y - top - 40f);
            var window = new Rect((size.x - w) * 0.5f, top, w, h);
            UiCheck.Begin("Main menu: load", window, UiStyles.Window);
            Ui.Label("Load Game", UiTheme.Title);
            int picked = MenuScreens.DrawSaves(h - 110f);
            GUILayout.FlexibleSpace();
            if (Ui.Button("Back (Esc)", GUILayout.Width(110f))) Back();
            UiCheck.End();
            if (picked > 0) GameSession.Play(picked, false);
        }

        void DrawSettings(Vector2 size, float top)
        {
            float w = Mathf.Min(520f, size.x - 40f), h = Mathf.Min(470f, size.y - top - 40f);
            var window = new Rect((size.x - w) * 0.5f, top, w, h);
            UiCheck.Begin("Main menu: settings", window, UiStyles.Window);
            Ui.Label("Settings", UiTheme.Title);
            MenuScreens.DrawSettings();
            GUILayout.FlexibleSpace();
            if (Ui.Button("Back (Esc)", GUILayout.Width(110f))) Back();
            UiCheck.End();
        }
    }

    /// <summary>
    /// The in-game menu, opened with Esc when nothing else is left to close: Resume, Save Game,
    /// Load Game, Settings, Save and Quit (to the main menu) and Quit Without Saving. The game
    /// stands still while it's open, and the store's menus and keys are switched off behind it.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        enum Page { Main, Load, Settings, ConfirmQuit }

        static PauseMenu instance;
        static int closedFrame = -1;

        Page page;
        int openedFrame;
        float timeScale = 1f;
        string notice;
        float noticeTime;
        readonly List<Behaviour> paused = new List<Behaviour>();

        public static bool IsOpen => instance != null && instance.enabled;
        /// <summary>True on the frame the menu closed, so the same Esc doesn't open something else.</summary>
        public static bool JustClosed => closedFrame == Time.frameCount;

        public static void Open()
        {
            if (instance == null || instance.enabled) return;
            instance.enabled = true;
        }

        void Awake()
        {
            instance = this;
            enabled = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnEnable()
        {
            page = Page.Main;
            openedFrame = Time.frameCount;
            notice = null;
            timeScale = Time.timeScale;
            Time.timeScale = 0f;
            // Everything that takes clicks or keys in the store goes quiet until the menu closes.
            paused.Clear();
            foreach (var behaviour in GetComponents<Behaviour>())
                if (behaviour != this && behaviour.enabled && !(behaviour is AutoSave) && !(behaviour is ShopDay))
                {
                    behaviour.enabled = false;
                    paused.Add(behaviour);
                }
        }

        void OnDisable()
        {
            Time.timeScale = timeScale;
            foreach (var behaviour in paused) if (behaviour != null) behaviour.enabled = true;
            paused.Clear();
            closedFrame = Time.frameCount;
        }

        void Close() => enabled = false;

        void Update()
        {
            if (Time.frameCount == openedFrame || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (page == Page.Main) Close();
            else page = Page.Main;
        }

        void OnGUI()
        {
            GUI.depth = -100;
            var size = MenuScreens.Begin(0.55f);
            switch (page)
            {
                case Page.Main: DrawMain(size); break;
                case Page.Load: DrawLoad(size); break;
                case Page.Settings: DrawSettings(size); break;
                default: DrawConfirmQuit(size); break;
            }
            MenuScreens.Swallow();
        }

        static string SinceSave()
        {
            if (SaveGame.LastSaved < 0f) return "not saved yet this session";
            int minutes = Mathf.FloorToInt((Time.unscaledTime - SaveGame.LastSaved) / 60f);
            return minutes < 1 ? "saved less than a minute ago" : $"last saved {minutes} minute{(minutes == 1 ? "" : "s")} ago";
        }

        static bool Save()
        {
            var auto = AutoSave.Current;
            return auto != null && auto.SaveNow();
        }

        void DrawMain(Vector2 size)
        {
            float w = 320f, h = 375f;
            var window = new Rect((size.x - w) * 0.5f, (size.y - h) * 0.5f, w, h);
            UiCheck.Begin("Pause menu", window, UiStyles.Window);
            Ui.Label("Paused", UiTheme.Title);
            Ui.NamedLabel("Pause: save state", $"<size=11><color=#9fb4bc>Save {SaveGame.Slot}, day {Economy.Day}, {SinceSave()}</color></size>", MenuScreens.Rich());
            GUILayout.Space(6f);
            var tall = GUILayout.Height(38f);
            if (Ui.Button("Resume (Esc)", UiTheme.Primary, tall)) Close();
            GUILayout.Space(4f);
            if (Ui.Button("Save Game", tall))
            {
                notice = Save() ? "Game saved." : "<color=#ffb0a0>Saving failed (see the log).</color>";
                noticeTime = Time.unscaledTime;
            }
            GUILayout.Space(4f);
            if (Ui.Button("Load Game", tall))
            {
                MenuScreens.RefreshSaves();
                page = Page.Load;
            }
            GUILayout.Space(4f);
            if (Ui.Button("Settings", tall)) page = Page.Settings;
            GUILayout.Space(4f);
            if (Ui.Button("Save and Quit", tall))
            {
                if (Save()) GameSession.ToMainMenu();
                else
                {
                    notice = "<color=#ffb0a0>Saving failed, so the game didn't quit.</color>";
                    noticeTime = Time.unscaledTime;
                }
            }
            GUILayout.Space(4f);
            if (Ui.Button("Quit Without Saving", tall)) page = Page.ConfirmQuit;
            if (notice != null && Time.unscaledTime - noticeTime < 3f) Ui.NamedLabel("Pause: notice", notice, MenuScreens.Rich());
            UiCheck.End();
        }

        void DrawConfirmQuit(Vector2 size)
        {
            float w = 400f, h = 190f;
            var window = new Rect((size.x - w) * 0.5f, (size.y - h) * 0.5f, w, h);
            UiCheck.Begin("Pause menu: quit", window, UiStyles.Window);
            Ui.Label("Quit without saving?", UiTheme.Title);
            Ui.NamedLabel("Pause: quit warning", $"Anything since the last save is lost (you {SinceSave().Replace("not saved", "haven't saved")}). Save {SaveGame.Slot} stays as it was.", MenuScreens.Rich());
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (Ui.Button("Quit Without Saving", UiTheme.Primary, GUILayout.Height(34f)))
            {
                if (AutoSave.Current != null) AutoSave.Current.Stop();
                GameSession.ToMainMenu();
            }
            if (Ui.Button("Cancel", GUILayout.Width(100f), GUILayout.Height(34f))) page = Page.Main;
            GUILayout.EndHorizontal();
            UiCheck.End();
        }

        void DrawLoad(Vector2 size)
        {
            float w = Mathf.Min(640f, size.x - 40f), h = Mathf.Min(480f, size.y - 60f);
            var window = new Rect((size.x - w) * 0.5f, (size.y - h) * 0.5f, w, h);
            UiCheck.Begin("Pause menu: load", window, UiStyles.Window);
            Ui.Label("Load Game", UiTheme.Title);
            Ui.NamedLabel("Pause: load warning", $"<size=11><color=#9fb4bc>Loading leaves this game without saving it ({SinceSave()}).</color></size>", MenuScreens.Rich());
            int picked = MenuScreens.DrawSaves(h - 130f, SaveGame.Slot);
            GUILayout.FlexibleSpace();
            if (Ui.Button("Back (Esc)", GUILayout.Width(110f))) page = Page.Main;
            UiCheck.End();
            if (picked > 0)
            {
                if (AutoSave.Current != null) AutoSave.Current.Stop();
                GameSession.Play(picked, false);
            }
        }

        void DrawSettings(Vector2 size)
        {
            float w = Mathf.Min(520f, size.x - 40f), h = Mathf.Min(500f, size.y - 60f);
            var window = new Rect((size.x - w) * 0.5f, (size.y - h) * 0.5f, w, h);
            UiCheck.Begin("Pause menu: settings", window, UiStyles.Window);
            Ui.Label("Settings", UiTheme.Title);
            MenuScreens.DrawSettings();
            GUILayout.FlexibleSpace();
            if (Ui.Button("Back (Esc)", GUILayout.Width(110f))) page = Page.Main;
            UiCheck.End();
        }
    }
}
