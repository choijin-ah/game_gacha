using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum LobbyIcon
    {
        Mail,
        Settings,
        Formation,
        Student,
        Cafe,
        Guild,
        Schedule,
        Mission,
        Summon
    }

    // AI로 만든 UI 아틀라스를 런타임 Sprite로 잘라 제공하는 곳입니다.
    public sealed class LobbyUiAssets
    {
        readonly Dictionary<LobbyIcon, Sprite> icons = new Dictionary<LobbyIcon, Sprite>();

        public Sprite ButtonNormal { get; }
        public Sprite ButtonHighlighted { get; }
        public Sprite ButtonPressed { get; }
        public Sprite ButtonSelected { get; }
        public bool HasButtonSkin => ButtonNormal != null;

        public LobbyUiAssets()
        {
            Texture2D buttons = Resources.Load<Texture2D>("Lobby/UI/button_states_v1");
            if (buttons != null)
            {
                // 생성된 2x2 시트의 투명 여백을 제외한 실제 버튼 영역입니다.
                ButtonNormal = CreateButtonSprite(buttons, .020f, .660f, .455f, .165f);
                ButtonHighlighted = CreateButtonSprite(buttons, .516f, .660f, .466f, .165f);
                ButtonPressed = CreateButtonSprite(buttons, .020f, .198f, .455f, .170f);
                ButtonSelected = CreateButtonSprite(buttons, .516f, .183f, .466f, .192f);
            }

            Texture2D iconAtlas = Resources.Load<Texture2D>("Lobby/UI/lobby_icons_v2");
            if (iconAtlas == null)
                iconAtlas = Resources.Load<Texture2D>("Lobby/UI/lobby_icons_v1");
            if (iconAtlas != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    int column = i % 3;
                    int rowFromTop = i / 3;
                    float cellWidth = iconAtlas.width / 3f;
                    float cellHeight = iconAtlas.height / 3f;
                    var rect = new Rect(column * cellWidth, (2 - rowFromTop) * cellHeight, cellWidth, cellHeight);
                    icons[(LobbyIcon)i] = Sprite.Create(iconAtlas, rect, new Vector2(.5f, .5f), 100,
                        0, SpriteMeshType.FullRect);
                }
            }
        }

        public Sprite GetIcon(LobbyIcon icon)
        {
            icons.TryGetValue(icon, out Sprite sprite);
            return sprite;
        }

        static Sprite CreateButtonSprite(Texture2D texture, float x, float y, float width, float height)
        {
            var rect = new Rect(
                Mathf.Round(texture.width * x),
                Mathf.Round(texture.height * y),
                Mathf.Round(texture.width * width),
                Mathf.Round(texture.height * height));
            float horizontalBorder = Mathf.Min(64, rect.width * .12f);
            float verticalBorder = Mathf.Min(48, rect.height * .24f);
            return Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect,
                new Vector4(horizontalBorder, verticalBorder, horizontalBorder, verticalBorder));
        }
    }
}
