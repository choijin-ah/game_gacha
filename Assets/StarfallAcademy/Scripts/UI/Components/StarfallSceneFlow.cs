using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    /// <summary>
    /// Shared asynchronous scene transition. It keeps navigation responsive, blocks duplicate
    /// taps, and gives every runtime screen the same branded loading state.
    /// </summary>
    public static class StarfallSceneFlow
    {
        static bool loading;

        public static bool IsLoading => loading;

        public static void Load(string sceneName)
        {
            if (loading || string.IsNullOrWhiteSpace(sceneName)) return;
            loading = true;
            var root = new GameObject("Starfall Scene Transition",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(StarfallSceneTransition));
            Object.DontDestroyOnLoad(root);
            root.GetComponent<StarfallSceneTransition>().Begin(sceneName,
                () => loading = false);
        }
    }

    [RequireComponent(typeof(RectTransform))]
    sealed class StarfallSceneTransition : MonoBehaviour
    {
        static readonly string[] Tips =
        {
            "속성과 역할을 조합하면 더 안정적인 편성을 만들 수 있습니다.",
            "미수령 출석 보상과 우편은 로비 배지에서 확인할 수 있습니다.",
            "장비 세트 효과는 전투 시작 시 자동으로 계산됩니다.",
            "주간 보스 최고 점수와 도전의 탑 별 기록은 매 전투 후 저장됩니다."
        };

        CanvasGroup group;
        Image progressFill;
        Text progressLabel;
        RectTransform sigil;
        string targetScene;
        System.Action completed;

        public void Begin(string sceneName, System.Action onCompleted)
        {
            targetScene = sceneName;
            completed = onCompleted;
            Build();
            StartCoroutine(LoadRoutine());
        }

        void Build()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            scaler.referencePixelsPerUnit = 100;

            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.interactable = true;
            group.blocksRaycasts = true;

            var ui = new LobbyUiFactory(new LobbyTheme());
            ui.CreateImage("Loading Backdrop", root, UrbanFantasyStyle.Backdrop,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            ui.CreateImage("Violet Glow", root, new Color(.22f, .12f, .55f, .22f),
                new Vector2(.18f, .12f), new Vector2(.82f, .88f), Vector2.zero, Vector2.zero);
            ui.CreateImage("Horizon", root, UrbanFantasyStyle.Line,
                new Vector2(.18f, .5f), new Vector2(.82f, .5f), Vector2.zero,
                new Vector2(0, 1));

            Text emblem = ui.CreateText("Loading Sigil", "✦", root, 92, FontStyle.Normal,
                UrbanFantasyStyle.Cyan, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, 90), new Vector2(160, 160), TextAnchor.MiddleCenter);
            sigil = emblem.rectTransform;
            ui.CreateText("Loading Title", "STARFALL  ACADEMY", root, 24, FontStyle.Bold,
                UrbanFantasyStyle.Silver, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0, -10), new Vector2(520, 44), TextAnchor.MiddleCenter);
            ui.CreateStatusPill("Destination", root, new Vector2(.5f, .5f),
                new Vector2(0, -62), targetScene.ToUpperInvariant(), StarfallStatusTone.Info);
            ui.CreateText("Loading Tip", Tips[(targetScene.GetHashCode() & int.MaxValue) % Tips.Length],
                root, 15, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 112),
                new Vector2(760, 34), TextAnchor.MiddleCenter);
            progressFill = ui.CreateProgressBar("Loading Progress", root, new Vector2(.5f, 0),
                new Vector2(0, 72), new Vector2(520, 12), 0f, UrbanFantasyStyle.Violet);
            progressLabel = ui.CreateText("Loading Progress Label", "LOADING  0%", root,
                11, FontStyle.Bold, UrbanFantasyStyle.Muted,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 43),
                new Vector2(260, 24), TextAnchor.MiddleCenter);
        }

        void Update()
        {
            if (sigil != null) sigil.Rotate(0, 0, -34f * Time.unscaledDeltaTime);
        }

        IEnumerator LoadRoutine()
        {
            yield return Fade(0f, 1f, .18f);
            AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);
            if (operation == null)
            {
                Complete();
                Destroy(gameObject);
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < .9f)
            {
                SetProgress(Mathf.Clamp01(operation.progress / .9f));
                yield return null;
            }

            SetProgress(1f);
            yield return new WaitForSecondsRealtime(.08f);
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            yield return null;
            yield return Fade(1f, 0f, .22f);
            Complete();
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            group.alpha = from;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.SmoothStep(from, to, elapsed / duration);
                yield return null;
            }
            group.alpha = to;
        }

        void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (progressFill != null)
            {
                Vector2 max = progressFill.rectTransform.anchorMax;
                max.x = value;
                progressFill.rectTransform.anchorMax = max;
            }
            if (progressLabel != null)
                progressLabel.text = "LOADING  " + Mathf.RoundToInt(value * 100f) + "%";
        }

        void OnDestroy()
        {
            Complete();
        }

        void Complete()
        {
            System.Action callback = completed;
            completed = null;
            callback?.Invoke();
        }
    }
}
