using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbyScreen : MonoBehaviour
    {
        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        LobbyModal modal;
        LobbySettingsPanel settings;
        LobbyMissionPanel missions;
        float nextLoginRefreshTime;

        void Awake()
        {
            MissionService.RecordLogin();
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (Time.unscaledTime < nextLoginRefreshTime) return;
            nextLoginRefreshTime = Time.unscaledTime + 30f;
            MissionService.RecordLogin();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) MissionService.RecordLogin();
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

            Image background = ui.CreateImage("Lobby Background", root, ui.Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art == null)
                art = Resources.Load<Texture2D>("Lobby/Art/lobby_hero_v2");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.type = Image.Type.Simple;
            }
            else
            {
                background.color = LobbyTheme.Hex("182B50");
            }

            AddAtmosphere(root);
            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform overlayRoot = CreateLayer("Overlay", safeRoot);

            LobbySpeechBubble speech = CreateController<LobbySpeechBubble>("Speech Controller", overlayRoot);
            modal = CreateController<LobbyModal>("Modal Controller", overlayRoot);
            settings = CreateController<LobbySettingsPanel>("Settings Controller", overlayRoot);
            missions = CreateController<LobbyMissionPanel>("Mission Controller", overlayRoot);
            toast = CreateController<LobbyToastOverlay>("Toast Controller", overlayRoot);
            toast.Initialize(overlayRoot, ui);
            modal.Initialize(overlayRoot, ui);
            settings.Initialize(overlayRoot, ui);
            missions.Initialize(overlayRoot, ui, toast);
            speech.Initialize(overlayRoot, ui, toast);

            GothicLobbyView.Build(safeRoot, ui, OpenCharacterArchive, OpenStoryArchive, OpenFormation,
                OpenGacha, OpenShop, OpenStageSelect, missions.Open, settings.Open, OpenPopup, toast.Show);
            overlayRoot.SetAsLastSibling();
        }

        void AddAtmosphere(RectTransform root)
        {
            ui.CreateImage("Monochrome Grade", root, new Color(.05f, .045f, .07f, .28f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Right Vignette", root, new Color(.005f, .006f, .01f, .42f),
                new Vector2(.66f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Bottom Vignette", root, new Color(.005f, .006f, .01f, .38f),
                Vector2.zero, new Vector2(1, .24f), Vector2.zero, Vector2.zero);
        }

        void OpenPopup(string title, string body) => modal.Open(title, body);
        void OpenFormation()
        {
            SceneNavigation.FormationReturnScene = SceneNames.Lobby;
            SceneManager.LoadScene(SceneNames.Formation);
        }
        void OpenGacha() => SceneManager.LoadScene(SceneNames.Gacha);
        void OpenShop() => SceneManager.LoadScene(SceneNames.Shop);
        void OpenCharacterArchive() => SceneManager.LoadScene(SceneNames.CharacterArchive);
        void OpenStoryArchive() => SceneManager.LoadScene(SceneNames.StoryArchive);
        void OpenStageSelect() => SceneManager.LoadScene(SceneNames.StageSelect);

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
