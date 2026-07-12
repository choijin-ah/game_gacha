namespace StarfallAcademy.Lobby
{
    public static class SceneNavigation
    {
        public static string FormationReturnScene { get; set; } = SceneNames.Lobby;

        public static string ConsumeFormationReturnScene()
        {
            string target = string.IsNullOrWhiteSpace(FormationReturnScene)
                ? SceneNames.Lobby : FormationReturnScene;
            FormationReturnScene = SceneNames.Lobby;
            return target;
        }
    }
}
