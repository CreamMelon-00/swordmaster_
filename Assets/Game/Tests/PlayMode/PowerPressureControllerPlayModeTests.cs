using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class PowerPressureControllerPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Frame = .002f;
        private const string Tuning = "{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.1," +
            "\"hitStopDuration\":0,\"showDirectorEnabled\":false,\"combatTempoEnabled\":false," +
            "\"unopposedStreakEnabled\":false,\"bladeExchangeEnabled\":false,\"autoDepthEnabled\":false," +
            "\"attackRhythmEnabled\":false,\"stepAnticipationDuration\":0}";

        [UnityTest]
        public IEnumerator CampaignPressureHoldsTheActualHitAndPose_PausesAndThenResumesExactlyOneImpact()
        {
            yield return null;
            using (var scope = new CampaignScope(1))
            {
                scope.Begin();
                var c = scope.Controller;
                var duel = c.Session;
                LegacyCurrentSlot slot = duel.CurrentSlot;
                var mine = c.ArenaView.PlayerRenderer;
                var theirs = c.ArenaView.EnemyRenderer;
                Sprite playerPose = mine.sprite, enemyPose = theirs.sprite;
                Vector3 playerPosition = mine.transform.localPosition, enemyPosition = theirs.transform.localPosition;
                int act = duel.Act;
                Assert.That(c.CanStep, Is.False, "The pressure presentation is not a timing-input window.");
                scope.Advance(.2f);
                Assert.That(c.Hud.PlayerPressureCharge, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That((duel.Player.Health, duel.Enemy.Health, slot.HitsResolved), Is.EqualTo((1000, 1000, 0)));
                Assert.That(mine.sprite, Is.SameAs(playerPose));
                Assert.That(theirs.sprite, Is.SameAs(enemyPose));
                Assert.That(mine.transform.localPosition, Is.EqualTo(playerPosition));
                Assert.That(theirs.transform.localPosition, Is.EqualTo(enemyPosition));
                Assert.That(duel.Act, Is.EqualTo(act), "Presentation cannot spend or recover ACT.");

                float pausedCharge = c.Hud.PlayerPressureCharge;
                Assert.That(c.PauseBattle(), Is.True);
                scope.Advance(1f);
                Assert.That(c.Hud.PlayerPressureCharge, Is.EqualTo(pausedCharge));
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(c.ResumeBattle(), Is.True);
                scope.Advance(DuelPowerPressure.HoldSeconds - .2f - .02f);
                Assert.That(c.Hud.IsPlayerPowerPressurizing, Is.True, "The lock beat still holds after the gauge fills.");
                Assert.That(slot.HitsResolved, Is.Zero);
                scope.Advance(.03f);
                Assert.That(c.Hud.IsPlayerPowerPressurizing, Is.False);
                Assert.That(slot.HitsResolved, Is.Zero, "Crossing the pressure boundary never batches an attack into its release.");
                scope.Until(() => slot.HitsResolved > 0);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That((duel.Player.Health, duel.Enemy.Health), Is.EqualTo(ExpectedCampaign(1, 1)),
                    "The first real contact must match one ordinary rules hit, not a pressure-induced extra hit.");
            }
        }

        [UnityTest]
        public IEnumerator MultiHitChargesOnce_AndEveryFollowingContactUsesTheExistingAttackClock()
        {
            yield return null;
            using (var scope = new CampaignScope(3))
            {
                scope.Begin();
                var c = scope.Controller;
                LegacyCurrentSlot slot = c.Session.CurrentSlot;
                scope.Advance(DuelPowerPressure.HoldSeconds + .01f);
                Assert.That(c.Hud.IsPlayerPowerPressurizing, Is.False);
                Assert.That(slot.HitsResolved, Is.Zero);
                scope.Until(() => slot.HitsResolved == 1);
                int frames = 0;
                while (!slot.IsResolved && frames++ < 10000)
                {
                    Assert.That(c.Hud.IsPlayerPowerPressurizing, Is.False, "A later hit of this same technique cannot recharge.");
                    scope.Advance(Frame);
                }
                Assert.That(slot.HitsResolved, Is.EqualTo(3));
                Assert.That((c.Session.Player.Health, c.Session.Enemy.Health), Is.EqualTo(ExpectedCampaign(3, 3)));
            }
        }

        [UnityTest]
        public IEnumerator HostAndGuestViewsHoldTheirOwnPressure_ThenResolveTheSameSingleHit()
        {
            yield return null;
            foreach (int viewer in new[] { 0, 1 })
                using (var scope = new VersusScope(viewer))
                {
                    scope.Begin();
                    var c = scope.Controller;
                    LocalVersusSlot slot = c.Match.CurrentSlot;
                    int left = c.Match.Left.State.Health, right = c.Match.Right.State.Health;
                    Sprite playerPose = scope.Arena.PlayerRenderer.sprite, enemyPose = scope.Arena.EnemyRenderer.sprite;
                    c.Tick(.2f, null);
                    Assert.That(c.BattleHud.IsPlayerPowerPressurizing, Is.True);
                    Assert.That(slot.HitsResolved, Is.Zero);
                    Assert.That((c.Match.Left.State.Health, c.Match.Right.State.Health), Is.EqualTo((left, right)));
                    Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(playerPose));
                    Assert.That(scope.Arena.EnemyRenderer.sprite, Is.SameAs(enemyPose));
                    c.Tick(DuelPowerPressure.HoldSeconds - .2f - .02f, null);
                    Assert.That(c.BattleHud.IsPlayerPowerPressurizing, Is.True);
                    c.Tick(.03f, null);
                    Assert.That(c.BattleHud.IsPlayerPowerPressurizing, Is.False);
                    Assert.That(slot.HitsResolved, Is.Zero);
                    scope.Until(() => slot.HitsResolved > 0);
                    Assert.That(slot.HitsResolved, Is.EqualTo(1));
                    Assert.That((c.Match.Left.State.Health, c.Match.Right.State.Health), Is.EqualTo(ExpectedVersus()),
                        "Both perspectives must resolve one identical authoritative contact.");
                }
        }

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "압력 시계 시험", 1, power, power, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, hits, 0, string.Empty, iconId: 1);

        private static LegacyQueuedDuel CampaignDuel(int hits)
            => new LegacyQueuedDuel(1000, 0, 1000, 0, new[] { Attack(99841, 6, hits) },
                new[] { Attack(99842, 4) }, new[] { 1 }, randomSeed: 7, meshPercent: 0, usePowerBreak: true);

        private static (int, int) ExpectedCampaign(int hits, int resolved)
        {
            LegacyQueuedDuel duel = CampaignDuel(hits);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            for (int hit = 0; hit < resolved; hit++) duel.ResolveNextHit();
            return (duel.Player.Health, duel.Enemy.Health);
        }

        private static (int, int) ExpectedVersus()
        {
            var match = new LocalVersusMatch(new[] { Attack(99851, 6) }, new[] { Attack(99852, 4) },
                randomSeed: 7, usePowerBreak: true);
            foreach (int player in new[] { 0, 1 })
            {
                Assert.That(match.TryQueueLane(player, 0), Is.True);
                Assert.That(match.TryCommit(player), Is.True);
            }
            match.BeginNextSlot();
            match.ResolveNextHit();
            return (match.Left.State.Health, match.Right.State.Health);
        }

        private sealed class CampaignScope : IDisposable
        {
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public CampaignScope(int hits)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", Private);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite(Tuning, settings);
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", CampaignDuel(hits));
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private)
                        .Invoke(Controller, new object[] { null, null });
                    Controller.ArenaView.SetEnemyAppearance(EnemyAppearance.Elisa);
                    Advance(0f);
                }
                catch { Dispose(); throw; }
            }
            public void Begin()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Until(() => Controller.Hud.IsPlayerPowerPressurizing);
                Assert.That(Controller.Session.CurrentSlot, Is.Not.Null);
            }
            public void Advance(float seconds) => advance(seconds, null);
            public void Until(Func<bool> done)
            {
                for (int frame = 0; frame < 20000 && !done(); frame++) Advance(Frame);
                Assert.That(done(), Is.True, "The real campaign controller must progress through its pressure presentation.");
            }
            private void Set(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
            public void Dispose()
            {
                if (Controller.IsPaused) Controller.ResumeBattle();
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settings);
            }
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly int viewer;
            public LegacyArenaView Arena { get; }
            public LocalVersusController Controller { get; }
            public VersusScope(int viewer)
            {
                this.viewer = viewer;
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Power pressure versus " + viewer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite(Tuning, settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Controller = new LocalVersusController(root.transform, art, Arena, () => { }, settings);
                Controller.StartOnline(7, viewer, viewer == 0, 0, new[] { Attack(99851, 6) },
                    new[] { Attack(99852, 4) }, (action, lane) => { }, () => { });
            }
            public void Begin()
            {
                if (viewer == 0)
                {
                    Assert.That(Controller.TryQueueLane(0), Is.True);
                    Assert.That(Controller.TryCommit(), Is.True);
                    Assert.That(Controller.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                    Assert.That(Controller.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                }
                else foreach (int owner in new[] { 0, 1 })
                    {
                        Assert.That(Controller.ApplyOnlineAcceptedAction(owner, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                        Assert.That(Controller.ApplyOnlineAcceptedAction(owner, OnlineVersusAction.Pass, -1, 1), Is.True);
                    }
                Until(() => Controller.BattleHud.IsPlayerPowerPressurizing);
                Assert.That(Controller.Match.CurrentSlot, Is.Not.Null);
            }
            public void Until(Func<bool> done)
            {
                for (int frame = 0; frame < 20000 && !done(); frame++) Controller.Tick(Frame, null);
                Assert.That(done(), Is.True, "The real versus controller must progress through its pressure presentation.");
            }
            public void Dispose()
            {
                Controller.Dispose();
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
