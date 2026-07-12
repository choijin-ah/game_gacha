using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbyBannerCarousel : MonoBehaviour
    {
        Text tagLabel;
        Text titleLabel;
        Text bodyLabel;
        Text pageLabel;
        CanvasGroup textGroup;
        int index;
        Coroutine transition;

        public void Initialize(RectTransform parent, LobbyUiFactory ui)
        {
            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;

            RectTransform card = ui.CreateImage("Event Banner", transform, new Color(.19f, .045f, .23f, .95f),
                Vector2.zero, Vector2.zero, new Vector2(326, 202), new Vector2(600, 168)).rectTransform;
            ui.CreateImage("Banner Glow", card, new Color(1, .18f, .54f, .14f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateText("Banner Star", "✦", card, 92, FontStyle.Bold, new Color(1, .45f, .7f, .15f),
                new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-76, 12), new Vector2(130, 130), TextAnchor.MiddleCenter);

            var textLayer = new GameObject("Banner Text", typeof(RectTransform), typeof(CanvasGroup));
            textLayer.transform.SetParent(card, false);
            RectTransform textRect = textLayer.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            textGroup = textLayer.GetComponent<CanvasGroup>();
            tagLabel = ui.CreateText("Banner Tag", string.Empty, textLayer.transform, 14, FontStyle.Bold, ui.Theme.Pink,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(89, -33), new Vector2(130, 26), TextAnchor.MiddleLeft);
            titleLabel = ui.CreateText("Banner Title", string.Empty, textLayer.transform, 29, FontStyle.Bold, ui.Theme.White,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(249, -69), new Vector2(450, 42), TextAnchor.MiddleLeft);
            bodyLabel = ui.CreateText("Banner Body", string.Empty, textLayer.transform, 15, FontStyle.Normal, ui.Theme.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(259, -108), new Vector2(470, 28), TextAnchor.MiddleLeft);

            ui.CreateButton("Previous Banner", card, new Vector2(1, 0), new Vector2(-92, 20), new Vector2(34, 34),
                "‹", 27, new Color(0, 0, 0, .34f), () => Change(-1));
            ui.CreateButton("Next Banner", card, new Vector2(1, 0), new Vector2(-50, 20), new Vector2(34, 34),
                "›", 27, new Color(0, 0, 0, .34f), () => Change(1));
            pageLabel = ui.CreateText("Banner Page", string.Empty, card, 13, FontStyle.Bold, ui.Theme.White,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-132, 20), new Vector2(55, 28), TextAnchor.MiddleCenter);
            ApplyContent();
            StartCoroutine(AutoRotate());
        }

        void Change(int delta)
        {
            index = (index + delta + LobbyContent.Banners.Length) % LobbyContent.Banners.Length;
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(TransitionContent());
        }

        IEnumerator TransitionContent()
        {
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 10f)
            {
                textGroup.alpha = t;
                yield return null;
            }
            ApplyContent();
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 8f)
            {
                textGroup.alpha = t;
                yield return null;
            }
            textGroup.alpha = 1;
            transition = null;
        }

        void ApplyContent()
        {
            BannerInfo banner = LobbyContent.Banners[index];
            tagLabel.text = banner.Tag;
            titleLabel.text = banner.Title;
            bodyLabel.text = banner.Body;
            pageLabel.text = (index + 1) + " / " + LobbyContent.Banners.Length;
        }

        IEnumerator AutoRotate()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(6f);
                Change(1);
            }
        }
    }
}
