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
            public BattleUnit Unit;
            public RectTransform Root;
            public Image HpFill;
            public Text HpText;
            public Text StateText;
            public Image ActiveFrame;
        }

        readonly Dictionary<BattleUnit, UnitView> views = new Dictionary<BattleUnit, UnitView>();
        LobbyUiFactory ui;
        StageData stage;
        StageDatabase stageDatabase;
        TurnBattleModel battle;
        Text roundLabel;
        Text turnLabel;
        Text logLabel;
        Text autoLabel;
        Text skillButtonLabel;
        Button attackButton;
        Button skillButton;
        Button defendButton;
        GameObject resultLayer;
        Text resultTitle;
        Text resultBody;
        Button nextButton;
        Text nextButtonLabel;
        int pendingAction = -1;
        bool battleFinished;
        bool changingScene;

        void Awake()
        {
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (!changingScene && Input.GetKeyDown(KeyCode.Escape)) ReturnToStages();
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
            CharacterDatabase database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            FormationState formation = new FormationState();
            formation.Load(database);
            BuildBackground(root);
            RectTransform safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();
            BuildHeader(safeRoot);

            if (stage == null || formation.Count == 0)
            {
                BuildInvalidState(safeRoot, stage == null ? "스테이지 데이터가 없습니다" : "편성된 캐릭터가 없습니다");
                return;
            }

            battle = new TurnBattleModel(formation, stage);
            BuildBattlefield(safeRoot);
            BuildActionPanel(safeRoot);
            BuildResultLayer(safeRoot);
            RefreshAllViews();
            StartCoroutine(BattleLoop());
        }

        StageData ResolveStage()
        {
            if (BattleSession.SelectedStage != null) return BattleSession.SelectedStage;
            if (stageDatabase == null || stageDatabase.Stages.Count == 0) return null;
            int index = Mathf.Clamp(StageProgression.HighestUnlocked, 0, stageDatabase.Stages.Count - 1);
            BattleSession.SelectedStageIndex = index;
            BattleSession.SelectedStage = stageDatabase.Stages[index];
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
            }
            else background.color = LobbyTheme.Hex("08080C");
            ui.CreateImage("Battle Grade", root, new Color(.015f, .01f, .025f, .42f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Battle Dim", root, new Color(.002f, .002f, .005f, .6f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform root)
        {
            RectTransform header = ui.CreateImage("Battle Header", root, new Color(.005f, .005f, .008f, .84f),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 92)).rectTransform;
            ui.CreateImage("Header Line", header, UrbanFantasyStyle.Line,
                Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            GameObject back = ui.CreateButton("Leave Battle", header, new Vector2(0, .5f),
                new Vector2(50, 0), new Vector2(52, 52), "‹", 30, UrbanFantasyStyle.PanelStrong, ReturnToStages);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());
            ui.CreateText("Battle Stage", stage != null ? stage.Chapter + "  /  " + stage.DisplayName : "BATTLE",
                header, 22, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(310, 0),
                new Vector2(430, 42), TextAnchor.MiddleLeft);
            roundLabel = ui.CreateText("Round", "ROUND 0", header, 15, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(180, 30), TextAnchor.MiddleCenter);
            GameObject auto = ui.CreateButton("Auto Battle", header, new Vector2(1, .5f),
                new Vector2(-105, 0), new Vector2(160, 48), string.Empty, 14,
                UrbanFantasyStyle.PanelStrong, ToggleAuto);
            UrbanFantasyStyle.AddBorder(ui, auto.GetComponent<RectTransform>());
            autoLabel = auto.GetComponentInChildren<Text>();
            RefreshAutoLabel();
        }

        void BuildBattlefield(RectTransform root)
        {
            turnLabel = ui.CreateText("Turn Banner", "전투 준비", root, 25, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 70), new Vector2(700, 52), TextAnchor.MiddleCenter);
            logLabel = ui.CreateText("Battle Log", string.Empty, root, 15, FontStyle.Normal,
                new Color(.88f, .88f, .91f, .75f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 18), new Vector2(760, 42), TextAnchor.MiddleCenter);

            for (int i = 0; i < battle.Players.Count; i++)
            {
                BattleUnit unit = battle.Players[i];
                Vector2 position = new Vector2(155 + i * 250, 180);
                views[unit] = CreateUnitView(root, unit, new Vector2(0, 0), position, new Vector2(226, 158));
            }
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                BattleUnit unit = battle.Enemies[i];
                Vector2 position = new Vector2(-120 - (battle.Enemies.Count - 1 - i) * 250, -220);
                views[unit] = CreateUnitView(root, unit, new Vector2(1, 1), position, new Vector2(226, 158));
            }
        }

        UnitView CreateUnitView(RectTransform root, BattleUnit unit, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform card = ui.CreateImage((unit.IsEnemy ? "Enemy " : "Player ") + unit.Name, root,
                UrbanFantasyStyle.PanelStrong, anchor, anchor, position, size).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, unit.IsEnemy
                ? new Color(.65f, .14f, .14f, .55f) : UrbanFantasyStyle.StrongLine);
            Image active = ui.CreateImage("Active Frame", card, Color.clear, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-7, -7));
            active.transform.SetAsFirstSibling();
            Image portrait = ui.CreateImage("Portrait", card, new Color(.08f, .08f, .10f, 1),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(52, 13), new Vector2(84, 112));
            if (!unit.IsEnemy && unit.Character.Portrait != null)
            {
                portrait.sprite = unit.Character.Portrait;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.color = Color.white;
            }
            else
            {
                ui.CreateText("Enemy Mark", unit.IsEnemy ? "✦" : unit.Name.Substring(0, 1), portrait.transform,
                    34, FontStyle.Normal, unit.IsEnemy ? new Color(.8f, .18f, .16f, .8f) : UrbanFantasyStyle.Silver,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            }
            ui.CreateText("Name", unit.Name, card, 16, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-68, -27),
                new Vector2(126, 28), TextAnchor.MiddleLeft);
            Image hpTrack = ui.CreateImage("HP Track", card, new Color(1, 1, 1, .12f),
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-68, 12), new Vector2(126, 9));
            Image hpFill = ui.CreateImage("HP Fill", hpTrack.transform,
                unit.IsEnemy ? new Color(.65f, .10f, .09f, 1) : new Color(.74f, .74f, .78f, 1),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text hpText = ui.CreateText("HP", string.Empty, card, 10, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-68, -10),
                new Vector2(126, 20), TextAnchor.MiddleRight);
            Text state = ui.CreateText("State", string.Empty, card, 10, FontStyle.Normal, UrbanFantasyStyle.Gold,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-68, 20),
                new Vector2(126, 20), TextAnchor.MiddleRight);
            return new UnitView { Unit = unit, Root = card, HpFill = hpFill, HpText = hpText,
                StateText = state, ActiveFrame = active };
        }

        void BuildActionPanel(RectTransform root)
        {
            RectTransform panel = ui.CreateImage("Action Panel", root, new Color(.008f, .008f, .012f, .9f),
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-315, 145),
                new Vector2(580, 210)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel, UrbanFantasyStyle.StrongLine);
            ui.CreateText("Action Header", "행동 선택", panel, 18, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(95, -30),
                new Vector2(150, 28), TextAnchor.MiddleLeft);
            attackButton = CreateActionButton(panel, "Attack", "공격", 100, 0);
            skillButton = CreateActionButton(panel, "Skill", "스킬", 290, 1);
            defendButton = CreateActionButton(panel, "Defend", "방어", 480, 2);
            skillButtonLabel = skillButton.GetComponentInChildren<Text>();
            SetActionsEnabled(false);
        }

        Button CreateActionButton(RectTransform panel, string name, string label, float x, int action)
        {
            GameObject button = ui.CreateButton(name, panel, new Vector2(0, 0), new Vector2(x, 66),
                new Vector2(170, 92), label, 18, UrbanFantasyStyle.PanelSoft,
                () => pendingAction = action);
            UrbanFantasyStyle.AddBorder(ui, button.GetComponent<RectTransform>());
            return button.GetComponent<Button>();
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
            RectTransform card = ui.CreateImage("Result Card", resultLayer.transform, UrbanFantasyStyle.PanelStrong,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(720, 500), true).rectTransform;
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
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 300)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card);
            ui.CreateText("Message", message, card, 24, FontStyle.Normal, UrbanFantasyStyle.Silver,
                Vector2.zero, Vector2.one, new Vector2(0, 35), new Vector2(-70, -90), TextAnchor.MiddleCenter);
            ui.CreateButton("Return", card, new Vector2(.5f, 0), new Vector2(0, 52),
                new Vector2(220, 58), "돌아가기", 17, UrbanFantasyStyle.PanelSoft, ReturnToStages);
        }

        IEnumerator BattleLoop()
        {
            yield return new WaitForSecondsRealtime(.7f);
            while (!battle.PlayersDefeated && !battle.EnemiesDefeated)
            {
                List<BattleUnit> order = battle.BeginRound();
                roundLabel.text = "ROUND " + battle.Round;
                logLabel.text = "새 라운드가 시작됩니다";
                yield return new WaitForSecondsRealtime(.45f);
                foreach (BattleUnit actor in order)
                {
                    if (!actor.IsAlive || battle.PlayersDefeated || battle.EnemiesDefeated) continue;
                    SetActiveUnit(actor);
                    turnLabel.text = actor.Name + "의 턴";
                    if (actor.IsEnemy)
                        yield return EnemyTurn(actor);
                    else
                        yield return PlayerTurn(actor);
                    RefreshAllViews();
                    yield return new WaitForSecondsRealtime(GameSettings.AutoBattle ? .25f : .48f);
                }
            }
            SetActiveUnit(null);
            SetActionsEnabled(false);
            ShowResult(battle.EnemiesDefeated);
        }

        IEnumerator PlayerTurn(BattleUnit actor)
        {
            BattleUnit target = battle.FirstAliveEnemy();
            if (target == null) yield break;
            pendingAction = -1;
            skillButtonLabel.text = actor.Character.SkillName;
            if (GameSettings.AutoBattle)
            {
                bool skillTurn = (battle.Round + actor.SlotIndex) % 3 == 0;
                bool usefulSkill = actor.Character.Role != CharacterRole.Healer || battle.HasInjuredPlayer();
                bool emergencyGuard = actor.HpRatio < .3f && battle.Round % 3 == 0;
                pendingAction = emergencyGuard ? 2 : skillTurn && usefulSkill ? 1 : 0;
            }
            else
            {
                SetActionsEnabled(true);
                while (pendingAction < 0) yield return null;
                SetActionsEnabled(false);
            }

            if (pendingAction == 2)
            {
                actor.Defend();
                logLabel.text = actor.Name + "이(가) 방어 태세를 취했습니다";
            }
            else if (pendingAction == 1)
            {
                int amount = battle.UsePlayerSkill(actor, target, out BattleUnit affected, out bool isHealing);
                logLabel.text = isHealing
                    ? actor.Name + "의 " + actor.Character.SkillName + "  ·  " + affected.Name + " HP " + amount + " 회복"
                    : actor.Name + "의 " + actor.Character.SkillName + "  ·  " + amount + " 피해";
            }
            else
            {
                int damage = battle.AttackUnit(actor, target, 1f);
                logLabel.text = actor.Name + "의 공격  ·  " + damage + " 피해";
            }
            pendingAction = -1;
        }

        IEnumerator EnemyTurn(BattleUnit actor)
        {
            yield return new WaitForSecondsRealtime(GameSettings.AutoBattle ? .15f : .38f);
            BattleUnit target = battle.RandomAlivePlayer();
            if (target == null) yield break;
            float multiplier = battle.Round % 4 == 0 ? 1.35f : 1f;
            int damage = battle.AttackUnit(actor, target, multiplier);
            logLabel.text = actor.Name + " → " + target.Name + "  ·  " + damage + " 피해";
        }

        void RefreshAllViews()
        {
            foreach (KeyValuePair<BattleUnit, UnitView> pair in views)
            {
                BattleUnit unit = pair.Key;
                UnitView view = pair.Value;
                view.HpFill.rectTransform.anchorMax = new Vector2(unit.HpRatio, 1);
                view.HpText.text = unit.CurrentHp.ToString("N0") + " / " + unit.MaxHp.ToString("N0");
                view.StateText.text = !unit.IsAlive ? "DEFEATED" : unit.Defending ? "GUARD" : string.Empty;
                CanvasGroup group = view.Root.GetComponent<CanvasGroup>();
                if (group == null) group = view.Root.gameObject.AddComponent<CanvasGroup>();
                group.alpha = unit.IsAlive ? 1f : .34f;
            }
        }

        void SetActiveUnit(BattleUnit active)
        {
            foreach (KeyValuePair<BattleUnit, UnitView> pair in views)
                pair.Value.ActiveFrame.color = pair.Key == active
                    ? new Color(.92f, .82f, .55f, .18f) : Color.clear;
        }

        void SetActionsEnabled(bool enabled)
        {
            attackButton.interactable = enabled;
            skillButton.interactable = enabled;
            defendButton.interactable = enabled;
        }

        void ToggleAuto()
        {
            GameSettings.AutoBattle = !GameSettings.AutoBattle;
            RefreshAutoLabel();
            if (GameSettings.AutoBattle && pendingAction < 0) pendingAction = 0;
        }

        void RefreshAutoLabel()
        {
            autoLabel.text = GameSettings.AutoBattle ? "AUTO  ON" : "AUTO  OFF";
            autoLabel.color = GameSettings.AutoBattle ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted;
        }

        void ShowResult(bool victory)
        {
            battleFinished = true;
            resultLayer.SetActive(true);
            resultLayer.transform.SetAsLastSibling();
            if (victory)
            {
                bool firstClear = StageProgression.Complete(stage, BattleSession.SelectedStageIndex);
                PlayerWallet.AddCredits(stage.RewardCredits);
                PlayerWallet.AddSkillMaterials(stage.RewardSkillMaterials);
                int nextIndex = FindNextStageIndex(BattleSession.SelectedStageIndex);
                bool hasNext = nextIndex >= 0;
                resultTitle.text = "작 전 완 료";
                resultBody.text = stage.DisplayName + " 클리어\n\n●  " + stage.RewardCredits.ToString("N0") +
                    " 크레딧     ◇  " + stage.RewardSkillMaterials + " " +
                    PlayerWallet.SkillMaterialDisplayName +
                    (firstClear ? hasNext
                        ? "\n\nFIRST CLEAR  ·  다음 작전이 해금되었습니다"
                        : "\n\nFIRST CLEAR  ·  마지막 작전을 완료했습니다" : string.Empty);
                nextButton.interactable = hasNext;
                nextButtonLabel.text = hasNext ? "다음 작전" : "마지막 작전";
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
            if (!battleFinished) return;
            if (battle.EnemiesDefeated)
            {
                int nextIndex = FindNextStageIndex(BattleSession.SelectedStageIndex);
                if (nextIndex < 0) return;
                BattleSession.SelectedStageIndex = nextIndex;
                BattleSession.SelectedStage = stageDatabase.Stages[nextIndex];
            }
            changingScene = true;
            SceneManager.LoadScene(SceneNames.TurnBattle);
        }

        int FindNextStageIndex(int currentIndex)
        {
            if (stageDatabase == null) return -1;
            for (int i = currentIndex + 1; i < stageDatabase.Stages.Count; i++)
                if (stageDatabase.Stages[i] != null) return i;
            return -1;
        }

        void ReturnToStages()
        {
            if (changingScene) return;
            changingScene = true;
            SceneManager.LoadScene(SceneNames.StageSelect);
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
