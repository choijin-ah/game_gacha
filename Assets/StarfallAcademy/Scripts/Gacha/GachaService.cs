using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class GachaResult
    {
        public CharacterData Character { get; }
        public bool IsFeatured { get; }
        public bool IsNew { get; }
        public int DuplicateSkillMaterials { get; }
        public int Rarity => Character != null ? Character.Rarity : 0;

        public GachaResult(CharacterData character, bool isFeatured, bool isNew = false,
            int duplicateSkillMaterials = 0)
        {
            Character = character;
            IsFeatured = isFeatured;
            IsNew = isNew;
            DuplicateSkillMaterials = Mathf.Max(0, duplicateSkillMaterials);
        }
    }

    public sealed class GachaPullResponse
    {
        public bool Success { get; }
        public string Error { get; }
        public IReadOnlyList<GachaResult> Results { get; }
        public int SpentCurrency { get; }

        GachaPullResponse(bool success, string error, IReadOnlyList<GachaResult> results,
            int spentCurrency)
        {
            Success = success;
            Error = error;
            Results = results;
            SpentCurrency = spentCurrency;
        }

        public static GachaPullResponse Failed(string error) =>
            new GachaPullResponse(false, error, Array.Empty<GachaResult>(), 0);

        public static GachaPullResponse Completed(List<GachaResult> results, int spent) =>
            new GachaPullResponse(true, string.Empty, results, spent);
    }

    // Probability calculation and all persistent results are kept outside the UI.
    // A pull is generated first and then wallet, pity, ownership and duplicate rewards
    // are committed together through the recoverable PlayerPrefs transaction journal.
    public sealed class GachaService
    {
        const float RateEpsilon = .0001f;

        readonly GachaConfig config;
        readonly CharacterDatabase database;
        readonly System.Random random;
        readonly string pityKey;
        readonly string featuredGuaranteeKey;
        int pityCount;
        bool featuredGuaranteed;

        public int PityCount => pityCount;
        public bool FeaturedGuaranteed => featuredGuaranteed;
        public int Currency => PlayerWallet.PremiumCurrency;

        public GachaService(GachaConfig config, CharacterDatabase database, int? randomSeed = null)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            this.config = config;
            this.database = database;
            random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random();
            string group = config != null ? config.PityGroupId : "default";
            pityKey = "StarfallAcademy.Gacha.Pity." + group;
            featuredGuaranteeKey = "StarfallAcademy.Gacha.FeaturedGuarantee." + group;
            pityCount = Mathf.Max(0, PlayerPrefs.GetInt(pityKey, 0));
            featuredGuaranteed = PlayerPrefs.GetInt(featuredGuaranteeKey, 0) == 1;
        }

        public GachaPullResponse Pull(int count, CharacterData selectedPickup)
        {
            if (!TryValidatePull(count, selectedPickup, out string validationError))
                return GachaPullResponse.Failed(validationError);

            int cost = count == 1 ? config.SinglePullCost : config.TenPullCost;
            if (Currency < cost)
                return GachaPullResponse.Failed(PlayerWallet.PremiumCurrencyDisplayName + "이 부족합니다.");

            int nextPityCount = pityCount;
            bool nextFeaturedGuaranteed = featuredGuaranteed;
            int compensationCapacity = int.MaxValue - PlayerWallet.SkillMaterials;
            int totalDuplicateMaterials = 0;
            bool hasFourStarOrHigher = false;
            var pulledCharacterIds = new HashSet<string>(StringComparer.Ordinal);
            var results = new List<GachaResult>(count);

            for (int i = 0; i < count; i++)
            {
                bool forceFourStar = count == 10 && config.GuaranteeFourStarOnTenPull
                    && i == count - 1 && !hasFourStarOrHigher;
                GachaResult result = PullOne(selectedPickup, forceFourStar,
                    ref nextPityCount, ref nextFeaturedGuaranteed, pulledCharacterIds,
                    ref compensationCapacity);
                if (result.Character == null)
                    return GachaPullResponse.Failed("가챠 등급 풀에서 캐릭터를 선택하지 못했습니다.");
                results.Add(result);
                totalDuplicateMaterials += result.DuplicateSkillMaterials;
                if (result.Rarity >= 4) hasFourStarOrHigher = true;
            }

            var writes = new List<MetaIntWrite>(4 + results.Count * 3);
            if (!PlayerWallet.TryStagePremiumCurrencySpend(cost, writes))
                return GachaPullResponse.Failed(PlayerWallet.PremiumCurrencyDisplayName + "이 부족합니다.");
            writes.Add(new MetaIntWrite(pityKey, nextPityCount));
            writes.Add(new MetaIntWrite(featuredGuaranteeKey, nextFeaturedGuaranteed ? 1 : 0));

            var registeredIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < results.Count; i++)
            {
                CharacterData character = results[i].Character;
                if (character != null && registeredIds.Add(character.Id))
                    CharacterProgressionService.AppendPullRegistrationWrites(character, writes);
            }
            if (totalDuplicateMaterials > 0)
                PlayerWallet.StageSkillMaterialsGrant(totalDuplicateMaterials, writes);

            if (!MetaPlayerPrefsTransaction.Commit(writes))
                return GachaPullResponse.Failed("가챠 결과를 저장하지 못했습니다. 다시 시도해 주세요.");

            pityCount = nextPityCount;
            featuredGuaranteed = nextFeaturedGuaranteed;
            return GachaPullResponse.Completed(results, cost);
        }

        public bool TryValidatePull(int count, CharacterData selectedPickup, out string error)
        {
            if (config == null)
            {
                error = "가챠 설정 에셋이 없습니다.";
                return false;
            }
            if (database == null || database.Characters == null || database.Characters.Count == 0)
            {
                error = "캐릭터 데이터가 없습니다.";
                return false;
            }
            if (selectedPickup == null || selectedPickup.Rarity < 5)
            {
                error = "5성 이상 픽업 캐릭터를 선택하세요.";
                return false;
            }
            if (count != 1 && count != 10)
            {
                error = "지원하지 않는 모집 횟수입니다.";
                return false;
            }
            if (!IsConfiguredPickup(selectedPickup))
            {
                error = "선택한 캐릭터가 현재 배너의 픽업 목록에 없습니다.";
                return false;
            }
            if (!ContainsCharacter(selectedPickup))
            {
                error = "선택한 픽업 캐릭터가 현재 배너 데이터베이스에 없습니다.";
                return false;
            }
            if (!HasCharacterPool(5, true))
            {
                error = "5성 이상 캐릭터 풀이 비어 있습니다.";
                return false;
            }

            return ValidateRarityPools(config.FourStarRatePercent,
                config.ThreeStarRatePercent, config.GuaranteeFourStarOnTenPull, count,
                true, HasCharacterPool(4, false), HasCharacterPool(3, false), out error);
        }

        GachaResult PullOne(CharacterData selectedPickup, bool forceFourStar,
            ref int nextPityCount, ref bool nextFeaturedGuaranteed,
            HashSet<string> pulledCharacterIds, ref int compensationCapacity)
        {
            int nextPull = nextPityCount + 1;
            float topRate = config.TopRarityRatePercent;
            if (nextPull >= config.SoftPityStart)
                topRate += (nextPull - config.SoftPityStart + 1)
                    * config.SoftPityBonusPerPullPercent;
            topRate = Mathf.Clamp(topRate, 0f, 100f);

            bool topRarity;
            int rarity;
            if (nextPull >= config.HardPity)
            {
                topRarity = true;
                rarity = 5;
            }
            else
            {
                double roll = random.NextDouble() * 100.0;
                rarity = SelectRarity(roll, topRate, config.FourStarRatePercent,
                    forceFourStar);
                topRarity = rarity >= 5;
            }

            CharacterData result;
            bool featured = false;
            if (topRarity)
            {
                nextPityCount = 0;
                featured = nextFeaturedGuaranteed || RollPercent(config.FeaturedSharePercent);
                if (featured)
                {
                    result = selectedPickup;
                    nextFeaturedGuaranteed = false;
                }
                else
                {
                    result = PickRandomCharacter(5, true, selectedPickup);
                    if (result == null)
                    {
                        // There is no off-banner top-rarity character. This remains a
                        // same-tier selection and is intentionally treated as featured.
                        result = selectedPickup;
                        featured = true;
                        nextFeaturedGuaranteed = false;
                    }
                    else if (config.GuaranteeFeaturedAfterMiss)
                    {
                        nextFeaturedGuaranteed = true;
                    }
                }
            }
            else
            {
                nextPityCount++;
                result = PickRandomCharacter(rarity, false, null);
            }

            bool alreadyOwned = result != null && (CharacterProgressionService.IsOwned(result)
                || pulledCharacterIds.Contains(result.Id));
            bool isNew = result != null && !alreadyOwned;
            if (result != null) pulledCharacterIds.Add(result.Id);

            int duplicateMaterials = alreadyOwned ? GetDuplicateSkillMaterialReward(result) : 0;
            duplicateMaterials = Mathf.Min(duplicateMaterials, Mathf.Max(0, compensationCapacity));
            compensationCapacity -= duplicateMaterials;
            return new GachaResult(result, featured, isNew, duplicateMaterials);
        }

        CharacterData PickRandomCharacter(int rarity, bool atLeastRarity, CharacterData exclude)
        {
            var pool = new List<CharacterData>();
            foreach (CharacterData character in database.Characters)
            {
                if (character == null || character == exclude) continue;
                bool matches = atLeastRarity ? character.Rarity >= rarity : character.Rarity == rarity;
                if (matches) pool.Add(character);
            }
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        bool ContainsCharacter(CharacterData selected)
        {
            foreach (CharacterData character in database.Characters)
                if (character == selected) return true;
            return false;
        }

        bool IsConfiguredPickup(CharacterData selected)
        {
            if (config == null || config.PickupCharacters == null) return false;
            foreach (CharacterData pickup in config.PickupCharacters)
                if (pickup == selected) return true;
            return false;
        }

        bool HasCharacterPool(int rarity, bool atLeastRarity)
        {
            foreach (CharacterData character in database.Characters)
            {
                if (character == null) continue;
                if (atLeastRarity ? character.Rarity >= rarity : character.Rarity == rarity)
                    return true;
            }
            return false;
        }

        static int GetDuplicateSkillMaterialReward(CharacterData character)
        {
            if (character == null) return 0;
            if (character.Rarity >= 5) return 50;
            if (character.Rarity == 4) return 20;
            return 10;
        }

        public static int SelectRarity(double rollPercent, float topRatePercent,
            float fourStarRatePercent, bool forceFourStar)
        {
            float topRate = Mathf.Clamp(topRatePercent, 0f, 100f);
            float fourRate = Mathf.Clamp(fourStarRatePercent, 0f, 100f - topRate);
            double roll = Math.Max(0.0, Math.Min(99.999999999, rollPercent));
            if (roll < topRate) return 5;
            if (forceFourStar || roll < topRate + fourRate) return 4;
            return 3;
        }

        public static bool ValidateRarityPools(float fourStarRatePercent,
            float threeStarRatePercent, bool guaranteeFourStarOnTenPull, int count,
            bool hasTopPool, bool hasFourStarPool, bool hasThreeStarPool, out string error)
        {
            if (!hasTopPool)
            {
                error = "5성 이상 캐릭터 풀이 비어 있습니다.";
                return false;
            }
            bool needsFourStarPool = fourStarRatePercent > RateEpsilon
                || (count == 10 && guaranteeFourStarOnTenPull);
            if (needsFourStarPool && !hasFourStarPool)
            {
                error = "4성 캐릭터 풀이 비어 있습니다.";
                return false;
            }
            if (threeStarRatePercent > RateEpsilon && !hasThreeStarPool)
            {
                error = "3성 캐릭터 풀이 비어 있습니다. 배너 확률 또는 캐릭터 데이터를 확인하세요.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        bool RollPercent(float percent) =>
            random.NextDouble() * 100.0 < Mathf.Clamp(percent, 0f, 100f);
    }
}
