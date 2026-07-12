using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // CharacterArchive.unity 전용 진입점입니다.
    public sealed class CharacterArchiveSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<CharacterArchiveScreen>() != null) return;
            var screen = new GameObject("Character Archive Screen", typeof(RectTransform));
            screen.AddComponent<CharacterArchiveScreen>();
        }
    }
}
