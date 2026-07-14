using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby
{
    /// <summary>씬 전환과 스테이지 선택에 맞춰 하나의 BGM을 유지하고 교차 재생합니다.</summary>
    public sealed class GameAudioDirector : MonoBehaviour
    {
        const string SettingsResourcePath = "Data/GameAudioSettings";
        static GameAudioDirector instance;
        static AudioClip requestedOverride;

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

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            requestedOverride = null;
            RefreshInternal(null);
        }

        public static void RefreshForCurrentScene(StageData selectedStage = null,
            AudioClip explicitOverride = null)
        {
            requestedOverride = explicitOverride;
            EnsureInstance();
            instance.RefreshInternal(selectedStage);
        }

        public static void ClearSceneOverride()
        {
            requestedOverride = null;
            if (instance != null) instance.RefreshInternal(null);
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
                target = settings.ResolveSceneBgm(SceneManager.GetActiveScene().name, stage,
                    requestedOverride);
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
