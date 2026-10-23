using UnityEngine;

namespace FishBreeder.Core
{
    /// <summary>
    /// Camera filter used while looking into a tank (Built-in pipeline image effect).
    /// Turned on and off by the store view; follows the focused tank's water colour.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class UnderwaterFilter : MonoBehaviour
    {
        Material material;
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int TintAmountId = Shader.PropertyToID("_TintAmount");

        void Awake()
        {
            var shader = Shader.Find("Hidden/FishBreeder/UnderwaterFilter");
            if (shader != null) material = new Material(shader);
            else enabled = false;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var tank = Tank.Focused;
            if (material == null || tank == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            material.SetColor(TintId, tank.WaterColor);
            material.SetFloat(TintAmountId, Mathf.Lerp(0.25f, 0.06f, tank.WaterHealth));
            Graphics.Blit(source, destination, material);
        }
    }
}
