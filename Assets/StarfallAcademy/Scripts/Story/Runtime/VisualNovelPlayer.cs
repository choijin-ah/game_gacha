using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    /// <summary>
    /// Runtime visual-novel renderer.  It deliberately consumes only StoryDatabase assets so the
    /// editor/import pipeline and the player stay independent from one another.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class VisualNovelPlayer : MonoBehaviour
    {
        StoryDatabase database;
        StoryEpisode episode;
        LobbyUiFactory ui;
        Action<StoryEpisode> onClosed;

        RectTransform stage;
        Image background;
        Image cg;
        Image leftCharacter;
        Image centerCharacter;
        Image rightCharacter;
        Image transitionOverlay;
        Image vignette;
        Text chapterText;
        Text speakerText;
        Text dialogueText;
        Text counterText;
        Text autoLabel;
        Text skipLabel;
        Text completionText;
        RectTransform choiceRoot;
        RectTransform logPanel;
        Text logText;
        AudioSource bgmSource;
        AudioSource sfxSource;

        readonly List<string> logEntries = new List<string>();
        Coroutine typingRoutine;
        Coroutine transitionRoutine;
        int lineIndex;
        bool isTyping;
        bool autoMode;
        bool skipMode;
        bool logVisible;
        bool endingShown;
        float autoTimer;
        string fullLineText = string.Empty;
        Vector2 stageOrigin;

        public StoryEpisode CurrentEpisode => episode;
        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(StoryDatabase source, StoryEpisode selectedEpisode, LobbyUiFactory factory,
            Action<StoryEpisode> closed)
        {
            database = source;
            ui = factory ?? new LobbyUiFactory(new LobbyTheme());
            onClosed = closed;
            Build();
            LoadEpisode(selectedEpisode, true);
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (logVisible) ToggleLog();
                else Close();
                return;
            }
            if (logVisible) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Backspace)) Previous();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.RightArrow)) Next();
            if (Input.GetKeyDown(KeyCode.A)) ToggleAuto();
            if (Input.GetKeyDown(KeyCode.S)) ToggleSkip();
            if (Input.GetKeyDown(KeyCode.L)) ToggleLog();

            if (isTyping || HasVisibleChoices() || episode == null) return;
            if (skipMode)
            {
                autoTimer += Time.unscaledDeltaTime;
                if (autoTimer >= .09f)
                {
                    autoTimer = 0f;
                    Next();
                }
            }
            else if (autoMode)
            {
                StoryLine line = CurrentLine();
                float wait = line != null && line.AutoDuration > 0f ? line.AutoDuration : 2.2f;
                autoTimer += Time.unscaledDeltaTime;
                if (autoTimer >= wait)
                {
                    autoTimer = 0f;
                    Next();
                }
            }
        }

        void Build()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            stage = CreateLayer("Visual Novel Stage", root);
            background = ui.CreateImage("Background", stage, LobbyTheme.Hex("080910"),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.type = Image.Type.Simple;
            background.preserveAspect = false;
            cg = ui.CreateImage("CG", stage, Color.clear, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            cg.type = Image.Type.Simple;
            cg.preserveAspect = false;

            ui.CreateImage("Top Grade", stage, new Color(.01f, .012f, .02f, .48f),
                new Vector2(0, .76f), Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Bottom Grade", stage, new Color(.002f, .002f, .005f, .72f),
                Vector2.zero, new Vector2(1, .25f), Vector2.zero, Vector2.zero);

            leftCharacter = CreateCharacter("Left Character", stage, new Vector2(.23f, 0), false);
            centerCharacter = CreateCharacter("Center Character", stage, new Vector2(.5f, 0), false);
            rightCharacter = CreateCharacter("Right Character", stage, new Vector2(.77f, 0), true);

            BuildTopBar(stage);
            BuildDialogue(stage);
            BuildChoices(stage);
            BuildLog(stage);

            vignette = ui.CreateImage("Vignette", stage, Color.clear, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            vignette.raycastTarget = false;
            vignette.transform.SetSiblingIndex(Mathf.Max(0, vignette.transform.GetSiblingIndex() - 4));

            transitionOverlay = ui.CreateImage("Transition", stage, Color.clear, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, true);
            transitionOverlay.transform.SetAsLastSibling();
            transitionOverlay.raycastTarget = false;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = GameSettings.MusicVolume;
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = GameSettings.SfxVolume;
        }

        void BuildTopBar(RectTransform parent)
        {
            RectTransform bar = ui.CreateImage("Reader Header", parent, new Color(.008f, .008f, .014f, .82f),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -45), new Vector2(0, 90), true).rectTransform;
            ui.CreateImage("Header Line", bar, UrbanFantasyStyle.StrongLine, Vector2.zero,
                new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            chapterText = ui.CreateText("Chapter", string.Empty, bar, 17, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(250, 0), new Vector2(420, 44), TextAnchor.MiddleLeft);
            counterText = ui.CreateText("Counter", string.Empty, bar, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(180, 30), TextAnchor.MiddleCenter);

            CreateControl(bar, "Log", "LOG", new Vector2(1, .5f), new Vector2(-358, 0), ToggleLog, out _);
            CreateControl(bar, "Auto", "AUTO", new Vector2(1, .5f), new Vector2(-262, 0), ToggleAuto, out autoLabel);
            CreateControl(bar, "Skip", "SKIP", new Vector2(1, .5f), new Vector2(-166, 0), ToggleSkip, out skipLabel);
            CreateControl(bar, "Exit", "EXIT", new Vector2(1, .5f), new Vector2(-70, 0), Close, out _);
        }

        void BuildDialogue(RectTransform parent)
        {
            RectTransform panel = ui.CreateImage("Dialogue Panel", parent, new Color(.006f, .006f, .011f, .93f),
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 142),
                new Vector2(1620, 248), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel, UrbanFantasyStyle.StrongLine);
            ui.CreateImage("Dialogue Accent", panel, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -44), new Vector2(4, 52));
            speakerText = ui.CreateText("Speaker", string.Empty, panel, 23, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(206, -39), new Vector2(330, 42), TextAnchor.MiddleLeft);
            dialogueText = ui.CreateText("Dialogue", string.Empty, panel, 22, FontStyle.Normal,
                new Color(.92f, .92f, .95f, 1f), new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, -28), new Vector2(-112, -82), TextAnchor.UpperLeft);
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            completionText = ui.CreateText("Completion", string.Empty, panel, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-108, 24), new Vector2(180, 22), TextAnchor.MiddleRight);

            ui.CreateHitButton("Advance Dialogue", panel, new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1510, 224), Next);
            GameObject previous = ui.CreateButton("Previous", panel, new Vector2(0, 0),
                new Vector2(56, 26), new Vector2(62, 38), "‹", 28,
                new Color(.07f, .07f, .09f, .9f), Previous);
            GameObject next = ui.CreateButton("Next", panel, new Vector2(1, 0),
                new Vector2(-56, 26), new Vector2(62, 38), "›", 28,
                new Color(.07f, .07f, .09f, .9f), Next);
            previous.transform.SetAsLastSibling();
            next.transform.SetAsLastSibling();
        }

        void BuildChoices(RectTransform parent)
        {
            choiceRoot = CreateLayer("Choices", parent);
            choiceRoot.anchorMin = new Vector2(.5f, .5f);
            choiceRoot.anchorMax = new Vector2(.5f, .5f);
            choiceRoot.pivot = new Vector2(.5f, .5f);
            choiceRoot.sizeDelta = new Vector2(760, 430);
            choiceRoot.anchoredPosition = new Vector2(0, 75);
            choiceRoot.gameObject.SetActive(false);
        }

        void BuildLog(RectTransform parent)
        {
            logPanel = ui.CreateImage("Dialogue Log", parent, new Color(.004f, .004f, .008f, .97f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(1540, 840), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, logPanel, UrbanFantasyStyle.StrongLine);
            ui.CreateText("Log Title", "D I A L O G U E   L O G   ·   대화 기록", logPanel, 20,
                FontStyle.Normal, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -42), new Vector2(-100, 46), TextAnchor.MiddleLeft);
            logText = ui.CreateText("Entries", string.Empty, logPanel, 17, FontStyle.Normal,
                new Color(.88f, .88f, .91f, .9f), new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, -18), new Vector2(-110, -120), TextAnchor.LowerLeft);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Truncate;
            ui.CreateButton("Close Log", logPanel, new Vector2(1, 1), new Vector2(-52, -42),
                new Vector2(56, 48), "×", 25, UrbanFantasyStyle.PanelStrong, ToggleLog);
            logPanel.gameObject.SetActive(false);
        }

        Image CreateCharacter(string name, Transform parent, Vector2 anchor, bool flip)
        {
            Image image = ui.CreateImage(name, parent, Color.clear, anchor, anchor,
                new Vector2(0, 365), new Vector2(700, 850));
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.rectTransform.pivot = new Vector2(.5f, 0);
            image.rectTransform.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
            return image;
        }

        void CreateControl(Transform parent, string name, string label, Vector2 anchor, Vector2 position,
            Action action, out Text createdLabel)
        {
            GameObject button = ui.CreateButton(name, parent, anchor, position, new Vector2(82, 42),
                label, 11, new Color(.035f, .035f, .05f, .86f), action);
            UrbanFantasyStyle.AddBorder(ui, button.GetComponent<RectTransform>());
            createdLabel = button.transform.Find("Label")?.GetComponent<Text>();
        }

        void LoadEpisode(StoryEpisode selectedEpisode, bool resume)
        {
            episode = selectedEpisode;
            endingShown = false;
            logEntries.Clear();
            autoTimer = 0f;
            if (episode == null)
            {
                chapterText.text = "NO STORY SELECTED";
                speakerText.text = string.Empty;
                dialogueText.text = "재생할 스토리가 없습니다.";
                counterText.text = "—";
                return;
            }

            chapterText.text = CategoryLabel(episode.Category) + "   /   " + episode.Title;
            lineIndex = resume ? StoryProgressService.GetLastLine(episode) : 0;
            SetLine(lineIndex, false);
        }

        public void Next()
        {
            if (episode == null) { Close(); return; }
            if (logVisible) { ToggleLog(); return; }
            if (isTyping)
            {
                FinishTyping();
                return;
            }
            if (HasVisibleChoices()) return;
            if (episode.Lines == null || lineIndex >= episode.Lines.Count - 1)
            {
                if (!endingShown)
                {
                    StoryProgressService.MarkCompleted(database, episode);
                    endingShown = true;
                    completionText.text = "COMPLETE  ·  한 번 더 누르면 기록실로 돌아갑니다";
                    autoMode = skipMode = false;
                    RefreshModes();
                }
                else Close();
                return;
            }
            SetLine(lineIndex + 1, false);
        }

        public void Previous()
        {
            if (episode == null || isTyping || lineIndex <= 0) return;
            endingShown = false;
            SetLine(lineIndex - 1, true);
        }

        void SetLine(int requestedIndex, bool instant)
        {
            if (episode == null || episode.Lines == null || episode.Lines.Count == 0)
            {
                speakerText.text = string.Empty;
                dialogueText.text = "이 에피소드에는 아직 대사가 없습니다.";
                counterText.text = "0 / 0";
                completionText.text = string.Empty;
                return;
            }

            lineIndex = Mathf.Clamp(requestedIndex, 0, episode.Lines.Count - 1);
            StoryLine line = episode.Lines[lineIndex];
            StoryProgressService.SaveLine(episode, lineIndex);
            completionText.text = Mathf.RoundToInt(StoryProgressService.GetReadProgress(episode) * 100f) + "% READ";
            counterText.text = (lineIndex + 1).ToString("000") + " / " + episode.Lines.Count.ToString("000");
            autoTimer = 0f;
            endingShown = false;

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;
            fullLineText = line != null ? line.Text ?? string.Empty : string.Empty;
            speakerText.text = ResolveSpeaker(line);
            ApplyPresentation(line);
            BuildChoiceButtons(line);
            AddLogEntry(speakerText.text, fullLineText);

            if (instant || skipMode || string.IsNullOrEmpty(fullLineText))
            {
                dialogueText.text = fullLineText;
                isTyping = false;
                RevealChoicesIfNeeded();
            }
            else
            {
                typingRoutine = StartCoroutine(TypeLine(line));
            }
        }

        IEnumerator TypeLine(StoryLine line)
        {
            isTyping = true;
            dialogueText.text = string.Empty;
            float configuredSpeed = line != null ? line.TextSpeed : 0f;
            // Excel/editor data uses seconds-per-character by default (.035).  Values above one
            // are also accepted as characters-per-second for convenience.
            float delay = configuredSpeed <= 0f ? .035f
                : configuredSpeed <= 1f ? configuredSpeed : 1f / configuredSpeed;
            for (int i = 0; i < fullLineText.Length; i++)
            {
                dialogueText.text = fullLineText.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(delay);
            }
            isTyping = false;
            typingRoutine = null;
            RevealChoicesIfNeeded();
        }

        void FinishTyping()
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = null;
            isTyping = false;
            dialogueText.text = fullLineText;
            RevealChoicesIfNeeded();
        }

        void ApplyPresentation(StoryLine line)
        {
            if (line == null) return;
            if (line.Background != null)
            {
                background.sprite = line.Background;
                background.color = Color.white;
            }
            else if (background.sprite == null)
            {
                Texture2D fallback = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
                if (fallback != null)
                {
                    background.sprite = ui.SpriteFromTexture(fallback);
                    background.color = Color.white;
                }
            }

            cg.sprite = line.Cg;
            cg.color = line.Cg != null ? Color.white : Color.clear;
            ApplyCharacter(leftCharacter, line.Left, false);
            ApplyCharacter(centerCharacter, line.Center, false);
            ApplyCharacter(rightCharacter, line.Right, true);

            if (line.Bgm != null && bgmSource.clip != line.Bgm)
            {
                bgmSource.clip = line.Bgm;
                bgmSource.Play();
            }
            if (line.Sfx != null) sfxSource.PlayOneShot(line.Sfx);

            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            stage.anchoredPosition = Vector2.zero;
            transitionRoutine = StartCoroutine(PlayLineEffects(line));
        }

        void ApplyCharacter(Image target, StoryCharacterDisplay display, bool defaultFlip)
        {
            if (target == null) return;
            if (display == null || !display.Visible)
            {
                target.sprite = null;
                target.color = Color.clear;
                return;
            }

            Sprite sprite = display.ResolvedSprite;
            target.sprite = sprite;
            Color tint = display.Tint;
            if (tint.a <= 0f) tint = Color.white;
            target.color = sprite != null ? tint : Color.clear;
            target.rectTransform.anchoredPosition = new Vector2(display.Offset.x, 365 + display.Offset.y);
            bool flip = defaultFlip ^ display.FlipX;
            target.rectTransform.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        }

        IEnumerator PlayLineEffects(StoryLine line)
        {
            float duration = Mathf.Clamp(line.EffectDuration > 0f ? line.EffectDuration : .3f, .08f, 2f);
            Color transitionColor = Color.black;
            bool slide = line.Transition == StoryTransition.SlideLeft
                || line.Transition == StoryTransition.SlideRight;
            bool transition = line.Transition != StoryTransition.None
                && line.Transition != StoryTransition.Cut && !slide;
            if (line.Transition == StoryTransition.FadeToWhite) transitionColor = Color.white;

            if (transition || (line.Effects & StoryScreenEffect.FadeIn) != 0)
            {
                float startAlpha = line.Transition == StoryTransition.CrossFade ? .45f : 1f;
                transitionOverlay.color = new Color(transitionColor.r, transitionColor.g,
                    transitionColor.b, startAlpha);
                yield return FadeImage(transitionOverlay, startAlpha, 0f, duration);
            }
            else transitionOverlay.color = Color.clear;

            if (slide)
                yield return SlideStage(line.Transition == StoryTransition.SlideLeft ? 140f : -140f,
                    duration);

            if ((line.Effects & StoryScreenEffect.FlashWhite) != 0)
                yield return Flash(Color.white, duration);
            if ((line.Effects & StoryScreenEffect.FlashBlack) != 0)
                yield return Flash(Color.black, duration);
            if ((line.Effects & StoryScreenEffect.Shake) != 0)
                yield return Shake(Mathf.Max(4f, line.ShakeStrength), duration);

            vignette.color = (line.Effects & StoryScreenEffect.Vignette) != 0
                ? new Color(0, 0, 0, .2f) : Color.clear;
            if ((line.Effects & StoryScreenEffect.FadeOut) != 0)
                yield return FadeImage(transitionOverlay, 0f, 1f, duration);
            transitionRoutine = null;
        }

        IEnumerator SlideStage(float fromX, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
                stage.anchoredPosition = Vector2.Lerp(new Vector2(fromX, 0f), Vector2.zero, t);
                yield return null;
            }
            stage.anchoredPosition = Vector2.zero;
        }

        IEnumerator Flash(Color color, float duration)
        {
            transitionOverlay.color = new Color(color.r, color.g, color.b, 0f);
            yield return FadeImage(transitionOverlay, 0f, .88f, duration * .25f);
            yield return FadeImage(transitionOverlay, .88f, 0f, duration * .75f);
        }

        IEnumerator FadeImage(Image image, float from, float to, float duration)
        {
            Color baseColor = image.color;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / duration));
                image.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, to);
        }

        IEnumerator Shake(float strength, float duration)
        {
            stageOrigin = stage.anchoredPosition;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                stage.anchoredPosition = stageOrigin + UnityEngine.Random.insideUnitCircle * strength;
                yield return null;
            }
            stage.anchoredPosition = stageOrigin;
        }

        void BuildChoiceButtons(StoryLine line)
        {
            foreach (Transform child in choiceRoot)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            choiceRoot.gameObject.SetActive(false);
            if (line == null || line.Choices == null || line.Choices.Count == 0) return;

            var available = new List<StoryChoice>();
            foreach (StoryChoice choice in line.Choices)
                if (choice != null && StoryProgressService.IsChoiceAvailable(choice)) available.Add(choice);
            if (available.Count == 0) return;

            float totalHeight = available.Count * 72f + Mathf.Max(0, available.Count - 1) * 12f;
            float top = totalHeight * .5f - 36f;
            for (int i = 0; i < available.Count; i++)
            {
                StoryChoice captured = available[i];
                GameObject button = ui.CreateButton("Choice " + i, choiceRoot, new Vector2(.5f, .5f),
                    new Vector2(0, top - i * 84f), new Vector2(720, 68),
                    (i + 1) + "   " + captured.Text, 17, new Color(.015f, .015f, .024f, .96f),
                    () => SelectChoice(captured), TextAnchor.MiddleLeft);
                UrbanFantasyStyle.AddBorder(ui, button.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
                Text label = button.transform.Find("Label")?.GetComponent<Text>();
                if (label != null) label.rectTransform.offsetMin = new Vector2(30, 0);
            }
        }

        void RevealChoicesIfNeeded()
        {
            if (choiceRoot.childCount > 0) choiceRoot.gameObject.SetActive(true);
        }

        bool HasVisibleChoices()
        {
            return choiceRoot != null && choiceRoot.gameObject.activeSelf && choiceRoot.childCount > 0;
        }

        void SelectChoice(StoryChoice choice)
        {
            choiceRoot.gameObject.SetActive(false);
            if (!string.IsNullOrWhiteSpace(choice.NextEpisodeId) && database != null)
            {
                StoryEpisode nextEpisode = database.FindEpisode(choice.NextEpisodeId);
                if (nextEpisode != null)
                {
                    StoryProgressService.SetUnlocked(nextEpisode);
                    StoryProgressService.MarkCompleted(database, episode);
                    LoadEpisode(nextEpisode, false);
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(choice.NextLineId))
            {
                int destination = FindLine(choice.NextLineId);
                if (destination >= 0)
                {
                    SetLine(destination, false);
                    return;
                }
            }
            Next();
        }

        int FindLine(string lineId)
        {
            if (episode == null || episode.Lines == null) return -1;
            for (int i = 0; i < episode.Lines.Count; i++)
                if (episode.Lines[i] != null && episode.Lines[i].Id == lineId) return i;
            return -1;
        }

        StoryLine CurrentLine()
        {
            if (episode == null || episode.Lines == null || lineIndex < 0 || lineIndex >= episode.Lines.Count)
                return null;
            return episode.Lines[lineIndex];
        }

        void ToggleAuto()
        {
            autoMode = !autoMode;
            if (autoMode) skipMode = false;
            autoTimer = 0f;
            RefreshModes();
        }

        void ToggleSkip()
        {
            skipMode = !skipMode;
            if (skipMode) autoMode = false;
            autoTimer = 0f;
            if (skipMode && isTyping) FinishTyping();
            RefreshModes();
        }

        void RefreshModes()
        {
            if (autoLabel != null)
            {
                autoLabel.text = autoMode ? "AUTO ON" : "AUTO";
                autoLabel.color = autoMode ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Silver;
            }
            if (skipLabel != null)
            {
                skipLabel.text = skipMode ? "SKIP ON" : "SKIP";
                skipLabel.color = skipMode ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Silver;
            }
        }

        void ToggleLog()
        {
            logVisible = !logVisible;
            logPanel.gameObject.SetActive(logVisible);
            if (logVisible)
            {
                var builder = new StringBuilder();
                int first = Mathf.Max(0, logEntries.Count - 18);
                for (int i = first; i < logEntries.Count; i++) builder.AppendLine(logEntries[i]);
                logText.text = builder.ToString();
                logPanel.transform.SetAsLastSibling();
            }
            else transitionOverlay.transform.SetAsLastSibling();
        }

        void AddLogEntry(string speaker, string text)
        {
            string name = string.IsNullOrWhiteSpace(speaker) ? "NARRATION" : speaker;
            string entry = "<color=#8A8A96>" + name + "</color>  " + text + "\n";
            if (logEntries.Count == 0 || logEntries[logEntries.Count - 1] != entry) logEntries.Add(entry);
        }

        string ResolveSpeaker(StoryLine line)
        {
            if (line == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(line.SpeakerName)) return line.SpeakerName;
            if (line.Speaker != null) return line.Speaker.DisplayName;
            return line.SpeakerPosition == StorySpeakerPosition.Narrator ? string.Empty : "UNKNOWN";
        }

        public void Close()
        {
            if (episode != null) StoryProgressService.SaveLine(episode, lineIndex);
            if (bgmSource != null) bgmSource.Stop();
            StoryEpisode closedEpisode = episode;
            gameObject.SetActive(false);
            onClosed?.Invoke(closedEpisode);
            Destroy(gameObject);
        }

        static string CategoryLabel(StoryCategory category)
        {
            switch (category)
            {
                case StoryCategory.Event: return "EVENT STORY";
                case StoryCategory.Character: return "CHARACTER STORY";
                case StoryCategory.Side: return "SIDE STORY";
                default: return "MAIN STORY";
            }
        }

        static RectTransform CreateLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
