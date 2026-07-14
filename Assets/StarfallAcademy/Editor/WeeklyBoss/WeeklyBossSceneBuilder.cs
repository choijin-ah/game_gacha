using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class WeeklyBossSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/WeeklyBoss.unity";
        static WeeklyBossSceneBuilder() => EditorApplication.delayCall += Ensure;

        [MenuItem("Starfall/Rebuild/Weekly Boss Scene")]
        public static void Create() => BattleSceneBuilderUtility.CreateScene<WeeklyBossSceneEntry>(
            ScenePath, "Weekly Boss Scene Entry");

        static void Ensure() => BattleSceneBuilderUtility.EnsureScene<WeeklyBossSceneEntry>(
            ScenePath, "Weekly Boss Scene Entry");
    }
}
