using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public static class GachaBannerScheduleService
    {
        public const string DatabaseResourcePath = "Data/GachaBannerDatabase";
        public const string LegacyConfigResourcePath = "Data/GachaConfig";
        public const string SelectedBannerKey = "StarfallAcademy.Gacha.SelectedBanner";

        public static GachaBannerData GetActiveBanner(GachaBannerDatabase database) =>
            GetActiveBanner(database, ContentTime.UtcNow);

        public static GachaBannerData GetActiveBanner(GachaBannerDatabase database,
            DateTime utcNow)
        {
            if (database == null || database.Banners == null) return null;
            DateTime now = ScheduleRange.NormalizeUtc(utcNow);
            string selectedId = PlayerPrefs.GetString(SelectedBannerKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                GachaBannerData selected = database.Find(selectedId);
                if (selected != null && selected.IsAvailableAt(now)) return selected;
            }
            for (int i = 0; i < database.Banners.Count; i++)
            {
                GachaBannerData banner = database.Banners[i];
                if (banner != null && banner.IsAvailableAt(now)) return banner;
            }
            return null;
        }

        public static List<GachaBannerData> GetActiveBanners(GachaBannerDatabase database,
            DateTime utcNow)
        {
            var result = new List<GachaBannerData>();
            if (database == null || database.Banners == null) return result;
            DateTime now = ScheduleRange.NormalizeUtc(utcNow);
            for (int i = 0; i < database.Banners.Count; i++)
            {
                GachaBannerData banner = database.Banners[i];
                if (banner != null && banner.IsAvailableAt(now)) result.Add(banner);
            }
            return result;
        }

        public static bool SelectBanner(GachaBannerData banner, DateTime utcNow)
        {
            if (banner == null || !banner.IsAvailableAt(ScheduleRange.NormalizeUtc(utcNow)))
                return false;
            PlayerPrefs.SetString(SelectedBannerKey, banner.Id);
            PlayerPrefs.Save();
            return true;
        }

        public static void ClearSelection()
        {
            PlayerPrefs.DeleteKey(SelectedBannerKey);
            PlayerPrefs.Save();
        }

        public static GachaConfig LoadActiveOrLegacy(out GachaBannerDatabase database,
            out GachaBannerData activeBanner)
        {
            database = Resources.Load<GachaBannerDatabase>(DatabaseResourcePath);
            activeBanner = GetActiveBanner(database);
            if (activeBanner != null) return activeBanner;

            // Existing projects keep working before the one-time editor migration.
            // Once a non-empty banner database exists, an inactive schedule remains
            // inactive instead of silently exposing the old banner.
            if (database == null || database.Banners == null || database.Banners.Count == 0)
                return Resources.Load<GachaConfig>(LegacyConfigResourcePath);
            return null;
        }
    }
}
