using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    // 코드 UI 생성 규칙을 한곳에 모은 공용 팩토리입니다.
    public sealed class LobbyUiFactory
    {
        static Sprite roundedSprite;
        static Sprite circleSprite;
        static readonly System.Collections.Generic.Dictionary<Texture2D, Sprite> TextureSprites =
            new System.Collections.Generic.Dictionary<Texture2D, Sprite>();

        public LobbyTheme Theme { get; }
        public LobbyUiAssets Assets { get; }

        public LobbyUiFactory(LobbyTheme theme)
        {
            Theme = theme;
            Assets = new LobbyUiAssets();
        }

        public Image CreateImage(string name, Transform parent, Color color, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax
                ? new Vector2(.5f, .5f)
                : new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : .5f,
                    anchorMin.y == anchorMax.y ? anchorMin.y : .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = raycast;
            return image;
        }

        public Text CreateText(string name, string value, Transform parent, int fontSize, FontStyle style,
            Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size,
            TextAnchor alignment, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? new Vector2(.5f, .5f) : new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.font = Theme.Font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = raycast;
            return text;
        }

        public GameObject CreateButton(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, string label, int fontSize, Color color, Action action,
            TextAnchor alignment = TextAnchor.MiddleCenter, bool useArtSkin = false)
        {
            Image image = CreateImage(name, parent, color, anchor, anchor, position, size, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic, wrapAround = true };
            if (useArtSkin && Assets.HasButtonSkin && size.x / Mathf.Max(1, size.y) >= 2f)
            {
                image.sprite = Assets.ButtonNormal;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = Assets.ButtonHighlighted,
                    pressedSprite = Assets.ButtonPressed,
                    selectedSprite = Assets.ButtonSelected,
                    disabledSprite = Assets.ButtonPressed
                };
            }
            else
            {
                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.highlightedColor = Color.Lerp(color, Color.white, .14f);
                colors.pressedColor = Color.Lerp(color, Color.black, .18f);
                colors.selectedColor = colors.highlightedColor;
                colors.fadeDuration = .08f;
                button.colors = colors;
            }
            if (action != null)
                button.onClick.AddListener(() => action());

            CreateText("Label", label, image.transform, fontSize, FontStyle.Bold, Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, alignment);
            image.gameObject.AddComponent<UiPressFeedback>();
            SelectIfNothingFocused(button);
            return image.gameObject;
        }

        public Image AddIcon(GameObject button, LobbyIcon icon, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Sprite sprite = Assets.GetIcon(icon);
            if (sprite == null) return null;
            Image image = CreateImage("Icon " + icon, button.transform, Color.white, anchor, anchor, position, size);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            UrbanFantasyStyle.ApplyMonochrome(image);
            return image;
        }

        public Image CreateCircleImage(string name, Transform parent, Color color, Vector2 anchor,
            Vector2 position, Vector2 size, bool raycast = false)
        {
            Image image = CreateImage(name, parent, color, anchor, anchor, position, size, raycast);
            image.sprite = CircleSprite();
            image.type = Image.Type.Simple;
            return image;
        }

        public Button CreateHitButton(string name, Transform parent, Vector2 anchor, Vector2 position,
            Vector2 size, Action action)
        {
            Image image = CreateImage(name, parent, Color.clear, anchor, anchor, position, size, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            // Invisible pointer hit areas are deliberately excluded from focus navigation. Their
            // corresponding visible controls remain keyboard/gamepad navigable.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (action != null)
                button.onClick.AddListener(() => action());
            return button;
        }

        static void SelectIfNothingFocused(Button button)
        {
            EventSystem events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject != null || button == null
                || !button.IsActive() || !button.IsInteractable()) return;
            events.SetSelectedGameObject(button.gameObject);
        }

        public void CreateBadge(Transform parent, Vector2 anchor, Vector2 position, string value)
        {
            Vector2 size = new Vector2(value == "NEW" ? 48 : 28, 28);
            Image badge = CreateImage("Badge " + value, parent, Theme.Pink, anchor, anchor, position, size);
            badge.sprite = CircleSprite();
            CreateText("Badge Label", value, badge.transform, 11, FontStyle.Bold, Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        }

        public Sprite SpriteFromTexture(Texture2D texture)
        {
            if (texture == null) return null;
            if (TextureSprites.TryGetValue(texture, out Sprite cached) && cached != null) return cached;
            Sprite created = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100);
            TextureSprites[texture] = created;
            return created;
        }

        static Sprite RoundedSprite()
        {
            if (roundedSprite != null) return roundedSprite;
            const int size = 128;
            const int radius = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Urban Fantasy Panel",
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 1, 0, x - (size - radius));
                float dy = Mathf.Max(radius - y - 1, 0, y - (size - radius));
                texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? Color.white : Color.clear);
            }
            texture.Apply();
            roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return roundedSprite;
        }

        static Sprite CircleSprite()
        {
            if (circleSprite != null) return circleSprite;
            const int size = 256;
            float center = (size - 1) * .5f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Circle UI HD",
                filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(center - distance + 1)));
            }
            texture.Apply();
            circleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect, new Vector4(size / 2f, size / 2f, size / 2f, size / 2f));
            return circleSprite;
        }
    }
}
