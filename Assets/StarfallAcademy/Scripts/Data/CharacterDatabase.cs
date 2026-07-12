using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Starfall/Character Database")]
    public sealed class CharacterDatabase : ScriptableObject
    {
        [SerializeField] List<CharacterData> characters = new List<CharacterData>();

        public IReadOnlyList<CharacterData> Characters => characters;

        public void Add(CharacterData character)
        {
            if (character != null && !characters.Contains(character))
                characters.Add(character);
        }

        public void Remove(CharacterData character)
        {
            characters.Remove(character);
        }

        public CharacterData Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            foreach (CharacterData character in characters)
                if (character != null && character.Id == id)
                    return character;
            return null;
        }
    }
}
