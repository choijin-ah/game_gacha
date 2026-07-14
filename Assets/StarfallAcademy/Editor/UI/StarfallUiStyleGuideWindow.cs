using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class StarfallUiStyleGuideWindow : EditorWindow
    {
        enum GuideTab
        {
            Palette,
            Components,
            Screens,
            Assets
        }

        readonly struct ScreenRow
        {
            public ScreenRow(string name, string purpose, string path)
            {
                Name = name;
                Purpose = purpose;
                Path = path;
            }

            public string Name { get; }
            public string Purpose { get; }
            public string Path { get; }
        }

        readonly struct AssetRow
        {
            public AssetRow(string label, string path, bool optional = false)
            {
                Label = label;
                Path = path;
                Optional = optional;
            }

            public string Label { get; }
            public string Path { get; }
            public bool Optional { get; }
        }

        static readonly ScreenRow[] Screens =
        {
            new ScreenRow("Lobby", "허브·재화·동적 배지·출석·우편",
                "Assets/StarfallAcademy/Scripts/Views/GothicLobbyView.cs"),
            new ScreenRow("Formation", "프리셋·역할 필터·정렬·팀 요약",
                "Assets/StarfallAcademy/Scripts/Formation/FormationScreen.cs"),
            new ScreenRow("Character Archive", "보유/속성 필터·성장·장비·각성",
                "Assets/StarfallAcademy/Scripts/CharacterArchive/CharacterArchiveScreen.cs"),
            new ScreenRow("Gacha", "다중 배너·확률·기록·천장·결과 연출",
                "Assets/StarfallAcademy/Scripts/Gacha/GachaScreen.cs"),
            new ScreenRow("Shop", "재화·행동력·성장 재료 교환",
                "Assets/StarfallAcademy/Scripts/Shop/ShopScreen.cs"),
            new ScreenRow("Story Archive", "카테고리·에피소드·비주얼 노벨",
                "Assets/StarfallAcademy/Scripts/Story/Runtime/StoryArchiveScreen.cs"),
            new ScreenRow("Stage Select", "카테고리·보상·별·편성·소탕",
                "Assets/StarfallAcademy/Scripts/Battle/StageSelectScreen.cs"),
            new ScreenRow("Turn Battle", "턴 순서·AUTO 전략·상태 툴팁·결과",
                "Assets/StarfallAcademy/Scripts/Battle/TurnBattleScreen.cs"),
            new ScreenRow("Weekly Boss", "난이도·점수·보상 구간·도전 횟수",
                "Assets/StarfallAcademy/Scripts/WeeklyBoss/WeeklyBossScreen.cs"),
            new ScreenRow("Challenge Tower", "층·특수 규칙·별·최초 보상",
                "Assets/StarfallAcademy/Scripts/Tower/ChallengeTowerScreen.cs")
        };

        static readonly AssetRow[] Assets =
        {
            new AssetRow("로비 키비주얼",
                "Assets/StarfallAcademy/Resources/Lobby/Art/lobby_urban_fantasy_v1.png"),
            new AssetRow("로비 대체 키비주얼",
                "Assets/StarfallAcademy/Resources/Lobby/Art/lobby_hero_v2.png"),
            new AssetRow("공통 버튼 상태 시트",
                "Assets/StarfallAcademy/Resources/Lobby/UI/button_states_v1.png"),
            new AssetRow("공통 아이콘 시트",
                "Assets/StarfallAcademy/Resources/Lobby/UI/lobby_icons_v2.png"),
            new AssetRow("가챠 포털 배경",
                "Assets/StarfallAcademy/Resources/Gacha/Art/gacha_portal_v1.png"),
            new AssetRow("기본 스킬 아이콘",
                "Assets/StarfallAcademy/Resources/CharacterArchive/UI/default_skill_icons_v1.png"),
            new AssetRow("주간 보스 키비주얼", string.Empty, true),
            new AssetRow("도전의 탑 키비주얼", string.Empty, true),
            new AssetRow("희귀도별 가챠 이펙트", string.Empty, true)
        };

        GuideTab tab;
        Vector2 scroll;

        [MenuItem("Starfall/UI/Style Guide", priority = 250)]
        public static void Open()
        {
            var window = GetWindow<StarfallUiStyleGuideWindow>("UI Style Guide");
            window.minSize = new Vector2(720, 560);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("STARFALL ACADEMY · UI STYLE GUIDE",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "어반 판타지 런타임 uGUI의 색상, 공통 컴포넌트, 화면 연결, 이미지 에셋 상태를 한곳에서 확인합니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);
            tab = (GuideTab)GUILayout.Toolbar((int)tab,
                new[] { "Palette", "Components", "Screens", "Assets" });
            EditorGUILayout.Space(8);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case GuideTab.Components: DrawComponents(); break;
                case GuideTab.Screens: DrawScreens(); break;
                case GuideTab.Assets: DrawAssets(); break;
                default: DrawPalette(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        static void DrawPalette()
        {
            EditorGUILayout.HelpBox(
                "딥 네이비를 베이스로 바이올렛·시안을 인터랙션에, 골드를 희귀/보상 강조에 사용합니다.",
                MessageType.Info);
            DrawSwatch("Backdrop", UrbanFantasyStyle.Backdrop, "전체 화면 딤·로딩 배경");
            DrawSwatch("Panel", UrbanFantasyStyle.Panel, "기본 정보 패널");
            DrawSwatch("Panel Strong", UrbanFantasyStyle.PanelStrong, "모달·중요 카드");
            DrawSwatch("Violet", UrbanFantasyStyle.Violet, "주 액션·마도 포인트");
            DrawSwatch("Cyan", UrbanFantasyStyle.Cyan, "정보·선택·포커스");
            DrawSwatch("Gold", UrbanFantasyStyle.Gold, "5성·프리미엄·핵심 보상");
            DrawSwatch("Success", UrbanFantasyStyle.Success, "성공·수령 완료");
            DrawSwatch("Warning", UrbanFantasyStyle.Warning, "기한 임박·주의");
            DrawSwatch("Danger", UrbanFantasyStyle.Danger, "실패·파괴적 액션");
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Typography", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Title 30–32 · Section 18–20 · Body 13–16 · Caption 10–12");
        }

        static void DrawSwatch(string label, Color color, string usage)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            Rect swatch = GUILayoutUtility.GetRect(56, 34, GUILayout.Width(56));
            EditorGUI.DrawRect(swatch, color);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(usage, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.SelectableLabel("#" + ColorUtility.ToHtmlStringRGBA(color),
                GUILayout.Width(90), GUILayout.Height(18));
            EditorGUILayout.EndHorizontal();
        }

        static void DrawComponents()
        {
            DrawComponentGroup("Buttons", new[]
            {
                "Standard · 일반 확인/이동", "Primary · 전투 시작/저장/수령",
                "Secondary · 취소/보조", "Warning · 비용/기한 주의",
                "Danger · 초기화/삭제", "Tab · 필터/카테고리", "Icon · 뒤로/닫기/정렬"
            });
            DrawComponentGroup("Cards", new[]
            {
                "Character · 초상/희귀도/속성/역할/레벨/전투력",
                "Stage · 잠금/별/행동력/권장 전투력", "Reward · 아이콘/수량/신규",
                "Banner · 타입/종료 시간/천장", "Equipment · 슬롯/등급/강화/세트"
            });
            DrawComponentGroup("Feedback", new[]
            {
                "Semantic Toast · Info/Success/Warning/Danger/Premium",
                "Modal · 단일 확인 또는 확인/취소", "Seal Badge · NEW/미수령/개수",
                "Async Loading · 진행률/팁/목적지", "Tooltip · Hover/Long Press"
            });
        }

        static void DrawComponentGroup(string title, IEnumerable<string> rows)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            foreach (string row in rows) EditorGUILayout.LabelField("• " + row);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        static void DrawScreens()
        {
            for (int i = 0; i < Screens.Length; i++)
            {
                ScreenRow row = Screens[i];
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                EditorGUILayout.LabelField(row.Name, EditorStyles.boldLabel,
                    GUILayout.Width(150));
                EditorGUILayout.LabelField(row.Purpose, GUILayout.MinWidth(320));
                if (GUILayout.Button("코드 선택", GUILayout.Width(80)))
                    SelectAsset(row.Path);
                EditorGUILayout.EndHorizontal();
            }
        }

        static void DrawAssets()
        {
            int present = 0;
            for (int i = 0; i < Assets.Length; i++)
                if (!string.IsNullOrEmpty(Assets[i].Path)
                    && AssetDatabase.LoadAssetAtPath<Object>(Assets[i].Path) != null) present++;
            EditorGUILayout.HelpBox("연결된 핵심 이미지 " + present + " / " + Assets.Length
                + " · 선택 에셋은 플레이스홀더 없이 추가 제작할 수 있습니다.", MessageType.Info);
            for (int i = 0; i < Assets.Length; i++)
            {
                AssetRow row = Assets[i];
                Object asset = string.IsNullOrEmpty(row.Path) ? null
                    : AssetDatabase.LoadAssetAtPath<Object>(row.Path);
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                GUIStyle state = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = asset != null ? new Color(.2f, .65f, .4f)
                        : row.Optional ? new Color(.85f, .55f, .2f) : new Color(.8f, .25f, .25f) }
                };
                EditorGUILayout.LabelField(asset != null ? "READY" : row.Optional ? "OPTIONAL" : "MISSING",
                    state, GUILayout.Width(80));
                EditorGUILayout.LabelField(row.Label, GUILayout.Width(220));
                EditorGUILayout.SelectableLabel(string.IsNullOrEmpty(row.Path) ? "제작 시 연결" : row.Path,
                    GUILayout.Height(18));
                using (new EditorGUI.DisabledScope(asset == null))
                    if (GUILayout.Button("선택", GUILayout.Width(58))) SelectAsset(row.Path);
                EditorGUILayout.EndHorizontal();
            }
        }

        static void SelectAsset(string path)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
