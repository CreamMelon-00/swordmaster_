using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The 서막's last mission: 이아 cannot fall, receives 수훈 at half health and then wins a forced loss
    /// that completes the 서막 with the Dominican motto turn (라우다레, 베네디체레, 프레디카레). The simulations play the
    /// battle as presentation does: at the reported hit the event's scene plays and the enemy switches, then the turn
    /// resumes where it paused.</summary>
    public sealed class MissionFourTests
    {
        /// <summary>How the player plans every turn after the event (and, for <see cref="Pass"/>, also before it).</summary>
        public enum Policy
        {
            /// <summary>Only attacks, turning 막기 away with 넘기기.</summary>
            Attack,
            /// <summary>Only 막기.</summary>
            Guard,
            /// <summary>Nothing queued, as when the 10-second limit runs out (숨고르기 is closed in the 서막).</summary>
            Pass,
            /// <summary>Attack and 막기 by turns within the queue.</summary>
            Mixed,
            /// <summary>막기 against 라우다레 and 프레디카레 (the motto turn's first and third slots), an attack between.</summary>
            GuardEnds,
        }

        private static PrologueMission MissionFour => PrologueMissions.Get(4);

        [Test]
        public void Briefing_NamesTheWanderingKnight_WhileTheBattleNamesIa()
        {
            Assert.That(MissionFour.Enemies.Single().Name, Is.EqualTo("떠돌이 기사"));
            Assert.That(MissionFour.BattleEnemyName, Is.EqualTo("이아"));
            foreach (PrologueMission mission in StoryMissions.All.Where(mission => mission.Number != 4))
                Assert.That(mission.BattleEnemyName, Is.EqualTo(mission.Enemies[0].Name), mission.Title);
            Assert.That(StoryMissions.All.Where(mission => mission.Number > 4).Select(mission => mission.BattleEnemyName).Distinct(),
                Is.EqualTo(new[] { "떠돌이 기사" }), "The lobby missions keep their name for now.");
            Assert.That(MissionFour.PlanningTimer, Is.True, "The 10-second limit stays.");
            Assert.That(MissionFour.IntroCutscene, Is.EqualTo("Cutscene/mission-04-intro"));
            Assert.That(MissionFour.OutroCutscene, Is.EqualTo("Cutscene/mission-04-outro"));
            MissionGuideBeat free = MissionFour.CreateGuide().Beats.Last();
            Assert.That(free.Title, Is.EqualTo("이아"), "The coach speaks during the battle, so it says 이아 too.");
            Assert.That(free.Description, Does.Contain("이아와").And.Not.Contain("떠돌이 기사"));
            Assert.That(MissionFour.Objectives.Last(), Does.Contain("떠돌이 기사"), "The briefing's objectives keep the old name.");
            // The battle is lost by design: its copy neither promises a win nor gives the loss away.
            foreach (string copy in MissionFour.Objectives.Concat(MissionFour.CreateGuide().Beats.Select(beat => beat.Description)))
                foreach (string word in new[] { "쓰러뜨", "꺾", "이기", "승리", "패배", "진다" })
                    Assert.That(copy, Does.Not.Contain(word), copy);
        }

        [Test]
        public void Empowerment_FiresAtHalfHealth_SwitchesToTheMottoTurn_AndIaCannotFall()
        {
            MissionEmpowerment empowerment = MissionFour.Empowerment;
            Assert.That(empowerment.ThresholdPercent, Is.EqualTo(50));
            Assert.That(empowerment.Scene, Is.EqualTo("Cutscene/mission-04-event"));
            Assert.That(empowerment.KeepsAura, Is.True);
            Assert.That(empowerment.ForcedLoss, Is.True);
            EnemyScript script = empowerment.EnemyScript;
            Assert.That(script.OpeningLength, Is.Zero);
            Assert.That(script.TurnSizes, Is.EqualTo(new[] { 3 }), "The same three every turn.");
            int[] motto = { 500, 501, 502 };
            Assert.That(script.Turn(1).Select(skill => skill.Name), Is.EqualTo(new[] { "라우다레", "베네디체레", "프레디카레" }));
            for (int slot = 0; slot < motto.Length; slot++)
                Assert.That(script.Turn(1)[slot], Is.SameAs(LegacySkillDefinitions.Skill(motto[slot])), "Read from the sheet by id.");
            Assert.That(script.Turn(4).Select(skill => skill.Id), Is.EqualTo(motto));
            Assert.That(LegacySkillDefinitions.EnemySkills.Select(skill => skill.Id), Is.EquivalentTo(motto), "All three are enemy-only rows.");
            Assert.That(MissionFour.EnemyHealthFloor, Is.EqualTo(1));

            LegacyQueuedDuel duel = MissionFour.CreateDuel(3);
            Assert.That(duel.EnemyHealthFloor, Is.EqualTo(1));
            Assert.That(duel.EnemyHealthThresholdPercent, Is.EqualTo(50));
            Assert.That(duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(new[] { 1, 5 }), "Before the event she fights as before.");
            foreach (PrologueMission other in StoryMissions.All.Where(mission => mission.Number != 4))
            {
                Assert.That(other.Empowerment, Is.Null, other.Title);
                Assert.That(other.EnemyHealthFloor, Is.Zero, other.Title);
                LegacyQueuedDuel otherDuel = other.CreateDuel();
                Assert.That(otherDuel.EnemyHealthFloor + otherDuel.EnemyHealthThresholdPercent, Is.Zero, other.Title);
            }
        }

        [Test]
        public void Completion_ADefeatCountsOnlyAfterTheEmpowerment_AndOpensTheLobby()
        {
            Assert.That(MissionFour.Completes(DuelMatchOutcome.PlayerVictory, false), Is.True);
            Assert.That(MissionFour.Completes(DuelMatchOutcome.EnemyVictory, false), Is.False, "Before the event: an ordinary failure.");
            Assert.That(MissionFour.Completes(DuelMatchOutcome.EnemyVictory, true), Is.True);
            Assert.That(MissionFour.Completes(DuelMatchOutcome.Draw, true), Is.False);
            Assert.That(PrologueMissions.Get(3).Completes(DuelMatchOutcome.EnemyVictory, true), Is.False, "No forced loss there.");

            var run = new PrologueRun();
            Assert.That(run.TryRestore(3), Is.True);
            Assert.That(run.TryComplete(4, DuelMatchOutcome.EnemyVictory), Is.False);
            Assert.That(run.IsArcComplete, Is.False);
            Assert.That(run.TryComplete(4, DuelMatchOutcome.EnemyVictory, empowered: true), Is.True);
            Assert.That(run.IsArcComplete, Is.True, "The forced loss ends the 서막 and opens the lobby, as the old win did.");
            Assert.That(run.CurrentMission.Number, Is.EqualTo(5));
            Assert.That(run.TryComplete(4, DuelMatchOutcome.EnemyVictory, empowered: true), Is.False, "Replays never move progress.");
        }

        [TestCase(Policy.Attack, 1)]
        [TestCase(Policy.Attack, 2)]
        [TestCase(Policy.Attack, 3)]
        [TestCase(Policy.Guard, 1)]
        [TestCase(Policy.Guard, 2)]
        [TestCase(Policy.Guard, 3)]
        [TestCase(Policy.Pass, 1)]
        [TestCase(Policy.Pass, 2)]
        [TestCase(Policy.Pass, 3)]
        [TestCase(Policy.Mixed, 1)]
        [TestCase(Policy.Mixed, 2)]
        [TestCase(Policy.Mixed, 3)]
        [TestCase(Policy.GuardEnds, 1)]
        [TestCase(Policy.GuardEnds, 2)]
        [TestCase(Policy.GuardEnds, 3)]
        public void AfterTheEvent_ThePlayerLosesWithinTwoMoreTurns(Policy policy, int seed)
        {
            // Attacks bring 이아 to half health first; from then on the policy plays.
            LegacyQueuedDuel duel = MissionFour.CreateDuel(seed);
            Play(duel, Policy.Attack, policy, 40, out int eventRound);
            Assert.That(eventRound, Is.InRange(1, 6), "Attacking reaches the event within a few turns.");
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            // 라우다레 breaks the player and lands five doubled hits (at least 50 health even through 막기); 프레디카레 then
            // strikes the still broken player, doubled once more at 30% health or less.
            Assert.That(duel.RoundNumber - eventRound, Is.InRange(1, 2), "Lost in the first or second motto turn.");
            Assert.That(duel.Enemy.Health, Is.GreaterThanOrEqualTo(1));
            Assert.That(MissionFour.Completes(duel.Outcome, empowered: true), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ADefeatBeforeTheEvent_IsAnOrdinaryFailure(int seed)
        {
            // Never attacking keeps 이아 above half health until the player falls.
            LegacyQueuedDuel duel = MissionFour.CreateDuel(seed);
            Play(duel, Policy.Pass, Policy.Pass, 200, out int eventRound);
            Assert.That(duel.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
            Assert.That(eventRound, Is.Zero);
            Assert.That(duel.EnemyHealthThresholdReached, Is.False);
            Assert.That(MissionFour.Completes(duel.Outcome, empowered: false), Is.False);
            var run = new PrologueRun();
            run.TryRestore(3);
            Assert.That(run.TryComplete(4, duel.Outcome), Is.False, "Retry as before.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TheMottoTurn_Breaks_RecoversBecauseThePlayerIsBroken_ThenFinishes(int seed)
        {
            LegacyQueuedDuel duel = MissionFour.CreateDuel(seed);
            Play(duel, Policy.Attack, Policy.Attack, 40, out int eventRound, stopAtEmpowerment: true);
            Assert.That(eventRound, Is.Not.Zero);
            Assert.That(duel.RoundNumber, Is.EqualTo(eventRound + 1), "Planning the first turn after the event's.");
            Assert.That(duel.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(new[] { 500, 501, 502 }));
            // Nothing queued, as when the planning time runs out: the motto meets three empty slots.
            duel.Commit();

            LegacyCurrentSlot laudare = duel.BeginNextSlot();
            Assert.That(laudare.EnemyFeedback.OpponentBroken, Is.True);
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
            Assert.That(duel.Player.IsResistanceBroken, Is.True);
            Assert.That(duel.Player.Health, Is.InRange(30, 40), "Five hits of 6 or 7, doubled on the broken player.");

            int iaResistance = duel.Enemy.Resistance;
            LegacyCurrentSlot benedicere = duel.BeginNextSlot();
            // A quarter of 15 is 3.75, rounded to 4, up to her maximum.
            int restored = Math.Min(4, duel.Enemy.MaxResistance - iaResistance);
            Assert.That(benedicere.EnemyFeedback.ResistanceRestored, Is.EqualTo(restored));
            Assert.That(benedicere.EnemyFeedback.ConditionMet && benedicere.EnemyFeedback.EffectActivated, Is.EqualTo(restored > 0));
            Assert.That(duel.Enemy.Resistance, Is.EqualTo(iaResistance + restored));
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            duel.CompleteCurrentSlot();

            int health = duel.Player.Health;
            bool lowHealth = health * 100 <= 30 * duel.Player.MaxHealth;
            LegacyCurrentSlot praedicare = duel.BeginNextSlot();
            Assert.That(praedicare.EnemyFeedback.ConditionalDamagePercent, Is.EqualTo(lowHealth ? 200 : 0));
            LegacyHitResult strike = duel.ResolveNextHit();
            Assert.That(strike.EnemyAttackConditionMet, Is.EqualTo(lowHealth));
            Assert.That(strike.PlayerDisplayedDamage, Is.EqualTo(strike.PlayerPushPower * (lowHealth ? 4 : 2)),
                "Broken: double; at 30% or less: double again.");
            Assert.That(strike.PlayerPushPower, Is.InRange(24, 32));
            Assert.That(duel.CompleteCurrentSlot().Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory), "48 or more on 40 health or less.");
        }

        /// <summary>Plays until the duel ends or <paramref name="maximumTurns"/> (with <paramref name="stopAtEmpowerment"/>, until
        /// the planning of the turn after the event's); returns the round the event fired in (0: never).</summary>
        private static void Play(LegacyQueuedDuel duel, Policy before, Policy after, int maximumTurns, out int eventRound,
            bool stopAtEmpowerment = false)
        {
            eventRound = 0;
            while (!duel.IsFinished && duel.RoundNumber <= maximumTurns && !(stopAtEmpowerment && eventRound != 0))
            {
                Plan(duel, eventRound == 0 ? before : after);
                duel.Commit();
                while (!duel.IsTurnResolved && !duel.IsFinished)
                {
                    duel.BeginNextSlot();
                    while (!duel.IsCurrentSlotResolved)
                    {
                        LegacyHitResult hit = duel.ResolveNextHit();
                        Assert.That(duel.Enemy.Health, Is.GreaterThanOrEqualTo(1), "이아 never falls.");
                        if (!hit.EnemyReachedHealthThreshold) continue;
                        Assert.That(eventRound, Is.Zero, "Once per attempt.");
                        eventRound = duel.RoundNumber;
                        duel.ReplaceEnemyScript(MissionFour.Empowerment.EnemyScript);
                    }
                    duel.CompleteCurrentSlot();
                }
                if (!duel.IsFinished) duel.BeginNextTurn();
            }
        }

        // Queues the Q lane's front while ACT lasts, turning the lane (넘기기) until the wanted kind comes up.
        private static void Plan(LegacyQueuedDuel duel, Policy policy)
        {
            if (policy == Policy.Pass) return;
            for (int step = 0; step < 40 && duel.PlayerQueue.Count < 8; step++)
            {
                int slot = duel.PlayerQueue.Count;
                bool guard = policy == Policy.Guard || policy == Policy.Mixed && slot % 2 == 1 ||
                    policy == Policy.GuardEnds && (slot == 0 || slot == 2);
                if ((duel.GetLane(0)[0].Kind == LegacySkillKind.Defence) != guard) duel.TryCycleLanes();
                else if (!duel.TryQueueLane(0)) return;
            }
        }
    }
}
