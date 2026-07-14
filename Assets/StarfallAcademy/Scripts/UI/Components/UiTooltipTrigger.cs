using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    /// <summary>Mouse hover and mobile long-press tooltip for dense HUD information.</summary>
    public sealed class UiTooltipTrigger : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        RectTransform overlayRoot;
        LobbyUiFactory ui;
        Func<string> contentProvider;
        string heading;
        GameObject tooltip;
        Coroutine pending;
        Vector2 pointerPosition;
        bool visible;

        public void Initialize(RectTransform root, LobbyUiFactory factory, string title,
            Func<string> provider)
        {
            overlayRoot = root;
            ui = factory;
            heading = title ?? string.Empty;
            contentProvider = provider;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerPosition = eventData.position;
            Schedule(.32f, false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelPending();
            Hide();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerPosition = eventData.position;
            Schedule(.42f, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelPending();
            if (visible) pending = StartCoroutine(HideAfter(1.6f));
        }

        void Schedule(float delay, bool autoHide)
        {
            CancelPending();
            pending = StartCoroutine(ShowAfter(delay, autoHide));
        }

        IEnumerator ShowAfter(float delay, bool autoHide)
        {
            yield return new WaitForSecondsRealtime(delay);
            Show();
            pending = null;
            if (autoHide && visible) pending = StartCoroutine(HideAfter(2.4f));
        }

        IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Hide();
            pending = null;
        }

        void Show()
        {
            if (overlayRoot == null || ui == null || contentProvider == null) return;
            string content = contentProvider.Invoke();
            if (string.IsNullOrWhiteSpace(content)) return;
            Hide();

            RectTransform card = ui.CreateCard("Context Tooltip", overlayRoot,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero,
                new Vector2(440, 180), true);
            tooltip = card.gameObject;
            tooltip.transform.SetAsLastSibling();
            CanvasGroup group = tooltip.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            ui.CreateText("Tooltip Heading", heading, card, 13, FontStyle.Bold,
                UrbanFantasyStyle.Cyan, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -24), new Vector2(-34, 24), TextAnchor.MiddleLeft);
            Text body = ui.CreateText("Tooltip Body", content, card, 13, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, -19), new Vector2(-34, -58), TextAnchor.UpperLeft);
            body.lineSpacing = 1.12f;
            Canvas.ForceUpdateCanvases();
            float height = Mathf.Clamp(body.preferredHeight + 78f, 118f, 310f);
            card.sizeDelta = new Vector2(440, height);
            Position(card);
            visible = true;
        }

        void Position(RectTransform card)
        {
            Camera camera = null;
            Canvas canvas = overlayRoot.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                camera = canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRoot,
                pointerPosition, camera, out Vector2 local);
            Vector2 half = card.sizeDelta * .5f;
            Rect bounds = overlayRoot.rect;
            local += new Vector2(half.x + 20f, -half.y - 20f);
            local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 12f,
                bounds.xMax - half.x - 12f);
            local.y = Mathf.Clamp(local.y, bounds.yMin + half.y + 12f,
                bounds.yMax - half.y - 12f);
            card.anchoredPosition = local;
        }

        void Hide()
        {
            visible = false;
            if (tooltip == null) return;
            Destroy(tooltip);
            tooltip = null;
        }

        void CancelPending()
        {
            if (pending == null) return;
            StopCoroutine(pending);
            pending = null;
        }

        void OnDisable()
        {
            CancelPending();
            Hide();
        }
    }
}
