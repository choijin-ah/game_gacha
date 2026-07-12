using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 모든 런타임 씬이 공유하는 어반 판타지 UI 규칙입니다.
    public static class UrbanFantasyStyle
    {
        static Material monochromeMaterial;
        public static readonly Color Backdrop = new Color(.003f, .003f, .006f, .86f);
        public static readonly Color Panel = new Color(.012f, .012f, .018f, .88f);
        public static readonly Color PanelSoft = new Color(.025f, .025f, .032f, .76f);
        public static readonly Color PanelStrong = new Color(.035f, .034f, .043f, .96f);
        public static readonly Color Silver = new Color(.88f, .88f, .91f, 1f);
        public static readonly Color Muted = new Color(.80f, .80f, .84f, .58f);
        public static readonly Color Line = new Color(.80f, .80f, .84f, .32f);
        public static readonly Color StrongLine = new Color(.90f, .90f, .93f, .54f);
        public static readonly Color Highlight = new Color(.92f, .92f, .95f, .15f);
        public static readonly Color Alert = new Color(.72f, .045f, .04f, 1f);
        public static readonly Color Gold = new Color(.84f, .73f, .49f, 1f);

        public static void ApplyMonochrome(Image image)
        {
            if (image == null) return;
            if (monochromeMaterial == null)
            {
                Shader shader = Shader.Find("Starfall/UI Monochrome");
                if (shader == null) return;
                monochromeMaterial = new Material(shader)
                {
                    name = "Urban Fantasy Monochrome UI",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            image.material = monochromeMaterial;
        }

        public static void AddBorder(LobbyUiFactory ui, RectTransform panel, Color? color = null)
        {
            Color line = color ?? Line;
            AddLine(ui, panel, "Border Top", line, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1));
            AddLine(ui, panel, "Border Bottom", line, Vector2.zero, new Vector2(1, 0), new Vector2(0, 1));
            AddLine(ui, panel, "Border Left", line, Vector2.zero, new Vector2(0, 1), new Vector2(1, 0));
            AddLine(ui, panel, "Border Right", line, new Vector2(1, 0), Vector2.one, new Vector2(1, 0));
        }

        static void AddLine(LobbyUiFactory ui, RectTransform panel, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            Image line = ui.CreateImage(name, panel, color, anchorMin, anchorMax, Vector2.zero, size);
            line.raycastTarget = false;
        }
    }
}
