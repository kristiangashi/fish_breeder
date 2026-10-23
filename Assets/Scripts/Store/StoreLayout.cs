using System.Collections.Generic;
using FishBreeder.Core;
using FishBreeder.Shop;
using UnityEngine;
using UnityEngine.AI;

namespace FishBreeder.Store
{
    /// <summary>
    /// The shop building: plain gray placeholder rooms in a row. The main floor (with the one
    /// entrance, the register and the supplier computer) is open from the start; the other
    /// sections are bought with money and each adds room for more display and for sale tanks.
    /// Breeder and quarantine tanks live in the back room to the west, which customers never see.
    /// Every spot has room for the biggest tank size, so tanks can be upgraded where they stand.
    /// Tanks always face -Z (the entrance side) because Tank bounds are axis aligned.
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
        static readonly float[] SlotRows = { 3.4f, 0.2f };
        static readonly float[] BackRoomColumns = { -2.2f, 2.2f };

        /// <summary>Centre of the back room, west of the main floor.</summary>
        public static float BackRoomCenterX => -SectionWidth * 0.5f - WallThickness - BackRoomWidth * 0.5f;

        static readonly Color WallColor = new Color(0.62f, 0.62f, 0.64f);
        static readonly Color FloorColor = new Color(0.74f, 0.74f, 0.75f);
        static readonly Color LockedFloorColor = new Color(0.36f, 0.36f, 0.38f);
        static readonly Color BackRoomFloorColor = new Color(0.66f, 0.64f, 0.6f);
        static readonly Color CounterColor = new Color(0.5f, 0.5f, 0.52f);
        static readonly Color DarkColor = new Color(0.16f, 0.16f, 0.18f);

        public class Section
        {
            public int index;
            public string name;
            public int price;
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
        public Vector3 EntrancePoint => new Vector3(0f, 0.1f, -SectionDepth * 0.5f - 0.6f);
        /// <summary>Where customers appear and disappear, out on the pavement.</summary>
        public Vector3 DoorOutside => new Vector3(0f, 0f, -SectionDepth * 0.5f - 3f);
        public Vector3 DoorInside => new Vector3(0f, 0f, -SectionDepth * 0.5f + 0.8f);
        public Vector3 ShopkeeperHand { get; private set; }

        /// <summary>Where a customer stands to look into a tank.</summary>
        public static Vector3 ViewingSpot(StoreTank tank)
        {
            var p = tank.transform.position;
            return new Vector3(p.x, 0f, p.z - tank.Tank.InnerSize.z * 0.5f - 0.75f);
        }

        /// <summary>The register queue runs left along the front of the counter; place 0 is being served.</summary>
        public Vector3 RegisterQueueSpot(int place) => registerFront + Vector3.left * (0.75f * place);

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
            foreach (var row in SlotRows)
                foreach (var column in BackRoomColumns)
                    slots.Add(new Slot { backRoom = true, position = new Vector3(BackRoomCenterX + column, StandHeight, row) });
            foreach (var section in sections)
                foreach (var row in SlotRows)
                    foreach (var column in SlotColumns)
                        slots.Add(new Slot { section = section, position = new Vector3(section.CenterX + column, StandHeight, row) });

            BuildShell();
            BuildBackRoom();
            BuildRegister();
            BuildComputer();
            RefreshEmptySlots();

            // One of each to start: a breeder in the back room, and a for sale and a display tank
            // along the shop's back wall.
            var starter = Balance.Data.store.starterDecorations;
            PlaceTank(FirstFreeSlot(TankRole.Breeder), TankRole.Breeder, starter);
            PlaceTank(slots.Find(s => !s.backRoom && s.section.index == 0 && s.tank == null), TankRole.ForSale, starter);
            PlaceTank(FirstFreeSlot(TankRole.Display), TankRole.Display, starter);
        }

        Slot FirstFreeSlot(TankRole role) => slots.Find(s => s.tank == null && s.IsOpen && s.Allows(role));

