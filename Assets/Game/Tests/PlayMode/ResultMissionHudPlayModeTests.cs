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
                    60, 120, 3, 71, 0, true, 2, true, CampaignCurriculum.Default.Find("horizontal-cut"));
                hud.Show(victory);
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("승리"));
                Assert.That(Label(hud.Root, "Result Stage").text, Does.Contain("01"));
                Assert.That(Label(hud.Root, "Result Rounds").text, Is.EqualTo("3"));
                Assert.That(Label(hud.Root, "Result Player HP").text, Is.EqualTo("71"));
                Assert.That(Label(hud.Root, "Result Enemy HP").text, Is.EqualTo("0"));
                Assert.That(Named(hud.Root, "Result Rounds Panel").GetComponent<Image>().color.a, Is.Zero,
                    "The three result numbers share a single score line instead of separate cards.");
                Assert.That(Size(hud.Root, "Result Next Stage"), Is.EqualTo(new Vector2(272f, 66f)),
                    "Advancing is the prominent result action.");
                Assert.That(Size(hud.Root, "Result Retry"), Is.EqualTo(new Vector2(194f, 50f)));
                Assert.That(Label(hud.Root, "Result Curriculum Heading").text, Is.EqualTo("커리큘럼"));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("가로베기 완료"));
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text, Does.Contain("가로베기").And.Contain("편성"),
                    "A completed node names the skill it granted.");
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("첫 클리어").And.Contain("02 개방"));
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                    60, 120, 3, 71, 0, true, 2, true, firstClearSkillName: "탐색"),
                    storyNotice: "새 임무 도착");
                Assert.That(Label(hud.Root, "Result Notice").text,
                    Does.Contain("탐색").And.Contain("새 임무 도착"));
                hud.Show(victory);
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.True);
                foreach (Text text in hud.Root.GetComponentsInChildren<Text>(true))
                    Assert.That(text.text, Does.Not.Contain("재화"), "Currency has no use, so the result never shows it.");

                var replay = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구",
                    30, 150, 5, 45, 0, false, 0, true, null, CampaignCurriculum.Default.Find("diagonal-cut"), 0);
                hud.Show(replay);
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("다시 클리어").And.Not.Contain("절반"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Not.Contain("개방!"));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("사선베기  0/1"));
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text, Does.Contain("진행 중"));
                var statNode = new CurriculumNode("stat-result", "지구력 훈련", CurriculumBranch.Guard,
                    0f, 0, new int[0], statReward: new CurriculumStatReward(health: 5, actGain: 1));
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 2, "이끼 낀 오솔길",
                    70, 220, 3, 80, 0, false, 0, true, completedCurriculumNode: statNode));
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text,
                    Does.Contain("최대 체력 +5").And.Contain("ACT 회복 +1").And.Not.Contain("새 기술"));
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
        public IEnumerator TrainingResult_ShowsDamageAndNextTarget_WithoutCampaignRewards()
        {
            yield return null;
            var parent = new GameObject("Training Result View Test");
            BattleResultHud hud = null;
            try
            {
                hud = new BattleResultHud(parent.transform, new LegacyDuelArt(), null, null, null);
                var draw = new BattleResult(DuelMatchOutcome.Draw, false, 2, "허수아비 수련",
                    0, 60, 5, 100, 12, false, 0, false, curriculumOpen: false, isTraining: true);
                hud.ShowTraining(draw, 50, 50);
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("수련 종료"));
                Assert.That(Label(hud.Root, "Result Rounds").text, Is.EqualTo("5 / 5"));
                Assert.That(Label(hud.Root, "Result Enemy HP").text, Is.EqualTo("12 / 50"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("가한 피해 38"));
                Assert.That(Named(hud.Root, "Result Curriculum Panel").gameObject.activeSelf, Is.False);
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                Assert.That(Caption(hud.Root, "Result Retry"), Is.EqualTo("재도전"));

                var victory = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 2, "허수아비 수련",
                    0, 60, 3, 100, 0, false, 0, false, curriculumOpen: false, isTraining: true);
                hud.ShowTraining(victory, 50, 100);
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("수련 성공"));
                Assert.That(Label(hud.Root, "Result Notice").text,
                    Does.Contain("가한 피해 50").And.Contain("다음 허수아비 체력 100"));
                Assert.That(Caption(hud.Root, "Result Retry"), Is.EqualTo("다음 수련"));
                Assert.That(Size(hud.Root, "Result Retry"), Is.EqualTo(new Vector2(272f, 66f)));
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ResultModal_DefeatDrawAndMissionsNeverAdvertiseStageUnlocks_MissionsSkipTheCurriculum()
        {
            yield return null;
            var parent = new GameObject("Other Result Views Test");
            BattleResultHud hud = null;
            try
            {
                hud = new BattleResultHud(parent.transform, new LegacyDuelArt(), null, null, null);
                hud.Show(new BattleResult(DuelMatchOutcome.EnemyVictory, false, 2, "깊은 숲", 0, 87, 4, 0, 18, false, 0, false,
                    CampaignCurriculum.Default.Find("breathing")));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("패배"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("편성").And.Not.Contain("개방!"));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("호흡 완료"),
                    "A defeat still finishes a battle, so it can complete the node in progress.");
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text, Does.Contain("호흡"));
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                Assert.That(Size(hud.Root, "Result Retry"), Is.EqualTo(new Vector2(272f, 66f)),
                    "Retry becomes the prominent action when there is no next stage.");
                hud.Show(new BattleResult(DuelMatchOutcome.Draw, false, 2, "깊은 숲", 0, 87, 4, 0, 0, false, 0, false));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("무승부"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Not.Contain("개방!"));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("진행 없음"));
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text, Does.Contain("커리큘럼"),
                    "Without a node in progress the result points to the lobby curriculum.");
                hud.Show(new BattleResult(DuelMatchOutcome.Draw, false, 2, "깊은 숲", 0, 87, 4, 0, 0, false, 0, false,
                    curriculumFinished: true));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("모두 완료"));
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text, Does.Contain("초기화").And.Not.Contain("고르면"),
                    "With nothing left to choose the result does not ask for a choice.");
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, true, 2, "인사는 칼로", 0, 87, 4, 70, 0, false, 0, true));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("임무 완료"));
                Assert.That(Label(hud.Root, "Result Stage").text, Is.EqualTo("깨어남  ·  임무 02  ·  인사는 칼로"));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("깨어남 이후"));
                Assert.That(Label(hud.Root, "Result Curriculum Detail").text, Does.Contain("깨어남"));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("다음 임무").And.Not.Contain("개방!"));
                Assert.That(Caption(hud.Root, "Result Retry"), Is.EqualTo("재도전"));
                Assert.That(Caption(hud.Root, "Result Next Stage"), Is.EqualTo("다음 임무"));
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.True);
                Assert.That(Button(hud.Root, "Result Lobby").gameObject.activeSelf, Is.False,
                    "After a mission victory the briefing exit would duplicate 다음 임무.");
                Assert.That(Named(hud.Root, "Result Retry").GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(-165f));
                Assert.That(Named(hud.Root, "Result Next Stage").GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(231f));
                hud.Show(new BattleResult(DuelMatchOutcome.EnemyVictory, true, 2, "맞서는 검", 0, 87, 2, 0, 20, false, 0, false));
                Assert.That(Label(hud.Root, "Result Heading").text, Is.EqualTo("임무 실패"));
                Assert.That(Caption(hud.Root, "Result Lobby"), Is.EqualTo("브리핑으로"));
                Assert.That(Button(hud.Root, "Result Lobby").gameObject.activeSelf, Is.True);
                Assert.That(Button(hud.Root, "Result Next Stage").gameObject.activeSelf, Is.False);
                hud.Show(new BattleResult(DuelMatchOutcome.EnemyVictory, true, PrologueMissions.Count, "마지막 결투",
                    0, 87, 2, 0, 20, false, 0, false), missionExitsToLobby: true);
                Assert.That(Caption(hud.Root, "Result Lobby"), Is.EqualTo("로비로"), "A replay after the arc exits to the lobby.");
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("로비로"));
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, true, PrologueMissions.Count, "마지막 결투",
                    0, 87, 5, 40, 0, false, 0, true));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("편성").And.Contain("커리큘럼"));
                Assert.That(Caption(hud.Root, "Result Next Stage"), Is.EqualTo("여정 계속"), "The last mission opens the lobby.");
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 60, 60, 2, 80, 0, true, 2, true));
                Assert.That(Button(hud.Root, "Result Lobby").gameObject.activeSelf, Is.True);
                Assert.That(Caption(hud.Root, "Result Lobby"), Is.EqualTo("로비로"));
                Assert.That(Named(hud.Root, "Result Lobby").GetComponent<RectTransform>().anchoredPosition.x, Is.EqualTo(-262f));
                Assert.That(Caption(hud.Root, "Result Next Stage"), Is.EqualTo("다음 스테이지"));
                var backdrop = Named(hud.Root, "Result Backdrop").GetComponent<Image>();
                Assert.That(backdrop.raycastTarget, Is.True, "Result backdrop must block clicks reaching battle controls.");
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator ResultModal_WithTheCurriculumClosed_DropsItsRowAndClosesUpTheCard()
        {
            yield return null;
            var parent = new GameObject("Closed Curriculum Result Test");
            BattleResultHud hud = null;
            try
            {
                hud = new BattleResultHud(parent.transform, new LegacyDuelArt(), null, null, null);
                var open = new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 60, 60, 2, 80, 0, true, 2, true);
                hud.Show(open);
                Assert.That(Named(hud.Root, "Result Curriculum Panel").gameObject.activeSelf, Is.True);
                Assert.That(Size(hud.Root, "Result Card"), Is.EqualTo(new Vector2(840f, 650f)));

                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, false, 1, "숲길 입구", 60, 60, 2, 80, 0, true, 2, true,
                    curriculumOpen: false));
                Assert.That(Named(hud.Root, "Result Curriculum Panel").gameObject.activeSelf, Is.False,
                    "Before mission 8 the result has no curriculum row.");
                foreach (Text text in hud.Root.GetComponentsInChildren<Text>())
                    Assert.That(text.text, Does.Not.Contain("커리큘럼"), text.name);
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("첫 클리어").And.Contain("02 개방"));
                // The card closes up by the row: half from above, half from below.
                Assert.That(Size(hud.Root, "Result Card Border"), Is.EqualTo(new Vector2(844f, 540f)));
                Assert.That(Size(hud.Root, "Result Card"), Is.EqualTo(new Vector2(840f, 536f)));
                Assert.That(Position(hud.Root, "Result Heading").y, Is.EqualTo(181f));
                Assert.That(Position(hud.Root, "Result Rule").y, Is.EqualTo(87f));
                Assert.That(Position(hud.Root, "Result Player HP Panel"), Is.EqualTo(new Vector2(0f, 7f)));
                Assert.That(Position(hud.Root, "Result Stat Divider Left").y, Is.EqualTo(7f));
                Assert.That(Position(hud.Root, "Result Notice").y, Is.EqualTo(-104f));
                foreach (string name in new[] { "Result Lobby", "Result Retry", "Result Next Stage" })
                    Assert.That(Position(hud.Root, name).y, Is.EqualTo(-198f), name);
                Assert.That(Position(hud.Root, "Result Lobby").x, Is.EqualTo(-262f), "The secondary exit keeps its place.");

                // The 서막's last win no longer promises the curriculum.
                hud.Show(new BattleResult(DuelMatchOutcome.PlayerVictory, true, PrologueMissions.Count, "떠돌이 기사",
                    0, 0, 5, 40, 0, false, 0, true, curriculumOpen: false));
                Assert.That(Label(hud.Root, "Result Notice").text, Does.Contain("편성과 스테이지").And.Not.Contain("커리큘럼"));
                Assert.That(Named(hud.Root, "Result Curriculum Panel").gameObject.activeSelf, Is.False);
                Assert.That(Position(hud.Root, "Result Next Stage").y, Is.EqualTo(-198f));

                hud.Show(open);
                Assert.That(Named(hud.Root, "Result Curriculum Panel").gameObject.activeSelf, Is.True, "An open curriculum brings it back.");
                Assert.That(Size(hud.Root, "Result Card Border"), Is.EqualTo(new Vector2(844f, 654f)));
                Assert.That(Position(hud.Root, "Result Heading").y, Is.EqualTo(238f));
                Assert.That(Position(hud.Root, "Result Player HP Panel"), Is.EqualTo(new Vector2(0f, 64f)));
                Assert.That(Position(hud.Root, "Result Stat Divider Left").y, Is.EqualTo(64f));
                Assert.That(Position(hud.Root, "Result Notice").y, Is.EqualTo(-161f));
                Assert.That(Position(hud.Root, "Result Next Stage").y, Is.EqualTo(-255f));
                Assert.That(Label(hud.Root, "Result Curriculum").text, Is.EqualTo("진행 없음"));
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
                Assert.That(Label(hud.Root, "Coach Step").text, Is.EqualTo("임무 02  ·  안내 01 / 09"));
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
                Assert.That(guide.Kind, Is.EqualTo(MissionGuideStepKind.Cycle));
                Assert.That(Button(hud.Root, "Coach Continue").gameObject.activeSelf, Is.False, "The 넘기기 beat waits for Shift.");
                guide.NotifyCycled();
                hud.Show(guide, "임무 02");
                Assert.That(guide.IsFree, Is.True);
                for (int i = 0; i < 20; i++) hud.Show(guide, "임무 02");
                Assert.That(Label(hud.Root, "Coach Description").text, Is.EqualTo(guide.Description));
                Assert.That(Label(hud.Root, "Coach Step").text, Is.EqualTo("임무 02  ·  안내 09 / 09"));
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
                Assert.That(Label(hud.Root, "Briefing Chapter").text, Is.EqualTo("깨어남  ·  임무 2 / 4"));
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
                Assert.That(Position(hud.Root, "Current Q"), Is.EqualTo(new Vector2(0f, hud.SkillWindowY)), "The lone open lane moves to the middle.");
                Assert.That(Position(hud.Root, "Next Q").x, Is.LessThan(0f), "Its next skill sits at its gear's upper left.");
                Assert.That(Position(hud.Root, "Used Q").x, Is.GreaterThan(0f), "…the used one at its upper right.");
                Assert.That(Named(hud.Root, "Idler 1").gameObject.activeSelf, Is.False, "One gear needs no idler.");
                Assert.That(CycleEffect(hud.Root), Is.EqualTo("맨 앞 한 칸"));
                Assert.That(Position(hud.Root, "CycleButton"), Is.EqualTo(new Vector2(-360f, -4f)), "넘기기 does not move with the lanes.");

                hud.SetMissionMode(false);
                hud.SetStage(2, 8, "깊은 숲");
                hud.Refresh(new LegacyQueuedDuel(), 10f, false, -1, null, null, null);
                Assert.That(Label(hud.Root, "Stage Label").text, Is.EqualTo("02 / 08  ·  깊은 숲"));
                Assert.That(Named(hud.Root, "Timer").gameObject.activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Timer Track").gameObject.activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Untimed Hint").gameObject.activeSelf, Is.False);
                foreach (string name in new[] { "Current W", "Current E", "Next W", "Next E" })
                    Assert.That(Named(hud.Root, name).gameObject.activeSelf, Is.True, name + " returns with its lane.");
                float pitch = hud.LanePitch;
                Assert.That(pitch, Is.EqualTo(LegacySkillGear.DefaultPitch), "Three default gears fit between 넘기기 and 숨고르기.");
                Assert.That(Position(hud.Root, "Current Q"), Is.EqualTo(new Vector2(-pitch, hud.SkillWindowY)), "Three lanes spread out again.");
                Assert.That(Position(hud.Root, "Current W"), Is.EqualTo(new Vector2(0f, hud.SkillWindowY)));
                Assert.That(Position(hud.Root, "Current E"), Is.EqualTo(new Vector2(pitch, hud.SkillWindowY)));
                Assert.That(Position(hud.Root, "Next E").x - Position(hud.Root, "Current E").x,
                    Is.EqualTo(Position(hud.Root, "Next Q").x - Position(hud.Root, "Current Q").x).Within(.01f), "Each next slot follows its gear.");
                Assert.That(Named(hud.Root, "Idler 1").gameObject.activeSelf && Named(hud.Root, "Idler 2").gameObject.activeSelf, Is.True,
                    "An idler between each two open gears.");
                Assert.That(CycleEffect(hud.Root), Is.EqualTo("모든 열 한 칸"));
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
        private static Vector2 Position(GameObject root, string name) => Named(root, name).GetComponent<RectTransform>().anchoredPosition;
        private static Vector2 Size(GameObject root, string name) => Named(root, name).GetComponent<RectTransform>().sizeDelta;
        private static string CycleEffect(GameObject root) => Label(Named(root, "CycleButton").gameObject, "Effect").text;
        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();
        private static string Caption(GameObject root, string name) => Button(root, name).GetComponentInChildren<Text>().text;
    }
}
