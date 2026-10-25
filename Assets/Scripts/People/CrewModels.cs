using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder.People
{
    /// <summary>
    /// The shop's people, from the Meshy crew lineup split into one model each under
    /// Assets/Resources/Crew. They share one texture atlas. Each comes with a simple skeleton that
    /// CrewRig moves by code.
    /// </summary>
    public static class CrewModels
    {
        public const string Folder = "Crew/";
        public const string OwnerMan = "OwnerMan";
        public const string OwnerWoman = "OwnerWoman";
        public const string FishSeller = "FishSeller";
        public static readonly string[] Customers = { "CustomerSunhat", "CustomerKid", "CustomerTourist", "CustomerRocker" };

        /// <summary>Turns the imported models (which face +Z) to face the way their parent faces.</summary>
        const float ModelYaw = 0f;

        static Material material;

        /// <summary>
        /// A character standing at the parent's origin, facing the parent's forward. Returns null
        /// (and the caller keeps its placeholder) if the model is missing.
        /// </summary>
        public static CrewRig Spawn(string id, Transform parent, float scale)
        {
            var prefab = Resources.Load<GameObject>(Folder + id);
            if (prefab == null)
            {
                Debug.LogWarning($"[FishBreeder] Character {id} is missing from Resources/{Folder}.");
                return null;
            }
            var body = new GameObject(id).transform;
            body.SetParent(parent, false);
            body.localScale = Vector3.one * scale;
            var model = Object.Instantiate(prefab, body, false);
            model.name = "Model";
            model.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            Dress(model);
            return body.gameObject.AddComponent<CrewRig>();
        }

        static void Dress(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = Material;
                // The bones are moved by code, so the stored bounds don't cover every pose.
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
        }

        static Material Material
        {
            get
            {
                if (material != null) return material;
                material = MaterialFactory.Create(Color.white, 0.25f);
                material.name = "Crew";
                var albedo = Resources.Load<Texture2D>(Folder + "Crew_Albedo");
                var normal = Resources.Load<Texture2D>(Folder + "Crew_Normal");
                if (albedo != null) material.mainTexture = albedo;
                if (normal != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                return material;
            }
        }
    }

    public enum OwnerLook { Woman, Man }

    /// <summary>Who runs the shop: picked once by the player and remembered on this computer.</summary>
    public static class ShopOwner
    {
        const string Key = "FishBreeder.ShopOwner";

        public static event System.Action Changed;

        public static bool Chosen => PlayerPrefs.HasKey(Key);

        public static OwnerLook Look
        {
            get => PlayerPrefs.GetInt(Key, 0) == 1 ? OwnerLook.Man : OwnerLook.Woman;
            set
            {
                bool changed = !Chosen || value != Look;
                PlayerPrefs.SetInt(Key, value == OwnerLook.Man ? 1 : 0);
                PlayerPrefs.Save();
                if (changed) Changed?.Invoke();
            }
        }

        public static string ModelId => Look == OwnerLook.Man ? CrewModels.OwnerMan : CrewModels.OwnerWoman;
    }
}
