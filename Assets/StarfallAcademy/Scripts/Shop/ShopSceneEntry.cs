using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // Shop.unity 전용 진입점입니다.
    public sealed class ShopSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<ShopScreen>() != null) return;
            var screen = new GameObject("Shop Screen", typeof(RectTransform));
            screen.AddComponent<ShopScreen>();
        }
    }
}
