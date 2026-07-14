using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 모든 런타임 씬이 공유하는 어반 판타지 UI 규칙입니다.
    public static class UrbanFantasyStyle
    {
        static Material monochromeMaterial;
        public static readonly Color Backdrop = new Color(.015f, .025f, .065f, .90f);
        public static readonly Color Panel = new Color(.035f, .055f, .12f, .90f);
        public static readonly Color PanelSoft = new Color(.065f, .09f, .17f, .82f);
        public static readonly Color PanelStrong = new Color(.075f, .095f, .19f, .97f);
        public static readonly Color Silver = new Color(.94f, .95f, .99f, 1f);
        public static readonly Color Muted = new Color(.65f, .68f, .77f, .72f);
        public static readonly Color Line = new Color(.33f, .78f, .96f, .28f);
        public static readonly Color StrongLine = new Color(.57f, .48f, 1f, .62f);
        public static readonly Color Highlight = new Color(.33f, .84f, 1f, .16f);
        public static readonly Color Alert = new Color(.90f, .22f, .34f, 1f);
        public static readonly Color Gold = new Color(.91f, .79f, .47f, 1f);
        public static readonly Color Violet = new Color(.55f, .42f, 1f, 1f);
        public static readonly Color Cyan = new Color(.33f, .84f, 1f, 1f);
        public static readonly Color Success = new Color(.25f, .84f, .64f, 1f);
        public static readonly Color Warning = new Color(.94f, .61f, .26f, 1f);
        public static readonly Color Danger = new Color(.94f, .30f, .36f, 1f);
        public static readonly Color Info = new Color(.36f, .70f, 1f, 1f);

        public static Color Rarity(int rarity)
        {
            if (rarity >= 5) return Gold;
            if (rarity == 4) return Violet;
            return new Color(.52f, .76f, .94f, 1f);
        }

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
