using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class BladeBindDamagePlayModeTests : InputTestFixture
    {
        private const float Frame = .002f;
        private const int MaximumFrames = 20000;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Tuning = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.1," +
            "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.08,\"stepTimingWindow\":0.05," +
            "\"showDirectorEnabled\":true,\"combatTempoEnabled\":false,\"unopposedStreakEnabled\":false," +
            "\"majorHitStopScale\":1,\"turnExtraSecondsCap\":5,\"bladeBindGap\":0.15,\"bladeBindSeconds\":0.2," +
            "\"bladeBindSpring\":0,\"clashMotionEnabled\":true";

        [UnityTest]
        public IEnumerator CampaignBind_HoldsAllDamageAndNumbersUntilRelease_ResolvesOnce_AndContinuesMultihits()
        {
            yield return null;
            LegacyQueuedDuel expected = Duel(30, 27, 3);
            expected.TryQueueLane(0);
            expected.Commit();
            expected.BeginNextSlot();
            expected.ResolveNextHit();
            var afterFirst = State(expected);
            while (!expected.IsCurrentSlotResolved) expected.ResolveNextHit();
            var afterAll = State(expected);

            using (var scope = new CampaignScope(Duel(30, 27, 3)))
            {
                DuelPrototypeController controller = scope.Controller;
                var untouched = State(controller.Session);
                scope.BeginBind();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                AssertHeld(controller, untouched);
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.False, "The contact cannot become a late dodge while damage waits.");
                scope.Advance(controller.HitStopTimeRemaining + .05f);
                AssertHeld(controller, untouched);
                // A hitch crossing the whole remaining hold cannot also batch the second impact on release.
                scope.Advance(controller.BladeBindTimeRemaining + .5f);
                Assert.That(controller.BladeBindTimeRemaining, Is.Zero);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                Assert.That(controller.HitStopTimeRemaining, Is.Zero, "Release applies the held hit without adding a second contact stop.");
                Assert.That(State(controller.Session), Is.EqualTo(afterFirst));
                Assert.That(DamageNumbers(controller.Hud), Is.EqualTo(2), "Each fighter's first hit is presented once, at release.");
                for (int frame = 0; frame < 5; frame++) scope.Advance(0f);
                Assert.That(slot.HitsResolved, Is.EqualTo(1), "Repeated release-frame updates cannot resolve the deferred hit again.");
                Assert.That(State(controller.Session), Is.EqualTo(afterFirst));
                Assert.That(DamageNumbers(controller.Hud), Is.EqualTo(2));
                scope.Until(() => controller.CanChoose);
                Assert.That(slot.HitsResolved, Is.EqualTo(3), "The remaining two hits continue after the bind.");
                Assert.That(State(controller.Session), Is.EqualTo(afterAll));
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator PausingPreservesThePendingHit_WhileRestartingDiscardsItWithoutDelayedDamage()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(30, 27, 3)))
            {
                DuelPrototypeController controller = scope.Controller;
                var untouched = State(controller.Session);
                scope.BeginBind();
                LegacyCurrentSlot held = controller.Session.CurrentSlot;
                float remaining = controller.BladeBindTimeRemaining;
                Assert.That(controller.PauseBattle(), Is.True);
                scope.Advance(2f);
                Assert.That(controller.BladeBindTimeRemaining, Is.EqualTo(remaining));
                AssertHeld(controller, untouched);
                controller.ResumeBattle();
                scope.Until(() => held.HitsResolved > 0);
                Assert.That(held.HitsResolved, Is.EqualTo(1));
            }

            using (var scope = new CampaignScope(Duel(30, 27, 3)))
            {
                DuelPrototypeController controller = scope.Controller;
                scope.BeginBind();
                LegacyCurrentSlot discarded = controller.Session.CurrentSlot;
                Assert.That(discarded.HitsResolved, Is.Zero);
                controller.RestartMatch();
                var restarted = State(controller.Session);
                Assert.That(controller.BladeBindTimeRemaining, Is.Zero);
                Assert.That(controller.ArenaView.IsBladeBinding, Is.False);
                scope.Advance(1f);
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.CurrentSlot, Is.Null);
                Assert.That(discarded.HitsResolved, Is.Zero, "A reset abandons the deferred action instead of applying it to a later attempt.");
                Assert.That(State(controller.Session), Is.EqualTo(restarted));
                Assert.That(DamageNumbers(controller.Hud), Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator ADisabledBindOrUnequalClash_ResolvesAtContactWithoutDeferringDamage()
        {
            yield return null;
            foreach (bool disabled in new[] { true, false })
            {
                using (var scope = new CampaignScope(Duel(30, disabled ? 27 : 9, 1),
                    disabled ? "\"bladeBindSeconds\":0" : null))
                {
                    DuelPrototypeController controller = scope.Controller;
                    var untouched = State(controller.Session);
                    Assert.That(controller.QueueLane(0), Is.True);
                    controller.CommitTurn();
                    scope.Until(() => controller.Session.CurrentSlot?.HitsResolved > 0);
                    Assert.That(controller.BladeBindTimeRemaining, Is.Zero);
                    Assert.That(controller.ArenaView.IsBladeBinding, Is.False);
                    Assert.That(State(controller.Session), Is.Not.EqualTo(untouched));
                    Assert.That(DamageNumbers(controller.Hud), Is.EqualTo(2));
                }
            }
        }

        [UnityTest]
        public IEnumerator LocalHostAndMirroredGuest_DeferTheSameDamage_ThenReleaseOnceAndFinishTheMultihitSlot()
        {
            yield return null;
            LocalVersusMatch expected = new LocalVersusMatch(new[] { Attack(88211, 30, 3) }, new[] { Attack(88212, 27, 3) }, randomSeed: 7);
            Assert.That(expected.TryQueueLane(0, 0), Is.True);
            Assert.That(expected.TryCommit(0), Is.True);
            Assert.That(expected.TryQueueLane(1, 0), Is.True);
            Assert.That(expected.TryCommit(1), Is.True);
            expected.BeginNextSlot();
            expected.ResolveNextHit();
            var afterFirst = State(expected);
            while (!expected.CurrentSlot.IsResolved) expected.ResolveNextHit();
            var afterAll = State(expected);

            using (var host = new VersusScope(0, true))
            using (var guest = new VersusScope(1, false))
            {
                var untouched = State(host.Versus.Match);
                Assert.That(host.Versus.TryQueueLane(0), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Versus.TryCommit(), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(0, OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(host.Versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(host.Versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                Assert.That(guest.Versus.ApplyOnlineAcceptedAction(1, OnlineVersusAction.Pass, -1, 1), Is.True);
                UntilBoth(host, guest, () => host.Versus.BladeBindTimeRemaining > 0f && guest.Versus.BladeBindTimeRemaining > 0f);
                LocalVersusSlot hostSlot = host.Versus.Match.CurrentSlot, guestSlot = guest.Versus.Match.CurrentSlot;
                Assert.That(host.Versus.BladeBindTimeRemaining, Is.EqualTo(guest.Versus.BladeBindTimeRemaining));
                Assert.That((hostSlot.HitsResolved, guestSlot.HitsResolved), Is.EqualTo((0, 0)));
                Assert.That(State(host.Versus.Match), Is.EqualTo(untouched));
                Assert.That(State(guest.Versus.Match), Is.EqualTo(untouched));
                Assert.That(DamageNumbers(host.Versus.BattleHud) + DamageNumbers(guest.Versus.BattleHud), Is.Zero);
                float releaseFrame = Mathf.Max(host.Versus.HitStopTimeRemaining + host.Versus.BladeBindTimeRemaining,
                    guest.Versus.HitStopTimeRemaining + guest.Versus.BladeBindTimeRemaining) + .5f;
                host.Versus.Tick(releaseFrame, null);
                guest.Versus.Tick(releaseFrame, null);
                Assert.That(host.Versus.BladeBindTimeRemaining + guest.Versus.BladeBindTimeRemaining, Is.Zero);
                Assert.That((hostSlot.HitsResolved, guestSlot.HitsResolved), Is.EqualTo((1, 1)));
                Assert.That(host.Versus.HitStopTimeRemaining + guest.Versus.HitStopTimeRemaining, Is.Zero);
                Assert.That(State(host.Versus.Match), Is.EqualTo(afterFirst));
                Assert.That(State(guest.Versus.Match), Is.EqualTo(afterFirst));
                Assert.That(DamageNumbers(host.Versus.BattleHud), Is.EqualTo(2));
                Assert.That(DamageNumbers(guest.Versus.BattleHud), Is.EqualTo(2));
                host.Versus.Tick(0f, null);
                guest.Versus.Tick(0f, null);
                Assert.That((hostSlot.HitsResolved, guestSlot.HitsResolved), Is.EqualTo((1, 1)));
                UntilBoth(host, guest, () => !host.Versus.IsResolving && !guest.Versus.IsResolving);
                Assert.That((hostSlot.HitsResolved, guestSlot.HitsResolved), Is.EqualTo((3, 3)));
                Assert.That(State(host.Versus.Match), Is.EqualTo(afterAll));
                Assert.That(State(guest.Versus.Match), Is.EqualTo(afterAll));
            }
        }

        [UnityTest]
        public IEnumerator AnEmphasisedResistanceOnlyRelease_KeepsBothAttackPoses_InCampaignAndVersus()
        {
            yield return null;
            using (var scope = new CampaignScope(Duel(15, 15, 1)))
            {
                DuelPrototypeController controller = scope.Controller;
                controller.ArenaView.SetEnemyAppearance(EnemyAppearance.Elisa);
                scope.BeginBind();
                Sprite playerContact = controller.ArenaView.PlayerRenderer.sprite;
                Sprite enemyContact = controller.ArenaView.EnemyRenderer.sprite;
                AssertAttackContact(playerContact);
                AssertAttackContact(enemyContact);
                scope.Advance(controller.HitStopTimeRemaining + controller.BladeBindTimeRemaining + .5f);
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.EqualTo(1));
                Assert.That((controller.PlayerHealth, controller.EnemyHealth), Is.EqualTo((1000, 1000)));
                Assert.That((controller.Session.Player.Resistance, controller.Session.Enemy.Resistance), Is.EqualTo((985, 985)));
                Assert.That(controller.ArenaView.PlayerRenderer.sprite, Is.SameAs(playerContact), "A prominent resistance number must not turn a sword release into a hurt pose.");
                Assert.That(controller.ArenaView.EnemyRenderer.sprite, Is.SameAs(enemyContact));
                Assert.That(controller.HitStopTimeRemaining, Is.Zero);
                Assert.That(DamageNumbers(controller.Hud), Is.EqualTo(2));
            }

            using (var scope = new VersusScope(0, true, 15, 15, 1))
            {
                LocalVersusController versus = scope.Versus;
                Assert.That(versus.TryQueueLane(0), Is.True);
                Assert.That(versus.TryCommit(), Is.True);
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.QueueLane, 0, 1), Is.True);
                Assert.That(versus.TryApplyOnlinePeerAction(OnlineVersusAction.Pass, -1, 1), Is.True);
                for (int frame = 0; frame < MaximumFrames && versus.BladeBindTimeRemaining == 0f; frame++) versus.Tick(Frame, null);
                Assert.That(versus.BladeBindTimeRemaining, Is.GreaterThan(0f));
                Sprite playerContact = scope.Arena.PlayerRenderer.sprite;
                Sprite enemyContact = scope.Arena.EnemyRenderer.sprite;
                AssertAttackContact(playerContact);
                AssertAttackContact(enemyContact);
                versus.Tick(versus.HitStopTimeRemaining + versus.BladeBindTimeRemaining + .5f, null);
                Assert.That(versus.Match.CurrentSlot.HitsResolved, Is.EqualTo(1));
                Assert.That((versus.Match.Left.State.Health, versus.Match.Right.State.Health), Is.EqualTo((100, 100)));
                Assert.That((versus.Match.Left.State.Resistance, versus.Match.Right.State.Resistance), Is.EqualTo((35, 35)));
                Assert.That(scope.Arena.PlayerRenderer.sprite, Is.SameAs(playerContact));
                Assert.That(scope.Arena.EnemyRenderer.sprite, Is.SameAs(enemyContact));
                Assert.That(versus.HitStopTimeRemaining, Is.Zero);
                Assert.That(DamageNumbers(versus.BattleHud), Is.EqualTo(2));
            }
        }

        private static void AssertAttackContact(Sprite sprite)
        {
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.name, Does.Match("^slash(?:-[23])?-frame-05$"));
        }

        private static void AssertHeld(DuelPrototypeController controller, (int, int, int, int) untouched)
        {
            Assert.That(controller.BladeBindTimeRemaining, Is.GreaterThan(0f));
            Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.Zero);
            Assert.That(State(controller.Session), Is.EqualTo(untouched), "Neither health nor resistance changes while the swords are held.");
            Assert.That(DamageNumbers(controller.Hud), Is.Zero);
            Assert.That(controller.CanStep, Is.False);
        }

        private static (int, int, int, int) State(LegacyQueuedDuel duel)
            => (duel.Player.Health, duel.Enemy.Health, duel.Player.Resistance, duel.Enemy.Resistance);
        private static (int, int, int, int) State(LocalVersusMatch match)
            => (match.Left.State.Health, match.Right.State.Health, match.Left.State.Resistance, match.Right.State.Resistance);

        private static int DamageNumbers(LegacyCombatHud hud)
        {
            int count = 0;
            foreach (Text text in hud.Root.GetComponentsInChildren<Text>(true))
                if (text.name == "Damage" && text.gameObject.activeInHierarchy) count++;
            return count;
        }

        private static LegacySkill Attack(int id, int power, int hits) => new LegacySkill(id, "바인드 검증", 0, power, power,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, string.Empty);
        private static LegacyQueuedDuel Duel(int player, int enemy, int hits)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { Attack(88201, player, hits) },
                new[] { Attack(88202, enemy, hits) }, new[] { 1 }, 7, meshPercent: 0);

        private sealed class CampaignScope : IDisposable
        {
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public CampaignScope(LegacyQueuedDuel duel, string extra = null)
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
                    JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                    if (extra != null) JsonUtility.FromJsonOverwrite("{" + extra + "}", settings);
                    Set("presentationSettings", settings);
                    arenaSettings.SetValue(Controller.ArenaView, settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", Private));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", Private).Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch { Dispose(); throw; }
            }

            public void BeginBind()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Until(() => Controller.BladeBindTimeRemaining > 0f);
            }

            public void Advance(float seconds) => advance(seconds, null);
            public void Until(Func<bool> condition)
            {
                for (int frame = 0; frame < MaximumFrames && !condition(); frame++) Advance(Frame);
                Assert.That(condition(), Is.True, "The presentation must make progress.");
            }
            private void Set(string name, object value) => typeof(DuelPrototypeController).GetField(name, Private).SetValue(Controller, value);
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

        private static void UntilBoth(VersusScope host, VersusScope guest, Func<bool> condition)
        {
            for (int frame = 0; frame < MaximumFrames && !condition(); frame++)
            {
                host.Versus.Tick(Frame, null);
                guest.Versus.Tick(Frame, null);
            }
            Assert.That(condition(), Is.True, "Both screens must make progress together.");
        }

        private sealed class VersusScope : IDisposable
        {
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            private readonly LegacyArenaView arena;
            public LocalVersusController Versus { get; }
            public LegacyArenaView Arena => arena;
            public VersusScope(int localPlayer, bool isHost, int playerPower = 30, int enemyPower = 27, int hits = 3)
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                root = new GameObject("Blade Bind Damage Versus " + localPlayer);
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{" + Tuning + "}", settings);
                arena = LegacyArenaView.Create(root.transform, art, settings);
                Versus = new LocalVersusController(root.transform, art, arena, () => { }, settings);
                Versus.StartOnline(7, localPlayer, isHost, 0, new[] { Attack(88211, playerPower, hits) }, new[] { Attack(88212, enemyPower, hits) },
                    (action, lane) => { }, () => { });
            }
            public void Dispose()
            {
                Versus.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(root);
                Object.Destroy(settings);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
