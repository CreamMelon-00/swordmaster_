using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The presentation director in the campaign's battles (2026-10-15, <see cref="LegacyShowDirector"/>): a turn
    /// never runs longer than the cap (and never at all longer with the cap at 0) and never stalls, on or off; the step
    /// windows keep their time while a Major slot pushes the camera from its windup and stops longer at its first hit; a
    /// Minor slot's rest and the wait after it are shorter; the finishing blow keeps its finale; an even 합 holds the two
    /// fighters in a blade bind on its own real time (the battle's clock stands still) that sways them together as far one
    /// way as the other, springs them apart alike and leaves them where it found them; and a reset in the middle of a bind
    /// leaves nothing of it.</summary>
    public sealed class ShowDirectorPlayModeTests : InputTestFixture
    {
        // Small frames, so a phase's last frame overshoots its end by little: a turn's length compares within a few frames.
        private const float Step = 1f / 500f, Tolerance = .06f, Cap = .8f, HitStop = .04f;
        private const int MaximumSteps = 40000;
        private const string Off = "\"showDirectorEnabled\":false";

        [UnityTest]
        public IEnumerator ATurn_NeverRunsLongerThanTheCap_NorStalls_OnOrOff()
        {
            yield return null;
            // Two even 합 (binds), two large gaps (Major) and a guard against a guard (Minor).
            float off = ResolveTurn(Off, out float offNet);
            float on = ResolveTurn(null, out float onNet);
            float strict = ResolveTurn("\"turnExtraSecondsCap\":0", out float strictNet);
            Assert.That(offNet, Is.Zero, "Switched off, the director adds and saves nothing.");
            Assert.That(onNet, Is.LessThanOrEqualTo(Cap + 1e-4f), "The ledger keeps to the cap…");
            Assert.That(on - off, Is.LessThanOrEqualTo(Cap + Tolerance), "…and so does the turn on screen.");
            Assert.That(on - off, Is.GreaterThan(.4f), "The binds and the longer stops did play.");
            Assert.That(on - off, Is.EqualTo(onNet).Within(Tolerance), "The turn grew by what the ledger says.");
            Assert.That(strictNet, Is.LessThanOrEqualTo(1e-4f), "With no cap only what Minor slots saved is spent…");
            Assert.That(strict - off, Is.LessThanOrEqualTo(Tolerance), "…so the turn is never longer than without it.");
        }

        [UnityTest]
        public IEnumerator AMajorSlot_KeepsItsStepWindow_PushesTheCameraFromItsWindup_AndStopsLongerAtItsFirstHit()
        {
            yield return null;
            StepTimes off = MajorSlotTimes(Off, out float offStop, out float offPush, out float offEmphasis);
            StepTimes on = MajorSlotTimes(null, out float onStop, out float onPush, out float onEmphasis);
            Assert.That(on.WindowOpens, Is.EqualTo(off.WindowOpens).Within(Step * 1.5f), "The window opens as ever…");
            Assert.That(on.WindowCloses, Is.EqualTo(off.WindowCloses).Within(Step * 1.5f), "…closes as ever…");
            Assert.That(on.FirstHit, Is.EqualTo(off.FirstHit).Within(Step * 1.5f), "…and the hit lands as ever.");
            Assert.That(offPush, Is.Zero);
            Assert.That(onPush, Is.GreaterThan(0f), "The camera comes in during the windup of a slot known to be Major.");
            Assert.That(offStop, Is.EqualTo(HitStop).Within(1e-4f));
            Assert.That(onStop, Is.EqualTo(HitStop * 3f).Within(1e-4f), "Its first hit stops three times as long.");
            Assert.That(offEmphasis, Is.EqualTo(1f));
            Assert.That(onEmphasis, Is.EqualTo(1.35f).Within(1e-4f), "The seam's pair goes farther.");
        }

        [UnityTest]
        public IEnumerator AMinorSlot_KeepsItsWindup_ButItsRestAndTheWaitAfterItAreShorter()
        {
            yield return null;
            MinorTimes off = MinorSlotTimes(Off);
            MinorTimes on = MinorSlotTimes(null);
            Assert.That(on.FirstHit, Is.EqualTo(off.FirstHit).Within(Step * 1.5f), "Up to its first hit nothing changes.");
            float rest = off.Duration - off.FirstHitSlotTime;
            Assert.That(on.Duration, Is.EqualTo(on.FirstHitSlotTime + rest / 2f).Within(Step * 1.5f), "Its rest is half as long…");
            Assert.That(on.Gap, Is.EqualTo(off.Gap / 2f).Within(Step * 2f), "…and so is the wait after it.");
            Assert.That(on.Saved, Is.EqualTo(rest / 2f + off.Gap / 2f).Within(Step * 2f), "What it saved is banked.");
        }

        [UnityTest]
        public IEnumerator TheFinishingBlow_KeepsItsFinale_EvenFromAMajorSlot()
        {
            yield return null;
            var player = new[] { Attack(990, 30, 0) };
            var enemy = new[] { Guard(995, 1, 0) };
            using (var scope = new DirectorScope(new LegacyQueuedDuel(1000, 1000, 10, 1000, player, enemy, new[] { 1 }, 1,
                meshPercent: 0)))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                Assert.That(controller.ShowDirector.IsMajor, Is.True, "30 through a guard of 1: a deep pierce.");
                scope.Until(() => controller.Session.IsFinished);
                Assert.That(controller.Finale.IsRunning, Is.True);
                Assert.That(controller.ShowDirector.LastSlotRank.Tier, Is.EqualTo(LegacySlotTier.Climax));
                Assert.That(controller.HitStopTimeRemaining, Is.EqualTo(HitStop).Within(1e-4f), "The finale's hit stop, as ever.");
                Assert.That(controller.BladeBindTimeRemaining, Is.Zero);
                scope.Until(() => controller.Finale.IsFrozen || !controller.Finale.IsRunning);
                Assert.That(controller.ArenaView.IsFinishingFocus || !controller.Finale.IsRunning, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator AnEvenClash_HoldsABladeBind_ThatSwaysBothAlike_AndLeavesThemWhereItFoundThem()
        {
            yield return null;
            var player = new[] { Attack(990, 10, 0) };
            var enemy = new[] { Attack(993, 10, 0) };
            using (var scope = new DirectorScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { 1 }, 1,
                meshPercent: 0)))
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Transform playerFigure = arena.PlayerRenderer.transform, enemyFigure = arena.EnemyRenderer.transform;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.BladeBindTimeRemaining > 0f);
                float seconds = scope.Settings.BladeBindSeconds;
                Assert.That(controller.BladeBindTimeRemaining, Is.EqualTo(seconds).Within(1e-4f), "10 against 10: a bind.");
                Assert.That(arena.IsBladeBinding, Is.True);
                Assert.That(controller.Hud.IsClashBound && controller.Hud.IsClashBinding, Is.True, "The seam's pair binds too.");
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.Zero, "The first impact waits until the swords release.");
                Assert.That(controller.Session.Player.Health, Is.EqualTo(1000), "Both blades met: no body was reached.");
                Assert.That((controller.Session.Player.Resistance, controller.Session.Enemy.Resistance), Is.EqualTo((1000, 1000)));
                Vector3 playerAt = playerFigure.localPosition, enemyAt = enemyFigure.localPosition;
                float slotTime = controller.ActiveSlotElapsedTime;

                // The contact's own stop comes first; the unresolved hit waits through the bind.
                scope.Advance(controller.HitStopTimeRemaining);
                Assert.That(controller.BladeBindTimeRemaining, Is.EqualTo(seconds).Within(1e-4f));
                Assert.That(arena.BladeBindProgress, Is.Zero);
                float most = 0f, least = 0f;
                while (controller.BladeBindTimeRemaining > .011f)
                {
                    scope.Advance(.01f);
                    Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime), "The battle's clock stands still…");
                    float p = arena.PlayerBladeBindOffset, e = arena.EnemyBladeBindOffset;
                    Assert.That(p - e, Is.GreaterThanOrEqualTo(-1e-5f), "…both lean into each other alike…");
                    Assert.That(playerFigure.localPosition.x, Is.EqualTo(playerAt.x + p).Within(1e-4f));
                    Assert.That(enemyFigure.localPosition.x, Is.EqualTo(enemyAt.x + e).Within(1e-4f));
                    most = Mathf.Max(most, (p + e) * .5f);
                    least = Mathf.Min(least, (p + e) * .5f);
                    Assert.That(controller.Hud.IsClashBinding, Is.True);
                }
                Assert.That(most, Is.GreaterThan(0f), "…sway one way…");
                Assert.That(least, Is.LessThan(0f), "…and the other…");
                Assert.That(most, Is.EqualTo(-least).Within(scope.Settings.BladeBindSway * .2f), "…as far.");

                // The bind's last moment, with no time left over for the battle's clock.
                scope.Advance(controller.BladeBindTimeRemaining);
                Assert.That(controller.BladeBindTimeRemaining, Is.Zero);
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.EqualTo(1), "The deferred impact resolves on release.");
                Assert.That((controller.Session.Player.Resistance, controller.Session.Enemy.Resistance), Is.EqualTo((990, 990)));
                Assert.That(arena.IsBladeBinding || controller.Hud.IsClashBinding, Is.False);
                Assert.That(controller.Hud.IsClashBound, Is.True, "The pair goes on as an even 합: both bounce back.");
                Assert.That(arena.BladeBindSparksStruck, Is.EqualTo(scope.Settings.BladeBindSparks));
                Assert.That((arena.PlayerBladeBindOffset, arena.EnemyBladeBindOffset), Is.EqualTo((0f, 0f)));
                Assert.That(playerFigure.localPosition.x, Is.EqualTo(playerAt.x).Within(1e-4f), "Where it found them.");
                Assert.That(enemyFigure.localPosition.x, Is.EqualTo(enemyAt.x).Within(1e-4f));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime));
                float playerSpring = playerAt.x - arena.PlayerKnockbackTarget.x, enemySpring = arena.EnemyKnockbackTarget.x - enemyAt.x;
                Assert.That(playerSpring, Is.Zero.Within(1e-4f), "A slash bind releases the pose without pushing either body.");
                Assert.That(enemySpring, Is.Zero.Within(1e-4f));

                scope.Until(() => controller.CanChoose);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2), "The turn ends.");
                Assert.That(arena.IsBladeBinding, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator AResetInTheMiddleOfABind_LeavesNothingOfIt()
        {
            yield return null;
            var player = new[] { Attack(990, 10, 0) };
            var enemy = new[] { Attack(993, 9, 0) };
            using (var scope = new DirectorScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { 1 }, 1,
                meshPercent: 0)))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.BladeBindTimeRemaining > 0f);
                scope.Advance(controller.HitStopTimeRemaining + .1f);
                Assert.That(controller.ArenaView.IsBladeBinding, Is.True, "10 against 9: a bind, a little way in.");
                controller.RestartMatch();
                Assert.That(controller.BladeBindTimeRemaining, Is.Zero);
                Assert.That(controller.ArenaView.IsBladeBinding, Is.False);
                Assert.That((controller.ArenaView.PlayerBladeBindOffset, controller.ArenaView.EnemyBladeBindOffset),
                    Is.EqualTo((0f, 0f)));
                Assert.That(controller.Hud.IsClashBound || controller.Hud.IsClashBinding, Is.False);
                Assert.That(controller.ShowDirector.HasSlot, Is.False);
                Assert.That((controller.ShowDirector.Saved, controller.ShowDirector.Spent), Is.EqualTo((0f, 0f)));
                Assert.That(controller.ArenaView.DirectorPushAmount, Is.Zero);
                Assert.That(controller.CanChoose, Is.True);
            }
        }

        // One turn of the scenario (two even 합, two large gaps, a guard against a guard): its real seconds from the commit
        // to the next planning turn, and the director's net added time.
        private static float ResolveTurn(string tuning, out float netExtra)
        {
            var player = new[] { Attack(990, 10, 0), Attack(991, 30, 1), Guard(992, 5, 2) };
            var enemy = new[] { Attack(993, 10, 0), Attack(994, 10, 0), Guard(995, 5, 0), Attack(996, 10, 0), Attack(997, 10, 0) };
            using (var scope = new DirectorScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { 5 }, 1,
                meshPercent: 0), tuning))
            {
                DuelPrototypeController controller = scope.Controller;
                foreach (int lane in new[] { 0, 1, 2, 0, 1 }) Assert.That(controller.QueueLane(lane), Is.True);
                controller.CommitTurn();
                float elapsed = scope.Until(() => controller.CanChoose);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2), "The turn ends: no stall.");
                Assert.That(controller.HitStopTimeRemaining + controller.BladeBindTimeRemaining, Is.Zero);
                Assert.That(controller.ArenaView.IsBladeBinding, Is.False);
                netExtra = controller.ShowDirector.NetExtra;
                return elapsed;
            }
        }

        private struct StepTimes
        {
            public float WindowOpens, WindowCloses, FirstHit;
        }

        // A slot rated Major from its start (30 against 10): from the slot's start, when the step window opens and closes and
        // when its first hit lands; the stop that hit got, the camera's push before it and the seam pair's weight.
        private static StepTimes MajorSlotTimes(string tuning, out float hitStop, out float push, out float emphasis)
        {
            var player = new[] { Attack(990, 30, 0) };
            var enemy = new[] { Attack(993, 10, 0) };
            var times = new StepTimes { WindowOpens = -1f, WindowCloses = -1f, FirstHit = -1f };
            using (var scope = new DirectorScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { 1 }, 1,
                meshPercent: 0), tuning))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(controller.IsSkillWindup, Is.True, "The steps' warning.");
                float elapsed = 0f;
                push = 0f;
                for (int step = 0; step < MaximumSteps && slot.HitsResolved == 0; step++)
                {
                    scope.Advance(Step);
                    elapsed += Step;
                    if (slot.HitsResolved > 0) break;
                    push = Mathf.Max(push, controller.ArenaView.DirectorPushAmount);
                    bool window = controller.IsStepTimingWindow;
                    if (window && times.WindowOpens < 0f) times.WindowOpens = elapsed;
                    if (!window && times.WindowOpens >= 0f && times.WindowCloses < 0f) times.WindowCloses = elapsed;
                }
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                times.FirstHit = elapsed;
                if (times.WindowCloses < 0f) times.WindowCloses = elapsed;
                Assert.That(times.WindowOpens, Is.GreaterThan(0f), "The window opened before the hit.");
                hitStop = controller.HitStopTimeRemaining;
                emphasis = controller.Hud.ClashEmphasis;
                Assert.That(controller.BladeBindTimeRemaining, Is.Zero, "A gap of two thirds is no bind.");
                scope.Until(() => controller.CanChoose);
            }
            return times;
        }

        private struct MinorTimes
        {
            public float FirstHit, FirstHitSlotTime, Duration, Gap, Saved;
        }

        // A guard against a guard (Minor), then a plain 합 (10 against 8: Normal, no bind): when the Minor slot's hit lands
        // after its start, its length as the controller then holds it, the wait after it, and what the director banked.
        private static MinorTimes MinorSlotTimes(string tuning)
        {
            var player = new[] { Guard(992, 5, 0), Attack(991, 10, 1) };
            var enemy = new[] { Guard(995, 5, 0), Attack(994, 8, 0) };
            var times = new MinorTimes();
            using (var scope = new DirectorScope(new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { 2 }, 1,
                meshPercent: 0), tuning))
            {
                DuelPrototypeController controller = scope.Controller;
                Assert.That(controller.QueueLane(0) && controller.QueueLane(1), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                times.FirstHit = scope.Until(() => slot.HitsResolved > 0);
                times.FirstHitSlotTime = controller.ActiveSlotElapsedTime;
                times.Duration = controller.ActiveSlotDuration;
                scope.Until(() => controller.IsBetweenSlots);
                times.Gap = scope.Until(() => !controller.IsBetweenSlots);
                times.Saved = controller.ShowDirector.Saved;
                Assert.That(controller.ShowDirector.LastSlotRank.Tier, Is.EqualTo(LegacySlotTier.Minor));
                scope.Until(() => controller.CanChoose);
            }
            return times;
        }

        private static LegacySkill Attack(int id, int power, int lane)
            => new LegacySkill(id, "감독 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, "검증");

        private static LegacySkill Guard(int id, int power, int lane)
            => new LegacySkill(id, "감독 방어", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, lane, "검증");

        /// <summary>The real controller on a given duel, driven with chosen real seconds, on a copy of the project's
        /// presentation settings with fixed tempo and the director's defaults, and <c>tuning</c> (JSON fields) over them.</summary>
        private sealed class DirectorScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private const string Tempo = "\"animationPlaybackSpeed\":1,\"attackInterval\":0.1,\"skillInterval\":0.28," +
                "\"hitStopDuration\":0.04,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1," +
                "\"decisiveHealthDamagePercent\":25,\"showDirectorEnabled\":true,\"combatTempoEnabled\":false,\"minorSlotSpeed\":2,\"majorHitStopScale\":3," +
                "\"majorCameraPush\":0.1,\"majorClashScale\":1.35,\"turnExtraSecondsCap\":0.8,\"largeClashGap\":0.6," +
                "\"deepPierce\":0.7,\"bladeBindGap\":0.15,\"bladeBindSeconds\":0.45,\"bladeBindSway\":0.16," +
                "\"bladeBindPress\":0.1,\"bladeBindSpring\":0,\"bladeBindSparks\":2,\"clashMotionEnabled\":true,\"bladeExchangeEnabled\":false";
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }

            public DirectorScope(LegacyQueuedDuel duel, string tuning = null)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                Settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{" + Tempo + "}", Settings);
                    if (!string.IsNullOrEmpty(tuning)) JsonUtility.FromJsonOverwrite("{" + tuning + "}", Settings);
                    Set("presentationSettings", Settings);
                    arenaSettings.SetValue(Controller.ArenaView, Settings);
                    Set("mission", null);
                    Set("guide", null);
                    Set("session", duel);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch
                {
                    // A failed setup never reaches Dispose: restore the shared controller here.
                    try { Dispose(); } catch { Controller.enabled = originalEnabled; }
                    throw;
                }
            }

            public void Advance(float realDelta) => advance(realDelta, null);

            /// <summary>Small frames until <paramref name="condition"/> holds; returns the real seconds it took.</summary>
            public float Until(Func<bool> condition)
            {
                float elapsed = 0f;
                for (int step = 0; step < MaximumSteps && !condition(); step++)
                {
                    Advance(Step);
                    elapsed += Step;
                }
                Assert.That(condition(), Is.True, "The presentation must make progress (no stall).");
                return elapsed;
            }

            private void Set(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null, name);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(Settings);
            }
        }
    }
}
