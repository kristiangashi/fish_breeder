using FishBreeder.Core;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The reputation window (Y, or click the stars). Left: the stars, what brings people in (tanks,
    /// decorations, stars, perks) and what's keeping them away or stopping them buying, each with
    /// its effect in percent, then the latest reviews. Right: the expansion rooms and the stars each
    /// one needs, with a button to buy the next one when it's open to you. Drawn in the
    /// #reputation-window (Window.uxml, styled in FishBreeder.uss).
    /// </summary>
    public class ReputationView : MonoBehaviour
    {
        static bool open;
        static StoreLayout store;
        static ShopDay shopDay;

        UiWindow window;
        UiFrame ui;

        public static bool IsOpen => open;
        public static void Open() => open = true;
        public static void Close() => open = false;

        public static void Toggle()
        {
            if (open) Close();
            else Open();
        }

        public void Initialize(StoreLayout layout, ShopDay day)
        {
            store = layout;
            shopDay = day;
        }

        void Awake()
        {
            open = false;
            window = new UiWindow(transform, "reputation-window", "Reputation", 10, true, Close);
            window.CloseButton.text = "Close (Y / Esc)";
            ui = window.Frame;
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            if (!ViewMode.Watching && !UiPanels.Typing && Input.GetKeyDown(KeyCode.Y)) Toggle();
            bool show = open && !ViewMode.Watching && store != null;
            window.Show(show);
            if (show) window.Fill(DrawHeader, DrawBody);
        }

        void DrawHeader()
        {
            ui.Label($"<size=22><color={UiTheme.SunHex}>{Stars(StoreLayout.Stars)}</color></size>  <b>{StoreLayout.Stars:0.0}</b> / 5", "fb-text fb-header-text");
            ui.Flex();
#if UNITY_EDITOR
            if (ui.Button("+0.5 star (dev)", "fb-button fb-button--small")) Economy.ChangeReputation(0.1f);
            if (ui.Button("-0.5 star (dev)", "fb-button fb-button--small")) Economy.ChangeReputation(-0.1f);
#endif
        }

        void DrawBody()
        {
            var rep = Balance.Data.reputation;
            ui.Label($"Stars are slow to earn. A day open goes up only when more than {rep.neutralScore * 100f:0}% of shoppers' reactions are good (a sale counts twice), and each star is harder than the last; a bad day costs about twice what a good day earns. More stars bring more people in and open bigger rooms. What shoppers complain about is remembered for a few days, and costs you visitors or sales until it fades.", "fb-text fb-text--small");
            ui.BeginRow("fb-halves");
            ui.BeginScroll("fb-scroll fb-half--left");
            DrawTraffic();
            DrawComplaints();
            DrawWishes();
            DrawReviews();
            ui.Pop();
            ui.BeginScroll();
            DrawRooms();
            ui.Pop();
            ui.Pop();
        }

        /// <summary>Where foot traffic comes from: a line for each part, with its effect.</summary>
        void DrawTraffic()
        {
            ui.Section("FOOT TRAFFIC");
            var t = shopDay.TrafficParts;
            ui.Label($"About <b>{t.PerHour:0}</b> customers an hour while open (from {t.baseRate:0} at the start).");
            Effect($"{t.displayCount} display tank{(t.displayCount == 1 ? "" : "s")}", t.display);
            Effect($"{t.otherCount} other tank{(t.otherCount == 1 ? "" : "s")} on the shop floor", t.otherTanks);
            Effect($"{t.decorationCount} decoration{(t.decorationCount == 1 ? "" : "s")} in those tanks", t.decorations);
            if (t.attractivenessAverage > 0f) Effect($"How eye-catching the fish are (average {t.attractivenessAverage * 100f:0}/100)", t.attractiveness);
            Effect($"{StoreLayout.Stars:0.0} star reputation", t.reputation - 1f);
            if (t.perks > 1.001f) Effect("Perks", t.perks - 1f);
            if (t.complaints < 0.999f) Effect("Complaints (below)", t.complaints - 1f);
        }

        /// <summary>Each kind of complaint: how many shoppers made it lately, and what it costs now.</summary>
        void DrawComplaints()
        {
            ui.Section("WHAT SHOPPERS ARE SAYING");
            bool any = false;
            for (int i = 0; i < Reputation.Count; i++)
            {
                var complaint = (Complaint)i;
                float penalty = Reputation.Penalty(complaint);
                float heat = Reputation.Heat(complaint);
                if (heat <= 0f) continue;
                any = true;
                string target = Reputation.HurtsSales(complaint) ? "sales" : "foot traffic";
                string color = penalty >= 0.1f ? UiTheme.CoralHex : UiTheme.SunHex;
                ui.BeginCard(false, "fb-card");
                ui.Label($"<b>{Reputation.Label(complaint)}</b>\n<size=11>{heat * 100f:0}% of shoppers lately. {Advice(complaint)}</size>", "fb-text fb-grow");
                ui.Label($"<color={color}><b>-{penalty * 100f:0}%</b></color>\n<size=11>{target}</size>", "fb-text fb-value");
                ui.Pop();
            }
            if (!any) ui.Label("<i>No complaints lately. Keep it up!</i>");
            float traffic = 1f - Reputation.TrafficMultiplier, sales = 1f - Reputation.SalesMultiplier;
            if (any) ui.Label($"In all: <b>-{traffic * 100f:0}%</b> foot traffic, <b>-{sales * 100f:0}%</b> chance of a sale.", "fb-small");
        }

        static string Advice(Complaint complaint)
        {
            switch (complaint)
            {
                case Complaint.DeadFish: return "Net out dead fish before opening.";
                case Complaint.DirtyWater: return "Wipe the glass, treat the water, add filters.";
                case Complaint.SickFish: return "Treat sick fish, or move them to quarantine.";
                case Complaint.HighPrices: return "Lower prices nearer what fish are worth.";
                case Complaint.FewFish: return "Keep more juveniles and adults priced for sale.";
                default: return "Separate fish that don't get on.";
            }
        }

        /// <summary>What shoppers came for yesterday and couldn't find.</summary>
        void DrawWishes()
        {
            var wishes = Reputation.LatestWishes;
            if (wishes.Count == 0) return;
            ui.Section("WHAT SHOPPERS WANTED");
            ui.Label("Shoppers who left with nothing because their favourite kind wasn't for sale, on the last day open.", "fb-small");
            for (int i = 0; i < wishes.Count && i < 5; i++)
            {
                var species = Genetics.Species.Find(wishes[i].Key);
                if (species == null) continue;
                ui.Label($"• {species.displayName}: <b>{wishes[i].Value}</b> shopper{(wishes[i].Value == 1 ? "" : "s")}");
            }
        }

        void DrawReviews()
        {
            ui.Section("LATEST REVIEWS");
            var reviews = Reputation.Reviews;
            if (reviews.Count == 0) ui.Label("<i>No reviews yet. Open the shop to get some.</i>");
            foreach (var review in reviews)
                ui.Label($"<size=11><color={UiTheme.SunHex}>{Stars(review.stars)}</color>  Day {review.day}</size>\n<i>\"{review.text}\"</i>");
        }

        /// <summary>The expansion rooms in order, with the stars and money each needs.</summary>
        void DrawRooms()
        {
            ui.Section("EXPANSION ROOMS");
            ui.Label($"The {PublicAquarium.RoomName.ToLower()} opens next door as your stars climb: its own entrance, two big exhibits, and visitors who pay to see them (its own rating is in the aquarium window, U).", "fb-small");
            var next = store.NextLockedSection;
            foreach (var section in store.Sections)
            {
                if (section.index == 0) continue;
                float need = StoreLayout.SectionStars(section);
                ui.BeginCard(section == next);
                string state = section.unlocked ? $"<color={UiTheme.LeafHex}>open</color>"
                    : StoreLayout.Stars + 0.001f >= need ? $"<color={UiTheme.LeafHex}>reputation reached</color>" : $"<color=#9fb4bc>needs {need:0.0} stars</color>";
                ui.Line($"<b>{section.name}</b>  <size=11>{state}</size>", section.unlocked ? "" : $"<b>{Economy.Format(section.Price)}</b>");
                if (!section.unlocked)
                {
                    ui.Bar(need > 0f ? StoreLayout.Stars / need : 1f, UiTheme.Sun);
                    if (section == next)
                    {
                        var why = store.WhyLocked(section);
                        bool can = why == null && !shopDay.IsOpen && Economy.CanAfford(section.Price);
                        string label = shopDay.IsOpen ? "Wait until closing" : why != null ? "Not yet" : Economy.CanAfford(section.Price) ? $"Open it for {Economy.Format(section.Price)}" : $"Need {Economy.Format(section.Price - Economy.Money)} more";
                        if (ui.Button(label, "fb-button fb-button--primary", can)) store.TryUnlock(section);
                        if (why != null) ui.Label(why, "fb-small");
                    }
                }
                ui.Pop();
            }
        }

        void Effect(string what, float change)
        {
            string color = change > 0.0005f ? UiTheme.LeafHex : change < -0.0005f ? UiTheme.CoralHex : "#9fb4bc";
            ui.Line(what, $"<color={color}><b>{(change >= 0f ? "+" : "")}{change * 100f:0}%</b></color>");
        }

        public static string Stars(float stars)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            return new string('★', full) + "<color=#5f8a92>" + new string('★', 5 - full) + "</color>";
        }
    }
}
