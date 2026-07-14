using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarfallAcademy.Lobby
{
    /// <summary>Four-way story archive and entry point for the in-scene visual novel player.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class StoryArchiveScreen : MonoBehaviour
    {
        sealed class EpisodeRowState
        {
            public StoryEpisode Episode;
            public Text Title;
            public Text State;
            public Text Open;
            public Image Fill;
            public Image Selection;
            public Image Thumbnail;
        }

        readonly List<EpisodeRowState> rows = new List<EpisodeRowState>();
        LobbyUiFactory ui;
        StoryDatabase database;
        RectTransform safeRoot;
        StoryEpisode selectedMain;
        Image heroBanner;
        Image heroCharacter;
        Text heroEyebrow;
        Text heroTitle;
        Text heroSummary;
        Text heroProgress;
        Image heroProgressFill;
        Text heroPlayLabel;
        Text archiveStatus;
        VisualNovelPlayer player;
        bool changingScene;

        void Awake()
        {
            BuildCanvas();
            BuildScreen();
        }

        void Update()
        {
            if (changingScene || player != null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) ReturnToLobby();
        }

        void BuildCanvas()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            scaler.referencePixelsPerUnit = 100;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildScreen()
        {
            ui = new LobbyUiFactory(new LobbyTheme());
            database = Resources.Load<StoryDatabase>("Data/StoryDatabase");
            RectTransform root = (RectTransform)transform;
            BuildBackground(root);
            safeRoot = CreateLayer("Safe Area", root);
            safeRoot.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform workspace = ui.CreateImage("Story Archive Workspace", safeRoot,
                new Color(.006f, .006f, .011f, .91f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(1840, 1000), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, workspace, UrbanFantasyStyle.StrongLine);
            BuildHeader(workspace);
            BuildMainArchive(workspace);
            BuildSideArchive(workspace);
            RefreshProgress();
        }

        void BuildBackground(RectTransform root)
        {
            Image image = ui.CreateImage("Archive Background", root, LobbyTheme.Hex("080910"),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Texture2D art = Resources.Load<Texture2D>("Lobby/Art/lobby_urban_fantasy_v1");
            if (art == null) art = Resources.Load<Texture2D>("Lobby/Art/lobby_hero_v2");
            if (art != null)
            {
                image.sprite = ui.SpriteFromTexture(art);
                image.type = Image.Type.Simple;
                image.color = new Color(.54f, .56f, .62f, 1f);
            }
            ui.CreateImage("Archive Dim", root, new Color(.001f, .001f, .004f, .72f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void BuildHeader(RectTransform workspace)
        {
            ui.CreateImage("Header Accent", workspace, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -3), new Vector2(-58, 2));
            GameObject back = ui.CreateButton("Back To Lobby", workspace, new Vector2(0, 1),
                new Vector2(48, -48), new Vector2(54, 54), "‹", 31,
                UrbanFantasyStyle.PanelStrong, ReturnToLobby);
            UrbanFantasyStyle.AddBorder(ui, back.GetComponent<RectTransform>());
            ui.CreateText("Archive English", "S T O R Y   A R C H I V E", workspace, 11,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(260, -30), new Vector2(360, 22), TextAnchor.MiddleLeft);
            ui.CreateText("Archive Title", "기록실", workspace, 34, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(260, -67), new Vector2(360, 42), TextAnchor.MiddleLeft);
            archiveStatus = ui.CreateText("Archive Status", string.Empty, workspace, 13, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-280, -50), new Vector2(500, 28), TextAnchor.MiddleRight);
            ui.CreateImage("Header Divider", workspace, UrbanFantasyStyle.Line,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -100), new Vector2(-52, 1));
        }

        void BuildMainArchive(RectTransform workspace)
        {
            RectTransform panel = ui.CreateImage("Main Story Section", workspace, UrbanFantasyStyle.Panel,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-315, -42),
                new Vector2(1170, 820), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, panel);
            ui.CreateText("Main Section Title", "MAIN STORY", panel, 22, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(130, -35), new Vector2(220, 36), TextAnchor.MiddleLeft);
            ui.CreateText("Main Section Korean", "메인 스토리", panel, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-100, -35), new Vector2(180, 28), TextAnchor.MiddleRight);

            BuildHero(panel);
            BuildMainEpisodeStrip(panel);
        }

        void BuildHero(RectTransform panel)
        {
            RectTransform hero = ui.CreateImage("Main Story Hero", panel, LobbyTheme.Hex("11121A"),
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -315),
                new Vector2(1110, 500), true).rectTransform;
            hero.gameObject.AddComponent<RectMask2D>();
            UrbanFantasyStyle.AddBorder(ui, hero, UrbanFantasyStyle.StrongLine);
            heroBanner = ui.CreateImage("Banner", hero, LobbyTheme.Hex("11121A"),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            heroBanner.type = Image.Type.Simple;
            heroBanner.preserveAspect = false;
            ui.CreateImage("Banner Dim", hero, new Color(.003f, .003f, .008f, .58f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ui.CreateImage("Banner Gradient", hero, new Color(.003f, .003f, .008f, .3f),
                new Vector2(0, 0), new Vector2(.62f, 1), Vector2.zero, Vector2.zero);

            heroCharacter = ui.CreateImage("Focus Character", hero, Color.clear,
                new Vector2(.79f, 0), new Vector2(.79f, 0), new Vector2(0, 14),
                new Vector2(470, 500));
            heroCharacter.rectTransform.pivot = new Vector2(.5f, 0);
            heroCharacter.type = Image.Type.Simple;
            heroCharacter.preserveAspect = true;
            heroEyebrow = ui.CreateText("Hero Eyebrow", string.Empty, hero, 12, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(235, -70), new Vector2(390, 25), TextAnchor.MiddleLeft);
            heroTitle = ui.CreateText("Hero Title", string.Empty, hero, 33, FontStyle.Normal,
                UrbanFantasyStyle.Silver, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(300, -118), new Vector2(520, 54), TextAnchor.MiddleLeft);
            heroSummary = ui.CreateText("Hero Summary", string.Empty, hero, 16, FontStyle.Normal,
                new Color(.88f, .88f, .91f, .78f), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(316, -206), new Vector2(550, 96), TextAnchor.UpperLeft);
            heroSummary.horizontalOverflow = HorizontalWrapMode.Wrap;
            heroSummary.verticalOverflow = VerticalWrapMode.Truncate;

            ui.CreateImage("Hero Progress Track", hero, new Color(1, 1, 1, .12f),
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(230, 78), new Vector2(380, 3));
            heroProgressFill = ui.CreateImage("Hero Progress Fill", hero, UrbanFantasyStyle.Silver,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 78), new Vector2(0, 3));
            heroProgressFill.rectTransform.pivot = new Vector2(0, .5f);
            heroProgress = ui.CreateText("Hero Progress", string.Empty, hero, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(230, 104), new Vector2(380, 22), TextAnchor.MiddleLeft);
            GameObject play = ui.CreateButton("Read Main Story", hero, new Vector2(0, 0),
                new Vector2(178, 37), new Vector2(276, 58), "스토리 보기   ›", 17,
                new Color(.075f, .075f, .09f, .96f), PlaySelectedMain, TextAnchor.MiddleCenter);
            UrbanFantasyStyle.AddBorder(ui, play.GetComponent<RectTransform>(), UrbanFantasyStyle.StrongLine);
            heroPlayLabel = play.transform.Find("Label")?.GetComponent<Text>();
        }

        void BuildMainEpisodeStrip(RectTransform panel)
        {
            ui.CreateText("Episode List Title", "EPISODE LIST", panel, 11, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(105, 161), new Vector2(160, 22), TextAnchor.MiddleLeft);
            RectTransform viewport = ui.CreateImage("Main Episode Viewport", panel, Color.clear,
                new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 77),
                new Vector2(1110, 132), true).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateHorizontalScroll(viewport, out ScrollRect scroll);

            IReadOnlyList<StoryEpisode> episodes = database != null
                ? database.GetEpisodes(StoryCategory.Main) : Array.Empty<StoryEpisode>();
            if (episodes.Count == 0)
            {
                CreateEmptyNotice(viewport, "등록된 메인 스토리 없음\nStarfall > Story > Visual Novel Editor");
                SetHero(null);
                return;
            }
            foreach (StoryEpisode episode in episodes) CreateMainEpisodeCard(content, episode);
            selectedMain = FirstUnlocked(episodes) ?? episodes[0];
            SetHero(selectedMain);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.horizontalNormalizedPosition = 0f;
        }

        void CreateMainEpisodeCard(RectTransform content, StoryEpisode episode)
        {
            bool unlocked = StoryProgressService.IsUnlocked(database, episode);
            GameObject row = ui.CreateButton("Main " + episode.Id, content, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(248, 122), string.Empty, 14,
                new Color(.022f, .022f, .032f, .94f), () => SelectMain(episode));
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredWidth = 248;
            layout.preferredHeight = 122;
            UrbanFantasyStyle.AddBorder(ui, row.GetComponent<RectTransform>());
            Image selection = ui.CreateImage("Selection", row.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-4, -4));
            selection.transform.SetAsFirstSibling();
            ui.CreateText("Index", "CHAPTER " + Mathf.Max(1, episode.SortOrder).ToString("00"), row.transform,
                10, FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -22), new Vector2(-26, 20), TextAnchor.MiddleLeft);
            Text title = ui.CreateText("Title", unlocked ? episode.Title : "미해금 기록", row.transform, 16,
                FontStyle.Normal, unlocked ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, 6),
                new Vector2(-26, 34), TextAnchor.MiddleLeft);
            Text state = ui.CreateText("State", string.Empty, row.transform, 10, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 20), new Vector2(-26, 18), TextAnchor.MiddleLeft);
            Image fill = CreateMiniProgress(row.transform, 11f, 11f, 7f);
            rows.Add(new EpisodeRowState
            {
                Episode = episode,
                Title = title,
                State = state,
                Fill = fill,
                Selection = selection
            });
        }

        void BuildSideArchive(RectTransform workspace)
        {
            RectTransform column = ui.CreateImage("Other Stories", workspace, new Color(.008f, .008f, .014f, .55f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(610, -42),
                new Vector2(520, 820), true).rectTransform;
            BuildCategorySection(column, StoryCategory.Event, "EVENT STORY", "이벤트 스토리", -132);
            BuildCategorySection(column, StoryCategory.Character, "CHARACTER STORY", "캐릭터 스토리", -410);
            BuildCategorySection(column, StoryCategory.Side, "SIDE STORY", "사이드 스토리", -688);
        }

        void BuildCategorySection(RectTransform column, StoryCategory category, string english, string korean,
            float y)
        {
            RectTransform section = ui.CreateImage(english, column, UrbanFantasyStyle.Panel,
                new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, y),
                new Vector2(520, 258), true).rectTransform;
            UrbanFantasyStyle.AddBorder(ui, section);
            ui.CreateText("English", english, section, 17, FontStyle.Normal, UrbanFantasyStyle.Silver,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -31),
                new Vector2(200, 30), TextAnchor.MiddleLeft);
            ui.CreateText("Korean", korean, section, 11, FontStyle.Normal, UrbanFantasyStyle.Muted,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-82, -31),
                new Vector2(130, 24), TextAnchor.MiddleRight);
            ui.CreateImage("Divider", section, UrbanFantasyStyle.Line, new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(0, -58), new Vector2(-24, 1));

            RectTransform viewport = ui.CreateImage("Viewport", section, Color.clear,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -26),
                new Vector2(490, 176), true).rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateVerticalScroll(viewport, out ScrollRect scroll);
            IReadOnlyList<StoryEpisode> episodes = database != null
                ? database.GetEpisodes(category) : Array.Empty<StoryEpisode>();
            if (episodes.Count == 0)
            {
                CreateEmptyNotice(viewport, "등록된 스토리 없음\nStarfall > Story > Visual Novel Editor", 13);
                return;
            }
            foreach (StoryEpisode episode in episodes) CreateSideEpisodeRow(content, episode);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scroll.verticalNormalizedPosition = 1f;
        }

        void CreateSideEpisodeRow(RectTransform content, StoryEpisode episode)
        {
            bool unlocked = StoryProgressService.IsUnlocked(database, episode);
            GameObject row = ui.CreateButton("Episode " + episode.Id, content, new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(478, 68), string.Empty, 14,
                new Color(.022f, .022f, .031f, .92f), () => TryPlay(episode));
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.preferredHeight = 68;
            UrbanFantasyStyle.AddBorder(ui, row.GetComponent<RectTransform>());

            Image portrait = ui.CreateImage("Thumbnail", row.transform, new Color(.2f, .2f, .24f, .35f),
                new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(34, 0), new Vector2(54, 54));
            Sprite thumbnail = episode.Thumbnail != null ? episode.Thumbnail
                : episode.FocusCharacter != null ? episode.FocusCharacter.Portrait : null;
            if (thumbnail != null)
            {
                portrait.sprite = thumbnail;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.color = unlocked ? Color.white : new Color(.35f, .35f, .38f, .7f);
                if (!unlocked) UrbanFantasyStyle.ApplyMonochrome(portrait);
            }
            Text title = ui.CreateText("Title", unlocked ? episode.Title : "미해금 스토리", row.transform, 15,
                FontStyle.Normal, unlocked ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted,
                new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(42, 10),
                new Vector2(-150, 27), TextAnchor.MiddleLeft);
            Text state = ui.CreateText("State", string.Empty, row.transform, 9, FontStyle.Normal,
                UrbanFantasyStyle.Muted, new Vector2(0, .5f), new Vector2(1, .5f),
                new Vector2(42, -16), new Vector2(-150, 17), TextAnchor.MiddleLeft);
            Image fill = CreateMiniProgress(row.transform, 78f, 54f, 5f);
            Text open = ui.CreateText("Open", unlocked ? "›" : "LOCK", row.transform, unlocked ? 24 : 9,
                FontStyle.Normal, UrbanFantasyStyle.Muted, new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-28, 0), new Vector2(48, 30), TextAnchor.MiddleCenter);
            rows.Add(new EpisodeRowState
            {
                Episode = episode,
                Title = title,
                State = state,
                Open = open,
                Fill = fill,
                Thumbnail = portrait
            });
        }

        Image CreateMiniProgress(Transform parent, float leftPadding, float rightPadding, float y)
        {
            ui.CreateImage("Progress Track", parent, new Color(1, 1, 1, .09f),
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2((leftPadding - rightPadding) * .5f, y),
                new Vector2(-(leftPadding + rightPadding), 2));
            Image fill = ui.CreateImage("Progress Fill", parent, UrbanFantasyStyle.Silver,
                new Vector2(0, 0), new Vector2(0, 0), new Vector2(leftPadding, y), new Vector2(0, 2));
            fill.rectTransform.pivot = new Vector2(0, .5f);
            return fill;
        }

        RectTransform CreateHorizontalScroll(RectTransform viewport, out ScrollRect scroll)
        {
            var go = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));
            go.transform.SetParent(viewport, false);
            RectTransform content = go.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 0);
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, .5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(2, 2, 4, 4);
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 32;
            return content;
        }

        RectTransform CreateVerticalScroll(RectTransform viewport, out ScrollRect scroll)
        {
            var go = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            go.transform.SetParent(viewport, false);
            RectTransform content = go.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 7;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28;
            return content;
        }

        void CreateEmptyNotice(Transform parent, string text, int size = 14)
        {
            ui.CreateText("Empty Story Notice", text, parent, size, FontStyle.Normal,
                UrbanFantasyStyle.Muted, Vector2.zero, Vector2.one, Vector2.zero,
                new Vector2(-24, -12), TextAnchor.MiddleCenter);
        }

        void SelectMain(StoryEpisode episode)
        {
            selectedMain = episode;
            SetHero(episode);
            RefreshProgress();
        }

        void SetHero(StoryEpisode episode)
        {
            selectedMain = episode;
            if (episode == null)
            {
                heroBanner.sprite = null;
                heroBanner.color = LobbyTheme.Hex("10111A");
                heroCharacter.sprite = null;
                heroCharacter.color = Color.clear;
                heroEyebrow.text = "MAIN STORY";
                heroTitle.text = "등록된 메인 스토리 없음";
                heroSummary.text = "Starfall > Story > Visual Novel Editor에서 에피소드를 추가하거나 Excel 파일을 가져오세요.";
                heroProgress.text = "NO EPISODE DATA";
                heroProgressFill.rectTransform.sizeDelta = new Vector2(0, 3);
                if (heroPlayLabel != null) heroPlayLabel.text = "스토리 없음";
                return;
            }

            bool unlocked = StoryProgressService.IsUnlocked(database, episode);
            Sprite banner = episode.Banner != null ? episode.Banner : episode.Thumbnail;
            heroBanner.material = null;
            heroBanner.sprite = banner;
            heroBanner.color = banner != null ? (unlocked ? Color.white : new Color(.32f, .32f, .35f, 1f))
                : LobbyTheme.Hex("11121A");
            if (banner != null && !unlocked) UrbanFantasyStyle.ApplyMonochrome(heroBanner);
            CharacterData focus = episode.FocusCharacter;
            heroCharacter.material = null;
            heroCharacter.sprite = focus != null ? focus.GachaArt : null;
            heroCharacter.color = focus != null ? (unlocked ? Color.white : new Color(.3f, .3f, .34f, .8f)) : Color.clear;
            if (focus != null && !unlocked) UrbanFantasyStyle.ApplyMonochrome(heroCharacter);
            heroEyebrow.text = "MAIN STORY   ·   CHAPTER " + Mathf.Max(1, episode.SortOrder).ToString("00") +
                               (focus != null ? "   ·   " + focus.DisplayName : string.Empty);
            heroTitle.text = unlocked ? episode.Title : "아직 열리지 않은 기록";
            heroSummary.text = unlocked ? (string.IsNullOrWhiteSpace(episode.Summary)
                ? "이 기록의 줄거리는 아직 작성되지 않았습니다." : episode.Summary)
                : UnlockHint(episode);
            if (heroPlayLabel != null) heroPlayLabel.text = unlocked ? "스토리 보기   ›" : "미해금";
        }

        void PlaySelectedMain()
        {
            TryPlay(selectedMain);
        }

        void TryPlay(StoryEpisode episode)
        {
            if (episode == null)
            {
                archiveStatus.text = "재생할 스토리가 없습니다.";
                return;
            }
            if (!StoryProgressService.IsUnlocked(database, episode))
            {
                archiveStatus.text = "미해금 스토리  ·  " + UnlockHint(episode);
                return;
            }
            if (player != null) return;
            var go = new GameObject("Visual Novel Player", typeof(RectTransform));
            go.transform.SetParent(safeRoot, false);
            player = go.AddComponent<VisualNovelPlayer>();
            player.Initialize(database, episode, ui, OnPlayerClosed);
            go.transform.SetAsLastSibling();
        }

        void OnPlayerClosed(StoryEpisode closedEpisode)
        {
            player = null;
            if (closedEpisode != null && closedEpisode.Category == StoryCategory.Main)
                selectedMain = closedEpisode;
            RefreshProgress();
        }

        void RefreshProgress()
        {
            int completed = 0;
            int total = 0;
            if (database != null && database.Episodes != null)
            {
                foreach (StoryEpisode episode in database.Episodes)
                {
                    if (episode == null) continue;
                    total++;
                    if (StoryProgressService.IsCompleted(episode)) completed++;
                }
            }
            archiveStatus.text = "ARCHIVE COMPLETION   " + completed.ToString("00") + " / " + total.ToString("00");

            foreach (EpisodeRowState row in rows)
            {
                bool unlocked = StoryProgressService.IsUnlocked(database, row.Episode);
                bool complete = StoryProgressService.IsCompleted(row.Episode);
                float progress = StoryProgressService.GetReadProgress(row.Episode);
                if (row.Title != null)
                {
                    row.Title.text = unlocked ? row.Episode.Title
                        : row.Episode.Category == StoryCategory.Main ? "미해금 기록" : "미해금 스토리";
                    row.Title.color = unlocked ? UrbanFantasyStyle.Silver : UrbanFantasyStyle.Muted;
                }
                if (row.Open != null)
                {
                    row.Open.text = unlocked ? "›" : "LOCK";
                    row.Open.fontSize = unlocked ? 24 : 9;
                }
                if (row.Thumbnail != null)
                {
                    bool hasThumbnail = row.Thumbnail.sprite != null;
                    row.Thumbnail.color = hasThumbnail
                        ? unlocked ? Color.white : new Color(.35f, .35f, .38f, .7f)
                        : new Color(.2f, .2f, .24f, .35f);
                    if (hasThumbnail && unlocked) row.Thumbnail.material = null;
                }
                row.State.text = !unlocked ? "LOCKED" : complete ? "COMPLETE" : progress > 0f
                    ? "READING  " + Mathf.RoundToInt(progress * 100f) + "%" : "UNREAD";
                row.State.color = complete ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Muted;
                if (row.Fill != null)
                {
                    float width = row.Episode.Category == StoryCategory.Main ? 226f : 346f;
                    row.Fill.rectTransform.sizeDelta = new Vector2(unlocked ? width * progress : 0f, 2);
                    row.Fill.color = complete ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Silver;
                }
                if (row.Selection != null)
                    row.Selection.color = row.Episode == selectedMain ? UrbanFantasyStyle.Highlight : Color.clear;
            }

            if (selectedMain != null)
            {
                float progress = StoryProgressService.GetReadProgress(selectedMain);
                bool complete = StoryProgressService.IsCompleted(selectedMain);
                bool unlocked = StoryProgressService.IsUnlocked(database, selectedMain);
                heroProgress.text = !unlocked ? "LOCKED" : complete ? "COMPLETE  ·  100% READ"
                    : Mathf.RoundToInt(progress * 100f) + "% READ";
                heroProgressFill.rectTransform.sizeDelta = new Vector2(380f * progress, 3);
                heroProgressFill.color = complete ? UrbanFantasyStyle.Gold : UrbanFantasyStyle.Silver;
            }
        }

        StoryEpisode FirstUnlocked(IReadOnlyList<StoryEpisode> episodes)
        {
            foreach (StoryEpisode episode in episodes)
                if (StoryProgressService.IsUnlocked(database, episode)) return episode;
            return null;
        }

        string UnlockHint(StoryEpisode episode)
        {
            if (episode == null) return "해금 조건을 확인할 수 없습니다.";
            if (!string.IsNullOrWhiteSpace(episode.PrerequisiteEpisodeId) && database != null)
            {
                StoryEpisode prerequisite = database.FindEpisode(episode.PrerequisiteEpisodeId);
                return prerequisite != null ? "「" + prerequisite.Title + "」 완독 후 해금" : "선행 에피소드 완독 후 해금";
            }
            if (!string.IsNullOrWhiteSpace(episode.UnlockKey)) return "조건: " + episode.UnlockKey;
            return "이전 에피소드 완독 후 해금";
        }

        void ReturnToLobby()
        {
            if (changingScene) return;
            changingScene = true;
            StarfallSceneFlow.Load(SceneNames.Lobby);
        }

        static RectTransform CreateLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
