using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishBreeder.Feeding;
using FishBreeder.Shop;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// A scripted player for whole days: the chores before opening and after closing. The dev
    /// fast-forward and the pacing simulator both drive the day with <see cref="DevFastForward.Run"/>
    /// and let a bot do the player's part, through <see cref="PlayerActions"/> so it plays by the
    /// game's own rules. Any class with a parameterless constructor that implements this shows up
    /// in the fast-forward's bot list by itself.
    /// </summary>
    public interface IDayBot
    {
        /// <summary>Shown in the dev window.</summary>
        string Name { get; }
        /// <summary>Morning, before opening (or before letting a setup day pass): feed, clean, buy, move, price.</summary>
        void Morning();
        /// <summary>Evening, after closing: see to what the day did, before going to sleep.</summary>
        void Evening();
    }

    /// <summary>
    /// Developer fast-forward: plays N whole days as if the player had played them, through the real
    /// day (open, the dev "skip rest of day" with the same shopper rules, close, sleep) with a bot
    /// doing the chores. For trying the game mid-way through without playing up to there. It plays
    /// on the current game, so the save moves on too.
    /// </summary>
    public static class DevFastForward
    {
        /// <summary>A fast-forward is running (from the dev window).</summary>
        public static bool Running { get; private set; }
        /// <summary>Days played so far and asked for, while <see cref="Running"/>.</summary>
        public static int Done { get; private set; }
        public static int Asked { get; private set; }
        static bool stopAsked;

        /// <summary>
        /// Plays <paramref name="days"/> whole days straight away, one after the other: whatever
        /// part of the day it is now is finished first, and it stops at the morning of the last
        /// day. Returns how many days it played.
        /// </summary>
        public static int Run(ShopDay shopDay, IDayBot bot, int days)
        {
            if (shopDay == null || bot == null) return 0;
            int played = 0;
            for (int i = 0; i < days; i++)
            {
                if (!PlayDay(shopDay, bot)) break;
                played++;
            }
            return played;
        }

        /// <summary>
        /// Plays from wherever the day is now to the next morning: morning chores, open (or let a
        /// setup day pass), the rest of the day at once, evening chores, sleep. False if it got
        /// stuck (shouldn't happen).
        /// </summary>
        public static bool PlayDay(ShopDay shopDay, IDayBot bot)
        {
            int day = Economy.Day;
            // Each part of the day is at most one step; a few spare steps guard against a loop.
            for (int guard = 0; guard < 6 && Economy.Day == day; guard++)
            {
                switch (shopDay.Phase)
                {
                    case DayPhase.Morning:
                        Safely(bot.Morning, bot, "morning");
                        if (Economy.CanOpenShop)
                        {
                            shopDay.Open();
                            if (shopDay.IsOpen) shopDay.SkipRestOfDay();
                            else shopDay.StayClosed();
                        }
                        else shopDay.StayClosed();
                        break;
                    case DayPhase.Open:
                        shopDay.SkipRestOfDay();
                        break;
                    case DayPhase.Evening:
                        Safely(bot.Evening, bot, "evening");
                        shopDay.GoToSleep();
                        break;
                }
            }
            return Economy.Day != day;
        }

        /// <summary>A bot that throws shouldn't stop the day: the day goes on without its chores.</summary>
        static void Safely(Action chores, IDayBot bot, string when)
        {
            try { chores(); }
            catch (Exception e) { Debug.LogError($"Fast-forward: the {bot.Name} bot failed in the {when}: {e}"); }
        }

        /// <summary>
        /// The dev window's version: one day a frame, so the game keeps drawing and the window can
        /// show how far it's got (and stop it). Saves at the end.
        /// </summary>
        public static IEnumerator RunOverFrames(ShopDay shopDay, IDayBot bot, int days)
        {
            if (Running || shopDay == null || bot == null) yield break;
            Running = true;
            stopAsked = false;
            Done = 0;
            Asked = days;
            int startDay = Economy.Day, startMoney = Economy.Money;
            GameLog.Add($"Fast-forwarding {days} day{(days == 1 ? "" : "s")} with the {bot.Name} bot...");
            float started = Time.realtimeSinceStartup;
            try
            {
                while (Done < days && !stopAsked)
                {
                    if (!PlayDay(shopDay, bot)) break;
                    Done++;
                    yield return null;
                }
            }
            finally
            {
                Running = false;
                // The reports were played past; the morning report of the day it stopped on stays.
                shopDay.SetSpeed(1f);
                if (AutoSave.Current != null) AutoSave.Current.SaveNow();
            }
            GameLog.Add($"Fast-forwarded from day {startDay} to day {Economy.Day} in {Time.realtimeSinceStartup - started:0.0}s: " +
                $"{Economy.Format(startMoney)} to {Economy.Format(Economy.Money)}, Fishkeeping {Progression.Fishkeeping}, Shopkeeping {Progression.Shopkeeping}.");
        }

        public static void Stop() => stopAsked = true;

        static List<Type> botTypes;

        /// <summary>Every bot there is: any class that implements <see cref="IDayBot"/> with a parameterless constructor.</summary>
        public static IReadOnlyList<Type> BotTypes
        {
            get
            {
                if (botTypes != null) return botTypes;
                botTypes = new List<Type>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = assembly.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                    foreach (var type in types)
                        if (typeof(IDayBot).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
                            botTypes.Add(type);
                }
                // The simple caretaker last: any proper player bot comes first.
                botTypes.Sort((a, b) =>
                {
                    int last = (a == typeof(CaretakerBot)).CompareTo(b == typeof(CaretakerBot));
                    return last != 0 ? last : string.CompareOrdinal(a.Name, b.Name);
                });
                return botTypes;
            }
        }

        /// <summary>The bot the dev window starts on: the pacing simulator's cautious player if it's there.</summary>
        public static int DefaultBot
        {
            get
            {
                var types = BotTypes;
                for (int i = 0; i < types.Count; i++) if (types[i].Name == "CautiousBot") return i;
                return 0;
            }
        }

        public static IDayBot Create(int index)
        {
            var types = BotTypes;
            if (types.Count == 0) return new CaretakerBot();
            return (IDayBot)Activator.CreateInstance(types[Mathf.Clamp(index, 0, types.Count - 1)]);
        }

        public static string BotName(int index)
        {
            var types = BotTypes;
            if (types.Count == 0) return "Caretaker";
            var type = types[Mathf.Clamp(index, 0, types.Count - 1)];
            try { return ((IDayBot)Activator.CreateInstance(type)).Name; }
            catch { return type.Name; }
        }
    }

    /// <summary>
    /// The simplest player: keeps every fish fed and the tanks clean, nets out the dead, buys food
    /// when it runs out, and orders the best egg box it can afford into a For Sale tank with room
    /// (keeping some money back). Fish in For Sale tanks sell at their appraised price. It doesn't
    /// breed, expand or decorate; the pacing simulator's bots play properly.
    /// </summary>
    public class CaretakerBot : IDayBot
    {
        public string Name => "Caretaker";

        /// <summary>Money it won't spend on eggs.</summary>
        const int Reserve = 300;

        StoreLayout store;
        StoreLayout Store => store != null ? store : store = UnityEngine.Object.FindObjectOfType<StoreLayout>();

        public void Morning()
        {
            Care();
            OrderEggs();
        }

        public void Evening() => Care();

        void Care()
        {
            if (Store == null) return;
            foreach (var tank in Store.Tanks)
            {
                PlayerActions.RemoveDead(tank);
                if (tank.Tank.Residents.Count > 0 && !tank.Tank.FedSinceTimeStep) Feed(tank);
                if (tank.Tank.InBloom) PlayerActions.DeepClean(tank);
                for (int i = 0; i < 3 && ToDoList.NeedsClean(tank.Tank, Balance.Data.notifications); i++)
                    if (!PlayerActions.Clean(tank)) break;
            }
        }

        static void Feed(StoreTank tank)
        {
            FoodType? best = null;
            foreach (FoodType food in Enum.GetValues(typeof(FoodType)))
            {
                if (food == FoodType.AlgaeWafers || Economy.FoodPortions(food) <= 0) continue;
                best = food;
                break;
            }
            if (best == null)
            {
                foreach (var offer in Balance.Data.supplier.food)
                    if (offer != null && offer.type == FoodType.Flakes && PlayerActions.BuyFood(offer)) break;
                if (Economy.FoodPortions(FoodType.Flakes) > 0) best = FoodType.Flakes;
            }
            if (best != null) PlayerActions.Feed(tank, best.Value);
        }

        void OrderEggs()
        {
            if (Store == null) return;
            foreach (var tank in Store.Tanks)
            {
                if (tank.Role != TankRole.ForSale || tank.Tank.Residents.Count > 0) continue;
                // The dearest box it can afford: better boxes bring better fish.
                EggTierBalance pick = null;
                foreach (var tier in EggBoxes.Tiers)
                {
                    if (PlayerActions.WhyCantOrder(tier, tank, false) != null) continue;
                    if (Economy.Money - Perks.Supplier(tier.price) < Reserve) continue;
                    if (pick == null || tier.price > pick.price) pick = tier;
                }
                if (pick != null && PlayerActions.OrderEggs(pick, tank)) return;
            }
        }
    }
}
