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
    public sealed class PressureAttackTrailPlayModeTests
    {
        [UnityTest]
        public IEnumerator PressureAfterDash_ContinuesUsingCurrentAttackSprite()
        {
            using (var fixture = new ArenaScope())
            {
                fixture.Begin(Attack(3), Attack(3), .5f, .1f);
                Sprite movementPose = fixture.Arena.PlayerRenderer.sprite;
                fixture.Arena.PerformStep(LegacyStepAction.Pressure, false);
                fixture.FinishDash();
                Assert.That(fixture.Arena.IsStepping, Is.False);
                for (int frame = 0; frame < 20; frame++) fixture.Advance(.01f, .015f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.GreaterThan(0),
                    "Pressure must continue after the dash and all of its original snapshots have expired.");
                SpriteRenderer source = fixture.Arena.PlayerRenderer;
                Assert.That(source.sprite.name, Is.EqualTo("slash-1-upper-3"));
                Assert.That(source.sprite, Is.Not.SameAs(movementPose));
                fixture.Advance(0f, .04f);
                bool currentPoseFound = false;
                foreach (SpriteRenderer ghost in fixture.Views)
                {
                    if (!ghost.gameObject.activeSelf || ghost.color.a < DuelStepAfterimages.InitialAlpha - .001f) continue;
                    Assert.That(ghost.sprite, Is.SameAs(source.sprite), "New ghosts must snapshot the displayed sword pose.");
                    Assert.That(ghost.sharedMaterial, Is.SameAs(source.sharedMaterial));
                    Assert.That(ghost.sortingOrder, Is.LessThan(source.sortingOrder));
                    Assert.That(ghost.transform.position, Is.EqualTo(source.transform.position));
                    currentPoseFound = true;
                }
                Assert.That(currentPoseFound, Is.True);
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.True,
                    "An accepted mistimed pressure still has its movement feedback; success remains a separate rule.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MultiHitGaps_PauseEmission_ThenResumeUntilOwnFinalClip()
        {
            using (var fixture = new ArenaScope())
            {
                fixture.Begin(Attack(3), Attack(3), 1f, .3f);
                fixture.Arena.PerformStep(LegacyStepAction.Pressure, true);
                fixture.FinishDash();
                fixture.Hold(.05f);
                fixture.Advance(0f, .04f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.GreaterThan(0));
                fixture.Hold(.18f);
                Assert.That(fixture.Arena.PlayerRenderer.sprite.name, Does.StartWith("idle-upper-"));
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero,
                    "Inter-hit idle must not leave an endlessly refreshed stationary ghost.");
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.True,
                    "The same skill retains its armed feedback for the next strike.");
                fixture.Hold(.5f);
                fixture.Advance(0f, .04f);
                Assert.That(fixture.Arena.PlayerRenderer.sprite.name, Is.EqualTo("slash-2-upper-1"));
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.EqualTo(1));
                float lastClipEnd = LegacyArenaView.OriginalClipDuration * 3f + .3f * 2f;
                fixture.Hold(lastClipEnd + .0001f);
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WindupAndSlowPlayback_RespectAnimationClockAndOwnSkillEnd()
        {
            using (var fixture = new ArenaScope())
            {
                fixture.Begin(Attack(1), Attack(3), .5f, .1f);
                fixture.Arena.PerformStep(LegacyStepAction.Pressure, true);
                fixture.FinishDash();
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f, false);
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.True);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero,
                    "Windup can arm the trail, but must not keep stamping an animation that has not started.");
                fixture.Hold(.28f);
                fixture.Advance(0f, .04f);
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.True,
                    "A slowed clip must retain feedback beyond the unscaled original one-sixth-second duration.");
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.EqualTo(1));
                fixture.Hold(.34f);
                fixture.Advance(0f, .04f);
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False,
                    "The opponent's longer three-hit skill must not extend the player's finished one-hit trail.");
                fixture.Hold(.5f);
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DodgeAndGuardPressure_KeepShortMovementOnly()
        {
            using (var fixture = new ArenaScope())
            {
                fixture.Begin(Attack(3), Attack(3), .5f, .1f);
                fixture.Arena.PerformStep(LegacyStepAction.Dodge, true);
                fixture.FinishDash();
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.EqualTo(3));
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);

                fixture.Begin(LegacyInitialSkills.All[6], Attack(3), .5f, .1f);
                fixture.Arena.PerformStep(LegacyStepAction.Pressure, true);
                fixture.FinishDash();
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.EqualTo(3));
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NewSlotAndTurnTransitions_ResetPressureTrailState()
        {
            using (var fixture = new ArenaScope())
            {
                fixture.Begin(Attack(3), Attack(3));
                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.FinishDash();
                fixture.Begin(Attack(3), Attack(3));
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);

                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.Arena.EndTurn();
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                fixture.Advance(0f, DuelStepAfterimages.Lifetime + .02f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);

                fixture.Arena.BeginTurn();
                fixture.Begin(Attack(3), Attack(3));
                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.Arena.BeginTurn();
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                fixture.Begin(Attack(3), Attack(3));
                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.Arena.Reset();
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
                fixture.Begin(Attack(3), Attack(3));
                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.Arena.Dispose();
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
                fixture.Advance(0f, 1f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedPressureAndLongFrames_ReuseBoundedPool()
        {
            using (var fixture = new ArenaScope())
            {
                fixture.Begin(Attack(3), Attack(3), .5f, .1f);
                SpriteRenderer[] prewarmed = fixture.Views;
                Assert.That(prewarmed, Has.Length.EqualTo(DuelStepAfterimages.Capacity));
                fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                fixture.FinishDash();
                fixture.Advance(0f, 1f);
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.EqualTo(1),
                    "A hitch must produce one fresh pose, not a catch-up burst of identical endpoint ghosts.");
                for (int index = 0; index < 50; index++)
                {
                    fixture.Arena.PerformStep(LegacyStepAction.Pressure);
                    fixture.Advance(0f, LegacyArenaView.StepDuration);
                    Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.LessThanOrEqualTo(DuelStepAfterimages.Capacity));
                }
                Assert.That(fixture.Views, Is.EqualTo(prewarmed));
                fixture.Arena.Reset();
                Assert.That(fixture.Arena.ActiveStepAfterimageCount, Is.Zero);
                Assert.That(fixture.Arena.IsPressureAttackTrailActive, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ControllerPressure_EmitsDuringLaterAttackAfterMovementEnds()
        {
            yield return null;
            using (var fixture = new ControllerScope())
            {
                fixture.BeginPlayingSlot();
                DuelPrototypeController controller = fixture.Controller;
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool success), Is.True);
                Assert.That(success, Is.True);
                float realElapsed = 0f;
                int frames = 0;
                while (controller.Session.CurrentSlot != null && controller.ActiveSlotElapsedTime < .29f && frames++ < 4000)
                {
                    fixture.Advance(.001f);
                    realElapsed += .001f;
                }
                Assert.That(controller.Session.CurrentSlot, Is.Not.Null);
                Assert.That(controller.ActiveSlotElapsedTime, Is.GreaterThanOrEqualTo(.29f));
                Assert.That(realElapsed, Is.GreaterThan(LegacyArenaView.StepDuration + DuelStepAfterimages.Lifetime));
                Assert.That(controller.ArenaView.IsStepping, Is.False);
                Assert.That(controller.ArenaView.IsPressureAttackTrailActive, Is.True);
                Assert.That(controller.ArenaView.PlayerRenderer.sprite.name, Does.StartWith("slash-"));
                Assert.That(controller.ArenaView.ActiveStepAfterimageCount, Is.GreaterThan(0),
                    "The real controller must keep sampling after its contact/animation holds, not only during the dash.");
                controller.ReturnToLobby();
                Assert.That(controller.ArenaView.IsPressureAttackTrailActive, Is.False);
                Assert.That(controller.ArenaView.ActiveStepAfterimageCount, Is.Zero);
            }
        }

        private static LegacySkill Attack(int count, int id = 101, int lane = 0) =>
            new LegacySkill(id, "Pressure Trail Test", 1, 1, 1, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, count, lane, string.Empty, "Slash", 1);

        private sealed class ArenaScope : IDisposable
        {
            private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly GameObject host;
            private readonly Action<float, float> configure;
            private readonly Action<float> hold;
            private readonly Action<float, bool> tickTrail;
            private readonly Transform trailRoot;
            public LegacyArenaView Arena { get; }
            public SpriteRenderer[] Views
            {
                get
                {
                    // One pooled ghost now contains upper/lower renderers; pool capacity counts actors.
                    var views = new SpriteRenderer[trailRoot.childCount];
                    for (int index = 0; index < views.Length; index++)
                        views[index] = trailRoot.GetChild(index).GetComponent<SpriteRenderer>();
                    return views;
                }
            }

            public ArenaScope()
            {
                host = new GameObject("Pressure Attack Trail Test Host");
                Arena = LegacyArenaView.Create(host.transform, new LegacyDuelArt());
                Arena.CloseDistance(1f);
                configure = Bind<Action<float, float>>("ConfigureSlotTiming");
                hold = Bind<Action<float>>("HoldSlotAtTime");
                tickTrail = Bind<Action<float, bool>>("TickPressureAttackTrail");
                trailRoot = host.transform.Find("Legacy Duel Arena/Duel Step Afterimages");
                Assert.That(trailRoot, Is.Not.Null);
            }

            public void Begin(LegacySkill player, LegacySkill enemy, float speed = 1f, float interval = .1f)
            {
                configure(speed, interval);
                Arena.BeginSlot(player, enemy);
            }

            public void Advance(float animationDelta, float realDelta, bool playing = true)
            {
                Arena.Tick(animationDelta, realDelta);
                tickTrail(realDelta, playing);
            }

            public void Hold(float elapsed) => hold(elapsed);
            public void FinishDash() => Advance(0f, LegacyArenaView.StepDuration, false);

            private T Bind<T>(string name) where T : Delegate
            {
                MethodInfo method = typeof(LegacyArenaView).GetMethod(name, NonPublic);
                Assert.That(method, Is.Not.Null);
                return (T)Delegate.CreateDelegate(typeof(T), Arena, method);
            }

            public void Dispose()
            {
                Arena.Dispose();
                Object.Destroy(host);
            }
        }

        private sealed class ControllerScope : IDisposable
        {
            private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly bool originalEnabled;
            private readonly DuelPresentationSettings originalSettings, settingsClone;
            private readonly LegacyQueuedDuel originalSession;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public ControllerScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                settingsClone = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1," +
                    "\"hitStopDuration\":0,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1}", settingsClone);
                Set("presentationSettings", settingsClone);
                Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { Attack(3), Attack(1, 103, 1), Attack(1, 105, 2) },
                    new[] { Attack(3, 201) }, new[] { 1 }, 1));
                MethodInfo method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", NonPublic);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", NonPublic).Invoke(Controller, null);
            }

            public void Advance(float realDelta) => advance(realDelta, null);

            public void BeginPlayingSlot()
            {
                Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                int frames = 0;
                while ((Controller.Session.CurrentSlot == null || Controller.IsSkillWindup) && frames++ < 10000) Advance(.001f);
                Assert.That(Controller.Session.CurrentSlot, Is.Not.Null);
                Assert.That(Controller.IsSkillWindup, Is.False);
                Assert.That(Controller.IsStepTimingWindow, Is.True);
            }

            private void Set(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, NonPublic);
                Assert.That(field, Is.Not.Null);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                Set("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settingsClone);
            }
        }
    }
}
