using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ShopScreen : MonoBehaviour
    {
        const int FreeCrystalAmount = 1600;

        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        Text balanceValue;
        bool changingScene;

        void Awake()
        {
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (!changingScene && Input.GetKeyDown(KeyCode.Escape))
                ReturnToLobby();
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
            BuildBackground(root);

            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            BuildHeader(safeRoot);
            BuildShopContent(safeRoot);

            toast = CreateController<LobbyToastOverlay>("Shop Toast", safeRoot);
            toast.Initialize(safeRoot, ui);
            RefreshBalance();
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Shop Background", root, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
                UrbanFantasyStyle.ApplyMonochrome(background);
            }
            else
            {
                background.color = LobbyTheme.Hex("09090D");
            }

            ui.CreateImage("Shop Background Dim", root, new Color(.002f, .002f, .006f, .86f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Shop Left Shade", root, new Color(0, 0, 0, .40f),
                Vector2.zero, new Vector2(.34f, 1), Vector2.zero, Vector2.zero);
            ui.CreateImage("Shop Bottom Shade", root, new Color(0, 0, 0, .28f),
                Vector2.zero, new Vector2(1, .25f), Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform root)
        {
            RectTransform bar = ui.CreateImage("Shop Header", root, new Color(.005f, .005f, .009f, .82f),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 96), true).rectTransform;
            ui.CreateImage("Header Bottom Line", bar, UrbanFantasyStyle.Line,
                Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));

            GameObject back = ui.CreateButton("Back To Lobby", bar, new Vector2(0, .5f),
                new Vector2(54, 0), new Vector2(56, 56), "‹", 32,
                UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());

            ui.CreateText("Shop Eyebrow", "S T A R F A L L   E X C H A N G E", bar, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(260, 15),
                new Vector2(340, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Shop Title", "상점", bar, 29, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(260, -16),
                new Vector2(340, 38), TextAnchor.MiddleLeft);

            RectTransform wallet = ui.CreateImage("Crystal Wallet", bar, UrbanFantasyStyle.PanelStrong,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-198, 0),
                new Vector2(330, 58), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, wallet);
            ui.CreateText("Crystal Symbol", "✦", wallet, 24, FontStyle.Normal, UrbanFantasyStyle.Gold,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(34, 0),
                new Vector2(42, 42), TextAnchor.MiddleCenter);
            ui.CreateText("Crystal Label", "별의 결정", wallet, 13, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(102, 0), new Vector2(100, 32), TextAnchor.MiddleLeft);
            balanceValue = ui.CreateText("Crystal Balance", "0", wallet, 20, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-90, 0), new Vector2(150, 36), TextAnchor.MiddleRight);
        }

        void BuildShopContent(RectTransform root)
        {
            RectTransform workspace = ui.CreateImage("Shop Workspace", root, UrbanFantasyStyle.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -25),
                new Vector2(1320, 760), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            ui.CreateImage("Workspace Top Accent", workspace, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-64, 2));

            ui.CreateText("Section Eyebrow", "C R Y S T A L   S U P P L Y", workspace, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(216, -54),
                new Vector2(350, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Section Title", "별의 결정 충전", workspace, 31, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(216, -94), new Vector2(350, 42), TextAnchor.MiddleLeft);
            ui.CreateText("Development Notice", "개발용 무료 충전  /  횟수 제한 없음", workspace, 15,
                FontStyle.Normal, UrbanFantasyStyle.Gold,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-250, -78),
                new Vector2(390, 34), TextAnchor.MiddleRight);
            ui.CreateImage("Section Divider", workspace, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -132), new Vector2(-72, 1));

            RectTransform item = ui.CreateImage("Free Crystal Item", workspace, UrbanFantasyStyle.PanelSoft,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -12),
                new Vector2(1080, 390), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, item, UrbanFantasyStyle.StrongLine);
            ui.CreateImage("Item Accent", item, UrbanFantasyStyle.Gold,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0), new Vector2(4, -22));

            RectTransform emblem = ui.CreateCircleImage("Crystal Emblem", item,
                new Color(.84f, .73f, .49f, .12f), new Vector2(0, .5f),
                new Vector2(176, 0), new Vector2(210, 210)).rectTransform;
            ui.CreateText("Crystal Mark", "✦", emblem, 88, FontStyle.Normal, UrbanFantasyStyle.Gold,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            ui.CreateText("Item Type", "FREE TEST SUPPLY", item, 11, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(402, 76), new Vector2(330, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Item Name", "별의 결정 1,600개", item, 28, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(462, 30), new Vector2(450, 46), TextAnchor.MiddleLeft);
            ui.CreateText("Item Description",
                "모집에 사용할 수 있는 별의 결정을 즉시 지급합니다.\n개발 테스트 기간에는 원하는 만큼 받을 수 있습니다.",
                item, 15, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(506, -42),
                new Vector2(540, 70), TextAnchor.UpperLeft);

            GameObject receive = ui.CreateButton("Receive Free Crystals", item, new Vector2(1, .5f),
                new Vector2(-166, -118), new Vector2(270, 62), "무료로 받기", 18,
                new Color(.17f, .16f, .18f, .98f), ReceiveFreeCrystals);
            UrbanFantasyStyle.AddBorder(ui, receive.GetComponent<RectTransform>(), UrbanFantasyStyle.Gold);
            ui.CreateText("Unlimited Label", "개발용 무료 충전 · 횟수 제한 없음", item, 12,
                FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-166, -162),
                new Vector2(310, 24), TextAnchor.MiddleCenter);

            ui.CreateText("Shop Footer",
                "현재는 테스트용 별의 결정 상품만 제공됩니다. 이후 재화 및 교환 상품을 이 화면에 추가할 수 있습니다.",
                workspace, 13, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 49),
                new Vector2(-100, 30), TextAnchor.MiddleCenter);
        }

        void ReceiveFreeCrystals()
        {
            PlayerWallet.AddPremiumCurrency(FreeCrystalAmount);
            RefreshBalance();
            toast.Show("별의 결정 1,600개를 받았습니다.");
        }

        void RefreshBalance()
        {
            if (balanceValue != null)
                balanceValue.text = PlayerWallet.PremiumCurrency.ToString("N0");
        }

        void ReturnToLobby()
        {
            if (changingScene) return;
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
