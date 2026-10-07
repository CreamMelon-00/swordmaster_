using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class StepRingHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator FirstSkillProgress_ShrinksThinActorArcs_WithLeftDodgeAndRightPressure()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyQueuedDuel duel = CreateDuel();
                LegacyCurrentSlot slot = duel.CurrentSlot;
                fixture.Refresh(slot, 0f, false);
                Assert.That(fixture.Hud.IsVisible, Is.True);
                Assert.That(fixture.Hud.Root.name, Is.EqualTo("Step Timing"));
                RectTransform root = fixture.Hud.Root.GetComponent<RectTransform>();
                Assert.That(root.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
                foreach (Transform child in fixture.Hud.Root.GetComponentsInChildren<Transform>(true))
                    Assert.That(child.name, Does.Not.Contain("Activation Track"),
                        "No secondary bottom timing track should remain in the ring HUD.");
                foreach (DuelStepRing ring in fixture.Rings)
                {
                    Assert.That(ring.gameObject.activeInHierarchy, Is.True);
                    Assert.That(ring.Progress, Is.Zero);
                    Assert.That(ring.Radius, Is.EqualTo(60f).Within(.001f));
                    Assert.That(ring.IsTimingWindow, Is.False);
                    Assert.That(ring.raycastTarget, Is.False);
                }
                Assert.That(fixture.Hud.DodgeRing.IsLeftArc, Is.True);
                Assert.That(fixture.Hud.PressureRing.IsLeftArc, Is.False);
                AssertProjection(fixture);
                foreach (string cueName in new[] { "Dodge Cue", "Pressure Cue" })
                {
                    RectTransform cue = fixture.Cue(cueName);
                    foreach (string labelName in new[] { "Step Key", "Timing Status", "Success Window Marker" })
                    {
                        Text label = cue.Find(labelName).GetComponent<Text>();
                        float signedX = label.rectTransform.anchoredPosition.x * (cueName == "Dodge Cue" ? -1f : 1f);
                        Assert.That(signedX, Is.GreaterThan(0f), "A belongs left of the actor and D belongs right.");
                        Assert.That(label.rectTransform.localScale, Is.EqualTo(Vector3.one));
                        Assert.That(label.raycastTarget, Is.False);
                    }
                    Text marker = cue.Find("Success Window Marker").GetComponent<Text>();
                    Assert.That(marker.gameObject.activeInHierarchy, Is.True);
                    Assert.That(marker.text, Is.EqualTo("성공 구간"));
                }

                fixture.Refresh(slot, .5f, false);
                foreach (DuelStepRing ring in fixture.Rings)
                    Assert.That(ring.Radius, Is.EqualTo(50f).Within(.001f));
                fixture.Refresh(slot, 1f, true);
                foreach (DuelStepRing ring in fixture.Rings)
                {
                    Assert.That(ring.Progress, Is.EqualTo(1f));
                    Assert.That(ring.Radius, Is.EqualTo(40f).Within(.001f),
                        "The shrinking ring reaches the fixed target radius at the first skill impact.");
                    Assert.That(ring.IsTimingWindow, Is.True);
                }
            }
        }

        [UnityTest]
        public IEnumerator ActorArcs_FollowStablePivotAndCameraPanZoom_WithoutSwappingKeys()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                fixture.Refresh(slot, .35f, false);
                AssertProjection(fixture);
                Vector3 oldPosition = fixture.Cue("Dodge Cue").position;
                float oldScale = fixture.Hud.DodgeRing.rectTransform.localScale.x;
                // Camera framing and a real step affect the actor pivot, not skill-card pulse or sprite bounds.
                fixture.Actor.position += new Vector3(1.7f, -.4f, 0f);
                fixture.Camera.transform.position += new Vector3(-.5f, .2f, 0f);
                fixture.Camera.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
                fixture.Camera.orthographicSize *= .7f;
                fixture.Actor.localScale = Vector3.one * 1.2f;
                fixture.Refresh(slot, .6f, false);
                AssertProjection(fixture);
                Assert.That(Vector3.Distance(fixture.Cue("Dodge Cue").position, oldPosition), Is.GreaterThan(1f));
                Assert.That(fixture.Hud.DodgeRing.rectTransform.localScale.x, Is.GreaterThan(oldScale));
                Assert.That(fixture.Hud.DodgeRing.Radius, Is.EqualTo(48f).Within(.001f));
                Assert.That(fixture.Hud.PressureRing.Radius, Is.EqualTo(48f).Within(.001f));
                Text a = fixture.Cue("Dodge Cue").Find("Step Key").GetComponent<Text>();
                Text d = fixture.Cue("Pressure Cue").Find("Step Key").GetComponent<Text>();
                Assert.That(a.rectTransform.position.x, Is.LessThan(d.rectTransform.position.x));
                Assert.That(a.rectTransform.lossyScale, Is.EqualTo(d.rectTransform.lossyScale));
                Assert.That(a.fontSize, Is.EqualTo(16));
                Assert.That(d.fontSize, Is.EqualTo(16));
            }
        }

        [UnityTest]
        public IEnumerator SuccessBand_IsMarkedFromTimingFraction_WithoutThickeningTheMovingStroke()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                fixture.Refresh(slot, .4f, false, windowFraction: .25f);
                foreach (DuelStepRing ring in fixture.Rings)
                {
                    Assert.That(ring.HasSuccessBand, Is.True);
                    Assert.That(ring.WindowStartRadius, Is.EqualTo(45f).Within(.001f));
                    Assert.That(ring.CoreLineWidth, Is.LessThan(1.2f));
                }
                fixture.Refresh(slot, .8f, true, windowFraction: .25f);
                foreach (DuelStepRing ring in fixture.Rings)
                {
                    Assert.That(ring.CoreLineWidth, Is.EqualTo(1.05f).Within(.001f));
                    Assert.That(ring.IsTimingWindow, Is.True);
                }
                fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true);
                fixture.Refresh(slot, .8f, true, windowFraction: .5f);
                Assert.That(fixture.Hud.DodgeRing.SuccessPulse, Is.True);
                Assert.That(fixture.Hud.DodgeRing.CoreLineWidth, Is.EqualTo(1.05f).Within(.001f));
                Assert.That(fixture.Hud.DodgeRing.WindowStartRadius, Is.EqualTo(50f).Within(.001f));
                fixture.Hud.BindActor(fixture.Camera, null);
                fixture.Refresh(slot, .8f, true);
                foreach (DuelStepRing ring in fixture.Rings)
                    Assert.That(ring.gameObject.activeInHierarchy, Is.False,
                        "An unavailable actor cannot leave a cue fixed at an old screen position.");
            }
        }

        [UnityTest]
        public IEnumerator MissingSkillsAndEnemyGuard_HideOnlyTheirUnavailableTimingRing()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyQueuedDuel guardDuel = CreateDuel(playerGuard: true, enemyGuard: true);
                fixture.Refresh(guardDuel.CurrentSlot, .85f, true);
                Assert.That(fixture.Hud.DodgeRing.gameObject.activeInHierarchy, Is.False,
                    "Enemy guard has no incoming attack timing to evade.");
                Assert.That(fixture.Hud.PressureRing.gameObject.activeInHierarchy, Is.True,
                    "The player's own guard still has a pressure timing.");
                Assert.That(fixture.Hud.PressureRing.IsTimingWindow, Is.True);

                LegacyQueuedDuel emptyPlayer = CreateDuel(queuePlayer: false);
                fixture.Refresh(emptyPlayer.CurrentSlot, .9f, true);
                Assert.That(fixture.Hud.DodgeRing.gameObject.activeInHierarchy, Is.True);
                Assert.That(fixture.Hud.PressureRing.gameObject.activeInHierarchy, Is.False,
                    "A missing player skill must not show an actionable pressure ring.");
                fixture.Refresh(null, 0f, false);
                foreach (DuelStepRing ring in fixture.Rings)
                    Assert.That(ring.gameObject.activeInHierarchy, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator MultiHitSkill_HidesItsTimingRingAfterFirstImpact_WithoutReopeningOnLaterHits()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyQueuedDuel duel = CreateDuel(hits: 3);
                LegacyCurrentSlot slot = duel.CurrentSlot;
                fixture.Refresh(slot, .95f, true);
                foreach (DuelStepRing ring in fixture.Rings)
                    Assert.That(ring.gameObject.activeInHierarchy, Is.True);
                for (int hit = 1; hit <= 3; hit++)
                {
                    duel.ResolveNextHit();
                    Assert.That(slot.HitsResolved, Is.EqualTo(hit));
                    fixture.Refresh(slot, 1f, false);
                    foreach (DuelStepRing ring in fixture.Rings)
                        Assert.That(ring.gameObject.activeInHierarchy, Is.False,
                            "Later strikes are not another activation window for this same queued skill.");
                }
            }
        }

        [UnityTest]
        public IEnumerator SuccessPulse_IsShortLived_AndDoesNotCarryIntoNextSlotOrPlanning()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyQueuedDuel duel = CreateDuel(hits: 3, enemyActions: 2);
                LegacyCurrentSlot first = duel.CurrentSlot;
                fixture.Refresh(first, .95f, true);
                Assert.That(duel.TryStep(LegacyStepAction.Pressure, true, out bool success), Is.True);
                Assert.That(success, Is.True);
                fixture.Hud.ShowFeedback(LegacyStepAction.Pressure, true);
                fixture.Refresh(first, .95f, true, true);
                Assert.That(fixture.Hud.PressureRing.SuccessPulse, Is.True);
                Assert.That(fixture.Hud.PressureRing.Radius, Is.EqualTo(40f).Within(.001f));
                Assert.That(fixture.Hud.PressureRing.gameObject.activeInHierarchy, Is.True);
                Assert.That(fixture.Hud.Root.transform.Find("ACT Recovery Notice").GetComponent<Text>().text,
                    Does.Contain("성공 구간이 좁아졌습니다"));

                duel.ResolveNextHit();
                fixture.Hud.Tick(.5f);
                fixture.Refresh(first, 1f, false, true);
                Assert.That(fixture.Hud.PressureRing.SuccessPulse, Is.False);
                Assert.That(fixture.Hud.PressureRing.gameObject.activeInHierarchy, Is.False);
                while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                LegacyCurrentSlot next = duel.BeginNextSlot();
                fixture.Refresh(next, 0f, false, true);
                Assert.That(next.PlayerSkill, Is.Null);
                Assert.That(fixture.Hud.PressureRing.SuccessPulse, Is.False);
                Assert.That(fixture.Hud.PressureRing.gameObject.activeInHierarchy, Is.False);

                fixture.Hud.Refresh(false, next, 0f, false, .3f, true);
                Assert.That(fixture.Hud.IsVisible, Is.False);
                foreach (DuelStepRing ring in fixture.Rings)
                    Assert.That(ring.gameObject.activeInHierarchy, Is.False);
                fixture.Hud.Reset();
                Assert.That(fixture.Hud.IsVisible, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RingMesh_IsHollowFiniteAndWhite_WithNoPointerBlocking()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                fixture.Refresh(CreateDuel().CurrentSlot, .8f, true);
                CanvasGroup group = fixture.Hud.Root.GetComponent<CanvasGroup>();
                Assert.That(group, Is.Not.Null);
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
                foreach (Graphic graphic in fixture.Hud.Root.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False);

                DuelStepRing ring = fixture.Hud.PressureRing;
                MethodInfo populate = typeof(DuelStepRing).GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
                Assert.That(populate, Is.Not.Null);
                using (var vertices = new VertexHelper())
                {
                    populate.Invoke(ring, new object[] { vertices });
                    Assert.That(vertices.currentVertCount, Is.GreaterThan(12));
                    Assert.That(vertices.currentIndexCount, Is.GreaterThan(12));
                    bool hasWhite = false;
                    bool hasDarkUnderlay = false;
                    bool hasFeatheredGlow = false;
                    for (int index = 0; index < vertices.currentVertCount; index++)
                    {
                        UIVertex vertex = default;
                        vertices.PopulateUIVertex(ref vertex, index);
                        Assert.That(float.IsNaN(vertex.position.x) || float.IsInfinity(vertex.position.x), Is.False);
                        Assert.That(float.IsNaN(vertex.position.y) || float.IsInfinity(vertex.position.y), Is.False);
                        Assert.That(new Vector2(vertex.position.x, vertex.position.y).magnitude, Is.GreaterThan(20f),
                            "A timing arc must remain hollow so the actor stays visible.");
                        Assert.That(vertex.position.x, Is.GreaterThan(0f),
                            "The D cue draws only the screen-right semicircle, leaving A's side clear.");
                        hasWhite |= vertex.color.r >= 245 && vertex.color.g >= 245 && vertex.color.b >= 245;
                        hasDarkUnderlay |= vertex.color.r < 100 && vertex.color.g < 100 && vertex.color.b < 100;
                        hasFeatheredGlow |= vertex.color.r >= 245 && vertex.color.a > 0 && vertex.color.a < 60;
                    }
                    Assert.That(hasWhite, Is.True);
                    Assert.That(hasDarkUnderlay, Is.True,
                        "The white arc needs a dark edge to remain readable over bright sparks or forest.");
                    Assert.That(hasFeatheredGlow, Is.True);
                }
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulInput_AddsBroaderWhiteHaloAndOutwardPulse_WithoutFillingTheActor()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                DuelStepRing ring = fixture.Hud.PressureRing;
                fixture.Refresh(CreateDuel().CurrentSlot, 1f, true);
                ring.Configure(1f, true, false, false, 0f, 1f);
                MeshMetrics normal = MeasureMesh(ring);
                ring.Configure(1f, true, true, false, 0f, 1f, 1.6f, 0f);
                MeshMetrics success = MeasureMesh(ring);
                Assert.That(ring.CoreLineWidth, Is.EqualTo(1.05f));
                Assert.That(ring.PulseRadius, Is.EqualTo(DuelStepRing.ExpectedRadius));
                Assert.That(success.WhiteAlpha, Is.GreaterThan(normal.WhiteAlpha));
                Assert.That(success.MaximumRadius, Is.GreaterThan(normal.MaximumRadius));
                Assert.That(success.MinimumRadius, Is.GreaterThan(20f), "The brighter rim must not turn into a disc over the actor.");
                ring.Configure(1f, true, true, false, 0f, 1f, 1.6f, .5f);
                Assert.That(ring.PulseRadius, Is.GreaterThan(DuelStepRing.ExpectedRadius));
                Assert.That(ring.PulseRadius, Is.LessThanOrEqualTo(DuelStepRing.ExpectedRadius + 18f));
                Assert.That(MeasureMesh(ring).MinimumRadius, Is.GreaterThan(20f));
                Assert.That(ring.raycastTarget, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SuccessPulse_UsesOnlyExplicitRealTime_AndHiddenHudClearsResidualFeedback()
        {
            yield return null;
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                using (var fixture = new RingFixture())
                {
                    LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                    fixture.Refresh(slot, .95f, true);
                    fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true);
                    fixture.Refresh(slot, .95f, true);
                    Assert.That(fixture.Hud.DodgeRing.PulseProgress, Is.Zero);
                    fixture.Hud.Tick(.175f);
                    fixture.Refresh(slot, .95f, true);
                    Assert.That(fixture.Hud.DodgeRing.PulseProgress, Is.EqualTo(.5f).Within(.001f));
                    fixture.Hud.Tick(0f);
                    fixture.Hud.Tick(-1f);
                    fixture.Hud.Tick(float.NaN);
                    fixture.Refresh(slot, .95f, true);
                    Assert.That(fixture.Hud.DodgeRing.PulseProgress, Is.EqualTo(.5f).Within(.001f));
                    fixture.Hud.Refresh(false, slot, .95f, true, .3f, true);
                    fixture.Refresh(slot, .95f, true);
                    Assert.That(fixture.Hud.DodgeRing.SuccessPulse, Is.False);
                    Assert.That(fixture.Hud.DodgeRing.PulseProgress, Is.Zero);
                    fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true);
                    fixture.Hud.Tick(.351f);
                    fixture.Refresh(slot, .95f, true);
                    Assert.That(fixture.Hud.DodgeRing.SuccessPulse, Is.False);
                }
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [UnityTest]
        public IEnumerator ConsecutiveSuccesses_KeepAnAdjacentCount_AndEscalateTheResultBurst()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                fixture.Refresh(slot, .9f, true);
                Text count = FindStepGraphic<Text>(fixture.Hud.Root, "Step Combo Count");
                DuelStepResultBurst burst = FindStepGraphic<DuelStepResultBurst>(fixture.Hud.Root, "Step Result Burst");
                Assert.That(count, Is.Not.Null);
                Assert.That(burst, Is.Not.Null);
                Assert.That(count.raycastTarget, Is.False);
                Assert.That(burst.raycastTarget, Is.False);

                fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true, 1);
                fixture.Refresh(slot, .9f, true);
                int firstTier = burst.StreakTier;
                int firstVisualWeight = MeasureBurstAlpha(burst, out float innerRadius);
                Assert.That(fixture.Hud.SuccessStreak, Is.EqualTo(1));
                Assert.That(count.gameObject.activeInHierarchy, Is.True);
                Assert.That(count.text, Does.Contain("1"));
                Assert.That(burst.gameObject.activeInHierarchy, Is.True);
                Assert.That(burst.Success, Is.True);
                Assert.That(innerRadius, Is.GreaterThan(20f), "The impulse must leave the actor's body clear.");

                fixture.Hud.ShowFeedback(LegacyStepAction.Pressure, true, 2, 1);
                fixture.Refresh(slot, .9f, true);
                int secondTier = burst.StreakTier;
                int secondVisualWeight = MeasureBurstAlpha(burst, out _);
                Assert.That(fixture.Hud.SuccessStreak, Is.EqualTo(2));
                Assert.That(count.text, Does.Contain("2"));
                Assert.That(secondTier, Is.GreaterThan(firstTier));
                Assert.That(secondVisualWeight, Is.GreaterThan(firstVisualWeight));

                fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true, 3, 2);
                fixture.Refresh(slot, .9f, true);
                Assert.That(fixture.Hud.SuccessStreak, Is.EqualTo(3));
                Assert.That(count.text, Does.Contain("3"));
                Assert.That(burst.StreakTier, Is.GreaterThan(secondTier));
                Assert.That(MeasureBurstAlpha(burst, out _), Is.GreaterThan(secondVisualWeight));

                fixture.Hud.Tick(.5f);
                fixture.Refresh(slot, .9f, true);
                Assert.That(count.gameObject.activeInHierarchy, Is.True,
                    "The combo should remain readable after the short result burst has gone.");
                Assert.That(count.text, Does.Contain("3"));
            }
        }

        [UnityTest]
        public IEnumerator FailedStep_ClearsTheCount_AndShowsASeparateDangerEdge()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                fixture.Refresh(slot, .9f, true);
                fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true, 2, 1);
                fixture.Refresh(slot, .9f, true);
                Assert.That(fixture.Hud.SuccessStreak, Is.EqualTo(2));

                fixture.Hud.ShowFeedback(LegacyStepAction.Pressure, false, 0, 2);
                fixture.Refresh(slot, .9f, true);
                Text count = FindStepGraphic<Text>(fixture.Hud.Root, "Step Combo Count");
                DuelStepResultBurst burst = FindStepGraphic<DuelStepResultBurst>(fixture.Hud.Root, "Step Result Burst");
                DuelStepFailureEdge danger = FindStepGraphic<DuelStepFailureEdge>(fixture.Hud.Root, "Step Failure Edge");
                Assert.That(count, Is.Not.Null);
                Assert.That(burst, Is.Not.Null);
                Assert.That(danger, Is.Not.Null);
                Assert.That(fixture.Hud.SuccessStreak, Is.Zero);
                if (count.gameObject.activeInHierarchy)
                    Assert.That(count.text, Does.Not.Contain("2"), "A miss must not retain the old combo count.");
                Assert.That(burst.gameObject.activeInHierarchy, Is.True);
                Assert.That(burst.Success, Is.False);
                Assert.That(danger.gameObject.activeInHierarchy, Is.True);
                Assert.That(danger.raycastTarget, Is.False);
                Assert.That(danger.Progress, Is.InRange(0f, 1f));
            }
        }

        [UnityTest]
        public IEnumerator StepResultEffects_AdvanceOnExplicitRealTime_AndExpireWithoutBlockingInput()
        {
            yield return null;
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                using (var fixture = new RingFixture())
                {
                    LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                    fixture.Refresh(slot, .9f, true);
                    DuelStepResultBurst burst = FindStepGraphic<DuelStepResultBurst>(fixture.Hud.Root, "Step Result Burst");
                    DuelStepFailureEdge danger = FindStepGraphic<DuelStepFailureEdge>(fixture.Hud.Root, "Step Failure Edge");
                    Assert.That(burst, Is.Not.Null);
                    Assert.That(danger, Is.Not.Null);
                    Assert.That(burst.raycastTarget, Is.False);
                    Assert.That(danger.raycastTarget, Is.False);

                    fixture.Hud.ShowFeedback(LegacyStepAction.Dodge, true, 1);
                    fixture.Refresh(slot, .9f, true);
                    float start = burst.Progress;
                    fixture.Hud.Tick(.12f);
                    fixture.Refresh(slot, .9f, true);
                    Assert.That(burst.Progress, Is.GreaterThan(start));
                    float advanced = burst.Progress;
                    fixture.Hud.Tick(0f);
                    fixture.Hud.Tick(-1f);
                    fixture.Hud.Tick(float.NaN);
                    fixture.Refresh(slot, .9f, true);
                    Assert.That(burst.Progress, Is.EqualTo(advanced).Within(.001f));
                    fixture.Hud.Tick(2f);
                    fixture.Refresh(slot, .9f, true);
                    Assert.That(burst.gameObject.activeInHierarchy, Is.False);

                    fixture.Hud.ShowFeedback(LegacyStepAction.Pressure, false, 0, 1);
                    fixture.Refresh(slot, .9f, true);
                    Assert.That(danger.gameObject.activeInHierarchy, Is.True);
                    float dangerStart = danger.Progress;
                    fixture.Hud.Tick(.12f);
                    fixture.Refresh(slot, .9f, true);
                    Assert.That(danger.Progress, Is.GreaterThan(dangerStart));
                    fixture.Hud.Tick(2f);
                    fixture.Refresh(slot, .9f, true);
                    Assert.That(danger.gameObject.activeInHierarchy, Is.False);
                    fixture.Hud.Reset();
                    Assert.That(fixture.Hud.SuccessStreak, Is.Zero);
                    Assert.That(fixture.Hud.IsVisible, Is.False);
                }
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [UnityTest]
        public IEnumerator MissedInput_KeepsRedStatus_WithoutAnySuccessHaloOrPulse()
        {
            yield return null;
            using (var fixture = new RingFixture())
            {
                LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                fixture.Refresh(slot, .7f, false);
                MeshMetrics before = MeasureMesh(fixture.Hud.PressureRing);
                fixture.Hud.ShowFeedback(LegacyStepAction.Pressure, false);
                fixture.Refresh(slot, .7f, false);
                Assert.That(fixture.Hud.PressureRing.SuccessPulse, Is.False);
                Assert.That(fixture.Hud.PressureRing.PulseProgress, Is.Zero);
                Assert.That(MeasureMesh(fixture.Hud.PressureRing).WhiteAlpha, Is.EqualTo(before.WhiteAlpha));
                Text status = fixture.Cue("Pressure Cue").Find("Timing Status").GetComponent<Text>();
                Assert.That(status.text, Is.EqualTo("빗나감"));
                Assert.That(status.color, Is.EqualTo(DuelVisualTheme.Danger));
            }
        }

        [UnityTest]
        public IEnumerator GlowSetting_IsLiveAndBounded_AndZeroKeepsTheThinTimingCore()
        {
            yield return null;
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            try
            {
                FieldInfo intensity = typeof(DuelPresentationSettings).GetField("stepRingGlowIntensity",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(intensity, Is.Not.Null);
                using (var fixture = new RingFixture(settings))
                {
                    LegacyCurrentSlot slot = CreateDuel().CurrentSlot;
                    fixture.Refresh(slot, 1f, true);
                    Assert.That(fixture.Hud.PressureRing.GlowStrength, Is.EqualTo(1.6f).Within(.001f));
                    intensity.SetValue(settings, 0f);
                    fixture.Refresh(slot, 1f, true);
                    MeshMetrics zero = MeasureMesh(fixture.Hud.PressureRing);
                    Assert.That(fixture.Hud.PressureRing.GlowStrength, Is.Zero);
                    Assert.That(zero.WhiteAlpha, Is.GreaterThan(0), "The fixed target and thin core remain when glow is disabled.");
                    intensity.SetValue(settings, 3f);
                    fixture.Refresh(slot, 1f, true);
                    Assert.That(MeasureMesh(fixture.Hud.PressureRing).WhiteAlpha, Is.GreaterThan(zero.WhiteAlpha));
                    foreach (float value in new[] { -100f, 100f, float.NaN, float.PositiveInfinity })
                    {
                        fixture.Hud.PressureRing.Configure(1f, true, true, false, .3f, 1f, value, value);
                        Assert.That(fixture.Hud.PressureRing.GlowStrength, Is.InRange(0f, 3f));
                        Assert.That(fixture.Hud.PressureRing.PulseProgress, Is.InRange(0f, 1f));
                        Assert.That(float.IsNaN(MeasureMesh(fixture.Hud.PressureRing).MaximumRadius), Is.False);
                    }
                }
            }
            finally { Object.Destroy(settings); }
        }

        private struct MeshMetrics
        {
            public float MinimumRadius, MaximumRadius;
            public int WhiteAlpha;
        }

        private static MeshMetrics MeasureMesh(DuelStepRing ring)
        {
            MethodInfo populate = typeof(DuelStepRing).GetMethod("OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
            var result = new MeshMetrics { MinimumRadius = float.PositiveInfinity };
            using (var vertices = new VertexHelper())
            {
                populate.Invoke(ring, new object[] { vertices });
                for (int index = 0; index < vertices.currentVertCount; index++)
                {
                    UIVertex vertex = default;
                    vertices.PopulateUIVertex(ref vertex, index);
                    float radius = new Vector2(vertex.position.x, vertex.position.y).magnitude;
                    Assert.That(float.IsNaN(radius) || float.IsInfinity(radius), Is.False);
                    result.MinimumRadius = Mathf.Min(result.MinimumRadius, radius);
                    result.MaximumRadius = Mathf.Max(result.MaximumRadius, radius);
                    if (vertex.color.r >= 245 && vertex.color.g >= 245 && vertex.color.b >= 245)
                        result.WhiteAlpha += vertex.color.a;
                }
            }
            return result;
        }

        private static T FindStepGraphic<T>(GameObject root, string name) where T : Component
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.GetComponent<T>();
            return null;
        }

        private static int MeasureBurstAlpha(DuelStepResultBurst burst, out float minimumRadius)
        {
            MethodInfo populate = typeof(DuelStepResultBurst).GetMethod("OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
            minimumRadius = float.PositiveInfinity;
            int alpha = 0;
            using (var vertices = new VertexHelper())
            {
                populate.Invoke(burst, new object[] { vertices });
                for (int index = 0; index < vertices.currentVertCount; index++)
                {
                    UIVertex vertex = default;
                    vertices.PopulateUIVertex(ref vertex, index);
                    minimumRadius = Mathf.Min(minimumRadius,
                        new Vector2(vertex.position.x, vertex.position.y).magnitude);
                    alpha += vertex.color.a;
                }
            }
            return alpha;
        }

        private static LegacyQueuedDuel CreateDuel(int hits = 1, bool playerGuard = false,
            bool enemyGuard = false, bool queuePlayer = true, int enemyActions = 1)
        {
            var player = Skill(101, hits, playerGuard);
            var enemy = Skill(201, hits, enemyGuard);
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                new[] { player }, new[] { enemy }, new[] { enemyActions }, 1);
            if (queuePlayer) Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            return duel;
        }

        private static LegacySkill Skill(int id, int hits, bool guard)
            => new LegacySkill(id, "Ring Test", 0, hits, hits,
                guard ? LegacySkillKind.Defence : LegacySkillKind.Attack,
                guard ? LegacySkillProperty.Defence : LegacySkillProperty.Slash,
                hits, 0, string.Empty, guard ? "Defense" : "Slash", 1);

        private static void AssertProjection(RingFixture fixture)
        {
            RectTransform root = fixture.Hud.Root.GetComponent<RectTransform>();
            Vector3 bodyCenter = fixture.Actor.TransformPoint(DuelStepHud.ActorBodyLocalOffset);
            Vector3 screen = fixture.Camera.WorldToScreenPoint(bodyCenter);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 center), Is.True);
            Vector3 edgeScreen = fixture.Camera.WorldToScreenPoint(bodyCenter +
                fixture.Camera.transform.right * DuelStepHud.ActorTargetWorldRadius);
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(root, edgeScreen, null, out Vector2 edge), Is.True);
            foreach (string cueName in new[] { "Dodge Cue", "Pressure Cue" })
            {
                RectTransform cue = fixture.Cue(cueName);
                Assert.That(Vector2.Distance(cue.anchoredPosition, center), Is.LessThan(.01f),
                    "Both step cues must share the player's fixed local body center, not the sword sprite pivot.");
                Assert.That(cue.localScale, Is.EqualTo(Vector3.one));
                DuelStepRing ring = cue.Find("Timing Ring").GetComponent<DuelStepRing>();
                Assert.That(ring.rectTransform.localScale.x * DuelStepRing.ExpectedRadius,
                    Is.EqualTo(Vector2.Distance(center, edge)).Within(.01f));
                Assert.That(cue.localRotation, Is.EqualTo(Quaternion.identity));
            }
        }

        private sealed class RingFixture : IDisposable
        {
            private readonly GameObject owner;
            private readonly GameObject cameraOwner, actorOwner;
            public readonly DuelStepHud Hud;
            public readonly Camera Camera;
            public readonly Transform Actor;
            public DuelStepRing[] Rings => new[] { Hud.DodgeRing, Hud.PressureRing };

            public RingFixture(DuelPresentationSettings settings = null)
            {
                owner = new GameObject("Step Ring HUD Fixture", typeof(RectTransform), typeof(Canvas));
                owner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                cameraOwner = new GameObject("Step Ring Fixture Camera", typeof(Camera));
                Camera = cameraOwner.GetComponent<Camera>();
                Camera.enabled = false;
                Camera.orthographic = true;
                Camera.orthographicSize = 5f;
                Camera.transform.position = new Vector3(0f, 0f, -10f);
                actorOwner = new GameObject("Step Ring Fixture Actor");
                Actor = actorOwner.transform;
                Actor.position = new Vector3(-2f, .5f, 0f);
                Hud = new DuelStepHud(owner.transform, new LegacyDuelArt(), settings);
                Hud.BindActor(Camera, Actor);
                Canvas.ForceUpdateCanvases();
            }

            public RectTransform Cue(string name)
                => Hud.Root.transform.Find(name).GetComponent<RectTransform>();

            public void Refresh(LegacyCurrentSlot slot, float progress, bool timingWindow, bool usedStep = false,
                float windowFraction = .3f)
            {
                Hud.Refresh(true, slot, progress, timingWindow, windowFraction, usedStep);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(owner);
                Object.Destroy(cameraOwner);
                Object.Destroy(actorOwner);
            }
        }
    }
}
