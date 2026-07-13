using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby
{
    public enum GameAudioChannelType
    {
        Music,
        Sfx
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudioChannel : MonoBehaviour
    {
        [SerializeField] GameAudioChannelType channel = GameAudioChannelType.Sfx;
        [SerializeField, Range(0f, 1f)] float baseVolume = 1f;

        AudioSource source;

        void OnEnable()
        {
            source = GetComponent<AudioSource>();
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void OnValidate()
        {
            baseVolume = Mathf.Clamp01(baseVolume);
            if (isActiveAndEnabled) Apply();
        }

        void Apply()
        {
            if (source == null) source = GetComponent<AudioSource>();
            float channelVolume = channel == GameAudioChannelType.Music
                ? GameSettings.MusicVolume : GameSettings.SfxVolume;
            source.volume = baseVolume * channelVolume;
        }
    }

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
        [SerializeField, Tooltip("스테이지에 Battle BGM이 없을 때 사용하는 기본 전투곡입니다.")]
        AudioClip defaultBattleBgm;

        [Header("Playback")]
        [SerializeField, Range(0f, 1f)] float musicVolume = 1f;
        [SerializeField, Range(0f, 3f)] float crossFadeSeconds = .6f;

        public float MusicVolume => Mathf.Clamp01(musicVolume);
        public float CrossFadeSeconds => Mathf.Clamp(crossFadeSeconds, 0f, 3f);

        public AudioClip ResolveSceneBgm(string sceneName, StageData selectedStage = null)
        {
            switch (sceneName)
            {
                case SceneNames.Lobby: return lobbyBgm;
                case SceneNames.Formation: return formationBgm;
                case SceneNames.Gacha: return gachaBgm;
                case SceneNames.Shop: return shopBgm;
                case SceneNames.CharacterArchive: return characterArchiveBgm;
                case SceneNames.StoryArchive: return storyArchiveBgm;
                case SceneNames.StageSelect: return stageSelectBgm;
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

    /// <summary>씬 전환과 스테이지 선택에 맞춰 하나의 BGM을 유지하고 교차 재생합니다.</summary>
    public sealed class GameAudioDirector : MonoBehaviour
    {
        const string SettingsResourcePath = "Data/GameAudioSettings";
        static GameAudioDirector instance;

        GameAudioSettings settings;
        AudioSource activeSource;
        AudioSource inactiveSource;
        Coroutine fadeRoutine;
        bool fading;
        bool sceneMusicSuppressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() => EnsureInstance();

        static void EnsureInstance()
        {
            if (instance != null) return;
            var root = new GameObject("[Game Audio Director]");
            instance = root.AddComponent<GameAudioDirector>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            settings = Resources.Load<GameAudioSettings>(SettingsResourcePath);
            activeSource = CreateMusicSource("BGM A");
            inactiveSource = CreateMusicSource("BGM B");
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameSettings.Changed += ApplyVolume;
        }

        void Start() => RefreshInternal(null);

        void OnDestroy()
        {
            if (instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.Changed -= ApplyVolume;
            instance = null;
        }

        AudioSource CreateMusicSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshInternal(null);

        public static void RefreshForCurrentScene(StageData selectedStage = null)
        {
            EnsureInstance();
            instance.RefreshInternal(selectedStage);
        }

        public static void SetSceneMusicSuppressed(bool suppressed)
        {
            if (instance == null)
            {
                if (!suppressed) return;
                EnsureInstance();
            }
            if (instance.sceneMusicSuppressed == suppressed) return;
            instance.sceneMusicSuppressed = suppressed;
            instance.RefreshInternal(null);
        }

        void RefreshInternal(StageData selectedStage)
        {
            if (settings == null) settings = Resources.Load<GameAudioSettings>(SettingsResourcePath);
            AudioClip target = null;
            if (!sceneMusicSuppressed && settings != null)
            {
                StageData stage = selectedStage != null ? selectedStage : BattleSession.SelectedStage;
                target = settings.ResolveSceneBgm(SceneManager.GetActiveScene().name, stage);
            }
            TransitionTo(target);
        }

        void TransitionTo(AudioClip target)
        {
            if (activeSource != null && activeSource.clip == target && activeSource.isPlaying)
            {
                ApplyVolume();
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
                fading = false;
            }

            inactiveSource.Stop();
            inactiveSource.clip = target;
            inactiveSource.volume = 0f;
            if (target != null) inactiveSource.Play();

            float duration = settings != null ? settings.CrossFadeSeconds : 0f;
            if (duration <= .01f)
            {
                activeSource.Stop();
                activeSource.clip = null;
                SwapSources();
                ApplyVolume();
                return;
            }
            fadeRoutine = StartCoroutine(CrossFade(duration));
        }

        IEnumerator CrossFade(float duration)
        {
            fading = true;
            AudioSource from = activeSource;
            AudioSource to = inactiveSource;
            float startVolume = from != null ? from.volume : 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float targetVolume = ResolveVolume();
                if (from != null) from.volume = Mathf.Lerp(startVolume, 0f, progress);
                if (to != null) to.volume = Mathf.Lerp(0f, targetVolume, progress);
                yield return null;
            }

            if (from != null)
            {
                from.Stop();
                from.clip = null;
                from.volume = 0f;
            }
            SwapSources();
            fading = false;
            fadeRoutine = null;
            ApplyVolume();
        }

        void SwapSources()
        {
            AudioSource previous = activeSource;
            activeSource = inactiveSource;
            inactiveSource = previous;
        }

        float ResolveVolume() => (settings != null ? settings.MusicVolume : 1f)
            * GameSettings.MusicVolume;

        void ApplyVolume()
        {
            if (fading) return;
            if (activeSource != null) activeSource.volume = ResolveVolume();
            if (inactiveSource != null) inactiveSource.volume = 0f;
        }
    }
}
