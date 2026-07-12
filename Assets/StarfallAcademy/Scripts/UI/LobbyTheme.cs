using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // 전체 색상과 폰트는 이 파일에서 한 번에 바꿀 수 있습니다.
    public sealed class LobbyTheme
    {
        public readonly Color Navy = Hex("09090D");
        public readonly Color Panel = Hex("17171D");
        public readonly Color Cyan = Hex("D7D7DE");
        public readonly Color Pink = Hex("B80C0A");
        public readonly Color Gold = Hex("D0B478");
        public readonly Color White = Hex("E9E9ED");
        public readonly Color Muted = new Color(.82f, .82f, .86f, .58f);
        public readonly Font Font;

        public LobbyTheme()
        {
            Font = CreateRuntimeFont();
        }

        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        static Font CreateRuntimeFont()
        {
            string[] preferred =
            {
                "KoPub Batang", "Noto Serif CJK KR", "Noto Serif KR", "Batang", "BatangChe",
                "Pretendard", "Noto Sans KR", "Malgun Gothic", "Arial"
            };
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in preferred)
                foreach (string available in installed)
                    if (string.Equals(candidate, available, StringComparison.OrdinalIgnoreCase))
                        return Font.CreateDynamicFontFromOSFont(available, 48);

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
