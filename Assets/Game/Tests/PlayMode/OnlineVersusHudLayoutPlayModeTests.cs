using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class OnlineVersusHudLayoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnlineHost_UsesCompactDuelLayoutAndOwnSideLabels()
        {
            yield return null;
            var host = new GameObject("Online Host HUD Layout Test");
            int cycles = 0, passes = 0;
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => cycles++,
                () => { }, () => passes++, () => { }, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 17.4f, 20f, 0);

                RectTransform turnHeader = Rect(hud.Root, "Versus Turn Header");
                RectTransform desk = Rect(hud.Root, "Versus Command Desk");
                Assert.That(turnHeader.gameObject.activeSelf, Is.True);
                Assert.That(turnHeader.anchorMin, Is.EqualTo(new Vector2(.5f, 1f)));
                Assert.That(turnHeader.anchorMax, Is.EqualTo(new Vector2(.5f, 1f)));
                Assert.That(turnHeader.anchoredPosition, Is.EqualTo(new Vector2(0f, -36f)));
                Assert.That(desk.anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(desk.anchorMax, Is.EqualTo(new Vector2(.5f, 0f)));
                Assert.That(desk.anchoredPosition, Is.EqualTo(new Vector2(0f, 14f)));
                Assert.That(desk.sizeDelta.x, Is.EqualTo(980f).Within(1f));
                Assert.That(desk.sizeDelta.y, Is.EqualTo(200f).Within(1f));
                Assert.That(Label(hud.Root, "Versus Online Clock").text, Is.EqualTo("17.4s"));
                foreach (string lane in new[] { "Q", "W", "E" })
                {
                    GameObject gear = Named(hud.Root, "Versus Skill Gear " + lane);
                    Assert.That(gear.activeInHierarchy, Is.True, lane + " gear must be visible online.");
                    Assert.That(gear.GetComponent<CanvasRenderer>(), Is.Not.Null,
                        lane + " gear needs a CanvasRenderer to draw.");
                    Assert.That(gear.GetComponent<Graphic>().enabled, Is.True);
                }

                Assert.That(Label(Named(hud.Root, "Versus 1P Status"), "Versus Player Name").text,
                    Is.EqualTo("나"));
                Assert.That(Label(Named(hud.Root, "Versus 2P Status"), "Versus Player Name").text,
                    Is.EqualTo("상대"));
                Assert.That(Label(Named(hud.Root, "Versus 1P Queue"), "Queue Heading").text,
                    Does.Contain("나"));
                Assert.That(Label(Named(hud.Root, "Versus 2P Queue"), "Queue Heading").text,
                    Does.Contain("상대"));
                Assert.That(Labels(hud.Root, "Versus Player Clock")[0].text, Is.EqualTo("17.4s"));
                Assert.That(Labels(hud.Root, "Versus Player Clock")[1].text, Is.EqualTo("20.0s"));
                RectTransform exit = Rect(hud.Root, "Versus Exit");
                Assert.That(exit.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(exit.anchorMax, Is.EqualTo(Vector2.one));

                // The duel layout keeps each public queue by its fighter instead of at the bottom corners.
                Assert.That(Vector2.Distance(Rect(hud.Root, "Versus 1P Status").anchoredPosition,
                    Rect(hud.Root, "Versus 1P Queue").anchoredPosition), Is.LessThan(450f));
                Assert.That(Vector2.Distance(Rect(hud.Root, "Versus 2P Status").anchoredPosition,
                    Rect(hud.Root, "Versus 2P Queue").anchoredPosition), Is.LessThan(450f));

                Button cycle = Button(hud.Root, "Versus Cycle");
                Button pass = Button(hud.Root, "Versus Pass");
                Assert.That(cycle.interactable, Is.True);
                Assert.That(pass.interactable, Is.True);
                Assert.That(cycle.transform, Is.Not.SameAs(pass.transform));
                cycle.onClick.Invoke();
                Assert.That(cycles, Is.EqualTo(1));
                Assert.That(passes, Is.Zero);
                pass.onClick.Invoke();
                Assert.That(cycles, Is.EqualTo(1));
                Assert.That(passes, Is.EqualTo(1));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator OnlineGuest_SeesOwnSkillsAndPublicQueue_ButOnlyActsOnOwnTurn()
        {
            yield return null;
            var host = new GameObject("Online Guest HUD Layout Test");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 20f, 13.5f, 1);
                Assert.That(Label(Named(hud.Root, "Versus 1P Status"), "Versus Player Name").text,
                    Is.EqualTo("상대"));
                Assert.That(Label(Named(hud.Root, "Versus 2P Status"), "Versus Player Name").text,
                    Is.EqualTo("나"));
                Assert.That(Label(Named(hud.Root, "Versus 1P Queue"), "Queue Heading").text,
                    Does.Contain("상대"));
                Assert.That(Label(Named(hud.Root, "Versus 2P Queue"), "Queue Heading").text,
                    Does.Contain("나"));
                Assert.That(Label(hud.Root, "Versus Active Player").text, Is.EqualTo("상대 차례"));
                Assert.That(Label(hud.Root, "Versus Online Clock").text, Is.EqualTo("20.0s"));
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Cycle").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Breath").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Pass").interactable, Is.False);

                hud.ShowSkillDetail(0);
                Assert.That(Label(hud.Root, "Versus Detail Name").text,
                    Is.EqualTo(match.Right.GetLane(0)[0].Name));
                hud.HideSkillDetail();

                Assert.That(match.TryQueueLane(0, 0), Is.True);
                hud.Refresh(match, 19.5f, 13.5f, 1);
                Assert.That(Label(hud.Root, "Versus Active Player").text, Is.EqualTo("내 차례"));
                Assert.That(Label(hud.Root, "Versus Online Clock").text, Is.EqualTo("13.5s"));
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Cycle").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Breath").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Pass").interactable, Is.True);

                // While the guest waits for host approval, a second command cannot be sent.
                hud.Refresh(match, 19.5f, 13.5f, 1, requestPending: true);
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Cycle").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Pass").interactable, Is.False);
                hud.Refresh(match, 19.5f, 13.5f, 1);

                hud.ShowQueuedSkillDetail(0, 0);
                Assert.That(Label(hud.Root, "Versus Detail Name").text,
                    Is.EqualTo(match.Left.Queue[0].Name));
                hud.ToggleExitConfirmation();
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.False);
                Assert.That(Button(hud.Root, "Versus Pass").interactable, Is.False);
                hud.ToggleExitConfirmation();
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.True);
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator OnlineResult_UsesLocalVictoryAndDefeatWords()
        {
            yield return null;
            var host = new GameObject("Online Result Perspective Test");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                var attack = new LegacySkill(90001, "검증 공격", 0, 30, 30,
                    LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "검증");
                var match = new LocalVersusMatch(new[] { attack }, LegacyInitialSkills.All,
                    rightHealth: 1, rightResistance: 0);
                Assert.That(match.TryQueueLane(0, 0), Is.True);
                Assert.That(match.TryPass(1), Is.True);
                Assert.That(match.TryPass(0), Is.True);
                match.ResolveNextSlot();
                Assert.That(match.Outcome, Is.EqualTo(LocalVersusOutcome.LeftVictory));

                hud.Refresh(match, 20f, 20f, 0);
                Assert.That(Label(hud.Root, "Versus Result Heading").text, Is.EqualTo("승리"));
                hud.Refresh(match, 20f, 20f, 1);
                Assert.That(Label(hud.Root, "Versus Result Heading").text, Is.EqualTo("패배"));
                hud.Refresh(match, 20f, 20f);
                Assert.That(Label(hud.Root, "Versus Result Heading").text, Is.EqualTo("1P 승리"));
            }
            Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator LocalDuel_RetainsSharedDeskAndPlayerNumbers()
        {
            yield return null;
            var host = new GameObject("Local HUD Layout Regression Test");
            using (var art = new LegacyDuelArt())
            using (var hud = new LocalVersusHud(host.transform, art, _ => { }, () => { },
                () => { }, () => { }, () => { }, () => { }))
            {
                var match = new LocalVersusMatch(LegacyInitialSkills.All, LegacyInitialSkills.All);
                hud.Refresh(match, 20f, 20f);
                RectTransform desk = Rect(hud.Root, "Versus Command Desk");
                Assert.That(desk.sizeDelta.x, Is.EqualTo(1290f).Within(1f));
                Assert.That(desk.sizeDelta.y, Is.EqualTo(180f).Within(1f));
                Assert.That(desk.anchorMin, Is.EqualTo(Vector2.one * .5f));
                Assert.That(Named(hud.Root, "Versus Skill Gear Q").activeSelf, Is.False);
                GameObject onlineTurnHeader = Find(hud.Root, "Versus Turn Header");
                Assert.That(onlineTurnHeader == null || !onlineTurnHeader.activeSelf, Is.True);
                Assert.That(Label(Named(hud.Root, "Versus 1P Status"), "Versus Player Name").text,
                    Is.EqualTo("1P"));
                Assert.That(Label(Named(hud.Root, "Versus 2P Status"), "Versus Player Name").text,
                    Is.EqualTo("2P"));
                Assert.That(Label(Named(hud.Root, "Versus 1P Queue"), "Queue Heading").text,
                    Does.Contain("1P"));
                Assert.That(Label(Named(hud.Root, "Versus 2P Queue"), "Queue Heading").text,
                    Does.Contain("2P"));
                Assert.That(Button(hud.Root, "Versus Lane Q").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Cycle").interactable, Is.True);
                Assert.That(Button(hud.Root, "Versus Pass").interactable, Is.True);
            }
            Object.Destroy(host);
        }

        private static GameObject Named(GameObject root, string name)
        {
            GameObject found = Find(root, name);
            Assert.That(found, Is.Not.Null, "Missing UI object: " + name);
            return found;
        }

        private static GameObject Find(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }

        private static RectTransform Rect(GameObject root, string name) =>
            Named(root, name).GetComponent<RectTransform>();

        private static Text Label(GameObject root, string name) =>
            Named(root, name).GetComponent<Text>();

        private static Text[] Labels(GameObject root, string name)
        {
            var found = new System.Collections.Generic.List<Text>();
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                if (label.name == name) found.Add(label);
            Assert.That(found.Count, Is.EqualTo(2));
            return found.ToArray();
        }

        private static Button Button(GameObject root, string name) =>
            Named(root, name).GetComponent<Button>();
    }
}
