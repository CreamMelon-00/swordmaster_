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
    }
}
