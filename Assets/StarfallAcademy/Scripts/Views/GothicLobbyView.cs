using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 레퍼런스 이미지와 같은 좌측 레일 + 우측 대시보드 구조를 한 파일에서 조정합니다.
    public static class GothicLobbyView
    {
        static readonly Color Panel = new Color(.018f, .018f, .024f, .82f);
        static readonly Color PanelSoft = new Color(.025f, .025f, .032f, .68f);
        static readonly Color PanelHover = new Color(.10f, .10f, .12f, .92f);
        static readonly Color Line = new Color(.78f, .78f, .82f, .34f);
        static readonly Color Silver = new Color(.84f, .84f, .87f, 1f);
        static readonly Color Muted = new Color(.78f, .78f, .82f, .62f);
        static readonly Color Alert = new Color(.78f, .055f, .045f, 1f);
        const float RightCenter = -320f;
        const float RightWidth = 560f;

        public static void Build(RectTransform root, LobbyUiFactory ui, Action openCharacterArchive,
            Action openStoryArchive, Action openFormation, Action openGacha, Action openShop,
            Action openStageSelect, Action openMissions, Action openSettings, Action<string, string> openPopup,
            Action<string> showToast)
        {
            BuildProfile(root, ui, openPopup);
            BuildCurrencies(root, ui, openGacha, openSettings, openPopup);
            BuildStoryBanner(root, ui, openStoryArchive);
            BuildQuickCards(root, ui, openCharacterArchive, openFormation, openShop, openPopup);
            BuildBattleArea(root, ui, openStoryArchive, openGacha, openStageSelect, openMissions, openPopup);
            BuildSocialButtons(root, ui, openPopup, showToast);
        }

        static void BuildProfile(RectTransform root, LobbyUiFactory ui, Action<string, string> openPopup)
        {
            int accountLevel = PlayerProfileService.CurrentLevel;
            int accountExperience = PlayerProfileService.CurrentExperience;
            int requiredExperience = PlayerProfileService.Default.GetRequiredExperienceForNextLevel(accountLevel);
            float experienceRatio = requiredExperience > 0
                ? Mathf.Clamp01(accountExperience / (float)requiredExperience) : 1f;
            RectTransform panel = ui.CreateImage("Profile Area", root, new Color(0, 0, 0, .18f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(184, -91), new Vector2(344, 150)).rectTransform;

            Image outer = ui.CreateCircleImage("Portrait Ring", panel, Line, new Vector2(0, .5f),
                new Vector2(61, 0), new Vector2(102, 102));
            Image inner = ui.CreateCircleImage("Portrait Mask", outer.transform, new Color(.08f, .075f, .10f, 1),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(90, 90));
            Mask mask = inner.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            CharacterData profileCharacter = FindProfileCharacter();
            if (profileCharacter != null)
            {
                Image portrait = ui.CreateImage("Profile Character", inner.transform, Color.white,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                portrait.sprite = profileCharacter.Portrait;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                UrbanFantasyStyle.ApplyMonochrome(portrait);
            }
            else
            {
                Image portraitIcon = ui.AddIcon(inner.gameObject, LobbyIcon.Student, new Vector2(.5f, .5f),
                    Vector2.zero, new Vector2(66, 66));
                if (portraitIcon != null) portraitIcon.color = new Color(.82f, .82f, .86f, .86f);
            }

            ui.CreateText("Level", "ACCOUNT LV. " + accountLevel, panel, 15, FontStyle.Normal, Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(203, -27), new Vector2(156, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Player Name", LobbyContent.TeacherName, panel, 26, FontStyle.Normal, Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(229, -60), new Vector2(208, 40), TextAnchor.MiddleLeft);
            ui.CreateText("EXP Value", requiredExperience > 0
                    ? accountExperience.ToString("N0") + " / " + requiredExperience.ToString("N0") : "MAX LEVEL",
                panel, 15, FontStyle.Normal, Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(229, -98), new Vector2(208, 25), TextAnchor.MiddleLeft);
            Image track = ui.CreateImage("EXP Track", panel, new Color(1, 1, 1, .16f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(229, -122), new Vector2(208, 3));
            ui.CreateImage("EXP Fill", track.transform, new Color(.88f, .88f, .92f, .88f),
                Vector2.zero, new Vector2(experienceRatio, 1), Vector2.zero, Vector2.zero);
            GameObject add = ui.CreateButton("Profile Detail", panel, new Vector2(1, 0), new Vector2(-16, 19),
                new Vector2(28, 28), "+", 18, PanelHover,
                () =>
                {
                    StaminaSnapshot stamina = StaminaService.Default.GetSnapshot();
                    openPopup("프로필", "ACCOUNT LV. " + PlayerProfileService.CurrentLevel + "  "
                        + LobbyContent.TeacherName + "\n\n행동력  " + stamina.Current + " / " + stamina.Maximum
                        + "\nUID 120 072 411\n칭호  별을 이끄는 사람");
                },
                TextAnchor.MiddleCenter, false);
            AddBorder(ui, add.GetComponent<RectTransform>());
        }

        static CharacterData FindProfileCharacter()
        {
            CharacterDatabase database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            if (database == null) return null;
            foreach (CharacterData character in database.Characters)
                if (character != null && character.Portrait != null)
                    return character;
            return null;
        }

        static void BuildCurrencies(RectTransform root, LobbyUiFactory ui, Action openGacha,
            Action openSettings, Action<string, string> openPopup)
        {
            RectTransform top = ui.CreateImage("Top Utilities", root, new Color(.005f, .005f, .008f, .38f),
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-445, -45), new Vector2(810, 66)).rectTransform;
            string[] values =
            {
                PlayerWallet.SkillMaterials.ToString("N0"),
                PlayerWallet.PremiumCurrency.ToString("N0"),
                PlayerWallet.Credits.ToString("N0")
            };
            string[] symbols = { "◇", "♦", "●" };
            string[] titles = { PlayerWallet.SkillMaterialDisplayName,
                PlayerWallet.PremiumCurrencyDisplayName, "크레딧" };
            float[] x = { -315, -126, 85 };
            float[] widths = { 176, 176, 210 };
            for (int i = 0; i < values.Length; i++)
            {
                int index = i;
                Action action = i == 1
                    ? openGacha
                    : () => openPopup(titles[index], "보유 " + titles[index] + "  " + values[index]);
                GameObject currency = ui.CreateButton("Currency " + titles[i], top, new Vector2(.5f, .5f),
                    new Vector2(x[i], 0), new Vector2(widths[i], 48), symbols[i] + "   " + values[i] + "   +", 17,
                    Panel, action,
                    TextAnchor.MiddleCenter, false);
                AddBorder(ui, currency.GetComponent<RectTransform>());
            }

            CreateUtilityButton(ui, top, 238, LobbyIcon.Mail, "우편함",
                () => openPopup("우편함", "우편함 메뉴입니다."), true);
            CreateUtilityButton(ui, top, 306, LobbyIcon.Schedule, "출석 달력",
                () => openPopup("출석 달력", "출석 달력 메뉴입니다."), false);
            CreateUtilityButton(ui, top, 374, LobbyIcon.Settings, "환경 설정", openSettings, false);
        }

        static void CreateUtilityButton(LobbyUiFactory ui, RectTransform parent, float x, LobbyIcon icon,
            string title, Action action, bool badge)
        {
            GameObject button = ui.CreateButton("Utility " + title, parent, new Vector2(.5f, .5f), new Vector2(x, 0),
                new Vector2(54, 48), string.Empty, 14, Color.clear,
                action, TextAnchor.MiddleCenter, false);
            Image image = ui.AddIcon(button, icon, new Vector2(.5f, .5f), Vector2.zero, new Vector2(36, 36));
            if (image != null) image.color = new Color(.86f, .86f, .88f, .8f);
            if (badge) AddNotification(ui, button.transform, new Vector2(1, 1), new Vector2(-8, -8));
        }

        static void BuildStoryBanner(RectTransform root, LobbyUiFactory ui, Action openStoryArchive)
        {
            GameObject banner = ui.CreateButton("Main Story Banner", root, new Vector2(1, 1),
                new Vector2(RightCenter, -254), new Vector2(RightWidth, 188), string.Empty, 18, Panel,
                openStoryArchive,
                TextAnchor.MiddleCenter, false);
            RectTransform panel = banner.GetComponent<RectTransform>();
            AddBorder(ui, panel);
            ui.CreateImage("Banner Wash", panel, new Color(.22f, .21f, .28f, .22f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateText("Story Eyebrow", "MAIN STORY UPDATE", panel, 13, FontStyle.Normal, Muted,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(50, -37), new Vector2(330, 24), TextAnchor.MiddleCenter);
            ui.CreateText("Story Title", "그림자 너머의 진실", panel, 31, FontStyle.Normal, Silver,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(28, 5), new Vector2(430, 52), TextAnchor.MiddleCenter);
            ui.CreateText("Chapter", "—  C H A P T E R  7  —", panel, 14, FontStyle.Normal, Muted,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(32, 42), new Vector2(330, 28), TextAnchor.MiddleCenter);
            ui.CreateText("Banner Mark", "✦", panel, 58, FontStyle.Normal, new Color(1, 1, 1, .14f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(68, 0), new Vector2(100, 100), TextAnchor.MiddleCenter);
            ui.CreateText("Page Dots", "●  ○  ○  ○", panel, 12, FontStyle.Normal, Silver,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 20), new Vector2(110, 22), TextAnchor.MiddleRight);
        }

        static void BuildQuickCards(RectTransform root, LobbyUiFactory ui, Action openCharacterArchive,
            Action openFormation, Action openShop, Action<string, string> openPopup)
        {
            string[] korean = { "캐릭터", "편성", "가방", "상점" };
            string[] english = { "CHARACTER", "FORMATION", "INVENTORY", "SHOP" };
            LobbyIcon[] icons = { LobbyIcon.Student, LobbyIcon.Formation, LobbyIcon.Cafe, LobbyIcon.Guild };
            for (int i = 0; i < korean.Length; i++)
            {
                int index = i;
                Action action = i == 0 ? openCharacterArchive : i == 1
                    ? openFormation
                    : i == 3 ? openShop
                    : () => openPopup(korean[index], korean[index] + " 화면은 추후 독립 씬으로 연결할 수 있습니다.");
                GameObject card = CreateCard(ui, root, "Quick " + korean[i], new Vector2(1, 1),
                    new Vector2(-534.5f + i * 143f, -469), new Vector2(131, 170),
                    icons[i], korean[i], english[i], action);
                if (i == 1 || i == 2) AddNotification(ui, card.transform, new Vector2(1, 1), new Vector2(-10, -10));
            }
        }

        static void BuildBattleArea(RectTransform root, LobbyUiFactory ui, Action openStoryArchive,
            Action openGacha, Action openStageSelect, Action openMissions,
            Action<string, string> openPopup)
        {
            GameObject battle = ui.CreateButton("Battle", root, new Vector2(1, 0),
                new Vector2(RightCenter, 320), new Vector2(RightWidth, 142), string.Empty, 20, Panel,
                openStageSelect,
                TextAnchor.MiddleCenter, false);
            RectTransform battleRect = battle.GetComponent<RectTransform>();
            AddBorder(ui, battleRect);
            ui.CreateText("Battle Korean", "출 격", battleRect, 36, FontStyle.Normal, Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(105, 10), new Vector2(150, 55), TextAnchor.MiddleLeft);
            ui.CreateText("Battle English", "B A T T L E", battleRect, 13, FontStyle.Normal, Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(111, -33), new Vector2(165, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Battle Emblem", "✦", battleRect, 76, FontStyle.Normal, new Color(.86f, .83f, .92f, .72f),
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-93, 0), new Vector2(130, 110), TextAnchor.MiddleCenter);
            ui.CreateImage("Battle Divider", battleRect, Line, new Vector2(0, 0), new Vector2(.43f, 0),
                new Vector2(38, 20), new Vector2(-70, 1));

            string[] korean = { "기록실", "모집", "업적" };
            string[] english = { "ARCHIVE", "RECRUIT", "ACHIEVEMENT" };
            LobbyIcon[] icons = { LobbyIcon.Mail, LobbyIcon.Summon, LobbyIcon.Mission };
            for (int i = 0; i < korean.Length; i++)
            {
                int index = i;
                Action action = i == 0
                    ? openStoryArchive
                    : i == 1
                    ? openGacha
                    : openMissions;
                GameObject card = CreateCard(ui, root, "Lower " + korean[i], new Vector2(1, 0),
                    new Vector2(-511 + i * 191, 161), new Vector2(178, 136),
                    icons[i], korean[i], english[i], action);
                if (i == 1) AddNotification(ui, card.transform, new Vector2(1, 1), new Vector2(-12, -12));
                if (i == 2 && MissionService.HasClaimableDailyReward())
                    AddNotification(ui, card.transform, new Vector2(1, 1), new Vector2(-12, -12));
            }
        }

        static GameObject CreateCard(LobbyUiFactory ui, RectTransform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size, LobbyIcon icon, string korean, string english, Action action)
        {
            GameObject card = ui.CreateButton(name, parent, anchor, position, size, string.Empty, 16, PanelSoft, action,
                TextAnchor.MiddleCenter, false);
            RectTransform rect = card.GetComponent<RectTransform>();
            AddBorder(ui, rect);
            Image iconImage = ui.AddIcon(card, icon, new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(56, 56));
            if (iconImage != null) iconImage.color = new Color(.78f, .78f, .81f, .72f);
            ui.CreateText("Korean", korean, card.transform, 22, FontStyle.Normal, Silver,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 47), new Vector2(size.x - 12, 34), TextAnchor.MiddleCenter);
            ui.CreateText("English", english, card.transform, 11, FontStyle.Normal, Muted,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 22), new Vector2(size.x - 10, 20), TextAnchor.MiddleCenter);
            return card;
        }

        static void BuildSocialButtons(RectTransform root, LobbyUiFactory ui, Action<string, string> openPopup,
            Action<string> showToast)
        {
            string[] labels = { "♟", "●●●", "↗" };
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                ui.CreateButton("Social " + i, root, new Vector2(0, 0), new Vector2(47 + i * 72, 35),
                    new Vector2(52, 44), labels[i], i == 1 ? 12 : 22, Color.clear,
                    () =>
                    {
                        if (index == 1) openPopup("채팅", "현재 대화 채널에 새 메시지가 없습니다.");
                        else showToast(index == 0 ? "친구 목록" : "공식 커뮤니티");
                    }, TextAnchor.MiddleCenter, false);
            }
        }

        static void AddBorder(LobbyUiFactory ui, RectTransform panel)
        {
            ui.CreateImage("Border Top", panel, Line, new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, new Vector2(0, 1));
            ui.CreateImage("Border Bottom", panel, Line, Vector2.zero, new Vector2(1, 0),
                Vector2.zero, new Vector2(0, 1));
            ui.CreateImage("Border Left", panel, Line, Vector2.zero, new Vector2(0, 1),
                Vector2.zero, new Vector2(1, 0));
            ui.CreateImage("Border Right", panel, Line, new Vector2(1, 0), Vector2.one,
                Vector2.zero, new Vector2(1, 0));
        }

        static void AddNotification(LobbyUiFactory ui, Transform parent, Vector2 anchor, Vector2 position)
        {
            Image dot = ui.CreateImage("Notification", parent, Alert, anchor, anchor, position, new Vector2(13, 13));
            dot.raycastTarget = false;
        }
    }
}
