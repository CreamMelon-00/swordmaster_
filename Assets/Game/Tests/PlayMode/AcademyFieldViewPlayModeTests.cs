using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class AcademyFieldViewPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator SchoolField_ResumesAtItsPosition_AndRepositionsVisibleOpponents()
        {
            yield return null;
            var host = new GameObject("Academy Field Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            int chosenStage = 0;
            int storyOpens = 0;
            int trainingStarts = 0;
            int menusOpened = 0;
            var field = new AcademyFieldView(host.transform, art, arena,
                stage => chosenStage = stage, () => storyOpens++, () => trainingStarts++, () => menusOpened++);
            try
            {
                var campaign = new CampaignRun();
                field.Show(campaign, false, false, "스테이지를 완료하면 임무 개방");
                Assert.That(field.IsVisible, Is.True);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Corridor));
                Assert.That(field.VisibleStageCount, Is.EqualTo(2));
                Assert.That(field.PlayerPosition, Is.EqualTo(new Vector2(-29f, -1.45f)));
                Assert.That(field.FieldCamera.cullingMask, Is.EqualTo(1 << 29));
                Assert.That(field.TryInteractNearest(), Is.False);
                Transform board = Find(field.FieldCamera.transform.parent, "Story Board");
                Assert.That(board.gameObject.activeSelf, Is.True, "Locked story board should still explain the next step.");
                Transform dummy = Find(field.FieldCamera.transform.parent, "Training Dummy");
                Assert.That(dummy.gameObject.activeSelf, Is.False);

                Vector2 first = field.StagePosition(1);
                Vector2 second = field.StagePosition(2);
                Assert.That(first.x, Is.InRange(-14.55f, -9.25f));
                Assert.That(first.y, Is.InRange(-1.85f, -.55f));
                Assert.That(second.x, Is.InRange(-14.55f, -9.25f));
                Assert.That(second, Is.Not.EqualTo(first));
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.dKey);
                Advance(field, keyboard, .5f);
                ReleaseInput(keyboard.dKey);
                Vector2 savedPosition = field.PlayerPosition;
                field.Hide();
                Assert.That(field.IsVisible, Is.False);
                field.Resume();
                Assert.That(field.PlayerPosition, Is.EqualTo(savedPosition));
                Assert.That(field.StagePosition(1), Is.EqualTo(first), "ESC should keep the corridor layout.");
                field.Show(campaign, false, false);
                Assert.That(field.StagePosition(1), Is.Not.EqualTo(first), "Re-entering the field should reposition opponents.");
                Assert.That(field.PlayerPosition, Is.EqualTo(savedPosition), "Battles and story scenes return Elisa to where she stood.");
                Assert.That(chosenStage, Is.Zero);
                Assert.That(storyOpens, Is.Zero);
                Assert.That(trainingStarts, Is.Zero);
                Assert.That(menusOpened, Is.Zero);
            }
            finally
            {
                field.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator SpaceAndClickAtThePaintedDoors_TravelBothWays_AndBlockInputDuringTheFade()
        {
            yield return null;
            var host = new GameObject("Exploration Doors Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            int menusOpened = 0;
            var field = new AcademyFieldView(host.transform, art, arena,
                _ => Assert.Fail("The door must not start a stage."),
                () => Assert.Fail("The door must not open the story board."),
                () => Assert.Fail("The door must not start training."), () => menusOpened++);
            try
            {
                var campaign = new CampaignRun();
                field.Show(campaign, false, false);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                var mouse = InputSystem.AddDevice<Mouse>();
                Vector2 initialPosition = field.PlayerPosition;
                Vector2 distantDoorScreen = field.FieldCamera.WorldToScreenPoint(field.PortalPosition + Vector2.up * .6f);
                Set(mouse.position, distantDoorScreen);
                InputSystem.Update();
                Press(mouse.leftButton);
                TickInput(field, keyboard, .016f, mouse);
                ReleaseInput(mouse.leftButton);
                Assert.That(field.IsTransitioning, Is.False, "Clicking a distant door cannot teleport Elisa across the field.");
                Assert.That(field.PlayerPosition, Is.EqualTo(initialPosition));
                WalkTo(field, keyboard, field.PortalPosition);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Corridor));
                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                ReleaseInput(keyboard.spaceKey);
                Assert.That(field.IsTransitioning, Is.True);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Corridor), "The map swaps only after the fade reaches black.");

                Vector2 departingPosition = field.PlayerPosition;
                Press(keyboard.dKey);
                TickInput(field, keyboard, .1f);
                Assert.That(field.PlayerPosition, Is.EqualTo(departingPosition), "Movement is suspended during the door transition.");
                Press(keyboard.escapeKey);
                TickInput(field, keyboard, .016f);
                Assert.That(menusOpened, Is.Zero, "ESC cannot put a menu over a map transition.");
                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                Assert.That(field.TryInteractNearest(), Is.False, "Repeated interaction cannot start a second transition.");
                ReleaseInput(keyboard.dKey);
                ReleaseInput(keyboard.escapeKey);
                ReleaseInput(keyboard.spaceKey);
                Advance(field, keyboard, 1f);
                Assert.That(field.IsTransitioning, Is.False);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Forest));
                Assert.That(field.VisibleStageCount, Is.Zero, "Existing academy fights stay in the corridor.");
                Assert.That(field.PlayerPosition.x, Is.GreaterThan(field.PortalPosition.x), "Elisa emerges onto the forest path beside the school door.");

                Vector2 forestPosition = field.PlayerPosition;
                field.Hide();
                Assert.That(field.TryInteractNearest(), Is.False, "A hidden field cannot receive an interaction.");
                field.Show(campaign, false, false);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Forest));
                Assert.That(field.PlayerPosition, Is.EqualTo(forestPosition), "Returning from a scene keeps the active map and position.");
                Vector2 nearbyDoorScreen = field.FieldCamera.WorldToScreenPoint(field.PortalPosition + Vector2.up * .6f);
                Set(mouse.position, nearbyDoorScreen);
                InputSystem.Update();
                Press(mouse.leftButton);
                TickInput(field, keyboard, .016f, mouse);
                ReleaseInput(mouse.leftButton);
                Assert.That(field.IsTransitioning, Is.True, "Clicking the painted doorway beside Elisa starts the return transition.");
                Advance(field, keyboard, 1f);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Corridor));
                Assert.That(field.IsTransitioning, Is.False);
                Assert.That(field.VisibleStageCount, Is.EqualTo(2));
                Assert.That(field.PlayerPosition.x, Is.LessThan(field.PortalPosition.x), "The return entrance is on the corridor side of its door.");

                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                ReleaseInput(keyboard.spaceKey);
                Assert.That(field.IsTransitioning, Is.True);
                field.ResetExploration();
                field.Show(campaign, false, false);
                Advance(field, keyboard, 1f);
                Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Corridor));
                Assert.That(field.PlayerPosition, Is.EqualTo(new Vector2(-29f, -1.45f)), "A fresh journey starts at the academy entrance.");
                Assert.That(field.IsTransitioning, Is.False, "A reset also cancels any pending door transition.");
            }
            finally
            {
                field.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator WasdMovesElisa_AndSpaceOpensTheNearbyStoryBoard()
        {
            yield return null;
            var host = new GameObject("Academy Field Input Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            int storiesOpened = 0;
            int menusOpened = 0;
            var field = new AcademyFieldView(host.transform, art, arena,
                _ => { }, () => storiesOpened++, () => { }, () => menusOpened++);
            try
            {
                field.Show(new CampaignRun(), true, false);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Vector2 startingPoint = field.PlayerPosition;
                Press(keyboard.dKey);
                for (int step = 0; step < 10; step++) TickInput(field, keyboard, .1f);
                ReleaseInput(keyboard.dKey);
                Assert.That(field.PlayerPosition.x, Is.GreaterThan(startingPoint.x + 3f));
                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                Assert.That(storiesOpened, Is.EqualTo(1));
                ReleaseInput(keyboard.spaceKey);
                Press(keyboard.escapeKey);
                TickInput(field, keyboard, .016f);
                Assert.That(menusOpened, Is.EqualTo(1));
                ReleaseInput(keyboard.escapeKey);
            }
            finally
            {
                field.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(host);
            }
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            Assert.Fail("Missing field object: " + name);
            return null;
        }

        private void WalkTo(AcademyFieldView field, Keyboard keyboard, Vector2 destination)
        {
            // Travel through the same public input path as a player, including the camera and walk bounds.
            var vertical = destination.y > field.PlayerPosition.y ? keyboard.wKey : keyboard.sKey;
            Press(vertical);
            for (int step = 0; step < 40 && Mathf.Abs(destination.y - field.PlayerPosition.y) > .25f; step++)
                Advance(field, keyboard, .1f);
            ReleaseInput(vertical);
            Assert.That(Mathf.Abs(destination.y - field.PlayerPosition.y), Is.LessThanOrEqualTo(.25f), "The door must be reachable in the walking belt.");
            var horizontal = destination.x > field.PlayerPosition.x ? keyboard.dKey : keyboard.aKey;
            Press(horizontal);
            for (int step = 0; step < 200 && Mathf.Abs(destination.x - field.PlayerPosition.x) > .25f; step++)
                Advance(field, keyboard, .1f);
            ReleaseInput(horizontal);
            Assert.That(Mathf.Abs(destination.x - field.PlayerPosition.x), Is.LessThanOrEqualTo(.25f), "The painted doorway must be reachable before the map boundary.");
        }

        private static void Advance(AcademyFieldView field, Keyboard keyboard, float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds - .0001f; elapsed += .1f)
                TickInput(field, keyboard, Mathf.Min(.1f, seconds - elapsed));
        }

        private static void TickInput(AcademyFieldView field, Keyboard keyboard, float delta, Mouse mouse = null)
        {
            // InputTestFixture queues PlayMode UnityTest events for the player loop. These tests manually
            // advance the field, so process the queued events before reading the device state each frame.
            InputSystem.Update();
            field.Tick(delta, keyboard, mouse);
        }

        private void ReleaseInput(ButtonControl button)
        {
            Release(button);
            // Finish this state change before another event snapshots neighbouring keyboard bits.
            InputSystem.Update();
        }
    }
}

