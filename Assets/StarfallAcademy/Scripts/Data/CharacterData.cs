using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum CharacterRole
    {
        Striker,
        Support,
        Tank,
        Healer,
        Special
    }

    public enum AttackType
    {
        Normal,
        Piercing,
        Mystic,
        Sonic
    }

    public enum DefaultSkillIconStyle
    {
        Auto = -1,
        Blade = 0,
        Sigil = 1,
        Bastion = 2,
        Bloom = 3,
        Eclipse = 4
    }

    [CreateAssetMenu(fileName = "Character", menuName = "Starfall/Character Data")]
    public sealed class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string characterId;
        [SerializeField] string displayName = "새 캐릭터";
        [SerializeField] string affiliation = "소속 미정";
        [SerializeField, TextArea(2, 4)] string description;

        [Header("Presentation")]
        [SerializeField] Sprite portrait;
        [SerializeField] Sprite gachaArt;
        [SerializeField] Color accentColor = new Color(.38f, .85f, 1f, 1f);

        [Header("Formation")]
        [SerializeField] CharacterRole role = CharacterRole.Striker;
        [SerializeField] AttackType attackType = AttackType.Normal;
        [SerializeField, Range(1, 6)] int rarity = 3;
        [SerializeField, Min(1)] int level = 1;
        [SerializeField, Min(0)] int combatPower = 1000;

        [Header("Growth")]
        [SerializeField, Min(1)] int maxLevel = 100;
        [SerializeField, Min(0)] int levelUpBaseCreditCost = 1000;
        [SerializeField, Min(0)] int levelUpCreditCostGrowth = 250;
        [SerializeField, Min(0)] int combatPowerPerLevel = 120;

        [Header("Skill")]
        [SerializeField] string skillName = "고유 스킬";
        [SerializeField] Sprite skillIcon;
        [SerializeField] DefaultSkillIconStyle defaultSkillIcon = DefaultSkillIconStyle.Auto;
        [SerializeField, Min(1)] int skillMaxLevel = 10;
        [SerializeField, Min(0)] int skillBaseMaterialCost = 20;
        [SerializeField, Min(0)] int skillMaterialCostGrowth = 10;
        [SerializeField, Min(0)] int combatPowerPerSkillLevel = 250;

        public string Id => string.IsNullOrWhiteSpace(characterId) ? name : characterId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Affiliation => affiliation;
        public string Description => description;
        public Sprite Portrait => portrait;
        public Sprite GachaArt => gachaArt != null ? gachaArt : portrait;
        public Color AccentColor => accentColor;
        public CharacterRole Role => role;
        public AttackType AttackType => attackType;
        public int Rarity => rarity;
        public int Level => level;
        public int CombatPower => combatPower;
        public int MaxLevel => maxLevel;
        public int LevelUpBaseCreditCost => levelUpBaseCreditCost;
        public int LevelUpCreditCostGrowth => levelUpCreditCostGrowth;
        public int CombatPowerPerLevel => combatPowerPerLevel;
        public string SkillName => string.IsNullOrWhiteSpace(skillName) ? "고유 스킬" : skillName;
        public Sprite SkillIcon => skillIcon;
        public int DefaultSkillIconIndex => (int)defaultSkillIcon >= 0
            ? Mathf.Clamp((int)defaultSkillIcon, 0, 4) : StableIconIndex(Id);
        public int SkillMaxLevel => skillMaxLevel;
        public int SkillBaseMaterialCost => skillBaseMaterialCost;
        public int SkillMaterialCostGrowth => skillMaterialCostGrowth;
        public int CombatPowerPerSkillLevel => combatPowerPerSkillLevel;

        void OnValidate()
        {
            rarity = Mathf.Clamp(rarity, 1, 6);
            level = Mathf.Max(1, level);
            combatPower = Mathf.Max(0, combatPower);
            maxLevel = Mathf.Max(level, maxLevel);
            levelUpBaseCreditCost = Mathf.Max(0, levelUpBaseCreditCost);
            levelUpCreditCostGrowth = Mathf.Max(0, levelUpCreditCostGrowth);
            combatPowerPerLevel = Mathf.Max(0, combatPowerPerLevel);
            defaultSkillIcon = (DefaultSkillIconStyle)Mathf.Clamp((int)defaultSkillIcon, -1, 4);
            skillMaxLevel = Mathf.Max(1, skillMaxLevel);
            skillBaseMaterialCost = Mathf.Max(0, skillBaseMaterialCost);
            skillMaterialCostGrowth = Mathf.Max(0, skillMaterialCostGrowth);
            combatPowerPerSkillLevel = Mathf.Max(0, combatPowerPerSkillLevel);
        }

        static int StableIconIndex(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return (int)(hash % 5);
            }
        }
    }
}
