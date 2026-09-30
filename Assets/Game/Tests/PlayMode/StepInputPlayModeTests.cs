using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class StepInputPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator Planning_ADoNotCommitOrStep_SpaceAndEnterCommit()
        {
            yield return null;
            using (var scope = new StepScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var controller = scope.Controller;
                Press(keyboard.aKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.aKey);
                yield return null;
                Press(keyboard.dKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.dKey);
                yield return null;
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.CanStep, Is.False);
                Assert.That(controller.Session.UsedStepThisTurn, Is.False);
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool success), Is.False);
                Assert.That(success, Is.False);

                Press(keyboard.spaceKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.spaceKey);
                yield return null;
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.CanStep, Is.False, "The initial approach is not a skill timing window.");
                Assert.That(controller.Session.CurrentSlot, Is.Null);

                controller.RestartMatch();
                Press(keyboard.enterKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.enterKey);
                Assert.That(controller.IsResolving, Is.True, "Enter remains a confirmation alias.");
            }
        }

        [UnityTest]
        public IEnumerator DodgeKey_EarlyAttemptMisses_TimedAttemptAvoidsTheWholeEnemyMultiHit()
        {
            yield return null;
            using (var scope = new StepScope(enemyHits: 3))
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var controller = scope.Controller;
                scope.BeginWindup();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(controller.CanStep, Is.True);
                Assert.That(controller.IsStepTimingWindow, Is.False);
                Press(keyboard.aKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.aKey);
                Assert.That(slot.DodgeSucceeded, Is.False);
                Assert.That(controller.Session.UsedStepThisTurn, Is.True);
            }

            // Keep the success scenario independent of the early miss's real
            // backward movement; movement/range behavior has separate coverage.
            using (var scope = new StepScope(enemyHits: 3))
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var controller = scope.Controller;
                scope.BeginWindup();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                scope.EnterWindow();
                Press(keyboard.aKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.aKey);
                Assert.That(slot.DodgeSucceeded, Is.True);
                Assert.That(slot.HitsResolved, Is.Zero, "Input must be evaluated before its impact.");
                scope.Until(() => controller.Session.LastResolvedSlot == 0);
                Assert.That(slot.HitsResolved, Is.EqualTo(3));
                Assert.That(controller.Session.Player.Health, Is.EqualTo(1000));
                Assert.That(controller.Session.Player.Resistance, Is.EqualTo(1000));
                Assert.That(controller.Session.Enemy.Resistance, Is.EqualTo(999));
                Assert.That(controller.ArenaView.IsFatalFocus, Is.False);
                int visibleDamageNumbers = 0;
                foreach (Text text in controller.Hud.Root.GetComponentsInChildren<Text>())
                    if (text.name == "Damage" && text.gameObject.activeInHierarchy) visibleDamageNumbers++;
                Assert.That(visibleDamageNumbers, Is.EqualTo(1),
                    "Only the player's attack displays damage; evaded enemy hits must not create hit feedback.");
            }
        }

        [UnityTest]
        public IEnumerator PressureKey_AddsOneSkillPowerWithoutStacking_AndCannotSucceedAfterFirstHit()
        {
            yield return null;
            using (var scope = new StepScope(playerHits: 3))
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var controller = scope.Controller;
                scope.BeginWindup();
                scope.EnterWindow();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Press(keyboard.dKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.dKey);
                Assert.That(slot.PressureSucceeded, Is.True);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool repeated), Is.True);
                Assert.That(repeated, Is.False, "Another attempt is accepted but cannot multiply this skill's bonus.");
                scope.Until(() => slot.HitsResolved >= 1);
                Assert.That(controller.IsStepTimingWindow, Is.False);
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool late), Is.True);
                Assert.That(late, Is.False, "A later hit is not a new skill activation timing window.");
                scope.Until(() => controller.Session.LastResolvedSlot == 0);
                Assert.That(controller.Session.Enemy.Health, Is.EqualTo(997),
                    "The three-power skill adds three HP damage in total, not another three for each tap or hit.");
                Assert.That(controller.Session.Enemy.Resistance, Is.EqualTo(997));
            }
        }

        [UnityTest]
        public IEnumerator PressureMultiHit_AfterFirstImpactWaitsForContactAgainWhileKnockbackIsMoving()
        {
            yield return null;
            using (var scope = new StepScope(playerHits: 3))
            {
                scope.Settings("{\"animationPlaybackSpeed\":2,\"attackInterval\":0,\"hitStopDuration\":0," +
                    "\"movementDistanceMultiplier\":1,\"movementSpeedMultiplier\":1}");
                var controller = scope.Controller;
                scope.BeginWindup();
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool success), Is.True);
                Assert.That(success, Is.True);
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                scope.Until(() => slot.HitsResolved == 1);
                Assert.That(slot.PressureSucceeded, Is.True);
                Assert.That(controller.ArenaView.HasPendingPush, Is.True);

                // This regression isolates later-hit contact timing. Let only
                // the new real-time cinematic pulse expire, without advancing
                // either combat animation or its pending knockback.
                controller.ArenaView.Tick(0f, 10f);

                // Both fighters receive the first impact's outward push. At
                // double speed the next impact falls within the .1s push, before
                // either fighter can chase. Only the first step impact may
                // bypass contact; a successful flag must not bypass all hits.
                scope.Advance(.04f);
                Assert.That(controller.ArenaView.IsInRange, Is.False);
                Assert.That(slot.HitsResolved, Is.EqualTo(1));
                scope.Advance(.05f);
                Assert.That(controller.ArenaView.HasPendingPush, Is.True);
                Assert.That(controller.ArenaView.IsInRange, Is.False);
                Assert.That(slot.HitsResolved, Is.EqualTo(1),
                    "The second pressure hit must wait for contact instead of striking across the knockback gap.");
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(1f / 8f).Within(.0001f),
                    "The slot clock holds the second impact pose while movement closes the gap.");
                scope.Until(() => slot.HitsResolved == 2);
                Assert.That(controller.ArenaView.IsInRange, Is.True);
                scope.Until(() => controller.Session.LastResolvedSlot == 0);
                Assert.That(slot.HitsResolved, Is.EqualTo(3));
                Assert.That(controller.Session.Enemy.Health, Is.EqualTo(997));
            }
        }

        [UnityTest]
        public IEnumerator RepeatedMissesHaveNoTurnLimit_AndSuppressOnlyTheFollowingNaturalRecovery()
        {
            yield return null;
            using (var scope = new StepScope(enemyActionCount: 2))
            {
                var controller = scope.Controller;
                scope.BeginWindup();
                Assert.That(controller.Session.Act, Is.EqualTo(2));
                for (int attempt = 0; attempt < 32; attempt++)
                {
                    LegacyStepAction action = attempt % 2 == 0 ? LegacyStepAction.Dodge : LegacyStepAction.Pressure;
                    Assert.That(controller.TryStep(action, out bool success), Is.True);
                    Assert.That(success, Is.False);
                }
                scope.Until(() => controller.IsBetweenSlots);
                Assert.That(controller.CanStep, Is.True);
                Assert.That(controller.Session.CurrentSlot, Is.Null);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool gapAttempt), Is.True);
                Assert.That(gapAttempt, Is.False, "The gap accepts steps but has no skill timing success.");
                scope.Until(() => controller.CanChoose && controller.Session.RoundNumber == 2);
                Assert.That(controller.Session.Act, Is.EqualTo(2), "A miss costs the next natural ACT recovery.");
                Assert.That(controller.Session.UsedStepThisTurn, Is.False);
                Assert.That(controller.Session.StepAttemptsThisTurn, Is.Zero, "The narrowing restarts every turn.");
                scope.BeginWindup();
                scope.Until(() => controller.CanChoose && controller.Session.RoundNumber == 3);
                Assert.That(controller.Session.Act, Is.EqualTo(4),
                    "Without another step, leftover one ACT plus natural three ACT recovers normally.");
            }
        }

        [UnityTest]
        public IEnumerator WindupCue_OpensNearFirstImpact_ClosesAfterIt_AndDoesNotResolveEarly()
        {
            yield return null;
            using (var scope = new StepScope(enemyHits: 3))
            {
                var controller = scope.Controller;
                scope.BeginWindup();
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(controller.IsSkillWindup, Is.True);
                Assert.That(controller.StepCueProgress, Is.EqualTo(0f).Within(.0001f));
                scope.Advance(.2f);
                Assert.That(controller.IsSkillWindup, Is.True);
                Assert.That(controller.IsStepTimingWindow, Is.False);
                Assert.That(slot.HitsResolved, Is.Zero);
                float beforeWindow = controller.StepCueProgress;
                scope.EnterWindow();
                Assert.That(controller.StepCueProgress, Is.GreaterThan(beforeWindow));
                Assert.That(controller.StepCueProgress, Is.InRange(0f, 1f));
                Assert.That(slot.HitsResolved, Is.Zero);
                scope.Until(() => !controller.IsSkillWindup);
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(0f).Within(.0001f));
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(controller.IsStepTimingWindow, Is.True);
                scope.Until(() => slot.HitsResolved >= 1);
                Assert.That(controller.IsStepTimingWindow, Is.False);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool late), Is.True);
                Assert.That(late, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator StepSuccess_RequiresEnemyAttackForDodge_AndOwnSkillForPressure()
        {
            yield return null;
            using (var scope = new StepScope(playerGuard: true, enemyGuard: true))
            {
                var controller = scope.Controller;
                scope.BeginWindup();
                scope.EnterWindow();
                // Pressure first: a missed dodge steps back out of range, and later presses judge a narrower window.
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool pressure), Is.True);
                Assert.That(pressure, Is.True, "Own guard skills are valid pressure targets.");
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool dodge), Is.True);
                Assert.That(dodge, Is.False, "There is no enemy attack to evade when the opponent guards.");
            }
            using (var scope = new StepScope())
            {
                var controller = scope.Controller;
                scope.BeginWindup(queuePlayer: false);
                scope.EnterWindow();
                Assert.That(controller.Session.CurrentSlot.PlayerSkill, Is.Null);
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool pressure), Is.True);
                Assert.That(pressure, Is.False, "An empty player slot cannot gain pressure damage.");
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool dodge), Is.True);
                Assert.That(dodge, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator LobbyAndMissions_DisableStep_AndRestartClearsItsState()
        {
            yield return null;
            using (var scope = new StepScope())
            {
                var controller = scope.Controller;
                scope.BeginWindup();
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool success), Is.True);
                Assert.That(success, Is.True);
                controller.RestartJourney();
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.CanStep, Is.False);
                Assert.That(controller.IsSkillWindup, Is.False);
                Assert.That(controller.StepCueProgress, Is.Zero);
                Assert.That(controller.Session.UsedStepThisTurn, Is.False);
                Assert.That(controller.ArenaView.IsStepping, Is.False);
                Assert.That(controller.ArenaView.ActiveStepAfterimageCount, Is.Zero);
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out success), Is.False);
                Assert.That(success, Is.False);

                controller.StartNewGame();
                Assert.That(controller.CanStep, Is.False);
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(controller.ContinueDialogue(), Is.False);
                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.Until(() => controller.Session.CurrentSlot != null);
                Assert.That(controller.IsMission, Is.True);
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.CanStep, Is.False, "Steps are not open in the opening arc yet.");
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out success), Is.False);
                Assert.That(success, Is.False);
                Assert.That(controller.Session.UsedStepThisTurn, Is.False);
                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True, "Leaving a mission returns to its briefing.");
                Assert.That(controller.CanStep, Is.False);
                controller.RestartMatch();
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.UsedStepThisTurn, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator EachAttempt_NarrowsTheWindowForTheTurn_AndConsecutiveSuccessesCountOnTheCue()
        {
            yield return null;
            using (var scope = new StepScope())
            {
                var controller = scope.Controller;
                scope.BeginWindup();
                float fresh = controller.CurrentStepWindow;
                float freshFraction = controller.StepWindowFraction;
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool dodged), Is.True);
                Assert.That(dodged, Is.True);
                DuelPresentationSettings settings = controller.PresentationSettings;
                Assert.That(controller.CurrentStepWindow, Is.EqualTo(LegacyStepTiming.Window(fresh, 1,
                    settings.StepWindowDecay, settings.StepMinimumWindow)).Within(.0001f));
                Assert.That(controller.CurrentStepWindow, Is.LessThan(fresh));
                Assert.That(controller.StepWindowFraction, Is.LessThan(freshFraction), "The success band on the ring narrows too.");
                Transform root = controller.StepHud.Root.transform;
                Assert.That(root.Find("Dodge Cue/Timing Status").GetComponent<Text>().text, Is.EqualTo("성공"));
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool pressed), Is.True);
                Assert.That(pressed, Is.True);
                Assert.That(controller.Session.StepSuccessStreak, Is.EqualTo(2));
                Assert.That(root.Find("Pressure Cue/Timing Status").GetComponent<Text>().text, Is.EqualTo("연속 2"));
                Assert.That(root.Find("ACT Recovery Notice").GetComponent<Text>().text, Does.Contain("이번 턴 2회"));
                scope.Until(() => controller.CanChoose && controller.Session.RoundNumber == 2);
                Assert.That(controller.CurrentStepWindow, Is.EqualTo(fresh).Within(.0001f), "A new turn restores the full window.");
                Assert.That(controller.Session.StepSuccessStreak, Is.Zero);
            }
        }

        [Test]
        public void StreakSlowMotion_DeepensAndLengthensUpToItsCap()
        {
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                LegacyArenaView.StepSlowMotionFor(settings, 1, out float duration, out float scale);
                Assert.That(duration, Is.EqualTo(settings.StepSlowMotionDuration));
                Assert.That(scale, Is.EqualTo(settings.StepSlowMotionScale));
                for (int streak = 2; streak <= LegacyArenaView.MaximumSlowStreak; streak++)
                {
                    LegacyArenaView.StepSlowMotionFor(settings, streak, out float longer, out float slower);
                    Assert.That(longer, Is.GreaterThan(duration));
                    Assert.That(slower, Is.LessThan(scale));
                    duration = longer;
                    scale = slower;
                }
                LegacyArenaView.StepSlowMotionFor(settings, 99, out float capped, out float cappedScale);
                Assert.That(capped, Is.EqualTo(duration));
                Assert.That(cappedScale, Is.EqualTo(scale));
                Assert.That(cappedScale, Is.GreaterThanOrEqualTo(LegacyArenaView.DecisiveSlowMotionScale),
                    "A step streak never slows combat more than a decisive close-up.");
                JsonUtility.FromJsonOverwrite("{\"stepSlowMotionDuration\":0}", settings);
                LegacyArenaView.StepSlowMotionFor(settings, 5, out float offDuration, out _);
                Assert.That(offDuration, Is.Zero, "A switched-off step slow motion stays off on a streak.");
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [UnityTest]
        public IEnumerator HoldingSpaceOrShift_DoesNotSlowCombat()
        {
            yield return null;
            using (var scope = new StepScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var controller = scope.Controller;
                scope.BeginWindup();
                scope.Until(() => !controller.IsSkillWindup);
                float globalScale = Time.timeScale;
                float before = controller.ActiveSlotElapsedTime;
                Press(keyboard.spaceKey);
                yield return null;
                scope.Advance(.01f, keyboard);
                Release(keyboard.spaceKey);
                yield return null;
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(before + .01f).Within(.0001f),
                    "Holding the confirmation key must not keep combat in slow motion.");
                before = controller.ActiveSlotElapsedTime;
                Press(keyboard.leftShiftKey);
                yield return null;
                scope.Advance(.01f, keyboard);
                Release(keyboard.leftShiftKey);
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(before + .01f).Within(.0001f),
                    "Shift no longer offers slow viewing.");
                Assert.That(Time.timeScale, Is.EqualTo(globalScale));
            }
        }

        [UnityTest]
        public IEnumerator StepCueHud_ShowsPlayerCenteredArcsAndFeedback_WithoutTakingPointerInput()
        {
            yield return null;
            using (var scope = new StepScope(enemyActionCount: 2))
            {
                scope.Settings("{\"skillInterval\":0}");
                var controller = scope.Controller;
                Assert.That(controller.StepHud.IsVisible, Is.False);
                scope.BeginWindup();
                Assert.That(controller.StepHud.IsVisible, Is.True);
                Transform root = controller.StepHud.Root.transform;
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.blocksRaycasts, Is.False);
                Assert.That(group.interactable, Is.False);
                foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>())
                    Assert.That(graphic.raycastTarget, Is.False, "The cue must not cover existing HUD interaction.");
                foreach (Text text in root.GetComponentsInChildren<Text>())
                {
                    Assert.That(text.font, Is.Not.Null);
                    Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(16));
                    if (text.resizeTextForBestFit)
                        Assert.That(text.resizeTextMinSize, Is.GreaterThanOrEqualTo(12));
                }
                Assert.That(root.Find("Dodge Cue/Step Key").GetComponent<Text>().text, Is.EqualTo("A  회피"));
                Assert.That(root.Find("Pressure Cue/Step Key").GetComponent<Text>().text, Is.EqualTo("D  압박"));
                Assert.That(controller.StepHud.DodgeRing.gameObject.activeInHierarchy, Is.True);
                Assert.That(controller.StepHud.PressureRing.gameObject.activeInHierarchy, Is.True);
                Assert.That(controller.StepHud.Actor, Is.SameAs(controller.ArenaView.PlayerRenderer.transform));
                Assert.That(controller.StepHud.DodgeRing.IsLeftArc, Is.True);
                Assert.That(controller.StepHud.PressureRing.IsLeftArc, Is.False);
                Assert.That(controller.StepHud.DodgeRing.Progress, Is.EqualTo(controller.StepCueProgress).Within(.0001f));
                Assert.That(controller.StepHud.DodgeRing.Radius, Is.EqualTo(60f).Within(.0001f));
                scope.EnterWindow();
                Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "Dodge Cue", "Pressure Cue" })
                {
                    Transform cue = root.Find(name);
                    Assert.That(cue.Find("Activation Track"), Is.Null,
                        "Timing should read around the character instead of through a second bottom progress bar.");
                    Assert.That(cue.Find("Success Window Marker").gameObject.activeInHierarchy, Is.True);
                }
                foreach (DuelStepRing ring in new[] { controller.StepHud.DodgeRing, controller.StepHud.PressureRing })
                {
                    Assert.That(ring.IsTimingWindow, Is.True);
                    Assert.That(ring.Progress, Is.EqualTo(controller.StepCueProgress).Within(.0001f));
                    Assert.That(ring.Radius, Is.EqualTo(40f + 20f * (1f - controller.StepCueProgress)).Within(.0001f));
                    Assert.That(ring.WindowFraction, Is.EqualTo(controller.StepWindowFraction).Within(.0001f));
                    Assert.That(ring.CoreLineWidth, Is.LessThan(1.2f));
                }
                Vector3 bodyCenter = controller.StepHud.Actor.TransformPoint(DuelStepHud.ActorBodyLocalOffset);
                Vector3 screen = controller.ArenaView.ArenaCamera.WorldToScreenPoint(bodyCenter);
                Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(root.GetComponent<RectTransform>(),
                    screen, null, out Vector2 expectedCenter), Is.True);
                foreach (string name in new[] { "Dodge Cue", "Pressure Cue" })
                    Assert.That(Vector2.Distance(root.Find(name).GetComponent<RectTransform>().anchoredPosition,
                        expectedCenter), Is.LessThan(.01f));
                Assert.That(root.Find("Dodge Cue/Step Key").position.x,
                    Is.LessThan(root.Find("Pressure Cue/Step Key").position.x));
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool success), Is.True);
                Assert.That(success, Is.True);
                Assert.That(root.Find("Pressure Cue/Timing Status").GetComponent<Text>().text, Does.Contain("성공"));
                Assert.That(root.Find("ACT Recovery Notice").GetComponent<Text>().text, Does.Contain("성공 구간이 좁아졌습니다"));
                LegacyCurrentSlot first = controller.Session.CurrentSlot;
                scope.Until(() => controller.Session.CurrentSlot != null &&
                    !ReferenceEquals(controller.Session.CurrentSlot, first));
                Assert.That(controller.Session.CurrentSlot.PlayerSkill, Is.Null);
                Assert.That(controller.Session.CurrentSlot.PressureSucceeded, Is.False);
                Assert.That(root.Find("Pressure Cue/Timing Status").GetComponent<Text>().text, Does.Not.Contain("성공"),
                    "Feedback for the completed skill must not label the next empty player slot as successful.");
            }
        }

        [UnityTest]
        public IEnumerator AcceptedSteps_PlayTheirOwnSounds_AndLobbyStopsBothVoices()
        {
            yield return null;
            using (var scope = new StepScope())
            {
                var controller = scope.Controller;
                AudioSource dodge = controller.transform.Find("Step Audio/Dodge Sound").GetComponent<AudioSource>();
                AudioSource pressure = controller.transform.Find("Step Audio/Pressure Sound").GetComponent<AudioSource>();
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out _), Is.False);
                Assert.That(dodge.isPlaying || pressure.isPlaying, Is.False);
                scope.BeginWindup();
                int act = controller.Session.Act, health = controller.PlayerHealth;
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool early), Is.True);
                Assert.That(early, Is.False);
                Assert.That(pressure.clip.name, Is.EqualTo("Step Pressure Miss"));
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Dodge, out bool evaded), Is.True);
                Assert.That(evaded, Is.True);
                Assert.That(dodge.clip.name, Is.EqualTo("Step Dodge Success"));
                scope.EnterWindow();
                Assert.That(controller.TryStep(LegacyStepAction.Pressure, out bool pressed), Is.True);
                Assert.That(pressed, Is.True);
                Assert.That(pressure.clip.name, Is.EqualTo("Step Pressure Success"));
                Assert.That(controller.Session.Act, Is.EqualTo(act));
                Assert.That(controller.PlayerHealth, Is.EqualTo(health), "Sounds must not resolve a hit themselves.");
                controller.ReturnToLobby();
                Assert.That(dodge.isPlaying || pressure.isPlaying, Is.False);
                Assert.That(controller.StepHud.IsVisible, Is.False);
            }
        }

        /// <summary>Drive the actual controller and input clock without editing the shared settings asset.</summary>
        private sealed class StepScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, settingsClone;
            private readonly FieldInfo arenaSettingsField;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public StepScope(int playerHits = 1, int enemyHits = 1, bool playerGuard = false, bool enemyGuard = false,
                int enemyActionCount = 1)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettingsField = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                Assert.That(arenaSettingsField, Is.Not.Null);
                originalArenaSettings = (DuelPresentationSettings)arenaSettingsField.GetValue(Controller.ArenaView);
                settingsClone = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":1,\"attackInterval\":0.1," +
                    "\"skillInterval\":0.28,\"hitStopDuration\":0,\"stepAnticipationDuration\":0.24,\"stepTimingWindow\":0.1}", settingsClone);
                SetField("presentationSettings", settingsClone);
                arenaSettingsField.SetValue(Controller.ArenaView, settingsClone);
                var playerSkills = new[]
                {
                    WeakSkill(101, 0, playerHits, playerGuard), WeakSkill(103, 1, 1, false), WeakSkill(105, 2, 1, false)
                };
                SetField("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000, playerSkills,
                    new[] { WeakSkill(201, 0, enemyHits, enemyGuard) }, new[] { enemyActionCount }, 1));
                MethodInfo method = typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance);
                Assert.That(method, Is.Not.Null);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller, method);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void Advance(float realDelta, Keyboard keyboard = null) => advance(realDelta, keyboard);
            public void Settings(string json) => JsonUtility.FromJsonOverwrite(json, settingsClone);

            public void BeginWindup(bool queuePlayer = true)
            {
                if (queuePlayer) Assert.That(Controller.QueueLane(0), Is.True);
                Controller.CommitTurn();
                Until(() => Controller.Session.CurrentSlot != null);
                Assert.That(Controller.IsSkillWindup, Is.True);
                Assert.That(Controller.Session.CurrentSlot.HitsResolved, Is.Zero);
            }

            public void EnterWindow()
            {
                Until(() => Controller.IsStepTimingWindow);
                Assert.That(Controller.Session.CurrentSlot.HitsResolved, Is.Zero);
            }

            public void Until(Func<bool> condition)
            {
                int steps = 0;
                while (!condition() && steps++ < 10000) Advance(.001f);
                Assert.That(condition(), Is.True, "The controller must progress within ten simulated seconds.");
            }

            private static LegacySkill WeakSkill(int id, int lane, int count, bool guard)
                => new LegacySkill(id, "Step Test", 1, count, count,
                    guard ? LegacySkillKind.Defence : LegacySkillKind.Attack,
                    guard ? LegacySkillProperty.Defence : LegacySkillProperty.Slash,
                    count, lane, string.Empty, guard ? "Defense" : "Slash", 1);

            private void SetField(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null);
                field.SetValue(Controller, value);
            }

            public void Dispose()
            {
                SetField("presentationSettings", originalSettings);
                arenaSettingsField.SetValue(Controller.ArenaView, originalArenaSettings);
                SetField("session", originalSession);
                Controller.RestartMatch();
                Controller.enabled = originalEnabled;
                Object.Destroy(settingsClone);
            }
        }
    }
}
