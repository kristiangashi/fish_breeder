# Fish Breeder

A Fish Tycoon–style breeding game in Unity with fully 3D fish.

- **Fish never die of old age.** They grow from fry to adult and then live until a chance event gets them.
- **Every meal is a dice roll.** Each pellet a fish eats can make it sick (Aqua Flu, Fin Rot, White Spot, Swim Bladder). Cheap flakes, dirty water, starving fish and low hardiness all raise the odds. Some illnesses spread to nearby fish.
- **Breeding mixes genes.** Every trait has two alleles, one from each parent. Colours and sizes blend; fin shape, tail shape and pattern follow dominant/recessive rules, so rare looks can hide for generations and then pop out.

## Opening the project

1. Install **Unity 6 (6000.0 LTS)** with Unity Hub. Any 6000.0.x patch is fine; Hub may ask to switch versions, say yes.
2. In Unity Hub, **Add > Add project from disk** and pick this folder. First import takes a few minutes.
3. Open any scene (or **File > New Scene**) and press **Play**. `GameBootstrap` builds the store, its three starting tanks, camera, light and HUD automatically.

If Hub refuses to open the folder, create a new **3D (Built-In Render Pipeline)** project called FishBreeder and copy the `Assets` folder from here into it.

## Playing (placeholder HUD)

The game alternates between **closed** and **open**. Time only moves when you open or close the shop, so nothing changes while it stays closed.

**Shop closed** (you start here with $1,000, a tub of flakes and three empty tanks: For Sale and Display on the shop floor, the Breeder tank in the back room to the west, which customers never see):

- **Store view**: A/D, arrow keys or right-drag to pan, scroll to zoom. Everything clickable has a label.
- **Levels**: two tracks up to level 60, shown in the top bar with today's two goals. Fishkeeping XP comes from feeding, wiping, curing, hatching and breeding (more for rarer fish and looks) and unlocks equipment, food, egg boxes, rarer species, tank sizes, breeder and quarantine tanks. Shopkeeping XP comes from sales, happy customers, good reviews and buying things for the shop, and unlocks decorations, more display and for sale tanks and new store sections. Each day has one goal per track worth a big chunk of XP. Higher Fishkeeping makes wipes clean more.
- **Supplier computer** (tabs, each item with a picture: photos of the real decoration and fish models, drawn icons for the rest): egg boxes, decorations, food and equipment. Things above your level show what unlocks them. Egg boxes hold mixed species: **Simple** eggs hatch common fish with a small chance of a rare one; **Rare** eggs hatch a mix with a good chance of rare fish. Every rare egg already in a box makes the next one less likely, so 2 rares in a box is much harder than 1 (the odds are listed). Ultra Rare, Legendary and Ancient boxes are locked for later.
- **Click a tank** to look inside: water quality, algae, oxygen, temperature, pH, salt, pests and equipment, plus feeding, wiping (3 wipes per closed session), treating the water, removing dead fish, its decorations (add from the supplier, remove for half price back), upgrading to a Large or Grand tank, and with a heater, setting the temperature (it shows the range every fish in the tank is happy with).
- **Click a fish** for its status window: health and what is pulling it down (hunger, illness, comfort, water, old age, dead tank mates), its needs, appraised value, price (For Sale tanks), medicine and moving it to another tank. The camera follows it zoomed in; the left/right arrow keys or the < > buttons beside it step through the tank's fish. Esc closes the fish, Esc again leaves the tank.
- **After closing**: the shop lists what needs doing (dead fish, sick fish, dirty tanks, hungry fish) and takes you to each one in turn, moving on by itself once it's dealt with. You can skip, go back or stop and pick it up later from the top bar.
- **Breeding**: any compatible pair (an adult male and female of the same species, healthy, fed, rested, and happy with the water) may breed at each time step, likeliest in a breeder tank; in a breeder tank you can also pair two fish by hand. Livebearers (guppy, platy, black molly) get pregnant and give birth a few time steps later; egg-layers lay a clutch on the sand that hatches a few time steps later. Filthy water kills eggs. The tank panel lists eggs and pregnant fish with how long until they hatch or give birth. Pregnancy length and hatch time are per species in the balance console.
- **Empty tank spots** and **locked sections**: buy more tanks and floor space.

**Open Shop** (top bar or register): a time step runs, then customers come in for one real hour. They wander between tanks, react with thought bubbles (happy, grossed out by bad water, sad about dead fish, angry about prices), sometimes buy a fish from a For Sale tank, pay at the register and go home. The shop closes on its own, another time step runs, and you get the day's report and reviews, which move your reputation and next day's traffic. Developer buttons in the top bar speed the day up (5x, 20x) or skip the rest of it; the keys 1, 2, 3 and K do the same.

Each time step: eggs develop and hatch when due, pregnant fish give birth when due; water quality drops with waste, algae grows (faster with more decorations), pH drifts and pests may appear; fish age and get hungrier; illness progresses, spreads or strikes; compatible pairs may breed (likelier in breeder tanks); and fish in poor health roll for death.

