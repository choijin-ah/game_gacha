using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "GachaBannerDatabase", menuName = "Starfall/Gacha Banner Database")]
    public sealed class GachaBannerDatabase : ScriptableObject
    {
        [SerializeField] List<GachaBannerData> banners = new List<GachaBannerData>();

        public IReadOnlyList<GachaBannerData> Banners => banners
            ?? (IReadOnlyList<GachaBannerData>)System.Array.Empty<GachaBannerData>();

        public GachaBannerData Find(string bannerId)
        {
            if (string.IsNullOrWhiteSpace(bannerId) || banners == null) return null;
            for (int i = 0; i < banners.Count; i++)
            {
                GachaBannerData banner = banners[i];
                if (banner != null && banner.Id == bannerId) return banner;
            }
            return null;
        }

        public bool Add(GachaBannerData banner)
        {
            if (banner == null) return false;
            if (banners == null) banners = new List<GachaBannerData>();
            if (banners.Contains(banner)) return false;
            banners.Add(banner);
            return true;
        }

        public bool Remove(GachaBannerData banner) =>
            banner != null && banners != null && banners.Remove(banner);

        public bool Move(int fromIndex, int toIndex)
        {
            if (banners == null || fromIndex < 0 || fromIndex >= banners.Count
                || toIndex < 0 || toIndex >= banners.Count || fromIndex == toIndex)
                return false;
            GachaBannerData banner = banners[fromIndex];
            banners.RemoveAt(fromIndex);
            banners.Insert(toIndex, banner);
            return true;
        }

        public int IndexOf(GachaBannerData banner) =>
            banners == null || banner == null ? -1 : banners.IndexOf(banner);

        void OnValidate()
        {
            if (banners == null) banners = new List<GachaBannerData>();
        }
    }
}
