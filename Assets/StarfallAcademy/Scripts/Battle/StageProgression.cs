using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public static class StageProgression
    {
        const string HighestUnlockedKey = "StarfallAcademy.Battle.HighestUnlocked";
        const string ClearPrefix = "StarfallAcademy.Battle.Cleared.";

        public static int HighestUnlocked => Mathf.Max(0, PlayerPrefs.GetInt(HighestUnlockedKey, 0));
        public static bool IsUnlocked(int index) => index <= HighestUnlocked;
        public static bool IsCleared(StageData stage) => stage != null && PlayerPrefs.GetInt(ClearPrefix + stage.Id, 0) == 1;

        public static bool Complete(StageData stage, int index)
        {
            if (stage == null) return false;
            bool firstClear = !IsCleared(stage);
            PlayerPrefs.SetInt(ClearPrefix + stage.Id, 1);
            PlayerPrefs.SetInt(HighestUnlockedKey, Mathf.Max(HighestUnlocked, index + 1));
            PlayerPrefs.Save();
            return firstClear;
        }
    }

    public static class BattleSession
    {
        public static StageData SelectedStage { get; set; }
        public static int SelectedStageIndex { get; set; }
    }
}
