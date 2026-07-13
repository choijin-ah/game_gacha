using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public static class ProjectSettingsTools
    {
        const string ApplyMenuPath = "Starfall/Project/Apply Recommended Player Settings";

        [MenuItem(ApplyMenuPath)]
        public static void ApplyRecommendedPlayerSettings()
        {
            bool changed = ApplyLandscapeOrientation();
            AssetDatabase.SaveAssets();

            Debug.Log(changed
                ? "[Starfall] Applied landscape-only player settings."
                : "[Starfall] Recommended player settings are already applied.");
        }

        [MenuItem(ApplyMenuPath, true)]
        static bool CanApplyRecommendedPlayerSettings()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        static bool ApplyLandscapeOrientation()
        {
            bool changed = false;

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                changed = true;
            }

            if (PlayerSettings.allowedAutorotateToPortrait)
            {
                PlayerSettings.allowedAutorotateToPortrait = false;
                changed = true;
            }

            if (PlayerSettings.allowedAutorotateToPortraitUpsideDown)
            {
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                changed = true;
            }

            if (!PlayerSettings.allowedAutorotateToLandscapeLeft)
            {
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                changed = true;
            }

            if (!PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                changed = true;
            }

            return changed;
        }
    }
}
