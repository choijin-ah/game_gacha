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

    public enum BattleElement
    {
        Auto = -1,
        Fire = 0,
        Ice,
        Lightning,
        Wind,
        Light,
        Dark
    }

    public enum BattleActionKind
    {
        Basic,
        Skill,
        Ultimate,
        Guard,
        Enemy
    }

    public enum BattleTargetType
    {
        SingleEnemy,
        SingleAlly,
        AllEnemies,
        AllAllies,
        AdjacentEnemies,
        AdjacentAllies,
        LowestHpAlly,
        RandomEnemy,
        Self
    }

    public readonly struct BattleActionConfig
    {
        public string Name { get; }
        public Sprite Icon { get; }
        public BattleActionKind Kind { get; }
        public BattleTargetType TargetType { get; }
        public float DamageMultiplier { get; }
        public float HealingMultiplier { get; }
        public int FixedValue { get; }
        public int SkillPointCost { get; }
        public int EnergyCost { get; }
        public int EnergyGain { get; }
        public int BreakDamage { get; }
        public BattleElement Element { get; }

        public BattleActionConfig(string name, Sprite icon, BattleActionKind kind,
            BattleTargetType targetType, float damageMultiplier, float healingMultiplier,
            int fixedValue, int skillPointCost, int energyCost, int energyGain,
            int breakDamage, BattleElement element)
        {
            Name = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name;
            Icon = icon;
            Kind = kind;
            TargetType = targetType;
            DamageMultiplier = Mathf.Max(0f, damageMultiplier);
            HealingMultiplier = Mathf.Max(0f, healingMultiplier);
            FixedValue = Mathf.Max(0, fixedValue);
            SkillPointCost = Mathf.Clamp(skillPointCost, -5, 5);
            EnergyCost = Mathf.Max(0, energyCost);
            EnergyGain = Mathf.Max(0, energyGain);
            BreakDamage = Mathf.Max(0, breakDamage);
            Element = element;
        }
    }

    [System.Serializable]
    public sealed class CharacterBattleActionData
    {
        [SerializeField] string actionName;
        [SerializeField] Sprite icon;
        [SerializeField] BattleTargetType targetType = BattleTargetType.SingleEnemy;
        [SerializeField, Min(0f)] float damageMultiplier = 1f;
        [SerializeField, Min(0f)] float healingMultiplier;
        [SerializeField, Min(0)] int fixedValue;
        [SerializeField, Range(-5, 5), Tooltip("음수는 스킬 포인트 회복을 의미합니다.")]
        int skillPointCost;
        [SerializeField, Min(0)] int energyCost;
        [SerializeField, Min(0)] int energyGain;
        [SerializeField, Min(0)] int breakDamage;
        [SerializeField] BattleElement element = BattleElement.Auto;

        public string Name => actionName;
        public Sprite Icon => icon;
        public BattleTargetType TargetType => targetType;
        public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
        public float HealingMultiplier => Mathf.Max(0f, healingMultiplier);
        public int FixedValue => Mathf.Max(0, fixedValue);
        public int SkillPointCost => Mathf.Clamp(skillPointCost, -5, 5);
        public int EnergyCost => Mathf.Max(0, energyCost);
        public int EnergyGain => Mathf.Max(0, energyGain);
        public int BreakDamage => Mathf.Max(0, breakDamage);
        public BattleElement Element => element;

        internal BattleActionConfig Resolve(BattleActionKind kind, string fallbackName,
            Sprite fallbackIcon, BattleElement fallbackElement)
        {
            string resolvedName = string.IsNullOrWhiteSpace(actionName) ? fallbackName : actionName;
            Sprite resolvedIcon = icon != null ? icon : fallbackIcon;
            BattleElement resolvedElement = element == BattleElement.Auto ? fallbackElement : element;
            return new BattleActionConfig(resolvedName, resolvedIcon, kind, targetType,
                DamageMultiplier, HealingMultiplier, FixedValue, SkillPointCost,
                EnergyCost, EnergyGain, BreakDamage, resolvedElement);
        }

        internal void Sanitize()
        {
            targetType = (BattleTargetType)Mathf.Clamp((int)targetType,
                (int)BattleTargetType.SingleEnemy, (int)BattleTargetType.Self);
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
            healingMultiplier = Mathf.Max(0f, healingMultiplier);
            fixedValue = Mathf.Max(0, fixedValue);
            skillPointCost = Mathf.Clamp(skillPointCost, -5, 5);
            energyCost = Mathf.Max(0, energyCost);
            energyGain = Mathf.Max(0, energyGain);
            breakDamage = Mathf.Max(0, breakDamage);
            element = (BattleElement)Mathf.Clamp((int)element,
                (int)BattleElement.Auto, (int)BattleElement.Dark);
        }

        internal static CharacterBattleActionData Create(string name, Sprite actionIcon,
            BattleTargetType target, float damage, float healing, int fixedAmount,
            int pointCost, int ultimateEnergyCost, int gainedEnergy, int breakAmount)
        {
            return new CharacterBattleActionData
            {
                actionName = name,
                icon = actionIcon,
                targetType = target,
                damageMultiplier = damage,
                healingMultiplier = healing,
                fixedValue = fixedAmount,
                skillPointCost = pointCost,
                energyCost = ultimateEnergyCost,
                energyGain = gainedEnergy,
                breakDamage = breakAmount,
                element = BattleElement.Auto
            };
        }
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

        [Header("Battle MVP Stats")]
        [SerializeField] BattleElement battleElement = BattleElement.Auto;
        [SerializeField, Min(0), Tooltip("0이면 역할과 전투력으로 계산합니다.")]
        int maxHpOverride;
        [SerializeField, Min(0), Tooltip("0이면 역할과 전투력으로 계산합니다.")]
        int attackOverride;
        [SerializeField, Min(0), Tooltip("0이면 역할과 전투력으로 계산합니다.")]
        int defenseOverride;
        [SerializeField, Min(0), Tooltip("0이면 역할 기본값을 사용합니다.")]
        int speedOverride;
        [SerializeField, Range(0f, 1f)] float critChance = .05f;
        [SerializeField, Min(1f)] float critDamage = 1.5f;
        [SerializeField, Min(0), Tooltip("0이면 역할 기본값을 사용합니다.")]
        int maxEnergyOverride;
        [SerializeField, Min(0f)] float aggroWeight = 1f;

        [Header("Battle MVP Actions")]
        [SerializeField] CharacterBattleActionData basicAction;
        [SerializeField] CharacterBattleActionData skillAction;
        [SerializeField] CharacterBattleActionData ultimateAction;

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
        public BattleElement Element => ResolveElement();
        public int MaxHpOverride => Mathf.Max(0, maxHpOverride);
        public int AttackOverride => Mathf.Max(0, attackOverride);
        public int DefenseOverride => Mathf.Max(0, defenseOverride);
        public int SpeedOverride => Mathf.Max(0, speedOverride);
        public float CritChance => Mathf.Clamp01(critChance);
        public float CritDamage => Mathf.Max(1f, critDamage);
        public int MaxEnergyOverride => Mathf.Max(0, maxEnergyOverride);
        public float AggroWeight => Mathf.Max(0f, aggroWeight);
        public int BattleMaxHp => ResolveMaxHp(combatPower);
        public int BattleAttack => ResolveAttack(combatPower);
        public int BattleDefense => ResolveDefense(combatPower);
        public int BattleSpeed => ResolveSpeed();
        public int BattleMaxEnergy => ResolveMaxEnergy();
        public BattleActionConfig BasicAction => ResolveBasicAction();
        public BattleActionConfig SkillAction => ResolveSkillAction();
        public BattleActionConfig UltimateAction => ResolveUltimateAction();

        public int ResolveMaxHp(int effectiveCombatPower)
        {
            if (MaxHpOverride > 0) return MaxHpOverride;
            int roleBonus = role == CharacterRole.Tank ? 650
                : role == CharacterRole.Healer ? 600
                : role == CharacterRole.Support ? 250
                : role == CharacterRole.Special ? 400 : 320;
            return Mathf.Max(1, 700 + roleBonus + Mathf.RoundToInt(Mathf.Max(0, effectiveCombatPower) * .22f));
        }

        public int ResolveAttack(int effectiveCombatPower)
        {
            if (AttackOverride > 0) return AttackOverride;
            int roleBonus = role == CharacterRole.Striker ? 90
                : role == CharacterRole.Special ? 55
                : role == CharacterRole.Support ? 35
                : role == CharacterRole.Healer ? 20 : 25;
            return Mathf.Max(1, 70 + roleBonus + Mathf.RoundToInt(Mathf.Max(0, effectiveCombatPower) * .055f));
        }

        public int ResolveDefense(int effectiveCombatPower)
        {
            if (DefenseOverride > 0) return DefenseOverride;
            int roleBonus = role == CharacterRole.Tank ? 55
                : role == CharacterRole.Healer ? 45
                : role == CharacterRole.Special ? 35
                : role == CharacterRole.Support ? 30 : 25;
            return Mathf.Max(1, 40 + roleBonus + Mathf.RoundToInt(Mathf.Max(0, effectiveCombatPower) * .01f));
        }

        public int ResolveSpeed()
        {
            if (SpeedOverride > 0) return SpeedOverride;
            switch (role)
            {
                case CharacterRole.Support: return 112;
                case CharacterRole.Tank: return 90;
                case CharacterRole.Healer: return 96;
                case CharacterRole.Special: return 118;
                default: return 102;
            }
        }

        public int ResolveMaxEnergy()
        {
            if (MaxEnergyOverride > 0) return MaxEnergyOverride;
            if (role == CharacterRole.Striker) return 110;
            if (role == CharacterRole.Support) return 120;
            return 100;
        }

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
            battleElement = (BattleElement)Mathf.Clamp((int)battleElement,
                (int)BattleElement.Auto, (int)BattleElement.Dark);
            maxHpOverride = Mathf.Max(0, maxHpOverride);
            attackOverride = Mathf.Max(0, attackOverride);
            defenseOverride = Mathf.Max(0, defenseOverride);
            speedOverride = Mathf.Max(0, speedOverride);
            critChance = Mathf.Clamp01(critChance);
            critDamage = Mathf.Max(1f, critDamage);
            maxEnergyOverride = Mathf.Max(0, maxEnergyOverride);
            aggroWeight = Mathf.Max(0f, aggroWeight);
            EnsureBattleActionDefaults();
            basicAction.Sanitize();
            skillAction.Sanitize();
            ultimateAction.Sanitize();
        }

        BattleActionConfig ResolveBasicAction()
        {
            CharacterBattleActionData data = basicAction ?? CreateBasicActionDefault();
            return data.Resolve(BattleActionKind.Basic, "일반 공격", null, Element);
        }

        BattleActionConfig ResolveSkillAction()
        {
            CharacterBattleActionData data = skillAction ?? CreateSkillActionDefault();
            return data.Resolve(BattleActionKind.Skill, SkillName, skillIcon, Element);
        }

        BattleActionConfig ResolveUltimateAction()
        {
            CharacterBattleActionData data = ultimateAction ?? CreateUltimateActionDefault();
            return data.Resolve(BattleActionKind.Ultimate, "필살기", skillIcon, Element);
        }

        void EnsureBattleActionDefaults()
        {
            if (basicAction == null) basicAction = CreateBasicActionDefault();
            if (skillAction == null) skillAction = CreateSkillActionDefault();
            if (ultimateAction == null) ultimateAction = CreateUltimateActionDefault();
        }

        CharacterBattleActionData CreateBasicActionDefault()
        {
            float multiplier = role == CharacterRole.Striker ? 1f
                : role == CharacterRole.Special || role == CharacterRole.Support ? .9f : .8f;
            int breakAmount = role == CharacterRole.Special ? 40 : 30;
            return CharacterBattleActionData.Create("일반 공격", null,
                BattleTargetType.SingleEnemy, multiplier, 0f, 0, -1, 0, 20, breakAmount);
        }

        CharacterBattleActionData CreateSkillActionDefault()
        {
            if (role == CharacterRole.Healer)
                return CharacterBattleActionData.Create(SkillName, skillIcon,
                    BattleTargetType.SingleAlly, 0f, .22f, 120, 1, 0, 30, 0);
            if (role == CharacterRole.Support)
                return CharacterBattleActionData.Create(SkillName, skillIcon,
                    BattleTargetType.SingleAlly, 0f, 0f, 0, 1, 0, 30, 0);
            if (role == CharacterRole.Tank)
                return CharacterBattleActionData.Create(SkillName, skillIcon,
                    BattleTargetType.AllAllies, 0f, .15f, 100, 1, 0, 30, 0);
            if (role == CharacterRole.Special)
                return CharacterBattleActionData.Create(SkillName, skillIcon,
                    BattleTargetType.AdjacentEnemies, 1.5f, 0f, 0, 1, 0, 30, 70);
            return CharacterBattleActionData.Create(SkillName, skillIcon,
                BattleTargetType.SingleEnemy, 2.2f, 0f, 0, 1, 0, 30, 60);
        }

        CharacterBattleActionData CreateUltimateActionDefault()
        {
            int energyCost = ResolveMaxEnergy();
            if (role == CharacterRole.Healer || role == CharacterRole.Tank)
                return CharacterBattleActionData.Create("수정 방벽", skillIcon,
                    BattleTargetType.AllAllies, 0f, .15f, 100, 0, energyCost, 0, 0);
            if (role == CharacterRole.Support)
                return CharacterBattleActionData.Create("승리의 신호", skillIcon,
                    BattleTargetType.AllAllies, 0f, 0f, 0, 0, energyCost, 0, 0);
            if (role == CharacterRole.Special)
                return CharacterBattleActionData.Create("낙뢰 봉쇄", skillIcon,
                    BattleTargetType.AllEnemies, 1.8f, 0f, 0, 0, energyCost, 0, 45);
            return CharacterBattleActionData.Create("적색 소각선", skillIcon,
                BattleTargetType.SingleEnemy, 4.2f, 0f, 0, 0, energyCost, 0, 90);
        }

        BattleElement ResolveElement()
        {
            if (battleElement >= BattleElement.Fire && battleElement <= BattleElement.Dark)
                return battleElement;
            switch (role)
            {
                case CharacterRole.Striker: return BattleElement.Fire;
                case CharacterRole.Support: return BattleElement.Light;
                case CharacterRole.Tank: return BattleElement.Wind;
                case CharacterRole.Healer: return BattleElement.Ice;
                default: return (BattleElement)(StableHash(Id) % 6);
            }
        }

        static int StableIconIndex(string value)
        {
            return (int)(StableHash(value) % 5);
        }

        static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return hash;
            }
        }
    }
}
