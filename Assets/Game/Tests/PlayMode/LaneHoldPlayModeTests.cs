using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>Holding a lane through the real controller (<see cref="LegacyLaneHold"/>): a tap of its key or its gear's
    /// window queues, a hold opens the skill's explanation sooner (0.35 s), larger and above the dock (above the coach's
    /// card while one is up), its release never queues, nor does a hold's that Tab cut, and while the player reads it the
    /// planning clock runs slower; nothing slows outside planning.</summary>
    public sealed class LaneHoldPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator KeyHold_TapQueues_LongerPressDoesNot_AndAHoldOpensTheExplanationWithoutQueuing()
        {
            yield return null;
            using (var scope = new HoldScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;
                float tap = scope.Settings.LaneTapSeconds, explain = scope.Settings.ExplanationHoldSeconds;
                Assert.That(tap, Is.EqualTo(.2f), "The author's quicker tap…");
                Assert.That(explain, Is.EqualTo(.35f), "…and quicker explanation.");

                // The frame a press is first seen counts no time held; the held frames after it do.
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(tap * .75f, keyboard);
                Assert.That(scope.HoldFill(0), Is.Zero, "No bar through a tap.");
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "A tap queues on release.");
                Assert.That(duel.PlayerQueue[0].Id, Is.EqualTo(101));
                Assert.That(scope.HoldCancel(0).activeSelf, Is.False, "A successful tap needs no cancel cue.");

                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance((tap + explain) * .5f, keyboard);
                Assert.That(scope.HoldFill(0), Is.EqualTo(.5f).Within(.01f), "The bar fills between the tap and the explanation.");
                Assert.That(scope.Explanation.gameObject.activeSelf, Is.False);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "Past a tap, a release queues nothing.");
                Assert.That(scope.HoldFill(0), Is.Zero);
                Assert.That(scope.HoldCancel(0).activeSelf, Is.True, "The safety interval gives a brief cancel cue.");
                Assert.That(scope.Get("Input/Keys/Current Q/Hold Cancel/Hold Cancel Label").GetComponent<Text>().text,
                    Is.EqualTo("입력 취소"));
                scope.Advance(.37f, keyboard);
                Assert.That(scope.HoldCancel(0).GetComponent<CanvasGroup>().alpha, Is.InRange(.1f, .9f),
                    "The short cue fades before it disappears.");
                scope.Advance(.09f, keyboard);
                Assert.That(scope.HoldCancel(0).activeSelf, Is.False, "The cue clears on real time.");

                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(explain, keyboard);
                Assert.That(scope.Explanation.gameObject.activeSelf, Is.True, "At 0.35 s the explanation opens.");
                Assert.That(scope.HoldFill(0), Is.EqualTo(1f).Within(1e-4f));
                Assert.That(controller.IsReadingExplanation, Is.True);
                scope.Advance(1f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(scope.Explanation.gameObject.activeSelf, Is.False, "Letting go closes it…");
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "…and never queues.");
                Assert.That(controller.IsReadingExplanation, Is.False);
                Assert.That(scope.HoldCancel(0).activeSelf, Is.False, "Closing a fully opened explanation is not a cancellation.");
            }
        }

        [UnityTest]
        public IEnumerator ReadingAnExplanation_SlowsThePlanningClock_AndNothingSlowsOutsidePlanning()
        {
            yield return null;
            using (var scope = new HoldScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                float reading = scope.Settings.ExplanationTimeScale;
                Assert.That(reading, Is.EqualTo(.3f));
                Assert.That(controller.PlanningTimeScale, Is.EqualTo(1f));
                Press(keyboard.wKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                Assert.That(controller.IsReadingExplanation, Is.True);
                Assert.That(controller.PlanningTimeScale, Is.EqualTo(reading));
                float clock = controller.TurnTimeRemaining;
                scope.Advance(1f, keyboard);
                Assert.That(clock - controller.TurnTimeRemaining, Is.EqualTo(reading).Within(1e-4f),
                    "A second of reading spends 0.3 s of planning time.");
                Release(keyboard.wKey);
                yield return null;
                scope.Advance(0f, keyboard);
                clock = controller.TurnTimeRemaining;
                scope.Advance(1f, keyboard);
                Assert.That(clock - controller.TurnTimeRemaining, Is.EqualTo(1f).Within(1e-4f), "Closed, the clock runs again.");

                Press(keyboard.wKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                Assert.That(controller.IsReadingExplanation, Is.True);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.IsReadingExplanation, Is.False, "The commit closes it…");
                Assert.That(controller.PlanningTimeScale, Is.EqualTo(1f), "…and nothing slows outside planning.");
                Release(keyboard.wKey);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheExplanation_OpensLargerAboveTheDock_CentredOnTheHeldLane_OverTheHeadHud()
        {
            yield return null;
            using (var scope = new HoldScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                foreach (int lane in new[] { 0, 2 })
                {
                    Press(Key(keyboard, lane));
                    yield return null;
                    scope.Advance(0f, keyboard);
                    yield return null;
                    scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                    Canvas.ForceUpdateCanvases();
                    RectTransform explanation = scope.Explanation;
                    Assert.That(explanation.gameObject.activeSelf, Is.True);
                    Rect panel = WorldRect(explanation), dock = WorldRect(scope.Get("Input"));
                    Rect window = WorldRect(scope.Get("Input/Keys/Current " + "QWE"[lane]));
                    float unit = scope.Controller.Hud.Root.GetComponent<Canvas>().scaleFactor;
                    Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(dock.yMax), "Above the dock, clear of the gears…");
                    Assert.That(panel.yMin - dock.yMax, Is.EqualTo(LegacyCombatHud.ExplanationGap * unit).Within(1f), "…just above it…");
                    Assert.That(panel.center.x, Is.EqualTo(window.center.x).Within(1f), "…centred on the held lane.");
                    Assert.That(panel.xMin >= 0f && panel.xMax <= Screen.width && panel.yMax <= Screen.height, Is.True, "On screen.");
                    Release(Key(keyboard, lane));
                    yield return null;
                    scope.Advance(0f, keyboard);
                }

                Transform root = scope.Controller.Hud.Root.transform;
                RectTransform player = scope.Explanation, enemy = scope.Get("Enemy Skill Explain");
                Assert.That(player.rect.width, Is.EqualTo(enemy.rect.width * LegacyCombatHud.PlayerExplanationScale).Within(.01f),
                    "Larger than the enemy's card…");
                Assert.That(player.Find("Name").GetComponent<Text>().fontSize, Is.GreaterThan(enemy.Find("Name").GetComponent<Text>().fontSize),
                    "…its type too…");
                Assert.That(player.Find("Explanation Hint").GetComponent<Text>().fontSize,
                    Is.GreaterThan(enemy.Find("Explanation Hint").GetComponent<Text>().fontSize));
                Shadow shade = player.GetComponent<Shadow>();
                Assert.That(shade, Is.Not.Null, "…on a dark shadow that lifts it off the arena.");
                Assert.That(shade.effectColor.a, Is.GreaterThan(.5f));
                foreach (string head in new[] { "PlayerStatus", "Player Requests", "EnemyStatus", "Enemy Requests", "Input" })
                    Assert.That(player.GetSiblingIndex(), Is.GreaterThan(root.Find(head).GetSiblingIndex()), head + " never covers it.");
            }
        }

        [UnityTest]
        public IEnumerator UnderTheCoachsCard_TheExplanationOpensAboveIt_AndBackAboveTheDockWithoutIt()
        {
            yield return null;
            using (var scope = new HoldScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                MissionCoachHud coach = scope.Controller.CoachHud;
                // A coached mission keeps its card up over the dock through its free beats while the player holds a lane to
                // read it (수련 5 asks for just that); any beat's card stands in for it here.
                coach.Show(PrologueMissions.Get(2).CreateGuide(), MissionCoachCard.MissionLabel(2));
                yield return null;
                try
                {
                    var coachRoot = (RectTransform)coach.Root.transform;
                    var card = (RectTransform)coachRoot.Find("Coach Border");
                    Assert.That(card, Is.Not.Null);
                    float unit = scope.Controller.Hud.Root.GetComponent<Canvas>().scaleFactor;
                    foreach (int lane in new[] { 0, 2 })
                    {
                        Press(Key(keyboard, lane));
                        yield return null;
                        scope.Advance(0f, keyboard);
                        yield return null;
                        scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                        Canvas.ForceUpdateCanvases();
                        Assert.That(scope.Explanation.gameObject.activeSelf, Is.True);
                        Rect panel = WorldRect(scope.Explanation), over = WorldRect(card);
                        Assert.That(coach.TopEdge, Is.EqualTo(over.yMax).Within(1f), "The card's top, on the screen.");
                        Assert.That(panel.Overlaps(over), Is.False, "QWE"[lane] + ": the coach's card never covers the explanation…");
                        Assert.That(panel.yMin - over.yMax, Is.EqualTo(LegacyCombatHud.ExplanationGap * unit).Within(1f), "…just above it.");
                        Release(Key(keyboard, lane));
                        yield return null;
                        scope.Advance(0f, keyboard);
                    }
                }
                finally { coach.Hide(); }

                Assert.That(coach.TopEdge, Is.Zero, "A hidden card holds nothing up…");
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                Canvas.ForceUpdateCanvases();
                Rect alone = WorldRect(scope.Explanation), dock = WorldRect(scope.Get("Input"));
                Assert.That(alone.yMin - dock.yMax, Is.EqualTo(LegacyCombatHud.ExplanationGap *
                    scope.Controller.Hud.Root.GetComponent<Canvas>().scaleFactor).Within(1f), "…and the explanation sits on the dock again.");
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
            }
        }

        [UnityTest]
        public IEnumerator AHoldCutByTab_NeverQueuesOnItsRelease_AndTheNextPressStillTaps()
        {
            yield return null;
            using (var scope = new HoldScope())
            {
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;

                // Q held until its explanation opens, then Tab to compare with the enemy's queue, then both let go at once.
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                Assert.That(controller.IsReadingExplanation, Is.True);
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(.1f, keyboard);
                Assert.That(controller.IsReadingExplanation, Is.False, "Tab shows the enemy's queue instead.");
                Release(keyboard.tabKey);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue, Is.Empty, "Letting go of the hold Tab cut never queues…");
                Assert.That(scope.HoldFill(0), Is.Zero);
                Assert.That(scope.HoldCancel(0).activeSelf, Is.False, "An interrupted hold is not a safety cancellation.");

                // The same, Q let go a moment (under a tap) after Tab.
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(scope.Settings.ExplanationHoldSeconds, keyboard);
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(.1f, keyboard);
                Release(keyboard.tabKey);
                yield return null;
                scope.Advance(scope.Settings.LaneTapSeconds * .5f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue, Is.Empty, "…nor a moment after Tab.");

                // Q let go under Tab: its hold ends there, unseen, and queues nothing either.
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                yield return null;
                scope.Advance(scope.Settings.LaneTapSeconds * .5f, keyboard);
                Press(keyboard.tabKey);
                yield return null;
                scope.Advance(.1f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(.1f, keyboard);
                Release(keyboard.tabKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue, Is.Empty, "A key let go under Tab queues nothing.");

                // A new press after all that is a tap again.
                Press(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Release(keyboard.qKey);
                yield return null;
                scope.Advance(0f, keyboard);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "The next press still taps.");
            }
        }

        [UnityTest]
        public IEnumerator PointerHold_OnTheWindow_TapsHoldsAndLetsGoAway_LikeTheKey()
        {
            yield return null;
            using (var scope = new HoldScope())
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyQueuedDuel duel = controller.Session;
                GameObject window = scope.Get("Input/Keys/Current Q").gameObject;
                Assert.That(window.GetComponent<LanePointerHold>(), Is.Not.Null, "The window is the lane's hold for the pointer.");

                // A click: down, and up on the window with its click in the same frame. The controller judges the release.
                Down(window);
                scope.Advance(0f);
                scope.Advance(.1f);
                Up(window, true);
                Assert.That(duel.PlayerQueue, Is.Empty, "The click itself does not queue a press the window saw…");
                scope.Advance(0f);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "…its release, a tap, does: once.");
                Assert.That(controller.Hud.IsLaneTurning(0), Is.True, "The gear turns for it.");

                // Releasing between a tap and an explanation deliberately cancels, with visible feedback.
                Down(window);
                scope.Advance(0f);
                scope.Advance((scope.Settings.LaneTapSeconds + scope.Settings.ExplanationHoldSeconds) * .5f);
                Up(window, true);
                scope.Advance(0f);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1));
                Assert.That(scope.HoldCancel(0).activeSelf, Is.True);
                Assert.That(controller.CycleLanes(), Is.True);
                Assert.That(scope.HoldCancel(0).activeSelf, Is.False,
                    "Turning to a new skill clears a previous cancel cue immediately.");

                // A hold: the explanation opens at 0.35 s and its release only closes it.
                Down(window);
                scope.Advance(0f);
                scope.Advance(scope.Settings.ExplanationHoldSeconds);
                Assert.That(scope.Explanation.gameObject.activeSelf, Is.True, "Holding the window explains its skill.");
                Assert.That(controller.IsReadingExplanation, Is.True);
                Up(window, true);
                scope.Advance(0f);
                Assert.That(scope.Explanation.gameObject.activeSelf, Is.False);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "Releasing after it opened never queues.");

                // Pressed, dragged away and let go off the window: like a cancelled click, nothing.
                Down(window);
                scope.Advance(0f);
                scope.Advance(.05f);
                Up(window, false);
                scope.Advance(0f);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(1), "Let go off the window: no queue.");
                Assert.That(scope.HoldFill(0), Is.Zero);
                Assert.That(scope.HoldCancel(0).activeSelf, Is.False, "Dragging away cancels without the safety interval cue.");

                // A click with no press the window saw (an assistive input, a test) still queues at once.
                yield return null;
                window.GetComponent<Button>().onClick.Invoke();
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(2));
            }
        }

        private static KeyControl Key(Keyboard keyboard, int lane) => lane == 0 ? keyboard.qKey : lane == 1 ? keyboard.wKey : keyboard.eKey;

        private static PointerEventData Pointer(GameObject over) => new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            pointerCurrentRaycast = new RaycastResult { gameObject = over },
        };

        private static void Down(GameObject window)
            => ExecuteEvents.Execute(window, Pointer(window), ExecuteEvents.pointerDownHandler);

        // Up on the window (its click follows in the same frame, as the input module sends them) or off it.
        private static void Up(GameObject window, bool onWindow)
        {
            PointerEventData pointer = Pointer(onWindow ? window : null);
            ExecuteEvents.Execute(window, pointer, ExecuteEvents.pointerUpHandler);
            if (onWindow) ExecuteEvents.Execute(window, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>The real controller, driven frame by frame with chosen real seconds, on a free duel with Q = 하나 둘 넷,
        /// W = 셋 여섯 and E = 다섯; the presentation settings are a copy of the project's.</summary>
        private sealed class HoldScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originallyEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }
            public RectTransform Explanation => Get("Skill Explain");

            public HoldScope()
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "Tests never write the player's save.");
                originallyEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                Settings = Object.Instantiate(originalSettings);
                JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0,\"skillInterval\":0}", Settings);
                Set("presentationSettings", Settings);
                arenaSettings.SetValue(Controller.ArenaView, Settings);
                advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                    typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                Set("mission", null);
                Set("guide", null);
                Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                    new[] { Skill(101, "하나", 0), Skill(102, "둘", 0), Skill(104, "넷", 0), Skill(103, "셋", 1),
                        Skill(106, "여섯", 1), Skill(105, "다섯", 2) },
                    new[] { Skill(201, "적", 0) }, new[] { 1 }, 1));
                typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance).Invoke(Controller, null);
                Advance(0f);
                Assert.That(Controller.CanChoose, Is.True);
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            public RectTransform Get(string path)
            {
                Transform node = Controller.Hud.Root.transform.Find(path);
                Assert.That(node, Is.Not.Null, "Missing HUD node: " + path);
                return (RectTransform)node;
            }

            public float HoldFill(int lane) => Get("Input/Keys/Current " + "QWE"[lane] + "/KeyHoldImage").GetComponent<Image>().fillAmount;

            public GameObject HoldCancel(int lane) => Get("Input/Keys/Current " + "QWE"[lane] + "/Hold Cancel").gameObject;

            private static LegacySkill Skill(int id, string name, int lane)
                => new LegacySkill(id, name, 1, 1, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, lane,
                    string.Empty, "Slash", 1);

            private void Set(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, PrivateInstance).SetValue(Controller, value);

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                Set("session", originalSession);
                // Leave a fresh arc and lobby behind, as the other flow tests do.
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originallyEnabled;
                Object.Destroy(Settings);
            }
        }
    }
}
