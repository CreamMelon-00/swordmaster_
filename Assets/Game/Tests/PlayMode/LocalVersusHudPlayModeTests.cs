using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LocalVersusHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator SharedDesk_ShowsBothClocksAndReservationsAndCurrentSkillDetails()
        {
            yield return null;
            var host = new GameObject("Versus HUD Test Host");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 20f, 13.5f);
                Assert.That(Label(hud.Root, "Versus Active Player").text, Does.Contain("1P"));
                Assert.That(Labels(hud.Root, "Versus Player Clock")[0].text, Is.EqualTo("20.0s"));
                Assert.That(Labels(hud.Root, "Versus Player Clock")[1].text, Is.EqualTo("13.5s"));
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Breath").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Cycle").interactable, Is.True);
                foreach (Image gauge in hud.Root.GetComponentsInChildren<Image>(true))
                    if (gauge.name.StartsWith("Versus ", StringComparison.Ordinal) &&
                        gauge.name.EndsWith(" Fill", StringComparison.Ordinal))
                        Assert.That(gauge.sprite, Is.Not.Null, gauge.name + " needs a sprite for fillAmount.");

                hud.ShowSkillDetail(0);
                Assert.That(Named(hud.Root, "Versus Skill Detail").activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(match.Left.GetLane(0)[0].Name));
                Assert.That(Label(hud.Root, "Versus Detail Values").text, Is.Not.Empty);
                hud.HideSkillDetail();
                Assert.That(Named(hud.Root, "Versus Skill Detail").activeSelf, Is.False);

                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 19.2f, 13.5f);
                Assert.That(Label(hud.Root, "Versus Active Player").text, Does.Contain("2P"));
                Assert.That(match.Left.Queue.Count, Is.EqualTo(1));
                Assert.That(Named(hud.Root, "Versus 1P Queue").GetComponentsInChildren<Image>().Length,
                    Is.GreaterThan(1));
                Assert.That(Labels(hud.Root, "Versus HP")[0].text, Does.Contain("100 / 100"));

                // A public queued technique must expose the same readable facts as the current lane.
                GameObject leftChip = QueueChip(hud.Root, 1);
                Assert.That(leftChip.GetComponent<Image>().raycastTarget, Is.True);
                Enter(leftChip);
                Assert.That(Named(hud.Root, "Versus Skill Detail").activeSelf, Is.True);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(match.Left.Queue[0].Name));
                Assert.That(Label(hud.Root, "ACT Value").text,
                    Is.EqualTo(match.Left.EffectiveCost(match.Left.Queue[0]).ToString()));
                Assert.That(Label(hud.Root, "Versus Detail Values").text, Is.Not.Empty);
                Assert.That(Label(hud.Root, "Versus Detail Effect").text, Is.Not.Empty);
                Exit(leftChip);
                Assert.That(Named(hud.Root, "Versus Skill Detail").activeSelf, Is.False);

                Assert.That(match.TryQueueBreath(1), Is.True);
                hud.Refresh(match, 19.2f, 13.1f);
                GameObject rightChip = QueueChip(hud.Root, 2);
                Enter(rightChip);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo("숨고르기"));
                Assert.That(Label(hud.Root, "Versus Breath Description").text,
                    Does.Contain("ACT 0").And.Contain("기술 순서"));
                Assert.That(Named(hud.Root, "Skill Summary").activeSelf, Is.False);
                Exit(rightChip);
                Assert.That(Named(hud.Root, "Versus Skill Detail").activeSelf, Is.False);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator ExitRequiresConfirmation_AndModalBlocksGameplayButtons()
        {
            yield return null;
            var host = new GameObject("Versus HUD Exit Test Host");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 20f, 20f);
                Button(hud.Root, "Versus Exit").onClick.Invoke();
                Assert.That(hud.IsExitConfirming, Is.True);
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.False);
                Assert.That(Named(hud.Root, "Versus Exit Confirmation").activeSelf, Is.True);
                Button(hud.Root, "Versus Cancel Exit").onClick.Invoke();
                Assert.That(hud.IsExitConfirming, Is.False);
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.True);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator FinishedMatch_TitleButtonReturnsDirectly()
        {
            yield return null;
            var host = new GameObject("Versus HUD Result Test Host");
            int exitCalls = 0;
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => exitCalls++))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All,
                    roundLimit: 1);
                Assert.That(match.TryPass(0), Is.True);
                Assert.That(match.TryPass(1), Is.True);
                Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.Draw));
                hud.Refresh(match, 20f, 20f);
                Assert.That(Named(hud.Root, "Versus Result Overlay").activeSelf, Is.True);
                Button(hud.Root, "Versus Result Exit").onClick.Invoke();
                Assert.That(exitCalls, Is.EqualTo(1));
                Assert.That(hud.IsExitConfirming, Is.False);
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

        private static Text Label(GameObject root, string name)
        {
            return Named(root, name).GetComponent<Text>();
        }

        private static Text[] Labels(GameObject root, string name)
        {
            var found = new System.Collections.Generic.List<Text>();
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                if (label.name == name) found.Add(label);
            Assert.That(found.Count, Is.EqualTo(2));
            return found.ToArray();
        }

        private static Button Button(GameObject root, string name)
        {
            return Named(root, name).GetComponent<Button>();
        }

        private static GameObject QueueChip(GameObject root, int player)
        {
            Transform row = Named(root, "Versus " + player + "P Queue").transform;
            foreach (Transform child in row)
                if (child.name == "Queued Skill" && child.gameObject.activeSelf) return child.gameObject;
            Assert.Fail("Missing visible " + player + "P queued skill");
            return null;
        }

        private static void Enter(GameObject card)
        {
            ExecuteEvents.Execute<IPointerEnterHandler>(card, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerEnterHandler);
        }

        private static void Exit(GameObject card)
        {
            ExecuteEvents.Execute<IPointerExitHandler>(card, new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerExitHandler);
        }
    }
}

