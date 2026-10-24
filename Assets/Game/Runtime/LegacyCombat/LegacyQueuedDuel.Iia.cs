using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed partial class LegacyQueuedDuel
    {
        private bool iiaBossPatterns;
        private IReadOnlyList<LegacySkill> iiaPattern;
        private int iiaPatternStep, iiaGeneratedEntries, iiaAutoBreathSlotIndex = -1;
        private int iiaJealousy, iiaPlayerUsesThisTurn, iiaPreviousPlayerUses;
        private int iiaFixedTurns, iiaPendingFixedTurns;
        private bool iiaReceivedUnopposedHit, iiaJealousyReserved;
        private List<int> iiaCounterPreviewSlots;

        public bool IsIiaBossBattle => iiaBossPatterns;
        public int IiaPhase { get; private set; }
        public int IiaJealousy => iiaJealousy;
        public int PlayerFixedTurnsRemaining => Math.Max(iiaFixedTurns, iiaPendingFixedTurns);
        public int PlayerSkillsUsedPreviousTurn => iiaPreviousPlayerUses;

        /// <summary>Campaign-only opt-in, before the first reservation. Ordinary scripted, tavern and versus fights
        /// keep their own queue and keyword rules. Reset retains this configuration and resets all battle state.</summary>
        public LegacyQueuedDuel EnableIiaBossPatterns()
        {
            if (Phase != LegacyDuelPhase.Planning || RoundNumber != 1 || playerQueue.Count != 0 || enemyStates.Count != 1)
                throw new InvalidOperationException("Iia's patterns must be enabled before the duel's first reservation.");
            if (!iiaBossPatterns)
            {
                iiaBossPatterns = true;
                Reset();
            }
            return this;
        }

        private void ResetIiaState()
        {
            IiaPhase = iiaPatternStep = iiaGeneratedEntries = 0;
            iiaPattern = null;
            iiaAutoBreathSlotIndex = -1;
            iiaJealousy = iiaPlayerUsesThisTurn = iiaPreviousPlayerUses = 0;
            iiaFixedTurns = iiaPendingFixedTurns = 0;
            iiaReceivedUnopposedHit = iiaJealousyReserved = false;
        }

        private void CopyIiaStateFrom(LegacyQueuedDuel source)
        {
            iiaBossPatterns = source.iiaBossPatterns;
            IiaPhase = source.IiaPhase;
            iiaPattern = source.iiaPattern;
            iiaPatternStep = source.iiaPatternStep;
            iiaGeneratedEntries = source.iiaGeneratedEntries;
            iiaAutoBreathSlotIndex = source.iiaAutoBreathSlotIndex;
            iiaJealousy = source.iiaJealousy;
            iiaPlayerUsesThisTurn = source.iiaPlayerUsesThisTurn;
            iiaPreviousPlayerUses = source.iiaPreviousPlayerUses;
            iiaFixedTurns = source.iiaFixedTurns;
            iiaPendingFixedTurns = source.iiaPendingFixedTurns;
            iiaReceivedUnopposedHit = source.iiaReceivedUnopposedHit;
            iiaJealousyReserved = source.iiaJealousyReserved;
        }

        private void BeginIiaPlanningTurn()
        {
            if (!iiaBossPatterns) return;
            if (RoundNumber > 1 && !iiaReceivedUnopposedHit) iiaJealousy = 0;
            iiaReceivedUnopposedHit = false;
            iiaPreviousPlayerUses = iiaPlayerUsesThisTurn;
            iiaPlayerUsesThisTurn = 0;
            // Hits during resolution restrict the next preparation, rather than expiring at its entrance.
            iiaFixedTurns = Math.Max(Math.Max(0, iiaFixedTurns - 1), iiaPendingFixedTurns);
            iiaPendingFixedTurns = 0;
            int phase = GreenRibbonBossBattle.PhaseAtHealth(Enemy.Health, Enemy.MaxHealth);
            iiaPatternStep = phase != IiaPhase ? 1 : iiaPatternStep % GreenRibbonBossBattle.PatternCount(phase) + 1;
            IiaPhase = phase;
            // Pattern choices do not consume damage rolls; forecasts cannot alter the live branch.
            bool alternative = new Random(unchecked(randomSeed * 397 ^ RoundNumber * 7919 ^ phase * 104729)).Next(2) != 0;
            iiaPattern = GreenRibbonBossBattle.CreatePattern(phase, iiaPatternStep, alternative);
            iiaGeneratedEntries = 0;
            iiaAutoBreathSlotIndex = -1;
            iiaJealousyReserved = false;
            enemyQueue.Clear();
            enemyQueueOwners.Clear();
            GenerateIiaTechnique();
            GenerateIiaTechnique();
            if (iiaJealousy >= 5)
            {
                LegacySkill skill = LegacySkillDefinitions.Skill(GreenRibbonBossBattle.JealousySkillId);
                enemyQueue.Add(skill);
                enemyQueueOwners.Add(0);
                iiaJealousyReserved = true;
                if (LegacySkillDefinitions.Find(skill)?.Effect.ConsumeJealousyOnReserve == true) iiaJealousy = 0;
            }
        }

        private void GenerateIiaTechnique()
        {
            // A hypothetical player reservation must not disclose a technique the live battle has not generated.
            if (averageRolls || !iiaBossPatterns || iiaPattern == null || iiaGeneratedEntries >= iiaPattern.Count) return;
            int insertion = enemyQueue.Count - (iiaJealousyReserved ? 1 : 0);
            enemyQueue.Insert(insertion, iiaPattern[iiaGeneratedEntries++]);
            enemyQueueOwners.Insert(insertion, 0);
            // 호흡 and its free breath are one reservation. The breath itself never requests a new technique.
            if (iiaGeneratedEntries < iiaPattern.Count && iiaPattern[iiaGeneratedEntries].IsWait)
            {
                enemyQueue.Insert(insertion + 1, iiaPattern[iiaGeneratedEntries++]);
                enemyQueueOwners.Insert(insertion + 1, 0);
                iiaAutoBreathSlotIndex = insertion + 1;
            }
        }

        private void RecordIiaPlayerTechnique(LegacySkill skill)
        {
            if (iiaBossPatterns && skill != null && !skill.IsWait) iiaPlayerUsesThisTurn++;
        }

        private int GrantIiaFollowPower(LegacySkillEffect effect, LegacyStatusCollection statuses, bool player)
        {
            if (!iiaBossPatterns || player || !effect.BuffPowerFromPreviousOpponentUses || iiaPreviousPlayerUses == 0) return 0;
            statuses.Grant(LegacyStatusKind.PowerUp, 50, iiaPreviousPlayerUses);
            return iiaPreviousPlayerUses;
        }

        private int RollIiaEnemyPower(LegacySkill skill, LegacySkill opposingSkill, int opposingCycleUses,
            double multiplier, int powerBonus)
        {
            if (!iiaBossPatterns || LegacySkillDefinitions.Find(skill)?.Effect.MirrorOpponentMaxPower != true)
                return RollPower(skill, multiplier, out _, powerBonus);
            int maximum = IiaMirrorMaximum(opposingSkill, opposingCycleUses);
            // A missing action or a breath has no technique power, including no minimum-one attack roll.
            if (maximum == 0) return 0;
            var mirror = new LegacySkill(skill.Id, skill.Name, skill.Cost, maximum, maximum, skill.Kind,
                skill.Property, skill.AttackCount, skill.LaneIndex, skill.Description, skill.AnimationName, skill.IconId,
                skill.DurationHalfUnits);
            return RollPower(mirror, multiplier, out _, powerBonus);
        }

        private static int IiaMirrorMaximum(LegacySkill opposingSkill, int opposingCycleUses)
        {
            if (opposingSkill == null || opposingSkill.IsWait) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(opposingSkill)?.Effect;
            return checked(opposingSkill.MaxPower + (effect?.HasCycle == true ? opposingCycleUses * effect.CyclePowerPerUse : 0));
        }

        private int IiaMirrorPreview(LegacySkill skill)
        {
            int index = enemyQueue.IndexOf(skill);
            if (index < 0) return 0;
            if (HasTempoSkills) return TimedIiaMirrorPreview(index);
            if (CurrentSlot != null && CurrentSlot.SlotIndex == index)
            {
                LegacySkill current = CurrentSlot.PlayerSkill ?? CurrentSlot.PendingPlayerCounter;
                return IiaMirrorMaximum(current, CurrentSlot.PendingPlayerCounter != null
                    ? CycleUsesRead(current) : CurrentSlot.PlayerCycleUses);
            }
            IReadOnlyList<LegacySkill> players = QueueForForecast(true);
            LegacySkill opposing = index < players.Count ? players[index] : null;
            bool counter = false;
            if (opposing == null && PlayerCounter != null)
            {
                if (iiaCounterPreviewSlots == null) iiaCounterPreviewSlots = new List<int>();
                ForecastPlayerCounterSlots(iiaCounterPreviewSlots);
                counter = iiaCounterPreviewSlots.Contains(index);
            }
            if (counter) opposing = PlayerCounter.Skill;
            return IiaMirrorMaximum(opposing, opposing == null ? 0 : counter ? CycleUsesRead(opposing) : CycleUsesAtSlot(index));
        }

        private void RefreshIiaMirrorAfterCounterChoice(LegacyCurrentSlot slot)
        {
            if (iiaBossPatterns && slot.PlayerSkill == null &&
                LegacySkillDefinitions.Find(slot.EnemySkill)?.Effect.MirrorOpponentMaxPower == true)
                slot.EnemyPower = 0;
        }

        private void ApplyIiaHitKeywords(LegacyCurrentSlot slot, bool playerAttacked, bool enemyAttacked,
            int playerDamage, int enemyDamage)
        {
            if (!iiaBossPatterns) return;
            if (playerAttacked && enemyDamage > 0 && (slot.EnemySkill == null || slot.EnemySkill.IsWait))
            {
                iiaJealousy = checked(iiaJealousy + 1);
                iiaReceivedUnopposedHit = true;
            }
            if (enemyAttacked && !slot.DodgeSucceeded && playerDamage > 0)
                iiaPendingFixedTurns = Math.Max(iiaPendingFixedTurns,
                    LegacySkillDefinitions.Find(slot.EnemySkill)?.Effect.InflictFixedTurns ?? 0);
        }
    }
}
