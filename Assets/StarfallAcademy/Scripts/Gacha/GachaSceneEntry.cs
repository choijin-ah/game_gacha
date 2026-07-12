using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // Gacha.unity 전용 진입점입니다.
    public sealed class GachaSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<GachaScreen>() != null) return;
            var screen = new GameObject("Gacha Screen", typeof(RectTransform));
            screen.AddComponent<GachaScreen>();
        }
    }
}
