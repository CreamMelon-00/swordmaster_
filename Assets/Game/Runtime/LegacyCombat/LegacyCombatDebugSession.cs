using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Creates a self-contained combat presentation sample. It owns no campaign, progression or save state;
    /// callers commit the prepared queue and may create another sample after it resolves.</summary>
    public static class LegacyCombatDebugSession
    {
        public const int MaximumAttackCount = 10, DefaultPlayerAttackCount = 5;
        private static readonly LegacySkill Attack = new LegacySkill(-10001, "검증용 베기", 0, 10, 10,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);

        /// <summary>A planning duel with both queues ready. Counts are kept between zero and ten, both fighters take
        /// damage and may break but cannot die, and the usual single-player step anticipation remains available.</summary>
        public static LegacyQueuedDuel Create(int enemyAttackCount, int playerAttackCount = DefaultPlayerAttackCount,
            int randomSeed = 1, bool useFeaturedTechnique = false, bool usePowerBreak = false)
        {
            enemyAttackCount = ClampCount(enemyAttackCount);
            playerAttackCount = ClampCount(playerAttackCount);
            LegacySkill playerAttack = useFeaturedTechnique ? FeaturedAttack() : Attack;
            var duel = new LegacyQueuedDuel(100, 30, 100, 30, new[] { playerAttack },
                enemyAttackCount == 0 ? Array.Empty<LegacySkill>() : new[] { Attack }, new[] { enemyAttackCount },
                randomSeed: randomSeed, features: CombatFeature.All, enemyHealthFloor: 1, meshPercent: 0,
                playerHealthFloor: 1, usePowerBreak: usePowerBreak);
            var preparedPlayerQueue = new LegacySkill[playerAttackCount];
            for (int index = 0; index < preparedPlayerQueue.Length; index++) preparedPlayerQueue[index] = playerAttack;
            if (!duel.TrySetPreparedQueues(preparedPlayerQueue, duel.EnemyQueue))
                throw new InvalidOperationException("Could not prepare the combat presentation sample.");
            return duel;
        }

        private static LegacySkill FeaturedAttack()
        {
            LegacySkill source = LegacySkillDefinitions.Skill(LegacyFeaturedTechnique.SkillId);
            // Prepared samples spend no ACT, while the production ID, power and artwork select the real presentation.
            return new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower, source.Kind,
                source.Property, source.AttackCount, source.LaneIndex, source.Description, source.AnimationName, source.IconId, source.DurationHalfUnits);
        }

        private static int ClampCount(int count) => Math.Max(0, Math.Min(MaximumAttackCount, count));
    }
}
