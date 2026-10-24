using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ForecastHiddenQueuePlayModeTests
    {
        [UnityTest]
        public IEnumerator Versus_ForecastAndDodgePreviewsStopBeforeTheFirstHiddenOpponent()
        {
            yield return null;
            LegacySkill visibleAttack = Attack(89000, "공개 공격", 0);
            LegacySkill hiddenDodge = Attack(54, "숨긴 회피 기술", 2);
            LegacySkill ownAttack = Attack(89001, "내 공격", 0);
            LegacySkill ownDodge = Attack(54, "내 회피", 2);
            var match = new LocalVersusMatch(new[] { visibleAttack, hiddenDodge }, new[] { ownAttack, ownDodge },
                leftHealth: 1000, leftResistance: 1000, rightHealth: 1000, rightResistance: 1000);
            Assert.That(match.TryQueueLane(0, 0) && match.TryQueueLane(0, 0) && match.TryQueueLane(0, 2) &&
                match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0) && match.TryQueueLane(1, 2), Is.True);
            var view = new LocalVersusPerspective(match, 1);
            var raw = new List<LegacySlotForecast>();
            view.ForecastTurn(raw);
            Assert.That(raw.Count, Is.EqualTo(3), "The deterministic rule forecast remains exact.");

            using (var fixture = new HudFixture())
            {
                fixture.Refresh(view);
                LegacyCombatHud hud = fixture.Hud;
                Assert.That(hud.SlotForecast.Count, Is.EqualTo(2));
                Assert.That(hud.SlotForecast[0].Summary, Is.EqualTo(raw[0].Summary));
                Assert.That(hud.SlotForecast[1].Summary, Is.EqualTo(raw[1].Summary));
                Assert.That(hud.GetForecastVerdict(0), Is.Not.Null);
                Assert.That(hud.GetForecastVerdict(1), Is.Not.Null);
                Assert.That(hud.IsEnemyCardDodged(1), Is.True, "The revealed opponent keeps its automatic-dodge preview.");

                hud.ShowExplanation(ownDodge, false);
                Assert.That(hud.ForecastNote, Is.Null, "A held skill facing the concealed third reservation gets no damage/verdict line.");
                Assert.That(LegacyKeywordPreview.Note(view, ownDodge), Does.Not.Contain(hiddenDodge.Name));
                Assert.That(LegacyKeywordPreview.QueueMark(view, false, 2, true).IsEmpty, Is.True,
                    "The hidden technique's automatic-dodge effect is not identified by its badge.");
                hud.SetInspectedSlot(2);
                hud.ShowQueueComparison(hiddenDodge, null);
                Assert.That(hud.EnemyForecastNote, Is.Null, "Tab cannot forecast the concealed slot.");

                Assert.That(match.TryQueueLane(1, 2), Is.True);
                fixture.Refresh(view);
                Assert.That(hud.GetForecastVerdict(2), Is.Null);
                Assert.That(hud.IsEnemyCardDodged(2), Is.False);
                Assert.That(hud.SlotForecast.Count, Is.EqualTo(2), "The public list never exposes the concealed skill object.");

                Assert.That(match.TryCommit(1), Is.True);
                fixture.Refresh(view, true);
                Assert.That(hud.SlotForecast, Is.Empty, "Planning previews do not remain in the public list during combat.");
            }
        }

        [UnityTest]
        public IEnumerator VisibleCyclingTechnique_DoesNotForecastAConcealedLaterFeed()
        {
            yield return null;
            LegacySkill cycle = Attack(46, "공개 순환", 1);
            LegacySkill attack = Attack(89002, "다른 공격", 0);
            var match = new LocalVersusMatch(new[] { cycle, attack }, new[] { attack },
                leftHealth: 1000, leftResistance: 1000, rightHealth: 1000, rightResistance: 1000);
            Assert.That(match.TryQueueLane(0, 1) && match.TryQueueLane(0, 0) && match.TryQueueLane(0, 0) &&
                match.TryCommit(0) && match.TryQueueLane(1, 0), Is.True);
            var view = new LocalVersusPerspective(match, 1);
            Assert.That(view.IsEnemySkillRevealed(0), Is.True, "The cycling technique's identity is public.");
            using (var fixture = new HudFixture())
            {
                fixture.Refresh(view);
                Assert.That(fixture.Hud.SlotForecast, Is.Empty);
                Assert.That(fixture.Hud.GetForecastVerdict(0), Is.Null);
                fixture.Hud.SetInspectedSlot(0);
                fixture.Hud.ShowQueueComparison(cycle, attack);
                Assert.That(fixture.Hud.EnemyForecastNote, Is.Null);
                Assert.That(LegacyKeywordPreview.CycleCount(view, false, 0, true, out bool raised, out bool capped), Is.Null);
                Assert.That(raised || capped, Is.False, "No raised/capped visual can disclose a later concealed feeder.");
            }
        }

        [UnityTest]
        public IEnumerator Campaign_StillForecastsPastTheSecondOpponentReservation()
        {
            yield return null;
            LegacySkill attack = Attack(89003, "캠페인 공격", 0);
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { attack },
                new EnemyScript(new[] { new[] { attack, attack, attack } }), 3);
            Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
            using (var fixture = new HudFixture())
            {
                fixture.Refresh(duel);
                Assert.That(fixture.Hud.SlotForecast.Count, Is.EqualTo(3));
                Assert.That(fixture.Hud.GetForecastVerdict(2), Is.Not.Null);
                fixture.Hud.SetInspectedSlot(2);
                fixture.Hud.ShowQueueComparison(attack, attack);
                Assert.That(fixture.Hud.EnemyForecastNote, Is.EqualTo(duel.ForecastTurn()[2].Summary));
            }
        }

        private static LegacySkill Attack(int id, string name, int lane)
            => new LegacySkill(id, name, 0, 5, 5, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, string.Empty);

        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent = new GameObject("Hidden Queue Forecast Test");
            public LegacyCombatHud Hud { get; }

            public HudFixture()
            {
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            public void Refresh(ILegacyDuelView view, bool resolving = false)
            {
                Hud.Refresh(view, 10f, resolving, -1, null, null, null);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
            }
        }
    }
}
