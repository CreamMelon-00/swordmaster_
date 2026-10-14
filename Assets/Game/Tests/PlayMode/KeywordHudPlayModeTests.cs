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
    /// <summary>The effects that read the queue on the duel HUD (Docs/SkillSheet.md 예약 효과·사용 효과): the lane window's
    /// "if reserved now" line, 숨고르기's automatic count, the queued cards' keyword marks and 순환 rises, the held
    /// explanation's row, the 반환 tumble, the cues and the glossary.</summary>
    public sealed class KeywordHudPlayModeTests
    {
        private const int Q = 0, E = 2;

        [UnityTest]
        public IEnumerator CycleFeed_TheLaneSaysWhatItWouldRaise_ItsCardCarriesTheMark_AndTheCountShowsTheRise()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel(15, 53);
                fixture.Refresh(duel);
                Assert.That(hud.GetLanePreview(E), Is.EqualTo("앞 순환 0"));
                Assert.That(hud.GetLanePreview(Q), Is.Null, "A cycling skill keeps its count in the line's place.");
                Assert.That(duel.TryQueueLane(Q), Is.True);
                fixture.Refresh(duel);
                Assert.That(hud.GetLanePreview(E), Is.EqualTo("앞 순환 1"));
                Assert.That(hud.GetCycleCountLabel(true, 0), Is.EqualTo("0/2"));

                Assert.That(duel.TryQueueLane(E), Is.True);
                fixture.Refresh(duel);
                Assert.That(hud.GetKeywordMarkLabel(true, 1), Is.EqualTo("순환 +1 ×1"));
                Assert.That(hud.GetCycleCountLabel(true, 0), Is.EqualTo("0→1/2"), "The rise is written on the count.");
                Assert.That(hud.GetLanePreview(E), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn), "A second copy feeds nothing.");
                hud.ShowExplanation(duel.GetLane(E)[0], false);
                Assert.That(hud.KeywordNote, Is.EqualTo("순환 +1 · 턴당 1회 · 사용함"));
                hud.HideExplanation();

                duel.Commit();
                fixture.Refresh(duel);
                Assert.That(hud.GetKeywordMarkLabel(true, 1), Is.Null, "The feed is done once the turn is committed.");
                Assert.That(hud.GetCycleCountLabel(true, 0), Is.EqualTo("1/2"));
            }
        }

        [UnityTest]
        public IEnumerator AutoBreathAndBreathPayoff_CountTheBreaths_AndTheAutomaticOneIsTagged()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel(17, 55);
                fixture.Refresh(duel);
                Assert.That(hud.AutoBreathCount, Is.EqualTo("자동 1/1"), "A lane holds a skill that adds one.");
                Assert.That(hud.GetLanePreview(Q), Is.EqualTo("자동 숨 1/1"));
                Assert.That(hud.GetLanePreview(E), Is.EqualTo("숨 0/4 · +0"));

                Assert.That(duel.TryQueueLane(Q), Is.True);
                fixture.Refresh(duel);
                Assert.That(hud.GetKeywordMarkLabel(true, 1), Is.EqualTo(LegacyKeywordPreview.AutoBreathTag));
                Assert.That(hud.GetKeywordMarkLabel(true, 0), Is.Null, "The guard itself carries no mark.");
                Assert.That(hud.AutoBreathCount, Is.EqualTo("자동 0/1"));
                Assert.That(hud.GetLanePreview(Q), Is.EqualTo(LegacyKeywordPreview.UsedThisTurn));
                Assert.That(hud.GetLanePreview(E), Is.EqualTo("숨 1/4 · +4"));
                hud.ShowExplanation(duel.GetLane(E)[0], false);
                Assert.That(hud.KeywordNote, Is.EqualTo("숨고르기 1/4 · 지금 예약하면 위력 +4"));
                hud.HideExplanation();

                Assert.That(duel.TryQueueLane(E), Is.True);
                fixture.Refresh(duel);
                Assert.That(hud.GetKeywordMarkLabel(true, 2), Is.EqualTo("숨 +4"));
            }
        }

        [UnityTest]
        public IEnumerator AutoDodge_MarksItsCard_AndTheForecastStrikesThroughTheAttackItWillDodge()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = Duel(54);
                fixture.Refresh(duel);
                Assert.That(duel.EnemyQueue.Count, Is.EqualTo(1));
                Assert.That(hud.IsEnemyCardDodged(0), Is.False);
                Assert.That(duel.TryQueueLane(E), Is.True);
                fixture.Refresh(duel);
                Assert.That(hud.GetKeywordMarkLabel(true, 0), Is.EqualTo(LegacyKeywordPreview.AutoDodgeTag));
                Assert.That(hud.IsEnemyCardDodged(0), Is.True, "The enemy's attack in that slot will not land.");
                duel.Commit();
                fixture.Refresh(duel);
                Assert.That(hud.IsEnemyCardDodged(0), Is.False, "The strike belongs to the forecast, while planning.");
            }
        }

        [UnityTest]
        public IEnumerator ReturnedCards_TumbleOutInReturnsGreen()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyQueuedDuel duel = Duel(15, 53);
                Assert.That(duel.TryQueueLane(Q), Is.True);
                fixture.Refresh(duel);
                var before = new LegacySkill[duel.PlayerQueue.Count];
                for (int index = 0; index < before.Length; index++) before[index] = duel.PlayerQueue[index];
                fixture.Hud.PlayQueueCancellation(before, LegacyQueueRemovalMode.Return);
                Transform layer = fixture.Hud.Root.transform.Find("Queue Cancellation Layer");
                Transform returned = layer.Find("Returned Skill");
                Assert.That(returned, Is.Not.Null);
                Assert.That(returned.GetComponent<Image>().color, Is.EqualTo(DuelVisualTheme.Health));
                fixture.Hud.PlayQueueCancellation(before);
                Assert.That(layer.Find("Cancelled Skill").GetComponent<Image>().color, Is.EqualTo(DuelVisualTheme.Danger));
            }
        }

        [UnityTest]
        public IEnumerator KeywordCue_ShowsOnItsSide_AndJoinsAMatchedConditionsWords()
        {
            yield return null;
            var parent = new GameObject("Keyword Cue Test", typeof(RectTransform), typeof(Canvas));
            var art = new LegacyDuelArt();
            var cue = new DuelSkillActivationCue(parent.transform, art.UIFont);
            try
            {
                LegacySkill cadence = LegacySkillDefinitions.Skill(53);
                Assert.That(cue.ShowKeyword(true, cadence, "순환 +1 · 사선베기"), Is.True);
                Assert.That(cue.ShownTitle(true), Is.EqualTo(cadence.Name + " 발동!"));
                Assert.That(cue.ShownDetail(true), Is.EqualTo("순환 +1 · 사선베기"));
                Assert.That(cue.ShownTitle(false), Is.Null);
                Assert.That(cue.ShowKeyword(false, cadence, "순환 +1 · 사선베기"), Is.True);
                Assert.That(cue.ShownTitle(false), Is.EqualTo("상대 " + cadence.Name + " 발동!"));
                Assert.That(cue.ShowKeyword(true, cadence, null), Is.False, "Nothing to say, nothing shown.");
                cue.Reset();
                Assert.That(cue.Show(true, LegacySkillDefinitions.Skill(55), null, "숨고르기 2 · 위력 +8"), Is.True,
                    "A keyword's words show even when no condition matched.");
                Assert.That(cue.ShownDetail(true), Is.EqualTo("숨고르기 2 · 위력 +8"));
            }
            finally
            {
                cue.Dispose();
                art.Dispose();
                Object.Destroy(parent);
            }
        }

        [Test]
        public void StepHint_AnAutomaticDodgeTakesNoKey_ButPressureStays()
        {
            Assert.That(DuelStepHud.AutoDodgeHintFor(CombatFeature.All, false), Is.EqualTo("자동 회피 · D 압박"));
            Assert.That(DuelStepHud.AutoDodgeHintFor(CombatFeature.All & ~CombatFeature.Pressure, false), Is.EqualTo("자동 회피"));
            Assert.That(DuelStepHud.AutoDodgeHintFor(CombatFeature.All, true), Is.EqualTo("맞물린 기술 · 자동 회피"));
        }

        [Test]
        public void Glossary_ExplainsBreathAndDodge_AndEveryMeaningFitsThePopup()
        {
            Assert.That(SkillKeywordHover.TryExplain("숨고르기", out string breath), Is.True);
            Assert.That(breath, Does.Contain("ACT 0").And.Contain("1회 더"));
            Assert.That(SkillKeywordHover.TryExplain("회피", out string dodge), Is.True);
            Assert.That(dodge, Does.Contain("자동 회피"));
            Assert.That(SkillKeywordHover.TryExplain("순환", out string cycle), Is.True);
            Assert.That(cycle, Does.Contain("올려 줄 때").And.Contain("상한에서 멈춥니다"));
            Assert.That(SkillKeywordHover.TryExplain("일회성", out string oneShot), Is.True);
            Assert.That(oneShot, Does.Contain("예약하거나 턴이 끝나면 사라집니다"));
            Assert.That(SkillKeywordHover.TryExplain("제한시간", out string time), Is.True);
            Assert.That(time, Does.Contain("상대가 내게 닿기까지").And.Contain("시간이 멈춘 전투"));
            foreach (string term in new[] { "순환", "취소", "반환", "붕괴", "맞물림", "숨고르기", "회피", "일회성", "제한시간" })
                Assert.That(SkillKeywordHover.TryExplain(term, out _), Is.True, term);
        }

        [Test]
        public void Badges_KeepTheKeywordEffectsAuthoredWords()
        {
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(53), false).Main, Is.EqualTo("앞 순환 +1"));
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(17), false).Main, Is.EqualTo("숨고르기 +1"));
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(54), false).Main, Is.EqualTo("자동 회피"));
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(52), false).Main, Is.EqualTo("순환"),
                "A carrier's 순환 still takes the first badge.");
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(56), false).Main, Is.EqualTo("반환"));
            Assert.That(SkillInfoContent.For(LegacySkillDefinitions.Skill(56), false).Secondary, Is.EqualTo("다음 최대 ×2"),
                "A time cost is no keyword of its own: 두블레 keeps its authored second badge.");
            SkillInfoContent whetstone = SkillInfoContent.For(LegacySkillDefinitions.Skill(58), false);
            Assert.That((whetstone.Main, whetstone.MainSymbol), Is.EqualTo(("갈아내기 ×3", LegacySkillSymbol.OneShot)));
            SkillInfoContent steps = SkillInfoContent.For(LegacySkillDefinitions.Skill(57), false);
            Assert.That((steps.Main, steps.MainSymbol), Is.EqualTo(("다음 턴 +2초", LegacySkillSymbol.Time)));
            SkillInfoContent grind = SkillInfoContent.For(LegacySkillDefinitions.Skill(59), false);
            Assert.That((grind.Main, grind.Secondary, grind.SecondarySymbol), Is.EqualTo(("순환", "일회성", LegacySkillSymbol.OneShot)));
        }

        // A duel of the given sheet rows (each in its own lane) against one plain enemy attack a turn, with ACT to spare.
        private static LegacyQueuedDuel Duel(params int[] ids)
        {
            var skills = new LegacySkill[ids.Length];
            for (int index = 0; index < ids.Length; index++) skills[index] = LegacySkillDefinitions.Skill(ids[index]);
            return new LegacyQueuedDuel(1000, 50, 1000, 100, skills, new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3,
                playerActGainBonus: 7, meshPercent: 0);
        }

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent;
            private readonly DuelPresentationSettings settings;
            public LegacyCombatHud Hud { get; }

            public HudFixture()
            {
                parent = new GameObject("Keyword HUD Test");
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null, settings);
                Canvas.ForceUpdateCanvases();
            }

            public void Refresh(LegacyQueuedDuel duel)
            {
                int activeSlot = duel.Phase == LegacyDuelPhase.Planning ? -1 : duel.LastResolvedSlot + 1;
                Hud.Refresh(duel, 10f, duel.Phase != LegacyDuelPhase.Planning, activeSlot, null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
                Object.Destroy(settings);
            }
        }
    }
}
