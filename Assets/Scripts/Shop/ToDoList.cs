using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Store;
using UnityEngine;

namespace FishBreeder.Shop
{
    /// <summary>One problem waiting after closing: a tank (and maybe a fish) that needs seeing to.</summary>
    public class ToDo
    {
        public enum Kind { DeadFish, SickFish, DirtyWater, HungryFish }

        public Kind kind;
        public StoreTank tank;
        public Fish fish;
        public string title;
        public string hint;

        public bool Resolved
        {
            get
            {
                if (tank == null) return true;
                var t = tank.Tank;
                var settings = Balance.Data.notifications;
                switch (kind)
                {
                    case Kind.DeadFish: return t.Corpses.Count == 0;
                    case Kind.SickFish: return fish == null || fish.IsDead || !fish.Health.IsSick;
                    case Kind.DirtyWater:
                        bool clean = t.WaterHealth >= settings.dirtyWaterBelow && t.Algae <= settings.algaeAbove && !t.HasPests;
                        // Out of wipes (and nothing left to treat pests with) is as far as it can go today.
                        bool doneAllYouCan = t.WipesLeft == 0 && (!t.HasPests || Economy.WaterConditionerDoses == 0);
                        return clean || doneAllYouCan;
                    default: return t.FedSinceTimeStep || t.Residents.Count == 0;
                }
            }
        }
    }

    /// <summary>
    /// The list of problems to sort through after closing the shop: dead fish, sick fish, dirty
    /// tanks and hungry fish, tank by tank. The store view takes the player to each one in turn and
    /// moves on by itself once it's dealt with.
    /// </summary>
    public class ToDoList
    {
        readonly List<ToDo> items = new List<ToDo>();

        public int Index { get; private set; } = -1;
        public int Count => items.Count;
        public bool Active => Index >= 0 && Index < items.Count;
        public ToDo Current => Active ? items[Index] : null;
        /// <summary>Seconds the current item has been resolved, for moving on by itself.</summary>
        public float ResolvedFor { get; set; }

        public int Remaining
        {
            get
            {
                int left = 0;
                foreach (var item in items) if (!item.Resolved) left++;
                return left;
            }
        }

        /// <summary>Looks at every tank and lists what needs doing. Returns how many problems there are.</summary>
        public int Build(IReadOnlyList<StoreTank> tanks)
        {
            items.Clear();
            Index = -1;
            ResolvedFor = 0f;
            var settings = Balance.Data.notifications;
            foreach (var storeTank in tanks)
            {
                var tank = storeTank.Tank;
                if (tank.Corpses.Count > 0)
                    Add(ToDo.Kind.DeadFish, storeTank, null,
                        tank.Corpses.Count == 1 ? $"A fish died in {storeTank.Title}." : $"{tank.Corpses.Count} fish died in {storeTank.Title}.",
                        "Net them out with \"Remove dead fish\" on the left. Dead fish make the others ill and upset customers.");

                foreach (var fish in tank.Residents)
                    if (fish.Health.IsSick)
                        Add(ToDo.Kind.SickFish, storeTank, fish, $"{fish.FishName} the {fish.Species} has {fish.Health.Disease.displayName}.",
                            "Give it the right medicine in its window, or move it to a quarantine tank.");

                bool dirty = tank.WaterHealth < settings.dirtyWaterBelow || tank.Algae > settings.algaeAbove || tank.HasPests;
                if (dirty && (tank.Residents.Count > 0 || tank.Algae > settings.algaeAbove))
                    Add(ToDo.Kind.DirtyWater, storeTank, null, $"{storeTank.Title} is dirty" + (tank.HasPests ? " and has pests." : "."),
                        tank.HasPests ? "Treat the water to get rid of the pests, then wipe the tank." : "Wipe the tank (and treat the water if the pH is off).");

                bool hungry = false;
                foreach (var fish in tank.Residents)
                    if (fish.Hunger >= settings.hungryAbove) hungry = true;
                if (hungry)
                    Add(ToDo.Kind.HungryFish, storeTank, null, $"The fish in {storeTank.Title} are hungry.", "Feed them flakes or premium pellets.");
            }
            return items.Count;
        }

        void Add(ToDo.Kind kind, StoreTank tank, Fish fish, string title, string hint)
            => items.Add(new ToDo { kind = kind, tank = tank, fish = fish, title = title, hint = hint });

        /// <summary>Moves to the next problem that still needs doing. False when there are none left.</summary>
        public bool Next()
        {
            ResolvedFor = 0f;
            for (int i = Index + 1; i < items.Count; i++)
                if (!items[i].Resolved)
                {
                    Index = i;
                    return true;
                }
            Index = items.Count;
            return false;
        }

        public bool Previous()
        {
            ResolvedFor = 0f;
            for (int i = Index - 1; i >= 0; i--)
                if (!items[i].Resolved)
                {
                    Index = i;
                    return true;
                }
            return false;
        }

        /// <summary>Stops going through the list for now; the remaining problems stay for later.</summary>
        public void Pause()
        {
            Index = -1;
            ResolvedFor = 0f;
        }

        public void Dismiss()
        {
            items.Clear();
            Index = -1;
        }
    }
}
