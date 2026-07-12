using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    // 로비 원화를 무압축 UI 텍스처로 유지합니다.
    [InitializeOnLoad]
    static class LobbyArtImporter
    {
        static readonly string[] ArtPaths =
        {
            "Assets/StarfallAcademy/Resources/Lobby/Art/lobby_hero_v2.png",
            "Assets/StarfallAcademy/Resources/Lobby/Art/lobby_urban_fantasy_v1.png",
            "Assets/StarfallAcademy/Resources/Lobby/UI/button_states_v1.png",
            "Assets/StarfallAcademy/Resources/Lobby/UI/lobby_icons_v1.png",
            "Assets/StarfallAcademy/Resources/Lobby/UI/lobby_icons_v2.png",
            "Assets/StarfallAcademy/Resources/Gacha/Art/gacha_portal_v1.png",
            "Assets/StarfallAcademy/Resources/CharacterArchive/UI/default_skill_icons_v1.png"
        };

        static LobbyArtImporter()
        {
            EditorApplication.delayCall += Configure;
        }

        static void Configure()
        {
            foreach (string path in ArtPaths)
                ConfigureTexture(path);
        }

        static void ConfigureTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            bool needsImport = importer.maxTextureSize != 4096
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.mipmapEnabled
                || importer.filterMode != FilterMode.Bilinear
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp;
            if (!needsImport) return;

            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
