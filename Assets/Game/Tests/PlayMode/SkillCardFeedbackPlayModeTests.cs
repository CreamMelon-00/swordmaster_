using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillCardFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator HeldOpponentCondition_DistinguishesAllCandidatesFromTheNextReservationSlot()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var draw = Skill(42, 0);
                var blocking = Skill(7, 1, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                var recovery = Skill(19, 2, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                var duel = Duel(new[] { draw, blocking, recovery }, new[]
                {
                    Skill(900, 0), Skill(901, 0, property: LegacySkillProperty.Hit),
                    Skill(902, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence),
                    Skill(903, 0, property: LegacySkillProperty.Hit),
                    Skill(904, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence)
                });
                fixture.Refresh(duel);
                fixture.Hud.ShowExplanation(draw, false);
                AssertTargets(fixture.Hud, new[] { 2, 4 }, -1);
                Assert.That(fixture.Lane(0).ConditionReady, Is.False);
                Assert.That(duel.TryQueueBreath(), Is.True);
                Assert.That(duel.TryQueueBreath(), Is.True);
                fixture.Refresh(duel);
                AssertTargets(fixture.Hud, new[] { 2, 4 }, 2);
                Assert.That(fixture.Lane(0).ConditionReady, Is.True);
                Assert.That(fixture.Effect(false).ConditionReady, Is.True);
                Assert.That(fixture.Hint.text, Does.Contain("지금 예약하면 조건 충족"));

                fixture.Hud.ShowExplanation(blocking, false);
                AssertTargets(fixture.Hud, new[] { 1, 3 }, -1);
                Assert.That(fixture.Lane(0).ConditionReady, Is.False, "Changing the inspected skill clears the old lane.");
                Assert.That(duel.TryQueueBreath(), Is.True);
                fixture.Refresh(duel);
                AssertTargets(fixture.Hud, new[] { 1, 3 }, 3);
                Assert.That(fixture.Lane(1).ConditionReady, Is.True);
                foreach (SkillCardFeedbackGraphic view in fixture.Views)
                    Assert.That(view.HasBuffParticles, Is.False, "Prospective conditions do not apply combat buffs.");

                fixture.Hud.ShowExplanation(recovery, false);
                AssertTargets(fixture.Hud, Array.Empty<int>(), -1);
                Assert.That(fixture.Lane(2).ConditionReady, Is.False, "Full resistance cannot recover.");
                fixture.Hud.ShowExplanation(Skill(10, 0), false);
                AssertTargets(fixture.Hud, Array.Empty<int>(), -1);
                fixture.Hud.ShowExplanation(draw, false);
                fixture.Hud.ShowExplanation(duel.EnemyQueue[2], true);
                AssertTargets(fixture.Hud, Array.Empty<int>(), -1, "Enemy inspection has no reverse condition matching.");
                Assert.That(fixture.Hint.transform.parent.gameObject.activeSelf, Is.False,
                    "Enemy inspection replaces the player's popup rather than leaving two explanations visible.");
                Assert.That(fixture.Effect(false).gameObject.activeSelf, Is.False);
                fixture.Hud.ShowExplanation(blocking, false);
                fixture.Hud.HideExplanation();
                AssertTargets(fixture.Hud, Array.Empty<int>(), -1);
                Assert.That(fixture.Lane(1).gameObject.activeSelf, Is.False);
                fixture.Hud.ShowExplanation(blocking, false);
                duel.Commit(); fixture.Hud.EndTurn();
                fixture.Refresh(duel);
                AssertTargets(fixture.Hud, Array.Empty<int>(), -1);
                fixture.Hud.Reset();
                foreach (SkillCardFeedbackGraphic view in fixture.Views)
                    Assert.That(view.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SelfRecoveryCondition_EmphasizesTheHeldEffectWithoutMatchingAnyEnemyCard()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var recovery = Skill(19, 2, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                var duel = Duel(new[] { Skill(100, 0), recovery }, new[] { Skill(900, 0) });
                Assert.That(duel.TryQueueLane(0), Is.True);
                duel.Commit(); duel.ResolveNextSlot(); duel.BeginNextTurn();
                Assert.That(duel.Player.Resistance, Is.LessThan(duel.Player.MaxResistance));
                fixture.Hud.BeginTurn(); fixture.Refresh(duel);
                fixture.Hud.ShowExplanation(recovery, false);
                Assert.That(fixture.Lane(2).ConditionReady, Is.True);
                Assert.That(fixture.Effect(false).ConditionReady, Is.True);
                Assert.That(fixture.Hint.text, Does.Contain("현재 저항: 회복 가능"));
                AssertTargets(fixture.Hud, Array.Empty<int>(), -1);
                Assert.That(fixture.Effect(false).raycastTarget, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ActualConditionalEffects_EmphasizeOnlyChangesThatActuallyOccurred()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var guard = Skill(7, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                foreach (bool matches in new[] { true, false })
                {
                    var duel = Duel(new[] { guard }, new[] { Skill(900, 0, property: matches
                        ? LegacySkillProperty.Hit : LegacySkillProperty.Slash) });
                    Assert.That(duel.TryQueueLane(0), Is.True); duel.Commit();
                    LegacyCurrentSlot slot = duel.BeginNextSlot(); fixture.ShowSlot(duel, slot);
                    Assert.That(fixture.Queue(true, 0).ConditionReady, Is.EqualTo(matches));
                    Assert.That(fixture.Queue(true, 0).EffectActivated, Is.EqualTo(matches));
                    Assert.That(fixture.Queue(true, 0).HasBuffParticles, Is.False,
                        "Conditional ACT recovery is an effect, not an active power/protection buff.");
                    fixture.Hud.ShowExplanation(guard, false);
                    Assert.That(fixture.Effect(false).EffectActivated, Is.EqualTo(matches));
                }

                var recovery = Duel(new[] { Skill(19, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence) },
                    new[] { Skill(900, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence) });
                Assert.That(recovery.TryQueueLane(0), Is.True); recovery.Commit();
                fixture.ShowSlot(recovery, recovery.BeginNextSlot());
                Assert.That(fixture.Queue(true, 0).EffectActivated, Is.False,
                    "A capped zero-point restoration has no actual recovery effect.");

                foreach (bool matches in new[] { true, false })
                {
                    var enemyGuard = Duel(new[] { Skill(100, 0, property: matches
                        ? LegacySkillProperty.Hit : LegacySkillProperty.Slash) }, new[] { guard });
                    Assert.That(enemyGuard.TryQueueLane(0), Is.True); enemyGuard.Commit();
                    LegacyCurrentSlot enemySlot = enemyGuard.BeginNextSlot(); fixture.ShowSlot(enemyGuard, enemySlot);
                    Assert.That(enemySlot.EnemyFeedback.ConditionMet, Is.EqualTo(matches));
                    Assert.That(fixture.Queue(false, 0).ConditionReady, Is.EqualTo(matches),
                        "An actual matching condition remains visible even when it grants no opponent ACT effect.");
                    Assert.That(fixture.Queue(false, 0).EffectActivated, Is.False,
                        "Matching a condition cannot imply an ACT effect for the opponent, which has no ACT pool.");
                }

                foreach (bool matches in new[] { true, false })
                {
                    var enemyDraw = new LegacyQueuedDuel(1000, 0, 1000, 1000,
                        new[] { matches ? guard : Skill(100, 0) }, new[] { Skill(42, 0) }, new[] { 1 });
                    Assert.That(enemyDraw.TryQueueLane(0), Is.True); enemyDraw.Commit();
                    LegacyCurrentSlot drawSlot = enemyDraw.BeginNextSlot(); fixture.ShowSlot(enemyDraw, drawSlot);
                    Assert.That(fixture.Queue(false, 0).ConditionReady, Is.EqualTo(matches),
                        "The opponent's guard condition is independent of whether any resistance can be removed.");
                    Assert.That(fixture.Queue(false, 0).EffectActivated, Is.False,
                        "Zero remaining resistance produces no direct-reduction effect or opponent ACT recovery.");
                }
            }
        }

        [UnityTest]
        public IEnumerator ActualPowerBuff_FollowsItsSlotThroughAllHitsAndReusesTheSameBoundedMesh()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var duel = Duel(new[] { Skill(10, 0, property: LegacySkillProperty.Hit), Skill(100, 0, hits: 2), Skill(101, 0) },
                    new[] { Skill(900, 0), Skill(901, 0), Skill(902, 0) });
                QueueThree(duel); duel.Commit();
                fixture.ShowSlot(duel, duel.BeginNextSlot());
                Assert.That(fixture.Queue(true, 0).EffectActivated, Is.True);
                Assert.That(fixture.Queue(true, 0).HasBuffParticles, Is.False, "Ready buffs the following slot.");
                duel.ResolveNextSlot();
                LegacyCurrentSlot boosted = duel.BeginNextSlot(); fixture.ShowSlot(duel, boosted);
                SkillCardFeedbackGraphic view = fixture.Queue(true, 1);
                Assert.That(view.PowerBuffPercent, Is.EqualTo(30));
                Assert.That(view.ActiveSparkCount, Is.EqualTo(SkillCardFeedbackGraphic.MaximumSparks));
                Assert.That(view.raycastTarget, Is.False);
                int nodeCount = fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length;
                {
                    Canvas.ForceUpdateCanvases();
                    Mesh rendered = view.canvasRenderer.GetMesh();
                    Assert.That(rendered, Is.Not.Null);
                    Vector3[] before = rendered.vertices;
                    Assert.That(before.Length, Is.InRange(24, SkillCardFeedbackGraphic.MaximumVertices));
                    duel.ResolveNextHit();
                    Assert.That(boosted.IsResolved, Is.False);
                    fixture.Refresh(duel, .7f, 0f);
                    rendered = view.canvasRenderer.GetMesh();
                    Assert.That(rendered, Is.Not.Null);
                    Assert.That(rendered.vertices, Is.Not.EqualTo(before), "Sparks use the supplied real clock even when the combat clock is paused.");
                    Assert.That(view.HasBuffParticles, Is.True);
                    for (int frame = 0; frame < 40; frame++) fixture.Refresh(duel, .025f, 0f);
                    Assert.That(fixture.Queue(true, 1), Is.SameAs(view));
                    Assert.That(fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                }
                duel.ResolveNextHit();
                fixture.Refresh(duel);
                Assert.That(view.HasBuffParticles, Is.True, "The final hit still owns its visible card until completion.");
                duel.CompleteCurrentSlot(); fixture.Refresh(duel);
                Assert.That(view.gameObject.activeSelf, Is.False);
                fixture.ShowSlot(duel, duel.BeginNextSlot());
                Assert.That(fixture.Queue(true, 2).HasBuffParticles, Is.False, "A one-slot bonus cannot leak into a later skill.");
                fixture.Hud.ClearSkillFeedback();
                foreach (SkillCardFeedbackGraphic feedback in fixture.Views)
                    Assert.That(feedback.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ActualProtectionBuff_HasSparksWhileVulnerabilityAndCommonBreathingDoNot()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                foreach (int buffId in new[] { 8, 12 })
                {
                    LegacySkill buffSkill = buffId == 8
                        ? Skill(8, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence)
                        : Skill(12, 0, property: LegacySkillProperty.Penetrate, hits: 2);
                    var duel = Duel(new[] { buffSkill, Skill(100, 0) },
                        new[] { Skill(900, 0), Skill(901, 0), Skill(902, 0) });
                    Assert.That(duel.TryQueueLane(0), Is.True);
                    Assert.That(duel.TryQueueLane(0), Is.True);
                    Assert.That(duel.TryQueueBreath(), Is.True);
                    duel.Commit(); duel.ResolveNextSlot();
                    fixture.ShowSlot(duel, duel.BeginNextSlot());
                    Assert.That(fixture.Queue(true, 1).ProtectionBuffPercent, Is.EqualTo(buffId == 8 ? 30 : 0));
                    Assert.That(fixture.Queue(true, 1).HasBuffParticles, Is.EqualTo(buffId == 8));
                    fixture.Hud.HideExplanation();
                    Assert.That(fixture.Queue(true, 1).HasBuffParticles, Is.EqualTo(buffId == 8),
                        "Closing inspection cannot remove an actual combat buff.");
                    duel.ResolveNextSlot();
                    LegacyCurrentSlot breathing = duel.BeginNextSlot(); fixture.ShowSlot(duel, breathing);
                    Assert.That(fixture.Queue(true, 2).HasBuffParticles, Is.False,
                        "Common breathing is an idle action even while the fighter retains protection.");
                    fixture.Hud.Reset();
                    foreach (SkillCardFeedbackGraphic view in fixture.Views)
                        Assert.That(view.gameObject.activeSelf, Is.False);
                }
            }
        }

        [UnityTest]
        public IEnumerator EnemyBuffRows_ShowNewlyGrantedDurationThenTheBuffAppliedToTheFollowingSkill()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                foreach (int id in new[] { 3, 8, 9, 10 })
                {
                    int amount = id == 3 ? 10 : id == 9 ? 3 : 30;
                    int duration = id == 3 ? 3 : id == 10 ? 1 : 10;
                    bool protection = id == 8;
                    var source = Skill(id, 0, id == 8 || id == 9 ? LegacySkillKind.Defence : LegacySkillKind.Attack);
                    var guard = Skill(100, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                    var duel = Duel(new[] { guard }, new[] { source, guard, guard });
                    QueueThree(duel); duel.Commit();
                    fixture.ShowSlot(duel, duel.BeginNextSlot());
                    Text granted = fixture.Buff(false, protection ? "Granted Protection Buff" : "Granted Power Buff");
                    Assert.That(granted.text, Is.EqualTo(protection
                        ? $"다음 {duration}칸 피해 감소 {amount}%" : $"다음 {duration}칸 위력 +{amount}%"));
                    Assert.That(granted.gameObject.activeInHierarchy, Is.True);
                    Assert.That(fixture.Queue(false, 0).HasBuffParticles, Is.False,
                        "Receiving a future buff must not label the granting skill's own power as boosted.");
                    Assert.That(fixture.Buff(false, "Active Power Buff").gameObject.activeInHierarchy, Is.False);
                    Assert.That(fixture.Buff(false, "Active Protection Buff").gameObject.activeInHierarchy, Is.False);
                    int nodes = fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length;

                    duel.ResolveNextSlot(); fixture.ShowSlot(duel, duel.BeginNextSlot());
                    Text applied = fixture.Buff(false, protection ? "Active Protection Buff" : "Active Power Buff");
                    Assert.That(applied.text, Is.EqualTo(protection
                        ? $"현재 피해 감소 {amount}%" : $"현재 위력 +{amount}%"));
                    Assert.That(applied.gameObject.activeInHierarchy, Is.True);
                    Assert.That(granted.gameObject.activeInHierarchy, Is.False);
                    Assert.That(fixture.Queue(false, 1).HasBuffParticles, Is.True);
                    fixture.Hud.HideExplanation();
                    fixture.Refresh(duel, .5f, 0f);
                    Assert.That(applied.gameObject.activeInHierarchy, Is.True);
                    Assert.That(fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                    if (id == 10)
                    {
                        duel.ResolveNextSlot(); fixture.ShowSlot(duel, duel.BeginNextSlot());
                        Assert.That(applied.gameObject.activeInHierarchy, Is.False, "The one-slot Ready bonus has expired.");
                        Assert.That(fixture.Queue(false, 2).HasBuffParticles, Is.False);
                    }
                    fixture.Hud.Reset(); AssertNoBuffRows(fixture, false);
                    Assert.That(fixture.Status(false).sizeDelta.y, Is.EqualTo(92));
                }
            }
        }

        [UnityTest]
        public IEnumerator BothFighters_ShowIndependentAppliedAndGrantedBuffsAndClearOnTurnOrRestart()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var guard = Skill(100, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                var duel = Duel(new[] { Skill(3, 0), guard },
                    new[] { Skill(8, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence), guard });
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True); duel.Commit();
                fixture.ShowSlot(duel, duel.BeginNextSlot());
                Assert.That(fixture.Buff(true, "Granted Power Buff").text, Is.EqualTo("다음 3칸 위력 +10%"));
                Assert.That(fixture.Buff(false, "Granted Protection Buff").text, Is.EqualTo("다음 10칸 피해 감소 30%"));
                Assert.That(fixture.Buff(true, "Granted Protection Buff").gameObject.activeInHierarchy, Is.False);
                Assert.That(fixture.Buff(false, "Granted Power Buff").gameObject.activeInHierarchy, Is.False);
                duel.ResolveNextSlot(); fixture.ShowSlot(duel, duel.BeginNextSlot());
                Assert.That(fixture.Buff(true, "Active Power Buff").text, Is.EqualTo("현재 위력 +10%"));
                Assert.That(fixture.Buff(false, "Active Protection Buff").text, Is.EqualTo("현재 피해 감소 30%"));
                fixture.Hud.BeginTurn();
                AssertNoBuffRows(fixture, true); AssertNoBuffRows(fixture, false);
                fixture.Hud.SetSkillFeedback(duel.CurrentSlot); fixture.Refresh(duel);
                Assert.That(fixture.Buff(false, "Active Protection Buff").gameObject.activeInHierarchy, Is.True);
                fixture.Hud.Reset(); AssertNoBuffRows(fixture, true); AssertNoBuffRows(fixture, false);
            }
        }

        [UnityTest]
        public IEnumerator VulnerabilityAndBreathing_DoNotShowBeneficialBuffRowsOnEitherFighter()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                foreach (int id in new[] { 8, 12 })
                {
                    var source = Skill(id, 0, id == 8 ? LegacySkillKind.Defence : LegacySkillKind.Attack);
                    var duel = Duel(new[] { source }, new[] { source, LegacyCommonActions.Breathe });
                    Assert.That(duel.TryQueueLane(0) && duel.TryQueueBreath(), Is.True); duel.Commit();
                    fixture.ShowSlot(duel, duel.BeginNextSlot());
                    if (id == 12) { AssertNoBuffRows(fixture, true); AssertNoBuffRows(fixture, false); }
                    duel.ResolveNextSlot(); fixture.ShowSlot(duel, duel.BeginNextSlot());
                    AssertNoBuffRows(fixture, true); AssertNoBuffRows(fixture, false);
                    Assert.That(fixture.Queue(true, 1).HasBuffParticles, Is.False);
                    Assert.That(fixture.Queue(false, 1).HasBuffParticles, Is.False);
                }
            }
        }

        [UnityTest]
        public IEnumerator MultipleBuffRows_StayReadableKeepGaugeAnchorsAndReuseNonBlockingObjects()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var flow = Skill(8, 0, LegacySkillKind.Defence, LegacySkillProperty.Defence);
                var duel = Duel(new[] { Skill(3, 0), flow, flow }, new[] { Skill(3, 0), flow, flow });
                QueueThree(duel); duel.Commit();
                fixture.ShowSlot(duel, duel.BeginNextSlot());
                int nodes = fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length;
                var before = new Vector3[4]; var after = new Vector3[4];
                fixture.Status(false).GetWorldCorners(before);
                float originalGap = fixture.Status(false).Find("HP").position.y - before[0].y;
                duel.ResolveNextSlot(); duel.ResolveNextSlot();
                fixture.ShowSlot(duel, duel.BeginNextSlot());
                foreach (bool player in new[] { true, false })
                {
                    Assert.That(fixture.Status(player).sizeDelta.y, Is.EqualTo(164));
                    foreach (string rowName in BuffRowNames)
                    {
                        Text value = fixture.Buff(player, rowName);
                        if (!value.gameObject.activeInHierarchy) continue;
                        Assert.That(value.preferredWidth, Is.LessThanOrEqualTo(value.rectTransform.rect.width), value.text);
                        Assert.That(value.raycastTarget, Is.False);
                        Assert.That(value.transform.parent.GetComponent<Image>().raycastTarget, Is.False);
                    }
                }
                fixture.Status(false).GetWorldCorners(after);
                Assert.That(fixture.Status(false).Find("HP").position.y - after[0].y, Is.EqualTo(originalGap).Within(.01f));
                for (int frame = 0; frame < 40; frame++) fixture.Refresh(duel, .025f, 0f);
                Assert.That(fixture.Hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                fixture.Hud.ClearSkillFeedback();
                AssertNoBuffRows(fixture, true); AssertNoBuffRows(fixture, false);
                Assert.That(fixture.Status(false).sizeDelta.y, Is.EqualTo(92));
            }
        }

        private static readonly string[] BuffRowNames =
            { "Active Power Buff", "Active Protection Buff", "Granted Power Buff", "Granted Protection Buff" };

        private static void AssertNoBuffRows(Fixture fixture, bool player)
        {
            foreach (string name in BuffRowNames)
                Assert.That(fixture.Buff(player, name).gameObject.activeInHierarchy, Is.False, name);
        }

        private static void AssertTargets(LegacyCombatHud hud, int[] candidates, int ready, string reason = null)
        {
            Transform queue = hud.Root.transform.Find("Enemy Requests");
            int index = 0;
            foreach (Transform card in queue)
            {
                if (card.name != "Queued Skill") continue;
                var view = card.Find("Skill Condition Feedback").GetComponent<SkillCardFeedbackGraphic>();
                Assert.That(view.ConditionTarget, Is.EqualTo(Array.IndexOf(candidates, index) >= 0), reason ?? "Wrong opponent candidate at " + index);
                Assert.That(view.ConditionReady, Is.EqualTo(index == ready), "Wrong exact-slot marker at " + index);
                index++;
            }
        }

        private static LegacySkill Skill(int id, int lane, LegacySkillKind kind = LegacySkillKind.Attack,
            LegacySkillProperty property = LegacySkillProperty.Slash, int hits = 1)
            => new LegacySkill(id, "Feedback " + id, 0, 2, 2, kind, property, hits, lane, "피드백 조건 설명", iconId: 1);

        private static LegacyQueuedDuel Duel(LegacySkill[] player, LegacySkill[] enemy)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000, player, enemy, new[] { enemy.Length });

        private static void QueueThree(LegacyQueuedDuel duel)
        {
            for (int count = 0; count < 3; count++) Assert.That(duel.TryQueueLane(0), Is.True);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject owner = new GameObject("Skill Card Feedback Fixture");
            private readonly Camera camera;
            private readonly Transform player, enemy;
            public readonly LegacyCombatHud Hud;
            public SkillCardFeedbackGraphic[] Views => Hud.Root.GetComponentsInChildren<SkillCardFeedbackGraphic>(true);
            public Text Hint => Named("Skill Explain").Find("Explanation Hint").GetComponent<Text>();

            public Fixture()
            {
                camera = new GameObject("Feedback Camera").AddComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true; camera.orthographicSize = 6f; camera.enabled = false;
                player = new GameObject("Feedback Player").transform;
                enemy = new GameObject("Feedback Enemy").transform;
                player.SetParent(owner.transform, false); enemy.SetParent(owner.transform, false);
                player.position = Vector3.left * 2f; enemy.position = Vector3.right * 2f;
                Hud = new LegacyCombatHud(owner.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            public void Refresh(LegacyQueuedDuel duel, float realDelta = 0f, float delta = 0f)
            {
                Hud.Refresh(duel, 10f, duel.Phase == LegacyDuelPhase.Resolving,
                    duel.CurrentSlot != null ? duel.CurrentSlot.SlotIndex : 0,
                    camera, player, enemy, delta, realDelta);
                Canvas.ForceUpdateCanvases();
            }

            public void ShowSlot(LegacyQueuedDuel duel, LegacyCurrentSlot slot)
            {
                Hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill);
                Hud.SetSkillFeedback(slot);
                Refresh(duel);
            }

            public SkillCardFeedbackGraphic Queue(bool playerSide, int index)
                => Hud.GetQueuedSkillAnchor(playerSide, index).Find("Skill Condition Feedback").GetComponent<SkillCardFeedbackGraphic>();
            public SkillCardFeedbackGraphic Lane(int index)
                => Named("Current " + "QWE"[index]).Find("Lane Condition Feedback").GetComponent<SkillCardFeedbackGraphic>();
            public SkillCardFeedbackGraphic Effect(bool enemySide)
                => Named(enemySide ? "Enemy Skill Explain" : "Skill Explain").GetComponentsInChildren<SkillCardFeedbackGraphic>(true)[0];
            public RectTransform Status(bool playerSide) => (RectTransform)Named(playerSide ? "PlayerStatus" : "EnemyStatus");
            public Text Buff(bool playerSide, string name) => Status(playerSide).Find(name).GetComponentInChildren<Text>(true);
            private Transform Named(string name)
            {
                foreach (Transform node in Hud.Root.GetComponentsInChildren<Transform>(true))
                    if (node.name == name) return node;
                throw new InvalidOperationException("Missing " + name);
            }
            public void Dispose() { Hud.Dispose(); Object.Destroy(owner); }
        }
    }
}
