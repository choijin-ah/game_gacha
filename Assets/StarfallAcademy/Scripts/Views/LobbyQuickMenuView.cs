using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public static class LobbyQuickMenuView
    {
        public static void Build(RectTransform root, LobbyUiFactory ui, Action openFormation,
            Action<string, string> openPopup)
        {
            RectTransform panel = ui.CreateImage("Quick Menu", root, new Color(.035f, .06f, .13f, .88f),
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-165, -30), new Vector2(278, 532)).rectTransform;
            ui.CreateText("Quick Menu Title", "QUICK MENU", panel, 13, FontStyle.Bold, ui.Theme.Cyan,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -31), new Vector2(-36, 30), TextAnchor.MiddleLeft);

            for (int i = 0; i < LobbyContent.QuickMenus.Length; i++)
            {
                MenuInfo item = LobbyContent.QuickMenus[i];
                float y = -103 - i * 96;
                Action action = item.Icon == LobbyIcon.Formation
                    ? openFormation
                    : () => openPopup(item.PopupTitle, item.PopupBody);
                GameObject button = ui.CreateButton("Quick " + item.Label, panel, new Vector2(.5f, 1),
                    new Vector2(0, y), new Vector2(242, 82), item.Label, 21,
                    new Color(.10f, .15f, .27f, .96f), action, TextAnchor.MiddleLeft);
                button.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(78, 0);
                ui.AddIcon(button, item.Icon, new Vector2(0, .5f), new Vector2(43, 0), new Vector2(58, 58));
            }
            ui.CreateBadge(panel, new Vector2(1, 1), new Vector2(-18, -252), "!");
        }
    }
}
