using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public static class LobbyHeaderView
    {
        public static void Build(RectTransform root, LobbyUiFactory ui, Action<string, string> openPopup)
        {
            RectTransform bar = ui.CreateImage("Top Bar", root, new Color(.025f, .04f, .09f, .9f),
                new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, 96)).rectTransform;
            ui.CreateText("Game Title", "✦  STARFALL", bar, 30, FontStyle.Bold, ui.Theme.White,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(154, 0), new Vector2(270, 60), TextAnchor.MiddleLeft);

            string premiumCurrency = PlayerWallet.PremiumCurrency.ToString("N0");
            CreateCurrency(ui, bar, new Vector2(-604, 0), new Vector2(188, 52), "♦  " + premiumCurrency, ui.Theme.Cyan,
                () => openPopup(PlayerWallet.PremiumCurrencyDisplayName,
                    "보유 " + PlayerWallet.PremiumCurrencyDisplayName + "  " + premiumCurrency +
                    "개\n\n모집과 상점에서 사용할 수 있습니다."));
            CreateCurrency(ui, bar, new Vector2(-404, 0), new Vector2(176, 52), "●  82,500", ui.Theme.Gold,
                () => openPopup("크레딧", "보유 크레딧  82,500\n\n학생 성장과 장비 강화에 사용됩니다."));
            GameObject mail = ui.CreateButton("Open Mail", bar, new Vector2(1, .5f), new Vector2(-196, 0), new Vector2(56, 56),
                string.Empty, 25, new Color(.15f, .2f, .35f, .96f),
                () => openPopup("우편함", "새 우편이 3개 도착했습니다.\n\n· 점검 보상\n· 이벤트 출석 보상\n· 서클 주간 보상"));
            ui.AddIcon(mail, LobbyIcon.Mail, new Vector2(.5f, .5f), Vector2.zero, new Vector2(42, 42));
            ui.CreateBadge(bar, new Vector2(1, .5f), new Vector2(-174, 20), "3");
            GameObject settings = ui.CreateButton("Open Settings", bar, new Vector2(1, .5f), new Vector2(-128, 0), new Vector2(56, 56),
                string.Empty, 24, new Color(.15f, .2f, .35f, .96f),
                () => openPopup("환경 설정", "사운드, 알림, 그래픽과 계정 설정을 관리합니다.\n\n현재 그래픽 품질  HIGH"));
            ui.AddIcon(settings, LobbyIcon.Settings, new Vector2(.5f, .5f), Vector2.zero, new Vector2(44, 44));
            GameObject profile = ui.CreateButton("Open Profile", bar, new Vector2(1, .5f), new Vector2(-58, 0), new Vector2(58, 58),
                string.Empty, 25, ui.Theme.Pink,
                () => openPopup("프로필", "Lv. 42  선생님\n\nUID  120 072 411\n칭호  별을 이끄는 사람"));
            ui.AddIcon(profile, LobbyIcon.Student, new Vector2(.5f, .5f), Vector2.zero, new Vector2(47, 47));
        }

        static void CreateCurrency(LobbyUiFactory ui, Transform parent, Vector2 position, Vector2 size,
            string label, Color accent, Action action)
        {
            GameObject button = ui.CreateButton("Currency " + label, parent, new Vector2(1, .5f), position, size,
                label, 17, new Color(.12f, .16f, .28f, .96f), action);
            button.GetComponentInChildren<Text>().color = accent;
            ui.CreateText("Add Currency", "＋", button.transform, 16, FontStyle.Bold, ui.Theme.White,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-17, 0), new Vector2(28, 28), TextAnchor.MiddleCenter);
        }
    }
}
