using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public sealed class GachaPickupListView
    {
        readonly Dictionary<CharacterData, Image> selectionMarks = new Dictionary<CharacterData, Image>();
        readonly List<CharacterData> validPickups = new List<CharacterData>();
        readonly Action<CharacterData> onSelected;
        readonly LobbyUiFactory ui;

        public CharacterData Selected { get; private set; }

        public GachaPickupListView(RectTransform parent, LobbyUiFactory ui, GachaConfig config,
            Action<CharacterData> onSelected)
        {
            this.ui = ui;
            this.onSelected = onSelected;
            Build(parent, config);
        }

        public void SelectFirst()
        {
            Select(validPickups.Count > 0 ? validPickups[0] : null);
        }

        void Build(RectTransform parent, GachaConfig config)
        {
            RectTransform panel = ui.CreateImage("Pickup Selector", parent, GachaGothicStyle.Panel,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(240, -16), new Vector2(430, 830)).rectTransform;
            GachaGothicStyle.AddBorder(ui, panel);
            ui.CreateText("Pickup Eyebrow", "S E L E C T   P I C K U P", panel, 11, FontStyle.Normal,
                GachaGothicStyle.Muted,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -28), new Vector2(-38, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Pickup Title", "픽업 캐릭터 선택", panel, 26, FontStyle.Normal, GachaGothicStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -63), new Vector2(-38, 38), TextAnchor.MiddleLeft);
            ui.CreateText("Pickup Guide", "선택한 캐릭터의 픽업 확률이 적용됩니다.", panel, 13, FontStyle.Normal,
                GachaGothicStyle.Muted, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96),
                new Vector2(-38, 24), TextAnchor.MiddleLeft);
            ui.CreateImage("Title Divider", panel, GachaGothicStyle.Line, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -120), new Vector2(-38, 1));

            Image viewportImage = ui.CreateImage("Pickup Viewport", panel, Color.clear,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -66), new Vector2(394, 640), true);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Pickup Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportImage.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewportImage.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28f;

            if (config != null)
            {
                foreach (CharacterData character in config.PickupCharacters)
                {
                    if (character == null || character.Rarity < 5 || validPickups.Contains(character)) continue;
                    validPickups.Add(character);
                    CreatePickupCard(content, character);
                }
            }

            if (validPickups.Count == 0)
                ui.CreateText("Empty Pickup", "등록된 5★ 픽업 캐릭터가 없습니다.\n\nStarfall > Data > Gacha Banner Database\n에서 픽업을 추가하세요.",
                    content, 16, FontStyle.Normal, ui.Theme.Muted, new Vector2(.5f, 1), new Vector2(.5f, 1),
                    new Vector2(0, -100), new Vector2(350, 150), TextAnchor.MiddleCenter);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        void CreatePickupCard(RectTransform parent, CharacterData character)
        {
            GameObject button = ui.CreateButton("Pickup " + character.DisplayName, parent, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(380, 116), string.Empty, 16, GachaGothicStyle.PanelSoft,
                () => Select(character), TextAnchor.MiddleCenter, false);
            LayoutElement layout = button.AddComponent<LayoutElement>();
            layout.preferredHeight = 116;
            GachaGothicStyle.AddBorder(ui, button.GetComponent<RectTransform>());
            Image mark = ui.CreateImage("Pickup Selected", button.transform, new Color(1f, 1f, 1f, 0),
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
            mark.transform.SetAsFirstSibling();
            selectionMarks[character] = mark;

            Image portrait = ui.CreateImage("Pickup Portrait", button.transform,
                new Color(character.AccentColor.r, character.AccentColor.g, character.AccentColor.b, .24f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(62, 0), new Vector2(92, 92));
            if (character.Portrait != null)
            {
                portrait.sprite = character.Portrait;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.color = Color.white;
            }
            else
            {
                string initial = character.DisplayName.Length > 0 ? character.DisplayName.Substring(0, 1) : "?";
                ui.CreateText("Initial", initial, portrait.transform, 30, FontStyle.Bold, ui.Theme.White,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            }

            ui.CreateText("Pickup Name", character.DisplayName, button.transform, 19, FontStyle.Normal,
                GachaGothicStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(211, 24), new Vector2(210, 30), TextAnchor.MiddleLeft);
            ui.CreateText("Pickup Info", new string('★', Mathf.Min(6, character.Rarity)) + "  ·  " + RoleLabel(character.Role),
                button.transform, 13, FontStyle.Normal, GachaGothicStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(211, -12), new Vector2(210, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Pickup Affiliation", character.Affiliation, button.transform, 12, FontStyle.Normal,
                GachaGothicStyle.Muted, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(211, -37),
                new Vector2(210, 20), TextAnchor.MiddleLeft);
        }

        void Select(CharacterData character)
        {
            Selected = character;
            foreach (KeyValuePair<CharacterData, Image> pair in selectionMarks)
                pair.Value.color = pair.Key == Selected
                    ? GachaGothicStyle.Highlight
                    : new Color(1f, 1f, 1f, 0);
            onSelected?.Invoke(character);
        }

        static string RoleLabel(CharacterRole role)
        {
            switch (role)
            {
                case CharacterRole.Striker: return "스트라이커";
                case CharacterRole.Support: return "서포터";
                case CharacterRole.Tank: return "탱커";
                case CharacterRole.Healer: return "힐러";
                default: return "스페셜";
            }
        }
    }
}
