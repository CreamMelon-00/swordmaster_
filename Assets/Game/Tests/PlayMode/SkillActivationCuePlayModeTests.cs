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
    public sealed class SkillActivationCuePlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerGuard_AnnouncesOnlyTheMatchedNextTurnActGain()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill guard = Guard(7, "막기");
                LegacyCurrentSlot matched = BeginSlot(guard, Attack(900, LegacySkillProperty.Hit));
                Assert.That(matched.PlayerFeedback.ConditionMet, Is.True);
                Assert.That(matched.PlayerFeedback.ActGainGranted, Is.EqualTo(2));
                Assert.That(fixture.Cue.Show(true, guard, matched.PlayerFeedback), Is.True);
                Assert.That(fixture.Player.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PlayerTitle.text, Is.EqualTo("막기 성공!"));
                Assert.That(fixture.PlayerDetail.text, Is.EqualTo("다음 턴 ACT 회복 +2"));
                Assert.That(fixture.Enemy.gameObject.activeSelf, Is.False);
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.False,
                    "The actor-side flash starts once the arena camera can project the fighter.");
                fixture.Tick(.02f);
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PlayerBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Shield));
                Assert.That(fixture.PlayerBurst.Progress, Is.GreaterThan(0f).And.LessThan(1f));
                AssertBurstOutsideActor(fixture, true);

                fixture.Cue.Reset();
                LegacyCurrentSlot unmatched = BeginSlot(guard, Attack(901, LegacySkillProperty.Slash));
                Assert.That(unmatched.PlayerFeedback.ConditionMet, Is.False);
                Assert.That(unmatched.PlayerFeedback.ActGainGranted, Is.Zero);
                Assert.That(fixture.Cue.Show(true, guard, unmatched.PlayerFeedback), Is.False);
                Assert.That(fixture.Player.gameObject.activeSelf, Is.False);
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.False);
                Assert.That(fixture.PlayerDetail.text, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator LocalVersusGuard_ShowsFirstAndSecondPlayerActRecovery_WithoutChangingSingleplayerCue()
        {
            yield return null;
            LegacySkill guard = LegacySkillDefinitions.Skill(7);
            LegacySkill hit = LegacySkillDefinitions.Skill(5);
            using (var fixture = new Fixture(localVersus: true))
            {
                var leftGuards = new LocalVersusMatch(new[] { guard }, new[] { hit });
                Assert.That(leftGuards.TryQueueLane(0, 0), Is.True);
                Assert.That(leftGuards.TryQueueLane(1, 2), Is.True);
                Assert.That(leftGuards.TryPass(0) && leftGuards.TryPass(1), Is.True);
                LocalVersusSlot leftSlot = leftGuards.BeginNextSlot();
                Assert.That(leftSlot.LeftFeedback.ActGainGranted, Is.EqualTo(2));
                Assert.That(fixture.Cue.Show(true, guard, leftSlot.LeftFeedback), Is.True);
                Assert.That(fixture.PlayerTitle.text, Is.EqualTo("1P 막기 성공!"));
                Assert.That(fixture.PlayerDetail.text, Is.EqualTo("다음 턴 ACT 회복 +2"));

                var rightGuards = new LocalVersusMatch(new[] { hit }, new[] { guard });
                Assert.That(rightGuards.TryQueueLane(0, 2), Is.True);
                Assert.That(rightGuards.TryQueueLane(1, 0), Is.True);
                Assert.That(rightGuards.TryPass(0) && rightGuards.TryPass(1), Is.True);
                LocalVersusSlot rightSlot = rightGuards.BeginNextSlot();
                Assert.That(rightSlot.RightFeedback.ActGainGranted, Is.EqualTo(2));
                Assert.That(fixture.Cue.Show(false, guard, rightSlot.RightFeedback), Is.True);
                Assert.That(fixture.EnemyTitle.text, Is.EqualTo("2P 막기 성공!"));
                Assert.That(fixture.EnemyDetail.text, Is.EqualTo("다음 턴 ACT 회복 +2"));
                Assert.That(fixture.EnemyDetail.text, Does.Not.Contain("내 공격을 읽음"));
            }

            using (var singleplayer = new Fixture())
            {
                LegacyCurrentSlot slot = BeginSlot(Attack(900, LegacySkillProperty.Hit), guard);
                Assert.That(singleplayer.Cue.Show(false, guard, slot.EnemyFeedback), Is.True);
                Assert.That(singleplayer.EnemyTitle.text, Is.EqualTo("상대 막기 대응!"));
                Assert.That(singleplayer.EnemyDetail.text, Is.EqualTo("내 공격을 읽음"));
            }
        }

        [UnityTest]
        public IEnumerator OpeningESkill_ShowsAnAppliedResistanceCutOnlyAgainstAnAttack()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill response = LegacySkillDefinitions.Skill(5);
                Assert.That(response.Name, Is.EqualTo("기세 꺾기"));
                Assert.That(response.LaneIndex, Is.EqualTo(2));
                var duel = new LegacyQueuedDuel(100, 100, 100, 100,
                    new[] { response }, new[] { Attack(900, LegacySkillProperty.Slash) }, new[] { 1 });
                Assert.That(duel.TryQueueLane(2), Is.True);
                duel.Commit();
                LegacyCurrentSlot matched = duel.BeginNextSlot();
                Assert.That(matched.PlayerFeedback.ConditionMet, Is.True);
                Assert.That(matched.PlayerFeedback.OpponentResistanceReduced, Is.EqualTo(5));
                Assert.That(fixture.Cue.Show(true, response, matched.PlayerFeedback), Is.True);
                Assert.That(fixture.PlayerTitle.text, Is.EqualTo("기세 꺾기 성공!"));
                Assert.That(fixture.PlayerDetail.text, Is.EqualTo("상대 저항 -5"));
                fixture.Tick(.02f);
                Assert.That(fixture.PlayerBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Strike));
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.True);

                fixture.Cue.Reset();
                var blocked = new LegacyQueuedDuel(100, 100, 100, 100,
                    new[] { response }, new[] { Guard(901, "방어") }, new[] { 1 });
                Assert.That(blocked.TryQueueLane(2), Is.True);
                blocked.Commit();
                LegacyCurrentSlot unmatched = blocked.BeginNextSlot();
                Assert.That(unmatched.PlayerFeedback.ConditionMet, Is.False);
                Assert.That(unmatched.PlayerFeedback.OpponentResistanceReduced, Is.Zero);
                Assert.That(fixture.Cue.Show(true, response, unmatched.PlayerFeedback), Is.False);
                Assert.That(fixture.Player.gameObject.activeSelf, Is.False);

                var enemyDuel = new LegacyQueuedDuel(100, 100, 100, 100,
                    new[] { Attack(902, LegacySkillProperty.Slash) }, new[] { response }, new[] { 1 });
                Assert.That(enemyDuel.TryQueueLane(0), Is.True);
                enemyDuel.Commit();
                LegacyCurrentSlot enemyMatched = enemyDuel.BeginNextSlot();
                Assert.That(enemyMatched.EnemyFeedback.OpponentResistanceReduced, Is.EqualTo(5));
                Assert.That(fixture.Cue.Show(false, response, enemyMatched.EnemyFeedback), Is.True);
                Assert.That(fixture.EnemyTitle.text, Is.EqualTo("상대 기세 꺾기 성공!"));
                Assert.That(fixture.EnemyDetail.text, Is.EqualTo("내 저항 -5"));
                fixture.Tick(.02f);
                Assert.That(fixture.EnemyBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Danger));
            }
        }

        [UnityTest]
        public IEnumerator EnemyGuard_MatchedResponseWarnsWithoutClaimingEnemyAct()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill guard = Guard(7, "막기");
                LegacyCurrentSlot slot = BeginSlot(Attack(900, LegacySkillProperty.Hit), guard);
                Assert.That(slot.EnemyFeedback.ConditionMet, Is.True);
                Assert.That(slot.EnemyFeedback.EffectActivated, Is.False);
                Assert.That(slot.EnemyFeedback.ActGainGranted, Is.Zero);
                Assert.That(fixture.Cue.Show(false, guard, slot.EnemyFeedback), Is.True);
                Assert.That(fixture.Enemy.gameObject.activeSelf, Is.True);
                Assert.That(fixture.EnemyTitle.text, Is.EqualTo("상대 막기 대응!"));
                Assert.That(fixture.EnemyDetail.text, Is.EqualTo("내 공격을 읽음"));
                Assert.That(fixture.EnemyDetail.text, Does.Not.Contain("ACT"));
                Assert.That(fixture.EnemyTitle.color, Is.EqualTo(DuelVisualTheme.Danger));
                Assert.That(fixture.Player.gameObject.activeSelf, Is.False);
                fixture.Tick(.02f);
                Assert.That(fixture.EnemyBurst.gameObject.activeSelf, Is.True);
                Assert.That(fixture.EnemyBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Danger));
                AssertBurstOutsideActor(fixture, false);
            }
        }

        [UnityTest]
        public IEnumerator EnemyDraw_ReportsActualCappedResistanceLossAndSuppressesZeroEffect()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill draw = Attack(42, LegacySkillProperty.Slash, "쿠페");
                LegacyCurrentSlot slot = BeginSlot(Guard(900, "방어"), draw, playerResistance: 13);
                Assert.That(slot.EnemyFeedback.ConditionMet, Is.True);
                Assert.That(slot.EnemyFeedback.OpponentResistanceReduced, Is.EqualTo(13));
                Assert.That(slot.EnemyFeedback.ActGainGranted, Is.Zero);
                Assert.That(fixture.Cue.Show(false, draw, slot.EnemyFeedback), Is.True);
                Assert.That(fixture.EnemyTitle.text, Is.EqualTo("상대 쿠페 성공!"));
                Assert.That(fixture.EnemyDetail.text, Is.EqualTo("내 저항 -13"));
                Assert.That(fixture.EnemyDetail.text, Does.Not.Contain("ACT"));
                Assert.That(fixture.EnemyTitle.color, Is.EqualTo(DuelVisualTheme.Danger));
                fixture.Tick(.02f);
                Assert.That(fixture.EnemyBurst.gameObject.activeSelf, Is.True);
                Assert.That(fixture.EnemyBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Danger));

                fixture.Cue.Reset();
                LegacyCurrentSlot zero = BeginSlot(Guard(901, "방어"), draw, playerResistance: 0);
                Assert.That(zero.EnemyFeedback.ConditionMet, Is.True);
                Assert.That(zero.EnemyFeedback.EffectActivated, Is.False);
                Assert.That(zero.EnemyFeedback.OpponentResistanceReduced, Is.Zero);
                Assert.That(fixture.Cue.Show(false, draw, zero.EnemyFeedback), Is.False);
                Assert.That(fixture.Enemy.gameObject.activeSelf, Is.False);
                Assert.That(fixture.EnemyBurst.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator SelfRecovery_FullResistanceShowsNothing()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill recovery = Guard(19, "르프리즈");
                LegacyCurrentSlot slot = BeginSlot(recovery, Guard(900, "방어"));
                Assert.That(slot.PlayerFeedback.ResistanceRestored, Is.Zero);
                Assert.That(slot.PlayerFeedback.ConditionMet, Is.False);
                Assert.That(fixture.Cue.Show(true, recovery, slot.PlayerFeedback), Is.False);
                Assert.That(fixture.Player.gameObject.activeSelf, Is.False);
                fixture.Tick(.02f);
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator PlayerDrawAndRecovery_UseDifferentBurstStylesForAppliedEffects()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill draw = Attack(42, LegacySkillProperty.Slash, "쿠페");
                LegacyCurrentSlot strike = BeginSlot(draw, Guard(900, "방어"));
                Assert.That(strike.PlayerFeedback.OpponentResistanceReduced, Is.EqualTo(20));
                Assert.That(fixture.Cue.Show(true, draw, strike.PlayerFeedback), Is.True);
                fixture.Tick(.02f);
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PlayerBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Strike));

                fixture.Cue.Reset();
                LegacySkill recovery = Guard(19, "르프리즈");
                LegacySkill heavyHit = new LegacySkill(902, "강한 타격", 1, 30, 30,
                    LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 0, string.Empty);
                var recoveryDuel = new LegacyQueuedDuel(100, 100, 100, 100,
                    new[] { Attack(903, LegacySkillProperty.Slash), recovery },
                    new[] { heavyHit, Guard(901, "방어") }, new[] { 2 });
                Assert.That(recoveryDuel.TryQueueLane(0), Is.True);
                Assert.That(recoveryDuel.TryQueueLane(0), Is.True);
                recoveryDuel.Commit();
                recoveryDuel.ResolveNextSlot();
                LegacyCurrentSlot restored = recoveryDuel.BeginNextSlot();
                Assert.That(restored.PlayerFeedback.ResistanceRestored, Is.EqualTo(10));
                Assert.That(fixture.Cue.Show(true, recovery, restored.PlayerFeedback), Is.True);
                fixture.Tick(.02f);
                Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.True);
                Assert.That(fixture.PlayerBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Recovery));
                Assert.That(fixture.PlayerDetail.text, Is.EqualTo("저항 회복 +10"));
            }
        }

        [UnityTest]
        public IEnumerator BadgesStaySmallAndOutsideTheFighters()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                LegacySkill guard = Guard(7, "막기");
                LegacySkill draw = Attack(42, LegacySkillProperty.Slash, "쿠페");
                LegacyCurrentSlot playerSlot = BeginSlot(guard, Attack(900, LegacySkillProperty.Hit));
                LegacyCurrentSlot enemySlot = BeginSlot(Guard(901, "방어"), draw, playerResistance: 13);
                fixture.Cue.Show(true, guard, playerSlot.PlayerFeedback);
                fixture.Cue.Show(false, draw, enemySlot.EnemyFeedback);
                fixture.Tick(.2f);
                Canvas.ForceUpdateCanvases();

                var playerRect = (RectTransform)fixture.Player;
                var enemyRect = (RectTransform)fixture.Enemy;
                Assert.That(playerRect.sizeDelta, Is.EqualTo(new Vector2(312f, 68f)));
                Assert.That(enemyRect.sizeDelta, Is.EqualTo(new Vector2(312f, 68f)));
                Assert.That(playerRect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
                Assert.That(enemyRect.anchorMin, Is.EqualTo(new Vector2(1f, 1f)));
                Assert.That(playerRect.localScale.x, Is.LessThanOrEqualTo(1.04f));
                Assert.That(enemyRect.localScale.x, Is.LessThanOrEqualTo(1.04f));
                Vector2 playerBody = fixture.ActorScreenPoint(true);
                Vector2 enemyBody = fixture.ActorScreenPoint(false);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(playerRect, playerBody), Is.False);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(enemyRect, enemyBody), Is.False);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(playerRect, enemyBody), Is.False);
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(enemyRect, playerBody), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ActualController_ShowsMatchedGuardAndClearsAtLifecycleBoundaries()
        {
            yield return null;
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo sessionField = typeof(DuelPrototypeController).GetField("session", privateInstance);
            FieldInfo missionField = typeof(DuelPrototypeController).GetField("mission", privateInstance);
            FieldInfo guideField = typeof(DuelPrototypeController).GetField("guide", privateInstance);
            MethodInfo reset = typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", privateInstance);
            MethodInfo begin = typeof(DuelPrototypeController).GetMethod("BeginSlotAnimation", privateInstance);
            MethodInfo startPlanning = typeof(DuelPrototypeController).GetMethod("StartPlanning", privateInstance);
            Assert.That(sessionField, Is.Not.Null);
            Assert.That(missionField, Is.Not.Null);
            Assert.That(guideField, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);
            Assert.That(begin, Is.Not.Null);
            Assert.That(startPlanning, Is.Not.Null);
            bool originalEnabled = controller.enabled;
            LegacyQueuedDuel originalSession = controller.Session;
            controller.enabled = false;
            try
            {
                missionField.SetValue(controller, null);
                guideField.SetValue(controller, null);
                Transform playerCue = controller.Hud.Root.transform.Find("Skill Activation Cues/Player Skill Activation Cue");
                Transform playerBurst = controller.Hud.Root.transform.Find("Skill Activation Cues/Player Skill Activation Burst");
                Assert.That(playerCue, Is.Not.Null);
                Assert.That(playerBurst, Is.Not.Null);

                BeginMatchedControllerSlot(controller, sessionField, reset, begin, false);
                Assert.That(playerCue.gameObject.activeSelf, Is.True);
                Assert.That(playerCue.Find("Skill").GetComponent<Text>().text, Is.EqualTo("막기 성공!"));
                Assert.That(playerCue.Find("Applied Effect").GetComponent<Text>().text,
                    Is.EqualTo("다음 턴 ACT 회복 +2"));
                Assert.That(CountNamed(controller.Hud.Root.transform, "Player Skill Activation Cue"), Is.EqualTo(1));
                reset.Invoke(controller, new object[] { null, null });
                Assert.That(playerCue.gameObject.activeSelf, Is.False,
                    "ResetBattlePresentation must clear the previous activation.");
                Assert.That(playerBurst.gameObject.activeSelf, Is.False);

                LegacyQueuedDuel duel = BeginMatchedControllerSlot(controller, sessionField, reset, begin, false);
                duel.ResolveNextSlot();
                Assert.That(duel.IsTurnResolved, Is.True);
                startPlanning.Invoke(controller, null);
                Assert.That(playerCue.gameObject.activeSelf, Is.False,
                    "StartPlanning must clear a cue left visible after the final slot.");

                duel = BeginMatchedControllerSlot(controller, sessionField, reset, begin, true);
                duel.ResolveNextSlot();
                begin.Invoke(controller, null);
                Assert.That(duel.CurrentSlot.PlayerSkill.Id, Is.EqualTo(904));
                Assert.That(playerCue.gameObject.activeSelf, Is.False,
                    "A subsequent skill without a matched condition must clear the previous cue.");

                BeginMatchedControllerSlot(controller, sessionField, reset, begin, false);
                Assert.That(playerCue.gameObject.activeSelf, Is.True);
                controller.RestartMatch();
                Assert.That(playerCue.gameObject.activeSelf, Is.False,
                    "Restarting the match must clear a live activation.");
                Assert.That(playerBurst.gameObject.activeSelf, Is.False);
            }
            finally
            {
                sessionField.SetValue(controller, originalSession);
                controller.RestartMatch();
                controller.enabled = originalEnabled;
            }
        }

        [UnityTest]
        public IEnumerator RealClockAndReset_ExpireBadgesAndBurstsWithoutBlockingInputOrGrowingObjects()
        {
            yield return null;
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                using (var fixture = new Fixture())
                {
                    LegacySkill guard = Guard(7, "막기");
                    LegacySkill draw = Attack(42, LegacySkillProperty.Slash, "쿠페");
                    LegacyCurrentSlot playerSlot = BeginSlot(guard, Attack(900, LegacySkillProperty.Hit));
                    LegacyCurrentSlot enemySlot = BeginSlot(Guard(901, "방어"), draw, playerResistance: 13);
                    int nodeCount = fixture.Root.GetComponentsInChildren<Transform>(true).Length;
                    Assert.That(fixture.Root.childCount, Is.EqualTo(4));
                    for (int index = 0; index < 25; index++)
                    {
                        Assert.That(fixture.Cue.Show(true, guard, playerSlot.PlayerFeedback), Is.True);
                        Assert.That(fixture.Cue.Show(false, draw, enemySlot.EnemyFeedback), Is.True);
                        fixture.Tick(.01f);
                    }
                    Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                    Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.True);
                    Assert.That(fixture.EnemyBurst.gameObject.activeSelf, Is.True);
                    foreach (Graphic graphic in fixture.Root.GetComponentsInChildren<Graphic>(true))
                        Assert.That(graphic.raycastTarget, Is.False, graphic.name);
                    foreach (CanvasGroup group in fixture.Root.GetComponentsInChildren<CanvasGroup>(true))
                    {
                        Assert.That(group.blocksRaycasts, Is.False);
                        Assert.That(group.interactable, Is.False);
                    }

                    fixture.Tick(.66f);
                    Assert.That(fixture.Player.gameObject.activeSelf, Is.True);
                    Assert.That(fixture.Enemy.gameObject.activeSelf, Is.True);
                    Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.EnemyBurst.gameObject.activeSelf, Is.False);
                    fixture.Tick(.39f);
                    Assert.That(fixture.Player.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.Enemy.gameObject.activeSelf, Is.False);

                    fixture.Cue.Show(true, guard, playerSlot.PlayerFeedback);
                    fixture.Cue.Show(false, draw, enemySlot.EnemyFeedback);
                    fixture.Cue.Reset();
                    Assert.That(fixture.Player.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.Enemy.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.PlayerBurst.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.EnemyBurst.gameObject.activeSelf, Is.False);
                    Assert.That(fixture.PlayerTitle.text, Is.Empty);
                    Assert.That(fixture.EnemyDetail.text, Is.Empty);
                    Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                }
            }
            finally { Time.timeScale = originalTimeScale; }
        }

        [UnityTest]
        public IEnumerator StateConditions_AnnounceTheGatedRecoveryAndTheArmedMultiplier()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                // 라우다레 breaks the player, whose 20-power slash wears the enemy to 30; 베네디체레 then recovers 12.
                LegacySkill benedicere = LegacySkillDefinitions.Skill(501);
                var broken = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Fixed(900, LegacySkillKind.Attack, 20) },
                    new EnemyScript(new[] { new[] { LegacySkillDefinitions.Skill(500), benedicere } }), 3);
                Assert.That(broken.TryQueueLane(0), Is.True);
                broken.Commit();
                broken.ResolveNextSlot();
                LegacyCurrentSlot recovery = broken.BeginNextSlot();
                Assert.That(recovery.EnemyFeedback.ResistanceRestored, Is.EqualTo(12));
                Assert.That(fixture.Cue.Show(false, benedicere, recovery.EnemyFeedback), Is.True);
                Assert.That(fixture.EnemyTitle.text, Is.EqualTo("상대 베네디체레 성공!"));
                Assert.That(fixture.EnemyDetail.text, Is.EqualTo("저항 회복 +12"));

                // 프레디카레 against a player at 30% health: armed from the slot's start.
                fixture.Cue.Reset();
                LegacySkill praedicare = LegacySkillDefinitions.Skill(502);
                var low = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Fixed(901, LegacySkillKind.Attack, 1) },
                    new EnemyScript(new[] { new[] { Fixed(902, LegacySkillKind.Attack, 700), praedicare } }), 3);
                low.Commit();
                low.ResolveNextSlot();
                LegacyCurrentSlot strike = low.BeginNextSlot();
                Assert.That(strike.EnemyFeedback.ConditionalDamagePercent, Is.EqualTo(200));
                Assert.That(fixture.Cue.Show(false, praedicare, strike.EnemyFeedback), Is.True);
                Assert.That(fixture.EnemyTitle.text, Is.EqualTo("상대 프레디카레 성공!"));
                Assert.That(fixture.EnemyDetail.text, Is.EqualTo("피해 2배 (내 체력 30% 이하)"));
                fixture.Tick(.02f);
                Assert.That(fixture.EnemyBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Danger));

                // The same strike clashing with the player's slash while her resistance is whole: it takes resistance only,
                // so nothing is doubled and nothing is announced.
                fixture.Cue.Reset();
                var clash = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Fixed(901, LegacySkillKind.Attack, 1) },
                    new EnemyScript(new[] { new[] { Fixed(902, LegacySkillKind.Attack, 700), praedicare } }), 3);
                Assert.That(clash.TryQueueBreath() && clash.TryQueueLane(0), Is.True);
                clash.Commit();
                clash.ResolveNextSlot();
                Assert.That(clash.Player.Health, Is.EqualTo(300));
                LegacyCurrentSlot clashing = clash.BeginNextSlot();
                Assert.That(clashing.EnemyFeedback.ConditionalDamagePercent, Is.Zero);
                Assert.That(fixture.Cue.Show(false, praedicare, clashing.EnemyFeedback), Is.False);
                Assert.That(fixture.Enemy.gameObject.activeSelf, Is.False);

                // The same strike as the player's: nothing against a healthy enemy, a strike burst once she is at 30%.
                fixture.Cue.Reset();
                var healthy = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { praedicare },
                    new EnemyScript(new[] { new[] { Fixed(903, LegacySkillKind.Defence, 1) } }), 3);
                Assert.That(healthy.TryQueueLane(0), Is.True);
                healthy.Commit();
                LegacyCurrentSlot whole = healthy.BeginNextSlot();
                Assert.That(whole.PlayerFeedback.ConditionMet || whole.PlayerFeedback.EffectActivated, Is.False);
                Assert.That(fixture.Cue.Show(true, praedicare, whole.PlayerFeedback), Is.False, "A healthy enemy arms nothing.");
                Assert.That(fixture.Player.gameObject.activeSelf, Is.False);

                // A 701 slash through her 1-power guard leaves her at 300 of 1000.
                var weakened = new LegacyQueuedDuel(1000, 50, 1000, 50, new[] { Fixed(904, LegacySkillKind.Attack, 701), praedicare },
                    new EnemyScript(new[] { new[] { Fixed(903, LegacySkillKind.Defence, 1), Fixed(905, LegacySkillKind.Defence, 1) } }), 3);
                Assert.That(weakened.TryQueueLane(0) && weakened.TryQueueLane(0), Is.True);
                weakened.Commit();
                weakened.ResolveNextSlot();
                Assert.That(weakened.Enemy.Health, Is.EqualTo(300));
                LegacyCurrentSlot armed = weakened.BeginNextSlot();
                Assert.That(armed.PlayerFeedback.ConditionalDamagePercent, Is.EqualTo(200));
                Assert.That(fixture.Cue.Show(true, praedicare, armed.PlayerFeedback), Is.True);
                Assert.That(fixture.PlayerTitle.text, Is.EqualTo("프레디카레 성공!"));
                Assert.That(fixture.PlayerDetail.text, Is.EqualTo("피해 2배 (상대 체력 30% 이하)"));
                Assert.That(fixture.PlayerTitle.color, Is.EqualTo(DuelVisualTheme.Accent));
                fixture.Tick(.02f);
                Assert.That(fixture.PlayerBurst.Style, Is.EqualTo(DuelSkillBurstStyle.Strike));
            }
        }

        private static LegacySkill Fixed(int id, LegacySkillKind kind, int power)
            => new LegacySkill(id, "fixed", 0, power, power, kind,
                kind == LegacySkillKind.Attack ? LegacySkillProperty.Slash : LegacySkillProperty.Defence, 1, 0, string.Empty);

        private static LegacyCurrentSlot BeginSlot(LegacySkill player, LegacySkill enemy,
            int playerResistance = 100)
        {
            var duel = new LegacyQueuedDuel(100, playerResistance, 100, 100,
                new[] { player }, new[] { enemy }, new[] { 1 });
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            return duel.BeginNextSlot();
        }

        private static LegacySkill Guard(int id, string name)
            => new LegacySkill(id, name, 1, 5, 8, LegacySkillKind.Defence,
                LegacySkillProperty.Defence, 1, 0, string.Empty);

        private static LegacySkill Attack(int id, LegacySkillProperty property, string name = "공격")
            => new LegacySkill(id, name, 1, 4, 8, LegacySkillKind.Attack,
                property, 1, 0, string.Empty);

        private static LegacyQueuedDuel BeginMatchedControllerSlot(DuelPrototypeController controller,
            FieldInfo sessionField, MethodInfo reset, MethodInfo begin, bool includeTrailingSkill)
        {
            // Resolve a plain opener so the controller begins 막기 against the second enemy action.
            LegacySkill[] playerSkills = includeTrailingSkill
                ? new[] { Attack(901, LegacySkillProperty.Slash), Guard(7, "막기"), Attack(904, LegacySkillProperty.Slash) }
                : new[] { Attack(901, LegacySkillProperty.Slash), Guard(7, "막기") };
            LegacySkill[] enemySkills = includeTrailingSkill
                ? new[] { Attack(902, LegacySkillProperty.Slash), Attack(903, LegacySkillProperty.Hit),
                    Attack(905, LegacySkillProperty.Slash) }
                : new[] { Attack(902, LegacySkillProperty.Slash), Attack(903, LegacySkillProperty.Hit) };
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                playerSkills, enemySkills, new[] { enemySkills.Length });
            sessionField.SetValue(controller, duel);
            reset.Invoke(controller, new object[] { null, null });
            for (int index = 0; index < playerSkills.Length; index++)
                Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.ResolveNextSlot();
            begin.Invoke(controller, null);
            Assert.That(duel.CurrentSlot.PlayerSkill.Id, Is.EqualTo(7));
            Assert.That(duel.CurrentSlot.EnemySkill.Property, Is.EqualTo(LegacySkillProperty.Hit));
            Assert.That(duel.CurrentSlot.PlayerFeedback.ActGainGranted, Is.EqualTo(2));
            return duel;
        }

        private static int CountNamed(Transform root, string name)
        {
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) count++;
            return count;
        }

        private static void AssertBurstOutsideActor(Fixture fixture, bool playerSide)
        {
            DuelSkillActivationBurst burst = playerSide ? fixture.PlayerBurst : fixture.EnemyBurst;
            Vector2 body = fixture.ActorScreenPoint(playerSide);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, burst.rectTransform.position);
            Assert.That(playerSide ? center.x < body.x : center.x > body.x, Is.True,
                "The burst should flare outward from its fighter, leaving the sprite visible.");
        }

        private sealed class Fixture : IDisposable
        {
            private readonly LegacyDuelArt art;
            public readonly GameObject Owner;
            public readonly DuelSkillActivationCue Cue;
            public readonly Transform Root, Player, Enemy;
            public readonly DuelSkillActivationBurst PlayerBurst, EnemyBurst;
            public readonly Camera ArenaCamera;
            public readonly Transform PlayerActor, EnemyActor;
            public readonly Text PlayerTitle, PlayerDetail, EnemyTitle, EnemyDetail;

            public Fixture(bool localVersus = false)
            {
                Owner = new GameObject("Skill Activation Cue Fixture", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler));
                Owner.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = Owner.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = .5f;
                ArenaCamera = new GameObject("Skill Cue Arena Camera", typeof(Camera)).GetComponent<Camera>();
                ArenaCamera.enabled = false;
                ArenaCamera.orthographic = true;
                ArenaCamera.orthographicSize = 6f;
                ArenaCamera.transform.position = new Vector3(0f, 0f, -10f);
                PlayerActor = new GameObject("Skill Cue Player Actor").transform;
                EnemyActor = new GameObject("Skill Cue Enemy Actor").transform;
                PlayerActor.position = new Vector3(-3f, 0f, 0f);
                EnemyActor.position = new Vector3(3f, 0f, 0f);
                art = new LegacyDuelArt();
                Cue = new DuelSkillActivationCue(Owner.transform, art.UIFont, localVersus);
                Root = Owner.transform.Find("Skill Activation Cues");
                Assert.That(Root, Is.Not.Null);
                Player = Root.Find("Player Skill Activation Cue");
                Enemy = Root.Find("Enemy Skill Activation Cue");
                Assert.That(Player, Is.Not.Null);
                Assert.That(Enemy, Is.Not.Null);
                PlayerBurst = Root.Find("Player Skill Activation Burst").GetComponent<DuelSkillActivationBurst>();
                EnemyBurst = Root.Find("Enemy Skill Activation Burst").GetComponent<DuelSkillActivationBurst>();
                Assert.That(PlayerBurst, Is.Not.Null);
                Assert.That(EnemyBurst, Is.Not.Null);
                PlayerTitle = Player.Find("Skill").GetComponent<Text>();
                PlayerDetail = Player.Find("Applied Effect").GetComponent<Text>();
                EnemyTitle = Enemy.Find("Skill").GetComponent<Text>();
                EnemyDetail = Enemy.Find("Applied Effect").GetComponent<Text>();
                Canvas.ForceUpdateCanvases();
            }

            public void Tick(float realDelta) => Cue.Tick(realDelta, ArenaCamera, PlayerActor, EnemyActor);

            public Vector2 ActorScreenPoint(bool playerSide)
            {
                Transform actor = playerSide ? PlayerActor : EnemyActor;
                Vector3 offset = playerSide ? DuelStepHud.ActorBodyLocalOffset : new Vector3(-.4f, -.35f, 0f);
                return ArenaCamera.WorldToScreenPoint(actor.TransformPoint(offset));
            }

            public void Dispose()
            {
                Cue.Dispose();
                art.Dispose();
                Object.Destroy(Owner);
                Object.Destroy(ArenaCamera.gameObject);
                Object.Destroy(PlayerActor.gameObject);
                Object.Destroy(EnemyActor.gameObject);
            }
        }
    }
}
