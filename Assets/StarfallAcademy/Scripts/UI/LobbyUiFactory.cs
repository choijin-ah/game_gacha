using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    public enum StarfallButtonStyle
    {
        Standard,
        Primary,
        Secondary,
        Warning,
        Danger,
        Tab,
        Icon
    }

    public enum StarfallStatusTone
    {
        Info,
        Success,
        Warning,
        Danger,
        Premium
    }

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
                colors.highlightedColor = Color.Lerp(color, UrbanFantasyStyle.Cyan, .18f);
                colors.pressedColor = Color.Lerp(color, Color.black, .24f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(color.r * .42f, color.g * .42f,
                    color.b * .48f, Mathf.Min(color.a, .56f));
                colors.fadeDuration = .08f;
                button.colors = colors;
            }
            if (action != null)
                button.onClick.AddListener(() => action());

            Text buttonLabel = CreateText("Label", label, image.transform, fontSize,
                FontStyle.Bold, Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, alignment);
            Shadow shadow = buttonLabel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .55f);
            shadow.effectDistance = new Vector2(0, -1.5f);
            image.gameObject.AddComponent<UiPressFeedback>();
            SelectIfNothingFocused(button);
            return image.gameObject;
        }

        public GameObject CreateStyledButton(string name, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, string label, int fontSize,
            StarfallButtonStyle style, Action action,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Color color;
            Color border;
            switch (style)
            {
                case StarfallButtonStyle.Primary:
                    color = new Color(.22f, .15f, .52f, .98f);
                    border = UrbanFantasyStyle.StrongLine;
                    break;
                case StarfallButtonStyle.Secondary:
                    color = UrbanFantasyStyle.PanelSoft;
                    border = UrbanFantasyStyle.Line;
                    break;
                case StarfallButtonStyle.Warning:
                    color = new Color(.46f, .25f, .07f, .98f);
                    border = UrbanFantasyStyle.Warning;
                    break;
                case StarfallButtonStyle.Danger:
                    color = new Color(.43f, .07f, .12f, .98f);
                    border = UrbanFantasyStyle.Danger;
                    break;
                case StarfallButtonStyle.Tab:
                    color = new Color(.08f, .10f, .20f, .94f);
                    border = UrbanFantasyStyle.Line;
                    break;
                case StarfallButtonStyle.Icon:
                    color = new Color(.06f, .08f, .16f, .90f);
                    border = UrbanFantasyStyle.Line;
                    break;
                default:
                    color = UrbanFantasyStyle.PanelStrong;
                    border = UrbanFantasyStyle.Line;
                    break;
            }

            GameObject button = CreateButton(name, parent, anchor, position, size, label,
                fontSize, color, action, alignment);
            UrbanFantasyStyle.AddBorder(this, button.GetComponent<RectTransform>(), border);
            if (style == StarfallButtonStyle.Primary)
            {
                Image glow = CreateImage("Primary Glow", button.transform,
                    new Color(UrbanFantasyStyle.Cyan.r, UrbanFantasyStyle.Cyan.g,
                        UrbanFantasyStyle.Cyan.b, .13f),
                    Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6, -6));
                glow.transform.SetAsFirstSibling();
            }
            return button;
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

        public GameObject CreateBadge(Transform parent, Vector2 anchor, Vector2 position, string value)
        {
            value ??= string.Empty;
            bool capsule = string.Equals(value, "NEW", StringComparison.OrdinalIgnoreCase)
                || value.Length > 2;
            Vector2 size = new Vector2(capsule ? Mathf.Max(52, value.Length * 12 + 20) : 30, 30);
            Image badge = CreateImage("Badge " + value, parent,
                capsule ? UrbanFantasyStyle.Alert : UrbanFantasyStyle.Violet,
                anchor, anchor, position, size);
            if (!capsule)
            {
                badge.sprite = CircleSprite();
                Image seal = CreateCircleImage("Seal Ring", badge.transform,
                    new Color(UrbanFantasyStyle.Cyan.r, UrbanFantasyStyle.Cyan.g,
                        UrbanFantasyStyle.Cyan.b, .78f),
                    new Vector2(.5f, .5f), Vector2.zero, size + new Vector2(5, 5));
                seal.transform.SetAsFirstSibling();
            }
            else
            {
                UrbanFantasyStyle.AddBorder(this, badge.rectTransform,
                    new Color(1f, .58f, .67f, .72f));
            }
            CreateText("Badge Label", value, badge.transform, 11, FontStyle.Bold, Theme.White,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            return badge.gameObject;
        }

        public RectTransform CreateCard(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size, bool emphasized = false)
        {
            Image card = CreateImage(name, parent,
                emphasized ? UrbanFantasyStyle.PanelStrong : UrbanFantasyStyle.PanelSoft,
                anchorMin, anchorMax, position, size);
            UrbanFantasyStyle.AddBorder(this, card.rectTransform,
                emphasized ? UrbanFantasyStyle.StrongLine : UrbanFantasyStyle.Line);
            if (emphasized)
                CreateImage("Card Accent", card.transform, UrbanFantasyStyle.Cyan,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2),
                    new Vector2(-24, 2));
            return card.rectTransform;
        }

        public Image CreateProgressBar(string name, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, float normalizedValue, Color? fillColor = null)
        {
            Image track = CreateImage(name, parent, new Color(.02f, .03f, .07f, .92f),
                anchor, anchor, position, size);
            UrbanFantasyStyle.AddBorder(this, track.rectTransform, UrbanFantasyStyle.Line);
            Image fill = CreateImage("Fill", track.transform,
                fillColor ?? UrbanFantasyStyle.Cyan, Vector2.zero,
                new Vector2(Mathf.Clamp01(normalizedValue), 1), Vector2.zero,
                new Vector2(-4, -4));
            fill.rectTransform.offsetMin = new Vector2(2, 2);
            fill.rectTransform.offsetMax = new Vector2(-2, -2);
            return fill;
        }

        public GameObject CreateStatusPill(string name, Transform parent, Vector2 anchor,
            Vector2 position, string label, StarfallStatusTone tone)
        {
            Color color = tone switch
            {
                StarfallStatusTone.Success => UrbanFantasyStyle.Success,
                StarfallStatusTone.Warning => UrbanFantasyStyle.Warning,
                StarfallStatusTone.Danger => UrbanFantasyStyle.Danger,
                StarfallStatusTone.Premium => UrbanFantasyStyle.Gold,
                _ => UrbanFantasyStyle.Info
            };
            float width = Mathf.Clamp((label == null ? 0 : label.Length) * 12 + 30, 64, 220);
            Image pill = CreateImage(name, parent,
                new Color(color.r * .24f, color.g * .24f, color.b * .24f, .96f),
                anchor, anchor, position, new Vector2(width, 28));
            UrbanFantasyStyle.AddBorder(this, pill.rectTransform,
                new Color(color.r, color.g, color.b, .72f));
            CreateText("Label", label ?? string.Empty, pill.transform, 11, FontStyle.Bold,
                color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                TextAnchor.MiddleCenter);
            return pill.gameObject;
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
