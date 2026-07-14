using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 짧은 상태 메시지를 표시하는 재사용 오버레이입니다.
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbyToastOverlay : MonoBehaviour
    {
        GameObject box;
        CanvasGroup group;
        Text label;
        Image accent;
        Coroutine animation;

        public void Initialize(RectTransform parent, LobbyUiFactory ui)
        {
            transform.SetParent(parent, false);
            var controllerRect = (RectTransform)transform;
            controllerRect.anchorMin = Vector2.zero;
            controllerRect.anchorMax = Vector2.one;
            controllerRect.offsetMin = controllerRect.offsetMax = Vector2.zero;

            Image image = ui.CreateImage("Toast", transform, UrbanFantasyStyle.PanelStrong,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 132), new Vector2(520, 58));
            UrbanFantasyStyle.AddBorder(ui, image.rectTransform);
            box = image.gameObject;
            group = box.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            accent = ui.CreateImage("Toast Accent", box.transform, UrbanFantasyStyle.Cyan,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(3, 0),
                new Vector2(4, -14));
            label = ui.CreateText("Toast Message", string.Empty, box.transform, 17, FontStyle.Normal,
                UrbanFantasyStyle.Silver,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36, 0), TextAnchor.MiddleCenter);
            box.SetActive(false);
        }

        public void Show(string message)
        {
            Show(message, StarfallStatusTone.Info);
        }

        public void Show(string message, StarfallStatusTone tone)
        {
            if (animation != null) StopCoroutine(animation);
            if (accent != null)
            {
                accent.color = tone switch
                {
                    StarfallStatusTone.Success => UrbanFantasyStyle.Success,
                    StarfallStatusTone.Warning => UrbanFantasyStyle.Warning,
                    StarfallStatusTone.Danger => UrbanFantasyStyle.Danger,
                    StarfallStatusTone.Premium => UrbanFantasyStyle.Gold,
                    _ => UrbanFantasyStyle.Cyan
                };
            }
            box.transform.SetAsLastSibling();
            animation = StartCoroutine(Animate(message));
        }

        IEnumerator Animate(string message)
        {
            box.SetActive(true);
            label.text = message;
            group.alpha = 0;
            RectTransform rect = (RectTransform)box.transform;
            Vector2 target = new Vector2(0, 132);
            rect.anchoredPosition = target + Vector2.down * 14;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 7f)
            {
                group.alpha = Mathf.SmoothStep(0, 1, t);
                rect.anchoredPosition = Vector2.Lerp(target + Vector2.down * 14, target, t);
                yield return null;
            }
            group.alpha = 1;
            rect.anchoredPosition = target;
            yield return new WaitForSecondsRealtime(1.65f);
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 6f)
            {
                group.alpha = t;
                yield return null;
            }
            box.SetActive(false);
            animation = null;
        }
    }
}
