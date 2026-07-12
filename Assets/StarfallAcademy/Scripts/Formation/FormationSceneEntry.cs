using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // Formation.unity 전용 진입점입니다.
    public sealed class FormationSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<FormationScreen>() != null) return;
            var screen = new GameObject("Formation Screen", typeof(RectTransform));
            screen.AddComponent<FormationScreen>();
        }
    }
}
