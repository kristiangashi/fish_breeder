# Meshy.ai asset pipeline

Goal: real 3D fish models from Meshy that still show each fish's genes (colour, pattern, fins, tail).
The code is already set up for this: `FishModelLibrary` picks a model by fin + tail shape, scales it to the
gene-driven length and tints it with the body colour. Any combination without a model falls back to the
procedural placeholder, so models can be added one at a time.

## 1. What to generate

Fin shape (5) x tail shape (5) is 25 combinations. Start with **one model per tail shape** (5 models);
the library falls back to "same tail, any fins" when an exact match is missing. Add fin variants later.

Suggested Meshy **Text to 3D** prompt (swap the tail/fin words):

> stylized aquarium fish, side-flattened body, **forked tail**, **rounded dorsal fin**, small pectoral fins,
> clean simple shapes, game asset, **plain white body with no colour or pattern**, smooth surface

Settings:
- **Art style:** stylized / cartoon (easier to keep consistent than realistic).
- **Topology:** quad, **target ~5k–10k polygons** (dozens of fish on screen at once).
- **Texture:** keep it **white or light grey**. Genes provide colour at runtime by tinting the material,
  so a coloured Meshy texture would fight the genetics.
- Use **Refine** on the best preview, and keep the same seed/style across the set so the fish look like one species family.

## 2. Export

- Format: **FBX** (or GLB with the glTFast package). Units: metres.
- Download into a `MeshyRaw/` folder outside `Assets` first (it's git-ignored).

## 3. Clean up in Blender (10 minutes per fish)

What was done for the Comet Goldfish: merge duplicate vertices, a Decimate (Collapse) modifier down to about
3,000 triangles (Meshy exports around 34,000), centre the origin, export FBX with Forward -Z / Up Y /
Apply Transform, and scale the albedo and normal maps to 1024. Splitting off the tail is no longer needed:
the swim shader bends the whole body. Below 3,000 triangles the goldfish's thin fins started to smear
their texture.

Older manual steps, still useful for unusual models:

1. Rotate so the fish **faces +Z** in Unity (in Blender: nose along -Y, then export FBX with "Forward: -Z, Up: Y", or just fix it once in a prefab).
2. Put the pivot at the **centre of the body**.
3. **Separate the tail fin into its own object named `Tail`**, with its origin at the tail base. `FishVisual` wags any child called `Tail` (the name is configurable per library entry).
4. Optional: separate dorsal/pectoral fins too, for later fin-specific colouring.
5. Export FBX to `Assets/Art/Fish/Meshy/<TailShape>_<FinShape>.fbx`, e.g. `Fork_Round.fbx`.

## 4. Hook up in Unity

Quick way (no prefab needed): put `<Name>.fbx`, `<Name>_Albedo.png` and `<Name>_Normal.png` in
`Assets/Resources/Fish/<Name>/`. `FishAssetPostprocessor` sets the import options (Read/Write on, no
materials, normal map type). Then point `FishModelLibrary.LoadBuiltIn` at it, or add a library entry.

Library asset way:

1. Drag the FBX into the scene, check it faces the blue (Z) axis, and save it as a prefab in `Assets/Prefabs/Fish/`.
2. **Assets > Create > Fish Breeder > Fish Model Library**.
3. Add an entry per prefab: fin shape, tail shape, prefab, tail transform name (`Tail`).
4. Create an empty GameObject in your scene, add **GameBootstrap**, and assign the library.
5. Press Play: fish with that tail shape now use the Meshy model, tinted by their genes.

## 5. Later: patterns and swimming on Meshy models

- **Patterns.** The placeholder paints stripes/spots into its own UV layout. For Meshy models, the plan is a
  small shader (Shader Graph) that draws the pattern in object space (along the body's Z axis), using the
  same gene values (`pattern`, `patternScale`, `patternOffset`, `patternColor`). That way it works on any
  model regardless of Meshy's UV unwrap.
- **Swimming.** Meshy's auto-rigging targets humanoids, so fish use a **vertex-shader body wave**
  (side-to-side sine that grows toward the tail) in the same shader, plus the separate `Tail` object wag.
  No skeleton needed.

## 6. Other assets from Meshy

Same approach for the tank set dressing: rocks, driftwood, plants, a treasure chest, a castle. These don't
need genes, so keep Meshy's textures. Budget ~2k–5k polygons each.

The current set lives in `Assets/Resources/Decor/` and came from one Meshy "kit" mesh:

1. Separate by loose parts (after merging doubles); fold tiny islands (the pearl, small leaves) into the
   piece they touch.
2. Per piece: put the pivot at the centre of its base, decimate the render mesh (1.2k–5k tris) and a copy
   to 120–300 tris named `<Piece>_Collider`, export both in one FBX (forward -Z, up Y, bake space transform).
3. All pieces share one atlas: `Decor_Albedo`, `Decor_Normal` and `Decor_MetallicSmoothness`
   (metallic in R, 1 − roughness in A, built from Meshy's metallic and roughness maps).
4. `TankDecor` places each piece by name, scales it to a height in metres, gives the `_Collider` mesh a
   MeshCollider and marks the piece as a `TankObstacle`. Edit its `Layout` table to rearrange the tank.

## Checklist per fish model

- [ ] Generated with white body and target poly count
- [ ] Faces +Z, pivot at body centre
- [ ] Tail split into a child named `Tail`
- [ ] Saved as `Assets/Art/Fish/Meshy/<Tail>_<Fin>.fbx` and a prefab
- [ ] Added to the Fish Model Library
