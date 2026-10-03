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
    public sealed class CompactHudPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator ButtonSkillSprites_AllSeventeenLoadCenteredDistinctAndMatchArtLookup()
        {
            yield return null;
            var art = new LegacyDuelArt();
            var sprites = new HashSet<Sprite>();
            var imported = Resources.LoadAll<Sprite>("SkillRoles/skill-role-atlas");
            Assert.That(imported.Length, Is.EqualTo(15), "The original atlas remains unchanged.");
            for (int skillId = 1; skillId <= LegacyDuelArt.SkillIconCount; skillId++)
            {
                Sprite source = art.GetSkillIcon(skillId);
                Assert.That(source, Is.Not.Null, "Role icon " + skillId + " must import.");
                if (skillId <= 15)
                    CollectionAssert.Contains(imported, source);
                else
                    Assert.That(source, Is.SameAs(Resources.Load<Sprite>("SkillRoles/role" + skillId)));
                Assert.That(sprites.Add(source), Is.True, "Each skill must have its own Sprite.");
                Assert.That(source.name, Is.EqualTo("role" + skillId));
                Assert.That(source.rect.width, Is.GreaterThanOrEqualTo(128));
                Assert.That(source.rect.height, Is.GreaterThanOrEqualTo(128));
                Assert.That(source.rect.width / source.rect.height, Is.InRange(.8f, 1.2f), "Tight role crop must preserve the full glyph without another row's fragments.");
                Assert.That(source.pivot.x, Is.EqualTo(source.rect.width / 2f).Within(.01f));
                Assert.That(source.pivot.y, Is.EqualTo(source.rect.height / 2f).Within(.01f));
                Assert.That(source.texture.mipmapCount, Is.EqualTo(1));
                Assert.That(source.texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(source.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(art.GetSkillIcon(skillId), Is.SameAs(source));
            }
            // -1 is the separate breathing action's generated icon.
            foreach (int invalidId in new[] { 0, 18, int.MaxValue })
                Assert.That(art.GetSkillIcon(invalidId), Is.Null);
        }

        [UnityTest]
        public IEnumerator ButtonSkillSprites_CurrentPreviewQueuesAndResolvedLogUseTheNewSet()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Transform root = controller.Hud.Root.transform;
            try
            {
                AssertLane(root, 0, 1, 2, 1);
                AssertLane(root, 1, 3, 4, 2);
                AssertLane(root, 2, 5, 6, 1);
                AssertIconColumn(root.Find("Enemy Requests"), new[] { 1, 2 });
                yield return AdvanceEmptyTurnToSixAct(controller);
                AssertIconColumn(root.Find("Enemy Requests"), new[] { 3, 4, 5 });
                for (int lane = 0; lane < 3; lane++)
                    Assert.That(controller.QueueLane(lane), Is.True);
                yield return null;
                AssertLane(root, 0, 2, 7, 1);
                AssertLane(root, 1, 4, 8, 3);
                AssertLane(root, 2, 6, 9, 2);
                AssertIconColumn(root.Find("Player Requests"), new[] { 1, 3, 5 });

                controller.CommitTurn();
                float deadline = Time.realtimeSinceStartup + 25f;
                while (controller.IsResolving && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(controller.IsResolving, Is.False, "The queue must finish before opening its log.");
                Assert.That(controller.CanChoose, Is.True);
                Assert.That(controller.Hud.LogCount, Is.EqualTo(5));
                Get<Button>(root, "Input/Keys/LogButton").onClick.Invoke();
                Assert.That(controller.Hud.LogOpen, Is.True);
                const string logPath = "Log View/Log Scroll View/Viewport/Content/";
                AssertIconColumn(root.Find(logPath + "Player"), new[] { 0, 0, 1, 3, 5 });
                AssertIconColumn(root.Find(logPath + "Enemy"), new[] { 1, 2, 3, 4, 5 });
                Get<Button>(root, "Log View/Close Log").onClick.Invoke();
                Assert.That(controller.Hud.LogOpen, Is.False);
            }
            finally { controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator CompactCanvas_UsesBalancedScalingAndOneAlignedActTrack()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Canvas.ForceUpdateCanvases();
            Transform root = controller.Hud.Root.transform;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(.5f));
            RectTransform input = Get<RectTransform>(root, "Input");
            Assert.That(input.rect.width, Is.InRange(900f, 1050f),
                "The battle commands occupy a compact center dock instead of a screen-wide footer.");
            Assert.That(input.rect.height, Is.LessThanOrEqualTo(200f));
            Rect dockBounds = ScreenRect(input);
            RectTransform log = Get<RectTransform>(root, "Input/Keys/LogButton");
            RectTransform commit = Get<RectTransform>(root, "Input/Keys/AButton");
            Assert.That(ScreenRect(log).xMax, Is.LessThan(dockBounds.xMin), "The log command flanks the dock.");
            Assert.That(ScreenRect(commit).xMin, Is.GreaterThan(dockBounds.xMax), "The commit command flanks the dock.");
            AssertOnScreen(log);
            AssertOnScreen(commit);

            Image fill = Get<Image>(root, "Input/Keys/Act_Gauge");
            Image track = Get<Image>(root, "Input/Keys/Act_BG");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(fill.rectTransform.rect.size, Is.EqualTo(new Vector2(520, 8)));
            Assert.That(track.rectTransform.rect.size, Is.EqualTo(fill.rectTransform.rect.size));
            Assert.That(track.rectTransform.anchorMin, Is.EqualTo(fill.rectTransform.anchorMin));
            Assert.That(track.rectTransform.anchorMax, Is.EqualTo(fill.rectTransform.anchorMax));
            Assert.That(track.rectTransform.pivot, Is.EqualTo(fill.rectTransform.pivot));
            Assert.That(track.rectTransform.anchoredPosition, Is.EqualTo(fill.rectTransform.anchoredPosition));
            Assert.That(fill.rectTransform.anchorMin.x, Is.EqualTo(.5f));
            Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(.5f));
            Assert.That(fill.rectTransform.anchoredPosition.x, Is.Zero);
            AssertAct(root, 3);
        }

        [UnityTest]
        public IEnumerator LaneButtons_QueueSpendActRotateIconsRejectUnaffordableClickAndReset()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Transform root = controller.Hud.Root.transform;
            AssertLane(root, 0, 1, 2, 1);
            AssertLane(root, 1, 3, 4, 2);
            AssertLane(root, 2, 5, 6, 1);
            AssertAct(root, 3);
            yield return AdvanceEmptyTurnToSixAct(controller);
            int playerHealth = controller.PlayerHealth, enemyHealth = controller.EnemyHealth;

            for (int lane = 0; lane < 3; lane++)
            {
                Button button = LaneButton(root, lane);
                Assert.That(button.interactable, Is.True);
                button.onClick.Invoke();
                yield return null;
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(lane + 1));
                AssertAct(root, lane == 0 ? 5 : lane == 1 ? 3 : 2);
                Assert.That(controller.PlayerHealth, Is.EqualTo(playerHealth));
                Assert.That(controller.EnemyHealth, Is.EqualTo(enemyHealth));
            }
            AssertLane(root, 0, 2, 7, 1);
            AssertLane(root, 1, 4, 8, 3);
            AssertLane(root, 2, 6, 9, 2);
            Assert.That(controller.CanChoose, Is.True);
            Button unaffordable = LaneButton(root, 1);
            Assert.That(unaffordable.interactable, Is.False, "The next W skill costs more than the remaining ACT.");
            unaffordable.onClick.Invoke();
            Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(3));
            Assert.That(LaneButton(root, 2).interactable, Is.True);
            LaneButton(root, 2).onClick.Invoke();
            yield return null;
            AssertAct(root, 0);
            AssertLane(root, 2, 9, 5, 1);
            Assert.That(controller.CanChoose, Is.True, "ACT exhaustion leaves the turn in planning.");
            for (int lane = 0; lane < 3; lane++)
            {
                Button button = LaneButton(root, lane);
                Assert.That(button.interactable, Is.False);
                // Unity's event can also be invoked directly; the session must
                // reject the unavailable action independently of visual state.
                button.onClick.Invoke();
            }
            Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(4));
            Assert.That(controller.Session.Act, Is.Zero);
            AssertLane(root, 0, 2, 7, 1);
            controller.RestartMatch();
            AssertAct(root, 3);
            Assert.That(controller.Session.PlayerQueue, Is.Empty);
            AssertLane(root, 0, 1, 2, 1);
            AssertLane(root, 1, 3, 4, 2);
            AssertLane(root, 2, 5, 6, 1);
            for (int lane = 0; lane < 3; lane++) Assert.That(LaneButton(root, lane).interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator CompactFooter_KeepsIconsAndCostsSeparateAndInsidePanel()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Canvas.ForceUpdateCanvases();
            Transform root = controller.Hud.Root.transform;
            RectTransform panel = Get<RectTransform>(root, "Input");
            for (int lane = 0; lane < 3; lane++)
            {
                string letter = "QWE"[lane].ToString();
                RectTransform current = Get<RectTransform>(root, "Input/Keys/Current " + letter + "/Skill Image");
                RectTransform next = Get<RectTransform>(root, "Input/Keys/Next " + letter + "/Next Skill Image");
                RectTransform cost = Get<RectTransform>(root, "Input/Keys/Current " + letter + "/SkillCost");
                AssertWithin(current, panel);
                AssertWithin(next, panel);
                AssertWithin(cost, panel);
                Assert.That(ScreenRect(current).Overlaps(ScreenRect(cost)), Is.False, letter + " cost must not cover its skill image.");
                Assert.That(ScreenRect(next).Overlaps(ScreenRect(cost)), Is.False, letter + " cost must not cover the preview image.");
                Assert.That(ScreenRect(cost).Overlaps(ScreenRect(Get<RectTransform>(root, "Input/Keys/Act_Value"))), Is.False,
                    letter + " cost must not cover the ACT value.");
                bool hasKeyLabel = false;
                foreach (Text label in LaneButton(root, lane).GetComponentsInChildren<Text>(true))
                    if (label.text == letter) hasKeyLabel = true;
                Assert.That(hasKeyLabel, Is.True, letter + " must remain visible as text.");
            }
            AssertWithin(Get<RectTransform>(root, "Input/Keys/Act_Gauge"), panel);
            AssertWithin(Get<RectTransform>(root, "Input/Keys/Act_Value"), panel);
        }

        [UnityTest]
        public IEnumerator PointerRaycasts_ReachEveryLaneAndCommitWithoutDecorationsBlockingThem()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            Canvas.ForceUpdateCanvases();
            var root = controller.Hud.Root.transform;
            try
            {
                yield return AdvanceEmptyTurnToSixAct(controller);
                for (int lane = 0; lane < 3; lane++)
                {
                    var button = LaneButton(root, lane);
                    ClickThroughRaycast(button);
                    Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(lane + 1));
                    Assert.That(controller.Session.Act, Is.EqualTo(lane == 0 ? 5 : lane == 1 ? 3 : 2));
                }
                ClickThroughRaycast(Get<Button>(root, "Input/Keys/AButton"));
                Assert.That(controller.IsResolving, Is.True);
                Assert.That(controller.Session.PlayerQueue.Count, Is.EqualTo(3));
            }
            finally { controller.RestartMatch(); }
        }

        private static void ClickThroughRaycast(Button button)
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject, Is.EqualTo(button.gameObject), "A decorative image must not intercept the click.");
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator CompactStatusAndQueues_UseFlatBarsSmallIconsAndOppositeGrowthDirections()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Transform root = controller.Hud.Root.transform;
            foreach (string statusName in new[] { "PlayerStatus", "EnemyStatus" })
            {
                Transform status = root.Find(statusName);
                Assert.That(status, Is.Not.Null);
                Assert.That(status.localScale, Is.EqualTo(Vector3.one));
                AssertBar(status, "HP", 10);
                AssertBar(status, "HP delayed", 10);
                AssertBar(status, "Resistance", 6);
                AssertBar(status, "Resistance delayed", 6);
            }
            yield return AdvanceEmptyTurnToSixAct(controller);
            Assert.That(controller.QueueLane(0), Is.True);
            Assert.That(controller.QueueLane(1), Is.True);
            Assert.That(controller.QueueLane(2), Is.True);
            yield return null;
            // The rows grow outward: the player's to the left, the enemy's to the right.
            AssertQueue(root.Find("Player Requests"), 3, true);
            AssertQueue(root.Find("Enemy Requests"), 3, false);
        }

        [UnityTest]
        public IEnumerator HeldSkillExplanation_StaysOnScreenAtEveryScreenEdgeAndDoesNotQueue()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            Transform root = controller.Hud.Root.transform;
            RectTransform current = Get<RectTransform>(root, "Input/Keys/Current Q");
            Vector3 originalPosition = current.position;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Vector2[] edges =
            {
                new Vector2(4, 4), new Vector2(Screen.width - 4, 4),
                new Vector2(4, Screen.height - 4), new Vector2(Screen.width - 4, Screen.height - 4),
            };
            Assert.That(root.Find("Skill Explain").gameObject.activeSelf, Is.False);
            Assert.That(root.Find("Enemy Skill Explain").gameObject.activeSelf, Is.False);
            try
            {
                foreach (Vector2 position in edges)
                {
                    current.position = new Vector3(position.x, position.y, originalPosition.z);
                    Press(keyboard.qKey);
                    yield return new WaitForSecondsRealtime(1.1f);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    RectTransform explanation = Get<RectTransform>(root, "Skill Explain");
                    Assert.That(explanation.gameObject.activeSelf, Is.True, "A hold should reveal the selected technique.");
                    AssertOnScreen(explanation);
                    Assert.That(controller.Session.PlayerQueue, Is.Empty);
                    Assert.That(controller.Session.Act, Is.EqualTo(3));
                    Release(keyboard.qKey);
                    yield return null;
                    yield return null;
                }
                Press(keyboard.tabKey);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform enemyExplanation = Get<RectTransform>(root, "Enemy Skill Explain");
                Assert.That(enemyExplanation.gameObject.activeSelf, Is.True);
                AssertOnScreen(enemyExplanation);
                Assert.That(controller.Hud.LogOpen, Is.False);
            }
            finally
            {
                Release(keyboard.qKey);
                Release(keyboard.tabKey);
                current.position = originalPosition;
                controller.RestartMatch();
            }
        }

        private static IEnumerator AdvanceEmptyTurnToSixAct(DuelPrototypeController controller)
        {
            Assert.That(controller.Session.PlayerQueue, Is.Empty);
            controller.CommitTurn();
            float deadline = Time.realtimeSinceStartup + 25f;
            while (controller.IsResolving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsResolving, Is.False, "The empty funding turn must finish.");
            Assert.That(controller.CanChoose, Is.True);
            Assert.That(controller.Session.RoundNumber, Is.EqualTo(2));
            Assert.That(controller.Session.Act, Is.EqualTo(6));
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
            Assert.That(child, Is.Not.Null, "Missing compact HUD node: " + path);
            T component = child.GetComponent<T>();
            Assert.That(component, Is.Not.Null, "Missing " + typeof(T).Name + " at " + path);
            return component;
        }

        private static Button LaneButton(Transform root, int lane)
            => Get<Button>(root, "Input/Keys/Current " + "QWE"[lane]);

        private static void AssertAct(Transform root, int act)
        {
            Assert.That(Get<Image>(root, "Input/Keys/Act_Gauge").fillAmount, Is.EqualTo(act / 10f).Within(.0001f));
            Assert.That(Get<Text>(root, "Input/Keys/Act_Value").text, Is.EqualTo(act + " / 10 ACT"));
        }

        private static void AssertLane(Transform root, int lane, int currentId, int nextId, int cost)
        {
            string letter = "QWE"[lane].ToString();
            AssertIcon(Get<Image>(root, "Input/Keys/Current " + letter + "/Skill Image"), currentId);
            AssertIcon(Get<Image>(root, "Input/Keys/Next " + letter + "/Next Skill Image"), nextId);
            Assert.That(Get<Text>(root, "Input/Keys/Current " + letter + "/SkillCost").text, Is.EqualTo(cost + " ACT"));
        }

        private static void AssertIcon(Image image, int skillId)
        {
            Sprite source = new LegacyDuelArt().GetSkillIcon(LegacySkillDefinitions.Skill(skillId).IconId);
            Assert.That(source, Is.Not.Null);
            Assert.That(image.sprite, Is.Not.Null);
            // Every consumer must retain the matching newly drawn button art.
            Assert.That(image.sprite.texture, Is.EqualTo(source.texture));
            Assert.That(image.sprite.name, Is.EqualTo(source.name));
        }

        private static void AssertIconColumn(Transform column, int[] skillIds)
        {
            Assert.That(column, Is.Not.Null);
            var visible = new List<Transform>();
            foreach (Transform child in column)
                if (child.gameObject.activeSelf) visible.Add(child);
            Assert.That(visible.Count, Is.EqualTo(skillIds.Length));
            for (int index = 0; index < skillIds.Length; index++)
            {
                Image icon = Get<Image>(visible[index], "Icon");
                if (skillIds[index] == 0)
                {
                    Assert.That(icon.enabled, Is.False, "An unmatched log slot must not invent a skill icon.");
                    Assert.That(icon.sprite, Is.Null);
                }
                else AssertIcon(icon, skillIds[index]);
            }
        }

        private static void AssertBar(Transform status, string name, float height)
        {
            Image bar = Get<Image>(status, name);
            Assert.That(bar.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(bar.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(bar.fillAmount, Is.EqualTo(1));
            Assert.That(bar.rectTransform.rect.size, Is.EqualTo(new Vector2(196, height)));
            Assert.That(bar.GetComponent<Mask>(), Is.Null);
            Assert.That(bar.transform.localScale, Is.EqualTo(Vector3.one));
        }

        private static void AssertQueue(Transform queue, int expectedCount, bool growsLeft)
        {
            Assert.That(queue, Is.Not.Null);
            var visible = new List<RectTransform>();
            foreach (Transform child in queue)
                if (child.gameObject.activeSelf) visible.Add(child.GetComponent<RectTransform>());
            Assert.That(visible.Count, Is.EqualTo(expectedCount));
            for (int index = 0; index < visible.Count; index++)
            {
                Assert.That(visible[index].rect.size, Is.EqualTo(new Vector2(64, 64)));
                Assert.That(Get<RectTransform>(visible[index], "Icon").rect.size, Is.EqualTo(new Vector2(48, 48)));
                if (index == 0) continue;
                if (growsLeft) Assert.That(visible[index].anchoredPosition.x, Is.LessThan(visible[index - 1].anchoredPosition.x));
                else Assert.That(visible[index].anchoredPosition.x, Is.GreaterThan(visible[index - 1].anchoredPosition.x));
            }
        }

        private static Rect ScreenRect(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
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
            Assert.That(inside.xMin, Is.GreaterThanOrEqualTo(outside.xMin - 1), child.name + " crosses the panel's left edge.");
            Assert.That(inside.xMax, Is.LessThanOrEqualTo(outside.xMax + 1), child.name + " crosses the panel's right edge.");
            Assert.That(inside.yMin, Is.GreaterThanOrEqualTo(outside.yMin - 1), child.name + " crosses the panel's bottom edge.");
            Assert.That(inside.yMax, Is.LessThanOrEqualTo(outside.yMax + 1), child.name + " crosses the panel's top edge.");
        }

        private static void AssertOnScreen(RectTransform panel)
        {
            Rect bounds = ScreenRect(panel);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(-1));
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Screen.width + 1));
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(-1));
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Screen.height + 1));
        }
    }
}
