using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.People;
using FishBreeder.Shop;
using UnityEngine;
using UnityEngine.AI;

namespace FishBreeder.Store
{
    /// <summary>
    /// The shop building: a sunny beach-shack fish shop of rooms in a row (seafoam walls, wooden
    /// floors, a bamboo counter, palms and surfboards). The main floor (with the one entrance and the
    /// register) is open from the start; the East wing beside it is bought with money once the
    /// shop's reputation is high enough. It's the last room this shop grows into: twice as deep as
    /// the main floor (it reaches north past the main floor's back wall), with a wall aquarium set
    /// into each of its walls, four in all. Tanks stand round the walls, facing into the room,
    /// which leaves the middle free to walk about. Breeder and quarantine tanks live round the
    /// walls of the breeding room (the back room) to the west, which customers never see, with the
    /// supplier computer; through a door in its west wall is the big breeding room, whose back
    /// wall takes a wall breeding tank for the big fish. The fish seller parks his van in the empty
    /// parking lot behind the breeding room, by its back door. Every spot has room for the biggest
    /// tank size, so tanks can be upgraded where they stand.
    /// </summary>
    public class StoreLayout : MonoBehaviour
    {
        public const float SectionWidth = 14f;
        public const float SectionDepth = 10f;
        public const float WallHeight = 4f;
        public const float StandHeight = 0.85f;
        public const float BackRoomWidth = 9f;
        const float WallThickness = 0.2f;
        const float FrontWallHeight = 1.1f;
        const float EntranceWidth = 2.2f;
        const float StaffDoorWidth = 1.6f;

        static readonly float[] SlotColumns = { -4.5f, 0f, 4.5f };
        // The main floor's back row sits a little east, clear of the tanks along its west wall.
        static readonly float[] MainSlotColumns = { -3.8f, 0.35f, 4.5f };
        // Far enough off the back wall for the biggest tank to clear the palms in the corners.
        const float BackSlotZ = 3.7f;
        const float FrontSlotZ = -4.3f;
        const float WestSlotX = -SectionWidth * 0.5f + 0.7f;
        // Grand tanks are 3 m long: these leave a gap between the two and keep clear of the staff door.
        static readonly float[] WestSlotZ = { 2.15f, -1.05f };
        /// <summary>
        /// The room whose walls are kept for wall aquariums (the East wing). It's twice as deep as
        /// the others, reaching north past the main floor's back wall, and it's the last room.
        /// </summary>
        public const int WallAquariumSection = 1;
        /// <summary>Rooms this shop can have: the main floor and the East wing. Later growth is more shops.</summary>
        public const int MaxSections = 2;
        /// <summary>The share of a wall aquarium's depth in front of its wall, in the room; the rest is in the wall and behind it.</summary>
        const float WallAquariumInRoom = 0.2f;
        /// <summary>Wall left above the wall aquarium's frame; a taller one sits lower to keep it.</summary>
        const float WallAquariumHeadroom = 0.3f;
        /// <summary>The lowest the wall aquarium's base goes, however tall it's raised.</summary>
        const float WallAquariumLowestBase = 0.3f;
        /// <summary>Where the two wall aquariums along the East wing's east wall sit, as a share of the wall from its south end.</summary>
        static readonly float[] WallRoomEastShares = { 0.75f, 0.25f };
        /// <summary>The big breeding room, west of the breeding room: just wide enough for the wall breeding tank and a palm.</summary>
        public const float BigRoomWidth = 11f;
        /// <summary>The door from the breeding room into the big breeding room, near the front of their shared wall.</summary>
        const float BigRoomDoorZ = -4f;
        // The breeding room's tanks stand round its walls like the shop's, in walking order:
        // up the west wall, along the back (leaving the back door clear), then the east wall.
        // Each is x from the room's centre, z, and yaw (front facing into the room).
        static readonly Vector3[] BackRoomSpots =
        {
            new Vector3(-3.8f, -1.8f, -90f), new Vector3(-3.8f, 1.4f, -90f),
            new Vector3(-2.6f, 3.7f, 0f), new Vector3(0.9f, 3.7f, 0f),
            new Vector3(3.8f, 0.6f, 90f),
        };
        /// <summary>The breeding room's back door to the parking lot, from the room's centre.</summary>
        const float BackDoorOffset = 3.4f;
        const float BackDoorWidth = 1.1f;
        /// <summary>How far behind the shop the fish seller sets up his table: the north end of the parking lot.</summary>
        const float SellerTableZ = 12f;
        /// <summary>The back edge of the parking lot.</summary>
        public const float LotFarZ = 15f;
        /// <summary>The back edge of the sandy ground behind the shop, past the East wing's back wall.</summary>
        const float GroundFarZ = 19f;

        /// <summary>Centre of the back room, west of the main floor.</summary>
        public static float BackRoomCenterX => -SectionWidth * 0.5f - WallThickness - BackRoomWidth * 0.5f;
        /// <summary>Centre of the big breeding room, west of the back room (they share a wall).</summary>
        public static float BigRoomCenterX => BackRoomCenterX - BackRoomWidth * 0.5f - WallThickness - BigRoomWidth * 0.5f;
        /// <summary>The far west edge of the building, outside the big breeding room's west wall.</summary>
        static float BuildingWestX => BigRoomCenterX - BigRoomWidth * 0.5f - WallThickness;
        /// <summary>Above the breeding room's back door, where the sign pointing to the parking lot hangs.</summary>
        public Vector3 BackDoorSign => new Vector3(BackRoomCenterX + BackDoorOffset, 2.55f, SectionDepth * 0.5f - 0.05f);
        /// <summary>Where the store camera looks to see the fish seller in the parking lot.</summary>
        public static Vector3 ParkingLotView => new Vector3(BackRoomCenterX + BackDoorOffset - 1.5f, 0f, SellerTableZ + 0.3f);

        static readonly Color WallColor = new Color(0.55f, 0.82f, 0.8f);
        static readonly Color FloorColor = new Color(0.93f, 0.84f, 0.66f);
        static readonly Color LockedFloorColor = new Color(0.45f, 0.52f, 0.55f);
        static readonly Color BackRoomFloorColor = new Color(0.8f, 0.72f, 0.58f);
        static readonly Color CounterColor = new Color(0.72f, 0.52f, 0.32f);
        static readonly Color CounterTopColor = new Color(0.97f, 0.91f, 0.78f);
        static readonly Color DarkColor = new Color(0.1f, 0.22f, 0.3f);
        static readonly Color StripeColor = new Color(1f, 0.5f, 0.4f);
        static readonly Color StripeColor2 = new Color(0.98f, 0.82f, 0.4f);
        static readonly Color[] BoardColors = { new Color(1f, 0.55f, 0.42f), new Color(0.3f, 0.75f, 0.78f), new Color(1f, 0.82f, 0.36f), new Color(0.98f, 0.5f, 0.7f) };

        public class Section
        {
            public int index;
            public string name;
            public int basePrice;
            /// <summary>What unlocking it costs now, after building discounts.</summary>
            public int Price => Perks.Build(basePrice);
            public bool unlocked;
            public Transform shell;
            public float CenterX => index * SectionWidth;
            /// <summary>Front to back: the East wing is twice as deep as the main floor, reaching north.</summary>
            public float Depth => index == WallAquariumSection ? SectionDepth * 2f : SectionDepth;
            /// <summary>Its back (north) edge; every room's front lines up with the main floor's.</summary>
            public float BackZ => -SectionDepth * 0.5f + Depth;
            public float CenterZ => -SectionDepth * 0.5f + Depth * 0.5f;
            public Vector3 LabelPoint => new Vector3(CenterX, 0.2f, 0f);
        }

        public class Slot
        {
            public Section section;   // null in the back room
            public bool backRoom;
            public Vector3 position; // sand level of a tank placed here
            public float yaw;         // 0: the tank's front faces -Z, the entrance side
            public StoreTank tank;
            public GameObject emptyMarker;
            /// <summary>A wall where only a wall aquarium goes (the East wing's, and the big breeding room's back wall).</summary>
            public bool wall;

            public bool IsOpen => backRoom || section.unlocked;

            /// <summary>The big breeding room's wall breeding tank.</summary>
            public bool WallBreeder => wall && backRoom;

            /// <summary>
            /// The floor in front of the tank here: its middle for a free-standing tank, and just
            /// off the glass for a wall aquarium, which is mostly in the wall.
            /// </summary>
            public Vector3 FloorFront
            {
                get
                {
                    var floor = new Vector3(position.x, 0f, position.z);
                    if (!wall) return floor;
                    return floor + Quaternion.Euler(0f, yaw, 0f) * Vector3.back * (Balance.Data.store.wallAquarium.depth * 0.5f + 0.4f);
                }
            }

            /// <summary>
            /// Breeder and quarantine tanks go in the back room, display and for sale tanks on the
            /// shop floor; the wall breeding tank is only ever a breeder.
            /// </summary>
            public bool Allows(TankRole role) => WallBreeder ? role == TankRole.Breeder : backRoom == (role == TankRole.Breeder || role == TankRole.Quarantine);
        }

        readonly List<Section> sections = new List<Section>();
        readonly List<Slot> slots = new List<Slot>();
        readonly List<StoreTank> tanks = new List<StoreTank>();
        Transform tankRoot;

        public IReadOnlyList<Section> Sections => sections;
        public IReadOnlyList<Slot> Slots => slots;
        public IReadOnlyList<StoreTank> Tanks => tanks;
        readonly List<StoreTank> publicTanks = new List<StoreTank>();
        /// <summary>Tanks on the shop floor, the ones customers look at (not the back room).</summary>
        public IReadOnlyList<StoreTank> PublicTanks => publicTanks;
        public Vector3 RegisterPoint { get; private set; }
        public Vector3 ComputerPoint { get; private set; }
        public Vector3 FishSellerPoint { get; private set; }
        public Vector3 EntrancePoint => new Vector3(0f, 0.1f, -SectionDepth * 0.5f - 0.6f);
        /// <summary>Where customers appear and disappear, out on the pavement.</summary>
        public Vector3 DoorOutside => new Vector3(0f, 0f, -SectionDepth * 0.5f - 3f);
        public Vector3 DoorInside => new Vector3(0f, 0f, -SectionDepth * 0.5f + 0.8f);
        public Vector3 ShopkeeperHand { get; private set; }

