using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    internal static class GachaBannerValidation
    {
        internal const string ProviderId = "Gacha Banners";
        const float RateEpsilon = .0001f;

        static GachaBannerValidation()
        {
            ContentValidationRegistry.Register(ProviderId, CollectIssues);
        }

        [MenuItem("Starfall/Validate/Gacha Banners")]
        public static void OpenValidation() => ContentValidationWindow.Open(ProviderId);

        [MenuItem("Starfall/Validate/Gacha Configuration")]
        public static void OpenLegacyValidationAlias() => OpenValidation();

        internal static IEnumerable<ContentValidationIssue> CollectIssues()
        {
            var issues = new List<ContentValidationIssue>();
            GachaBannerDatabase database =
                AssetDatabase.LoadAssetAtPath<GachaBannerDatabase>(
                    GachaBannerDatabaseBootstrap.DatabasePath);
            if (database == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                    ProviderId, "Database", "GachaBannerDatabase asset is missing."));
                return issues;
            }

            CharacterDatabase characters = GachaBannerDatabaseBootstrap.LoadCharacterDatabase();
            if (characters == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                    ProviderId, "Character Database",
                    "CharacterDatabase is required to validate banner pools.", database));
            }

            bool hasTop = false;
            bool hasFour = false;
            bool hasThree = false;
            if (characters != null && characters.Characters != null)
            {
                for (int i = 0; i < characters.Characters.Count; i++)
                {
                    CharacterData character = characters.Characters[i];
                    if (character == null) continue;
                    if (character.Rarity >= 5) hasTop = true;
                    else if (character.Rarity == 4) hasFour = true;
                    else if (character.Rarity == 3) hasThree = true;
                }
            }

            if (database.Banners == null || database.Banners.Count == 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                    ProviderId, "Database", "At least one gacha banner is required.", database));
                return issues;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < database.Banners.Count; i++)
            {
                GachaBannerData banner = database.Banners[i];
                if (banner == null)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                        ProviderId, "Banner " + (i + 1), "The database contains an empty banner reference.",
                        database));
                    continue;
                }

                string location = string.IsNullOrWhiteSpace(banner.Id)
                    ? banner.name : banner.Id;
                if (string.IsNullOrWhiteSpace(banner.Id))
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "bannerId is empty.", banner));
                else if (!ids.Add(banner.Id))
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "bannerId is duplicated.", banner));

                if (banner.Schedule != null && !banner.Schedule.IsValid)
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "The banner schedule is invalid or ends before it starts.", banner));
                var serializedBanner = new SerializedObject(banner);
                SerializedProperty rawPityGroup =
                    serializedBanner.FindProperty("pityGroupId");
                if (rawPityGroup == null
                    || string.IsNullOrWhiteSpace(rawPityGroup.stringValue))
                    issues.Add(Issue(ContentValidationSeverity.Warning, location,
                        "pityGroupId is empty; runtime will use the default group.", banner));
                if (banner.HardPity <= 0 || banner.SoftPityStart <= 0
                    || banner.SoftPityStart > banner.HardPity)
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "Soft/hard pity values are not ordered correctly.", banner));
                if (banner.SinglePullCost < 0 || banner.TenPullCost < 0)
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "Pull costs cannot be negative.", banner));
                if (banner.TopRarityRatePercent <= 0f
                    || banner.FourStarRatePercent < 0f
                    || banner.FeaturedSharePercent < 0f
                    || banner.FeaturedSharePercent > 100f
                    || banner.TopRarityRatePercent + banner.FourStarRatePercent
                        > 100f + RateEpsilon)
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "The absolute rarity rates are outside the 0-100% range.", banner));

                ValidatePickups(issues, banner, characters, location);
                if (!GachaService.ValidateRarityPools(banner.FourStarRatePercent,
                    banner.ThreeStarRatePercent, banner.GuaranteeFourStarOnTenPull, 10,
                    hasTop, hasFour, hasThree, out string poolError))
                    issues.Add(Issue(ContentValidationSeverity.Error, location, poolError, banner));
            }

            for (int left = 0; left < database.Banners.Count; left++)
            {
                GachaBannerData first = database.Banners[left];
                if (first == null || first.Schedule != null && !first.Schedule.IsValid) continue;
                for (int right = left + 1; right < database.Banners.Count; right++)
                {
                    GachaBannerData second = database.Banners[right];
                    if (second == null || second.Schedule != null && !second.Schedule.IsValid) continue;
                    if (!Overlaps(first.Schedule, second.Schedule)) continue;
                    issues.Add(Issue(ContentValidationSeverity.Error, second.Id,
                        "Schedule overlaps banner '" + first.Id
                        + "'. Runtime selects the first active banner in database order.", second));
                }
            }

            List<GachaBannerData> active = GachaBannerScheduleService.GetActiveBanners(
                database, ContentTime.UtcNow);
            if (active.Count == 0)
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning,
                    ProviderId, "Schedule", "No banner is active at the current content UTC.", database));
            return issues;
        }

        static void ValidatePickups(List<ContentValidationIssue> issues,
            GachaBannerData banner, CharacterDatabase characters, string location)
        {
            if (banner.PickupCharacters == null || banner.PickupCharacters.Count == 0)
            {
                if (banner.BannerType == GachaBannerType.Standard) return;
                issues.Add(Issue(ContentValidationSeverity.Error, location,
                    "At least one pickup character is required.", banner));
                return;
            }

            var seen = new HashSet<CharacterData>();
            for (int i = 0; i < banner.PickupCharacters.Count; i++)
            {
                CharacterData pickup = banner.PickupCharacters[i];
                if (pickup == null)
                {
                    issues.Add(Issue(ContentValidationSeverity.Error,
                        location + " / Pickup " + (i + 1), "Pickup reference is empty.", banner));
                    continue;
                }
                if (!seen.Add(pickup))
                    issues.Add(Issue(ContentValidationSeverity.Warning,
                        location + " / " + pickup.DisplayName,
                        "The same pickup character is registered more than once.", banner));
                if (pickup.Rarity < 5)
                    issues.Add(Issue(ContentValidationSeverity.Error,
                        location + " / " + pickup.DisplayName,
                        "Pickup characters must be 5-star or higher.", pickup));
                if (characters != null && !Contains(characters, pickup))
                    issues.Add(Issue(ContentValidationSeverity.Error,
                        location + " / " + pickup.DisplayName,
                        "Pickup character is not registered in CharacterDatabase.", pickup));
            }
        }

        static bool Contains(CharacterDatabase database, CharacterData character)
        {
            if (database == null || database.Characters == null) return false;
            for (int i = 0; i < database.Characters.Count; i++)
                if (database.Characters[i] == character) return true;
            return false;
        }

        static bool Overlaps(ScheduleRange first, ScheduleRange second)
        {
            if (first == null && second == null) return true;
            if (first == null) return second.IsValid;
            if (second == null) return first.IsValid;
            return first.Overlaps(second);
        }

        static ContentValidationIssue Issue(ContentValidationSeverity severity,
            string location, string message, UnityEngine.Object context) =>
            new ContentValidationIssue(severity, ProviderId,
                location ?? string.Empty, message, context);
    }
}
