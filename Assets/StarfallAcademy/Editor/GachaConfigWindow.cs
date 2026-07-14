using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    // Compatibility entry point for editor extensions that still request the old
    // single-config asset. New runtime content is authored through the banner database.
    internal static class GachaConfigBootstrap
    {
        internal const string ConfigPath =
            "Assets/StarfallAcademy/Resources/Data/GachaConfig.asset";

        internal static GachaConfig LoadOrCreate()
        {
            GachaConfig config = AssetDatabase.LoadAssetAtPath<GachaConfig>(ConfigPath);
            if (config != null) return config;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.Refresh();
            config = ScriptableObject.CreateInstance<GachaConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }
    }

    /// <summary>
    /// Kept so saved editor layouts and external calls to GetWindow&lt;GachaConfigWindow&gt;
    /// continue to work. It immediately redirects to the multi-banner editor.
    /// </summary>
    public sealed class GachaConfigWindow : EditorWindow
    {
        [MenuItem("Starfall/Data/Gacha Configuration")]
        public static void Open() => GachaBannerDatabaseWindow.Open();

        [MenuItem("Starfall/Fix/Gacha Rates For Current Pool")]
        public static void OpenLegacyRateFixAlias() => GachaBannerDatabaseWindow.Open();

        void OnEnable()
        {
            EditorApplication.delayCall += Redirect;
        }

        void Redirect()
        {
            if (this == null) return;
            GachaBannerDatabaseWindow.Open();
            Close();
        }
    }
}
