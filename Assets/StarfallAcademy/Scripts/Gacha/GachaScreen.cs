using System.Collections;
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
        GachaService service;
        GachaPickupListView pickupList;
        GachaPullPanelView pullPanel;
        GachaResultView resultView;
        LobbyToastOverlay toast;
        CharacterData selectedPickup;
        Image featuredArt;
        Text featuredInitial;
        Text featuredName;
        Text featuredInfo;
        Image portalFlash;
        bool pulling;
        bool changingScene;

        void Awake()
        {
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (!changingScene && Input.GetKeyDown(KeyCode.Escape))
            {
                if (resultView != null && resultView.IsOpen) resultView.Close();
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
            config = Resources.Load<GachaConfig>("Data/GachaConfig");
            CharacterDatabase database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            service = new GachaService(config, database);
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
            pickupList.SelectFirst();
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Gacha Background", root, ui.Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Gacha/Art/gacha_portal_v1");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.type = Image.Type.Simple;
            }
            else
                background.color = LobbyTheme.Hex("0B1530");
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
            ui.CreateText("Gacha Scene Label", "A S T R A L   R E C R U I T M E N T", bar, 12, FontStyle.Normal,
                GachaGothicStyle.Muted,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-125, 0), new Vector2(210, 28), TextAnchor.MiddleRight);
            ui.CreateImage("Header Line", bar, GachaGothicStyle.Line, Vector2.zero, new Vector2(1, 0),
                Vector2.zero, new Vector2(0, 1));
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
            SceneManager.LoadScene(SceneNames.Lobby);
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
