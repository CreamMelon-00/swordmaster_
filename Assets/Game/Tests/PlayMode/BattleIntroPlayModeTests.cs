using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>A battle's opening and its quiet cues through the real controller: the start card (it holds the battle,
    /// names only the two fighters, skips on a click, Enter, Space or Escape without the key reaching the battle, is short
    /// on a retry and never comes back after the 서막's 수훈), 엘리사's eye over the enemy's queue each planning turn, the
    /// forest's ambience bed, and the planning clock's push on the camera.</summary>
    public sealed class BattleIntroPlayModeTests : InputTestFixture
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private const float Frame = .02f;

        [UnityTest]
        public IEnumerator TheStartCard_HoldsTheBattle_NamesOnlyTheTwoFighters_ThenHandsOverToTheFirstPlanningTurn()
        {
            yield return null;
            using (var scope = new IntroScope(startCards: true))
            {
                DuelPrototypeController controller = scope.Controller;
                DuelStartCard card = controller.StartCard;
                float seconds = scope.Tuning.StartCardSeconds;
                Assume.That(seconds, Is.GreaterThan(.5f));
                scope.StartStage(OneBlowDuel(1000));
                Assert.That(controller.IsShowingStartCard && card.IsShowing, Is.True, "A battle opens on its card.");
                Assert.That(card.Timeline.Seconds, Is.EqualTo(seconds));
                Assert.That(controller.Hud.Root.activeSelf, Is.False, "The duel HUD waits under it.");
                Assert.That(card.PlayerLabel.text, Is.EqualTo(DuelStartCard.PlayerName));
                Assert.That(card.OpponentLabel.text, Is.Empty,
                    "A stage's enemy has no name yet: its silhouette stands alone, with no place name in its stead.");
                Assert.That(card.PlayerFigure.sprite, Is.Not.Null, "엘리사's idle frame…");
                Assert.That(card.OpponentFigure.sprite, Is.Not.Null, "…and the knight's, both as silhouettes.");
                Assert.That(controller.ArenaView.EnemyAppearance, Is.EqualTo(EnemyAppearance.CadetA));
                Assert.That(card.OpponentFigure.sprite,
                    Is.SameAs(Resources.Load<Sprite>(CampaignEnemyVariant.SilhouetteResource(1))),
                    "The start card uses the same opponent as the first campaign battle.");
                Assert.That(card.PlayerFigure.color, Is.EqualTo(MissionBriefingHud.SilhouetteColor));
                foreach (Text text in card.Root.GetComponentsInChildren<Text>(true))
                {
                    Assert.That(text.text == DuelStartCard.PlayerName || text.text.Length == 0, Is.True,
                        "엘리사's name is a stage card's only word: " + text.text);
                    Assert.That(text.text, Does.Not.Contain("결투").And.Not.Contain("전투"));
                }
                int sorting = card.Root.GetComponent<Canvas>().sortingOrder;
                Assert.That(sorting, Is.GreaterThan(controller.CoachHud.Root.GetComponent<Canvas>().sortingOrder));
                Assert.That(sorting, Is.GreaterThan(DuelLetterbox.SortingOrder), "Over its bars…");
                Assert.That(sorting, Is.LessThan(controller.ResultHud.Root.GetComponent<Canvas>().sortingOrder), "…under the result…");
                Assert.That(sorting, Is.LessThan(CutsceneHud.SortingOrder), "…and the cutscene layers.");

                float planning = controller.TurnTimeRemaining;
                float elapsed = 0f;
                for (int frame = 0; frame < 10; frame++)
                {
                    scope.Advance(Frame);
                    elapsed += Frame;
                }
                Assert.That(card.Letterbox.Amount, Is.GreaterThan(0f), "The bars slide in.");
                Assert.That(controller.TurnTimeRemaining, Is.EqualTo(planning), "The planning clock waits for the card…");
                Assert.That(controller.ArenaView.BulletTimeAmount, Is.Zero, "…and so does bullet time…");
                Assert.That(controller.GearShimmer.IsPlaying, Is.False, "…and 엘리사's eye.");
                Assert.That(controller.Session.Phase, Is.EqualTo(LegacyDuelPhase.Planning));

                while (card.IsShowing && !card.IsLeaving && elapsed < seconds + 1f)
                {
                    scope.Advance(Frame);
                    elapsed += Frame;
                }
                Assert.That(card.IsShowing && card.IsLeaving, Is.True);
                Assert.That(controller.Hud.Root.activeSelf, Is.True, "The HUD comes back under the leaving card.");
                while (card.IsShowing && elapsed < seconds + 1f)
                {
                    scope.Advance(Frame);
                    elapsed += Frame;
                }
                Assert.That(card.IsShowing, Is.False);
                Assert.That(elapsed, Is.EqualTo(seconds).Within(2 * Frame), "The card lasts its real seconds.");
                Assert.That(card.Letterbox.IsVisible || card.Root.activeSelf, Is.False, "No bar or figure is left behind.");
                scope.Advance(Frame);
                Assert.That(controller.TurnTimeRemaining, Is.LessThan(planning), "The first planning turn runs…");
                Assert.That(controller.GearShimmer.IsPlaying, Is.True, "…its queue revealed.");
            }
        }

        [UnityTest]
        public IEnumerator EnterSpaceEscapeOrAClick_SkipTheCard_AndNeverReachTheBattle()
        {
            yield return null;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            using (var scope = new IntroScope(startCards: true))
            {
                DuelPrototypeController controller = scope.Controller;
                DuelStartCard card = controller.StartCard;
                KeyControl[] keys = { keyboard.spaceKey, keyboard.enterKey, keyboard.escapeKey };
                string[] names = { "Space", "Enter", "Escape" };
                for (int index = 0; index < keys.Length; index++)
                {
                    KeyControl key = keys[index];
                    scope.StartStage(OneBlowDuel(1000));
                    Assert.That(controller.QueueLane(0), Is.True, "A turn ready to commit…");
                    scope.Advance(Frame, keyboard);
                    Assert.That(card.IsShowing, Is.True);
                    Press(key);
                    yield return null;
                    scope.Advance(Frame, keyboard);
                    Assert.That(card.IsShowing, Is.False, names[index] + " skips the card…");
                    Assert.That(controller.CanChoose && !controller.IsResolving, Is.True, "…without committing the turn…");
                    Assert.That(controller.Campaign.Phase, Is.EqualTo(CampaignPhase.Battle), "…or leaving the battle.");
                    Assert.That(controller.Hud.Root.activeSelf, Is.True);
                    Release(key);
                    yield return null;
                    scope.Advance(Frame, keyboard);
                    Assert.That(controller.IsResolving, Is.False, "The key is spent.");
                }

                scope.StartStage(OneBlowDuel(1000));
                Assert.That(card.SkipButton.gameObject.activeInHierarchy, Is.True, "The card takes every click…");
                card.SkipButton.onClick.Invoke();
                Assert.That(card.IsShowing, Is.False, "…and a click skips it.");
                Assert.That(controller.Hud.Root.activeSelf, Is.True);

                // A turn committed under the card (direct use) begins the battle and takes the card away first.
                scope.StartStage(OneBlowDuel(1000));
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                Assert.That(card.IsShowing, Is.False);
                Assert.That(controller.IsResolving, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ARetryHasTheShortCard_MissionsNameTheirFoe_AndTheEmpoweredResumeHasNone()
        {
            yield return null;
            using (var scope = new IntroScope(startCards: true))
            {
                DuelPrototypeController controller = scope.Controller;
                DuelStartCard card = controller.StartCard;
                Assume.That(scope.Tuning.StartCardRetrySeconds, Is.GreaterThan(0f).And.LessThan(scope.Tuning.StartCardSeconds));
                scope.StartStage(OneBlowDuel(10));
                Assert.That(controller.SkipStartCard(), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.IsShowingResult, "The strike wins the stage.");
                Assert.That(card.IsShowing || card.Letterbox.IsVisible, Is.False);
                Assert.That(controller.RetryBattleResult(), Is.True);
                Assert.That(card.IsShowing, Is.True, "A retry opens on a card too…");
                Assert.That(card.Timeline.Seconds, Is.EqualTo(scope.Tuning.StartCardRetrySeconds), "…the short one.");
                controller.ReturnToLobby();
                Assert.That(card.IsShowing || card.Letterbox.IsVisible, Is.False, "Leaving takes the card away.");
                scope.StartStage(OneBlowDuel(10));
                Assert.That(card.Timeline.Seconds, Is.EqualTo(scope.Tuning.StartCardSeconds), "A new start has the full card.");

                string[] foes = { "허수아비", "떠돌이 기사", "떠돌이 기사", "이아" };
                for (int number = 1; number <= PrologueMissions.Count; number++)
                {
                    scope.StartMission(number);
                    Assert.That(card.IsShowing, Is.True, "Mission " + number + " opens on its card after its intro.");
                    Assert.That(card.OpponentLabel.text, Is.EqualTo(foes[number - 1]), "Mission " + number + " names its foe as the battle does.");
                    Assert.That(card.OpponentFigure.sprite, Is.Not.Null.And.SameAs(Silhouette(PrologueMissions.Get(number))),
                        "…drawn from its briefing silhouette.");
                }

                PrologueMission missionFour = PrologueMissions.Get(4);
                scope.Scenes[missionFour.Empowerment.Scene] = "도미니코 기사단, 수훈!";
                var flurry = new LegacySkill(201, "Test Flurry", 1, 30, 30, LegacySkillKind.Attack, LegacySkillProperty.Slash, 3, 0, "",
                    iconId: 1);
                scope.Install(new LegacyQueuedDuel(100, 50, 40, 10, new[] { flurry }, new[] { LegacyCommonActions.Breathe },
                    new[] { 1 }, 5, features: CombatFeature.LaneQ | CombatFeature.Cycle, enemyHealthFloor: 1,
                    enemyHealthThresholdPercent: 50));
                scope.SetField("guide", null);
                Assert.That(controller.SkipStartCard(), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => scope.Field<bool>("missionEventPending"), "The flurry brings 이아 to half health.");
                scope.SetField("hitStopRemaining", 0f);
                scope.Advance(0f);
                Assert.That(controller.IsBattlePausedForEvent, Is.True);
                Assert.That(controller.SkipScene(), Is.True);
                Assert.That(controller.IsMissionEmpowered, Is.True);
                Assert.That(card.IsShowing || card.Letterbox.IsVisible, Is.False, "The resumed battle has no card.");
                scope.Advance(Frame);
                Assert.That(card.IsShowing, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ElisasEye_PlaysOverTheEnemysQueueEachPlanningTurn_BesideTheRowNotInIt()
        {
            yield return null;
            using (var scope = new IntroScope(startCards: false))
            {
                DuelPrototypeController controller = scope.Controller;
                DuelGearShimmer shimmer = controller.GearShimmer;
                float seconds = scope.Tuning.GearShimmerSeconds;
                Assume.That(seconds, Is.GreaterThan(.2f));
                Assume.That(scope.Tuning.GearShimmerStrength, Is.GreaterThan(0f));
                scope.StartStage(OneBlowDuel(1000));
                Assert.That(shimmer.IsPlaying, Is.False, "Not before the turn is on screen.");
                scope.Advance(Frame);
                Assert.That(shimmer.IsPlaying, Is.True, "The first planning turn reveals the enemy's queue.");
                Assert.That(controller.Hud.TryGetEnemyQueueRow(out RectTransform row, out Vector2 span), Is.True);
                RectTransform gear = shimmer.Gear.rectTransform;
                Assert.That(gear.parent, Is.SameAs(row.parent), "Beside the row…");
                Assert.That(shimmer.GlintArea.parent, Is.SameAs(row.parent));
                Assert.That(gear.GetSiblingIndex(), Is.EqualTo(row.GetSiblingIndex() - 1), "…the gear under its cards…");
                Assert.That(shimmer.GlintArea.GetSiblingIndex(), Is.EqualTo(row.GetSiblingIndex() + 1), "…the glint over them.");
                int cards = 0;
                foreach (Transform child in row)
                    if (child.gameObject.activeSelf) cards++;
                Assert.That(cards, Is.EqualTo(controller.Session.EnemyQueue.Count), "The row holds only its cards.");
                Assert.That(gear.anchoredPosition.x, Is.EqualTo(row.anchoredPosition.x + span.x + LegacyCombatHud.QueueCardSize * .5f)
                    .Within(.01f), "The gear sits behind slot 1.");
                Assert.That(shimmer.Gear.raycastTarget || shimmer.Glint.raycastTarget, Is.False);
                Assert.That(shimmer.GearSprite, Is.Not.Null, "The gear is drawn in code.");
                Assert.That(shimmer.GlintArea.GetComponent<RectMask2D>(), Is.Not.Null, "The glint stays on the row.");

                float glintX = shimmer.Glint.rectTransform.anchoredPosition.x, turned = gear.localEulerAngles.z;
                float elapsed = Frame;
                for (int frame = 0; frame < 12; frame++)
                {
                    scope.Advance(Frame);
                    elapsed += Frame;
                }
                Assert.That(shimmer.Glint.rectTransform.anchoredPosition.x, Is.GreaterThan(glintX), "The glint sweeps outward…");
                Assert.That(gear.localEulerAngles.z, Is.Not.EqualTo(turned), "…and the gear turns.");
                Assert.That(shimmer.Gear.color.a, Is.GreaterThan(0f));
                while (shimmer.IsPlaying && elapsed < seconds + 1f)
                {
                    scope.Advance(Frame);
                    elapsed += Frame;
                }
                Assert.That(shimmer.IsPlaying || shimmer.Gear.gameObject.activeSelf, Is.False);
                Assert.That(elapsed, Is.EqualTo(seconds).Within(2 * Frame), "It lasts its real seconds.");

                // The next turn reveals a new queue.
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                scope.AdvanceUntil(() => controller.Session.RoundNumber == 2 && controller.CanChoose, "The turn plays out.");
                scope.Advance(Frame);
                Assert.That(shimmer.IsPlaying, Is.True, "Each planning turn reveals the enemy's queue again.");
                controller.ReturnToLobby();
                Assert.That(shimmer.IsPlaying || shimmer.Gear.gameObject.activeSelf, Is.False, "Leaving takes it away.");

                scope.Settings("{\"gearShimmerSeconds\":0}");
                scope.StartStage(OneBlowDuel(1000));
                scope.Advance(Frame);
                Assert.That(shimmer.IsPlaying, Is.False, "0 seconds switches it off.");
            }
        }

        [UnityTest]
        public IEnumerator TheForestBed_StaysOutOfCorridorBattlesAndScenes_AndPlaysUnderForestScenes()
        {
            yield return null;
            using (var scope = new IntroScope(startCards: false))
            {
                DuelPrototypeController controller = scope.Controller;
                DuelForestAmbience forest = controller.ForestAmbience;
                Assert.That(forest.Clip, Is.Not.Null, "Resources/" + DuelForestAmbience.ClipResource + " is the bed.");
                DuelPresentationSettings tuning = scope.Tuning;
                float volume = tuning.ForestAmbienceVolume, fade = tuning.ForestAmbienceFadeSeconds;
                Assume.That(volume, Is.GreaterThan(0f));
                Assume.That(tuning.ForestAmbienceSceneDuck, Is.GreaterThan(0f));
                scope.AdvanceFor(fade + .1f);
                Assert.That(controller.IsInLobby, Is.True);
                Assert.That(forest.IsPlaying, Is.False, "No forest in the lobby.");

                scope.StartStage(OneBlowDuel(1000));
                scope.AdvanceFor(fade);
                Assert.That(controller.ArenaView.BackdropKind, Is.EqualTo(ArenaBackdropKind.SchoolCorridor));
                Assert.That(forest.IsPlaying, Is.False, "The indoor battle does not sound like a forest.");

                controller.ReturnToLobby();

                Assert.That(controller.StartCutscene(CutsceneScriptParser.Parse("school-test", "@ambience aura-loop 0\n첫째")), Is.True);
                scope.Advance(Frame);
                Assert.That(controller.ArenaView.BackdropKind, Is.EqualTo(ArenaBackdropKind.SchoolCorridor),
                    "Standalone scenes launched after the opening arc take place indoors.");
                Assert.That(forest.TargetVolume, Is.Zero, "A cutscene's own loop does not bring forest ambience indoors.");
                Assert.That(controller.SkipCutscene(), Is.True);

                Assert.That(controller.StartCutscene(
                    CutsceneScriptParser.Parse("forest-test", "@ambience aura-loop 0\n첫째"), ArenaBackdropKind.Forest), Is.True);
                scope.Advance(Frame);
                Assert.That(controller.ArenaView.BackdropKind, Is.EqualTo(ArenaBackdropKind.Forest));
                Assert.That(forest.TargetVolume, Is.EqualTo(volume * (1f - tuning.ForestAmbienceSceneDuck)).Within(1e-4f),
                    "An outdoor scene plays over the forest and ducks its bed under the scene loop.");
                Assert.That(controller.SkipCutscene(), Is.True);

                Assert.That(controller.StartCutscene(
                    CutsceneScriptParser.Parse("forest-test", "@fade out 0\n첫째"), ArenaBackdropKind.Forest), Is.True);
                scope.Advance(Frame);
                Assert.That(forest.TargetVolume, Is.Zero, "A black screen hides the forest's sound with it.");
                Assert.That(controller.SkipCutscene(), Is.True);

                controller.ShowTitle();
                scope.AdvanceFor(fade + .1f);
                Assert.That(forest.IsPlaying, Is.False, "No forest on the title either.");
            }
        }

        [Test]
        public void TheForestLoop_IsAQuietSeamlessTenSecondMonoLoop()
        {
            AudioClip clip = Resources.Load<AudioClip>(DuelForestAmbience.ClipResource);
            Assert.That(clip, Is.Not.Null, "Resources/" + DuelForestAmbience.ClipResource);
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.length, Is.EqualTo(10f).Within(.01f), "A ten-second loop.");
            var samples = new float[clip.samples];
            if (!clip.GetData(samples, 0)) Assert.Inconclusive("The clip's samples are not readable here.");
            float peak = 0f, step = 0f;
            for (int index = 0; index < samples.Length; index++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
                if (index > 0) step = Mathf.Max(step, Mathf.Abs(samples[index] - samples[index - 1]));
            }
            Assert.That(peak, Is.LessThan(.6f), "Normalised with headroom.");
            Assert.That(Mathf.Abs(samples[samples.Length - 1] - samples[0]), Is.LessThanOrEqualTo(step),
                "The end meets the start like any two samples inside it.");
        }

        [UnityTest]
        public IEnumerator TheClocksLastShare_PushesTheCameraIn_AndTheCommitLetsGo()
        {
            yield return null;
            using (var scope = new IntroScope(startCards: false))
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyArenaView arena = controller.ArenaView;
                DuelPresentationSettings tuning = controller.PresentationSettings;
                Assume.That(tuning.TimePressureShare, Is.GreaterThan(.1f));
                Assume.That(arena.TimePressureAmount, Is.Zero);
                scope.StartStage(OneBlowDuel(1000));
                scope.AdvanceFor(.5f);
                Assert.That(arena.TimePressureAmount, Is.Zero, "A fresh turn presses nothing.");

                scope.SetField("planningTime", controller.TurnTimeRemaining * tuning.TimePressureShare * .5f);
                scope.AdvanceFor(LegacyTimePressure.EaseInSeconds);
                Assert.That(arena.TimePressureAmount, Is.GreaterThan(.3f), "The last share of the clock presses in…");
                float baseSize = 5f + Mathf.Clamp(controller.TurnTimeRemaining, 0f, 10f) / 10f;
                if (tuning.TimePressureCameraPush > 0f)
                    Assert.That(arena.ArenaCamera.orthographicSize, Is.LessThan(baseSize - .01f), "…pushing the camera in.");
                Assert.That(controller.QueueLane(0), Is.True);
                controller.CommitTurn();
                Assert.That(arena.TimePressureAmount, Is.Zero, "The commit lets go: the combat framing takes over.");

                // A clock that does not run (training) never presses.
                controller.ReturnToLobby();
                Assert.That(controller.StartTraining(), Is.True);
                scope.SetField("planningTime", .5f);
                scope.AdvanceFor(.5f);
                Assert.That(arena.TimePressureAmount, Is.Zero, "Training has no clock to press with.");
            }
        }

        // The briefing's silhouette sprite, loaded as the briefing and the card load it.
        private static Sprite Silhouette(PrologueMission mission)
        {
            string path = mission.Enemies[0].SilhouetteResource;
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;
            Sprite[] all = Resources.LoadAll<Sprite>(path);
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>A strike of 30 against an enemy of <paramref name="enemyHealth"/> without resistance, who only breathes.</summary>
        private static LegacyQueuedDuel OneBlowDuel(int enemyHealth)
        {
            var strike = new LegacySkill(210, "Test Strike", 1, 30, 30, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "",
                iconId: 1);
            return new LegacyQueuedDuel(100, 50, enemyHealth, 0, new[] { strike }, new[] { LegacyCommonActions.Breathe }, new[] { 1 }, 3);
        }

        /// <summary>The controller driven frame by frame on fixture battles, with a tuning clone, start cards on or off, and
        /// the mission scenes it is handed in place of the Resources files.</summary>
        private sealed class IntroScope : IDisposable
        {
            private readonly bool originalEnabled, originalCards;
            private readonly DuelPresentationSettings originalSettings;
            private readonly FieldInfo sceneSource;
            private readonly object originalSource;
            private readonly Action<float, Keyboard> advance;
            /// <summary>Mission scene texts by Resources path; any other path has no cutscene.</summary>
            public readonly Dictionary<string, string> Scenes = new Dictionary<string, string>();
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Tuning { get; }

            public IntroScope(bool startCards)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                originalEnabled = Controller.enabled;
                originalCards = Controller.StartCardsEnabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                Tuning = Object.Instantiate(originalSettings);
                SetField("presentationSettings", Tuning);
                sceneSource = typeof(DuelPrototypeController).GetField("missionSceneText", PrivateInstance);
                Assert.That(sceneSource, Is.Not.Null);
                originalSource = sceneSource.GetValue(Controller);
                sceneSource.SetValue(Controller, (Func<string, string>)(path => Scenes.TryGetValue(path, out string text) ? text : null));
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Controller.RestartJourney();
                Controller.StartCardsEnabled = startCards;
            }

            public void Settings(string json) => JsonUtility.FromJsonOverwrite(json, Tuning);

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public void AdvanceFor(float seconds)
            {
                for (float elapsed = 0f; elapsed < seconds - 1e-4f; elapsed += Frame) Advance(Frame);
            }

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

            /// <summary>A stage battle (stage 1) on <paramref name="duel"/>, opening as a new attempt would.</summary>
            public void StartStage(LegacyQueuedDuel duel)
            {
                if (!Controller.IsInLobby) Controller.ReturnToLobby();
                Assert.That(Controller.StartCampaignStage(1), Is.True);
                Install(duel);
            }

            /// <summary>The battle on screen restarts as <paramref name="duel"/>.</summary>
            public void Install(LegacyQueuedDuel duel)
            {
                SetField("session", duel);
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
            }

            /// <summary>The 서막's mission <paramref name="number"/> in battle: the missions before it counted won, its intro
            /// skipped.</summary>
            public void StartMission(int number)
            {
                Controller.StartNewGame();
                for (int before = 1; before < number; before++)
                    Assert.That(Controller.Prologue.TryComplete(before, DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(Controller.StartMission(), Is.True);
                if (Controller.IsPlayingScene) Assert.That(Controller.SkipScene(), Is.True);
                Assert.That(Controller.IsMission && Controller.ActiveMission.Number == number, Is.True);
            }

            public void Dispose()
            {
                sceneSource.SetValue(Controller, originalSource);
                SetField("presentationSettings", originalSettings);
                Controller.StartCardsEnabled = originalCards;
                Controller.RestartJourney();
                Controller.enabled = originalEnabled;
                Object.Destroy(Tuning);
            }
        }
    }
}
