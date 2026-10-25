using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Tavern;

namespace TurnLimbo.Core.Tests
{
    public sealed class PowerBreakFactoryTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void EveryCampaignFactory_OptsIntoThePrototypeWithoutChangingHealthOrTheEnemyRoster(int stage)
        {
            CampaignRun run = AtStage(stage);
            LegacyQueuedDuel legacy = run.CreateDuel(37, meshPercent: 0);
            LegacyQueuedDuel prototype = run.CreateDuel(37, meshPercent: 0, usePowerBreak: true);
            Assert.That(legacy.UsePowerBreak, Is.False, "The factory retains its explicit rollback path.");
            Assert.That(prototype.UsePowerBreak, Is.True);
            Assert.That(prototype.Player.MaxHealth, Is.EqualTo(legacy.Player.MaxHealth));
            Assert.That(prototype.Enemies.Count, Is.EqualTo(legacy.Enemies.Count));
            for (int index = 0; index < prototype.Enemies.Count; index++)
                Assert.That(prototype.Enemies[index].MaxHealth, Is.EqualTo(legacy.Enemies[index].MaxHealth));
            Assert.That(prototype.RoundLimit, Is.EqualTo(legacy.RoundLimit));
            Assert.That(prototype.Features, Is.EqualTo(legacy.Features));
            Assert.That(prototype.TryQueueLane(0), Is.True);
            prototype.Commit();
            LegacySlotResult first = prototype.ResolveNextSlot();
            Assert.That(first.PlayerResistanceDamage + first.EnemyResistanceDamage, Is.Zero,
                "A factory-created battle actually executes the HP-only rule.");
        }

        [Test]
        public void CampaignFactory_ProducesSingleSkillDestructionAndResetClearsItWithoutChangingTheMode()
        {
            LegacyQueuedDuel duel = AtStage(1).CreateDuel(21, meshPercent: 0, usePowerBreak: true);
            Assert.That(duel.TrySetFixedQueues(new[] { Attack(-99100, 15), Attack(-99101, 4), Attack(-99102, 4) },
                new[] { Attack(-99200, 10), Attack(-99201, 4), Attack(-99202, 4) }), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            Assert.That(duel.IsEnemySkillDestroyed(1), Is.True);
            Assert.That(duel.IsEnemySkillDestroyed(2), Is.False);
            Assert.That(duel.ResolveNextSlot().EnemyHealthDamage, Is.EqualTo(8));
            Assert.That(duel.ResolveNextSlot().EnemySkill.Id, Is.EqualTo(-99202));
            duel.Reset();
            Assert.That(duel.UsePowerBreak, Is.True);
            Assert.That(duel.IsEnemySkillDestroyed(1), Is.False);
            Assert.That(duel.Player.Health, Is.EqualTo(duel.Player.MaxHealth));
            Assert.That(duel.Enemy.Health, Is.EqualTo(duel.Enemy.MaxHealth));
        }

        [Test]
        public void TrainingFactory_ZeroResistanceDoesNotCreatePermanentDoubleDamage()
        {
            CampaignRun run = AtStage(2);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.TryStartTraining(), Is.True);
            LegacyQueuedDuel duel = run.CreateDuel(meshPercent: 0, usePowerBreak: true);
            Assert.That(duel.UsePowerBreak, Is.True);
            Assert.That(duel.Enemy.MaxResistance, Is.Zero, "The existing training asset need not be rewritten.");
            Assert.That(duel.TrySetFixedQueues(new[] { Attack(-99100, 10) }, new[] { LegacyCommonActions.Breathe }), Is.True);
            duel.Commit();
            LegacySlotResult result = duel.ResolveNextSlot();
            Assert.That(result.EnemyHealthDamage, Is.EqualTo(10));
            Assert.That(result.EnemyResistanceDamage, Is.Zero);
            Assert.That(duel.Enemy.IsResistanceBroken, Is.False);
        }

