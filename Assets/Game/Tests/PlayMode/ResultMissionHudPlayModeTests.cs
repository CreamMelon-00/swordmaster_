using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ResultMissionHudPlayModeTests
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
        public IEnumerator ResultModal_DefeatDrawAndMissionsNeverAdvertiseRewardsOrStageUnlocks()
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
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, true, 2, "맞서는 검", 0, 87, 4, 70, 0, false, 0, true));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("임무 완료"));
                Assert.That(Label(hud.Root, "Result Stage").text, Is.EqualTo("서막  ·  임무 02  ·  맞서는 검"));
                Assert.That(Label(hud.Root, "Result Reward").text, Is.EqualTo("+ 0"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("다음 임무").And.Not.Contain("개방!"));
                Assert.That(Caption(hud.Root, "Result Retry"), Is.EqualTo("재도전"));
                Assert.That(Caption(hud.Root, "Result Next Stage"), Is.EqualTo("다음 임무"));
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.True);
                Assert.That(Button(hud.Root, "Result Lobby").gameObject.activeSelf, Is.False,
                    "After a mission victory the briefing exit would duplicate 다음 임무.");
                Assert.That(Named(hud.Root, "Result Retry").GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(-132f));
                Assert.That(Named(hud.Root, "Result Next Stage").GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(132f));
                hud.Show(new BattleResult(DuelMatchOutcome.EnemyVictory, true, 2, "맞서는 검", 0, 87, 2, 0, 20, false, 0, false));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("임무 실패"));
                Assert.That(Caption(hud.Root, "Result Lobby"), Is.EqualTo("브리핑으로"));
                Assert.That(Button(hud.Root, "Result Lobby").gameObject.activeSelf, Is.True);
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                hud.Show(new BattleResult(DuelMatchOutcome.EnemyVictory, true, PrologueMissions.Count, "마지막 결투",
                    0, 87, 2, 0, 20, false, 0, false), arcComplete: true);
                Assert.That(Caption(hud.Root, "Result Lobby"), Is.EqualTo("로비로"), "A replay after the arc exits to the lobby.");
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("로비로"));
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, true, PrologueMissions.Count, "마지막 결투",
                    0, 87, 5, 40, 0, false, 0, true));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("편성"));
                Assert.That(Caption(hud.Root, "Result Next Stage"), Is.EqualTo("여정 계속"), "The last mission opens the lobby.");
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 60, 60, 2, 80, 0, true, 2, true));
                Assert.That(Button(hud.Root, "Result Lobby").gameObject.activeSelf, Is.True);
                Assert.That(Caption(hud.Root, "Result Lobby"), Is.EqualTo("로비로"));
                Assert.That(Named(hud.Root, "Result Lobby").GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(-248f));
                Assert.That(Caption(hud.Root, "Result Next Stage"), Is.EqualTo("다음 스테이지"));
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
        public IEnumerator MissionCoach_FollowsTheGuideKeepsControlsUnblockedAndReusesNodes()
        {
            yield return null;
            var parent = new GameObject("Mission Coach Test");
            MissionCoachHud hud = null;
            try
            {
                MissionGuide guide = PrologueMissions.Get(2).CreateGuide();
                int advances = 0, inspections = 0, skips = 0;
                hud = new MissionCoachHud(parent.transform, new LegacyDuelArt(),
                    () => { advances++; guide.TryAdvance(); }, () => skips++,
                    () => { inspections++; guide.NotifyInspected(); });
                hud.Show(guide, "임무 02");
                int nodeCount = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                var coachFrame = Named(hud.Root, "Coach Border").GetComponent<RectTransform>();
                Assert.That(coachFrame.anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(coachFrame.anchorMax, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(coachFrame.anchoredPosition.y - coachFrame.sizeDelta.y * .5f, Is.GreaterThan(200f),
                    "Coach must stay above the skill input dock.");
                Assert.That(coachFrame.anchoredPosition.y + coachFrame.sizeDelta.y * .5f, Is.LessThan(450f),
                    "Coach must stay below the actors and their head-attached queues/status bars.");
                Assert.That(Label(hud.Root, "Coach Step").text, Is.EqualTo("임무 02  ·  안내 01 / 08"));
                Assert.That(Button(hud.Root, "Coach Continue").gameObject.activeSelf, Is.True);
                Assert.That(Button(hud.Root, "Coach Inspect Enemy").gameObject.activeSelf, Is.False);
                Assert.That(Caption(hud.Root, "Coach Continue"), Is.EqualTo("시작"));
                Assert.That(Caption(hud.Root, "Coach Abandon"), Is.EqualTo("임무 포기"));
                Button(hud.Root, "Coach Continue").onClick.Invoke();
                hud.Show(guide, "임무 02");
                Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Inspect));
                Assert.That(Button(hud.Root, "Coach Continue").gameObject.activeSelf, Is.False);
                Assert.That(Button(hud.Root, "Coach Inspect Enemy").gameObject.activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Coach Title").text, Is.EqualTo(guide.Title));
                Button(hud.Root, "Coach Inspect Enemy").onClick.Invoke();
                hud.Show(guide, "임무 02");
                Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Queue));
                Assert.That(Button(hud.Root, "Coach Inspect Enemy").gameObject.activeSelf, Is.False);
                guide.NotifyQueued(0);
                guide.NotifyQueued(0);
                guide.NotifyCommitted();
                guide.NotifyTurnBegan(2);
                hud.Show(guide, "임무 02");
                Assert.That(guide.FocusesAct, Is.True);
                Assert.That(Button(hud.Root, "Coach Continue").gameObject.activeSelf, Is.True);
                Assert.That(Caption(hud.Root, "Coach Continue"), Is.EqualTo("계속"));
                Button(hud.Root, "Coach Continue").onClick.Invoke();
                hud.Show(guide, "임무 02");
                Assert.That(guide.IsFree, Is.True);
                for (int i = 0; i < 20; i++) hud.Show(guide, "임무 02");
                Assert.That(Label(hud.Root, "Coach Description").text, Is.EqualTo(guide.Description));
                Assert.That(Label(hud.Root, "Coach Step").text, Is.EqualTo("임무 02  ·  안내 08 / 08"));
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount));
                foreach (Graphic graphic in hud.Root.GetComponentsInChildren<Graphic>(true))
                    if (graphic.GetComponent<Button>() == null) Assert.That(graphic.raycastTarget, Is.False);
                foreach (Button button in hud.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
                Button(hud.Root, "Coach Abandon").onClick.Invoke();
                Assert.That(advances, Is.EqualTo(2));
                Assert.That(inspections, Is.EqualTo(1));
                Assert.That(skips, Is.EqualTo(1));
                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator MissionBriefing_ShowsBackgroundTitleObjectivesAndEnemySilhouettesWithOneStartButton()
        {
            yield return null;
            var parent = new GameObject("Mission Briefing Test");
            MissionBriefingHud hud = null;
            try
            {
                int starts = 0;
                hud = new MissionBriefingHud(parent.transform, new LegacyDuelArt(), () => starts++);
                Assert.That(hud.IsVisible, Is.False);
                PrologueMission mission = PrologueMissions.Get(2);
                hud.Show(mission, PrologueMissions.Count);
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.Mission, Is.SameAs(mission));
                Assert.That(hud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(MissionBriefingHud.SortingOrder));
                Assert.That(Label(hud.Root, "Briefing Chapter").text, Is.EqualTo("서막  ·  임무 2 / 4"));
                Assert.That(Label(hud.Root, "Briefing Title").text, Is.EqualTo(mission.Title));
                foreach (string objective in mission.Objectives)
                    Assert.That(Label(hud.Root, "Objectives Text").text, Does.Contain(objective));
                Assert.That(Named(hud.Root, "Briefing Background").GetComponent<Image>().sprite, Is.Not.Null,
                    "Each mission names a background that exists in Resources.");
                Image silhouette = Named(hud.Root, "Enemy Silhouette").GetComponent<Image>();
                Assert.That(silhouette.sprite, Is.Not.Null);
                Assert.That(silhouette.color, Is.EqualTo(MissionBriefingHud.SilhouetteColor), "Enemies are shown as silhouettes.");
                Assert.That(Label(hud.Root, "Enemy Name").text, Is.EqualTo(mission.Enemies[0].Name));
                Assert.That(hud.Root.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(1), "The briefing has one action.");
                Assert.That(hud.StartButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));

                int nodeCount = hud.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int number = 1; number <= PrologueMissions.Count; number++)
                {
                    hud.Show(PrologueMissions.Get(number), PrologueMissions.Count);
                    Assert.That(Named(hud.Root, "Briefing Background").GetComponent<Image>().sprite, Is.Not.Null);
                }
                hud.Show(mission, PrologueMissions.Count);
                yield return null;
                Assert.That(hud.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodeCount),
                    "Showing another mission replaces its enemy views instead of stacking them.");
                hud.StartButton.onClick.Invoke();
                Assert.That(starts, Is.EqualTo(1));
                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
                hud.Dispose();
                hud.Dispose();
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator MissionBattleHud_LabelsTheMissionTogglesItsTimerAndFocusThenRestoresNormalHud()
        {
            yield return null;
            var parent = new GameObject("Mission Battle HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                hud.SetStage(2, 4, "맞서는 검");
                hud.SetMissionMode(true);
                hud.SetGuideFocus(0, true, true, true);
                Canvas.ForceUpdateCanvases();
                hud.Refresh(new LegacyQueuedDuel(), 0f, false, -1, null, null, null);
                Assert.That(Label(hud.Root, "Stage Label").text, Is.EqualTo("임무 02 / 04  ·  맞서는 검"));
                Assert.That(Label(hud.Root, "Untimed Hint").gameObject.activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Untimed Hint").text, Does.Contain("시간 제한 없음"));
                Assert.That(Named(hud.Root, "Timer").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Timer Track").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<Outline>().enabled, Is.True);
                Assert.That(Named(hud.Root, "Current W").GetComponent<Outline>().enabled, Is.False);
                Assert.That(Named(hud.Root, "AButton").GetComponent<Outline>().enabled, Is.True);
                Assert.That(Named(hud.Root, "Guide Enemy Queue Focus").gameObject.activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Guide ACT Focus").gameObject.activeSelf, Is.True);
                Assert.That(Named(hud.Root, "BreathButton").gameObject.activeSelf, Is.False, "Breathing stays closed in missions.");
                foreach (Image image in Named(hud.Root, "Guide ACT Focus").GetComponentsInChildren<Image>(true))
                    Assert.That(image.raycastTarget, Is.False);

                hud.SetMissionMode(true, true);
                Assert.That(Named(hud.Root, "Timer").gameObject.activeSelf, Is.True, "A timed mission shows the clock.");
                Assert.That(Label(hud.Root, "Untimed Hint").gameObject.activeSelf, Is.False);

                hud.Refresh(PrologueMissions.Get(1).CreateDuel(1), 10f, false, -1, null, null, null);
                foreach (string name in new[] { "Current W", "Current E", "Next W", "Next E" })
                    Assert.That(Named(hud.Root, name).gameObject.activeSelf, Is.False, name + " belongs to a closed lane.");
                Assert.That(Named(hud.Root, "Current Q").gameObject.activeSelf, Is.True);

                hud.SetMissionMode(false);
                hud.SetStage(2, 8, "깊은 숲");
                hud.Refresh(new LegacyQueuedDuel(), 10f, false, -1, null, null, null);
                Assert.That(Label(hud.Root, "Stage Label").text, Is.EqualTo("02 / 08  ·  깊은 숲"));
                Assert.That(Named(hud.Root, "Timer").gameObject.activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Timer Track").gameObject.activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Untimed Hint").gameObject.activeSelf, Is.False);
                foreach (string name in new[] { "Current W", "Current E", "Next W", "Next E" })
                    Assert.That(Named(hud.Root, name).gameObject.activeSelf, Is.True, name + " returns with its lane.");
                Assert.That(Named(hud.Root, "Current Q").GetComponent<Outline>().enabled, Is.False);
                Assert.That(Named(hud.Root, "AButton").GetComponent<Outline>().enabled, Is.False);
                Assert.That(Named(hud.Root, "Guide ACT Focus").gameObject.activeSelf, Is.False);
                Assert.That(Named(hud.Root, "Guide Enemy Queue Focus").gameObject.activeSelf, Is.False);
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator MissionGuideInput_EnablesOnlyTheExpectedActionAndRestoresRegularPlanning()
        {
            yield return null;
            var parent = new GameObject("Mission Input Availability Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                PrologueMission mission = PrologueMissions.Get(3);
                MissionGuide guide = mission.CreateGuide();
                LegacyQueuedDuel duel = mission.CreateDuel(1);
                hud.SetMissionMode(true);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                hud.SetGuide(guide);
                AssertActions(hud.Root, false, false, false, false);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.4f));
                Assert.That(Named(hud.Root, "AButton").GetComponent<CanvasGroup>().alpha, Is.EqualTo(.4f));
                guide.TryAdvance();
                hud.SetGuide(guide);
                AssertActions(hud.Root, true, false, false, false);
                Assert.That(Named(hud.Root, "Current Q").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(duel.TryQueueLane(0), Is.True);
                guide.NotifyQueued(0);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                AssertActions(hud.Root, true, false, false, false);
                Assert.That(duel.TryQueueLane(0), Is.True);
                guide.NotifyQueued(0);
                hud.Refresh(duel, 10f, false, -1, null, null, null);
                AssertActions(hud.Root, false, false, false, true);
                hud.SetMissionMode(false);
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
        private static string Caption(GameObject root, string name) => Button(root, name).GetComponentInChildren<Text>().text;
    }
}
