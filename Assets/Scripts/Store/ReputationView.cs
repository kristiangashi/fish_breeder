using FishBreeder.Core;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The reputation window (Y, or click the stars). Left: the stars, what brings people in (tanks,
    /// decorations, stars, perks) and what's keeping them away or stopping them buying, each with
    /// its effect in percent, then the latest reviews. Right: the expansion rooms and the stars each
    /// one needs, with a button to buy the next one when it's open to you.
    /// </summary>
    public class ReputationView : MonoBehaviour
    {
        static bool open;
        static StoreLayout store;
        static ShopDay shopDay;

        Vector2 leftScroll, rightScroll;
        Rect window;

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

        void Awake() => open = false;

        void Update()
        {
            if (ViewMode.Watching || GUIUtility.keyboardControl != 0) return;
            if (Input.GetKeyDown(KeyCode.Y)) Toggle();
        }

        void OnGUI()
        {
            if (!open || ViewMode.Watching || store == null) return;
            UiTheme.Apply();
            GUI.depth = -10;
            float scale = Mathf.Max(1f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float w = Mathf.Min(980f, width - 40f);
            float h = Mathf.Min(700f, height - 50f);
            window = new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.01f, 0.06f, 0.1f, 0.45f), 0f, 0f);

            UiCheck.Begin("Reputation window", window, UiStyles.Window);
            GUILayout.BeginHorizontal();
            Ui.Label("Reputation", UiTheme.Title, GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            Ui.Label($"<size=22><color={UiTheme.SunHex}>{Stars(StoreLayout.Stars)}</color></size>  <b>{StoreLayout.Stars:0.0}</b> / 5", Rich(), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
#if UNITY_EDITOR
            if (Ui.Button("+0.5 star (dev)", GUILayout.Width(120f))) Economy.ChangeReputation(0.1f);
            if (Ui.Button("-0.5 star (dev)", GUILayout.Width(120f))) Economy.ChangeReputation(-0.1f);
#endif
            if (Ui.Button("Close (Y / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            var rep = Balance.Data.reputation;
            Ui.Label($"<size=12>Stars are slow to earn. A day open goes up only when more than {rep.neutralScore * 100f:0}% of shoppers' reactions are good (a sale counts twice), and each star is harder than the last; a bad day costs about twice what a good day earns. More stars bring more people in and open bigger rooms. What shoppers complain about is remembered for a few days, and costs you visitors or sales until it fades.</size>", Rich());
            GUILayout.Space(4f);

            float column = (w - UiStyles.Window.padding.horizontal - 16f) * 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(column));
            leftScroll = UiCheck.BeginScroll("Reputation: effects and reviews", leftScroll);
            DrawTraffic();
            DrawComplaints();
            DrawWishes();
            DrawReviews();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.Space(16f);
            GUILayout.BeginVertical(GUILayout.Width(column));
            rightScroll = UiCheck.BeginScroll("Reputation: rooms", rightScroll);
            DrawRooms();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
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

        /// <summary>Where foot traffic comes from: a line for each part, with its effect.</summary>
        static void DrawTraffic()
        {
            Heading("FOOT TRAFFIC");
            var t = shopDay.TrafficParts;
            Ui.Label($"About <b>{t.PerHour:0}</b> customers an hour while open (from {t.baseRate:0} at the start).", Rich());
            Effect($"{t.displayCount} display tank{(t.displayCount == 1 ? "" : "s")}", t.display);
            Effect($"{t.otherCount} other tank{(t.otherCount == 1 ? "" : "s")} on the shop floor", t.otherTanks);
            Effect($"{t.decorationCount} decoration{(t.decorationCount == 1 ? "" : "s")} in those tanks", t.decorations);
            if (t.attractivenessAverage > 0f) Effect($"How eye-catching the fish are (average {t.attractivenessAverage * 100f:0}/100)", t.attractiveness);
            Effect($"{StoreLayout.Stars:0.0} star reputation", t.reputation - 1f);
            if (t.perks > 1.001f) Effect("Perks", t.perks - 1f);
            if (t.complaints < 0.999f) Effect("Complaints (below)", t.complaints - 1f);
        }

        /// <summary>Each kind of complaint: how many shoppers made it lately, and what it costs now.</summary>
        static void DrawComplaints()
        {
            GUILayout.Space(8f);
            Heading("WHAT SHOPPERS ARE SAYING");
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
                GUILayout.BeginHorizontal(UiTheme.Card);
                Ui.Label($"<b>{Reputation.Label(complaint)}</b>\n<size=11>{heat * 100f:0}% of shoppers lately. {Advice(complaint)}</size>", Rich());
                Ui.Label($"<color={color}><b>-{penalty * 100f:0}%</b></color>\n<size=11>{target}</size>", Rich(), GUILayout.Width(86f));
                GUILayout.EndHorizontal();
            }
            if (!any) Ui.Label("<i>No complaints lately. Keep it up!</i>", Rich());
            float traffic = 1f - Reputation.TrafficMultiplier, sales = 1f - Reputation.SalesMultiplier;
            if (any) Ui.Label($"<size=11>In all: <b>-{traffic * 100f:0}%</b> foot traffic, <b>-{sales * 100f:0}%</b> chance of a sale.</size>", Rich());
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
        static void DrawWishes()
        {
            var wishes = Reputation.LatestWishes;
            if (wishes.Count == 0) return;
            GUILayout.Space(8f);
            Heading("WHAT SHOPPERS WANTED");
            Ui.Label("<size=11>Shoppers who left with nothing because their favourite kind wasn't for sale, on the last day open.</size>", Rich());
            for (int i = 0; i < wishes.Count && i < 5; i++)
            {
                var species = Genetics.Species.Find(wishes[i].Key);
                if (species == null) continue;
                Ui.Label($"• {species.displayName}: <b>{wishes[i].Value}</b> shopper{(wishes[i].Value == 1 ? "" : "s")}", Rich());
            }
        }

        static void DrawReviews()
        {
            GUILayout.Space(8f);
            Heading("LATEST REVIEWS");
            var reviews = Reputation.Reviews;
            if (reviews.Count == 0) Ui.Label("<i>No reviews yet. Open the shop to get some.</i>", Rich());
            foreach (var review in reviews)
                Ui.Label($"<size=11><color={UiTheme.SunHex}>{Stars(review.stars)}</color>  Day {review.day}</size>\n<i>\"{review.text}\"</i>", Rich());
        }

        /// <summary>The expansion rooms in order, with the stars and money each needs.</summary>
        void DrawRooms()
        {
            Heading("EXPANSION ROOMS");
            Ui.Label("<size=11>Each new room adds space for three or six more tanks. They open in order, as your stars climb.</size>", Rich());
            var next = store.NextLockedSection;
            foreach (var section in store.Sections)
            {
                if (section.index == 0) continue;
                float need = StoreLayout.SectionStars(section);
                GUILayout.BeginVertical(section == next ? UiTheme.CardSelected : UiTheme.Card);
                GUILayout.BeginHorizontal();
                string state = section.unlocked ? $"<color={UiTheme.LeafHex}>open</color>"
                    : StoreLayout.Stars + 0.001f >= need ? $"<color={UiTheme.LeafHex}>reputation reached</color>" : $"<color=#9fb4bc>needs {need:0.0} stars</color>";
                Ui.Label($"<b>{section.name}</b>  <size=11>{state}</size>", Rich());
                GUILayout.FlexibleSpace();
                if (!section.unlocked) Ui.Label($"<b>{Economy.Format(section.Price)}</b>", Rich(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                if (!section.unlocked)
                {
                    ProgressBar(need > 0f ? StoreLayout.Stars / need : 1f, UiTheme.Sun);
                    if (section == next)
                    {
                        var why = store.WhyLocked(section);
                        GUI.enabled = why == null && !shopDay.IsOpen && Economy.CanAfford(section.Price);
                        string label = shopDay.IsOpen ? "Wait until closing" : why != null ? "Not yet" : Economy.CanAfford(section.Price) ? $"Open it for {Economy.Format(section.Price)}" : $"Need {Economy.Format(section.Price - Economy.Money)} more";
                        if (Ui.Button(label, UiTheme.Primary)) store.TryUnlock(section);
                        GUI.enabled = true;
                        if (why != null) Ui.Label($"<size=11>{why}</size>", Rich());
                    }
                }
                GUILayout.EndVertical();
            }
        }

        static void Effect(string what, float change)
        {
            string color = change > 0.0005f ? UiTheme.LeafHex : change < -0.0005f ? UiTheme.CoralHex : "#9fb4bc";
            GUILayout.BeginHorizontal();
            Ui.Label(what, Rich());
            Ui.Label($"<color={color}><b>{(change >= 0f ? "+" : "")}{change * 100f:0}%</b></color>", Rich(), GUILayout.Width(64f));
            GUILayout.EndHorizontal();
        }

        public static string Stars(float stars)
        {
            int full = Mathf.Clamp(Mathf.RoundToInt(stars), 0, 5);
            return new string('★', full) + "<color=#5f8a92>" + new string('★', 5 - full) + "</color>";
        }

        static void Heading(string text) => Ui.Label($"<b><color={UiTheme.SunHex}>{text}</color></b>", Rich());

        static void ProgressBar(float fill, Color color)
        {
            var bar = Ui.Slot("Bar", 100f, 8f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0.05f, 0.1f, 0.6f), 0f, 4f);
            if (fill > 0f) GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(8f, bar.width * Mathf.Clamp01(fill)), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, 4f);
            GUILayout.Space(2f);
        }

        static GUIStyle rich;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };
    }
}
