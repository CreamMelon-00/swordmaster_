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

        [UnityTest]
        public IEnumerator HeadQueuesAndStatus_FollowCameraAndActors_KeepTheirStack()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            for (int lane = 0; lane < 3; lane++) Assert.That(controller.QueueLane(lane), Is.True);
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
                AssertStackLayout(root);
                camera.orthographicSize = 2.8f;
                camera.transform.position = cameraPosition + new Vector3(4, -2, 0);
                camera.transform.rotation = Quaternion.Euler(0, 0, 17);
                player.position = playerPosition + new Vector3(3, 1.5f, 0);
                enemy.position = enemyPosition + new Vector3(-3, -1, 0);
                RefreshHud(controller, false);
                for (int index = 0; index < QueueNodes.Length; index++)
                    Assert.That(Vector2.Distance(Get<RectTransform>(root, QueueNodes[index]).anchoredPosition, positions[index]),
                        Is.GreaterThan(1f), QueueNodes[index] + " must follow the camera/actor projection instead of staying screen-fixed.");
                // At these extreme camera positions, the whole stack may clamp.
                // Its queue/status relationship must survive that boundary correction.
                AssertStackLayout(root);
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
        public IEnumerator HeadHudStacks_KeepQueuesAboveStatusThroughPlanningCombatAndNextTurn()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Transform root = controller.Hud.Root.transform;
            try
            {
                for (int lane = 0; lane < 3; lane++) Assert.That(controller.QueueLane(lane), Is.True);
                yield return null;
                AssertStackLayout(root);
                controller.CommitTurn();
                Assert.That(controller.IsResolving, Is.True);
                float deadline = Time.realtimeSinceStartup + 25f;
                while (controller.IsResolving && Time.realtimeSinceStartup < deadline)
                {
                    AssertStackLayout(root);
                    yield return null;
                }
                Assert.That(controller.IsResolving, Is.False, "The queue must finish rather than stall.");
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
                AssertStackLayout(root);
            }
            finally { controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator HeadHudStacks_FollowActorsImmediately_KeepScreenSizeWhileZooming()
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
                AssertStackLayout(root);
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
                        Is.GreaterThan(1f), "Each queue must follow its status panel immediately, even at delta zero.");
                AssertStackLayout(root);

                camera.orthographicSize = 4f;
                camera.transform.position += new Vector3(.2f, .2f, 0);
                RefreshHud(controller, false, 0f);
                AssertHeadStatus(playerStatus, player, new Vector3(-.6f, 1.1f, 0), camera, root);
                AssertHeadStatus(enemyStatus, enemy, new Vector3(-.4f, 1f, 0), camera, root);
                Assert.That(Vector2.Distance(ScreenRect(playerStatus).size, playerScreenSize), Is.LessThan(.01f));
                Assert.That(Vector2.Distance(ScreenRect(enemyStatus).size, enemyScreenSize), Is.LessThan(.01f));
                AssertQueueCardSizes(root, queueCardSizes);
                AssertStackLayout(root);
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
        public IEnumerator TabInspection_ZoomsToEnemy_AndKeepsQueueAboveConstantSizeStatus()
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
            float initialZoom = camera.orthographicSize;
            Vector2 initialStatusSize = ScreenRect(status).size;
            Vector2[] initialCardSizes = CaptureQueueCardSizes(root);
            try
            {
                Press(keyboard.tabKey);
                yield return new WaitForSecondsRealtime(.55f);
                yield return null;
                Assert.That(camera.orthographicSize, Is.LessThan(initialZoom), "Tab must still use the real enemy inspection zoom.");
                float inspectionZoom = camera.orthographicSize;
                Assert.That(Get<RectTransform>(root, "Enemy Skill Explain").gameObject.activeSelf, Is.True);
                AssertHeadStatus(status, enemy, new Vector3(-.4f, 1f, 0), camera, root);
                AssertStackLayout(root);
                Assert.That(Vector2.Distance(ScreenRect(status).size, initialStatusSize), Is.LessThan(.01f));
                AssertQueueCardSizes(root, initialCardSizes);

                Release(keyboard.tabKey);
                yield return new WaitForSecondsRealtime(.55f);
                yield return null;
                Assert.That(camera.orthographicSize, Is.GreaterThan(inspectionZoom), "Releasing Tab must restore the normal view.");
                Assert.That(Get<RectTransform>(root, "Enemy Skill Explain").gameObject.activeSelf, Is.False);
                AssertStackLayout(root);
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
            // A standalone run begins in the lobby. The newly enabled battle
            // graphics must get a render frame before testing actual raycasts.
            yield return null;
            Transform root = controller.Hud.Root.transform;
            Canvas.ForceUpdateCanvases();
            try
            {
                string[] buttons = { "LogButton", "AButton" };
                string[] resources = { "combat-log", "confirm-turn" };
                string[] keys = { "L", "Space" };
                for (int index = 0; index < buttons.Length; index++)
                {
                    Button button = Get<Button>(root, "Input/Keys/" + buttons[index]);
                    Image icon = Get<Image>(button.transform, "Icon");
                    Text label = Get<Text>(button.transform, "Label");
                    Text hint = Get<Text>(button.transform, "KeyHint");
                    Sprite source = Resources.Load<Sprite>("HudActions/" + resources[index]);
                    Assert.That(source, Is.Not.Null);
                    Assert.That(icon.sprite, Is.SameAs(source));
                    Assert.That(icon.preserveAspect, Is.True);
                    Assert.That(hint.text, Is.EqualTo(keys[index]));
                    Assert.That(hint.preferredWidth, Is.LessThanOrEqualTo(hint.rectTransform.rect.width),
                        "The complete confirmation key must fit on one line.");
                    Assert.That(label.text, Is.Not.Empty);
                    RectTransform buttonRect = button.GetComponent<RectTransform>();
                    Assert.That(buttonRect.rect.size, Is.EqualTo(new Vector2(96, 112)));
                    Assert.That(icon.rectTransform.rect.size, Is.EqualTo(new Vector2(64, 64)));
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
        public IEnumerator LongSkillQueues_WrapDownwardInsideTheScreenOnBothSides()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            RectTransform root = controller.Hud.Root.GetComponent<RectTransform>();
            Canvas.ForceUpdateCanvases();
            int columns = Mathf.Clamp(Mathf.FloorToInt((root.rect.width / 2f - 48f) / 72f), 1, 5);
            int skillCount = columns + 1;
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
                AssertStackLayout(root);
                foreach (string name in new[] { "Player Requests", "Enemy Requests" })
                {
                    var cards = new List<RectTransform>();
                    foreach (Transform child in root.Find(name))
                        if (child.gameObject.activeSelf) cards.Add(child.GetComponent<RectTransform>());
                    Assert.That(cards.Count, Is.EqualTo(skillCount));
                    Assert.That(cards[columns].anchoredPosition.x, Is.EqualTo(0f),
                        name + " must center the partial second row above its status panel.");
                    Assert.That(cards[columns].anchoredPosition.y, Is.EqualTo(cards[0].anchoredPosition.y - 72f),
                        name + " must wrap down instead of continuing outside the screen.");
                    for (int index = 0; index < cards.Count; index++)
                    {
                        Assert.That(cards[index].rect.size, Is.EqualTo(new Vector2(64, 64)));
                        AssertWithin(cards[index], root);
                        for (int previous = 0; previous < index; previous++)
                            Assert.That(ScreenRect(cards[index]).Overlaps(ScreenRect(cards[previous])), Is.False,
                                name + " wrapped cards must not overlap.");
                    }
                    if (columns > 1)
                    {
                        Assert.That(cards[0].anchoredPosition.x,
                            Is.EqualTo((name == "Player Requests" ? 1f : -1f) * (columns - 1) * 36f));
                        Assert.That(Mathf.Sign(cards[1].anchoredPosition.x - cards[0].anchoredPosition.x),
                            Is.EqualTo(name == "Player Requests" ? -1f : 1f));
                    }
                }
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

        private static void AssertStackLayout(Transform root)
        {
            RectTransform canvasRect = root.GetComponent<RectTransform>();
            int columns = Mathf.Clamp(Mathf.FloorToInt((canvasRect.rect.width / 2f - 48f) / 72f), 1, 5);
            for (int side = 0; side < QueueNodes.Length; side++)
            {
                RectTransform status = Get<RectTransform>(root, side == 0 ? "PlayerStatus" : "EnemyStatus");
                RectTransform queue = Get<RectTransform>(root, QueueNodes[side]);
                foreach (var node in new[] { status, queue })
                {
                    Assert.That(node.parent, Is.EqualTo(root));
                    Assert.That(node.anchorMin, Is.EqualTo(Vector2.one * .5f));
                    Assert.That(node.anchorMax, Is.EqualTo(Vector2.one * .5f));
                    Assert.That(node.localScale, Is.EqualTo(Vector3.one));
                }
                Assert.That(status.rect.size, Is.EqualTo(new Vector2(236, 92)));
                AssertWithin(status, canvasRect);
                Assert.That(queue.anchoredPosition.x, Is.EqualTo(status.anchoredPosition.x).Within(.01f),
                    "Queue and status must share one head attachment X.");
                var cards = VisibleQueueCards(queue);
                int rows = (cards.Count + columns - 1) / columns;
                float height = rows > 0 ? rows * 72f - 8f : 0;
                float expectedQueueOffset = 46f + 12f + Mathf.Max(64f, height) - 32f;
                Assert.That(queue.anchoredPosition.y - status.anchoredPosition.y,
                    Is.EqualTo(expectedQueueOffset).Within(.01f), "The queue's lowest row must sit immediately above the status panel.");
                for (int index = 0; index < cards.Count; index++)
                {
                    RectTransform card = cards[index];
                    int row = index / columns;
                    int rowCount = Mathf.Min(columns, cards.Count - row * columns);
                    float expectedX = (side == 0 ? -1f : 1f) * (index % columns - (rowCount - 1) * .5f) * 72f;
                    Assert.That(card.rect.size, Is.EqualTo(new Vector2(64, 64)));
                    Assert.That(Get<RectTransform>(card, "Icon").rect.size, Is.EqualTo(new Vector2(48, 48)));
                    Assert.That(card.anchoredPosition.x, Is.EqualTo(expectedX).Within(.01f));
                    Assert.That(card.anchoredPosition.y, Is.EqualTo(-row * 72f).Within(.01f));
                    Assert.That(card.localScale.x, Is.InRange(0f, 1.2001f));
                    AssertWithin(card, canvasRect);
                    Assert.That(ScreenRect(card).Overlaps(ScreenRect(status)), Is.False,
                        "Skill icons must stay above, not cover, the HP/resistance panel during focus or playback.");
                }
            }
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
