using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class FormationPreset
    {
        public string name;
        public List<string> characterIds = new List<string>();

        public FormationPreset Clone() => new FormationPreset
        {
            name = name,
            characterIds = characterIds == null ? new List<string>() : new List<string>(characterIds)
        };
    }

    [CreateAssetMenu(fileName = "FormationSettings", menuName = "Starfall/Formation Settings")]
    public sealed class FormationSettings : ScriptableObject
    {
        [SerializeField, Range(1, 5)] int maximumPresetCount = 3;
        [SerializeField] string[] defaultPresetNames = { "파티 1", "파티 2", "파티 3" };

        public int MaximumPresetCount => Mathf.Clamp(maximumPresetCount, 1, 5);
        public IReadOnlyList<string> DefaultPresetNames => defaultPresetNames
            ?? (IReadOnlyList<string>)Array.Empty<string>();

        public string GetDefaultName(int index) => index >= 0 && index < DefaultPresetNames.Count
            && !string.IsNullOrWhiteSpace(DefaultPresetNames[index])
                ? DefaultPresetNames[index] : "파티 " + (index + 1);

        void OnValidate()
        {
            maximumPresetCount = Mathf.Clamp(maximumPresetCount, 1, 5);
            defaultPresetNames ??= Array.Empty<string>();
        }
    }
}
