using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    public sealed class LegacyQueuedDuel
    {
        public const int MaximumBreathsPerTurn = 3;
        private const int BaseActGain = 3;
        private const int MaximumAct = 10;
        private readonly LegacySkill[] initialPlayerSkills;
        private readonly LegacySkill[] enemyPattern;
        private readonly int[] enemyActionCounts;
        private readonly List<LegacySkill>[] lanes = { new List<LegacySkill>(), new List<LegacySkill>(), new List<LegacySkill>() };
        private readonly IReadOnlyList<LegacySkill>[] laneViews;
        private readonly List<LegacySkill> playerQueue = new List<LegacySkill>();
        private readonly List<LegacySkill> enemyQueue = new List<LegacySkill>();
        private readonly List<SkillBuff> playerBuffs = new List<SkillBuff>();
        private readonly List<SkillBuff> enemyBuffs = new List<SkillBuff>();
        private readonly int playerMaxHealth;
        private readonly int playerMaxResistance;
        private readonly int enemyMaxHealth;
        private readonly int enemyMaxResistance;
        private readonly int randomSeed;
        private Random random;
        private LegacySkill[] committedPlayerQueue;
        private LegacySkill[] committedEnemyQueue;
        private int enemyPatternIndex;
        private int nextSlot;
        private int slotCount;

        public LegacyQueuedDuel() : this(100, 50, 80, 15, LegacyInitialSkills.All,
            FirstSixSkills(), new[] { 2, 3, 2, 1 }, Environment.TickCount) { }

        public LegacyQueuedDuel(int playerHealth, int playerResistance, int enemyHealth, int enemyResistance,
            IReadOnlyList<LegacySkill> playerSkills, IReadOnlyList<LegacySkill> enemySkills,
            IReadOnlyList<int> enemyTurnActionCounts, int randomSeed = 1)
        {
            if (playerHealth <= 0 || enemyHealth <= 0 || playerResistance < 0 || enemyResistance < 0)
                throw new ArgumentOutOfRangeException(nameof(playerHealth));
            initialPlayerSkills = CopySkills(playerSkills, nameof(playerSkills));
            enemyPattern = CopySkills(enemySkills, nameof(enemySkills));
            if (enemyTurnActionCounts == null || enemyTurnActionCounts.Count == 0)
                throw new ArgumentException("At least one enemy turn action count is required.", nameof(enemyTurnActionCounts));
            enemyActionCounts = new int[enemyTurnActionCounts.Count];
            for (int i = 0; i < enemyActionCounts.Length; i++)
            {
                if (enemyTurnActionCounts[i] < 1) throw new ArgumentOutOfRangeException(nameof(enemyTurnActionCounts));
                enemyActionCounts[i] = enemyTurnActionCounts[i];
            }
            playerMaxHealth = playerHealth;
            playerMaxResistance = playerResistance;
            enemyMaxHealth = enemyHealth;
            enemyMaxResistance = enemyResistance;
            this.randomSeed = randomSeed;
            laneViews = new IReadOnlyList<LegacySkill>[3];
            for (int i = 0; i < lanes.Length; i++) laneViews[i] = lanes[i].AsReadOnly();
            PlayerQueue = playerQueue.AsReadOnly();
            EnemyQueue = enemyQueue.AsReadOnly();
            Reset();
        }

        public LegacyFighterState Player { get; private set; }
        public LegacyFighterState Enemy { get; private set; }
        public int RoundNumber { get; private set; }
        public int Act { get; private set; }
        public int NextActGain { get; private set; }
        public LegacyDuelPhase Phase { get; private set; }
        public DuelMatchOutcome Outcome { get; private set; }
        public IReadOnlyList<LegacySkill> PlayerQueue { get; }
        public IReadOnlyList<LegacySkill> EnemyQueue { get; }
        public LegacyCurrentSlot CurrentSlot { get; private set; }
        public bool UsedStepThisTurn { get; private set; }
        public int BreathsQueuedThisTurn { get; private set; }
        public int BreathsRemainingThisTurn => MaximumBreathsPerTurn - BreathsQueuedThisTurn;
        public int LastResolvedSlot { get; private set; }
        public int ResolutionSlotCount => slotCount;
        public bool IsFinished => Phase == LegacyDuelPhase.Finished;
        public bool IsTurnResolved => Phase == LegacyDuelPhase.Resolving && CurrentSlot == null && nextSlot >= slotCount;
        public bool IsCurrentSlotResolved => CurrentSlot != null && CurrentSlot.IsResolved;

        public IReadOnlyList<LegacySkill> GetLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length) throw new ArgumentOutOfRangeException(nameof(laneIndex));
            return laneViews[laneIndex];
        }

        public bool TryQueueLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= lanes.Length) return false;
            List<LegacySkill> lane = lanes[laneIndex];
            if (Phase != LegacyDuelPhase.Planning || lane.Count == 0 || lane[0].IsWait || Act < lane[0].Cost) return false;
            LegacySkill skill = lane[0];
            Act -= skill.Cost;
            playerQueue.Add(skill);
            lane.RemoveAt(0);
            lane.Add(skill);
            return true;
        }

        public bool TryQueueBreath()
        {
            if (Phase != LegacyDuelPhase.Planning || BreathsQueuedThisTurn >= MaximumBreathsPerTurn) return false;
            playerQueue.Add(LegacyCommonActions.Breathe);
            BreathsQueuedThisTurn++;
            return true;
        }

        public void Commit()
        {
            if (Phase != LegacyDuelPhase.Planning) throw new InvalidOperationException("Only a planning turn can be committed.");
            committedPlayerQueue = playerQueue.ToArray();
            committedEnemyQueue = enemyQueue.ToArray();
            nextSlot = 0;
            slotCount = Math.Max(committedPlayerQueue.Length, committedEnemyQueue.Length);
            Phase = LegacyDuelPhase.Resolving;
        }

        public bool TryStep(LegacyStepAction action, bool timingSuccessful, out bool success)
        {
            success = false;
            if (Phase != LegacyDuelPhase.Resolving ||
                (action != LegacyStepAction.Dodge && action != LegacyStepAction.Pressure)) return false;

            // The natural ACT recovery is the price of attempting a step, not
            // of succeeding. Gaps between skills and repeated inputs still count.
            UsedStepThisTurn = true;
            LegacyCurrentSlot slot = CurrentSlot;
            if (!timingSuccessful || slot == null || slot.HitsResolved != 0) return true;
            if (action == LegacyStepAction.Dodge)
            {
                if (slot.EnemySkill == null || slot.EnemySkill.Kind != LegacySkillKind.Attack || slot.DodgeSucceeded)
                    return true;
                slot.DodgeSucceeded = success = true;
            }
            else
            {
                if (slot.PlayerSkill == null || slot.PlayerSkill.IsWait || slot.PressureSucceeded) return true;
                slot.PressureSucceeded = success = true;
            }
            return true;
        }

        public LegacySlotResult ResolveNextSlot()
        {
            if (CurrentSlot == null) BeginNextSlot();
            while (!IsCurrentSlotResolved) ResolveNextHit();
            return CompleteCurrentSlot();
        }

        public LegacyCurrentSlot BeginNextSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot != null || nextSlot >= slotCount)
                throw new InvalidOperationException("A committed slot must be available and the previous slot must be complete.");
            LegacySkill playerSkill = nextSlot < committedPlayerQueue.Length ? committedPlayerQueue[nextSlot] : null;
            LegacySkill enemySkill = nextSlot < committedEnemyQueue.Length ? committedEnemyQueue[nextSlot] : null;
            double playerAttackMultiplier = AttackMultiplier(playerBuffs, out int playerPowerBuffPercent);
            double enemyAttackMultiplier = AttackMultiplier(enemyBuffs, out int enemyPowerBuffPercent);
            double playerReceivedMultiplier = ReceivedMultiplier(playerBuffs, out int playerProtectionBuffPercent);
            double enemyReceivedMultiplier = ReceivedMultiplier(enemyBuffs, out int enemyProtectionBuffPercent);
            int playerPower = RollPower(playerSkill, playerAttackMultiplier, out int playerTotalPower);
            int enemyPower = RollPower(enemySkill, enemyAttackMultiplier, out _);

            CurrentSlot = new LegacyCurrentSlot(nextSlot, playerSkill, enemySkill,
                playerPower, enemyPower, playerTotalPower,
                playerReceivedMultiplier, enemyReceivedMultiplier, Player, Enemy);
            // Original UseBuff/AttackStart/End/BuffClear initializes power once
            // and applies effects before animation events deal individual hits.
            TickBuffs(playerBuffs);
            TickBuffs(enemyBuffs);
            CurrentSlot.PlayerFeedback = ApplyInitialSkillEffects(playerSkill, enemySkill, playerBuffs, true,
                playerPowerBuffPercent, playerProtectionBuffPercent);
            CurrentSlot.EnemyFeedback = ApplyInitialSkillEffects(enemySkill, playerSkill, enemyBuffs, false,
                enemyPowerBuffPercent, enemyProtectionBuffPercent);
            return CurrentSlot;
        }

        public LegacyHitResult ResolveNextHit()
        {
            if (Phase != LegacyDuelPhase.Resolving || CurrentSlot == null || CurrentSlot.IsResolved)
                throw new InvalidOperationException("There is no unresolved hit in the current slot.");
            LegacyCurrentSlot slot = CurrentSlot;
            int hitIndex = slot.HitsResolved;
            int playerHealth = Player.Health, enemyHealth = Enemy.Health;
            int playerResistance = Player.Resistance, enemyResistance = Enemy.Resistance;
            bool playerAttacked = HasAttackHit(slot.PlayerSkill, hitIndex);
            bool enemyAttacked = HasAttackHit(slot.EnemySkill, hitIndex);
            LegacyHitImpact playerImpact = default;
            LegacyHitImpact enemyImpact = default;
            // Both actions remain valid throughout the slot, including after a
            // lethal hit. Original Controller checks death after its full wait.
            if (playerAttacked)
            {
                enemyImpact = ResolveSingleHit(slot.PlayerSkill, slot.EnemySkill,
                    slot.PlayerPower, slot.EnemyPower, Enemy, slot.EnemyReceivedMultiplier);
                if (slot.PressureSucceeded)
                {
                    // Add one whole rolled skill power, divided across its hits
                    // without guard/resistance/received multipliers.
                    int bonus = PressureBonus(slot, hitIndex);
                    LegacyHitImpact pressureImpact = Enemy.ReceiveFlatHealthDamage(bonus);
                    enemyImpact = new LegacyHitImpact(enemyImpact.PushPower + pressureImpact.PushPower,
                        enemyImpact.DisplayedDamage + pressureImpact.DisplayedDamage);
                }
            }
            if (enemyAttacked && !slot.DodgeSucceeded)
            {
                bool halveDamage = slot.PressureSucceeded && slot.PlayerSkill != null &&
                    slot.PlayerSkill.Kind == LegacySkillKind.Defence;
                playerImpact = ResolveSingleHit(slot.EnemySkill, slot.PlayerSkill,
                    slot.EnemyPower, slot.PlayerPower, Player, slot.PlayerReceivedMultiplier, halveDamage);
            }
            slot.HitsResolved++;
            return new LegacyHitResult(slot, hitIndex, playerAttacked, enemyAttacked,
                playerHealth - Player.Health, enemyHealth - Enemy.Health,
                playerResistance - Player.Resistance, enemyResistance - Enemy.Resistance,
                playerImpact, enemyImpact,
                slot.IsResolved ? DetermineOutcome() : DuelMatchOutcome.InProgress,
                enemyAttacked && slot.DodgeSucceeded, slot.PressureSucceeded);
        }

        public LegacySlotResult CompleteCurrentSlot()
        {
            if (Phase != LegacyDuelPhase.Resolving || !IsCurrentSlotResolved)
                throw new InvalidOperationException("Every hit must resolve before completing the current slot.");
            LegacyCurrentSlot slot = CurrentSlot;
            LastResolvedSlot = slot.SlotIndex;
            nextSlot++;
            Outcome = DetermineOutcome();
            if (Outcome != DuelMatchOutcome.InProgress) Phase = LegacyDuelPhase.Finished;
            CurrentSlot = null;
            return new LegacySlotResult(slot.SlotIndex, slot.PlayerSkill, slot.EnemySkill,
                slot.PlayerStartingHealth - Player.Health, slot.EnemyStartingHealth - Enemy.Health,
                slot.PlayerStartingResistance - Player.Resistance, slot.EnemyStartingResistance - Enemy.Resistance, Outcome);
        }

        public void BeginNextTurn()
        {
            if (!IsTurnResolved) throw new InvalidOperationException("All slots must resolve before starting another turn.");
            RoundNumber++;
            StartPlanningTurn();
        }

        public void Reset()
        {
            random = new Random(randomSeed);
            Player = new LegacyFighterState(playerMaxHealth, playerMaxResistance);
            Enemy = new LegacyFighterState(enemyMaxHealth, enemyMaxResistance);
            for (int i = 0; i < lanes.Length; i++) lanes[i].Clear();
            foreach (LegacySkill skill in initialPlayerSkills) lanes[skill.LaneIndex].Add(skill);
            enemyPatternIndex = 0;
            RoundNumber = 1;
            Act = 0;
            NextActGain = BaseActGain;
            UsedStepThisTurn = false;
            Outcome = DuelMatchOutcome.InProgress;
            StartPlanningTurn();
        }

        private void StartPlanningTurn()
        {
            int actGain = NextActGain - (UsedStepThisTurn ? BaseActGain : 0);
            Act = Math.Min(MaximumAct, Act + actGain);
            NextActGain = BaseActGain;
            UsedStepThisTurn = false;
            BreathsQueuedThisTurn = 0;
            Player.BeginTurn();
            Enemy.BeginTurn();
            playerBuffs.Clear();
            enemyBuffs.Clear();
            playerQueue.Clear();
            enemyQueue.Clear();
            int count = enemyActionCounts[(RoundNumber - 1) % enemyActionCounts.Length];
            for (int i = 0; i < count; i++)
            {
                enemyQueue.Add(enemyPattern[enemyPatternIndex]);
                enemyPatternIndex = (enemyPatternIndex + 1) % enemyPattern.Length;
            }
            committedPlayerQueue = committedEnemyQueue = null;
            CurrentSlot = null;
            slotCount = nextSlot = 0;
            LastResolvedSlot = -1;
            Phase = LegacyDuelPhase.Planning;
        }

        private int RollPower(LegacySkill skill, double multiplier, out int totalPower)
        {
            totalPower = 0;
            if (skill == null || skill.IsWait) return 0;
            int rolledPower = random.Next(skill.MinPower, skill.MaxPower + 1);
            totalPower = (int)Math.Floor(rolledPower * multiplier);
            return Math.Max(1, (int)Math.Floor(rolledPower * multiplier / skill.AttackCount));
        }

        private static int PressureBonus(LegacyCurrentSlot slot, int hitIndex)
        {
            int hits = slot.PlayerSkill.AttackCount;
            return slot.PlayerTotalPower / hits + (hitIndex < slot.PlayerTotalPower % hits ? 1 : 0);
        }

        private static bool HasAttackHit(LegacySkill skill, int hitIndex)
            => skill != null && skill.Kind == LegacySkillKind.Attack && hitIndex < skill.AttackCount;

        private DuelMatchOutcome DetermineOutcome()
        {
            // Original Controller.PhaseEnd prioritizes the player's death.
            return Player.IsDefeated ? DuelMatchOutcome.EnemyVictory
                : Enemy.IsDefeated ? DuelMatchOutcome.PlayerVictory : DuelMatchOutcome.InProgress;
        }

        private static LegacyHitImpact ResolveSingleHit(LegacySkill attacker, LegacySkill defender, int attackerPower,
            int defenderPower, LegacyFighterState target, double receivedMultiplier, bool halveDamage = false)
        {
            bool resistanceHit = defender != null && defender.Kind == LegacySkillKind.Attack;
            int power = attackerPower;
            if (defender != null && defender.Kind == LegacySkillKind.Defence)
                power = Math.Max(0, attackerPower - defenderPower / attacker.AttackCount);
            return target.ReceiveHit(power, resistanceHit, receivedMultiplier, halveDamage);
        }

        private LegacySkillFeedback ApplyInitialSkillEffects(LegacySkill skill, LegacySkill opposingSkill,
            List<SkillBuff> buffs, bool player, int powerBuffPercent, int protectionBuffPercent)
        {
            if (skill == null || skill.IsWait)
                return new LegacySkillFeedback(skill, false, false, powerBuffPercent, protectionBuffPercent);
            bool conditionMet = LegacySkillConditions.MatchesOpponent(skill, opposingSkill);
            bool effectActivated = false;
            SkillBuff grantedBuff = null;
            switch (skill.Id)
            {
                case 1:
                    if (player)
                    {
                        NextActGain++;
                        effectActivated = true;
                    }
                    break;
                case 3:
                    grantedBuff = new SkillBuff(10, 0, 3);
                    buffs.Add(grantedBuff);
                    effectActivated = true;
                    break;
                case 7:
                    if (player && conditionMet)
                    {
                        NextActGain += 2;
                        effectActivated = true;
                    }
                    break;
                case 8:
                    grantedBuff = new SkillBuff(0, 30, 10);
                    buffs.Add(grantedBuff);
                    effectActivated = true;
                    break;
                case 9:
                    // The source CSV says "next turn", but Skill_Blocking adds
                    // to Cur, and Unit.TurnInit discards it at the turn boundary.
                    grantedBuff = new SkillBuff(3, 0, 10);
                    buffs.Add(grantedBuff);
                    effectActivated = true;
                    break;
                case 10:
                    // Ready's Setting runs after the source's power snapshot,
                    // so its one-use bonus strengthens the following slot.
                    grantedBuff = new SkillBuff(30, 0, 1);
                    buffs.Add(grantedBuff);
                    effectActivated = true;
                    break;
                case 12:
                    if (player) NextActGain += 3;
                    // Forward uses Cur/count=1 in the source despite its CSV
                    // saying next turn. Vulnerability expires at this turn's end.
                    grantedBuff = new SkillBuff(0, -50, 1);
                    buffs.Add(grantedBuff);
                    effectActivated = true;
                    break;
                case 19:
                    LegacyFighterState fighter = player ? Player : Enemy;
                    int restored = fighter.RestoreResistance(LegacySkillConditions.ResistanceRecoveryAmount(fighter));
                    conditionMet = effectActivated = restored > 0;
                    break;
                case 42:
                    if (conditionMet)
                    {
                        effectActivated = (player ? Enemy : Player).ReduceResistance(20) > 0;
                        // The player skill's actual code grants three, not the
                        // CSV's one. Never use the enemy-only instant break rule.
                        if (player)
                        {
                            NextActGain += 3;
                            effectActivated = true;
                        }
                    }
                    break;
                // Skill_Smashing only sets a guard's isAttack flag to false in
                // the source. It does not remove that guard's mitigation power.
            }
            return new LegacySkillFeedback(skill, conditionMet, effectActivated, powerBuffPercent, protectionBuffPercent,
                grantedBuff?.AttackPercent ?? 0, grantedBuff?.DefencePercent ?? 0, grantedBuff?.SlotsRemaining ?? 0);
        }

        private static double AttackMultiplier(List<SkillBuff> buffs, out int powerBuffPercent)
        {
            int percent = 100;
            foreach (SkillBuff buff in buffs) percent += buff.AttackPercent;
            powerBuffPercent = percent - 100;
            return percent / 100d;
        }

        private static double ReceivedMultiplier(List<SkillBuff> buffs, out int protectionBuffPercent)
        {
            int percent = 100;
            foreach (SkillBuff buff in buffs) percent -= buff.DefencePercent;
            percent = Math.Max(0, percent);
            protectionBuffPercent = Math.Max(0, 100 - percent);
            return percent / 100d;
        }

        private static void TickBuffs(List<SkillBuff> buffs)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
                if (--buffs[i].SlotsRemaining == 0) buffs.RemoveAt(i);
        }

        private static LegacySkill[] FirstSixSkills()
        {
            var result = new LegacySkill[6];
            for (int i = 0; i < result.Length; i++) result[i] = LegacyInitialSkills.All[i];
            return result;
        }

        private static LegacySkill[] CopySkills(IReadOnlyList<LegacySkill> source, string argument)
        {
            if (source == null || source.Count == 0) throw new ArgumentException("At least one skill is required.", argument);
            var copy = new LegacySkill[source.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = source[i] ?? throw new ArgumentException("Skills cannot be null.", argument);
            return copy;
        }

        private sealed class SkillBuff
        {
            public SkillBuff(int attackPercent, int defencePercent, int slotsRemaining)
            {
                AttackPercent = attackPercent;
                DefencePercent = defencePercent;
                SlotsRemaining = slotsRemaining;
            }
            public int AttackPercent { get; }
            public int DefencePercent { get; }
            public int SlotsRemaining;
        }
    }
}
