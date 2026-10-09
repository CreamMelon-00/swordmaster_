using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class OnlineVersusTurnMarkersPlayModeTests
    {
        [UnityTest]
        public IEnumerator OwnCharacterStaysMarked_AndTurnRingFollowsActingFighter()
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
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                hud.SetActorAnchors(camera, left.transform, right.transform);
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 20f, 20f, 0);
                GameObject leftMarker = Named(hud.Root, "Versus Foot Marker 1P");
                GameObject rightMarker = Named(hud.Root, "Versus Foot Marker 2P");
                Assert.That(leftMarker.activeSelf, Is.True);
                Assert.That(rightMarker.activeSelf, Is.True);
                Assert.That(Label(leftMarker).text, Is.EqualTo("내 행동 차례"));
                Assert.That(Label(rightMarker).text, Is.EqualTo("상대"));
                Assert.That(Ring(leftMarker).activeSelf, Is.True);
                Assert.That(Ring(rightMarker).activeSelf, Is.False);

                float beforeX = leftMarker.GetComponent<RectTransform>().anchoredPosition.x;
                left.transform.position += Vector3.right;
                hud.Refresh(match, 20f, 20f, 0);
                Assert.That(leftMarker.GetComponent<RectTransform>().anchoredPosition.x,
                    Is.GreaterThan(beforeX + 1f), "The identity marker follows its fighter.");

                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 19f, 20f, 0);
                Assert.That(Label(leftMarker).text, Is.EqualTo("내 캐릭터"));
                Assert.That(Label(rightMarker).text, Is.EqualTo("상대 행동 차례"));
                Assert.That(Ring(leftMarker).activeSelf, Is.False);
                Assert.That(Ring(rightMarker).activeSelf, Is.True);

                hud.Refresh(match, 19f, 20f, 1);
                Assert.That(Label(leftMarker).text, Is.EqualTo("상대"));
                Assert.That(Label(rightMarker).text, Is.EqualTo("내 행동 차례"));

                Assert.That(match.TryPass(1), Is.True);
                Assert.That(match.TryPass(0), Is.True);
                hud.Refresh(match, 19f, 20f, 1);
                Assert.That(Ring(leftMarker).activeSelf, Is.False);
                Assert.That(Ring(rightMarker).activeSelf, Is.False);
                Assert.That(Label(rightMarker).text, Is.EqualTo("내 캐릭터"));

                hud.Refresh(match, 19f, 20f);
                Assert.That(leftMarker.activeSelf, Is.False);
                Assert.That(rightMarker.activeSelf, Is.False);
            }
            Object.Destroy(host);
        }

        private static GameObject Named(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            Assert.Fail("Missing HUD marker: " + name);
            return null;
        }

        private static Text Label(GameObject root)
            => Named(root, "Versus Foot Identity Label").GetComponent<Text>();

        private static GameObject Ring(GameObject root)
            => Named(root, "Versus Turn Ring");
    }
}
