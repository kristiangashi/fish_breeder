using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Genetics;
using FishBreeder.Shop;
using UnityEngine;

namespace FishBreeder.Store
{
    /// <summary>
    /// The public aquarium window (U, the Aquarium button, or click the ticket booth). Left: the
    /// rating and what it does to visitor numbers, each exhibit's appeal piece by piece, and the
    /// latest reviews. Right: the ticket price, the booth's estimate of the day at that price
    /// (a little off until the booth has all three upgrades), and the booth upgrades. Drawn in the
    /// #aquarium-window (Window.uxml, styled in FishBreeder.uss).
    /// </summary>
    public class AquariumView : MonoBehaviour
    {
        static bool open;
        static StoreLayout store;
        static ShopDay shopDay;

        UiWindow window;
        UiFrame ui;

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

        void Awake()
        {
            open = false;
            window = new UiWindow(transform, "aquarium-window", PublicAquarium.RoomName, 10, true, Close);
            window.CloseButton.text = "Close (U / Esc)";
            ui = window.Frame;
        }

        void OnDestroy() => window?.Destroy();

        void OnDisable() => window?.Show(false);

        void Update()
        {
            if (!ViewMode.Watching && !UiPanels.Typing && Tank.Focused == null && Input.GetKeyDown(KeyCode.U)) Toggle();
            bool show = open && !ViewMode.Watching && store != null;
            window.Show(show);
            if (show) window.Fill(DrawHeader, DrawBody);
        }

        void DrawHeader()
        {
            ui.Label($"<size=22><color={UiTheme.SunHex}>{ReputationView.Stars(PublicAquarium.Stars)}</color></size>  <b>{PublicAquarium.Stars:0.0}</b> / 5", "fb-text fb-header-text");
            ui.Flex();
#if UNITY_EDITOR
            if (ui.Button("+0.5 star (dev)", "fb-button fb-button--small")) PublicAquarium.DevChangeRating(0.1f);
            if (ui.Button("-0.5 star (dev)", "fb-button fb-button--small")) PublicAquarium.DevChangeRating(-0.1f);
#endif
        }

        void DrawBody()
        {
            ui.Label("People pay at the booth to see the two exhibits, then leave. The better the exhibits look, the more people come and the more a ticket is worth. Every visitor leaves with an opinion of what they saw and what they paid; the day's visits move the aquarium's rating, and the rating multiplies how many come.", "fb-text fb-text--small");
            ui.BeginRow("fb-halves");
            ui.BeginScroll("fb-scroll fb-half--left");
            DrawVisitors();
            DrawExhibits();
            DrawLimits();
            DrawReviews();
            ui.Pop();
            ui.BeginScroll();
            DrawTickets();
            DrawBooth();
            ui.Pop();
            ui.Pop();
        }

        void DrawVisitors()
        {
            ui.Section("VISITORS");
            var why = PublicAquarium.WhyClosed(store);
            if (why != null) ui.Label($"<color={UiTheme.SunHex}>Closed: {why}</color>");
            else ui.Label($"About <b>{PublicAquarium.VisitorsPerHour(store):0}</b> people an hour come to the booth while the shop is open (from {PublicAquarium.Data.visitorsPerHour:0} to start with).");
            float appeal = PublicAquarium.AquariumAppeal(store);
            Effect($"Appeal {appeal:0}/100 (the better exhibit counts 65%, the other 35%)", PublicAquarium.AppealMultiplier(appeal) - 1f);
            int kinds = PublicAquarium.AquariumKinds(store);
            float bonus = PublicAquarium.KindBonus(store);
            ui.Label($"{kinds} kind{(kinds == 1 ? "" : "s")} of fish in the whole aquarium{(bonus > 0.05f ? $": +{bonus:0} appeal for the variety" : $". Past {PublicAquarium.Data.kindsBeforeBonus}, every kind adds appeal: people come to see lots of different fish.")}", "fb-small");
            Effect($"{PublicAquarium.Stars:0.0} star rating", PublicAquarium.RatingMultiplier - 1f);
            var today = PublicAquarium.Today;
            if (today != null)
                ui.Label($"Today so far: {today.visitors} came, {today.tickets} bought tickets ({Economy.Format(today.earnings)}), {today.turnedAway} turned away at the price, {today.happy} left happy.", "fb-text fb-text--small");
        }

