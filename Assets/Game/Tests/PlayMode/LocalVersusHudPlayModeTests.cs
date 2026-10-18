using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The thin PvP overlay over the duel HUD (<see cref="LocalVersusHud"/>): whose turn and the hint from each
    /// player's side, the foot markers (own fighter on the left), and the exit question that pauses nothing.</summary>
    public sealed class LocalVersusHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator ActBadges_ShowCurrentRemainingAct_FromBothPerspectives_AndWarnOnlyAtEightOrMore()
        {
            yield return null;
            var host = new GameObject("Versus ACT Test");
            var skill = new LegacySkill(90821, "ACT 검증", 1, 4, 4, LegacySkillKind.Attack,
                LegacySkillProperty.Penetrate, 1, 0, "검증");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, () => { }))
            {
                var match = new LocalVersusMatch(new[] { skill }, new[] { skill });
                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 0);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 2 / 10"));
                Assert.That(hud.OpponentActText, Is.EqualTo("상대  ACT 3 / 10"));
                Assert.That(hud.OwnActAtRisk || hud.OpponentActAtRisk, Is.False);
                hud.Refresh(match, 1);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 3 / 10"));
                Assert.That(hud.OpponentActText, Is.EqualTo("상대  ACT 2 / 10"),
                    "The guest's own and opponent values are mirrored, just like its queue panels.");

                Assert.That(match.TryCommit(0) && match.TryCommit(1), Is.True);
                hud.Refresh(match, 0);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 2 / 10"));
                Assert.That(Named(hud.Root, LocalVersusHud.OwnActBadgeName).activeInHierarchy, Is.True,
                    "Resolution keeps both amounts visible after the command dock folds away.");
                while (!match.IsTurnResolved) match.ResolveNextSlot();
                match.BeginNextTurn();
                hud.Refresh(match, 0);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 5 / 10"));
                Assert.That(hud.OpponentActText, Is.EqualTo("상대  ACT 6 / 10"), "Natural recovery is current ACT too.");

                Assert.That(match.TryCommit(1) && match.TryCommit(0), Is.True);
                match.BeginNextTurn();
                hud.Refresh(match, 0);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 8 / 10"));
                Assert.That(hud.OpponentActText, Is.EqualTo("상대  ACT 9 / 10"));
                Assert.That(hud.OwnActAtRisk && hud.OpponentActAtRisk, Is.True);
                Assert.That(Named(hud.Root, LocalVersusHud.OwnActBadgeName).GetComponent<Image>().color,
                    Is.EqualTo(DuelVisualTheme.Danger));

                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 0);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 7 / 10"));
                Assert.That(hud.OwnActAtRisk, Is.False, "Spending below the threshold immediately clears the warning.");
                Assert.That(hud.OpponentActAtRisk, Is.True);
                hud.Refresh(match, 1);
                Assert.That(hud.OwnActAtRisk, Is.True);
                Assert.That(hud.OpponentActAtRisk, Is.False);
                Assert.That(hud.OwnActText, Is.EqualTo("나  ACT 9 / 10"));
                Assert.That(hud.OpponentActText, Is.EqualTo("상대  ACT 7 / 10"));

                hud.SetBattleVisible(false);
                Assert.That(Named(hud.Root, LocalVersusHud.OwnActBadgeName).activeInHierarchy, Is.False,
                    "The start and result cards hide counters with the rest of the battle layer.");
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator ActBadges_ClearBothQueueTrays_AtWideAndNarrowAspect_AndFollowTheirResolvingFold()
        {
            yield return null;
            var host = new GameObject("Versus ACT Layout Test");
            var cameraObject = new GameObject("Versus ACT Layout Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, -1000f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .10f, .08f);
            camera.cullingMask = 1 << 5;
            camera.depth = -100f;
            RenderTexture target = null;
            try
            {
                using (var art = new LegacyDuelArt())
                using (var battle = new LegacyCombatHud(host.transform, art, lane => { }, () => { }, () => { }))
                using (var hud = new LocalVersusHud(host.transform, art, () => { }))
                {
                    hud.SetQueueAnchors(battle.PlayerQueuePanel, battle.EnemyQueuePanel);
                    foreach (Vector2Int pixels in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
                    {
                        camera.targetTexture = null;
                        if (target != null) { target.Release(); Object.Destroy(target); }
                        target = new RenderTexture(pixels.x, pixels.y, 24);
                        camera.targetTexture = target;
                        ConfigureLayoutCanvas(battle.Root, camera, pixels);
                        ConfigureLayoutCanvas(hud.Root, camera, pixels);
                        yield return null;
                        yield return null;
                        Canvas.ForceUpdateCanvases();

                        var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                        var perspective = new LocalVersusPerspective(match, 1);
                        battle.BeginTurn();
                        battle.Refresh(perspective, 20f, false, -1, null, null, null);
                        hud.Refresh(match, 1);
                        Canvas.ForceUpdateCanvases();
                        AssertActLayout(hud, battle, camera, pixels);
                        float planningY = Named(hud.Root, LocalVersusHud.OwnActBadgeName)
                            .GetComponent<RectTransform>().anchoredPosition.y;
                        CaptureActLayout(camera, target, "Planning-" + pixels.x + "x" + pixels.y);

                        Assert.That(match.TryCommit(0) && match.TryCommit(1), Is.True);
                        battle.EndTurn();
                        battle.Refresh(perspective, 20f, true, 0, null, null, null, 1f, 1f);
                        hud.Refresh(match, 1);
                        Canvas.ForceUpdateCanvases();
                        AssertActLayout(hud, battle, camera, pixels);
                        Assert.That(Named(hud.Root, LocalVersusHud.OwnActBadgeName)
                            .GetComponent<RectTransform>().anchoredPosition.y, Is.LessThan(planningY - 100f),
                            "Both badges follow their trays down, independently of the command dock's visibility.");
                        CaptureActLayout(camera, target, "Resolving-" + pixels.x + "x" + pixels.y);
                    }
                }
            }
            finally
            {
                camera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Object.Destroy(cameraObject);
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator TurnPlateAndHint_ReadFromEachPlayersSide()
        {
            yield return null;
            var host = new GameObject("Versus Overlay Turn Test");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All, roundLimit: 2);
                hud.Refresh(match, 0);
                Assert.That(hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.FirstPlannerHint));
                Assert.That(Named(hud.Root, "Versus Turn Hint").activeSelf, Is.True);
                hud.Refresh(match, 1);
                Assert.That(hud.TurnText, Is.EqualTo(LocalVersusHud.OpponentTurnText));
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.OpponentChoosingHint));
                hud.Refresh(match, 1, requestPending: true);
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.OpponentChoosingHint),
                    "Only the player whose turn it is can be waiting for the host.");

                Assert.That(match.TryCommit(0), Is.True);
                hud.Refresh(match, 1);
                Assert.That(hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.ResponsePlannerHint));
                hud.Refresh(match, 1, requestPending: true);
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.RequestPendingHint));
                Assert.That(Named(hud.Root, "Versus Turn Hint").activeSelf, Is.True);
                hud.Refresh(match, 0);
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.WaitingForResponseHint));

                Assert.That(match.TryCommit(1), Is.True);
                hud.Refresh(match, 0);
                Assert.That(hud.TurnText, Is.EqualTo(LocalVersusHud.ResolvingText));
                Assert.That(hud.HintText, Is.Empty);

                match.BeginNextTurn();
                hud.Refresh(match, 1);
                Assert.That(match.RoundNumber, Is.EqualTo(2));
                Assert.That(hud.TurnText, Is.EqualTo(LocalVersusHud.MyTurnText));
                Assert.That(hud.HintText, Is.EqualTo(LocalVersusHud.FirstPlannerHint));

                Assert.That(match.TryCommit(1) && match.TryCommit(0), Is.True);
                Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                hud.Refresh(match, 0);
                Assert.That(hud.TurnText, Is.EqualTo(LocalVersusHud.FinishedText));
                // Every screen is one player's: there is no shared-keyboard view any more.
                Assert.Throws<System.ArgumentOutOfRangeException>(() => hud.Refresh(match, -1));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator FootMarkers_OwnFighterOnTheLeft_RingFollowsTheActingFighter()
        {
            yield return null;
            var host = new GameObject("Versus Turn Marker Test");
            var cameraObject = new GameObject("Versus Marker Camera");
            cameraObject.transform.SetParent(host.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var left = new GameObject("Left Fighter");
            var right = new GameObject("Right Fighter");
            left.transform.SetParent(host.transform);
            right.transform.SetParent(host.transform);
            left.transform.position = new Vector3(-2f, 0f, 0f);
            right.transform.position = new Vector3(2f, 0f, 0f);

            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, () => { }))
            {
                hud.SetActorAnchors(camera, left.transform, right.transform);
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 0);
                GameObject own = Named(hud.Root, LocalVersusHud.OwnMarkerName);
                GameObject opponent = Named(hud.Root, LocalVersusHud.OpponentMarkerName);
                Assert.That(own.activeSelf, Is.True);
                Assert.That(opponent.activeSelf, Is.True);
                Assert.That(Label(own).text, Is.EqualTo(LocalVersusHud.OwnActingText));
                Assert.That(Label(opponent).text, Is.EqualTo(LocalVersusHud.OpponentText));
                Assert.That(Ring(own).activeSelf, Is.True);
                Assert.That(Ring(opponent).activeSelf, Is.False);

                // The markers belong to the arena: their own canvas sits under the duel HUD, so its log and the held or
                // Tab explanation cover them; the turn plate, the hint and the exit stay over the duel HUD.
                Canvas markers = Named(hud.Root, LocalVersusHud.FootMarkerLayerName).GetComponent<Canvas>();
                Assert.That(markers, Is.Not.Null);
                Assert.That(markers.overrideSorting, Is.True);
                Assert.That(markers.sortingOrder, Is.EqualTo(LocalVersusHud.FootMarkerSortingOrder));
                Assert.That(markers.sortingOrder, Is.LessThan(LegacyCombatHud.SortingOrder));
                Assert.That(own.transform.parent, Is.SameAs(markers.transform));
                Assert.That(opponent.transform.parent, Is.SameAs(markers.transform));
                Assert.That(Named(hud.Root, "Versus Turn State").GetComponentInParent<Canvas>().sortingOrder,
                    Is.EqualTo(LocalVersusHud.SortingOrder));
                Assert.That(LocalVersusHud.SortingOrder, Is.GreaterThan(LegacyCombatHud.SortingOrder));

                float beforeX = own.GetComponent<RectTransform>().anchoredPosition.x;
                left.transform.position += Vector3.right;
                hud.Refresh(match, 0);
                Assert.That(own.GetComponent<RectTransform>().anchoredPosition.x,
                    Is.GreaterThan(beforeX + 1f), "The identity marker follows its fighter.");

                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 0);
                Assert.That(Label(own).text, Is.EqualTo(LocalVersusHud.OwnActingText));
                Assert.That(Ring(own).activeSelf, Is.True, "Reserving a skill keeps the planning turn.");
                Assert.That(Ring(opponent).activeSelf, Is.False);
                Assert.That(match.TryCommit(0), Is.True);
                hud.Refresh(match, 0);
                Assert.That(Label(own).text, Is.EqualTo(LocalVersusHud.OwnText));
                Assert.That(Label(opponent).text, Is.EqualTo(LocalVersusHud.OpponentActingText));
                Assert.That(Ring(own).activeSelf, Is.False);
                Assert.That(Ring(opponent).activeSelf, Is.True);

                // The guest's own fighter (Right) is the left actor on its screen too: Right holds the turn now.
                hud.Refresh(match, 1);
                Assert.That(Label(own).text, Is.EqualTo(LocalVersusHud.OwnActingText));
                Assert.That(Label(opponent).text, Is.EqualTo(LocalVersusHud.OpponentText));

                Assert.That(match.TryCommit(1), Is.True);
                hud.Refresh(match, 1);
                Assert.That(Ring(own).activeSelf, Is.False);
                Assert.That(Ring(opponent).activeSelf, Is.False);
                Assert.That(Label(own).text, Is.EqualTo(LocalVersusHud.OwnText));

                hud.SetActorAnchors(camera, null, right.transform);
                hud.Refresh(match, 1);
                Assert.That(own.activeSelf, Is.False, "A marker without its fighter hides.");
                Assert.That(opponent.activeSelf, Is.True);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator ExitQuestion_HasItsOwnCanvasOverTheResult_AndStaysReachableWithoutTheBattleLayer()
        {
            yield return null;
            var host = new GameObject("Versus Overlay Exit Test");
            int exits = 0;
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, () => exits++))
            {
                hud.Show();
                Assert.That(hud.IsBattleVisible, Is.True);
                Button(hud.Root, "Versus Exit").onClick.Invoke();
                Assert.That(hud.IsExitConfirming, Is.True);
                Canvas question = Named(hud.Root, "Versus Exit Confirmation").GetComponent<Canvas>();
                Assert.That(question.overrideSorting, Is.True);
                Assert.That(question.sortingOrder, Is.EqualTo(LocalVersusHud.ExitSortingOrder));
                Assert.That(LocalVersusHud.ExitSortingOrder, Is.GreaterThan(400), "Over the result card.");
                Assert.That(Label(Named(hud.Root, "Versus Exit Question")).text, Is.EqualTo(LocalVersusHud.ExitQuestion));
                Button(hud.Root, "Versus Cancel Exit").onClick.Invoke();
                Assert.That(hud.IsExitConfirming, Is.False);
                Assert.That(exits, Is.Zero);

                // Under the start card or the result card the battle's layer goes; Esc still asks.
                hud.SetBattleVisible(false);
                Assert.That(hud.IsBattleVisible, Is.False);
                Assert.That(Named(hud.Root, "Versus Exit").activeInHierarchy, Is.False);
                hud.ToggleExitConfirmation();
                Assert.That(hud.IsExitConfirming, Is.True);
                Button(hud.Root, "Versus Confirm Exit").onClick.Invoke();
                Assert.That(exits, Is.EqualTo(1));

                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(hud.IsExitConfirming, Is.False);
                hud.ToggleExitConfirmation();
                Assert.That(hud.IsExitConfirming, Is.False, "A hidden overlay asks nothing.");
            }
            Object.Destroy(host);
        }

        private static GameObject Named(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            Assert.Fail("Missing UI object: " + name);
            return null;
        }

        private static Text Label(GameObject root)
        {
            Text label = root.GetComponent<Text>();
            return label != null ? label : Named(root, "Versus Foot Identity Label").GetComponent<Text>();
        }

        private static GameObject Ring(GameObject root) => Named(root, "Versus Turn Ring");

        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();

        private static void ConfigureLayoutCanvas(GameObject root, Camera camera, Vector2Int pixels)
        {
            Canvas canvas = root.GetComponent<Canvas>();
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            // A texture-backed canvas gives real pixel layout without changing the user's Game view resolution.
            float scale = Mathf.Sqrt((float)pixels.x / 1920f * pixels.y / 1080f);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = scale;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;
        }

        private static void AssertActLayout(LocalVersusHud hud, LegacyCombatHud battle, Camera camera, Vector2Int pixels)
        {
            Rect own = ScreenBounds(Named(hud.Root, LocalVersusHud.OwnActBadgeName).GetComponent<RectTransform>(), camera);
            Rect opponent = ScreenBounds(Named(hud.Root, LocalVersusHud.OpponentActBadgeName).GetComponent<RectTransform>(), camera);
            Rect ownTray = ScreenBounds(battle.PlayerQueuePanel, camera);
            Rect opponentTray = ScreenBounds(battle.EnemyQueuePanel, camera);
            Assert.That(own.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(opponent.xMax, Is.LessThanOrEqualTo(pixels.x));
            Assert.That(own.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(opponent.yMax, Is.LessThanOrEqualTo(pixels.y));
            Assert.That(own.Overlaps(ownTray) || own.Overlaps(opponentTray), Is.False,
                "My ACT stays outside cards and their keyword tabs.");
            Assert.That(opponent.Overlaps(ownTray) || opponent.Overlaps(opponentTray), Is.False,
                "The opponent's ACT stays outside cards and their keyword tabs.");
            Assert.That(own.xMax, Is.LessThan(ownTray.xMin));
            Assert.That(opponent.xMin, Is.GreaterThan(opponentTray.xMax));
            Assert.That(own.center.y, Is.EqualTo(ownTray.center.y).Within(1f));
            Assert.That(opponent.center.y, Is.EqualTo(opponentTray.center.y).Within(1f));
            Assert.That(own.Overlaps(ScreenBounds(Named(hud.Root, "Versus Turn State").GetComponent<RectTransform>(), camera)),
                Is.False);
            foreach (string name in new[] { LocalVersusHud.OwnActBadgeName, LocalVersusHud.OpponentActBadgeName })
                foreach (Graphic graphic in Named(hud.Root, name).GetComponentsInChildren<Graphic>())
                    Assert.That(graphic.raycastTarget, Is.False, "Resource readouts never block combat inputs.");
        }

        private static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 lower = camera.WorldToScreenPoint(corners[0]), upper = camera.WorldToScreenPoint(corners[2]);
            return Rect.MinMaxRect(lower.x, lower.y, upper.x, upper.y);
        }

        private static void CaptureActLayout(Camera camera, RenderTexture target, string name)
        {
            string directory = System.Environment.GetEnvironmentVariable("SWORDMASTER_ACT_UI_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = null;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                capture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) Object.Destroy(capture);
            }
        }
    }
}
