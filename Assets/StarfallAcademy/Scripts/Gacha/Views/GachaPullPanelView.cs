using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public sealed class GachaPullPanelView
    {
        readonly GachaConfig config;
        readonly GachaService service;
        readonly Text selectedNameLabel;
        readonly Text probabilityLabel;
        readonly Text pityLabel;
        readonly Text currencyLabel;
        readonly Button singleButton;
        readonly Button tenButton;
        CharacterData selected;

        public GachaPullPanelView(RectTransform parent, LobbyUiFactory ui, GachaConfig config,
            GachaService service, Action<int> onPull)
        {
            this.config = config;
            this.service = service;
            RectTransform panel = ui.CreateImage("Gacha Pull Panel", parent, GachaGothicStyle.Panel,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-330, 224), new Vector2(590, 370)).rectTransform;
            GachaGothicStyle.AddBorder(ui, panel);
            ui.CreateText("Pull Eyebrow", "R E C R U I T M E N T", panel, 11, FontStyle.Normal,
                GachaGothicStyle.Muted,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -27), new Vector2(-38, 22), TextAnchor.MiddleLeft);
            selectedNameLabel = ui.CreateText("Selected Pickup", "픽업을 선택하세요", panel, 24, FontStyle.Normal,
                GachaGothicStyle.Silver, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60),
                new Vector2(-38, 38), TextAnchor.MiddleLeft);
            probabilityLabel = ui.CreateText("Probability", string.Empty, panel, 14, FontStyle.Normal,
                GachaGothicStyle.Muted,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -105), new Vector2(-38, 28), TextAnchor.MiddleLeft);
            pityLabel = ui.CreateText("Pity", string.Empty, panel, 14, FontStyle.Normal, GachaGothicStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -137), new Vector2(-38, 28), TextAnchor.MiddleLeft);
            currencyLabel = ui.CreateText("Gacha Currency", string.Empty, panel, 17, FontStyle.Normal,
                GachaGothicStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -180), new Vector2(-38, 30), TextAnchor.MiddleRight);
            ui.CreateImage("Pull Divider", panel, GachaGothicStyle.Line, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -205), new Vector2(-38, 2));

            GameObject single = ui.CreateButton("Single Pull", panel, new Vector2(0, 0), new Vector2(155, 78),
                new Vector2(260, 112), "1회 모집\n♦  " + (config != null ? config.SinglePullCost.ToString("N0") : "-"),
                18, GachaGothicStyle.PanelStrong, () => onPull(1), TextAnchor.MiddleCenter, false);
            GameObject ten = ui.CreateButton("Ten Pull", panel, new Vector2(1, 0), new Vector2(-155, 78),
                new Vector2(260, 112), "10회 모집\n♦  " + (config != null ? config.TenPullCost.ToString("N0") : "-"),
                18, new Color(.18f, .18f, .21f, .98f), () => onPull(10), TextAnchor.MiddleCenter, false);
            GachaGothicStyle.AddBorder(ui, single.GetComponent<RectTransform>());
            GachaGothicStyle.AddBorder(ui, ten.GetComponent<RectTransform>(), new Color(.92f, .92f, .95f, .56f));
            singleButton = single.GetComponent<Button>();
            tenButton = ten.GetComponent<Button>();
            Refresh();
        }

        public void SetSelected(CharacterData character)
        {
            selected = character;
            Refresh();
        }

        public void Refresh()
        {
            selectedNameLabel.text = selected != null ? selected.DisplayName + "  PICK UP" : "픽업을 선택하세요";
            if (config != null)
            {
                probabilityLabel.text = "5★ 기본 " + config.TopRarityRatePercent.ToString("0.###") + "%  ·  선택 픽업 " +
                    config.FeaturedSharePercent.ToString("0.##") + "%  (절대 " +
                    config.EffectiveSelectedPickupRatePercent.ToString("0.###") + "%)";
                probabilityLabel.text += "  ·  4★ "
                    + config.FourStarRatePercent.ToString("0.###") + "%  ·  3★ "
                    + config.ThreeStarRatePercent.ToString("0.###") + "%";
                pityLabel.text = "천장까지 " + Mathf.Max(0, config.HardPity - service.PityCount) + "회" +
                    (service.FeaturedGuaranteed ? "  ·  다음 5★ 픽업 확정" : string.Empty);
            }
            else
            {
                probabilityLabel.text = "GachaConfig가 없습니다";
                pityLabel.text = string.Empty;
            }
            currencyLabel.text = "보유 " + PlayerWallet.PremiumCurrencyDisplayName + "  ♦  " +
                service.Currency.ToString("N0");
            bool canPull = selected != null && config != null;
            singleButton.interactable = canPull;
            tenButton.interactable = canPull;
        }
    }
}
