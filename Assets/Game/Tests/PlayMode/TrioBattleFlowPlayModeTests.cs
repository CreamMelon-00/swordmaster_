using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class TrioBattleFlowPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator StageFive_UsesThreeWeakerMembers_AndEachOwnerActuallyTakesItsTurn_WhilePauseFreezesEveryone()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                var roster = (ILegacyEnemyRosterView)controller.Session;
                Assert.That(controller.IsGroupBattle, Is.True);
                Assert.That(roster.Enemies.Count, Is.EqualTo(3));
                Assert.That(controller.ArenaView.EnemyCount, Is.EqualTo(3));
                Assert.That(controller.EnemyHealth, Is.EqualTo(150));
                var owners = new int[3];
                for (int slot = 0; slot < controller.Session.EnemyQueue.Count; slot++) owners[roster.EnemyOwnerAtSlot(slot)]++;
                Assert.That(owners, Is.EqualTo(new[] { 2, 2, 2 }));
                foreach (var member in roster.Enemies)
                {
                    Assert.That(member.MaxHealth, Is.EqualTo(50));
                    Assert.That(member.MaxResistance, Is.EqualTo(10));
                }
                var positions = new Vector3[3];
                for (int i = 0; i < 3; i++) positions[i] = controller.ArenaView.GetEnemyGroundPosition(i);
                Vector3 player = controller.ArenaView.PlayerGroundPosition;
                float time = controller.TurnTimeRemaining;
                Assert.That(controller.PauseBattle(), Is.True);
                scope.Advance(.5f);
                Assert.That(controller.ArenaView.PlayerGroundPosition, Is.EqualTo(player));
                for (int i = 0; i < 3; i++) Assert.That(controller.ArenaView.GetEnemyGroundPosition(i), Is.EqualTo(positions[i]));
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(time));
                Assert.That(controller.ResumeBattle(), Is.True);
                controller.CommitTurn();
                var observed = new HashSet<int>();
                int frames = 0;
                while (controller.IsResolving && frames++ < 6000)
                {
                    scope.Advance(.02f);
                    if (controller.Session.CurrentSlot != null)
                    {
                        int active = roster.ActiveEnemyIndex;
                        observed.Add(active);
                        Assert.That(controller.ArenaView.ActiveEnemyIndex, Is.EqualTo(active), "The rules and the visible attacker must be the same member.");
                    }
                }
                Assert.That(frames, Is.LessThan(6000), "Every relay must reach contact and finish; no crossed positions may stall it.");
                Assert.That(observed.Count, Is.EqualTo(3), "All three enemies enter the actual shared turn.");
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.EnemyQueue.Count, Is.EqualTo(3), "The lighter turn is one technique per living member.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IndividualFalls_StopTheirMultiHitWithoutEndingTheBattle_ThenAllThreePayOneReward_AndRetryRestoresTheRoster()
        {
            yield return null;
            using (var scope = new FlowScope())
            {
                var controller = scope.Controller;
                var strike = new LegacySkill(98921, "교대 검증", 0, 100, 100, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 3, 0, string.Empty, iconId: 1);
                var secondLaneStrike = new LegacySkill(98921, "교대 검증", 0, 100, 100, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 3, 1, string.Empty, iconId: 1);
                var harmless = new LegacySkill(98922, "받아내기 검증", 0, 0, 0, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);
                var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { new[] { harmless, harmless } });
                var definitions = new[]
                {
                    new LegacyEnemyDefinition("훈련생 1", 6, 0, script),
                    new LegacyEnemyDefinition("훈련생 2", 6, 0, script),
                    new LegacyEnemyDefinition("훈련생 3", 6, 0, script),
                };
                scope.Install(new LegacyQueuedDuel(100, 50, new[] { strike, secondLaneStrike }, definitions, 412, meshPercent: 0));
                var roster = (ILegacyEnemyRosterView)controller.Session;
                int currency = controller.Campaign.Currency;
                for (int i = 0; i < 6; i++) Assert.That(controller.QueueLane(i < 3 ? 0 : 1), Is.True);
                controller.CommitTurn();
                int deaths = 0, frames = 0;
                while (!controller.IsShowingResult && frames++ < 6000)
                {
                    scope.Advance(.02f);
                    int now = 0;
                    foreach (var member in roster.Enemies) if (member.IsDefeated) now++;
                    if (now > deaths)
                    {
                        deaths = now;
                        if (now < 3)
                        {
                            Assert.That(controller.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
                            Assert.That(controller.IsShowingResult, Is.False);
                            Assert.That(controller.Session.CurrentSlot?.HitsResolved ?? 1, Is.LessThanOrEqualTo(1), "A fallen member cannot receive the remaining strikes of the skill.");
                        }
                    }
                }
                Assert.That(deaths, Is.EqualTo(3));
                Assert.That(controller.IsShowingResult, Is.True);
                Assert.That(controller.Outcome, Is.EqualTo(DuelMatchOutcome.PlayerVictory));
                Assert.That(controller.EnemyHealth, Is.Zero);
                Assert.That(controller.Campaign.Currency, Is.EqualTo(currency + controller.Campaign.GetStage(5).Reward));
                Assert.That(controller.Campaign.GetOwnedSkill(46), Is.Not.Null, "The stage's first-clear technique is still granted once.");
                Assert.That(controller.RetryBattleResult(), Is.True);
                controller.SkipStartCard();
                Assert.That(controller.ArenaView.EnemyCount, Is.EqualTo(3));
                Assert.That(controller.EnemyHealth, Is.EqualTo(150));
                controller.ReturnToLobby();
                Assert.That(controller.StartCampaignStage(4), Is.True);
                Assert.That(controller.IsGroupBattle, Is.False);
                Assert.That(controller.ArenaView.EnemyCount, Is.EqualTo(1), "Returning to a normal stage removes the extra bodies and anchors.");
            }
            yield return null;
        }

        private sealed class FlowScope : IDisposable
        {
            private readonly bool wasEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public FlowScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                wasEnabled = Controller.enabled;
                Controller.enabled = false;
                Controller.RestartJourney();
                for (int stage = 1; stage <= 4; stage++)
                {
                    Assert.That(Controller.Campaign.TryStartStage(stage), Is.True);
                    Assert.That(Controller.Campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                    Assert.That(Controller.Campaign.ReturnToLobby(), Is.True);
                }
                Assert.That(Controller.StartCampaignStage(5), Is.True);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                Controller.SkipStartCard();
                for (int i = 0; i < 35; i++) Advance(.02f);
            }
            public void Advance(float delta) => advance(delta, null);
            public void Install(LegacyQueuedDuel duel)
            {
                typeof(DuelPrototypeController).GetField("session", Private).SetValue(Controller, duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                Controller.SkipStartCard();
            }
            public void Dispose() { Controller.RestartMatch(); Controller.enabled = wasEnabled; }
        }
    }
}
