using FishBreeder.Core;
using FishBreeder.Genetics;
using UnityEngine;

namespace FishBreeder
{
    public static class FishFactory
    {
        public static Fish Spawn(Genome genome, int generation, float growth, Tank tank, Vector3 position, string parents = null)
        {
            var go = new GameObject("Fish");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, GameRandom.Range(0f, 360f), 0f);
            var fish = go.AddComponent<Fish>(); // RequireComponent adds health, swimmer and visual
            fish.Initialize(genome, generation, growth, tank, parents);
            return fish;
        }
    }
}
