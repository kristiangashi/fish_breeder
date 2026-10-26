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
    /// floors, a bamboo counter, palms and surfboards). The main floor (with the one entrance, the
    /// register and the supplier computer) is open from the start; the other sections are bought
    /// with money and each adds room for more display and for sale tanks. Tanks stand round the
    /// walls, facing into the room, which leaves the middle free to walk about. Breeder and
    /// quarantine tanks live in the back room to the west, which customers never see. The fish
    /// seller parks his van on the pavement outside, west of the door. Every spot has room for the
    /// biggest tank size, so tanks can be upgraded where they stand.
    /// </summary>
    public class StoreLayout : MonoBehaviour
    {
        public const float SectionWidth = 12f;
        public const float SectionDepth = 10f;
        public const float WallHeight = 4f;
        public const float StandHeight = 0.85f;
        public const float BackRoomWidth = 9f;
        const float WallThickness = 0.2f;
        const float FrontWallHeight = 1.1f;
        const float EntranceWidth = 2.2f;
        const float StaffDoorWidth = 1.6f;

        static readonly float[] SlotColumns = { -4f, 0f, 4f };
        // Far enough off the back wall for the biggest tank to clear the palms in the corners.
        const float BackSlotZ = 3.7f;
        const float FrontSlotZ = -4.3f;
        const float WestSlotX = -SectionWidth * 0.5f + 0.7f;
        static readonly float[] WestSlotZ = { 2.05f, -1.15f };
        static readonly float[] BackRoomRows = { 3.4f, 0.2f };
        static readonly float[] BackRoomColumns = { -2.2f, 2.2f };

        /// <summary>Centre of the back room, west of the main floor.</summary>
        public static float BackRoomCenterX => -SectionWidth * 0.5f - WallThickness - BackRoomWidth * 0.5f;

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

            public bool IsOpen => backRoom || section.unlocked;

            /// <summary>Breeder and quarantine tanks go in the back room, display and for sale tanks on the shop floor.</summary>
            public bool Allows(TankRole role) => backRoom == (role == TankRole.Breeder || role == TankRole.Quarantine);
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
            for (int i = 0; i < sectionBalance.Count; i++)
                AddSection(sectionBalance[i].name, sectionBalance[i].price, i == 0);

            tankRoot = new GameObject("Tanks").transform;
            tankRoot.SetParent(transform, false);
            foreach (var row in BackRoomRows)
                foreach (var column in BackRoomColumns)
                    slots.Add(new Slot { backRoom = true, position = new Vector3(BackRoomCenterX + column, StandHeight, row) });
            // Round the walls in walking order. The main floor: up the west wall (its front is the
            // staff door and the computer), then along the back. The others: along the back wall,
            // then back along the front wall facing in. Their east side stays open into the next one.
            foreach (var section in sections)
            {
                float cx = section.CenterX;
                if (section.index == 0)
                    for (int i = WestSlotZ.Length - 1; i >= 0; i--)
                        slots.Add(new Slot { section = section, position = new Vector3(cx + WestSlotX, StandHeight, WestSlotZ[i]), yaw = -90f });
                foreach (var column in SlotColumns)
                    slots.Add(new Slot { section = section, position = new Vector3(cx + column, StandHeight, BackSlotZ) });
                if (section.index > 0)
                    for (int i = SlotColumns.Length - 1; i >= 0; i--)
                        slots.Add(new Slot { section = section, position = new Vector3(cx + SlotColumns[i], StandHeight, FrontSlotZ), yaw = 180f });
            }

            BuildShell();
            BuildBackRoom();
            BuildRegister();
            BuildComputer();
            BuildFishVan();
            RefreshEmptySlots();

            // One of each to start: a breeder in the back room, and a for sale and a display tank
            // along the shop's back wall.
            var starter = Balance.Data.store.starterDecorations;
            PlaceTank(FirstFreeSlot(TankRole.Breeder), TankRole.Breeder, starter);
            PlaceTank(slots.Find(s => !s.backRoom && s.section.index == 0 && s.yaw == 0f && s.tank == null), TankRole.ForSale, starter);
            PlaceTank(slots.Find(s => !s.backRoom && s.section.index == 0 && s.yaw == 0f && s.tank == null), TankRole.Display, starter);
        }

        /// <summary>Where a slot sits in walking order (back room first), for flicking between tanks.</summary>
        public int SlotOrder(Slot slot) => slot == null ? int.MaxValue : slots.IndexOf(slot);

        Slot FirstFreeSlot(TankRole role) => slots.Find(s => s.tank == null && s.IsOpen && s.Allows(role));

        void AddSection(string name, int price, bool unlocked)
        {
            sections.Add(new Section { index = sections.Count, name = name, basePrice = price, unlocked = unlocked });
        }

        public int CountTanks(TankRole role)
        {
            int count = 0;
            foreach (var tank in tanks) if (tank.Role == role) count++;
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

        public bool TryUnlock(Section section)
        {
            if (section == null || section.unlocked) return false;
            if (section != NextLockedSection)
            {
                GameLog.Add($"Unlock the {NextLockedSection.name} first.");
                return false;
            }
            if (!Progression.IsUnlocked(XpTrack.Shopkeeping, SectionLevel(section)))
            {
                GameLog.Add($"The {section.name} needs {Progression.LockText(XpTrack.Shopkeeping, SectionLevel(section))}.");
                return false;
            }
            if (!Economy.TrySpend(section.Price, $"unlocking the {section.name}")) return false;
            section.unlocked = true;
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpUnlockSection);
            BuildShell();
            RefreshEmptySlots();
            GameLog.Add($"The {section.name} is open! Click an empty spot to buy a tank for it.");
            return true;
        }

        public bool CanBuyTank(Slot slot, TankRole role, out string reason)
        {
            reason = null;
            SupplierCatalog.TankLevel(role, out var track, out var level);
            if (slot.tank != null) reason = "There is already a tank here.";
            else if (!slot.IsOpen) reason = "This part of the store is locked.";
            else if (!slot.Allows(role)) reason = slot.backRoom ? "Only breeder and quarantine tanks go in the back room." : "Breeder and quarantine tanks go in the back room.";
            else if (CountTanks(role) >= SupplierCatalog.TankLimit(role)) reason = $"The store can't hold more {StoreTank.RoleName(role).ToLower()} tanks.";
            else if (!Progression.IsUnlocked(track, level)) reason = $"Needs {Progression.LockText(track, level)}.";
            else if (!Economy.CanAfford(SupplierCatalog.TankPrice(role))) reason = "Not enough money.";
            return reason == null;
        }

        public const string NoMoney = "Not enough money.";

        /// <summary>Why a tank can't be upgraded to this size right now (<see cref="NoMoney"/> last), or null if it can.</summary>
        public static string WhyCantUpgrade(StoreTank tank, int size)
        {
            var sizes = Balance.Data.store.tankSizes;
            if (size <= tank.Tank.SizeIndex || size >= sizes.Count) return "It's already this size or bigger.";
            var info = sizes[size];
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel)) return $"Needs {Progression.LockText(XpTrack.Fishkeeping, info.unlockLevel)}.";
            if (tank.Tank.Quality < info.qualityNeeded) return $"Needs aquarium quality {info.qualityNeeded} (it's {tank.Tank.Quality}): add better equipment and more decorations.";
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
            var info = Balance.Data.store.tankSizes[size];
            if (!Economy.TrySpend(Perks.Upgrade(info.upgradePrice), $"upgrading {tank.Title} to a {info.name.ToLower()} tank")) return false;
            tank.Tank.Resize(size);
            BuildStand(tank);
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpTankUpgrade);
            GameLog.Add($"{tank.Title} is now a {info.name.ToLower()} tank: room for {tank.Tank.Capacity} fish.");
            return true;
        }

        public StoreTank TryBuyTank(Slot slot, TankRole role)
        {
            if (!CanBuyTank(slot, role, out var reason))
            {
                GameLog.Add(reason);
                return null;
            }
            if (!Economy.TrySpend(SupplierCatalog.TankPrice(role), $"a {StoreTank.RoleName(role).ToLower()} tank")) return null;
            var tank = PlaceTank(slot, role, null);
            Progression.Award(XpTrack.Shopkeeping, Balance.Data.progression.xpBuyTank);
            GameLog.Add($"Your new {tank.Title.ToLower()} is set up and ready for fish.");
            return tank;
        }

        StoreTank PlaceTank(Slot slot, TankRole role, IEnumerable<string> decorations)
        {
            var tank = StoreTank.Create(role, CountTanks(role) + 1, slot.position, slot.yaw, tankRoot, 0, decorations);
            slot.tank = tank;
            tank.Slot = slot;
            tanks.Add(tank);
            if (!slot.backRoom) publicTanks.Add(tank);
            BuildStand(tank);
            RefreshEmptySlots();
            return tank;
        }

        /// <summary>The stand under a tank and its click volume, sized to the tank (rebuilt after an upgrade).</summary>
        void BuildStand(StoreTank tank)
        {
            if (tank.Stand != null) Destroy(tank.Stand);
            var size = tank.Tank.InnerSize;
            var stand = new GameObject("Stand and click");
            stand.transform.SetParent(tank.transform, false);
            // The stand stops just under the tank's base, so the two never share a surface (which flickers).
            float top = -Tank.BaseDepth;
            MaterialFactory.Decor(PrimitiveType.Cube, "Stand", stand.transform, new Vector3(0f, (top - StandHeight) * 0.5f, 0f),
                new Vector3(size.x + 0.12f, StandHeight + top, size.z + 0.12f), RoleColor(tank.Role));
            var click = StoreClickable.Add(stand.transform, tank.Title, tank.transform.position + new Vector3(0f, size.y * 0.5f - StandHeight * 0.5f, 0f),
                new Vector3(size.x + 0.16f, size.y + StandHeight + 0.1f, size.z + 0.16f), StoreClickable.Kind.Tank);
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
                    MaterialFactory.Decor(PrimitiveType.Cube, "Empty tank spot", marker.transform, Vector3.up * 0.01f,
                        new Vector3(1.72f, 0.02f, 0.82f), new Color(0.45f, 0.74f, 0.74f));
                    var click = StoreClickable.Add(marker.transform, "Empty spot", floorPoint + Vector3.up * 0.3f, new Vector3(1.72f, 0.6f, 0.82f), StoreClickable.Kind.EmptySlot);
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

        /// <summary>Floors and walls. Rebuilt whenever a section unlocks so the outer wall moves out.</summary>
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
                float cx = section.CenterX;

                if (section.unlocked) Floor(root, new Vector3(cx, -0.05f, 0f), new Vector3(SectionWidth, 0.1f, SectionDepth), FloorColor);
                else Wall(root, "Floor", new Vector3(cx, -0.05f, 0f), new Vector3(SectionWidth, 0.1f, SectionDepth), LockedFloorColor);

                if (!section.unlocked)
                {
                    // Low dark curbs outline the space that can be bought.
                    Wall(root, "Curb", new Vector3(cx, 0.1f, halfD), new Vector3(SectionWidth, 0.2f, t), DarkColor);
                    Wall(root, "Curb", new Vector3(cx, 0.1f, -halfD), new Vector3(SectionWidth, 0.2f, t), DarkColor);
                    if (section.index == sections.Count - 1)
                        Wall(root, "Curb", new Vector3(cx + halfW, 0.1f, 0f), new Vector3(t, 0.2f, SectionDepth), DarkColor);
                    var click = StoreClickable.Add(root, section.name, new Vector3(cx, 0.5f, 0f), new Vector3(SectionWidth, 1f, SectionDepth), StoreClickable.Kind.LockedSection);
                    click.section = section;
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
                }
                else
                {
                    Wall(root, "FrontWall", new Vector3(cx, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(SectionWidth + t, FrontWallHeight, t), WallColor);
                    // Pillars where two open sections meet.
                    Wall(root, "Pillar", new Vector3(cx - halfW, WallHeight * 0.5f, halfD - 0.15f), new Vector3(0.3f, WallHeight, 0.3f), CounterColor);
                    Wall(root, "Pillar", new Vector3(cx - halfW, FrontWallHeight * 0.5f + 0.1f, -halfD + 0.15f), new Vector3(0.3f, FrontWallHeight + 0.2f, 0.3f), CounterColor);
                }

                if (section.index == 0)
                {
                    // The west wall has a staff door through to the back room, near the front.
                    float doorZ = -halfD + 1.6f;
                    float backLength = halfD + t - (doorZ + StaffDoorWidth * 0.5f);
                    float frontLength = (doorZ - StaffDoorWidth * 0.5f) - (-halfD - t);
                    float wallX = cx - halfW - t * 0.5f;
                    Wall(root, "SideWall", new Vector3(wallX, WallHeight * 0.5f, halfD + t - backLength * 0.5f), new Vector3(t, WallHeight, backLength), WallColor);
                    Wall(root, "SideWall", new Vector3(wallX, FrontWallHeight * 0.5f, -halfD - t + frontLength * 0.5f), new Vector3(t, FrontWallHeight, frontLength), WallColor);
                    Wall(root, "StaffDoorLintel", new Vector3(wallX, 2.5f, doorZ), new Vector3(t * 1.4f, 0.15f, StaffDoorWidth + t), CounterColor);
                }
                if (section.index == lastOpen)
                    Wall(root, "SideWall", new Vector3(cx + halfW + t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
            }
        }

        /// <summary>Staff only: where the breeder and quarantine tanks live, out of the customers' sight.</summary>
        void BuildBackRoom()
        {
            var root = new GameObject("Back room").transform;
            root.SetParent(transform, false);
            float cx = BackRoomCenterX, halfW = BackRoomWidth * 0.5f, halfD = SectionDepth * 0.5f, t = WallThickness;
            Floor(root, new Vector3(cx, -0.05f, 0f), new Vector3(BackRoomWidth + t * 2f, 0.1f, SectionDepth), BackRoomFloorColor);
            Wall(root, "BackWall", new Vector3(cx, WallHeight * 0.5f, halfD + t * 0.5f), new Vector3(BackRoomWidth + t * 2f, WallHeight, t), WallColor);
            Stripes(root, cx, BackRoomWidth, halfD);
            Wall(root, "SideWall", new Vector3(cx - halfW - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
            Wall(root, "FrontWall", new Vector3(cx, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(BackRoomWidth + t * 2f, FrontWallHeight, t), WallColor);
            // A work bench along the front wall, to make it look like a back room.
            Wall(root, "Bench", new Vector3(cx, 0.45f, -halfD + 0.45f), new Vector3(BackRoomWidth * 0.6f, 0.9f, 0.6f), CounterColor);
            Wall(root, "Buckets", new Vector3(cx - BackRoomWidth * 0.2f, 1.05f, -halfD + 0.45f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.75f));
        }

        public Vector3 BackRoomLabelPoint => new Vector3(BackRoomCenterX, 1.6f, -SectionDepth * 0.5f + 0.5f);

        /// <summary>
        /// The counter stands along the east wall by the front, side on to the room: the shopkeeper
        /// behind it faces into the shop, and customers queue up from the front of it back into the room.
        /// </summary>
        void BuildRegister()
        {
            var root = new GameObject("Register").transform;
            root.SetParent(transform, false);
            var counter = new Vector3(4.5f, 0f, -3.1f);
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
            var desk = new Vector3(-2.8f, 0f, -3.6f);
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

        /// <summary>
        /// The pavement along the shop front, and the fish seller's van parked on it west of the
        /// door: a wholesaler's van with its back doors open, a folding table of aerated holding
        /// tanks and bagged fish, foam shipping boxes and a sign on the roof. He stands behind the
        /// table facing the shop (and the camera).
        /// </summary>
        void BuildFishVan()
        {
            float halfD = SectionDepth * 0.5f;
            float west = BackRoomCenterX - BackRoomWidth * 0.5f - WallThickness;
            float east = (sections.Count - 1) * SectionWidth + SectionWidth * 0.5f + WallThickness;
            var outside = new GameObject("Pavement").transform;
            outside.SetParent(transform, false);
            Wall(outside, "Pavement", new Vector3((west + east) * 0.5f, -0.055f, -halfD - 2.3f), new Vector3(east - west, 0.1f, 4.2f), new Color(0.8f, 0.77f, 0.7f));
            Wall(outside, "Pavement", new Vector3((west + east) * 0.5f, -0.06f, -halfD - 6.4f), new Vector3(east - west, 0.1f, 4f), new Color(0.32f, 0.34f, 0.36f));

            var root = new GameObject("Fish seller").transform;
            root.SetParent(transform, false);
            var van = new Vector3(-5.4f, 0f, -6.6f);
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
                var open = new Vector3(0.8f, 0f, sz * 0.6f).normalized;
                var door = Wall(root, "VanDoor", hinge + open * 0.39f + Vector3.up * 1.12f, new Vector3(0.78f, 1.5f, 0.04f), paint);
                door.transform.localRotation = Quaternion.FromToRotation(Vector3.right, open);
            }
            // Foam shipping boxes stacked inside the back and beside the van.
            var foam = new Color(0.96f, 0.96f, 0.93f);
            Wall(root, "FoamBox", van + new Vector3(1.25f, 0.62f, 0.35f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", van + new Vector3(1.25f, 0.62f, -0.2f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", van + new Vector3(1.25f, 0.99f, 0.1f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", van + new Vector3(1.1f, 0.18f, 1.18f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "FoamBox", van + new Vector3(1.1f, 0.55f, 1.18f), new Vector3(0.5f, 0.36f, 0.42f), foam);
            Wall(root, "BoxTape", van + new Vector3(1.1f, 0.735f, 1.18f), new Vector3(0.08f, 0.01f, 0.43f), new Color(0.3f, 0.6f, 0.85f));

            // The folding table: two aerated holding tanks, an air pump and a row of bagged fish.
            var table = new Vector3(-2.75f, 0f, -6.55f);
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
            for (int b = 0; b < 4; b++)
            {
                var bag = table + new Vector3(0.12f + b * 0.15f, 0.86f, -0.18f);
                MaterialFactory.Decor(PrimitiveType.Sphere, "FishBag", root, bag, new Vector3(0.12f, 0.16f, 0.12f), new Color(0.7f, 0.88f, 0.96f));
                MaterialFactory.Decor(PrimitiveType.Capsule, "Fish", root, bag + new Vector3(0f, -0.01f, -0.055f), new Vector3(0.025f, 0.03f, 0.02f), fishColors[b]).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                MaterialFactory.Decor(PrimitiveType.Cylinder, "BagKnot", root, bag + new Vector3(0f, 0.1f, 0f), new Vector3(0.03f, 0.03f, 0.03f), new Color(0.9f, 0.9f, 0.9f));
            }

            // A sign along the roof, facing the street (and the camera).
            var sign = Wall(root, "VanSign", van + new Vector3(0.4f, 2.18f, -0.3f), new Vector3(2.3f, 0.42f, 0.05f), new Color(0.98f, 0.92f, 0.72f));
            Wall(root, "VanSign", van + new Vector3(0.4f, 1.95f, -0.3f), new Vector3(0.06f, 0.08f, 0.06f), DarkColor);
            var text = new GameObject("VanSignText").AddComponent<TextMesh>();
            text.transform.SetParent(sign.transform.parent, false);
            text.transform.position = sign.transform.position + new Vector3(0f, 0f, -0.03f);
            text.text = "TROPICAL FISH WHOLESALE\n<size=34>live fish \u00b7 eggs \u00b7 fry</size>";
            text.richText = true;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.011f;
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
            seller.position = table + new Vector3(0f, 0f, 0.7f);
            seller.rotation = Quaternion.LookRotation(new Vector3(0.25f, 0f, -1f));
            seller.gameObject.AddComponent<FishSeller>();
            FishSellerPoint = seller.position + Vector3.up * 1.85f;
            StoreClickable.Add(root, "Fish seller", new Vector3(-3.1f, 0.9f, -6.3f), new Vector3(2.2f, 1.8f, 1.9f), StoreClickable.Kind.FishSeller);
        }

        // Wall dressing and palm fronds overhead don't block the walkway (the palm's pot and trunk do).
        static readonly HashSet<string> NotObstacles = new HashSet<string> { "Floor", "DoorMat", "Empty tank spot", "Water", "Caustics", "Bubble", "Stripe", "Frond", "Surfboard", "Pavement", "VanSign", "VanSignText" };

        /// <summary>
        /// Bakes where customers can walk: the open sections' floors and the pavement outside,
        /// minus every wall, stand, counter and desk (taken from their renderer bounds).
        /// </summary>
        public void BuildNavMesh()
        {
            var sources = new List<NavMeshBuildSource>();
            foreach (var section in sections)
                if (section.unlocked)
                    sources.Add(StoreNavMesh.Box(new Vector3(section.CenterX, -0.05f, 0f), new Vector3(SectionWidth, 0.1f, SectionDepth), true));
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
            var bounds = new Bounds(new Vector3((sections.Count - 1) * SectionWidth * 0.5f, 1f, -2f), new Vector3(width, 5f, SectionDepth + 10f));
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

        /// <summary>A coral and sunny stripe round the back wall, like a beach hut.</summary>
        static void Stripes(Transform parent, float centerX, float width, float halfD)
        {
            Wall(parent, "Stripe", new Vector3(centerX, 2.35f, halfD - 0.01f), new Vector3(width, 0.22f, 0.02f), StripeColor);
            Wall(parent, "Stripe", new Vector3(centerX, 2.58f, halfD - 0.01f), new Vector3(width, 0.08f, 0.02f), StripeColor2);
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
