using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    /// <summary>
    /// Runtime-created lobby overlay. LobbyScreen can create this controller beside its other
    /// overlay controllers and call Initialize/Open without adding a scene or prefab.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class AttendancePopup : MonoBehaviour
    {
        sealed class DayCell
        {
            public GameObject Root;
            public Image Panel;
            public Text DayLabel;
            public Text RewardLabel;
            public Text StateLabel;
        }

        readonly List<DayCell> cells = new List<DayCell>();
        GameObject layer;
        LobbyUiFactory ui;
        LobbyToastOverlay toast;
        AttendanceCampaignDatabase database;
        AttendanceService service;
        AttendanceCampaignData selectedCampaign;
        Text campaignTitle;
        Text scheduleLabel;
        Text statusLabel;
        Button claimButton;
        Text claimButtonLabel;

        public bool IsOpen => layer != null && layer.activeSelf;

        public void Initialize(RectTransform parent, LobbyUiFactory factory,
            LobbyToastOverlay toastOverlay, AttendanceCampaignDatabase campaignDatabase = null,
            AttendanceService attendanceService = null)
        {
            ui = factory ?? new LobbyUiFactory(new LobbyTheme());
            toast = toastOverlay;
            database = campaignDatabase != null ? campaignDatabase
                : Resources.Load<AttendanceCampaignDatabase>("Data/AttendanceCampaignDatabase");
            service = attendanceService ?? AttendanceService.Default;

            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;
            Build();
        }

        public void Open()
        {
            if (layer == null) return;
            selectedCampaign = ResolveCampaign();
            layer.transform.SetAsLastSibling();
            layer.SetActive(true);
            Refresh();
        }

        public bool TryOpenIfClaimable()
        {
            if (database == null || !service.HasClaimableReward(database)) return false;
            Open();
            return true;
        }

        public void Close()
        {
            if (layer != null) layer.SetActive(false);
        }

        public void Refresh()
        {
            if (layer == null) return;
            if (selectedCampaign == null) selectedCampaign = ResolveCampaign();
            if (selectedCampaign == null)
            {
                campaignTitle.text = "출석 캠페인 없음";
                scheduleLabel.text = "AttendanceCampaignDatabase를 확인해 주세요.";
                statusLabel.text = "현재 표시할 캠페인이 없습니다.";
                claimButton.interactable = false;
                claimButtonLabel.text = "수령 불가";
                for (int i = 0; i < cells.Count; i++) cells[i].Root.SetActive(false);
                return;
            }

            campaignTitle.text = selectedCampaign.DisplayName;
            scheduleLabel.text = ScheduleText(selectedCampaign.Schedule);
            AttendanceProgress progress = service.GetProgress(selectedCampaign);
            bool claimable = service.TryGetClaimableDay(selectedCampaign,
                out AttendanceDayDefinition claimableDay, out string reason);
            statusLabel.text = reason;
            claimButton.interactable = claimable;
            claimButtonLabel.text = claimable && claimableDay != null
                ? "Day " + claimableDay.DayNumber + " 보상 수령" : "수령 불가";

            int displayCount = Mathf.Min(cells.Count, selectedCampaign.DayCount);
            for (int i = 0; i < cells.Count; i++)
            {
                DayCell cell = cells[i];
                bool visible = i < displayCount;
                cell.Root.SetActive(visible);
                if (!visible) continue;
                AttendanceDayDefinition day = selectedCampaign.GetDay(i);
                bool claimed = progress != null && (progress.Completed
                    || i < progress.CurrentSequenceIndex);
                bool next = claimable && progress != null
                    && i == progress.CurrentSequenceIndex;
                cell.DayLabel.text = "DAY " + (day == null ? (i + 1) : day.DayNumber).ToString("00");
                cell.RewardLabel.text = day == null || day.Reward == null
                    ? "보상 없음" : day.Reward.Summary;
                cell.StateLabel.text = claimed ? "CLAIMED" : next ? "AVAILABLE" : "LOCKED";
                cell.Panel.color = claimed ? new Color(.12f, .18f, .16f, .96f)
                    : next ? new Color(.19f, .18f, .12f, .98f)
                    : UrbanFantasyStyle.PanelSoft;
            }
        }

        void Build()
        {
            layer = new GameObject("Attendance Layer", typeof(RectTransform));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;

            Image dim = ui.CreateImage("Attendance Backdrop", layer.transform,
                UrbanFantasyStyle.Backdrop, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, true);
            Button backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(Close);

            RectTransform card = ui.CreateImage("Attendance Window", layer.transform,
                UrbanFantasyStyle.PanelStrong, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(1040, 700), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            ui.CreateImage("Attendance Accent", card, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-52, 2));
            ui.CreateText("Attendance Eyebrow", "L O G I N   R E W A R D S", card, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -35), new Vector2(-76, 22), TextAnchor.MiddleLeft);
            campaignTitle = ui.CreateText("Attendance Title", string.Empty, card, 30,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -73), new Vector2(-76, 42), TextAnchor.MiddleLeft);
            scheduleLabel = ui.CreateText("Attendance Schedule", string.Empty, card, 12,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -108), new Vector2(-76, 24), TextAnchor.MiddleLeft);
            ui.CreateButton("Close Attendance", card, new Vector2(1, 1), new Vector2(-34, -34),
                new Vector2(46, 46), "×", 27, UrbanFantasyStyle.PanelSoft, Close);

            for (int i = 0; i < 7; i++) CreateDayCell(card, i);

            statusLabel = ui.CreateText("Attendance Status", string.Empty, card, 13,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, 102), new Vector2(850, 28), TextAnchor.MiddleCenter);
            GameObject claim = ui.CreateButton("Claim Attendance", card, new Vector2(.5f, 0),
                new Vector2(0, 52), new Vector2(310, 62), "보상 수령", 18,
                new Color(.18f, .17f, .14f, .98f), Claim);
            UrbanFantasyStyle.AddBorder(ui, claim.GetComponent<RectTransform>(),
                UrbanFantasyStyle.StrongLine);
            claimButton = claim.GetComponent<Button>();
            claimButtonLabel = claim.GetComponentInChildren<Text>();
            layer.SetActive(false);
        }

        void CreateDayCell(RectTransform parent, int index)
        {
            int row = index < 4 ? 0 : 1;
            int column = index < 4 ? index : index - 4;
            int columns = row == 0 ? 4 : 3;
            float spacing = row == 0 ? 224f : 224f;
            float startX = -(columns - 1) * spacing * .5f;
            float y = row == 0 ? 125f : -85f;
            Image panel = ui.CreateImage("Attendance Day " + (index + 1), parent,
                UrbanFantasyStyle.PanelSoft, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(startX + column * spacing, y), new Vector2(204, 168));
            UrbanFantasyStyle.AddBorder(ui, panel.rectTransform);
            Text day = ui.CreateText("Day", string.Empty, panel.transform, 13, FontStyle.Bold,
                UrbanFantasyStyle.Gold, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -25), new Vector2(-24, 24), TextAnchor.MiddleLeft);
            Text reward = ui.CreateText("Reward", string.Empty, panel.transform, 12, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, -7), new Vector2(-24, -62), TextAnchor.MiddleLeft);
            reward.resizeTextForBestFit = true;
            reward.resizeTextMinSize = 9;
            Text state = ui.CreateText("State", string.Empty, panel.transform, 10, FontStyle.Bold,
                UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 20), new Vector2(-24, 20), TextAnchor.MiddleRight);
            cells.Add(new DayCell
            {
                Root = panel.gameObject,
                Panel = panel,
                DayLabel = day,
                RewardLabel = reward,
                StateLabel = state
            });
        }

        AttendanceCampaignData ResolveCampaign()
        {
            if (database == null || database.Campaigns == null) return null;
            AttendanceCampaignData firstActive = null;
            for (int i = 0; i < database.Campaigns.Count; i++)
            {
                AttendanceCampaignData campaign = database.Campaigns[i];
                if (campaign == null || !campaign.IsAvailable(ContentTime.UtcNow)) continue;
                if (firstActive == null) firstActive = campaign;
                if (service.CanClaim(campaign)) return campaign;
            }
            return firstActive;
        }

        void Claim()
        {
            AttendanceClaimResult result = service.Claim(selectedCampaign);
            if (toast != null) toast.Show(result.Message);
            Refresh();
        }

        static string ScheduleText(ScheduleRange schedule)
        {
            if (schedule == null || !schedule.IsValid) return "잘못된 캠페인 기간";
            string start = schedule.StartUtc.HasValue
                ? schedule.StartUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'") : "상시";
            string end = schedule.EndUtc.HasValue
                ? schedule.EndUtc.Value.ToString("yyyy-MM-dd HH:mm 'UTC'") : "제한 없음";
            return start + "  —  " + end;
        }
    }
}
