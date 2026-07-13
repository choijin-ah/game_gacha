using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    /// <summary>
    /// Defers automatic scene creation until the editor is fully out of play mode and compilation.
    /// A keyed queue prevents InitializeOnLoad builders from registering a new delayCall every frame.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneBuilderSafety
    {
        static readonly Dictionary<string, Action> Pending = new Dictionary<string, Action>();

        static SceneBuilderSafety()
        {
            EditorApplication.update -= RetryWhenSafe;
        }

        internal static bool TryBegin(string key, Action retry)
        {
            if (IsSafe()) return true;
            Pending[key] = retry;
            EditorApplication.update -= RetryWhenSafe;
            EditorApplication.update += RetryWhenSafe;
            return false;
        }

        internal static bool CanRunManualBuild()
        {
            if (IsSafe()) return true;
            UnityEngine.Debug.LogWarning(
                "[Starfall] Scene rebuild is unavailable while the editor is playing, compiling, or importing assets.");
            return false;
        }

        static bool IsSafe()
        {
            return !EditorApplication.isCompiling
                && !EditorApplication.isUpdating
                && !EditorApplication.isPlaying
                && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        static void RetryWhenSafe()
        {
            if (!IsSafe()) return;

            EditorApplication.update -= RetryWhenSafe;
            Action[] callbacks = new Action[Pending.Count];
            Pending.Values.CopyTo(callbacks, 0);
            Pending.Clear();
            foreach (Action callback in callbacks)
                if (callback != null) EditorApplication.delayCall += () => callback();
        }
    }
}