        void AddSection(string name, int price, bool unlocked)
        {
            sections.Add(new Section { index = sections.Count, name = name, price = price, unlocked = unlocked });
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
            if (!Economy.TrySpend(section.price, $"unlocking the {section.name}")) return false;
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

        /// <summary>Why a tank can't be upgraded to this size right now, or null if it can.</summary>
        public static string WhyCantUpgrade(StoreTank tank, int size)
        {
            var sizes = Balance.Data.store.tankSizes;
            if (size <= tank.Tank.SizeIndex || size >= sizes.Count) return "It's already this size or bigger.";
            var info = sizes[size];
            if (!Progression.IsUnlocked(XpTrack.Fishkeeping, info.unlockLevel)) return $"Needs {Progression.LockText(XpTrack.Fishkeeping, info.unlockLevel)}.";
            if (!Economy.CanAfford(info.upgradePrice)) return "Not enough money.";
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
            if (!Economy.TrySpend(info.upgradePrice, $"upgrading {tank.Title} to a {info.name.ToLower()} tank")) return false;
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
            var tank = StoreTank.Create(role, CountTanks(role) + 1, slot.position, tankRoot, 0, decorations);
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
            MaterialFactory.Decor(PrimitiveType.Cube, "Stand", stand.transform, new Vector3(0f, -StandHeight * 0.5f, 0f),
                new Vector3(size.x + 0.12f, StandHeight, size.z + 0.12f), RoleColor(tank.Role));
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
                    var marker = MaterialFactory.Decor(PrimitiveType.Cube, "Empty tank spot", transform, floorPoint + Vector3.up * 0.01f,
                        new Vector3(1.72f, 0.02f, 0.82f), new Color(0.52f, 0.52f, 0.54f));
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

                Wall(root, "Floor", new Vector3(cx, -0.05f, 0f), new Vector3(SectionWidth, 0.1f, SectionDepth), section.unlocked ? FloorColor : LockedFloorColor);

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
            Wall(root, "Floor", new Vector3(cx, -0.05f, 0f), new Vector3(BackRoomWidth + t * 2f, 0.1f, SectionDepth), BackRoomFloorColor);
            Wall(root, "BackWall", new Vector3(cx, WallHeight * 0.5f, halfD + t * 0.5f), new Vector3(BackRoomWidth + t * 2f, WallHeight, t), WallColor);
            Wall(root, "SideWall", new Vector3(cx - halfW - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
            Wall(root, "FrontWall", new Vector3(cx, FrontWallHeight * 0.5f, -halfD - t * 0.5f), new Vector3(BackRoomWidth + t * 2f, FrontWallHeight, t), WallColor);
            // A work bench along the front wall, to make it look like a back room.
            Wall(root, "Bench", new Vector3(cx, 0.45f, -halfD + 0.45f), new Vector3(BackRoomWidth * 0.6f, 0.9f, 0.6f), CounterColor);
            Wall(root, "Buckets", new Vector3(cx - BackRoomWidth * 0.2f, 1.05f, -halfD + 0.45f), new Vector3(0.3f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.75f));
        }

        public Vector3 BackRoomLabelPoint => new Vector3(BackRoomCenterX, 2.2f, -2.4f);

        void BuildRegister()
        {
            var root = new GameObject("Register").transform;
            root.SetParent(transform, false);
            var counter = new Vector3(3.6f, 0f, -3.4f);
            Wall(root, "Counter", counter + new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 0.6f), CounterColor);
            Wall(root, "CounterTop", counter + new Vector3(0f, 1.02f, 0f), new Vector3(2.08f, 0.04f, 0.66f), WallColor);
            Wall(root, "Till", counter + new Vector3(0.45f, 1.16f, 0.05f), new Vector3(0.44f, 0.24f, 0.38f), DarkColor);
            Wall(root, "TillDrawer", counter + new Vector3(0.45f, 1.08f, -0.15f), new Vector3(0.4f, 0.08f, 0.06f), new Color(0.3f, 0.3f, 0.32f));
            var display = Wall(root, "TillDisplay", counter + new Vector3(0.45f, 1.38f, 0.12f), new Vector3(0.3f, 0.16f, 0.03f), new Color(0.3f, 0.75f, 0.45f));
            display.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            RegisterPoint = counter + new Vector3(0.45f, 1.6f, 0f);
            registerFront = new Vector3(counter.x + 0.45f, 0f, counter.z + 0.75f);

            // The shopkeeper (you) behind the counter.
            var keeper = new GameObject("Shopkeeper").transform;
            keeper.SetParent(root, false);
            keeper.position = new Vector3(counter.x + 0.45f, 0f, counter.z - 0.62f);
            keeper.localScale = Vector3.one * Customer.PersonScale;
            var apron = new Color(0.2f, 0.45f, 0.6f);
            MaterialFactory.Decor(PrimitiveType.Cube, "Legs", keeper, new Vector3(0f, 0.42f, 0f), new Vector3(0.32f, 0.84f, 0.2f), new Color(0.25f, 0.25f, 0.3f));
            MaterialFactory.Decor(PrimitiveType.Capsule, "Torso", keeper, new Vector3(0f, 1.17f, 0f), new Vector3(0.44f, 0.36f, 0.28f), apron);
            MaterialFactory.Decor(PrimitiveType.Sphere, "Head", keeper, new Vector3(0f, 1.62f, 0f), new Vector3(0.24f, 0.27f, 0.24f), new Color(0.9f, 0.75f, 0.6f));
            ShopkeeperHand = keeper.position + new Vector3(0f, 1.1f * Customer.PersonScale, 0.3f);
            StoreClickable.Add(root, "Register", counter + new Vector3(0f, 0.75f, 0f), new Vector3(2.1f, 1.5f, 0.7f), StoreClickable.Kind.Register);
        }

        void BuildComputer()
        {
            var root = new GameObject("Supplier computer").transform;
            root.SetParent(transform, false);
            var desk = new Vector3(-3.6f, 0f, -3.4f);
            Wall(root, "Desk", desk + new Vector3(0f, 0.72f, 0f), new Vector3(1.4f, 0.06f, 0.7f), CounterColor);
            for (int sx = -1; sx <= 1; sx += 2)
                Wall(root, "DeskLeg", desk + new Vector3(sx * 0.62f, 0.35f, 0f), new Vector3(0.06f, 0.7f, 0.62f), CounterColor);
            Wall(root, "MonitorStand", desk + new Vector3(0f, 0.84f, 0.18f), new Vector3(0.08f, 0.18f, 0.08f), DarkColor);
            Wall(root, "Monitor", desk + new Vector3(0f, 1.1f, 0.18f), new Vector3(0.62f, 0.4f, 0.05f), DarkColor);
            Wall(root, "Screen", desk + new Vector3(0f, 1.1f, 0.15f), new Vector3(0.56f, 0.34f, 0.01f), new Color(0.35f, 0.6f, 0.9f));
            Wall(root, "Keyboard", desk + new Vector3(0f, 0.76f, -0.12f), new Vector3(0.45f, 0.02f, 0.15f), DarkColor);
            Wall(root, "Chair", desk + new Vector3(0f, 0.25f, -0.65f), new Vector3(0.5f, 0.5f, 0.5f), WallColor);
            ComputerPoint = desk + new Vector3(0f, 1.45f, 0.18f);
            StoreClickable.Add(root, "Supplier computer", desk + new Vector3(0f, 0.7f, -0.1f), new Vector3(1.45f, 1.4f, 1.3f), StoreClickable.Kind.Computer);
        }

        static readonly HashSet<string> NotObstacles = new HashSet<string> { "Floor", "DoorMat", "Empty tank spot", "Water", "Caustics", "Bubble" };

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
    }
}