        [Test]
        public void StoryMissionFactories_ForwardTheModeAndPreserveTheirFloorsAndPlanningRules()
        {
            foreach (PrologueMission mission in StoryMissions.All)
            {
                LegacyQueuedDuel legacy = mission.CreateDuel(31, meshPercent: 0);
                LegacyQueuedDuel prototype = mission.CreateDuel(31, meshPercent: 0, usePowerBreak: true);
                Assert.That(legacy.UsePowerBreak, Is.False, mission.Title);
                Assert.That(prototype.UsePowerBreak, Is.True, mission.Title);
                Assert.That(prototype.Player.MaxHealth, Is.EqualTo(legacy.Player.MaxHealth), mission.Title);
                Assert.That(prototype.Enemy.MaxHealth, Is.EqualTo(legacy.Enemy.MaxHealth), mission.Title);
                Assert.That(prototype.Player.HealthFloor, Is.EqualTo(legacy.Player.HealthFloor), mission.Title);
                Assert.That(prototype.Enemy.HealthFloor, Is.EqualTo(legacy.Enemy.HealthFloor), mission.Title);
                Assert.That(prototype.Features, Is.EqualTo(legacy.Features), mission.Title);
            }
        }

        [Test]
        public void Tavern_CompletedTurnReplayKeepsModeAndSettledHealthWhenTheNextTurnIsAbandoned()
        {
            var run = new SkillTavernRun(new[] { Attack(-99100, 12) }, seed: 4);
            var battle = new SkillTavernBattle(run, seed: 53, usePowerBreak: true);
            Assert.That(battle.TryStartTurn(), Is.True);
            ResolveTurn(battle.Duel);
            Assert.That(battle.TryCompleteTurn(), Is.True);
            int playerHealth = battle.Duel.Player.Health, enemyHealth = battle.Duel.Enemy.Health;
            Assert.That(playerHealth, Is.LessThan(SkillTavernBattle.FighterHealth));
            Assert.That(run.RoundNumber, Is.EqualTo(2));
            Assert.That(battle.TryStartTurn(), Is.True);
            battle.Duel.ResolveNextSlot();
            Assert.That(battle.TryCancelTurn(), Is.True);
            Assert.That(battle.Duel.UsePowerBreak, Is.True);
            Assert.That(battle.Duel.Player.Health, Is.EqualTo(playerHealth));
            Assert.That(battle.Duel.Enemy.Health, Is.EqualTo(enemyHealth));
            Assert.That(run.RoundNumber, Is.EqualTo(2));
            Assert.That(battle.TryStartTurn(), Is.True);
            Assert.That(battle.Duel.UsePowerBreak, Is.True);
        }

        [Test]
        public void Tavern_CancelingADestructiveOpeningRestoresTheSameRollAndOnlyTheNewAttemptHasADestroyedCard()
        {
            var run = new SkillTavernRun(new[] { Attack(-99100, 12) }, seed: 4);
            Assert.That(run.TryBuyAndDeploy(0), Is.True);
            var battle = new SkillTavernBattle(run, seed: 71, usePowerBreak: true);
            Assert.That(battle.TryStartTurn(), Is.True);
            LegacySlotResult original = battle.Duel.ResolveNextSlot();
            Assert.That(battle.Duel.IsEnemySkillDestroyed(1), Is.True);
            Assert.That(battle.Duel.IsEnemySkillDestroyed(2), Is.False);
            Assert.That(battle.TryCancelTurn(), Is.True);
            Assert.That(battle.Duel.UsePowerBreak, Is.True);
            Assert.That(battle.Duel.Player.Health, Is.EqualTo(SkillTavernBattle.FighterHealth));
            Assert.That(battle.Duel.Enemy.Health, Is.EqualTo(SkillTavernBattle.FighterHealth));
            Assert.That(battle.Duel.IsEnemySkillDestroyed(1), Is.False);
            Assert.That(battle.TryStartTurn(), Is.True);
            LegacySlotResult replay = battle.Duel.ResolveNextSlot();
            Assert.That(replay.PlayerHealthDamage, Is.EqualTo(original.PlayerHealthDamage));
            Assert.That(replay.EnemyHealthDamage, Is.EqualTo(original.EnemyHealthDamage));
            Assert.That(battle.Duel.IsEnemySkillDestroyed(1), Is.True);
            Assert.That(battle.Duel.IsEnemySkillDestroyed(2), Is.False);
        }

        private static CampaignRun AtStage(int stage)
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            for (int current = 1; current < stage; current++)
            {
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.TryStartNextStage(), Is.True);
            }
            return run;
        }

        private static LegacySkill Attack(int id, int power)
            => new LegacySkill(id, "Factory power-break probe", 1, power, power,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);

        private static void ResolveTurn(LegacyQueuedDuel duel)
        {
            while (!duel.IsTurnResolved && !duel.IsFinished) duel.ResolveNextSlot();
        }
    }
}
