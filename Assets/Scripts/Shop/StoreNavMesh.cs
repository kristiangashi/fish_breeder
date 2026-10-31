using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FishBreeder.Shop
{
    /// <summary>
    /// Walkable area for customers, baked at runtime from the store's floor and furniture boxes
    /// (no scene setup or extra packages). Rebuilt when the store grows.
    /// </summary>
    public static class StoreNavMesh
    {
        static NavMeshDataInstance instance;
        static NavMeshBuildSettings settings;
        static bool hasSettings;

        public static int AgentTypeId => Settings.agentTypeID;
        public static bool Ready => instance.valid;

        static NavMeshBuildSettings Settings
        {
            get
            {
                if (!hasSettings)
                {
                    settings = NavMesh.CreateSettings();
                    settings.agentRadius = 0.2f;
                    settings.agentHeight = 1.7f;
                    settings.agentClimb = 0.05f;
                    settings.agentSlope = 30f;
                    settings.minRegionArea = 0.5f;
                    hasSettings = true;
                }
                return settings;
            }
        }

        public static NavMeshBuildSource Box(Vector3 center, Vector3 size, bool walkable)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = size,
                transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                area = walkable ? 0 : 1, // 1 = Not Walkable
            };
        }

        public static void Build(List<NavMeshBuildSource> sources, Bounds bounds)
        {
            var data = NavMeshBuilder.BuildNavMeshData(Settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogWarning("[FishBreeder] Could not build the store's walkable area; customers will walk in straight lines.");
                return;
            }
            if (instance.valid) instance.Remove();
            instance = NavMesh.AddNavMeshData(data);
        }

        public static void Clear()
        {
            if (instance.valid) instance.Remove();
        }
    }
}
