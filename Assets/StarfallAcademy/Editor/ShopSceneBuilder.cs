using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class ShopSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/Shop.unity";

        static ShopSceneBuilder() => EditorApplication.delayCall += Ensure;

        [MenuItem("Starfall/Rebuild/Shop Scene")]
        public static void Create() => BattleSceneBuilderUtility.CreateScene<ShopSceneEntry>(
            ScenePath, "Shop Scene Entry");

        static void Ensure() => BattleSceneBuilderUtility.EnsureScene<ShopSceneEntry>(
            ScenePath, "Shop Scene Entry");
    }
}
