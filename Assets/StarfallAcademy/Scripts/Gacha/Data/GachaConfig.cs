using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "GachaConfig", menuName = "Starfall/Gacha Configuration")]
    public sealed class GachaConfig : ScriptableObject
    {
        [Header("Banner")]
        [SerializeField] string bannerTitle = "별을 잇는 인연";
        [SerializeField] string bannerSubtitle = "SELECTED PICKUP";
        [SerializeField] string pityGroupId = "standard_pickup";
        [SerializeField] List<CharacterData> pickupCharacters = new List<CharacterData>();

        [Header("Rates (%)")]
        [Tooltip("한 번 모집에서 5성 이상이 나올 기본 확률")]
        [SerializeField, Range(0.01f, 100f)] float topRarityRatePercent = 3f;
        [Tooltip("5성 이상 등장 시 현재 선택한 픽업 캐릭터일 조건부 확률")]
        [SerializeField, Range(0f, 100f)] float featuredSharePercent = 50f;
        [SerializeField, Range(0f, 100f)] float fourStarRatePercent = 15f;

        [Header("Pity")]
        [SerializeField, Min(1)] int hardPity = 80;
        [SerializeField, Min(1)] int softPityStart = 70;
        [SerializeField, Min(0f)] float softPityBonusPerPullPercent = 6f;
        [SerializeField] bool guaranteeFeaturedAfterMiss = true;
        [SerializeField] bool guaranteeFourStarOnTenPull = true;

        [Header("Cost")]
        [SerializeField, Min(0)] int singlePullCost = 160;
        [SerializeField, Min(0)] int tenPullCost = 1600;

        public string BannerTitle => bannerTitle;
        public string BannerSubtitle => bannerSubtitle;
        public string PityGroupId => string.IsNullOrWhiteSpace(pityGroupId) ? "default" : pityGroupId;
        public IReadOnlyList<CharacterData> PickupCharacters => pickupCharacters;
        public float TopRarityRatePercent => topRarityRatePercent;
        public float FeaturedSharePercent => featuredSharePercent;
        public float FourStarRatePercent => fourStarRatePercent;
        public float ThreeStarRatePercent =>
            Mathf.Max(0f, 100f - topRarityRatePercent - fourStarRatePercent);
        public float EffectiveSelectedPickupRatePercent => topRarityRatePercent * featuredSharePercent / 100f;
        public int HardPity => hardPity;
        public int SoftPityStart => softPityStart;
        public float SoftPityBonusPerPullPercent => softPityBonusPerPullPercent;
        public bool GuaranteeFeaturedAfterMiss => guaranteeFeaturedAfterMiss;
        public bool GuaranteeFourStarOnTenPull => guaranteeFourStarOnTenPull;
        public int SinglePullCost => singlePullCost;
        public int TenPullCost => tenPullCost;

        public void AddPickup(CharacterData character)
        {
            if (character != null && !pickupCharacters.Contains(character)) pickupCharacters.Add(character);
        }

        public void RemovePickup(CharacterData character) => pickupCharacters.Remove(character);

        void OnValidate()
        {
            topRarityRatePercent = Mathf.Clamp(topRarityRatePercent, .01f, 100f);
            featuredSharePercent = Mathf.Clamp(featuredSharePercent, 0f, 100f);
            fourStarRatePercent = Mathf.Clamp(fourStarRatePercent, 0f, 100f - topRarityRatePercent);
            hardPity = Mathf.Max(1, hardPity);
            softPityStart = Mathf.Clamp(softPityStart, 1, hardPity);
            softPityBonusPerPullPercent = Mathf.Max(0, softPityBonusPerPullPercent);
            singlePullCost = Mathf.Max(0, singlePullCost);
            tenPullCost = Mathf.Max(0, tenPullCost);
        }
    }
}
