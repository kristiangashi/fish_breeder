using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>
    /// The public aquarium's day: it opens and closes with the shop (when it has an exhibit with
    /// fish in it). People turn up at its ticket booth (how many depends on its appeal and rating,
    /// see <see cref="PublicAquarium"/>), queue at the window and either pay or walk off at the
    /// price; those who pay look round the exhibits, react to what they see and leave. Each visit's
    /// score joins the day's report, which moves the rating at closing. The developer "skip day"
    /// plays the rest of the day out with the same rules.
    /// </summary>
    public class AquariumDay : MonoBehaviour
    {
        StoreLayout layout;
        ShopDay shopDay;
        float spawnTimer;
        readonly List<Customer> visitors = new List<Customer>();
        readonly List<Customer> queue = new List<Customer>();

        public IReadOnlyList<Customer> Visitors => visitors;
        /// <summary>Open today: the shop is open and the aquarium had something to see when it opened.</summary>
        public bool IsOpen => shopDay != null && shopDay.IsOpen && PublicAquarium.Today != null;
        public StoreLayout Layout => layout;

        public void Initialize(StoreLayout store, ShopDay day)
        {
            layout = store;
            shopDay = day;
        }

        float SpawnInterval => 3600f / Mathf.Max(0.01f, PublicAquarium.VisitorsPerHour(layout));

        /// <summary>The shop opened: so does the aquarium, if there's anything in its exhibits to see.</summary>
        public void Open()
        {
            PublicAquarium.Today = null;
            PublicAquarium.ForgetAppeal();
            if (!PublicAquarium.CanOpen(layout))
            {
                var section = layout.AquariumSection;
                if (section != null && section.unlocked) GameLog.Add($"The {PublicAquarium.RoomName.ToLower()} stays shut today: {PublicAquarium.WhyClosed(layout).ToLower()}");
                return;
            }
            PublicAquarium.Today = new AquariumReport();
            PublicAquarium.FairPriceToday = PublicAquarium.FairPrice(layout);
            spawnTimer = 3f;
            GameLog.Add($"The {PublicAquarium.RoomName.ToLower()} is open too: tickets {Economy.Format(PublicAquarium.TicketPrice)}.");
        }

        /// <summary>A game saved while open carries on: the day so far came back with the save.</summary>
        public void Resume()
        {
            if (PublicAquarium.Today == null && PublicAquarium.CanOpen(layout)) PublicAquarium.Today = new AquariumReport();
            PublicAquarium.FairPriceToday = PublicAquarium.FairPrice(layout);
            spawnTimer = 3f;
        }

        /// <summary>The shop is closing: everyone inside finishes their visit, the rating moves and the reviews are written. The day's report, or null if it never opened.</summary>
        public AquariumReport Close()
        {
            foreach (var visitor in visitors.ToArray())
                if (visitor != null) visitor.FinishNow();
            visitors.Clear();
            queue.Clear();
            var report = PublicAquarium.Today;
            PublicAquarium.Today = null;
            if (report == null) return null;
            PublicAquarium.EndOfDay(report, Economy.Day);
            GameLog.Add($"The {PublicAquarium.RoomName.ToLower()} closed: {report.tickets} tickets sold ({Economy.Format(report.earnings)}), {report.turnedAway} turned away at the booth. Rating now {PublicAquarium.Stars:0.0} stars.");
            return report;
        }

        /// <summary>Developer shortcut: plays out the rest of the day's visitors at once, with the same rules.</summary>
        public void Skip(float secondsLeft)
        {
            if (!IsOpen) return;
            int remaining = Mathf.RoundToInt(secondsLeft / SpawnInterval);
            var exhibits = OpenExhibits();
            var d = PublicAquarium.Data;
            for (int i = 0; i < remaining && exhibits.Count > 0; i++)
            {
                PublicAquarium.Today.visitors++;
                if (!TryAdmit(layout.BoothWindowPoint, out float willing)) continue;
                int looks = GameRandom.Range(d.minLooks, d.maxLooks + 1);
                float sight = 0f;
                for (int l = 0; l < looks; l++)
                {
                    var look = PublicAquarium.LookAt(exhibits[(l + i) % exhibits.Count].Tank);
                    RecordLook(look);
                    sight += look.score;
                }
                RecordVisit(sight / Mathf.Max(1, looks), willing);
            }
        }

        void Update()
        {
            if (!IsOpen || layout == null) return;
            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;
            spawnTimer = SpawnInterval * GameRandom.Range(0.6f, 1.4f);
            if (visitors.Count >= PublicAquarium.MaxInside || OpenExhibits().Count == 0) return;
            visitors.Add(Customer.SpawnVisitor(this, layout));
            PublicAquarium.Today.visitors++;
        }

        /// <summary>The exhibits with living fish in them, worth walking up to.</summary>
        public List<StoreTank> OpenExhibits()
        {
            var list = new List<StoreTank>();
            foreach (var exhibit in PublicAquarium.Exhibits(layout))
                foreach (var fish in exhibit.Tank.Residents)
                    if (fish != null && !fish.IsDead)
                    {
                        list.Add(exhibit);
                        break;
                    }
            return list;
        }

        /// <summary>
        /// A visitor at the window: they pay if the price is no more than they'd pay today (out:
        /// what that is), or walk off grumbling about it.
        /// </summary>
        public bool TryAdmit(Vector3 window, out float willing)
        {
            willing = PublicAquarium.RollWillingness(PublicAquarium.FairPriceToday);
            var today = PublicAquarium.Today;
            if (today == null) return false;
            int price = PublicAquarium.TicketPrice;
            if (price > willing)
            {
                today.turnedAway++;
                today.issues[(int)AquariumIssue.TooPricey]++;
                // A wasted trip, at half the weight of a visit.
                today.scoreSum += 0.5f * 0.2f;
                return false;
            }
            Economy.Earn(price, "an aquarium ticket", MoneyKind.Tickets);
            today.tickets++;
            today.earnings += price;
            Progression.Award(XpTrack.Shopkeeping, PublicAquarium.Data.xpPerTicket);
            if (shopDay != null) shopDay.Popups.Add(new ShopDay.MoneyPopup { position = window, amount = price });
            Fx.Burst(window + Vector3.up * 0.3f, UiTheme.Sun, 8);
            return true;
        }

        /// <summary>What spoiled a look (or nearly empty tanks), for the day's reviews.</summary>
        public void RecordLook(PublicAquarium.Look look)
        {
            var today = PublicAquarium.Today;
            if (today == null || (!look.spoiled && look.issue != AquariumIssue.FewFish)) return;
            today.issues[(int)look.issue]++;
        }

        /// <summary>A visitor leaves: what they saw (0..1) and what they paid against what they'd have paid make the visit's score.</summary>
        public float RecordVisit(float sight, float willing)
        {
            float score = PublicAquarium.VisitScore(sight, willing, PublicAquarium.TicketPrice);
            var today = PublicAquarium.Today;
            if (today == null) return score;
            today.scoreSum += score;
            if (score >= PublicAquarium.Data.happyScore)
            {
                today.happy++;
                Progression.Award(XpTrack.Shopkeeping, PublicAquarium.Data.xpHappyVisitor);
            }
            else if (score < PublicAquarium.Data.unhappyScore) today.unhappy++;
            return score;
        }

        public void Remove(Customer visitor)
        {
            visitors.Remove(visitor);
            queue.Remove(visitor);
        }

        public void JoinQueue(Customer visitor)
        {
            if (!queue.Contains(visitor)) queue.Add(visitor);
        }

        public void LeaveQueue(Customer visitor) => queue.Remove(visitor);

        public int QueuePlace(Customer visitor) => Mathf.Max(0, queue.IndexOf(visitor));
    }
}
