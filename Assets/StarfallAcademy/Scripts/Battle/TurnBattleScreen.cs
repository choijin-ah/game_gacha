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
    public sealed class TurnBattleScreen : MonoBehaviour
    {
        sealed class UnitView
        {
            public CombatUnit Unit;
            public RectTransform Root;
            public Image Card;
            public Button TargetButton;
            public Image Portrait;
            public Text PortraitMark;
            public Image HpFill;
            public Image ShieldFill;
            public Image EnergyFill;
            public Image BreakFill;
            public Text HpText;
            public Text ShieldText;
            public Text EnergyText;
            public Text WeaknessText;
            public Text StatusText;
            public Text WarningText;
            public Image ActiveFrame;
            public Image TargetFrame;
            public Button UltimateButton;
            public Text UltimateLabel;
            public Text QueueBadge;
            public CanvasGroup CanvasGroup;
        }

        sealed class TurnOrderView
        {
            public RectTransform Root;
            public Image Card;
            public Image Portrait;
            public Text PortraitMark;
            public Text NameText;
            public Text ValueText;
            public Text WarningText;
        }

        readonly Dictionary<CombatUnit, UnitView> unitViews = new Dictionary<CombatUnit, UnitView>();
        readonly Dictionary<Guid, string> autoDecisionReasons = new Dictionary<Guid, string>();
        readonly List<TurnOrderView> turnOrderViews = new List<TurnOrderView>();
        readonly List<Image> skillPointPips = new List<Image>();

        LobbyUiFactory ui;
        StageData stage;
        StageDatabase stageDatabase;
        TurnBattleModel battle;
        AutoDecisionService autoDecisionService;
        RectTransform battlefieldRoot;
        AudioSource battleSfxSource;
        AudioSource battleVoiceSource;
        float currentVoiceVolumeScale = 1f;

        Text actionLabel;
        Text turnLabel;
        Text logLabel;
        Text queueLabel;
        Text autoLabel;
        Text autoPresetLabel;
        Text speedLabel;
        Text commandActorLabel;
        Text attackButtonLabel;
        Text skillButtonLabel;
        Text commandHintLabel;
        Button attackButton;
        Button skillButton;
        Button cancelButton;

        GameObject pauseLayer;
        GameObject resultLayer;
        Text resultTitle;
        Text resultBody;
        Button nextButton;
        Text nextButtonLabel;

        CombatUnit inputActor;
        CombatUnit targetingActor;
        BattleActionConfig targetingAction;
        bool targetIsUltimate;
        bool targetSelectionActive;
        ActionRequest pendingPlayerAction;

        float presentationSpeed = 1f;
        int regularActionCount;
        bool paused;
        bool battleFinished;
        bool changingScene;
        bool victoryProgressCommitted;

        void Awake()
        {
            BuildBattleAudio();
            BuildCanvas();
            BuildScreen();
        }

        void OnEnable()
        {
            GameSettings.Changed += ApplyBattleAudioVolume;
            ApplyBattleAudioVolume();
        }

        void Update()
        {
            if (changingScene || battleFinished) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (paused) ResumeBattle();
                else OpenPause();
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            GameSettings.Changed -= ApplyBattleAudioVolume;
        }

        void BuildBattleAudio()
        {
            battleSfxSource = gameObject.AddComponent<AudioSource>();
            battleSfxSource.playOnAwake = false;
            battleSfxSource.loop = false;
            battleSfxSource.spatialBlend = 0f;
            battleVoiceSource = gameObject.AddComponent<AudioSource>();
            battleVoiceSource.playOnAwake = false;
            battleVoiceSource.loop = false;
            battleVoiceSource.spatialBlend = 0f;
            ApplyBattleAudioVolume();
        }

        void ApplyBattleAudioVolume()
        {
            if (battleSfxSource != null) battleSfxSource.volume = GameSettings.SfxVolume;
            if (battleVoiceSource != null)
                battleVoiceSource.volume = GameSettings.SfxVolume * currentVoiceVolumeScale;
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
            stage = ResolveStage();
            GameAudioDirector.RefreshForCurrentScene(stage);
            CharacterDatabase database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            FormationState formation = new FormationState();
            formation.Load(database);

            BuildBackground(root);
            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            BuildHeader(safeRoot);

            if (stage == null || formation.Count == 0)
            {
                BuildInvalidState(safeRoot,
                    stage == null ? "스테이지 데이터가 없습니다" : "편성된 캐릭터가 없습니다");
                return;
            }

            battle = new TurnBattleModel(formation, stage, null, BattleSession.Rules);
            autoDecisionService = new AutoDecisionService(battle);
            BuildTurnOrderPanel(safeRoot);
            BuildBattlefield(safeRoot);
            BuildActionPanel(safeRoot);
            BuildResultLayer(safeRoot);
            BuildPauseLayer(safeRoot);
            RefreshAll();
            StartCoroutine(BattleLoop());
        }

        StageData ResolveStage()
        {
            if (BattleSession.SelectedStage != null) return BattleSession.SelectedStage;
            if (stageDatabase == null || stageDatabase.Stages.Count == 0) return null;
            int index = -1;
            for (int i = 0; i < stageDatabase.Stages.Count; i++)
            {
                StageData candidate = stageDatabase.Stages[i];
                if (candidate == null || candidate.Category != StageCategory.Main
                    || !StageProgression.IsUnlocked(candidate, i, stageDatabase.Stages)) continue;
                index = i;
                if (!StageProgression.IsCleared(candidate)) break;
            }
            if (index < 0) return null;
            BattleSession.BeginRun(stageDatabase.Stages[index], index, false);
            return BattleSession.SelectedStage;
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Battle Background", root, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Gacha/Art/gacha_portal_v1");
            if (art == null) art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art != null)
            {
                background.sprite = ui.SpriteFromTexture(art);
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
            }
            else background.color = LobbyTheme.Hex("08080C");
            ui.CreateImage("Battle Grade", root, new Color(.015f, .01f, .025f, .45f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Battle Dim", root, new Color(.002f, .002f, .006f, .64f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform root)
        {
            RectTransform header = ui.CreateImage("Battle Header", root,
                new Color(.005f, .005f, .009f, .9f), new Vector2(0, 1), new Vector2(1, 1),
                Vector2.zero, new Vector2(0, 84)).rectTransform;
            ui.CreateImage("Header Line", header, UrbanFantasyStyle.Line,
                Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));

            GameObject back = ui.CreateButton("Pause Or Leave", header, new Vector2(0, .5f),
                new Vector2(45, 0), new Vector2(50, 50), "‹", 30,
                UrbanFantasyStyle.PanelStrong, OpenPause);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());
            ui.CreateText("Battle Stage",
                BattleHeaderText(),
                header, 20, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(285, 0),
                new Vector2(410, 38), TextAnchor.MiddleLeft);

            actionLabel = ui.CreateText("Action Count", "ACTION 000", header, 14, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(180, 28), TextAnchor.MiddleCenter);

            GameObject auto = ui.CreateButton("Auto Battle", header, new Vector2(1, .5f),
                new Vector2(-424, 0), new Vector2(106, 46), string.Empty, 12,
                UrbanFantasyStyle.PanelStrong, ToggleAuto);
            UrbanFantasyStyle.AddBorder(ui, auto.GetComponent<RectTransform>());
            autoLabel = auto.GetComponentInChildren<Text>();

            GameObject preset = ui.CreateButton("Auto Preset", header, new Vector2(1, .5f),
                new Vector2(-293, 0), new Vector2(144, 46), string.Empty, 11,
                UrbanFantasyStyle.PanelStrong, CycleAutoPreset);
            UrbanFantasyStyle.AddBorder(ui, preset.GetComponent<RectTransform>());
            autoPresetLabel = preset.GetComponentInChildren<Text>();

            GameObject speed = ui.CreateButton("Battle Speed", header, new Vector2(1, .5f),
                new Vector2(-156, 0), new Vector2(84, 46), string.Empty, 14,
                UrbanFantasyStyle.PanelStrong, ToggleSpeed);
            UrbanFantasyStyle.AddBorder(ui, speed.GetComponent<RectTransform>());
            speedLabel = speed.GetComponentInChildren<Text>();

            GameObject pause = ui.CreateButton("Pause", header, new Vector2(1, .5f),
                new Vector2(-50, 0), new Vector2(48, 46), "Ⅱ", 16,
                UrbanFantasyStyle.PanelStrong, OpenPause);
            UrbanFantasyStyle.AddBorder(ui, pause.GetComponent<RectTransform>());

            RefreshHeaderControls();
        }

        void BuildTurnOrderPanel(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Turn Order Panel", root,
                new Color(.006f, .006f, .011f, .9f), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -145), new Vector2(1060, 110)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel, UrbanFantasyStyle.StrongLine);

            queueLabel = ui.CreateText("Ultimate Queue", string.Empty, panel, 10, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -10), new Vector2(-24, 18), TextAnchor.MiddleRight);

            RectTransform viewport = CreateLayer("Order Viewport", panel);
            viewport.offsetMin = new Vector2(18, 9);
            viewport.offsetMax = new Vector2(-18, -23);
            viewport.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 8; i++) turnOrderViews.Add(CreateTurnOrderView(viewport, i));
        }

        TurnOrderView CreateTurnOrderView(RectTransform parent, int index)
        {
            float x = -438f + index * 125f;
            Image card = ui.CreateImage("Order " + (index + 1), parent, UrbanFantasyStyle.PanelSoft,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(x, 0), new Vector2(108, 73));
            UrbanFantasyStyle.AddBorder(ui, card.rectTransform, UrbanFantasyStyle.Line);
            Image portrait = ui.CreateImage("Portrait", card.transform, new Color(.06f, .06f, .08f, 1f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(27, 0), new Vector2(48, 58));
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            Text mark = ui.CreateText("Mark", "✦", portrait.transform, 20, FontStyle.Normal,
                UrbanFantasyStyle.Silver, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);
            Text name = ui.CreateText("Name", string.Empty, card.transform, 10, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-27, -20), new Vector2(48, 22), TextAnchor.MiddleCenter);
            Text value = ui.CreateText("Action Value", string.Empty, card.transform, 9, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-27, 15), new Vector2(48, 18), TextAnchor.MiddleCenter);
            Text warning = ui.CreateText("Warning", string.Empty, card.transform, 11, FontStyle.Bold,
                new Color(.95f, .2f, .16f, 1f), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-8, -8), new Vector2(22, 22), TextAnchor.MiddleCenter);
            return new TurnOrderView
            {
                Root = card.rectTransform,
                Card = card,
                Portrait = portrait,
                PortraitMark = mark,
                NameText = name,
                ValueText = value,
                WarningText = warning
            };
        }

        void BuildBattlefield(RectTransform root)
        {
            battlefieldRoot = root;
            turnLabel = ui.CreateText("Turn Banner", "전투 준비", root, 25, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 65), new Vector2(760, 50), TextAnchor.MiddleCenter);
            logLabel = ui.CreateText("Battle Log", "행동 순서를 계산하고 있습니다", root, 14,
                FontStyle.Normal, new Color(.88f, .88f, .91f, .78f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 19),
                new Vector2(820, 38), TextAnchor.MiddleCenter);

            for (int i = 0; i < battle.Players.Count && i < 4; i++)
            {
                CombatUnit unit = battle.Players[i];
                Vector2 position = new Vector2(165 + i * 282, 177);
                unitViews[unit] = CreateUnitView(root, unit, new Vector2(0, 0), position,
                    new Vector2(262, 246));
            }

            int enemyCount = Mathf.Min(StageData.MaxEnemyCount, battle.Enemies.Count);
            for (int i = 0; i < enemyCount; i++)
            {
                CombatUnit unit = battle.Enemies[i];
                Vector2 position = new Vector2(-165 - (enemyCount - 1 - i) * 310, -350);
                unitViews[unit] = CreateUnitView(root, unit, new Vector2(1, 1), position,
                    new Vector2(288, 256));
            }
        }

        UnitView CreateUnitView(RectTransform root, CombatUnit unit, Vector2 anchor,
            Vector2 position, Vector2 size)
        {
            bool enemy = unit.Team == BattleTeam.Enemy;
            Image card = ui.CreateImage((enemy ? "Enemy " : "Player ") + unit.DisplayName, root,
                UrbanFantasyStyle.PanelStrong, anchor, anchor, position, size, true);
            UrbanFantasyStyle.AddBorder(ui, card.rectTransform, enemy
                ? new Color(.66f, .14f, .14f, .66f) : UrbanFantasyStyle.StrongLine);

            Button targetButton = card.gameObject.AddComponent<Button>();
            targetButton.targetGraphic = card;
            targetButton.transition = Selectable.Transition.None;
            targetButton.navigation = new Navigation { mode = Navigation.Mode.None };
            targetButton.onClick.AddListener(() => OnUnitClicked(unit));

            Image activeFrame = ui.CreateImage("Active Frame", card.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
            activeFrame.transform.SetAsFirstSibling();
            Image targetFrame = ui.CreateImage("Target Frame", card.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12, -12));
            targetFrame.transform.SetAsFirstSibling();

            Image portrait = ui.CreateImage("Portrait", card.transform, new Color(.07f, .07f, .095f, 1f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(55, 5),
                new Vector2(94, enemy ? 184 : 158));
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            Text portraitMark = ui.CreateText("Portrait Mark", "✦", portrait.transform, 30,
                FontStyle.Normal, enemy ? new Color(.82f, .2f, .18f, .9f) : UrbanFantasyStyle.Silver,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            ui.CreateText("Name", unit.DisplayName, card.transform, 16, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-77, -25), new Vector2(140, 28), TextAnchor.MiddleLeft);
            ui.CreateText("Element", ElementLabel(unit.Element), card.transform, 10, FontStyle.Normal,
                ElementColor(unit.Element), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-77, -50), new Vector2(140, 20), TextAnchor.MiddleLeft);

            float rightX = enemy ? -79 : -75;
            float trackWidth = enemy ? 142 : 132;
            Image hpTrack = ui.CreateImage("HP Track", card.transform, new Color(1, 1, 1, .12f),
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(rightX, 29),
                new Vector2(trackWidth, 10));
            Image hpFill = CreateFill("HP Fill", hpTrack.transform,
                enemy ? new Color(.68f, .11f, .1f, 1f) : new Color(.72f, .75f, .8f, 1f));
            Image shieldFill = CreateFill("Shield Fill", hpTrack.transform,
                new Color(.24f, .72f, .95f, .8f));
            Text hpText = ui.CreateText("HP", string.Empty, card.transform, 10, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(rightX, 10), new Vector2(trackWidth, 18), TextAnchor.MiddleRight);
            Text shieldText = ui.CreateText("Shield", string.Empty, card.transform, 9, FontStyle.Normal,
                new Color(.42f, .82f, 1f, .9f), new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(rightX, -9), new Vector2(trackWidth, 18), TextAnchor.MiddleRight);

            Image energyFill = null;
            Image breakFill = null;
            Text energyText = null;
            Text weaknessText = null;
            Button ultimateButton = null;
            Text ultimateLabel = null;
            Text queueBadge = null;

            if (enemy)
            {
                Image breakTrack = ui.CreateImage("Break Track", card.transform,
                    new Color(1, 1, 1, .1f), new Vector2(1, .5f), new Vector2(1, .5f),
                    new Vector2(rightX, -34), new Vector2(trackWidth, 8));
                breakFill = CreateFill("Break Fill", breakTrack.transform,
                    new Color(.78f, .64f, .27f, 1f));
                weaknessText = ui.CreateText("Weaknesses", string.Empty, card.transform, 10,
                    FontStyle.Normal, UrbanFantasyStyle.Gold, new Vector2(1, .5f), new Vector2(1, .5f),
                    new Vector2(rightX, -58), new Vector2(trackWidth, 22), TextAnchor.MiddleRight);
            }
            else
            {
                Image energyTrack = ui.CreateImage("Energy Track", card.transform,
                    new Color(1, 1, 1, .1f), new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0, 18), new Vector2(-82, 7));
                energyFill = CreateFill("Energy Fill", energyTrack.transform,
                    new Color(.77f, .65f, .3f, 1f));
                energyText = ui.CreateText("Energy", string.Empty, card.transform, 9,
                    FontStyle.Normal, UrbanFantasyStyle.Gold, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(0, 35), new Vector2(-90, 18), TextAnchor.MiddleRight);

                GameObject ultimate = ui.CreateButton("Ultimate", card.transform, new Vector2(1, 0),
                    new Vector2(-36, 28), new Vector2(60, 52), string.Empty, 10,
                    new Color(.14f, .12f, .09f, 1f), () => BeginUltimate(unit));
                UrbanFantasyStyle.AddBorder(ui, ultimate.GetComponent<RectTransform>(), UrbanFantasyStyle.Gold);
                ultimateButton = ultimate.GetComponent<Button>();
                ultimateLabel = ultimate.GetComponentInChildren<Text>();
                queueBadge = ui.CreateText("Queue Badge", string.Empty, ultimate.transform, 10,
                    FontStyle.Bold, new Color(1f, .45f, .32f, 1f), new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-4, -4), new Vector2(28, 20), TextAnchor.MiddleCenter);
            }

            Text statusText = ui.CreateText("Statuses", string.Empty, card.transform, 9,
                FontStyle.Normal, new Color(.75f, .78f, .84f, .9f), new Vector2(0, 0),
                new Vector2(1, 0), new Vector2(0, enemy ? 18 : 58), new Vector2(-20, 20),
                TextAnchor.MiddleLeft, true);
            UiTooltipTrigger statusTooltip = statusText.gameObject.AddComponent<UiTooltipTrigger>();
            statusTooltip.Initialize((RectTransform)transform, ui,
                "상태 효과 · " + unit.DisplayName, () => BuildStatusTooltip(unit));
            Text warningText = ui.CreateText("Danger Warning", string.Empty, card.transform, 11,
                FontStyle.Bold, new Color(.95f, .22f, .17f, 1f), new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(0, 16), new Vector2(-16, 24), TextAnchor.MiddleCenter);

            CanvasGroup group = card.gameObject.AddComponent<CanvasGroup>();
            return new UnitView
            {
                Unit = unit,
                Root = card.rectTransform,
                Card = card,
                TargetButton = targetButton,
                Portrait = portrait,
                PortraitMark = portraitMark,
                HpFill = hpFill,
                ShieldFill = shieldFill,
                EnergyFill = energyFill,
                BreakFill = breakFill,
                HpText = hpText,
                ShieldText = shieldText,
                EnergyText = energyText,
                WeaknessText = weaknessText,
                StatusText = statusText,
                WarningText = warningText,
                ActiveFrame = activeFrame,
                TargetFrame = targetFrame,
                UltimateButton = ultimateButton,
                UltimateLabel = ultimateLabel,
                QueueBadge = queueBadge,
                CanvasGroup = group
            };
        }

        void BuildActionPanel(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Command Panel", root,
                new Color(.007f, .007f, .012f, .94f), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-320, 171), new Vector2(590, 272)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel, UrbanFantasyStyle.StrongLine);

            commandActorLabel = ui.CreateText("Command Actor", "행동 대기", panel, 17,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(118, -29), new Vector2(210, 28), TextAnchor.MiddleLeft);
            ui.CreateText("SP Label", "SKILL POINT", panel, 10, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-165, -25), new Vector2(110, 20), TextAnchor.MiddleRight);
            for (int i = 0; i < 5; i++)
            {
                Image pip = ui.CreateImage("SP " + (i + 1), panel, new Color(1, 1, 1, .12f),
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-92 + i * 22, -28),
                    new Vector2(15, 15));
                pip.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
                skillPointPips.Add(pip);
            }

            GameObject attack = ui.CreateButton("Basic Attack", panel, new Vector2(0, 0),
                new Vector2(145, 107), new Vector2(250, 106), string.Empty, 15,
                UrbanFantasyStyle.PanelSoft, ChooseBasicAttack);
            UrbanFantasyStyle.AddBorder(ui, attack.GetComponent<RectTransform>());
            attackButton = attack.GetComponent<Button>();
            attackButtonLabel = attack.GetComponentInChildren<Text>();

            GameObject skill = ui.CreateButton("Battle Skill", panel, new Vector2(0, 0),
                new Vector2(425, 107), new Vector2(250, 106), string.Empty, 15,
                new Color(.13f, .13f, .16f, 1f), ChooseBattleSkill);
            UrbanFantasyStyle.AddBorder(ui, skill.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            skillButton = skill.GetComponent<Button>();
            skillButtonLabel = skill.GetComponentInChildren<Text>();

            GameObject cancel = ui.CreateButton("Cancel Target", panel, new Vector2(1, 0),
                new Vector2(-69, 32), new Vector2(118, 38), "취소", 12,
                UrbanFantasyStyle.PanelSoft, CancelTargeting);
            cancelButton = cancel.GetComponent<Button>();
            commandHintLabel = ui.CreateText("Command Hint", "행동 순서를 기다리는 중입니다", panel,
                11, FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(-55, 31), new Vector2(-170, 32), TextAnchor.MiddleLeft);
            SetCommandInput(false);
        }

        void BuildPauseLayer(RectTransform root)
        {
            pauseLayer = new GameObject("Pause Layer", typeof(RectTransform));
            pauseLayer.transform.SetParent(root, false);
            RectTransform layer = pauseLayer.GetComponent<RectTransform>();
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            ui.CreateImage("Pause Backdrop", pauseLayer.transform, new Color(0, 0, 0, .78f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            RectTransform card = ui.CreateImage("Pause Card", pauseLayer.transform,
                UrbanFantasyStyle.PanelStrong, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(520, 360), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            ui.CreateText("Pause Title", "일 시 정 지", card, 30, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -72), new Vector2(-40, 48), TextAnchor.MiddleCenter);
            ui.CreateText("Pause Body", "전투 진행이 정지되었습니다\nESC 키로도 전투를 재개할 수 있습니다", card,
                14, FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, .5f), new Vector2(1, .5f),
                new Vector2(0, 30), new Vector2(-50, 70), TextAnchor.MiddleCenter);
            GameObject resume = ui.CreateButton("Resume", card, new Vector2(.5f, 0),
                new Vector2(0, 112), new Vector2(300, 58), "전투 재개", 16,
                new Color(.18f, .18f, .21f, 1f), ResumeBattle);
            UrbanFantasyStyle.AddBorder(ui, resume.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            GameObject retreat = ui.CreateButton("Retreat", card, new Vector2(.5f, 0),
                new Vector2(0, 45), new Vector2(300, 48), "작전에서 퇴각", 14,
                UrbanFantasyStyle.PanelSoft, ReturnToStages);
            UrbanFantasyStyle.AddBorder(ui, retreat.GetComponent<RectTransform>());
            pauseLayer.SetActive(false);
        }

        void BuildResultLayer(RectTransform root)
        {
            resultLayer = new GameObject("Battle Result Layer", typeof(RectTransform));
            resultLayer.transform.SetParent(root, false);
            RectTransform layer = resultLayer.GetComponent<RectTransform>();
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            ui.CreateImage("Result Backdrop", resultLayer.transform, UrbanFantasyStyle.Backdrop,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            RectTransform card = ui.CreateImage("Result Card", resultLayer.transform,
                UrbanFantasyStyle.PanelStrong, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(720, 500), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            resultTitle = ui.CreateText("Result Title", string.Empty, card, 42, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -95), new Vector2(-70, 60), TextAnchor.MiddleCenter);
            resultBody = ui.CreateText("Result Body", string.Empty, card, 18, FontStyle.Normal,
                new Color(.88f, .88f, .91f, .78f), new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, -30), new Vector2(-90, -230), TextAnchor.MiddleCenter);
            GameObject stages = ui.CreateButton("Back To Stages", card, new Vector2(0, 0),
                new Vector2(210, 58), new Vector2(250, 62), "스테이지 선택", 17,
                UrbanFantasyStyle.PanelSoft, ReturnToStages);
            UrbanFantasyStyle.AddBorder(ui, stages.GetComponent<RectTransform>());
            GameObject next = ui.CreateButton("Next Stage", card, new Vector2(1, 0),
                new Vector2(-210, 58), new Vector2(250, 62), string.Empty, 17,
                new Color(.18f, .18f, .21f, 1), NextStage);
            UrbanFantasyStyle.AddBorder(ui, next.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            nextButton = next.GetComponent<Button>();
            nextButtonLabel = next.GetComponentInChildren<Text>();
            resultLayer.SetActive(false);
        }

        void BuildInvalidState(RectTransform root, string message)
        {
            RectTransform card = ui.CreateImage("Invalid Battle", root, UrbanFantasyStyle.PanelStrong,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(620, 300)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card);
            ui.CreateText("Message", message, card, 24, FontStyle.Normal, UrbanFantasyStyle.Silver,
                Vector2.zero, Vector2.one, new Vector2(0, 35), new Vector2(-70, -90),
                TextAnchor.MiddleCenter);
            ui.CreateButton("Return", card, new Vector2(.5f, 0), new Vector2(0, 52),
                new Vector2(220, 58), "돌아가기", 17, UrbanFantasyStyle.PanelSoft, ReturnToStages);
        }

        IEnumerator BattleLoop()
        {
            yield return PresentationWait(.65f);
            while (battle.Outcome == BattleOutcome.Ongoing && !changingScene)
            {
                yield return WaitForPauseAndUltimateTarget();
                QueueReadyAutoUltimates();
                while (battle.Outcome == BattleOutcome.Ongoing
                    && battle.TryExecuteNextUltimate(out ActionResolution queuedBeforeTurn))
                {
                    if (queuedBeforeTurn?.Request?.Actor != null)
                        turnLabel.text = "ULTIMATE  ·  " + queuedBeforeTurn.Request.Actor.DisplayName;
                    ShowResolution(queuedBeforeTurn, true,
                        TakeAutoDecisionReason(queuedBeforeTurn?.Request));
                    RefreshAll();
                    yield return PresentationWait(.62f);
                    yield return WaitForPauseAndUltimateTarget();
                    QueueReadyAutoUltimates();
                }
                if (battle.Outcome != BattleOutcome.Ongoing) break;

                CombatUnit actor = battle.NextActor();
                if (actor == null)
                {
                    yield return null;
                    continue;
                }

                regularActionCount++;
                actionLabel.text = "ACTION " + regularActionCount.ToString("000");
                turnLabel.text = actor.DisplayName + "의 행동";
                RefreshAll();

                ActionRequest request;
                if (actor.Team == BattleTeam.Enemy)
                {
                    SetCommandInput(false);
                    logLabel.text = actor.Warning
                        ? "⚠ " + (string.IsNullOrWhiteSpace(actor.WarningText) ? "강력한 공격 준비" : actor.WarningText)
                        : actor.DisplayName + "이(가) 행동을 결정합니다";
                    yield return PresentationWait(.42f);
                    request = battle.CreateEnemyAction(actor);
                }
                else
                {
                    request = null;
                    inputActor = actor;
                    pendingPlayerAction = null;
                    RefreshCommandPanel();
                    if (GameSettings.AutoBattle)
                    {
                        yield return PresentationWait(.18f);
                        request = CreateAutoAction(actor);
                    }
                    else
                    {
                        SetCommandInput(true);
                        while (pendingPlayerAction == null && battle.Outcome == BattleOutcome.Ongoing
                            && !changingScene)
                        {
                            if (GameSettings.AutoBattle && !targetSelectionActive)
                            {
                                pendingPlayerAction = CreateAutoAction(actor);
                                break;
                            }
                            yield return null;
                        }
                        request = pendingPlayerAction;
                    }
                    SetCommandInput(false);
                    inputActor = null;
                    pendingPlayerAction = null;
                    ClearTargetingState();
                }

                if (request != null && battle.Outcome == BattleOutcome.Ongoing)
                {
                    ActionResolution resolution = battle.Execute(request);
                    ShowResolution(resolution, false, TakeAutoDecisionReason(request));
                    RefreshAll();
                    yield return PresentationWait(resolution != null && resolution.Success ? .48f : .2f);
                }

                yield return WaitForPauseAndUltimateTarget();
                QueueReadyAutoUltimates();
                while (battle.Outcome == BattleOutcome.Ongoing
                    && battle.TryExecuteNextUltimate(out ActionResolution ultimateResolution))
                {
                    if (ultimateResolution?.Request?.Actor != null)
                        turnLabel.text = "ULTIMATE  ·  " + ultimateResolution.Request.Actor.DisplayName;
                    ShowResolution(ultimateResolution, true,
                        TakeAutoDecisionReason(ultimateResolution?.Request));
                    RefreshAll();
                    yield return PresentationWait(.62f);
                    yield return WaitForPauseAndUltimateTarget();
                    QueueReadyAutoUltimates();
                }
            }

            SetCommandInput(false);
            ClearTargetingState();
            RefreshAll();
            if (!changingScene) ShowResult(battle.CreateResult());
        }

        IEnumerator WaitForPauseAndUltimateTarget()
        {
            while ((paused || targetSelectionActive && targetIsUltimate) && !changingScene)
                yield return null;
        }

        IEnumerator PresentationWait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !changingScene)
            {
                if (!paused) elapsed += Time.unscaledDeltaTime * presentationSpeed;
                yield return null;
            }
        }

        ActionRequest CreateAutoAction(CombatUnit actor)
        {
            if (autoDecisionService == null) return null;
            AutoActionDecision decision = autoDecisionService.DecideRegularAction(actor,
                AutoBattleSettings.CurrentPreset);
            if (decision.Request != null)
            {
                autoDecisionReasons[decision.Request.RequestId] = decision.Reason;
                logLabel.text = "AUTO  ·  " + decision.Reason;
            }
            else if (!string.IsNullOrWhiteSpace(decision.Reason))
                logLabel.text = "AUTO  ·  " + decision.Reason;
            return decision.Request;
        }

        void QueueReadyAutoUltimates()
        {
            if (!GameSettings.AutoBattle || autoDecisionService == null || paused
                || battleFinished || targetSelectionActive
                || battle.Outcome != BattleOutcome.Ongoing) return;
            var queued = new List<string>();
            foreach (CombatUnit player in battle.Players)
            {
                AutoUltimateDecision decision = autoDecisionService.TryQueueUltimate(player,
                    AutoBattleSettings.CurrentPreset);
                if (!decision.Queued || decision.Request == null) continue;
                autoDecisionReasons[decision.Request.RequestId] = decision.Reason;
                queued.Add(player.DisplayName + " · " + decision.Reason);
            }
            if (queued.Count > 0)
                logLabel.text = "AUTO ULT  ·  " + string.Join("   /   ", queued);
        }

        string TakeAutoDecisionReason(ActionRequest request)
        {
            if (request == null || !autoDecisionReasons.TryGetValue(request.RequestId, out string reason))
                return string.Empty;
            autoDecisionReasons.Remove(request.RequestId);
            return reason;
        }

        void ChooseBasicAttack()
        {
            if (!CanChooseRegularAction()) return;
            BeginTargeting(inputActor, inputActor.CharacterData.BasicAction, false);
        }

        void ChooseBattleSkill()
        {
            if (!CanChooseRegularAction()) return;
            BattleActionConfig config = inputActor.CharacterData.SkillAction;
            if (config.SkillPointCost > 0 && battle.SkillPoints < config.SkillPointCost)
            {
                commandHintLabel.text = "스킬 포인트가 부족합니다";
                return;
            }
            BeginTargeting(inputActor, config, false);
        }

        bool CanChooseRegularAction()
        {
            return !paused && !battleFinished && inputActor != null && inputActor.IsAlive
                && inputActor.CharacterData != null && battle.CurrentActor == inputActor;
        }

        void BeginUltimate(CombatUnit actor)
        {
            if (paused || battleFinished || actor == null || !actor.IsAlive
                || actor.Team != BattleTeam.Player || actor.CharacterData == null) return;
            BattleActionConfig config = actor.CharacterData.UltimateAction;
            float cost = config.EnergyCost > 0 ? config.EnergyCost : actor.MaxEnergy;
            if (!actor.CanUseUltimate(cost))
            {
                logLabel.text = actor.DisplayName + "의 필살기 에너지가 부족합니다";
                return;
            }
            if (IsUltimateQueued(actor))
            {
                logLabel.text = actor.DisplayName + "의 필살기는 이미 대기열에 있습니다";
                return;
            }
            BeginTargeting(actor, config, true);
        }

        void BeginTargeting(CombatUnit actor, BattleActionConfig config, bool ultimate)
        {
            if (actor == null || !actor.IsAlive) return;
            if (!NeedsTargetSelection(config.TargetType))
            {
                CombatUnit target = battle.AutoTarget(actor, config);
                SubmitAction(actor, config, target, ultimate);
                return;
            }

            targetingActor = actor;
            targetingAction = config;
            targetIsUltimate = ultimate;
            targetSelectionActive = true;
            commandHintLabel.text = ultimate
                ? actor.DisplayName + " 필살기의 대상을 선택하세요"
                : "유효한 대상을 선택하세요";
            cancelButton.interactable = true;
            RefreshAll();
        }

        void OnUnitClicked(CombatUnit target)
        {
            if (paused || !targetSelectionActive || targetingActor == null) return;
            if (!IsValidPrimaryTarget(targetingActor, targetingAction.TargetType, target)) return;
            SubmitAction(targetingActor, targetingAction, target, targetIsUltimate);
        }

        void SubmitAction(CombatUnit actor, BattleActionConfig config, CombatUnit target, bool ultimate)
        {
            ActionRequest request = battle.CreatePlayerAction(actor, config, target);
            if (request == null)
            {
                logLabel.text = "행동을 생성할 수 없습니다";
                ClearTargetingState();
                RefreshAll();
                return;
            }

            if (ultimate)
            {
                if (battle.QueueUltimate(request, out string reason))
                    logLabel.text = actor.DisplayName + "의 필살기가 대기열에 등록되었습니다";
                else
                    logLabel.text = LocalizeFailure(reason);
            }
            else pendingPlayerAction = request;

            ClearTargetingState();
            RefreshAll();
        }

        void CancelTargeting()
        {
            if (!targetSelectionActive) return;
            bool wasUltimate = targetIsUltimate;
            ClearTargetingState();
            commandHintLabel.text = wasUltimate && inputActor == null
                ? "필살기 입력을 취소했습니다"
                : "행동을 선택하세요";
            RefreshAll();
        }

        void ClearTargetingState()
        {
            targetingActor = null;
            targetingAction = default;
            targetIsUltimate = false;
            targetSelectionActive = false;
            if (cancelButton != null) cancelButton.interactable = false;
        }

        static bool NeedsTargetSelection(BattleTargetType targetType)
        {
            return targetType == BattleTargetType.SingleEnemy
                || targetType == BattleTargetType.SingleAlly
                || targetType == BattleTargetType.AdjacentEnemies
                || targetType == BattleTargetType.AdjacentAllies;
        }

        static bool IsValidPrimaryTarget(CombatUnit actor, BattleTargetType targetType, CombatUnit target)
        {
            if (actor == null || target == null || !target.IsAlive) return false;
            switch (targetType)
            {
                case BattleTargetType.SingleEnemy:
                case BattleTargetType.AdjacentEnemies:
                    return actor.Team != target.Team;
                case BattleTargetType.SingleAlly:
                case BattleTargetType.AdjacentAllies:
                    return actor.Team == target.Team;
                case BattleTargetType.Self:
                    return ReferenceEquals(actor, target);
                default:
                    return false;
            }
        }

        void ShowResolution(ActionResolution resolution, bool ultimate, string autoReason = "")
        {
            if (resolution == null)
            {
                logLabel.text = "행동 결과를 확인할 수 없습니다";
                return;
            }
            if (!resolution.Success)
            {
                logLabel.text = LocalizeFailure(resolution.FailureReason);
                AppendAutoReason(autoReason);
                return;
            }

            PlayActionAudio(resolution);

            string actorName = resolution.Request?.Actor?.DisplayName ?? "유닛";
            string skillName = resolution.Request?.SkillName;
            if (string.IsNullOrWhiteSpace(skillName)) skillName = ultimate ? "필살기" : "행동";
            int totalDamage = 0;
            int totalHealing = 0;
            bool critical = false;
            bool broken = false;
            foreach (DamageResult result in resolution.DamageResults)
            {
                totalDamage += result.FinalDamage;
                critical |= result.IsCritical;
            }
            foreach (HealingResult result in resolution.HealingResults) totalHealing += result.AppliedHealing;
            foreach (BreakResult result in resolution.BreakResults)
                broken |= result.BreakTriggered && result.Target != null && result.Target.IsBroken;

            string detail = totalDamage > 0 ? totalDamage.ToString("N0") + " 피해" : string.Empty;
            if (totalHealing > 0) detail += (detail.Length > 0 ? "  ·  " : string.Empty)
                + totalHealing.ToString("N0") + " 회복";
            if (critical) detail += "  ·  CRITICAL";
            if (broken) detail += "  ·  BREAK";
            if (detail.Length == 0) detail = "효과 적용";
            logLabel.text = actorName + "  /  " + skillName + "  ·  " + detail;
            AppendAutoReason(autoReason);
        }

        void PlayActionAudio(ActionResolution resolution)
        {
            CharacterData character = resolution?.Request?.Actor?.CharacterData;
            if (character == null) return;

            BattleActionKind kind = resolution.Request.Kind;
            AudioClip sfx = character.ResolveActionSfx(kind);
            if (sfx != null && battleSfxSource != null)
                battleSfxSource.PlayOneShot(sfx, character.ActionSfxVolume);

            AudioClip voice = character.ResolveActionVoice(kind);
            if (voice == null || battleVoiceSource == null) return;
            currentVoiceVolumeScale = character.VoiceVolume;
            battleVoiceSource.Stop();
            battleVoiceSource.clip = voice;
            ApplyBattleAudioVolume();
            battleVoiceSource.Play();
        }

        void AppendAutoReason(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                logLabel.text += "\nAUTO 판단  ·  " + reason;
        }

        void RefreshAll()
        {
            if (battle == null) return;
            SyncSummonedEnemies();
            RefreshTurnOrder();
            RefreshResources();
            foreach (KeyValuePair<CombatUnit, UnitView> pair in unitViews)
                RefreshUnitView(pair.Key, pair.Value);
            RefreshCommandPanel();
        }

        void SyncSummonedEnemies()
        {
            if (battlefieldRoot == null) return;
            bool added = false;
            foreach (CombatUnit enemy in battle.Enemies)
            {
                if (unitViews.ContainsKey(enemy)) continue;
                unitViews[enemy] = CreateUnitView(battlefieldRoot, enemy, Vector2.one,
                    Vector2.zero, new Vector2(288, 256));
                added = true;
            }
            if (!added) return;
            int count = Mathf.Min(StageData.MaxEnemyCount, battle.Enemies.Count);
            for (int i = 0; i < count; i++)
            {
                CombatUnit enemy = battle.Enemies[i];
                if (!unitViews.TryGetValue(enemy, out UnitView view)) continue;
                view.Root.anchoredPosition = new Vector2(-165 - (count - 1 - i) * 310, -350);
            }
        }

        void RefreshTurnOrder()
        {
            IReadOnlyList<TurnPreviewEntry> timeline = battle.Core.Turns.PeekTimeline(8);
            for (int i = 0; i < turnOrderViews.Count; i++)
            {
                TurnOrderView view = turnOrderViews[i];
                bool visible = i < timeline.Count && timeline[i]?.Unit != null;
                view.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                TurnPreviewEntry entry = timeline[i];
                CombatUnit unit = entry.Unit;
                bool enemy = unit.Team == BattleTeam.Enemy;
                view.Card.color = entry.IsCurrent
                    ? new Color(.3f, .25f, .13f, 1f)
                    : enemy ? new Color(.14f, .055f, .06f, 1f) : UrbanFantasyStyle.PanelSoft;
                view.NameText.text = ShortName(unit.DisplayName, 5);
                view.ValueText.text = entry.IsCurrent ? "NOW"
                    : "AV " + Mathf.CeilToInt((float)entry.RelativeActionValue);
                view.ValueText.color = entry.IsCurrent ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted;
                view.WarningText.text = unit.Warning ? "!" : string.Empty;
                ApplyPortrait(view.Portrait, view.PortraitMark, unit);
            }

            var queuedNames = new List<string>();
            foreach (ActionRequest request in battle.Core.Ultimates.Pending)
                if (request?.Actor != null)
                    queuedNames.Add(request.QueueSequence + ". " + request.Actor.DisplayName);
            queueLabel.text = queuedNames.Count > 0
                ? "ULT QUEUE  ·  " + string.Join("   ", queuedNames)
                : "NEXT ACTION ×8";
        }

        void RefreshResources()
        {
            for (int i = 0; i < skillPointPips.Count; i++)
                skillPointPips[i].color = i < battle.SkillPoints
                    ? UrbanFantasyStyle.Gold : new Color(1, 1, 1, .12f);
        }

        void RefreshUnitView(CombatUnit unit, UnitView view)
        {
            SetFill(view.HpFill, unit.HpRatio);
            SetFill(view.ShieldFill, unit.MaxHp > 0f ? unit.Shield / unit.MaxHp : 0f);
            view.HpText.text = Mathf.CeilToInt(unit.CurrentHp).ToString("N0") + " / "
                + Mathf.CeilToInt(unit.MaxHp).ToString("N0");
            view.ShieldText.text = unit.Shield > .5f
                ? "SHIELD  " + Mathf.CeilToInt(unit.Shield).ToString("N0") : string.Empty;
            view.CanvasGroup.alpha = unit.IsAlive ? 1f : .32f;
            ApplyPortrait(view.Portrait, view.PortraitMark, unit);

            bool active = ReferenceEquals(battle.CurrentActor, unit);
            view.ActiveFrame.color = active ? new Color(.95f, .78f, .34f, .19f) : Color.clear;
            bool validTarget = targetSelectionActive
                && IsValidPrimaryTarget(targetingActor, targetingAction.TargetType, unit);
            view.TargetFrame.color = validTarget
                ? unit.Team == BattleTeam.Enemy
                    ? new Color(.95f, .18f, .14f, .2f)
                    : new Color(.2f, .75f, .98f, .2f)
                : Color.clear;
            view.TargetButton.interactable = validTarget && !paused;

            view.StatusText.text = BuildStatusText(unit);
            view.WarningText.text = unit.Warning
                ? "⚠ " + (string.IsNullOrWhiteSpace(unit.WarningText) ? "강공격 준비" : unit.WarningText)
                : unit.IsBroken ? "BREAK" : !unit.IsAlive ? "DEFEATED"
                : unit.IsBoss && unit.Phase >= 2 ? "PHASE 2" : string.Empty;

            if (unit.Team == BattleTeam.Enemy)
            {
                SetFill(view.BreakFill, unit.BreakMax > 0f ? unit.BreakCurrent / unit.BreakMax : 0f);
                view.WeaknessText.text = "약점  " + BuildWeaknessText(unit);
            }
            else
            {
                SetFill(view.EnergyFill, unit.MaxEnergy > 0f ? unit.Energy / unit.MaxEnergy : 0f);
                view.EnergyText.text = "ENERGY  " + Mathf.FloorToInt(unit.Energy) + " / "
                    + Mathf.CeilToInt(unit.MaxEnergy);
                RefreshUltimateButton(unit, view);
            }
        }

        void RefreshUltimateButton(CombatUnit unit, UnitView view)
        {
            if (view.UltimateButton == null || unit.CharacterData == null) return;
            BattleActionConfig config = unit.CharacterData.UltimateAction;
            float cost = config.EnergyCost > 0 ? config.EnergyCost : unit.MaxEnergy;
            bool queued = IsUltimateQueued(unit);
            bool ready = unit.IsAlive && unit.CanUseUltimate(cost) && !queued;
            view.UltimateButton.interactable = ready && !paused && !battleFinished;
            view.UltimateLabel.text = queued ? "ULT\n대기" : ready ? "ULT\nREADY" : "ULT\n"
                + Mathf.FloorToInt(unit.MaxEnergy > 0 ? unit.Energy / unit.MaxEnergy * 100f : 0f) + "%";
            view.UltimateLabel.color = ready ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted;
            int sequence = UltimateQueueSequence(unit);
            view.QueueBadge.text = sequence > 0 ? sequence.ToString() : string.Empty;
        }

        void RefreshCommandPanel()
        {
            if (battle == null || commandActorLabel == null) return;
            CombatUnit actor = inputActor;
            bool canInput = actor != null && actor.IsAlive && actor.CharacterData != null
                && !paused && !battleFinished;
            if (!canInput)
            {
                commandActorLabel.text = targetSelectionActive && targetIsUltimate
                    ? targetingActor.DisplayName + "  ·  ULTIMATE TARGET"
                    : "행동 순서를 기다리는 중";
                attackButtonLabel.text = "일반 공격";
                skillButtonLabel.text = "전투 스킬";
                if (!targetSelectionActive) commandHintLabel.text = "현재 행동 유닛을 확인하세요";
                attackButton.interactable = false;
                skillButton.interactable = false;
                cancelButton.interactable = targetSelectionActive && !paused;
                return;
            }

            BattleActionConfig basic = actor.CharacterData.BasicAction;
            BattleActionConfig skill = actor.CharacterData.SkillAction;
            commandActorLabel.text = actor.DisplayName + "  ·  " + ElementLabel(actor.Element);
            attackButtonLabel.text = basic.Name + "\n<color=#C9A866>SP "
                + ResourceDeltaLabel(basic.SkillPointCost) + "</color>";
            skillButtonLabel.text = skill.Name + "\n<color=#C9A866>SP "
                + ResourceDeltaLabel(skill.SkillPointCost) + "</color>";
            bool enoughSp = skill.SkillPointCost <= 0 || battle.SkillPoints >= skill.SkillPointCost;
            attackButton.interactable = !targetSelectionActive;
            skillButton.interactable = !targetSelectionActive && enoughSp;
            cancelButton.interactable = targetSelectionActive;
            if (!targetSelectionActive)
                commandHintLabel.text = enoughSp ? "행동을 선택하세요" : "스킬 포인트가 부족합니다";
        }

        void SetCommandInput(bool enabled)
        {
            if (!enabled)
            {
                if (attackButton != null) attackButton.interactable = false;
                if (skillButton != null) skillButton.interactable = false;
                if (cancelButton != null) cancelButton.interactable = targetSelectionActive && !paused;
            }
            else RefreshCommandPanel();
        }

        bool IsUltimateQueued(CombatUnit actor)
        {
            return UltimateQueueSequence(actor) > 0;
        }

        int UltimateQueueSequence(CombatUnit actor)
        {
            if (battle?.Core?.Ultimates == null || actor == null) return 0;
            foreach (ActionRequest request in battle.Core.Ultimates.Pending)
                if (request != null && ReferenceEquals(request.Actor, actor)) return request.QueueSequence;
            return 0;
        }

        void ToggleAuto()
        {
            // AUTO and speed predate account progression. Keep both available so adding the
            // new profile system does not remove an existing player-facing feature.
            GameSettings.AutoBattle = !GameSettings.AutoBattle;
            if (GameSettings.AutoBattle && targetSelectionActive && !targetIsUltimate)
                ClearTargetingState();
            RefreshHeaderControls();
            RefreshAll();
        }

        void CycleAutoPreset()
        {
            AutoBattlePreset preset = AutoBattleSettings.CyclePreset();
            RefreshHeaderControls();
            if (logLabel != null)
                logLabel.text = "AUTO 전략  ·  " + AutoBattleSettings.GetDisplayName(preset);
        }

        void ToggleSpeed()
        {
            presentationSpeed = presentationSpeed < 1.5f ? 2f : 1f;
            RefreshHeaderControls();
        }

        void RefreshHeaderControls()
        {
            if (autoLabel != null)
            {
                autoLabel.text = GameSettings.AutoBattle ? "AUTO  ON" : "AUTO  OFF";
                autoLabel.color = GameSettings.AutoBattle ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted;
            }
            if (autoPresetLabel != null)
            {
                autoPresetLabel.text = "전략  ·  "
                    + AutoBattleSettings.GetDisplayName(AutoBattleSettings.CurrentPreset);
                autoPresetLabel.color = GameSettings.AutoBattle
                    ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Silver;
            }
            if (speedLabel != null)
            {
                speedLabel.text = presentationSpeed > 1.5f ? "×2" : "×1";
                speedLabel.color = presentationSpeed > 1.5f ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Silver;
            }
        }

        void OpenPause()
        {
            if (battleFinished || changingScene || pauseLayer == null) return;
            paused = true;
            pauseLayer.SetActive(true);
            pauseLayer.transform.SetAsLastSibling();
            RefreshAll();
        }

        void ResumeBattle()
        {
            paused = false;
            if (pauseLayer != null) pauseLayer.SetActive(false);
            RefreshAll();
        }

        void ShowResult(BattleResult battleResult)
        {
            if (battleFinished) return;
            battleFinished = true;
            victoryProgressCommitted = false;
            paused = false;
            if (pauseLayer != null) pauseLayer.SetActive(false);
            resultLayer.SetActive(true);
            resultLayer.transform.SetAsLastSibling();
            if (BattleSession.ModeContext != null)
            {
                ShowModeResult(BattleSession.ModeContext, battleResult);
                return;
            }

            bool rewardEligible = BattleSession.RewardEligible;
            if (rewardEligible) MissionService.RecordBattleCompleted();
            if (battleResult != null && battleResult.IsSuccessful)
            {
                int defeatedAllies = 0;
                foreach (CombatUnit player in battle.Players)
                    if (!player.IsAlive) defeatedAllies++;

                if (!rewardEligible)
                {
                    resultTitle.text = "연 습 전 완 료";
                    resultBody.text = stage.DisplayName
                        + " 전투를 완료했습니다.\n\n씬 직접 실행은 연습 전투로 처리되어 "
                        + "행동력·보상·별·해금·임무 진행이 적용되지 않습니다.";
                    nextButton.interactable = true;
                    nextButtonLabel.text = "보상 전투 도전";
                    return;
                }

                string runId = string.IsNullOrWhiteSpace(BattleSession.RunId)
                    ? Guid.NewGuid().ToString("N") : BattleSession.RunId;
                bool firstClearCandidate = !StageProgression.IsCleared(stage);
                RewardPackage rewardPackage = firstClearCandidate
                    ? stage.FirstClearRewardPackage : stage.RepeatClearRewardPackage;
                bool usesRewardPackage = rewardPackage != null && !rewardPackage.IsEmpty;
                StageCompletionResult completion = null;
                Action rollbackProgression = StageProgression.CaptureRollback(stage);
                string transactionId = "battle:" + stage.Id + ":" + runId;
                int equipmentDrops = 0;
                var equipmentInventory = EquipmentInventoryService.Default;
                var equipmentDropService = new EquipmentDropService(equipmentInventory);
                EquipmentInventoryStorageSnapshot equipmentSnapshot;
                IReadOnlyList<EquipmentDefinition> rolledEquipment;
                try
                {
                    equipmentSnapshot = equipmentInventory.CaptureStorageSnapshot();
                    rolledEquipment = equipmentDropService.Roll(stage.EquipmentDropTable,
                        StableSeed(transactionId));
                }
                catch (Exception exception)
                {
                    StaminaService.Default.Charge(stage.StaminaCost, true);
                    resultTitle.text = "보상 처리 실패";
                    resultBody.text = "보상 준비에 실패해 행동력을 반환했습니다. 다시 도전해 주세요.";
                    nextButton.interactable = true;
                    nextButtonLabel.text = "다시 도전";
                    Debug.LogError("[Starfall] Equipment drop preparation failed for "
                        + transactionId + ": " + exception);
                    return;
                }
                Action commitParticipants = () =>
                {
                    completion = StageProgression.Complete(stage,
                        BattleSession.SelectedStageIndex, defeatedAllies,
                        battle.Core.RegularTurnsCompleted, false);
                    equipmentDrops = equipmentDropService.StageGrant(transactionId,
                        rolledEquipment).Count;
                };
                Action rollbackParticipants = () =>
                {
                    try { rollbackProgression?.Invoke(); }
                    finally { equipmentInventory.RestoreStorageSnapshot(equipmentSnapshot); }
                };
                RewardGrantResult reward = usesRewardPackage
                    ? RewardPackageService.Default.Grant(transactionId, rewardPackage,
                        commitParticipants, rollbackParticipants)
                    : RewardService.Default.GrantReward(transactionId,
                        new RewardBundle(stage.RewardCredits, stage.RewardSkillMaterials,
                            stage.AccountExperienceReward,
                            firstClearCandidate ? stage.FirstClearPremiumCurrency : 0),
                        commitParticipants, rollbackParticipants);
                victoryProgressCommitted = reward.Succeeded || reward.AlreadyProcessed;
                if (reward.AlreadyProcessed && completion == null)
                    completion = StageProgression.Complete(stage,
                        BattleSession.SelectedStageIndex, defeatedAllies,
                        battle.Core.RegularTurnsCompleted);
                if (!victoryProgressCommitted || completion == null)
                {
                    StaminaService.Default.Charge(stage.StaminaCost, true);
                    resultTitle.text = "보 상 처 리 실 패";
                    resultBody.text = "진행도와 보상을 저장하지 못해 행동력을 반환했습니다. 다시 도전하세요.";
                    nextButton.interactable = true;
                    nextButtonLabel.text = "다시 도전";
                    return;
                }

                int nextIndex = FindNextStageIndex(BattleSession.SelectedStageIndex);
                bool hasNext = nextIndex >= 0;
                string rewardSummary = usesRewardPackage
                    ? rewardPackage.Summary
                    : stage.RewardCredits.ToString("N0") + " 크레딧     ◇  "
                        + stage.RewardSkillMaterials + " " + PlayerWallet.SkillMaterialDisplayName
                        + "     EXP " + stage.AccountExperienceReward
                        + (completion.FirstClear
                            ? "     ♦ " + stage.FirstClearPremiumCurrency : string.Empty);
                resultTitle.text = "작 전 완 료";
                resultBody.text = stage.DisplayName + " 클리어   " + StarLabel(completion.EarnedStars)
                    + "\nACTION " + completion.RegularTurns.ToString("N0") + "  ·  전투 불능 "
                    + completion.DefeatedAllies + "명\n\n●  " + rewardSummary
                    + (equipmentDrops > 0 ? "\n장비 드롭  " + equipmentDrops + "개" : string.Empty)
                    + (completion.FirstClear ? hasNext
                        ? "\n\nFIRST CLEAR  ·  다음 작전이 해금되었습니다"
                        : stage.Category == StageCategory.Main
                            ? "\n\nFIRST CLEAR  ·  마지막 작전을 완료했습니다"
                            : "\n\nFIRST CLEAR  ·  던전 소탕 조건을 확인하세요" : string.Empty);
                nextButton.interactable = hasNext;
                nextButtonLabel.text = hasNext ? "다음 작전"
                    : stage.Category == StageCategory.Main ? "마지막 작전" : "던전 완료";
            }
            else
            {
                resultTitle.text = "작 전 실 패";
                resultBody.text = "편성과 캐릭터 성장을 확인한 뒤 다시 도전하세요.";
                nextButton.interactable = true;
                nextButtonLabel.text = "다시 도전";
            }
        }

        void NextStage()
        {
            if (!battleFinished || changingScene) return;
            if (BattleSession.ModeContext != null)
            {
                if (!BattleSession.ModeContext.TryCreateRetry(out IBattleModeRunContext retry,
                    out string failureReason))
                {
                    nextButton.interactable = false;
                    nextButtonLabel.text = "재도전 불가";
                    if (!string.IsNullOrWhiteSpace(failureReason))
                        resultBody.text += "\n\n" + failureReason;
                    return;
                }
                BattleSession.BeginSpecialRun(retry);
                changingScene = true;
                StarfallSceneFlow.Load(SceneNames.TurnBattle);
                return;
            }
            StageData targetStage = stage;
            int targetIndex = BattleSession.SelectedStageIndex;
            if (battle.Outcome == BattleOutcome.Victory && victoryProgressCommitted)
            {
                int nextIndex = FindNextStageIndex(BattleSession.SelectedStageIndex);
                if (nextIndex < 0) return;
                targetIndex = nextIndex;
                targetStage = stageDatabase.Stages[nextIndex];
            }
            if (targetStage == null || !StaminaService.Default.TrySpend(targetStage.StaminaCost))
            {
                nextButton.interactable = false;
                nextButtonLabel.text = "행동력 부족";
                resultBody.text += "\n\n행동력이 부족합니다. 스테이지 선택으로 돌아가세요.";
                return;
            }
            MissionService.RecordStaminaSpent(targetStage.StaminaCost);
            BattleSession.BeginRun(targetStage, targetIndex, true);
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.TurnBattle);
        }

        static string StarLabel(int stars) => new string('★', Mathf.Clamp(stars, 0, 3))
            + new string('☆', 3 - Mathf.Clamp(stars, 0, 3));

        int FindNextStageIndex(int currentIndex)
        {
            if (stageDatabase == null) return -1;
            StageData current = currentIndex >= 0 && currentIndex < stageDatabase.Stages.Count
                ? stageDatabase.Stages[currentIndex] : null;
            if (current == null) return -1;
            for (int i = currentIndex + 1; i < stageDatabase.Stages.Count; i++)
            {
                StageData candidate = stageDatabase.Stages[i];
                if (candidate != null && candidate.Category == current.Category
                    && StageProgression.IsUnlocked(candidate, i, stageDatabase.Stages)) return i;
            }
            return -1;
        }

        void ReturnToStages()
        {
            if (changingScene) return;
            string returnScene = BattleSession.ReturnScene;
            changingScene = true;
            paused = false;
            BattleSession.Clear();
            StarfallSceneFlow.Load(returnScene);
        }

        void ShowModeResult(IBattleModeRunContext context, BattleResult battleResult)
        {
            BattleModeCompletion completion;
            try
            {
                completion = context.Complete(battleResult) ?? new BattleModeCompletion
                {
                    Succeeded = false,
                    Title = "결 과 처 리 실 패",
                    Body = "콘텐츠 결과를 처리하지 못했습니다.",
                    NextLabel = "돌아가기",
                    CanRetry = false
                };
            }
            catch (Exception exception)
            {
                completion = new BattleModeCompletion
                {
                    Succeeded = false,
                    Title = "결 과 처 리 실 패",
                    Body = exception.Message,
                    NextLabel = "돌아가기",
                    CanRetry = false
                };
            }
            victoryProgressCommitted = completion.Succeeded;
            resultTitle.text = string.IsNullOrWhiteSpace(completion.Title)
                ? "전 투 결 과" : completion.Title;
            resultBody.text = completion.Body ?? string.Empty;
            nextButton.interactable = completion.CanRetry;
            nextButtonLabel.text = string.IsNullOrWhiteSpace(completion.NextLabel)
                ? "재도전" : completion.NextLabel;
        }

        string BattleHeaderText()
        {
            if (stage == null) return "BATTLE";
            BattleRuleSet activeRules = BattleSession.Rules;
            string prefix = activeRules.Mode == BattleMode.WeeklyBoss ? "WEEKLY BOSS"
                : activeRules.Mode == BattleMode.ChallengeTower ? "CHALLENGE TOWER"
                : stage.Chapter;
            string limit = activeRules.HasTurnLimit ? "  ·  LIMIT " + activeRules.TurnLimit : string.Empty;
            return prefix + "  /  " + stage.DisplayName + limit;
        }

        static int StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < (value?.Length ?? 0); i++)
                    hash = (hash ^ value[i]) * 16777619;
                return (int)hash;
            }
        }

        static Image CreateFill(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void SetFill(Image image, float ratio)
        {
            if (image == null) return;
            image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }

        static void ApplyPortrait(Image portrait, Text mark, CombatUnit unit)
        {
            Sprite sprite = unit?.CharacterData?.Portrait;
            if (sprite != null)
            {
                portrait.sprite = sprite;
                portrait.color = Color.white;
                portrait.preserveAspect = true;
                mark.text = string.Empty;
            }
            else
            {
                portrait.sprite = null;
                portrait.color = new Color(.07f, .07f, .095f, 1f);
                mark.text = unit != null && unit.Team == BattleTeam.Enemy
                    ? "✦" : FirstLetter(unit?.DisplayName);
            }
        }

        static string BuildWeaknessText(CombatUnit unit)
        {
            var labels = new List<string>();
            foreach (BattleElement element in unit.Weaknesses) labels.Add(ElementLabel(element));
            return labels.Count > 0 ? string.Join("  ", labels) : "없음";
        }

        static string BuildStatusText(CombatUnit unit)
        {
            if (!unit.IsAlive) return "전투 불능";
            var labels = new List<string>();
            int visible = Mathf.Min(4, unit.Statuses.Count);
            for (int i = 0; i < visible; i++)
            {
                StatusEffectInstance status = unit.Statuses[i];
                labels.Add(StatusLabel(status.Type) + " " + status.RemainingOwnerActions);
            }
            if (unit.Statuses.Count > visible) labels.Add("+" + (unit.Statuses.Count - visible));
            return labels.Count > 0 ? string.Join("   ", labels) : "상태 효과 없음";
        }

        static string BuildStatusTooltip(CombatUnit unit)
        {
            if (unit == null || unit.Statuses.Count == 0)
                return "현재 적용된 상태 효과가 없습니다.";

            var rows = new List<string>(unit.Statuses.Count);
            for (int i = 0; i < unit.Statuses.Count; i++)
            {
                StatusEffectInstance status = unit.Statuses[i];
                if (status == null) continue;
                string duration = status.RemainingOwnerActions < 0
                    ? "지속" : status.RemainingOwnerActions + "회 남음";
                string stacks = status.Stacks > 1 ? " · " + status.Stacks + "중첩" : string.Empty;
                rows.Add("<b>" + StatusLabel(status.Type) + "</b>  "
                    + StatusDescription(status) + "  ·  " + duration + stacks);
            }
            return rows.Count == 0 ? "현재 적용된 상태 효과가 없습니다."
                : string.Join("\n", rows);
        }

        static string StatusDescription(StatusEffectInstance status)
        {
            float percent = Mathf.Abs(status.TotalMagnitude) * 100f;
            float damage = Mathf.Max(Mathf.Abs(status.TotalFlatValue),
                Mathf.Abs(status.TotalMagnitude));
            switch (status.Type)
            {
                case StatusEffectType.AttackUp: return "공격력 +" + percent.ToString("0.#") + "%";
                case StatusEffectType.DamageUp: return "주는 피해 +" + percent.ToString("0.#") + "%";
                case StatusEffectType.DefenseDown: return "방어력 -" + percent.ToString("0.#") + "%";
                case StatusEffectType.SpeedDown: return "속도 -" + percent.ToString("0.#") + "%";
                case StatusEffectType.AttackDown: return "공격력 -" + percent.ToString("0.#") + "%";
                case StatusEffectType.Burn: return "행동 시작 시 화상 피해 " + damage.ToString("0.#");
                case StatusEffectType.Shock: return "행동 시작 시 감전 피해 " + damage.ToString("0.#");
                case StatusEffectType.Bleed: return "행동 시작 시 출혈 피해 " + damage.ToString("0.#");
                case StatusEffectType.Shield: return "보호막 " + Mathf.Max(0f, status.RuntimeValue).ToString("0.#");
                default: return status.EffectId;
            }
        }

        static string StatusLabel(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.AttackUp: return "공↑";
                case StatusEffectType.DamageUp: return "피해↑";
                case StatusEffectType.DefenseDown: return "방↓";
                case StatusEffectType.SpeedDown: return "속↓";
                case StatusEffectType.AttackDown: return "공↓";
                case StatusEffectType.Burn: return "화상";
                case StatusEffectType.Shock: return "감전";
                case StatusEffectType.Bleed: return "출혈";
                case StatusEffectType.Shield: return "보호";
                default: return type.ToString();
            }
        }

        static string ElementLabel(BattleElement element)
        {
            switch (element)
            {
                case BattleElement.Fire: return "화염";
                case BattleElement.Ice: return "냉기";
                case BattleElement.Lightning: return "전격";
                case BattleElement.Wind: return "바람";
                case BattleElement.Light: return "광휘";
                case BattleElement.Dark: return "암흑";
                default: return "무속성";
            }
        }

        static Color ElementColor(BattleElement element)
        {
            switch (element)
            {
                case BattleElement.Fire: return new Color(.95f, .38f, .25f, 1f);
                case BattleElement.Ice: return new Color(.45f, .82f, 1f, 1f);
                case BattleElement.Lightning: return new Color(.73f, .55f, 1f, 1f);
                case BattleElement.Wind: return new Color(.39f, .88f, .7f, 1f);
                case BattleElement.Light: return new Color(1f, .86f, .48f, 1f);
                case BattleElement.Dark: return new Color(.74f, .45f, .84f, 1f);
                default: return UrbanFantasyStyle.Muted;
            }
        }

        static string ResourceDeltaLabel(int cost)
        {
            if (cost < 0) return "+" + (-cost);
            if (cost > 0) return "-" + cost;
            return "±0";
        }

        static string LocalizeFailure(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) return "행동을 실행할 수 없습니다";
            string lower = reason.ToLowerInvariant();
            if (lower.Contains("skill point")) return "스킬 포인트가 부족합니다";
            if (lower.Contains("energy")) return "필살기 에너지가 부족합니다";
            if (lower.Contains("target")) return "선택 가능한 대상이 없습니다";
            if (lower.Contains("queue") || lower.Contains("already")) return "이미 필살기 대기열에 있습니다";
            if (lower.Contains("actor") || lower.Contains("turn")) return "현재 행동할 수 없는 유닛입니다";
            return reason;
        }

        static string ShortName(string value, int length)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            return value.Length <= length ? value : value.Substring(0, length);
        }

        static string FirstLetter(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Substring(0, 1);
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
    }
}
