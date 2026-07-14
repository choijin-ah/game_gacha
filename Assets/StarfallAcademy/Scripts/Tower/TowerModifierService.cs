using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class TowerModifierService : IBattleRuntimeModifier
    {
        readonly IReadOnlyList<TowerModifierDefinition> modifiers;
        readonly HashSet<Guid> appliedRequests = new HashSet<Guid>();

        public TowerModifierService(IReadOnlyList<TowerModifierDefinition> modifiers)
        {
            this.modifiers = modifiers ?? Array.Empty<TowerModifierDefinition>();
        }

        public void ModifyStats(BattleTeam team, BattleBaseStats stats)
        {
            if (stats == null || team != BattleTeam.Enemy) return;
            for (int i = 0; i < modifiers.Count; i++)
            {
                TowerModifierDefinition modifier = modifiers[i];
                if (modifier == null) continue;
                float multiplier = Math.Max(.1f, 1f + modifier.Value);
                switch (modifier.Type)
                {
                    case TowerModifierType.EnemyMaxHp: stats.MaxHp *= multiplier; break;
                    case TowerModifierType.EnemyAttack: stats.Attack *= multiplier; break;
                    case TowerModifierType.EnemySpeed: stats.Speed *= multiplier; break;
                }
            }
        }

        public void ModifyAction(ActionRequest request)
        {
            if (request == null || !appliedRequests.Add(request.RequestId)) return;
            for (int i = 0; i < modifiers.Count; i++)
            {
                TowerModifierDefinition modifier = modifiers[i];
                if (modifier == null) continue;
                float multiplier = Math.Max(.1f, 1f + modifier.Value);
                if (modifier.Type == TowerModifierType.PlayerDamage
                    && request.Actor?.Team == BattleTeam.Player)
                {
                    request.DamageMultiplier *= multiplier;
                    if (request.SecondaryDamageMultiplier.HasValue)
                        request.SecondaryDamageMultiplier *= multiplier;
                }
                else if (modifier.Type == TowerModifierType.ElementDamage
                    && request.Actor?.Team == BattleTeam.Player
                    && (modifier.Element == BattleElement.Auto || request.Element == modifier.Element))
                {
                    request.DamageMultiplier *= multiplier;
                    if (request.SecondaryDamageMultiplier.HasValue)
                        request.SecondaryDamageMultiplier *= multiplier;
                }
                else if (modifier.Type == TowerModifierType.Healing
                    && request.Actor?.Team == BattleTeam.Player)
                {
                    request.HealingAttackMultiplier *= multiplier;
                    request.HealingMaxHpMultiplier *= multiplier;
                    request.FixedHealing *= multiplier;
                }
            }
        }

        public string Summary
        {
            get
            {
                var labels = new List<string>();
                for (int i = 0; i < modifiers.Count; i++)
                    if (modifiers[i] != null) labels.Add(modifiers[i].Description);
                return labels.Count == 0 ? "적용 효과 없음" : string.Join("  ·  ", labels);
            }
        }
    }
}