        /// <summary>Where a customer stands to look into a tank, in front of its glass.</summary>
        public static Vector3 ViewingSpot(StoreTank tank)
        {
            var p = tank.transform.position + tank.Tank.Front * (tank.Tank.InnerSize.z * 0.5f + 0.75f);
            return new Vector3(p.x, 0f, p.z);
        }

        /// <summary>A step sideways along a tank's glass, so shoppers don't all stand on one spot.</summary>
        public static Vector3 AlongGlass(StoreTank tank, float offset) => tank.transform.right * offset;

        /// <summary>The register queue runs back into the shop from the front of the counter; place 0 is being served.</summary>
        public Vector3 RegisterQueueSpot(int place) => registerFront + Vector3.forward * (0.75f * place);

        /// <summary>Which way a customer at the register faces: the counter, and the shopkeeper behind it.</summary>
        public Vector3 RegisterFacing => Vector3.right;

        Vector3 registerFront;

        public int UnlockedCount
        {
            get
            {
                int count = 0;
                foreach (var section in sections) if (section.unlocked) count++;
                return count;
            }
        }

        public void Build()
        {
            var sectionBalance = Balance.Data.store.sections;
            for (int i = 0; i < sectionBalance.Count && i < MaxSections; i++)
                AddSection(sectionBalance[i].name, sectionBalance[i].price, i == 0);

            tankRoot = new GameObject("Tanks").transform;
            tankRoot.SetParent(transform, false);
            // Walking order runs west to east: the big breeding room's wall tank comes first.
            slots.Add(WallSlot(null, new Vector3(BigRoomCenterX, 0f, SectionDepth * 0.5f + WallThickness * 0.5f), 0f));
            foreach (var spot in BackRoomSpots)
                slots.Add(new Slot { backRoom = true, position = new Vector3(BackRoomCenterX + spot.x, StandHeight, spot.y), yaw = spot.z });
            // Round the walls in walking order. The main floor: up the west wall (its front is the
            // staff door and the computer), then along the back. The others: along the back wall,
            // then back along the front wall facing in. Their east side stays open into the next one.
            foreach (var section in sections)
            {
                float cx = section.CenterX;
                if (section.index == 0)
                    for (int i = WestSlotZ.Length - 1; i >= 0; i--)
                        slots.Add(new Slot { section = section, position = new Vector3(cx + WestSlotX, StandHeight, WestSlotZ[i]), yaw = -90f });
                if (section.index == WallAquariumSection)
                {
                    // A wall aquarium in every wall: the west wall north of the main floor, the
                    // back wall, then two along the east wall coming back south.
                    float halfW = SectionWidth * 0.5f, halfT = WallThickness * 0.5f;
                    slots.Add(WallSlot(section, new Vector3(cx - halfW - halfT, 0f, (SectionDepth * 0.5f + section.BackZ) * 0.5f), -90f));
                    slots.Add(WallSlot(section, new Vector3(cx, 0f, section.BackZ + halfT), 0f));
                    foreach (var share in WallRoomEastShares)
                        slots.Add(WallSlot(section, new Vector3(cx + halfW + halfT, 0f, -SectionDepth * 0.5f + share * section.Depth), 90f));
                    continue;
                }
                foreach (var column in section.index == 0 ? MainSlotColumns : SlotColumns)
                    slots.Add(new Slot { section = section, position = new Vector3(cx + column, StandHeight, BackSlotZ) });
                if (section.index > 0)
                    for (int i = SlotColumns.Length - 1; i >= 0; i--)
                        slots.Add(new Slot { section = section, position = new Vector3(cx + SlotColumns[i], StandHeight, FrontSlotZ), yaw = 180f });
            }

            BuildShell();
            BuildBackRoom();
            BuildRegister();
            BuildComputer();
            BuildPavement();
            BuildParkingLot();
            RefreshEmptySlots();

            // One of each to start: a breeder in the back room, and a for sale and a display tank
            // along the shop's back wall.
            var starter = Balance.Data.store.starterDecorations;
            PlaceTank(FirstFreeSlot(TankRole.Breeder), TankRole.Breeder, starter);
            PlaceTank(slots.Find(s => !s.backRoom && s.section.index == 0 && s.yaw == 0f && s.tank == null), TankRole.ForSale, starter);
            PlaceTank(slots.Find(s => !s.backRoom && s.section.index == 0 && s.yaw == 0f && s.tank == null), TankRole.Display, starter);
        }

        /// <summary>
        /// A spot for a wall aquarium set into a wall: the middle of the wall's thickness at floor
        /// level, and which way the tank faces. Only its front fifth stands out in the room; the
        /// rest goes through the wall, reaching out behind it.
        /// </summary>
        static Slot WallSlot(Section section, Vector3 wallMiddle, float yaw)
        {
            var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var at = wallMiddle + outward * (Balance.Data.store.wallAquarium.depth * (0.5f - WallAquariumInRoom) - WallThickness * 0.5f);
            return new Slot { section = section, backRoom = section == null, position = new Vector3(at.x, StandHeight, at.z), yaw = yaw, wall = true };
        }

        /// <summary>How far a wall aquarium's housing reaches out behind the middle of its wall.</summary>
        static float WallAquariumReach => Balance.Data.store.wallAquarium.depth * (1f - WallAquariumInRoom) - WallThickness * 0.5f + 0.3f;

        /// <summary>Where a slot sits in walking order (back room first), for flicking between tanks.</summary>
        public int SlotOrder(Slot slot) => slot == null ? int.MaxValue : slots.IndexOf(slot);

        Slot FirstFreeSlot(TankRole role) => slots.Find(s => s.tank == null && s.IsOpen && !s.wall && s.Allows(role));

        void AddSection(string name, int price, bool unlocked)
        {
            sections.Add(new Section { index = sections.Count, name = name, basePrice = price, unlocked = unlocked });
        }

        /// <summary>Tanks of this kind, towards the store's limit (the wall breeding tank has its own room, so it doesn't count).</summary>
        public int CountTanks(TankRole role)
        {
            int count = 0;
            foreach (var tank in tanks) if (tank.Role == role && (tank.Slot == null || !tank.Slot.WallBreeder)) count++;
            return count;
        }

        /// <summary>The next locked section, or null when everything is open. Sections unlock in order.</summary>
        public Section NextLockedSection
        {
            get
            {
                foreach (var section in sections) if (!section.unlocked) return section;
                return null;
            }
        }

        public static int SectionLevel(Section section)
        {
            var list = Balance.Data.store.sections;
            return section.index < list.Count ? list[section.index].unlockLevel : 1;
        }

        /// <summary>Reputation (in stars) the shop needs before this room can be bought.</summary>
        public static float SectionStars(Section section)
        {
            var list = Balance.Data.store.sections;
            return section.index < list.Count ? list[section.index].starsNeeded : 0f;
        }

        public static float Stars => Economy.Reputation * 5f;

        /// <summary>Why this room can't be bought yet (not counting money), or null if it can.</summary>
        public string WhyLocked(Section section)
        {
            if (section == null || section.unlocked) return "It's already open.";
            var next = NextLockedSection;
            if (section != next) return $"Unlock the {next.name} first.";
            if (!Progression.IsUnlocked(XpTrack.Shopkeeping, SectionLevel(section))) return $"Needs {Progression.LockText(XpTrack.Shopkeeping, SectionLevel(section))}.";
            if (!DevMode.On && Stars + 0.001f < SectionStars(section)) return $"Needs a {SectionStars(section):0.0} star reputation (you have {Stars:0.0}).";
            return null;
        }

        public bool TryUnlock(Section section)
        {
            if (section == null || section.unlocked) return false;
            var why = WhyLocked(section);
            if (why != null)
            {
                GameLog.Add($"The {section.name}: {why}");
                return false;
            }
            if (!Economy.TrySpend(section.Price, $"unlocking the {section.name}", MoneyKind.Rooms)) return false;
            section.unlocked = true;
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpUnlockSection);
            BuildShell();
            RefreshEmptySlots();
            GameLog.Add($"The {section.name} is open! Click an empty spot to buy a tank for it.");
            return true;
        }

        /// <summary>What a tank for this spot costs: a wall aquarium's own price on a wall (the wall breeding tank's in the big breeding room), the usual price anywhere else.</summary>
        public static int TankPrice(Slot slot, TankRole role)
        {
            if (slot == null || !slot.wall) return SupplierCatalog.TankPrice(role);
            return Perks.Build(slot.WallBreeder ? Balance.Data.store.wallBreederPrice : Balance.Data.store.wallAquariumPrice);
        }

        /// <summary>The level that unlocks a tank of this kind for this spot: a wall aquarium needs more.</summary>
        public static void TankLevel(Slot slot, TankRole role, out XpTrack track, out int level)
        {
            SupplierCatalog.TankLevel(role, out track, out level);
            if (slot == null || !slot.wall) return;
            // The wall breeding tank is for breeding the big fish, so it goes with fishkeeping;
            // the shop's wall aquariums are for showing and selling, so they go with shopkeeping.
            if (slot.WallBreeder) level = Mathf.Max(level, Balance.Data.store.wallBreederLevel);
            else
            {
                track = XpTrack.Shopkeeping;
                level = Mathf.Max(level, Balance.Data.store.wallAquariumLevel);
            }
        }

        public bool CanBuyTank(Slot slot, TankRole role, out string reason)
        {
            reason = null;
            TankLevel(slot, role, out var track, out var level);
            if (slot.tank != null) reason = "There is already a tank here.";
            else if (!slot.IsOpen) reason = "This part of the store is locked.";
            else if (!slot.Allows(role)) reason = slot.WallBreeder ? "The wall breeding tank is only for breeding." : slot.backRoom ? "Only breeder and quarantine tanks go in the breeding room." : "Breeder and quarantine tanks go in the breeding room.";
            else if (!slot.WallBreeder && CountTanks(role) >= SupplierCatalog.TankLimit(role)) reason = $"The store can't hold more {StoreTank.RoleName(role).ToLower()} tanks.";
            else if (!Progression.IsUnlocked(track, level)) reason = $"Needs {Progression.LockText(track, level)}.";
            else if (!Economy.CanAfford(TankPrice(slot, role))) reason = "Not enough money.";
            return reason == null;
        }

        public const string NoMoney = "Not enough money.";

