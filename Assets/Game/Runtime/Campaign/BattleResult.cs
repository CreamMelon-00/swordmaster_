using System;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>An immutable snapshot shown after combat settles; presenting it cannot award rewards again.</summary>
    public sealed class BattleResult
    {
        /// <param name="isMission">An opening-arc mission: its number is the mission number, it pays no currency and
        /// unlocks no stage, but a victory can advance to the next mission.</param>
        /// <param name="curriculumPoints">The 수련 점수 this result earned, by source: a stage battle's battle, victory and
        /// first clear (<see cref="CampaignRun.LastCurriculumPointGain"/>), a story mission's first completion. Empty while
        /// the curriculum is closed: points still accrue then, but nothing about them is shown.</param>
        /// <param name="curriculumBalance">The unspent 수련 점수 after this result (<see cref="CampaignRun.CurriculumPoints"/>).</param>
        /// <param name="affordableCurriculumNodes">How many available nodes the balance covers now (익힐 수 있는 과정 k개).</param>
        /// <param name="curriculumPointsToNext">With none affordable, the points still missing for the cheapest available node
        /// (다음 과정까지 m점); zero otherwise.</param>
        /// <param name="curriculumFinished">Whether no curriculum node is left to learn.</param>
        /// <param name="curriculumOpen">Whether the curriculum exists for the player yet (see
        /// <see cref="CampaignRun.IsCurriculumOpen"/>). A closed one is never shown and reports nothing.</param>
        /// <param name="firstClearSkillName">The technique awarded by this stage's first clear, if any.</param>
        /// <param name="curriculumNewlyAffordable">The balance newly covers some available node it did not cover before
        /// (<see cref="CampaignRun.CurriculumNewlyAffordable"/>): the field says that a node can be learned.</param>
        /// <param name="curriculumJustOpened">This story mission's first win opened the curriculum (mission 8): the field
        /// shows the points banked so far once.</param>
        public BattleResult(DuelMatchOutcome outcome, bool isMission, int stageNumber, string stageName,
            int reward, int currency, int roundNumber, int playerHealth, int enemyHealth,
            bool firstClear, int unlockedStageNumber, bool canAdvance,
            CurriculumPointGain curriculumPoints = default, int curriculumBalance = 0, int affordableCurriculumNodes = 0,
            int curriculumPointsToNext = 0, bool curriculumFinished = false, bool curriculumOpen = true,
            string firstClearSkillName = null, bool isTraining = false, bool curriculumNewlyAffordable = false,
            bool curriculumJustOpened = false)
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
            if (isMission ? curriculumPoints.Battle != 0 || curriculumPoints.Victory != 0 || curriculumPoints.FirstClear != 0
                    : curriculumPoints.Mission != 0)
                throw new ArgumentException("Stage battles earn battle points and story missions their mission point.",
                    nameof(curriculumPoints));
            if (!victory && (curriculumPoints.Victory != 0 || curriculumPoints.FirstClear != 0 || curriculumPoints.Mission != 0))
                throw new ArgumentException("Only a victory earns victory, first-clear or mission points.", nameof(curriculumPoints));
            if (!firstClear && curriculumPoints.FirstClear != 0)
                throw new ArgumentException("Only a first clear earns first-clear points.", nameof(curriculumPoints));
            if (isTraining && (isMission || reward != 0 || firstClear || unlockedStageNumber != 0 || canAdvance ||
                               !curriculumPoints.IsEmpty))
                throw new ArgumentException("Training results cannot award or advance campaign progress.", nameof(isTraining));
            if (curriculumBalance < 0) throw new ArgumentOutOfRangeException(nameof(curriculumBalance));
            if (affordableCurriculumNodes < 0) throw new ArgumentOutOfRangeException(nameof(affordableCurriculumNodes));
            if (curriculumPointsToNext < 0) throw new ArgumentOutOfRangeException(nameof(curriculumPointsToNext));
            if (curriculumBalance < curriculumPoints.Total)
                throw new ArgumentException("The balance after a result holds what it earned.", nameof(curriculumBalance));
            if (affordableCurriculumNodes > 0 && (curriculumPointsToNext != 0 || curriculumFinished))
                throw new ArgumentException("Points to the next node only count while none is affordable.",
                    nameof(curriculumPointsToNext));
            if (curriculumFinished && curriculumPointsToNext != 0)
                throw new ArgumentException("A finished curriculum has no next node.", nameof(curriculumPointsToNext));
            if (curriculumNewlyAffordable && affordableCurriculumNodes == 0)
                throw new ArgumentException("A newly affordable node is an affordable one.", nameof(curriculumNewlyAffordable));
            if (curriculumJustOpened && (!isMission || !victory))
                throw new ArgumentException("Only a story mission's win opens the curriculum.", nameof(curriculumJustOpened));
            if (!curriculumOpen && (!curriculumPoints.IsEmpty || curriculumBalance != 0 || affordableCurriculumNodes != 0 ||
                                    curriculumPointsToNext != 0 || curriculumFinished || curriculumNewlyAffordable ||
                                    curriculumJustOpened))
                throw new ArgumentException("A closed curriculum has nothing to report.", nameof(curriculumOpen));

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
            CurriculumPoints = curriculumPoints;
            CurriculumBalance = curriculumBalance;
            AffordableCurriculumNodes = affordableCurriculumNodes;
            CurriculumPointsToNext = curriculumPointsToNext;
            CurriculumFinished = curriculumFinished;
            CurriculumOpen = curriculumOpen;
            FirstClearSkillName = firstClearSkillName;
            CurriculumNewlyAffordable = curriculumNewlyAffordable;
            CurriculumJustOpened = curriculumJustOpened;
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
        /// <summary>The 수련 점수 this result earned, by source; empty while the curriculum is closed.</summary>
        public CurriculumPointGain CurriculumPoints { get; }
        /// <summary>The 수련 점수 earned in all (<see cref="CurriculumPointGain.Total"/>).</summary>
        public int CurriculumPointsEarned => CurriculumPoints.Total;
        /// <summary>The unspent 수련 점수 after this result (보유 B); zero while the curriculum is closed.</summary>
        public int CurriculumBalance { get; }
        /// <summary>How many available nodes the balance covers (익힐 수 있는 과정 k개).</summary>
        public int AffordableCurriculumNodes { get; }
        /// <summary>With none affordable, the points still missing for the cheapest available node (다음 과정까지 m점).</summary>
        public int CurriculumPointsToNext { get; }
        /// <summary>No node is left to learn.</summary>
        public bool CurriculumFinished { get; }
        /// <summary>The balance newly covers some available node: the field says a node can be learned.</summary>
        public bool CurriculumNewlyAffordable { get; }
        /// <summary>This mission's first win opened the curriculum: the field shows the banked points once.</summary>
        public bool CurriculumJustOpened { get; }
        /// <summary>Whether the curriculum was open when the battle ended; the result says nothing about it otherwise.</summary>
        public bool CurriculumOpen { get; }
        public string FirstClearSkillName { get; }
        public bool Victory => Outcome == DuelMatchOutcome.PlayerVictory;
        public bool CanRetry => true;
    }
}
