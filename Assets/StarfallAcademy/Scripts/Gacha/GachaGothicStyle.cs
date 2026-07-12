using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 가챠 화면에서 함께 쓰는 무채색 패널과 얇은 테두리 규칙입니다.
    public static class GachaGothicStyle
    {
        public static readonly Color Panel = new Color(.012f, .012f, .018f, .88f);
        public static readonly Color PanelSoft = new Color(.025f, .025f, .032f, .76f);
        public static readonly Color PanelStrong = new Color(.035f, .034f, .043f, .96f);
        public static readonly Color Silver = new Color(.88f, .88f, .91f, 1f);
        public static readonly Color Muted = new Color(.80f, .80f, .84f, .58f);
        public static readonly Color Line = new Color(.80f, .80f, .84f, .32f);
        public static readonly Color Highlight = new Color(.92f, .92f, .95f, .16f);
        public static readonly Color Red = new Color(.70f, .055f, .05f, 1f);

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
