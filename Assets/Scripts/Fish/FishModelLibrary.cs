using System;
using System.Collections.Generic;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Maps species (and optionally fin/tail combinations within a species) to imported Meshy
    /// models, with a default model for anything else. Create one via Assets > Create > Fish Breeder >
    /// Fish Model Library, or let GameBootstrap use <see cref="LoadBuiltIn"/> (the models in Resources/Fish).
    /// </summary>
    [CreateAssetMenu(menuName = "Fish Breeder/Fish Model Library", fileName = "FishModelLibrary")]
    public class FishModelLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string displayName = "Fish";
            [Tooltip("Species id this model is for (see Species). Empty matches any species.")]
            public string species;
            [Tooltip("Optional: only use this model for fish with these fins and tail.")]
            public bool matchFinsAndTail;
            public FinShape finShape;
            public TailShape tailShape;
            [Tooltip("The Meshy model. Any orientation with Y up works; the head end is detected automatically.")]
            public GameObject model;
            public Texture2D albedo;
            public Texture2D normalMap;
            [Tooltip("Optional greyscale smoothness map (white = glossy), multiplied by Smoothness.")]
            public Texture2D smoothnessMap;
            [Range(0f, 1f)] public float smoothness = 0.6f;
            [Tooltip("How much the genetic body colour tints the texture. 0 keeps the texture as is.")]
            [Range(0f, 1f)] public float geneTintStrength;

            [Header("Swimming")]
            [Tooltip("Multiplier on the body wave. 1 = a typical carangiform fish (tail tip sweeps 10% of body length).")]
            public float strokeStrength = 1f;
            [Tooltip("Body waves per body length. About 1 for goldfish and most fish, 2+ for eels.")]
            public float wavesPerBody = 1f;
            [Tooltip("How closely the body follows the head's path through turns (0 = rigid).")]
            [Range(0f, 1f)] public float pathFollow = 0.85f;
            [Tooltip("Peak roll of the tail during each beat, in degrees.")]
            public float tailTwistDegrees = 8f;
            [Tooltip("Tail beats per second at cruising speed.")]
            public float tailBeatsPerSecond = 2.4f;
            [Tooltip("Only for models without a species: 0 = slender fish that bend along most of their body; 1 = deep-bodied fish whose front half stays stiff. Species set where their body bends with bendStart (console Swimming tab).")]
            [Range(0f, 1f)] public float bodyStiffness = 0.3f;
        }

        /// <summary>
        /// How stiff each body is. Deep, short bodies (goldfish, angelfish, discus-shaped fish)
        /// swim mostly with the back third; slim fish (danios, tetras, sharks) bend further forward.
        /// </summary>
        static readonly Dictionary<string, float> Stiffness = new Dictionary<string, float>
        {
            { "CometGoldfish", 0.75f }, { "FancyGoldfish", 0.9f }, { "Angelfish", 0.9f }, { "SilverDollar", 0.9f },
            { "Oscar", 0.8f }, { "GreenTerror", 0.7f }, { "BlueAcara", 0.7f }, { "Betta", 0.35f }, { "Clownfish", 0.55f },
            { "BlackMolly", 0.5f }, { "Platy", 0.5f }, { "GoldenMbuna", 0.45f }, { "Guppy", 0.3f }, { "TigerBarb", 0.45f },
            { "NeonTetra", 0.25f }, { "ZebraDanio", 0.15f }, { "Corydoras", 0.6f }, { "RedTailShark", 0.2f }, { "SiameseAlgaeEater", 0.15f },
            { "BlackSkirtTetra", 0.55f }, { "BleedingHeartTetra", 0.5f }, { "RedeyeTetra", 0.45f }, { "SerpaeTetra", 0.4f }, { "FlameTetra", 0.4f },
            { "LemonTetra", 0.4f }, { "XRayTetra", 0.3f }, { "EmperorTetra", 0.35f }, { "CongoTetra", 0.3f }, { "BucktoothTetra", 0.25f },
            { "CrimsonJewelCichlid", 0.8f }, { "SilverFlamefin", 0.5f },
        };

        /// <summary>Fish with their own atlases (folder, texture prefix, fish): the wild tetras in two Meshy sets, and the big wall-aquarium fish one per folder.</summary>
        static readonly (string folder, string prefix, string[] fish)[] TetraSets =
        {
            ("Fish/TetraSetA/", "TetraFishA", new[] { "BloodfinTetra", "SerpaeTetra", "XRayTetra", "BlackSkirtTetra", "RedeyeTetra", "LemonTetra", "BleedingHeartTetra", "BuenosAiresTetra" }),
            ("Fish/TetraSetB/", "TetraFishB", new[] { "EmperorTetra", "FlameTetra", "CongoTetra", "PenguinTetra", "GlowlightTetra", "BucktoothTetra", "RubyTetra", "SilvertipTetra", "RummyNoseTetra" }),
            ("Fish/CrimsonJewelCichlid/", "CrimsonJewelCichlid", new[] { "CrimsonJewelCichlid" }),
            ("Fish/SilverFlamefin/", "SilverFlamefin", new[] { "SilverFlamefin" }),
        };

        public Entry defaultModel = new Entry();
        public List<Entry> entries = new List<Entry>();

        public Entry Find(string species, FinShape finShape, TailShape tailShape)
        {
            Entry speciesOnly = null;
            foreach (var entry in entries)
            {
                if (entry.model == null) continue;
                if (!string.IsNullOrEmpty(entry.species) && entry.species != species) continue;
                if (!entry.matchFinsAndTail)
                {
                    if (speciesOnly == null) speciesOnly = entry;
                    continue;
                }
                if (entry.finShape == finShape && entry.tailShape == tailShape) return entry;
            }
            if (speciesOnly != null) return speciesOnly;
            return defaultModel != null && defaultModel.model != null ? defaultModel : null;
        }

        /// <summary>
        /// Library with every built-in Meshy fish: the Comet Goldfish (also the default) and the
        /// aquarium and freshwater sets in Resources/Fish, each sharing one texture atlas.
        /// Swimming values are per species: small fish beat their tails faster, angelfish and
        /// bettas glide on big fins, the armoured corydoras bends less.
        /// </summary>
        public static FishModelLibrary LoadBuiltIn()
        {
            var library = CreateInstance<FishModelLibrary>();
            library.name = "Built-in Fish Models";

            const string comet = "Fish/CometGoldfish/";
            library.defaultModel = new Entry
            {
                displayName = "Comet Goldfish",
                species = Species.CometGoldfish.id,
                model = Resources.Load<GameObject>(comet + "CometGoldfish"),
                albedo = Resources.Load<Texture2D>(comet + "CometGoldfish_Albedo"),
                normalMap = Resources.Load<Texture2D>(comet + "CometGoldfish_Normal"),
                bodyStiffness = Stiffness["CometGoldfish"],
            };
            if (library.defaultModel.model == null)
                Debug.LogWarning("[FishBreeder] Comet Goldfish model not found in Resources; using placeholder fish.");
            library.entries.Add(library.defaultModel);

            const string set = "Fish/AquariumSet/";
            var albedo = Resources.Load<Texture2D>(set + "AquariumFish_Albedo");
            var normal = Resources.Load<Texture2D>(set + "AquariumFish_Normal");
            var smoothnessMap = Resources.Load<Texture2D>(set + "AquariumFish_Smoothness");

            AddSetFish(library, set, albedo, normal, smoothnessMap, "FancyGoldfish", 2.2f, 0.9f, 0.8f, 10f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "Angelfish", 1.4f, 0.6f, 0.6f, 6f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "Betta", 1.6f, 0.8f, 0.8f, 12f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "Clownfish", 3.2f, 0.7f, 0.6f, 6f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "Guppy", 3.5f, 1f, 0.85f, 10f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "TigerBarb", 3.6f, 1f, 0.85f, 8f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "NeonTetra", 4f, 1f, 0.9f, 8f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "ZebraDanio", 4.5f, 1.1f, 0.9f, 8f);
            AddSetFish(library, set, albedo, normal, smoothnessMap, "Corydoras", 4f, 1f, 0.5f, 4f);

            // Second Meshy set: livebearers, sharks and cichlids, sharing their own atlas.
            const string freshwater = "Fish/FreshwaterSet/";
            var fwAlbedo = Resources.Load<Texture2D>(freshwater + "FreshwaterFish_Albedo");
            var fwNormal = Resources.Load<Texture2D>(freshwater + "FreshwaterFish_Normal");
            var fwSmoothness = Resources.Load<Texture2D>(freshwater + "FreshwaterFish_Smoothness");
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "BlackMolly", 3f, 0.9f, 0.8f, 10f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "Platy", 3.4f, 1f, 0.85f, 10f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "RedTailShark", 3f, 1f, 0.8f, 8f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "SiameseAlgaeEater", 3.4f, 1f, 0.85f, 8f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "SilverDollar", 2.2f, 0.8f, 0.7f, 6f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "Oscar", 1.8f, 0.8f, 0.7f, 8f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "GreenTerror", 2.2f, 0.9f, 0.75f, 8f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "BlueAcara", 2.4f, 0.9f, 0.75f, 8f);
            AddSetFish(library, freshwater, fwAlbedo, fwNormal, fwSmoothness, "GoldenMbuna", 3f, 1f, 0.8f, 8f);

            // Wild tetras: quick little tail beats like the neon; the big bucktooth and congo a bit slower.
            // The metre-long wall-aquarium fish beat slowly and deeply.
            foreach (var tetras in TetraSets)
            {
                var tAlbedo = Resources.Load<Texture2D>(tetras.folder + tetras.prefix + "_Albedo");
                var tNormal = Resources.Load<Texture2D>(tetras.folder + tetras.prefix + "_Normal");
                var tSmoothness = Resources.Load<Texture2D>(tetras.folder + tetras.prefix + "_Smoothness");
                foreach (var id in tetras.fish)
                {
                    if (id == "CrimsonJewelCichlid" || id == "SilverFlamefin")
                    {
                        AddSetFish(library, tetras.folder, tAlbedo, tNormal, tSmoothness, id, 1.2f, 1f, 0.8f, 6f);
                        continue;
                    }
                    bool big = id == "BucktoothTetra" || id == "CongoTetra";
                    AddSetFish(library, tetras.folder, tAlbedo, tNormal, tSmoothness, id, big ? 3.2f : 3.9f, 1f, 0.9f, 8f);
                }
            }
            return library;
        }

        static void AddSetFish(FishModelLibrary library, string folder, Texture2D albedo, Texture2D normal, Texture2D smoothnessMap,
            string id, float beats, float stroke, float follow, float twist)
        {
            var model = Resources.Load<GameObject>(folder + id);
            if (model == null)
            {
                Debug.LogWarning($"[FishBreeder] {id} model not found in Resources/{folder}.");
                return;
            }
            library.entries.Add(new Entry
            {
                displayName = Species.Get(id).displayName,
                species = id,
                model = model,
                albedo = albedo,
                normalMap = normal,
                smoothnessMap = smoothnessMap,
                smoothness = 0.75f,
                tailBeatsPerSecond = beats,
                strokeStrength = stroke,
                pathFollow = follow,
                tailTwistDegrees = twist,
                bodyStiffness = Stiffness.TryGetValue(id, out var stiffness) ? stiffness : 0.3f,
            });
        }
    }
}
