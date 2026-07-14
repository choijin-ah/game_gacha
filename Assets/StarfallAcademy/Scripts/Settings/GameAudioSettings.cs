using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "GameAudioSettings", menuName = "Starfall/Game Audio Settings")]
    public sealed class GameAudioSettings : ScriptableObject
    {
        [Header("Scene BGM")]
        [SerializeField] AudioClip lobbyBgm;
        [SerializeField] AudioClip formationBgm;
        [SerializeField] AudioClip gachaBgm;
        [SerializeField] AudioClip shopBgm;
        [SerializeField] AudioClip characterArchiveBgm;
        [SerializeField] AudioClip storyArchiveBgm;
        [SerializeField] AudioClip stageSelectBgm;
        [SerializeField] AudioClip weeklyBossMenuBgm;
        [SerializeField] AudioClip challengeTowerBgm;
        [SerializeField] AudioClip mailInboxBgm;
        [SerializeField, Tooltip("스테이지에 Battle BGM이 없을 때 사용하는 기본 전투곡입니다.")]
        AudioClip defaultBattleBgm;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] float musicVolume = 1f;
        [SerializeField, Range(0f, 3f)] float crossFadeSeconds = .6f;

        public float MusicVolume => Mathf.Clamp01(musicVolume);
        public float CrossFadeSeconds => Mathf.Clamp(crossFadeSeconds, 0f, 3f);
        public AudioClip MailInboxBgm => mailInboxBgm;

        public AudioClip ResolveSceneBgm(string sceneName, StageData selectedStage = null,
            AudioClip explicitOverride = null)
        {
            if (explicitOverride != null) return explicitOverride;
            switch (sceneName)
            {
                case SceneNames.Lobby: return lobbyBgm;
                case SceneNames.Formation: return formationBgm;
                case SceneNames.Gacha: return gachaBgm;
                case SceneNames.Shop: return shopBgm;
                case SceneNames.CharacterArchive: return characterArchiveBgm;
                case SceneNames.StoryArchive: return storyArchiveBgm;
                case SceneNames.StageSelect: return stageSelectBgm;
                case SceneNames.WeeklyBoss: return weeklyBossMenuBgm;
                case SceneNames.ChallengeTower: return challengeTowerBgm;
                case SceneNames.TurnBattle:
                    return selectedStage != null && selectedStage.BattleBgm != null
                        ? selectedStage.BattleBgm : defaultBattleBgm;
                default: return null;
            }
        }

        void OnValidate()
        {
            musicVolume = Mathf.Clamp01(musicVolume);
            crossFadeSeconds = Mathf.Clamp(crossFadeSeconds, 0f, 3f);
        }
    }
}
