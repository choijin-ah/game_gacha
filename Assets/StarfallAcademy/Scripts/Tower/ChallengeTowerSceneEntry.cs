using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class ChallengeTowerSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<ChallengeTowerScreen>() != null) return;
            var screen = new GameObject("Challenge Tower Screen", typeof(RectTransform));
            screen.AddComponent<ChallengeTowerScreen>();
        }
    }
}
