using FishBreeder.Core;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The public aquarium window (U, the Aquarium button, or click the ticket booth). Left: the
    /// rating and what it does to visitor numbers, each exhibit's appeal piece by piece, and the
    /// latest reviews. Right: the ticket price, the booth's estimate of the day at that price
    /// (a little off until the booth has all three upgrades), and the booth upgrades.
    /// </summary>
    public class AquariumView : MonoBehaviour
    {
        static bool open;
        static StoreLayout store;
        static ShopDay shopDay;

        Vector2 leftScroll, rightScroll;
        Rect window;

        public static bool IsOpen => open;
        public static void Close() => open = false;

        /// <summary>Opens once the aquarium is built (before that it says so in the log).</summary>
        public static void Open()
        {
            var section = store != null ? store.AquariumSection : null;
            if (section == null || !section.unlocked)
            {
                GameLog.Add($"The {PublicAquarium.RoomName.ToLower()} isn't built yet: unlock the room east of the shop first.");
                return;
            }
            open = true;
        }

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
            if (ViewMode.Watching || GUIUtility.keyboardControl != 0 || Tank.Focused != null) return;
            if (Input.GetKeyDown(KeyCode.U)) Toggle();
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

            UiCheck.Begin("Aquarium window", window, UiStyles.Window);
            GUILayout.BeginHorizontal();
            Ui.Label(PublicAquarium.RoomName, UiTheme.Title, GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            Ui.Label($"<size=22><color={UiTheme.SunHex}>{ReputationView.Stars(PublicAquarium.Stars)}</color></size>  <b>{PublicAquarium.Stars:0.0}</b> / 5", Rich(), GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
#if UNITY_EDITOR
            if (Ui.Button("+0.5 star (dev)", GUILayout.Width(120f))) PublicAquarium.DevChangeRating(0.1f);
            if (Ui.Button("-0.5 star (dev)", GUILayout.Width(120f))) PublicAquarium.DevChangeRating(-0.1f);
#endif
            if (Ui.Button("Close (U / Esc)", GUILayout.Width(120f))) Close();
            GUILayout.EndHorizontal();
            Ui.Label("<size=12>People pay at the booth to see the two exhibits, then leave. The better the exhibits look, the more people come and the more a ticket is worth. Every visitor leaves with an opinion of what they saw and what they paid; the day's visits move the aquarium's rating, and the rating multiplies how many come.</size>", Rich());
            GUILayout.Space(4f);

            float column = (w - UiStyles.Window.padding.horizontal - 16f) * 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(column));
            leftScroll = UiCheck.BeginScroll("Aquarium: rating and exhibits", leftScroll);
            DrawVisitors();
            DrawExhibits();
            DrawReviews();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.Space(16f);
            GUILayout.BeginVertical(GUILayout.Width(column));
            rightScroll = UiCheck.BeginScroll("Aquarium: tickets and booth", rightScroll);
            DrawTickets();
            DrawBooth();
            UiCheck.EndScroll();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            UiCheck.End();

            // Modal: clicks outside close it instead of reaching the store behind.
            var e = Event.current;
            if (e.type == EventType.MouseDown && !UiLayout.Editing)
            {
                if (!window.Contains(e.mousePosition)) Close();
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel && window.Contains(e.mousePosition)) e.Use();
        }

        static void DrawVisitors()
        {
            Heading("VISITORS");
            var why = PublicAquarium.WhyClosed(store);
            if (why != null) Ui.Label($"<color={UiTheme.SunHex}>Closed: {why}</color>", Rich());
            else Ui.Label($"About <b>{PublicAquarium.VisitorsPerHour(store):0}</b> people an hour come to the booth while the shop is open (from {PublicAquarium.Data.visitorsPerHour:0} to start with).", Rich());
            float appeal = PublicAquarium.AquariumAppeal(store);
            Effect($"Appeal {appeal:0}/100 (the better exhibit counts 65%, the other 35%)", PublicAquarium.AppealMultiplier(appeal) - 1f);
            Effect($"{PublicAquarium.Stars:0.0} star rating", PublicAquarium.RatingMultiplier - 1f);
            var today = PublicAquarium.Today;
            if (today != null)
                Ui.Label($"<size=12>Today so far: {today.visitors} came, {today.tickets} bought tickets ({Economy.Format(today.earnings)}), {today.turnedAway} turned away at the price, {today.happy} left happy.</size>", Rich());
        }

        static void DrawExhibits()
        {
            GUILayout.Space(8f);
            Heading("THE EXHIBITS");
            var exhibits = PublicAquarium.Exhibits(store);
            foreach (var climate in new[] { ExhibitClimate.Temperate, ExhibitClimate.Tropical })
            {
                var exhibit = exhibits.Find(x => x.Climate == climate);
                GUILayout.BeginVertical(UiTheme.Card);
                string name = $"{PublicAquarium.ClimateName(climate)} aquarium";
                string where = climate == ExhibitClimate.Temperate ? "north wall, cold-water fish" : "east wall, warm-water fish";
                if (exhibit == null)
                {
                    Ui.Label($"<b>{name}</b>  <size=11>({where})</size>\n<size=11>Not built yet: click its wall in the aquarium to buy it ({Economy.Format(StoreLayout.TankPrice(null, TankRole.Exhibit))}).</size>", Rich());
                    GUILayout.EndVertical();
                    continue;
                }
                var a = PublicAquarium.ExhibitAppeal(exhibit.Tank);
                Ui.Label($"<b>{exhibit.Title}</b>  <size=11>({where})</size>   appeal <b>{a.total:0}</b>/100", Rich());
                if (a.fish == 0 && exhibit.Tank.Residents.Count == 0) Ui.Label("<size=11><i>No fish yet. Visitors only come when there's something to see.</i></size>", Rich());
                Part($"{a.fish} juveniles and adults", a.fullness, 30f);
                Part($"{a.kinds} kind{(a.kinds == 1 ? "" : "s")} of fish", a.variety, 20f);
                Part("How eye-catching they are", a.looks, 25f);
                Part($"Aquarium quality {exhibit.Tank.Quality}", a.quality, 20f);
                Part("Clear, clean water", a.clean, 5f);
                if (a.dead < 0f) Part("Dead fish", a.dead, 0f);
                if (a.dirty < 0f) Part("Dirty water or algae", a.dirty, 0f);
                if (a.sick < 0f) Part("Sick fish", a.sick, 0f);
                if (a.fighting < 0f) Part("Fish fighting", a.fighting, 0f);
                GUILayout.EndVertical();
            }
        }

        static void DrawReviews()
        {
            GUILayout.Space(8f);
            Heading("LATEST REVIEWS");
            var reviews = PublicAquarium.Reviews;
            if (reviews.Count == 0) Ui.Label("<i>No reviews yet.</i>", Rich());
            foreach (var review in reviews)
                Ui.Label($"<size=11><color={UiTheme.SunHex}>{ReputationView.Stars(review.stars)}</color>  Day {review.day}</size>\n<i>\"{review.text}\"</i>", Rich());
        }

        static void DrawTickets()
        {
            Heading("TICKET PRICE");
            bool canChange = !shopDay.IsOpen;
            GUILayout.BeginHorizontal();
            GUI.enabled = canChange;
            int price = PublicAquarium.TicketPrice;
            if (Ui.Button("-5", GUILayout.Width(40f))) PublicAquarium.SetTicketPrice(price - 5);
            if (Ui.Button("-1", GUILayout.Width(40f))) PublicAquarium.SetTicketPrice(price - 1);
            Ui.Label($"<size=22><b>{Economy.Format(PublicAquarium.TicketPrice)}</b></size>", Centered(), GUILayout.Width(90f));
            if (Ui.Button("+1", GUILayout.Width(40f))) PublicAquarium.SetTicketPrice(price + 1);
            if (Ui.Button("+5", GUILayout.Width(40f))) PublicAquarium.SetTicketPrice(price + 5);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!canChange) Ui.Label("<size=11><i>The price is on the board for today: change it once the shop has closed.</i></size>", Rich());

            GUILayout.Space(6f);
            Heading("THE BOOTH'S ESTIMATE");
            var guess = PublicAquarium.Guess(store, PublicAquarium.TicketPrice);
            float range = PublicAquarium.EstimatorRange;
            string sure = range <= 0.001f ? $"<color={UiTheme.LeafHex}>spot on</color>" : $"could be off by up to {range * 100f:0}% either way today";
            GUILayout.BeginVertical(UiTheme.Card);
            Ui.Label($"A fair ticket price: about <b>{Economy.Format(Mathf.Max(1, Mathf.RoundToInt(guess.fairPrice)))}</b>", Rich());
            Ui.Label($"At {Economy.Format(PublicAquarium.TicketPrice)}: about <b>{guess.visitors:0}</b> people today, <b>{guess.tickets:0}</b> buying tickets, <b>{Economy.Format(Mathf.RoundToInt(guess.earnings))}</b>", Rich());
            int best = PublicAquarium.BestPriceGuess(store);
            Ui.Label($"<size=11>Most ticket money at about {Economy.Format(best)}, but a lower price leaves people happier, and happy visitors raise the rating.</size>", Rich());
            Ui.Label($"<size=11>The estimate {sure}. Upgrade the booth to make it surer.</size>", Rich());
            GUILayout.BeginHorizontal();
            GUI.enabled = canChange && best != PublicAquarium.TicketPrice;
            if (Ui.Button($"Use {Economy.Format(best)}")) PublicAquarium.SetTicketPrice(best);
            int fair = Mathf.Max(1, Mathf.RoundToInt(guess.fairPrice));
            GUI.enabled = canChange && fair != PublicAquarium.TicketPrice;
            if (Ui.Button($"Use the fair price ({Economy.Format(fair)})")) PublicAquarium.SetTicketPrice(fair);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            Ui.Label("<size=11>Above what a visitor will pay, they walk off at the booth and grumble about it. Below it, the cheaper the ticket, the better they feel about the visit.</size>", Rich());
        }

        static void DrawBooth()
        {
            GUILayout.Space(8f);
            Heading("TICKET BOOTH UPGRADES");
            var upgrades = PublicAquarium.Data.boothUpgrades;
            Ui.Label($"<size=11>Up to {PublicAquarium.MaxInside} visitors inside at once.</size>", Rich());
            for (int i = 0; upgrades != null && i < upgrades.Count; i++)
            {
                var upgrade = upgrades[i];
                bool owned = i < PublicAquarium.BoothLevel, next = i == PublicAquarium.BoothLevel;
                GUILayout.BeginVertical(next ? UiTheme.CardSelected : UiTheme.Card);
                GUILayout.BeginHorizontal();
                var errors = PublicAquarium.Data.estimatorError;
                float after = errors != null && errors.Count > 0 ? errors[Mathf.Min(i + 1, errors.Count - 1)] : 0f;
                Ui.Label($"<b>{i + 1}. {upgrade.name}</b>  <size=11>{(owned ? $"<color={UiTheme.LeafHex}>done</color>" : $"estimate within {after * 100f:0}%, +{upgrade.extraVisitors} inside")}</size>", Rich());
                GUILayout.FlexibleSpace();
                if (!owned) Ui.Label($"<b>{Economy.Format(PublicAquarium.BoothUpgradePrice(upgrade))}</b>", Rich(), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                Ui.Label($"<size=11>{upgrade.blurb}</size>", Rich());
                if (next)
                {
                    var why = PublicAquarium.WhyCantUpgradeBooth();
                    GUI.enabled = why == null && !shopDay.IsOpen;
                    string label = shopDay.IsOpen ? "Wait until closing" : why == StoreLayout.NoMoney ? $"Need {Economy.Format(PublicAquarium.BoothUpgradePrice(upgrade) - Economy.Money)} more" : why != null ? "Not yet" : $"Upgrade for {Economy.Format(PublicAquarium.BoothUpgradePrice(upgrade))}";
                    if (Ui.Button(label, UiTheme.Primary)) PublicAquarium.TryUpgradeBooth(store);
                    GUI.enabled = true;
                    if (why != null && why != StoreLayout.NoMoney) Ui.Label($"<size=11>{why}</size>", Rich());
                }
                GUILayout.EndVertical();
            }
        }

        static void Part(string what, float points, float most)
        {
            string color = points > 0.05f ? UiTheme.LeafHex : points < -0.05f ? UiTheme.CoralHex : "#9fb4bc";
            GUILayout.BeginHorizontal();
            Ui.Label($"<size=12>{what}</size>", Rich());
            Ui.Label($"<size=12><color={color}><b>{(points >= 0f ? "+" : "")}{points:0}</b></color>{(most > 0f ? $" / {most:0}" : "")}</size>", Rich(), GUILayout.Width(70f));
            GUILayout.EndHorizontal();
        }

        static void Effect(string what, float change)
        {
            string color = change > 0.0005f ? UiTheme.LeafHex : change < -0.0005f ? UiTheme.CoralHex : "#9fb4bc";
            GUILayout.BeginHorizontal();
            Ui.Label(what, Rich());
            Ui.Label($"<color={color}><b>{(change >= 0f ? "+" : "")}{change * 100f:0}%</b></color>", Rich(), GUILayout.Width(64f));
            GUILayout.EndHorizontal();
        }

        static void Heading(string text) => Ui.Label($"<b><color={UiTheme.SunHex}>{text}</color></b>", Rich());

        static GUIStyle rich, centered;
        static GUIStyle Rich() => rich != null ? rich : rich = new GUIStyle(UiTheme.Skin.label) { richText = true, wordWrap = true };
        static GUIStyle Centered() => centered != null ? centered : centered = new GUIStyle(Rich()) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
    }
}
