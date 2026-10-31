using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// Keeps real-time shadows to what the view needs: close in when you're looking into a tank,
    /// the store's width from the store view. A shorter range means a sharper shadow map and
    /// fewer things drawn into it.
    /// </summary>
    public class ShadowRange : MonoBehaviour
    {
        const float TankView = 6f, StoreView = 30f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<ShadowRange>() != null) return;
            new GameObject("Shadow range").AddComponent<ShadowRange>();
        }

        float original;

        void Awake() => original = QualitySettings.shadowDistance;

        // In the editor, quality settings changed in Play would otherwise stick to the project.
        void OnDestroy() => QualitySettings.shadowDistance = original;

        void LateUpdate()
        {
            float wanted = Tank.Focused != null ? TankView : StoreView;
            if (!Mathf.Approximately(QualitySettings.shadowDistance, wanted)) QualitySettings.shadowDistance = wanted;
        }
    }
}
