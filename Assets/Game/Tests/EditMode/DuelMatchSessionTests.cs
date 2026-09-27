using System;
using NUnit.Framework;
using TurnLimbo.Core.Combat;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Core.Tests
{
    public sealed class DuelMatchSessionTests
    {
        [Test]
        public void ResolveRound_UsesVisibleIntentAndAdvancesPattern()
        {
            DuelMatchSession session = CreateSession();
            CombatMove firstIntent = session.EnemyIntent;

            DuelTurnResult result = session.ResolveRound(CombatMove.Guard("guard", 2));

            Assert.That(result.EnemyMove, Is.SameAs(firstIntent));
            Assert.That(result.Resolution.DamageTakenByLeft, Is.EqualTo(1));
            Assert.That(session.RoundNumber, Is.EqualTo(2));
            Assert.That(session.EnemyIntent.Id, Is.EqualTo("enemy-wait"));
        }

        [Test]
        public void LethalPlayerAttack_EndsMatchWithVictory()
        {
            DuelMatchSession session = new DuelMatchSession(
                4,
                new[] { CombatMove.Wait("enemy-wait") });

            DuelTurnResult result = session.ResolveRound(
                CombatMove.Attack("finisher", DamageType.Slash, 4));

            Assert.That(result.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
            Assert.That(session.IsFinished, Is.True);
            Assert.That(session.Enemy.Health, Is.Zero);
        }

        [Test]
        public void SimultaneousLethalAttacks_EndMatchInDraw()
        {
            DuelMatchSession session = new DuelMatchSession(
                4,
                new[] { CombatMove.Attack("enemy-attack", DamageType.Slash, 4) });

            DuelTurnResult result = session.ResolveRound(
                CombatMove.Attack("player-attack", DamageType.Slash, 4));

            Assert.That(result.Outcome, Is.EqualTo(DuelMatchOutcome.Draw));
            Assert.That(session.Player.Health, Is.Zero);
            Assert.That(session.Enemy.Health, Is.Zero);
        }

        [Test]
        public void FinishedMatch_RejectsAdditionalRoundsUntilReset()
        {
            DuelMatchSession session = new DuelMatchSession(
                2,
                new[] { CombatMove.Wait("enemy-wait") });
            session.ResolveRound(CombatMove.Attack("finisher", DamageType.Slash, 2));

            Assert.Throws<InvalidOperationException>(() =>
                session.ResolveRound(CombatMove.Wait()));

            session.Reset();

            Assert.That(session.IsFinished, Is.False);
            Assert.That(session.Player.Health, Is.EqualTo(2));
            Assert.That(session.Enemy.Health, Is.EqualTo(2));
            Assert.That(session.RoundNumber, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_CopiesEnemyPattern()
        {
            CombatMove[] pattern =
            {
                CombatMove.Wait("original"),
            };
            DuelMatchSession session = new DuelMatchSession(10, pattern);

            pattern[0] = CombatMove.Attack("replacement", DamageType.Strike, 9);

            Assert.That(session.EnemyIntent.Id, Is.EqualTo("original"));
        }

        private static DuelMatchSession CreateSession()
        {
            return new DuelMatchSession(
                10,
                new[]
                {
                    CombatMove.Attack("enemy-attack", DamageType.Slash, 3),
                    CombatMove.Wait("enemy-wait"),
                });
        }
    }
}
