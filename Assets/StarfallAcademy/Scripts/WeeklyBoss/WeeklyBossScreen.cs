using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class WeeklyBossScreen : MonoBehaviour
    {
        WeeklyBossDatabase database;
        CharacterDatabase characterDatabase;
        FormationPresetService presetService;
        WeeklyBossDefinition selectedBoss;
        int difficultyIndex;
        int presetIndex;
        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        Text title;
        Text description;
        Text difficulty;
        Text stats;
        Text score;
        Text presetLabel;
        Text startLabel;
        Button startButton;
        Image portrait;
        bool changingScene;
        bool menuBgmOverrideApplied;

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
            database = Resources.Load<WeeklyBossDatabase>("Data/WeeklyBossDatabase");
            characterDatabase = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            presetService = FormationPresetService.Default;
            presetIndex = presetService.ActivePresetIndex;
            ui = new LobbyUiFactory(new LobbyTheme());
            RectTransform root = (RectTransform)transform;
            Image background = ui.CreateImage("Background", root, LobbyTheme.Hex("08080C"),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.color = new Color(.28f, .28f, .32f, 1f);
            }
            RectTransform safe = CreateLayer("Safe Area", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform workspace = ui.CreateImage("Weekly Boss Workspace", safe,
                new Color(.01f, .01f, .015f, .94f), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(1700, 920), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            ui.CreateButton("Back", workspace, new Vector2(0, 1), new Vector2(48, -48),
                new Vector2(54, 54), "‹", 30, UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            ui.CreateText("Header", "W E E K L Y   B O S S", workspace, 30, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -54), new Vector2(-160, 48), TextAnchor.MiddleCenter);
            BuildBossList(workspace);
            BuildDetail(workspace);
            toast = CreateController<LobbyToastOverlay>("Weekly Boss Toast", safe);
            toast.Initialize(safe, ui);
            SelectInitial();
        }

        void BuildBossList(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Boss List", root, UrbanFantasyStyle.PanelSoft,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(30, 30),
                new Vector2(430, -130), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("List Title", "ROTATION", panel, 15, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -28), new Vector2(-30, 28), TextAnchor.MiddleLeft);
            if (database == null) return;
            int visible = Mathf.Min(8, database.Bosses.Count);
            for (int i = 0; i < visible; i++)
            {
                WeeklyBossDefinition boss = database.Bosses[i];
                if (boss == null) continue;
                int captured = i;
                ui.CreateButton("Boss " + boss.Id, panel, new Vector2(.5f, 1),
                    new Vector2(0, -90 - i * 78), new Vector2(370, 64),
                    boss.Id + "   " + boss.DisplayName, 16, UrbanFantasyStyle.PanelStrong,
                    () => SelectBoss(captured), TextAnchor.MiddleLeft);
            }
        }

        void BuildDetail(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Boss Detail", root, UrbanFantasyStyle.PanelStrong,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(485, 30),
                new Vector2(-30, -130), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel, UrbanFantasyStyle.StrongLine);
            portrait = ui.CreateImage("Portrait", panel, new Color(.05f, .05f, .07f, 1f),
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(205, -240), new Vector2(330, 380));
            portrait.preserveAspect = true;
            title = ui.CreateText("Boss Name", string.Empty, panel, 34, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(435, -72), new Vector2(-70, 60), TextAnchor.MiddleLeft);
            description = ui.CreateText("Description", string.Empty, panel, 16, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(435, -155), new Vector2(-70, 95), TextAnchor.UpperLeft);
            GameObject difficultyButton = ui.CreateButton("Difficulty", panel, new Vector2(0, .5f),
                new Vector2(520, 85), new Vector2(500, 64), string.Empty, 18,
                UrbanFantasyStyle.PanelSoft, NextDifficulty);
            difficulty = difficultyButton.GetComponentInChildren<Text>();
            stats = ui.CreateText("Stats", string.Empty, panel, 16, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, .5f), new Vector2(1, .5f),
                new Vector2(435, -35), new Vector2(-70, 125), TextAnchor.UpperLeft);
            score = ui.CreateText("Score", string.Empty, panel, 17, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(0, 0), new Vector2(1, .5f),
                new Vector2(435, 155), new Vector2(-70, -40), TextAnchor.UpperLeft);
            ui.CreateButton("Previous Preset", panel, new Vector2(0, 0),
                new Vector2(58, 62), new Vector2(54, 54), "‹", 26,
                UrbanFantasyStyle.PanelSoft, SelectPreviousPreset);
            presetLabel = ui.CreateText("Current Preset", string.Empty, panel, 15,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 0),
                new Vector2(0, 0), new Vector2(255, 62), new Vector2(330, 58),
                TextAnchor.MiddleCenter);
            ui.CreateButton("Next Preset", panel, new Vector2(0, 0),
                new Vector2(452, 62), new Vector2(54, 54), "›", 26,
                UrbanFantasyStyle.PanelSoft, SelectNextPreset);
            GameObject formation = ui.CreateButton("Formation", panel, new Vector2(0, 0),
                new Vector2(640, 62), new Vector2(250, 62), "편성 변경", 17,
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
            if (database == null || database.Bosses.Count == 0)
            {
                title.text = "주간 보스 데이터가 없습니다";
                startButton.interactable = false;
                return;
            }
            int index = -1;
            for (int i = 0; i < database.Bosses.Count; i++)
            {
                WeeklyBossDefinition boss = database.Bosses[i];
                if (boss == null) continue;
                if (index < 0) index = i;
                if (boss.IsAvailable(ContentTime.UtcNow)) { index = i; break; }
            }
            if (index < 0)
            {
                title.text = "유효한 주간 보스 데이터가 없습니다";
                startButton.interactable = false;
                return;
            }
            SelectBoss(index);
        }

        void SelectBoss(int index)
        {
            if (database == null || index < 0 || index >= database.Bosses.Count) return;
            selectedBoss = database.Bosses[index];
            difficultyIndex = 0;
            RefreshDetail();
            ApplyBossMenuBgm();
        }

        void NextDifficulty()
        {
            if (selectedBoss == null || selectedBoss.Difficulties.Count == 0) return;
            difficultyIndex = (difficultyIndex + 1) % selectedBoss.Difficulties.Count;
            RefreshDetail();
        }

        void RefreshDetail()
        {
            if (selectedBoss == null)
            {
                title.text = "유효하지 않은 보스 데이터";
                description.text = difficulty.text = stats.text = score.text = string.Empty;
                portrait.sprite = null;
                startButton.interactable = false;
                startLabel.text = "출격 불가";
                return;
            }
            title.text = selectedBoss.DisplayName;
            description.text = selectedBoss.Description;
            portrait.sprite = selectedBoss.Portrait;
            WeeklyBossDifficulty entry = CurrentDifficulty;
            bool unlocked = PlayerProfileService.Default.IsUnlocked(AccountFeature.WeeklyBoss);
            bool valid = entry != null && selectedBoss.BaseStage != null
                && selectedBoss.IsAvailable(ContentTime.UtcNow);
            if (entry == null)
            {
                difficulty.text = "난이도 데이터 없음";
                stats.text = score.text = string.Empty;
            }
            else
            {
                WeeklyBossSnapshot snapshot = WeeklyBossService.Default.GetSnapshot(selectedBoss, entry);
                difficulty.text = "난이도  " + entry.DisplayName + "   ›";
                stats.text = "권장 전투력  " + entry.RecommendedPower.ToString("N0")
                    + "\nHP ×" + entry.HpMultiplier.ToString("0.##")
                    + "   ATK ×" + entry.AttackMultiplier.ToString("0.##")
                    + "   SPD ×" + entry.SpeedMultiplier.ToString("0.##")
                    + "\n제한 ACTION  " + entry.TurnLimit;
                score.text = "BEST  " + snapshot.BestScore.ToString("N0")
                    + "\n남은 도전  " + snapshot.AttemptsRemaining + " / " + snapshot.MaximumAttempts;
                valid &= snapshot.AttemptsRemaining > 0;
            }
            startButton.interactable = unlocked && valid;
            startLabel.text = !unlocked ? "ACCOUNT LV.15 해금"
                : valid ? "보스전 개시" : "출격 불가";
        }

        WeeklyBossDifficulty CurrentDifficulty => selectedBoss != null
            && difficultyIndex >= 0 && difficultyIndex < selectedBoss.Difficulties.Count
                ? selectedBoss.Difficulties[difficultyIndex] : null;

        void StartBattle()
        {
            if (changingScene || !PlayerProfileService.Default.IsUnlocked(AccountFeature.WeeklyBoss)) return;
            if (!TryLoadSelectedFormation(out _))
            {
                toast.Show("출격할 캐릭터를 먼저 편성하세요");
                return;
            }
            if (!WeeklyBossService.Default.TryBeginRun(selectedBoss, CurrentDifficulty,
                out WeeklyBossRunContext context, out string reason))
            {
                toast.Show(reason);
                RefreshDetail();
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

        void ApplyBossMenuBgm()
        {
            GameAudioDirector.RefreshForCurrentScene(null, selectedBoss != null
                ? selectedBoss.MenuBgm : null);
            menuBgmOverrideApplied = true;
        }

        void OpenFormation()
        {
            if (presetService == null || !presetService.Select(presetIndex))
            {
                toast.Show("프리셋을 저장하지 못했습니다. 다시 시도해 주세요");
                return;
            }
            SceneNavigation.FormationReturnScene = SceneNames.WeeklyBoss;
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

        void OnDestroy()
        {
            if (!menuBgmOverrideApplied) return;
            GameAudioDirector.ClearSceneOverride();
            menuBgmOverrideApplied = false;
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
