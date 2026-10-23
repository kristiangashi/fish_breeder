using System;
using System.Collections.Generic;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Maps fin/tail combinations to imported Meshy models, with a default model for every
    /// other fish. Create one via Assets > Create > Fish Breeder > Fish Model Library, or let
    /// GameBootstrap use <see cref="LoadBuiltIn"/> (the Comet Goldfish in Resources/Fish).
    /// </summary>
    [CreateAssetMenu(menuName = "Fish Breeder/Fish Model Library", fileName = "FishModelLibrary")]
    public class FishModelLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string displayName = "Fish";
            public FinShape finShape;
            public TailShape tailShape;
            [Tooltip("The Meshy model. Any orientation with Y up works; the head end is detected automatically.")]
            public GameObject model;
            public Texture2D albedo;
            public Texture2D normalMap;
            [Range(0f, 1f)] public float smoothness = 0.6f;
            [Tooltip("How much the genetic body colour tints the texture. 0 keeps the texture as is.")]
            [Range(0f, 1f)] public float geneTintStrength;

            [Header("Swimming")]
            [Tooltip("Side-to-side bend at the tail tip, as a fraction of body length.")]
            public float swimAmplitude = 0.12f;
            [Tooltip("How many waves run along the body. Below 1 looks like a goldfish, above 1 like an eel.")]
            public float swimWaves = 0.9f;
            [Range(0f, 0.9f)] public float headStiffness = 0.3f;
            [Tooltip("Tail beats per second at cruising speed.")]
            public float tailBeatsPerSecond = 2.2f;
        }

        public Entry defaultModel = new Entry();
        public List<Entry> entries = new List<Entry>();

        public Entry Find(FinShape finShape, TailShape tailShape)
        {
            Entry tailOnly = null;
            foreach (var entry in entries)
            {
                if (entry.model == null || entry.tailShape != tailShape) continue;
                if (entry.finShape == finShape) return entry;
                if (tailOnly == null) tailOnly = entry;
            }
            if (tailOnly != null) return tailOnly;
            return defaultModel != null && defaultModel.model != null ? defaultModel : null;
        }

        /// <summary>Library with the Comet Goldfish from Resources/Fish/CometGoldfish as the default model.</summary>
        public static FishModelLibrary LoadBuiltIn()
        {
            const string folder = "Fish/CometGoldfish/";
            var library = CreateInstance<FishModelLibrary>();
            library.name = "Built-in Fish Models";
            library.defaultModel = new Entry
            {
                displayName = "Comet Goldfish",
                model = Resources.Load<GameObject>(folder + "CometGoldfish"),
                albedo = Resources.Load<Texture2D>(folder + "CometGoldfish_Albedo"),
                normalMap = Resources.Load<Texture2D>(folder + "CometGoldfish_Normal"),
            };
            if (library.defaultModel.model == null)
                Debug.LogWarning("[FishBreeder] Comet Goldfish model not found in Resources; using placeholder fish.");
            return library;
        }
    }
}
