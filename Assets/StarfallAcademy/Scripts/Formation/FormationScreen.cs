using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class FormationScreen : MonoBehaviour
    {
        enum RosterSort
        {
            CombatPower,
            Level,
            Rarity,
            Name
        }

        static readonly CharacterRole?[] RoleFilters =
        {
            null, CharacterRole.Striker, CharacterRole.Support, CharacterRole.Tank,
            CharacterRole.Healer, CharacterRole.Special
        };

        readonly FormationState state = new FormationState();
        readonly Dictionary<CharacterData, Image> selectionMarks = new Dictionary<CharacterData, Image>();

        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        CharacterDatabase database;
        RectTransform rosterContent;
        RectTransform slotsRoot;
        Text emptyLabel;
        Text memberCountLabel;
        Text totalPowerLabel;
        Text teamSummaryLabel;
        Text presetIndexLabel;
        Text roleFilterLabel;
        Text sortLabel;
        InputField presetNameInput;
        int roleFilterIndex;
        RosterSort rosterSort;
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
            RectTransform workspace = ui.CreateImage("Formation Workspace", safeRoot,
                new Color(.008f, .008f, .012f, .86f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -8), new Vector2(1740, 960), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            BuildHeader(workspace);
            BuildRosterArea(workspace);
            BuildSlotArea(workspace);

            toast = CreateController<LobbyToastOverlay>("Formation Toast", safeRoot);
            toast.Initialize(safeRoot, ui);
            database = Resources.Load<CharacterDatabase>("Data/CharacterDatabase");
            state.Load(database);
            UpdatePresetControls();
            RebuildRoster();
            RebuildSlots();
        }

        void BuildBackground(RectTransform root)
        {
            Image background = ui.CreateImage("Formation Background", root, ui.Theme.White,
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
                background.color = LobbyTheme.Hex("0A0A0E");
            ui.CreateImage("Formation Monochrome Grade", root, new Color(.04f, .035f, .05f, .34f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Formation Background Dim", root, new Color(.002f, .002f, .005f, .72f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform workspace)
        {
            ui.CreateImage("Header Accent", workspace, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-64, 2));
            GameObject back = ui.CreateButton("Back To Lobby", workspace, new Vector2(0, 1), new Vector2(52, -52),
                new Vector2(54, 54), "‹", 31, UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());
            ui.CreateText("Formation Eyebrow", "T E A M   M A N A G E M E N T", workspace, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(221, -33), new Vector2(250, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Formation Title", "캐릭터 편성", workspace, 32, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(249, -70), new Vector2(300, 44), TextAnchor.MiddleLeft);
            ui.CreateText("Formation Help", "캐릭터를 눌러 최대 4명까지 편성하세요.", workspace, 15,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(573, -71), new Vector2(340, 32), TextAnchor.MiddleLeft);
            ui.CreateText("Scene Label", "F O R M A T I O N", workspace, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-104, -36), new Vector2(150, 24), TextAnchor.MiddleRight);
            BuildPresetControls(workspace);
            GameObject clear = ui.CreateButton("Clear Formation", workspace, new Vector2(1, 0), new Vector2(-390, 46),
                new Vector2(190, 58), "편성 초기화", 17, UrbanFantasyStyle.PanelStrong, ClearFormation);
            GameObject save = ui.CreateButton("Save Formation", workspace, new Vector2(1, 0), new Vector2(-170, 46),
                new Vector2(220, 58), "저장하고 로비로", 18, new Color(.17f, .17f, .20f, .98f), SaveAndReturn);
            UrbanFantasyStyle.AddBorder(ui, clear.GetComponent<RectTransform>());
            UrbanFantasyStyle.AddBorder(ui, save.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
        }

        void BuildPresetControls(RectTransform workspace)
        {
            GameObject previous = ui.CreateButton("Previous Preset", workspace, new Vector2(1, 1),
                new Vector2(-570, -46), new Vector2(42, 38), "‹", 22,
                UrbanFantasyStyle.PanelStrong, () => SwitchPreset(-1));
            GameObject next = ui.CreateButton("Next Preset", workspace, new Vector2(1, 1),
                new Vector2(-210, -46), new Vector2(42, 38), "›", 22,
                UrbanFantasyStyle.PanelStrong, () => SwitchPreset(1));
            UrbanFantasyStyle.AddBorder(ui, previous.GetComponent<RectTransform>());
            UrbanFantasyStyle.AddBorder(ui, next.GetComponent<RectTransform>());
            presetIndexLabel = ui.CreateText("Preset Index", "PRESET 1", workspace, 10,
                FontStyle.Bold, UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-390, -21), new Vector2(170, 20), TextAnchor.MiddleCenter);

            Image field = ui.CreateImage("Preset Name Field", workspace, UrbanFantasyStyle.PanelStrong,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-390, -53), new Vector2(300, 38), true);
            UrbanFantasyStyle.AddBorder(ui, field.rectTransform);
            Text value = ui.CreateText("Value", string.Empty, field.transform, 14, FontStyle.Normal,
                UrbanFantasyStyle.Silver, Vector2.zero, Vector2.one, new Vector2(10, 0),
                new Vector2(-20, 0), TextAnchor.MiddleLeft);
            presetNameInput = field.gameObject.AddComponent<InputField>();
            presetNameInput.targetGraphic = field;
            presetNameInput.textComponent = value;
            presetNameInput.lineType = InputField.LineType.SingleLine;
            presetNameInput.characterLimit = 24;
            presetNameInput.onEndEdit.AddListener(RenameActivePreset);
        }

        void SwitchPreset(int direction)
        {
            if (database == null) return;
            FormationPresetService service = FormationPresetService.Default;
            if (!state.Save())
            {
                toast?.Show("편성을 저장하지 못했습니다. 다시 시도해 주세요");
                return;
            }
            int next = (service.ActivePresetIndex + direction + service.PresetCount) % service.PresetCount;
            if (!service.Select(next))
            {
                toast?.Show("프리셋을 전환하지 못했습니다. 다시 시도해 주세요");
                return;
            }
            state.Load(database);
            UpdatePresetControls();
            foreach (CharacterData character in selectionMarks.Keys) UpdateSelectionMark(character);
            RebuildSlots();
            toast?.Show("프리셋 " + (next + 1) + "을 불러왔습니다");
        }

        void RenameActivePreset(string value)
        {
            FormationPresetService service = FormationPresetService.Default;
            if (!string.IsNullOrWhiteSpace(value)
                && !service.Rename(service.ActivePresetIndex, value))
                toast?.Show("프리셋 이름을 저장하지 못했습니다");
            UpdatePresetControls();
        }

        void UpdatePresetControls()
        {
            FormationPresetService service = FormationPresetService.Default;
            int index = service.ActivePresetIndex;
            if (presetIndexLabel != null)
                presetIndexLabel.text = "PRESET " + (index + 1) + " / " + service.PresetCount;
            if (presetNameInput != null)
            {
                FormationPreset preset = service.GetActive(database);
                presetNameInput.SetTextWithoutNotify(preset?.name ?? "파티 " + (index + 1));
            }
        }

        void BuildRosterArea(RectTransform workspace)
        {
            RectTransform panel = ui.CreateImage("Roster Panel", workspace, UrbanFantasyStyle.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-400, -18), new Vector2(760, 680)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("Roster Title", "보유 캐릭터", panel, 20, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(112, -34), new Vector2(180, 32), TextAnchor.MiddleLeft);
            ui.CreateText("Roster Subtitle", "C H A R A C T E R   L I S T", panel, 10, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-122, -34), new Vector2(210, 24), TextAnchor.MiddleRight);
            GameObject roleFilter = ui.CreateStyledButton("Role Filter", panel,
                new Vector2(0, 1), new Vector2(126, -82), new Vector2(210, 34),
                string.Empty, 12, StarfallButtonStyle.Tab, CycleRoleFilter);
            roleFilterLabel = roleFilter.GetComponentInChildren<Text>();
            GameObject sort = ui.CreateStyledButton("Roster Sort", panel,
                new Vector2(0, 1), new Vector2(356, -82), new Vector2(210, 34),
                string.Empty, 12, StarfallButtonStyle.Tab, CycleRosterSort);
            sortLabel = sort.GetComponentInChildren<Text>();
            UpdateRosterControlLabels();
            ui.CreateImage("Roster Header Line", panel, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -108), new Vector2(-30, 1));

            Image viewportImage = ui.CreateImage("Roster Viewport", panel, new Color(.005f, .005f, .008f, .52f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -50), new Vector2(730, 544), true);
            RectTransform viewport = viewportImage.rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Roster Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            rosterContent = contentObject.GetComponent<RectTransform>();
            rosterContent.anchorMin = new Vector2(0, 1);
            rosterContent.anchorMax = new Vector2(1, 1);
            rosterContent.pivot = new Vector2(.5f, 1);
            rosterContent.anchoredPosition = Vector2.zero;
            rosterContent.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(220, 250);
            grid.spacing = new Vector2(14, 14);
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = rosterContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = .08f;
            scroll.scrollSensitivity = 34f;

            emptyLabel = ui.CreateText("Empty Roster", "보유한 캐릭터가 없습니다.\n\n로비의 모집 메뉴에서\n캐릭터를 획득하세요.",
                viewport, 18, FontStyle.Normal, UrbanFantasyStyle.Muted, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-80, -80), TextAnchor.MiddleCenter);
        }

        void BuildSlotArea(RectTransform workspace)
        {
            RectTransform panel = ui.CreateImage("Formation Slots Panel", workspace,
                UrbanFantasyStyle.Panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(405, -18), new Vector2(780, 680)).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("Slots Title", "현재 편성", panel, 20, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(104, -34), new Vector2(170, 32), TextAnchor.MiddleLeft);
            memberCountLabel = ui.CreateText("Member Count", "0 / 4", panel, 15, FontStyle.Normal,
                UrbanFantasyStyle.Silver,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -34), new Vector2(90, 32), TextAnchor.MiddleRight);
            totalPowerLabel = ui.CreateText("Total Power", "TOTAL POWER  0", panel, 17, FontStyle.Normal,
                UrbanFantasyStyle.Gold,
                new Vector2(.62f, 0), new Vector2(1, 0), new Vector2(-20, 28), new Vector2(-20, 36), TextAnchor.MiddleRight);
            teamSummaryLabel = ui.CreateText("Team Summary", "편성을 시작하세요", panel, 12,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 0),
                new Vector2(.64f, 0), new Vector2(20, 28), new Vector2(-20, 36),
                TextAnchor.MiddleLeft);
            ui.CreateImage("Slots Header Line", panel, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -62), new Vector2(-30, 1));

            var slotsObject = new GameObject("Slots Root", typeof(RectTransform));
            slotsObject.transform.SetParent(panel, false);
            slotsRoot = slotsObject.GetComponent<RectTransform>();
            slotsRoot.anchorMin = Vector2.zero;
            slotsRoot.anchorMax = Vector2.one;
            slotsRoot.offsetMin = new Vector2(18, 66);
            slotsRoot.offsetMax = new Vector2(-18, -68);
        }

        void RebuildRoster()
        {
            ClearChildren(rosterContent);
            selectionMarks.Clear();
            var visible = new List<CharacterData>();
            if (database != null)
            {
                foreach (CharacterData character in database.Characters)
                {
                    if (character == null || !CharacterProgressionService.IsOwned(character)) continue;
                    CharacterRole? role = RoleFilters[roleFilterIndex];
                    if (role.HasValue && character.Role != role.Value) continue;
                    visible.Add(character);
                }
            }
            visible.Sort(CompareRosterCharacters);
            for (int i = 0; i < visible.Count; i++) CreateCharacterCard(visible[i]);
            emptyLabel.text = roleFilterIndex == 0
                ? "보유한 캐릭터가 없습니다.\n\n로비의 모집 메뉴에서\n캐릭터를 획득하세요."
                : "선택한 역할의 보유 캐릭터가 없습니다.\n\n다른 역할 필터를 선택해 주세요.";
            emptyLabel.gameObject.SetActive(visible.Count == 0);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rosterContent);
        }

        void CycleRoleFilter()
        {
            roleFilterIndex = (roleFilterIndex + 1) % RoleFilters.Length;
            UpdateRosterControlLabels();
            RebuildRoster();
        }

        void CycleRosterSort()
        {
            rosterSort = (RosterSort)(((int)rosterSort + 1)
                % System.Enum.GetValues(typeof(RosterSort)).Length);
            UpdateRosterControlLabels();
            RebuildRoster();
        }

        void UpdateRosterControlLabels()
        {
            if (roleFilterLabel != null)
                roleFilterLabel.text = "역할  ·  " + (RoleFilters[roleFilterIndex].HasValue
                    ? RoleLabel(RoleFilters[roleFilterIndex].Value) : "전체");
            if (sortLabel != null)
                sortLabel.text = "정렬  ·  " + SortLabel(rosterSort);
        }

        int CompareRosterCharacters(CharacterData left, CharacterData right)
        {
            int comparison;
            switch (rosterSort)
            {
                case RosterSort.Level:
                    comparison = CharacterProgressionService.GetLevel(right)
                        .CompareTo(CharacterProgressionService.GetLevel(left));
                    break;
                case RosterSort.Rarity:
                    comparison = right.Rarity.CompareTo(left.Rarity);
                    break;
                case RosterSort.Name:
                    comparison = string.Compare(left.DisplayName, right.DisplayName,
                        System.StringComparison.CurrentCultureIgnoreCase);
                    break;
                default:
                    comparison = CharacterProgressionService.GetCombatPower(right)
                        .CompareTo(CharacterProgressionService.GetCombatPower(left));
                    break;
            }
            return comparison != 0 ? comparison : string.Compare(left.Id, right.Id,
                System.StringComparison.Ordinal);
        }

        static string SortLabel(RosterSort value)
        {
            switch (value)
            {
                case RosterSort.Level: return "레벨";
                case RosterSort.Rarity: return "희귀도";
                case RosterSort.Name: return "이름";
                default: return "전투력";
            }
        }

        void CreateCharacterCard(CharacterData character)
        {
            GameObject cardButton = ui.CreateButton("Character " + character.DisplayName, rosterContent,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(220, 250), string.Empty, 16,
                UrbanFantasyStyle.PanelSoft, () => ToggleCharacter(character));
            UrbanFantasyStyle.AddBorder(ui, cardButton.GetComponent<RectTransform>());
            Image mark = ui.CreateImage("Selected", cardButton.transform, new Color(1, 1, 1, 0),
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
            mark.transform.SetAsFirstSibling();
            selectionMarks[character] = mark;

            Image portrait = ui.CreateImage("Portrait", cardButton.transform,
                new Color(character.AccentColor.r, character.AccentColor.g, character.AccentColor.b, .25f),
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -82), new Vector2(190, 134));
            SetPortrait(portrait, character, 46);
            ui.CreateText("Rarity", new string('★', character.Rarity), cardButton.transform, 14, FontStyle.Bold,
                UrbanFantasyStyle.Gold, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -157),
                new Vector2(192, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Character Name", character.DisplayName, cardButton.transform, 18, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 64),
                new Vector2(192, 28), TextAnchor.MiddleLeft);
            ui.CreateText("Character Info", RoleLabel(character.Role) + "  ·  Lv." +
                CharacterProgressionService.GetLevel(character),
                cardButton.transform, 13, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 37), new Vector2(192, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Character Power", "전투력  " +
                CharacterProgressionService.GetCombatPower(character).ToString("N0"),
                cardButton.transform, 13, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 15), new Vector2(192, 22), TextAnchor.MiddleRight);
            UpdateSelectionMark(character);
        }

        void ToggleCharacter(CharacterData character)
        {
            FormationToggleResult result = state.Toggle(character);
            if (result == FormationToggleResult.Full)
            {
                toast.Show("편성은 최대 4명까지 가능합니다");
                return;
            }
            foreach (CharacterData item in selectionMarks.Keys)
                UpdateSelectionMark(item);
            RebuildSlots();
        }

        void RebuildSlots()
        {
            ClearChildren(slotsRoot);
            for (int i = 0; i < FormationState.MaxMembers; i++)
                CreateSlot(i, i < state.Count ? state.Members[i] : null);
            memberCountLabel.text = state.Count + " / " + FormationState.MaxMembers;
            totalPowerLabel.text = "TOTAL POWER  " + state.TotalPower.ToString("N0");
            RefreshTeamSummary();
        }

        void RefreshTeamSummary()
        {
            if (teamSummaryLabel == null) return;
            if (state.Count == 0)
            {
                teamSummaryLabel.text = "편성을 시작하세요";
                teamSummaryLabel.color = UrbanFantasyStyle.Muted;
                return;
            }

            int totalLevel = 0;
            int healerCount = 0;
            var elements = new HashSet<BattleElement>();
            for (int i = 0; i < state.Members.Count; i++)
            {
                CharacterData character = state.Members[i];
                if (character == null) continue;
                totalLevel += CharacterProgressionService.GetLevel(character);
                if (character.Role == CharacterRole.Healer) healerCount++;
                if (character.Element != BattleElement.Auto) elements.Add(character.Element);
            }
            int averageLevel = state.Count > 0 ? Mathf.RoundToInt(totalLevel / (float)state.Count) : 0;
            teamSummaryLabel.text = "평균 LV." + averageLevel + "  ·  속성 " + elements.Count
                + (healerCount == 0 ? "  ·  회복 역할 없음" : "  ·  역할 균형 양호");
            teamSummaryLabel.color = healerCount == 0
                ? UrbanFantasyStyle.Warning : UrbanFantasyStyle.Success;
        }

        void CreateSlot(int index, CharacterData character)
        {
            int column = index % 2;
            int row = index / 2;
            Vector2 position = new Vector2(-178 + column * 356, 136 - row * 260);
            string label = character == null ? "+\nSLOT 0" + (index + 1) : string.Empty;
            GameObject slot = ui.CreateButton("Formation Slot " + (index + 1), slotsRoot, new Vector2(.5f, .5f),
                position, new Vector2(330, 238), label, 18, UrbanFantasyStyle.PanelSoft,
                character == null ? (System.Action)null : () => ToggleCharacter(character));
            UrbanFantasyStyle.AddBorder(ui, slot.GetComponent<RectTransform>(), character == null
                ? UrbanFantasyStyle.Line : UrbanFantasyStyle.StrongLine);
            if (character == null) return;

            Image portrait = ui.CreateImage("Slot Portrait", slot.transform,
                new Color(character.AccentColor.r, character.AccentColor.g, character.AccentColor.b, .22f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(75, 8), new Vector2(126, 184));
            SetPortrait(portrait, character, 42);
            ui.CreateText("Slot Number", "0" + (index + 1), slot.transform, 13, FontStyle.Normal,
                UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(25, -20), new Vector2(32, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Slot Name", character.DisplayName, slot.transform, 18, FontStyle.Normal,
                UrbanFantasyStyle.Silver,
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-86, 46), new Vector2(150, 32), TextAnchor.MiddleLeft);
            ui.CreateText("Slot Role", RoleLabel(character.Role) + "  ·  Lv." +
                CharacterProgressionService.GetLevel(character), slot.transform,
                13, FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-86, 14), new Vector2(150, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Slot Power", CharacterProgressionService.GetCombatPower(character).ToString("N0"),
                slot.transform, 14, FontStyle.Bold,
                UrbanFantasyStyle.Gold, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-86, -23),
                new Vector2(150, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Remove Guide", "터치하여 제외", slot.transform, 11, FontStyle.Normal,
                new Color(1, 1, 1, .42f), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-86, 20),
                new Vector2(150, 20), TextAnchor.MiddleLeft);
        }

        void SetPortrait(Image image, CharacterData character, int initialSize)
        {
            if (character.Portrait != null)
            {
                image.sprite = character.Portrait;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.color = Color.white;
                return;
            }
            string initial = character.DisplayName.Length > 0 ? character.DisplayName.Substring(0, 1) : "?";
            ui.CreateText("Initial", initial, image.transform, initialSize, FontStyle.Bold, ui.Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        }

        void UpdateSelectionMark(CharacterData character)
        {
            if (!selectionMarks.TryGetValue(character, out Image mark)) return;
            mark.color = state.Contains(character)
                ? UrbanFantasyStyle.Highlight
                : new Color(1, 1, 1, 0);
        }

        void ClearFormation()
        {
            state.Clear();
            foreach (CharacterData character in selectionMarks.Keys)
                UpdateSelectionMark(character);
            RebuildSlots();
        }

        void SaveAndReturn()
        {
            if (!changingScene) StartCoroutine(SaveAndReturnRoutine());
        }

        IEnumerator SaveAndReturnRoutine()
        {
            changingScene = true;
            if (!state.Save())
            {
                changingScene = false;
                toast.Show("편성을 저장하지 못했습니다. 다시 시도해 주세요");
                yield break;
            }
            toast.Show("편성을 저장했습니다  ·  " + state.Count + "명");
            yield return new WaitForSecondsRealtime(.55f);
            StarfallSceneFlow.Load(SceneNavigation.ConsumeFormationReturnScene());
        }

        void ReturnToLobby()
        {
            if (changingScene) return;
            changingScene = true;
            StarfallSceneFlow.Load(SceneNavigation.ConsumeFormationReturnScene());
        }

        static string RoleLabel(CharacterRole role)
        {
            switch (role)
            {
                case CharacterRole.Striker: return "스트라이커";
                case CharacterRole.Support: return "서포터";
                case CharacterRole.Tank: return "탱커";
                case CharacterRole.Healer: return "힐러";
                default: return "스페셜";
            }
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

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }
    }
}
