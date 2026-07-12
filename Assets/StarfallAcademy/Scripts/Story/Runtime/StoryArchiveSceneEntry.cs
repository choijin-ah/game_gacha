using UnityEngine;

namespace StarfallAcademy.Lobby
{
    /// <summary>Minimal scene entry; the complete archive is built at runtime from StoryDatabase.</summary>
    public sealed class StoryArchiveSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<StoryArchiveScreen>() != null) return;
            var screen = new GameObject("Story Archive Screen", typeof(RectTransform));
            screen.AddComponent<StoryArchiveScreen>();
        }
    }
}
