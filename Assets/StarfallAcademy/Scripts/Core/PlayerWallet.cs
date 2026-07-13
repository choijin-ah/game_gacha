using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // 프로토타입용 공용 재화 저장소입니다. 실제 서비스에서는 서버 저장으로 교체하세요.
    public static class PlayerWallet
    {
        internal const string PremiumCurrencyKey = "StarfallAcademy.PremiumCurrency";
        internal const string CreditsKey = "StarfallAcademy.Credits";
        internal const string SkillMaterialsKey = "StarfallAcademy.SkillMaterials";
        public const string PremiumCurrencyDisplayName = "별의 결정";
        public const string SkillMaterialDisplayName = "스킬 코어";
        public const int DefaultPremiumCurrency = 12800;
        public const int DefaultCredits = 8632100;
        public const int DefaultSkillMaterials = 2450;

        public static int PremiumCurrency
        {
            get
            {
                MetaPlayerPrefsTransaction.RecoverPending();
                if (!PlayerPrefs.HasKey(PremiumCurrencyKey))
                    PlayerPrefs.SetInt(PremiumCurrencyKey, DefaultPremiumCurrency);
                return Mathf.Max(0, PlayerPrefs.GetInt(PremiumCurrencyKey, DefaultPremiumCurrency));
            }
        }

        public static int Credits => GetOrCreate(CreditsKey, DefaultCredits);
        public static int SkillMaterials => GetOrCreate(SkillMaterialsKey, DefaultSkillMaterials);

        public static bool TrySpendPremiumCurrency(int amount)
        {
            var writes = new List<MetaIntWrite>(1);
            return TryStagePremiumCurrencySpend(amount, writes)
                && MetaPlayerPrefsTransaction.Commit(writes);
        }

        internal static bool TryStagePremiumCurrencySpend(int amount,
            ICollection<MetaIntWrite> writes) =>
            TryStageSpend(PremiumCurrencyKey, DefaultPremiumCurrency, amount, writes);

        internal static bool TryStageCreditsSpend(int amount,
            ICollection<MetaIntWrite> writes) =>
            TryStageSpend(CreditsKey, DefaultCredits, amount, writes);

        internal static bool TryStageSkillMaterialsSpend(int amount,
            ICollection<MetaIntWrite> writes) =>
            TryStageSpend(SkillMaterialsKey, DefaultSkillMaterials, amount, writes);

        internal static int StageSkillMaterialsGrant(int amount,
            ICollection<MetaIntWrite> writes)
        {
            int current = GetOrCreate(SkillMaterialsKey, DefaultSkillMaterials);
            int next = ClampWalletValue((long)current + Mathf.Max(0, amount));
            writes.Add(new MetaIntWrite(SkillMaterialsKey, next));
            return next - current;
        }

        public static void AddPremiumCurrency(int amount)
        {
            long next = (long)PremiumCurrency + amount;
            MetaPlayerPrefsTransaction.Commit(new[]
            {
                new MetaIntWrite(PremiumCurrencyKey, ClampWalletValue(next))
            });
        }

        public static bool TrySpendCredits(int amount) => TrySpend(CreditsKey, DefaultCredits, amount);
        public static bool TrySpendSkillMaterials(int amount) => TrySpend(SkillMaterialsKey, DefaultSkillMaterials, amount);
        public static void AddCredits(int amount) => Add(CreditsKey, DefaultCredits, amount);
        public static void AddSkillMaterials(int amount) => Add(SkillMaterialsKey, DefaultSkillMaterials, amount);

        public static bool TryExchangePremiumForCredits(int premiumCost, int creditAmount) =>
            TryExchange(PremiumCurrencyKey, DefaultPremiumCurrency, premiumCost,
                CreditsKey, DefaultCredits, creditAmount);

        public static bool TryExchangeCreditsForSkillMaterials(int creditCost, int materialAmount) =>
            TryExchange(CreditsKey, DefaultCredits, creditCost,
                SkillMaterialsKey, DefaultSkillMaterials, materialAmount);

        static int GetOrCreate(string key, int defaultValue)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            if (!PlayerPrefs.HasKey(key))
                PlayerPrefs.SetInt(key, defaultValue);
            return Mathf.Max(0, PlayerPrefs.GetInt(key, defaultValue));
        }

        static bool TrySpend(string key, int defaultValue, int amount)
        {
            var writes = new List<MetaIntWrite>(1);
            return TryStageSpend(key, defaultValue, amount, writes)
                && MetaPlayerPrefsTransaction.Commit(writes);
        }

        static void Add(string key, int defaultValue, int amount)
        {
            long next = (long)GetOrCreate(key, defaultValue) + amount;
            MetaPlayerPrefsTransaction.Commit(new[]
            {
                new MetaIntWrite(key, ClampWalletValue(next))
            });
        }

        static bool TryExchange(string sourceKey, int sourceDefault, int cost,
            string targetKey, int targetDefault, int amount)
        {
            cost = Mathf.Max(0, cost);
            amount = Mathf.Max(0, amount);
            if (amount == 0) return cost == 0;
            int current = GetOrCreate(sourceKey, sourceDefault);
            if (current < cost) return false;
            int target = GetOrCreate(targetKey, targetDefault);
            long nextTarget = (long)target + amount;
            if (nextTarget > int.MaxValue) return false;
            return MetaPlayerPrefsTransaction.Commit(new[]
            {
                new MetaIntWrite(sourceKey, current - cost),
                new MetaIntWrite(targetKey, (int)nextTarget)
            });
        }

        static bool TryStageSpend(string key, int defaultValue, int amount,
            ICollection<MetaIntWrite> writes)
        {
            if (writes == null) return false;
            amount = Mathf.Max(0, amount);
            int current = GetOrCreate(key, defaultValue);
            if (current < amount) return false;
            writes.Add(new MetaIntWrite(key, current - amount));
            return true;
        }

        internal static int ClampWalletValue(long value)
        {
            if (value <= 0) return 0;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
