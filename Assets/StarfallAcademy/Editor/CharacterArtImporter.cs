using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    // 이 폴더에 넣은 캐릭터 이미지는 Portrait/Gacha Art 필드에서 바로 선택할 수 있게 합니다.
    public sealed class CharacterArtImporter : AssetPostprocessor
    {
        const string CharacterArtFolder = "Assets/StarfallAcademy/Arts/Characters/";
        const string StoryArtFolder = "Assets/StarfallAcademy/Resources/Story/";

        [InitializeOnLoadMethod]
        static void ScheduleImportCheck()
        {
            EditorApplication.delayCall += EnsureManagedArtSettings;
        }

        [MenuItem("Starfall/Art/Reimport Character Art")]
        static void ReimportCharacterArt()
        {
            ReimportFolder(CharacterArtFolder);
        }

        [MenuItem("Starfall/Art/Reimport Story Art")]
        static void ReimportStoryArt()
        {
            ReimportFolder(StoryArtFolder);
        }

        static void ReimportFolder(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
        }

        static void EnsureManagedArtSettings()
        {
            EnsureFolderSettings(CharacterArtFolder);
            EnsureFolderSettings(StoryArtFolder);
        }

        static void EnsureFolderSettings(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        void OnPreprocessTexture()
        {
            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(CharacterArtFolder)
                && !normalizedPath.StartsWith(StoryArtFolder)) return;
            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
