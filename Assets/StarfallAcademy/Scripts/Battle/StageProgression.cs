using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class StageCompletionResult
    {
        public bool FirstClear { get; internal set; }
        public int PreviousStars { get; internal set; }
        public int EarnedStars { get; internal set; }
        public int BestStars { get; internal set; }
        public int RegularTurns { get; internal set; }
        public int DefeatedAllies { get; internal set; }
        public bool StarsImproved => BestStars > PreviousStars;
    }

    public static class StageProgression
    {
        const string HighestUnlockedKey = "StarfallAcademy.Battle.HighestUnlocked";
        const string ClearPrefix = "StarfallAcademy.Battle.Cleared.";
        const string StarsPrefix = "StarfallAcademy.Battle.Stars.";
        const string BestTurnsPrefix = "StarfallAcademy.Battle.BestTurns.";

        public static int HighestUnlocked => Mathf.Max(0, PlayerPrefs.GetInt(HighestUnlockedKey, 0));
        public static bool IsUnlocked(int index) => index <= HighestUnlocked;
        public static bool IsUnlocked(StageData stage, int index,
            IReadOnlyList<StageData> stages)
        {
            if (stage == null || index < 0) return false;
            int requiredAccountLevel = GetRequiredAccountLevel(stage);
            if (requiredAccountLevel > PlayerProfileService.CurrentLevel) return false;

            if (stage.Category != StageCategory.Main) return true;
            if (stages == null) return IsUnlocked(index);
            for (int i = Mathf.Min(index - 1, stages.Count - 1); i >= 0; i--)
            {
                StageData previous = stages[i];
                if (previous != null && previous.Category == StageCategory.Main)
                    return IsCleared(previous);
            }
            return true;
        }

        public static int GetRequiredAccountLevel(StageData stage)
        {
            if (stage == null) return 0;
            switch (stage.Category)
            {
                case StageCategory.Growth:
                    return PlayerProfileService.GetRequiredLevel(AccountFeature.GrowthDungeon);
                case StageCategory.Equipment:
                    return PlayerProfileService.GetRequiredLevel(AccountFeature.Equipment);
                default:
                    return 0;
            }
        }
        public static bool IsCleared(StageData stage) => stage != null && PlayerPrefs.GetInt(ClearPrefix + stage.Id, 0) == 1;
        public static int GetStars(StageData stage) => stage == null ? 0
            : Mathf.Clamp(PlayerPrefs.GetInt(StarsPrefix + stage.Id, 0), 0, 3);
        public static int GetBestTurns(StageData stage) => stage == null ? 0
            : Mathf.Max(0, PlayerPrefs.GetInt(BestTurnsPrefix + stage.Id, 0));
        public static bool IsSweepUnlocked(StageData stage) => stage != null && stage.SweepEnabled
            && IsCleared(stage) && GetStars(stage) >= 3;

        public static bool Complete(StageData stage, int index)
        {
            return Complete(stage, index, 0, int.MaxValue).FirstClear;
        }

        public static StageCompletionResult Complete(StageData stage, int index,
            int defeatedAllies, int regularTurns)
        {
            return Complete(stage, index, defeatedAllies, regularTurns, true);
        }

        internal static StageCompletionResult Complete(StageData stage, int index,
            int defeatedAllies, int regularTurns, bool save)
        {
            if (stage == null) return new StageCompletionResult();
            bool firstClear = !IsCleared(stage);
            int previousStars = GetStars(stage);
            defeatedAllies = Mathf.Max(0, defeatedAllies);
            regularTurns = Mathf.Max(0, regularTurns);
            int earnedStars = 1;
            if (defeatedAllies <= 1) earnedStars++;
            if (regularTurns > 0 && regularTurns <= stage.ThreeStarTurnLimit) earnedStars++;
            int bestStars = Mathf.Max(previousStars, earnedStars);
            PlayerPrefs.SetInt(ClearPrefix + stage.Id, 1);
            PlayerPrefs.SetInt(HighestUnlockedKey, Mathf.Max(HighestUnlocked, index + 1));
            PlayerPrefs.SetInt(StarsPrefix + stage.Id, bestStars);
            int bestTurns = GetBestTurns(stage);
            if (regularTurns > 0 && (bestTurns == 0 || regularTurns < bestTurns))
                PlayerPrefs.SetInt(BestTurnsPrefix + stage.Id, regularTurns);
            if (save) PlayerPrefs.Save();
            return new StageCompletionResult
            {
                FirstClear = firstClear,
                PreviousStars = previousStars,
                EarnedStars = earnedStars,
                BestStars = bestStars,
                RegularTurns = regularTurns,
                DefeatedAllies = defeatedAllies
            };
        }

        internal static Action CaptureRollback(StageData stage)
        {
            if (stage == null) return null;
            string clearKey = ClearPrefix + stage.Id;
            string starsKey = StarsPrefix + stage.Id;
            string bestTurnsKey = BestTurnsPrefix + stage.Id;
            bool hadHighest = PlayerPrefs.HasKey(HighestUnlockedKey);
            bool hadClear = PlayerPrefs.HasKey(clearKey);
            bool hadStars = PlayerPrefs.HasKey(starsKey);
            bool hadBestTurns = PlayerPrefs.HasKey(bestTurnsKey);
            int highest = PlayerPrefs.GetInt(HighestUnlockedKey, 0);
            int clear = PlayerPrefs.GetInt(clearKey, 0);
            int stars = PlayerPrefs.GetInt(starsKey, 0);
            int bestTurns = PlayerPrefs.GetInt(bestTurnsKey, 0);
            return () =>
            {
                RestoreInt(HighestUnlockedKey, hadHighest, highest);
                RestoreInt(clearKey, hadClear, clear);
                RestoreInt(starsKey, hadStars, stars);
                RestoreInt(bestTurnsKey, hadBestTurns, bestTurns);
            };
        }

        static void RestoreInt(string key, bool hadValue, int value)
        {
            if (hadValue) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
    }

    public static class BattleSession
    {
        public static StageData SelectedStage { get; set; }
        public static int SelectedStageIndex { get; set; }
        public static string RunId { get; set; }
        public static bool EntryStaminaPaid { get; set; }

        public static void BeginRun(StageData stage, int stageIndex, bool staminaPaid)
        {
            SelectedStage = stage;
            SelectedStageIndex = stageIndex;
            EntryStaminaPaid = staminaPaid;
            RunId = System.Guid.NewGuid().ToString("N");
        }
    }
}
