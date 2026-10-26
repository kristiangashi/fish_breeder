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
                // Off somewhere new: a free spot well across the tank, in the band the species likes.
                Milling = false;
                var species = Members[0].Species;
                var b = tank.InnerBounds;
                float margin = Mathf.Max(bodyLength * 2f, 0.06f);
                Destination = Center;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var point = tank.RandomFreePoint(margin);
                    float low = b.min.y + margin, high = b.max.y - margin;
                    point.y = GameRandom.Range(Mathf.Lerp(low, high, species.depthMin), Mathf.Lerp(low, high, species.depthMax));
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
    }
}
