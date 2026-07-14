using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public struct CurrencyReward
    {
        [SerializeField, Min(0)] int credits;
        [SerializeField, Min(0)] int skillMaterials;
        [SerializeField, Min(0)] int accountExperience;
        [SerializeField, Min(0)] int premiumCurrency;

        public CurrencyReward(int credits, int skillMaterials, int accountExperience,
            int premiumCurrency)
        {
            this.credits = Mathf.Max(0, credits);
            this.skillMaterials = Mathf.Max(0, skillMaterials);
            this.accountExperience = Mathf.Max(0, accountExperience);
            this.premiumCurrency = Mathf.Max(0, premiumCurrency);
        }

        public int Credits => Mathf.Max(0, credits);
        public int SkillMaterials => Mathf.Max(0, skillMaterials);
        public int AccountExperience => Mathf.Max(0, accountExperience);
        public int PremiumCurrency => Mathf.Max(0, premiumCurrency);
        public bool IsValid => credits >= 0 && skillMaterials >= 0
            && accountExperience >= 0 && premiumCurrency >= 0;
        public bool IsEmpty => Credits == 0 && SkillMaterials == 0
            && AccountExperience == 0 && PremiumCurrency == 0;

        public RewardBundle ToRewardBundle() => new RewardBundle(
            Credits, SkillMaterials, AccountExperience, PremiumCurrency);
    }

    [Serializable]
    public sealed class ItemReward
    {
        [SerializeField] string itemId;
        [SerializeField, Min(1)] int amount = 1;

        public ItemReward()
        {
        }

        public ItemReward(string itemId, int amount)
        {
            this.itemId = itemId == null ? string.Empty : itemId.Trim();
            this.amount = Mathf.Max(1, amount);
        }

        public string ItemId => itemId == null ? string.Empty : itemId.Trim();
        public int Amount => Mathf.Max(0, amount);
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && amount > 0;
    }

    /// <summary>
    /// Reusable content reward. Currency keeps compatibility with RewardBundle while item IDs
    /// cover equipment, recruitment tickets and character fragments without coupling content
    /// definitions to a particular inventory implementation.
    /// </summary>
    [Serializable]
    public sealed class RewardPackage
    {
        [SerializeField] CurrencyReward currencyReward;
        [SerializeField] List<ItemReward> itemRewards = new List<ItemReward>();

        public RewardPackage()
        {
        }

        public RewardPackage(CurrencyReward currency, IEnumerable<ItemReward> items = null)
        {
            currencyReward = currency;
            itemRewards = items == null
                ? new List<ItemReward>()
                : new List<ItemReward>(items.Where(item => item != null));
        }

        public CurrencyReward Currency => currencyReward;
        public IReadOnlyList<ItemReward> ItemRewards => itemRewards ?? (IReadOnlyList<ItemReward>)Array.Empty<ItemReward>();
        public bool IsEmpty => Currency.IsEmpty && (itemRewards == null
            || itemRewards.All(item => item == null || item.Amount <= 0));

        public bool IsValid
        {
            get
            {
                if (!currencyReward.IsValid || itemRewards == null) return false;
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < itemRewards.Count; i++)
                {
                    ItemReward item = itemRewards[i];
                    if (item == null || !item.IsValid || !ids.Add(item.ItemId)) return false;
                }
                return true;
            }
        }

        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (Currency.Credits > 0) parts.Add("Credits " + Currency.Credits.ToString("N0"));
                if (Currency.SkillMaterials > 0) parts.Add("Skill Materials " + Currency.SkillMaterials.ToString("N0"));
                if (Currency.AccountExperience > 0) parts.Add("Account EXP " + Currency.AccountExperience.ToString("N0"));
                if (Currency.PremiumCurrency > 0) parts.Add("Premium " + Currency.PremiumCurrency.ToString("N0"));
                if (itemRewards != null)
                {
                    for (int i = 0; i < itemRewards.Count; i++)
                    {
                        ItemReward item = itemRewards[i];
                        if (item != null && item.Amount > 0)
                            parts.Add(item.ItemId + " x" + item.Amount.ToString("N0"));
                    }
                }
                return parts.Count == 0 ? "No rewards" : string.Join(", ", parts);
            }
        }
    }

    /// <summary>Small ID-based inventory used by content rewards and debug tooling.</summary>
    public sealed class ItemInventoryService
    {
        readonly IMetaStorage storage;

        public ItemInventoryService(IMetaStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public static ItemInventoryService Default { get; } =
            new ItemInventoryService(PlayerPrefsMetaStorage.Shared);

        public int GetAmount(string itemId)
        {
            string normalized = NormalizeItemId(itemId);
            return string.IsNullOrEmpty(normalized)
                ? 0 : Math.Max(0, storage.GetInt(Key(normalized), 0));
        }

        internal int StageGrant(string itemId, int amount)
        {
            string normalized = NormalizeItemId(itemId);
            if (string.IsNullOrEmpty(normalized) || amount < 0)
                throw new ArgumentException("A valid item ID and non-negative amount are required.");
            PlayerDataKeyManifest.TrackItemKey(storage, Key(normalized));
            int next = SaturatingAdd(GetAmount(normalized), amount);
            storage.SetInt(Key(normalized), next);
            return next;
        }

        internal bool TryStageSpend(string itemId, int amount, out int remaining)
        {
            string normalized = NormalizeItemId(itemId);
            int current = GetAmount(normalized);
            if (string.IsNullOrEmpty(normalized) || amount < 0 || current < amount)
            {
                remaining = current;
                return false;
            }
            PlayerDataKeyManifest.TrackItemKey(storage, Key(normalized));
            remaining = current - amount;
            storage.SetInt(Key(normalized), remaining);
            return true;
        }

        internal void Restore(string itemId, bool hadValue, int previousValue)
        {
            string key = Key(NormalizeItemId(itemId));
            if (hadValue) storage.SetInt(key, Math.Max(0, previousValue));
            else storage.DeleteKey(key);
        }

        internal bool HasStoredValue(string itemId) => storage.HasKey(Key(NormalizeItemId(itemId)));

        public static string StorageKeyFor(string itemId) => Key(NormalizeItemId(itemId));

        public static void TrackPlayerPrefsKey(string key)
        {
            PlayerDataKeyManifest.TrackItemKey(PlayerPrefsMetaStorage.Shared, key);
        }

        internal static void AppendGrantWrite(string itemId, int amount,
            ICollection<MetaIntWrite> writes)
        {
            if (writes == null || amount <= 0) return;
            string normalized = NormalizeItemId(itemId);
            if (string.IsNullOrEmpty(normalized)) return;
            string key = Key(normalized);
            int current = Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
            writes.Add(new MetaIntWrite(key, SaturatingAdd(current, amount)));
        }

        internal static bool TryAppendSpendWrite(string itemId, int amount,
            ICollection<MetaIntWrite> writes)
        {
            if (writes == null) return false;
            string normalized = NormalizeItemId(itemId);
            if (string.IsNullOrEmpty(normalized)) return false;
            amount = Math.Max(0, amount);
            string key = Key(normalized);
            int current = Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
            if (current < amount) return false;
            writes.Add(new MetaIntWrite(key, current - amount));
            return true;
        }

        public static string CharacterFragmentId(string characterId) =>
            "character_fragment:" + NormalizeItemId(characterId);

        public static string NormalizeItemId(string itemId) =>
            itemId == null ? string.Empty : itemId.Trim().ToLowerInvariant();

        static string Key(string normalizedItemId)
        {
            if (string.IsNullOrEmpty(normalizedItemId))
                return PlayerDataKeyManifest.ItemKeyPrefix + "invalid";
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedItemId));
                var builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return PlayerDataKeyManifest.ItemKeyPrefix + builder;
            }
        }

        internal static int SaturatingAdd(int current, int amount)
        {
            long total = (long)Math.Max(0, current) + Math.Max(0, amount);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }
    }

    public sealed class RewardPackageService
    {
        readonly IMetaStorage storage;
        readonly RewardService currencyService;
        readonly ItemInventoryService itemInventory;

        public RewardPackageService(IMetaStorage storage, PlayerProfileService profile = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            currencyService = new RewardService(storage, profile);
            itemInventory = new ItemInventoryService(storage);
        }

        public static RewardPackageService Default { get; } =
            new RewardPackageService(PlayerPrefsMetaStorage.Shared, PlayerProfileService.Default);

        public RewardGrantResult Grant(string transactionId, RewardPackage package)
        {
            return Grant(transactionId, package, null, null);
        }

        internal RewardGrantResult Grant(string transactionId, RewardPackage package,
            Action beforeCommit, Action rollbackParticipant)
        {
            if (package == null || !package.IsValid)
                return currencyService.GrantReward(transactionId,
                    new RewardBundle(-1, 0, 0, 0));

            var snapshots = new List<ItemSnapshot>();
            for (int i = 0; i < package.ItemRewards.Count; i++)
            {
                ItemReward item = package.ItemRewards[i];
                snapshots.Add(new ItemSnapshot(item.ItemId,
                    itemInventory.HasStoredValue(item.ItemId), itemInventory.GetAmount(item.ItemId)));
            }

            return currencyService.GrantReward(transactionId, package.Currency.ToRewardBundle(),
                () =>
                {
                    for (int i = 0; i < package.ItemRewards.Count; i++)
                    {
                        ItemReward item = package.ItemRewards[i];
                        itemInventory.StageGrant(item.ItemId, item.Amount);
                    }
                    beforeCommit?.Invoke();
                },
                () =>
                {
                    try
                    {
                        rollbackParticipant?.Invoke();
                    }
                    finally
                    {
                        for (int i = 0; i < snapshots.Count; i++)
                        {
                            ItemSnapshot snapshot = snapshots[i];
                            itemInventory.Restore(snapshot.ItemId, snapshot.HadValue,
                                snapshot.Amount);
                        }
                    }
                });
        }

        readonly struct ItemSnapshot
        {
            public ItemSnapshot(string itemId, bool hadValue, int amount)
            {
                ItemId = itemId;
                HadValue = hadValue;
                Amount = amount;
            }

            public string ItemId { get; }
            public bool HadValue { get; }
            public int Amount { get; }
        }
    }
}