        void DrawExhibits()
        {
            ui.Section("THE EXHIBITS");
            var exhibits = PublicAquarium.Exhibits(store);
            foreach (var climate in new[] { ExhibitClimate.Temperate, ExhibitClimate.Tropical })
            {
                var exhibit = exhibits.Find(x => x.Climate == climate);
                ui.BeginCard();
                string name = $"{PublicAquarium.ClimateName(climate)} aquarium";
                string where = climate == ExhibitClimate.Temperate ? "north wall, cold-water fish" : "east wall, warm-water fish";
                if (exhibit == null)
                {
                    ui.Label($"<b>{name}</b>  <size=11>({where})</size>\n<size=11>Not built yet: click its wall in the aquarium to buy it ({Economy.Format(StoreLayout.TankPrice(null, TankRole.Exhibit))}).</size>");
                    ui.Pop();
                    continue;
                }
                var a = PublicAquarium.ExhibitAppeal(exhibit.Tank);
                ui.Label($"<b>{exhibit.Title}</b>  <size=11>({where})</size>   appeal <b>{a.total:0}</b>/100");
                if (a.fish == 0 && exhibit.Tank.Residents.Count == 0) ui.Label("<i>No fish yet. Visitors only come when there's something to see.</i>", "fb-small");
                var d = PublicAquarium.Data;
                var sizes = new List<string>();
                foreach (var size in new[] { AquariumSize.Small, AquariumSize.Medium, AquariumSize.Large })
                    sizes.Add($"{PublicAquarium.SizeName(size).ToLower()} {PublicAquarium.CountOfSize(exhibit.Tank, size)}/{PublicAquarium.ExhibitLimit(exhibit.Tank, size)}");
                ui.Label($"Fish by size: {string.Join(", ", sizes)}", "fb-small");
                Part($"{a.fish} juveniles and adults", a.fullness, d.fullnessPoints);
                Part($"{a.kinds} kind{(a.kinds == 1 ? "" : "s")} of fish ({d.fullVarietyKinds} for full marks)", a.variety, d.varietyPoints);
                if (a.samey < 0f) Part("Mostly one kind: samey", a.samey, 0f);
                Part("How eye-catching they are", a.looks, d.looksPoints);
                Part($"Aquarium quality {exhibit.Tank.Quality}", a.quality, d.qualityPoints);
                Part("Clear, clean water", a.clean, d.cleanPoints);
                if (a.dead < 0f) Part("Dead fish", a.dead, 0f);
                if (a.dirty < 0f) Part("Dirty water or algae", a.dirty, 0f);
                if (a.sick < 0f) Part("Sick fish", a.sick, 0f);
                if (a.fighting < 0f) Part("Fish fighting", a.fighting, 0f);
                ui.Pop();
            }
        }

        /// <summary>
        /// Each kind in the aquarium and the most of it the player wants: grown fish past the limit
        /// (babies grown up, usually) go to the fish market at the next time step, picked at random.
        /// </summary>
        void DrawLimits()
        {
            ui.Section("KINDS AND LIMITS");
            ui.Label("Set the most of each kind you want here. When babies grow up and a kind goes over its limit, grown ones go to the fish market (picked at random) at the next time step. Fish in the exhibits and holding tanks can also be sold on the market or let go from the fish's window.", "fb-small");
            var ids = new List<string>();
            foreach (var exhibit in PublicAquarium.Exhibits(store))
                foreach (var fish in exhibit.Tank.Residents)
                    if (fish != null && !fish.IsDead && !ids.Contains(fish.Species.id)) ids.Add(fish.Species.id);
            if (ids.Count == 0)
            {
                ui.Label("<i>No fish in the exhibits yet.</i>", "fb-small");
                return;
            }
            bool canChange = !shopDay.IsOpen;
            foreach (var id in ids)
            {
                var species = Species.Get(id);
                int have = PublicAquarium.CountInAquarium(store, id), limit = PublicAquarium.SpeciesLimit(id);
                ui.BeginRow();
                string over = limit > 0 && have > limit ? $"<color={UiTheme.CoralHex}>" : "<color=#ffffff>";
                ui.Label($"<size=12>{species.displayName}  <size=11>({PublicAquarium.SizeName(PublicAquarium.SizeClass(species)).ToLower()})</size>: {over}{have}</color></size>", "fb-text fb-grow");
                if (ui.Button("-", "fb-button fb-step", canChange)) PublicAquarium.SetSpeciesLimit(id, limit <= 0 ? Mathf.Max(1, have - 1) : limit - 1);
                ui.Label($"<size=12>{(limit > 0 ? $"most {limit}" : "no limit")}</size>", "fb-text fb-step-value");
                if (ui.Button("+", "fb-button fb-step", canChange)) PublicAquarium.SetSpeciesLimit(id, limit <= 0 ? have + 1 : limit + 1);
                if (ui.Button("Any", "fb-button fb-step", canChange)) PublicAquarium.SetSpeciesLimit(id, 0);
                ui.Pop();
            }
        }

        void DrawReviews()
        {
            ui.Section("LATEST REVIEWS");
            var reviews = PublicAquarium.Reviews;
            if (reviews.Count == 0) ui.Label("<i>No reviews yet.</i>");
            foreach (var review in reviews)
                ui.Label($"<size=11><color={UiTheme.SunHex}>{ReputationView.Stars(review.stars)}</color>  Day {review.day}</size>\n<i>\"{review.text}\"</i>");
        }

