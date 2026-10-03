using System;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>An immutable snapshot shown after combat settles; presenting it cannot award rewards again.</summary>
    public sealed class BattleResult
    {
        /// <param name="isMission">An opening-arc mission: its number is the mission number, it pays no currency and
        /// unlocks no stage, but a victory can advance to the next mission.</param>
        /// <param name="completedCurriculumNode">The curriculum node this battle completed, or null.</param>
        /// <param name="activeCurriculumNode">The node still in progress after this battle, or null.</param>
        /// <param name="activeCurriculumBattles">Battles counted toward <paramref name="activeCurriculumNode"/>.</param>
        /// <param name="curriculumFinished">Whether no curriculum node is left to start after this battle.</param>
        /// <param name="curriculumOpen">Whether the curriculum exists for the player yet (see
        /// <see cref="CampaignRun.IsCurriculumOpen"/>). A closed one is never shown and reports no progress.</param>
        /// <param name="firstClearSkillName">The technique awarded by this stage's first clear, if any.</param>
        public BattleResult(DuelMatchOutcome outcome, bool isMission, int stageNumber, string stageName,
            int reward, int currency, int roundNumber, int playerHealth, int enemyHealth,
            bool firstClear, int unlockedStageNumber, bool canAdvance,
            CurriculumNode completedCurriculumNode = null, CurriculumNode activeCurriculumNode = null, int activeCurriculumBattles = 0,
            bool curriculumFinished = false, bool curriculumOpen = true, string firstClearSkillName = null,
            bool isTraining = false)
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
            if (!firstClear && !string.IsNullOrEmpty(firstClearSkillName))
                throw new ArgumentException("Only a first-clear victory can grant a technique.", nameof(firstClearSkillName));
            if ((!victory || isMission) && unlockedStageNumber != 0)
                throw new ArgumentException("Only a campaign victory can unlock a stage.", nameof(unlockedStageNumber));
            if (unlockedStageNumber != 0 && (!firstClear || unlockedStageNumber <= stageNumber))
                throw new ArgumentException("A newly unlocked stage must follow a first clear.", nameof(unlockedStageNumber));
            if (!victory && canAdvance)
                throw new ArgumentException("Only a victory can advance.", nameof(canAdvance));
            if (isMission && (completedCurriculumNode != null || activeCurriculumNode != null || curriculumFinished))
                throw new ArgumentException("Opening-arc missions do not count toward the curriculum.", nameof(completedCurriculumNode));
            if (isTraining && (isMission || reward != 0 || firstClear || unlockedStageNumber != 0 || canAdvance ||
                               completedCurriculumNode != null || activeCurriculumNode != null || curriculumFinished))
                throw new ArgumentException("Training results cannot award or advance campaign progress.", nameof(isTraining));
            if (!curriculumOpen && (completedCurriculumNode != null || activeCurriculumNode != null || curriculumFinished))
                throw new ArgumentException("A closed curriculum has no progress to report.", nameof(curriculumOpen));
            if (activeCurriculumNode == null ? activeCurriculumBattles != 0
                    : activeCurriculumBattles < 0 || activeCurriculumBattles >= activeCurriculumNode.Battles)
                throw new ArgumentOutOfRangeException(nameof(activeCurriculumBattles));

            Outcome = outcome;
            IsMission = isMission;
            IsTraining = isTraining;
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
            CompletedCurriculumNode = completedCurriculumNode;
            ActiveCurriculumNode = activeCurriculumNode;
            ActiveCurriculumBattles = activeCurriculumBattles;
            CurriculumFinished = curriculumFinished;
            CurriculumOpen = curriculumOpen;
            FirstClearSkillName = firstClearSkillName;
        }

        public DuelMatchOutcome Outcome { get; }
        public bool IsMission { get; }
        public bool IsTraining { get; }
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
        public CurriculumNode CompletedCurriculumNode { get; }
        public CurriculumNode ActiveCurriculumNode { get; }
        public int ActiveCurriculumBattles { get; }
        public bool CurriculumFinished { get; }
        /// <summary>Whether the curriculum was open when the battle ended; the result says nothing about it otherwise.</summary>
        public bool CurriculumOpen { get; }
        public string FirstClearSkillName { get; }
        public bool Victory => Outcome == DuelMatchOutcome.PlayerVictory;
        public bool CanRetry => true;
    }
}
