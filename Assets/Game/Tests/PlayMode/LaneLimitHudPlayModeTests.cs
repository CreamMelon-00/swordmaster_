using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LaneLimitHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator ThreeAcceptedClicksExhaustOnlyTheirLane_AndNextTurnRestoresItsControlWithoutNewNodes()
        {
            yield return null;
            var parent = new GameObject("Lane Limit HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                var duel = Duel();
                int calls = 0;
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), lane =>
                {
                    calls++;
                    duel.TryQueueLane(lane);
                }, null, null);
                Refresh(hud, duel);
                Transform q = Lane(hud, 0), w = Lane(hud, 1);
                Button qButton = q.GetComponent<Button>();
                Text qCount = q.Find("Lane Reservations").GetComponent<Text>();
                Text wCount = w.Find("Lane Reservations").GetComponent<Text>();
                Assert.That(qButton.interactable, Is.True);
                Assert.That(w.GetComponent<Button>().interactable, Is.False, "W costs more ACT than the player has.");
                Assert.That(wCount.text, Is.EqualTo("남은 3/3"), "Short ACT does not spend a lane reservation.");
                Assert.That(wCount.color, Is.Not.EqualTo(DuelVisualTheme.Danger));
                int act = duel.Act;

                // No HUD frame between clicks: the control must read the view rather than its old enabled state.
                for (int click = 0; click < 4; click++) qButton.onClick.Invoke();
                Assert.That(calls, Is.EqualTo(LegacyLaneLimit.MaxReservations));
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(LegacyLaneLimit.MaxReservations));
                Assert.That(duel.Act, Is.EqualTo(act), "The test lane is free: exhaustion is independent of ACT.");
                Assert.That(qButton.interactable, Is.False);
                Assert.That(qCount.text, Does.Contain("0/3").And.Contain("다음 턴"));
                Assert.That(qCount.color, Is.EqualTo(DuelVisualTheme.Danger));
                Refresh(hud, duel);
                Assert.That(q.Find("Skill Image").GetComponent<Image>().color.a, Is.LessThan(.9f));
                Assert.That(Lane(hud, 2).GetComponent<Button>().interactable, Is.True, "An unused school remains available.");

                int nodes = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int frame = 0; frame < 10; frame++) Refresh(hud, duel);
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                Assert.That(qCount.raycastTarget, Is.False, "The count never takes a pointer hold away from its window.");
                var countRect = qCount.rectTransform;
                var windowRect = (RectTransform)q;
                float countY = windowRect.anchoredPosition.y + countRect.anchoredPosition.y;
                Assert.That(countY - countRect.rect.height * .5f, Is.GreaterThanOrEqualTo(-124f));
                float holdY = windowRect.anchoredPosition.y + ((RectTransform)q.Find("KeyHoldTrack")).anchoredPosition.y;
                Assert.That(countY + countRect.rect.height * .5f, Is.LessThan(holdY - 3.5f));

                duel.Commit();
                while (!duel.IsTurnResolved) duel.ResolveNextSlot();
                duel.BeginNextTurn();
                hud.BeginTurn();
                Refresh(hud, duel);
                Assert.That(duel.LaneReservationsThisTurn(0), Is.Zero);
                Assert.That(qButton.interactable, Is.True);
                Assert.That(qCount.text, Is.EqualTo("남은 3/3"));
                Assert.That(qCount.color, Is.Not.EqualTo(DuelVisualTheme.Danger));
                qButton.onClick.Invoke();
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1));
                Assert.That(duel.LaneReservationsThisTurn(0), Is.EqualTo(1));
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ClosedSchoolsHideTheirCounts_WithTheirWindows()
        {
            yield return null;
            var parent = new GameObject("Closed Lane Limit HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Refresh(hud, Duel(CombatFeature.LaneQ));
                Assert.That(Lane(hud, 0).Find("Lane Reservations").gameObject.activeInHierarchy, Is.True);
                Assert.That(Lane(hud, 1).Find("Lane Reservations").gameObject.activeInHierarchy, Is.False);
                Assert.That(Lane(hud, 2).Find("Lane Reservations").gameObject.activeInHierarchy, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator CancellingBackToTheSameQueueStillInvalidatesAHeldForecastWhenTheLaneIsExhausted()
        {
            yield return null;
            var parent = new GameObject("Lane Limit Forecast Test");
            LegacyCombatHud hud = null;
            var rows = CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();
            string[] removerRow = rows.Single(row => row[0] == "45");
            removerRow[Array.IndexOf(rows[0], LegacySkillSheet.Column.Lane)] = "Q";
            removerRow[Array.IndexOf(rows[0], LegacySkillSheet.Column.Cost)] = "0";
            removerRow[Array.IndexOf(rows[0], LegacySkillSheet.Column.QueuePowerMultiplierCap)] = string.Empty;
            string sheet = CsvTable.Write(rows);
            try
            {
                LegacySkillDefinitions.Install(() => sheet);
                LegacySkill remover = LegacySkillDefinitions.Skill(45);
                var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, new[] { remover },
                    Array.Empty<LegacySkill>(), new[] { 0 }, features: CombatFeature.LaneQ, meshPercent: 0);
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Assert.That(duel.TryQueueLane(0), Is.True);
                Refresh(hud, duel);
                hud.ShowExplanation(remover, false);
                Assert.That(hud.ForecastNote, Is.Not.EqualTo(LegacyCombatHud.ForecastUnavailable));
                int act = duel.Act;

                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1));
                Assert.That(duel.PlayerQueue[0], Is.SameAs(remover));
                Assert.That(duel.GetLane(0)[0], Is.SameAs(remover));
                Assert.That(duel.Act, Is.EqualTo(act));
                Assert.That(duel.LaneReservationsRemaining(0), Is.Zero);
                Refresh(hud, duel);
                hud.ShowExplanation(remover, false);
                Assert.That(hud.ForecastNote, Is.EqualTo(LegacyCombatHud.ForecastUnavailable),
                    "Queue, front and ACT are unchanged, but a fourth reservation is no longer possible.");
                Assert.That(duel.TryQueueLane(0), Is.False);
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
                LegacySkillDefinitions.Install(null);
            }
        }

        private static LegacyQueuedDuel Duel(CombatFeature features = CombatFeature.All)
            => new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                new[] { Skill(901, 0, 0), Skill(902, 1, 4), Skill(903, 2, 0) },
                Array.Empty<LegacySkill>(), new[] { 0 }, features: features, meshPercent: 0);

        private static LegacySkill Skill(int id, int lane, int cost)
            => new LegacySkill(id, "검사 기술", cost, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane, "");

        private static Transform Lane(LegacyCombatHud hud, int lane)
            => hud.Root.transform.Find("Input/Keys/Current " + "QWE"[lane]);

        private static void Refresh(LegacyCombatHud hud, LegacyQueuedDuel duel)
        {
            hud.Refresh(duel, 10f, false, -1, null, null, null);
            Canvas.ForceUpdateCanvases();
        }
    }
}
