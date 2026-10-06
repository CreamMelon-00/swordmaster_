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
                Assert.That(controller.Session.CurrentSlot.HitsResolved, Is.EqualTo(2));
                Assert.That(controller.ActiveSlotElapsedTime, Is.EqualTo(slotTime), "The slot resumes on the frame it paused on.");
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(controller.IsMission, Is.True, "The skip key never abandons the battle.");

                scope.AdvanceUntil(() => controller.CanChoose, "The turn plays out to the next planning.");
                Assert.That(controller.EnemyHealth, Is.EqualTo(10), "The flurry's last hit landed after the scene.");
                Assert.That(controller.Session.EnemyQueue, Is.Not.Empty);
                Assert.That(controller.Session.EnemyQueue.Select(skill => skill.Id), Is.All.EqualTo(Laudare),
                    "From the next turn she uses 라우다레.");
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True);

                scope.LoseTheBattle();
                Assert.That(controller.IsPlayingCutscene, Is.True, "The defeat after the 수훈 plays the outro…");
                Assert.That(controller.IsShowingResult || controller.ResultHud.IsVisible, Is.False, "…with no defeat result.");
                Assert.That(controller.Prologue.ClearedCount, Is.EqualTo(PrologueMissions.Count), "The 서막 is complete.");
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.True, "The outro opens with the aura still on her…");
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(-5f).Within(1e-4f), "…at the battle's starting places.");
                Assert.That(arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(controller.DialogueHud.CurrentLine.Text, Is.EqualTo("헉… 헉…"));
                Assert.That(controller.AdvanceCutscene(), Is.True);
                Assert.That(arena.EnemyPowerAura.IsAuraOn, Is.False, "…until the outro's @aura knight off.");
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
                Assert.That(controller.ArenaView.EnemyPowerAura.IsAuraOn, Is.False, "…and takes the aura away.");
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
                Assert.That(controller.Guide.Title, Is.EqualTo("이아"), "…and so does the coach.");

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

        private static void AssertFreshAttempt(DuelPrototypeController controller)
        {
            Assert.That(controller.IsMission && controller.ActiveMission.Number == PrologueMissions.Count, Is.True);
            Assert.That(controller.IsMissionEmpowered || controller.IsBattlePausedForEvent, Is.False, "Each attempt starts before the 수훈.");
            Assert.That(controller.Session.EnemyHealthThresholdReached, Is.False);
            DuelPowerAura aura = controller.ArenaView.EnemyPowerAura;
            Assert.That(aura.IsAuraOn || aura.AuraAmount > 0f, Is.False);
            Assert.That(controller.Session.EnemyQueue.Any(skill => skill.Id == Laudare), Is.False);
        }

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
