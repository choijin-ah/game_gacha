using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class StageSelectScreen : MonoBehaviour
    {
        readonly Dictionary<int, Image> selectionMarks = new Dictionary<int, Image>();
        LobbyUiFactory ui;
        StageDatabase stageDatabase;
        CharacterDatabase characterDatabase;
        FormationState formation;
        LobbyToastOverlay toast;
        Text stageNumber;
        Text stageName;
        Text description;
        Text recommended;
        Text enemies;
        Text rewards;
        Text lockState;
        Text startLabel;
        Button startButton;
        RectTransform formationRoot;
        StageData selected;
        int selectedIndex = -1;
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
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildScreen()
        {
            RectTransform root = (RectTransform)transform;
            ui = new LobbyUiFactory(new LobbyTheme());
            stageDatabase = Resources.Load<StageDatabase>("Data/StageDatabase");
            characterDatabase = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            EnsureStarterAndFormation();
            BuildBackground(root);
            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform workspace = ui.CreateImage("Stage Workspace", safeRoot,
                new Color(.008f, .008f, .012f, .9f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(1740, 960), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            BuildHeader(workspace);
            BuildStageList(workspace);
            BuildDetail(workspace);
            toast = CreateController<LobbyToastOverlay>("Stage Toast", safeRoot);
            toast.Initialize(safeRoot, ui);
            SelectInitialStage();
        }

        void EnsureStarterAndFormation()
        {
            if (characterDatabase == null) return;
            CharacterData first = null;
            CharacterData firstOwned = null;
            foreach (CharacterData character in characterDatabase.Characters)
            {
                if (character == null) continue;
                if (first == null) first = character;
                if (firstOwned == null && CharacterProgressionService.IsOwned(character)) firstOwned = character;
            }
            if (firstOwned == null && first != null)
            {
                CharacterProgressionService.RegisterPull(first);
                firstOwned = first;
            }
            formation = new FormationState();
            formation.Load(characterDatabase);
            if (formation.Count == 0 && firstOwned != null)
            {
                formation.Toggle(firstOwned);
                formation.Save();
            }
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Stage Background", root, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.type = Image.Type.Simple;
            }
            else background.color = LobbyTheme.Hex("09090D");
            ui.CreateImage("Stage Background Dim", root, new Color(.002f, .002f, .005f, .82f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform workspace)
        {
            ui.CreateImage("Header Accent", workspace, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-64, 2));
            GameObject back = ui.CreateButton("Back", workspace, new Vector2(0, 1), new Vector2(52, -52),
                new Vector2(54, 54), "‹", 31, UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());
            ui.CreateText("Stage Eyebrow", "O P E R A T I O N   S E L E C T", workspace, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(260, -33), new Vector2(340, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Stage Title", "작전 지역 선택", workspace, 32, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(260, -70), new Vector2(340, 44), TextAnchor.MiddleLeft);
            GameObject formationButton = ui.CreateButton("Change Formation", workspace, new Vector2(1, 1),
                new Vector2(-140, -54), new Vector2(220, 54), "편성 변경", 17,
                UrbanFantasyStyle.PanelStrong, ChangeFormation);
            UrbanFantasyStyle.AddBorder(ui, formationButton.GetComponent<RectTransform>());
        }

        void BuildStageList(RectTransform workspace)
        {
            RectTransform panel = ui.CreateImage("Stage List", workspace, UrbanFantasyStyle.Panel,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(295, -35),
                new Vector2(520, 760)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("List Header", "작전 목록", panel, 20, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -34),
                new Vector2(150, 30), TextAnchor.MiddleLeft);
            ui.CreateImage("List Line", panel, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -62), new Vector2(-30, 1));
            Image viewportImage = ui.CreateImage("Stage Viewport", panel, Color.clear,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -35),
                new Vector2(488, 660), true);
            viewportImage.gameObject.AddComponent<RectMask2D>();
            var contentObject = new GameObject("Stage Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportImage.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewportImage.rectTransform;
            scroll.horizontal = false;

            if (stageDatabase == null) return;
            for (int i = 0; i < stageDatabase.Stages.Count; i++)
            {
                StageData stage = stageDatabase.Stages[i];
                if (stage == null) continue;
                CreateStageCard(content, stage, i);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        void CreateStageCard(RectTransform content, StageData stage, int index)
        {
            bool unlocked = StageProgression.IsUnlocked(index);
            GameObject card = ui.CreateButton("Stage " + stage.Id, content, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(476, 112), string.Empty, 16, UrbanFantasyStyle.PanelSoft,
                () => SelectStage(stage, index));
            LayoutElement element = card.AddComponent<LayoutElement>();
            element.preferredHeight = 112;
            UrbanFantasyStyle.AddBorder(ui, card.GetComponent<RectTransform>());
            Image selectedMark = ui.CreateImage("Selected", card.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
            selectedMark.transform.SetAsFirstSibling();
            selectionMarks[index] = selectedMark;
            ui.CreateText("Stage Number", (index + 1).ToString("00"), card.transform, 28, FontStyle.Normal,
                unlocked ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(52, 0),
                new Vector2(66, 48), TextAnchor.MiddleCenter);
            ui.CreateText("Stage Name", unlocked ? stage.DisplayName : "잠긴 작전", card.transform,
                19, FontStyle.Normal, unlocked ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(251, 18),
                new Vector2(300, 30), TextAnchor.MiddleLeft);
            string sub = unlocked ? stage.Chapter + "  ·  권장 " + stage.RecommendedPower.ToString("N0") : "◆  LOCKED";
            ui.CreateText("Stage Info", sub, card.transform, 12, FontStyle.Normal,
                unlocked ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(251, -19),
                new Vector2(300, 24), TextAnchor.MiddleLeft);
            if (StageProgression.IsCleared(stage))
                ui.CreateText("Clear", "CLEAR", card.transform, 10, FontStyle.Normal, UrbanFantasyStyle.Gold,
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-38, -18),
                    new Vector2(64, 20), TextAnchor.MiddleCenter);
        }

        void BuildDetail(RectTransform workspace)
        {
            RectTransform panel = ui.CreateImage("Stage Detail", workspace, UrbanFantasyStyle.Panel,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-575, -35),
                new Vector2(1080, 760)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            stageNumber = ui.CreateText("Selected Stage Number", string.Empty, panel, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -35), new Vector2(-58, 22), TextAnchor.MiddleLeft);
            stageName = ui.CreateText("Selected Stage Name", string.Empty, panel, 34, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -80), new Vector2(-58, 48), TextAnchor.MiddleLeft);
            description = ui.CreateText("Stage Description", string.Empty, panel, 16, FontStyle.Normal,
                new Color(.88f, .88f, .91f, .76f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -135), new Vector2(-58, 58), TextAnchor.UpperLeft);
            ui.CreateImage("Detail Line", panel, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -174), new Vector2(-58, 1));
            recommended = CreateInfo(ui, panel, "권장 전투력", -215);
            enemies = CreateInfo(ui, panel, "적 편성", -257);
            rewards = CreateInfo(ui, panel, "클리어 보상", -299);
            lockState = ui.CreateText("Stage Lock State", string.Empty, panel, 13, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-110, -215), new Vector2(180, 30), TextAnchor.MiddleRight);

            ui.CreateText("Formation Header", "현재 편성", panel, 18, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(95, -360), new Vector2(150, 30), TextAnchor.MiddleLeft);
            formationRoot = CreateLayer("Formation Preview", panel);
            formationRoot.offsetMin = new Vector2(28, 148);
            formationRoot.offsetMax = new Vector2(-28, -397);
            BuildFormationPreview();
            GameObject start = ui.CreateButton("Start Battle", panel, new Vector2(1, 0),
                new Vector2(-150, 58), new Vector2(260, 68), string.Empty, 20,
                new Color(.18f, .18f, .21f, .98f), StartBattle);
            UrbanFantasyStyle.AddBorder(ui, start.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            startButton = start.GetComponent<Button>();
            startLabel = start.GetComponentInChildren<Text>();
            ui.CreateText("Team Power", "TOTAL POWER  " + (formation != null ? formation.TotalPower.ToString("N0") : "0"),
                panel, 15, FontStyle.Normal, UrbanFantasyStyle.Gold,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(185, 58),
                new Vector2(280, 38), TextAnchor.MiddleLeft);
        }

        void BuildFormationPreview()
        {
            if (formation == null) return;
            for (int i = 0; i < FormationState.MaxMembers; i++)
            {
                float x = 125 + i * 247;
                RectTransform slot = ui.CreateImage("Member " + i, formationRoot, UrbanFantasyStyle.PanelSoft,
                    new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(x, 0),
                    new Vector2(220, 180)).rectTransform;
                UrbanFantasyStyle.AddBorder(ui, slot);
                if (i >= formation.Count)
                {
                    ui.CreateText("Empty", "+\nEMPTY", slot, 14, FontStyle.Normal, UrbanFantasyStyle.Muted,
                        Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
                    continue;
                }
                CharacterData character = formation.Members[i];
                Image portrait = ui.CreateImage("Portrait", slot, new Color(.12f, .12f, .15f, 1),
                    new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -69),
                    new Vector2(190, 112));
                if (character.Portrait != null)
                {
                    portrait.sprite = character.Portrait;
                    portrait.type = Image.Type.Simple;
                    portrait.preserveAspect = true;
                    portrait.color = Color.white;
                }
                ui.CreateText("Name", character.DisplayName, slot, 16, FontStyle.Normal,
                    UrbanFantasyStyle.Silver, new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(0, 44), new Vector2(190, 26), TextAnchor.MiddleCenter);
                ui.CreateText("Level", "LV." + CharacterProgressionService.GetLevel(character), slot,
                    11, FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(.5f, 0), new Vector2(.5f, 0),
                    new Vector2(0, 20), new Vector2(190, 20), TextAnchor.MiddleCenter);
            }
        }

        static Text CreateInfo(LobbyUiFactory ui, RectTransform parent, string label, float y)
        {
            ui.CreateText(label + " Label", label, parent, 13, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, y),
                new Vector2(160, 26), TextAnchor.MiddleLeft);
            return ui.CreateText(label + " Value", string.Empty, parent, 16, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(100, y), new Vector2(-260, 28), TextAnchor.MiddleLeft);
        }

        void SelectInitialStage()
        {
            if (stageDatabase == null || stageDatabase.Stages.Count == 0)
            {
                SelectStage(null, -1);
                return;
            }
            int index = Mathf.Clamp(StageProgression.HighestUnlocked, 0, stageDatabase.Stages.Count - 1);
            while (index > 0 && stageDatabase.Stages[index] == null) index--;
            if (stageDatabase.Stages[index] == null)
            {
                for (int i = 0; i < stageDatabase.Stages.Count; i++)
                {
                    if (stageDatabase.Stages[i] == null) continue;
                    index = i;
                    break;
                }
            }
            SelectStage(stageDatabase.Stages[index], index);
        }

        void SelectStage(StageData stage, int index)
        {
            selected = stage;
            selectedIndex = index;
            foreach (KeyValuePair<int, Image> pair in selectionMarks)
                pair.Value.color = pair.Key == index ? UrbanFantasyStyle.Highlight : Color.clear;
            if (stage == null)
            {
                stageNumber.text = "NO STAGE DATA";
                stageName.text = "스테이지 데이터가 없습니다";
                description.text = "Unity 메뉴에서 Stage Database를 생성하세요.";
                recommended.text = enemies.text = rewards.text = lockState.text = string.Empty;
                startLabel.text = "출격 불가";
                startButton.interactable = false;
                return;
            }
            bool unlocked = StageProgression.IsUnlocked(index);
            stageNumber.text = stage.Chapter + "   /   STAGE " + (index + 1).ToString("00");
            stageName.text = stage.DisplayName;
            description.text = stage.Description;
            recommended.text = stage.RecommendedPower.ToString("N0");
            enemies.text = stage.EnemyName + "  × " + stage.EnemyCount + "  ·  LV." + stage.EnemyLevel;
            rewards.text = "● " + stage.RewardCredits.ToString("N0") + " 크레딧   ◇ " +
                stage.RewardSkillMaterials + " " + PlayerWallet.SkillMaterialDisplayName;
            lockState.text = unlocked ? (StageProgression.IsCleared(stage) ? "CLEAR" : "UNLOCKED") : "LOCKED";
            startLabel.text = unlocked ? "작전 개시" : "잠긴 스테이지";
            startButton.interactable = unlocked;
        }

        void StartBattle()
        {
            if (selected == null || !StageProgression.IsUnlocked(selectedIndex)) return;
            if (formation == null || formation.Count == 0)
            {
                toast.Show("출격할 캐릭터를 편성하세요");
                return;
            }
            BattleSession.SelectedStage = selected;
            BattleSession.SelectedStageIndex = selectedIndex;
            changingScene = true;
            SceneManager.LoadScene(SceneNames.TurnBattle);
        }

        void ChangeFormation()
        {
            SceneNavigation.FormationReturnScene = SceneNames.StageSelect;
            changingScene = true;
            SceneManager.LoadScene(SceneNames.Formation);
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
