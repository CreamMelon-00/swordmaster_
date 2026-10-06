using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The opening mission's straw training dummy (Docs/Art/TrainingDummy): its imported cels, the authored
    /// idle/hurt timing, how the arena plays it, and which duels field it.</summary>
    public sealed class TrainingDummyAnimationPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        // Where each hurt cel starts (ms): [35,45,55,70,65,65,65,80,100,120] summed.
        private static readonly int[] HurtStarts = { 0, 35, 80, 135, 205, 270, 335, 400, 480, 580 };

        [Test]
        public void EighteenCelsImportAtFortyPixelsPerUnitWithTheAuthoredPivotAndStandOnTheStudentsGround()
        {
            using (var set = new TrainingDummyAnimationSet())
            using (var student = new EnemyStudentAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(set.MissingResources, Is.Empty);
                Assert.That(TrainingDummyAnimationSet.RequiredSpriteCount, Is.EqualTo(18));
                Assert.That(student.HasRequiredAssets, Is.True, string.Join(", ", student.MissingResources));
                Sprite studentIdle = student.GetIdle(0f);
                var textures = new HashSet<Texture2D>();
                for (int index = 0; index < TrainingDummyAnimationSet.RequiredSpriteCount; index++)
                {
                    bool idle = index < TrainingDummyAnimationSet.IdleFrameCount;
                    int cel = idle ? index + 1 : index - TrainingDummyAnimationSet.IdleFrameCount + 1;
                    string path = (idle ? "idle" : "hurt") + "/frame-" + cel.ToString("00");
                    var source = Resources.Load<Sprite>(TrainingDummyAnimationSet.ResourceRoot + path);
                    Assert.That(source, Is.Not.Null, path);
                    Assert.That(source.rect.size, Is.EqualTo(new Vector2(256, 224)), path);
                    Assert.That(source.pivot, Is.EqualTo(new Vector2(128, 22)), path);
                    Assert.That(source.pixelsPerUnit, Is.EqualTo(40), path);
                    Assert.That(source.texture.width, Is.EqualTo(256), path);
                    Assert.That(source.texture.height, Is.EqualTo(224), path);
                    Assert.That(source.texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(source.texture.mipmapCount, Is.EqualTo(1), path);

                    Sprite adapted = idle ? set.GetIdle((cel - 1) * .18f + .005f) : set.GetHurt(HurtStarts[cel - 1] / 1000f + .005f);
                    Assert.That(adapted, Is.Not.Null, path);
                    Assert.That(adapted.name, Is.EqualTo("dummy-" + path.Replace('/', '-')));
                    Assert.That(adapted.texture, Is.SameAs(source.texture), path);
                    Assert.That(adapted.rect, Is.EqualTo(source.rect), path);
                    Assert.That(adapted.pixelsPerUnit, Is.EqualTo(40), path);
                    Assert.That(adapted.pivot.x, Is.EqualTo(128f).Within(.001f), path);
                    // The same ground shift as the other duel characters, so the stand's base meets the shadow.
                    Assert.That(adapted.pivot.y, Is.EqualTo(22f - MobStudentAnimationSet.GroundOffset * 40f).Within(.001f), path);
                    Assert.That(adapted.pivot.y, Is.EqualTo(studentIdle.pivot.y).Within(.0001f),
                        path + " stands on the same ground as the student.");
                    textures.Add(adapted.texture);
                }
                Assert.That(textures.Count, Is.EqualTo(18), "Every cel is its own imported frame.");
            }
        }

        [Test]
        public void IdleSwaysEveryCelFor180MsAndLoopsWhileHurtKeepsTheAuthoredTimingAndHoldsItsLastCel()
        {
            using (var set = new TrainingDummyAnimationSet())
            {
                Assert.That(set.HasRequiredAssets, Is.True, string.Join(", ", set.MissingResources));
                Assert.That(TrainingDummyAnimationSet.IdleDuration, Is.EqualTo(1.44f).Within(1e-5f));
                Assert.That(TrainingDummyAnimationSet.HurtDuration, Is.EqualTo(.70f).Within(1e-5f));

                Assert.That(set.GetIdle(0f).name, Is.EqualTo(Idle(1)));
                Assert.That(set.GetIdle(-1f), Is.SameAs(set.GetIdle(0f)));
                for (int cel = 1; cel < TrainingDummyAnimationSet.IdleFrameCount; cel++)
                {
                    float boundary = cel * .18f;
                    Assert.That(set.GetIdle(boundary - .001f).name, Is.EqualTo(Idle(cel)), "Just before " + boundary + " s.");
                    Assert.That(set.GetIdle(boundary + .001f).name, Is.EqualTo(Idle(cel + 1)), "Just after " + boundary + " s.");
                }
                Assert.That(set.GetIdle(1.44f - .001f).name, Is.EqualTo(Idle(8)));
                Assert.That(set.GetIdle(TrainingDummyAnimationSet.IdleDuration), Is.SameAs(set.GetIdle(0f)), "The sway loops at 1.44 s.");
                Assert.That(set.GetIdle(1.44f + .001f).name, Is.EqualTo(Idle(1)));
                Assert.That(set.GetIdle(1.44f + .181f).name, Is.EqualTo(Idle(2)));
                Assert.That(set.GetIdle(3 * 1.44f + .45f).name, Is.EqualTo(Idle(3)));

                Assert.That(set.GetHurt(0f).name, Is.EqualTo(Hurt(1)));
                Assert.That(set.GetHurt(-1f), Is.SameAs(set.GetHurt(0f)));
                for (int cel = 1; cel < TrainingDummyAnimationSet.HurtFrameCount; cel++)
                {
                    float boundary = HurtStarts[cel] / 1000f;
                    Assert.That(set.GetHurt(boundary - .001f).name, Is.EqualTo(Hurt(cel)), "Just before " + boundary + " s.");
                    Assert.That(set.GetHurt(boundary + .001f).name, Is.EqualTo(Hurt(cel + 1)), "Just after " + boundary + " s.");
                }
                Assert.That(set.GetHurt(.70f - .001f).name, Is.EqualTo(Hurt(10)));
                Assert.That(set.GetHurt(TrainingDummyAnimationSet.HurtDuration).name, Is.EqualTo(Hurt(10)));
                Assert.That(set.GetHurt(5f).name, Is.EqualTo(Hurt(10)), "The reaction plays once and holds its last cel.");

                set.Dispose();
                Assert.That(set.HasRequiredAssets, Is.False);
                Assert.That(set.GetIdle(0f), Is.Null);
                Assert.That(set.GetHurt(0f), Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator HitsRestartTheHurtReactionOnCombatTimeAcrossSlotsAndTurnsWithoutRandomDraws()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                Assert.That(scope.Settings.AnimationPlaybackSpeed, Is.EqualTo(.5f), "The other characters play at half speed.");
                Assert.That(a.HasTrainingDummyAnimations, Is.True);
                Assert.That(a.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student), "A new arena fields the student.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-idle-frame-01"));

                a.SetEnemyAppearance(EnemyAppearance.TrainingDummy);
                Assert.That(a.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)), "The new look shows at once.");
                a.Reset();
                Assert.That(a.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy), "Reset keeps the chosen look.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)));
                Assert.That(a.EnemyRenderer.flipX, Is.False, "The cels are drawn struck from the left; never mirrored.");
                Assert.That(a.IsEnemyHurtPlaying, Is.False);

                // The sway keeps its own clock on raw combat time, not the half-speed playback.
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)));
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(2)));
                a.Tick(0f, .5f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(2)), "Real time alone never moves the sway.");

                a.BeginSlot(LegacySkillDefinitions.Skill(1), null);
                a.PresentHit(true, 3, 1, false, false, 2);
                Assert.That(a.IsEnemyHurtPlaying, Is.True);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)), "A landed hit starts the reaction from its first cel.");
                for (int frame = 0; frame < 10; frame++) a.Tick(0f, .02f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)), "Hit stop (no combat time) holds the reaction.");
                a.Tick(.03f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)));
                a.Tick(.02f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(2)), "35 ms of combat time, unscaled by the playback speed.");
                a.Tick(.05f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(3)));

                a.PresentHit(true, 2, 0, false, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)), "A hit during the reaction starts it again.");
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(3)));
                a.PresentHit(true, 0, 2, true, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)), "A guarded hit reels the dummy too.");
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(3)));
                a.PresentHit(true, 0, 1, false, false, 1, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(3)), "A mutual clash keeps the current cel.");
                a.PresentHit(true, 0, 0, false, false, 0);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(3)), "A miss does not restart it.");

                a.BeginSlot(null, null);
                Assert.That(a.IsEnemyHurtPlaying, Is.True, "A new slot does not cut the reaction.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(3)));
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(4)));
                a.EndTurn(); a.Tick(0f, 0f);
                Assert.That(a.IsEnemyHurtPlaying, Is.True, "Nor does the turn's end.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(4)));
                a.BeginTurn();
                a.Tick(.49f, 0f);
                Assert.That(a.IsEnemyHurtPlaying, Is.True, "0.69 s into the 0.70 s reaction.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(10)));
                a.Tick(.02f, 0f);
                Assert.That(a.IsEnemyHurtPlaying, Is.False, "At half speed it would still be reeling; the dummy keeps 0.70 s.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)), "The sway restarts from its first cel.");
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)));
                a.Tick(.1f, 0f);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(2)));

                a.PresentHit(true, 1, 0, false, false, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)));
                a.SetEnemyAppearance(EnemyAppearance.TrainingDummy);
                Assert.That(a.IsEnemyHurtPlaying, Is.False, "Choosing the look again starts a standing dummy.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)));
                a.PresentHit(true, 1, 0, false, true, 1);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)), "A fatal hit reels it the same way.");
                a.Reset();
                Assert.That(a.IsEnemyHurtPlaying, Is.False, "A new battle starts standing.");
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)));

                Assert.That(scope.EnemyReactions.Calls, Is.Zero, "The dummy has one authored reaction; it never draws a pose.");
                Assert.That(scope.EnemyAttacks.Calls, Is.Zero);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DummyOnlyStandsOrReelsNeverWalksOrGetsPushedAndTheStudentReturnsOnRequest()
        {
            using (var scope = new ArenaScope())
            {
                var a = scope.Arena;
                a.SetEnemyAppearance(EnemyAppearance.TrainingDummy);
                a.Reset();
                Transform enemy = a.EnemyRenderer.transform;
                Vector3 stand = enemy.localPosition;

                // The approach: only the player walks up to the dummy.
                a.BeginApproach();
                for (int frame = 0; frame < 60 && !a.ApproachComplete; frame++)
                {
                    a.Tick(.02f, .02f);
                    Assert.That(enemy.localPosition, Is.EqualTo(stand), "The dummy never walks out to meet the player.");
                    Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("dummy-idle-frame-"));
                }
                Assert.That(a.ApproachComplete, Is.True);
                Assert.That(a.IsInRange, Is.True);
                Assert.That(a.Separation, Is.EqualTo(LegacyArenaView.ContactDistance).Within(.01f), "The player covers the whole gap.");

                // It has no guard or attack cels.
                a.BeginSlot(null, LegacySkillDefinitions.Skill(7));
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("dummy-idle-frame-"), "A guard slot keeps it standing.");
                a.BeginSlot(null, LegacySkillDefinitions.Skill(1));
                Hold(a, LegacyArenaView.OriginalImpactTime);
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("dummy-idle-frame-"), "An attack slot keeps it standing.");

                // A heavy, fatal blow reels it but never pushes it off its base, so the player has nothing to chase.
                a.BeginSlot(LegacySkillDefinitions.Skill(1), null);
                a.PresentHit(true, 40, 20, false, true, 12);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo(Hurt(1)));
                Assert.That(a.HasPendingPush, Is.False, "The dummy stands on a fixed base.");
                Assert.That(a.EnemyKnockbackTarget, Is.EqualTo(stand));
                Assert.That(a.IsPursuing, Is.False);
                a.PresentHit(true, 0, 3, true, false, 6);
                Assert.That(a.HasPendingPush, Is.False, "Nor is a guarded hit pushed.");
                for (int frame = 0; frame < 40; frame++)
                {
                    a.Tick(.02f, .02f);
                    Assert.That(enemy.localPosition, Is.EqualTo(stand));
                    Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("dummy-"));
                }

                // Closing the distance between slots moves the player alone as well.
                a.Reset();
                Assert.That(enemy.localPosition, Is.EqualTo(stand));
                Assert.That(a.IsInRange, Is.False);
                a.CloseDistance(1f);
                Assert.That(enemy.localPosition, Is.EqualTo(stand), "The dummy never closes in.");
                Assert.That(a.IsInRange, Is.True);
                Assert.That(scope.EnemyReactions.Calls, Is.Zero);
                Assert.That(scope.EnemyAttacks.Calls, Is.Zero);

                a.SetEnemyAppearance(EnemyAppearance.Student);
                Assert.That(a.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student));
                Assert.That(a.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"), "The student's cels come back at once.");
                a.Reset();
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-idle-frame-01"));
                a.BeginSlot(null, LegacySkillDefinitions.Skill(7));
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"));
                a.BeginSlot(null, null);
                a.PresentHit(true, 40, 20, false, false, 12);
                Assert.That(a.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-hurt"));
                Assert.That(a.IsEnemyHurtPlaying, Is.False, "The student uses its own poses, not the dummy's reaction.");
                Assert.That(scope.EnemyReactions.Calls, Is.EqualTo(1), "The student draws its reaction pose again.");
                Assert.That(a.HasPendingPush, Is.True, "The student is knocked back again.");
                Assert.That(a.EnemyKnockbackTarget.x, Is.GreaterThan(stand.x));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FirstMissionBriefsAndFieldsTheDummyWhileLaterMissionsAndStagesFieldTheStudent()
        {
            yield return null;
            using (var scope = new ControllerScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                Assert.That(PrologueMissions.DummySilhouette, Is.EqualTo(TrainingDummyAnimationSet.ResourceRoot + "idle/frame-01"));

                controller.StartNewGame();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(1));
                Assert.That(controller.BriefingHud.Mission.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Sprite dummyFrame = Resources.Load<Sprite>(PrologueMissions.DummySilhouette);
                Assert.That(dummyFrame, Is.Not.Null);
                Image silhouette = Active<Image>(controller.BriefingHud.Root, "Enemy Silhouette");
                Assert.That(silhouette.sprite, Is.SameAs(dummyFrame), "The briefing shows the dummy's first idle cel.");
                Assert.That(silhouette.enabled, Is.True);
                Assert.That(Active<Text>(controller.BriefingHud.Root, "Enemy Name").text, Is.EqualTo("허수아비"));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student), "No duel yet: the arena keeps the student.");

                Assert.That(controller.StartMission(), Is.True);
                scope.SkipIntro();
                Assert.That(controller.IsMission, Is.True);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(controller.HasRequiredArt, Is.True, "The required art includes the dummy's 18 cels.");
                Assert.That(arena.HasTrainingDummyAnimations, Is.True);
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.TrainingDummy));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)));

                // One real turn: the player walks up and strikes; the dummy only sways and reels, in place.
                Assert.That(controller.AdvanceGuide(), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                Transform enemy = arena.EnemyRenderer.transform;
                Vector3 stand = enemy.localPosition;
                float playerStart = arena.PlayerRenderer.transform.localPosition.x;
                string firstHurt = null;
                int frames = 0;
                while (!controller.CanChoose && !controller.IsPlayingScene && !controller.IsShowingResult && frames++ < 2000)
                {
                    scope.Advance(.025f);
                    string cel = arena.EnemyRenderer.sprite.name;
                    Assert.That(cel, Does.StartWith("dummy-"), "The dummy only ever stands or reels.");
                    Assert.That(enemy.localPosition, Is.EqualTo(stand), "The dummy never walks or gets pushed.");
                    if (firstHurt == null && cel.StartsWith("dummy-hurt-frame-")) firstHurt = cel;
                }
                Assert.That(controller.CanChoose || controller.IsPlayingScene || controller.IsShowingResult, Is.True,
                    "The turn must settle.");
                Assert.That(firstHurt, Is.EqualTo(Hurt(1)), "A landed hit starts the authored reaction from its first cel.");
                Assert.That(arena.PlayerRenderer.transform.localPosition.x, Is.GreaterThan(playerStart), "Only the player closes in.");
                if (controller.CanChoose)
                {
                    // Planning keeps the arena's clock running: the reaction ends and the sway resumes.
                    scope.Advance(1f);
                    Assert.That(arena.IsEnemyHurtPlaying, Is.False);
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("dummy-idle-frame-"));
                }

                controller.RestartMatch();
                Assert.That(controller.IsMission, Is.False);
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student), "A stage battle fields the student.");
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));

                controller.StartNewGame();
                Assert.That(controller.StartMission(), Is.True);
                scope.SkipIntro();
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(1));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo(Idle(1)), "Every attempt at the first mission fields the dummy.");

                Assert.That(controller.Prologue.TryRestore(1), Is.True);
                controller.ReturnToLobby();
                Assert.That(controller.IsInBriefing, Is.True);
                Assert.That(controller.BriefingHud.Mission.Number, Is.EqualTo(2));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student), "Leaving the dummy's duel restores the student.");
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-"));
                Assert.That(Active<Image>(controller.BriefingHud.Root, "Enemy Silhouette").sprite,
                    Is.SameAs(Resources.Load<Sprite>(PrologueMissions.EnemySilhouette)));

                Assert.That(controller.StartMission(), Is.True);
                scope.SkipIntro();
                Assert.That(controller.IsMission, Is.True);
                Assert.That(controller.ActiveMission.Number, Is.EqualTo(2));
                Assert.That(controller.ActiveMission.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student));
                Assert.That(arena.EnemyAppearance, Is.EqualTo(EnemyAppearance.Student));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"), "The second mission fields the student.");
                Assert.That(arena.IsEnemyHurtPlaying, Is.False);
            }
        }

        private static string Idle(int cel) => "dummy-idle-frame-" + cel.ToString("00");
        private static string Hurt(int cel) => "dummy-hurt-frame-" + cel.ToString("00");

        private static void Hold(LegacyArenaView arena, float time) => typeof(LegacyArenaView)
            .GetMethod("HoldSlotAtTime", PrivateInstance).Invoke(arena, new object[] { time });

        private static T Active<T>(GameObject root, string name) where T : Component
        {
            // A rebuilt briefing destroys its old enemy views at the end of the frame; only active nodes count.
            foreach (T candidate in root.GetComponentsInChildren<T>(false))
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing active UI node: " + name);
            return null;
        }

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject host = new GameObject("Training dummy test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            public DuelPresentationSettings Settings { get; } = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public CountingZero PlayerReactions { get; } = new CountingZero();
            public CountingZero PlayerAttacks { get; } = new CountingZero();
            public CountingZero EnemyAttacks { get; } = new CountingZero();
            public CountingZero EnemyReactions { get; } = new CountingZero();
            public LegacyArenaView Arena { get; }

            public ArenaScope()
            {
                // Half-speed playback for the other characters; the dummy must keep its authored timing regardless.
                JsonUtility.FromJsonOverwrite("{\"animationPlaybackSpeed\":0.5}", Settings);
                Arena = LegacyArenaView.Create(host.transform, art, Settings,
                    PlayerReactions, PlayerAttacks, EnemyAttacks, EnemyReactions);
            }

            public void Dispose()
            {
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(Settings);
                Object.Destroy(host);
            }
        }

        private sealed class ControllerScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }

            public ControllerScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "Tests never write the player's save.");
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
            }

            public void Advance(float delta) => advance(delta, null);

            /// <summary>Skips a mission's intro scene (its battlefield cutscene, or its dialogue), which starts the duel.</summary>
            public void SkipIntro()
            {
                Assert.That(Controller.IsPlayingScene, Is.True, "Each mission opens with its intro scene.");
                Assert.That(Controller.SkipScene(), Is.True);
                Assert.That(Controller.IsPlayingScene, Is.False);
            }

            public void Dispose()
            {
                // Leave a fresh arc and lobby behind, as the other flow tests do.
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
            }
        }

        private sealed class CountingZero : System.Random
        {
            public int Calls { get; private set; }
            public override int Next(int maxValue) { Calls++; return 0; }
        }
    }
}
