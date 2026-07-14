using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class EquipmentSetEffect
    {
        [SerializeField] EquipmentStatType stat = EquipmentStatType.Attack;
        [SerializeField] float value;
        [SerializeField] bool percentage = true;
        [SerializeField, TextArea(1, 3)] string description;

        public EquipmentStatType Stat => stat;
        public float Value => value;
        public bool Percentage => percentage;
        public string Description => description ?? string.Empty;
    }

    [CreateAssetMenu(fileName = "EquipmentSet", menuName = "Starfall/Equipment/Set")]
    public sealed class EquipmentSetDefinition : ScriptableObject
    {
        [SerializeField] string setId;
        [SerializeField] string displayName = "새 장비 세트";
        [SerializeField] EquipmentSetEffect twoPieceEffect = new EquipmentSetEffect();
        [SerializeField] EquipmentSetEffect fourPieceEffect = new EquipmentSetEffect();

        public string Id => string.IsNullOrWhiteSpace(setId) ? name : setId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public EquipmentSetEffect TwoPieceEffect => twoPieceEffect;
        public EquipmentSetEffect FourPieceEffect => fourPieceEffect;
    }
}
