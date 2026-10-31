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
    public enum ResearchState { Locked, Bounty, Ready, Queued, Researching, Done }

    /// <summary>
    /// Research projects. Each project posts a bounty (feed fish 8 times, cure 2 sick fish, keep 15
    /// fish at once...) once the Research level and any project it builds on allow. Doing the
    /// bounty makes the project ready; the player then starts it in the research window. Like in
    /// Civilization, the bench earns research points every night towards the project it's on (one
    /// at a time; switching keeps what's been done): a base amount, plus more for the daily
    /// research budget, which is paid each night (never while the bench is empty), all faster at
    /// a higher Research level. Up to three more ready projects can wait in a queue: when one
    /// finishes, the next goes on the bench by itself, with any points left over. A
    /// finished project makes the game tell you more: what temperature fish
    /// like, who they fight with, why they won't breed, how much each thing does to their health.
    /// Until then those messages stay vague. Bounties and research both give Research XP, and every
    /// kind of fish you keep earns a little at each time step.
    /// </summary>
    public static partial class Research
    {
        /// <summary>Bounty progress for counted bounties, per project id, since the bounty was posted.</summary>
        static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        static readonly Dictionary<MilestoneKind, int> totals = new Dictionary<MilestoneKind, int>();
        static readonly HashSet<string> posted = new HashSet<string>();
        static readonly HashSet<string> ready = new HashSet<string>();
        static readonly HashSet<string> done = new HashSet<string>();
        static readonly HashSet<ResearchUnlock> unlocked = new HashSet<ResearchUnlock>();
        static readonly HashSet<string> favouritesSeen = new HashSet<string>();
        static readonly Dictionary<string, float> progress = new Dictionary<string, float>();
        static string current;
        static readonly List<string> queue = new List<string>();

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
            progress.Clear();
            current = null;
            queue.Clear();
            DailyBudget = 0;
        }

        /// <summary>Money put into research every night (paid only while a project is at the bench).</summary>
        public static int DailyBudget { get; private set; }

        public static void SetBudget(int dollars) => DailyBudget = Mathf.Clamp(dollars, 0, Mathf.Max(0, Data.maxDailyBudget));

        /// <summary>Multiplies every night's points: 1 at Research level 1, more at higher levels and with perks.</summary>
        public static float Speed => (1f + Data.speedPerLevel * (Progression.Level(XpTrack.Research) - 1)) * Perks.More(PerkEffect.FasterResearch);

        /// <summary>Research points a night brings with this budget.</summary>
        public static float PointsPerNight(int budget) => (Data.basePointsPerDay + Mathf.Max(0, budget) * Data.pointsPerDollar) * Speed;

        public static float PointsPerNight() => PointsPerNight(DailyBudget);

        /// <summary>Points earned so far on a project (kept when you switch away from it).</summary>
        public static float Progress(ResearchProjectBalance project) => project != null && progress.TryGetValue(project.id, out var p) ? p : 0f;

        public static int Cost(ResearchProjectBalance project) => project == null ? 0 : Mathf.Max(1, project.points);

        public static float Fraction(ResearchProjectBalance project) => project == null ? 0f : Mathf.Clamp01(Progress(project) / Cost(project));

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
        public static float CurrentFraction => Fraction(Current);

        /// <summary>Nights until a project is done at this budget.</summary>
        public static int NightsFor(ResearchProjectBalance project, int budget)
        {
            if (project == null) return 0;
            return Mathf.Max(1, Mathf.CeilToInt((Cost(project) - Progress(project)) / Mathf.Max(0.01f, PointsPerNight(budget))));
        }

        public static int NightsFor(ResearchProjectBalance project) => NightsFor(project, DailyBudget);

        /// <summary>Nights until the project at the bench is done, at today's budget.</summary>
        public static int NightsLeft => NightsFor(Current);

        public static ResearchState State(ResearchProjectBalance project)
        {
            if (project == null) return ResearchState.Locked;
            if (done.Contains(project.id)) return ResearchState.Done;
            if (current == project.id) return ResearchState.Researching;
            if (queue.Contains(project.id)) return ResearchState.Queued;
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

        /// <summary>
        /// Puts a ready project on the research bench. If another project is there it goes back on
        /// the shelf with its points kept, to pick up again later.
        /// </summary>
        public static bool Start(ResearchProjectBalance project)
        {
            if (project == null || !ready.Contains(project.id)) return false;
            var previous = Current;
            if (previous != null)
            {
                ready.Add(previous.id);
                GameLog.Add($"{previous.name} goes back on the shelf with {Progress(previous):0}/{Cost(previous)} points.");
            }
            ready.Remove(project.id);
            current = project.id;
            GameLog.Add($"Research started: {project.name}. About {NightsFor(project)} night{(NightsFor(project) == 1 ? "" : "s")} at {PointsPerNight():0} points a night.");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Most projects that can wait in the queue behind the bench.</summary>
        public static int QueueSize => Mathf.Max(0, Data.queueSize);

        /// <summary>The projects waiting behind the bench, next first.</summary>
        public static List<ResearchProjectBalance> Queue
        {
            get
            {
                var list = new List<ResearchProjectBalance>();
                foreach (var id in queue)
                {
                    var project = Find(id);
                    if (project != null) list.Add(project);
                }
                return list;
            }
        }

        /// <summary>Lines a ready project up behind the bench (or starts it if the bench is free).</summary>
        public static bool Enqueue(ResearchProjectBalance project)
        {
            if (project == null || !ready.Contains(project.id)) return false;
            if (current == null) return Start(project);
            if (queue.Count >= QueueSize) return false;
            ready.Remove(project.id);
            queue.Add(project.id);
            GameLog.Add($"{project.name} is queued for research ({queue.Count} waiting).");
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes a project out of the queue, back onto the shelf.</summary>
        public static void Dequeue(ResearchProjectBalance project)
        {
            if (project == null || !queue.Remove(project.id)) return;
            ready.Add(project.id);
            Changed?.Invoke();
        }

        /// <summary>Moves a queued project one place nearer the bench.</summary>
        public static void MoveUp(ResearchProjectBalance project)
        {
            int index = project == null ? -1 : queue.IndexOf(project.id);
            if (index <= 0) return;
            queue.RemoveAt(index);
            queue.Insert(index - 1, project.id);
            Changed?.Invoke();
        }

        /// <summary>Nights until a queued project would be done, counting the ones ahead of it.</summary>
        public static int NightsUntilDone(ResearchProjectBalance project)
        {
            float points = Mathf.Max(0.01f, PointsPerNight());
            float needed = Current != null ? Cost(Current) - Progress(Current) : 0f;
            foreach (var id in queue)
            {
                var next = Find(id);
                if (next == null) continue;
                needed += Cost(next) - Progress(next);
                if (next == project) break;
            }
            return Mathf.Max(1, Mathf.CeilToInt(needed / points));
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

        /// <summary>Time step: every kind of fish kept earns a little Research XP.</summary>
        public static void OnTimeStep(IEnumerable<Tank> tanks)
        {
            var kept = new HashSet<string>();
            foreach (var tank in tanks)
                foreach (var fish in tank.Residents)
                    if (fish != null && !fish.IsDead && !fish.IdentityHidden) kept.Add(fish.Species.id);
            if (kept.Count > 0) Progression.Award(XpTrack.Research, Data.xpPerSpeciesPerStep * kept.Count);
            Refresh();
        }

        /// <summary>
        /// The night's research: the budget is paid (never so much that it leaves you broke) and
        /// the bench earns its points. Nothing is paid while the bench is empty.
        /// </summary>
        public static void OnNight()
        {
            var project = Current;
            if (project == null) return;
            int paid = 0;
            if (DailyBudget > 0)
            {
                int affordable = DevMode.On ? DailyBudget : Mathf.Clamp(Economy.Money - Balance.Data.store.brokeBelow, 0, DailyBudget);
                if (affordable > 0 && Economy.TrySpend(affordable, $"research on {project.name}", MoneyKind.Research)) paid = affordable;
                if (paid < DailyBudget) GameLog.Add($"Money was short, so only {Economy.Format(paid)} of the {Economy.Format(DailyBudget)} research budget went in.");
            }
            float points = PointsPerNight(paid);
            progress[project.id] = Progress(project) + points;
            GameLog.Add($"Research: +{points:0} points on {project.name} ({Mathf.Min(Progress(project), Cost(project)):0}/{Cost(project)})" + (paid > 0 ? $", {Economy.Format(paid)} spent." : "."));
            if (Progress(project) < Cost(project)) return;
            float spare = Progress(project) - Cost(project);
            Finish(project);
            StartNextInQueue(spare);
        }

        /// <summary>The bench is free: the next queued project goes on it, with the night's spare points.</summary>
        static void StartNextInQueue(float spare)
        {
            if (current != null || queue.Count == 0) return;
            var next = Find(queue[0]);
            queue.RemoveAt(0);
            if (next == null) return;
            current = next.id;
            progress[next.id] = Progress(next) + spare;
            GameLog.Add($"Next up at the research bench: {next.name}.");
            Changed?.Invoke();
            if (Progress(next) >= Cost(next))
            {
                float more = Progress(next) - Cost(next);
                Finish(next);
                StartNextInQueue(more);
            }
        }

        static void Finish(ResearchProjectBalance project)
        {
            current = null;
            progress.Remove(project.id);
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
            if (Current != null)
            {
                Finish(Current);
                StartNextInQueue(0f);
            }
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
