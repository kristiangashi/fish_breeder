using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A tank's algae (see <see cref="AlgaeBalance"/>): one of four stages, Clear, Healthy green,
    /// Overgrown and Bloom. Load (fish, feedings, plants, decorations) over capacity (the tank,
    /// its filter and algae eaters) picks the stage the tank heads for, and at each time step it
    /// moves one stage towards it. A clean takes it down a stage; only a deep clean ends a bloom.
    /// </summary>
    public partial class Tank
    {
        AlgaeStage algaeStage;
        /// <summary>The stage as the last time step found it, before it moved (algae eaters graze on this).</summary>
        AlgaeStage stageAtStep;
        /// <summary>Feedings (and wafer feedings) since the last time step, and in the time step before that.</summary>
        int feedings, feedingsBefore, wafers, wafersBefore;

        static AlgaeBalance AlgaeRules => Balance.Data.algae;

        /// <summary>The tank's algae stage.</summary>
        public AlgaeStage Algae => algaeStage;
        /// <summary>An algal bloom: fish are choked, and only a deep clean ends it.</summary>
        public bool InBloom => algaeStage == AlgaeStage.Bloom;
        /// <summary>Overgrown or in bloom: too much algae for fish and customers.</summary>
        public bool TooMuchAlgae => algaeStage >= AlgaeStage.Overgrown;
        /// <summary>Cleans left this closed-shop session.</summary>
        public int CleansLeft { get; private set; }
        /// <summary>Deep cleans left this closed-shop session.</summary>
        public int DeepCleansLeft { get; private set; }

        /// <summary>The stage's name as the game says it.</summary>
        public static string StageName(AlgaeStage stage)
        {
            switch (stage)
            {
                case AlgaeStage.HealthyGreen: return "Healthy green";
                case AlgaeStage.Overgrown: return "Overgrown";
                case AlgaeStage.Bloom: return "Algal bloom";
                default: return "Clear";
            }
        }

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

        /// <summary>A plant like the Java moss rock keeps a tank with algae eaters at least Healthy green.</summary>
        public bool HasAlgaeFilm
        {
            get
            {
                if (decor == null || AlgaeEaterCount == 0) return false;
                foreach (var id in decor.Ids)
                {
                    var item = TankDecor.Find(id);
                    if (item != null && item.algaeFilm) return true;
                }
                return false;
            }
        }

        /// <summary>Load from the plants (each its own amount; a negative one soaks up algae).</summary>
        public float PlantLoad
        {
            get
            {
                if (decor == null) return 0f;
                float total = 0f;
                foreach (var id in decor.Ids)
                {
                    var item = TankDecor.Find(id);
                    if (item != null && item.plant) total += item.algaeLoad;
                }
                return total;
            }
        }

        /// <summary>Load from the decorations that aren't plants.</summary>
        public float DecorationLoad
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
                return count * Mathf.Max(0f, AlgaeRules.decorationLoad);
            }
        }

        /// <summary>How much bigger this tank is than the standard one, by fish limit (its own load and capacity grow with it).</summary>
        float AlgaeSizeScale => Mathf.Max(1, Capacity) / (float)StandardCapacity;

        /// <summary>Load from feedings: each one counts half at each of the next two time steps.</summary>
        public float FeedingLoad
        {
            get
            {
                var a = AlgaeRules;
                return 0.5f * ((feedings + feedingsBefore) * Mathf.Max(0f, a.feedingLoad) + (wafers + wafersBefore) * Mathf.Max(0f, a.waferLoad));
            }
        }

        /// <summary>Everything that feeds algae, in tiny fish.</summary>
        public float AlgaeLoad => Mathf.Max(0f, AlgaeRules.naturalLoad * AlgaeSizeScale + FishLoad + FeedingLoad + PlantLoad + DecorationLoad);

        /// <summary>Fish the working filter handles, with the Steady filters perk (0 without one).</summary>
        public int FilterFishCapacity => FilterWorking ? Mathf.Max(0, filter.Info.fishCapacity + Perks.Count(PerkEffect.FilterFish)) : 0;

        /// <summary>Everything that holds algae back, in tiny fish: the tank, its working filter, its algae eaters (and Fishkeeping level if the console says so).</summary>
        public float AlgaeCapacity
        {
            get
            {
                var a = AlgaeRules;
                return Mathf.Max(0.5f, a.tankCapacity * AlgaeSizeScale + FilterFishCapacity * a.filterCapacityShare + AlgaeEaterCount * a.eaterCapacity
                                       + a.capacityPerFishkeepingLevel * (Progression.Fishkeeping - 1));
            }
        }

        /// <summary>Load over capacity: under 1 the tank copes.</summary>
        public float AlgaeRatio => AlgaeLoad / AlgaeCapacity;

        /// <summary>The stage the tank is heading for, from load over capacity (never below Healthy green with an algae film for eaters).</summary>
        public AlgaeStage AlgaeHeading
        {
            get
            {
                var a = AlgaeRules;
                float r = AlgaeRatio;
                var stage = r >= a.bloomFrom ? AlgaeStage.Bloom : r >= a.overgrownFrom ? AlgaeStage.Overgrown : r >= a.healthyFrom ? AlgaeStage.HealthyGreen : AlgaeStage.Clear;
                if (stage == AlgaeStage.Clear && HasAlgaeFilm) stage = AlgaeStage.HealthyGreen;
                return stage;
            }
        }

        /// <summary>The stage after the next time step: one towards where it's heading, and a bloom stays until a deep clean.</summary>
        public AlgaeStage AlgaeNextStep
        {
            get
            {
                if (InBloom) return AlgaeStage.Bloom;
                var heading = AlgaeHeading;
                if (heading > algaeStage) return algaeStage + 1;
                if (heading < algaeStage) return algaeStage - 1;
                return algaeStage;
            }
        }

        /// <summary>The standard tank's fish limit: a tank's own algae load and capacity are set for it.</summary>
        static int StandardCapacity
        {
            get
            {
                var sizes = Balance.Data.store.tankSizes;
                return Mathf.Max(1, sizes != null && sizes.Count > 0 ? CapacityOf(sizes[0]) : CapacityOf(new TankSizeBalance()));
            }
        }

        /// <summary>How much of the glass the algae covers for this stage (looks only).</summary>
        public static float GlassCover(AlgaeStage stage)
        {
            var a = AlgaeRules;
            switch (stage)
            {
                case AlgaeStage.HealthyGreen: return a.healthyGlass;
                case AlgaeStage.Overgrown: return a.overgrownGlass;
                case AlgaeStage.Bloom: return a.bloomGlass;
                default: return a.clearGlass;
            }
        }

        /// <summary>How much the algae puts customers off the look of the water, 0..1.</summary>
        public float AlgaeNuisance => algaeStage == AlgaeStage.Bloom ? AlgaeRules.bloomNuisance : algaeStage == AlgaeStage.Overgrown ? AlgaeRules.overgrownNuisance : 0f;

        /// <summary>Chance a customer looking at the tank minds its algae.</summary>
        public float AlgaeBotherChance => algaeStage == AlgaeStage.Bloom ? AlgaeRules.bloomBotherChance : algaeStage == AlgaeStage.Overgrown ? AlgaeRules.overgrownBotherChance : 0f;

        /// <summary>Algae eaters graze in a Healthy green or Overgrown tank (as the last time step found it), or on wafers from the last two time steps.</summary>
        public bool AlgaeToGraze => stageAtStep == AlgaeStage.HealthyGreen || stageAtStep == AlgaeStage.Overgrown || wafers + wafersBefore > 0 || HasAlgaeFilm;

        /// <summary>Time step: one stage towards where the tank is heading. Reaching Bloom starts a bloom.</summary>
        void UpdateAlgae()
        {
            stageAtStep = algaeStage;
            var next = AlgaeNextStep;
            feedingsBefore = feedings;
            wafersBefore = wafers;
            feedings = wafers = 0;
            if (next == algaeStage) return;
            if (next == AlgaeStage.Bloom) StartBloom();
            else algaeStage = next;
        }

        /// <summary>The algae bursts: some fish die and many fall ill.</summary>
        void StartBloom()
        {
            var a = AlgaeRules;
            algaeStage = AlgaeStage.Bloom;
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
            GameLog.Add($"An algal bloom has broken out in {Title}!" + (died > 0 ? $" {died} fish died" : "") + (ill > 0 ? $"{(died > 0 ? "," : "")} {ill} fell ill" : "")
                        + ". Only a deep clean (Care tab) can clear it.");
            Fx.Text(InnerBounds.center + Vector3.up * innerSize.y * 0.3f, "Algal bloom!", UiTheme.Coral, 22f, 2.5f);
        }

        /// <summary>Clean tank: scrubs the algae down a stage and freshens the water. Limited per closed-shop session; no use against a bloom.</summary>
        public bool Clean()
        {
            var a = AlgaeRules;
            if (InBloom)
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
            var before = algaeStage;
            int floor = a.cleanLeavesHealthy ? Mathf.Min((int)algaeStage, (int)AlgaeStage.HealthyGreen) : 0;
            algaeStage = (AlgaeStage)Mathf.Max(floor, (int)algaeStage - Mathf.Max(0, a.cleanStages));
            waterQuality = Mathf.Clamp01(waterQuality + CleanQualityRestored);
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpCleanTank);
            Progression.Count(GoalKind.CleanTanks);
            Fx.Sparkle(InnerBounds);
            Fx.Text(InnerBounds.center + Vector3.up * innerSize.y * 0.3f, "Sparkling!", UiTheme.Aqua, 20f);
            GameLog.Add($"You cleaned {Title}: " + (before != algaeStage ? $"algae {StageName(before)} to {StageName(algaeStage)}, " : "") + $"water {waterQuality * 100f:0}%."
                        + (CleansLeft > 0 ? $" {CleansLeft} more here this time." : " That's this tank's cleans until the next time step."));
            return true;
        }

        /// <summary>How much water quality one Clean gives back: more with the Stronger wipes perk (and with Fishkeeping level only if the console's per-level water bonus is set).</summary>
        public static float CleanQualityRestored
        {
            get
            {
                var a = AlgaeRules;
                return a.cleanQualityRestored * (1f + a.cleanQualityBonusPerFishkeepingLevel * (Progression.Fishkeeping - 1)) * Perks.More(PerkEffect.StrongerWipes);
            }
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
        /// Deep clean: the fish are netted out, the tank scrubbed down and the water changed. The algae
        /// goes to Clear wherever it was, a bloom too, but every fish risks getting hurt.
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
            bool wasBloom = InBloom;
            var before = algaeStage;
            algaeStage = AlgaeStage.Clear;
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
            GameLog.Add($"You deep cleaned {Title}" + (wasBloom ? " and cleared the algal bloom" : "") + $": algae {StageName(before)} to {StageName(algaeStage)}, water {waterQuality * 100f:0}%."
                        + (hurt.Count > 0 ? $" Hurt in the clean: {string.Join(", ", hurt)}." : " No fish were hurt."));
            return true;
        }

        /// <summary>A feeding: it feeds the algae a little over the next two time steps (algae wafers feed the algae eaters too).</summary>
        public void CountFeeding(bool algaeWafers)
        {
            if (algaeWafers) wafers++;
            else feedings++;
        }

        /// <summary>Developer: sets the algae stage (Bloom starts a bloom at once, with its deaths and illness).</summary>
        public void DevSetAlgae(AlgaeStage stage)
        {
            if (stage == AlgaeStage.Bloom) { if (!InBloom) StartBloom(); }
            else algaeStage = stage;
            stageAtStep = algaeStage;
        }
    }
}
