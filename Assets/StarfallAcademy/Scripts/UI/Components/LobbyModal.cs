using System.Collections;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbyModal : MonoBehaviour
    {
        GameObject layer;
        RectTransform card;
        CanvasGroup group;
        Text titleLabel;
        Text bodyLabel;
        GameObject confirmButton;
        GameObject cancelButton;
        Text confirmLabel;
        Text cancelLabel;
        Action confirmAction;
        Action cancelAction;
        Coroutine animation;

        public void Initialize(RectTransform parent, LobbyUiFactory ui)
        {
            transform.SetParent(parent, false);
            var controllerRect = (RectTransform)transform;
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.offsetMin = controllerRect.offsetMax = Vector2.zero;

            layer = new GameObject("Modal Layer", typeof(RectTransform), typeof(CanvasGroup));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
            group = layer.GetComponent<CanvasGroup>();

            Image dim = ui.CreateImage("Backdrop", layer.transform, UrbanFantasyStyle.Backdrop,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(Cancel);

            Image cardImage = ui.CreateImage("Popup Card", layer.transform, UrbanFantasyStyle.PanelStrong,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(650, 470), true);
            card = cardImage.rectTransform;
            UrbanFantasyStyle.AddBorder(ui, card, UrbanFantasyStyle.StrongLine);
            ui.CreateImage("Accent Line", card, UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -3), new Vector2(-44, 2));
            ui.CreateText("Popup Eyebrow", "S T A R F A L L   A C A D E M Y", card, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -39), new Vector2(-76, 24), TextAnchor.MiddleLeft);
            titleLabel = ui.CreateText("Popup Title", string.Empty, card, 31, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -82), new Vector2(-76, 46), TextAnchor.MiddleLeft);
            ui.CreateImage("Divider", card, UrbanFantasyStyle.Line, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -126), new Vector2(-76, 2));
            bodyLabel = ui.CreateText("Popup Body", string.Empty, card, 20, FontStyle.Normal,
                new Color(UrbanFantasyStyle.Silver.r, UrbanFantasyStyle.Silver.g,
                    UrbanFantasyStyle.Silver.b, .82f),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -22), new Vector2(-76, -190), TextAnchor.UpperLeft);
            bodyLabel.lineSpacing = 1.25f;
            ui.CreateButton("Close Popup", card, new Vector2(1, 1), new Vector2(-32, -32), new Vector2(46, 46),
                "×", 27, UrbanFantasyStyle.PanelSoft, Cancel);
            confirmButton = ui.CreateStyledButton("Confirm Popup", card, new Vector2(.5f, 0),
                new Vector2(0, 42), new Vector2(220, 58), "확인", 18,
                StarfallButtonStyle.Primary, Confirm);
            confirmLabel = confirmButton.GetComponentInChildren<Text>();
            cancelButton = ui.CreateStyledButton("Cancel Popup", card, new Vector2(.5f, 0),
                new Vector2(-130, 42), new Vector2(220, 58), "취소", 18,
                StarfallButtonStyle.Secondary, Cancel);
            cancelLabel = cancelButton.GetComponentInChildren<Text>();
            cancelButton.SetActive(false);
            layer.SetActive(false);
        }

        public void Open(string title, string body)
        {
            Open(title, body, "확인", null, null, null);
        }

        public void Open(string title, string body, string confirmText, Action onConfirm,
            string cancelText = "취소", Action onCancel = null)
        {
            if (animation != null) StopCoroutine(animation);
            layer.transform.SetAsLastSibling();
            titleLabel.text = title;
            bodyLabel.text = body;
            confirmAction = onConfirm;
            cancelAction = onCancel;
            confirmLabel.text = string.IsNullOrWhiteSpace(confirmText) ? "확인" : confirmText;
            bool hasCancel = !string.IsNullOrWhiteSpace(cancelText);
            cancelButton.SetActive(hasCancel);
            cancelLabel.text = hasCancel ? cancelText : string.Empty;
            confirmButton.GetComponent<RectTransform>().anchoredPosition =
                hasCancel ? new Vector2(130, 42) : new Vector2(0, 42);
            layer.SetActive(true);
            group.interactable = true;
            group.blocksRaycasts = true;
            animation = StartCoroutine(ShowAnimation());
        }

        void Confirm()
        {
            Action callback = confirmAction;
            ClearActions();
            Close();
            callback?.Invoke();
        }

        void Cancel()
        {
            Action callback = cancelAction;
            ClearActions();
            Close();
            callback?.Invoke();
        }

        public void Close()
        {
            if (!layer.activeSelf) return;
            ClearActions();
            if (animation != null) StopCoroutine(animation);
            group.interactable = false;
            group.blocksRaycasts = false;
            animation = StartCoroutine(HideAnimation());
        }

        void ClearActions()
        {
            confirmAction = null;
            cancelAction = null;
        }

        IEnumerator ShowAnimation()
        {
            group.alpha = 0;
            card.localScale = Vector3.one * .92f;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 7f)
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = eased;
                card.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, eased);
                yield return null;
            }
            group.alpha = 1;
            card.localScale = Vector3.one;
            animation = null;
        }

        IEnumerator HideAnimation()
        {
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 8f)
            {
                group.alpha = t;
                card.localScale = Vector3.one * Mathf.Lerp(.96f, 1f, t);
                yield return null;
            }
            layer.SetActive(false);
            animation = null;
        }
    }
}
