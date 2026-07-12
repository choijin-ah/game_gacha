using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public static class SkillIconLibrary
    {
        static readonly Dictionary<int, Sprite> defaults = new Dictionary<int, Sprite>();

        public static Sprite Get(CharacterData character)
        {
            if (character == null) return null;
            if (character.SkillIcon != null) return character.SkillIcon;
            int index = character.DefaultSkillIconIndex;
            if (defaults.TryGetValue(index, out Sprite cached)) return cached;
            Texture2D atlas = Resources.Load<Texture2D>("CharacterArchive/UI/default_skill_icons_v1");
            if (atlas == null) return null;
            float cellWidth = atlas.width / 3f;
            float cellHeight = atlas.height / 2f;
            int column = index % 3;
            int rowFromTop = index / 3;
            Rect rect = new Rect(column * cellWidth, (1 - rowFromTop) * cellHeight, cellWidth, cellHeight);
            Sprite sprite = Sprite.Create(atlas, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            defaults[index] = sprite;
            return sprite;
        }
    }
}
