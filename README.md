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
- **Levels**: three tracks up to level 60, shown in the top bar with today's goals. Fishkeeping XP comes from feeding, wiping, curing, hatching and breeding (more for rarer fish and looks) and unlocks equipment, food, egg boxes, rarer species, tank sizes, breeder and quarantine tanks. Shopkeeping XP comes from sales, happy customers, good reviews and buying things for the shop, and unlocks decorations, more display and for sale tanks and new store sections. Research XP comes from studying the fish you keep and unlocks faster research and, at level 2, measuring pH. Each day has one goal per track worth a big chunk of XP. Higher Fishkeeping makes wipes clean more and adds an extra wipe every 8 levels.
- **Supplier computer** (tabs, each item with a picture: photos of the real decoration and fish models, drawn icons for the rest): eggs, fry, decorations, food, and equipment and care. Things above your level show what unlocks them. **Fry boxes** work like egg boxes but can hold any species (livebearers only come this way, egg boxes only hold egg-layers). Bought fry are all tiny grey fish, so you can't tell what they are until they grow into juveniles; fry you breed yourself are known. **Equipment** comes in four quality tiers per kind (filters: sponge, hang-on-back, canister, sump; heaters: basic, adjustable, titanium, smart controller; bubblers: air stone, twin-outlet, silent pump, oxygen reactor). Better tiers clean more, hold the temperature closer to the setting, or give air to more fish, and break down less. Equipment wears with age and can break at any time step (the chance grows with wear and is shown in the tank panel); a broken filter, heater or bubbler does nothing until you fix it with a **repair kit**, and each repair only takes off a quarter of the wear, so old kit keeps breaking. Egg boxes hold mixed species: **Simple** eggs hatch common fish with a small chance of a rare one; **Rare** eggs hatch a mix with a good chance of rare fish. Every rare egg already in a box makes the next one less likely, so 2 rares in a box is much harder than 1 (the odds are listed). Ultra Rare, Legendary and Ancient boxes are locked for later.
- **Click a tank** to look inside: water quality, algae, oxygen, temperature, pH, salt, pests and equipment, the fish count against the tank's limit, plus feeding with any food you own, wiping (1 wipe per tank per closed session, more with Fishkeeping levels), treating the water, dosing the whole tank with medicine, repairing broken equipment, removing dead fish, its decorations (add from the supplier, remove for half price back), upgrading to a Large or Grand tank, and with a heater, setting the temperature (it shows the range every fish in the tank is happy with, as far as research knows). The temperature is shown as a hot-to-cold bar with the liked range outlined. The tank floor is gravel in fresh water and sand with bits of coral in salt water. **Tank limit**: a tank can hold up to 1.5x its limit, but every fish over it dirties the water faster, spreads illness and stresses everyone. Orders from the supplier only fill a tank to its limit.
- **Food**: flakes, algae wafers (they sink fast, for bottom feeders), premium pellets, frozen brine shrimp and frozen bloodworms. Every fish eats any food, but each species has favourites that fill it more and cheer it up. Feeding a favourite sometimes reveals it.
- **Research** (notes with R or the Notes button): every species you keep earns research points at each time step, times your research speed, and its facts unlock as the points add up: temperature range, plants, need for company, pH range, breeding and lifespan. Two kinds of adult fish sharing a tank may show how they get on. The notes list every species studied with what you know and how far off the rest is. Until you've researched something about a fish, its window doesn't show it.
- **Click a fish** for its status window (it scrolls): sex and age as coloured badges, health and where it's heading at the next time step, what is pulling it down (hunger, illness, comfort, water, crowding, old age, dead tank mates) with each cause on its own line, its needs as far as research knows (temperature and pH bars), what it thinks of its tank mates, appraised value, price (For Sale tanks; only juveniles and adults sell) and moving it to another tank, after which the next fish is picked. The camera follows it zoomed in; the left/right arrow keys or the < > buttons beside it step through the tank's fish. Esc closes the fish, Esc again leaves the tank. With no fish selected, the arrow keys or the < > bar at the bottom move to the previous or next tank (back room first, then the shop floor left to right).
- **Medicine** goes into the whole tank: it cures every fish with the matching illness, but stresses healthy fish (lower comfort for a few time steps) and builds up in every fish. Too much in a short time is an overdose that permanently lowers health and value, and two overdoses leave a fish unable to breed.
- **Tank mates**: every pair of species has a relationship, set in the balance console from real fishkeeping advice. **Predator**: the fish hunts the other, eating it if it is small enough to fit in its mouth and biting it otherwise (an oscar eats neons, an angelfish eats neons and guppies, goldfish eat neons). **Competitive**: they stress each other, and in an overcrowded tank they nip and injure each other (tiger barbs and long-finned fish, two male bettas, mbuna with most fish). **Preferred**: both are happier and a little healthier together (schooling fish with their own kind, neons with corydoras, fantails with danios). **Neutral**: they get along fine.
- **After closing**: the shop lists what needs doing, always in the same order: hungry fish, sick fish, dead fish, dirty tanks, then broken equipment, and takes you to each one in turn, moving on by itself once it's dealt with. You can skip, go back or stop and pick it up later from the top bar.
- **Breeding**: any compatible pair (an adult male and female of the same species, healthy, fed, rested, and happy with the water) may breed at each time step, likeliest in a breeder tank; in a breeder tank you can also pair two fish by hand. Livebearers (guppy, platy, black molly) get pregnant and give birth a few time steps later; egg-layers lay a clutch on the sand that hatches a few time steps later. Filthy water kills eggs. The tank panel lists eggs and pregnant fish with how long until they hatch or give birth. Pregnancy length and hatch time are per species in the balance console.
- **Empty tank spots** and **locked sections**: buy more tanks and floor space.

