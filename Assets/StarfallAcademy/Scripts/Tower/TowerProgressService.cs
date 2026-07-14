using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class TowerPlayerDataSnapshot
    {
        public int totalStars;
        public List<TowerPlayerDataEntry> floors = new List<TowerPlayerDataEntry>();
        public List<int> claimedCumulativeRewardTierIndexes = new List<int>();
    }

    [Serializable]
    public sealed class TowerPlayerDataEntry
    {
        public int floorNumber;
        public bool cleared;
        public int stars;
    }

    public readonly struct TowerFloorSnapshot
    {
        public TowerFloorSnapshot(bool cleared, int stars)
        {
            Cleared = cleared;
            Stars = Math.Max(0, Math.Min(3, stars));
        }
        public bool Cleared { get; }
        public int Stars { get; }
    }

    public readonly struct TowerCompletionResult
    {
        public TowerCompletionResult(bool succeeded, bool firstClear, int earnedStars,
            int bestStars, int rewardsClaimed, string error)
        {
            Succeeded = succeeded;
            FirstClear = firstClear;
            EarnedStars = earnedStars;
            BestStars = bestStars;
            RewardsClaimed = rewardsClaimed;
            Error = error;
        }
        public bool Succeeded { get; }
        public bool FirstClear { get; }
        public int EarnedStars { get; }
        public int BestStars { get; }
        public int RewardsClaimed { get; }
        public string Error { get; }
    }

    public sealed class TowerProgressService
    {
        const string Prefix = "StarfallAcademy.Tower.v1.";
        readonly IMetaStorage storage;
        readonly RewardPackageService rewards;

        public TowerProgressService(IMetaStorage storage, PlayerProfileService profile = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            rewards = new RewardPackageService(storage, profile ?? new PlayerProfileService(storage));
        }

        public static TowerProgressService Default { get; } = new TowerProgressService(
            PlayerPrefsMetaStorage.Shared, PlayerProfileService.Default);

        public TowerFloorSnapshot GetSnapshot(TowerFloorData floor)
        {
            if (floor == null) return default;
            return new TowerFloorSnapshot(storage.GetInt(ClearKey(floor.FloorNumber), 0) == 1,
                storage.GetInt(StarsKey(floor.FloorNumber), 0));
        }

        public bool IsUnlocked(TowerFloorData floor, TowerDatabase database)
        {
            if (floor == null || database == null) return false;
            if (floor.FloorNumber <= 1) return true;
            TowerFloorData previous = database.FindFloor(floor.FloorNumber - 1);
            return previous != null && GetSnapshot(previous).Cleared;
        }

        public int GetTotalStars(TowerDatabase database)
        {
            if (database == null) return 0;
            int total = 0;
            for (int i = 0; i < database.Floors.Count; i++)
                total += GetSnapshot(database.Floors[i]).Stars;
            return total;
        }

        public bool TryBeginRun(TowerFloorData floor, TowerDatabase database,
            out TowerRunContext context, out string failureReason)
        {
            context = null;
            if (floor == null || floor.BaseStage == null)
            {
                failureReason = "탑 층 전투 데이터가 올바르지 않습니다.";
                return false;
            }
            if (!IsUnlocked(floor, database))
            {
                failureReason = "이전 층을 먼저 클리어해야 합니다.";
                return false;
            }
            context = new TowerRunContext(this, database, floor, Guid.NewGuid().ToString("N"));
            failureReason = string.Empty;
            return true;
        }

        internal TowerCompletionResult Complete(TowerDatabase database, TowerFloorData floor,
            BattleResult battleResult)
        {
            if (database == null || floor == null || battleResult == null
                || !battleResult.IsSuccessful || !battleResult.EnemiesDefeated)
                return new TowerCompletionResult(true, false, 0, GetSnapshot(floor).Stars, 0, string.Empty);

            TowerFloorSnapshot before = GetSnapshot(floor);
            int earned = floor.EvaluateStars(battleResult);
            int best = Math.Max(before.Stars, earned);
            int claimed = 0;
            bool hasFirstClearReward = !before.Cleared && floor.FirstClearReward != null
                && !floor.FirstClearReward.IsEmpty;
            if (hasFirstClearReward)
            {
                RewardGrantResult grant = rewards.Grant("tower:floor:" + floor.FloorNumber
                    + ":first", floor.FirstClearReward,
                    () => StageFloor(floor.FloorNumber, best),
                    () => RestoreFloor(floor.FloorNumber, before));
                if (!grant.Succeeded && !grant.AlreadyProcessed)
                    return new TowerCompletionResult(false, false, earned, before.Stars, 0,
                        "최초 클리어 보상을 저장하지 못했습니다.");
                if (grant.AlreadyProcessed && !GetSnapshot(floor).Cleared)
                {
                    try
                    {
                        StageFloor(floor.FloorNumber, best);
                        storage.Save();
                    }
                    catch (Exception exception)
                    {
                        RestoreFloor(floor.FloorNumber, before);
                        try { storage.Save(); } catch { }
                        return new TowerCompletionResult(false, false, earned, before.Stars, 0,
                            exception.Message);
                    }
                }
                claimed++;
            }
            else
            {
                try
                {
                    StageFloor(floor.FloorNumber, best);
                    storage.Save();
                }
                catch (Exception exception)
                {
                    RestoreFloor(floor.FloorNumber, before);
                    try { storage.Save(); } catch { }
                    return new TowerCompletionResult(false, false, earned, before.Stars, 0,
                        exception.Message);
                }
            }
            claimed += ClaimCumulativeRewards(database);
            return new TowerCompletionResult(true, !before.Cleared, earned, best, claimed, string.Empty);
        }

        public int ClaimCumulativeRewards(TowerDatabase database)
        {
            if (database == null) return 0;
            int totalStars = GetTotalStars(database);
            int claimed = 0;
            for (int i = 0; i < database.CumulativeStarRewards.Count; i++)
            {
                TowerStarRewardTier tier = database.CumulativeStarRewards[i];
                if (tier == null || tier.Reward == null || tier.Reward.IsEmpty
                    || totalStars < tier.RequiredTotalStars
                    || storage.GetInt(StarRewardKey(i), 0) == 1) continue;
                string markerKey = StarRewardKey(i);
                bool hadMarker = storage.HasKey(markerKey);
                int previousMarker = storage.GetInt(markerKey, 0);
                RewardGrantResult grant = rewards.Grant("tower:stars:tier:" + i, tier.Reward,
                    () => storage.SetInt(markerKey, 1),
                    () => RestoreInt(markerKey, hadMarker, previousMarker));
                if (grant.Succeeded)
                {
                    claimed++;
                    continue;
                }
                if (!grant.AlreadyProcessed) continue;

                // Reconcile saves produced by older builds where reward and marker were separate.
                try
                {
                    storage.SetInt(markerKey, 1);
                    storage.Save();
                    claimed++;
                }
                catch
                {
                    RestoreInt(markerKey, hadMarker, previousMarker);
                    try { storage.Save(); } catch { }
                }
            }
            return claimed;
        }

        public TowerPlayerDataSnapshot CapturePlayerData(TowerDatabase database)
        {
            var result = new TowerPlayerDataSnapshot();
            if (database == null) return result;
            for (int floorIndex = 0; floorIndex < database.Floors.Count; floorIndex++)
            {
                TowerFloorData floor = database.Floors[floorIndex];
                if (floor == null) continue;
                TowerFloorSnapshot snapshot = GetSnapshot(floor);
                result.floors.Add(new TowerPlayerDataEntry
                {
                    floorNumber = floor.FloorNumber,
                    cleared = snapshot.Cleared,
                    stars = snapshot.Stars
                });
                result.totalStars += snapshot.Stars;
            }
            for (int tierIndex = 0; tierIndex < database.CumulativeStarRewards.Count; tierIndex++)
            {
                if (storage.GetInt(StarRewardKey(tierIndex), 0) == 1)
                    result.claimedCumulativeRewardTierIndexes.Add(tierIndex);
            }
            return result;
        }

        public string ExportPlayerDataJson(TowerDatabase database, bool prettyPrint = true) =>
            JsonUtility.ToJson(CapturePlayerData(database), prettyPrint);

        public void ResetPlayerData(TowerDatabase database)
        {
            if (database == null) return;
            List<string> keys = GetPlayerDataKeys(database);
            ApplyStorageChanges(keys, () =>
            {
                for (int i = 0; i < keys.Count; i++) storage.DeleteKey(keys[i]);
            });
        }

        public void RestorePlayerData(TowerDatabase database, TowerPlayerDataSnapshot snapshot)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.floors == null)
                throw new ArgumentException("탑 floors가 없습니다.", nameof(snapshot));

            var resolved = new List<ResolvedFloor>();
            var floorNumbers = new HashSet<int>();
            for (int i = 0; i < snapshot.floors.Count; i++)
            {
                TowerPlayerDataEntry entry = snapshot.floors[i];
                if (entry == null) throw new ArgumentException("빈 탑 층 항목이 있습니다.", nameof(snapshot));
                TowerFloorData floor = database.FindFloor(entry.floorNumber);
                if (floor == null)
                    throw new ArgumentException("존재하지 않는 탑 층입니다: " + entry.floorNumber,
                        nameof(snapshot));
                if (!floorNumbers.Add(entry.floorNumber))
                    throw new ArgumentException("중복된 탑 층 저장 항목입니다: " + entry.floorNumber,
                        nameof(snapshot));
                if (entry.stars < 0 || entry.stars > 3 || !entry.cleared && entry.stars > 0)
                    throw new ArgumentException("탑 별 기록이 올바르지 않습니다: " + entry.floorNumber,
                        nameof(snapshot));
                resolved.Add(new ResolvedFloor(floor, entry));
            }

            var claimedTiers = new HashSet<int>();
            if (snapshot.claimedCumulativeRewardTierIndexes != null)
            {
                for (int i = 0; i < snapshot.claimedCumulativeRewardTierIndexes.Count; i++)
                {
                    int tierIndex = snapshot.claimedCumulativeRewardTierIndexes[i];
                    if (tierIndex < 0 || tierIndex >= database.CumulativeStarRewards.Count)
                        throw new ArgumentException("누적 별 보상 인덱스가 범위를 벗어났습니다.",
                            nameof(snapshot));
                    claimedTiers.Add(tierIndex);
                }
            }

            List<string> keys = GetPlayerDataKeys(database);
            ApplyStorageChanges(keys, () =>
            {
                for (int i = 0; i < keys.Count; i++) storage.DeleteKey(keys[i]);
                for (int i = 0; i < resolved.Count; i++)
                {
                    ResolvedFloor item = resolved[i];
                    if (item.Entry.cleared)
                        storage.SetInt(ClearKey(item.Floor.FloorNumber), 1);
                    if (item.Entry.stars > 0)
                        storage.SetInt(StarsKey(item.Floor.FloorNumber), item.Entry.stars);
                }
                foreach (int tierIndex in claimedTiers)
                    storage.SetInt(StarRewardKey(tierIndex), 1);
            });
        }

        public bool TryImportPlayerDataJson(TowerDatabase database, string json, out string error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    throw new ArgumentException("가져올 JSON이 비어 있습니다.", nameof(json));
                TowerPlayerDataSnapshot snapshot = JsonUtility.FromJson<TowerPlayerDataSnapshot>(json);
                RestorePlayerData(database, snapshot);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        void RestoreFloor(int floorNumber, TowerFloorSnapshot snapshot)
        {
            if (snapshot.Cleared) storage.SetInt(ClearKey(floorNumber), 1);
            else storage.DeleteKey(ClearKey(floorNumber));
            if (snapshot.Stars > 0) storage.SetInt(StarsKey(floorNumber), snapshot.Stars);
            else storage.DeleteKey(StarsKey(floorNumber));
        }

        void RestoreInt(string key, bool hadValue, int value)
        {
            if (hadValue) storage.SetInt(key, value);
            else storage.DeleteKey(key);
        }

        void StageFloor(int floorNumber, int stars)
        {
            storage.SetInt(ClearKey(floorNumber), 1);
            storage.SetInt(StarsKey(floorNumber), Math.Max(1, Math.Min(3, stars)));
        }

        List<string> GetPlayerDataKeys(TowerDatabase database)
        {
            var keys = new List<string>();
            for (int i = 0; i < database.Floors.Count; i++)
            {
                TowerFloorData floor = database.Floors[i];
                if (floor == null) continue;
                keys.Add(ClearKey(floor.FloorNumber));
                keys.Add(StarsKey(floor.FloorNumber));
            }
            for (int i = 0; i < database.CumulativeStarRewards.Count; i++)
                keys.Add(StarRewardKey(i));
            return keys;
        }

        void ApplyStorageChanges(IReadOnlyList<string> affectedKeys, Action changes)
        {
            var rollback = new List<StoredInt>(affectedKeys.Count);
            for (int i = 0; i < affectedKeys.Count; i++)
            {
                string key = affectedKeys[i];
                rollback.Add(new StoredInt(key, storage.HasKey(key), storage.GetInt(key, 0)));
            }
            try
            {
                changes();
                storage.Save();
            }
            catch
            {
                for (int i = 0; i < rollback.Count; i++)
                {
                    StoredInt value = rollback[i];
                    if (value.HadValue) storage.SetInt(value.Key, value.Value);
                    else storage.DeleteKey(value.Key);
                }
                try { storage.Save(); } catch { }
                throw;
            }
        }

        readonly struct StoredInt
        {
            public StoredInt(string key, bool hadValue, int value)
            {
                Key = key;
                HadValue = hadValue;
                Value = value;
            }
            public string Key { get; }
            public bool HadValue { get; }
            public int Value { get; }
        }

        readonly struct ResolvedFloor
        {
            public ResolvedFloor(TowerFloorData floor, TowerPlayerDataEntry entry)
            {
                Floor = floor;
                Entry = entry;
            }
            public TowerFloorData Floor { get; }
            public TowerPlayerDataEntry Entry { get; }
        }

        static string ClearKey(int floor) => Prefix + "floor." + Math.Max(1, floor) + ".clear";
        static string StarsKey(int floor) => Prefix + "floor." + Math.Max(1, floor) + ".stars";
        static string StarRewardKey(int tier) => Prefix + "star_reward." + Math.Max(0, tier);
    }
}
