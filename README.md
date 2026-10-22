# Fish Breeder

A Fish Tycoon–style breeding game in Unity with fully 3D fish.

- **Fish never die of old age.** They grow from fry to adult and then live until a chance event gets them.
- **Every meal is a dice roll.** Each pellet a fish eats can make it sick (Aqua Flu, Fin Rot, White Spot, Swim Bladder). Cheap flakes, dirty water, starving fish and low hardiness all raise the odds. Some illnesses spread to nearby fish.
- **Breeding mixes genes.** Every trait has two alleles, one from each parent. Colours and sizes blend; fin shape, tail shape and pattern follow dominant/recessive rules, so rare looks can hide for generations and then pop out.

## Opening the project

1. Install **Unity 6 (6000.0 LTS)** with Unity Hub. Any 6000.0.x patch is fine; Hub may ask to switch versions, say yes.
2. In Unity Hub, **Add > Add project from disk** and pick this folder. First import takes a few minutes.
3. Open any scene (or **File > New Scene**) and press **Play**. `GameBootstrap` builds the tank, camera, light, HUD and six wild fish automatically.

If Hub refuses to open the folder, create a new **3D (Built-In Render Pipeline)** project called FishBreeder and copy the `Assets` folder from here into it.

## Playing (placeholder HUD)

- **Feed flakes / Feed premium**: flakes are cheap but sometimes spoiled; premium is much safer.
- **Clean tank**: water gets dirtier from fish waste and rotting food; dirty water triples illness odds.
- **Click a fish** to inspect it (traits, hunger, hardiness, illness). Sick fish show medicine buttons; the wrong medicine does nothing.
- **Click a second adult** and press **Breed these two**. Fry inherit a mix of both parents' genes and grow up over about 3 minutes.

All tuning numbers live in `GameConfig` (Assets > Create > Fish Breeder > Game Config, then drag it onto the GameBootstrap object).

## Code layout

| Folder | What's there |
| --- | --- |
| `Scripts/Genetics` | `Genes` (allele types and trait enums), `Genome` (a fish's DNA), `Phenotype` (what the DNA looks like in game units, rarity) |
| `Scripts/Breeding` | `BreedingService` (pure Mendelian crossover + mutation), `BreedingSystem` (in-game rules: adults, healthy, cooldown, tank space) |
| `Scripts/Feeding` | `FeedingSystem` drops food; `FoodPellet` sinks, gets eaten or rots |
| `Scripts/Health` | `DiseaseDefinition` (the illness catalogue), `FishHealth` (infection per meal, progression, recovery/death rolls, contagion, medicine) |
| `Scripts/Fish` | `Fish` (hunger, growth, death), `FishSwimmer` (wander + chase food), `FishVisual` (Meshy model or procedural placeholder), `PlaceholderFishMesh` and `FishPatternTexture` (gene-driven 3D placeholder), `FishModelLibrary` (Meshy model lookup) |
| `Scripts/Tank` | `Tank` (bounds, residents, water quality, placeholder glass tank) |
| `Scripts/Core` | `GameBootstrap`, `GameConfig`, `GameRandom`, `GameLog`, `FishTankHUD`, helpers |
| `Art/Fish/Meshy` | Imported Meshy models go here (see `Docs/MeshyPipeline.md`) |

## Placeholder fish

Until Meshy models are in, every fish is a real 3D mesh generated from its genes: body length and height, fin shape (Round, Fan, Spiky, Veil, Ribbon), tail shape (Fork, Round, Lyre, Sword, Veil), body/pattern colours and pattern (Solid, Striped, Spotted, Marbled). The tail is a separate part that wags while swimming.

## Next steps

1. Generate the first Meshy fish and wire it into a `FishModelLibrary` (see `Docs/MeshyPipeline.md`).
2. Save/load the tank (genomes are already JSON-serialisable via `Genome.ToJson`).
3. Economy: sell fish by rarity, buy food, medicine and tank upgrades.
4. Replace the IMGUI HUD with a real UI.
5. EditMode tests for `BreedingService` inheritance odds.
