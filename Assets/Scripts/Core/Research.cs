using System.Collections.Generic;
using FishBreeder.Feeding;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>Things research teaches about a species.</summary>
    public enum ResearchFact { Temperature, Ph, Plants, School, Breeding, Lifespan }

    /// <summary>What a finished research project lets the game show.</summary>
    public enum ResearchUnlock { HealthDetails, Temperature, TankMates, WaterTesting, FavouriteFoods, Breeding, Shoaling, Pathology, Plants, Ph, Lifespan }

    /// <summary>Where a research project is up to.</summary>
    public enum ResearchState { Locked, Bounty, Ready, Researching, Done }

    /// <summary>
    /// Research projects. Each project posts a bounty (feed fish 8 times, cure 2 sick fish, keep 15
    /// fish at once...) once the Research level and any project it builds on allow. Doing the
    /// bounty makes the project ready; the player then starts it in the research window, and it
    /// takes a few time steps at the research bench (one project at a time, faster at a higher
    /// Research level). A finished project makes the game tell you more: what temperature fish
    /// like, who they fight with, why they won't breed, how much each thing does to their health.
    /// Until then those messages stay vague. Bounties and research both give Research XP, and every
    /// kind of fish you keep earns a little at each time step.
    /// </summary>
    public static class Research
    {
        /// <summary>Bounty progress for counted bounties, per project id, since the bounty was posted.</summary>
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static readonly Dictionary<MilestoneKind, int> totals = new Dictionary<MilestoneKind, int>();
        static readonly HashSet<string> posted = new HashSet<string>();
        static readonly HashSet<string> ready = new HashSet<string>();
        static readonly HashSet<string> done = new HashSet<string>();
        static readonly HashSet<ResearchUnlock> unlocked = new HashSet<ResearchUnlock>();
        static readonly HashSet<string> favouritesSeen = new HashSet<string>();
        static string current;
        static float currentProgress;

        static ResearchBalance Data => Balance.Data.research;

        public static List<ResearchProjectBalance> Projects => Data.projects;

        /// <summary>Raised when a bounty is done or research finishes, so windows can react.</summary>
        public static event System.Action Changed;

        public static void Reset()
        {
            counts.Clear();
            totals.Clear();
            posted.Clear();
            ready.Clear();
            done.Clear();
            unlocked.Clear();
            favouritesSeen.Clear();
            current = null;
            currentProgress = 0f;
        }

        /// <summary>Research progress per time step: 1 at Research level 1, more at higher levels.</summary>
        public static float Speed => (1f + Data.speedPerLevel * (Progression.Level(XpTrack.Research) - 1)) * Perks.More(PerkEffect.FasterResearch);

        public static bool Has(ResearchUnlock unlock) => unlocked.Contains(unlock);

        public static bool CanReadPh => Has(ResearchUnlock.WaterTesting);

        /// <summary>Whether a fact about this species is known: its research is done and you've discovered the species.</summary>
        public static bool Knows(Species species, ResearchFact fact)
        {
            if (species == null || !Journal.IsDiscovered(species)) return false;
            switch (fact)
            {
                case ResearchFact.Temperature: return Has(ResearchUnlock.Temperature);
                case ResearchFact.Ph: return CanReadPh && Has(ResearchUnlock.Ph);
                case ResearchFact.Plants: return Has(ResearchUnlock.Plants);
                case ResearchFact.School: return Has(ResearchUnlock.Shoaling);
                case ResearchFact.Breeding: return Has(ResearchUnlock.Breeding);
                default: return Has(ResearchUnlock.Lifespan);
            }
        }

        public static bool KnowsRelationship(Species a, Species b) => Has(ResearchUnlock.TankMates) && Journal.IsDiscovered(a) && Journal.IsDiscovered(b);

        /// <summary>A favourite is known once the feeding study is done and a fish has been seen enjoying it.</summary>
        public static bool KnowsFavourite(Species species, FoodType food) => species != null && Has(ResearchUnlock.FavouriteFoods) && favouritesSeen.Contains(species.id + "|" + (int)food);

        // ---------------------------------------------------------------- projects

        public static ResearchProjectBalance Find(string id) => Projects.Find(p => p != null && p.id == id);

        public static ResearchProjectBalance Current => current != null ? Find(current) : null;

        /// <summary>How far the project at the research bench is, 0..1.</summary>
        public static float CurrentFraction
        {
            get
            {
                var project = Current;
                return project == null ? 0f : Mathf.Clamp01(currentProgress / Mathf.Max(0.01f, project.steps));
            }
        }

        /// <summary>Time steps until the project at the bench is done, at today's speed.</summary>
        public static int StepsLeft
        {
            get
            {
                var project = Current;
                return project == null ? 0 : Mathf.Max(1, Mathf.CeilToInt((project.steps - currentProgress) / Mathf.Max(0.01f, Speed)));
            }
        }

        public static int StepsFor(ResearchProjectBalance project) => Mathf.Max(1, Mathf.CeilToInt(project.steps / Mathf.Max(0.01f, Speed)));

        public static ResearchState State(ResearchProjectBalance project)
        {
            if (project == null) return ResearchState.Locked;
            if (done.Contains(project.id)) return ResearchState.Done;
            if (current == project.id) return ResearchState.Researching;
            if (ready.Contains(project.id)) return ResearchState.Ready;
            return posted.Contains(project.id) ? ResearchState.Bounty : ResearchState.Locked;
        }

        /// <summary>Why a locked project's bounty isn't up yet.</summary>
        public static string LockReason(ResearchProjectBalance project)
        {
            var needs = new List<string>();
            if (!Progression.IsUnlocked(XpTrack.Research, project.level)) needs.Add($"Research level {project.level}");
            var before = string.IsNullOrEmpty(project.requires) ? null : Find(project.requires);
            if (before != null && !done.Contains(before.id)) needs.Add($"finish {before.name}");
            return needs.Count > 0 ? "Needs " + string.Join(" and ", needs) : "";
        }

        /// <summary>Bounty progress: how many done so far (capped at the target).</summary>
        public static int BountyProgress(ResearchProjectBalance project)
        {
            if (project == null) return 0;
            if (State(project) >= ResearchState.Ready) return project.target;
            int have = IsTotal(project.bounty) ? (totals.TryGetValue(project.bounty, out var t) ? t : 0) : counts.TryGetValue(project.id, out var c) ? c : 0;
            return Mathf.Min(have, project.target);
        }

        public static string BountyText(ResearchProjectBalance project)
        {
            int n = project.target;
            string s = n == 1 ? "" : "s";
            switch (project.bounty)
            {
                case MilestoneKind.GetFish: return $"Get {n} fish";
                case MilestoneKind.KeepFish: return $"Keep {n} fish at once";
                case MilestoneKind.DiscoverSpecies: return $"Discover {n} kind{s} of fish";
                case MilestoneKind.GrowUp: return $"Raise {n} fish to adulthood";
                case MilestoneKind.FeedFish: return $"Feed a tank {n} time{s}";
                case MilestoneKind.CleanTank: return $"Wipe a tank {n} time{s}";
                case MilestoneKind.HatchEggs: return $"Hatch {n} egg{s}";
                case MilestoneKind.BreedFish: return $"Breed {n} fry of your own";
                case MilestoneKind.LearnFact: return $"Finish {n} research project{s}";
                case MilestoneKind.BuyEquipment: return $"Install {n} piece{s} of equipment";
                case MilestoneKind.BuyDecoration: return $"Buy {n} decoration{s}";
                case MilestoneKind.OpenShop: return $"Open the shop {n} time{s}";
                case MilestoneKind.SellFish: return $"Sell {n} fish";
                case MilestoneKind.EarnMoney: return $"Earn {Economy.Format(n)} from sales";
                case MilestoneKind.HappyCustomers: return $"Make {n} customer{s} happy";
                case MilestoneKind.FishkeepingLevel: return $"Reach Fishkeeping level {n}";
                case MilestoneKind.CureFish: return $"Cure {n} sick fish with medicine";
                case MilestoneKind.RepairEquipment: return $"Repair {n} broken piece{s} of equipment";
                case MilestoneKind.TreatWater: return $"Treat the water {n} time{s}";
                default: return $"{project.bounty} x{n}";
            }
        }

        /// <summary>Running totals (fish kept, kinds discovered, levels) count as they stand, not since the bounty went up.</summary>
        static bool IsTotal(MilestoneKind kind) => kind == MilestoneKind.KeepFish || kind == MilestoneKind.DiscoverSpecies || kind == MilestoneKind.FishkeepingLevel;

        /// <summary>Starts a ready project at the research bench, if the bench is free.</summary>
        public static bool Start(ResearchProjectBalance project)
        {
            if (project == null || current != null || !ready.Contains(project.id)) return false;
            ready.Remove(project.id);
            current = project.id;
            currentProgress = 0f;
            GameLog.Add($"Research started: {project.name}. About {StepsFor(project)} time steps to go.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Posts the bounties whose Research level and earlier project are done, and checks finished bounties.</summary>
        public static void Refresh()
        {
            foreach (var project in Projects)
            {
                if (project == null || string.IsNullOrEmpty(project.id) || posted.Contains(project.id) || done.Contains(project.id)) continue;
                if (LockReason(project).Length > 0) continue;
                posted.Add(project.id);
                counts[project.id] = 0;
            }
            foreach (var project in Projects)
                if (project != null && State(project) == ResearchState.Bounty && BountyProgress(project) >= project.target) CompleteBounty(project);
        }

        static void CompleteBounty(ResearchProjectBalance project)
        {
            ready.Add(project.id);
            Progression.Award(XpTrack.Research, project.xpBounty);
            GameLog.Add($"Bounty done: {BountyText(project)}. You can research {project.name} now (Research, R).");
            Fx.Card("BOUNTY DONE!", project.name, "Ready to research: open Research (R) to start it.", $"+{project.xpBounty} Research XP", UiTheme.Hibiscus);
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- events

        /// <summary>Something that bounties count happened (fed a tank, cured a fish, ...).</summary>
        public static void OnEvent(MilestoneKind kind, int amount)
        {
            if (amount <= 0) return;
            bool any = false;
            foreach (var project in Projects)
            {
                if (project == null || project.bounty != kind || State(project) != ResearchState.Bounty) continue;
                counts[project.id] = (counts.TryGetValue(project.id, out var n) ? n : 0) + amount;
                any = true;
            }
            if (any) Refresh();
        }

        /// <summary>A running total changed (fish kept, kinds discovered, Fishkeeping level).</summary>
        public static void OnTotal(MilestoneKind kind, int value)
        {
            int before = totals.TryGetValue(kind, out var t) ? t : 0;
            if (value <= before) return;
            totals[kind] = value;
            Refresh();
        }

        /// <summary>Time step: the research bench moves on, and every kind of fish kept earns a little Research XP.</summary>
        public static void OnTimeStep(IEnumerable<Tank> tanks)
        {
            var kept = new HashSet<string>();
            foreach (var tank in tanks)
                foreach (var fish in tank.Residents)
                    if (fish != null && !fish.IsDead && !fish.IdentityHidden) kept.Add(fish.Species.id);
            if (kept.Count > 0) Progression.Award(XpTrack.Research, Data.xpPerSpeciesPerStep * kept.Count);

            var project = Current;
            if (project != null)
            {
                currentProgress += Speed;
                if (currentProgress >= project.steps) Finish(project);
            }
            Refresh();
        }

        static void Finish(ResearchProjectBalance project)
        {
            current = null;
            currentProgress = 0f;
            done.Add(project.id);
            unlocked.Add(project.unlock);
            Progression.Award(XpTrack.Research, project.xpResearch);
            Milestones.Add(MilestoneKind.LearnFact);
            GameLog.Add($"Research finished: {project.name}! {project.teaches}");
            Fx.Card("RESEARCH FINISHED", project.name, project.teaches, $"+{project.xpResearch} Research XP", UiTheme.Aqua);
            Changed?.Invoke();
        }

        /// <summary>Developer: finishes the project at the bench, or every ready one, straight away.</summary>
        public static void DevFinish()
        {
            if (Current != null) Finish(Current);
            else
                foreach (var id in new List<string>(ready))
                {
                    ready.Remove(id);
                    Finish(Find(id));
                }
        }

        /// <summary>Developer: marks every posted bounty done.</summary>
        public static void DevCompleteBounties()
        {
            foreach (var project in Projects)
                if (project != null && State(project) == ResearchState.Bounty) CompleteBounty(project);
        }

        /// <summary>A fish ate: one of its favourites is noted, and shown once the feeding study is done.</summary>
        public static void OnAte(Fish fish, FoodType food)
        {
            var species = fish.Species;
            if (fish.IdentityHidden || !species.favoriteFoods.Contains(food)) return;
            string key = species.id + "|" + (int)food;
            if (!favouritesSeen.Add(key) || !Has(ResearchUnlock.FavouriteFoods)) return;
            GameLog.Add($"Research: {Plural(species)} love {FoodPellet.Name(food)}!");
            Fx.Text(fish.transform.position + Vector3.up * 0.07f, "Favourite food!", UiTheme.Sun, 16f, 2f);
        }

        /// <summary>One sentence on how two species get on, both ways round.</summary>
        public static string DescribeRelationship(Species a, Species b)
        {
            var ab = Relationships.Get(a, b);
            var ba = Relationships.Get(b, a);
            string an = Plural(a), bn = Plural(b);
            if (a.id == b.id)
            {
                switch (ab)
                {
                    case Relationship.Predator: return $"{an} eat smaller {an}.";
                    case Relationship.Competitive: return $"{an} fight their own kind.";
                    case Relationship.Preferred: return $"{an} are happiest in a group of their own kind.";
                    default: return $"{an} get along fine with each other.";
                }
            }
            if (ab == Relationship.Predator && ba == Relationship.Predator) return $"{an} and {bn} hunt each other.";
            if (ab == Relationship.Predator) return $"{an} hunt {bn}.";
            if (ba == Relationship.Predator) return $"{bn} hunt {an}.";
            if (ab == Relationship.Competitive || ba == Relationship.Competitive) return $"{an} and {bn} nip at each other.";
            if (ab == Relationship.Preferred && ba == Relationship.Preferred) return $"{an} and {bn} do well together.";
            if (ab == Relationship.Preferred) return $"{an} like having {bn} around.";
            if (ba == Relationship.Preferred) return $"{bn} like having {an} around.";
            return $"{an} and {bn} get along fine.";
        }

        public static string Plural(Species species)
        {
            string name = species.displayName;
            if (name.EndsWith("fish") || name.EndsWith("Corydoras Catfish")) return name;
            if (name.EndsWith("y") && !name.EndsWith("ey")) return name.Substring(0, name.Length - 1) + "ies";
            return name + "s";
        }
    }
}
