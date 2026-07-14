using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CharacterArchiveScreen : MonoBehaviour
    {
        LobbyUiFactory ui;
        bool changingScene;

        void Awake()
        {
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (!changingScene && Input.GetKeyDown(KeyCode.Escape)) ReturnToLobby();
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
            CharacterDatabase database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            BuildBackground(root);

            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform workspace = ui.CreateImage("Archive Workspace", safeRoot,
                new Color(.008f, .008f, .012f, .88f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(1740, 960), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            BuildHeader(workspace, database);

            LobbyToastOverlay toast = CreateController<LobbyToastOverlay>("Archive Toast", safeRoot);
            toast.Initialize(safeRoot, ui);
            var detail = new CharacterArchiveDetailView(workspace, ui, toast.Show);
            var list = new CharacterArchiveListView(workspace, ui, database, detail.SetCharacter);
            list.SelectFirst();
            toast.transform.SetAsLastSibling();
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Archive Background", root, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art == null) art = Resources.Load<Texture2D>("Lobby/Art/lobby_hero_v2");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.type = Image.Type.Simple;
            }
            else background.color = LobbyTheme.Hex("09090D");
            ui.CreateImage("Archive Grade", root, new Color(.025f, .02f, .035f, .42f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Archive Dim", root, new Color(.002f, .002f, .004f, .74f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform workspace, CharacterDatabase database)
        {
            ui.CreateImage("Header Accent", workspace, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-64, 2));
            GameObject back = ui.CreateButton("Back To Lobby", workspace, new Vector2(0, 1),
                new Vector2(52, -52), new Vector2(54, 54), "‹", 31,
                UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());
            ui.CreateText("Archive Eyebrow", "C H A R A C T E R   A R C H I V E", workspace, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(250, -33), new Vector2(320, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Archive Title", "캐릭터 도감", workspace, 32, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(250, -70), new Vector2(320, 44), TextAnchor.MiddleLeft);
            int count = 0;
            if (database != null)
                foreach (CharacterData character in database.Characters)
                    if (character != null) count++;
            ui.CreateText("Archive Count", "등록 캐릭터  " + count.ToString("00"), workspace, 15,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-140, -52), new Vector2(220, 30), TextAnchor.MiddleRight);
        }

        void ReturnToLobby()
        {
            if (changingScene) return;
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.Lobby);
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
