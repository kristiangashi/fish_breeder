using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// The skill trees. Two pages, Fishkeeping (Aquariums, Biology, Breeding) and Shopkeeping
    /// (Finances, Construction, Public Relations), each with its three trees side by side. Every
    /// tree is a column of tiers; a tier's perks open once enough points are spent in the tiers
    /// above (the milestone bar between tiers shows how close it is). P opens and closes it; Esc
    /// closes it (handled by the store view, which owns Esc). Drawn in the #perks-window
    /// (Window.uxml, styled in FishBreeder.uss).
    /// </summary>
    public class PerksView : MonoBehaviour
    {
        static bool open;
        static XpTrack page = XpTrack.Fishkeeping;

        UiWindow window;

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
            window = new UiWindow(transform, "perks-window", "Skills & perks", 10, true, Close);
            window.CloseButton.text = "Close (P / Esc)";
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            if (!ViewMode.Watching && !UiPanels.Typing && Input.GetKeyDown(KeyCode.P)) Toggle();
            bool show = open && !ViewMode.Watching;
            window.Show(show);
            if (show) window.Fill(DrawHeader, DrawBody);
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

        void DrawHeader()
        {
            var ui = window.Frame;
            PageTab(XpTrack.Fishkeeping);
            PageTab(XpTrack.Shopkeeping);
            ui.Flex();
#if UNITY_EDITOR
            if (ui.Button("Level up (dev)", "fb-button fb-button--small"))
                Progression.DevAward(page, Progression.XpToNext(Progression.Level(page)) - Progression.Xp(page));
            if (ui.Button("Refund (dev)", "fb-button fb-button--small")) Perks.DevRefund();
#endif
        }

        void PageTab(XpTrack track)
        {
            int points = Perks.Available(track);
            string badge = points > 0 ? $"  <color={(track == page ? "#7a3c00" : UiTheme.SunHex)}>●{points}</color>" : "";
            string label = $"{Progression.TrackName(track)} {Progression.Level(track)}{badge}";
            if (window.Frame.Button(label, track == page ? "fb-button fb-tab fb-tab--on" : "fb-button fb-tab")) page = track;
        }

        void DrawBody()
        {
            var ui = window.Frame;
            int points = Perks.Available(page);
            string pointsText = points > 0
                ? $"<color={UiTheme.SunHex}><b>{points} perk point{(points == 1 ? "" : "s")} to spend.</b></color> Pick a perk in any of the three trees."
                : $"No points to spend right now. Every {Progression.TrackName(page)} level gives one.";
            ui.Label($"{pointsText}  Each tier opens once {Balance.Data.perkSettings.pointsPerTier} points are spent in the tiers above it.", "fb-text fb-text--small");

            ui.BeginScroll("fb-scroll fb-window__body");
            ui.BeginRow("fb-columns");
            foreach (PerkTree tree in System.Enum.GetValues(typeof(PerkTree)))
            {
                if (Perks.TrackOf(tree) != page) continue;
                ui.BeginColumn("fb-column fb-columns__column");
                DrawTree(tree);
                ui.Pop();
            }
            ui.Pop();
            ui.Pop();
        }

        void DrawTree(PerkTree tree)
        {
            var ui = window.Frame;
            var color = TreeColor(tree);
            string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            ui.Label($"<size=17><b><color={hex}>{Perks.TreeName(tree).ToUpper()}</color></b></size>");
            ui.Label($"{Perks.TreeBlurb(tree)}  <b>{Perks.Spent(tree)}</b> spent.", "fb-text fb-text--small");
            int tiers = Mathf.Max(1, Balance.Data.perkSettings.tiers);
            for (int tier = 1; tier <= tiers; tier++)
            {
                var perks = new List<PerkBalance>();
                foreach (var perk in Perks.All) if (perk != null && perk.tree == tree && Mathf.Clamp(perk.tier, 1, tiers) == tier) perks.Add(perk);
                if (perks.Count == 0) continue;
                DrawMilestone(tree, tier, color);
                foreach (var perk in perks) DrawPerk(perk, hex);
            }
        }

        /// <summary>The tier's heading, and while it's locked, a bar of the points spent above towards opening it.</summary>
        void DrawMilestone(PerkTree tree, int tier, Color color)
        {
            var ui = window.Frame;
            bool unlocked = Perks.TierOpen(tree, tier);
            int need = Perks.NeededFor(tier);
            int have = Mathf.Min(need, Perks.SpentAbove(tree, tier));
            string state = tier == 1 ? "" : unlocked ? $"  <color={UiTheme.LeafHex}>open</color>" : $"  <color=#9fb4bc>{have}/{need} points above</color>";
            ui.Label($"<size=12><b>TIER {tier}</b>{state}</size>", "fb-text fb-tier");
            if (tier == 1) return;
            ui.Bar(need > 0 ? have / (float)need : 1f, unlocked ? color : Color.Lerp(color, Color.gray, 0.5f), "fb-bar fb-bar--thin");
        }

        void DrawPerk(PerkBalance perk, string hex)
        {
            var ui = window.Frame;
            int rank = Perks.Rank(perk);
            bool maxed = rank >= perk.maxRank;
            bool tierOpen = Perks.TierOpen(perk.tree, perk.tier);
            string why = Perks.WhyNot(perk);
            ui.BeginCard(rank > 0, tierOpen ? "fb-card fb-card--column" : "fb-card fb-card--column fb-faded");
            ui.BeginRow();
            ui.BeginColumn("fb-column fb-grow");
            string pips = $"<color={hex}>{new string('●', rank)}</color><color=#4a6a74>{new string('●', Mathf.Max(0, perk.maxRank - rank))}</color>";
            ui.Label($"<b>{perk.name}</b>  <size=11>{pips}  {rank}/{perk.maxRank}</size>");
            string now = rank > 0 ? perk.Describe(rank) : perk.Describe(1);
            string next = rank > 0 && !maxed ? $"  <color=#9fb4bc>(next: {perk.FormatValue(perk.perRank * (rank + 1))})</color>" : "";
            ui.Label($"{now}.{next}", "fb-text fb-text--small");
            ui.Pop();
            string button = maxed ? "Max" : rank == 0 ? "Learn" : "+1";
            if (ui.Button(button, "fb-button fb-button--small", why == null, why ?? $"Spend a point on {perk.name}")) Perks.TrySpend(perk);
            ui.Pop();
            if (why != null && !maxed && tierOpen && Perks.Available(Perks.TrackOf(perk.tree)) > 0)
                ui.Label($"<color=#ffb0a0>{why}</color>", "fb-text fb-text--tiny");
            ui.Pop();
        }
    }
}
