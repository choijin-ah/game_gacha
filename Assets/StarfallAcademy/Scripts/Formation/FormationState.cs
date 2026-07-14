using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum FormationToggleResult
    {
        Added,
        Removed,
        Full,
        Invalid
    }

    // 캐릭터 에셋을 수정하지 않고 ID 목록만 저장하는 편성 상태입니다.
    public sealed class FormationState
    {
        public const int MaxMembers = 4;
        const string PlayerPrefsKey = "StarfallAcademy.Formation";

        readonly List<CharacterData> members = new List<CharacterData>();

        public IReadOnlyList<CharacterData> Members => members;
        public int Count => members.Count;
        public int TotalPower
        {
            get
            {
                int total = 0;
                foreach (CharacterData member in members)
                    if (member != null) total += CharacterProgressionService.GetCombatPower(member);
                return total;
            }
        }

        public void Load(CharacterDatabase database)
        {
            members.Clear();
            if (database == null) return;
            FormationPreset preset = FormationPresetService.Default.GetActive(database);
            string saved = preset != null && preset.characterIds != null
                ? string.Join("|", preset.characterIds)
                : PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(saved)) return;

            foreach (string id in saved.Split('|'))
            {
                CharacterData character = database.Find(id);
                if (character != null && CharacterProgressionService.IsOwned(character) &&
                    !members.Contains(character) && members.Count < MaxMembers)
                    members.Add(character);
            }
        }

        public FormationToggleResult Toggle(CharacterData character)
        {
            if (character == null || !CharacterProgressionService.IsOwned(character))
                return FormationToggleResult.Invalid;
            if (members.Remove(character)) return FormationToggleResult.Removed;
            if (members.Count >= MaxMembers) return FormationToggleResult.Full;
            members.Add(character);
            return FormationToggleResult.Added;
        }

        public bool Contains(CharacterData character) => character != null && members.Contains(character);

        public void Clear() => members.Clear();

        public bool Save()
        {
            FormationPresetService service = FormationPresetService.Default;
            return service.Save(service.ActivePresetIndex, members);
        }
    }
}
