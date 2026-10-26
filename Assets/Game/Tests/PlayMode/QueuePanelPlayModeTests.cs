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
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The two facing queue panels over the command dock (2026-10-15, <c>LegacyCombatHud.QueuePanels.cs</c>,
    /// <see cref="LegacyQueuePanelLayout"/>): after each resolved slot both rows slide one step toward the centre seam on the
    /// battle's clock and come to rest, without ever holding the resolution; a reset and a new turn put them at rest; the
    /// coach's card opens the seam so it covers no card; and the arena keeps the fighters' feet over the panels in the
    /// corridor's framing while the forest already clears them and Tab keeps the same planning view. The head-queue layout tests became the panel
    /// tests in <see cref="FixedHudPlayModeTests"/>.</summary>
    public sealed class QueuePanelPlayModeTests : InputTestFixture
    {
        private const float Frame = 1f / 60f;

        [UnityTest]
        public IEnumerator ThirtyReservations_UseOneMirroredWindow_ThatFollowsTabAndTheResolvingSlot()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            LegacyCombatHud hud = controller.Hud;
            LegacyQueuedDuel duel = ThreeSlotDuel(30);
            try
            {
                Refresh(controller, duel, false, -1, 0f);
                Assert.That(hud.QueueWindowCapacity, Is.InRange(2, 29));
                Assert.That(hud.QueueWindowStart, Is.EqualTo(30 - hud.QueueWindowCapacity), "New reservations stay visible.");
                Assert.That(hud.GetQueuedSkillAnchor(true, 29), Is.Not.Null);
                Assert.That(hud.GetQueuedSkillAnchor(false, 29), Is.Not.Null);
                Assert.That(hud.GetQueuedSkillAnchor(true, 0), Is.Null, "The overflow is hidden, never piled over other cards.");
                foreach (int slot in new[] { 0, 17, 29 })
                {
                    hud.SetInspectedSlot(slot);
                    Refresh(controller, duel, false, -1, 0f);
                    Assert.That(hud.GetQueuedSkillAnchor(true, slot), Is.Not.Null);
                    Assert.That(hud.GetQueuedSkillAnchor(false, slot), Is.Not.Null);
                    AssertMirror(Cards(hud, "Player Requests"), Cards(hud, "Enemy Requests"));
                }
                hud.SetInspectedSlot(-1);
                Refresh(controller, duel, true, 17, 0f);
                Refresh(controller, duel, true, 17, controller.PresentationSettings.QueueSlideSeconds + .01f);
                Assert.That(hud.QueueWindowStart, Is.EqualTo(17));
                RectTransform mine = hud.GetQueuedSkillAnchor(true, 17), theirs = hud.GetQueuedSkillAnchor(false, 17);
                Assert.That(mine, Is.Not.Null);
                Assert.That(theirs, Is.Not.Null);
                Assert.That(mine.anchoredPosition.x, Is.EqualTo(-theirs.anchoredPosition.x).Within(.01f));
                Assert.That(hud.GetQueuedSkillAnchor(true, 16), Is.Null, "Consumed slots never reappear.");

                // A coach-like central reserve leaves the panels against the viewport edges. Even a two-slot slide
                // must keep its complete visible cards on screen, while retaining the active pair and its mirror.
                RectTransform root = (RectTransform)hud.Root.transform;
                hud.SetQueueSeamReserve(root.rect.width * .5f - 220f);
                Refresh(controller, duel, true, 19, 0f);
                Assert.That(hud.QueueSlide, Is.EqualTo(LegacyQueuePanelLayout.MaximumSlide).Within(.001f));
                Assert.That(hud.GetQueuedSkillAnchor(true, 19), Is.Not.Null);
                Assert.That(hud.GetQueuedSkillAnchor(false, 19), Is.Not.Null);
                foreach (string row in new[] { "Player Requests", "Enemy Requests" })
                    foreach (RectTransform card in Cards(hud, row))
                    {
                        Rect viewport = ScreenRect(root), bounds = ScreenRect(card);
                        Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(viewport.xMin));
                        Assert.That(bounds.xMax, Is.LessThanOrEqualTo(viewport.xMax));
                    }
                AssertMirror(Cards(hud, "Player Requests"), Cards(hud, "Enemy Requests"));
            }
            finally { hud.SetInspectedSlot(-1); hud.SetQueueSeamReserve(0f); controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator AfterEachResolvedSlot_BothRowsSlideOneStepTowardTheSeam_AndComeToRest()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            LegacyCombatHud hud = controller.Hud;
            float seconds = controller.PresentationSettings.QueueSlideSeconds;
            Assume.That(seconds, Is.GreaterThan(.05f));
            LegacyQueuedDuel duel = ThreeSlotDuel();
            try
            {
                Refresh(controller, duel, true, 0, .1f);
                List<RectTransform> player = Cards(hud, "Player Requests"), enemy = Cards(hud, "Enemy Requests");
                Assert.That(player.Count, Is.EqualTo(3));
                Assert.That(enemy.Count, Is.EqualTo(3));
                float inner = -player[0].anchoredPosition.x, spacing = hud.QueueSpacing;
                Assert.That(inner, Is.GreaterThan(0f), "Slot 1 is the player's innermost card, by the seam.");
                Assert.That(-player[1].anchoredPosition.x, Is.EqualTo(inner + spacing).Within(.01f));
                Assert.That(hud.QueueSlide, Is.Zero);

                // Slot 1 is resolved: its cards go at once, and the next pair starts where it stood…
                Refresh(controller, duel, true, 1, 0f);
                player = Cards(hud, "Player Requests");
                enemy = Cards(hud, "Enemy Requests");
                Assert.That(player.Count, Is.EqualTo(2), "The resolved slot's card is gone…");
                Assert.That(hud.QueueSlide, Is.EqualTo(1f).Within(.001f));
                Assert.That(-player[0].anchoredPosition.x, Is.EqualTo(inner + spacing).Within(.01f), "…the next starts one step out…");
                AssertMirror(player, enemy);
                // …slides in on the battle's clock (it stands still while the battle does)…
                Refresh(controller, duel, true, 1, 0f);
                Assert.That(hud.QueueSlide, Is.EqualTo(1f).Within(.001f), "A stopped battle clock holds the slide.");
                Refresh(controller, duel, true, 1, seconds * .5f);
                float halfway = -Cards(hud, "Player Requests")[0].anchoredPosition.x;
                Assert.That(halfway, Is.InRange(inner + .01f, inner + spacing - .01f), "…moves toward the seam…");
                AssertMirror(Cards(hud, "Player Requests"), Cards(hud, "Enemy Requests"));
                Refresh(controller, duel, true, 1, seconds);
                Assert.That(hud.QueueSlide, Is.Zero);
                Assert.That(-Cards(hud, "Player Requests")[0].anchoredPosition.x, Is.EqualTo(inner).Within(.01f),
                    "…and the pair being resolved is the innermost again.");

                // Two slots at once (a skipped stretch) never carry the rows further than the cap.
                Refresh(controller, duel, true, 3, 0f);
                Assert.That(hud.QueueSlide, Is.EqualTo(LegacyQueuePanelLayout.MaximumSlide).Within(.001f));
                // A reset puts them at rest, whatever the slide.
                hud.Reset();
                Assert.That(hud.QueueSlide, Is.Zero, "A reset leaves no slide behind.");
            }
            finally { controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator ANewTurn_StartsAtRest_AndPlanningNeverSlides()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            controller.RestartMatch();
            LegacyCombatHud hud = controller.Hud;
            LegacyQueuedDuel duel = ThreeSlotDuel();
            try
            {
                Refresh(controller, duel, true, 2, .01f);
                Refresh(controller, duel, true, 3, 0f);
                Assert.That(hud.QueueSlide, Is.GreaterThan(0f));
                // The next planning turn: nothing consumed, the rows snap home.
                Refresh(controller, duel, false, -1, 0f);
                Assert.That(hud.QueueSlide, Is.Zero);
                Refresh(controller, duel, false, -1, .1f);
                Assert.That(hud.QueueSlide, Is.Zero, "Reserving and removing cards while planning never slides the rows.");
            }
            finally { controller.RestartMatch(); }
        }

        [UnityTest]
        public IEnumerator TheSlide_NeverHoldsTheResolution()
        {
            yield return null;
            // The same turn, frame by frame on the real controller, with no slide and with the longest: every slot starts
            // and the turn ends on the same frame.
            List<int> still = ResolveTurnRecordingSlotFrames(0f);
            List<int> sliding = ResolveTurnRecordingSlotFrames(.6f);
            Assert.That(still.Count, Is.GreaterThanOrEqualTo(4), "Three slots and the turn's end.");
            Assert.That(sliding, Is.EqualTo(still), "The slide is drawn only; the resolution keeps its own time.");
        }

        [UnityTest]
        public IEnumerator TheCoachsCard_OpensTheSeam_SoItCoversNoCard()
        {
            yield return null;
            using (var scope = new PanelScope())
            {
                LegacyCombatHud hud = scope.Controller.Hud;
                MissionCoachHud coach = scope.Controller.CoachHud;
                Assert.That(scope.Controller.QueueLane(0) && scope.Controller.QueueLane(1), Is.True);
                scope.Advance(Frame);
                float tunedSeam = Seam(hud);
                Assert.That(tunedSeam, Is.EqualTo(scope.Settings.QueueSeamGap * .5f).Within(.01f));
                coach.Show(PrologueMissions.Get(2).CreateGuide(), MissionCoachCard.MissionLabel(2));
                try
                {
                    scope.Advance(Frame);
                    Assert.That(coach.TopEdge, Is.GreaterThan(0f), "The coach's card is up over the dock.");
                    Rect card = WorldRect((RectTransform)coach.Root.transform.Find("Coach Border"));
                    Assert.That(Seam(hud), Is.GreaterThan(tunedSeam), "The seam opens round it…");
                    foreach (RectTransform tray in new[] { hud.PlayerQueuePanel, hud.EnemyQueuePanel })
                        Assert.That(WorldRect(tray).Overlaps(card), Is.False, "…so it covers no panel…");
                    foreach (bool playerSide in new[] { true, false })
                        for (int slot = 0; slot < 2; slot++)
                        {
                            RectTransform queued = hud.GetQueuedSkillAnchor(playerSide, slot);
                            if (queued != null) Assert.That(WorldRect(queued).Overlaps(card), Is.False, "…and no card.");
                        }
                    AssertMirror(Cards(hud, "Player Requests"), Cards(hud, "Enemy Requests"));
                }
                finally { coach.Hide(); }
                scope.Advance(Frame);
                Assert.That(Seam(hud), Is.EqualTo(tunedSeam).Within(.01f), "Without it the panels meet at the tuned seam again.");
            }
        }

        [UnityTest]
        public IEnumerator TheCorridorFraming_KeepsTheFeetOverThePanels_AndTabKeepsTheSamePlanningView()
        {
            yield return null;
            DuelPrototypeController controller = FindPrototype();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            try
            {
                controller.RestartMatch();
                LegacyArenaView arena = controller.ArenaView;
                Camera camera = arena.ArenaCamera;
                Refresh(controller, controller.Session, false, -1, 0f);
                float floor = controller.Hud.QueuePanelFloor;
                Assert.That(floor, Is.InRange(.2f, .45f), "The panels stand over the dock, about a third of the way up.");

                arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                arena.Reset();
                arena.BeginTurn();
                // The tightest planning framing (the clock run down): the corridor's feet would sit under the panels.
                arena.SetPlanningState(0f, false);
                Settle(arena, 0f);
                // Compact icons may already clear the corridor. Request a little more clearance to exercise the
                // camera's floor response as well, independent of the current panel-size tuning.
                floor = Mathf.Max(floor, FeetShare(arena) + .025f);
                Assert.That(FeetShare(arena), Is.LessThan(floor), "Unframed, the corridor's feet fall behind the panels…");
                Settle(arena, floor);
                Assert.That(arena.HudFloor, Is.EqualTo(floor).Within(1e-5f));
                Assert.That(FeetShare(arena), Is.GreaterThanOrEqualTo(floor - .002f), "…framed, they stand over them…");
                Assert.That(FeetShare(arena), Is.LessThan(floor + .01f), "…moving the camera no further than that.");
                float lowered = camera.transform.localPosition.y;
                Settle(arena, 0f);
                Assert.That(camera.transform.localPosition.y, Is.GreaterThan(lowered), "An arena ticked without it frames as before.");
                // Inspecting a queue keeps the same clock, framing and HUD clearance as normal planning.
                arena.SetPlanningState(10f, false);
                Settle(arena, floor);
                Vector3 normalPosition = camera.transform.localPosition;
                float normalSize = camera.orthographicSize;
                arena.SetPlanningState(10f, true);
                Settle(arena, floor);
                Assert.That(Vector3.Distance(camera.transform.localPosition, normalPosition), Is.LessThan(.001f),
                    "Tab keeps both fighters in the normal planning view.");
                Assert.That(camera.orthographicSize, Is.EqualTo(normalSize).Within(.001f));
                Assert.That(FeetShare(arena), Is.GreaterThanOrEqualTo(floor - .002f));

                // The forest's framing already clears the panels: unchanged.
                arena.SetBackdrop(ArenaBackdropKind.Forest);
                arena.Reset();
                arena.BeginTurn();
                arena.SetPlanningState(10f, false);
                Settle(arena, floor);
                Assert.That(camera.transform.localPosition.y,
                    Is.EqualTo(arena.DuelCenter.y - 1f + controller.PresentationSettings.CameraFramingLift).Within(.01f));
                Assert.That(FeetShare(arena), Is.GreaterThan(floor));
            }
            finally
            {
                controller.RestartMatch();
                controller.enabled = wasEnabled;
            }
        }

        // Prepared queues isolate the HUD's long-row capacity from manual per-lane reservation limits.
        private static LegacyQueuedDuel ThreeSlotDuel(int count = 3)
        {
            var free = new List<LegacySkill>();
            foreach (LegacySkill source in LegacyInitialSkills.All)
                free.Add(new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower,
                    source.Kind, source.Property, source.AttackCount, source.LaneIndex, source.Description, source.AnimationName));
            var duel = new LegacyQueuedDuel(1000, 50, 1000, 15, free, LegacyInitialSkills.All, new[] { count }, 1);
            var prepared = new List<LegacySkill>(count);
            for (int index = 0; index < count; index++)
            {
                IReadOnlyList<LegacySkill> lane = duel.GetLane(index % 3);
                prepared.Add(lane[(index / 3) % lane.Count]);
            }
            Assert.That(duel.TrySetFixedQueues(prepared, duel.EnemyQueue), Is.True);
            return duel;
        }

        private static void Refresh(DuelPrototypeController controller, ILegacyDuelView duel, bool resolving, int slot, float delta)
        {
            LegacyArenaView arena = controller.ArenaView;
            controller.Hud.Refresh(duel, 10f, resolving, slot, arena.ArenaCamera, arena.PlayerRenderer.transform,
                arena.EnemyRenderer.transform, delta, delta);
            Canvas.ForceUpdateCanvases();
        }

        private static List<int> ResolveTurnRecordingSlotFrames(float slideSeconds)
        {
            var frames = new List<int>();
            using (var scope = new PanelScope("\"queueSlideSeconds\":" + slideSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            {
                DuelPrototypeController controller = scope.Controller;
                for (int lane = 0; lane < 3; lane++) Assert.That(controller.QueueLane(lane), Is.True);
                controller.CommitTurn();
                int lastSlot = controller.Session.LastResolvedSlot;
                for (int frame = 0; frame < 60 * 40 && controller.IsResolving; frame++)
                {
                    scope.Advance(Frame);
                    if (controller.Session.LastResolvedSlot != lastSlot)
                    {
                        lastSlot = controller.Session.LastResolvedSlot;
                        frames.Add(frame);
                    }
                }
                Assert.That(controller.IsResolving, Is.False, "The turn ends.");
                frames.Add(-1);
            }
            return frames;
        }

        private static void Settle(LegacyArenaView arena, float floor)
        {
            for (int frame = 0; frame < 12; frame++)
            {
                if (floor > 0f) arena.SetHudFloor(floor);
                arena.Tick(0f, 1f);
            }
        }

        // The fighters' feet (their ground line, where their shadows lie) as a share of the screen's height from its bottom.
        private static float FeetShare(LegacyArenaView arena)
        {
            Transform actor = arena.PlayerRenderer.transform;
            Assert.That(actor.localPosition.y + MobStudentAnimationSet.GroundOffset, Is.EqualTo(arena.FeetY).Within(1e-4f));
            Vector3 feet = actor.TransformPoint(new Vector3(0f, MobStudentAnimationSet.GroundOffset, 0f));
            return arena.ArenaCamera.WorldToViewportPoint(feet).y;
        }

        private static float Seam(LegacyCombatHud hud)
            => hud.EnemyQueuePanel.anchoredPosition.x - hud.EnemyQueuePanel.sizeDelta.x * .5f;

        private static void AssertMirror(List<RectTransform> player, List<RectTransform> enemy)
        {
            for (int index = 0; index < Mathf.Min(player.Count, enemy.Count); index++)
                Assert.That(player[index].anchoredPosition.x, Is.EqualTo(-enemy[index].anchoredPosition.x).Within(.01f),
                    "Slot " + (index + 1) + " pairs mirror each other across the seam.");
        }

        private static List<RectTransform> Cards(LegacyCombatHud hud, string row)
        {
            Transform queue = row == "Player Requests" ? hud.PlayerQueueRow : hud.PlayerQueueRow.parent.Find(row);
            Assert.That(queue, Is.Not.Null, "Missing HUD node: " + row);
            var cards = new List<RectTransform>();
            foreach (Transform child in queue)
                if (child.gameObject.activeSelf && child.name == "Queued Skill") cards.Add((RectTransform)child);
            return cards;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]), max = min;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static DuelPrototypeController FindPrototype()
        {
            DuelPrototypeController controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }

        /// <summary>The real controller, driven frame by frame with chosen real seconds, on a free three-lane duel (zero-cost
        /// skills, three enemy skills a turn, health enough for a whole turn); the presentation settings are a copy of the
        /// project's without the hit stop, with <paramref name="tuning"/> (JSON fields) over it.</summary>
        private sealed class PanelScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originallyEnabled;
            private readonly FieldInfo arenaSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }

            public PanelScope(string tuning = null)
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
                try
                {
                    // The slide's own clock: the presentation director's pacing has its own tests.
                    JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0,\"showDirectorEnabled\":false" +
                        (string.IsNullOrEmpty(tuning) ? "" : "," + tuning) + "}", Settings);
                    Set("presentationSettings", Settings);
                    arenaSettings.SetValue(Controller.ArenaView, Settings);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                    Set("mission", null);
                    Set("guide", null);
                    var free = new List<LegacySkill>();
                    foreach (LegacySkill source in LegacyInitialSkills.All)
                        free.Add(new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower, source.Kind,
                            source.Property, source.AttackCount, source.LaneIndex, source.Description, source.AnimationName));
                    Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000, free, LegacyInitialSkills.All, new[] { 3 }, 1));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch
                {
                    // A failed setup never reaches Dispose: restore the shared controller here.
                    try { Dispose(); } catch { Controller.enabled = originallyEnabled; }
                    throw;
                }
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

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
