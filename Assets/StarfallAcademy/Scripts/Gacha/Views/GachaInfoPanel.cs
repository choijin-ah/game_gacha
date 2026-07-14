using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class GachaInfoPanel : MonoBehaviour
    {
        const int HistoryPageSize = 10;

        LobbyUiFactory ui;
        GachaConfig config;
        GachaService service;
        GameObject layer;
        CanvasGroup group;
        RectTransform card;
        RectTransform content;
        Text titleLabel;
        Text subtitleLabel;
        GameObject filterButton;
        Text filterLabel;
        GameObject previousButton;
        GameObject nextButton;
        Text pageLabel;
        IReadOnlyList<GachaHistoryEntry> history = Array.Empty<GachaHistoryEntry>();
        int rarityFilter;
        int page;
        Coroutine animation;

        public bool IsOpen => layer != null && layer.activeSelf;

        public void Initialize(RectTransform parent, LobbyUiFactory factory,
            GachaConfig activeConfig, GachaService gachaService)
        {
            ui = factory;
            config = activeConfig;
            service = gachaService;
            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;

            layer = new GameObject("Gacha Information Layer", typeof(RectTransform),
                typeof(CanvasGroup));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
            group = layer.GetComponent<CanvasGroup>();

            Image backdrop = ui.CreateImage("Information Backdrop", layer.transform,
                UrbanFantasyStyle.Backdrop, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, true);
            Button closeArea = backdrop.gameObject.AddComponent<Button>();
            closeArea.transition = Selectable.Transition.None;
            closeArea.onClick.AddListener(Close);
            card = ui.CreateCard("Gacha Information Window", layer.transform,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1320, 820), true);
            card.GetComponent<Image>().raycastTarget = true;
            ui.CreateText("Information Eyebrow", "R E C R U I T M E N T   A R C H I V E",
                card, 11, FontStyle.Bold, UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -35),
                new Vector2(-80, 24), TextAnchor.MiddleLeft);
            titleLabel = ui.CreateText("Information Title", string.Empty, card, 30,
                FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -73),
                new Vector2(-80, 40), TextAnchor.MiddleLeft);
            subtitleLabel = ui.CreateText("Information Subtitle", string.Empty, card, 12,
                FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108),
                new Vector2(-80, 24), TextAnchor.MiddleLeft);
            ui.CreateButton("Close Information", card, new Vector2(1, 1),
                new Vector2(-36, -36), new Vector2(48, 48), "×", 28,
                UrbanFantasyStyle.PanelSoft, Close);
            ui.CreateImage("Information Divider", card, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -136),
                new Vector2(-74, 1));

            var contentObject = new GameObject("Information Content", typeof(RectTransform));
            contentObject.transform.SetParent(card, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(1, 1);
            content.offsetMin = new Vector2(40, 84);
            content.offsetMax = new Vector2(-40, -148);

            filterButton = ui.CreateStyledButton("History Rarity Filter", card,
                new Vector2(0, 0), new Vector2(150, 42), new Vector2(220, 42),
                string.Empty, 12, StarfallButtonStyle.Tab, CycleRarityFilter);
            filterLabel = filterButton.GetComponentInChildren<Text>();
            previousButton = ui.CreateStyledButton("Previous History Page", card,
                new Vector2(.5f, 0), new Vector2(-130, 42), new Vector2(54, 42),
                "‹", 22, StarfallButtonStyle.Icon, PreviousPage);
            nextButton = ui.CreateStyledButton("Next History Page", card,
                new Vector2(.5f, 0), new Vector2(130, 42), new Vector2(54, 42),
                "›", 22, StarfallButtonStyle.Icon, NextPage);
            pageLabel = ui.CreateText("History Page", string.Empty, card, 12,
                FontStyle.Bold, UrbanFantasyStyle.Silver,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 42),
                new Vector2(180, 42), TextAnchor.MiddleCenter);
            layer.SetActive(false);
        }

        public void OpenRates()
        {
            Open(false);
            BuildRates();
        }

        public void OpenHistory()
        {
            history = GachaHistoryService.Load();
            page = 0;
            Open(true);
            BuildHistory();
        }

        void Open(bool historyMode)
        {
            if (animation != null) StopCoroutine(animation);
            filterButton.SetActive(historyMode);
            previousButton.SetActive(historyMode);
            nextButton.SetActive(historyMode);
            pageLabel.gameObject.SetActive(historyMode);
            titleLabel.text = historyMode ? "모집 기록" : "상세 확률 및 천장 규칙";
            subtitleLabel.text = historyMode
                ? "최근 모집 결과는 최대 200건까지 이 기기에 보관됩니다."
                : "표시된 확률은 현재 선택한 배너 설정을 기준으로 계산됩니다.";
            layer.SetActive(true);
            layer.transform.SetAsLastSibling();
            group.interactable = true;
            group.blocksRaycasts = true;
            animation = StartCoroutine(Fade(0f, 1f));
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (animation != null) StopCoroutine(animation);
            group.interactable = false;
            group.blocksRaycasts = false;
            animation = StartCoroutine(HideRoutine());
        }

        void BuildRates()
        {
            ClearContent();
            if (config == null)
            {
                CreateCenteredMessage("활성 모집 설정을 찾을 수 없습니다.");
                return;
            }

            CreateRateRow("5★ 최고 희귀도", config.TopRarityRatePercent,
                UrbanFantasyStyle.Gold, 0);
            CreateRateRow("4★ 희귀도", config.FourStarRatePercent,
                UrbanFantasyStyle.Violet, 1);
            CreateRateRow("3★ 기본 희귀도", config.ThreeStarRatePercent,
                UrbanFantasyStyle.Info, 2);

            RectTransform rules = ui.CreateCard("Pity Rules", content,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 116),
                new Vector2(1120, 196), false);
            ui.CreateText("Rules Heading", "천장 및 확정 규칙", rules, 18,
                FontStyle.Bold, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -30),
                new Vector2(-38, 28), TextAnchor.MiddleLeft);
            string pickup = service != null && service.RequiresPickupSelection
                ? "픽업 비중 " + config.FeaturedSharePercent.ToString("0.###")
                    + "%  ·  선택 픽업 절대 확률 "
                    + config.EffectiveSelectedPickupRatePercent.ToString("0.###") + "%"
                : "상시 배너는 5★ 풀에서 동일 규칙으로 캐릭터를 선택합니다.";
            string rulesText = "소프트 천장 " + config.SoftPityStart + "회부터 매회 +"
                + config.SoftPityBonusPerPullPercent.ToString("0.###") + "%  ·  하드 천장 "
                + config.HardPity + "회\n" + pickup + "\n10회 4★ 이상 확정 "
                + (config.GuaranteeFourStarOnTenPull ? "ON" : "OFF")
                + "  ·  픽업 실패 후 확정 "
                + (config.GuaranteeFeaturedAfterMiss ? "ON" : "OFF");
            Text rulesBody = ui.CreateText("Rules Body", rulesText, rules, 14,
                FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -20),
                new Vector2(-38, -72), TextAnchor.MiddleLeft);
            rulesBody.lineSpacing = 1.25f;
            ui.CreateStatusPill("Current Pity", rules, new Vector2(1, 1),
                new Vector2(-170, -32), "현재 천장 " + (service?.PityCount ?? 0),
                service != null && service.FeaturedGuaranteed
                    ? StarfallStatusTone.Premium : StarfallStatusTone.Info);
        }

        void CreateRateRow(string label, float percent, Color color, int index)
        {
            float y = -36 - index * 86;
            ui.CreateText(label + " Label", label, content, 15, FontStyle.Bold,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(178, y), new Vector2(280, 30), TextAnchor.MiddleLeft);
            ui.CreateProgressBar(label + " Bar", content, new Vector2(0, 1),
                new Vector2(560, y), new Vector2(460, 16), percent / 100f, color);
            ui.CreateText(label + " Value", percent.ToString("0.###") + "%", content,
                15, FontStyle.Bold, color, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-100, y), new Vector2(150, 30), TextAnchor.MiddleRight);
        }

        void BuildHistory()
        {
            ClearContent();
            var filtered = new List<GachaHistoryEntry>();
            for (int i = 0; i < history.Count; i++)
            {
                GachaHistoryEntry entry = history[i];
                if (entry != null && (rarityFilter == 0 || entry.Rarity == rarityFilter))
                    filtered.Add(entry);
            }
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)HistoryPageSize));
            page = Mathf.Clamp(page, 0, pageCount - 1);
            filterLabel.text = rarityFilter == 0 ? "희귀도  ·  전체"
                : "희귀도  ·  " + rarityFilter + "★";
            pageLabel.text = (page + 1) + " / " + pageCount;
            previousButton.GetComponent<Button>().interactable = page > 0;
            nextButton.GetComponent<Button>().interactable = page + 1 < pageCount;

            if (filtered.Count == 0)
            {
                CreateCenteredMessage(rarityFilter == 0
                    ? "아직 저장된 모집 기록이 없습니다."
                    : rarityFilter + "★ 모집 기록이 없습니다.");
                return;
            }

            int start = page * HistoryPageSize;
            int count = Mathf.Min(HistoryPageSize, filtered.Count - start);
            for (int i = 0; i < count; i++)
                CreateHistoryRow(filtered[start + i], i);
        }

        void CreateHistoryRow(GachaHistoryEntry entry, int row)
        {
            float y = -31 - row * 54;
            RectTransform item = ui.CreateImage("History " + entry.EntryId, content,
                row % 2 == 0 ? UrbanFantasyStyle.PanelSoft : UrbanFantasyStyle.Panel,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y),
                new Vector2(1160, 46)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, item, UrbanFantasyStyle.Line);
            Color rarityColor = UrbanFantasyStyle.Rarity(entry.Rarity);
            ui.CreateText("Rarity", new string('★', Mathf.Clamp(entry.Rarity, 0, 6)), item,
                12, FontStyle.Bold, rarityColor, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(78, 0), new Vector2(130, 26), TextAnchor.MiddleLeft);
            ui.CreateText("Character", string.IsNullOrWhiteSpace(entry.CharacterName)
                    ? entry.CharacterId : entry.CharacterName,
                item, 14, FontStyle.Bold, UrbanFantasyStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(280, 0),
                new Vector2(250, 28), TextAnchor.MiddleLeft);
            string flags = (entry.IsNew ? "NEW  " : string.Empty)
                + (entry.IsFeatured ? "PICK UP" : string.Empty);
            ui.CreateText("Flags", flags, item, 10, FontStyle.Bold,
                entry.IsNew ? UrbanFantasyStyle.Success : UrbanFantasyStyle.Gold,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(42, 0),
                new Vector2(180, 26), TextAnchor.MiddleCenter);
            ui.CreateText("Banner", entry.BannerId, item, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-300, 0), new Vector2(300, 26), TextAnchor.MiddleRight);
            string time = entry.PulledAtUtc == DateTime.MinValue ? "-"
                : entry.PulledAtUtc.ToString("yyyy-MM-dd  HH:mm 'UTC'");
            ui.CreateText("Time", time, item, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-100, 0), new Vector2(190, 26), TextAnchor.MiddleRight);
        }

        void CycleRarityFilter()
        {
            rarityFilter = rarityFilter == 0 ? 5 : rarityFilter == 5 ? 4
                : rarityFilter == 4 ? 3 : 0;
            page = 0;
            BuildHistory();
        }

        void PreviousPage()
        {
            if (page <= 0) return;
            page--;
            BuildHistory();
        }

        void NextPage()
        {
            page++;
            BuildHistory();
        }

        void CreateCenteredMessage(string message)
        {
            ui.CreateText("Empty Information", message, content, 18, FontStyle.Normal,
                UrbanFantasyStyle.Muted, Vector2.zero, Vector2.one, Vector2.zero,
                new Vector2(-160, -160), TextAnchor.MiddleCenter);
        }

        void ClearContent()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        IEnumerator Fade(float from, float to)
        {
            group.alpha = from;
            card.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, from);
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 8f)
            {
                group.alpha = Mathf.Lerp(from, to, t);
                card.localScale = Vector3.one * Mathf.Lerp(.96f, 1f,
                    Mathf.Lerp(from, to, t));
                yield return null;
            }
            group.alpha = to;
            card.localScale = Vector3.one;
            animation = null;
        }

        IEnumerator HideRoutine()
        {
            yield return Fade(1f, 0f);
            layer.SetActive(false);
        }
    }
}
