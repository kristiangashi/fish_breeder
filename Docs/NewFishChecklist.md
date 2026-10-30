# New fish checklist

Do every step for every new fish. Nothing here is a step for Kristian: the cloud runs it and saves to his PC.

## Model
1. Stage the Meshy FBX and textures from his Downloads folder.
2. `Tools/Meshy/split_fish_kit.py`: add a KITS entry. Decimate to about 1800 tris (small fish) or 3000 (big fish). Plump it if the body is flat. Single fish lying along Y get turned to lie along X. Check the straightened tail and the dimensions it prints.
3. `Tools/Meshy/split_fins.py`: split body and fins (add a peduncle OVERRIDE if the tail cut is wrong). Render the parts and look at them.
4. Textures in `Assets/Resources/Fish/<Sid>/`: `<Sid>_Albedo.png`, `<Sid>_Normal.png` (normal-map import settings), `<Sid>_Smoothness.png` (inverted roughness). Every file and the folder get a .meta with a fresh GUID.
5. `FishModelLibrary.cs`: register the model (TetraSets / AddSetFish), swim parameters, Stiffness fallback.

## Species data (`Species.cs`)
6. `Make()` with real-world data: category, rarity, value, fry price, length range, speed, swim depth, temperature, pH, hunger, school size, reproduction type and litter, daysToAdult, lifespan, unlockLevel, minTankSize (Tank.WallSize = wall aquarium only), likesPlants, eatsAlgae, swim style, onlyInBox.
7. Tables: MinTankSizes, Favourites (foods), LikedDecorations, Roamers (if it roams), BendStarts (where the body starts to wiggle).
8. Size scales: sizeSmall / sizeMedium / sizeLarge (defaults 0.85 / 1 / 1.18).

## Fitting it into the game
9. `Relationships.cs`: predator, competitive and preferred rows, both directions.
10. `CrossBreeding.cs`: DefaultPairs with fish of the same category and the same reproduction type.
11. Egg / fry box hatch entries for the right tier and unlock level.
12. `Balance.cs`: bump CurrentDataVersion and add a migration so saved balance files get the relationship rows, cross pairs and hatch entries (AddMissingSpecies adds the species itself).

## Admin console
13. `Tools/Meshy/console_pictures.py <root> <Sid>` so the Swimming tab has its side and top pictures. Open the console and check the species form, the Swimming card (bend line and size row) and any new enum values.

## Check and ship
14. Compile with mcs with and without UNITY_EDITOR; `node --check` the console script.
15. Commit, push, sync to his PC (bundle + ff-merge), ctrl+r in Unity, play: spawn it from the Dev tab, check swimming, size, clipping with other fish, the move message and breeding.
16. Update project memory.
