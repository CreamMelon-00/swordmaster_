using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class TensionPresentationPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(69, "상대 긴장 +3")]
        [TestCase(70, "상대 긴장 +2")]
        [TestCase(71, "자기 긴장 +3")]
        [TestCase(72, "긴장 -2")]
        [TestCase(73, "긴장 위력 +10")]
        [TestCase(74, "긴장 위력 +8")]
        [TestCase(75, "자기 긴장 +4")]
        public void TensionMarks_KeepPlanningConditionalAndShowActualEffectsFromEitherSide(int id, string expected)
        {
            LegacySkill skill = Free(id);
            LocalVersusMatch match = Match(skill);
            Statuses(match.Left).GrantTension(2);
            Statuses(match.Right).GrantTension(1);
            var host = new LocalVersusPerspective(match, 0);
            Assert.That(LegacyKeywordPreview.LaneLine(host, skill), Does.Contain("사용 시").And.Contain("긴장"));
            Commit(match);
            if (id == 73 || id == 74)
                Assert.That(LegacyKeywordPreview.QueueMark(host, true, 0, true).Text,
                    Does.Contain(id == 73 ? "상대 긴장 1+ 시 +10" : "자기 긴장 1+ 시 +8"));
            match.BeginNextSlot();
            Assert.That(LegacyKeywordPreview.QueueMark(host, true, 0, false).Text, Is.EqualTo(expected));
            Assert.That(LegacyKeywordPreview.QueueMark(new LocalVersusPerspective(match, 1), false, 0, false).Text,
                Is.EqualTo(expected), "The mark remains relative to its actor on the opposite screen.");
        }

        [TestCase(73)]
        [TestCase(74)]
        public void UnmetTensionCondition_ShowsActualZeroInsteadOfThePlanningReward(int id)
        {
            var match = Match(Free(id));
            Commit(match); match.BeginNextSlot();
            LegacyKeywordMark mark = LegacyKeywordPreview.QueueMark(new LocalVersusPerspective(match, 0), true, 0, false);
            Assert.That(mark.Text, Is.EqualTo("긴장 위력 +0"));
            Assert.That(mark.Spent, Is.True);
        }

        [Test]
        public void Focus_ShowsBothNextTurnRecoveryAndSelfTension_WithoutTreatingRecoveryAsCurrentAct()
        {
            LegacySkill skill = Free(75);
            foreach (bool enemy in new[] { false, true })
            {
                SkillInfoContent content = SkillInfoContent.For(skill, enemy, enemyActRecoveryIsPlayerOnly: false);
                Assert.That(content.Description, Does.Contain("다음 턴 ACT 회복 +3").And.Contain("자신에게 긴장 4"));
            }
            LocalVersusMatch match = Match(skill);
            Commit(match);
            int currentAct = match.Left.Act;
            LegacySkillFeedback feedback = match.BeginNextSlot().LeftFeedback;
            Assert.That(feedback.ActGainGranted, Is.EqualTo(3));
            Assert.That(feedback.SelfTensionGranted, Is.EqualTo(4));
            Assert.That(match.Left.Act, Is.EqualTo(currentAct));
        }

        [Test]
        public void TensionTempoMark_RemainsConditionalDuringPreparation_AndReadsTheCompletedQueueIndex()
        {
            LegacySkill source = Free(74);
            var payoff = new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower, source.Kind,
                source.Property, source.AttackCount, 0, source.Description, durationHalfUnits: 4);
            var opponent = new LegacySkill(89813, "일반 시험", 0, 1, 1, LegacySkillKind.Attack,
                LegacySkillProperty.Slash, 1, 0, "");
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                new[] { payoff }, new[] { opponent }, new[] { 2 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            LegacyCurrentSlot preparing = duel.BeginNextSlot();
            Assert.That(preparing.PlayerQueueIndex, Is.Zero);
            Assert.That(preparing.PlayerActionSkill.Id, Is.EqualTo(74));
            Assert.That(preparing.PlayerSkill, Is.Null, "The normal technique is still preparing across the opponent's completion.");
            Assert.That(LegacyKeywordPreview.QueueMark(duel, true, 0, false).Text, Is.EqualTo("자기 긴장 1+ 시 +8"));
            duel.ResolveNextSlot();
            LegacyCurrentSlot completion = duel.BeginNextSlot();
            Assert.That(completion.PlayerSkill.Id, Is.EqualTo(74));
            Assert.That(completion.PlayerQueueIndex, Is.Zero);
            Assert.That(completion.SlotIndex, Is.Not.EqualTo(completion.PlayerQueueIndex));
            Assert.That(completion.PlayerFeedback.SelfTensionPowerBonus, Is.Zero, "No tension was present when this action began.");
            LegacyKeywordMark actual = LegacyKeywordPreview.QueueMark(duel, true, 0, false);
            Assert.That(actual.Text, Is.EqualTo("긴장 위력 +0"));
            Assert.That(actual.Spent, Is.True);
        }

        [Test]
        public void TensionGlossary_ReusesTheAttackCountDefinitionWithoutPercentageLanguage()
        {
            Assert.That(SkillKeywordHover.TryExplain("긴장", out string meaning), Is.True);
            Assert.That(meaning, Is.EqualTo(LegacyStatusNames.Description(LegacyStatusKind.Tension, 1)));
            Assert.That(meaning, Does.Contain("2타").And.Contain("단타").And.Contain("반격").And.Contain("턴"));
            Assert.That(LegacyStatusNames.Label(LegacyStatusKind.Tension, 1, 3), Is.EqualTo("긴장 3"));
            Assert.That(meaning, Does.Not.Contain("%"));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TensionChanges_InvalidateForecastAndCounterCachesWithoutAQueueChange(int owner)
        {
            LocalVersusMatch match = Match(Free(71));
            var view = new LocalVersusPerspective(match, 0);
            MethodInfo signature = typeof(LegacyCombatHud).GetMethod("CycleSignature", BindingFlags.Static | BindingFlags.NonPublic);
            int before = (int)signature.Invoke(null, new object[] { view });
            Statuses(match.GetFighter(owner)).GrantTension(1);
            Assert.That((int)signature.Invoke(null, new object[] { view }), Is.Not.EqualTo(before));
            Assert.That(match.Left.Queue, Is.Empty);
            Assert.That(match.Right.Queue, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TensionHash_DistinguishesOwnerDurationExecutionSnapshotAndEachFeedbackField(int owner)
        {
            var match = Match(Free(71));
            LocalVersusFighter fighter = match.GetFighter(owner);
            string initial = OnlineVersusProtocol.ComputeStateHash(match);
            Statuses(fighter).GrantTension(2);
            Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(initial));
            Commit(match);
            LocalVersusSlot slot = match.BeginNextSlot();
            string originalHash = OnlineVersusProtocol.ComputeStateHash(match);
            string prefix = owner == 0 ? "Left" : "Right";
            foreach (string suffix in new[] { "AttackCount", "StartingTension", "TensionActive", "TensionConsumed" })
            {
                PropertyInfo property = typeof(LocalVersusSlot).GetProperty(prefix + suffix);
                object original = property.GetValue(slot);
                property.SetValue(slot, original is bool flag ? (object)!flag : (int)original + 1);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(originalHash), prefix + suffix);
                property.SetValue(slot, original);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(originalHash));
            }
            PropertyInfo snapshot = typeof(LocalVersusSlot).GetProperty(prefix + "Feedback");
            var feedback = (LegacySkillFeedback)snapshot.GetValue(slot);
            int[] values = { feedback.SelfTensionGranted, feedback.OpponentTensionGranted, feedback.TensionRemoved,
                feedback.SelfTensionPowerBonus, feedback.OpponentTensionPowerBonus };
            MethodInfo copy = typeof(LegacySkillFeedback).GetMethod("WithTensionEffects", Private);
            for (int field = 0; field < values.Length; field++)
            {
                object[] changed = { values[0], values[1], values[2], values[3], values[4] };
                changed[field] = values[field] + 1;
                snapshot.SetValue(slot, copy.Invoke(feedback, changed));
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.Not.EqualTo(originalHash), "Feedback field " + field);
                snapshot.SetValue(slot, feedback);
                Assert.That(OnlineVersusProtocol.ComputeStateHash(match), Is.EqualTo(originalHash));
            }
        }

        [UnityTest]
        public IEnumerator TensionIcon_CombinesDurationAndDisplaysFixedMinusOneHit_WithTheSharedTooltip()
        {
            yield return null;
            var host = new GameObject("Tension status test", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                LegacyStatusStrip strip = LegacyStatusStrip.Create((RectTransform)host.transform, new LegacyDuelArt().UIFont, "Tension");
                var source = new[] { new LegacyStatusSnapshot(LegacyStatusKind.Tension, 1, 2),
                    new LegacyStatusSnapshot(LegacyStatusKind.Tension, 1, 3) };
                strip.Refresh(source);
                Assert.That(strip.Count, Is.EqualTo(1));
                Assert.That(strip.GetStatus(0).Percent, Is.EqualTo(1), "Duration stacking cannot increase the fixed one-hit reduction.");
                Assert.That(strip.GetStatus(0).RemainingCount, Is.EqualTo(5));
                Assert.That(source[0].RemainingCount, Is.EqualTo(2));
                Assert.That(strip.GetIconRoot(0).Find("Status Magnitude").GetComponent<Text>().text, Is.EqualTo("-1타"));
                Assert.That(strip.GetIconRoot(0).Find("Status Remaining").GetComponent<Text>().text, Is.EqualTo("5"));
                Assert.That(strip.GetIconRoot(0).Find("Status Symbol").GetComponent<LegacyStatusIconGraphic>(), Is.Not.Null);
                strip.ShowTooltip(0);
                Assert.That(strip.TooltipRoot.Find("Status Name").GetComponent<Text>().text,
                    Does.Contain("긴장").And.Contain("공격 기술 5개 남음"));
                Assert.That(strip.TooltipRoot.Find("Status Definition").GetComponent<Text>().text,
                    Is.EqualTo(LegacyStatusNames.Description(LegacyStatusKind.Tension, 1)));
                strip.Refresh(Array.Empty<LegacyStatusSnapshot>());
                Assert.That(strip.Root.gameObject.activeSelf, Is.False);
                Assert.That(strip.TooltipRoot.gameObject.activeSelf, Is.False);
            }
            finally { Object.Destroy(host); }
        }

        [UnityTest]
        public IEnumerator ActualAttackCount_StopsTheRemovedSwingAndRestoresTheUnmodifiedCardAfterward()
        {
            yield return null;
            var host = new GameObject("Tension swing test", typeof(RectTransform), typeof(Canvas));
            var settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            using (var art = new LegacyDuelArt())
            using (var arena = LegacyArenaView.Create(host.transform, art, settings))
            {
                try
                {
                    LegacySkill skill = new LegacySkill(89810, "세 타 공격", 0, 12, 12, LegacySkillKind.Attack,
                        LegacySkillProperty.Slash, 3, 0, "");
                    typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(arena, new object[] { 1f, 0f });
                    arena.BeginSlot(skill, null, 2, 0);
                    object actor = typeof(LegacyArenaView).GetField("player", Private).GetValue(arena);
                    FieldInfo clock = actor.GetType().GetField("AnimationTime");
                    MethodInfo frame = typeof(LegacyArenaView).GetMethod("TryGetSkillFrame", Private);
                    clock.SetValue(actor, LegacyArenaView.OriginalClipDuration * 1.5f);
                    Assert.That((bool)frame.Invoke(arena, new object[] { actor, 0f }), Is.True, "The second swing still plays.");
                    clock.SetValue(actor, LegacyArenaView.OriginalClipDuration * 2.5f);
                    Assert.That((bool)frame.Invoke(arena, new object[] { actor, 0f }), Is.False, "There is no third swing after the reduction.");
                    var view = new SkillInfoView(host.transform, art.UIFont, Vector2.zero, 334f, "Tension info");
                    view.SetSkill(skill, effectiveAttackCount: 2);
                    Assert.That(Named(view.Root, "Hits Value").GetComponent<Text>().text, Is.EqualTo("2회"));
                    view.SetSkill(skill);
                    Assert.That(Named(view.Root, "Hits Value").GetComponent<Text>().text, Is.EqualTo("3회"));
                    Assert.That(skill.AttackCount, Is.EqualTo(3), "The technique definition and hit power divisor remain untouched.");
                }
                finally { Object.Destroy(settings); Object.Destroy(host); }
            }
        }

        [UnityTest]
        public IEnumerator TensionCues_ReportTheActualAffectedSideAndConditionalPower()
        {
            yield return null;
            var host = new GameObject("Tension cue test", typeof(RectTransform), typeof(Canvas));
            using (var art = new LegacyDuelArt())
            using (var cue = new DuelSkillActivationCue(host.transform, art.UIFont))
            {
                try
                {
                    foreach (int id in new[] { 69, 70, 71, 72, 73, 74, 75 })
                    {
                        LegacySkill skill = Free(id);
                        LocalVersusMatch match = Match(skill);
                        Statuses(match.Left).GrantTension(2); Statuses(match.Right).GrantTension(1);
                        Commit(match); LegacySkillFeedback feedback = match.BeginNextSlot().LeftFeedback;
                        Assert.That(cue.Show(true, skill, feedback), Is.True, skill.Name);
                        string own = id == 69 ? "상대 긴장 +3" : id == 70 ? "상대 긴장 +2" : id == 71 ? "내 긴장 +3"
                            : id == 72 ? "내 긴장 -2" : id == 75 ? "내 긴장 +4" : "긴장 조건 · 기본 위력 +" + (id == 73 ? 10 : 8);
                        string ownDetail = Named(host, "Player Skill Activation Cue").Find("Applied Effect").GetComponent<Text>().text;
                        Assert.That(ownDetail, Does.Contain(own));
                        if (id == 75) Assert.That(ownDetail, Does.Contain("다음 턴 ACT 회복 +3"));
                        cue.Reset();
                        Assert.That(cue.Show(false, skill, feedback), Is.True, skill.Name);
                        string other = id == 69 ? "내 긴장 +3" : id == 70 ? "내 긴장 +2" : id == 71 ? "상대 긴장 +3"
                            : id == 72 ? "상대 긴장 -2" : id == 75 ? "상대 긴장 +4" : own;
                        string otherDetail = Named(host, "Enemy Skill Activation Cue").Find("Applied Effect").GetComponent<Text>().text;
                        Assert.That(otherDetail, Does.Contain(other));
                        if (id == 75) Assert.That(otherDetail, Does.Contain("다음 턴 ACT 회복 +3"));
                        cue.Reset();
                    }
                }
                finally { Object.Destroy(host); }
            }
        }

        [UnityTest]
        public IEnumerator TensionCurriculum_HasANamedSchoolBandAroundItsSevenNodes()
        {
            yield return null;
            var host = new GameObject("Tension curriculum test", typeof(RectTransform), typeof(Canvas));
            CampaignCurriculumHud hud = null;
            try
            {
                var run = new CampaignRun();
                var panel = new GameObject("Tension curriculum panel", typeof(RectTransform)).GetComponent<RectTransform>();
                panel.SetParent(host.transform, false); panel.sizeDelta = new Vector2(1300f, 850f);
                hud = new CampaignCurriculumHud(panel, new LegacyDuelArt(), run, null, null);
                Assert.That(CampaignCurriculumHud.BranchName(CurriculumBranch.Tension), Is.EqualTo("긴장"));
                Assert.That(CampaignCurriculumHud.BranchTone(CurriculumBranch.Tension), Is.EqualTo(LegacySkillTone.Followup));
                Assert.That(Named(hud.Root, "Branch Tension").GetComponent<Text>().text, Is.EqualTo("긴장"));
                RectTransform band = (RectTransform)Named(hud.Root, "Band Tension");
                int count = 0;
                foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
                {
                    if (node.Branch != CurriculumBranch.Tension) continue;
                    count++;
                    var card = (RectTransform)Named(hud.Root, "Curriculum Node " + node.Id);
                    Vector2 relative = band.InverseTransformPoint(card.position);
                    Assert.That(band.rect.Contains(relative), Is.True, node.Id);
                }
                Assert.That(count, Is.EqualTo(7));
                Assert.That(band.GetComponent<Image>().raycastTarget, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(host); }
        }

        private static LegacySkill Free(int id)
        {
            LegacySkill source = LegacySkillDefinitions.Skill(id);
            return new LegacySkill(id, source.Name, 0, source.MinPower, source.MaxPower, source.Kind, source.Property,
                source.AttackCount, 0, source.Description, durationHalfUnits: source.DurationHalfUnits);
        }

        private static LocalVersusMatch Match(LegacySkill skill)
            => new LocalVersusMatch(new[] { skill }, new[] { new LegacySkill(89811, "상대 공격", 0, 1, 1,
                LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "") });

        private static void Commit(LocalVersusMatch match)
        {
            while (match.Phase == LegacyDuelPhase.Planning)
            {
                int planner = match.CurrentPlanner;
                Assert.That(match.TryQueueLane(planner, 0), Is.True);
                Assert.That(match.TryCommit(planner), Is.True);
            }
        }

        private static LegacyStatusCollection Statuses(LocalVersusFighter fighter)
            => (LegacyStatusCollection)typeof(LocalVersusFighter).GetField("StatusEffects", Private).GetValue(fighter);

        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            throw new InvalidOperationException("Missing tension presentation node: " + name);
        }
    }
}
