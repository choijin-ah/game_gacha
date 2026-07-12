using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // Lobby.unity 씬의 진입점입니다. 다른 씬에서는 로비 UI가 생성되지 않습니다.
    public sealed class LobbySceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<LobbyScreen>() != null) return;
            var screen = new GameObject("Lobby Screen", typeof(RectTransform));
            screen.AddComponent<LobbyScreen>();
        }
    }
}
