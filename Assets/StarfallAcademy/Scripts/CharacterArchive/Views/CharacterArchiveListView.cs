using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public sealed class CharacterArchiveListView
    {
        readonly List<CharacterData> characters = new List<CharacterData>();
        readonly Dictionary<CharacterData, Image> selectionMarks = new Dictionary<CharacterData, Image>();
        readonly Action<CharacterData, int> onSelected;
        readonly LobbyUiFactory ui;
        CharacterData selected;

        public CharacterArchiveListView(RectTransform workspace, LobbyUiFactory ui, CharacterDatabase database,
            Action<CharacterData, int> onSelected)
        {
            this.ui = ui;
            this.onSelected = onSelected;
            Build(workspace, database);
        }

        public void SelectFirst()
        {
            Select(characters.Count > 0 ? characters[0] : null);
        }

        void Build(RectTransform workspace, CharacterDatabase database)
        {
            RectTransform panel = ui.CreateImage("Character List Panel", workspace, UrbanFantasyStyle.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-585, -32),
                new Vector2(500, 760)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("List Title", "인물 목록", panel, 20, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(88, -36),
                new Vector2(140, 30), TextAnchor.MiddleLeft);
            ui.CreateText("List Index", "I N D E X", panel, 10, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-65, -36),
                new Vector2(110, 22), TextAnchor.MiddleRight);
            ui.CreateImage("List Divider", panel, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -64), new Vector2(-30, 1));

            Image viewportImage = ui.CreateImage("Character List Viewport", panel, Color.clear,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -34),
                new Vector2(468, 660), true);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Character List Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportImage.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(3, 3, 3, 3);
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
            scroll.scrollSensitivity = 30;

            var owned = new List<CharacterData>();
            var locked = new List<CharacterData>();
            if (database != null)
            {
                foreach (CharacterData character in database.Characters)
                {
                    if (character == null) continue;
                    if (CharacterProgressionService.IsOwned(character)) owned.Add(character);
                    else locked.Add(character);
                }
            }

            if (owned.Count > 0)
            {
                CreateSection(content, "보유 캐릭터", "O W N E D", owned.Count);
                foreach (CharacterData character in owned)
                {
                    characters.Add(character);
                    CreateRow(content, character, characters.Count - 1, true);
                }
            }
            if (locked.Count > 0)
            {
                CreateSection(content, "미보유 캐릭터", "N O T   O W N E D", locked.Count);
                foreach (CharacterData character in locked)
                {
                    characters.Add(character);
                    CreateRow(content, character, characters.Count - 1, false);
                }
            }

            if (characters.Count == 0)
                ui.CreateText("Empty Archive", "등록된 캐릭터가 없습니다.\n\nStarfall > Data > Character Database\n에서 캐릭터를 추가하세요.",
                    content, 16, FontStyle.Normal, UrbanFantasyStyle.Muted,
                    new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -120),
                    new Vector2(420, 170), TextAnchor.MiddleCenter);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        void CreateSection(RectTransform content, string korean, string english, int count)
        {
            RectTransform section = ui.CreateImage("Section " + korean, content, Color.clear,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(456, 38)).rectTransform;
            LayoutElement element = section.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 38;
            ui.CreateText("Section Label", korean + "  " + count.ToString("00"), section, 13,
                FontStyle.Normal, UrbanFantasyStyle.Silver, Vector2.zero, new Vector2(.5f, 1),
                new Vector2(12, 0), Vector2.zero, TextAnchor.MiddleLeft);
            ui.CreateText("Section English", english, section, 9, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(.5f, 0), Vector2.one, new Vector2(-12, 0), Vector2.zero, TextAnchor.MiddleRight);
            ui.CreateImage("Section Line", section, UrbanFantasyStyle.Line,
                Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(-8, 1));
        }

        void CreateRow(RectTransform content, CharacterData character, int index, bool owned)
        {
            GameObject row = ui.CreateButton("Archive " + character.DisplayName, content, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(456, 108), string.Empty, 16, UrbanFantasyStyle.PanelSoft,
                () => Select(character));
            LayoutElement element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 108;
            UrbanFantasyStyle.AddBorder(ui, row.GetComponent<RectTransform>());
            Image mark = ui.CreateImage("Selected", row.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
            mark.transform.SetAsFirstSibling();
            selectionMarks[character] = mark;

            Image portrait = ui.CreateImage("Portrait", row.transform,
                new Color(character.AccentColor.r, character.AccentColor.g, character.AccentColor.b, .18f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(58, 0), new Vector2(82, 82));
            if (character.Portrait != null)
            {
                portrait.sprite = character.Portrait;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.color = owned ? Color.white : new Color(.34f, .34f, .37f, .82f);
                if (!owned) UrbanFantasyStyle.ApplyMonochrome(portrait);
            }
            else
            {
                string initial = character.DisplayName.Length > 0 ? character.DisplayName.Substring(0, 1) : "?";
                ui.CreateText("Initial", initial, portrait.transform, 30, FontStyle.Normal,
                    UrbanFantasyStyle.Silver, Vector2.zero, Vector2.one, Vector2.zero,
                    Vector2.zero, TextAnchor.MiddleCenter);
            }
            ui.CreateText("Index", (index + 1).ToString("000"), row.transform, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(125, -22), new Vector2(64, 20), TextAnchor.MiddleLeft);
            ui.CreateText("Name", character.DisplayName, row.transform, 20, FontStyle.Normal,
                owned ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(264, 8), new Vector2(292, 32), TextAnchor.MiddleLeft);
            string info = owned
                ? new string('★', Mathf.Min(6, character.Rarity)) + "  ·  " + character.Affiliation
                : "LOCKED  ·  미획득";
            ui.CreateText("Info", info, row.transform, 12, FontStyle.Normal,
                owned ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(264, -26),
                new Vector2(292, 22), TextAnchor.MiddleLeft);
            if (!owned)
                ui.CreateText("Lock", "◆", row.transform, 18, FontStyle.Normal,
                    new Color(1, 1, 1, .22f), new Vector2(1, .5f), new Vector2(1, .5f),
                    new Vector2(-24, 0), new Vector2(28, 28), TextAnchor.MiddleCenter);
        }

        void Select(CharacterData character)
        {
            selected = character;
            foreach (KeyValuePair<CharacterData, Image> pair in selectionMarks)
                pair.Value.color = pair.Key == selected ? UrbanFantasyStyle.Highlight : Color.clear;
            int index = character != null ? characters.IndexOf(character) : -1;
            onSelected?.Invoke(character, index);
        }
    }
}
