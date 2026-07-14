using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum GachaBannerType
    {
        Standard,
        Pickup,
        Event
    }

    [CreateAssetMenu(fileName = "GachaBanner", menuName = "Starfall/Gacha Banner")]
    public sealed class GachaBannerData : GachaConfig
    {
        [Header("Banner Identity")]
        [SerializeField] string bannerId = "banner_new";
        [SerializeField] GachaBannerType bannerType = GachaBannerType.Pickup;
        [SerializeField] Sprite bannerImage;
        [SerializeField] ScheduleRange schedule = new ScheduleRange();
        [SerializeField] string ticketItemId;

        // Kept only so the editor migration can prove that a legacy source was
        // already converted. Runtime selection never depends on this reference.
        [SerializeField, HideInInspector] GachaConfig legacySource;

        public string Id => string.IsNullOrWhiteSpace(bannerId) ? string.Empty : bannerId.Trim();
        public GachaBannerType BannerType => bannerType;
        public Sprite BannerImage => bannerImage;
        public ScheduleRange Schedule => schedule;
        public string TicketItemId => ticketItemId == null ? string.Empty : ticketItemId.Trim();
        public GachaConfig LegacySource => legacySource;

        public ScheduleState GetScheduleState(DateTime utcNow) =>
            schedule == null ? ScheduleState.Active : schedule.GetState(utcNow);

        public bool IsAvailableAt(DateTime utcNow) =>
            schedule == null || schedule.Contains(utcNow);

        protected override void OnValidate()
        {
            base.OnValidate();
            bannerId = bannerId == null ? string.Empty : bannerId.Trim();
            ticketItemId = ticketItemId == null ? string.Empty : ticketItemId.Trim();
            if (schedule == null) schedule = new ScheduleRange();
        }
    }
}
