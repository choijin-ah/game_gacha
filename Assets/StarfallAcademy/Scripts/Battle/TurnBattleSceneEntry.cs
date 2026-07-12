using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class TurnBattleSceneEntry : MonoBehaviour
    {
        void Awake()
        {
            if (FindAnyObjectByType<TurnBattleScreen>() != null) return;
            var screen = new GameObject("Turn Battle Screen", typeof(RectTransform));
            screen.AddComponent<TurnBattleScreen>();
        }
    }
}
