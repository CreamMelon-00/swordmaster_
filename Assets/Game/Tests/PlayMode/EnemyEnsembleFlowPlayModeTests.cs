using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The real stage-five controller owns the relay clock; preparing another body never starts its rules.</summary>
    public sealed class EnemyEnsembleFlowPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator StageFivePlanning_AllThreeApproachTogetherAtDifferentSpeeds_AndPauseHoldsAllOfThem()
        {
            yield return null;
            using (var scope = new EnsembleScope())
            {
                var controller = scope.Controller;
                var arena = controller.ArenaView;
                Assert.That(controller.IsGroupBattle, Is.True);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1), "Planning has no recovery preview left over.");
                var before = EnemyPositions(arena);
                float clock = controller.TurnTimeRemaining;
                scope.AdvanceFor(.3f);
                var travel = new float[3];
                for (int enemy = 0; enemy < 3; enemy++)
                {
                    travel[enemy] = before[enemy].x - arena.GetEnemyGroundPosition(enemy).x;
                    Assert.That(travel[enemy], Is.GreaterThan(.001f), "Every living enemy approaches during the same planning interval: " + enemy);
                }
                Assert.That(travel[1], Is.GreaterThan(travel[0] + .001f));
                Assert.That(travel[2], Is.GreaterThan(travel[1] + .001f), "Different movement rates keep the three approaches from looking synchronized.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(clock - .3f).Within(.002f));
                Assert.That(controller.Session.CurrentSlot, Is.Null, "Walking in preparation cannot execute an attack.");

                var held = EnemyPositions(arena);
                Vector3 player = arena.PlayerGroundPosition;
                clock = controller.TurnTimeRemaining;
                Assert.That(controller.PauseBattle(), Is.True);
                scope.AdvanceFor(.4f);
                Assert.That(arena.PlayerGroundPosition, Is.EqualTo(player));
                AssertEnemyPositions(arena, held);
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(clock));
                Assert.That(controller.ResumeBattle(), Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingleHitRecovery_PreparesTheNextBodyWithoutAdvancingItsSkillOrDamage_ThenPromotesIt()
        {
            yield return null;
            using (var scope = new EnsembleScope())
            {
                scope.Install(CreateDuel(1));
                var controller = scope.Controller;
                var arena = controller.ArenaView;
                var roster = (ILegacyEnemyRosterView)controller.Session;
                int[] queueIds = QueueIds(controller.Session.EnemyQueue);
                int currentOwner = roster.EnemyOwnerAtSlot(0), nextOwner = roster.EnemyOwnerAtSlot(1);
                Assert.That(nextOwner, Is.Not.EqualTo(currentOwner));
                QueueSixAndCommit(controller);
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 0 && controller.Session.CurrentSlot.IsResolved &&
                    arena.UpcomingEnemyIndex == nextOwner, "The next member should be readied during the current skill's recovery.");

                LegacyCurrentSlot current = controller.Session.CurrentSlot;
                int health = roster.TotalEnemyHealth;
                int playerHealth = controller.Session.Player.Health;
                int playerResistance = controller.Session.Player.Resistance;
                var resistance = new int[3];
                for (int enemy = 0; enemy < 3; enemy++) resistance[enemy] = roster.Enemies[enemy].Resistance;
                Vector3 nextBefore = arena.GetEnemyGroundPosition(nextOwner);
                Assert.That(roster.ActiveEnemyIndex, Is.EqualTo(currentOwner));
                Assert.That(arena.ActiveEnemyIndex, Is.EqualTo(currentOwner));
                Assert.That(controller.Session.LastResolvedSlot, Is.EqualTo(-1), "The current slot is still recovering.");
                Assert.That(roster.Enemies[nextOwner].Health, Is.EqualTo(5000), "Preparing an attack cannot damage its owner early.");
                Assert.That(QueueIds(controller.Session.EnemyQueue), Is.EqualTo(queueIds));

                Assert.That(controller.PauseBattle(), Is.True);
                Vector3[] held = EnemyPositions(arena);
                scope.AdvanceFor(.4f);
                AssertEnemyPositions(arena, held);
                Assert.That(controller.Session.CurrentSlot, Is.SameAs(current));
                Assert.That(roster.TotalEnemyHealth, Is.EqualTo(health));
                Assert.That(controller.ResumeBattle(), Is.True);

                bool movedBeforePromotion = false;
                int frames = 0;
                while (arena.ActiveEnemyIndex == currentOwner && frames++ < 1000)
                {
                    scope.Advance(.005f);
                    if (arena.ActiveEnemyIndex != currentOwner) break;
                    movedBeforePromotion |= arena.GetEnemyGroundPosition(nextOwner).x < nextBefore.x - .001f;
                    Assert.That(roster.ActiveEnemyIndex, Is.EqualTo(currentOwner));
                    Assert.That(roster.TotalEnemyHealth, Is.EqualTo(health), "The next attack is visual preparation only.");
                    Assert.That(controller.Session.Player.Health, Is.EqualTo(playerHealth));
                    Assert.That(controller.Session.Player.Resistance, Is.EqualTo(playerResistance));
                    for (int enemy = 0; enemy < 3; enemy++)
                        Assert.That(roster.Enemies[enemy].Resistance, Is.EqualTo(resistance[enemy]), "Preparation must not apply the incoming clash's resistance damage.");
                }
                Assert.That(frames, Is.LessThan(1000));
                Assert.That(movedBeforePromotion, Is.True, "The incoming enemy must already advance before the outgoing slot releases it.");
                Assert.That(arena.ActiveEnemyIndex, Is.EqualTo(nextOwner));
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 1, "The previewed body must become the actual next attacker.");
                Assert.That(roster.ActiveEnemyIndex, Is.EqualTo(nextOwner));
                Assert.That(controller.Session.CurrentSlot.EnemySkill.Id, Is.EqualTo(queueIds[1]));

                scope.Until(() => controller.Session.RoundNumber == 2 && controller.CanChoose, "The relay must finish and return to planning.");
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                controller.RestartMatch();
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1), "A reset cannot retain a queued incoming attacker.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeHitSkill_KeepsTheNextEnemyWaitingThroughItsFirstTwoActualImpacts()
        {
            yield return null;
            using (var scope = new EnsembleScope())
            {
                scope.Install(CreateDuel(3));
                var controller = scope.Controller;
                var arena = controller.ArenaView;
                var roster = (ILegacyEnemyRosterView)controller.Session;
                int currentOwner = roster.EnemyOwnerAtSlot(0), nextOwner = roster.EnemyOwnerAtSlot(1);
                QueueSixAndCommit(controller);
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 0, "The first three-hit exchange must begin.");
                var observed = new HashSet<int>();
                int frames = 0;
                while (controller.Session.CurrentSlot?.SlotIndex == 0 && !controller.Session.CurrentSlot.IsResolved && frames++ < 3000)
                {
                    int hits = controller.Session.CurrentSlot.HitsResolved;
                    observed.Add(hits);
                    Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1), "The next enemy cannot enter before the current skill's final actual hit. Hits: " + hits);
                    Assert.That(roster.ActiveEnemyIndex, Is.EqualTo(currentOwner));
                    scope.Advance(.005f);
                }
                Assert.That(frames, Is.LessThan(3000));
                Assert.That(observed, Has.Member(1));
                Assert.That(observed, Has.Member(2));
                Assert.That(controller.Session.CurrentSlot?.HitsResolved, Is.EqualTo(3));
                scope.Until(() => arena.UpcomingEnemyIndex == nextOwner, "Only after the third hit may the incoming enemy prepare.");
                Assert.That(controller.Session.CurrentSlot?.SlotIndex, Is.EqualTo(0));
                Assert.That(roster.ActiveEnemyIndex, Is.EqualTo(currentOwner));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecoveryPreview_SkipsAFallenOwnersCanceledReservation_AndPreservesTheSharedQueue()
        {
            yield return null;
            using (var scope = new EnsembleScope())
            {
                LegacyQueuedDuel probe = CreateDuel(1);
                int fallenOwner = probe.EnemyOwnerAtSlot(0);
                scope.Install(CreateDuel(1, fallenOwner));
                var controller = scope.Controller;
                var arena = controller.ArenaView;
                var roster = (ILegacyEnemyRosterView)controller.Session;
                int[] queueIds = QueueIds(controller.Session.EnemyQueue);
                int currentOwner = roster.EnemyOwnerAtSlot(2), expectedIncoming = roster.EnemyOwnerAtSlot(4);
                Assert.That(roster.EnemyOwnerAtSlot(3), Is.EqualTo(fallenOwner));
                Assert.That(expectedIncoming, Is.Not.EqualTo(currentOwner));
                QueueSixAndCommit(controller);
                scope.Until(() => controller.Session.CurrentSlot?.SlotIndex == 2 && controller.Session.CurrentSlot.IsResolved &&
                    arena.UpcomingEnemyIndex == expectedIncoming, "The preview must look past the dead member's canceled slot to the next original owner.");
                Assert.That(roster.Enemies[fallenOwner].IsDefeated, Is.True);
                Assert.That(roster.IsEnemySlotCanceled(3), Is.True);
                Assert.That(roster.ActiveEnemyIndex, Is.EqualTo(currentOwner));
                Assert.That(arena.ActiveEnemyIndex, Is.EqualTo(currentOwner));
                Assert.That(arena.UpcomingEnemyIndex, Is.Not.EqualTo(fallenOwner));
                Assert.That(QueueIds(controller.Session.EnemyQueue), Is.EqualTo(queueIds), "Visual look-ahead does not compact or reorder the public reservation queue.");
                Assert.That(controller.Outcome, Is.EqualTo(DuelMatchOutcome.InProgress));
            }
            yield return null;
        }

        private static LegacyQueuedDuel CreateDuel(int playerHits, int weakOwner = -1)
        {
            var strike = new LegacySkill(98931, "교대 선행 검증", 0, 60, 60, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, playerHits, 0, string.Empty, iconId: 1);
            var enemies = new LegacyEnemyDefinition[3];
            for (int owner = 0; owner < enemies.Length; owner++)
            {
                var skill = new LegacySkill(98932 + owner, "교대 검증 " + owner, 0, 1, 1, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);
                var script = new EnemyScript(new IReadOnlyList<LegacySkill>[] { new[] { skill, skill } });
                enemies[owner] = new LegacyEnemyDefinition("검증생 " + owner, owner == weakOwner ? 1 : 5000,
                    owner == weakOwner ? 0 : 5000, script);
            }
            return new LegacyQueuedDuel(5000, 5000, new[] { strike }, enemies, 873, features: CombatFeature.LaneQ);
        }

        private static void QueueSixAndCommit(DuelPrototypeController controller)
        {
            for (int slot = 0; slot < 6; slot++) Assert.That(controller.QueueLane(0), Is.True);
            controller.CommitTurn();
            Assert.That(controller.IsResolving, Is.True);
        }

        private static int[] QueueIds(IReadOnlyList<LegacySkill> queue)
        {
            var result = new int[queue.Count];
            for (int i = 0; i < queue.Count; i++) result[i] = queue[i]?.Id ?? -1;
            return result;
        }

        private static Vector3[] EnemyPositions(LegacyArenaView arena)
        {
            var positions = new Vector3[arena.EnemyCount];
            for (int i = 0; i < positions.Length; i++) positions[i] = arena.GetEnemyGroundPosition(i);
            return positions;
        }

        private static void AssertEnemyPositions(LegacyArenaView arena, IReadOnlyList<Vector3> positions)
        {
            for (int i = 0; i < positions.Count; i++) Assert.That(arena.GetEnemyGroundPosition(i), Is.EqualTo(positions[i]));
        }

        private sealed class EnsembleScope : IDisposable
        {
            private readonly bool wasEnabled;
            private readonly Action<float, Keyboard> advance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly FieldInfo arenaSettings;
            public DuelPrototypeController Controller { get; }

            public EnsembleScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "Tests do not write the player's progress.");
                wasEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0,\"bladeBindSeconds\":0,\"battleDriftSpeed\":0.5,\"skillInterval\":0.16}", settings);
                typeof(DuelPrototypeController).GetField("presentationSettings", Private).SetValue(Controller, settings);
                arenaSettings.SetValue(Controller.ArenaView, settings);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                Controller.RestartJourney();
                for (int stage = 1; stage <= 4; stage++)
                {
                    Assert.That(Controller.Campaign.TryStartStage(stage), Is.True);
                    Assert.That(Controller.Campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                    Assert.That(Controller.Campaign.ReturnToLobby(), Is.True);
                }
                Assert.That(Controller.StartCampaignStage(5), Is.True);
                Controller.SkipStartCard();
                AdvanceFor(.7f);
            }

            public void Advance(float delta) => advance(delta, null);
            public void AdvanceFor(float seconds)
            {
                int frames = Mathf.CeilToInt(seconds / .01f);
                for (int frame = 0; frame < frames; frame++) Advance(seconds / frames);
            }
            public void Until(Func<bool> condition, string reason)
            {
                int frame = 0;
                while (!condition() && frame++ < 12000) Advance(.005f);
                Assert.That(condition(), Is.True, reason + " Phase resolving=" + Controller.IsResolving +
                    ", round=" + Controller.Session.RoundNumber + ", slot=" + Controller.Session.CurrentSlot?.SlotIndex +
                    ", hits=" + Controller.Session.CurrentSlot?.HitsResolved + ", upcoming=" + Controller.ArenaView.UpcomingEnemyIndex);
            }
            public void Install(LegacyQueuedDuel duel)
            {
                typeof(DuelPrototypeController).GetField("session", Private).SetValue(Controller, duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                Controller.SkipStartCard();
                AdvanceFor(.7f);
            }
            public void Dispose()
            {
                typeof(DuelPrototypeController).GetField("presentationSettings", Private).SetValue(Controller, originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Controller.RestartMatch();
                Controller.enabled = wasEnabled;
                Object.Destroy(settings);
            }
        }
    }
}
