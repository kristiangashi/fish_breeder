using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The skill trees. Two pages, Fishkeeping (Aquariums, Biology, Breeding) and Shopkeeping
    /// (Finances, Construction, Public Relations), each with its three trees side by side. Every
    /// tree is a column of tiers; a tier's perks open once enough points are spent in the tiers
    /// above (the milestone bar between tiers shows how close it is). P opens and closes it; Esc
    /// closes it (handled by the store view, which owns Esc).
    /// </summary>
    public class PerksView : MonoBehaviour
    {
        static bool open;
        static XpTrack page = XpTrack.Fishkeeping;

        Vector2 scroll;
        Rect window;

        public static bool IsOpen => open;

        /// <summary>Opens on the page with points to spend, if either has some.</summary>
        public static void Open()
        {
            open = true;
            if (Perks.Available(XpTrack.Shopkeeping) > 0 && Perks.Available(XpTrack.Fishkeeping) == 0) page = XpTrack.Shopkeeping;
            else if (Perks.Available(XpTrack.Fishkeeping) > 0) page = XpTrack.Fishkeeping;
        }

        public static void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public static void Close() => open = false;

        void Awake()
        {
            open = false;
            page = XpTrack.Fishkeeping;
        }

        void Update()
        {
            if (ViewMode.Watching || GUIUtility.keyboardControl != 0) return;
            if (Input.GetKeyDown(KeyCode.P)) Toggle();
        }

        public static Color TreeColor(PerkTree tree)
        {
            switch (tree)
            {
                case PerkTree.Aquariums: return UiTheme.Aqua;
                case PerkTree.Biology: return UiTheme.Leaf;
                case PerkTree.Breeding: return UiTheme.Hibiscus;
                case PerkTree.Finances: return UiTheme.Sun;
                case PerkTree.Construction: return UiTheme.Coral;
                default: return new Color(0.7f, 0.62f, 1f);
            }
        }

        void OnGUI()
        {
            if (!open || ViewMode.Watching) return;
            UiTheme.Apply();
            GUI.depth = -10;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float w = Mathf.Min(1020f, width - 40f);
            float h = Mathf.Min(720f, height - 50f);
            window = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);

            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.01f, 0.06f, 0.1f, 0.45f), 0f, 0f);

            UiCheck.Begin("Perks", window, UiStyles.Window);
            DrawHeader();
            DrawTrees(w);
            UiCheck.End();

            // Modal: clicks outside close it instead of reaching the store behind.
            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                if (!window.Contains(e.mousePosition)) Close();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && window.Contains(e.mousePosition)) e.Use();
        }

        void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            Ui.Label("Skills & perks", UiTheme.Title, GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            PageTab(XpTrack.Fishkeeping);
            PageTab(XpTrack.Shopkeeping);
            GUILayout.FlexibleSpace();
#if UNITY_EDITOR
            if (Ui.Button("Level up (dev)", GUILayout.Width(110f)))
                Progression.DevAward(page, Progression.XpToNext(Progression.Level(page)) - Progression.Xp(page));
            if (Ui.Button("Refund (dev)", GUILayout.Width(96f))) Perks.DevRefund();
#endif
            if (Ui.Button("Close (P / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            int points = Perks.Available(page);
            string pointsText = points > 0
                ? $"<color={UiTheme.SunHex}><b>{points} perk point{(points == 1 ? "" : "s")} to spend.</b></color> Pick a perk in any of the three trees."
                : $"No points to spend right now. Every {Progression.TrackName(page)} level gives one.";
            Ui.Label($"<size=12>{pointsText}  Each tier opens once {Balance.Data.perkSettings.pointsPerTier} points are spent in the tiers above it.</size>", Rich());
        }

        static void PageTab(XpTrack track)
        {
            int points = Perks.Available(track);
            string badge = points > 0 ? $"  <color={UiTheme.SunHex}>●{points}</color>" : "";
            string label = $"{Progression.TrackName(track)} {Progression.Level(track)}{badge}";
            var style = track == page ? UiTheme.Primary : UiTheme.Skin.button;
            if (Ui.Button(label, style, GUILayout.Width(170f), GUILayout.Height(30f))) page = track;
        }

        void DrawTrees(float windowWidth)
        {
            var trees = new List<PerkTree>();
            foreach (PerkTree tree in System.Enum.GetValues(typeof(PerkTree)))
                if (Perks.TrackOf(tree) == page) trees.Add(tree);
            // Each column and the gap after it share the list's visible width exactly.
            float column = (UiCheck.ScrollWidth("Perks: trees", windowWidth - 60f) - 6f * trees.Count) / Mathf.Max(1, trees.Count);
            scroll = UiCheck.BeginScroll("Perks: trees", scroll);
            GUILayout.BeginHorizontal();
            foreach (var tree in trees)
            {
                GUILayout.BeginVertical(GUILayout.Width(column));
                DrawTree(tree, column);
                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }
            GUILayout.EndHorizontal();
            UiCheck.EndScroll();
        }

        void DrawTree(PerkTree tree, float width)
        {
            var color = TreeColor(tree);
            string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            Ui.Label($"<size=17><b><color={hex}>{Perks.TreeName(tree).ToUpper()}</color></b></size>", Rich());
            Ui.Label($"<size=11>{Perks.TreeBlurb(tree)}  <b>{Perks.Spent(tree)}</b> spent.</size>", Rich());
            int tiers = Mathf.Max(1, Balance.Data.perkSettings.tiers);
            for (int tier = 1; tier <= tiers; tier++)
            {
                var perks = new List<PerkBalance>();
                foreach (var perk in Perks.All) if (perk != null && perk.tree == tree && Mathf.Clamp(perk.tier, 1, tiers) == tier) perks.Add(perk);
                if (perks.Count == 0) continue;
                DrawMilestone(tree, tier, color, width);
                foreach (var perk in perks) DrawPerk(perk, color, hex);
            }
        }

        /// <summary>The tier's heading, and while it's locked, a bar of the points spent above towards opening it.</summary>
        static void DrawMilestone(PerkTree tree, int tier, Color color, float width)
        {
            bool unlocked = Perks.TierOpen(tree, tier);
            int need = Perks.NeededFor(tier);
            int have = Mathf.Min(need, Perks.SpentAbove(tree, tier));
            GUILayout.Space(6f);
            string state = tier == 1 ? "" : unlocked ? $"  <color={UiTheme.LeafHex}>open</color>" : $"  <color=#9fb4bc>{have}/{need} points above</color>";
            Ui.Label($"<size=12><b>TIER {tier}</b>{state}</size>", Rich());
            if (tier == 1) return;
            var bar = Ui.Slot("Bar", width - 10f, 6f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 3f);
            float fill = need > 0 ? have / (float)need : 1f;
            if (fill > 0f)
                GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(6f, bar.width * fill), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                    unlocked ? color : Color.Lerp(color, Color.gray, 0.5f), 0f, 3f);
            GUILayout.Space(3f);
        }

        static void DrawPerk(PerkBalance perk, Color color, string hex)
        {
            int rank = Perks.Rank(perk);
            bool maxed = rank >= perk.maxRank;
            bool tierOpen = Perks.TierOpen(perk.tree, perk.tier);
            string why = Perks.WhyNot(perk);
            var old = GUI.color;
            if (!tierOpen) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUILayout.BeginVertical(rank > 0 ? UiTheme.CardSelected : UiTheme.Card);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            string pips = $"<color={hex}>{new string('●', rank)}</color><color=#4a6a74>{new string('●', Mathf.Max(0, perk.maxRank - rank))}</color>";
            Ui.Label($"<b>{perk.name}</b>  <size=11>{pips}  {rank}/{perk.maxRank}</size>", Rich());
            string now = rank > 0 ? perk.Describe(rank) : perk.Describe(1);
            string next = rank > 0 && !maxed ? $"  <color=#9fb4bc>(next: {perk.FormatValue(perk.perRank * (rank + 1))})</color>" : "";
            Ui.Label($"<size=11>{now}.{next}</size>", Rich());
            GUILayout.EndVertical();
            GUI.enabled = why == null;
            string button = maxed ? "Max" : rank == 0 ? "Learn" : "+1";
            if (Ui.Button(new GUIContent(button, why ?? $"Spend a point on {perk.name}"), GUILayout.Width(56f), GUILayout.Height(30f))) Perks.TrySpend(perk);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (why != null && !maxed && tierOpen && Perks.Available(Perks.TrackOf(perk.tree)) > 0)
                Ui.Label($"<size=10><color=#ffb0a0>{why}</color></size>", Rich());
            GUILayout.EndVertical();
            GUI.color = old;
        }

        static GUIStyle rich;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };
    }
}
