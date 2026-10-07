using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Barks;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Battle barks through the real controller: speech bubbles over the fighters' head HUDs that never pause the
    /// battle, keep real time through its slow motion, stay clear of both head HUDs and of each other, read thoughts in the
    /// monologue colour, give way to the finishing blow and to the 서막's 수훈 scene, and leave with the battle. The lines are
    /// written here, so the author's bark files can change freely.</summary>
    public sealed class BattleBarksPlayModeTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private const float Frame = .02f;
        private static readonly BarkSpeaker[] Speakers = { BarkSpeaker.Enemy, BarkSpeaker.Player };

        [UnityTest]
        public IEnumerator TheStartLines_ShowOverTheHeadHuds_ForTheirRealSeconds_WhileTheBattleGoesOn()
        {
            yield return null;
            using (var scope = new BarkScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelBarks barks = controller.Barks;
                float seconds = controller.PresentationSettings.BarkSeconds;
                Assume.That(seconds, Is.GreaterThan(.5f));
                scope.StartStage(OneBlowDuel(1000), "@on start\n덤벼라.\n@on start player\n(긴장된다…)");
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(barks.IsShowing(BarkSpeaker.Enemy) || barks.IsShowing(BarkSpeaker.Player), Is.False,
                    "Nothing before the first planning frame.");
                float planning = controller.TurnTimeRemaining;
                scope.Advance(Frame);
                Assert.That(barks.ShownText(BarkSpeaker.Enemy), Is.EqualTo("덤벼라."));
                Assert.That(barks.ShownText(BarkSpeaker.Player), Is.EqualTo("(긴장된다…)"), "Both sides may speak at once.");
                Assert.That(barks.BubbleText(BarkSpeaker.Player).color, Is.EqualTo(DialogueHud.MonologueColor),
                    "A thought in parentheses reads in the monologue colour…");
                Assert.That(barks.BubbleTail(BarkSpeaker.Player).IsThought, Is.True, "…trailing beads for a tail.");
                Assert.That(barks.BubbleText(BarkSpeaker.Enemy).color, Is.EqualTo(DuelVisualTheme.Foreground));
                Assert.That(barks.BubbleTail(BarkSpeaker.Enemy).IsThought, Is.False);
                Assert.That(barks.BubbleRect(BarkSpeaker.Enemy).IsChildOf(controller.Hud.Root.transform), Is.True,
                    "The bubbles live on the duel HUD.");
                AssertPlacement(controller);

                // They never pause the battle, and stay their real seconds.
                float shown = 0f;
                while (barks.IsShowing(BarkSpeaker.Enemy) && shown < seconds + 1f)
                {
                    Assert.That(controller.CanChoose, Is.True);
                    scope.Advance(Frame);
                    shown += Frame;
                    AssertPlacement(controller);
                }
                Assert.That(controller.TurnTimeRemaining, Is.LessThan(planning - seconds * .9f), "The planning clock ran meanwhile.");
                Assert.That(shown, Is.EqualTo(seconds).Within(Frame * 1.5f));
                Assert.That(barks.IsShowing(BarkSpeaker.Player), Is.False, "Said together, gone together.");
                Assert.That(barks.Tracker.BattleStarted(), Is.Zero, "The start is said once a battle.");
            }
        }

        [UnityTest]
        public IEnumerator AHeavyHit_IsAnswered_AndTheBubbleKeepsRealTimeThroughTheSlowMotion()
        {
            yield return null;
            using (var scope = new BarkScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelBarks barks = controller.Barks;
                // Every hit that takes health is heavy here, and earns the decisive close-up's slow motion.
                scope.Settings("{\"decisiveHealthDamagePercent\":1}");
                scope.StartStage(OneBlowDuel(1000), "@on enemy-hurt\n크윽…\n@on enemy-hurt player\n(통했다!)");
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => barks.IsShowing(BarkSpeaker.Enemy), "The strike lands.");
                Assert.That(controller.Session.IsFinished, Is.False);
                Assert.That(controller.EnemyHealth, Is.LessThan(1000));
                Assert.That(barks.ShownText(BarkSpeaker.Enemy), Is.EqualTo("크윽…"));
                Assert.That(barks.ShownText(BarkSpeaker.Player), Is.EqualTo("(통했다!)"));
                Assert.That(controller.IsResolving && !controller.IsBattlePausedForEvent, Is.True, "The battle goes on…");
                Assert.That(controller.ArenaView.IsFatalFocus, Is.True, "…in the decisive close-up's slow motion…");
                FieldInfo phaseTime = typeof(DuelPrototypeController).GetField("phaseTime", PrivateInstance);
                float clock = (float)phaseTime.GetValue(controller);
                float elapsed = barks.Tracker.Elapsed(BarkSpeaker.Enemy);
                for (int frame = 0; frame < 5; frame++)
                {
                    scope.Advance(Frame);
                    AssertPlacement(controller);
                }
                Assert.That(barks.Tracker.Elapsed(BarkSpeaker.Enemy) - elapsed, Is.EqualTo(5 * Frame).Within(1e-4f),
                    "…while the bubble keeps real time.");
                Assert.That((float)phaseTime.GetValue(controller) - clock, Is.LessThan(5 * Frame * .5f),
                    "The battle's clock crawled meanwhile.");
            }
        }

        [UnityTest]
        public IEnumerator TheFinishingBlow_AndLeavingTheBattle_TakeTheBubblesAway()
        {
            yield return null;
            using (var scope = new BarkScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelBarks barks = controller.Barks;
                const string lines = "@on start\n덤벼라.\n@on enemy-hurt\n크윽…\n@on enemy-low\n아직…";
                scope.StartStage(OneBlowDuel(10), lines);
                scope.Advance(Frame);
                Assert.That(barks.ShownText(BarkSpeaker.Enemy), Is.EqualTo("덤벼라."));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.Finale.IsRunning, "The strike ends the duel.");
                Assert.That(barks.IsShowing(BarkSpeaker.Enemy) || barks.IsShowing(BarkSpeaker.Player), Is.False,
                    "The finishing blow has the moment to itself…");
                Assert.That(barks.Tracker.IsShowingAny, Is.False, "…and the hit that ended the duel is not answered.");
                scope.AdvanceUntil(() => controller.IsShowingResult, "The result follows.");
                Assert.That(barks.Script, Is.Null, "A finished battle says nothing more.");

                // A retry hears its barks afresh.
                Assert.That(controller.RetryBattleResult(), Is.True);
                scope.Install(OneBlowDuel(1000), lines);
                scope.Advance(Frame);
                Assert.That(barks.ShownText(BarkSpeaker.Enemy), Is.EqualTo("덤벼라."));

                // Leaving mid-line leaves no bubble behind.
                controller.ReturnToLobby();
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(barks.IsShowing(BarkSpeaker.Enemy) || barks.Script != null, Is.False);
                scope.StartStage(OneBlowDuel(1000), lines);
                scope.Advance(Frame);
                Assert.That(barks.IsShowing(BarkSpeaker.Enemy), Is.True);
                controller.ShowTitle();
                Assert.That(barks.IsShowing(BarkSpeaker.Enemy) || barks.Script != null, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator TheSutunScene_TakesTheBubblesAway_AndTheResumedBattleHearsSutun()
        {
            yield return null;
            using (var scope = new BarkScope())
            {
                DuelPrototypeController controller = scope.Controller;
                DuelBarks barks = controller.Barks;
                PrologueMission missionFour = PrologueMissions.Get(4);
                scope.Scenes[missionFour.Empowerment.Scene] = "도미니코 기사단, 수훈!";
                scope.StartMissionFour();
                Assert.That(barks.Script, Is.Not.Null, "The 서막's last mission has its bark file…");
                Assert.That(barks.Script.Id, Is.EqualTo(BarkScript.MissionResource(4)));
                // Three hits of 10 on a breathing 40-health 이아: the second crosses half and ends this turn.
                var flurry = new LegacySkill(201, "Test Flurry", 1, 30, 30, LegacySkillKind.Attack, LegacySkillProperty.Slash, 3, 0, "",
                    iconId: 1);
                scope.Install(new LegacyQueuedDuel(100, 50, 40, 10, new[] { flurry }, new[] { LegacyCommonActions.Breathe },
                        new[] { 1 }, 5, features: CombatFeature.LaneQ | CombatFeature.Cycle, enemyHealthFloor: 1,
                        enemyHealthThresholdPercent: 50),
                    "@on start player\n(간다…!)\n@on enemy-hurt\n크윽…\n@on sutun\n이것이 수훈이다.");
                scope.Advance(Frame);
                Assert.That(barks.ShownText(BarkSpeaker.Player), Is.EqualTo("(간다…!)"));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => scope.Field<bool>("missionEventPending"), "The flurry brings 이아 to half health.");
                Assert.That(barks.Tracker.Current(BarkSpeaker.Enemy), Is.Null, "The hit that opens the scene is the scene's to answer.");
                scope.SetField("hitStopRemaining", 0f);
                scope.Advance(0f);
                Assert.That(controller.IsBattlePausedForEvent && controller.IsPlayingCutscene, Is.True);
                Assert.That(barks.Tracker.IsShowingAny || barks.IsShowing(BarkSpeaker.Player), Is.False, "No bubble waits over the scene.");

                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsMissionEmpowered, Is.True);
                Assert.That(controller.CanChoose && controller.Session.RoundNumber == 2, Is.True);
                Assert.That(controller.EnemyHealth, Is.EqualTo(20), "The flurry's last hit is cancelled by the scene.");
                Assert.That(barks.Tracker.Current(BarkSpeaker.Enemy)?.Text, Is.EqualTo("이것이 수훈이다."), "The resumed battle hears 수훈…");
                scope.Advance(Frame);
                Assert.That(barks.ShownText(BarkSpeaker.Enemy), Is.EqualTo("이것이 수훈이다."));
                AssertPlacement(controller);
                for (int frame = 0; frame < 5; frame++) scope.Advance(Frame);
                Assert.That(controller.EnemyHealth, Is.EqualTo(20), "The previous turn never resumes under the bark.");
                Assert.That(barks.ShownText(BarkSpeaker.Enemy), Is.EqualTo("이것이 수훈이다."), "…and the new planning turn leaves it audible.");
            }
        }

        // Each bubble on screen sits clear of both fighters' head HUDs and of the other bubble, within the HUD.
        private static void AssertPlacement(DuelPrototypeController controller)
        {
            DuelBarks barks = controller.Barks;
            Assert.That(controller.Hud.TryGetHeadStack(true, out Rect playerStack, out _), Is.True);
            Assert.That(controller.Hud.TryGetHeadStack(false, out Rect enemyStack, out _), Is.True);
            Rect area = ((RectTransform)controller.Hud.Root.transform).rect;
            var placed = new List<Rect>();
            foreach (BarkSpeaker speaker in Speakers)
            {
                if (!barks.IsShowing(speaker)) continue;
                BarkBox box = barks.PlacedBox(speaker);
                var bubble = new Rect(box.X, box.Y, box.Width, box.Height);
                Assert.That(bubble.Overlaps(playerStack) || bubble.Overlaps(enemyStack), Is.False, speaker + "'s bubble covers a head HUD.");
                Assert.That(bubble.xMin >= area.xMin && bubble.xMax <= area.xMax && bubble.yMin >= area.yMin && bubble.yMax <= area.yMax,
                    Is.True, speaker + "'s bubble leaves the screen.");
                Assert.That(barks.BubbleRect(speaker).sizeDelta, Is.EqualTo(new Vector2(box.Width, box.Height)));
                Assert.That(bubble.yMin, Is.GreaterThanOrEqualTo((speaker == BarkSpeaker.Player ? playerStack : enemyStack).yMax),
                    speaker + "'s bubble sits above its head HUD.");
                placed.Add(bubble);
            }
            if (placed.Count == 2) Assert.That(placed[0].Overlaps(placed[1]), Is.False, "The two bubbles overlap.");
        }

        /// <summary>A strike of 30 against an enemy of <paramref name="enemyHealth"/> without resistance, who only breathes.</summary>
        private static LegacyQueuedDuel OneBlowDuel(int enemyHealth)
        {
            var strike = new LegacySkill(210, "Test Strike", 1, 30, 30, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "",
                iconId: 1);
            return new LegacyQueuedDuel(100, 50, enemyHealth, 0, new[] { strike }, new[] { LegacyCommonActions.Breathe }, new[] { 1 }, 3);
        }

        /// <summary>The controller driven frame by frame on fixture battles with lines written here, a tuning clone, and the
        /// mission scenes it is handed in place of the Resources files.</summary>
        private sealed class BarkScope : IDisposable
        {
            private readonly bool originalEnabled;
            private readonly DuelPresentationSettings originalSettings, settings;
            private readonly FieldInfo sceneSource;
            private readonly object originalSource;
            private readonly Action<float, Keyboard> advance;
            /// <summary>Mission scene texts by Resources path; any other path has no cutscene.</summary>
            public readonly Dictionary<string, string> Scenes = new Dictionary<string, string>();
            public DuelPrototypeController Controller { get; }

            public BarkScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                settings = Object.Instantiate(originalSettings);
                SetField("presentationSettings", settings);
                sceneSource = typeof(DuelPrototypeController).GetField("missionSceneText", PrivateInstance);
                Assert.That(sceneSource, Is.Not.Null);
                originalSource = sceneSource.GetValue(Controller);
                sceneSource.SetValue(Controller, (Func<string, string>)(path => Scenes.TryGetValue(path, out string text) ? text : null));
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Controller.RestartJourney();
            }

            public void Settings(string json) => JsonUtility.FromJsonOverwrite(json, settings);

            public void Advance(float delta) => advance(delta, null);

            public void AdvanceUntil(Func<bool> condition, string message)
            {
                int frames = 0;
                while (!condition() && frames++ < 4000) Advance(Frame);
                Assert.That(condition(), Is.True, message);
            }

            public T Field<T>(string name) => (T)typeof(DuelPrototypeController).GetField(name, PrivateInstance).GetValue(Controller);

            public void SetField(string name, object value)
            {
                FieldInfo field = typeof(DuelPrototypeController).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null, name);
                field.SetValue(Controller, value);
            }

            /// <summary>A stage battle (stage 1) on <paramref name="duel"/> with <paramref name="lines"/> for its barks.</summary>
            public void StartStage(LegacyQueuedDuel duel, string lines)
            {
                if (!Controller.IsInLobby) Controller.ReturnToLobby();
                Assert.That(Controller.StartCampaignStage(1), Is.True);
                Install(duel, lines);
            }

            /// <summary>The battle on screen restarts as <paramref name="duel"/>, its barks <paramref name="lines"/>.</summary>
            public void Install(LegacyQueuedDuel duel, string lines)
            {
                SetField("session", duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
                Controller.Barks.Use(BarkScriptParser.Parse("Barks/test", lines));
            }

            /// <summary>The 서막's last mission in battle: the missions before it counted won, its intro skipped, no coach.</summary>
            public void StartMissionFour()
            {
                Controller.StartNewGame();
                for (int number = 1; number < PrologueMissions.Count; number++)
                    Assert.That(Controller.Prologue.TryComplete(number, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(Controller.StartMission(), Is.True);
                if (Controller.IsPlayingScene) Assert.That(Controller.SkipScene(), Is.True);
                Assert.That(Controller.IsMission && Controller.ActiveMission.Number == PrologueMissions.Count, Is.True);
                SetField("guide", null);
            }

            public void Dispose()
            {
                sceneSource.SetValue(Controller, originalSource);
                SetField("presentationSettings", originalSettings);
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
                Object.Destroy(settings);
            }
        }
    }
}
