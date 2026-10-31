using System.Collections.Generic;
using FishBreeder.Genetics;

namespace FishBreeder.Core
{
    /// <summary>
    /// The fish journal's discoveries: every species the player has seen for what it is (a fish
    /// that arrives known, or a mystery fry that grows enough to tell). The first sighting of a
    /// species is celebrated and opens the journal on it.
    /// </summary>
    public static partial class Journal
    {
        /// <summary>A heading in the journal and the fish categories under it.</summary>
        public class Group
        {
            public string title;
            public string blurb;
            public FishCategory[] categories;
        }

        public static readonly Group[] Groups =
        {
            new Group { title = "Cold fresh water", blurb = "Hardy fish for unheated tanks.", categories = new[] { FishCategory.FreshwaterCold } },
            new Group { title = "Tropical fresh water", blurb = "Warm-water fish from rivers and lakes.", categories = new[] { FishCategory.FreshwaterTropical, FishCategory.BottomFeeder, FishCategory.Shellfish } },
            new Group { title = "Salt water", blurb = "Reef fish: they need salt water tanks.", categories = new[] { FishCategory.SaltwaterTropical, FishCategory.SaltwaterCold } },
        };

        static readonly HashSet<string> discovered = new HashSet<string>();
        static readonly HashSet<string> unseen = new HashSet<string>();

        public static int Count => discovered.Count;

        public static void Reset()
        {
            discovered.Clear();
            unseen.Clear();
        }

        public static bool IsDiscovered(Species species) => species != null && discovered.Contains(species.id);

        /// <summary>Discovered but not looked at in the journal yet.</summary>
        public static bool IsNew(Species species) => species != null && unseen.Contains(species.id);

        public static void MarkSeen(Species species)
        {
            if (species != null) unseen.Remove(species.id);
        }

        /// <summary>The species in one journal group, in balance order.</summary>
        public static List<Species> SpeciesIn(Group group)
        {
            var list = new List<Species>();
            foreach (var species in Species.All)
                if (System.Array.IndexOf(group.categories, species.category) >= 0) list.Add(species);
            return list;
        }

        public static void Discover(Species species)
        {
            if (species == null || !discovered.Add(species.id)) return;
            unseen.Add(species.id);
            GameLog.Add($"New fish for your journal: the {species.displayName}!");
            Milestones.Report(MilestoneKind.DiscoverSpecies, discovered.Count);
            Fx.Discovery(species);
        }
    }
}
