using System;
using System.Collections.Generic;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Maps fin/tail combinations to imported Meshy models. Any combination without an
    /// entry falls back to the procedural placeholder, so models can be added one at a time.
    /// Create via Assets > Create > Fish Breeder > Fish Model Library.
    /// </summary>
    [CreateAssetMenu(menuName = "Fish Breeder/Fish Model Library", fileName = "FishModelLibrary")]
    public class FishModelLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public FinShape finShape;
            public TailShape tailShape;
            [Tooltip("Prefab of the Meshy model, facing +Z, with a white/greyscale texture so genes can tint it.")]
            public GameObject prefab;
            [Tooltip("Optional child transform that wags when swimming (split the tail into its own mesh in Blender).")]
            public string tailTransformName = "Tail";
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(FinShape finShape, TailShape tailShape)
        {
            Entry tailOnly = null;
            foreach (var entry in entries)
            {
                if (entry.prefab == null || entry.tailShape != tailShape) continue;
                if (entry.finShape == finShape) return entry;
                if (tailOnly == null) tailOnly = entry;
            }
            return tailOnly;
        }
    }
}
