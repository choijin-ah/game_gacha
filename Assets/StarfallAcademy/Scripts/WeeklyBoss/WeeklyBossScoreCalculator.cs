using System;

namespace StarfallAcademy.Lobby
{
    public static class WeeklyBossScoreCalculator
    {
        public static int Calculate(BattleResult result, WeeklyBossDifficulty difficulty)
        {
            if (result == null) return 0;
            long damage = Math.Max(0L, result.DamageDealtToEnemies);
            long clearBonus = result.EnemiesDefeated ? 100000L : 0L;
            int limit = difficulty?.TurnLimit ?? Math.Max(1, result.RegularTurns);
            long turnBonus = Math.Max(0, limit - result.RegularTurns) * 1000L;
            long survivalBonus = Math.Max(0, 4 - result.DefeatedAllies) * 2500L;
            long total = damage + clearBonus + turnBonus + survivalBonus;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