        void DrawTickets()
        {
            ui.Section("TICKET PRICE");
            bool canChange = !shopDay.IsOpen;
            int price = PublicAquarium.TicketPrice;
            ui.BeginRow("fb-row fb-center-row");
            if (ui.Button("-5", "fb-button fb-step", canChange)) PublicAquarium.SetTicketPrice(price - 5);
            if (ui.Button("-1", "fb-button fb-step", canChange)) PublicAquarium.SetTicketPrice(price - 1);
            ui.Label($"<size=22><b>{Economy.Format(PublicAquarium.TicketPrice)}</b></size>", "fb-text fb-step-value");
            if (ui.Button("+1", "fb-button fb-step", canChange)) PublicAquarium.SetTicketPrice(price + 1);
            if (ui.Button("+5", "fb-button fb-step", canChange)) PublicAquarium.SetTicketPrice(price + 5);
            ui.Pop();
            if (!canChange) ui.Label("<i>The price is on the board for today: change it once the shop has closed.</i>", "fb-small");

            ui.Section("THE BOOTH'S ESTIMATE");
            var guess = PublicAquarium.Guess(store, PublicAquarium.TicketPrice);
            float range = PublicAquarium.EstimatorRange;
            string sure = range <= 0.001f ? $"<color={UiTheme.LeafHex}>spot on</color>" : $"could be off by up to {range * 100f:0}% either way today";
            ui.BeginCard();
            ui.Label($"A fair ticket price: about <b>{Economy.Format(Mathf.Max(1, Mathf.RoundToInt(guess.fairPrice)))}</b>");
            ui.Label($"At {Economy.Format(PublicAquarium.TicketPrice)}: about <b>{guess.visitors:0}</b> people today, <b>{guess.tickets:0}</b> buying tickets, <b>{Economy.Format(Mathf.RoundToInt(guess.earnings))}</b>");
            int best = PublicAquarium.BestPriceGuess(store);
            ui.Label($"Most ticket money at about {Economy.Format(best)}, but a lower price leaves people happier, and happy visitors raise the rating.", "fb-small");
            ui.Label($"The estimate {sure}. Upgrade the booth to make it surer.", "fb-small");
            ui.BeginRow("fb-row fb-wrap");
            if (ui.Button($"Use {Economy.Format(best)}", "fb-button fb-button--gap", canChange && best != PublicAquarium.TicketPrice)) PublicAquarium.SetTicketPrice(best);
            int fair = Mathf.Max(1, Mathf.RoundToInt(guess.fairPrice));
            if (ui.Button($"Use the fair price ({Economy.Format(fair)})", "fb-button fb-button--gap", canChange && fair != PublicAquarium.TicketPrice)) PublicAquarium.SetTicketPrice(fair);
            ui.Pop();
            ui.Pop();
            ui.Label("Above what a visitor will pay, they walk off at the booth and grumble about it. Below it, the cheaper the ticket, the better they feel about the visit.", "fb-small");
        }

        void DrawBooth()
        {
            ui.Section("TICKET BOOTH UPGRADES");
            var upgrades = PublicAquarium.Data.boothUpgrades;
            ui.Label($"Up to {PublicAquarium.MaxInside} visitors inside at once.", "fb-small");
            for (int i = 0; upgrades != null && i < upgrades.Count; i++)
            {
                var upgrade = upgrades[i];
                bool owned = i < PublicAquarium.BoothLevel, next = i == PublicAquarium.BoothLevel;
                ui.BeginCard(next);
                var errors = PublicAquarium.Data.estimatorError;
                float after = errors != null && errors.Count > 0 ? errors[Mathf.Min(i + 1, errors.Count - 1)] : 0f;
                ui.Line($"<b>{i + 1}. {upgrade.name}</b>  <size=11>{(owned ? $"<color={UiTheme.LeafHex}>done</color>" : $"estimate within {after * 100f:0}%, +{upgrade.extraVisitors} inside")}</size>",
                    owned ? "" : $"<b>{Economy.Format(PublicAquarium.BoothUpgradePrice(upgrade))}</b>");
                ui.Label(upgrade.blurb, "fb-small");
                if (next)
                {
                    var why = PublicAquarium.WhyCantUpgradeBooth();
                    string label = shopDay.IsOpen ? "Wait until closing" : why == StoreLayout.NoMoney ? $"Need {Economy.Format(PublicAquarium.BoothUpgradePrice(upgrade) - Economy.Money)} more" : why != null ? "Not yet" : $"Upgrade for {Economy.Format(PublicAquarium.BoothUpgradePrice(upgrade))}";
                    if (ui.Button(label, "fb-button fb-button--primary", why == null && !shopDay.IsOpen)) PublicAquarium.TryUpgradeBooth(store);
                    if (why != null && why != StoreLayout.NoMoney) ui.Label(why, "fb-small");
                }
                ui.Pop();
            }
        }

        void Part(string what, float points, float most)
        {
            string color = points > 0.05f ? UiTheme.LeafHex : points < -0.05f ? UiTheme.CoralHex : "#9fb4bc";
            ui.Line($"<size=12>{what}</size>", $"<size=12><color={color}><b>{(points >= 0f ? "+" : "")}{points:0}</b></color>{(most > 0f ? $" / {most:0}" : "")}</size>");
        }

        void Effect(string what, float change)
        {
            string color = change > 0.0005f ? UiTheme.LeafHex : change < -0.0005f ? UiTheme.CoralHex : "#9fb4bc";
            ui.Line(what, $"<color={color}><b>{(change >= 0f ? "+" : "")}{change * 100f:0}%</b></color>");
        }
    }
}
