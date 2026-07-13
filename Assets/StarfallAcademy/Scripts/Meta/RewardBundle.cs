using System;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public readonly struct RewardBundle : IEquatable<RewardBundle>
    {
        public static RewardBundle Empty => new RewardBundle(0, 0, 0, 0);

        public RewardBundle(int credits, int skillMaterials, int accountExperience)
            : this(credits, skillMaterials, accountExperience, 0)
        {
        }

        public RewardBundle(int credits, int skillMaterials, int accountExperience,
            int premiumCurrency)
        {
            Credits = credits;
            SkillMaterials = skillMaterials;
            AccountExperience = accountExperience;
            PremiumCurrency = premiumCurrency;
        }

        public int Credits { get; }
        public int SkillMaterials { get; }
        public int AccountExperience { get; }
        public int PremiumCurrency { get; }

        public bool IsValid => Credits >= 0 && SkillMaterials >= 0 && AccountExperience >= 0
            && PremiumCurrency >= 0;
        public bool IsEmpty => Credits == 0 && SkillMaterials == 0 && AccountExperience == 0
            && PremiumCurrency == 0;

        public bool Equals(RewardBundle other) =>
            Credits == other.Credits
            && SkillMaterials == other.SkillMaterials
            && AccountExperience == other.AccountExperience
            && PremiumCurrency == other.PremiumCurrency;

        public override bool Equals(object obj) => obj is RewardBundle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Credits;
                hash = (hash * 397) ^ SkillMaterials;
                hash = (hash * 397) ^ AccountExperience;
                return (hash * 397) ^ PremiumCurrency;
            }
        }

        public override string ToString() =>
            $"Credits={Credits}, SkillMaterials={SkillMaterials}, AccountExp={AccountExperience}, "
            + $"PremiumCurrency={PremiumCurrency}";
    }
}
