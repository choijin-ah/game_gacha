using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbyMissionPanel : MonoBehaviour
    {
        sealed class MissionRow
        {
            public DailyMissionType Type;
            public Text Progress;
            public Text Reward;
            public Button ClaimButton;
            public Text ClaimLabel;
        }

        readonly List<MissionRow> rows = new List<MissionRow>();
        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        GameObject layer;
        Text profileLabel;
        Text staminaLabel;
        float nextRefreshTime;

        void Update()
        {
            if (layer == null || !layer.activeSelf || Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + 1f;
            MissionService.RecordLogin();
            Refresh();
        }

        public void Initialize(RectTransform parent, LobbyUiFactory factory, LobbyToastOverlay toastOverlay)
        {
            ui = factory;
            toast = toastOverlay;
            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;

            layer = new GameObject("Daily Mission Layer", typeof(RectTransform));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;

            Image dim = ui.CreateImage("Mission Backdrop", layer.transform, UrbanFantasyStyle.Backdrop,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(Close);

            RectTransform card = ui.CreateImage("Mission Window", layer.transform,
                UrbanFantasyStyle.PanelStrong, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(980, 810), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            ui.CreateText("Mission Eyebrow", "D A I L Y   M I S S I O N", card, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -32), new Vector2(-80, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Mission Title", "오늘의 임무", card, 31, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(.55f, 1),
                new Vector2(0, -72), new Vector2(-40, 42), TextAnchor.MiddleLeft);
            ui.CreateButton("Close Missions", card, new Vector2(1, 1), new Vector2(-34, -34),
                new Vector2(48, 48), "×", 27, UrbanFantasyStyle.PanelSoft, Close);
            ui.CreateImage("Mission Divider", card, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -112), new Vector2(-80, 1));

            profileLabel = ui.CreateText("Account Profile", string.Empty, card, 13, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(.5f, 1), new Vector2(1, 1),
                new Vector2(0, -72), new Vector2(-48, 28), TextAnchor.MiddleRight);
            staminaLabel = ui.CreateText("Account Stamina", string.Empty, card, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(.5f, 1), new Vector2(1, 1),
                new Vector2(0, -96), new Vector2(-48, 24), TextAnchor.MiddleRight);

            IReadOnlyList<DailyMissionDefinition> definitions = MissionService.Default.Definitions;
            for (int i = 0; i < definitions.Count; i++)
                CreateMissionRow(card, definitions[i], 164 + i * 120);

            GameObject claimAll = ui.CreateButton("Claim All Missions", card, new Vector2(.5f, 0),
                new Vector2(0, 52), new Vector2(280, 58), "완료 보상 모두 받기", 16,
                new Color(.17f, .17f, .20f, .98f), ClaimAll);
            UrbanFantasyStyle.AddBorder(ui, claimAll.GetComponent<RectTransform>(), UrbanFantasyStyle.Gold);
            ui.CreateText("Mission Reset", "매일 00:00 UTC에 진행도가 초기화됩니다", card, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 18), new Vector2(-80, 20), TextAnchor.MiddleCenter);
            layer.SetActive(false);
        }

        void CreateMissionRow(RectTransform parent, DailyMissionDefinition definition, float y)
        {
            RectTransform row = ui.CreateImage("Mission " + definition.Id, parent,
                UrbanFantasyStyle.PanelSoft, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -y), new Vector2(-80, 98), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, row);
            ui.CreateText("Mission Name", MissionName(definition.Type), row, 18, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(138, 17), new Vector2(220, 30), TextAnchor.MiddleLeft);
            Text progress = ui.CreateText("Mission Progress", string.Empty, row, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(138, -17), new Vector2(220, 24), TextAnchor.MiddleLeft);
            Text reward = ui.CreateText("Mission Reward", string.Empty, row, 13, FontStyle.Normal,
                UrbanFantasyStyle.Gold, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(72, 0), new Vector2(310, 44), TextAnchor.MiddleLeft);
            DailyMissionType type = definition.Type;
            GameObject claim = ui.CreateButton("Claim " + definition.Id, row, new Vector2(1, .5f),
                new Vector2(-104, 0), new Vector2(170, 50), string.Empty, 14,
                new Color(.17f, .17f, .20f, .98f), () => Claim(type));
            UrbanFantasyStyle.AddBorder(ui, claim.GetComponent<RectTransform>());
            rows.Add(new MissionRow
            {
                Type = type,
                Progress = progress,
                Reward = reward,
                ClaimButton = claim.GetComponent<Button>(),
                ClaimLabel = claim.GetComponentInChildren<Text>()
            });
        }

        public void Open()
        {
            MissionService.RecordLogin();
            Refresh();
            layer.SetActive(true);
            layer.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (layer != null) layer.SetActive(false);
        }

        void Claim(DailyMissionType type)
        {
            MissionClaimResult result = MissionService.ClaimDailyReward(type);
            toast?.Show(result.Succeeded ? "임무 보상을 받았습니다"
                : result.Status == MissionClaimStatus.AlreadyClaimed ? "이미 받은 보상입니다"
                : "아직 임무가 완료되지 않았습니다");
            Refresh();
        }

        void ClaimAll()
        {
            int claimed = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                DailyMissionProgress progress = MissionService.GetDailyProgress(rows[i].Type);
                if (progress.CanClaim && MissionService.ClaimDailyReward(rows[i].Type).Succeeded)
                    claimed++;
            }
            toast?.Show(claimed > 0 ? "임무 보상 " + claimed + "개를 받았습니다" : "받을 수 있는 보상이 없습니다");
            Refresh();
        }

        void Refresh()
        {
            int level = PlayerProfileService.CurrentLevel;
            int experience = PlayerProfileService.CurrentExperience;
            int required = PlayerProfileService.Default.GetRequiredExperienceForNextLevel(level);
            profileLabel.text = level >= PlayerProfileService.Default.MaximumLevel
                ? "ACCOUNT LV. " + level + "  ·  MAX"
                : "ACCOUNT LV. " + level + "  ·  EXP " + experience.ToString("N0") + " / " + required.ToString("N0");
            StaminaSnapshot stamina = StaminaService.Default.GetSnapshot();
            staminaLabel.text = "행동력  " + stamina.Current + " / " + stamina.Maximum
                + (stamina.IsFull ? "" : "  ·  다음 회복 " + FormatTime(stamina.TimeUntilNextRecovery));

            for (int i = 0; i < rows.Count; i++)
            {
                MissionRow row = rows[i];
                DailyMissionProgress progress = MissionService.GetDailyProgress(row.Type);
                if (progress.Definition == null) continue;
                row.Progress.text = Mathf.Min(progress.Current, progress.Definition.Target).ToString("N0")
                    + " / " + progress.Definition.Target.ToString("N0");
                RewardBundle reward = progress.Definition.Reward;
                row.Reward.text = "● " + reward.Credits.ToString("N0") + "   ◇ "
                    + reward.SkillMaterials.ToString("N0") + "   EXP " + reward.AccountExperience.ToString("N0");
                row.ClaimLabel.text = progress.Claimed ? "수령 완료" : progress.IsComplete ? "보상 받기" : "진행 중";
                row.ClaimButton.interactable = progress.CanClaim;
            }
        }

        static string MissionName(DailyMissionType type)
        {
            switch (type)
            {
                case DailyMissionType.Login: return "오늘 접속하기";
                case DailyMissionType.StaminaSpent: return "행동력 사용";
                case DailyMissionType.Enhancement: return "캐릭터·장비 강화";
                case DailyMissionType.BattleCompleted: return "전투 완료";
                default: return "일일 임무";
            }
        }

        static string FormatTime(System.TimeSpan value) =>
            Mathf.Max(0, (int)value.TotalMinutes).ToString("00") + ":" + Mathf.Max(0, value.Seconds).ToString("00");
    }
}