## Balance console

All balancing numbers (species rarity, prices, needs, compatibilities, diseases, customers, time-step chances, health weights, store and supplier prices, egg boxes and their odds, decorations, tank sizes, unlock levels, XP and daily goals, closing notifications) live in one file, `Assets/Resources/Balance/balance.json`. Edit them in the browser: with the project open in Unity, choose **Fish Breeder > Balance Console** (or go to http://localhost:8642). **Save** writes the file and, if the game is playing, applies the new values straight away. The console is an editor-only tool (`Tools/BalanceConsole/index.html`, served by `Assets/Scripts/Editor/BalanceConsoleServer.cs`) and is not part of a build; the JSON file is, so the game ships with whatever you saved. Until you first save, the defaults in code are used. Numbers are kept to 2 decimals. If a save can't reach Unity (for example while it recompiles), the console keeps retrying for a minute and keeps your edits in the browser until one gets through. Species added to the game later join your saved file automatically with their default values.


## Code layout

| Folder | What's there |
| --- | --- |
| `Scripts/Genetics` | `Genes` (allele types and trait enums), `Genome` (a fish's DNA), `Phenotype` (what the DNA looks like in game units, rarity) |
| `Scripts/Breeding` | `BreedingService` (pure Mendelian crossover + mutation), `BreedingSystem` (in-game rules: compatible pairs, chance breeding at time steps, pregnancy and egg timing) |
| `Scripts/Feeding` | `FeedingSystem` drops food; `FoodPellet` sinks, gets eaten or rots |
| `Scripts/Health` | `DiseaseDefinition` (the illness catalogue), `FishHealth` (infection per meal, progression, recovery/death rolls, contagion, medicine) |
| `Scripts/Fish` | `Fish` (hunger, growth, death), `FishSwimmer` (wander + chase food), `FishVisual` (Meshy model or procedural placeholder), `PlaceholderFishMesh` and `FishPatternTexture` (gene-driven 3D placeholder), `FishModelLibrary` (Meshy model lookup) |
| `Scripts/Tank` | `Tank` (bounds, residents, water quality, filter, placeholder glass tank), `EggClutch` (eggs that hatch after a few time steps), `TankDecor` (decorations placed in spots, more in bigger tanks) |
| `Scripts/Store` | `StoreLayout` (rooms, sections, tank spots, register, computer, walkable area), `StoreView` (store camera and windows), `StoreTank` (tank roles), `SupplierCatalog` (prices), `EggBoxes` (egg box rolls and odds) |
| `Scripts/Shop` | `ShopDay` (open-shop day, sales, reviews), `TimeStep` (everything that happens at open and close), `Customer` and `ShopperBrain` (shoppers and their reactions), `MoodIcons`, `StoreNavMesh`, `ToDoList` (problems to sort through after closing) |
| `Scripts/Core` | `GameBootstrap`, `Balance` (all balancing numbers, loaded from the balance file), `Economy` (money, stock, day, reputation), `Progression` (XP, levels, daily goals, unlocks), `ItemPreviews` (supplier pictures), `GameConfig`, `GameRandom`, `GameLog`, `FishTankHUD`, helpers |
| `Art/Fish/Meshy` | Imported Meshy models go here (see `Docs/MeshyPipeline.md`) |

## Fish models

The first real fish is the **Comet Goldfish** in `Assets/Resources/Fish/CometGoldfish` (Meshy model cut from 33,936 to 3,000 triangles, 1024 textures). Every fish uses it for now. It swims with the `FishBreeder/FishSwim` shader driven by `SwimSpine`: the body is laid along the path the head has just travelled (so turns curve the whole fish), carries a carangiform body wave using the measured mackerel amplitude envelope (about 2% of body length at the nose, 10% at the tail tip, one wave per body), and rolls into turns with a small tail twist. Tail beats speed up when the fish accelerates or turns and relax into gentle strokes while gliding, and headings ease in and out with a damped spring. The head end is detected automatically, so any Meshy fish exported with Y up works. Fish steer around decorations (rocks, the rock arch, driftwood, plants), keep off the glass, and pick wander targets in open water. Anything with a collider and a `TankObstacle` component counts as an obstacle.

To add another Meshy fish, see `Docs/MeshyPipeline.md`.

## Placeholder fish

When no model is available, a fish is a real 3D mesh generated from its genes: body length and height, fin shape (Round, Fan, Spiky, Veil, Ribbon), tail shape (Fork, Round, Lyre, Sword, Veil), body/pattern colours and pattern (Solid, Striped, Spotted, Marbled). The tail is a separate part that wags while swimming.

## Next steps

1. Let genes vary the goldfish's colours (hue shift in the swim shader) and add more Meshy species.
2. Save/load the tank (genomes are already JSON-serialisable via `Genome.ToJson`).
3. Economy: sell fish by rarity, buy food, medicine and tank upgrades.
4. Replace the IMGUI HUD with a real UI.
5. EditMode tests for `BreedingService` inheritance odds.
