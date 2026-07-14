using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // 전체 색상과 폰트는 이 파일에서 한 번에 바꿀 수 있습니다.
    public sealed class LobbyTheme
    {
        static Font cachedFont;
        public readonly Color Navy = Hex("070B18");
        public readonly Color Panel = Hex("11182B");
        public readonly Color Cyan = Hex("53D7FF");
        public readonly Color Pink = Hex("E54863");
        public readonly Color Gold = Hex("E7C878");
        public readonly Color White = Hex("F0F2FA");
        public readonly Color Muted = new Color(.65f, .68f, .77f, .72f);
        public readonly Font Font;

        public LobbyTheme()
        {
            if (cachedFont == null) cachedFont = CreateRuntimeFont();
            Font = cachedFont;
        }

        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        static Font CreateRuntimeFont()
        {
            string[] bundledPaths =
            {
                "Fonts/StarfallKorean",
                "Fonts/Pretendard-Regular",
                "Fonts/NotoSansKR-Regular",
                "Fonts/NotoSerifKR-Regular"
            };
            foreach (string path in bundledPaths)
            {
                Font bundled = Resources.Load<Font>(path);
                if (bundled != null) return bundled;
            }

            // Also accept a differently named font placed under any Resources/Fonts folder.
            Font[] bundledFonts = Resources.LoadAll<Font>("Fonts");
            if (bundledFonts != null && bundledFonts.Length > 0) return bundledFonts[0];

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
