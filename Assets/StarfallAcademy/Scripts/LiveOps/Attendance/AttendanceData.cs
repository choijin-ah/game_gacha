using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum AttendanceCycleMode
    {
        Looping,
        FixedPeriod
    }

    [Serializable]
    public sealed class AttendanceDayDefinition
    {
        [SerializeField, Min(1)] int dayNumber = 1;
        [SerializeField] RewardPackage reward = new RewardPackage();

        public int DayNumber => Mathf.Max(1, dayNumber);
        public RewardPackage Reward => reward ?? (reward = new RewardPackage());

        internal void Sanitize(int fallbackDayNumber)
        {
            dayNumber = Mathf.Max(1, dayNumber > 0 ? dayNumber : fallbackDayNumber);
            if (reward == null) reward = new RewardPackage();
        }
    }

    [CreateAssetMenu(fileName = "AttendanceCampaign",
        menuName = "Starfall/LiveOps/Attendance Campaign")]
    public sealed class AttendanceCampaignData : ScriptableObject
    {
        [SerializeField] string campaignId = "attendance_campaign";
        [SerializeField] string displayName = "7일 출석 캠페인";
        [SerializeField] ScheduleRange schedule = new ScheduleRange();
        [SerializeField] AttendanceCycleMode cycleMode = AttendanceCycleMode.FixedPeriod;
        [SerializeField] List<AttendanceDayDefinition> days =
            new List<AttendanceDayDefinition>();

        public string CampaignId => string.IsNullOrWhiteSpace(campaignId)
            ? name : campaignId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name : displayName.Trim();
        public ScheduleRange Schedule => schedule ?? (schedule = new ScheduleRange());
        public AttendanceCycleMode CycleMode => cycleMode;
        public IReadOnlyList<AttendanceDayDefinition> Days => days
            ?? (IReadOnlyList<AttendanceDayDefinition>)Array.Empty<AttendanceDayDefinition>();
        public int DayCount => days == null ? 0 : days.Count;

        public bool IsAvailable(DateTime utcNow) => Schedule.Contains(utcNow);

        public AttendanceDayDefinition GetDay(int sequenceIndex)
        {
            if (days == null || sequenceIndex < 0 || sequenceIndex >= days.Count) return null;
            return days[sequenceIndex];
        }

        void OnValidate()
        {
            campaignId = campaignId == null ? string.Empty : campaignId.Trim();
            displayName = displayName == null ? string.Empty : displayName.Trim();
            if (schedule == null) schedule = new ScheduleRange();
            cycleMode = (AttendanceCycleMode)Mathf.Clamp((int)cycleMode,
                (int)AttendanceCycleMode.Looping, (int)AttendanceCycleMode.FixedPeriod);
            if (days == null) days = new List<AttendanceDayDefinition>();
            for (int i = 0; i < days.Count; i++)
            {
                if (days[i] == null) days[i] = new AttendanceDayDefinition();
                days[i].Sanitize(i + 1);
            }
        }
    }

    [CreateAssetMenu(fileName = "AttendanceCampaignDatabase",
        menuName = "Starfall/LiveOps/Attendance Campaign Database")]
    public sealed class AttendanceCampaignDatabase : ScriptableObject
    {
        [SerializeField] List<AttendanceCampaignData> campaigns =
            new List<AttendanceCampaignData>();

        public IReadOnlyList<AttendanceCampaignData> Campaigns => campaigns;

        public AttendanceCampaignData Find(string campaignId)
        {
            if (string.IsNullOrWhiteSpace(campaignId) || campaigns == null) return null;
            string normalized = campaignId.Trim();
            for (int i = 0; i < campaigns.Count; i++)
            {
                AttendanceCampaignData campaign = campaigns[i];
                if (campaign != null && string.Equals(campaign.CampaignId, normalized,
                    StringComparison.Ordinal)) return campaign;
            }
            return null;
        }

        public List<AttendanceCampaignData> GetActive(DateTime utcNow)
        {
            var result = new List<AttendanceCampaignData>();
            if (campaigns == null) return result;
            for (int i = 0; i < campaigns.Count; i++)
            {
                AttendanceCampaignData campaign = campaigns[i];
                if (campaign != null && campaign.IsAvailable(utcNow)) result.Add(campaign);
            }
            return result;
        }

        public void Add(AttendanceCampaignData campaign)
        {
            if (campaign == null) return;
            if (campaigns == null) campaigns = new List<AttendanceCampaignData>();
            if (!campaigns.Contains(campaign)) campaigns.Add(campaign);
        }

        public void Remove(AttendanceCampaignData campaign)
        {
            if (campaigns != null) campaigns.Remove(campaign);
        }

        public bool Move(AttendanceCampaignData campaign, int offset)
        {
            if (campaigns == null || campaign == null || offset == 0) return false;
            int index = campaigns.IndexOf(campaign);
            int target = index + offset;
            if (index < 0 || target < 0 || target >= campaigns.Count) return false;
            campaigns.RemoveAt(index);
            campaigns.Insert(target, campaign);
            return true;
        }

        void OnValidate()
        {
            if (campaigns == null) campaigns = new List<AttendanceCampaignData>();
        }
    }
}
