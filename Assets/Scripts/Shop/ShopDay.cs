using System.Collections;
using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>What happened during one open-shop day, for the end-of-day report.</summary>
    public class DayReport
    {
        public int day;
        public int visitors, fishSold, earnings;
        public int happy, grossed, sad, mad;
        public float stars;
        public string review;
        public string overnight;
        public float reputationChange;
    }

    /// <summary>
    /// The open-shop phase. Opening runs a time step and starts a day of customers (one real hour by
    /// default); when the time is up the shop closes on its own, runs another time step, starts the
    /// next day and shows the day's report. Developer controls speed it up or skip it.
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

        public bool IsOpen { get; private set; }
        public float TimeLeft { get; private set; }
        public IReadOnlyList<Customer> Customers => customers;
        public List<MoneyPopup> Popups => popups;
        public DayReport Today => today;
        /// <summary>The report of the day that just ended, until the player dismisses it.</summary>
        public DayReport LastReport { get; set; }
        public float SpeedMultiplier { get; private set; } = 1f;

        public void Initialize(StoreLayout store) => layout = store;

        public float CustomersPerHour
        {
            get
            {
                var shop = Balance.Data.shop;
                int display = layout.CountTanks(TankRole.Display);
                int other = layout.Tanks.Count - display;
                float traffic = 1f + display * shop.trafficPerDisplayTank + other * shop.trafficPerOtherTank;
                return shop.customersPerHour * traffic * Mathf.Lerp(0.5f, 1.5f, Economy.Reputation);
            }
        }

        float SpawnInterval => 3600f / Mathf.Max(1f, CustomersPerHour);

        public void Open()
        {
            if (IsOpen) return;
            GameLog.Add("You flip the sign to OPEN. Overnight in the tanks:");
            string overnight = TimeStep.Run(layout.Tanks);
            IsOpen = true;
            TimeLeft = Balance.Data.shop.dayLengthSeconds;
            spawnTimer = 2f;
            today = new DayReport { day = Economy.Day, overnight = overnight };
            layout.BuildNavMesh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            foreach (var customer in customers.ToArray())
                if (customer != null) customer.FinishNow();
            customers.Clear();
            registerQueue.Clear();
            foreach (var bag in bags) if (bag != null) Destroy(bag);
            bags.Clear();
            IsOpen = false;
            SetSpeed(1f);

            FinishReport(today);
            GameLog.Add($"Closed for the day: {today.visitors} visitors, {today.fishSold} fish sold, {Economy.Format(today.earnings)} earned.");
            GameLog.Add("While you closed up:");
            today.overnight = TimeStep.Run(layout.Tanks);
            Economy.NextDay();
            LastReport = today;
            today = null;
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
                int visits = GameRandom.Range(shop.minTankVisits, shop.maxTankVisits + 1);
                for (int v = 0; v < visits && layout.Tanks.Count > 0; v++)
                {
                    var reaction = ShopperBrain.Evaluate(profile, layout.Tanks[GameRandom.Range(0, layout.Tanks.Count)]);
                    RecordMood(reaction.mood);
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
                if (customers.Count < Balance.Data.shop.maxCustomersInStore && layout.Tanks.Count > 0)
                {
                    customers.Add(Customer.Spawn(this, layout));
                    today.visitors++;
                }
            }
        }

        public void RecordMood(Mood mood)
        {
            if (today == null) return;
            switch (mood)
            {
                case Mood.Happy: today.happy++; break;
                case Mood.Grossed: today.grossed++; break;
                case Mood.Sad: today.sad++; break;
                case Mood.Mad: today.mad++; break;
            }
        }

        public void Sell(Fish fish, Vector3 registerPoint)
        {
            int price = fish.Price;
            string name = $"{fish.FishName} the {fish.Species}";
            fish.RemoveFromTank();
            Economy.Earn(price, $"sold {name}");
            popups.Add(new MoneyPopup { position = registerPoint, amount = price });
            if (today != null)
            {
                today.fishSold++;
                today.earnings += price;
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
            Economy.ChangeReputation(report.reputationChange);

            if (report.visitors == 0) report.review = "Nobody came in today. More tanks, especially display tanks, bring in more people.";
            else if (bad == 0 && report.fishSold > 0) report.review = "\"Lovely little shop, healthy fish. Will be back!\"";
            else if (report.sad >= report.grossed && report.sad >= report.mad && report.sad > 0) report.review = "\"There were dead fish floating in the tanks. So sad.\"";
            else if (report.grossed >= report.mad && report.grossed > 0) report.review = "\"The water looked filthy. Do they ever clean those tanks?\"";
            else if (report.mad > 0) report.review = "\"Nice fish, but the prices are ridiculous.\"";
            else report.review = "\"Nice enough. Not much for sale though.\"";
        }
    }
}
