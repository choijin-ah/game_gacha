namespace StarfallAcademy.Lobby
{
    // 로비 문구와 메뉴 이름은 이 파일만 수정하면 됩니다.
    public static class LobbyContent
    {
        public const string TeacherName = "선생님";
        public const string HeroName = "아리아";
        public const string HeroRole = "학생회 특무부";

        public static readonly DialogueLine[] Dialogues =
        {
            new DialogueLine("선생님, 오늘 일정도 제가 도와드릴게요.", "기본"),
            new DialogueLine("무리하면 안 돼요. 잠깐이라도 쉬었다 가요.", "걱정"),
            new DialogueLine("별이 유난히 밝네요. 같이 보고 가실래요?", "미소"),
            new DialogueLine("새 임무가 도착했어요. 준비되시면 출발하죠.", "진지"),
            new DialogueLine("카페에 신메뉴가 나왔다던데… 조금 궁금해요.", "기대"),
            new DialogueLine("불러 주셨나요? 언제든 곁에 있을게요.", "호감")
        };

        public static readonly BannerInfo[] Banners =
        {
            new BannerInfo("PICK UP", "별빛 아래의 약속", "SSR 아리아 출현 확률 UP  ·  7/24까지"),
            new BannerInfo("EVENT", "한여름의 청춘 기록", "이벤트 스토리 & 한정 코스튬 오픈"),
            new BannerInfo("UPDATE", "메인 스토리 4장", "잊힌 교정의 비밀을 확인하세요")
        };

        public static readonly MenuInfo[] QuickMenus =
        {
            new MenuInfo(LobbyIcon.Formation, "편성", "부대 편성", "출격할 학생과 진형을 구성합니다.\n\n현재 편성 전투력  24,860\n리더  아리아  ·  4명 편성 중"),
            new MenuInfo(LobbyIcon.Student, "학생", "학생 목록", "보유 학생과 성장 상태를 확인합니다.\n\n보유 학생  36 / 80\n성장 가능 학생  4명"),
            new MenuInfo(LobbyIcon.Cafe, "카페", "별빛 카페", "학생들이 쉬고 있는 카페입니다.\n\n받을 수 있는 인연 포인트가 있습니다."),
            new MenuInfo(LobbyIcon.Guild, "서클", "서클 로비", "서클 멤버의 소식과 협동 임무를 확인합니다.\n\n오늘의 출석  18 / 24"),
            new MenuInfo(LobbyIcon.Schedule, "스케줄", "오늘의 스케줄", "학생과 함께 일정을 진행해 인연도를 높입니다.\n\n남은 티켓  3장")
        };

        public static readonly NavigationInfo[] Navigation =
        {
            new NavigationInfo("⌂", "로비"),
            new NavigationInfo("⚔", "임무"),
            new NavigationInfo("✦", "모집", true),
            new NavigationInfo("▣", "상점"),
            new NavigationInfo("⋯", "메뉴")
        };
    }

    public sealed class DialogueLine
    {
        public readonly string Text;
        public readonly string Mood;
        public DialogueLine(string text, string mood) { Text = text; Mood = mood; }
    }

    public sealed class BannerInfo
    {
        public readonly string Tag;
        public readonly string Title;
        public readonly string Body;
        public BannerInfo(string tag, string title, string body) { Tag = tag; Title = title; Body = body; }
    }

    public sealed class MenuInfo
    {
        public readonly LobbyIcon Icon;
        public readonly string Label;
        public readonly string PopupTitle;
        public readonly string PopupBody;
        public MenuInfo(LobbyIcon icon, string label, string popupTitle, string popupBody)
        {
            Icon = icon; Label = label; PopupTitle = popupTitle; PopupBody = popupBody;
        }
    }

    public sealed class NavigationInfo
    {
        public readonly string Icon;
        public readonly string Label;
        public readonly bool IsNew;
        public NavigationInfo(string icon, string label, bool isNew = false)
        {
            Icon = icon; Label = label; IsNew = isNew;
        }
    }
}
