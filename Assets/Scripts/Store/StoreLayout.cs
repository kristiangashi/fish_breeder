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
    /// sections are bought with money and each adds room for more tanks.
    /// Tanks always face -Z (the entrance side) because Tank bounds are axis aligned.
    /// </summary>
    public class StoreLayout : MonoBehaviour
    {
        public const float SectionWidth = 7f;
        public const float SectionDepth = 7f;
        public const float WallHeight = 3f;
        public const float StandHeight = 0.85f;
        const float WallThickness = 0.2f;
        const float FrontWallHeight = 1f;
        const float EntranceWidth = 1.8f;

        static readonly float[] SlotColumns = { -2.2f, 0f, 2.2f };
        static readonly float[] SlotRows = { 2.45f, 0f };

        static readonly Color WallColor = new Color(0.62f, 0.62f, 0.64f);
        static readonly Color FloorColor = new Color(0.74f, 0.74f, 0.75f);
        static readonly Color LockedFloorColor = new Color(0.36f, 0.36f, 0.38f);
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
            public Section section;
            public Vector3 position; // sand level of a tank placed here
            public StoreTank tank;
            public GameObject emptyMarker;
        }

        readonly List<Section> sections = new List<Section>();
        readonly List<Slot> slots = new List<Slot>();
        readonly List<StoreTank> tanks = new List<StoreTank>();
        Transform tankRoot;

        public IReadOnlyList<Section> Sections => sections;
        public IReadOnlyList<Slot> Slots => slots;
        public IReadOnlyList<StoreTank> Tanks => tanks;
        public Vector3 RegisterPoint { get; private set; }
        public Vector3 ComputerPoint { get; private set; }
        public Vector3 EntrancePoint => new Vector3(0f, 0.1f, -SectionDepth * 0.5f - 0.6f);
        /// <summary>Where customers appear and disappear, out on the pavement.</summary>
        public Vector3 DoorOutside => new Vector3(0f, 0f, -SectionDepth * 0.5f - 2.6f);
        public Vector3 DoorInside => new Vector3(0f, 0f, -SectionDepth * 0.5f + 0.8f);
        public Vector3 ShopkeeperHand { get; private set; }

        /// <summary>Where a customer stands to look into a tank.</summary>
        public static Vector3 ViewingSpot(StoreTank tank)
        {
            var p = tank.transform.position;
            return new Vector3(p.x, 0f, p.z - 0.95f);
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
            foreach (var section in sections)
                foreach (var row in SlotRows)
                    foreach (var column in SlotColumns)
                        slots.Add(new Slot { section = section, position = new Vector3(section.CenterX + column, StandHeight, row) });

            BuildShell();
            BuildRegister();
            BuildComputer();
            RefreshEmptySlots();

            // One of each to start, along the back wall: breeder, display in the middle, for sale.
            PlaceTank(slots[0], TankRole.Breeder);
            PlaceTank(slots[1], TankRole.Display);
            PlaceTank(slots[2], TankRole.ForSale);
        }

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

        public bool TryUnlock(Section section)
        {
            if (section == null || section.unlocked) return false;
            if (section != NextLockedSection)
            {
                GameLog.Add($"Unlock the {NextLockedSection.name} first.");
                return false;
            }
            if (!Economy.TrySpend(section.price, $"unlocking the {section.name}")) return false;
            section.unlocked = true;
            BuildShell();
            RefreshEmptySlots();
            GameLog.Add($"The {section.name} is open! Click an empty spot to buy a tank for it.");
            return true;
        }

        public bool CanBuyTank(Slot slot, TankRole role, out string reason)
        {
            reason = null;
            if (slot.tank != null) reason = "There is already a tank here.";
            else if (!slot.section.unlocked) reason = "This part of the store is locked.";
            else if (CountTanks(role) >= SupplierCatalog.TankLimit(role)) reason = $"The store can't hold more {StoreTank.RoleName(role).ToLower()} tanks.";
            else if (!Economy.CanAfford(SupplierCatalog.TankPrice(role))) reason = "Not enough money.";
            return reason == null;
        }

        public StoreTank TryBuyTank(Slot slot, TankRole role)
        {
            if (!CanBuyTank(slot, role, out var reason))
            {
                GameLog.Add(reason);
                return null;
            }
            if (!Economy.TrySpend(SupplierCatalog.TankPrice(role), $"a {StoreTank.RoleName(role).ToLower()} tank")) return null;
            var tank = PlaceTank(slot, role);
            GameLog.Add($"Your new {tank.Title.ToLower()} is set up and ready for fish.");
            return tank;
        }

        StoreTank PlaceTank(Slot slot, TankRole role)
        {
            var tank = StoreTank.Create(role, CountTanks(role) + 1, slot.position, tankRoot);
            slot.tank = tank;
            tanks.Add(tank);

            var p = slot.position;
            MaterialFactory.Decor(PrimitiveType.Cube, "Stand", tank.transform, new Vector3(0f, -StandHeight * 0.5f, 0f),
                new Vector3(1.72f, StandHeight, 0.82f), RoleColor(role));
            var click = StoreClickable.Add(tank.transform, tank.Title, p + new Vector3(0f, 0.05f, 0f), new Vector3(1.76f, StandHeight * 2f + 0.2f, 0.86f), StoreClickable.Kind.Tank);
            click.tank = tank;
            click.slot = slot;
            RefreshEmptySlots();
            return tank;
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
                bool wantMarker = slot.section.unlocked && slot.tank == null;
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
                    Wall(root, "SideWall", new Vector3(cx - halfW - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
                if (section.index == lastOpen)
                    Wall(root, "SideWall", new Vector3(cx + halfW + t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, SectionDepth + t * 2f), WallColor);
            }
        }

        void BuildRegister()
        {
            var root = new GameObject("Register").transform;
            root.SetParent(transform, false);
            var counter = new Vector3(2.1f, 0f, -2.4f);
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
            var apron = new Color(0.2f, 0.45f, 0.6f);
            MaterialFactory.Decor(PrimitiveType.Cube, "Legs", keeper, new Vector3(0f, 0.42f, 0f), new Vector3(0.32f, 0.84f, 0.2f), new Color(0.25f, 0.25f, 0.3f));
            MaterialFactory.Decor(PrimitiveType.Capsule, "Torso", keeper, new Vector3(0f, 1.17f, 0f), new Vector3(0.44f, 0.36f, 0.28f), apron);
            MaterialFactory.Decor(PrimitiveType.Sphere, "Head", keeper, new Vector3(0f, 1.62f, 0f), new Vector3(0.24f, 0.27f, 0.24f), new Color(0.9f, 0.75f, 0.6f));
            ShopkeeperHand = keeper.position + new Vector3(0f, 1.1f, 0.3f);
            StoreClickable.Add(root, "Register", counter + new Vector3(0f, 0.75f, 0f), new Vector3(2.1f, 1.5f, 0.7f), StoreClickable.Kind.Register);
        }

        void BuildComputer()
        {
            var root = new GameObject("Supplier computer").transform;
            root.SetParent(transform, false);
            var desk = new Vector3(-2.2f, 0f, -2.4f);
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
            sources.Add(StoreNavMesh.Box(new Vector3(0f, -0.05f, -SectionDepth * 0.5f - 1.6f), new Vector3(5f, 0.1f, 3.4f), true));

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (NotObstacles.Contains(renderer.gameObject.name)) continue;
                var b = renderer.bounds;
                if (b.max.y < 0.02f) continue;
                sources.Add(StoreNavMesh.Box(b.center, b.size + new Vector3(0.02f, 0f, 0.02f), false));
            }

            float width = sections.Count * SectionWidth + 4f;
            var bounds = new Bounds(new Vector3((sections.Count - 1) * SectionWidth * 0.5f, 1f, -2f), new Vector3(width, 4f, SectionDepth + 10f));
            StoreNavMesh.Build(sources, bounds);
        }

        static GameObject Wall(Transform parent, string name, Vector3 position, Vector3 size, Color color)
            => MaterialFactory.Decor(PrimitiveType.Cube, name, parent, position, size, color);
    }
}
