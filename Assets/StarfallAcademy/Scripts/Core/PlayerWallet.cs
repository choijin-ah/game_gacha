using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // 프로토타입용 공용 재화 저장소입니다. 실제 서비스에서는 서버 저장으로 교체하세요.
    public static class PlayerWallet
    {
        const string PremiumCurrencyKey = "StarfallAcademy.PremiumCurrency";
        const string CreditsKey = "StarfallAcademy.Credits";
        const string SkillMaterialsKey = "StarfallAcademy.SkillMaterials";
        public const string PremiumCurrencyDisplayName = "별의 결정";
        public const string SkillMaterialDisplayName = "스킬 코어";
        public const int DefaultPremiumCurrency = 12800;
        public const int DefaultCredits = 8632100;
        public const int DefaultSkillMaterials = 2450;

        public static int PremiumCurrency
        {
            get
            {
                if (!PlayerPrefs.HasKey(PremiumCurrencyKey))
                    PlayerPrefs.SetInt(PremiumCurrencyKey, DefaultPremiumCurrency);
                return Mathf.Max(0, PlayerPrefs.GetInt(PremiumCurrencyKey, DefaultPremiumCurrency));
            }
        }

        public static int Credits => GetOrCreate(CreditsKey, DefaultCredits);
        public static int SkillMaterials => GetOrCreate(SkillMaterialsKey, DefaultSkillMaterials);

        public static bool TrySpendPremiumCurrency(int amount)
        {
            amount = Mathf.Max(0, amount);
            int current = PremiumCurrency;
            if (current < amount) return false;
            PlayerPrefs.SetInt(PremiumCurrencyKey, current - amount);
            PlayerPrefs.Save();
            return true;
        }

        public static void AddPremiumCurrency(int amount)
        {
            long next = (long)PremiumCurrency + amount;
            PlayerPrefs.SetInt(PremiumCurrencyKey, ClampWalletValue(next));
            PlayerPrefs.Save();
        }

        public static bool TrySpendCredits(int amount) => TrySpend(CreditsKey, DefaultCredits, amount);
        public static bool TrySpendSkillMaterials(int amount) => TrySpend(SkillMaterialsKey, DefaultSkillMaterials, amount);
        public static void AddCredits(int amount) => Add(CreditsKey, DefaultCredits, amount);
        public static void AddSkillMaterials(int amount) => Add(SkillMaterialsKey, DefaultSkillMaterials, amount);

        static int GetOrCreate(string key, int defaultValue)
        {
            if (!PlayerPrefs.HasKey(key))
                PlayerPrefs.SetInt(key, defaultValue);
            return Mathf.Max(0, PlayerPrefs.GetInt(key, defaultValue));
        }

        static bool TrySpend(string key, int defaultValue, int amount)
        {
            amount = Mathf.Max(0, amount);
            int current = GetOrCreate(key, defaultValue);
            if (current < amount) return false;
            PlayerPrefs.SetInt(key, current - amount);
            PlayerPrefs.Save();
            return true;
        }

        static void Add(string key, int defaultValue, int amount)
        {
            long next = (long)GetOrCreate(key, defaultValue) + amount;
            PlayerPrefs.SetInt(key, ClampWalletValue(next));
            PlayerPrefs.Save();
        }

        static int ClampWalletValue(long value)
        {
            if (value <= 0) return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
