using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class StageSelectSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<StageSelectScreen>() != null) return;
            var screen = new GameObject("Stage Select Screen", typeof(RectTransform));
            screen.AddComponent<StageSelectScreen>();
        }
    }
}
