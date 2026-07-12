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
        public int Rarity => Character != null ? Character.Rarity : 0;

        public GachaResult(CharacterData character, bool isFeatured, bool isNew = false)
        {
            Character = character;
            IsFeatured = isFeatured;
            IsNew = isNew;
        }
    }

    public sealed class GachaPullResponse
    {
        public bool Success { get; }
        public string Error { get; }
        public IReadOnlyList<GachaResult> Results { get; }
        public int SpentCurrency { get; }

        GachaPullResponse(bool success, string error, IReadOnlyList<GachaResult> results, int spentCurrency)
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

    // 확률 계산과 PlayerPrefs 상태를 UI에서 분리한 프로토타입 모집 서비스입니다.
    public sealed class GachaService
    {
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
            if (config == null) return GachaPullResponse.Failed("가챠 설정 에셋이 없습니다");
            if (database == null || database.Characters.Count == 0)
                return GachaPullResponse.Failed("캐릭터 데이터가 없습니다");
            if (selectedPickup == null || selectedPickup.Rarity < 5)
                return GachaPullResponse.Failed("5성 이상 픽업 캐릭터를 선택하세요");
            if (count != 1 && count != 10)
                return GachaPullResponse.Failed("지원하지 않는 모집 횟수입니다");

            int cost = count == 1 ? config.SinglePullCost : config.TenPullCost;
            if (!PlayerWallet.TrySpendPremiumCurrency(cost))
                return GachaPullResponse.Failed(PlayerWallet.PremiumCurrencyDisplayName + "이 부족합니다");

            var results = new List<GachaResult>(count);
            bool hasFourStarOrHigher = false;
            for (int i = 0; i < count; i++)
            {
                bool forceFourStar = count == 10 && config.GuaranteeFourStarOnTenPull && i == count - 1 && !hasFourStarOrHigher;
                GachaResult result = PullOne(selectedPickup, forceFourStar);
                results.Add(result);
                if (result.Rarity >= 4) hasFourStarOrHigher = true;
            }
            SaveState();
            return GachaPullResponse.Completed(results, cost);
        }

        GachaResult PullOne(CharacterData selectedPickup, bool forceFourStar)
        {
            int nextPull = pityCount + 1;
            float topRate = config.TopRarityRatePercent;
            if (nextPull >= config.SoftPityStart)
                topRate += (nextPull - config.SoftPityStart + 1) * config.SoftPityBonusPerPullPercent;
            bool topRarity = nextPull >= config.HardPity || RollPercent(Mathf.Min(100f, topRate));

            if (topRarity)
            {
                pityCount = 0;
                bool featured = featuredGuaranteed || RollPercent(config.FeaturedSharePercent);
                CharacterData result;
                if (featured)
                {
                    result = selectedPickup;
                    featuredGuaranteed = false;
                }
                else
                {
                    result = PickRandomCharacter(5, selectedPickup);
                    if (result == null || result == selectedPickup)
                    {
                        result = selectedPickup;
                        featured = true;
                        featuredGuaranteed = false;
                    }
                    else if (config.GuaranteeFeaturedAfterMiss)
                        featuredGuaranteed = true;
                }
                return new GachaResult(result, featured, CharacterProgressionService.RegisterPull(result));
            }

            pityCount++;
            int rarity = forceFourStar || RollPercent(config.FourStarRatePercent) ? 4 : 3;
            CharacterData character = PickRandomCharacter(rarity, null);
            if (character == null)
                character = PickAnyCharacterBelow(5);
            if (character == null)
                character = PickAnyCharacter();
            return new GachaResult(character, false, CharacterProgressionService.RegisterPull(character));
        }

        CharacterData PickRandomCharacter(int minimumRarity, CharacterData exclude)
        {
            var pool = new List<CharacterData>();
            foreach (CharacterData character in database.Characters)
            {
                if (character == null || character == exclude) continue;
                bool matches = minimumRarity >= 5 ? character.Rarity >= 5 : character.Rarity == minimumRarity;
                if (matches) pool.Add(character);
            }
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        CharacterData PickAnyCharacterBelow(int rarity)
        {
            var pool = new List<CharacterData>();
            foreach (CharacterData character in database.Characters)
                if (character != null && character.Rarity < rarity) pool.Add(character);
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        CharacterData PickAnyCharacter()
        {
            var pool = new List<CharacterData>();
            foreach (CharacterData character in database.Characters)
                if (character != null) pool.Add(character);
            return pool.Count > 0 ? pool[random.Next(pool.Count)] : null;
        }

        bool RollPercent(float percent) => random.NextDouble() * 100.0 < Mathf.Clamp(percent, 0f, 100f);

        void SaveState()
        {
            PlayerPrefs.SetInt(pityKey, pityCount);
            PlayerPrefs.SetInt(featuredGuaranteeKey, featuredGuaranteed ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
