using System.Collections.Generic;
using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The aquarium: its bounds, the fish living (and lying dead) in it, its water and equipment.
    /// Water changes only at time steps (shop opening and closing) and through the player's actions,
    /// so a tank left alone while the shop is closed stays exactly as it is.
    /// </summary>
    public class Tank : MonoBehaviour
    {
        [SerializeField] Vector3 innerSize = new Vector3(1.6f, 0.9f, 0.7f);
        [SerializeField] int sizeIndex;
        [SerializeField, Range(0f, 1f)] float waterQuality = 1f;
        [SerializeField, Range(0f, 1f)] float algae;
        [SerializeField] float temperature = 22f;
        [SerializeField] float ph = 7.2f;
        [SerializeField] float salinity;
        [SerializeField] bool pests;
        [SerializeField] float heaterSetting = 25f;
        [SerializeField] InstalledEquipment filter, heater, bubbler;

        readonly List<Fish> residents = new List<Fish>();
        readonly List<Fish> corpses = new List<Fish>();
        Material backWallMaterial;
        TankWater water;
        Transform visuals;
        TankDecor decor;
        TankBubbles bubbles;
        /// <summary>The 3D model of each kind of equipment, by <see cref="EquipmentKind"/>.</summary>
        readonly GameObject[] equipmentModels = new GameObject[3];
        static readonly Color ClearWater = new Color(0.12f, 0.35f, 0.55f);
        static readonly Color MurkyWater = new Color(0.25f, 0.33f, 0.12f);
        static readonly Color AlgaeWater = new Color(0.18f, 0.42f, 0.16f);

        /// <summary>The tank the camera is looking into; null in the store view.</summary>
        public static Tank Focused;

        /// <summary>Living fish.</summary>
        public IReadOnlyList<Fish> Residents => residents;
        /// <summary>Dead fish floating at the top until the player removes them.</summary>
        public IReadOnlyList<Fish> Corpses => corpses;
        public float WaterQuality => waterQuality;
        public float Algae => algae;
        public float Temperature => temperature;
        public float Ph => ph;
        public float Salinity => salinity;
        public bool IsSaltwater => salinity > 15f;
        public bool HasPests => pests;
        /// <summary>Name the player knows the tank by, for messages ("Breeder tank 1").</summary>
        public string Title { get; set; } = "a tank";
        public bool HasFilter => filter != null;
        public bool HasHeater => heater != null;
        public bool HasBubbler => bubbler != null;
        public bool FilterWorking => filter != null && !filter.broken;
        public bool HeaterWorking => heater != null && !heater.broken;
        public bool BubblerWorking => bubbler != null && !bubbler.broken;

        /// <summary>The installed filter, heater or bubbler, or null.</summary>
        public InstalledEquipment GetEquipment(EquipmentKind kind)
        {
            switch (kind)
            {
                case EquipmentKind.Filter: return filter;
                case EquipmentKind.Heater: return heater;
                default: return bubbler;
            }
        }

        /// <summary>Equipment that has broken down and needs a repair kit.</summary>
        public List<InstalledEquipment> BrokenEquipment
        {
            get
            {
                var list = new List<InstalledEquipment>();
                foreach (var kind in Equipment.Kinds)
                {
                    var item = GetEquipment(kind);
                    if (item != null && item.broken) list.Add(item);
                }
                return list;
            }
        }
        public float HeaterSetting => heaterSetting;
        public int WipesLeft { get; private set; }
        public TankDecor Decor => decor;
        public int DecorCount => decor != null ? decor.Count : 0;
        public bool HasPlants => decor != null && decor.HasPlants;
        /// <summary>Fed since the last time step (the to-do list uses it to know hungry fish were seen to).</summary>
        public bool FedSinceTimeStep { get; set; }

        /// <summary>Which of the balance's tank sizes this is (0 = the standard size every tank starts as).</summary>
        public int SizeIndex => sizeIndex;
        public TankSizeBalance SizeInfo
        {
            get
            {
                var sizes = Balance.Data.store.tankSizes;
                if (sizes == null || sizes.Count == 0) return new TankSizeBalance { name = "Standard" };
                return sizes[Mathf.Clamp(sizeIndex, 0, sizes.Count - 1)];
            }
        }
        /// <summary>
        /// The tank's fish limit. It's a soft limit: you can put more in (up to <see cref="HardCapacity"/>),
        /// but every fish over it hurts the health of all of them, dirties the water faster and spreads illness.
        /// </summary>
        public int Capacity => CapacityOf(SizeInfo);
        /// <summary>How many fish a tank of this size holds, with the Roomy tanks perk.</summary>
        public static int CapacityOf(TankSizeBalance size) => Mathf.Max(1, Mathf.RoundToInt(size.capacity * Perks.More(PerkEffect.RoomierTanks)));
        /// <summary>Decoration spots: the size's own, plus any from the Aquascaper perk.</summary>
        public int DecorationSlots => SizeInfo.decorationSlots + Perks.Count(PerkEffect.DecorationSpots);
        /// <summary>No more fish than this fit at all.</summary>
        public int HardCapacity => Mathf.Max(Capacity, Mathf.FloorToInt(Capacity * Balance.Data.store.tankHardLimitMultiplier));
        /// <summary>Fish over the limit (0 when within it).</summary>
        public int OverLimit => Mathf.Max(0, residents.Count - Capacity);
        public bool IsOvercrowded => residents.Count > Capacity;
        public Vector3 InnerSize => innerSize;
        /// <summary>Aquarium quality from its equipment and decorations; it opens up bigger sizes (see <see cref="TankQuality"/>).</summary>
        public int Quality => TankQuality.Score(this);

        /// <summary>Fish the tank keeps in full oxygen: more with a (working) bubbler, and more with a better one.</summary>
        public int OxygenFor => BubblerWorking ? Mathf.Max(1, Mathf.RoundToInt(bubbler.Info.effect)) : Mathf.Max(1, GameConfig.Active.fishPerTankWithoutBubbler);

        /// <summary>1 when there's plenty, dropping as more fish share the water than the air supply covers.</summary>
        public float Oxygen
        {
            get
            {
                int limit = OxygenFor;
                return Mathf.Clamp01(1f - Mathf.Max(0, residents.Count - limit) / (float)limit);
            }
        }

        /// <summary>One number for how the water looks to a customer: 1 sparkling, 0 a swamp.</summary>
        public float WaterHealth => Mathf.Clamp01(Mathf.Min(waterQuality, 1f - algae * 0.8f) - (pests ? 0.15f : 0f));

        /// <summary>The water, in world space. Tanks only ever turn in quarter turns, so this stays exact when one faces a side wall.</summary>
        public Bounds InnerBounds
        {
            get
            {
                var size = transform.rotation * innerSize;
                return new Bounds(transform.position + Vector3.up * innerSize.y * 0.5f, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
            }
        }

        /// <summary>The side customers look in from (the backdrop is on the other side).</summary>
        public Vector3 Front => -transform.forward;
        /// <summary>Height of the water's surface, where bubbles pop and ripples spread.</summary>
        public float SurfaceY => InnerBounds.max.y - 0.04f;
        /// <summary>Not even one more fish fits (the hard limit, well past the soft one).</summary>
        public bool IsFull => residents.Count >= HardCapacity;
        /// <summary>Room left before the hard limit.</summary>
        public int FreeSpace => Mathf.Max(0, HardCapacity - residents.Count);
        /// <summary>Room left before the tank is overcrowded.</summary>
        public int FreeSpaceBeforeLimit => Mathf.Max(0, Capacity - residents.Count);

        /// <summary>1 in clean water, up to GameConfig.dirtyWaterInfectionMultiplier in filthy water; more again when overcrowded.</summary>
        public float InfectionMultiplier => Mathf.Lerp(GameConfig.Active.dirtyWaterInfectionMultiplier, 1f, waterQuality) * (pests ? 1.3f : 1f)
                                            * (1f + OverLimit * Balance.Data.timeStep.overcrowdedIllnessPerFish);

        /// <summary>Wipes every tank gets per closed-shop session: one, and more as your Fishkeeping level grows.</summary>
        public static int WipesPerSession
        {
            get
            {
                var balance = Balance.Data.timeStep;
                int every = balance.extraWipeEveryFishkeepingLevels;
                return Mathf.Max(1, balance.wipesPerSession + (every > 0 ? (Progression.Fishkeeping - 1) / every : 0) + Perks.Count(PerkEffect.ExtraWipes));
            }
        }

        void Awake()
        {
            temperature = GameConfig.Active.roomTemperature;
            ph = GameConfig.Active.startingPh;
            WipesLeft = WipesPerSession;
            Perks.Changed += OnPerksChanged;
        }

        void OnDestroy() => Perks.Changed -= OnPerksChanged;

        /// <summary>A perk can add decoration spots: lay the tank's decorations out again with the new count.</summary>
        void OnPerksChanged() => decor.SetSpotCount(DecorationSlots);

        /// <summary>For Sale tanks: fish arriving without a price get their appraised value as a starting price.</summary>
        public bool PricesNewFish { get; set; }

        public void Add(Fish fish)
        {
            corpses.Remove(fish);
            if (!residents.Contains(fish)) residents.Add(fish);
            if (PricesNewFish && fish.Price <= 0) fish.Price = fish.AppraisedValue;
        }

        public void Remove(Fish fish)
        {
            residents.Remove(fish);
            corpses.Remove(fish);
        }

        /// <summary>A fish died here: it stays (and slowly harms the others) until removed.</summary>
        public void AddCorpse(Fish fish)
        {
            residents.Remove(fish);
            if (!corpses.Contains(fish)) corpses.Add(fish);
        }

        public void Pollute(float amount) => waterQuality = Mathf.Clamp01(waterQuality - amount);

        /// <summary>One wipe: scrubs algae and freshens the water. Limited per closed-shop session.</summary>
        public bool Clean()
        {
            if (WipesLeft <= 0)
            {
                GameLog.Add("This tank has had its wipe. It gets another at the next time step (more per tank as your Fishkeeping level grows).");
                return false;
            }
            var balance = Balance.Data.timeStep;
            WipesLeft--;
            // Experienced fishkeepers get more out of every wipe.
            float skill = (1f + Balance.Data.progression.wipeBonusPerFishkeepingLevel * (Progression.Fishkeeping - 1)) * Perks.More(PerkEffect.StrongerWipes);
            algae = Mathf.Clamp01(algae - balance.wipeAlgaeRemoved * skill);
            waterQuality = Mathf.Clamp01(waterQuality + balance.wipeQualityRestored * skill);
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpWipeTank);
            Progression.Count(GoalKind.CleanTanks);
            Fx.Sparkle(InnerBounds);
            Fx.Text(InnerBounds.center + Vector3.up * innerSize.y * 0.3f, "Sparkling!", UiTheme.Aqua, 20f);
            GameLog.Add($"You wiped the tank. Water {waterQuality * 100f:0}%, algae {algae * 100f:0}%." + (WipesLeft > 0 ? $" {WipesLeft} more wipes here this time." : " That's this tank's wipe until the next time step."));
            return true;
        }

        /// <summary>Water conditioner: resets pH for the water type, kills pests, freshens a little.</summary>
        public void TreatWater()
        {
            ph = IsSaltwater ? 8.1f : 7.0f;
            pests = false;
            waterQuality = Mathf.Clamp01(waterQuality + 0.1f);
            GameLog.Add($"Water treated: pH {ph:0.0}, pests gone.");
            var inner = InnerBounds;
            Fx.Bubbles(new Vector3(inner.center.x, inner.min.y + 0.03f, inner.center.z), 14, SurfaceY, inner.extents.x * 0.6f);
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpTreatWater);
            Milestones.Add(MilestoneKind.TreatWater);
        }

        /// <summary>Developer shortcut: makes the water filthy to check how dirty tanks look and how shoppers react.</summary>
        public void DevMakeDirty()
        {
            waterQuality = 0.15f;
            algae = 0.55f;
            GameLog.Add("(dev) Water made dirty.");
        }

        public void SetSaltwater(bool salt)
        {
            salinity = salt ? 35f : 0f;
            ph = salt ? 8.1f : GameConfig.Active.startingPh;
            // Marine tanks get sand, fresh water tanks gravel.
            if (visuals != null) BuildSubstrate();
            GameLog.Add(salt ? "The tank is now salt water." : "The tank is now fresh water.");
        }

        /// <summary>Sets the heater; the water reaches the new temperature right away if the heater works.</summary>
        public void SetHeater(float celsius)
        {
            heaterSetting = Mathf.Clamp(Mathf.Round(celsius), 18f, 32f);
            if (HeaterWorking) temperature = heaterSetting;
        }

        /// <summary>
        /// The temperature that suits the fish in the tank best: the middle of the range they all
        /// share, or the average of their ideal temperatures when they don't share one.
        /// </summary>
        public float SuggestedTemperature
        {
            get
            {
                if (residents.Count == 0) return 25f;
                float low = float.MinValue, high = float.MaxValue, sumMid = 0f;
                foreach (var fish in residents)
                {
                    var species = fish.Species;
                    low = Mathf.Max(low, species.tempMin);
                    high = Mathf.Min(high, species.tempMax);
                    sumMid += (species.tempMin + species.tempMax) * 0.5f;
                }
                return Mathf.Round(low <= high ? (low + high) * 0.5f : sumMid / residents.Count);
            }
        }

        /// <summary>The temperature range every fish in the tank is happy with, or null if they don't share one.</summary>
        public Vector2? SharedTemperatureRange
        {
            get
            {
                if (residents.Count == 0) return null;
                float low = float.MinValue, high = float.MaxValue;
                foreach (var fish in residents)
                {
                    low = Mathf.Max(low, fish.Species.tempMin);
                    high = Mathf.Min(high, fish.Species.tempMax);
                }
                return low <= high ? new Vector2(low, high) : (Vector2?)null;
            }
        }

        /// <summary>Called by the shop at every time step (opening and closing).</summary>
        public void OnTimeStep()
        {
            var balance = Balance.Data.timeStep;
            float waste = (FilterWorking ? filter.Info.effect : 1f) * Perks.Less(PerkEffect.CleanerWater);

            waterQuality = Mathf.Clamp01(waterQuality
                - balance.waterQualityLossPerStep * waste
                - residents.Count * balance.wasteQualityLossPerFish * waste
                - corpses.Count * balance.corpseQualityLoss
                - OverLimit * balance.overcrowdedWastePerFish);
            float algaeGrowth = (balance.algaePerStep + DecorCount * balance.algaePerDecoration) * Perks.Less(PerkEffect.SlowerAlgae);
            int grazers = 0;
            foreach (var fish in residents) if (!fish.IsDead && fish.Species.eatsAlgae) grazers++;
            algae = Mathf.Clamp01(algae + algaeGrowth * (1.5f - waterQuality) - grazers * balance.algaeEatenPerGrazer);
            float neutral = IsSaltwater ? 8.1f : 7f;
            ph = Mathf.MoveTowards(ph, neutral - 1.2f, balance.phDriftPerStep * (1f - waterQuality) * 2f);
            if (!pests && waterQuality < 0.5f && GameRandom.Chance(balance.pestChance * (1.5f - waterQuality)))
            {
                pests = true;
                GameLog.Add($"Snails and worms have got into a tank. Treat the water to get rid of them.");
            }
            WearEquipment();
            // A heater holds the water near its setting; cheaper ones let it drift a little.
            if (HeaterWorking)
            {
                float drift = heater.Info.effect;
                temperature = Mathf.Round((heaterSetting + GameRandom.Range(-drift, drift)) * 10f) / 10f;
            }
            else temperature = GameConfig.Active.roomTemperature;
            WipesLeft = WipesPerSession;
            FedSinceTimeStep = false;
        }

        public Vector3 RandomPoint(float margin)
        {
            var b = InnerBounds;
            b.Expand(-margin * 2f);
            return GameRandom.InsideBounds(b);
        }

        /// <summary>Random point in open water, at least <paramref name="clearance"/> away from any decoration.</summary>
        public Vector3 RandomFreePoint(float clearance)
        {
            var point = RandomPoint(clearance);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (!OverlapsObstacle(point, clearance)) return point;
                point = RandomPoint(clearance);
            }
            return point;
        }

        static readonly Collider[] overlapBuffer = new Collider[16];

        public static bool OverlapsObstacle(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, overlapBuffer, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (TankObstacle.IsObstacle(overlapBuffer[i])) return true;
            return false;
        }

        public Vector3 Clamp(Vector3 point, float margin) => Clamp(point, margin, margin);

        /// <summary>Keeps a point inside the water, with its own (usually smaller) margin above the sand.</summary>
        public Vector3 Clamp(Vector3 point, float margin, float floorMargin)
        {
            var b = InnerBounds;
            return new Vector3(
                Mathf.Clamp(point.x, b.min.x + margin, b.max.x - margin),
                Mathf.Clamp(point.y, b.min.y + floorMargin, b.max.y - margin),
                Mathf.Clamp(point.z, b.min.z + margin, b.max.z - margin));
        }

        void Update() => ApplyWaterLook();

        public Color WaterColor
        {
            get
            {
                var color = Color.Lerp(MurkyWater, ClearWater, waterQuality);
                return Color.Lerp(color, AlgaeWater, algae * 0.7f);
            }
        }

        void ApplyWaterLook()
        {
            var color = WaterColor;
            if (backWallMaterial != null) backWallMaterial.color = color * 1.3f;
            // Dirty water shows only inside the glass, through the water volume's murk.
            if (water != null) water.SetLook(color, waterQuality, algae);
        }

        /// <summary>Puts in new equipment of this model, replacing any old one of the same kind.</summary>
        public void Install(EquipmentKind kind, int tier)
        {
            bool had = GetEquipment(kind) != null;
            var item = new InstalledEquipment { kind = kind, tier = tier };
            switch (kind)
            {
                case EquipmentKind.Filter: filter = item; break;
                case EquipmentKind.Heater:
                    heater = item;
                    SetHeater(had ? heaterSetting : SuggestedTemperature);
                    break;
                default: bubbler = item; break;
            }
            // The new model may look different: swap in its design.
            if (visuals != null) BuildEquipment(kind);
            UpdateEquipmentLook();
        }

        /// <summary>
        /// Fixes broken equipment with a repair kit. It keeps most of its wear. Sometimes the kit
        /// isn't used up (<see cref="SupplierBalance.repairKitSaveChance"/>).
        /// </summary>
        public bool Repair(EquipmentKind kind)
        {
            var item = GetEquipment(kind);
            if (item == null || !item.broken) return false;
            if (Economy.RepairKits <= 0)
            {
                GameLog.Add("No repair kits. Order some at the supplier computer.");
                return false;
            }
            bool kept = GameRandom.Chance(Balance.Data.supplier.repairKitSaveChance);
            if (!kept) Economy.RepairKits--;
            item.broken = false;
            item.wear *= 1f - Mathf.Clamp01(Balance.Data.supplier.repairWearRemoved + Perks.Value(PerkEffect.BetterRepairs));
            if (kind == EquipmentKind.Heater) temperature = heaterSetting;
            UpdateEquipmentLook();
            Progression.Award(XpTrack.Fishkeeping, Balance.Data.progression.xpRepairEquipment);
            GameLog.Add($"Repaired the {item.Name.ToLower()} in {Title}." + (kept ? " The repair kit wasn't used up!" : "") + $" {Economy.RepairKits} repair kits left.");
            Milestones.Add(MilestoneKind.RepairEquipment);
            return true;
        }

        /// <summary>Time step: running equipment wears, and worn equipment may break down.</summary>
        void WearEquipment()
        {
            foreach (var kind in Equipment.Kinds)
            {
                var item = GetEquipment(kind);
                if (item == null || item.broken) continue;
                item.ageSteps++;
                item.wear += 1f;
                if (!GameRandom.Chance(item.BreakChance)) continue;
                item.broken = true;
                GameLog.Add($"The {item.Name.ToLower()} in {Title} broke down! Fix it with a repair kit.");
            }
            UpdateEquipmentLook();
        }

        /// <summary>A broken bubbler stops bubbling.</summary>
        void UpdateEquipmentLook()
        {
            if (bubbles != null) bubbles.enabled = BubblerWorking;
        }

        /// <summary>Tank height the equipment sizes are for (see <see cref="EquipmentModels"/>).</summary>
        const float StandardTankHeight = 0.9f;

        /// <summary>(Re)builds one kind of equipment's 3D model, for the installed model, or removes it if there is none.</summary>
        void BuildEquipment(EquipmentKind kind)
        {
            int index = (int)kind;
            if (equipmentModels[index] != null) Destroy(equipmentModels[index]);
            equipmentModels[index] = null;
            if (kind == EquipmentKind.Bubbler) bubbles = null;
            var item = GetEquipment(kind);
            if (item == null) return;
            var holder = new GameObject(kind.ToString()).transform;
            holder.SetParent(visuals, false);
            equipmentModels[index] = holder.gameObject;
            switch (kind)
            {
                case EquipmentKind.Filter: BuildFilter(holder, item.tier); break;
                case EquipmentKind.Heater: BuildHeater(holder, item.tier); break;
                default: BuildBubbler(holder, item.tier); break;
            }
        }

        /// <summary>
        /// One equipment design, turned, sized for this tank and lined up with an anchor (see
        /// <see cref="EquipmentModels.Place"/>). Null if the model is missing.
        /// </summary>
        GameObject PlaceModel(string design, Transform holder, Quaternion rotation, Vector3 anchor, Vector3 align, out Bounds bounds, bool colliders = true)
        {
            bounds = default;
            var piece = EquipmentModels.Spawn(design, holder, colliders);
            if (piece == null) return null;
            float size = EquipmentModels.Size(design) * innerSize.y / StandardTankHeight;
            bounds = EquipmentModels.Place(piece, holder, rotation, size, anchor, align);
            if (colliders) piece.AddComponent<TankObstacle>();
            return piece;
        }

        /// <summary>Yaw that turns an equipment model's front towards the glass the camera looks through.</summary>
        static readonly Quaternion FaceFront = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>
        /// The filter: the model's own design on the floor in the back right corner, and the part
        /// on top of the tank (the same for every model; a placeholder box until it has a design)
        /// joined to it by the intake pipe.
        /// </summary>
        void BuildFilter(Transform holder, int tier)
        {
            var b = innerSize;
            var dark = new Color(0.15f, 0.16f, 0.18f);
            var anchor = new Vector3(b.x * 0.5f - 0.03f, 0f, b.z * 0.5f - 0.015f);
            float x = b.x * 0.36f, top = 0.2f * b.y, pipeZ = b.z * 0.5f - 0.03f;
            if (PlaceModel(EquipmentModels.Design(EquipmentKind.Filter, tier), holder, FaceFront, anchor, new Vector3(1f, -1f, 1f), out var bounds) != null)
            {
                x = bounds.center.x;
                top = bounds.max.y;
                pipeZ = Mathf.Min(pipeZ, bounds.max.z - 0.02f);
            }
            MaterialFactory.Decor(PrimitiveType.Cube, "FilterTop", holder, new Vector3(x, b.y - 0.02f, b.z * 0.5f + 0.07f), new Vector3(0.22f, 0.2f, 0.1f), dark);
            float pipeLength = b.y - top;
            MaterialFactory.Decor(PrimitiveType.Cylinder, "FilterIntake", holder, new Vector3(x, top + pipeLength * 0.5f, pipeZ), new Vector3(0.025f, pipeLength * 0.5f, 0.025f), dark);
        }

        /// <summary>
        /// The heater stands upright against the back glass on the left. The smart controller's box
        /// hangs on the outside of the left glass, its cable running over the rim to the heater.
        /// </summary>
        void BuildHeater(Transform holder, int tier)
        {
            var b = innerSize;
            // The heater designs lie along their x axis with their suction cups underneath: stand
            // them up (x to up) with the cups against the back glass.
            var upright = Quaternion.LookRotation(Vector3.Cross(Vector3.up, Vector3.back), Vector3.back);
            var anchor = new Vector3(-b.x * 0.43f, b.y * 0.5f, b.z * 0.5f - 0.005f);
            var heaterModel = PlaceModel(EquipmentModels.Design(EquipmentKind.Heater, tier), holder, upright, anchor, new Vector3(0f, 0f, 1f), out var heaterBounds);
            if (heaterModel == null)
            {
                MaterialFactory.Decor(PrimitiveType.Cylinder, "Heater", holder, new Vector3(-b.x * 0.43f, b.y * 0.5f, b.z * 0.42f), new Vector3(0.03f, b.y * 0.3f, 0.03f), new Color(0.75f, 0.3f, 0.2f));
                heaterBounds = new Bounds(new Vector3(-b.x * 0.43f, b.y * 0.5f, b.z * 0.42f), new Vector3(0.03f, b.y * 0.6f, 0.03f));
            }
            if (!EquipmentModels.HasController(EquipmentKind.Heater, tier)) return;

            // Outside the left glass (the frame is 2 cm thick), towards the front so it can be seen.
            var boxAnchor = new Vector3(-b.x * 0.5f - 0.012f, b.y * 0.62f, -b.z * 0.2f);
            if (PlaceModel(EquipmentModels.HeaterController, holder, FaceFront, boxAnchor, new Vector3(1f, 0f, 0f), out var box, false) == null) return;
            float over = b.y + 0.03f;
            var cable = new Color(0.08f, 0.08f, 0.09f);
            var start = new Vector3(box.center.x, box.max.y - 0.01f, box.center.z);
            var rimOutside = new Vector3(box.center.x, over, box.center.z);
            var rimInside = new Vector3(heaterBounds.center.x, over, heaterBounds.center.z);
            var end = new Vector3(heaterBounds.center.x, heaterBounds.max.y - 0.01f, heaterBounds.center.z);
            Cable(holder, start, rimOutside, cable);
            Cable(holder, rimOutside, rimInside, cable);
            Cable(holder, rimInside, end, cable);
        }

        /// <summary>A straight piece of thin cable between two points.</summary>
        static void Cable(Transform parent, Vector3 from, Vector3 to, Color color)
        {
            var length = Vector3.Distance(from, to);
            if (length < 1e-4f) return;
            var piece = MaterialFactory.Decor(PrimitiveType.Cylinder, "Cable", parent, (from + to) * 0.5f, new Vector3(0.007f, length * 0.5f + 0.0035f, 0.007f), color);
            piece.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }

        /// <summary>The bubbler on the sand towards the back on the left; its bubbles rise from the top.</summary>
        void BuildBubbler(Transform holder, int tier)
        {
            var b = innerSize;
            var anchor = new Vector3(-b.x * 0.2f, 0f, b.z * 0.38f);
            var origin = new Vector3(anchor.x, 0.02f, anchor.z);
            if (PlaceModel(EquipmentModels.Design(EquipmentKind.Bubbler, tier), holder, FaceFront, anchor, new Vector3(0f, -1f, 0f), out var bounds) != null)
                origin = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            else
                MaterialFactory.Decor(PrimitiveType.Cylinder, "Bubbler", holder, new Vector3(anchor.x, 0.01f, anchor.z), new Vector3(0.1f, 0.01f, 0.1f), new Color(0.3f, 0.32f, 0.35f));
            bubbles = holder.gameObject.AddComponent<TankBubbles>();
            bubbles.Initialize(this, origin, b.y);
        }

        /// <summary>Builds a new tank of the given size with these decorations (ids from the balance file).</summary>
        public void Build(int size, IEnumerable<string> decorations)
        {
            sizeIndex = size;
            innerSize = SizeInfo.Size;
            if (decor == null) decor = gameObject.AddComponent<TankDecor>();
            if (GetComponent<Health.TankMateBehaviour>() == null) gameObject.AddComponent<Health.TankMateBehaviour>();
            BuildVisuals();
            if (decorations != null)
                foreach (var id in decorations) decor.Add(id);
        }

        /// <summary>
        /// Swaps the tank for a bigger (or smaller) size in place: same fish, eggs, water, equipment
        /// and decorations, just more room.
        /// </summary>
        public void Resize(int size)
        {
            sizeIndex = size;
            innerSize = SizeInfo.Size;
            BuildVisuals();
            foreach (var fish in residents) fish.transform.position = Clamp(fish.transform.position, 0.05f);
            foreach (var fish in corpses) fish.transform.position = Clamp(fish.transform.position, 0.05f);
        }

        /// <summary>Simple tank made of primitives. Replace with a Meshy or hand-made tank model later.</summary>
        void BuildVisuals()
        {
            if (visuals != null) Destroy(visuals.gameObject);
            var b = innerSize;
            var root = new GameObject("Visuals").transform;
            root.SetParent(transform, false);
            visuals = root;

            // The tank sits on its stand on a dark base rim; the substrate fills the bottom of the glass.
            var frame = new Color(0.1f, 0.1f, 0.12f);
            MaterialFactory.Decor(PrimitiveType.Cube, "Base", root, new Vector3(0f, -BaseDepth + 0.01f, 0f), new Vector3(b.x + 0.05f, 0.02f, b.z + 0.05f), frame);
            var wall = MaterialFactory.Decor(PrimitiveType.Cube, "BackWall", root, new Vector3(0f, b.y * 0.5f, b.z * 0.5f + 0.01f), new Vector3(b.x + 0.04f, b.y, 0.02f), ClearWater);
            backWallMaterial = wall.GetComponent<Renderer>().sharedMaterial;

            // Glass frame: the 12 edges of the box, the bottom ones down at the base below the substrate.
            const float t = 0.02f;
            float bottom = -BaseDepth + 0.02f + t * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                MaterialFactory.Decor(PrimitiveType.Cube, "FrameV", root, new Vector3(sx * b.x * 0.5f, (b.y + bottom) * 0.5f, sz * b.z * 0.5f), new Vector3(t, b.y - bottom, t), frame);
            for (int sy = 0; sy <= 1; sy++)
            for (int s = -1; s <= 1; s += 2)
            {
                float y = sy == 0 ? bottom : b.y;
                MaterialFactory.Decor(PrimitiveType.Cube, "FrameX", root, new Vector3(0f, y, s * b.z * 0.5f), new Vector3(b.x, t, t), frame);
                MaterialFactory.Decor(PrimitiveType.Cube, "FrameZ", root, new Vector3(s * b.x * 0.5f, y, 0f), new Vector3(t, t, b.z), frame);
            }

            foreach (var kind in Equipment.Kinds) BuildEquipment(kind);
            UpdateEquipmentLook();
            decor.Rebuild(root, b, DecorationSlots);
            BuildSubstrate();
            water = TankWater.Build(root, b);
            // New colliders must be in the physics scene before fish pick spawn points.
            Physics.SyncTransforms();
        }

        /// <summary>How far the tank's base (rim and substrate) reaches below the water's floor.</summary>
        public const float BaseDepth = 0.07f;

        GameObject substrate;

        /// <summary>Sand for salt water, gravel for fresh water. Pure looks: fish swim over it.</summary>
        void BuildSubstrate()
        {
            if (substrate != null) Destroy(substrate);
            substrate = TankSubstrate.Build(visuals, innerSize, IsSaltwater ? TankSubstrate.Kind.Sand : TankSubstrate.Kind.Gravel, BaseDepth - 0.02f);
        }
    }
}
