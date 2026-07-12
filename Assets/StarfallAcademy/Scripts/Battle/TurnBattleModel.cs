using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class BattleUnit
    {
        public CharacterData Character { get; }
        public string Name { get; }
        public bool IsEnemy { get; }
        public int SlotIndex { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public int Attack { get; }
        public int Speed { get; }
        public bool Defending { get; private set; }
        public bool IsAlive => CurrentHp > 0;
        public float HpRatio => MaxHp > 0 ? CurrentHp / (float)MaxHp : 0;

        BattleUnit(CharacterData character, string name, bool enemy, int slotIndex,
            int maxHp, int attack, int speed)
        {
            Character = character;
            Name = name;
            IsEnemy = enemy;
            SlotIndex = slotIndex;
            MaxHp = Mathf.Max(1, maxHp);
            CurrentHp = MaxHp;
            Attack = Mathf.Max(1, attack);
            Speed = Mathf.Max(1, speed);
        }

        public static BattleUnit CreatePlayer(CharacterData character, int index)
        {
            int power = CharacterProgressionService.GetCombatPower(character);
            int roleHp = character.Role == CharacterRole.Tank ? 550
                : character.Role == CharacterRole.Healer ? 220 : 320;
            int roleAttack = character.Role == CharacterRole.Striker ? 90
                : character.Role == CharacterRole.Support ? 45 : 30;
            int speed = character.Role == CharacterRole.Support ? 82
                : character.Role == CharacterRole.Striker ? 72
                : character.Role == CharacterRole.Healer ? 66 : 58;
            return new BattleUnit(character, character.DisplayName, false, index,
                700 + roleHp + Mathf.RoundToInt(power * .22f),
                70 + roleAttack + Mathf.RoundToInt(power * .055f), speed);
        }

        public static BattleUnit CreateEnemy(StageData stage, int index)
        {
            float scale = 1f + index * .06f;
            string suffix = stage.EnemyCount > 1 ? " " + (index + 1) : string.Empty;
            return new BattleUnit(null, stage.EnemyName + suffix, true, index,
                Mathf.RoundToInt(stage.EnemyMaxHp * scale),
                Mathf.RoundToInt(stage.EnemyAttack * scale), stage.EnemySpeed - index * 2);
        }

        public int TakeDamage(int amount)
        {
            int applied = Defending ? Mathf.CeilToInt(amount * .5f) : amount;
            Defending = false;
            CurrentHp = Mathf.Max(0, CurrentHp - Mathf.Max(1, applied));
            return applied;
        }

        public int Heal(int amount)
        {
            int before = CurrentHp;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + Mathf.Max(0, amount));
            return CurrentHp - before;
        }

        public void Defend() => Defending = true;
    }

    public sealed class TurnBattleModel
    {
        readonly System.Random random = new System.Random();
        public List<BattleUnit> Players { get; } = new List<BattleUnit>();
        public List<BattleUnit> Enemies { get; } = new List<BattleUnit>();
        public int Round { get; private set; }
        public bool PlayersDefeated => FindFirstAlive(Players) == null;
        public bool EnemiesDefeated => FindFirstAlive(Enemies) == null;

        public TurnBattleModel(FormationState formation, StageData stage)
        {
            for (int i = 0; i < formation.Count; i++)
                Players.Add(BattleUnit.CreatePlayer(formation.Members[i], i));
            for (int i = 0; i < stage.EnemyCount; i++)
                Enemies.Add(BattleUnit.CreateEnemy(stage, i));
        }

        public List<BattleUnit> BeginRound()
        {
            Round++;
            var order = new List<BattleUnit>();
            foreach (BattleUnit player in Players) if (player.IsAlive) order.Add(player);
            foreach (BattleUnit enemy in Enemies) if (enemy.IsAlive) order.Add(enemy);
            order.Sort((a, b) => b.Speed.CompareTo(a.Speed));
            return order;
        }

        public BattleUnit RandomAlivePlayer()
        {
            var alive = Players.FindAll(unit => unit.IsAlive);
            return alive.Count > 0 ? alive[random.Next(alive.Count)] : null;
        }

        public BattleUnit FirstAliveEnemy() => FindFirstAlive(Enemies);

        public int AttackUnit(BattleUnit attacker, BattleUnit target, float multiplier)
        {
            float variance = .9f + (float)random.NextDouble() * .2f;
            return target.TakeDamage(Mathf.RoundToInt(attacker.Attack * multiplier * variance));
        }

        public int UsePlayerSkill(BattleUnit actor, BattleUnit target, out BattleUnit affectedUnit,
            out bool isHealing)
        {
            CharacterRole role = actor.Character.Role;
            int skillLevel = CharacterProgressionService.GetSkillLevel(actor.Character);
            if (role == CharacterRole.Healer)
            {
                affectedUnit = LowestHpPlayer() ?? actor;
                isHealing = true;
                return affectedUnit.Heal(Mathf.RoundToInt(actor.Attack * (1.2f + skillLevel * .06f)));
            }
            affectedUnit = target;
            isHealing = false;
            float multiplier = 1.55f + skillLevel * .05f;
            return AttackUnit(actor, target, multiplier);
        }

        public bool HasInjuredPlayer()
        {
            foreach (BattleUnit player in Players)
                if (player.IsAlive && player.HpRatio < .999f) return true;
            return false;
        }

        BattleUnit LowestHpPlayer()
        {
            BattleUnit result = null;
            foreach (BattleUnit player in Players)
            {
                if (!player.IsAlive) continue;
                if (result == null || player.HpRatio < result.HpRatio) result = player;
            }
            return result;
        }

        static BattleUnit FindFirstAlive(List<BattleUnit> units)
        {
            foreach (BattleUnit unit in units)
                if (unit.IsAlive) return unit;
            return null;
        }
    }
}
