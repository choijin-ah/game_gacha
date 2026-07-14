using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class WeeklyBossPlayerDataSnapshot
    {
        public string weekId;
        public List<WeeklyBossPlayerDataEntry> entries = new List<WeeklyBossPlayerDataEntry>();
    }

    [Serializable]
    public sealed class WeeklyBossPlayerDataEntry
    {
        public string bossId;
        public string difficultyId;
        public int attemptsUsed;
        public int maximumAttempts;
        public int bestScore;
        public List<int> claimedRewardTierIndexes = new List<int>();
    }

    public readonly struct WeeklyBossSnapshot
    {
        public WeeklyBossSnapshot(string weekId, int attemptsUsed, int maximumAttempts,
            int bestScore)
        {
            WeekId = weekId;
            AttemptsUsed = attemptsUsed;
            MaximumAttempts = maximumAttempts;
            BestScore = bestScore;
        }

        public string WeekId { get; }
        public int AttemptsUsed { get; }
        public int MaximumAttempts { get; }
        public int AttemptsRemaining => Math.Max(0, MaximumAttempts - AttemptsUsed);
        public int BestScore { get; }
    }

    public sealed class WeeklyBossService
    {
        const string Prefix = "StarfallAcademy.WeeklyBoss.v1.";
        readonly IMetaStorage storage;
        readonly IUtcClock clock;
        readonly RewardPackageService rewards;

        public WeeklyBossService(IMetaStorage storage, IUtcClock clock,
            PlayerProfileService profile = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            rewards = new RewardPackageService(storage, profile ?? new PlayerProfileService(storage));
        }

        public static WeeklyBossService Default { get; } = new WeeklyBossService(
            PlayerPrefsMetaStorage.Shared, ContentUtcClock.Shared, PlayerProfileService.Default);

        public WeeklyBossSnapshot GetSnapshot(WeeklyBossDefinition boss,
            WeeklyBossDifficulty difficulty)
        {
            return GetSnapshot(boss, difficulty, GetWeekId(clock.UtcNow));
        }

        internal WeeklyBossSnapshot GetSnapshot(WeeklyBossDefinition boss,
            WeeklyBossDifficulty difficulty, string weekId)
        {
            if (boss == null || difficulty == null) return default;
            string week = string.IsNullOrWhiteSpace(weekId) ? GetWeekId(clock.UtcNow) : weekId;
            int used = Math.Max(0, storage.GetInt(Key(boss, difficulty, week, "attempts"), 0));
            int best = Math.Max(0, storage.GetInt(Key(boss, difficulty, week, "best"), 0));
            return new WeeklyBossSnapshot(week, used, difficulty.WeeklyAttempts, best);
        }

        public bool TryBeginRun(WeeklyBossDefinition boss, WeeklyBossDifficulty difficulty,
            out WeeklyBossRunContext context, out string failureReason)
        {
            context = null;
            if (boss == null || difficulty == null || boss.BaseStage == null)
            {
                failureReason = "주간 보스 전투 데이터가 올바르지 않습니다.";
                return false;
            }
            if (!boss.IsAvailable(clock.UtcNow))
            {
                failureReason = "현재 이용할 수 없는 주간 보스입니다.";
                return false;
            }
            WeeklyBossSnapshot snapshot = GetSnapshot(boss, difficulty);
            if (snapshot.AttemptsRemaining <= 0)
            {
                failureReason = "이번 주 도전 횟수를 모두 사용했습니다.";
                return false;
            }
            string attemptsKey = Key(boss, difficulty, snapshot.WeekId, "attempts");
            int previous = snapshot.AttemptsUsed;
            try
            {
                storage.SetInt(attemptsKey, previous + 1);
                storage.Save();
            }
            catch
            {
                storage.SetInt(attemptsKey, previous);
                failureReason = "도전 횟수를 저장하지 못했습니다.";
                return false;
            }
            context = new WeeklyBossRunContext(this, boss, difficulty, snapshot.WeekId,
                Guid.NewGuid().ToString("N"));
            failureReason = string.Empty;
            return true;
        }

        internal bool RecordScore(WeeklyBossDefinition boss, WeeklyBossDifficulty difficulty,
            string weekId, int score)
        {
            string key = Key(boss, difficulty, weekId, "best");
            int previous = Math.Max(0, storage.GetInt(key, 0));
            if (score <= previous) return true;
            try
            {
                storage.SetInt(key, score);
                storage.Save();
                return true;
            }
            catch
            {
                storage.SetInt(key, previous);
                return false;
            }
        }

        internal void RefundAttempt(WeeklyBossDefinition boss, WeeklyBossDifficulty difficulty,
            string weekId)
        {
            string key = Key(boss, difficulty, weekId, "attempts");
            storage.SetInt(key, Math.Max(0, storage.GetInt(key, 0) - 1));
            storage.Save();
        }

        public bool IsTierClaimed(WeeklyBossDefinition boss, WeeklyBossDifficulty difficulty,
            int tierIndex, string weekId = null)
        {
            if (boss == null || difficulty == null || tierIndex < 0) return false;
            string week = string.IsNullOrEmpty(weekId) ? GetWeekId(clock.UtcNow) : weekId;
            return storage.GetInt(Key(boss, difficulty, week, "tier." + tierIndex), 0) == 1;
        }

        public int ClaimEligibleRewards(WeeklyBossDefinition boss, WeeklyBossDifficulty difficulty,
            string weekId, int score)
        {
            int claimed = 0;
            if (boss == null || difficulty == null || string.IsNullOrWhiteSpace(weekId))
                return claimed;
            for (int i = 0; i < boss.RewardTiers.Count; i++)
            {
                WeeklyBossScoreRewardTier tier = boss.RewardTiers[i];
                if (tier == null || tier.Reward == null || tier.Reward.IsEmpty
                    || score < tier.RequiredScore
                    || IsTierClaimed(boss, difficulty, i, weekId)) continue;
                string transaction = "weekly:" + boss.Id + ":" + difficulty.Id + ":"
                    + weekId + ":tier:" + i;
                string markerKey = Key(boss, difficulty, weekId, "tier." + i);
                bool hadMarker = storage.HasKey(markerKey);
                int previousMarker = storage.GetInt(markerKey, 0);
                RewardGrantResult grant = rewards.Grant(transaction, tier.Reward,
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

        public WeeklyBossPlayerDataSnapshot CapturePlayerData(WeeklyBossDatabase database)
        {
            var result = new WeeklyBossPlayerDataSnapshot
            {
                weekId = GetWeekId(clock.UtcNow)
            };
            if (database == null) return result;

            for (int bossIndex = 0; bossIndex < database.Bosses.Count; bossIndex++)
            {
                WeeklyBossDefinition boss = database.Bosses[bossIndex];
                if (boss == null) continue;
                for (int difficultyIndex = 0; difficultyIndex < boss.Difficulties.Count;
                    difficultyIndex++)
                {
                    WeeklyBossDifficulty difficulty = boss.Difficulties[difficultyIndex];
                    if (difficulty == null) continue;
                    var entry = new WeeklyBossPlayerDataEntry
                    {
                        bossId = boss.Id,
                        difficultyId = difficulty.Id,
                        attemptsUsed = Math.Max(0, storage.GetInt(
                            Key(boss, difficulty, result.weekId, "attempts"), 0)),
                        maximumAttempts = difficulty.WeeklyAttempts,
                        bestScore = Math.Max(0, storage.GetInt(
                            Key(boss, difficulty, result.weekId, "best"), 0))
                    };
                    for (int tierIndex = 0; tierIndex < boss.RewardTiers.Count; tierIndex++)
                    {
                        if (IsTierClaimed(boss, difficulty, tierIndex, result.weekId))
                            entry.claimedRewardTierIndexes.Add(tierIndex);
                    }
                    result.entries.Add(entry);
                }
            }
            return result;
        }

        public string ExportPlayerDataJson(WeeklyBossDatabase database, bool prettyPrint = true) =>
            JsonUtility.ToJson(CapturePlayerData(database), prettyPrint);

        public void ResetPlayerData(WeeklyBossDatabase database)
        {
            if (database == null) return;
            string weekId = GetWeekId(clock.UtcNow);
            List<string> keys = GetPlayerDataKeys(database, weekId);
            ApplyStorageChanges(keys, () =>
            {
                for (int i = 0; i < keys.Count; i++) storage.DeleteKey(keys[i]);
            });
        }

        public void RestorePlayerData(WeeklyBossDatabase database,
            WeeklyBossPlayerDataSnapshot snapshot)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!DateTime.TryParseExact(snapshot.weekId, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime weekDate)
                || GetWeekId(weekDate) != snapshot.weekId)
                throw new ArgumentException("주간 보스 weekId는 월요일 UTC의 yyyyMMdd 형식이어야 합니다.",
                    nameof(snapshot));
            if (snapshot.entries == null)
                throw new ArgumentException("주간 보스 entries가 없습니다.", nameof(snapshot));

            var resolved = new List<ResolvedPlayerDataEntry>();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < snapshot.entries.Count; i++)
            {
                WeeklyBossPlayerDataEntry entry = snapshot.entries[i];
                if (entry == null) throw new ArgumentException("빈 주간 보스 항목이 있습니다.", nameof(snapshot));
                WeeklyBossDefinition boss = FindBoss(database, entry.bossId);
                WeeklyBossDifficulty difficulty = FindDifficulty(boss, entry.difficultyId);
                if (boss == null || difficulty == null)
                    throw new ArgumentException("존재하지 않는 보스 또는 난이도입니다: "
                        + entry.bossId + "/" + entry.difficultyId, nameof(snapshot));
                string identity = Safe(boss.Id) + "/" + Safe(difficulty.Id);
                if (!identities.Add(identity))
                    throw new ArgumentException("중복된 주간 보스 저장 항목입니다: " + identity,
                        nameof(snapshot));
                if (entry.attemptsUsed < 0 || entry.attemptsUsed > difficulty.WeeklyAttempts)
                    throw new ArgumentException("도전 횟수가 범위를 벗어났습니다: " + identity,
                        nameof(snapshot));
                if (entry.bestScore < 0)
                    throw new ArgumentException("최고 점수는 음수일 수 없습니다: " + identity,
                        nameof(snapshot));
                var claimed = new HashSet<int>();
                if (entry.claimedRewardTierIndexes != null)
                {
                    for (int tier = 0; tier < entry.claimedRewardTierIndexes.Count; tier++)
                    {
                        int tierIndex = entry.claimedRewardTierIndexes[tier];
                        if (tierIndex < 0 || tierIndex >= boss.RewardTiers.Count)
                            throw new ArgumentException("보상 단계 인덱스가 범위를 벗어났습니다: "
                                + identity, nameof(snapshot));
                        claimed.Add(tierIndex);
                    }
                }
                resolved.Add(new ResolvedPlayerDataEntry(boss, difficulty, entry, claimed));
            }

            List<string> keys = GetPlayerDataKeys(database, snapshot.weekId);
            ApplyStorageChanges(keys, () =>
            {
                for (int i = 0; i < keys.Count; i++) storage.DeleteKey(keys[i]);
                for (int i = 0; i < resolved.Count; i++)
                {
                    ResolvedPlayerDataEntry item = resolved[i];
                    if (item.Entry.attemptsUsed > 0)
                        storage.SetInt(Key(item.Boss, item.Difficulty, snapshot.weekId, "attempts"),
                            item.Entry.attemptsUsed);
                    if (item.Entry.bestScore > 0)
                        storage.SetInt(Key(item.Boss, item.Difficulty, snapshot.weekId, "best"),
                            item.Entry.bestScore);
                    foreach (int tierIndex in item.ClaimedTiers)
                        storage.SetInt(Key(item.Boss, item.Difficulty, snapshot.weekId,
                            "tier." + tierIndex), 1);
                }
            });
        }

        public bool TryImportPlayerDataJson(WeeklyBossDatabase database, string json,
            out string error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    throw new ArgumentException("가져올 JSON이 비어 있습니다.", nameof(json));
                WeeklyBossPlayerDataSnapshot snapshot =
                    JsonUtility.FromJson<WeeklyBossPlayerDataSnapshot>(json);
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

        public static string GetWeekId(DateTime utc)
        {
            DateTime normalized = ScheduleRange.NormalizeUtc(utc).Date;
            int daysFromMonday = ((int)normalized.DayOfWeek + 6) % 7;
            DateTime monday = normalized.AddDays(-daysFromMonday);
            return monday.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        static string Key(WeeklyBossDefinition boss, WeeklyBossDifficulty difficulty,
            string week, string suffix) => Prefix + Safe(boss?.Id) + "." + Safe(difficulty?.Id)
                + "." + Safe(week) + "." + suffix;

        List<string> GetPlayerDataKeys(WeeklyBossDatabase database, string weekId)
        {
            var keys = new List<string>();
            for (int bossIndex = 0; bossIndex < database.Bosses.Count; bossIndex++)
            {
                WeeklyBossDefinition boss = database.Bosses[bossIndex];
                if (boss == null) continue;
                for (int difficultyIndex = 0; difficultyIndex < boss.Difficulties.Count;
                    difficultyIndex++)
                {
                    WeeklyBossDifficulty difficulty = boss.Difficulties[difficultyIndex];
                    if (difficulty == null) continue;
                    keys.Add(Key(boss, difficulty, weekId, "attempts"));
                    keys.Add(Key(boss, difficulty, weekId, "best"));
                    for (int tierIndex = 0; tierIndex < boss.RewardTiers.Count; tierIndex++)
                        keys.Add(Key(boss, difficulty, weekId, "tier." + tierIndex));
                }
            }
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

        void RestoreInt(string key, bool hadValue, int value)
        {
            if (hadValue) storage.SetInt(key, value);
            else storage.DeleteKey(key);
        }

        static WeeklyBossDefinition FindBoss(WeeklyBossDatabase database, string bossId)
        {
            for (int i = 0; i < database.Bosses.Count; i++)
            {
                WeeklyBossDefinition boss = database.Bosses[i];
                if (boss != null && string.Equals(boss.Id, bossId,
                    StringComparison.OrdinalIgnoreCase)) return boss;
            }
            return null;
        }

        static WeeklyBossDifficulty FindDifficulty(WeeklyBossDefinition boss, string difficultyId)
        {
            if (boss == null) return null;
            for (int i = 0; i < boss.Difficulties.Count; i++)
            {
                WeeklyBossDifficulty difficulty = boss.Difficulties[i];
                if (difficulty != null && string.Equals(difficulty.Id, difficultyId,
                    StringComparison.OrdinalIgnoreCase)) return difficulty;
            }
            return null;
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

        readonly struct ResolvedPlayerDataEntry
        {
            public ResolvedPlayerDataEntry(WeeklyBossDefinition boss,
                WeeklyBossDifficulty difficulty, WeeklyBossPlayerDataEntry entry,
                HashSet<int> claimedTiers)
            {
                Boss = boss;
                Difficulty = difficulty;
                Entry = entry;
                ClaimedTiers = claimedTiers;
            }
            public WeeklyBossDefinition Boss { get; }
            public WeeklyBossDifficulty Difficulty { get; }
            public WeeklyBossPlayerDataEntry Entry { get; }
            public HashSet<int> ClaimedTiers { get; }
        }

        static string Safe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            var characters = new List<char>();
            foreach (char c in value.Trim().ToLowerInvariant())
                characters.Add(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return new string(characters.ToArray());
        }
    }
}
