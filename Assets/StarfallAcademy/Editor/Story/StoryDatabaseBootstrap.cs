using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class StoryDatabaseBootstrap
    {
        public const string DatabasePath = "Assets/StarfallAcademy/Resources/Data/StoryDatabase.asset";
        public const string EpisodeFolder = "Assets/StarfallAcademy/Data/Story/Episodes";

        static StoryDatabaseBootstrap()
        {
            EditorApplication.delayCall += () => EnsureDatabase();
        }

        public static StoryDatabase EnsureDatabase()
        {
            StoryDatabase database = AssetDatabase.LoadAssetAtPath<StoryDatabase>(DatabasePath);
            if (database != null) return database;

            EnsureFolders();
            database = ScriptableObject.CreateInstance<StoryDatabase>();
            database.name = "StoryDatabase";
            AssetDatabase.CreateAsset(database, DatabasePath);
            CreateSamples(database);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return database;
        }

        // Command-line smoke test entry point. This is also useful in CI after spreadsheet imports.
        public static void ValidateDatabaseFromCommandLine()
        {
            StoryDatabase database = EnsureDatabase();
            if (database == null) throw new System.InvalidOperationException("StoryDatabase could not be loaded.");
            if (database.Episodes == null || database.Episodes.Count < 4)
                throw new System.InvalidOperationException("StoryDatabase must contain the four starter episodes.");

            foreach (StoryCategory category in System.Enum.GetValues(typeof(StoryCategory)))
            {
                System.Collections.Generic.IReadOnlyList<StoryEpisode> episodes = database.GetEpisodes(category);
                if (episodes.Count == 0)
                    throw new System.InvalidOperationException($"No starter episode found for {category}.");
                if (episodes[0] == null || episodes[0].Lines == null || episodes[0].Lines.Count < 2)
                    throw new System.InvalidOperationException($"Starter episode for {category} has no dialogue.");
            }

            Debug.Log("[Story] StoryDatabase validation passed: four categories and sample dialogue are ready.");
        }

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            Directory.CreateDirectory(EpisodeFolder);
        }

        static void CreateSamples(StoryDatabase database)
        {
            CharacterData sampleCharacter = FindFirstCharacter();
            CreateSample(database, "main_prologue", "프롤로그 · 별이 내린 밤", StoryCategory.Main, 0,
                "도시의 밤하늘이 갈라진 날, 스타폴의 이야기가 시작된다.", sampleCharacter,
                ("내레이션", "별빛이 사라진 지 열두 번째 밤이었다."),
                (sampleCharacter != null ? sampleCharacter.DisplayName : "아리아", "이곳에서 기다리면… 분명 다시 만날 수 있어."),
                ("내레이션", "멀리서 종소리가 울리고, 정지했던 운명이 움직이기 시작했다."));

            CreateSample(database, "event_first_snow", "이벤트 · 첫눈의 약속", StoryCategory.Event, 0,
                "첫눈이 내리는 광장에서 벌어지는 작은 소동.", sampleCharacter,
                ("내레이션", "광장의 불빛 위로 올해의 첫눈이 내려앉았다."),
                (sampleCharacter != null ? sampleCharacter.DisplayName : "아리아", "오늘만큼은 임무도 잠깐 잊어도 되겠지?"));

            CreateSample(database, "character_sample", "인연 · 닫힌 창문 너머", StoryCategory.Character, 0,
                "한 인물의 숨겨진 마음을 들여다보는 짧은 이야기.", sampleCharacter,
                (sampleCharacter != null ? sampleCharacter.DisplayName : "아리아", "사람들은 내가 언제나 괜찮다고 생각해."),
                ("주인공", "괜찮지 않아도 돼. 오늘은 내가 들을게."),
                (sampleCharacter != null ? sampleCharacter.DisplayName : "아리아", "…그 말, 조금만 더 믿어 봐도 될까?"));

            CreateSample(database, "side_archive_room", "외전 · 기록실의 유령", StoryCategory.Side, 0,
                "늦은 밤 기록실에서 발견한 정체불명의 기록.", sampleCharacter,
                ("내레이션", "아무도 없는 기록실에서 책장이 저절로 넘어갔다."),
                ("???", "마지막 페이지를 찾는 사람은 누구지?"));
        }

        static void CreateSample(StoryDatabase database, string id, string title, StoryCategory category,
            int order, string summary, CharacterData focusCharacter, params (string speaker, string text)[] dialogue)
        {
            var episode = ScriptableObject.CreateInstance<StoryEpisode>();
            episode.name = id;
            episode.Id = id;
            episode.Title = title;
            episode.Category = category;
            episode.SortOrder = order;
            episode.Summary = summary;
            episode.FocusCharacter = focusCharacter;
            episode.IsInitiallyUnlocked = true;

            for (int i = 0; i < dialogue.Length; i++)
            {
                var line = new StoryLine
                {
                    Id = $"line_{i + 1:000}",
                    SpeakerName = dialogue[i].speaker,
                    Text = dialogue[i].text,
                    SpeakerPosition = dialogue[i].speaker == "내레이션"
                        ? StorySpeakerPosition.Narrator : StorySpeakerPosition.Center,
                    Transition = i == 0 ? StoryTransition.FadeToBlack : StoryTransition.CrossFade
                };

                if (focusCharacter != null && dialogue[i].speaker == focusCharacter.DisplayName)
                {
                    line.Speaker = focusCharacter;
                    line.Center.Character = focusCharacter;
                    line.Center.Visible = true;
                    line.Center.ExpressionKey = i == dialogue.Length - 1 ? "smile" : "default";
                }
                episode.AddLine(line);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{EpisodeFolder}/{id}.asset");
            AssetDatabase.CreateAsset(episode, path);
            database.AddEpisode(episode);
        }

        static CharacterData FindFirstCharacter()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterData");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
