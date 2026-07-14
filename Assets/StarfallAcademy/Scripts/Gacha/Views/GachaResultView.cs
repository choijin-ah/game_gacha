using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class GachaResultView : MonoBehaviour
    {
        LobbyUiFactory ui;
        GameObject layer;
        CanvasGroup group;
        RectTransform card;
        RectTransform content;
        Coroutine animation;

        public bool IsOpen => layer != null && layer.activeSelf;

        public void Initialize(RectTransform parent, LobbyUiFactory factory)
        {
            ui = factory;
            transform.SetParent(parent, false);
            RectTransform controller = (RectTransform)transform;
            controller.anchorMin = Vector2.zero;
            controller.anchorMax = Vector2.one;
            controller.offsetMin = controller.offsetMax = Vector2.zero;

            layer = new GameObject("Gacha Result Layer", typeof(RectTransform), typeof(CanvasGroup));
            layer.transform.SetParent(transform, false);
            RectTransform layerRect = layer.GetComponent<RectTransform>();
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
            group = layer.GetComponent<CanvasGroup>();

            Image dim = ui.CreateImage("Result Backdrop", layer.transform, new Color(.002f, .002f, .004f, .92f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button backdrop = dim.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;
            backdrop.onClick.AddListener(Close);
            card = ui.CreateImage("Result Window", layer.transform, GachaGothicStyle.PanelStrong,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1540, 850), true).rectTransform;
            GachaGothicStyle.AddBorder(ui, card, new Color(.88f, .88f, .92f, .48f));
            ui.CreateImage("Result Accent", card, GachaGothicStyle.Silver, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -3), new Vector2(-60, 2));
            ui.CreateText("Result Eyebrow", "R E C R U I T M E N T   R E S U L T", card, 11,
                FontStyle.Normal, GachaGothicStyle.Muted,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, -34), new Vector2(240, 24), TextAnchor.MiddleLeft);
            ui.CreateText("Result Title", "모집 결과", card, 31, FontStyle.Normal, GachaGothicStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(143, -72), new Vector2(250, 42), TextAnchor.MiddleLeft);
            ui.CreateButton("Close Results", card, new Vector2(1, 1), new Vector2(-36, -36), new Vector2(48, 48),
                "×", 28, GachaGothicStyle.PanelSoft, Close, TextAnchor.MiddleCenter, false);
            GameObject confirm = ui.CreateButton("Confirm Results", card, new Vector2(.5f, 0), new Vector2(0, 48),
                new Vector2(230, 58), "확인", 18, new Color(.17f, .17f, .20f, .98f), Close,
                TextAnchor.MiddleCenter, false);
            GachaGothicStyle.AddBorder(ui, confirm.GetComponent<RectTransform>(), new Color(.9f, .9f, .93f, .5f));

            var contentObject = new GameObject("Result Content", typeof(RectTransform));
            contentObject.transform.SetParent(card, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(.5f, .5f);
            content.anchorMax = new Vector2(.5f, .5f);
            content.sizeDelta = new Vector2(1380, 640);
            content.anchoredPosition = new Vector2(0, -12);
            layer.SetActive(false);
        }

        public void Show(IReadOnlyList<GachaResult> results)
        {
            ClearChildren(content);
            if (results.Count == 1)
                CreateResultCard(results[0], Vector2.zero, new Vector2(320, 450), true);
            else
            {
                for (int i = 0; i < results.Count; i++)
                {
                    int column = i % 5;
                    int row = i / 5;
                    Vector2 position = new Vector2(-520 + column * 260, 158 - row * 302);
                    CreateResultCard(results[i], position, new Vector2(238, 278), false);
                }
            }
            if (animation != null) StopCoroutine(animation);
            layer.SetActive(true);
            layer.transform.SetAsLastSibling();
            group.interactable = true;
            group.blocksRaycasts = true;
            animation = StartCoroutine(ShowAnimation());
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (animation != null) StopCoroutine(animation);
            group.interactable = false;
            group.blocksRaycasts = false;
            animation = StartCoroutine(HideAnimation());
        }

        void CreateResultCard(GachaResult result, Vector2 position, Vector2 size, bool large)
        {
            CharacterData character = result.Character;
            int rarity = result.Rarity;
            Color accent = rarity >= 5
                ? new Color(.88f, .76f, .48f, 1f)
                : rarity == 4 ? new Color(.68f, .58f, .78f, 1f) : new Color(.68f, .70f, .74f, 1f);
            RectTransform resultCard = ui.CreateImage("Result " + (character != null ? character.DisplayName : "Unknown"),
                content, GachaGothicStyle.PanelSoft, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                position, size).rectTransform;
            GachaGothicStyle.AddBorder(ui, resultCard, rarity >= 5
                ? new Color(.88f, .76f, .48f, .58f) : GachaGothicStyle.Line);
            ui.CreateImage("Rarity Accent", resultCard, accent, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -2), new Vector2(-16, 2));
            float portraitHeight = large ? 290 : 164;
            Image portrait = ui.CreateImage("Result Portrait", resultCard,
                character != null ? new Color(character.AccentColor.r, character.AccentColor.g, character.AccentColor.b, .24f)
                    : new Color(.15f, .18f, .25f, .4f), new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(0, -(portraitHeight * .5f + 18)), new Vector2(size.x - 24, portraitHeight));
            if (character != null && character.Portrait != null)
            {
                portrait.sprite = character.Portrait;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.color = Color.white;
            }
            else
            {
                string initial = character != null && character.DisplayName.Length > 0
                    ? character.DisplayName.Substring(0, 1) : "?";
                ui.CreateText("Result Initial", initial, portrait.transform, large ? 60 : 40, FontStyle.Bold,
                    ui.Theme.White, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            }
            ui.CreateText("Result Stars", rarity > 0 ? new string('★', Mathf.Min(6, rarity)) : "-", resultCard,
                large ? 20 : 14, FontStyle.Bold, accent, new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, large ? 112 : 83), new Vector2(size.x - 28, 28), TextAnchor.MiddleCenter);
            ui.CreateText("Result Name", character != null ? character.DisplayName : "UNKNOWN", resultCard,
                large ? 24 : 16, FontStyle.Normal, GachaGothicStyle.Silver,
                new Vector2(.5f, 0), new Vector2(.5f, 0),
                new Vector2(0, large ? 74 : 52), new Vector2(size.x - 28, 30), TextAnchor.MiddleCenter);
            if (result.IsFeatured)
                ui.CreateText("Featured Badge", "—  P I C K   U P  —", resultCard, 11, FontStyle.Normal,
                    GachaGothicStyle.Silver,
                    new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24),
                    new Vector2(size.x - 32, 22), TextAnchor.MiddleCenter);
            if (result.IsNew)
                ui.CreateText("New Badge", "N E W", resultCard, 11, FontStyle.Bold,
                    new Color(.82f, .12f, .10f, 1f), new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-38, -18), new Vector2(62, 22), TextAnchor.MiddleCenter);
            else if (result.DuplicateFragments > 0 || result.DuplicateSkillMaterials > 0)
                ui.CreateText("Duplicate Reward", result.DuplicateFragments > 0
                        ? "FRAG +" + result.DuplicateFragments
                        : "DUP +" + result.DuplicateSkillMaterials,
                    resultCard, 10, FontStyle.Bold, accent,
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-48, -18),
                    new Vector2(86, 22), TextAnchor.MiddleCenter);
        }

        IEnumerator ShowAnimation()
        {
            group.alpha = 0;
            card.localScale = Vector3.one * .94f;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 7f)
            {
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = eased;
                card.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, eased);
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
                card.localScale = Vector3.one * Mathf.Lerp(.97f, 1f, t);
                yield return null;
            }
            layer.SetActive(false);
            animation = null;
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }
    }
}
