using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum AutoBattlePreset
    {
        Balanced,
        OffenseFirst,
        BreakFirst,
        SurvivalFirst,
        PreserveUltimate
    }

    /// <summary>Stores only the player's last auto-battle strategy selection.</summary>
    public static class AutoBattleSettings
    {
        const string PresetKey = "StarfallAcademy.Battle.AutoPreset";

        public const AutoBattlePreset DefaultPreset = AutoBattlePreset.Balanced;

        public static AutoBattlePreset CurrentPreset
        {
            get => Sanitize(PlayerPrefs.GetInt(PresetKey, (int)DefaultPreset));
            set
            {
                PlayerPrefs.SetInt(PresetKey, (int)Sanitize((int)value));
                PlayerPrefs.Save();
            }
        }

        public static AutoBattlePreset CyclePreset()
        {
            CurrentPreset = NextPreset(CurrentPreset);
            return CurrentPreset;
        }

        public static AutoBattlePreset NextPreset(AutoBattlePreset preset)
        {
            int next = ((int)Sanitize((int)preset) + 1) % 5;
            return (AutoBattlePreset)next;
        }

        public static string GetDisplayName(AutoBattlePreset preset)
        {
            return Sanitize((int)preset) switch
            {
                AutoBattlePreset.OffenseFirst => "공격 우선",
                AutoBattlePreset.BreakFirst => "격파 우선",
                AutoBattlePreset.SurvivalFirst => "생존 우선",
                AutoBattlePreset.PreserveUltimate => "필살기 보존",
                _ => "균형"
            };
        }

        static AutoBattlePreset Sanitize(int value)
        {
            return (AutoBattlePreset)Mathf.Clamp(value,
                (int)AutoBattlePreset.Balanced, (int)AutoBattlePreset.PreserveUltimate);
        }
    }
}
