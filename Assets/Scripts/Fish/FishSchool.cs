using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// A school of one species in one tank. Schooling fish only school once there are enough of
    /// them (<see cref="Genetics.Species.schoolSize"/>, counting juveniles and adults, not fry);
    /// then they all do it together. The school shares a destination and takes turns between
    /// travelling to it and milling about loosely; each fish steers towards it while staying near
    /// the others, heading roughly the same way and not bumping into its neighbours
    /// (see <see cref="FishSwimmer"/>). Kept loose on purpose: a real tank school is a relaxed group,
    /// not a tight bait ball.
    /// </summary>
    public class FishSchool
    {
        static readonly Dictionary<Tank, Dictionary<string, FishSchool>> schools = new Dictionary<Tank, Dictionary<string, FishSchool>>();

        public readonly List<Fish> Members = new List<Fish>();
        /// <summary>Middle of the school and the way it's heading on average, this frame.</summary>
        public Vector3 Center, Heading;
        /// <summary>Where the school is going; while milling, a spot near where it stopped.</summary>
        public Vector3 Destination;
        public bool Milling;

        readonly Tank tank;
        readonly string speciesId;
        int frame = -1;
        float phaseEnds;

        FishSchool(Tank tank, string speciesId)
        {
            this.tank = tank;
            this.speciesId = speciesId;
        }

        /// <summary>The school this fish swims in right now, or null when it doesn't school (or there aren't enough of its kind).</summary>
        public static FishSchool For(Fish fish)
        {
            var tank = fish.Tank;
            var species = fish.Species;
            if (tank == null || species == null || species.schoolSize <= 1 || fish.IsDead || fish.Stage == LifeStage.Fry) return null;
            if (!schools.TryGetValue(tank, out var bySpecies))
            {
                schools[tank] = bySpecies = new Dictionary<string, FishSchool>();
                // Forget tanks that are gone.
                var gone = new List<Tank>();
                foreach (var t in schools.Keys) if (t == null) gone.Add(t);
                foreach (var t in gone) schools.Remove(t);
            }
            if (!bySpecies.TryGetValue(species.id, out var school))
                bySpecies[species.id] = school = new FishSchool(tank, species.id);
            school.Refresh(species.schoolSize, fish.BodyLength);
            return school.Members.Count >= species.schoolSize && school.Members.Contains(fish) ? school : null;
        }

        /// <summary>Once a frame: who is in the school, where it is, and whether it's time to move on.</summary>
        void Refresh(int needed, float bodyLength)
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            Members.Clear();
            foreach (var fish in tank.Residents)
                if (fish != null && !fish.IsDead && fish.Stage != LifeStage.Fry && fish.Species != null && fish.Species.id == speciesId)
                    Members.Add(fish);
            if (Members.Count < needed)
            {
                phaseEnds = 0f;
                return;
            }

            Center = Vector3.zero;
            Heading = Vector3.zero;
            foreach (var fish in Members)
            {
                Center += fish.transform.position;
                Heading += fish.transform.forward;
            }
            Center /= Members.Count;
            Heading = Heading.sqrMagnitude > 1e-6f ? Heading.normalized : Vector3.forward;

            bool arrived = !Milling && (Destination - Center).magnitude < bodyLength * 2f;
            if (Time.time < phaseEnds && !arrived) return;
            if (Milling || phaseEnds == 0f)
            {
                Milling = false;
                var species = Members[0].Species;
                if (VisitLikedPiece(species, bodyLength)) return;
                // Off somewhere new: a free spot well across the tank, mostly in the band the species likes.
                atPiece = -1;
                var b = tank.InnerBounds;
                float margin = Mathf.Max(bodyLength * 2f, 0.06f);
                Destination = Center;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var point = tank.RandomFreePoint(margin);
                    point.y = FishSwimmer.TripHeight(species, b.min.y + margin, b.max.y - margin);
                    Destination = point;
                    if ((point - Center).magnitude > b.size.x * 0.3f) break;
                }
                phaseEnds = Time.time + GameRandom.Range(8f, 14f);
            }
            else
            {
                // Mill about for a while where they got to.
                Milling = true;
                Destination = Center;
                phaseEnds = Time.time + GameRandom.Range(3f, 7f);
            }
        }

        readonly List<int> liked = new List<int>();
        /// <summary>The decoration the school is working its way round, or -1.</summary>
        int atPiece = -1;

        /// <summary>
        /// Schools of fish that like a decoration (tetras and their plants) spend most of their time
        /// at one: each leg goes to another part of it, low or high, round the side or in among the
        /// leaves, until they move on to another favourite or out into the tank. True if it picked one.
        /// </summary>
        bool VisitLikedPiece(Genetics.Species species, float bodyLength)
        {
            FishSwimmer.LikedPieces(tank, species, liked);
            if (liked.Count == 0 || !GameRandom.Chance(0.75f)) return false;
            int piece = liked.Contains(atPiece) && GameRandom.Chance(0.7f) ? atPiece : liked[GameRandom.Range(0, liked.Count)];
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var point = FishSwimmer.PointAroundPiece(tank, piece, bodyLength, Mathf.Max(bodyLength, 0.05f), 0.3f, 1.5f);
                if (point == null) continue;
                atPiece = piece;
                Destination = point.Value;
                phaseEnds = Time.time + GameRandom.Range(5f, 9f);
                return true;
            }
            return false;
        }
    }
}
