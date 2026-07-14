using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class WeeklyBossSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<WeeklyBossScreen>() != null) return;
            var screen = new GameObject("Weekly Boss Screen", typeof(RectTransform));
            screen.AddComponent<WeeklyBossScreen>();
        }
    }
}
