using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class SceneBuildSettingsUtility
    {
        internal static void Update()
        {
            string[] corePaths =
            {
                LobbySceneBuilder.ScenePath,
                FormationSceneBuilder.ScenePath,
                GachaSceneBuilder.ScenePath,
                ShopSceneBuilder.ScenePath,
                CharacterArchiveSceneBuilder.ScenePath,
                "Assets/StarfallAcademy/Scenes/StoryArchive.unity",
                StageSelectSceneBuilder.ScenePath,
                TurnBattleSceneBuilder.ScenePath,
                WeeklyBossSceneBuilder.ScenePath,
                ChallengeTowerSceneBuilder.ScenePath
            };
            var scenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < corePaths.Length; i++)
                if (File.Exists(corePaths[i])) scenes.Add(new EditorBuildSettingsScene(corePaths[i], true));

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                bool isCore = false;
                for (int i = 0; i < corePaths.Length; i++)
                    if (existing.path == corePaths[i]) { isCore = true; break; }
                if (!isCore && File.Exists(existing.path)) scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
