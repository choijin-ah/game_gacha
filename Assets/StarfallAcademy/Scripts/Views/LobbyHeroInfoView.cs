using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public static class LobbyHeroInfoView
    {
        public static void Build(RectTransform root, LobbyUiFactory ui)
        {
            RectTransform nameplate = ui.CreateImage("Hero Nameplate", root, new Color(.02f, .04f, .09f, .91f),
                new Vector2(.36f, 0), new Vector2(.70f, 0), new Vector2(0, 176), new Vector2(0, 74)).rectTransform;
            ui.CreateImage("Hero Accent", nameplate, ui.Theme.Cyan, Vector2.zero, new Vector2(0, 1),
                new Vector2(3, 0), new Vector2(6, -18));
            ui.CreateText("Hero Name", LobbyContent.HeroName + "  ·  " + LobbyContent.HeroRole, nameplate,
                22, FontStyle.Bold, ui.Theme.White, Vector2.zero, Vector2.one, new Vector2(0, 0),
                new Vector2(-52, 0), TextAnchor.MiddleLeft);
            ui.CreateText("Touch Guide", "캐릭터를 터치해 대화", nameplate, 12, FontStyle.Normal, ui.Theme.Muted,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-112, 0), new Vector2(190, 32), TextAnchor.MiddleRight);
        }
    }
}
