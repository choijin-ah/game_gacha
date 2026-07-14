using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class GachaScreen : MonoBehaviour
    {
        LobbyUiFactory ui;
        GachaConfig config;
        GachaBannerData activeBanner;
        List<GachaBannerData> activeBanners = new List<GachaBannerData>();
        GachaService service;
        GachaPickupListView pickupList;
        GachaPullPanelView pullPanel;
        GachaResultView resultView;
        GachaInfoPanel infoPanel;
        LobbyToastOverlay toast;
        CharacterData selectedPickup;
        Image featuredArt;
        Text featuredInitial;
        Text featuredName;
        Text featuredInfo;
        Image portalFlash;
        Text bannerTimerLabel;
        float nextTimerRefresh;
        bool pulling;
        bool changingScene;

        void Awake()
        {
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (Time.unscaledTime >= nextTimerRefresh)
            {
                nextTimerRefresh = Time.unscaledTime + 1f;
                RefreshBannerTimer();
            }
            if (!changingScene && Input.GetKeyDown(KeyCode.Escape))
            {
                if (infoPanel != null && infoPanel.IsOpen) infoPanel.Close();
                else if (resultView != null && resultView.IsOpen) resultView.Close();
                else ReturnToLobby();
            }
        }

        void BuildCanvas()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            scaler.referencePixelsPerUnit = 100;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildScreen()
        {
            RectTransform root = (RectTransform)transform;
            ui = new LobbyUiFactory(new LobbyTheme());
            config = GachaBannerScheduleService.LoadActiveOrLegacy(
                out GachaBannerDatabase bannerDatabase, out activeBanner);
            activeBanners = GachaBannerScheduleService.GetActiveBanners(
                bannerDatabase, ContentTime.UtcNow);
            CharacterDatabase database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            service = activeBanner != null
                ? new GachaService(activeBanner, database)
                : new GachaService(config, database);
            BuildBackground(root);

            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            BuildHeader(safeRoot);
            BuildFeaturedCharacter(safeRoot);
            pullPanel = new GachaPullPanelView(safeRoot, ui, config, service, Pull);
            pickupList = new GachaPickupListView(safeRoot, ui, config, OnPickupSelected);
            portalFlash = ui.CreateImage("Portal Flash", safeRoot, new Color(.92f, .90f, 1f, 0),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            resultView = CreateController<GachaResultView>("Gacha Result Controller", safeRoot);
            resultView.Initialize(safeRoot, ui);
            toast = CreateController<LobbyToastOverlay>("Gacha Toast", safeRoot);
            toast.Initialize(safeRoot, ui);
            infoPanel = CreateController<GachaInfoPanel>("Gacha Information Controller", safeRoot);
            infoPanel.Initialize(safeRoot, ui, config, service);
            pickupList.SelectFirst();
            RefreshBannerTimer();
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Gacha Background", root, ui.Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (activeBanner != null && activeBanner.BannerImage != null)
            {
                background.sprite = activeBanner.BannerImage;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
            }
            if (background.sprite == null)
            {
                Texture2D art = Resources.Load<Texture2D>("Gacha/Art/gacha_portal_v1");
                if (art != null)
                {
                    background.sprite = ui.SpriteFromTexture(art);
                    background.type = Image.Type.Simple;
                }
                else background.color = LobbyTheme.Hex("0B1530");
            }
            ui.CreateImage("Monochrome Grade", root, new Color(.035f, .03f, .045f, .38f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Left Vignette", root, new Color(.002f, .002f, .004f, .62f),
                Vector2.zero, new Vector2(.28f, 1), Vector2.zero, Vector2.zero);
            ui.CreateImage("Right Vignette", root, new Color(.002f, .002f, .004f, .58f),
                new Vector2(.69f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Bottom Vignette", root, new Color(.002f, .002f, .004f, .40f),
                Vector2.zero, new Vector2(1, .28f), Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform root)
        {
            RectTransform bar = ui.CreateImage("Gacha Header", root, new Color(.005f, .005f, .008f, .72f),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 94)).rectTransform;
            ui.CreateButton("Back To Lobby", bar, new Vector2(0, .5f), new Vector2(52, 0), new Vector2(54, 54),
                "‹", 31, GachaGothicStyle.PanelStrong, ReturnToLobby, TextAnchor.MiddleCenter, false);
            ui.CreateText("Gacha Eyebrow", config != null ? config.BannerSubtitle : "RECRUITMENT", bar, 12,
                FontStyle.Normal, GachaGothicStyle.Muted, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(205, 17), new Vector2(260, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Gacha Title", config != null ? config.BannerTitle : "캐릭터 모집", bar, 27,
                FontStyle.Normal, GachaGothicStyle.Silver, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(278, -12), new Vector2(410, 38), TextAnchor.MiddleLeft);
            string typeLabel = activeBanner == null ? "LEGACY"
                : activeBanner.BannerType == GachaBannerType.Standard ? "STANDARD"
                : activeBanner.BannerType == GachaBannerType.Event ? "EVENT" : "PICK UP";
            ui.CreateStatusPill("Banner Type", bar, new Vector2(0, .5f),
                new Vector2(555, -4), typeLabel,
                activeBanner != null && activeBanner.BannerType == GachaBannerType.Standard
                    ? StarfallStatusTone.Info : StarfallStatusTone.Premium);
            ui.CreateText("Gacha Scene Label", "A S T R A L   R E C R U I T M E N T", bar, 12, FontStyle.Normal,
                GachaGothicStyle.Muted,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-125, 0), new Vector2(210, 28), TextAnchor.MiddleRight);
            if (activeBanners.Count > 1)
            {
                int index = Mathf.Max(0, activeBanners.IndexOf(activeBanner));
                ui.CreateButton("Previous Banner", bar, new Vector2(.5f, .5f),
                    new Vector2(-115, 0), new Vector2(42, 42), "‹", 25,
                    GachaGothicStyle.PanelSoft, () => SwitchBanner(-1), TextAnchor.MiddleCenter, false);
                ui.CreateText("Banner Position", (index + 1) + " / " + activeBanners.Count,
                    bar, 12, FontStyle.Bold, GachaGothicStyle.Silver,
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                    new Vector2(150, 30), TextAnchor.MiddleCenter);
                ui.CreateButton("Next Banner", bar, new Vector2(.5f, .5f),
                    new Vector2(115, 0), new Vector2(42, 42), "›", 25,
                    GachaGothicStyle.PanelSoft, () => SwitchBanner(1), TextAnchor.MiddleCenter, false);
            }
            ui.CreateStyledButton("Open Rates", bar, new Vector2(1, .5f),
                new Vector2(-490, 14), new Vector2(126, 36), "확률 상세", 12,
                StarfallButtonStyle.Tab, () => infoPanel?.OpenRates());
            ui.CreateStyledButton("Open History", bar, new Vector2(1, .5f),
                new Vector2(-348, 14), new Vector2(126, 36), "모집 기록", 12,
                StarfallButtonStyle.Tab, () => infoPanel?.OpenHistory());
            bannerTimerLabel = ui.CreateText("Banner Timer", string.Empty, bar, 10,
                FontStyle.Bold, GachaGothicStyle.Muted,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-419, -24),
                new Vector2(300, 22), TextAnchor.MiddleCenter);
            ui.CreateImage("Header Line", bar, GachaGothicStyle.Line, Vector2.zero, new Vector2(1, 0),
                Vector2.zero, new Vector2(0, 1));
        }

        void RefreshBannerTimer()
        {
            if (bannerTimerLabel == null) return;
            DateTime? end = activeBanner?.Schedule?.EndUtc;
            if (!end.HasValue)
            {
                bannerTimerLabel.text = "상시 모집";
                return;
            }
            TimeSpan remaining = end.Value - ContentTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                bannerTimerLabel.text = "모집 종료";
                bannerTimerLabel.color = UrbanFantasyStyle.Danger;
                return;
            }
            bannerTimerLabel.color = remaining.TotalHours <= 24
                ? UrbanFantasyStyle.Warning : GachaGothicStyle.Muted;
            bannerTimerLabel.text = remaining.TotalDays >= 1
                ? "종료까지  " + Mathf.FloorToInt((float)remaining.TotalDays) + "일 "
                    + remaining.Hours.ToString("00") + ":" + remaining.Minutes.ToString("00")
                : "종료까지  " + remaining.Hours.ToString("00") + ":"
                    + remaining.Minutes.ToString("00") + ":" + remaining.Seconds.ToString("00");
        }

        void BuildFeaturedCharacter(RectTransform root)
        {
            RectTransform display = ui.CreateImage("Featured Character Display", root, new Color(.01f, .01f, .015f, .10f),
                new Vector2(.55f, .55f), new Vector2(.55f, .55f), new Vector2(-5, 16), new Vector2(520, 720)).rectTransform;
            featuredArt = ui.CreateImage("Featured Character Art", display, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -20));
            featuredArt.type = Image.Type.Simple;
            featuredArt.preserveAspect = true;
            featuredInitial = ui.CreateText("Featured Initial", "?", display, 110, FontStyle.Bold,
                new Color(1, 1, 1, .42f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            RectTransform plate = ui.CreateImage("Featured Nameplate", root, GachaGothicStyle.Panel,
                new Vector2(.55f, 0), new Vector2(.55f, 0), new Vector2(0, 202), new Vector2(500, 108)).rectTransform;
            GachaGothicStyle.AddBorder(ui, plate);
            ui.CreateImage("Featured Plate Accent", plate, GachaGothicStyle.Silver, new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(2, 0), new Vector2(3, -18));
            featuredName = ui.CreateText("Featured Name", "픽업을 선택하세요", plate, 26, FontStyle.Normal,
                GachaGothicStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(217, 17), new Vector2(390, 34), TextAnchor.MiddleLeft);
            featuredInfo = ui.CreateText("Featured Info", string.Empty, plate, 14, FontStyle.Normal, GachaGothicStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(217, -20), new Vector2(390, 26), TextAnchor.MiddleLeft);
        }

        void OnPickupSelected(CharacterData character)
        {
            selectedPickup = character;
            pullPanel.SetSelected(character);
            if (character == null)
            {
                featuredArt.sprite = null;
                featuredArt.color = Color.clear;
                featuredInitial.text = "?";
                featuredInitial.gameObject.SetActive(true);
                featuredName.text = "픽업을 선택하세요";
                featuredInfo.text = string.Empty;
                return;
            }

            Sprite art = character.GachaArt;
            if (art != null)
            {
                featuredArt.sprite = art;
                featuredArt.color = Color.white;
                featuredInitial.gameObject.SetActive(false);
            }
            else
            {
                featuredArt.sprite = null;
                featuredArt.color = new Color(character.AccentColor.r, character.AccentColor.g,
                    character.AccentColor.b, .14f);
                featuredInitial.text = character.DisplayName.Length > 0 ? character.DisplayName.Substring(0, 1) : "?";
                featuredInitial.gameObject.SetActive(true);
            }
            featuredName.text = character.DisplayName;
            featuredInfo.text = new string('★', Mathf.Min(6, character.Rarity)) + "  ·  " + character.Affiliation;
        }

        void Pull(int count)
        {
            if (pulling) return;
            GachaPullResponse response = service.Pull(count, selectedPickup);
            pullPanel.Refresh();
            if (!response.Success)
            {
                toast.Show(response.Error);
                return;
            }
            StartCoroutine(PlayPullEffect(response));
        }

        IEnumerator PlayPullEffect(GachaPullResponse response)
        {
            pulling = true;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 3.5f)
            {
                float pulse = Mathf.Sin(t * Mathf.PI);
                portalFlash.color = new Color(.92f, .90f, 1f, pulse * .48f);
                yield return null;
            }
            portalFlash.color = new Color(.92f, .90f, 1f, 0);
            resultView.Show(response.Results);
            pulling = false;
        }

        void ReturnToLobby()
        {
            if (changingScene || pulling) return;
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.Lobby);
        }

        void SwitchBanner(int direction)
        {
            if (changingScene || pulling || activeBanners.Count < 2) return;
            int current = Mathf.Max(0, activeBanners.IndexOf(activeBanner));
            int next = (current + direction) % activeBanners.Count;
            if (next < 0) next += activeBanners.Count;
            if (!GachaBannerScheduleService.SelectBanner(activeBanners[next], ContentTime.UtcNow))
            {
                toast?.Show("현재 선택할 수 없는 배너입니다.");
                return;
            }
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.Gacha);
        }

        static RectTransform CreateLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static T CreateController<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }
    }
}
