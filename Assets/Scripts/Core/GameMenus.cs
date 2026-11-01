using System;
using System.Collections.Generic;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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

    /// <summary>
    /// The pieces both menus share. The layouts live in Resources/UI (MainMenu.uxml, PauseMenu.uxml,
    /// and the LoadWindow, SettingsWindow, SaveRow and ChoiceRow pieces they're built from); edit
    /// them in UI Builder. This code only fills them in and reacts to clicks.
    /// </summary>
    public static class MenuScreens
    {
        public static string Describe(SaveSlot slot)
        {
            if (!slot.readable) return $"<b>Save {slot.slot}</b>   <color=#ffb0a0>can't be read</color>";
            string levels = slot.levels.Length >= 2 ? $"   Fishkeeping {slot.levels[0]}, Shopkeeping {slot.levels[1]}" : "";
            return $"<b>Save {slot.slot}</b>   Day {slot.day}   {Economy.Format(slot.money)}{levels}\n<size=11><color=#9fb4bc>Saved {slot.written:d MMM yyyy, HH:mm}</color></size>";
        }

        /// <summary>The UI check (F8) runs in the editor and in development builds.</summary>
        public static bool DevChecks => Application.isEditor || Debug.isDebugBuild;
    }

    /// <summary>
    /// The Load Game window: every save with Load and Delete buttons, newest first. Delete asks once
    /// more before it removes anything, and the save being played can't be deleted.
    /// </summary>
    public class SaveListView
    {
        readonly ScrollView list;
        readonly Label empty;
        readonly VisualTreeAsset rowLayout;
        readonly int playing;
        readonly Action<int> load;
        int confirmDelete = -1;

        public SaveListView(VisualElement window, int playing, Action<int> load, Action back)
        {
            list = window.Q<ScrollView>("save-list");
            empty = window.Q<Label>("no-saves");
            rowLayout = UiPanels.Layout("SaveRow");
            this.playing = playing;
            this.load = load;
            window.Q<Button>("back").clicked += back;
        }

        /// <summary>Re-reads the save folder.</summary>
        public void Refresh()
        {
            confirmDelete = -1;
            Rebuild(SaveGame.Slots());
        }

        void Rebuild(List<SaveSlot> slots)
        {
            list.Clear();
            UiPanels.Show(empty, slots.Count == 0);
            UiPanels.Show(list, slots.Count > 0);
            foreach (var slot in slots)
            {
                var row = rowLayout.Instantiate();
                var card = row.Q("save-row");
                bool isPlaying = slot.slot == playing;
                if (isPlaying) card.AddToClassList("fb-card--selected");
                row.Q<Label>("description").text = MenuScreens.Describe(slot) + (isPlaying ? $"  <color={UiTheme.SunHex}>(playing)</color>" : "");
                var loadButton = row.Q<Button>("load");
                loadButton.SetEnabled(slot.readable);
                int number = slot.slot;
                loadButton.clicked += () => load(number);
                var delete = row.Q<Button>("delete");
                delete.SetEnabled(!isPlaying);
                delete.text = confirmDelete == number ? "Delete for good" : "Delete";
                delete.clicked += () =>
                {
                    if (confirmDelete == number)
                    {
                        SaveGame.Delete(number);
                        Refresh();
                    }
                    else
                    {
                        confirmDelete = number;
                        Rebuild(slots);
                    }
                };
                list.Add(row);
            }
        }
    }

    /// <summary>The Settings window: screen, graphics and autosave settings; each change applies and is kept right away.</summary>
    public class SettingsView
    {
        /// <summary>One setting row: the value between back and forward arrows that step through the choices.</summary>
        class Choice
        {
            readonly VisualElement row;
            readonly Label value;
            readonly Func<string[]> names;
            readonly Func<int> get;
            readonly Action<int> set;

            public Choice(VisualElement row, Func<string[]> names, Func<int> get, Action<int> set, Action changed)
            {
                this.row = row;
                value = row.Q<Label>("value");
                this.names = names;
                this.get = get;
                this.set = set;
                row.Q<Button>("previous").clicked += () => { Step(-1); changed(); };
                row.Q<Button>("next").clicked += () => { Step(1); changed(); };
            }

            void Step(int by)
            {
                var choices = names();
                if (choices.Length == 0) return;
                int index = Mathf.Clamp(get(), 0, choices.Length - 1);
                set((index + by + choices.Length) % choices.Length);
            }

            public void Refresh(bool enabled = true)
            {
                var choices = names();
                value.text = choices.Length > 0 ? choices[Mathf.Clamp(get(), 0, choices.Length - 1)] : "-";
                row.SetEnabled(enabled);
            }
        }

        readonly List<Choice> choices = new List<Choice>();
        readonly Choice frameCap;
        readonly Label autosaveNote;

        public SettingsView(VisualElement window, Action back)
        {
            window.Q<Button>("back").clicked += back;
            UiPanels.Show(window.Q("editor-note"), Application.isEditor);
            autosaveNote = window.Q<Label>("autosave-note");

            Add(window, "screen-mode", () => new[] { "Windowed", "Fullscreen" },
                () => GameSettings.Fullscreen ? 1 : 0, i => GameSettings.Fullscreen = i == 1);
            Add(window, "resolution", () => GameSettings.Resolutions().ConvertAll(r => $"{r.x} × {r.y}").ToArray(),
                () => Mathf.Max(0, GameSettings.Resolutions().IndexOf(GameSettings.Resolution)), i => GameSettings.Resolution = GameSettings.Resolutions()[i]);
            Add(window, "quality", () => QualitySettings.names,
                () => GameSettings.Quality, i => GameSettings.Quality = i);
            Add(window, "vsync", () => new[] { "Off", "On" },
                () => GameSettings.VSync ? 1 : 0, i => GameSettings.VSync = i == 1);
            frameCap = Add(window, "frame-cap", () => Array.ConvertAll(GameSettings.FrameCapChoices, c => c > 0 ? $"{c} fps" : "No limit"),
                () => Mathf.Max(0, Array.IndexOf(GameSettings.FrameCapChoices, GameSettings.FrameCap)), i => GameSettings.FrameCap = GameSettings.FrameCapChoices[i]);
            Add(window, "autosave", () => Array.ConvertAll(GameSettings.AutosaveChoices, m => m > 0 ? $"Every {m} minutes" : "Off"),
                () => Mathf.Max(0, Array.IndexOf(GameSettings.AutosaveChoices, GameSettings.AutosaveMinutes)), i => GameSettings.AutosaveMinutes = GameSettings.AutosaveChoices[i]);
            Refresh();
        }

        Choice Add(VisualElement window, string name, Func<string[]> names, Func<int> get, Action<int> set)
        {
            var choice = new Choice(window.Q(name), names, get, set, Refresh);
            choices.Add(choice);
            return choice;
        }

        public void Refresh()
        {
            foreach (var choice in choices) choice.Refresh(choice != frameCap || !GameSettings.VSync);
            autosaveNote.text = GameSettings.Autosave
                ? "The game also saves whenever the day moves on and when you quit."
                : "Only the pause menu's Save buttons save. Quitting the window without them loses your progress.";
        }
    }

    /// <summary>
    /// The title screen: Continue (the save played last), New Game, Load Game, Settings and Quit.
    /// Shown when the game starts; the pause menu comes back here. Layout: Resources/UI/MainMenu.uxml.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        enum Page { Main, Load, Settings }

        public static MainMenu Current { get; private set; }

        Page page;
        SaveSlot latest;
        UIDocument document;
        VisualElement root, mainPage, loadPage, settingsPage;
        Button continueButton;
        SaveListView saves;
        SettingsView settings;
        Texture2D backdropTexture;
        readonly List<VisualElement> bubbleViews = new List<VisualElement>();
        readonly List<Vector4> bubbles = new List<Vector4>();
        float nextCheck;

        void Start()
        {
            Current = this;
            document = UiPanels.Open("Main menu", "MainMenu", transform, 0);
            root = document.rootVisualElement;
            mainPage = root.Q("main-page");
            loadPage = root.Q("load-page");
            settingsPage = root.Q("settings-page");

            continueButton = root.Q<Button>("continue");
            continueButton.clicked += () => { if (latest != null) GameSession.Play(latest.slot, false); };
            root.Q<Button>("new-game").clicked += GameSession.NewGame;
            root.Q<Button>("load-game").clicked += () => Show(Page.Load);
            root.Q<Button>("settings-button").clicked += () => Show(Page.Settings);
            root.Q<Button>("quit").clicked += GameSession.Quit;
            UiPanels.Show(loadPage.Q("load-warning"), false);
            saves = new SaveListView(loadPage, 0, slot => GameSession.Play(slot, false), () => Show(Page.Main));
            settings = new SettingsView(settingsPage, () => Show(Page.Main));

            string where = Application.isEditor ? "Editor saves" : "Saves";
            root.Q<Label>("footer").text = $"{where}: {SaveGame.Folder}   v{Application.version}";
            BuildBackdrop(root.Q("backdrop"));
            Show(Page.Main);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (backdropTexture != null) Destroy(backdropTexture);
        }

        void Show(Page show)
        {
            page = show;
            if (page == Page.Main)
            {
                latest = SaveGame.Latest();
                continueButton.SetEnabled(latest != null);
                continueButton.text = latest != null ? $"Continue  <size=12>(Save {latest.slot}, day {latest.day})</size>" : "Continue";
            }
            if (page == Page.Load) saves.Refresh();
            if (page == Page.Settings) settings.Refresh();
            UiPanels.Show(mainPage, page == Page.Main);
            UiPanels.Show(loadPage, page == Page.Load);
            UiPanels.Show(settingsPage, page == Page.Settings);
        }

        /// <summary>Deep water, darker towards the bottom, with bubbles rising past.</summary>
        void BuildBackdrop(VisualElement backdrop)
        {
            backdropTexture = UiPanels.Gradient(UiTheme.Lagoon, UiTheme.Abyss);
            backdrop.style.backgroundImage = backdropTexture;
            for (int i = 0; i < 40; i++)
            {
                var bubble = new VisualElement { pickingMode = PickingMode.Ignore };
                bubble.AddToClassList("fb-bubble");
                backdrop.Add(bubble);
                bubbleViews.Add(bubble);
                bubbles.Add(NewBubble(UnityEngine.Random.value));
            }
        }

        static Vector4 NewBubble(float y) => new Vector4(UnityEngine.Random.value, y, UnityEngine.Random.Range(3f, 14f), UnityEngine.Random.Range(0.02f, 0.08f));

        void Update()
        {
            if (root == null) return;
            if (Input.GetKeyDown(KeyCode.Escape) && page != Page.Main && !UiToolkitCheck.Sweeping) Show(Page.Main);
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                b.y -= b.w * Time.unscaledDeltaTime;
                if (b.y < -0.05f) b = NewBubble(1.05f);
                bubbles[i] = b;
                var view = bubbleViews[i];
                view.style.left = Length.Percent(b.x * 100f);
                view.style.top = Length.Percent(b.y * 100f);
                view.style.width = b.z;
                view.style.height = b.z;
            }
            if (!MenuScreens.DevChecks || UiToolkitCheck.Sweeping) return;
            if (Input.GetKeyDown(KeyCode.F8)) RunUiCheck();
            else if (Time.unscaledTime > nextCheck)
            {
                nextCheck = Time.unscaledTime + 1f;
                UiToolkitCheck.Check(root, $"Main menu: {page}");
            }
        }

        /// <summary>F8: every page at every screen size in <see cref="UiToolkitCheck.SweepSizes"/>, with pictures.</summary>
        public Coroutine RunUiCheck()
        {
            var was = page;
            var pages = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("Main", () => Show(Page.Main)),
                new KeyValuePair<string, Action>("Load Game", () => Show(Page.Load)),
                new KeyValuePair<string, Action>("Settings", () => Show(Page.Settings)),
            };
            return StartCoroutine(UiToolkitCheck.Sweep("Main menu", root, pages, () => Show(was)));
        }
    }

    /// <summary>
    /// The in-game menu, opened with Esc when nothing else is left to close: Resume, Save Game,
    /// Load Game, Settings, Save and Quit (to the main menu) and Quit Without Saving. The game
    /// stands still while it's open, and the store's menus and keys are switched off behind it.
    /// Layout: Resources/UI/PauseMenu.uxml.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        enum Page { Main, Load, Settings, ConfirmQuit }

        static PauseMenu instance;
        static int closedFrame = -1;

        Page page;
        int openedFrame;
        float timeScale = 1f;
        float noticeTime = -10f;
        float nextCheck;
        readonly List<Behaviour> paused = new List<Behaviour>();

        UIDocument document;
        VisualElement root, mainPage, quitPage, loadPage, settingsPage;
        Label saveState, notice, quitWarning, loadWarning;
        SaveListView saves;
        SettingsView settings;

        public static bool IsOpen => instance != null && instance.enabled;
        public static PauseMenu Current => instance;
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
            document = UiPanels.Open("Pause menu", "PauseMenu", transform, 100);
            root = document.rootVisualElement;
            mainPage = root.Q("main-page");
            quitPage = root.Q("quit-page");
            loadPage = root.Q("load-page");
            settingsPage = root.Q("settings-page");
            saveState = root.Q<Label>("save-state");
            notice = root.Q<Label>("notice");
            quitWarning = root.Q<Label>("quit-warning");
            loadWarning = loadPage.Q<Label>("load-warning");

            root.Q<Button>("resume").clicked += Close;
            root.Q<Button>("save").clicked += () => Notice(Save() ? "Game saved." : "<color=#ffb0a0>Saving failed (see the log).</color>");
            root.Q<Button>("load-game").clicked += () => Show(Page.Load);
            root.Q<Button>("settings-button").clicked += () => Show(Page.Settings);
            root.Q<Button>("save-and-quit").clicked += () =>
            {
                if (Save()) GameSession.ToMainMenu();
                else Notice("<color=#ffb0a0>Saving failed, so the game didn't quit.</color>");
            };
            root.Q<Button>("quit-without-saving").clicked += () => Show(Page.ConfirmQuit);
            root.Q<Button>("confirm-quit").clicked += () =>
            {
                if (AutoSave.Current != null) AutoSave.Current.Stop();
                GameSession.ToMainMenu();
            };
            root.Q<Button>("cancel").clicked += () => Show(Page.Main);
            saves = new SaveListView(loadPage, SaveGame.Slot, slot =>
            {
                if (AutoSave.Current != null) AutoSave.Current.Stop();
                GameSession.Play(slot, false);
            }, () => Show(Page.Main));
            settings = new SettingsView(settingsPage, () => Show(Page.Main));

            UiPanels.Show(root, false);
            enabled = false;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnEnable()
        {
            if (root == null) return;
            openedFrame = Time.frameCount;
            noticeTime = -10f;
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
            UiPanels.Show(root, true);
            Show(Page.Main);
        }

        void OnDisable()
        {
            if (root == null) return;
            UiPanels.Show(root, false);
            Time.timeScale = timeScale;
            foreach (var behaviour in paused) if (behaviour != null) behaviour.enabled = true;
            paused.Clear();
            closedFrame = Time.frameCount;
        }

        void Close() => enabled = false;

        void Show(Page show)
        {
            page = show;
            if (page == Page.Load) saves.Refresh();
            if (page == Page.Settings) settings.Refresh();
            UiPanels.Show(mainPage, page == Page.Main);
            UiPanels.Show(quitPage, page == Page.ConfirmQuit);
            UiPanels.Show(loadPage, page == Page.Load);
            UiPanels.Show(settingsPage, page == Page.Settings);
            Refresh();
        }

        void Notice(string text)
        {
            notice.text = text;
            noticeTime = Time.unscaledTime;
            Refresh();
        }

        /// <summary>The lines that change while the menu is open (time since the last save, the notice).</summary>
        void Refresh()
        {
            saveState.text = $"Save {SaveGame.Slot}, day {Economy.Day}, {SinceSave()}";
            loadWarning.text = $"Loading leaves this game without saving it ({SinceSave()}).";
            quitWarning.text = $"Anything since the last save is lost (you {SinceSave().Replace("not saved", "haven't saved")}). Save {SaveGame.Slot} stays as it was.";
            UiPanels.Show(notice, Time.unscaledTime - noticeTime < 3f);
        }

        void Update()
        {
            if (UiToolkitCheck.Sweeping) return;
            Refresh();
            if (Time.frameCount != openedFrame && Input.GetKeyDown(KeyCode.Escape))
            {
                if (page == Page.Main) Close();
                else Show(Page.Main);
                return;
            }
            if (!MenuScreens.DevChecks) return;
            if (Input.GetKeyDown(KeyCode.F8)) RunUiCheck();
            else if (Time.unscaledTime > nextCheck)
            {
                nextCheck = Time.unscaledTime + 1f;
                UiToolkitCheck.Check(root, $"Pause menu: {page}");
            }
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

        /// <summary>F8 while paused: every page at every screen size in <see cref="UiToolkitCheck.SweepSizes"/>, with pictures.</summary>
        public Coroutine RunUiCheck()
        {
            var was = page;
            var pages = new List<KeyValuePair<string, Action>>
            {
                new KeyValuePair<string, Action>("Paused", () => Show(Page.Main)),
                new KeyValuePair<string, Action>("Load Game", () => Show(Page.Load)),
                new KeyValuePair<string, Action>("Settings", () => Show(Page.Settings)),
                new KeyValuePair<string, Action>("Quit without saving", () => Show(Page.ConfirmQuit)),
            };
            return StartCoroutine(UiToolkitCheck.Sweep("Pause menu", root, pages, () => Show(was)));
        }
    }
}
