using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The player's settings from the Settings window (main menu or pause menu): screen, graphics
    /// and autosave. Kept in PlayerPrefs, apart from the saves, so every save shares them, and put
    /// back before the first scene loads.
    /// </summary>
    public static class GameSettings
    {
        public static readonly int[] AutosaveChoices = { 0, 5, 10, 15, 30 };
        public static readonly int[] FrameCapChoices = { 30, 60, 120, 144, 0 };

        const string FullscreenKey = "settings.fullscreen";
        const string WidthKey = "settings.width";
        const string HeightKey = "settings.height";
        const string QualityKey = "settings.quality";
        const string VSyncKey = "settings.vsync";
        const string FrameCapKey = "settings.frameCap";
        const string AutosaveKey = "settings.autosave";

        /// <summary>Minutes between timed autosaves; 0 turns autosave off.</summary>
        public static int AutosaveMinutes
        {
            get => PlayerPrefs.GetInt(AutosaveKey, 15);
            set { PlayerPrefs.SetInt(AutosaveKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static bool Autosave => AutosaveMinutes > 0;

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            set { PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); ApplyScreen(); }
        }

        public static bool VSync
        {
            get => PlayerPrefs.GetInt(VSyncKey, 1) == 1;
            set { PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); ApplyFrameRate(); }
        }

        /// <summary>Frames per second when VSync is off; 0 = no limit.</summary>
        public static int FrameCap
        {
            get => PlayerPrefs.GetInt(FrameCapKey, 60);
            set { PlayerPrefs.SetInt(FrameCapKey, Mathf.Max(0, value)); ApplyFrameRate(); }
        }

        public static int Quality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            set
            {
                PlayerPrefs.SetInt(QualityKey, value);
                PlayerPrefs.Save();
                QualitySettings.SetQualityLevel(Quality, true);
                ApplyFrameRate();
            }
        }

        /// <summary>The screen sizes the monitor offers, smallest first, without repeats for refresh rates.</summary>
        public static List<Vector2Int> Resolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!list.Contains(size)) list.Add(size);
            }
            if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
            return list;
        }

        /// <summary>The chosen screen size (the current one until something is picked).</summary>
        public static Vector2Int Resolution
        {
            get => new Vector2Int(PlayerPrefs.GetInt(WidthKey, Screen.width), PlayerPrefs.GetInt(HeightKey, Screen.height));
            set
            {
                PlayerPrefs.SetInt(WidthKey, value.x);
                PlayerPrefs.SetInt(HeightKey, value.y);
                ApplyScreen();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyAll()
        {
            if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(Quality, true);
            ApplyFrameRate();
            if (PlayerPrefs.HasKey(WidthKey) || PlayerPrefs.HasKey(FullscreenKey)) ApplyScreen();
        }

        static void ApplyScreen()
        {
            PlayerPrefs.Save();
            // The editor's Game view sizes itself; only builds change the real window.
            if (Application.isEditor) return;
            var size = Resolution;
            Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        static void ApplyFrameRate()
        {
            PlayerPrefs.Save();
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync || FrameCap <= 0 ? -1 : FrameCap;
        }
    }
}
