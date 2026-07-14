using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 가챠 화면의 호환 별칭입니다. 실제 색상 규칙은 공통 테마에서 관리합니다.
    public static class GachaGothicStyle
    {
        public static readonly Color Panel = UrbanFantasyStyle.Panel;
        public static readonly Color PanelSoft = UrbanFantasyStyle.PanelSoft;
        public static readonly Color PanelStrong = UrbanFantasyStyle.PanelStrong;
        public static readonly Color Silver = UrbanFantasyStyle.Silver;
        public static readonly Color Muted = UrbanFantasyStyle.Muted;
        public static readonly Color Line = UrbanFantasyStyle.Line;
        public static readonly Color Highlight = UrbanFantasyStyle.Highlight;
        public static readonly Color Red = UrbanFantasyStyle.Alert;

        public static void AddBorder(LobbyUiFactory ui, RectTransform panel, Color? color = null)
        {
            Color line = color ?? Line;
            CreateLine(ui, panel, "Border Top", line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
            CreateLine(ui, panel, "Border Bottom", line, Vector2.zero, new Vector2(1, 0), new Vector2(0, 1));
            CreateLine(ui, panel, "Border Left", line, Vector2.zero, new Vector2(0, 1), new Vector2(1, 0));
            CreateLine(ui, panel, "Border Right", line, new Vector2(1, 0), Vector2.one, new Vector2(1, 0));
        }

        static void CreateLine(LobbyUiFactory ui, RectTransform panel, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            Image line = ui.CreateImage(name, panel, color, anchorMin, anchorMax, Vector2.zero, size);
            line.raycastTarget = false;
        }
    }
}
