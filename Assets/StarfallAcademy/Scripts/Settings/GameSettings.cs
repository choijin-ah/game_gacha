using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum GameTextSpeed { Slow, Normal, Fast }

    public static class GameSettings
    {
        const string MasterVolumeKey = "StarfallAcademy.Settings.MasterVolume";
        const string MusicVolumeKey = "StarfallAcademy.Settings.MusicVolume";
        const string SfxVolumeKey = "StarfallAcademy.Settings.SfxVolume";
        const string GraphicsQualityKey = "StarfallAcademy.Settings.GraphicsQuality";
        const string TextSpeedKey = "StarfallAcademy.Settings.TextSpeed";
        const string AutoBattleKey = "StarfallAcademy.Settings.AutoBattle";

        public static event Action Changed;

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, .8f));
            set { PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value)); Apply(); }
        }

        public static float MusicVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, .7f));
            set { PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value)); SaveAndNotify(); }
        }

        public static float SfxVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, .8f));
            set { PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value)); SaveAndNotify(); }
        }

        public static int GraphicsQuality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(GraphicsQualityKey, 2), 0, 2);
            set
            {
                int quality = Mathf.Clamp(value, 0, 2);
                PlayerPrefs.SetInt(GraphicsQualityKey, quality);
                if (QualitySettings.names.Length > 0)
                {
                    int mapped = Mathf.RoundToInt(quality / 2f * (QualitySettings.names.Length - 1));
                    QualitySettings.SetQualityLevel(mapped, true);
                }
                SaveAndNotify();
            }
        }

        public static GameTextSpeed TextSpeed
        {
            get => (GameTextSpeed)Mathf.Clamp(PlayerPrefs.GetInt(TextSpeedKey, 1), 0, 2);
            set { PlayerPrefs.SetInt(TextSpeedKey, Mathf.Clamp((int)value, 0, 2)); SaveAndNotify(); }
        }

        public static bool AutoBattle
        {
            get => PlayerPrefs.GetInt(AutoBattleKey, 0) == 1;
            set { PlayerPrefs.SetInt(AutoBattleKey, value ? 1 : 0); SaveAndNotify(); }
        }

        public static float TextCharactersPerSecond => TextSpeed == GameTextSpeed.Slow ? 22f
            : TextSpeed == GameTextSpeed.Fast ? 75f : 40f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() => Apply();

        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            int quality = GraphicsQuality;
            if (QualitySettings.names.Length > 0)
            {
                int mapped = Mathf.RoundToInt(quality / 2f * (QualitySettings.names.Length - 1));
                QualitySettings.SetQualityLevel(mapped, true);
            }
            SaveAndNotify();
        }

        static void SaveAndNotify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