**Open Shop** (top bar or register): a time step runs, then customers come in for one real hour. They wander between tanks, react with thought bubbles (happy, grossed out by bad water, sad about dead fish, angry about prices), sometimes buy a juvenile or adult from a For Sale tank (a dead fish in the shop makes some sad and puts others off a little), pay at the register and go home. The shop closes on its own, another time step runs, and you get the day's report and reviews, which move your reputation and next day's traffic. Developer buttons in the top bar speed the day up (5x, 20x) or skip the rest of it; the keys 1, 2, 3 and K do the same.

Each time step: eggs develop and hatch when due, pregnant fish give birth when due; water quality drops with waste, algae grows (faster with more decorations), pH drifts and pests may appear; fish age and get hungrier, and their health moves toward what their conditions allow (it only changes at time steps); research advances; illness progresses, spreads or strikes; equipment wears and may break; predators hunt and crowded rivals nip (while the shop is open you see predators dart at their prey now and then); compatible pairs may breed (likelier in breeder tanks); and fish in poor health roll for death.

## Balance console

All balancing numbers (species rarity, prices, needs, relationships between species, diseases, customers, time-step chances, health weights, store and supplier prices, egg boxes and their odds, decorations, tank sizes, unlock levels, XP and daily goals, closing notifications) live in one file, `Assets/Resources/Balance/balance.json`. The search box at the top finds any setting by name or help text. Edit them in the browser: with the project open in Unity, choose **Fish Breeder > Balance Console** (or go to http://localhost:8642). **Save** writes the file and, if the game is playing, applies the new values straight away. The console is an editor-only tool (`Tools/BalanceConsole/index.html`, served by `Assets/Scripts/Editor/BalanceConsoleServer.cs`) and is not part of a build; the JSON file is, so the game ships with whatever you saved. Until you first save, the defaults in code are used. Numbers are kept to 2 decimals. If a save can't reach Unity (for example while it recompiles), the console keeps retrying for a minute and keeps your edits in the browser until one gets through. Species added to the game later join your saved file automatically with their default values.


## Code layout

| Folder | What's there |
| --- | --- |
| `Scripts/Genetics` | `Genes` (allele types and trait enums), `Genome` (a fish's DNA), `Phenotype` (what the DNA looks like in game units, rarity), `Species` (real-world names, families, temperature and pH ranges), `Relationships` (predator/competitive/neutral/preferred table) |
| `Scripts/Breeding` | `BreedingService` (pure Mendelian crossover + mutation), `BreedingSystem` (in-game rules: compatible pairs, chance breeding at time steps, pregnancy and egg timing) |
| `Scripts/Feeding` | `FeedingSystem` drops food; `FoodPellet` sinks, gets eaten or rots |
| `Scripts/Health` | `DiseaseDefinition` (the illness catalogue), `FishHealth` (infection per meal, progression, recovery/death rolls, contagion, tank-wide medicine and overdoses, comfort), `TankMates` (predators hunting and nipping in crowded tanks, plus the little lunges you see) |
| `Scripts/Fish` | `Fish` (hunger, growth, death), `FishSwimmer` (wander + chase food), `FishVisual` (Meshy model or procedural placeholder), `PlaceholderFishMesh` and `FishPatternTexture` (gene-driven 3D placeholder), `FishModelLibrary` (Meshy model lookup) |
| `Scripts/Tank` | `Tank` (bounds, residents, water quality, equipment wear and breakdowns, placeholder glass tank), `Equipment` (installed filter/heater/bubbler tiers), `EggClutch` (eggs that hatch after a few time steps), `TankDecor` (decorations placed in spots, more in bigger tanks), `TankSubstrate` (procedural gravel and sand floors) |
| `Scripts/Store` | `StoreLayout` (rooms, sections, tank spots, register, computer, walkable area), `StoreView` (store camera and windows), `StoreTank` (tank roles), `SupplierCatalog` (prices), `EggBoxes` (egg box rolls and odds) |
| `Scripts/Shop` | `ShopDay` (open-shop day, sales, reviews), `TimeStep` (everything that happens at open and close), `Customer` and `ShopperBrain` (shoppers and their reactions), `MoodIcons`, `StoreNavMesh`, `ToDoList` (problems to sort through after closing) |
| `Scripts/Core` | `GameBootstrap`, `Balance` (all balancing numbers, loaded from the balance file), `Economy` (money, stock, day, reputation), `Progression` (XP, levels, daily goals, unlocks), `Research` (what you've learned about each species), `ResearchNotesView` (the research notebook), `UiGauges` (temperature and pH bars, badges), `ItemPreviews` (supplier pictures), `GameConfig`, `GameRandom`, `GameLog`, `FishTankHUD`, helpers |
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