        /// <summary>
        /// What a tank would be at this size: one of the tank sizes, or for the wall aquarium one of
        /// its heights. Null past the biggest.
        /// </summary>
        public static TankSizeBalance SizeInfo(Tank tank, int size)
        {
            var store = Balance.Data.store;
            if (tank.IsWallAquarium) return size >= Tank.WallSize && size - Tank.WallSize < store.WallAquariumSteps ? store.WallAquariumStep(size - Tank.WallSize) : null;
            return size >= 0 && size < store.tankSizes.Count ? store.tankSizes[size] : null;
        }

        /// <summary>
        /// How high off the floor the wall aquarium's base sits: on the usual stand while there's
        /// wall above it, then lower as it's raised (the back wall rises over the tallest).
        /// </summary>
        public static float WallAquariumBase(float innerHeight) =>
            Mathf.Clamp(WallHeight - WallAquariumHeadroom - innerHeight, WallAquariumLowestBase, StandHeight);

        /// <summary>Why a tank can't be upgraded to this size right now (<see cref="NoMoney"/> last), or null if it can.</summary>
        public static string WhyCantUpgrade(StoreTank tank, int size)
        {
            var info = size > tank.Tank.SizeIndex ? SizeInfo(tank.Tank, size) : null;
            if (info == null) return "It's already this size or bigger.";
            if (tank.Tank.IsWallAquarium)
            {
                // Raising the wall aquarium is building work, so it goes with shopkeeping, like buying it.
                if (!Progression.IsUnlocked(XpTrack.Shopkeeping, info.unlockLevel)) return $"Needs {Progression.LockText(XpTrack.Shopkeeping, info.unlockLevel)}.";
                if (tank.Tank.Quality < info.qualityNeeded) return $"Needs quality {info.qualityNeeded} (it's {tank.Tank.Quality}).";
            }
            else
            {
                if (!Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel)) return $"Needs {Progression.LockText(XpTrack.Fishkeeping, info.unlockLevel)}.";
                if (tank.Tank.Quality < info.qualityNeeded) return $"Needs aquarium level {size + 1} (quality {info.qualityNeeded}, it's {tank.Tank.Quality}).";
            }
            if (!Economy.CanAfford(Perks.Upgrade(info.upgradePrice))) return NoMoney;
            return null;
        }

        public bool TryUpgradeTank(StoreTank tank, int size)
        {
            var reason = WhyCantUpgrade(tank, size);
            if (reason != null)
            {
                GameLog.Add(reason);
                return false;
            }
            var info = SizeInfo(tank.Tank, size);
            bool wall = tank.Tank.IsWallAquarium;
            if (!Economy.TrySpend(Perks.Upgrade(info.upgradePrice), wall ? $"raising {tank.Title} to {info.height:0.0#} m" : $"upgrading {tank.Title} to a {info.name.ToLower()} tank", MoneyKind.Tanks)) return false;
            if (wall)
            {
                // Once it reaches the roof it grows down as well as up, so every fish stays in the water.
                var at = tank.transform.position;
                tank.transform.position = new Vector3(at.x, WallAquariumBase(info.height), at.z);
            }
            tank.Tank.Resize(size);
            BuildStand(tank);
            if (wall) BuildShell();
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpTankUpgrade);
            GameLog.Add(wall
                ? $"{tank.Title} is now a {info.name.ToLower()}, {info.height:0.0#} m tall: room for {tank.Tank.Capacity} fish."
                : $"{tank.Title} is now a {info.name.ToLower()} tank: room for {tank.Tank.Capacity} fish.");
            return true;
        }

        public StoreTank TryBuyTank(Slot slot, TankRole role)
        {
            if (!CanBuyTank(slot, role, out var reason))
            {
                GameLog.Add(reason);
                return null;
            }
            if (!Economy.TrySpend(TankPrice(slot, role), slot.WallBreeder ? "the wall breeding tank" : slot.wall ? "a wall aquarium" : $"a {StoreTank.RoleName(role).ToLower()} tank", MoneyKind.Tanks)) return null;
            var tank = PlaceTank(slot, role, null);
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyTank);
            if (slot.wall)
            {
                // The wall opens up round the new tank.
                BuildShell();
                if (slot.WallBreeder)
                {
                    Fx.Card("WALL BREEDING TANK", tank.Title, $"The back wall of the big breeding room is one big breeding tank now: room for {tank.Tank.Capacity} fish, and big enough for the biggest fish to breed.", "Pair up some big fish", UiTheme.Aqua);
                    GameLog.Add($"The wall breeding tank is in! {tank.Title} holds {tank.Tank.Capacity} fish and {tank.Tank.DecorationSlots} decorations.");
                }
                else
                {
                    Fx.Card("WALL AQUARIUM", tank.Title, $"A whole wall of the {slot.section.name} is one big tank now: room for {tank.Tank.Capacity} fish.", "Fill it from the fish seller", UiTheme.Aqua);
                    GameLog.Add($"The wall aquarium is in! {tank.Title} holds {tank.Tank.Capacity} fish and {tank.Tank.DecorationSlots} decorations.");
                }
            }
            else GameLog.Add($"Your new {tank.Title.ToLower()} is set up and ready for fish.");
            return tank;
        }

        StoreTank PlaceTank(Slot slot, TankRole role, IEnumerable<string> decorations)
        {
            var position = slot.wall ? new Vector3(slot.position.x, WallAquariumBase(Balance.Data.store.wallAquarium.height), slot.position.z) : slot.position;
            var tank = StoreTank.Create(role, NextNumber(role), position, slot.yaw, tankRoot, slot.wall ? Tank.WallSize : 0, decorations);
            slot.tank = tank;
            tank.Slot = slot;
            tanks.Add(tank);
            if (!slot.backRoom) publicTanks.Add(tank);
            BuildStand(tank);
            RefreshEmptySlots();
            return tank;
        }

        /// <summary>The lowest number no tank of this kind has, so a renamed or changed tank never shares one.</summary>
        int NextNumber(TankRole role)
        {
            int number = 1;
            while (tanks.Exists(t => t.Role == role && t.Number == number)) number++;
            return number;
        }

        /// <summary>The types a tank here could be turned into: the other kind for its room.</summary>
        public static TankRole[] OtherRoles(StoreTank tank)
        {
            if (tank.Slot != null && tank.Slot.WallBreeder) return new TankRole[0];
            if (tank.InBackRoom) return new[] { tank.Role == TankRole.Breeder ? TankRole.Quarantine : TankRole.Breeder };
            return new[] { tank.Role == TankRole.ForSale ? TankRole.Display : TankRole.ForSale };
        }

        public static int ChangeRolePrice => Perks.Build(Balance.Data.store.changeTankTypePrice);

        /// <summary>Why this tank can't become that type right now (<see cref="NoMoney"/> last), or null if it can.</summary>
        public string WhyCantChangeRole(StoreTank tank, TankRole role)
        {
            if (tank.Role == role) return "It's already that type.";
            if (tank.Slot != null && !tank.Slot.Allows(role)) return tank.InBackRoom ? "Only breeder and quarantine tanks go in the breeding room." : "Breeder and quarantine tanks go in the breeding room.";
            if (CountTanks(role) >= SupplierCatalog.TankLimit(role)) return $"The store can't hold more {StoreTank.RoleName(role).ToLower()} tanks.";
            SupplierCatalog.TankLevel(role, out var track, out var level);
            if (!Progression.IsUnlocked(track, level)) return $"Needs {Progression.LockText(track, level)}.";
            if (!Economy.CanAfford(ChangeRolePrice)) return NoMoney;
            return null;
        }

        /// <summary>Turns a tank into another type, fish, water and all, for a fee.</summary>
        public bool TryChangeRole(StoreTank tank, TankRole role)
        {
            var reason = WhyCantChangeRole(tank, role);
            if (reason != null)
            {
                GameLog.Add(reason);
                return false;
            }
            string was = tank.Title;
            if (!Economy.TrySpend(ChangeRolePrice, $"turning {was} into a {StoreTank.RoleName(role).ToLower()} tank", MoneyKind.Tanks)) return false;
            tank.SetRole(role, NextNumber(role));
            publicTanks.Remove(tank);
            if (!tank.InBackRoom) publicTanks.Add(tank);
            BuildStand(tank);
            GameLog.Add($"{was} is now {tank.Title}." + (role == TankRole.ForSale ? " Its fish are up for sale at their appraised prices." : ""));
            return true;
        }

        /// <summary>The stand under a tank and its click volume, sized to the tank (rebuilt after an upgrade).</summary>
        void BuildStand(StoreTank tank)
        {
            if (tank.Stand != null) Destroy(tank.Stand);
            var size = tank.Tank.InnerSize;
            var stand = new GameObject("Stand and click");
            stand.transform.SetParent(tank.transform, false);
            // The stand stops just under the tank's base, so the two never share a surface (which flickers).
            // It reaches the floor wherever the base is (a raised wall aquarium sits lower).
            float top = -Tank.BaseDepth, standHeight = tank.transform.position.y;
            MaterialFactory.Decor(PrimitiveType.Cube, "Stand", stand.transform, new Vector3(0f, (top - standHeight) * 0.5f, 0f),
                new Vector3(size.x + 0.12f, standHeight + top, size.z + 0.12f), RoleColor(tank.Role));
            var click = StoreClickable.Add(stand.transform, tank.Title, tank.transform.position + new Vector3(0f, size.y * 0.5f - standHeight * 0.5f, 0f),
                new Vector3(size.x + 0.16f, size.y + standHeight + 0.1f, size.z + 0.16f), StoreClickable.Kind.Tank);
            click.tank = tank;
            click.slot = tank.Slot;
            tank.Stand = stand;
        }

        public static Color RoleColor(TankRole role)
        {
            switch (role)
            {
                case TankRole.Breeder: return new Color(0.42f, 0.52f, 0.68f);
                case TankRole.ForSale: return new Color(0.42f, 0.62f, 0.44f);
                case TankRole.Display: return new Color(0.7f, 0.6f, 0.4f);
                default: return new Color(0.7f, 0.44f, 0.44f);
            }
        }

        /// <summary>Thin floor pads mark the free spots in open sections; clicking one buys a tank.</summary>
        void RefreshEmptySlots()
        {
            foreach (var slot in slots)
            {
                bool wantMarker = slot.IsOpen && slot.tank == null;
                if (wantMarker && slot.emptyMarker == null)
                {
                    var floorPoint = new Vector3(slot.position.x, 0f, slot.position.z);
                    // An unscaled holder turned like the slot, so the pad and its click box line up with the wall.
                    var marker = new GameObject("Empty tank spot");
                    marker.transform.SetParent(transform, false);
                    marker.transform.SetPositionAndRotation(floorPoint, Quaternion.Euler(0f, slot.yaw, 0f));
                    // The wall aquarium's spot runs the length of the wall, along the floor just in front of where its glass will be.
                    var pad = slot.wall ? new Vector3(Balance.Data.store.wallAquarium.width, 0.02f, 0.8f) : new Vector3(1.72f, 0.02f, 0.82f);
                    var padAt = slot.wall ? new Vector3(0f, 0.01f, -Balance.Data.store.wallAquarium.depth * 0.5f - 0.05f) : Vector3.up * 0.01f;
                    MaterialFactory.Decor(PrimitiveType.Cube, "Empty tank spot", marker.transform, padAt, pad, new Color(0.45f, 0.74f, 0.74f));
                    var click = StoreClickable.Add(marker.transform, "Empty spot", floorPoint + padAt + Vector3.up * 0.29f, new Vector3(pad.x, 0.6f, pad.z), StoreClickable.Kind.EmptySlot);
                    click.slot = slot;
                    slot.emptyMarker = marker;
                }
                else if (!wantMarker && slot.emptyMarker != null)
                {
                    Destroy(slot.emptyMarker);
                    slot.emptyMarker = null;
                }
            }
        }

        /// <summary>Floors and walls. Rebuilt whenever a section unlocks or a wall aquarium goes in, so the walls open up round it.</summary>
        void BuildShell()
        {
            int lastOpen = UnlockedCount - 1;
            float halfW = SectionWidth * 0.5f, halfD = SectionDepth * 0.5f, t = WallThickness;

            foreach (var section in sections)
            {
                if (section.shell != null) Destroy(section.shell.gameObject);
                section.shell = new GameObject($"Shell - {section.name}").transform;
                section.shell.SetParent(transform, false);
                var root = section.shell;
                float cx = section.CenterX, cz = section.CenterZ, backZ = section.BackZ;
                var floorSize = new Vector3(SectionWidth, 0.1f, section.Depth);

                if (section.unlocked) Floor(root, new Vector3(cx, -0.05f, cz), floorSize, FloorColor);
                else Wall(root, "Floor", new Vector3(cx, -0.05f, cz), floorSize, LockedFloorColor);

                if (!section.unlocked)
                {
                    // Low dark curbs outline the space that can be bought.
                    Wall(root, "Curb", new Vector3(cx, 0.1f, backZ), new Vector3(SectionWidth, 0.2f, t), DarkColor);
                    Wall(root, "Curb", new Vector3(cx, 0.1f, -halfD), new Vector3(SectionWidth, 0.2f, t), DarkColor);
                    if (section.index == sections.Count - 1)
                        Wall(root, "Curb", new Vector3(cx + halfW, 0.1f, cz), new Vector3(t, 0.2f, section.Depth), DarkColor);
                    // A deeper room's west side, where it reaches north past the room before it.
                    if (backZ > halfD + 0.01f)
                        Wall(root, "Curb", new Vector3(cx - halfW, 0.1f, (halfD + backZ) * 0.5f), new Vector3(t, 0.2f, backZ - halfD), DarkColor);
                    var click = StoreClickable.Add(root, section.name, new Vector3(cx, 0.5f, cz), new Vector3(SectionWidth, 1f, section.Depth), StoreClickable.Kind.LockedSection);
                    click.section = section;
                    continue;
                }

                if (section.index == WallAquariumSection)
                {
                    BuildWallRoom(root, section);
                    continue;
                }

                Wall(root, "BackWall", new Vector3(cx, WallHeight * 0.5f, halfD + t * 0.5f), new Vector3(SectionWidth + t, WallHeight, t), WallColor);
                Stripes(root, cx, SectionWidth, halfD);
                Palm(root, new Vector3(cx + halfW - 0.45f, 0f, halfD - 0.45f));
                if (section.index == 0) Palm(root, new Vector3(cx - halfW + 0.45f, 0f, halfD - 0.45f));
                Surfboard(root, new Vector3(cx + (section.index % 2 == 0 ? 2f : -2f), 3.1f, halfD - 0.06f), BoardColors[section.index % BoardColors.Length]);

                // The front wall is cut down (dollhouse style) so the overhead camera can see in.
                if (section.index == 0)
                {
                    float side = (SectionWidth - EntranceWidth) * 0.5f;
                    float sideCenter = EntranceWidth * 0.5f + side * 0.5f;
                    Wall(root, "FrontWall", new Vector3(cx - sideCenter, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(side + t, FrontWallHeight, t), WallColor);
                    Wall(root, "FrontWall", new Vector3(cx + sideCenter, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(side + t, FrontWallHeight, t), WallColor);
                    // Door frame and a mat outside mark the one entrance.
                    Wall(root, "DoorPost", new Vector3(cx - EntranceWidth * 0.5f, 1.2f, -halfD - t * 0.5f), new Vector3(t, 2.4f, t * 1.4f), CounterColor);
                    Wall(root, "DoorPost", new Vector3(cx + EntranceWidth * 0.5f, 1.2f, -halfD - t * 0.5f), new Vector3(t, 2.4f, t * 1.4f), CounterColor);
                    Wall(root, "Lintel", new Vector3(cx, 2.45f, -halfD - t * 0.5f), new Vector3(EntranceWidth + t, 0.15f, t * 1.4f), CounterColor);
                    Wall(root, "DoorMat", new Vector3(cx, 0.005f, -halfD - 0.6f), new Vector3(EntranceWidth - 0.2f, 0.01f, 0.8f), DarkColor);

                    // The west wall has a staff door through to the back room, near the front.
                    float doorZ = -halfD + 1.6f;
                    float backLength = halfD + t - (doorZ + StaffDoorWidth * 0.5f);
                    float frontLength = (doorZ - StaffDoorWidth * 0.5f) - (-halfD - t);
                    float wallX = cx - halfW - t * 0.5f;
                    Wall(root, "SideWall", new Vector3(wallX, WallHeight * 0.5f, halfD + t - backLength * 0.5f), new Vector3(t, WallHeight, backLength), WallColor);
                    Wall(root, "SideWall", new Vector3(wallX, FrontWallHeight * 0.5f, -halfD - t + frontLength * 0.5f), new Vector3(t, FrontWallHeight, frontLength), WallColor);
                    Wall(root, "StaffDoorLintel", new Vector3(wallX, 2.5f, doorZ), new Vector3(t * 1.4f, 0.15f, StaffDoorWidth + t), CounterColor);
                }
                else
                {
                    Wall(root, "FrontWall", new Vector3(cx, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(SectionWidth + t, FrontWallHeight, t), WallColor);
                    SectionPillars(root, cx - halfW);
                }
                if (section.index == lastOpen)
                    Wall(root, "SideWall", new Vector3(cx + halfW + t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
            }
            BuildBigRoomShell();
        }

        /// <summary>Pillars where two open sections meet, at the back and (cut down) at the front.</summary>
        static void SectionPillars(Transform root, float x)
        {
            float halfD = SectionDepth * 0.5f;
            Wall(root, "Pillar", new Vector3(x, WallHeight * 0.5f, halfD - 0.15f), new Vector3(0.3f, WallHeight, 0.3f), CounterColor);
            Wall(root, "Pillar", new Vector3(x, FrontWallHeight * 0.5f + 0.1f, -halfD + 0.15f), new Vector3(0.3f, FrontWallHeight + 0.2f, 0.3f), CounterColor);
        }

        /// <summary>
        /// The East wing: twice as deep as the main floor, open to it along their shared side, and
        /// reaching north past the main floor's back wall. Each of its walls takes a wall aquarium
        /// (the east wall two), and each wall opens up round the aquariums set into it.
        /// </summary>
        void BuildWallRoom(Transform root, Section section)
        {
            float cx = section.CenterX, halfW = SectionWidth * 0.5f, halfD = SectionDepth * 0.5f, t = WallThickness, backZ = section.BackZ;
            // The west wall only runs north of the main floor; south of it the two rooms are open.
            float westLength = backZ + t - halfD;
            TankWall(root, section, new Vector3(cx - halfW - t * 0.5f, 0f, halfD + westLength * 0.5f), -90f, westLength);
            TankWall(root, section, new Vector3(cx, 0f, backZ + t * 0.5f), 0f, SectionWidth + t * 2f);
            TankWall(root, section, new Vector3(cx + halfW + t * 0.5f, 0f, (backZ - halfD) * 0.5f), 90f, section.Depth + t * 2f);
            Wall(root, "FrontWall", new Vector3(cx, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(SectionWidth + t, FrontWallHeight, t), WallColor);
            SectionPillars(root, cx - halfW);
            // The aquariums fill the corners, but the one by the front and the main floor stays free.
            Palm(root, new Vector3(cx - halfW + 0.6f, 0f, -halfD + 0.45f));
            // No room for the surfboard over a raised back-wall aquarium.
            var back = slots.Find(s => s.wall && s.section == section && s.yaw == 0f);
            if (back == null || back.tank == null || back.tank.transform.position.y + back.tank.Tank.InnerSize.y < 2.9f)
                Surfboard(root, new Vector3(cx - 2f, 3.1f, backZ - 0.06f), BoardColors[section.index % BoardColors.Length]);
        }

        Transform bigRoomShell;

        /// <summary>
        /// Staff only: the big breeding room, through a door in the breeding room's west wall. Its
        /// back wall is kept for the wall breeding tank, where the big fish breed. Rebuilt with the
        /// rest of the shell, so the wall opens up round the tank once it's in.
        /// </summary>
        void BuildBigRoomShell()
        {
            if (bigRoomShell != null) Destroy(bigRoomShell.gameObject);
            bigRoomShell = new GameObject("Big breeding room").transform;
            bigRoomShell.SetParent(transform, false);
            var root = bigRoomShell;
            float cx = BigRoomCenterX, halfW = BigRoomWidth * 0.5f, halfD = SectionDepth * 0.5f, t = WallThickness;
            // The floor runs under the west wall; the wall it shares with the breeding room stands on that room's floor.
            Floor(root, new Vector3(cx - t * 0.5f, -0.05f, 0f), new Vector3(BigRoomWidth + t, 0.1f, SectionDepth), BackRoomFloorColor);
            TankWall(root, null, new Vector3(cx - t * 0.5f, 0f, halfD + t * 0.5f), 0f, BigRoomWidth + t);
            Wall(root, "SideWall", new Vector3(cx - halfW - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
            Wall(root, "FrontWall", new Vector3(cx - t * 0.5f, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(BigRoomWidth + t, FrontWallHeight, t), WallColor);
            Palm(root, new Vector3(cx - halfW + 0.45f, 0f, -halfD + 0.45f));
            // A long bench of breeding gear down the west wall: buckets, nets and a spawning mop.
            Wall(root, "Bench", new Vector3(cx - halfW + 0.35f, 0.45f, -0.2f), new Vector3(0.6f, 0.9f, 2.6f), CounterColor);
            Wall(root, "Buckets", new Vector3(cx - halfW + 0.35f, 1.05f, -1f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.75f));
            Wall(root, "Buckets", new Vector3(cx - halfW + 0.35f, 1.05f, -0.6f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.95f, 0.6f, 0.3f));
            Wall(root, "Nets", new Vector3(cx - halfW + 0.3f, 0.98f, 0.4f), new Vector3(0.36f, 0.16f, 0.5f), new Color(0.3f, 0.62f, 0.66f));
            var tank = slots.Find(s => s.WallBreeder);
            if (tank == null || tank.tank == null || tank.tank.transform.position.y + tank.tank.Tank.InnerSize.y < 2.9f)
                Surfboard(root, new Vector3(cx + 2f, 3.1f, halfD - 0.06f), BoardColors[3]);
        }

        /// <summary>
        /// A full-height wall, its middle at floor level and turned like a wall aquarium against it
        /// would be (so its inside faces the room), opened up round each wall aquarium set into it:
        /// wall below and above each tank (rising like a bulkhead over the tallest), a coral stripe
        /// along the inside that stops either side of them, and a wall-coloured housing outside over
        /// the part of each tank that reaches out behind the wall. Section null is the big breeding room.
        /// </summary>
        void TankWall(Transform root, Section section, Vector3 middle, float yaw, float length)
        {
            var frame = new GameObject("Wall").transform;
            frame.SetParent(root, false);
            frame.SetPositionAndRotation(middle, Quaternion.Euler(0f, yaw, 0f));
            float t = WallThickness, half = length * 0.5f;
            var inWall = new List<Tank>();
            foreach (var slot in slots)
                if (slot.wall && slot.tank != null && slot.section == section && Mathf.Abs(Mathf.DeltaAngle(slot.yaw, yaw)) < 1f
                    && Mathf.Abs(frame.InverseTransformPoint(slot.tank.transform.position).x) < half)
                    inWall.Add(slot.tank.Tank);
            inWall.Sort((a, b) => frame.InverseTransformPoint(a.transform.position).x.CompareTo(frame.InverseTransformPoint(b.transform.position).x));

            const float rim = 0.03f;
            float from = -half;
            var housing = WallColor * 0.92f;
            housing.a = 1f;
            foreach (var tank in inWall)
            {
                var size = tank.InnerSize;
                var at = frame.InverseTransformPoint(tank.transform.position);
                float left = at.x - size.x * 0.5f - rim, right = at.x + size.x * 0.5f + rim;
                float bottom = at.y - Tank.BaseDepth, top = at.y + size.y + rim;
                if (left > from) Wall(frame, "BackWall", new Vector3((from + left) * 0.5f, WallHeight * 0.5f, 0f), new Vector3(left - from, WallHeight, t), WallColor);
                Wall(frame, "BackWall", new Vector3(at.x, bottom * 0.5f, 0f), new Vector3(right - left, bottom, t), WallColor);
                // Over the tallest wall aquarium the wall rises above the rest, like a bulkhead.
                float above = Mathf.Max(WallHeight, top + WallAquariumHeadroom);
                Wall(frame, "BackWall", new Vector3(at.x, (top + above) * 0.5f, 0f), new Vector3(right - left, above - top, t), WallColor);
                // The housing round the back: a roof, two sides and a back, just clear of the glass.
                float outer = at.z + size.z * 0.5f + 0.05f, depth = outer + 0.25f - t * 0.5f;
                float mid = t * 0.5f + depth * 0.5f;
                Wall(frame, "Housing", new Vector3(at.x, top + 0.06f, mid), new Vector3(right - left + 0.3f, 0.12f, depth), housing);
                Wall(frame, "Housing", new Vector3(left - 0.07f, (top + 0.12f) * 0.5f, mid), new Vector3(0.14f, top + 0.12f, depth), housing);
                Wall(frame, "Housing", new Vector3(right + 0.07f, (top + 0.12f) * 0.5f, mid), new Vector3(0.14f, top + 0.12f, depth), housing);
                Wall(frame, "Housing", new Vector3(at.x, (top + 0.12f) * 0.5f, outer + 0.15f), new Vector3(right - left + 0.3f, top + 0.12f, 0.2f), housing);
                from = right;
            }
            if (half > from) Wall(frame, "BackWall", new Vector3((from + half) * 0.5f, WallHeight * 0.5f, 0f), new Vector3(half - from, WallHeight, t), WallColor);
            StripeAlong(frame, -half, half, -t * 0.5f - 0.01f, 2.35f, 0.22f, StripeColor, inWall);
            StripeAlong(frame, -half, half, -t * 0.5f - 0.01f, 2.58f, 0.08f, StripeColor2, inWall);
        }

        /// <summary>One stripe along a <see cref="TankWall"/>'s inside, leaving a gap round any tank that reaches up past it.</summary>
        static void StripeAlong(Transform frame, float from, float to, float z, float y, float height, Color color, List<Tank> tanks)
        {
            float start = from;
            foreach (var tank in tanks)
            {
                var at = frame.InverseTransformPoint(tank.transform.position);
                if (y - height * 0.5f >= at.y + tank.InnerSize.y + 0.05f) continue;
                float gapWest = at.x - tank.InnerSize.x * 0.5f - 0.08f, gapEast = at.x + tank.InnerSize.x * 0.5f + 0.08f;
                if (gapWest > start) Wall(frame, "Stripe", new Vector3((start + gapWest) * 0.5f, y, z), new Vector3(gapWest - start, height, 0.02f), color);
                start = Mathf.Max(start, gapEast);
            }
            if (to > start) Wall(frame, "Stripe", new Vector3((start + to) * 0.5f, y, z), new Vector3(to - start, height, 0.02f), color);
        }

        /// <summary>
        /// Staff only: the breeding room, where the breeder and quarantine tanks stand round the
        /// walls, the supplier computer sits by the front wall, a little back door opens onto the
        /// parking lot where the fish seller parks his van, and a door in the west wall leads
        /// through to the big breeding room.
        /// </summary>
        void BuildBackRoom()
        {
            var root = new GameObject("Back room").transform;
            root.SetParent(transform, false);
            float cx = BackRoomCenterX, halfW = BackRoomWidth * 0.5f, halfD = SectionDepth * 0.5f, t = WallThickness;
            Floor(root, new Vector3(cx, -0.05f, 0f), new Vector3(BackRoomWidth + t * 2f, 0.1f, SectionDepth), BackRoomFloorColor);
            // The back wall, cut for the back door near its east end.
            float wallZ = halfD + t * 0.5f, west = cx - halfW - t, east = cx + halfW + t;
            float doorX = cx + BackDoorOffset, doorWest = doorX - BackDoorWidth * 0.5f, doorEast = doorX + BackDoorWidth * 0.5f;
            const float doorHeight = 2.1f;
            Wall(root, "BackWall", new Vector3((west + doorWest) * 0.5f, WallHeight * 0.5f, wallZ), new Vector3(doorWest - west, WallHeight, t), WallColor);
            Wall(root, "BackWall", new Vector3((doorEast + east) * 0.5f, WallHeight * 0.5f, wallZ), new Vector3(east - doorEast, WallHeight, t), WallColor);
            Wall(root, "BackWall", new Vector3(doorX, (doorHeight + WallHeight) * 0.5f, wallZ), new Vector3(BackDoorWidth, WallHeight - doorHeight, t), WallColor);
            BuildBackDoor(root, new Vector3(doorX, 0f, halfD + t * 0.5f), doorHeight);
            Stripes(root, cx - 0.6f, BackRoomWidth - 1.6f, halfD);
            // The west wall, shared with the big breeding room, with a door through near the front
            // (cut down in front of it, like the main floor's staff door).
            float sideX = cx - halfW - t * 0.5f;
            float doorSouth = BigRoomDoorZ - StaffDoorWidth * 0.5f, doorNorth = BigRoomDoorZ + StaffDoorWidth * 0.5f;
            Wall(root, "SideWall", new Vector3(sideX, WallHeight * 0.5f, (doorNorth + halfD + t) * 0.5f), new Vector3(t, WallHeight, halfD + t - doorNorth), WallColor);
            Wall(root, "SideWall", new Vector3(sideX, FrontWallHeight * 0.5f, (doorSouth - halfD - t) * 0.5f), new Vector3(t, FrontWallHeight, doorSouth + halfD + t), WallColor);
            Wall(root, "StaffDoorLintel", new Vector3(sideX, 2.5f, BigRoomDoorZ), new Vector3(t * 1.4f, 0.15f, StaffDoorWidth + t), CounterColor);
            Wall(root, "FrontWall", new Vector3(cx, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(BackRoomWidth + t * 2f, FrontWallHeight, t), WallColor);
            // A work bench along the front wall, beside the computer desk.
            Wall(root, "Bench", new Vector3(cx + 1.5f, 0.45f, -halfD + 0.45f), new Vector3(2.6f, 0.9f, 0.6f), CounterColor);
            Wall(root, "Buckets", new Vector3(cx + 0.7f, 1.05f, -halfD + 0.45f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.75f));
            Wall(root, "Nets", new Vector3(cx + 2.2f, 0.98f, -halfD + 0.4f), new Vector3(0.5f, 0.16f, 0.36f), new Color(0.3f, 0.62f, 0.66f));
        }

        /// <summary>A small wooden staff door in the back wall, with a step and a lamp outside.</summary>
        static void BuildBackDoor(Transform root, Vector3 bottom, float height)
        {
            var wood = new Color(0.55f, 0.36f, 0.22f);
            var frame = CounterColor;
            Wall(root, "DoorPost", bottom + new Vector3(-BackDoorWidth * 0.5f, height * 0.5f, 0f), new Vector3(0.08f, height, WallThickness * 1.3f), frame);
            Wall(root, "DoorPost", bottom + new Vector3(BackDoorWidth * 0.5f, height * 0.5f, 0f), new Vector3(0.08f, height, WallThickness * 1.3f), frame);
            Wall(root, "Lintel", bottom + new Vector3(0f, height + 0.04f, 0f), new Vector3(BackDoorWidth + 0.16f, 0.08f, WallThickness * 1.3f), frame);
            Wall(root, "BackDoor", bottom + new Vector3(0f, height * 0.5f, 0f), new Vector3(BackDoorWidth - 0.08f, height - 0.02f, 0.06f), wood);
            for (int side = -1; side <= 1; side += 2)
            {
                Wall(root, "DoorPanel", bottom + new Vector3(0f, height * 0.72f, side * 0.035f), new Vector3(BackDoorWidth - 0.4f, 0.5f, 0.01f), wood * 0.85f);
                Wall(root, "DoorPanel", bottom + new Vector3(0f, height * 0.3f, side * 0.035f), new Vector3(BackDoorWidth - 0.4f, 0.6f, 0.01f), wood * 0.85f);
                MaterialFactory.Decor(PrimitiveType.Sphere, "DoorKnob", root, bottom + new Vector3(BackDoorWidth * 0.32f, 1.0f, side * 0.06f), new Vector3(0.07f, 0.07f, 0.07f), new Color(0.85f, 0.72f, 0.35f));
            }
            Wall(root, "DoorStep", bottom + new Vector3(0f, 0.06f, 0.4f), new Vector3(BackDoorWidth + 0.3f, 0.12f, 0.5f), new Color(0.62f, 0.6f, 0.56f));
            Wall(root, "DoorLamp", bottom + new Vector3(0f, height + 0.3f, 0.16f), new Vector3(0.22f, 0.16f, 0.12f), new Color(1f, 0.92f, 0.65f));
        }

        /// <summary>
        /// The counter stands along the east wall by the front, side on to the room: the shopkeeper
        /// behind it faces into the shop, and customers queue up from the front of it back into the room.
        /// </summary>
        void BuildRegister()
        {
            var root = new GameObject("Register").transform;
            root.SetParent(transform, false);
            var counter = new Vector3(SectionWidth * 0.5f - 1.5f, 0f, -3.1f);
            Wall(root, "Counter", counter + new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 1f, 2f), CounterColor);
            Wall(root, "CounterTop", counter + new Vector3(0f, 1.02f, 0f), new Vector3(0.66f, 0.04f, 2.08f), CounterTopColor);
            // Bamboo canes up the customers' side of the counter.
            for (int i = 0; i < 9; i++)
                MaterialFactory.Decor(PrimitiveType.Cylinder, "Bamboo", root, counter + new Vector3(-0.31f, 0.5f, -0.88f + i * 0.22f), new Vector3(0.09f, 0.5f, 0.09f), i % 2 == 0 ? new Color(0.8f, 0.64f, 0.38f) : new Color(0.74f, 0.58f, 0.33f));
            var till = counter + new Vector3(0.05f, 0f, 0.45f);
            Wall(root, "Till", till + new Vector3(0f, 1.16f, 0f), new Vector3(0.38f, 0.24f, 0.44f), DarkColor);
            Wall(root, "TillDrawer", till + new Vector3(0.2f, 1.08f, 0f), new Vector3(0.06f, 0.08f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
            var display = Wall(root, "TillDisplay", till + new Vector3(-0.12f, 1.38f, 0f), new Vector3(0.03f, 0.16f, 0.3f), new Color(0.3f, 0.75f, 0.45f));
            display.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            RegisterPoint = till + new Vector3(0f, 1.6f, 0f);
            registerFront = new Vector3(counter.x - 0.75f, 0f, counter.z - 0.2f);

            // The shopkeeper (you) behind the counter, beside the till rather than hidden behind it.
            var keeper = new GameObject("Shopkeeper").transform;
            keeper.SetParent(root, false);
            keeper.position = new Vector3(counter.x + 0.72f, 0f, counter.z - 0.2f);
            keeper.rotation = Quaternion.LookRotation(Vector3.left);
            var placeholder = new GameObject("Placeholder").transform;
            placeholder.SetParent(keeper, false);
            placeholder.localScale = Vector3.one * Customer.PersonScale;
            var apron = new Color(0.2f, 0.45f, 0.6f);
            MaterialFactory.Decor(PrimitiveType.Cube, "Legs", placeholder, new Vector3(0f, 0.42f, 0f), new Vector3(0.32f, 0.84f, 0.2f), new Color(0.25f, 0.25f, 0.3f));
            MaterialFactory.Decor(PrimitiveType.Capsule, "Torso", placeholder, new Vector3(0f, 1.17f, 0f), new Vector3(0.44f, 0.36f, 0.28f), apron);
            MaterialFactory.Decor(PrimitiveType.Sphere, "Head", placeholder, new Vector3(0f, 1.62f, 0f), new Vector3(0.24f, 0.27f, 0.24f), new Color(0.9f, 0.75f, 0.6f));
            keeper.gameObject.AddComponent<Shopkeeper>().Build(placeholder.gameObject);
            ShopkeeperHand = keeper.position + new Vector3(-0.35f, 1.05f * Customer.PersonScale, 0f);
            StoreClickable.Add(root, "Register", counter + new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 1.5f, 2.1f), StoreClickable.Kind.Register);
        }

        void BuildComputer()
        {
            var root = new GameObject("Supplier computer").transform;
            root.SetParent(transform, false);
            // In the breeding room, by the front wall.
            var desk = new Vector3(BackRoomCenterX - 1.9f, 0f, -3.75f);
            Wall(root, "Desk", desk + new Vector3(0f, 0.72f, 0f), new Vector3(1.4f, 0.06f, 0.7f), CounterColor);
            for (int sx = -1; sx <= 1; sx += 2)
                Wall(root, "DeskLeg", desk + new Vector3(sx * 0.62f, 0.35f, 0f), new Vector3(0.06f, 0.7f, 0.62f), CounterColor);
            Wall(root, "MonitorStand", desk + new Vector3(0f, 0.84f, 0.18f), new Vector3(0.08f, 0.18f, 0.08f), DarkColor);
            Wall(root, "Monitor", desk + new Vector3(0f, 1.1f, 0.18f), new Vector3(0.62f, 0.4f, 0.05f), DarkColor);
            Wall(root, "Screen", desk + new Vector3(0f, 1.1f, 0.15f), new Vector3(0.56f, 0.34f, 0.01f), new Color(0.35f, 0.6f, 0.9f));
            Wall(root, "Keyboard", desk + new Vector3(0f, 0.76f, -0.12f), new Vector3(0.45f, 0.02f, 0.15f), DarkColor);
            Wall(root, "Chair", desk + new Vector3(0f, 0.25f, -0.65f), new Vector3(0.5f, 0.5f, 0.5f), StripeColor);
            ComputerPoint = desk + new Vector3(0f, 1.45f, 0.18f);
            StoreClickable.Add(root, "Supplier computer", desk + new Vector3(0f, 0.7f, -0.1f), new Vector3(1.45f, 1.4f, 1.3f), StoreClickable.Kind.Computer);
        }

        /// <summary>The pavement along the shop front and the street beyond it, where customers come from.</summary>
        void BuildPavement()
        {
            float halfD = SectionDepth * 0.5f;
            float west = BuildingWestX;
            float east = (sections.Count - 1) * SectionWidth + SectionWidth * 0.5f + WallThickness;
            var outside = new GameObject("Pavement").transform;
            outside.SetParent(transform, false);
            Wall(outside, "Pavement", new Vector3((west + east) * 0.5f, -0.055f, -halfD - 2.3f), new Vector3(east - west, 0.1f, 4.2f), new Color(0.72f, 0.68f, 0.6f));
            Wall(outside, "Pavement", new Vector3((west + east) * 0.5f, -0.06f, -halfD - 6.4f), new Vector3(east - west, 0.1f, 4f), new Color(0.32f, 0.34f, 0.36f));
        }

        /// <summary>
        /// Behind the shop: sandy ground all along the back, and an empty parking lot behind the
        /// breeding room (faded bays, a lamp post, kerbs) where the fish seller parks his van: a
        /// wholesaler's van with its back doors open, a folding table of aerated holding tanks and
        /// bagged fish, foam shipping boxes and a sign on the roof. He stands behind the table
        /// facing the breeding room's back door (and the camera).
        /// </summary>
        void BuildParkingLot()
        {
            float halfD = SectionDepth * 0.5f, wall = WallThickness;
            float west = BackRoomCenterX - BackRoomWidth * 0.5f - wall;
            float east = (sections.Count - 1) * SectionWidth + SectionWidth * 0.5f + wall;
            var lot = new GameObject("Parking lot").transform;
            lot.SetParent(transform, false);
            float near = halfD + wall;
            // The sand runs behind the whole building, from the big breeding room to the East wing,
            // and well past the housings of the wall aquariums reaching out behind its walls.
            float groundWest = BuildingWestX - 4f, groundEast = east + Mathf.Max(4f, WallAquariumReach + 1.5f);
            float groundFar = GroundFarZ;
            if (sections.Count > WallAquariumSection) groundFar = Mathf.Max(groundFar, sections[WallAquariumSection].BackZ + wall * 0.5f + WallAquariumReach + 1.5f);
            Wall(lot, "Pavement", new Vector3((groundWest + groundEast) * 0.5f, -0.08f, (near + groundFar) * 0.5f), new Vector3(groundEast - groundWest, 0.1f, groundFar - near), new Color(0.86f, 0.78f, 0.6f));
            float lotWest = west - 1f, lotEast = BackRoomCenterX + BackRoomWidth * 0.5f + 1.2f;
            float lotNear = near + 0.6f, lotFar = LotFarZ - 1.5f;
            var asphalt = new Color(0.3f, 0.31f, 0.33f);
            Wall(lot, "Pavement", new Vector3((lotWest + lotEast) * 0.5f, -0.04f, (lotNear + lotFar) * 0.5f), new Vector3(lotEast - lotWest, 0.08f, lotFar - lotNear), asphalt);
            // A concrete path from the back door out to the lot.
            Wall(lot, "Pavement", new Vector3(BackRoomCenterX + BackDoorOffset, -0.035f, (near + lotNear) * 0.5f + 0.05f), new Vector3(1.6f, 0.08f, lotNear - near + 0.1f), new Color(0.68f, 0.66f, 0.62f));
            // Kerbs round the asphalt.
            var kerb = new Color(0.78f, 0.76f, 0.72f);
            Wall(lot, "Kerb", new Vector3((lotWest + lotEast) * 0.5f, 0.04f, lotFar + 0.06f), new Vector3(lotEast - lotWest + 0.24f, 0.12f, 0.12f), kerb);
            Wall(lot, "Kerb", new Vector3(lotWest - 0.06f, 0.04f, (lotNear + lotFar) * 0.5f), new Vector3(0.12f, 0.12f, lotFar - lotNear), kerb);
            Wall(lot, "Kerb", new Vector3(lotEast + 0.06f, 0.04f, (lotNear + lotFar) * 0.5f), new Vector3(0.12f, 0.12f, lotFar - lotNear), kerb);
            // Faded white bays along the far kerb, all empty but the seller's.
            var lines = new Color(0.88f, 0.88f, 0.84f);
            const float bay = 2.6f;
            for (float x = lotWest + 0.4f; x <= lotEast - 0.3f; x += bay)
                Wall(lot, "Stripe", new Vector3(x, 0.002f, lotFar - 2.6f), new Vector3(0.1f, 0.01f, 5f), lines);
            Wall(lot, "Stripe", new Vector3((lotWest + lotEast) * 0.5f, 0.002f, lotFar - 5.1f), new Vector3(lotEast - lotWest - 0.8f, 0.01f, 0.1f), lines);
            // An oil stain or two and a lamp post in the corner.
            MaterialFactory.Decor(PrimitiveType.Cylinder, "Stripe", lot, new Vector3(lotWest + 3.2f, 0.003f, lotFar - 2.4f), new Vector3(0.9f, 0.002f, 0.6f), asphalt * 0.75f);
            MaterialFactory.Decor(PrimitiveType.Cylinder, "Stripe", lot, new Vector3(lotWest + 6.1f, 0.003f, lotFar - 3f), new Vector3(0.6f, 0.002f, 0.5f), asphalt * 0.8f);
            var lampAt = new Vector3(lotWest + 0.5f, 0f, lotNear + 0.5f);
            MaterialFactory.Decor(PrimitiveType.Cylinder, "LampPost", lot, lampAt + new Vector3(0f, 1.8f, 0f), new Vector3(0.1f, 1.8f, 0.1f), new Color(0.25f, 0.27f, 0.3f));
            Wall(lot, "LampArm", lampAt + new Vector3(0.35f, 3.55f, 0f), new Vector3(0.8f, 0.06f, 0.08f), new Color(0.25f, 0.27f, 0.3f));
            Wall(lot, "Lamp", lampAt + new Vector3(0.7f, 3.47f, 0f), new Vector3(0.3f, 0.1f, 0.18f), new Color(1f, 0.92f, 0.65f));
            for (int i = 0; i < 3; i++)
                MaterialFactory.Decor(PrimitiveType.Cylinder, "Bollard", lot, new Vector3(lotEast - 0.4f, 0.4f, lotNear + 1f + i * 1.4f), new Vector3(0.16f, 0.4f, 0.16f), i % 2 == 0 ? StripeColor2 : new Color(0.25f, 0.27f, 0.3f));

            var root = new GameObject("Fish seller").transform;
            root.SetParent(transform, false);
            // The table stands just out from the back door, the van parked beside it in a bay.
            var table = new Vector3(BackRoomCenterX + BackDoorOffset, 0f, SellerTableZ);
            var van = table + new Vector3(-2.65f, 0f, 0.05f);
            var paint = new Color(0.95f, 0.97f, 0.96f);
            var glass = new Color(0.2f, 0.32f, 0.42f);
            var tyre = new Color(0.12f, 0.12f, 0.13f);
            // Cab at the west end, the load box behind it; the open back faces the shop door.
            Wall(root, "VanChassis", van + new Vector3(0f, 0.32f, 0f), new Vector3(3.1f, 0.2f, 1.5f), tyre);
            Wall(root, "VanBox", van + new Vector3(0.4f, 1.12f, 0f), new Vector3(2.4f, 1.6f, 1.6f), paint);
            Wall(root, "VanCab", van + new Vector3(-1.2f, 0.88f, 0f), new Vector3(0.8f, 1.12f, 1.56f), paint);
            Wall(root, "VanBonnet", van + new Vector3(-1.45f, 0.6f, 0f), new Vector3(0.3f, 0.5f, 1.5f), paint);
            Wall(root, "VanWindscreen", van + new Vector3(-1.61f, 1.18f, 0f), new Vector3(0.03f, 0.42f, 1.36f), glass);
            Wall(root, "VanWindow", van + new Vector3(-1.15f, 1.18f, -0.79f), new Vector3(0.5f, 0.36f, 0.02f), glass);
            Wall(root, "VanWindow", van + new Vector3(-1.15f, 1.18f, 0.79f), new Vector3(0.5f, 0.36f, 0.02f), glass);
            // Coral and aqua stripes down the side the camera sees, with a painted fish.
            Wall(root, "Stripe", van + new Vector3(0.4f, 0.98f, -0.81f), new Vector3(2.4f, 0.16f, 0.02f), StripeColor);
            Wall(root, "Stripe", van + new Vector3(0.4f, 1.16f, -0.81f), new Vector3(2.4f, 0.07f, 0.02f), new Color(0.3f, 0.75f, 0.78f));
            MaterialFactory.Decor(PrimitiveType.Sphere, "Stripe", root, van + new Vector3(0.5f, 1.5f, -0.81f), new Vector3(0.5f, 0.26f, 0.02f), new Color(1f, 0.6f, 0.2f));
            var tail = MaterialFactory.Decor(PrimitiveType.Cube, "Stripe", root, van + new Vector3(0.84f, 1.5f, -0.81f), new Vector3(0.18f, 0.18f, 0.02f), new Color(1f, 0.6f, 0.2f));
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var wheel = MaterialFactory.Decor(PrimitiveType.Cylinder, "Wheel", root, van + new Vector3(sx < 0 ? -1.1f : 1f, 0.3f, sz * 0.74f), new Vector3(0.6f, 0.06f, 0.6f), tyre);
                    wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var hub = MaterialFactory.Decor(PrimitiveType.Cylinder, "Wheel", root, van + new Vector3(sx < 0 ? -1.1f : 1f, 0.3f, sz * 0.78f), new Vector3(0.28f, 0.02f, 0.28f), new Color(0.8f, 0.8f, 0.82f));
                    hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            // Back doors swung open.
            var back = van.x + 1.6f;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var hinge = new Vector3(back, 0f, van.z + sz * 0.8f);
                var open = new Vector3(0.95f, 0f, sz * 0.3f).normalized;
                var door = Wall(root, "VanDoor", hinge + open * 0.39f + Vector3.up * 1.12f, new Vector3(0.78f, 1.5f, 0.04f), paint);
                door.transform.localRotation = Quaternion.FromToRotation(Vector3.right, open);
            }
            // Foam shipping boxes stacked inside the back and beside the van.
            var foam = new Color(0.96f, 0.96f, 0.93f);
            Wall(root, "FoamBox", van + new Vector3(1.25f, 0.62f, 0.35f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", van + new Vector3(1.25f, 0.62f, -0.2f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", van + new Vector3(1.25f, 0.99f, 0.1f), new Vector3(0.5f, 0.36f, 0.42f), foam);

            // The folding table: two aerated holding tanks, an air pump and a row of bagged fish.
            var metal = new Color(0.62f, 0.64f, 0.66f);
            Wall(root, "StallTable", table + new Vector3(0f, 0.74f, 0f), new Vector3(1.3f, 0.04f, 0.6f), new Color(0.3f, 0.62f, 0.66f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Wall(root, "StallLeg", table + new Vector3(sx * 0.58f, 0.36f, sz * 0.24f), new Vector3(0.04f, 0.72f, 0.04f), metal);
            var water = new Color(0.45f, 0.75f, 0.9f);
            var fishColors = new[] { new Color(1f, 0.55f, 0.15f), new Color(0.95f, 0.85f, 0.2f), new Color(0.3f, 0.6f, 1f), new Color(0.95f, 0.35f, 0.5f) };
            for (int t = 0; t < 2; t++)
            {
                var holding = table + new Vector3(-0.38f + t * 0.42f, 0.9f, 0.08f);
                Wall(root, "HoldingTank", holding, new Vector3(0.36f, 0.28f, 0.26f), water);
                Wall(root, "HoldingTankLid", holding + new Vector3(0f, 0.15f, 0f), new Vector3(0.38f, 0.02f, 0.28f), DarkColor);
                for (int f = 0; f < 3; f++)
                    MaterialFactory.Decor(PrimitiveType.Capsule, "Fish", root, holding + new Vector3(-0.1f + f * 0.1f, -0.04f + (f % 2) * 0.07f, -0.135f), new Vector3(0.03f, 0.04f, 0.015f), fishColors[(f + t) % fishColors.Length]).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                MaterialFactory.Decor(PrimitiveType.Cylinder, "AirLine", root, holding + new Vector3(0.12f, 0.24f, 0.08f), new Vector3(0.012f, 0.1f, 0.012f), new Color(0.85f, 0.95f, 0.9f));
            }
            Wall(root, "AirPump", table + new Vector3(0.5f, 0.82f, 0.15f), new Vector3(0.16f, 0.1f, 0.12f), DarkColor);
            // A stack of taped-up foam boxes at the end of the table, waiting to come in.
            Wall(root, "FoamBox", table + new Vector3(0.98f, 0.18f, 0.05f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", table + new Vector3(0.98f, 0.55f, 0.05f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "BoxTape", table + new Vector3(0.98f, 0.735f, 0.05f), new Vector3(0.08f, 0.01f, 0.43f), new Color(0.3f, 0.6f, 0.85f));
            for (int b = 0; b < 4; b++)
            {
                var bag = table + new Vector3(0.12f + b * 0.15f, 0.86f, -0.18f);
                MaterialFactory.Decor(PrimitiveType.Sphere, "FishBag", root, bag, new Vector3(0.12f, 0.16f, 0.12f), new Color(0.7f, 0.88f, 0.96f));
                MaterialFactory.Decor(PrimitiveType.Capsule, "Fish", root, bag + new Vector3(0f, -0.01f, -0.055f), new Vector3(0.025f, 0.03f, 0.02f), fishColors[b]).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                MaterialFactory.Decor(PrimitiveType.Cylinder, "BagKnot", root, bag + new Vector3(0f, 0.1f, 0f), new Vector3(0.03f, 0.03f, 0.03f), new Color(0.9f, 0.9f, 0.9f));
            }

            // A sign along the roof, facing the street (and the camera).
            // Leaning back, so it faces the camera looking down from above the street.
            var tilt = Quaternion.Euler(35f, 0f, 0f);
            var sign = Wall(root, "VanSign", van + new Vector3(0.4f, 2.2f, -0.3f), new Vector3(2.3f, 0.48f, 0.05f), new Color(0.98f, 0.92f, 0.72f));
            sign.transform.localRotation = tilt;
            Wall(root, "VanSign", van + new Vector3(0.4f, 1.95f, -0.3f), new Vector3(0.06f, 0.1f, 0.06f), DarkColor);
            var text = new GameObject("VanSignText").AddComponent<TextMesh>();
            text.transform.SetParent(sign.transform.parent, false);
            text.transform.SetPositionAndRotation(sign.transform.position + tilt * new Vector3(0f, 0f, -0.03f), tilt);
            text.text = "TROPICAL FISH WHOLESALE\n<size=34>live fish \u00b7 eggs \u00b7 fry</size>";
            text.richText = true;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.028f;
            text.fontSize = 48;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.1f, 0.3f, 0.4f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            var seller = new GameObject("Fish seller (person)").transform;
            seller.SetParent(root, false);
            seller.position = table + new Vector3(0f, 0f, 0.68f);
            seller.rotation = Quaternion.LookRotation(new Vector3(0.25f, 0f, -1f));
            seller.gameObject.AddComponent<FishSeller>();
            FishSellerPoint = seller.position + Vector3.up * 1.85f;
            StoreClickable.Add(root, "Fish seller", table + new Vector3(-0.35f, 0.9f, 0.3f), new Vector3(2.2f, 1.8f, 1.8f), StoreClickable.Kind.FishSeller);
        }

        // Wall dressing and palm fronds overhead don't block the walkway (the palm's pot and trunk do).
        static readonly HashSet<string> NotObstacles = new HashSet<string> { "Floor", "DoorMat", "Empty tank spot", "Water", "Caustics", "Bubble", "Stripe", "Frond", "Surfboard", "Pavement", "VanSign", "VanSignText", "Kerb", "Bollard", "LampPost", "LampArm", "Lamp" };

        /// <summary>
        /// Bakes where customers can walk: the open sections' floors and the pavement outside,
        /// minus every wall, stand, counter and desk (taken from their renderer bounds).
        /// </summary>
        public void BuildNavMesh()
        {
            var sources = new List<NavMeshBuildSource>();
            foreach (var section in sections)
                if (section.unlocked)
                    sources.Add(StoreNavMesh.Box(new Vector3(section.CenterX, -0.05f, section.CenterZ), new Vector3(SectionWidth, 0.1f, section.Depth), true));
            sources.Add(StoreNavMesh.Box(new Vector3(0f, -0.05f, -SectionDepth * 0.5f - 1.8f), new Vector3(6f, 0.1f, 3.8f), true));

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (NotObstacles.Contains(renderer.gameObject.name)) continue;
                // People move and wave their arms about; they're never part of the floor plan.
                if (renderer is SkinnedMeshRenderer) continue;
                var b = renderer.bounds;
                if (b.max.y < 0.02f) continue;
                sources.Add(StoreNavMesh.Box(b.center, b.size + new Vector3(0.02f, 0f, 0.02f), false));
            }

            float width = sections.Count * SectionWidth + 4f;
            // From the street out front to the back of the East wing, the deepest room.
            float far = SectionDepth * 1.5f + 1f, near = -SectionDepth * 0.5f - 7f;
            var bounds = new Bounds(new Vector3((sections.Count - 1) * SectionWidth * 0.5f, 1f, (far + near) * 0.5f), new Vector3(width, 5f, far - near));
            StoreNavMesh.Build(sources, bounds);
        }

        static GameObject Wall(Transform parent, string name, Vector3 position, Vector3 size, Color color)
            => MaterialFactory.Decor(PrimitiveType.Cube, name, parent, position, size, color);

        static Texture2D planks;

        /// <summary>A wooden plank floor: the colour tints a procedural plank texture.</summary>
        static void Floor(Transform parent, Vector3 position, Vector3 size, Color color)
        {
            var floor = Wall(parent, "Floor", position, size, color);
            if (planks == null) planks = MakePlanks();
            var material = floor.GetComponent<Renderer>().sharedMaterial;
            material.mainTexture = planks;
            // Each tile of the texture is four planks 0.3 m wide and 2.4 m long.
            material.mainTextureScale = new Vector2(size.x / 2.4f, size.z / 1.2f);
        }

        static Texture2D MakePlanks()
        {
            const int w = 128, h = 64, rows = 4;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Planks", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var random = new System.Random(7);
            var pixels = new Color[w * h];
            for (int row = 0; row < rows; row++)
            {
                float tone = 0.86f + (float)random.NextDouble() * 0.14f;
                int seam = (row % 2 == 0 ? 0 : w / 2) + random.Next(-10, 10);
                for (int y = row * h / rows; y < (row + 1) * h / rows; y++)
                for (int x = 0; x < w; x++)
                {
                    float grain = 0.04f * Mathf.Sin(x * 0.21f + y * 1.7f + row * 3f) + 0.03f * Mathf.Sin(x * 0.05f + row);
                    float v = tone + grain;
                    bool edge = y == row * h / rows || ((x - seam + w) % w) == 0;
                    if (edge) v *= 0.72f;
                    pixels[y * w + x] = new Color(v, v * 0.98f, v * 0.94f, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>
        /// A coral and sunny stripe round the back wall, like a beach hut. With a gap (from gapWest
        /// to gapEast), any stripe lower than gapTop leaves it out, for a tank set into the wall.
        /// </summary>
        static void Stripes(Transform parent, float centerX, float width, float halfD, float gapWest = 0f, float gapEast = 0f, float gapTop = 0f)
        {
            Stripe(parent, centerX, width, halfD, 2.35f, 0.22f, StripeColor, gapWest, gapEast, gapTop);
            Stripe(parent, centerX, width, halfD, 2.58f, 0.08f, StripeColor2, gapWest, gapEast, gapTop);
        }

        static void Stripe(Transform parent, float centerX, float width, float halfD, float y, float height, Color color, float gapWest, float gapEast, float gapTop)
        {
            float west = centerX - width * 0.5f, east = centerX + width * 0.5f;
            if (gapEast <= gapWest || y - height * 0.5f >= gapTop)
            {
                Wall(parent, "Stripe", new Vector3(centerX, y, halfD - 0.01f), new Vector3(width, height, 0.02f), color);
                return;
            }
            if (gapWest > west) Wall(parent, "Stripe", new Vector3((west + gapWest) * 0.5f, y, halfD - 0.01f), new Vector3(gapWest - west, height, 0.02f), color);
            if (gapEast < east) Wall(parent, "Stripe", new Vector3((gapEast + east) * 0.5f, y, halfD - 0.01f), new Vector3(east - gapEast, height, 0.02f), color);
        }

        /// <summary>A potted palm: terracotta pot, a gently curved trunk and drooping fronds.</summary>
        static void Palm(Transform parent, Vector3 floorPoint)
        {
            var palm = new GameObject("Palm").transform;
            palm.SetParent(parent, false);
            palm.position = floorPoint;
            MaterialFactory.Decor(PrimitiveType.Cylinder, "Pot", palm, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.25f, 0.55f), new Color(0.82f, 0.45f, 0.3f));
            MaterialFactory.Decor(PrimitiveType.Cylinder, "Soil", palm, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.01f, 0.5f), new Color(0.32f, 0.22f, 0.15f));
            var trunk = new Color(0.55f, 0.4f, 0.26f);
            var top = new Vector3(0f, 0.5f, 0f);
            for (int i = 0; i < 6; i++)
            {
                var segment = MaterialFactory.Decor(PrimitiveType.Cylinder, "Trunk", palm, top + new Vector3(0.025f * i, 0.16f, 0f), new Vector3(0.14f - i * 0.01f, 0.17f, 0.14f - i * 0.01f), i % 2 == 0 ? trunk : trunk * 0.9f);
                segment.transform.localRotation = Quaternion.Euler(0f, 0f, -4f * i);
                top += new Vector3(0.025f * i, 0.32f, 0f);
            }
            for (int i = 0; i < 7; i++)
            {
                float yaw = i * 360f / 7f + 10f;
                var frond = MaterialFactory.Decor(PrimitiveType.Sphere, "Frond", palm, top, new Vector3(0.2f, 0.04f, 0.95f), Color.Lerp(new Color(0.2f, 0.62f, 0.3f), new Color(0.42f, 0.8f, 0.35f), i % 3 / 2f));
                frond.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(22f, 0f, 0f);
                frond.transform.localPosition = top + frond.transform.localRotation * new Vector3(0f, 0f, 0.42f);
            }
        }

        /// <summary>A surfboard hung flat on the wall.</summary>
        static void Surfboard(Transform parent, Vector3 position, Color color)
        {
            var board = MaterialFactory.Decor(PrimitiveType.Sphere, "Surfboard", parent, position, new Vector3(2f, 0.42f, 0.06f), color);
            MaterialFactory.Decor(PrimitiveType.Cube, "Surfboard", board.transform, new Vector3(0f, 0f, -0.55f), new Vector3(0.9f, 0.08f, 0.5f), Color.white);
        }
    }
}
