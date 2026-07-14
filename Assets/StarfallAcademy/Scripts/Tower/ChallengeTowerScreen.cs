using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class ChallengeTowerScreen : MonoBehaviour
    {
        TowerDatabase database;
        CharacterDatabase characterDatabase;
        FormationPresetService presetService;
        TowerFloorData selected;
        int presetIndex;
        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        readonly List<Text> floorLabels = new List<Text>();
        readonly List<int> floorLabelIndices = new List<int>();
        Text floorTitle;
        Text stageName;
        Text detail;
        Text modifiers;
        Text stars;
        Text presetLabel;
        Text startLabel;
        Button startButton;
        bool changingScene;

        void Awake()
        {
            BattleSession.Clear();
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
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildScreen()
        {
            database = Resources.Load<TowerDatabase>("Data/TowerDatabase");
            characterDatabase = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            presetService = FormationPresetService.Default;
            presetIndex = presetService.ActivePresetIndex;
            ui = new LobbyUiFactory(new LobbyTheme());
            RectTransform root = (RectTransform)transform;
            Image background = ui.CreateImage("Background", root, LobbyTheme.Hex("09090D"),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.color = new Color(.22f, .22f, .25f, 1f);
            }
            RectTransform safe = CreateLayer("Safe Area", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform workspace = ui.CreateImage("Tower Workspace", safe,
                new Color(.008f, .008f, .012f, .95f), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(1700, 920), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            ui.CreateButton("Back", workspace, new Vector2(0, 1), new Vector2(48, -48),
                new Vector2(54, 54), "‹", 30, UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            ui.CreateText("Header", "C H A L L E N G E   T O W E R", workspace, 30,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -54), new Vector2(-160, 48), TextAnchor.MiddleCenter);
            BuildFloorList(workspace);
            BuildDetail(workspace);
            toast = CreateController<LobbyToastOverlay>("Tower Toast", safe);
            toast.Initialize(safe, ui);
            SelectInitial();
        }

        void BuildFloorList(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Floor List", root, UrbanFantasyStyle.PanelSoft,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(30, 30),
                new Vector2(520, -130), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("List Title", "FLOORS", panel, 15, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -26), new Vector2(-30, 26), TextAnchor.MiddleLeft);
            if (database == null) return;
            int visible = Mathf.Min(30, database.Floors.Count);
            for (int i = 0; i < visible; i++)
            {
                TowerFloorData floor = database.Floors[i];
                if (floor == null) continue;
                int captured = i;
                float x = (i % 3 - 1) * 150f;
                float y = -72f - i / 3 * 72f;
                GameObject button = ui.CreateButton("Floor " + floor.FloorNumber, panel,
                    new Vector2(.5f, 1), new Vector2(x, y), new Vector2(136, 58),
                    string.Empty, 14, UrbanFantasyStyle.PanelStrong, () => SelectFloor(captured));
                floorLabels.Add(button.GetComponentInChildren<Text>());
                floorLabelIndices.Add(captured);
            }
        }

        void BuildDetail(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Floor Detail", root, UrbanFantasyStyle.PanelStrong,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(575, 30),
                new Vector2(-30, -130), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel, UrbanFantasyStyle.StrongLine);
            floorTitle = ui.CreateText("Floor Number", string.Empty, panel, 48, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(70, -85), new Vector2(-70, 70), TextAnchor.MiddleLeft);
            stageName = ui.CreateText("Stage Name", string.Empty, panel, 27, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(70, -162), new Vector2(-70, 52), TextAnchor.MiddleLeft);
            detail = ui.CreateText("Floor Detail Text", string.Empty, panel, 17, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, .5f), new Vector2(1, 1),
                new Vector2(70, -30), new Vector2(-70, -235), TextAnchor.UpperLeft);
            modifiers = ui.CreateText("Modifiers", string.Empty, panel, 18, FontStyle.Normal,
                new Color(.88f, .72f, .42f, 1f), new Vector2(0, .5f), new Vector2(1, .5f),
                new Vector2(70, -15), new Vector2(-70, 130), TextAnchor.UpperLeft);
            stars = ui.CreateText("Stars", string.Empty, panel, 18, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 0), new Vector2(1, .5f),
                new Vector2(70, 145), new Vector2(-70, -30), TextAnchor.UpperLeft);
            ui.CreateButton("Previous Preset", panel, new Vector2(0, 0),
                new Vector2(48, 62), new Vector2(50, 54), "‹", 26,
                UrbanFantasyStyle.PanelSoft, SelectPreviousPreset);
            presetLabel = ui.CreateText("Current Preset", string.Empty, panel, 14,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(205, 62), new Vector2(250, 58),
                TextAnchor.MiddleCenter);
            ui.CreateButton("Next Preset", panel, new Vector2(0, 0),
                new Vector2(362, 62), new Vector2(50, 54), "›", 26,
                UrbanFantasyStyle.PanelSoft, SelectNextPreset);
            GameObject formation = ui.CreateButton("Formation", panel, new Vector2(0, 0),
                new Vector2(520, 62), new Vector2(240, 62), "편성 변경", 17,
                UrbanFantasyStyle.PanelSoft, OpenFormation);
            UrbanFantasyStyle.AddBorder(ui, formation.GetComponent<RectTransform>());
            GameObject start = ui.CreateButton("Start", panel, new Vector2(1, 0),
                new Vector2(-220, 62), new Vector2(340, 68), string.Empty, 18,
                new Color(.18f, .18f, .22f, 1f), StartBattle);
            startButton = start.GetComponent<Button>();
            startLabel = start.GetComponentInChildren<Text>();
            UrbanFantasyStyle.AddBorder(ui, start.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            RefreshPresetDisplay();
        }

        void SelectInitial()
        {
            if (database == null || database.Floors.Count == 0)
            {
                floorTitle.text = "NO FLOOR DATA";
                startButton.interactable = false;
                return;
            }
            int index = -1;
            for (int i = 0; i < database.Floors.Count; i++)
            {
                TowerFloorData floor = database.Floors[i];
                if (floor == null) continue;
                if (index < 0) index = i;
                if (!TowerProgressService.Default.IsUnlocked(floor, database)) continue;
                index = i;
                if (!TowerProgressService.Default.GetSnapshot(floor).Cleared) break;
            }
            if (index < 0)
            {
                floorTitle.text = "NO VALID FLOOR DATA";
                startButton.interactable = false;
                return;
            }
            SelectFloor(index);
        }

        void SelectFloor(int index)
        {
            if (database == null || index < 0 || index >= database.Floors.Count) return;
            selected = database.Floors[index];
            RefreshDetail();
        }

        void RefreshDetail()
        {
            if (selected == null)
            {
                floorTitle.text = "INVALID FLOOR";
                stageName.text = detail.text = modifiers.text = stars.text = string.Empty;
                startButton.interactable = false;
                startLabel.text = "도전 불가";
                return;
            }
            TowerFloorSnapshot snapshot = TowerProgressService.Default.GetSnapshot(selected);
            bool unlocked = TowerProgressService.Default.IsUnlocked(selected, database);
            bool accountUnlocked = PlayerProfileService.Default.IsUnlocked(AccountFeature.ChallengeTower);
            floorTitle.text = "FLOOR  " + selected.FloorNumber.ToString("00");
            stageName.text = selected.BaseStage != null ? selected.BaseStage.DisplayName : "기반 스테이지 없음";
            detail.text = "권장 전투력  " + selected.RecommendedPower.ToString("N0")
                + "\n클리어 기록  " + (snapshot.Cleared ? "CLEAR" : "미클리어")
                + "\n전체 별  " + TowerProgressService.Default.GetTotalStars(database);
            var modifierService = new TowerModifierService(selected.Modifiers);
            modifiers.text = "층 효과\n" + modifierService.Summary;
            stars.text = "BEST  " + new string('★', snapshot.Stars)
                + new string('☆', 3 - snapshot.Stars)
                + "\n최초 보상  " + (selected.FirstClearReward?.Summary ?? "없음");
            startButton.interactable = accountUnlocked && unlocked && selected.BaseStage != null;
            startLabel.text = !accountUnlocked ? "ACCOUNT LV.20 해금"
                : !unlocked ? "이전 층 클리어 필요" : "도전 시작";
            RefreshFloorLabels();
        }

        void RefreshFloorLabels()
        {
            int count = Mathf.Min(floorLabels.Count, floorLabelIndices.Count);
            for (int i = 0; i < count; i++)
            {
                int databaseIndex = floorLabelIndices[i];
                if (database == null || databaseIndex < 0
                    || databaseIndex >= database.Floors.Count) continue;
                TowerFloorData floor = database.Floors[databaseIndex];
                if (floor == null) continue;
                TowerFloorSnapshot snapshot = TowerProgressService.Default.GetSnapshot(floor);
                bool unlocked = TowerProgressService.Default.IsUnlocked(floor, database);
                floorLabels[i].text = floor.FloorNumber.ToString("00") + "층   "
                    + (unlocked ? new string('★', snapshot.Stars)
                        + new string('☆', 3 - snapshot.Stars) : "LOCKED");
                floorLabels[i].color = floor == selected ? UrbanFantasyStyle.Gold
                    : unlocked ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted;
            }
        }

        void StartBattle()
        {
            if (changingScene || !PlayerProfileService.Default.IsUnlocked(AccountFeature.ChallengeTower)) return;
            if (!TryLoadSelectedFormation(out _))
            {
                toast.Show("출격할 캐릭터를 먼저 편성하세요");
                return;
            }
            if (!TowerProgressService.Default.TryBeginRun(selected, database,
                out TowerRunContext context, out string reason))
            {
                toast.Show(reason);
                return;
            }
            BattleSession.BeginSpecialRun(context);
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.TurnBattle);
        }

        void SelectPreviousPreset() => SelectPreset(presetIndex - 1);

        void SelectNextPreset() => SelectPreset(presetIndex + 1);

        void SelectPreset(int index)
        {
            if (presetService == null || presetService.PresetCount <= 0) return;
            int count = presetService.PresetCount;
            index = (index % count + count) % count;
            if (!presetService.Select(index)) return;
            presetIndex = index;
            RefreshPresetDisplay();
        }

        void RefreshPresetDisplay()
        {
            if (presetLabel == null || presetService == null) return;
            var presets = presetService.GetPresets(characterDatabase);
            if (presets.Count == 0)
            {
                presetLabel.text = "편성 프리셋 없음";
                return;
            }
            presetIndex = Mathf.Clamp(presetIndex, 0, presets.Count - 1);
            FormationPreset preset = presets[presetIndex];
            var formation = new FormationState();
            formation.Load(characterDatabase);
            string presetName = string.IsNullOrWhiteSpace(preset?.name)
                ? "파티 " + (presetIndex + 1) : preset.name;
            presetLabel.text = (presetIndex + 1) + "/" + presets.Count + "  " + presetName
                + "\n" + formation.Count + "/" + FormationState.MaxMembers
                + "명 · 전투력 " + formation.TotalPower.ToString("N0");
        }

        bool TryLoadSelectedFormation(out FormationState formation)
        {
            formation = new FormationState();
            if (presetService == null || !presetService.Select(presetIndex)) return false;
            formation.Load(characterDatabase);
            return formation.Count > 0;
        }

        void OpenFormation()
        {
            if (presetService == null || !presetService.Select(presetIndex))
            {
                toast.Show("프리셋을 저장하지 못했습니다. 다시 시도해 주세요");
                return;
            }
            SceneNavigation.FormationReturnScene = SceneNames.ChallengeTower;
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.Formation);
        }

        void ReturnToLobby()
        {
            if (changingScene) return;
            changingScene = true;
            BattleSession.Clear();
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
