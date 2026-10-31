using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>What a shopper can complain about.</summary>
    public enum Complaint { DeadFish, DirtyWater, SickFish, HighPrices, FewFish, Fighting }

    /// <summary>
    /// What one kind of complaint does to the shop. The share of visitors who complained about it
    /// each day is remembered (fading over a few days); that share times <see cref="perShare"/>,
    /// up to <see cref="maxPenalty"/>, comes off foot traffic or off the chance shoppers buy.
    /// </summary>
    [Serializable]
    public class ComplaintBalance
    {
        public Complaint complaint;
        [Tooltip("What the reputation screen calls it.")]
        public string label;
        [Tooltip("Lowers the chance shoppers buy (otherwise it lowers foot traffic: fewer people come in).")]
        public bool sales;
        [Tooltip("Penalty for each share of visitors complaining (1 = a 10% share of complaints takes off 10%).")]
        public float perShare = 1f;
        [Tooltip("Most this complaint can take off.")]
        [Range(0f, 1f)] public float maxPenalty = 0.2f;

        static ComplaintBalance Make(Complaint complaint, string label, bool sales, float perShare, float maxPenalty)
            => new ComplaintBalance { complaint = complaint, label = label, sales = sales, perShare = perShare, maxPenalty = maxPenalty };

        public static List<ComplaintBalance> CreateDefaults() => new List<ComplaintBalance>
        {
            Make(Complaint.DeadFish, "Dead fish in the tanks", false, 1f, 0.25f),
            Make(Complaint.DirtyWater, "Dirty water and algae", false, 0.8f, 0.2f),
            Make(Complaint.SickFish, "Sick-looking fish", true, 0.8f, 0.2f),
            Make(Complaint.HighPrices, "Prices too high", true, 0.6f, 0.2f),
            Make(Complaint.FewFish, "Not enough fish for sale", false, 0.6f, 0.15f),
            Make(Complaint.Fighting, "Fish fighting in the tanks", true, 0.8f, 0.2f),
        };
    }

    [Serializable]
    public class ReputationBalance
    {
        [Tooltip("Share of yesterday's complaints still remembered today (0.6: a bad day fades over three or four days).")]
        [Range(0f, 0.95f)] public float memory = 0.6f;
        [Tooltip("Reviews kept on the reputation screen.")]
        public int reviewsKept = 12;
        [Tooltip("Reputation a perfect day adds near zero stars (reputation runs 0..1, so 0.02 is a tenth of a star).")]
        public float gainPerDay = 0.02f;
        [Tooltip("Gains shrink as the shop gets better known: gain x (1 - reputation) to this power.")]
        public float gainFalloff = 1.5f;
        [Tooltip("Reputation a terrible day takes off.")]
        public float lossPerDay = 0.04f;
        [Tooltip("Share of good reactions (happy shoppers, and sales counted twice) a day needs before reputation goes up; below it, it goes down.")]
        [Range(0.05f, 0.95f)] public float neutralScore = 0.6f;
        [Tooltip("A day with fewer visitors than this moves reputation less, in proportion.")]
        public int fullDayVisitors = 15;
        [Tooltip("Foot traffic is multiplied by the first number at 0 stars and the second at 5 stars.")]
        public float trafficAtNoStars = 0.75f, trafficAtFiveStars = 1.75f;
        [Tooltip("Customer wishes (\"Wish they had more catfish\") the day's reviews can carry.")]
        public int wishesPerDay = 1;
        public List<ComplaintBalance> complaints = ComplaintBalance.CreateDefaults();
    }

    /// <summary>
    /// Word of mouth. Every complaint a shopper makes is counted for the day; at closing, the share
    /// of visitors who complained about each thing joins a fading memory of the last few days, and
    /// that memory costs foot traffic (dead fish, dirty water, not much for sale) or sales (high
    /// prices, sick or fighting fish). The reputation screen shows each one, what it costs, and
    /// the rooms more stars unlock. Reviews are written here too, in many voices.
    /// </summary>
    public static partial class Reputation
    {
        public const int Count = 6;

        public class Review
        {
            public int day;
            public float stars;
            public string text;
        }

        static readonly float[] heat = new float[Count];
        static readonly List<Review> reviews = new List<Review>();

        static ReputationBalance Data => Balance.Data.reputation;

        public static IReadOnlyList<Review> Reviews => reviews;

        public static void Reset()
        {
            Array.Clear(heat, 0, heat.Length);
            reviews.Clear();
            LatestWishes.Clear();
        }

        public static ComplaintBalance Info(Complaint complaint)
        {
            foreach (var c in Data.complaints) if (c != null && c.complaint == complaint) return c;
            return null;
        }

        /// <summary>Share of visitors complaining about this lately (0..1), from the fading memory.</summary>
        public static float Heat(Complaint complaint) => heat[(int)complaint];

        /// <summary>What this complaint takes off traffic or sales right now (0..max).</summary>
        public static float Penalty(Complaint complaint)
        {
            var info = Info(complaint);
            if (info == null) return 0f;
            return Mathf.Min(info.maxPenalty, Heat(complaint) * info.perShare);
        }

        public static bool HurtsSales(Complaint complaint)
        {
            var info = Info(complaint);
            return info != null && info.sales;
        }

        /// <summary>Foot traffic after complaints (1 = no effect).</summary>
        public static float TrafficMultiplier => Multiplier(false);

        /// <summary>Chance of buying after complaints (1 = no effect).</summary>
        public static float SalesMultiplier => Multiplier(true);

        static float Multiplier(bool sales)
        {
            float m = 1f;
            for (int i = 0; i < Count; i++)
                if (HurtsSales((Complaint)i) == sales) m *= 1f - Penalty((Complaint)i);
            return m;
        }

        public static string Label(Complaint complaint)
        {
            var info = Info(complaint);
            return info != null && !string.IsNullOrEmpty(info.label) ? info.label : complaint.ToString();
        }

        /// <summary>
        /// A day ended: what visitors complained about joins the memory (a day with no visitors
        /// lets it fade), and the day's reviews are kept.
        /// </summary>
        public static void EndOfDay(int visitors, int[] complaints, float stars, List<string> written, int day)
        {
            float keep = Mathf.Clamp(Data.memory, 0f, 0.95f);
            for (int i = 0; i < Count; i++)
            {
                float share = visitors > 0 && complaints != null ? Mathf.Clamp01(complaints[i] / (float)visitors) : 0f;
                heat[i] = heat[i] * keep + share * (1f - keep);
                if (heat[i] < 0.002f) heat[i] = 0f;
            }
            if (written == null) return;
            foreach (var text in written) reviews.Insert(0, new Review { day = day, stars = stars, text = text });
            int max = Mathf.Max(1, Data.reviewsKept);
            if (reviews.Count > max) reviews.RemoveRange(max, reviews.Count - max);
        }

        /// <summary>
        /// The day's reviews: one for each of the most common complaints (most first), then praise
        /// if the day went well. At most <paramref name="max"/>.
        /// </summary>
        public static List<string> Write(int visitors, int sold, int happy, int[] complaints, Dictionary<string, int> wishes = null, int max = 3)
        {
            var result = new List<string>();
            LatestWishes.Clear();
            if (wishes != null) foreach (var pair in wishes) LatestWishes.Add(new KeyValuePair<string, int>(pair.Key, pair.Value));
            LatestWishes.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (visitors == 0) return result;
            var order = new List<int>();
            for (int i = 0; i < Count; i++) if (complaints[i] > 0) order.Add(i);
            order.Sort((a, b) => complaints[b].CompareTo(complaints[a]));
            int bad = 0;
            foreach (var i in order) bad += complaints[i];
            bool goodDay = sold > 0 && bad <= happy / 2;
            int complaintSlots = goodDay ? max - 1 : max;
            for (int k = 0; k < order.Count && k < complaintSlots; k++) result.Add(Pick(ComplaintReviews((Complaint)order[k]), result));
            if (goodDay || result.Count == 0) result.Insert(0, Pick(sold > 0 ? Praise : Meh, result));

            // What people came for and couldn't find: the most wished-for kinds, in their words.
            int wishLines = Mathf.Max(0, Data.wishesPerDay);
            for (int k = 0; k < LatestWishes.Count && k < wishLines; k++)
            {
                var species = Genetics.Species.Find(LatestWishes[k].Key);
                if (species == null) continue;
                string line = Pick(Wishes, result);
                result.Add(line.Replace("{many}", Research.Plural(species).ToLower()).Replace("{one}", species.displayName.ToLower()));
            }
            return result;
        }

        /// <summary>The last day's wishes: species id and how many shoppers left without one, most first.</summary>
        public static readonly List<KeyValuePair<string, int>> LatestWishes = new List<KeyValuePair<string, int>>();

        static readonly string[] Wishes =
        {
            "Wish they had more {many}.",
            "Came in looking for {many}. Not a single one for sale.",
            "Would love to see some {many} here one day.",
            "Any chance of getting {many} in? I'd buy one tomorrow.",
            "Nice shop, but no {many}? Come on!",
            "My daughter really wanted a {one}. Maybe next time.",
            "I'll be back when they've got {many}.",
            "Great tanks. Now they just need {many}.",
            "Asked about a {one} and got a shrug. Please stock them!",
            "Drove over just for {many}. Left empty-handed.",
            "If they ever sell {many}, I'm their first customer.",
            "Lovely fish, but I had my heart set on a {one}.",
        };

        static string Pick(string[] options, List<string> already)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                string pick = options[GameRandom.Range(0, options.Length)];
                if (!already.Contains(pick)) return pick;
            }
            return options[GameRandom.Range(0, options.Length)];
        }

        static string[] ComplaintReviews(Complaint complaint)
        {
            switch (complaint)
            {
                case Complaint.DeadFish: return DeadFish;
                case Complaint.DirtyWater: return DirtyWater;
                case Complaint.SickFish: return SickFish;
                case Complaint.HighPrices: return HighPrices;
                case Complaint.FewFish: return FewFish;
                default: return Fighting;
            }
        }

        static readonly string[] DeadFish =
        {
            "There were dead fish floating in the tanks. So sad.",
            "My kid asked why the fish was sleeping upside down. I didn't have the heart to tell her.",
            "A dead fish was just lying there in the corner of the tank. Nobody seemed to notice.",
            "Pretty shop, but I counted two belly-up fish. Not a great sign.",
            "Does anyone check the tanks? There was a dead one right at the front.",
            "Came to buy fish, saw a funeral instead.",
            "If they can't keep the fish in the shop alive, how will mine do at home?",
            "One of the tanks had a dead fish in it. Put me right off.",
            "I wanted the orange one. Then I realised it wasn't moving. At all.",
            "Floaters in the display tank. Ugh.",
            "Nice staff, but please take the dead fish out before opening.",
            "Very sad visit. RIP to the little guy in tank two.",
        };

        static readonly string[] DirtyWater =
        {
            "The water looked filthy. Do they ever clean those tanks?",
            "Couldn't even see the fish through all the green.",
            "The glass was so dirty I thought it was frosted on purpose.",
            "Murky water, smelly shop. Not coming back.",
            "The algae in there has more personality than the fish.",
            "Cloudy tanks everywhere. I worry about those poor fish.",
            "Lovely fish, I think. Hard to tell through the gunk.",
            "Someone needs to wipe the glass. Desperately.",
            "Green water is for ponds, not shop tanks.",
            "It smelled a bit like a swamp in there.",
            "I wouldn't buy a fish that's been living in soup.",
            "The tanks need a good clean. Or three.",
        };

        static readonly string[] SickFish =
        {
            "Half the fish had white spots. I wasn't risking it.",
            "Some of the fish looked really poorly. Fins all ragged.",
            "The fish looked tired and pale. Are they okay?",
            "I'm no expert, but those fish did not look healthy.",
            "Saw a fish with fuzzy patches on it. No thank you.",
            "Sickly-looking stock. I'll buy somewhere else.",
            "Didn't want to bring disease home to my tank.",
            "The fish were just hanging at the top, gasping. Worrying.",
        };

        static readonly string[] HighPrices =
        {
            "Nice fish, but the prices are ridiculous.",
            "How much?! For a fish?!",
            "I could buy a whole aquarium for what they want for one fish.",
            "Lovely shop, shame about the price tags.",
            "Way overpriced. The place down the road is half this.",
            "I nearly dropped my wallet when I saw the prices.",
            "Great fish, but I'm not paying that.",
            "Do the fish come with a diamond collar at those prices?",
            "Beautiful stock, wallet-crushing prices.",
            "Came in happy, left with a sticker shock.",
            "I'd have bought three if they were reasonably priced.",
            "Too pricey for a casual fishkeeper like me.",
        };

        static readonly string[] FewFish =
        {
            "Nice enough. Not much for sale though.",
            "Lots of tanks, hardly any fish to buy.",
            "I wanted to buy something, but there was barely anything for sale.",
            "Pretty to look at, but the sale tanks were almost empty.",
            "Came for fish, found mostly water.",
            "They need more stock. Two fish isn't a selection.",
            "Half-empty sale tank. Maybe come back next week.",
            "Nothing caught my eye because there was nothing to catch it.",
            "Great display tanks, but you can't buy those ones.",
            "Is this a fish shop or a water shop?",
        };

        static readonly string[] Fighting =
        {
            "The fish were fighting! One was chasing the others all around.",
            "Watched a big fish bully the little ones. Horrible.",
            "Who puts those two kinds in the same tank? Total chaos.",
            "There's a tiny shark-wannabe in there terrorising everyone.",
            "My son cried when one fish bit another. Not a fun trip.",
            "Feels more like a boxing ring than an aquarium.",
            "The fish looked stressed out from all the chasing.",
            "Fin-nipping galore. I don't want to buy a fish with no tail.",
            "Somebody needs to separate the fish that don't get along.",
            "Saw a fish try to eat another one. I'm traumatised.",
        };

        static readonly string[] Praise =
        {
            "Lovely little shop, healthy fish. Will be back!",
            "Beautiful tanks and friendly service. Five stars!",
            "Found exactly the fish I wanted. Happy customer!",
            "Such a relaxing place. I could watch the tanks all day.",
            "Great fish, fair prices, clean tanks. What more could you want?",
            "My new fish are thriving. Thanks!",
            "The display tanks are gorgeous. Worth a visit just for those.",
            "Best fish shop in town, hands down.",
            "Knowledgeable owner and really healthy stock.",
            "The kids loved it. We'll be back for more fish!",
            "Spotless tanks and happy fish. A real gem.",
            "Bought one fish, came back the same afternoon for two more.",
        };

        static readonly string[] Meh =
        {
            "Nice enough place. Didn't find anything today.",
            "Had a look around. Pleasant, but nothing for me.",
            "Pretty tanks. Maybe next time.",
            "Fine. Just browsing really.",
            "Cute little shop. I'll keep an eye on it.",
        };
    }
}
