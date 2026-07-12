using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbySpeechBubble : MonoBehaviour
    {
        GameObject bubble;
        RectTransform bubbleRect;
        CanvasGroup group;
        Text moodLabel;
        Text dialogueLabel;
        Text autoLabel;
        LobbyToastOverlay toast;
        Coroutine lineRoutine;
        Coroutine autoRoutine;
        int lastDialogue = -1;
        bool autoEnabled = true;

        public void Initialize(RectTransform parent, LobbyUiFactory ui, LobbyToastOverlay lobbyToast)
        {
            toast = lobbyToast;
            transform.SetParent(parent, false);
            RectTransform controllerRect = (RectTransform)transform;
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.offsetMin = controllerRect.offsetMax = Vector2.zero;

            Button characterHit = ui.CreateHitButton("Character Touch Area", transform, new Vector2(.50f, .50f),
                new Vector2(70, 15), new Vector2(470, 650), OnCharacterTapped);
            characterHit.gameObject.AddComponent<UiPressFeedback>();

            Color bubbleColor = new Color(.012f, .012f, .018f, .82f);
            Image bubbleImage = ui.CreateImage("Speech Bubble", transform, bubbleColor,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(280, 158), new Vector2(510, 184), true);
            bubble = bubbleImage.gameObject;
            bubbleRect = bubbleImage.rectTransform;
            group = bubble.AddComponent<CanvasGroup>();
            Button nextButton = bubble.AddComponent<Button>();
            nextButton.transition = Selectable.Transition.None;
            nextButton.onClick.AddListener(() => ShowNextDialogue());

            ui.CreateImage("Border Top", bubble.transform, new Color(.82f, .82f, .86f, .36f),
                new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 1));
            ui.CreateImage("Border Bottom", bubble.transform, new Color(.82f, .82f, .86f, .36f),
                Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 1));
            ui.CreateImage("Border Left", bubble.transform, new Color(.82f, .82f, .86f, .36f),
                Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(1, 0));
            ui.CreateImage("Border Right", bubble.transform, new Color(.82f, .82f, .86f, .36f),
                new Vector2(1, 0), Vector2.one, Vector2.zero, new Vector2(1, 0));
            ui.CreateText("Speaker", LobbyContent.HeroName, bubble.transform, 22, FontStyle.Normal,
                new Color(.88f, .88f, .91f, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(78, -34), new Vector2(120, 30), TextAnchor.MiddleLeft);
            moodLabel = ui.CreateText("Mood", string.Empty, bubble.transform, 11, FontStyle.Normal,
                new Color(1, 1, 1, .45f), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(205, -35), new Vector2(120, 22), TextAnchor.MiddleLeft);
            dialogueLabel = ui.CreateText("Dialogue", string.Empty, bubble.transform, 17, FontStyle.Normal,
                new Color(.86f, .86f, .89f, .92f), new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, -8), new Vector2(-56, -72), TextAnchor.MiddleLeft);

            GameObject autoButton = ui.CreateButton("Auto Dialogue", bubble.transform, new Vector2(1, 1),
                new Vector2(-58, -27), new Vector2(88, 30), "AUTO", 11,
                new Color(.12f, .12f, .14f, .9f), ToggleAuto, TextAnchor.MiddleCenter, false);
            autoLabel = autoButton.GetComponentInChildren<Text>();
            bubble.SetActive(false);

            autoRoutine = StartCoroutine(AutoDialogueLoop());
            StartCoroutine(ShowInitialGreeting());
        }

        public void ShowNextDialogue()
        {
            if (LobbyContent.Dialogues.Length == 0) return;
            int next = Random.Range(0, LobbyContent.Dialogues.Length);
            if (LobbyContent.Dialogues.Length > 1)
                while (next == lastDialogue)
                    next = Random.Range(0, LobbyContent.Dialogues.Length);
            lastDialogue = next;
            ShowLine(LobbyContent.Dialogues[next]);
        }

        void OnCharacterTapped()
        {
            ShowNextDialogue();
            toast.Show("♡  아리아와의 인연 +1");
        }

        void ToggleAuto()
        {
            autoEnabled = !autoEnabled;
            autoLabel.text = autoEnabled ? "AUTO" : "PAUSE";
            autoLabel.color = autoEnabled ? Color.white : new Color(1, 1, 1, .55f);
            toast.Show(autoEnabled ? "자동 대화를 켰습니다" : "자동 대화를 껐습니다");
            if (autoEnabled && !bubble.activeSelf)
                ShowNextDialogue();
        }

        void ShowLine(DialogueLine line)
        {
            if (lineRoutine != null) StopCoroutine(lineRoutine);
            lineRoutine = StartCoroutine(PlayLine(line));
        }

        IEnumerator ShowInitialGreeting()
        {
            yield return new WaitForSecondsRealtime(.55f);
            lastDialogue = 0;
            ShowLine(LobbyContent.Dialogues[0]);
        }

        IEnumerator AutoDialogueLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(7.5f);
                if (autoEnabled)
                    ShowNextDialogue();
            }
        }

        IEnumerator PlayLine(DialogueLine line)
        {
            bubble.SetActive(true);
            bubble.transform.SetAsLastSibling();
            group.alpha = 0;
            group.blocksRaycasts = true;
            bubbleRect.localScale = Vector3.one * .94f;
            moodLabel.text = "· " + line.Mood;
            dialogueLabel.text = string.Empty;

            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 8f)
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = eased;
                bubbleRect.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, eased);
                yield return null;
            }
            group.alpha = 1;
            bubbleRect.localScale = Vector3.one;

            float visibleCharacters = 0;
            while (visibleCharacters < line.Text.Length)
            {
                visibleCharacters += Time.unscaledDeltaTime * GameSettings.TextCharactersPerSecond;
                dialogueLabel.text = line.Text.Substring(0, Mathf.Min(line.Text.Length, Mathf.FloorToInt(visibleCharacters)));
                yield return null;
            }
            dialogueLabel.text = line.Text;
            lineRoutine = null;
        }

        void OnDestroy()
        {
            if (autoRoutine != null) StopCoroutine(autoRoutine);
        }
    }
}
