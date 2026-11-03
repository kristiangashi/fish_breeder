using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A tank's algae (see <see cref="AlgaeBalance"/>). It climbs quickly to its target (10% on
    /// its own, more for fish, plants and decorations, less for algae eaters). A filter that
    /// handles the fish holds it there; otherwise it creeps on slowly. Feeding adds a little that
    /// goes again over the next time steps. The moment it reaches the bloom level the tank blooms,
    /// and only a deep clean clears it.
    /// </summary>
    public partial class Tank
    {
        bool bloom;
        float algaeAtStep = -1f;
        /// <summary>Algae from feeding that's still in the tank (part of <see cref="algae"/>): one feeding's worth goes at each time step.</summary>
        float algaeFromFeeding;

        static AlgaeBalance AlgaeRules => Balance.Data.algae;

        /// <summary>An algal bloom: the algae keeps climbing, fish are choked, and only a deep clean ends it.</summary>
        public bool InBloom => bloom;
        /// <summary>Cleans left this closed-shop session.</summary>
        public int CleansLeft { get; private set; }
        /// <summary>Deep cleans left this closed-shop session.</summary>
        public int DeepCleansLeft { get; private set; }

        /// <summary>Algae as the time step found it (algae eaters graze on this before it moves on).</summary>
        public float AlgaeForGrazing => algaeAtStep >= 0f ? algaeAtStep : algae;

        /// <summary>Cleans every tank gets per closed-shop session: two, and more with the Tireless perk.</summary>
        public static int CleansPerSession
        {
            get
            {
                var a = AlgaeRules;
                int every = a.extraCleanEveryFishkeepingLevels;
                return Mathf.Max(0, a.cleansPerSession + (every > 0 ? (Progression.Fishkeeping - 1) / every : 0) + Perks.Count(PerkEffect.ExtraWipes));
            }
        }

        void ResetCleans()
        {
            CleansLeft = CleansPerSession;
            DeepCleansLeft = Mathf.Max(0, AlgaeRules.deepCleansPerSession);
        }

        /// <summary>
        /// The fish that make algae, counted as tiny fish (a small fish counts as two and so on; in
        /// the exhibits and holding tanks every fish counts as one). Algae eaters don't count.
        /// </summary>
        public float FishLoad
        {
            get
            {
                var a = AlgaeRules;
                bool flat = IsExhibit || IsHolding;
                float load = 0f;
                foreach (var fish in residents)
                {
                    if (fish == null || fish.IsDead || fish.Species == null || fish.Species.eatsAlgae) continue;
                    load += flat ? 1f : a.LoadOf(fish.Species.sizeGroup);
                }
                return load;
            }
        }

        public int AlgaeEaterCount
        {
            get
            {
                int n = 0;
                foreach (var fish in residents) if (fish != null && !fish.IsDead && fish.Species != null && fish.Species.eatsAlgae) n++;
                return n;
            }
        }

        /// <summary>Algae eaters this tank's size needs to keep the water clear for longer.</summary>
        public int AlgaeEatersNeeded => Mathf.Max(1, SizeInfo.algaeEaters);

        /// <summary>The least algae a plant like the Java moss rock keeps for the algae eaters (0 with none, or no eaters).</summary>
        public float AlgaeFloor
        {
            get
            {
                if (decor == null || AlgaeEaterCount == 0) return 0f;
                float floor = 0f;
                foreach (var id in decor.Ids)
                {
                    var item = TankDecor.Find(id);
                    if (item != null) floor = Mathf.Max(floor, item.algaeFloor);
                }
                return floor;
            }
        }

        /// <summary>How much the plants raise the algae target (each plant its own amount).</summary>
        public float PlantAlgae
        {
            get
            {
                if (decor == null) return 0f;
                float total = 0f;
                foreach (var id in decor.Ids)
                {
                    var item = TankDecor.Find(id);
                    if (item != null && item.plant) total += Mathf.Max(0f, item.algaeTarget);
                }
                return total;
            }
        }

        /// <summary>How much the decorations that aren't plants raise the algae target.</summary>
        public float DecorationAlgae
        {
            get
            {
                if (decor == null) return 0f;
                int count = 0;
                foreach (var id in decor.Ids)
                {
                    var item = TankDecor.Find(id);
                    if (item != null && !item.plant) count++;
                }
                return count * Mathf.Max(0f, AlgaeRules.decorationAlgae);
            }
        }

        /// <summary>Per fish and per algae eater, algae counts less in a bigger tank: the standard tank's fish limit over this one's.</summary>
        float AlgaeScale => StandardCapacity / (float)Mathf.Max(1, Capacity);

        /// <summary>How much the fish raise the algae target (algae eaters don't count).</summary>
        public float FishAlgae => FishLoad * Mathf.Max(0f, AlgaeRules.fishAlgae) * AlgaeScale;

        /// <summary>How much the algae eaters lower the algae target.</summary>
        public float EaterAlgae => AlgaeEaterCount * Mathf.Max(0f, AlgaeRules.eaterAlgae) * AlgaeScale;

        /// <summary>
        /// Where the algae settles: the natural level plus the fish, plants and decorations, less the
        /// algae eaters, and never below what a moss rock keeps for the eaters.
        /// </summary>
        public float AlgaeTarget => Mathf.Clamp01(Mathf.Max(AlgaeRules.naturalAlgae + FishAlgae + PlantAlgae + DecorationAlgae - EaterAlgae, AlgaeFloor));

        /// <summary>Fish the working filter handles, with the Steady filters perk (0 without one).</summary>
        public int FilterFishCapacity => FilterWorking ? Mathf.Max(0, filter.Info.fishCapacity + Perks.Count(PerkEffect.FilterFish)) : 0;

        /// <summary>A working filter handles every fish, so algae stops at its target instead of creeping past it.</summary>
        public bool AlgaeHeld => FilterWorking && FishLoad <= FilterFishCapacity + 0.001f;

        /// <summary>Algae from feeding still in the tank (it goes one feeding's worth per time step).</summary>
        public float FeedingAlgae => algaeFromFeeding;

        /// <summary>Time steps until the feeding algae is all gone.</summary>
        public int FeedingAlgaeSteps => algaeFromFeeding <= 0.0001f ? 0 : Mathf.CeilToInt(algaeFromFeeding / Mathf.Max(0.001f, AlgaeRules.feedAlgae) - 0.001f);

        /// <summary>The standard tank's fish limit: algae per fish is set for it, and bigger tanks get less per fish.</summary>
        static int StandardCapacity
        {
            get
            {
                var sizes = Balance.Data.store.tankSizes;
                return sizes != null && sizes.Count > 0 ? CapacityOf(sizes[0]) : CapacityOf(new TankSizeBalance());
            }
        }

        /// <summary>Algae where the water starts to turn green: later with the tank's full set of algae eaters.</summary>
        public float GreenStart
        {
            get
            {
                var a = AlgaeRules;
                return Mathf.Lerp(a.greenStart, a.greenStartWithEaters, Mathf.Clamp01(AlgaeEaterCount / (float)AlgaeEatersNeeded));
            }
        }

        /// <summary>How green the water looks, 0..1: none until <see cref="GreenStart"/>, a tinge, then much greener.</summary>
        public float AlgaeGreen
        {
            get
            {
                var a = AlgaeRules;
                float start = GreenStart;
                float more = start + Mathf.Max(0.01f, a.greenMore - a.greenStart);
                if (algae <= start) return 0f;
                if (algae < more) return 0.35f * (algae - start) / (more - start);
                return Mathf.Lerp(0.35f, 1f, (algae - more) / Mathf.Max(0.01f, 1f - more));
            }
        }

        /// <summary>How much the algae puts customers off the look of the water, 0..1: nothing until they start minding it.</summary>
        public float AlgaeNuisance
        {
            get
            {
                var a = AlgaeRules;
                if (algae <= a.bothersAbove) return 0f;
                float more = Mathf.Max(a.bothersAbove + 0.01f, a.bothersMoreAbove);
                if (algae < more) return 0.15f * (algae - a.bothersAbove) / (more - a.bothersAbove);
                return Mathf.Lerp(0.15f, 1f, (algae - more) / Mathf.Max(0.01f, 1f - more));
            }
        }

        /// <summary>Chance a customer looking at the tank minds its algae.</summary>
        public float AlgaeBotherChance
        {
            get
            {
                var a = AlgaeRules;
                if (bloom || algae >= a.bothersMoreAbove) return a.bothersMoreChance;
                return algae >= a.bothersAbove ? a.bothersChance : 0f;
            }
        }

        /// <summary>
        /// Time step: one feeding's worth of feeding algae goes, then the rest climbs quickly to its
        /// target, stays there with a filter that handles the fish, or creeps on past it. The moment
        /// it reaches the bloom level the tank blooms.
        /// </summary>
        void UpdateAlgae()
        {
            var a = AlgaeRules;
            algaeAtStep = algae;
            float gone = Mathf.Min(algaeFromFeeding, Mathf.Max(0f, a.feedAlgae));
            algaeFromFeeding -= gone;
            algae = Mathf.Max(0f, algae - gone);
            if (bloom)
            {
                // A bloom feeds on itself until it's deep cleaned.
                algae = Mathf.Min(1f, algae + Mathf.Max(0f, a.bloomGrowth));
                return;
            }
            float settled = Mathf.Max(0f, algae - algaeFromFeeding), target = AlgaeTarget;
            if (settled < target)
            {
                float rise = Mathf.Clamp((target - settled) * a.riseShare, a.minRise, Mathf.Max(a.minRise, a.maxRise)) * Perks.Less(PerkEffect.SlowerAlgae);
                settled = Mathf.Min(target, settled + rise);
            }
            else if (AlgaeHeld) settled = Mathf.Max(target, settled - Mathf.Max(a.minFall, (settled - target) * a.fallShare));
            else settled += Mathf.Max(0f, a.overTargetRise);
            algae = Mathf.Clamp01(settled + algaeFromFeeding);
            CheckBloom();
        }

        /// <summary>The moment algae reaches the bloom level the tank blooms: there's no backing out of it.</summary>
        void CheckBloom()
        {
            if (!bloom && algae >= AlgaeRules.bloomAbove) StartBloom();
        }

        /// <summary>The algae bursts: much more of it at once, some fish die and many fall ill.</summary>
        void StartBloom()
        {
            var a = AlgaeRules;
            bloom = true;
            algae = Mathf.Min(1f, algae + a.bloomJump);
            int died = 0, ill = 0;
            foreach (var fish in new List<Fish>(residents))
            {
                if (fish == null || fish.IsDead) continue;
                if (GameRandom.Chance(a.bloomDeathChance))
                {
                    fish.Die("an algal bloom");
                    died++;
                }
                else if (GameRandom.Chance(a.bloomIllnessChance) && fish.Health.Infect(Health.DiseaseDefinition.PickWeighted(Balance.Data.diseases, GameRandom.Value), "the algal bloom"))
                    ill++;
            }
            GameLog.Add($"An algal bloom has broken out in {Title}! Algae {algae * 100f:0}%" + (died > 0 ? $", {died} fish died" : "") + (ill > 0 ? $", {ill} fell ill" : "")
                        + ". Only a deep clean (Care tab) can clear it.");
            Fx.Text(InnerBounds.center + Vector3.up * innerSize.y * 0.3f, "Algal bloom!", UiTheme.Coral, 22f, 2.5f);
        }

        /// <summary>Clean tank: scrubs off a little algae and freshens the water. Limited per closed-shop session; no use against a bloom.</summary>
        public bool Clean()
        {
            var a = AlgaeRules;
            if (bloom)
            {
                GameLog.Add($"{Title} has an algal bloom: a clean can't touch it. It needs a deep clean.");
                return false;
            }
            if (CleansLeft <= 0)
            {
                GameLog.Add("This tank has had its cleans. It gets more at the next time step.");
                return false;
            }
            CleansLeft--;
            // Experienced fishkeepers get more out of every clean.
            float skill = (1f + a.cleanBonusPerFishkeepingLevel * (Progression.Fishkeeping - 1)) * Perks.More(PerkEffect.StrongerWipes);
            float before = algae;
            algae = Mathf.Clamp01(algae - a.cleanAlgaeRemoved * skill);
            algaeFromFeeding = Mathf.Min(algaeFromFeeding, algae);
            waterQuality = Mathf.Clamp01(waterQuality + a.cleanQualityRestored * skill);
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpCleanTank);
            Progression.Count(GoalKind.CleanTanks);
            Fx.Sparkle(InnerBounds);
            Fx.Text(InnerBounds.center + Vector3.up * innerSize.y * 0.3f, "Sparkling!", UiTheme.Aqua, 20f);
            GameLog.Add($"You cleaned {Title}: algae {before * 100f:0}% to {algae * 100f:0}%, water {waterQuality * 100f:0}%." + (CleansLeft > 0 ? $" {CleansLeft} more here this time." : " That's this tank's cleans until the next time step."));
            return true;
        }

        /// <summary>Chance each fish is hurt in a deep clean: less as your Fishkeeping level grows.</summary>
        public static float DeepCleanInjuryChance
        {
            get
            {
                var a = AlgaeRules;
                return Mathf.Clamp01(Mathf.Max(a.deepCleanMinInjuryChance, a.deepCleanInjuryChance - a.deepCleanInjuryLessPerLevel * (Progression.Fishkeeping - 1)));
            }
        }

        /// <summary>
        /// Deep clean: the fish are netted out, the tank scrubbed down and the water changed. Almost
        /// all the algae goes (a bloom too), but every fish risks getting hurt.
        /// </summary>
        public bool DeepClean()
        {
            var a = AlgaeRules;
            if (DeepCleansLeft <= 0)
            {
                GameLog.Add("This tank has had its deep clean. It can have another after the next time step.");
                return false;
            }
            if (a.deepCleanPrice > 0 && !Economy.TrySpend(a.deepCleanPrice, $"a deep clean of {Title}", MoneyKind.Care)) return false;
            DeepCleansLeft--;
            bool wasBloom = bloom;
            bloom = false;
            float before = algae;
            algae = Mathf.Min(algae, a.deepCleanAlgaeLeft);
            algaeFromFeeding = 0f;
            waterQuality = Mathf.Clamp01(waterQuality + a.deepCleanQualityRestored);
            float chance = DeepCleanInjuryChance;
            var hurt = new List<string>();
            foreach (var fish in new List<Fish>(residents))
            {
                if (fish == null || fish.IsDead || !GameRandom.Chance(chance)) continue;
                var injury = InjuryBalance.Pick(a.deepCleanInjuries);
                if (injury == null) continue;
                fish.Health.Injure(injury.amount, injury.text);
                hurt.Add($"{fish.FishName} ({injury.text})");
            }
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpDeepClean);
            Progression.Count(GoalKind.CleanTanks);
            Fx.Sparkle(InnerBounds, 14);
            Fx.Text(InnerBounds.center + Vector3.up * innerSize.y * 0.3f, wasBloom ? "Bloom cleared!" : "Deep cleaned!", UiTheme.Aqua, 20f);
            GameLog.Add($"You deep cleaned {Title}" + (wasBloom ? " and cleared the algal bloom" : "") + $": algae {before * 100f:0}% to {algae * 100f:0}%, water {waterQuality * 100f:0}%."
                        + (hurt.Count > 0 ? $" Hurt in the clean: {string.Join(", ", hurt)}." : " No fish were hurt."));
            return true;
        }

        /// <summary>Algae wafers: algae for the algae eaters to graze.</summary>
        public void AddAlgae(float amount)
        {
            float before = algae;
            algae = Mathf.Clamp01(algae + amount);
            CheckBloom();
            if (AlgaeEaterCount > 0) GameLog.Add($"Algae wafers in {Title}: algae {before * 100f:0}% to {algae * 100f:0}% for the algae eaters.");
        }

        /// <summary>Feeding: leftover food feeds a little algae at once. One feeding's worth goes at each time step; reaching the bloom level blooms at once.</summary>
        public void AddFeedingAlgae(float amount)
        {
            if (amount <= 0f) return;
            float before = algae;
            algae = Mathf.Clamp01(algae + amount);
            algaeFromFeeding += algae - before;
            CheckBloom();
        }

        /// <summary>Developer: sets the algae (a bloom starts at once with <paramref name="startBloom"/>).</summary>
        public void DevSetAlgae(float value, bool startBloom)
        {
            algae = Mathf.Clamp01(value);
            algaeFromFeeding = 0f;
            bloom = false;
            if (startBloom) StartBloom();
        }
    }
}
