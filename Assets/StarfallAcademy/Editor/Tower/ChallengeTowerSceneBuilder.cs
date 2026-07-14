using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class ChallengeTowerSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/ChallengeTower.unity";
        static ChallengeTowerSceneBuilder() => EditorApplication.delayCall += Ensure;

        [MenuItem("Starfall/Rebuild/Challenge Tower Scene")]
        public static void Create() => BattleSceneBuilderUtility.CreateScene<ChallengeTowerSceneEntry>(
            ScenePath, "Challenge Tower Scene Entry");

        static void Ensure() => BattleSceneBuilderUtility.EnsureScene<ChallengeTowerSceneEntry>(
            ScenePath, "Challenge Tower Scene Entry");
    }
}
