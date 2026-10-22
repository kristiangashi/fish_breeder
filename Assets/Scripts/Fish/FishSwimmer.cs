using FishBreeder.Core;
using FishBreeder.Feeding;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Wanders around the tank and chases food when hungry. The fish always moves along
    /// its own forward axis and turns towards its target, which reads as real swimming.
    /// </summary>
    [RequireComponent(typeof(Fish))]
    public class FishSwimmer : MonoBehaviour
    {
        [SerializeField] float turnSpeed = 2.5f;
        [SerializeField] float acceleration = 0.4f;
        [SerializeField] float maxPitch = 35f;
        [SerializeField] float foodSenseRadius = 1.5f;

        Fish fish;
        Vector3 target;
        float retargetIn;
        float currentSpeed;
        FoodPellet targetFood;

        void Awake() => fish = GetComponent<Fish>();

        void Update()
        {
            if (fish.Tank == null) return;
            float dt = Time.deltaTime;
            var phenotype = fish.Phenotype;
            float bodyLength = phenotype.lengthMeters * transform.localScale.x;

            float cruiseSpeed = Mathf.Lerp(0.1f, 0.3f, phenotype.swimSpeed)
                                * fish.Health.SwimSpeedMultiplier
                                * Mathf.Lerp(0.6f, 1f, fish.Growth);
            float desiredSpeed = cruiseSpeed;
            float pitchLimit = maxPitch;

            UpdateFoodTarget();
            if (targetFood != null)
            {
                target = targetFood.transform.position;
                desiredSpeed *= 1.6f;
                pitchLimit = 70f;

                var mouth = transform.position + transform.forward * bodyLength * 0.5f;
                float reach = bodyLength * 0.3f + 0.03f;
                if ((target - mouth).sqrMagnitude < reach * reach)
                {
                    fish.Eat(targetFood);
                    targetFood = null;
                    PickWanderTarget();
                }
            }
            else
            {
                retargetIn -= dt;
                if (retargetIn <= 0f || (target - transform.position).sqrMagnitude < 0.01f)
                    PickWanderTarget();
            }

            var toTarget = target - transform.position;
            if (toTarget.sqrMagnitude > 1e-6f)
            {
                var look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                var euler = look.eulerAngles;
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), -pitchLimit, pitchLimit);
                look = Quaternion.Euler(pitch, euler.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * dt);
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * dt);
            var next = transform.position + transform.forward * currentSpeed * dt;
            transform.position = fish.Tank.Clamp(next, bodyLength * 0.6f);

            fish.Visual.Animate(currentSpeed / Mathf.Max(cruiseSpeed, 0.01f), dt);
        }

        void UpdateFoodTarget()
        {
            if (!fish.WantsFood)
            {
                targetFood = null;
                return;
            }
            if (targetFood == null || targetFood.IsConsumed)
                targetFood = FoodPellet.FindNearest(transform.position, foodSenseRadius);
        }

        void PickWanderTarget()
        {
            target = fish.Tank.RandomPoint(fish.Phenotype.lengthMeters);
            retargetIn = GameRandom.Range(3f, 8f);
        }

        void OnEnable()
        {
            if (fish != null && fish.Tank != null) PickWanderTarget();
        }
    }
}
