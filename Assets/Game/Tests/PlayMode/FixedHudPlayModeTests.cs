using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class FixedHudPlayModeTests : InputTestFixture
    {
        private static readonly string[] QueueNodes =
        {
            "Player Requests", "Enemy Requests",
        };

        // 2026-10-15: the queues left the fighters' heads for two facing panels over the dock (LegacyCombatHud.QueuePanels.cs).
        // The status panels still ride the heads; the queue rows stay put over the dock, mirrored round the centre seam.
        [UnityTest]
        public IEnumerator QueuePanels_StayOverTheDock_WhileTheCameraAndActorsMove()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Assert.That(controller.QueueLane(0), Is.True);
            Assert.That(controller.QueueLane(2), Is.True);
            Assert.That(controller.QueueLane(0), Is.True);
            Camera camera = controller.ArenaView.ArenaCamera;
            Transform player = controller.ArenaView.PlayerRenderer.transform;
            Transform enemy = controller.ArenaView.EnemyRenderer.transform;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            float cameraSize = camera.orthographicSize;
            Vector3 playerPosition = player.position, enemyPosition = enemy.position;
            Transform root = controller.Hud.Root.transform;
            try
            {
                RefreshHud(controller, false);
                Vector2[] positions = CaptureQueuePositions(root);
                Vector2[] cards = CaptureFirstCards(root);
                AssertPanelLayout(root);
                AssertPanelsOverDock(root);
                camera.orthographicSize = 2.8f;
                camera.transform.position = cameraPosition + new Vector3(4, -2, 0);
                camera.transform.rotation = Quaternion.Euler(0, 0, 17);
                player.position = playerPosition + new Vector3(3, 1.5f, 0);
                enemy.position = enemyPosition + new Vector3(-3, -1, 0);
                RefreshHud(controller, false);
                for (int index = 0; index < QueueNodes.Length; index++)
                {
                    Assert.That(Vector2.Distance(Get<RectTransform>(root, QueueNodes[index]).anchoredPosition, positions[index]),
                        Is.LessThan(.01f), QueueNodes[index] + " stays in its panel over the dock, whatever the camera does.");
                    Assert.That(Vector2.Distance(CaptureFirstCards(root)[index], cards[index]), Is.LessThan(.01f));
                }
                AssertPanelLayout(root);
            }
            finally
            {
                camera.transform.position = cameraPosition;
                camera.transform.rotation = cameraRotation;
                camera.orthographicSize = cameraSize;
                player.position = playerPosition;
                enemy.position = enemyPosition;
                controller.RestartMatch();
            }
        }

        [UnityTest]
        public IEnumerator QueuePanels_KeepTheirMirroredRows_ThroughPlanningCombatAndNextTurn()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Transform root = controller.Hud.Root.transform;
            try
            {
                // Three opening slots stay affordable after W's first skill rises to ACT 2.
                Assert.That(controller.QueueLane(0), Is.True);
                Assert.That(controller.QueueLane(2), Is.True);
                Assert.That(controller.QueueLane(0), Is.True);
                yield return null;
                AssertPanelLayout(root);
                AssertPanelsOverDock(root);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                float deadline = Time.realtimeSinceStartup + 25f;
                while (controller.IsResolving && Time.realtimeSinceStartup < deadline)
                {
                    // Sinking with the folding dock and sliding toward the seam, the rows keep their mirror and order (the
                    // clash motion's card is read at its place in the row, less the offset the motion draws it with).
                    AssertPanelLayout(root, false, controller.Hud);
                    yield return null;
                }
                Assert.That(controller.IsResolving, Is.False, "The queue must finish rather than stall.");
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
                yield return null;
                AssertPanelLayout(root);
                Assert.That(controller.Hud.QueueSlide, Is.Zero, "A new turn starts with the rows at rest.");
            }
            finally { controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator StatusPanels_FollowActorsImmediately_KeepScreenSizeWhileZooming_QueuesStayPut()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            RectTransform root = controller.Hud.Root.GetComponent<RectTransform>();
            Camera camera = controller.ArenaView.ArenaCamera;
            Transform player = controller.ArenaView.PlayerRenderer.transform;
            Transform enemy = controller.ArenaView.EnemyRenderer.transform;
            Vector3 cameraPosition = camera.transform.position;
            Quaternion cameraRotation = camera.transform.rotation;
            float cameraSize = camera.orthographicSize;
            Vector3 playerPosition = player.position, enemyPosition = enemy.position;
            try
            {
                // Keep both heads central so boundary clamping cannot hide tracking errors.
                camera.transform.rotation = Quaternion.identity;
                camera.orthographicSize = 6f;
                player.position = new Vector3(cameraPosition.x - 1.5f, cameraPosition.y - .5f, playerPosition.z);
                enemy.position = new Vector3(cameraPosition.x + 1.5f, cameraPosition.y - .5f, enemyPosition.z);
                Canvas.ForceUpdateCanvases();
                RefreshHud(controller, false, 0f);
                Vector2[] queuePositions = CaptureQueuePositions(root);
                RectTransform playerStatus = Get<RectTransform>(root, "PlayerStatus");
                RectTransform enemyStatus = Get<RectTransform>(root, "EnemyStatus");
                Vector2 playerScreenSize = ScreenRect(playerStatus).size;
                Vector2 enemyScreenSize = ScreenRect(enemyStatus).size;
                Vector2[] queueCardSizes = CaptureQueueCardSizes(root);
                AssertHeadStatus(playerStatus, player, new Vector3(-.6f, 1.1f, 0), camera, root);
                AssertHeadStatus(enemyStatus, enemy, new Vector3(-.4f, 1f, 0), camera, root);
                AssertPanelLayout(root);
                Vector2 playerCenter = playerStatus.anchoredPosition;
                Vector2 enemyCenter = enemyStatus.anchoredPosition;

                player.position += new Vector3(.6f, .35f, 0);
                enemy.position += new Vector3(-.6f, .3f, 0);
                RefreshHud(controller, false, 0f);
                Assert.That(Vector2.Distance(playerStatus.anchoredPosition, playerCenter), Is.GreaterThan(1f));
                Assert.That(Vector2.Distance(enemyStatus.anchoredPosition, enemyCenter), Is.GreaterThan(1f));
                AssertHeadStatus(playerStatus, player, new Vector3(-.6f, 1.1f, 0), camera, root);
                AssertHeadStatus(enemyStatus, enemy, new Vector3(-.4f, 1f, 0), camera, root);
                for (int index = 0; index < QueueNodes.Length; index++)
                    Assert.That(Vector2.Distance(Get<RectTransform>(root, QueueNodes[index]).anchoredPosition, queuePositions[index]),
                        Is.LessThan(.01f), "The queues stay in their panels over the dock while the fighters move.");
                AssertPanelLayout(root);

                camera.orthographicSize = 4f;
                camera.transform.position += new Vector3(.2f, .2f, 0);
                RefreshHud(controller, false, 0f);
                AssertHeadStatus(playerStatus, player, new Vector3(-.6f, 1.1f, 0), camera, root);
                AssertHeadStatus(enemyStatus, enemy, new Vector3(-.4f, 1f, 0), camera, root);
                Assert.That(Vector2.Distance(ScreenRect(playerStatus).size, playerScreenSize), Is.LessThan(.01f));
                Assert.That(Vector2.Distance(ScreenRect(enemyStatus).size, enemyScreenSize), Is.LessThan(.01f));
                AssertQueueCardSizes(root, queueCardSizes);
                AssertPanelLayout(root);
            }
            finally
            {
                camera.transform.position = cameraPosition;
                camera.transform.rotation = cameraRotation;
                camera.orthographicSize = cameraSize;
                player.position = playerPosition;
                enemy.position = enemyPosition;
                controller.RestartMatch();
            }
        }

        [UnityTest]
        public IEnumerator TabInspection_KeepsNormalPlanningCamera_AndQueuePanelsAndConstantSizeStatus()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            RectTransform root = controller.Hud.Root.GetComponent<RectTransform>();
            Camera camera = controller.ArenaView.ArenaCamera;
            Transform enemy = controller.ArenaView.EnemyRenderer.transform;
            RectTransform status = Get<RectTransform>(root, "EnemyStatus");
            Canvas.ForceUpdateCanvases();
            Vector2 initialStatusSize = ScreenRect(status).size;
            Vector2[] initialCardSizes = CaptureQueueCardSizes(root);
            try
            {
                Press(keyboard.tabKey);
                yield return new WaitForSecondsRealtime(.55f);
                yield return null;
                Assert.That(controller.Hud.IsQueueComparisonVisible, Is.True);
                AssertInspectionKeepsPlanningCamera(controller, true);
                Assert.That(Get<RectTransform>(root, "Enemy Skill Explain").gameObject.activeSelf, Is.True);
                AssertHeadStatus(status, enemy, new Vector3(-.4f, 1f, 0), camera, root);
                AssertPanelLayout(root);
                Assert.That(Vector2.Distance(ScreenRect(status).size, initialStatusSize), Is.LessThan(.01f));
                AssertQueueCardSizes(root, initialCardSizes);

                Release(keyboard.tabKey);
                yield return new WaitForSecondsRealtime(.55f);
                yield return null;
                Assert.That(controller.Hud.IsQueueComparisonVisible, Is.False);
                AssertInspectionKeepsPlanningCamera(controller, false);
                Assert.That(Get<RectTransform>(root, "Enemy Skill Explain").gameObject.activeSelf, Is.False);
                AssertPanelLayout(root);
                Assert.That(Vector2.Distance(ScreenRect(status).size, initialStatusSize), Is.LessThan(.01f));
                AssertQueueCardSizes(root, initialCardSizes);
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Assert.That(controller.Session.Act, Is.EqualTo(3));
            }
            finally
            {
                Release(keyboard.tabKey);
                controller.RestartMatch();
            }
        }

        [UnityTest]
        public IEnumerator LogKeyboardHint_TogglesWithLWithoutSpendingAct_AndSpaceCommitsAndClosesIt()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            int initialAct = controller.Session.Act;
            try
            {
                Press(keyboard.lKey);
                yield return null;
                yield return null;
                Assert.That(controller.Hud.LogOpen, Is.True);
                Assert.That(controller.Session.Act, Is.EqualTo(initialAct));
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Release(keyboard.lKey);
                yield return null;

                Press(keyboard.lKey);
                yield return null;
                yield return null;
                Assert.That(controller.Hud.LogOpen, Is.False);
                Assert.That(controller.Session.Act, Is.EqualTo(initialAct));
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Release(keyboard.lKey);
                yield return null;

                Press(keyboard.lKey);
                yield return null;
                yield return null;
                Assert.That(controller.Hud.LogOpen, Is.True);
                Release(keyboard.lKey);
                yield return null;
                Press(keyboard.spaceKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.Hud.LogOpen, Is.False, "A must close the log and commit the same whole-turn action.");
                Assert.That(controller.Session.Act, Is.EqualTo(initialAct));
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Release(keyboard.spaceKey);
                yield return null;

                Press(keyboard.lKey);
                yield return null;
                yield return null;
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.Hud.LogOpen, Is.False, "L must not open the log while combat input is blocked.");
            }
            finally
            {
                Release(keyboard.lKey);
                Release(keyboard.spaceKey);
                controller.RestartMatch();
            }
        }

        [UnityTest]
        public IEnumerator ActionButtonIconsAndHints_FitWithoutOverlap_AndIconClicksReachTheButton()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            // The battle opens behind the full-screen loading bridge, which blocks input by design;
            // real clicks wait for it to close.
            yield return WaitForLoadingBridge(controller);
            // A standalone run begins in the lobby. The newly enabled battle
            // graphics must get a render frame before testing actual raycasts.
            yield return null;
            Transform root = controller.Hud.Root.transform;
            Canvas.ForceUpdateCanvases();
            try
            {
                string[] buttons = { "LogButton", "AButton" };
                string[] resources = { "HudActions/combat-log", "AcademyUI/commit-seal" };
                string[] keys = { "L", "Space" };
                for (int index = 0; index < buttons.Length; index++)
                {
                    Button button = Get<Button>(root, "Input/Keys/" + buttons[index]);
                    Image icon = Get<Image>(button.transform, "Icon");
                    Text label = Get<Text>(button.transform, "Label");
                    Text hint = Get<Text>(button.transform, "KeyHint");
                    Sprite source = Resources.Load<Sprite>(resources[index]);
                    Assert.That(source, Is.Not.Null);
                    Assert.That(icon.sprite, Is.SameAs(source));
                    Assert.That(icon.preserveAspect, Is.True);
                    Assert.That(hint.text, Is.EqualTo(keys[index]));
                    Assert.That(hint.preferredWidth, Is.LessThanOrEqualTo(hint.rectTransform.rect.width),
                        "The complete confirmation key must fit on one line.");
                    Assert.That(label.text, Is.Not.Empty);
                    RectTransform buttonRect = button.GetComponent<RectTransform>();
                    // Different illustrated controls have different sizes. Their actual content must fit,
                    // remain separate and resolve to the same action when the illustration is clicked.
                    AssertWithin(icon.rectTransform, buttonRect);
                    AssertWithin(label.rectTransform, buttonRect);
                    AssertWithin(hint.rectTransform, buttonRect);
                    Assert.That(ScreenRect(label.rectTransform).yMax, Is.LessThanOrEqualTo(ScreenRect(icon.rectTransform).yMin + .5f),
                        buttons[index] + " label must sit below its icon.");
                    Assert.That(ScreenRect(hint.rectTransform).yMax, Is.LessThanOrEqualTo(ScreenRect(label.rectTransform).yMin + .5f),
                        buttons[index] + " keyboard hint must sit below its label.");
                }

                ClickIcon(Get<Button>(root, "Input/Keys/LogButton"));
                Assert.That(controller.Hud.LogOpen, Is.True);
                Assert.That(controller.Session.Act, Is.EqualTo(3));
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Get<Button>(root, "Log View/Close Log").onClick.Invoke();
                controller.Hud.CloseLog(true);
                Canvas.ForceUpdateCanvases();
                ClickIcon(Get<Button>(root, "Input/Keys/AButton"));
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.Hud.LogOpen, Is.False);
            }
            finally { controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator LongSkillQueues_KeepReadableMirroredCards_AndWindowTheOverflow()
        {
            yield return null;
            AssertLongQueue(6);
            // Far more than fits a panel: both sides share a readable window instead of overlapping icons.
            AssertLongQueue(24);
        }

        private static void AssertLongQueue(int skillCount)
        {
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            RectTransform root = controller.Hud.Root.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            // Zero-cost copies isolate presentation capacity from ACT budgeting.
            // The real catalog and the controller's authoritative match stay unchanged.
            var freePlayerSkills = new List<LegacySkill>();
            foreach (LegacySkill source in LegacyInitialSkills.All)
                freePlayerSkills.Add(new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower,
                    source.Kind, source.Property, source.AttackCount, source.LaneIndex, source.Description, source.AnimationName));
            var queueFixture = new LegacyQueuedDuel(100, 50, 80, 15, freePlayerSkills,
                LegacyInitialSkills.All, new[] { skillCount }, 1);
            for (int index = 0; index < skillCount; index++)
                Assert.That(queueFixture.TryQueueLane(index % 3), Is.True);
            try
            {
                var arena = controller.ArenaView;
                controller.Hud.Refresh(queueFixture, 10f, false, -1, arena.ArenaCamera,
                    arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, .1f, .1f);
                Canvas.ForceUpdateCanvases();
                AssertPanelLayout(root);
                // Slot 1 at the panel's inner end, by the seam: half the seam, the panel's padding and half a card out.
                float inner = SeamHalf(root) + LegacyQueuePanelLayout.Padding + controller.Hud.QueueCardWidth * .5f;
                foreach (string name in new[] { "Player Requests", "Enemy Requests" })
                {
                    bool playerSide = name == "Player Requests";
                    var cards = VisibleQueueCards(root.Find(name));
                    Assert.That(cards.Count, Is.EqualTo(Mathf.Min(skillCount, controller.Hud.QueueWindowCapacity)));
                    Assert.That(cards[0].anchoredPosition.x, Is.EqualTo(playerSide ? -inner : inner).Within(.01f),
                        name + ": slot 1 sits at the panel's inner end, by the centre seam.");
                    float pitch = Mathf.Abs(cards[1].anchoredPosition.x - cards[0].anchoredPosition.x);
                    for (int index = 0; index < cards.Count; index++)
                    {
                        Assert.That(cards[index].rect.size, Is.EqualTo(new Vector2(64, 64)));
                        Assert.That(cards[index].anchoredPosition.y, Is.EqualTo(0f), name + " never wraps.");
                        AssertWithin(cards[index], root);
                        if (index > 0)
                            Assert.That(Mathf.Sign(cards[index].anchoredPosition.x - cards[index - 1].anchoredPosition.x),
                                Is.EqualTo(playerSide ? -1f : 1f), name + " grows outward.");
                        for (int previous = 0; previous < index; previous++)
                            Assert.That(ScreenRect(cards[index]).Overlaps(ScreenRect(cards[previous])), Is.False,
                                name + " keeps every visible icon separate, even in a long queue.");
                    }
                    if (skillCount <= 6) Assert.That(pitch, Is.EqualTo(controller.PresentationSettings.QueueCardSpacing).Within(.01f));
                    if (skillCount >= 24)
                    {
                        Assert.That(controller.Hud.QueueCardWidth, Is.EqualTo(LegacyQueuePanelLayout.MinimumCardSize).Within(.01f));
                        Assert.That(controller.Hud.QueueWindowStart, Is.GreaterThan(0), "The newest reservation stays in view.");
                    }
                }
                // A long row never drags the panels together.
                Assert.That(ScreenRect(Get<RectTransform>(root, "PlayerStatus")).Overlaps(ScreenRect(Get<RectTransform>(root, "EnemyStatus"))),
                    Is.False);
                Assert.That(controller.Session.PlayerQueue, Is.Empty);
                Assert.That(controller.Session.Act, Is.EqualTo(3));
            }
            finally { controller.RestartMatch(); }
        }

        private static DuelPrototypeController FindPrototype()
        {
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }

        private static T Get<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            Assert.That(child, Is.Not.Null, "Missing HUD node: " + path);
            T component = child.GetComponent<T>();
            Assert.That(component, Is.Not.Null, "Missing " + typeof(T).Name + " at " + path);
            return component;
        }

        private static void RefreshHud(DuelPrototypeController controller, bool resolving, float delta = .1f)
        {
            var arena = controller.ArenaView;
            controller.Hud.Refresh(controller.Session, controller.TurnTimeRemaining, resolving, -1,
                arena.ArenaCamera, arena.PlayerRenderer.transform, arena.EnemyRenderer.transform, delta, delta);
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertInspectionKeepsPlanningCamera(DuelPrototypeController controller, bool inspecting)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                LegacyArenaView arena = controller.ArenaView;
                Camera camera = arena.ArenaCamera;
                float clock = controller.TurnTimeRemaining;
                float floor = controller.Hud.QueuePanelFloor;
                // Hold the same clock, actor positions and HUD floor while comparing both inspection states.
                arena.SetPlanningState(clock, inspecting);
                for (int frame = 0; frame < 4; frame++)
                {
                    arena.SetHudFloor(floor);
                    arena.Tick(0f, 1f);
                }
                float size = camera.orthographicSize;
                Vector3 position = camera.transform.localPosition;
                Quaternion rotation = camera.transform.localRotation;
                arena.SetPlanningState(clock, !inspecting);
                for (int frame = 0; frame < 4; frame++)
                {
                    arena.SetHudFloor(floor);
                    arena.Tick(0f, 1f);
                }
                Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.001f),
                    "Tab and normal planning use the same zoom at the same remaining time.");
                Assert.That(Vector3.Distance(camera.transform.localPosition, position), Is.LessThan(.001f),
                    "Tab cannot move the camera toward the enemy.");
                Assert.That(Quaternion.Angle(camera.transform.localRotation, rotation), Is.LessThan(.001f));
                arena.SetPlanningState(clock, inspecting);
                RefreshHud(controller, false, 0f);
            }
            finally { controller.enabled = wasEnabled; }
        }

        private static Vector2[] CaptureQueuePositions(Transform root)
        {
            var positions = new Vector2[QueueNodes.Length];
            for (int index = 0; index < QueueNodes.Length; index++)
                positions[index] = Get<RectTransform>(root, QueueNodes[index]).anchoredPosition;
            return positions;
        }

        private static List<RectTransform> VisibleQueueCards(Transform queue)
        {
            Assert.That(queue, Is.Not.Null);
            var cards = new List<RectTransform>();
            foreach (Transform child in queue)
                if (child.gameObject.activeSelf) cards.Add(child.GetComponent<RectTransform>());
            return cards;
        }

        private static Vector2[] CaptureQueueCardSizes(Transform root)
        {
            var sizes = new Vector2[QueueNodes.Length];
            for (int index = 0; index < QueueNodes.Length; index++)
            {
                var cards = VisibleQueueCards(root.Find(QueueNodes[index]));
                sizes[index] = cards.Count > 0 ? ScreenRect(cards[0]).size : Vector2.zero;
            }
            return sizes;
        }

        private static void AssertQueueCardSizes(Transform root, Vector2[] sizes)
        {
            for (int index = 0; index < QueueNodes.Length; index++)
            {
                var cards = VisibleQueueCards(root.Find(QueueNodes[index]));
                if (sizes[index] == Vector2.zero) Assert.That(cards, Is.Empty);
                else
                {
                    Assert.That(cards, Is.Not.Empty);
                    foreach (var card in cards)
                        Assert.That(Vector2.Distance(ScreenRect(card).size, sizes[index]), Is.LessThan(.01f),
                            "Camera zoom must not resize queue cards.");
                }
            }
        }

        private static Vector2[] CaptureFirstCards(Transform root)
        {
            var positions = new Vector2[QueueNodes.Length];
            for (int index = 0; index < QueueNodes.Length; index++)
            {
                var cards = VisibleQueueCards(root.Find(QueueNodes[index]));
                positions[index] = cards.Count > 0 ? (Vector2)cards[0].position : Vector2.zero;
            }
            return positions;
        }

        private static RectTransform Tray(Transform root, bool playerSide)
            => Get<RectTransform>(root, LegacyCombatHud.QueuePanelsName + "/" +
                (playerSide ? LegacyCombatHud.PlayerQueuePanelName : LegacyCombatHud.EnemyQueuePanelName));

        // Half the gap between the panels at the centre seam, HUD units (the enemy's tray starts there).
        private static float SeamHalf(Transform root)
        {
            RectTransform enemyTray = Tray(root, false);
            return enemyTray.anchoredPosition.x - enemyTray.sizeDelta.x * .5f;
        }

        // While planning the panels stand on the dock: just above its top, clear of the fighters' status panels.
        private static void AssertPanelsOverDock(Transform root)
        {
            RectTransform dock = Get<RectTransform>(root, "Input");
            float unit = root.GetComponent<Canvas>().scaleFactor;
            foreach (bool playerSide in new[] { true, false })
            {
                Rect tray = ScreenRect(Tray(root, playerSide));
                Assert.That(tray.yMin, Is.GreaterThanOrEqualTo(ScreenRect(dock).yMax - .5f), "Above the command dock…");
                Assert.That(tray.yMin - ScreenRect(dock).yMax, Is.LessThan(48f * unit), "…just above it.");
                foreach (string status in new[] { "PlayerStatus", "EnemyStatus" })
                    Assert.That(tray.Overlaps(ScreenRect(Get<RectTransform>(root, status))), Is.False,
                        "The panels never cover a fighter's status panel.");
            }
        }

        // The two facing panels: the player's queue (the viewer's) in the left one, the opponent's in the right one, both rows
        // starting from the centre seam (slot 1 innermost) and growing outward, slot k at the same distance from the seam on
        // both sides. At rest (atRest), every card stands inside its panel; mid-slide or as the dock folds the rows only keep
        // their mirror and order. While a slot resolves, the clash motion draws its pair off their places (pushed, knocked
        // back, dipped, stepped up: LegacyCombatHud.Clash.cs); with the HUD given, such a card is read at its place in the
        // row, its drawn position less the motion's offset.
        private static void AssertPanelLayout(Transform root, bool atRest = true, LegacyCombatHud hud = null)
        {
            RectTransform canvasRect = root.GetComponent<RectTransform>();
            float centre = ScreenRect(canvasRect).center.x;
            var rows = new List<Vector2>[QueueNodes.Length];
            for (int side = 0; side < QueueNodes.Length; side++)
            {
                bool playerSide = side == 0;
                RectTransform queue = Get<RectTransform>(root, QueueNodes[side]);
                Assert.That(queue.parent, Is.EqualTo(root), "The rows keep their names as the HUD root's children.");
                Assert.That(queue.anchorMin, Is.EqualTo(Vector2.one * .5f));
                Assert.That(queue.anchorMax, Is.EqualTo(Vector2.one * .5f));
                Assert.That(queue.localScale, Is.EqualTo(Vector3.one));
                Assert.That(queue.anchoredPosition.x, Is.EqualTo(0f).Within(.01f), "Both rows start from the centre seam.");
                RectTransform tray = Tray(root, playerSide);
                Rect trayRect = ScreenRect(tray);
                if (playerSide) Assert.That(trayRect.xMax, Is.LessThanOrEqualTo(centre + .5f), "The player's panel is the left one.");
                else Assert.That(trayRect.xMin, Is.GreaterThanOrEqualTo(centre - .5f), "The opponent's panel is the right one.");
                AssertWithin(tray, canvasRect);
                Assert.That(tray.parent.GetSiblingIndex(), Is.LessThan(queue.GetSiblingIndex()), "The panels draw under the cards.");
                var cards = VisibleQueueCards(queue);
                RectTransform posed = null;
                Vector2 offset = Vector2.zero;
                if (hud != null && hud.TryGetPosedQueueCard(playerSide, out int posedSlot, out offset))
                    posed = hud.GetQueuedSkillAnchor(playerSide, posedSlot);
                var places = new List<Vector2>(cards.Count);
                foreach (RectTransform card in cards) places.Add(card.anchoredPosition - (card == posed ? offset : Vector2.zero));
                rows[side] = places;
                for (int index = 0; index < cards.Count; index++)
                {
                    RectTransform card = cards[index];
                    Vector2 place = places[index];
                    Assert.That(card.rect.size, Is.EqualTo(new Vector2(64, 64)));
                    Assert.That(Get<RectTransform>(card, "Icon").rect.size, Is.EqualTo(new Vector2(48, 48)));
                    Assert.That(place.y, Is.EqualTo(0f).Within(.01f), "Never a second row.");
                    Assert.That(card.localScale.x, Is.InRange(0f, 1.2001f));
                    Assert.That(Mathf.Sign(place.x), Is.EqualTo(playerSide ? -1f : 1f), "On its own side of the seam.");
                    if (index > 0)
                        Assert.That(Mathf.Sign(place.x - places[index - 1].x),
                            Is.EqualTo(playerSide ? -1f : 1f), "Slot 1 innermost, later slots outward.");
                    AssertWithin(card, canvasRect);
                    if (!atRest) continue;
                    Vector2 middle = ScreenRect(card).center;
                    Assert.That(middle.x, Is.InRange(trayRect.xMin, trayRect.xMax), QueueNodes[side] + " card " + index + " in its panel.");
                    Assert.That(middle.y, Is.InRange(trayRect.yMin, trayRect.yMax));
                }
            }
            // Mirror images: slot k on both sides at the same distance from the seam (one shared spacing and slide).
            for (int index = 0; index < Mathf.Min(rows[0].Count, rows[1].Count); index++)
                Assert.That(rows[0][index].x, Is.EqualTo(-rows[1][index].x).Within(.01f),
                    "Slot " + (index + 1) + " pairs mirror each other across the seam.");
        }

        private static void AssertHeadStatus(RectTransform status, Transform actor, Vector3 headOffset, Camera camera, RectTransform root)
        {
            Assert.That(status.anchorMin, Is.EqualTo(Vector2.one * .5f));
            Assert.That(status.anchorMax, Is.EqualTo(Vector2.one * .5f));
            Assert.That(status.rect.size, Is.EqualTo(new Vector2(236, 92)));
            Assert.That(status.localScale, Is.EqualTo(Vector3.one));
            Vector3 head = camera.WorldToScreenPoint(actor.TransformPoint(headOffset));
            Rect bounds = ScreenRect(status);
            float canvasScale = root.GetComponent<Canvas>().scaleFactor;
            Assert.That(bounds.center.x, Is.EqualTo(head.x).Within(.5f), "The panel must be directly above its local head marker.");
            Assert.That(bounds.yMin, Is.EqualTo(head.y + 12f * canvasScale).Within(.5f),
                "The panel's lower edge must follow immediately with a constant 12-unit head gap, even at delta zero.");
            AssertWithin(status, root);
        }

        private static IEnumerator WaitForLoadingBridge(DuelPrototypeController controller)
        {
            Assert.That(controller.LoadingHud, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (controller.LoadingHud.IsVisible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.LoadingHud.IsVisible, Is.False, "The loading bridge must close on its own.");
        }

        private static void ClickIcon(Button button)
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            Image icon = Get<Image>(button.transform, "Icon");
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.EqualTo(button.gameObject),
                "Clicking the icon must resolve to its parent action button.");
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = min;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector2 corner = RectTransformUtility.WorldToScreenPoint(null, corners[index]);
                min = Vector2.Min(min, corner);
                max = Vector2.Max(max, corner);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertWithin(RectTransform child, RectTransform parent)
        {
            Rect inside = ScreenRect(child), outside = ScreenRect(parent);
            Assert.That(inside.xMin, Is.GreaterThanOrEqualTo(outside.xMin - 1), child.name);
            Assert.That(inside.xMax, Is.LessThanOrEqualTo(outside.xMax + 1), child.name);
            Assert.That(inside.yMin, Is.GreaterThanOrEqualTo(outside.yMin - 1), child.name);
            Assert.That(inside.yMax, Is.LessThanOrEqualTo(outside.yMax + 1), child.name);
        }
    }
}
