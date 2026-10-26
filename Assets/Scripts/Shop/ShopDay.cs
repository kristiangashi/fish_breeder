using System.Collections;
using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>The three parts of a day.</summary>
    public enum DayPhase
    {
        /// <summary>Getting the tanks ready before opening. No time has passed since the night.</summary>
        Morning,
        /// <summary>Customers are in. Time passes, and its events land when the shop closes.</summary>
        Open,
        /// <summary>After closing: deal with the day, then settle the fish in for the night.</summary>
        Evening,
    }

    /// <summary>What happened during the day (shown in the evening) or overnight (shown in the morning).</summary>
    public class DayReport
    {
        public int day;
        public int visitors, fishSold, earnings;
        public int happy, grossed, sad, mad;
        public float stars;
        /// <summary>The day's reviews, worst complaint first (praise first on a good day).</summary>
        public List<string> reviews = new List<string>();
        /// <summary>How many shoppers complained about each thing (indexed by <see cref="Complaint"/>).</summary>
        public int[] complaints = new int[Reputation.Count];
        /// <summary>What happened in the tanks: during the day for an evening report, overnight for a morning one.</summary>
        public string tanks;
        public float reputationChange;
        /// <summary>The shop stayed closed: time passed, but no customers came.</summary>
        public bool stayedClosed;
        /// <summary>The morning report, after the night.</summary>
        public bool morning;
    }

    /// <summary>
    /// The day in three parts. <b>Morning</b>: feed, clean and get ready; opening the shop doesn't
    /// move time on, so nothing changes between getting ready and opening. <b>Open</b>: customers
    /// come in for a day (one real hour by default) and time passes; when the shop closes, the day's
    /// time step runs (births, deaths, growth, illness) and the day's report shows. <b>Evening</b>:
    /// deal with what happened and settle the fish in; going to sleep runs the night's time step and
    /// starts the next morning with a report of the night. Instead of opening, the player can keep
    /// the shop closed: the day still passes, with nobody coming in. For the first few days (the setup
    /// days) that's the only choice. Developer controls speed the open shop up or skip it.
    /// </summary>
    public class ShopDay : MonoBehaviour
    {
        public struct MoneyPopup
        {
            public Vector3 position;
            public int amount;
            public float age;
        }

        StoreLayout layout;
        float spawnTimer;
        readonly List<Customer> customers = new List<Customer>();
        readonly List<Customer> registerQueue = new List<Customer>();
        readonly List<MoneyPopup> popups = new List<MoneyPopup>();
        readonly List<GameObject> bags = new List<GameObject>();
        DayReport today;

        public DayPhase Phase { get; private set; } = DayPhase.Morning;
        public bool IsOpen => Phase == DayPhase.Open;
        public float TimeLeft { get; private set; }
        public IReadOnlyList<Customer> Customers => customers;
        public List<MoneyPopup> Popups => popups;
        public DayReport Today => today;
        /// <summary>The report of the day (evening) or night (morning) that just ended, until the player dismisses it.</summary>
        public DayReport LastReport { get; set; }
        public float SpeedMultiplier { get; private set; } = 1f;

        public void Initialize(StoreLayout store) => layout = store;

        /// <summary>The parts foot traffic is made of, for the reputation screen.</summary>
        public struct Traffic
        {
            public float baseRate;
            /// <summary>Extra from display tanks, other shop-floor tanks and decorations (0.15 = +15%).</summary>
            public float display, otherTanks, decorations;
            public int displayCount, otherCount, decorationCount;
            /// <summary>Multipliers: reputation (stars), perks, and complaints.</summary>
            public float reputation, perks, complaints;
            public float PerHour => baseRate * (1f + display + otherTanks + decorations) * reputation * perks * complaints;
        }

        public Traffic TrafficParts
        {
            get
            {
                var shop = Balance.Data.shop;
                var t = new Traffic { baseRate = shop.customersPerHour };
                t.displayCount = layout.CountTanks(TankRole.Display);
                t.otherCount = layout.PublicTanks.Count - t.displayCount;
                foreach (var tank in layout.PublicTanks) t.decorationCount += tank.Tank.DecorCount;
                t.display = t.displayCount * shop.trafficPerDisplayTank * Perks.More(PerkEffect.TankTraffic);
                t.otherTanks = t.otherCount * shop.trafficPerOtherTank * Perks.More(PerkEffect.TankTraffic);
                t.decorations = t.decorationCount * shop.trafficPerDecoration * Perks.More(PerkEffect.DecorTraffic);
                t.reputation = Mathf.Lerp(0.5f, 1.5f, Economy.Reputation);
                t.perks = Perks.More(PerkEffect.MoreCustomers);
                t.complaints = Reputation.TrafficMultiplier;
                return t;
            }
        }

        public float CustomersPerHour => TrafficParts.PerHour;

        float SpawnInterval => 3600f / Mathf.Max(1f, CustomersPerHour);

        /// <summary>Opens the shop for the day. No time passes: the tanks are just as you left them.</summary>
        public void Open()
        {
            if (Phase != DayPhase.Morning) return;
            if (!Economy.CanOpenShop)
            {
                GameLog.Add($"The shop isn't ready to open yet: {Economy.DaysUntilOpening} more days to raise some fish to sell.");
                return;
            }
            GameLog.Add("You flip the sign to OPEN.");
            Phase = DayPhase.Open;
            TimeLeft = Balance.Data.shop.dayLengthSeconds;
            spawnTimer = 2f;
            today = new DayReport { day = Economy.Day };
            layout.BuildNavMesh();
            Milestones.Add(MilestoneKind.OpenShop);
            Fx.Stamp("OPEN!", UiTheme.Leaf);
        }

        /// <summary>
        /// Keeps the shop closed today: the day's time step runs straight away (fish grow, eggs
        /// hatch, water gets dirtier, and so on), but no customers come and nothing is sold. It's
        /// evening afterwards.
        /// </summary>
        public void StayClosed()
        {
            if (Phase != DayPhase.Morning) return;
            var report = new DayReport { day = Economy.Day, stayedClosed = true };
            GameLog.Add(Economy.CanOpenShop ? "You keep the shop closed today. During the day in the tanks:" : "Another day of getting ready. During the day in the tanks:");
            report.tanks = TimeStep.Run(layout.Tanks);
            // Nobody came in, so yesterday's complaints fade a little.
            Reputation.EndOfDay(0, null, 0f, null, report.day);
            StartEvening(report);
        }

        public void Close()
        {
            if (Phase != DayPhase.Open) return;
            foreach (var customer in customers.ToArray())
                if (customer != null) customer.FinishNow();
            customers.Clear();
            registerQueue.Clear();
            foreach (var bag in bags) if (bag != null) Destroy(bag);
            bags.Clear();
            SetSpeed(1f);

            FinishReport(today);
            GameLog.Add($"Closed for the day: {today.visitors} visitors, {today.fishSold} fish sold, {Economy.Format(today.earnings)} earned.");
            GameLog.Add("During the day in the tanks:");
            today.tanks = TimeStep.Run(layout.Tanks);
            Progression.Award(XpTrack.Shopkeeping, Mathf.RoundToInt(Balance.Data.progression.xpPerReviewStar * today.stars));
            var report = today;
            today = null;
            StartEvening(report);
        }

        void StartEvening(DayReport report)
        {
            Phase = DayPhase.Evening;
            LastReport = report;
            Fx.Day("Evening", "See to the day's events, then settle the fish in for the night");
        }

        /// <summary>Ends the evening: the night's time step runs and the next morning starts, with a report of the night.</summary>
        public void GoToSleep()
        {
            if (Phase != DayPhase.Evening) return;
            GameLog.Add("You turn the lights off for the night. Overnight in the tanks:");
            string night = TimeStep.Run(layout.Tanks);
            Research.OnNight();
            Economy.NextDay();
            Progression.NewDailyGoals();
            Phase = DayPhase.Morning;
            LastReport = new DayReport { day = Economy.Day, morning = true, tanks = night };
        }

        /// <summary>Developer shortcut: plays out the rest of the day instantly with the same shopper rules, then closes.</summary>
        public void SkipRestOfDay()
        {
            if (!IsOpen) return;
            int remaining = Mathf.RoundToInt(TimeLeft / SpawnInterval);
            var shop = Balance.Data.shop;
            for (int i = 0; i < remaining; i++)
            {
                today.visitors++;
                var profile = ShopperProfile.Random();
                int visits = GameRandom.Range(shop.minTankVisits, shop.maxTankVisits + 1) + Perks.Count(PerkEffect.ExtraTankVisits);
                var tanks = layout.PublicTanks;
                for (int v = 0; v < visits && tanks.Count > 0; v++)
                {
                    var reaction = ShopperBrain.Evaluate(profile, tanks[GameRandom.Range(0, tanks.Count)]);
                    Record(reaction);
                    if (reaction.purchase != null)
                    {
                        Sell(reaction.purchase, layout.RegisterPoint);
                        break;
                    }
                }
            }
            Close();
        }

        public void SetSpeed(float multiplier)
        {
            SpeedMultiplier = multiplier;
            Time.timeScale = multiplier;
        }

        void Update()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var popup = popups[i];
                popup.age += Time.unscaledDeltaTime;
                if (popup.age > 2.2f) popups.RemoveAt(i);
                else popups[i] = popup;
            }

            if (!IsOpen) return;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft <= 0f)
            {
                Close();
                return;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                spawnTimer = SpawnInterval * GameRandom.Range(0.6f, 1.4f);
                if (customers.Count < Balance.Data.shop.maxCustomersInStore + Perks.Count(PerkEffect.BiggerCrowds) && layout.PublicTanks.Count > 0)
                {
                    customers.Add(Customer.Spawn(this, layout));
                    today.visitors++;
                }
            }
        }

        /// <summary>A shopper's reaction to a tank: their mood, and anything they'll complain about.</summary>
        public void Record(Reaction reaction)
        {
            if (today == null) return;
            if (reaction.complained) today.complaints[(int)reaction.complaint]++;
            switch (reaction.mood)
            {
                case Mood.Happy:
                    today.happy++;
                    Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpHappyCustomer);
                    Progression.Count(GoalKind.HappyCustomers);
                    break;
                case Mood.Grossed: today.grossed++; break;
                case Mood.Sad: today.sad++; break;
                case Mood.Mad: today.mad++; break;
            }
        }

        public void Sell(Fish fish, Vector3 registerPoint)
        {
            int price = fish.Price;
            string name = $"{fish.FishName} the {fish.SpeciesLabel}";
            Finance.RecordSale(fish.SpeciesLabel, fish.Tank != null ? fish.Tank.Title : "?", price);
            fish.RemoveFromTank();
            Economy.Earn(price, $"sold {name}", MoneyKind.Sales);
            int tip = Mathf.RoundToInt(price * Perks.Value(PerkEffect.SaleTips));
            if (tip > 0) Economy.Earn(tip, $"tip for {name}", MoneyKind.Tips);
            var progression = Balance.Data.progression;
            Progression.Award(XpTrack.Shopkeeping, progression.xpSaleBase + Mathf.RoundToInt(price * progression.xpPerSaleDollar));
            Progression.Count(GoalKind.SellFish);
            Progression.Count(GoalKind.EarnMoney, price);
            popups.Add(new MoneyPopup { position = registerPoint, amount = price + tip });
            Fx.Burst(registerPoint + Vector3.up * 0.4f, UiTheme.Sun, 16);
            if (today != null)
            {
                today.fishSold++;
                today.earnings += price + tip;
            }
        }

        public void Remove(Customer customer)
        {
            customers.Remove(customer);
            registerQueue.Remove(customer);
        }

        public void JoinQueue(Customer customer)
        {
            if (!registerQueue.Contains(customer)) registerQueue.Add(customer);
        }

        public void LeaveQueue(Customer customer) => registerQueue.Remove(customer);

        public int QueuePlace(Customer customer) => Mathf.Max(0, registerQueue.IndexOf(customer));

        /// <summary>The shopkeeper bags the fish and hands it over the counter.</summary>
        public IEnumerator HandOverBag(Customer customer, Transform customerHand)
        {
            if (People.Shopkeeper.Instance != null) People.Shopkeeper.Instance.HandOver(customer);
            var bag = MaterialFactory.Decor(PrimitiveType.Cube, "Bag", null, layout.ShopkeeperHand, new Vector3(0.2f, 0.26f, 0.1f), new Color(0.93f, 0.9f, 0.8f));
            var water = MaterialFactory.Decor(PrimitiveType.Sphere, "Water", bag.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.9f, 0.5f, 0.9f), new Color(0.5f, 0.75f, 0.95f));
            water.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            bags.Add(bag);
            yield return new WaitForSeconds(0.6f);
            var start = layout.ShopkeeperHand;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
            {
                var end = customerHand.position;
                bag.transform.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
                yield return null;
            }
            bag.transform.SetParent(customerHand, false);
            bag.transform.localPosition = Vector3.zero;
            yield return new WaitForSeconds(0.3f);
        }

        void FinishReport(DayReport report)
        {
            var shop = Balance.Data.shop;
            int good = report.happy + report.fishSold * 2;
            int bad = report.grossed + report.sad + report.mad;
            float score = good + bad == 0 ? 0.5f : good / (float)(good + bad);
            report.stars = Mathf.Round(Mathf.Lerp(1f, 5f, score) * 2f) / 2f;
            report.reputationChange = (score - 0.5f) * 2f * shop.reputationChangePerDay;
            report.reputationChange *= report.reputationChange > 0f ? Perks.More(PerkEffect.ReputationGain) : Perks.Less(PerkEffect.ReputationShield);
            Economy.ChangeReputation(report.reputationChange);

            report.reviews = Reputation.Write(report.visitors, report.fishSold, report.happy, report.complaints);
            Reputation.EndOfDay(report.visitors, report.complaints, report.stars, report.reviews, report.day);
        }
    }
}
