using System;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>An immutable snapshot shown after combat settles; presenting it cannot award rewards again.</summary>
    public sealed class BattleResult
    {
        /// <param name="isMission">An opening-arc mission: its number is the mission number, it pays no currency and
        /// unlocks no stage, but a victory can advance to the next mission.</param>
        public BattleResult(DuelMatchOutcome outcome, bool isMission, int stageNumber, string stageName,
            int reward, int currency, int roundNumber, int playerHealth, int enemyHealth,
            bool firstClear, int unlockedStageNumber, bool canAdvance)
        {
            if (outcome != DuelMatchOutcome.PlayerVictory && outcome != DuelMatchOutcome.EnemyVictory
                && outcome != DuelMatchOutcome.Draw) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (stageNumber < 1) throw new ArgumentOutOfRangeException(nameof(stageNumber));
            if (string.IsNullOrWhiteSpace(stageName)) throw new ArgumentException("A stage name is required.", nameof(stageName));
            if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
            if (currency < 0) throw new ArgumentOutOfRangeException(nameof(currency));
            if (roundNumber < 1) throw new ArgumentOutOfRangeException(nameof(roundNumber));
            if (playerHealth < 0) throw new ArgumentOutOfRangeException(nameof(playerHealth));
            if (enemyHealth < 0) throw new ArgumentOutOfRangeException(nameof(enemyHealth));
            if (unlockedStageNumber < 0) throw new ArgumentOutOfRangeException(nameof(unlockedStageNumber));
            bool victory = outcome == DuelMatchOutcome.PlayerVictory;
            if ((!victory || isMission) && reward != 0)
                throw new ArgumentException("Only a campaign victory can award currency.", nameof(reward));
            if ((!victory || isMission) && firstClear)
                throw new ArgumentException("Only a campaign victory can be a first clear.", nameof(firstClear));
            if ((!victory || isMission) && unlockedStageNumber != 0)
                throw new ArgumentException("Only a campaign victory can unlock a stage.", nameof(unlockedStageNumber));
            if (unlockedStageNumber != 0 && (!firstClear || unlockedStageNumber <= stageNumber))
                throw new ArgumentException("A newly unlocked stage must follow a first clear.", nameof(unlockedStageNumber));
            if (!victory && canAdvance)
                throw new ArgumentException("Only a victory can advance.", nameof(canAdvance));

            Outcome = outcome;
            IsMission = isMission;
            StageNumber = stageNumber;
            StageName = stageName;
            Reward = reward;
            Currency = currency;
            RoundNumber = roundNumber;
            PlayerHealth = playerHealth;
            EnemyHealth = enemyHealth;
            FirstClear = firstClear;
            UnlockedStageNumber = unlockedStageNumber;
            CanAdvance = canAdvance;
        }

        public DuelMatchOutcome Outcome { get; }
        public bool IsMission { get; }
        public int StageNumber { get; }
        public string StageName { get; }
        public int Reward { get; }
        public int Currency { get; }
        public int RoundNumber { get; }
        public int PlayerHealth { get; }
        public int EnemyHealth { get; }
        public bool FirstClear { get; }
        /// <summary>The stage newly unlocked by this result, or zero if none was unlocked.</summary>
        public int UnlockedStageNumber { get; }
        public bool CanAdvance { get; }
        public bool Victory => Outcome == DuelMatchOutcome.PlayerVictory;
        public bool CanRetry => true;
    }
}
