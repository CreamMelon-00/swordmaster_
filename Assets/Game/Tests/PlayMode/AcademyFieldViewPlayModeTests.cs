using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using TurnLimbo.Runtime.Save;
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
                for (int step = 0; step < 6; step++) TickInput(field, keyboard, .1f);
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

        [UnityTest]
        public IEnumerator ShiftRunsAtOneAndAHalfWalkSpeed_WithEitherShiftKey_AndNormalizedDiagonals()
        {
            yield return null;
            var host = new GameObject("Academy Field Run Input Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            var field = new AcademyFieldView(host.transform, art, arena, _ => { }, () => { }, () => { }, () => { });
            try
            {
                field.Show(new CampaignRun(), false, false);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.dKey);
                Vector2 before = field.PlayerPosition;
                TickInput(field, keyboard, .1f);
                float walkingDistance = Vector2.Distance(before, field.PlayerPosition);
                Assert.That(walkingDistance, Is.EqualTo(.6f).Within(.001f), "The quicker base walk covers six units per second.");
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero);

                Press(keyboard.leftShiftKey);
                before = field.PlayerPosition;
                TickInput(field, keyboard, .1f);
                float runningDistance = Vector2.Distance(before, field.PlayerPosition);
                Assert.That(runningDistance / walkingDistance, Is.EqualTo(1.5f).Within(.001f));
                Assert.That(field.IsRunning, Is.True);

                ReleaseInput(keyboard.leftShiftKey);
                before = field.PlayerPosition;
                TickInput(field, keyboard, .1f);
                Assert.That(Vector2.Distance(before, field.PlayerPosition), Is.EqualTo(walkingDistance).Within(.001f),
                    "Releasing Shift restores the walking speed immediately.");
                Assert.That(field.IsRunning, Is.False);

                Press(keyboard.rightShiftKey);
                before = field.PlayerPosition;
                TickInput(field, keyboard, .1f);
                Assert.That(Vector2.Distance(before, field.PlayerPosition), Is.EqualTo(runningDistance).Within(.001f),
                    "The right Shift key also runs.");
                Assert.That(field.IsRunning, Is.True);

                Press(keyboard.wKey);
                before = field.PlayerPosition;
                TickInput(field, keyboard, .1f);
                Vector2 diagonal = field.PlayerPosition - before;
                Assert.That(diagonal.x, Is.GreaterThan(0f));
                Assert.That(diagonal.y, Is.GreaterThan(0f));
                Assert.That(diagonal.magnitude, Is.EqualTo(runningDistance).Within(.001f),
                    "Running diagonally has the same speed as running horizontally.");
                ReleaseInput(keyboard.wKey);
                ReleaseInput(keyboard.rightShiftKey);
                ReleaseInput(keyboard.dKey);
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
        public IEnumerator RunningTrail_RequiresActualMovement_AndFadesAfterStoppingOrWalking()
        {
            yield return null;
            var host = new GameObject("Academy Field Running Trail Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            var field = new AcademyFieldView(host.transform, art, arena, _ => { }, () => { }, () => { }, () => { });
            try
            {
                field.Show(new CampaignRun(), false, false);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.leftShiftKey);
                Advance(field, keyboard, .5f);
                Assert.That(field.IsRunning, Is.False, "Holding Shift while standing still is not running.");
                Assert.That(field.RunningAfterimageCount, Is.Zero);

                Press(keyboard.dKey);
                TickInput(field, keyboard, .1f);
                Assert.That(field.IsRunning, Is.True);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0), "Running leaves a visible trail.");
                ReleaseInput(keyboard.dKey);
                Advance(field, keyboard, .3f);
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero, "The brief trail fades out after stopping.");

                Press(keyboard.dKey);
                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0));
                ReleaseInput(keyboard.leftShiftKey);
                Advance(field, keyboard, .3f);
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero, "Walking does not keep emitting the trail.");

                ReleaseInput(keyboard.dKey);
                Press(keyboard.leftShiftKey);
                InputSystem.Update();
                Press(keyboard.wKey);
                for (int step = 0; step < 4; step++) TickInput(field, keyboard, .025f);
                Transform fieldRoot = field.FieldCamera.transform.parent;
                SpriteRenderer elisa = Find(fieldRoot, "Elisa").GetComponent<SpriteRenderer>();
                SpriteRenderer[] visibleTrail = Find(fieldRoot, "Duel Field Running Afterimages")
                    .GetComponentsInChildren<SpriteRenderer>();
                Assert.That(visibleTrail.Length, Is.GreaterThan(0));
                foreach (SpriteRenderer ghost in visibleTrail)
                    Assert.That(ghost.sortingOrder, Is.LessThan(elisa.sortingOrder),
                        "Older silhouettes stay behind Elisa as upward movement lowers her depth order.");
                ReleaseInput(keyboard.wKey);
                Advance(field, keyboard, .3f);
                Assert.That(field.RunningAfterimageCount, Is.Zero);

                Press(keyboard.dKey);
                Advance(field, keyboard, 15f);
                Vector2 boundaryPosition = field.PlayerPosition;
                Advance(field, keyboard, .5f);
                Assert.That(field.PlayerPosition, Is.EqualTo(boundaryPosition), "Elisa has reached the map's walking boundary.");
                Assert.That(field.IsRunning, Is.False, "Pushing into a boundary is not movement.");
                Assert.That(field.RunningAfterimageCount, Is.Zero, "No trail is emitted while standing against the boundary.");
                ReleaseInput(keyboard.dKey);
                ReleaseInput(keyboard.leftShiftKey);
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
        public IEnumerator RunningTrail_ClearsImmediatelyWhenOpeningMenus_Hiding_Resetting_OrChangingMaps()
        {
            yield return null;
            var host = new GameObject("Academy Field Running Lifecycle Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            int menusOpened = 0;
            var field = new AcademyFieldView(host.transform, art, arena, _ => { }, () => { }, () => { }, () => menusOpened++);
            try
            {
                var campaign = new CampaignRun();
                field.Show(campaign, false, false);
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.dKey);
                InputSystem.Update();
                Press(keyboard.leftShiftKey);
                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0));
                Press(keyboard.escapeKey);
                TickInput(field, keyboard, .016f);
                Assert.That(menusOpened, Is.EqualTo(1));
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero, "ESC clears the trail before the field stops ticking behind the menu.");
                ReleaseInput(keyboard.escapeKey);

                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0));
                field.Hide();
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero);
                Vector2 hiddenPosition = field.PlayerPosition;
                TickInput(field, keyboard, .1f);
                Assert.That(field.PlayerPosition, Is.EqualTo(hiddenPosition), "Hidden fields cannot move or emit trails.");
                Assert.That(field.RunningAfterimageCount, Is.Zero);

                field.Show(campaign, false, false);
                Assert.That(field.RunningAfterimageCount, Is.Zero);
                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0));
                field.Show(campaign, false, false);
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero, "Re-entering the field cannot revive an old trail.");
                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0));
                field.ResetExploration();
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero);
                ReleaseInput(keyboard.dKey);
                ReleaseInput(keyboard.leftShiftKey);

                field.Show(campaign, false, false);
                WalkTo(field, keyboard, field.PortalPosition);
                Press(keyboard.aKey);
                InputSystem.Update();
                Press(keyboard.rightShiftKey);
                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.GreaterThan(0));
                ReleaseInput(keyboard.aKey);
                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                Assert.That(field.IsTransitioning, Is.True);
                Assert.That(field.IsRunning, Is.False);
                Assert.That(field.RunningAfterimageCount, Is.Zero, "The fade starts without ghosts from the departing map.");
                ReleaseInput(keyboard.spaceKey);
                Press(keyboard.dKey);
                TickInput(field, keyboard, .1f);
                Assert.That(field.RunningAfterimageCount, Is.Zero, "Holding run during a transition emits no ghosts.");
                ReleaseInput(keyboard.dKey);
                ReleaseInput(keyboard.rightShiftKey);
            }
            finally
            {
                field.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(host);
            }
        }

        // TEMPORARY field pickup test (Docs/FieldPickups.md).
        [UnityTest]
        public IEnumerator ForestPickups_FloatWithTheirIcons_PromptNearby_TakeOnce_AndStayGoneAfterReload()
        {
            yield return null;
            var host = new GameObject("Academy Field Pickup Test");
            var art = new LegacyDuelArt();
            LegacyArenaView arena = LegacyArenaView.Create(host.transform, art);
            var campaign = new CampaignRun();
            var taken = new List<string>();
            // Takes the pickup through the run like the controller's CollectFieldPickup.
            var field = new AcademyFieldView(host.transform, art, arena,
                _ => Assert.Fail("A pickup must not start a stage."),
                () => Assert.Fail("A pickup must not open the story board."),
                () => Assert.Fail("A pickup must not start training."), () => { },
                id => { taken.Add(id); campaign.TryCollectFieldPickup(id); });
            try
            {
                field.Show(campaign, false, false);
                Assert.That(field.VisiblePickupCount, Is.Zero, "The pickups lie in the forest, not in the corridor.");
                var keyboard = InputSystem.AddDevice<Keyboard>();
                EnterForest(field, keyboard);

                IReadOnlyList<CampaignFieldPickup> forest = CampaignFieldPickups.In(CampaignFieldArea.Forest);
                Assert.That(forest.Count, Is.EqualTo(4));
                Assert.That(field.VisiblePickupCount, Is.EqualTo(forest.Count));
                Transform fieldRoot = field.FieldCamera.transform.parent;
                foreach (CampaignFieldPickup each in forest)
                {
                    Assert.That(field.IsPickupVisible(each.Id), Is.True, each.Id);
                    Assert.That(field.PickupPosition(each.Id), Is.EqualTo(new Vector2(each.X, each.Y)), each.Id);
                    Assert.That(Vector2.Distance(field.PickupPosition(each.Id), field.PlayerPosition), Is.GreaterThan(2.8f),
                        "No pickup lies within reach of the door Elisa arrives at.");
                    SpriteRenderer eachIcon = PickupPart(fieldRoot, each.Id, "Pickup Icon");
                    Assert.That(eachIcon.sprite, Is.Not.Null.And.SameAs(art.GetSkillIcon(LegacySkillDefinitions.Skill(each.SkillId).IconId)),
                        "Each pickup shows its technique's skill icon.");
                }

                CampaignFieldPickup pierce = CampaignFieldPickups.Find("forest-pierce");
                LegacySkill skill = LegacySkillDefinitions.Skill(pierce.SkillId);
                SpriteRenderer icon = PickupPart(fieldRoot, pierce.Id, "Pickup Icon");
                SpriteRenderer halo = PickupPart(fieldRoot, pierce.Id, "Pickup Halo");
                Assert.That(halo.sortingOrder, Is.EqualTo(icon.sortingOrder - 1), "The icon draws over its own halo.");
                Assert.That(field.InteractionPrompt, Does.Not.Contain(skill.Name), "Far from it, the pickup asks nothing.");
                float lowest = float.MaxValue, highest = float.MinValue;
                for (int frame = 0; frame < 15; frame++)
                {
                    TickInput(field, keyboard, .1f);
                    lowest = Mathf.Min(lowest, icon.transform.localPosition.y);
                    highest = Mathf.Max(highest, icon.transform.localPosition.y);
                }
                Assert.That(highest - lowest, Is.GreaterThan(.05f), "The icon bobs.");
                Assert.That(lowest, Is.GreaterThan(MobStudentAnimationSet.GroundOffset + .6f), "…floating above the ground line.");
                Assert.That(halo.color.a, Is.GreaterThan(0f), "…in a visible halo.");

                int equipped = campaign.EquippedSkillCount;
                WalkTo(field, keyboard, field.PickupPosition(pierce.Id));
                Assert.That(field.InteractionPrompt, Is.EqualTo("Space · " + skill.Name + " 습득"),
                    "Beside a pickup the field's own interaction prompt offers it.");
                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                ReleaseInput(keyboard.spaceKey);
                Assert.That(taken, Is.EqualTo(new[] { pierce.Id }));
                Assert.That(campaign.GetOwnedSkill(pierce.SkillId), Is.Not.Null, "Taking it grants its technique.");
                Assert.That(campaign.EquippedSkillCount, Is.EqualTo(equipped), "…without equipping it.");
                Assert.That(field.IsPickupVisible(pierce.Id), Is.False, "A taken pickup leaves the field.");
                Assert.That(field.VisiblePickupCount, Is.EqualTo(forest.Count - 1));
                Assert.That(field.InteractionPrompt, Does.Not.Contain(skill.Name));

                Press(keyboard.spaceKey);
                TickInput(field, keyboard, .016f);
                ReleaseInput(keyboard.spaceKey);
                Assert.That(field.TryInteractNearest(), Is.False);
                Assert.That(taken.Count, Is.EqualTo(1), "A taken pickup cannot be taken again.");
                Assert.That(campaign.OwnedSkills.Count(owned => owned.SkillId == pierce.SkillId), Is.EqualTo(1));

                // Reload: through the save text, into a fresh run, and a fresh walk from the academy entrance.
                string saved = GameSaveCodec.Serialize(GameSave.Capture(new PrologueRun(), campaign));
                Assert.That(GameSaveCodec.TryParse(saved, out GameSave loaded, out string error), Is.True, error);
                var reloaded = new CampaignRun();
                Assert.That(reloaded.TryRestore(loaded.Campaign, out error), Is.True, error);
                campaign = reloaded;
                field.ResetExploration();
                field.Show(reloaded, false, false);
                EnterForest(field, keyboard);
                Assert.That(field.IsPickupVisible(pierce.Id), Is.False, "A taken pickup stays gone after loading the save.");
                Assert.That(field.VisiblePickupCount, Is.EqualTo(forest.Count - 1), "The others still lie there.");
                Assert.That(reloaded.GetOwnedSkill(pierce.SkillId), Is.Not.Null);
                WalkTo(field, keyboard, field.PickupPosition(pierce.Id));
                Assert.That(field.InteractionPrompt, Does.Not.Contain(skill.Name));
                Assert.That(field.TryInteractNearest(), Is.False);
                Assert.That(taken.Count, Is.EqualTo(1));
            }
            finally
            {
                field.Dispose();
                arena.Dispose();
                art.Dispose();
                Object.Destroy(host);
            }
        }

        // TEMPORARY field pickup test (Docs/FieldPickups.md).
        [Test]
        public void PickupNotice_NamesTheLaneOnlyWhileItIsOpen()
        {
            LegacySkill pierce = LegacySkillDefinitions.Skill(CampaignFieldPickups.Find("forest-pierce").SkillId);
            Assert.That(pierce.LaneIndex, Is.EqualTo(1));
            Assert.That(AcademyFieldView.PickupNotice(pierce, true, true),
                Is.EqualTo($"새 기술 · {pierce.Name}(W 강공)\nEsc 메뉴의 편성에서 장착할 수 있습니다."));
            string closed = AcademyFieldView.PickupNotice(pierce, true, false);
            Assert.That(closed, Is.EqualTo($"새 기술 · {pierce.Name}\n이 기술을 쓰는 열을 해금하면 편성할 수 있습니다."));
            Assert.That(closed, Does.Not.Contain("W").And.Not.Contain("강공"), "A closed lane is never named.");
            Assert.That(AcademyFieldView.PickupNotice(pierce, false, true), Is.EqualTo($"{pierce.Name} · 이미 익힌 기술입니다."));
        }

        private void EnterForest(AcademyFieldView field, Keyboard keyboard)
        {
            WalkTo(field, keyboard, field.PortalPosition);
            Press(keyboard.spaceKey);
            TickInput(field, keyboard, .016f);
            ReleaseInput(keyboard.spaceKey);
            Advance(field, keyboard, 1f);
            Assert.That(field.CurrentMap, Is.EqualTo(ExplorationFieldMap.Forest));
        }

        private static SpriteRenderer PickupPart(Transform fieldRoot, string pickupId, string part)
            => Find(Find(fieldRoot, "Field Pickup " + pickupId), part).GetComponent<SpriteRenderer>();

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
            for (int step = 0; step < 100 && Mathf.Abs(destination.y - field.PlayerPosition.y) > .25f; step++)
                TickInput(field, keyboard, .04f);
            ReleaseInput(vertical);
            Assert.That(Mathf.Abs(destination.y - field.PlayerPosition.y), Is.LessThanOrEqualTo(.25f), "The door must be reachable in the walking belt.");
            var horizontal = destination.x > field.PlayerPosition.x ? keyboard.dKey : keyboard.aKey;
            Press(horizontal);
            for (int step = 0; step < 400 && Mathf.Abs(destination.x - field.PlayerPosition.x) > .25f; step++)
                TickInput(field, keyboard, .04f);
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

