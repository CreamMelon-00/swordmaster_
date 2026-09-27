using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Tutorial;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ResultTutorialHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResultModal_ShowsSnapshotAndRefreshesWithoutDuplicatingHierarchy()
        {
            yield return null;
            var parent = new GameObject("Result View Test");
            BattleResultHud hud = null;
            try
            {
                hud = new BattleResultHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Assert.That(hud.IsVisible, Is.False);
                int nodeCount = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                var victory = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                    60, 120, 3, 71, 0, true, 2, true);
                hud.Show(victory);
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("승리"));
                Assert.That(Label(hud.Root, "Result Stage").text, Does.Contain("01"));
                Assert.That(Label(hud.Root, "Result Rounds").text, Is.EqualTo("3"));
                Assert.That(Label(hud.Root, "Result Player HP").text, Is.EqualTo("71"));
                Assert.That(Label(hud.Root, "Result Enemy HP").text, Is.EqualTo("0"));
                Assert.That(Label(hud.Root, "Result Reward").text, Is.EqualTo("+ 60"));
                Assert.That(Label(hud.Root, "Result Currency").text, Does.Contain("120"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("첫 클리어").And.Contain("02 개방"));
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.True);

                var replay = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                    30, 150, 5, 45, 0, false, 0, true);
                hud.Show(replay);
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("재클리어").And.Contain("절반"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Not.Contain("개방!"));
                Assert.That(Label(hud.Root, "Result Reward").text, Is.EqualTo("+ 30"));
                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
                hud.Show(replay);
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                Assert.That(hud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(400));
                Assert.That(hud.Root.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ResultModal_DefeatDrawAndTutorialNeverAdvertiseUnlockOrNextStage()
        {
            yield return null;
            var parent = new GameObject("Other Result Views Test");
            BattleResultHud hud = null;
            try
            {
                hud = new BattleResultHud(parent.transform, new LegacyDuelArt(), null, null, null);
                hud.Show(new BattleResult(DuelMatchOutcome.EnemyVictory, false, 2, "깊은 숲", 0, 87, 4, 0, 18, false, 0, false));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("패배"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("보상은 없습니다"));
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                hud.Show(new BattleResult(DuelMatchOutcome.Draw, false, 2, "깊은 숲", 0, 87, 4, 0, 0, false, 0, false));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("무승부"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Not.Contain("개방!"));
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, true, 0, "연습 결투", 0, 87, 4, 70, 0, false, 0, false));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("연습 완료"));
                Assert.That(Label(hud.Root, "Result Stage").text, Does.Contain("튜토리얼"));
                Assert.That(Button(hud.Root, "Result Retry").GetComponentInChildren<Text>().text, Is.EqualTo("다시 연습"));
                Assert.That(Label(hud.Root, "Result Reward").text, Is.EqualTo("+ 0"));
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                var backdrop = Named(hud.Root, "Result Backdrop").GetComponent<Image>();
                Assert.That(backdrop.raycastTarget, Is.True, "Result backdrop must block clicks reaching battle controls.");
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ResultActions_ClearSelectionAndInvokeOnlyTheirOwnCallback()
        {
            yield return null;
            var parent = new GameObject("Result Action Test");
            BattleResultHud hud = null;
            try
            {
                int lobby = 0, retry = 0, next = 0;
                hud = new BattleResultHud(parent.transform, new LegacyDuelArt(), () => lobby++, () => retry++, () => next++);
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 60, 60, 2, 80, 0, true, 2, true));
                foreach (string name in new[] { "Result Lobby", "Result Retry", "Result Next Stage" })
                {
                    var button = Button(hud.Root, name);
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
                    button.onClick.Invoke();
                    if (EventSystem.current != null) Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                }
                Assert.That(lobby, Is.EqualTo(1));
                Assert.That(retry, Is.EqualTo(1));
                Assert.That(next, Is.EqualTo(1));
                hud.Dispose();
                hud.Dispose();
                Assert.That(hud.IsVisible, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator TutorialCoach_FollowsSuccessfulActionsKeepsControlsUnblockedAndReusesNodes()
        {
            yield return null;
            var parent = new GameObject("Tutorial Coach Test");
            TutorialCoachHud hud = null;
            try
            {
                var progress = new TutorialProgress();
                int advances = 0, inspections = 0, skips = 0;
                hud = new TutorialCoachHud(parent.transform, new LegacyDuelArt(),
                    () => { advances++; progress.TryAdvance(); }, () => skips++,
                    () => { inspections++; progress.NotifyInspected(); });
                hud.Show(progress);
                int nodeCount = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                var coachFrame = Named(hud.Root, "Tutorial Coach Border").GetComponent<RectTransform>();
                Assert.That(coachFrame.anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(coachFrame.anchorMax, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(coachFrame.anchoredPosition.y - coachFrame.sizeDelta.y * .5f, Is.GreaterThan(200f),
                    "Coach must stay above the skill input dock.");
                Assert.That(coachFrame.anchoredPosition.y + coachFrame.sizeDelta.y * .5f, Is.LessThan(450f),
                    "Coach must stay below the actors and their head-attached queues/status bars.");
                Assert.That(Label(hud.Root, "Tutorial Step").text, Is.EqualTo("연습 01 / 08"));
                Assert.That(Button(hud.Root, "Tutorial Continue").gameObject.activeSelf, Is.True);
                Assert.That(Button(hud.Root, "Tutorial Inspect Enemy").gameObject.activeSelf, Is.False);
                Assert.That(Button(hud.Root, "Tutorial Continue").GetComponentInChildren<Text>().text, Is.EqualTo("연습 시작"));
                Button(hud.Root, "Tutorial Continue").onClick.Invoke();
                hud.Show(progress);
                Assert.That(Button(hud.Root, "Tutorial Continue").gameObject.activeSelf, Is.False);
                Assert.That(Label(hud.Root, "Tutorial Title").text, Is.EqualTo(progress.Title));
                progress.NotifyQueued(0);
                progress.NotifyQueued(1);
                progress.NotifyQueued(0);
                hud.Show(progress);
                Assert.That(Button(hud.Root, "Tutorial Inspect Enemy").gameObject.activeSelf, Is.True);
                Button(hud.Root, "Tutorial Inspect Enemy").onClick.Invoke();
                hud.Show(progress);
                Assert.That(progress.Step, Is.EqualTo(TutorialStep.CommitQueue));
                Assert.That(Button(hud.Root, "Tutorial Inspect Enemy").gameObject.activeSelf, Is.False);
                progress.NotifyCommitted();
                progress.NotifyTurnBegan(2);
                hud.Show(progress);
                Assert.That(Button(hud.Root, "Tutorial Continue").gameObject.activeSelf, Is.True);
                Button(hud.Root, "Tutorial Continue").onClick.Invoke();
                hud.Show(progress);
                Assert.That(progress.Step, Is.EqualTo(TutorialStep.FreeBattle));
                for (int i = 0; i < 20; i++) hud.Show(progress);
                Assert.That(Label(hud.Root, "Tutorial Description").text, Is.EqualTo(progress.Description));
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                foreach (Graphic graphic in hud.Root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null) Assert.That(graphic.raycastTarget, Is.False);
                foreach (Button button in hud.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Button(hud.Root, "Tutorial Skip").onClick.Invoke();
                Assert.That(advances, Is.EqualTo(2));
                Assert.That(inspections, Is.EqualTo(1));
                Assert.That(skips, Is.EqualTo(1));
                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator TutorialBattleHud_ShowsUnlimitedPracticeAndFocusThenRestoresNormalHud()
        {
            yield return null;
            var parent = new GameObject("Tutorial Battle HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                hud.SetStage(0, 8, "연습 결투");
                hud.SetTutorialMode(true);
                hud.SetTutorialFocus(0, true, true, true);
                Canvas.ForceUpdateCanvases();
                hud.Refresh(new LegacyQueuedDuel(), 0f, false, -1, null, null, null);
                Assert.That(Label(hud.Root, "Stage Label").text, Is.EqualTo("연습 스테이지 · 연습 결투"));
                Assert.That(Label(hud.Root, "Tutorial Time Hint").gameObject.activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Tutorial Time Hint").text, Does.Contain("시간 제한 없음"));
                Assert.That(Named(hud.Root, "Timer").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Timer Track").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<Outline>().enabled, Is.True);
                Assert.That(Named(hud.Root, "Current W").GetComponent<Outline>().enabled, Is.False);
                Assert.That(Named(hud.Root, "AButton").GetComponent<Outline>().enabled, Is.True);
                Assert.That(Named(hud.Root, "Tutorial Enemy Queue Focus").gameObject.activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Tutorial ACT Focus").gameObject.activeSelf, Is.True);
                foreach (Image image in Named(hud.Root, "Tutorial ACT Focus").GetComponentsInChildren<Image>(true))
                    Assert.That(image.raycastTarget, Is.False);

                hud.SetTutorialMode(false);
                hud.SetStage(2, 8, "깊은 숲");
                Assert.That(Label(hud.Root, "Stage Label").text, Is.EqualTo("02 / 08  ·  깊은 숲"));
                Assert.That(Named(hud.Root, "Timer").gameObject.activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Timer Track").gameObject.activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Tutorial Time Hint").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<Outline>().enabled, Is.False);
                Assert.That(Named(hud.Root, "AButton").GetComponent<Outline>().enabled, Is.False);
                Assert.That(Named(hud.Root, "Tutorial ACT Focus").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Tutorial Enemy Queue Focus").gameObject.activeSelf, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator TutorialInput_EnablesOnlyExpectedActionAndRestoresRegularPlanning()
        {
            yield return null;
            var parent = new GameObject("Tutorial Input Availability Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                var progress = new TutorialProgress();
                var duel = TutorialStage.CreateDuel();
                hud.SetTutorialMode(true);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                hud.SetTutorialInput(progress);
                AssertActions(hud.Root, false, false, false, false);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.4f));
                Assert.That(Named(hud.Root, "AButton").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.4f));
                progress.TryAdvance();
                hud.SetTutorialInput(progress);
                AssertActions(hud.Root, true, false, false, false);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(Named(hud.Root, "Current W").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.4f));
                Assert.That(duel.TryQueueLane(0), Is.True);
                progress.NotifyQueued(0);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                AssertActions(hud.Root, false, true, false, false);
                Assert.That(duel.TryQueueLane(1), Is.True);
                progress.NotifyQueued(1);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                AssertActions(hud.Root, true, false, false, false);
                Assert.That(duel.TryQueueLane(0), Is.True);
                progress.NotifyQueued(0);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                AssertActions(hud.Root, false, false, false, false);
                progress.NotifyInspected();
                hud.SetTutorialInput(progress);
                AssertActions(hud.Root, false, false, false, true);
                hud.SetTutorialMode(false);
                hud.Refresh(new LegacyQueuedDuel(), 10f, false, -1, null, null, null);
                AssertActions(hud.Root, true, true, true, true);
                foreach (string name in new[] { "Current Q", "Current W", "Current E", "AButton" })
                    Assert.That(Named(hud.Root, name).GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        private static void AssertActions(GameObject root, bool q, bool w, bool e, bool commit)
        {
            Assert.That(Button(root, "Current Q").interactable, Is.EqualTo(q));
            Assert.That(Button(root, "Current W").interactable, Is.EqualTo(w));
            Assert.That(Button(root, "Current E").interactable, Is.EqualTo(e));
            Assert.That(Button(root, "AButton").interactable, Is.EqualTo(commit));
        }

        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing UI node: " + name);
            return null;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();
    }
}
