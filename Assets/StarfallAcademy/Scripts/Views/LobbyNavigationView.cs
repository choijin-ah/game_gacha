using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public static class LobbyNavigationView
    {
        public static void Build(RectTransform root, LobbyUiFactory ui, Action openGacha,
            Action<string, string> openPopup, Action<string> showToast)
        {
            RectTransform nav = ui.CreateImage("Bottom Navigation", root, new Color(.02f, .035f, .08f, .97f),
                Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 106)).rectTransform;

            for (int i = 0; i < LobbyContent.Navigation.Length; i++)
            {
                NavigationInfo item = LobbyContent.Navigation[i];
                bool selected = i == 0;
                float x = .36f + i * .11f;
                Color color = selected ? new Color(.10f, .38f, .50f, .98f) : new Color(.07f, .10f, .18f, .92f);
                Action action = selected
                    ? () => showToast("현재 로비입니다")
                    : i == 2
                        ? openGacha
                        : () => openPopup(item.Label, item.Label + " 화면은 다음 씬으로 연결할 수 있습니다.\n\n현재는 로비 UI 확인용 팝업입니다.");
                GameObject button = ui.CreateButton("Navigation " + item.Label, nav, new Vector2(x, .5f), Vector2.zero,
                    new Vector2(172, 78), item.Icon + "\n" + item.Label, 18, color, action);
                if (i == 1 || i == 2)
                {
                    Text label = button.GetComponentInChildren<Text>();
                    label.text = item.Label;
                    label.alignment = TextAnchor.LowerCenter;
                    label.rectTransform.offsetMin = new Vector2(0, 5);
                    label.rectTransform.offsetMax = new Vector2(0, -48);
                    ui.AddIcon(button, i == 1 ? LobbyIcon.Mission : LobbyIcon.Summon,
                        new Vector2(.5f, .5f), new Vector2(0, 13), new Vector2(46, 46));
                }
                if (item.IsNew)
                    ui.CreateBadge(nav, new Vector2(x, .5f), new Vector2(64, 28), "NEW");
            }
        }
    }
}
