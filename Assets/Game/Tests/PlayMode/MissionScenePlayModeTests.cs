using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The 서막's battlefield scenes through the real controller: intros and outros on the mission's arena, the last
    /// mission's 수훈 event in the middle of its battle, and the forced loss after it. The scenes are written here and handed
    /// to the controller in place of the Resources files, so the author's scenes can change freely.</summary>
    public sealed class MissionScenePlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static PrologueMission MissionFour => PrologueMissions.Get(4);
        private static int Laudare => MissionFour.Empowerment.EnemyScript.AllSkills[0].Id;

        [Test]
        public void MissionIntroAndOutro_PlayOnTheMissionsBattlefield_AndLeadOnAsTheirDialoguesDid()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                PrologueMission first = PrologueMissions.Get(1);
                scope.Scenes[first.IntroCutscene] = "@actor dummy pose hurt\n(허수아비?)\n@actor elisa move -3 .2\n둘째";
                scope.Scenes[first.OutroCutscene] = "@actor elisa face left\n@actor elisa move -3 0\n뭔가, 익숙해…";
                controller.StartNewGame();
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(scope.Asked, Is.EqualTo(new[] { first.IntroCutscene }), "The battlefield cutscene is looked up first.");
                Assert.That(controller.IsPlayingCutscene, Is.True);
                Assert.That(controller.IsShowingDialogue || controller.IsMission || controller.IsInBriefing, Is.False);
                Assert.That(controller.BriefingHud.IsVisible || controller.Hud.Root.activeSelf, Is.False);
                Assert.That(controller.Cutscene.ResumesBattle, Is.False);
                Assert.That(controller.Cutscene.IsVisible(CutsceneActor.Elisa) && controller.Cutscene.IsVisible(CutsceneActor.Dummy),
                    Is.True, "The mission's fighters already stand on its battlefield…");
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-5f).Within(1e-4f), "…where its battle starts them…");
                Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(arena.PlayerRenderer.flipX || arena.EnemyRenderer.flipX, Is.False, "…facing each other…");
                Vector3 camera = arena.ArenaCamera.transform.localPosition;
                Assert.That(camera.x, Is.EqualTo(0f).Within(1e-4f), "…under the duel's camera, with no fade.");
                Assert.That(camera.y, Is.EqualTo(-1.5f).Within(1e-4f));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(6f).Within(1e-4f));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("dummy-hurt-frame-"), "The script poses the dummy at once.");
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("(허수아비?)"));

                Assert.That(controller.AdvanceCutscene(), Is.True);
                for (int frame = 0; frame < 10; frame++) scope.Advance(.05f);
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-3f).Within(1e-3f));
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("둘째"));
                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsMission && controller.ActiveMission.Number == 1, Is.True, "The intro's end starts the duel…");
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-5f).Within(1e-4f), "…from its own starting places.");
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Assert.That(controller.Hud.EnemyName, Is.EqualTo("허수아비"));

                scope.SetGuide(null);
                var zero = new LegacySkill(100, "Test", 1, 0, 0, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "", iconId: 1);
                scope.InstallDuel(new LegacyQueuedDuel(100, 50, 1, 0, LegacyInitialSkills.All, new[] { zero }, new[] { 1 }, 3));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsPlayingScene || controller.IsShowingResult, "The win settles.");
                Assert.That(controller.IsPlayingCutscene, Is.True, "A win plays the outro cutscene before the result.");
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(1));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy), "The outro stands on the same battlefield.");
                Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(arena.PlayerRenderer.flipX, Is.True, "Its script turns Elisa away at once.");
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsShowingResult && controller.Result.IsMission && controller.Result.Victory, Is.True);
                Assert.That(controller.Result.StageNumber, Is.EqualTo(1));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy),
                    "The result shows over the outro's last picture: the dummy, not the knight who has yet to appear…");
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-3f).Within(1e-4f),
                    "…and Elisa where the outro left her.");
                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(controller.IsPlayingScene, Is.False, "A retry still skips the intro.");
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-5f).Within(1e-4f),
                    "The retried duel restages the arena.");
                Assert.That(arena.PlayerRenderer.flipX, Is.False);
            }
        }

        [Test]
        public void WithoutACutscene_AMissionPlaysItsDialogue()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                for (int number = 1; number <= PrologueMissions.Count; number++)
                    Assert.That(controller.Prologue.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(controller.Campaign.TryStartStage(1), Is.True);
                Assert.That(controller.Campaign.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(controller.OpenNextMission(), Is.True);
                PrologueMission briefed = controller.BriefingHud.Mission;
                Assert.That(briefed.Number, Is.EqualTo(PrologueMissions.Count + 1));
                scope.Asked.Clear();
                Assert.That(controller.StartMission(), Is.True);
                Assert.That(scope.Asked, Is.EqualTo(new[] { briefed.IntroCutscene }));
                Assert.That(controller.IsShowingDialogue, Is.True, "The missions after the 서막 keep their dialogue placeholders.");
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsPlayingScene, Is.True);
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsMission && controller.ActiveMission.Number == briefed.Number, Is.True);
                Assert.That(controller.SkipScene(), Is.False, "Nothing is left to skip.");
            }
        }

        [UnityTest]
        public IEnumerator MissionFourEvent_PausesOnItsHit_PlaysOnTheArenaAsItStands_ResumesEmpowered_AndTheLossEndsTheArc()
        {
            yield return null;
            using (var scope = new SceneScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                scope.Scenes[MissionFour.Empowerment.Scene] =
                    "@actor knight move 2 .2\n@aura knight on 0\n@camera 0 4 .2\n도미니코 기사단, 수훈!";
                scope.Scenes[MissionFour.OutroCutscene] = "헉… 헉…\n@aura knight off 0\n(갑자기 강해졌어…)";
                scope.StartMissionFour();
                scope.InstallDuel(EventDuel(Flurry()));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => scope.Field<bool>("missionEventPending"), "The flurry brings 이아 to half health.");
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "Before her 수훈 she leaves no afterimage.");
                bool ghosts = controller.PresentationSettings.EmpowermentAfterimageAlpha > 0f;
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.HitsResolved, Is.EqualTo(2), "The event waits on the hit that crossed half…");
                Assert.That(slot.HitCount, Is.EqualTo(3), "…with the flurry's last hit still to come.");
                Assert.That(controller.EnemyHealth, Is.EqualTo(20));
                Assert.That(controller.IsBattlePausedForEvent || controller.IsPlayingCutscene, Is.False, "That hit's stop plays first.");

                // The frame the battle pauses on: what the scene has to give back.
                scope.SetField("hitStopRemaining", 0f);
                Transform player = arena.PlayerRenderer.transform, enemy = arena.EnemyRenderer.transform;
                Transform camera = arena.ArenaCamera.transform;
                Vector3 playerAt = player.localPosition, enemyAt = enemy.localPosition, cameraAt = camera.localPosition;
                Quaternion cameraRoll = camera.localRotation;
                float cameraSize = arena.ArenaCamera.orthographicSize;
                Sprite playerSprite = arena.PlayerRenderer.sprite, enemySprite = arena.EnemyRenderer.sprite;
                float slotTime = controller.ActiveSlotElapsedTime;
                scope.Advance(0f);
                Assert.That(controller.IsBattlePausedForEvent && controller.IsPlayingCutscene, Is.True);
                Assert.That(controller.Cutscene.ResumesBattle && arena.IsSuspendedForCutscene, Is.True);
                Assert.That(controller.Hud.Root.activeSelf || controller.CoachHud.IsVisible, Is.False, "The duel's HUD leaves for the scene.");
                Assert.That(controller.CanStep, Is.False);
                Assert.That(enemy.localPosition.x, Is.EqualTo(enemyAt.x).Within(1e-4f), "The scene starts where the battle stands…");
                Assert.That(player.localPosition.x, Is.EqualTo(playerAt.x).Within(1e-4f));
                Assert.That(camera.localPosition.x, Is.EqualTo(cameraAt.x).Within(1e-4f), "…camera included.");
                Assert.That(camera.localPosition.y, Is.EqualTo(cameraAt.y).Within(1e-4f));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(cameraSize).Within(1e-4f));

                for (int frame = 0; frame < 20 && controller.DialogueHud.CurrentLine == null; frame++) scope.Advance(.05f);
                Assert.That(controller.DialogueHud.CurrentLine?.Text, Is.EqualTo("도미니코 기사단, 수훈!"));
                Assert.That(enemy.localPosition.x, Is.EqualTo(2f).Within(1e-3f), "The scene stages its own way…");
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(4f).Within(1e-3f));
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True);
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.EqualTo(2), "…while the duel waits where it paused.");
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime));
                Assert.That(controller.EnemyHealth, Is.EqualTo(20));
                Assert.That(controller.IsMissionEmpowered, Is.False, "Not before the scene ends.");
                for (int frame = 0; frame < 6; frame++) scope.Advance(.05f);
                if (ghosts)
                    Assert.That(arena.ActiveAuraAfterimageCount, Is.GreaterThan(0),
                        "From the moment her aura comes on in the scene she leaves yellow afterimages, on its real time.");
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime));

                Press(keyboard.escapeKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.escapeKey);
                Assert.That(controller.IsPlayingCutscene || controller.IsBattlePausedForEvent, Is.False, "Escape skips the scene…");
                Assert.That(controller.IsMission, Is.True, "…without leaving the mission…");
                Assert.That(controller.IsMissionEmpowered, Is.True, "…and still empowers 이아.");
                Assert.That(arena.IsSuspendedForCutscene, Is.False);
                Assert.That(enemy.localPosition, Is.EqualTo(enemyAt), "The battle gets its picture back.");
                Assert.That(player.localPosition, Is.EqualTo(playerAt));
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemySprite));
                Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerSprite));
                Assert.That(camera.localPosition, Is.EqualTo(cameraAt));
                Assert.That(Quaternion.Angle(camera.localRotation, cameraRoll), Is.LessThan(1e-3f));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(cameraSize));
                Assert.That(arena.FlashbackAmount, Is.Zero);
                Assert.That(controller.Hud.Root.activeSelf, Is.True);
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True, "The 수훈 aura stays on for the rest of the battle.");
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero,
                    "The scene's afterimages stay with the scene: her trail starts afresh from her place in the battle.");
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.EqualTo(2));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime), "The slot resumes on the frame it paused on.");
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsMission, Is.True, "The skip key never abandons the battle.");

                scope.AdvanceUntil(() => controller.CanChoose, "The turn plays out to the next planning.");
                Assert.That(controller.EnemyHealth, Is.EqualTo(10), "The flurry's last hit landed after the scene.");
                Assert.That(controller.Session.EnemyQueue, Is.Not.Empty);
                Assert.That(controller.Session.EnemyQueue.Select(skill => skill.Id),
                    Is.EqualTo(MissionFour.Empowerment.EnemyScript.Turn(1).Select(skill => skill.Id)).And.EqualTo(new[] { Laudare, 501, 502 }),
                    "From the next turn she plays the motto: 라우다레, 베네디체레, 프레디카레.");
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True);
                for (int frame = 0; frame < 8; frame++) scope.Advance(.025f);
                if (ghosts)
                    Assert.That(arena.ActiveAuraAfterimageCount, Is.GreaterThan(0), "Through the rest of the battle she trails them.");

                scope.LoseTheBattle();
                Assert.That(controller.IsPlayingCutscene, Is.True, "The defeat after the 수훈 plays the outro…");
                Assert.That(controller.IsShowingResult || controller.ResultHud.IsVisible, Is.False, "…with no defeat result.");
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(PrologueMissions.Count), "The 서막 is complete.");
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True, "The outro opens with the aura still on her…");
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-5f).Within(1e-4f), "…at the battle's starting places.");
                Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("헉… 헉…"));
                for (int frame = 0; frame < 6; frame++) scope.Advance(.05f);
                if (ghosts) Assert.That(arena.ActiveAuraAfterimageCount, Is.GreaterThan(0), "Her afterimages go on in the outro…");
                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.False, "…until the outro's @aura knight off.");
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "Switched off at once, it takes them with it.");
                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(controller.IsPlayingCutscene, Is.False);
                Assert.That(controller.IsInLobby, Is.True, "Then the lobby, where the old win's 여정 계속 went…");
                Assert.That(controller.Result, Is.Null);
                Assert.That(controller.IsMissionEmpowered, Is.False);
                Assert.That(ActiveTexts(controller.LobbyHud.Root, "Outcome Banner"),
                    Is.EqualTo(new[] { BattleResultHud.PrologueCompleteNotice(controller.Campaign.IsCurriculumOpen) }),
                    "…which says what the old win's result said: the 서막 is over and what it opened.");
                controller.LobbyHud.ResetView();
                Assert.That(ActiveTexts(controller.LobbyHud.Root, "Outcome Banner"), Is.Empty, "It is a note, not saved state.");
            }
        }

        [Test]
        public void ADefeatBeforeTheEvent_IsAnOrdinaryFailure_AndEveryAttemptStartsUnempowered()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                scope.StartMissionFour();
                var crush = new LegacySkill(203, "Test Crush", 0, 50, 50, LegacySkillKind.Attack, LegacySkillProperty.Hit, 1, 0, "", iconId: 1);
                scope.InstallDuel(new LegacyQueuedDuel(1, 0, 40, 10, new[] { Strike() }, new[] { crush }, new[] { 1 }, 4,
                    features: CombatFeature.LaneQ | CombatFeature.Cycle, enemyHealthFloor: 1, enemyHealthThresholdPercent: 50));
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsShowingResult, "The player falls before 이아 reaches half health.");
                Assert.That(controller.Result.IsMission, Is.True);
                Assert.That(controller.Result.Victory, Is.False, "A defeat before the 수훈 is an ordinary failure…");
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(PrologueMissions.Count - 1), "…that completes nothing.");
                Assert.That(controller.IsMissionEmpowered, Is.False);
                Assert.That(controller.RetryBattleResult(), Is.True);
                AssertFreshAttempt(controller);

                // No event scene here (nor a dialogue): the empowerment comes at once, without a pause.
                scope.SetGuide(null);
                scope.InstallDuel(EventDuel(Strike()));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsMissionEmpowered || controller.IsBattlePausedForEvent, "The strike reaches half.");
                Assert.That(controller.IsBattlePausedForEvent || controller.IsPlayingScene, Is.False);
                Assert.That(controller.IsMissionEmpowered, Is.True);
                Assert.That(controller.ArenaView.EnemyPowerAura.IsAuraOn, Is.True, "The aura comes on without a scene too.");
                Assert.That(scope.Asked, Does.Contain(MissionFour.Empowerment.Scene));

                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True, "Abandoning goes back to the briefing…");
                Assert.That(controller.IsMissionEmpowered, Is.False);
                Assert.That(controller.ArenaView.EnemyPowerAura.IsAuraOn, Is.False, "…and takes the aura away…");
                Assert.That(controller.ArenaView.ActiveAuraAfterimageCount, Is.Zero, "…and every afterimage.");
                Assert.That(controller.StartMission(), Is.True);
                if (controller.IsPlayingScene) Assert.That(controller.SkipScene(), Is.True);
                AssertFreshAttempt(controller);
            }
        }

        [Test]
        public void TheLastMissionsBattle_NamesIa_WhileItsBriefingKeepsTheWanderingKnight()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.StartNewGame();
                for (int number = 1; number < PrologueMissions.Count; number++)
                    Assert.That(controller.Prologue.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
                scope.ShowBriefing();
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(PrologueMissions.Count));
                Assert.That(ActiveTexts(controller.BriefingHud.Root, "Enemy Name"), Is.EqualTo(new[] { "떠돌이 기사" }),
                    "The briefing keeps the name she is known by…");
                Assert.That(controller.StartMission(), Is.True);
                if (controller.IsPlayingScene) Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.Hud.EnemyName, Is.EqualTo("이아"), "…the battle, after her intro, says 이아…");
                Transform status = controller.Hud.Root.transform.Find("EnemyStatus");
                Assert.That(status, Is.Not.Null);
                Assert.That(status.Find("Battle Name").GetComponent<Text>().text, Is.EqualTo("이아"));
                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.Guide.Title, Does.Contain("이아").And.Not.Contain("떠돌이 기사"), "…and so does the coach.");

                controller.RestartMatch();
                Assert.That(controller.IsMission, Is.False);
                Assert.That(controller.Hud.EnemyName, Is.Empty, "Stages name no one.");
                Assert.That(controller.Hud.Root.transform.Find("PlayerStatus/Battle Name"), Is.Null, "Only the enemy panel has a name.");
            }
        }

        [Test]
        public void ASceneInTheMiddleOfABattle_HidesTheBreakOutlineAndTheHitsGrade_AndGivesTheArenaBackExactly()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                controller.RestartMatch();
                LegacyArenaView arena = controller.ArenaView;
                Transform player = arena.PlayerRenderer.transform, enemy = arena.EnemyRenderer.transform;
                Transform camera = arena.ArenaCamera.transform;
                player.localPosition = new Vector3(-1.25f, -.5f, 0f);
                enemy.localPosition = new Vector3(2.5f, -.5f, 0f);
                camera.localPosition = new Vector3(.6f, -.4f, -10f);
                camera.localRotation = Quaternion.Euler(0f, 0f, 8f);
                arena.ArenaCamera.orthographicSize = 3.5f;
                // A decisive hit's close-up grade. It fades only on the battle's clock, which stops for the scene.
                arena.PresentHit(true, 8, 0, false, true, 0);
                Assert.That(arena.ArenaProfile.TryGet(out ChromaticAberration aberration), Is.True);
                Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments grade), Is.True);
                float aberrationAt = aberration.intensity.value, exposureAt = grade.postExposure.value;
                float saturationAt = grade.saturation.value;
                Assert.That(aberrationAt, Is.EqualTo(1f));
                Assert.That(exposureAt, Is.GreaterThanOrEqualTo(1f));
                Assert.That(saturationAt, Is.EqualTo(25f));
                arena.SetResistanceBroken(false, true);
                arena.EnemyBreakAura.Tick(1f);
                bool outline = arena.EnemyBreakAura.HasRequiredAssets;
                if (outline) Assert.That(arena.EnemyBreakAura.IsVisible, Is.True);
                Vector3 playerAt = player.localPosition, enemyAt = enemy.localPosition, cameraAt = camera.localPosition;
                Quaternion cameraRoll = camera.localRotation;
                Sprite enemySprite = arena.EnemyRenderer.sprite;

                CutsceneScript script = CutsceneScriptParser.Parse("Cutscene/test", "@actor knight move 4 .2\n@actor elisa face left\n하나",
                    MissionFour.SceneCast);
                var director = new CutsceneDirector(script, arena, controller.CutsceneHud, controller.DialogueHud, null, resumesBattle: true);
                try
                {
                    director.Start();
                    Assert.That(arena.IsSuspendedForCutscene, Is.True);
                    if (outline) Assert.That(arena.EnemyBreakAura.IsVisible, Is.False, "The outline would trace the battle's frame.");
                    Assert.That(enemy.localPosition.x, Is.EqualTo(2.5f).Within(1e-4f), "The fighters stay where the battle has them…");
                    Assert.That(player.localPosition.x, Is.EqualTo(-1.25f).Within(1e-4f));
                    Assert.That(arena.PlayerRenderer.flipX, Is.False);
                    Assert.That(camera.localPosition.x, Is.EqualTo(.6f).Within(1e-4f), "…and the camera keeps its framing…");
                    Assert.That(camera.localPosition.y, Is.EqualTo(-.4f).Within(1e-4f));
                    Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(3.5f).Within(1e-4f));
                    Assert.That(aberration.intensity.value, Is.Zero, "The scene plays under the neutral grade…");
                    Assert.That(grade.postExposure.value, Is.Zero);
                    Assert.That(grade.saturation.value, Is.Zero);
                    director.Tick(.35f);
                    Assert.That(aberration.intensity.value, Is.Zero, "…however long it lasts.");
                    Assert.That(grade.postExposure.value, Is.Zero);
                    Assert.That(grade.saturation.value, Is.Zero);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, camera.localEulerAngles.z)), Is.LessThan(.01f), "…levelling the battle's tilt.");
                    Assert.That(enemy.localPosition.x, Is.EqualTo(4f).Within(1e-4f));
                    Assert.That(arena.PlayerRenderer.flipX, Is.True);
                    Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("하나"));
                }
                finally { director.Dispose(); }
                Assert.That(arena.IsSuspendedForCutscene, Is.False);
                Assert.That(enemy.localPosition, Is.EqualTo(enemyAt), "Everything the scene moved is back.");
                Assert.That(player.localPosition, Is.EqualTo(playerAt));
                Assert.That(arena.PlayerRenderer.flipX, Is.False);
                Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemySprite));
                Assert.That(camera.localPosition, Is.EqualTo(cameraAt));
                Assert.That(Quaternion.Angle(camera.localRotation, cameraRoll), Is.LessThan(1e-3f));
                Assert.That(arena.ArenaCamera.orthographicSize, Is.EqualTo(3.5f));
                Assert.That(aberration.intensity.value, Is.EqualTo(aberrationAt), "The close-up's grade is back, to fade from there.");
                Assert.That(grade.postExposure.value, Is.EqualTo(exposureAt));
                Assert.That(grade.saturation.value, Is.EqualTo(saturationAt));
                if (outline) Assert.That(arena.EnemyBreakAura.IsVisible, Is.True, "The break outline is back.");
                controller.RestartMatch();
                Assert.That(arena.EnemyBreakAura.IsVisible, Is.False);
                Assert.That(aberration.intensity.value + grade.postExposure.value + grade.saturation.value, Is.Zero);
            }
        }

        [Test]
        public void TheSutunPhase_CutsInOnHerTechniques_WarmsTheGrade_ShakesOnHerBarrage_AndLeavesWithTheAttempt()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                DuelEmpowermentCues cues = controller.EmpowermentCues;
                DuelPresentationSettings settings = controller.PresentationSettings;
                Assume.That(settings.EmpowermentCutInSeconds, Is.GreaterThan(.2f));
                Assume.That(settings.LaudareShakeStrength, Is.GreaterThan(0f));
                Assert.That(arena.ArenaProfile.TryGet(out ColorAdjustments grade), Is.True);
                Assert.That(arena.ArenaProfile.TryGet(out Vignette vignette), Is.True);
                float baseVignette = vignette.intensity.value;
                scope.StartMissionFour();
                Assert.That(cues.IsActive, Is.False, "Before her 수훈 the battle looks like any other.");
                Assert.That(arena.EmpowermentAmount, Is.Zero);
                // No event scene here: the empowerment comes on the hit's frame.
                scope.InstallDuel(EventDuel(Strike()));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsMissionEmpowered, "The strike brings 이아 to half health.");
                Assert.That(cues.IsActive, Is.True, "The 수훈 phase begins with the empowerment…");
                Assert.That(arena.EmpowermentAmount, Is.LessThan(1f), "…easing in rather than switching on.");

                scope.AdvanceUntil(() => controller.CanChoose, "The turn plays out to the next planning.");
                for (int frame = 0; frame < 80; frame++) scope.Advance(.025f);
                Assert.That(arena.EmpowermentAmount, Is.EqualTo(1f), "Then the warm grade is all there…");
                Color filter = grade.colorFilter.value;
                if (settings.EmpowermentWarmTint > 0f)
                    Assert.That(filter.r > filter.g && filter.g > filter.b, Is.True, "…golden…");
                Assert.That(vignette.intensity.value, Is.EqualTo(baseVignette + settings.EmpowermentVignette).Within(1e-4f),
                    "…with darker edges…");
                if (cues.LoopClip != null && settings.EmpowermentAuraLoopVolume > 0f)
                {
                    Assert.That(cues.IsLoopPlaying, Is.True, "…and her aura hums quietly under the fight.");
                    Assert.That(cues.LoopVolume, Is.EqualTo(settings.EmpowermentAuraLoopVolume).Within(1e-4f));
                }
                Assert.That(controller.Session.EnemyQueue.Select(skill => skill.Id), Is.EqualTo(new[] { Laudare, 501, 502 }));

                controller.CommitTurn();
                scope.AdvanceUntil(() => cues.IsCuttingIn, "라우다레 opens the motto turn with a cut-in.");
                LegacyCurrentSlot slot = controller.Session.CurrentSlot;
                Assert.That(slot.EnemySkill.Id, Is.EqualTo(Laudare));
                Assert.That(slot.HitsResolved, Is.Zero, "It comes before the first hit.");
                Assert.That(cues.CutInRemaining, Is.EqualTo(settings.EmpowermentCutInSeconds).Within(1e-4f));
                Assert.That(arena.EnemyPowerAura.IsCharging, Is.True, "Her aura gathers for its flare…");
                float slotTime = controller.ActiveSlotElapsedTime;
                float cameraSize = arena.ArenaCamera.orthographicSize;
                scope.Advance(.1f);
                scope.Advance(.1f);
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime), "…while the battle holds…");
                Assert.That(slot.HitsResolved, Is.Zero);
                Assert.That(cues.CutInRemaining, Is.EqualTo(settings.EmpowermentCutInSeconds - .2f).Within(1e-4f), "…on real time…");
                Assert.That(arena.CutInAmount, Is.GreaterThan(0f), "…and the camera eases toward her.");
                Assert.That(arena.ArenaCamera.orthographicSize, Is.LessThan(cameraSize));
                Assert.That(controller.CanStep, Is.False);
                scope.AdvanceUntil(() => !cues.IsCuttingIn, "The cut-in runs its length.");
                Assert.That(arena.CutInAmount, Is.Zero, "The camera is on its way back by the first hit.");

                scope.AdvanceUntil(() => slot.HitsResolved >= 1, "라우다레's first hit lands.");
                Assert.That(arena.CameraShakeAmplitude, Is.GreaterThan(0f), "Each of its hits shakes the camera…");
                Assert.That(arena.CameraShakeAmplitude, Is.LessThanOrEqualTo(settings.LaudareShakeStrength + 1e-4f));
                scope.AdvanceUntil(() => slot.HitsResolved >= 2 || controller.Session.IsFinished, "Its next hit lands.");
                Assert.That(arena.CameraShakeAmplitude, Is.LessThanOrEqualTo(settings.LaudareShakeStrength + 1e-4f),
                    "…a little, however many hits come.");

                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(cues.IsActive || cues.IsCuttingIn, Is.False, "Leaving ends the 수훈 phase…");
                Assert.That(arena.EmpowermentAmount + arena.CutInAmount + arena.CameraShakeAmplitude, Is.Zero,
                    "…its grade, cut-in and shake…");
                Assert.That(vignette.intensity.value, Is.EqualTo(baseVignette).Within(1e-4f));
                Assert.That(grade.colorFilter.value, Is.EqualTo(Color.white));
                Assert.That(controller.Finale.Letterbox.IsVisible, Is.False);
                for (int frame = 0; frame < 30; frame++) scope.Advance(.025f);
                Assert.That(cues.IsLoopPlaying, Is.False, "…and its hum fades out.");
            }
        }

        [Test]
        public void TheFinalFall_HoldsTheFinishingBlow_DrainsToGrey_AndTheOutroRegainsItsColour()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                DuelFinale finale = controller.Finale;
                DuelPresentationSettings settings = controller.PresentationSettings;
                Assume.That(settings.FinishingFreezeSeconds, Is.GreaterThan(0f));
                Assume.That(settings.FinalFallGreySeconds, Is.GreaterThan(.3f));
                Assume.That(settings.FinalFallColourReturnSeconds, Is.GreaterThan(.2f));
                scope.Scenes[MissionFour.OutroCutscene] = "헉… 헉…\n(갑자기 강해졌어…)";
                scope.StartMissionFour();
                scope.InstallDuel(EventDuel(Strike()));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsMissionEmpowered, "The strike brings 이아 to half health.");
                scope.PlayUntil(() => finale.IsRunning, "The empowered 이아 ends the battle.");
                Assert.That(controller.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
                Assert.That(finale.Timeline.Current, Is.EqualTo(LegacyFinishingBlow.Stage.SlowMotion),
                    "Her last blow gets the finishing blow's slow motion…");
                Assert.That(arena.IsFinishingFocus, Is.True);

                scope.AdvanceUntil(() => finale.IsFrozen, "…then its freeze frame…");
                Assert.That(arena.FlashbackAmount, Is.Zero, "…still in colour.");
                Assert.That(controller.Hud.Root.activeSelf && controller.Hud.Fade == 1f, Is.True, "The duel HUD is still up.");
                Assert.That(finale.Letterbox.Amount, Is.EqualTo(1f).Within(1e-3f));
                Vector3 elisaAt = arena.PlayerRenderer.transform.localPosition;
                bool ghosts = settings.EmpowermentAfterimageAlpha > 0f;
                SpriteRenderer[] frozenGhosts = AuraGhosts(arena);
                if (ghosts) Assert.That(frozenGhosts, Is.Not.Empty, "Her yellow afterimages trail her last blow…");
                // As drawn: the silhouette's tint (the sprite colour stays white there) or the fallback's sprite colour.
                float[] frozenAlphas = frozenGhosts.Select(view => DuelStepAfterimages.DrawnColor(view).a).ToArray();
                if (ghosts) Assert.That(frozenAlphas, Has.All.GreaterThan(0f));
                Vector3[] frozenPlaces = frozenGhosts.Select(view => view.transform.position).ToArray();
                scope.AdvanceUntil(() => finale.Timeline.Current == LegacyFinishingBlow.Stage.Fall, "The freeze runs its length.");
                Assert.That(controller.IsPlayingCutscene || controller.IsShowingResult, Is.False);
                float grey = arena.FlashbackAmount, bars = finale.Letterbox.Amount;
                for (int frame = 0; frame < 8; frame++) scope.Advance(.025f);
                Assert.That(arena.FlashbackAmount, Is.GreaterThan(grey), "Then the colour drains as in a flashback…");
                Assert.That(finale.Letterbox.Amount, Is.LessThan(bars), "…while the bars slide out…");
                Assert.That(controller.Hud.Fade, Is.LessThan(1f).And.EqualTo(1f - finale.Timeline.FallAmount).Within(1e-4f),
                    "…and the duel HUD, an overlay the grade never reaches, fades out with the colour…");
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(elisaAt), "…over the still picture.");
                Assert.That(AuraGhosts(arena), Is.EqualTo(frozenGhosts), "…her afterimages held in it as she is…");
                Assert.That(frozenGhosts.Select(view => DuelStepAfterimages.DrawnColor(view).a), Is.EqualTo(frozenAlphas));
                Assert.That(frozenGhosts.Select(view => view.transform.position), Is.EqualTo(frozenPlaces));

                scope.AdvanceUntil(() => controller.IsPlayingCutscene, "Then the outro, with no defeat result.");
                Assert.That(controller.IsShowingResult, Is.False);
                Assert.That(controller.Cutscene.OpensFromGreySeconds, Is.EqualTo(settings.FinalFallColourReturnSeconds));
                Assert.That(arena.FlashbackAmount, Is.EqualTo(1f), "The outro takes over in black and white…");
                Assert.That(arena.ActiveAuraAfterimageCount, Is.Zero, "…leaving the battle's afterimages behind…");
                Assert.That(finale.Letterbox.IsVisible || finale.IsRunning, Is.False, "…with no bars or freeze left…");
                Assert.That(controller.Hud.Root.activeSelf, Is.False, "…no duel HUD…");
                Assert.That(controller.Hud.Fade, Is.EqualTo(1f), "…(its fade undone for the next battle)…");
                Assert.That(controller.EmpowermentCues.IsActive, Is.False);
                Assert.That(arena.EmpowermentAmount, Is.Zero, "…nor the battle's warm grade…");
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True, "…while her aura stays until the outro ends it.");
                float half = settings.FinalFallColourReturnSeconds * .5f;
                scope.Advance(half);
                Assert.That(arena.FlashbackAmount, Is.EqualTo(.5f).Within(.01f), "It regains its colour…");
                if (ghosts) Assert.That(arena.ActiveAuraAfterimageCount, Is.GreaterThan(0), "…her aura's trail going on in it…");
                scope.Advance(half + .05f);
                Assert.That(arena.FlashbackAmount, Is.Zero, "…in its set time.");
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("헉… 헉…"));
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(arena.FlashbackAmount, Is.Zero);
            }
        }

        [Test]
        public void TheCoachsAbandon_NeverThrowsAwayADecidedBattle_ItSkipsTheFinishingBlowInstead()
        {
            using (var scope = new SceneScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelFinale finale = controller.Finale;
                Assume.That(controller.PresentationSettings.FinishingSlowMotionSeconds, Is.GreaterThan(0f));
                Button abandon = controller.CoachHud.Root.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "Coach Abandon");
                scope.Scenes[MissionFour.OutroCutscene] = "헉… 헉…";

                // Before the battle is decided, 임무 포기 leaves it for the briefing.
                scope.StartMissionFour();
                abandon.onClick.Invoke();
                Assert.That(controller.IsInBriefing, Is.True);

                scope.StartMissionFour();
                scope.InstallDuel(EventDuel(Strike()));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsMissionEmpowered, "The strike brings 이아 to half health.");
                scope.PlayUntil(() => finale.IsRunning, "The empowered 이아 ends the battle.");
                Assert.That(controller.Outcome, Is.EqualTo(DuelMatchOutcome.EnemyVictory));
                abandon.onClick.Invoke();
                Assert.That(controller.IsInBriefing, Is.False, "In the finishing blow the decided battle is not thrown away…");
                Assert.That(controller.IsPlayingCutscene, Is.True, "…its outro comes at once…");
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(PrologueMissions.Count), "…and the 서막 is complete.");
                Assert.That(finale.IsRunning || finale.Letterbox.IsVisible, Is.False);
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsInLobby, Is.True);
            }
        }

        private static void AssertFreshAttempt(DuelPrototypeController controller)
        {
            Assert.That(controller.IsMission && controller.ActiveMission.Number == PrologueMissions.Count, Is.True);
            Assert.That(controller.IsMissionEmpowered || controller.IsBattlePausedForEvent, Is.False, "Each attempt starts before the 수훈.");
            Assert.That(controller.Session.EnemyHealthThresholdReached, Is.False);
            DuelPowerAura aura = controller.ArenaView.EnemyPowerAura;
            Assert.That(aura.IsAuraOn || aura.AuraAmount > 0f, Is.False);
            Assert.That(controller.ArenaView.ActiveAuraAfterimageCount, Is.Zero, "No 수훈 afterimage is left behind.");
            Assert.That(controller.Session.EnemyQueue.Any(skill => skill.Id == Laudare), Is.False);
        }

        /// <summary>이아's 수훈 afterimages on screen now.</summary>
        private static SpriteRenderer[] AuraGhosts(LegacyArenaView arena)
            => arena.EnemyAuraAfterimages.Root.GetComponentsInChildren<SpriteRenderer>().ToArray();

        private static string[] ActiveTexts(GameObject root, string name)
            => root.GetComponentsInChildren<Text>().Where(text => text.name == name).Select(text => text.text).ToArray();

        // Three hits of 10 on a breathing 40-health fixture: the second crosses half, the third is still to come.
        private static LegacySkill Flurry() => new LegacySkill(201, "Test Flurry", 1, 30, 30, LegacySkillKind.Attack,
            LegacySkillProperty.Slash, 3, 0, "", iconId: 1);

        private static LegacySkill Strike() => new LegacySkill(202, "Test Strike", 1, 30, 30, LegacySkillKind.Attack,
            LegacySkillProperty.Slash, 1, 0, "", iconId: 1);

        /// <summary>이아 as a fixture: 40 health she cannot lose all of, her 수훈 at half, nothing but breathing until then.</summary>
        private static LegacyQueuedDuel EventDuel(LegacySkill playerSkill)
            => new LegacyQueuedDuel(100, 50, 40, 10, new[] { playerSkill }, new[] { LegacyCommonActions.Breathe }, new[] { 1 }, 5,
                features: CombatFeature.LaneQ | CombatFeature.Cycle, enemyHealthFloor: 1, enemyHealthThresholdPercent: 50);

        /// <summary>Hands the controller the scenes written in a test instead of the Resources files.</summary>
        private sealed class SceneScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly FieldInfo sceneSource;
            private readonly object originalSource;
            private readonly Action<float, Keyboard> advance;
            /// <summary>Scene texts by Resources path; any other path has no cutscene.</summary>
            public readonly Dictionary<string, string> Scenes = new Dictionary<string, string>();
            /// <summary>Every cutscene path the controller looked up, in order.</summary>
            public readonly List<string> Asked = new List<string>();
            public DuelPrototypeController Controller { get; }

            public SceneScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                sceneSource = typeof(DuelPrototypeController).GetField("missionSceneText", PrivateInstance);
                Assert.That(sceneSource, Is.Not.Null);
                originalSource = sceneSource.GetValue(Controller);
                sceneSource.SetValue(Controller, (Func<string, string>)(path =>
                {
                    Asked.Add(path);
                    return Scenes.TryGetValue(path, out string text) ? text : null;
                }));
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Controller.RestartJourney();
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void AdvanceUntil(Func<bool> condition, string message)
            {
                int frames = 0;
                while (!condition() && frames++ < 4000) Advance(.025f);
                Assert.That(condition(), Is.True, message);
            }

            public T Field<T>(string name) => (T)typeof(DuelPrototypeController).GetField(name, PrivateInstance).GetValue(Controller);

            public void SetField(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, PrivateInstance).SetValue(Controller, value);

            /// <summary>Drops the coach so a fixture duel can commit freely.</summary>
            public void SetGuide(MissionGuide guide) => SetField("guide", guide);

            public void InstallDuel(LegacyQueuedDuel duel)
            {
                SetField("session", duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            public void ShowBriefing()
                => typeof(DuelPrototypeController).GetMethod("ShowBriefing", PrivateInstance).Invoke(Controller, null);

            /// <summary>The 서막's last mission in battle: the missions before it counted won, its intro skipped, no coach.</summary>
            public void StartMissionFour()
            {
                Controller.StartNewGame();
                for (int number = 1; number < PrologueMissions.Count; number++)
                    Assert.That(Controller.Prologue.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(Controller.StartMission(), Is.True);
                if (Controller.IsPlayingScene) Assert.That(Controller.SkipScene(), Is.True);
                Assert.That(Controller.IsMission && Controller.ActiveMission.Number == PrologueMissions.Count, Is.True);
                SetGuide(null);
            }

            /// <summary>Commits empty turns until <paramref name="condition"/> holds.</summary>
            public void PlayUntil(Func<bool> condition, string message)
            {
                int frames = 0;
                while (!condition() && frames++ < 8000)
                {
                    if (Controller.CanChoose) Controller.CommitTurn();
                    Advance(.025f);
                }
                Assert.That(condition(), Is.True, message);
            }

            /// <summary>Commits empty turns until the battle ends in the player's defeat and something else takes the screen.</summary>
            public void LoseTheBattle()
            {
                int frames = 0;
                while (Controller.IsMission && !Controller.IsPlayingScene && !Controller.IsShowingResult && frames++ < 8000)
                {
                    if (Controller.CanChoose) Controller.CommitTurn();
                    Advance(.025f);
                }
                Assert.That(Controller.IsMission && !Controller.IsPlayingScene && !Controller.IsShowingResult, Is.False,
                    "The empowered enemy must end the battle.");
            }

            public void Dispose()
            {
                sceneSource.SetValue(Controller, originalSource);
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
            }
        }
    }
}
