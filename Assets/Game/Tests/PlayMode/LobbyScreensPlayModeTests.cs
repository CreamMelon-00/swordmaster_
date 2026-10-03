using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LobbyScreensPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Parent = new GameObject("Lobby Screens Test");
            public readonly CampaignRun Run;
            public readonly CampaignLobbyHud Hud;
            private readonly GameObject eventSystem;

            public Fixture(CampaignRun run = null)
            {
                Run = run ?? new CampaignRun();
                if (EventSystem.current == null)
                    eventSystem = new GameObject("Lobby Screens Event System", typeof(EventSystem));
                Hud = new CampaignLobbyHud(Parent.transform, new LegacyDuelArt(), null, null, null,
                    null, null, null, null);
                Hud.Show(Run);
            }

            public LobbyScreenTransition Transition => Hud.Root.GetComponent<LobbyScreenTransition>();
            public void Finish() => Transition.Finish();
            public void Click(string name)
            {
                Finish();
                Named(Hud.Root, name).GetComponent<Button>().onClick.Invoke();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(Parent);
                if (eventSystem != null) Object.Destroy(eventSystem);
            }
        }

        [UnityTest]
        public IEnumerator EveryLobbyTab_KeepsCompactNavigationSeparateFromTheContentPage()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Transition, Is.Not.Null);
                foreach (LobbyTab tab in new[] { LobbyTab.Home, LobbyTab.Stages, LobbyTab.Loadout, LobbyTab.Curriculum })
                {
                    fixture.Hud.ShowTab(tab);
                    fixture.Finish();
                    RectTransform page = fixture.Hud.CurrentPage;
                    Assert.That(page, Is.Not.Null, tab.ToString());
                    Assert.That(page.name, Is.EqualTo("Lobby Page"));
                    Assert.That(page.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(page.anchorMax, Is.EqualTo(Vector2.one));
                    Assert.That(page.sizeDelta, Is.EqualTo(new Vector2(0f, -100f)));
                    Assert.That(page.anchoredPosition, Is.EqualTo(new Vector2(0f, -50f)));
                    Assert.That(fixture.Hud.Root.GetComponentsInChildren<LobbyScreenTransition>(true).Length,
                        Is.EqualTo(1), "Repeated navigation must not add transition owners.");
                    Transform header = Named(fixture.Hud.Root, "Lobby Header");
                    Assert.That(header.IsChildOf(page), Is.False, "Navigation stays outside the incoming page's fade and slide.");
                    RectTransform headerRect = header.GetComponent<RectTransform>();
                    RectTransform canvasRect = fixture.Hud.Root.GetComponent<RectTransform>();
                    Assert.That(headerRect.rect.width, Is.InRange(700f, 900f),
                        "Navigation is a compact desk menu rather than a full-width browser bar.");
                    Assert.That(headerRect.rect.width, Is.LessThan(canvasRect.rect.width * .55f));
                    Assert.That(headerRect.rect.height, Is.InRange(72f, 94f));
                    Assert.That(headerRect.anchorMin.x, Is.EqualTo(0f));
                    Assert.That(headerRect.anchorMax.x, Is.EqualTo(0f));
                    Assert.That(header.GetComponent<CanvasGroup>(), Is.Null, "The shared header must remain interactive during the page transition.");
                    foreach (Transform node in fixture.Hud.Root.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.That(node.name, Is.Not.EqualTo("Stages Panel Border"));
                        Assert.That(node.name, Is.Not.EqualTo("Loadout Panel Border"));
                        Assert.That(node.name, Is.Not.EqualTo("Curriculum Panel Border"));
                        Assert.That(node.name, Is.Not.EqualTo("Lobby Modal Dimmer"));
                    }
                    if (tab == LobbyTab.Home)
                    {
                        Assert.That(Named(fixture.Hud.Root, "Bedroom Background").GetComponent<Image>().sprite, Is.Not.Null);
                        Transform sidebar = Named(fixture.Hud.Root, "Home Sidebar");
                        RectTransform sidebarRect = sidebar.GetComponent<RectTransform>();
                        Assert.That(sidebarRect.rect.size,
                            Is.EqualTo(new Vector2(300f, fixture.Run.IsCurriculumOpen ? 270f : 214f)));
                        Assert.That(Named(fixture.Hud.Root, "Home Open Stages").IsChildOf(sidebar), Is.False,
                            "The stage destination occupies the room instead of repeating the preparation menu.");
                    }
                    else
                    {
                        Image background = Named(page.gameObject, "Page Background").GetComponent<Image>();
                        Assert.That(background, Is.Not.Null, "A dedicated page needs an atmosphere layer: " + tab);
                        Assert.That(background.color.a, Is.GreaterThan(.3f).And.LessThan(.9f),
                            "The room must remain visible around the page's ledger surfaces.");
                        Assert.That(background.GetComponent<DuelPanelTrim>(), Is.Null,
                            "The atmosphere layer itself should remain an undecorated backdrop.");
                        Image ledger = Named(page.gameObject, "Page Ledger").GetComponent<Image>();
                        Assert.That(ledger.color.a, Is.EqualTo(1f).Within(.001f),
                            "The content ledger must remain readable over the visible room.");
                        Assert.That(ledger.rectTransform.rect.width, Is.LessThan(page.rect.width),
                            "The ledger leaves some room visible at the page edges.");
                        Assert.That(ledger.raycastTarget, Is.False);
                        string panelName = tab == LobbyTab.Stages ? "Stages Panel"
                            : tab == LobbyTab.Loadout ? "Loadout Panel" : "Curriculum Panel";
                        RectTransform content = Named(page.gameObject, panelName).GetComponent<RectTransform>();
                        Assert.That(content.rect.width * content.localScale.x,
                            Is.LessThanOrEqualTo(ledger.rectTransform.rect.width + .5f),
                            panelName + " must fit inside the ledger at the current screen ratio.");
                    }
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Transition_BlocksOnlyItsPageAndRestoresPositionAlphaAndInputOnFinishOrReplacement()
        {
            yield return null;
            var owner = new GameObject("Page Transition Test");
            var first = new GameObject("First Page", typeof(RectTransform), typeof(CanvasGroup));
            var second = new GameObject("Second Page", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                LobbyScreenTransition transition = owner.AddComponent<LobbyScreenTransition>();
                RectTransform firstRect = first.GetComponent<RectTransform>(), secondRect = second.GetComponent<RectTransform>();
                CanvasGroup firstGroup = first.GetComponent<CanvasGroup>(), secondGroup = second.GetComponent<CanvasGroup>();
                Vector3 firstPosition = new Vector3(17f, -33f, 4f), secondPosition = new Vector3(-20f, 29f, 0f);
                firstRect.localPosition = firstPosition;
                secondRect.localPosition = secondPosition;
                firstGroup.alpha = .8f;
                transition.Play(firstRect);
                Assert.That(transition.IsTransitioning, Is.True);
                Assert.That(transition.Progress, Is.Zero);
                Assert.That(firstRect.localPosition.x, Is.EqualTo(firstPosition.x + 32f));
                Assert.That(firstGroup.alpha, Is.Zero);
                Assert.That(firstGroup.interactable, Is.False);
                Assert.That(firstGroup.blocksRaycasts, Is.False);
                transition.Advance(.11f);
                Assert.That(transition.Progress, Is.EqualTo(.5f).Within(.001f));
                Assert.That(firstGroup.alpha, Is.GreaterThan(0f).And.LessThan(.8f));
                Assert.That(firstRect.localPosition.x, Is.GreaterThan(firstPosition.x).And.LessThan(firstPosition.x + 32f));
                transition.Play(secondRect, -1);
                AssertReady(firstRect, firstGroup, firstPosition, .8f);
                Assert.That(secondRect.localPosition.x, Is.EqualTo(secondPosition.x - 32f));
                transition.Advance(.22f);
                Assert.That(transition.IsTransitioning, Is.False);
                Assert.That(transition.Progress, Is.EqualTo(1f));
                AssertReady(secondRect, secondGroup, secondPosition, 1f);
                transition.Play(firstRect);
                transition.Finish();
                transition.Finish();
                AssertReady(firstRect, firstGroup, firstPosition, .8f);
                transition.Play(null);
                Assert.That(transition.IsTransitioning, Is.False);

                transition.Play(firstRect);
                transition.enabled = false;
                AssertReady(firstRect, firstGroup, firstPosition, .8f);
                Assert.That(transition.IsTransitioning, Is.False);
                transition.enabled = true;
                transition.Play(firstRect);
                first.SetActive(false);
                transition.Advance(.01f);
                Assert.That(transition.IsTransitioning, Is.False);
                AssertReady(firstRect, firstGroup, firstPosition, .8f);
                first.SetActive(true);
                transition.Play(firstRect);
                Object.Destroy(first);
                yield return null;
                transition.Advance(.01f);
                Assert.That(transition.IsTransitioning, Is.False, "A destroyed page must not leave the shared animation owner busy.");
                transition.Play(secondRect);
                Object.Destroy(owner);
                yield return null;
                AssertReady(secondRect, secondGroup, secondPosition, 1f);
            }
            finally
            {
                if (owner != null) Object.Destroy(owner);
                if (first != null) Object.Destroy(first);
                Object.Destroy(second);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Transition_UsesUnscaledTimeSoAPausedGameStillReachesAnInteractivePage()
        {
            yield return null;
            float originalTimeScale = Time.timeScale;
            var owner = new GameObject("Paused Lobby Transition Test");
            var pageObject = new GameObject("Paused Page", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                Time.timeScale = 0f;
                var transition = owner.AddComponent<LobbyScreenTransition>();
                RectTransform page = pageObject.GetComponent<RectTransform>();
                Vector3 position = page.localPosition;
                transition.Play(page);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (transition.IsTransitioning && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(transition.IsTransitioning, Is.False, "The page animation must not depend on scaled gameplay time.");
                AssertReady(page, pageObject.GetComponent<CanvasGroup>(), position, 1f);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(owner);
                Object.Destroy(pageObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RapidNavigation_RefreshHideAndResetNeverLeaveAnInvisibleOrBlockedPage()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Hud.IsTransitioning, Is.False, "Showing an existing lobby is a refresh, not a navigation animation.");
                foreach (LobbyTab tab in new[] { LobbyTab.Stages, LobbyTab.Curriculum, LobbyTab.Loadout, LobbyTab.Home, LobbyTab.Curriculum })
                {
                    fixture.Hud.ShowTab(tab);
                    Assert.That(fixture.Hud.CurrentTab, Is.EqualTo(tab), "The view API changes immediately even while input is guarded.");
                    Assert.That(fixture.Hud.IsTransitioning, Is.True);
                    Assert.That(fixture.Hud.CurrentPage.GetComponent<CanvasGroup>().interactable, Is.False);
                    Assert.That(Named(fixture.Hud.Root, "Tab Home").GetComponent<Button>().interactable, Is.True,
                        "A rapid navigation request must remain possible through the shared header.");
                }
                fixture.Hud.Show(fixture.Run);
                Assert.That(fixture.Hud.CurrentTab, Is.EqualTo(LobbyTab.Curriculum));
                Assert.That(fixture.Hud.IsTransitioning, Is.False, "Refreshing after a curriculum choice must not restart navigation.");
                AssertPageReady(fixture.Hud.CurrentPage);
                fixture.Hud.ShowTab(LobbyTab.Stages);
                fixture.Hud.Hide();
                Assert.That(fixture.Hud.IsTransitioning, Is.False);
                Assert.That(fixture.Hud.IsVisible, Is.False);
                fixture.Hud.Show(fixture.Run);
                Assert.That(fixture.Hud.CurrentTab, Is.EqualTo(LobbyTab.Stages));
                AssertPageReady(fixture.Hud.CurrentPage);
                fixture.Hud.ShowTab(LobbyTab.Loadout);
                fixture.Hud.ResetView();
                Assert.That(fixture.Hud.CurrentTab, Is.EqualTo(LobbyTab.Home));
                Assert.That(fixture.Hud.IsTransitioning, Is.False);
                AssertPageReady(fixture.Hud.CurrentPage);
                Assert.That(fixture.Hud.Root.GetComponents<LobbyScreenTransition>().Length, Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Navigation_PreservesSkillSelectionCurriculumSelectionAndUnsavedLoadoutWithoutChoosing()
        {
            yield return null;
            using (var fixture = new Fixture(FirstStageClearedLobby()))
            {
                fixture.Hud.ShowTab(LobbyTab.Loadout);
                fixture.Click("Loadout Slot W 1");
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("깊은 찌르기"));
                int savedQFirst = fixture.Run.GetEquippedLane(0)[0].SkillId;
                Assert.That(fixture.Run.TryPlaceLoadoutSkill(1, 0, 1), Is.True);
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True);
                int[] draft = SlotIds(fixture.Run);
                int owned = fixture.Run.OwnedSkills.Count;

                fixture.Hud.ShowTab(LobbyTab.Curriculum);
                fixture.Click("Curriculum Node breathing");
                Assert.That(Label(fixture.Hud.Root, "Curriculum Detail Name").text, Is.EqualTo("호흡"));
                fixture.Hud.ShowTab(LobbyTab.Stages);
                fixture.Hud.ShowTab(LobbyTab.Home);
                fixture.Hud.ShowTab(LobbyTab.Loadout);
                fixture.Finish();
                Assert.That(Label(fixture.Hud.Root, "Loadout Detail Name").text, Is.EqualTo("깊은 찌르기"),
                    "Leaving the page must not discard the selected lane and skill.");
                Assert.That(Label(fixture.Hud.Root, "Loadout Status").text, Does.StartWith("저장 전 변경"));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True);
                CollectionAssert.AreEqual(draft, SlotIds(fixture.Run));
                Assert.That(fixture.Run.GetEquippedLane(0)[0].SkillId, Is.EqualTo(savedQFirst), "Navigation must not save the combat order.");
                fixture.Hud.ShowTab(LobbyTab.Curriculum);
                fixture.Finish();
                Assert.That(Label(fixture.Hud.Root, "Curriculum Detail Name").text, Is.EqualTo("호흡"),
                    "Leaving the page must not discard the selected curriculum node.");
                Assert.That(fixture.Run.Curriculum.Active, Is.Null, "Viewing a node must not start it.");
                Assert.That(fixture.Run.OwnedSkills.Count, Is.EqualTo(owned));
                Assert.That(fixture.Run.HasLoadoutChanges, Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeywordBadges_UseReadableSemanticColorsAndActualViewsOmitSharedDamageRouting()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var view = new SkillInfoView(fixture.Hud.Root.transform, new LegacyDuelArt().UIFont,
                    Vector2.zero, 334f, "Keyword Test");
                var backgrounds = new HashSet<Color>();
                var inks = new HashSet<Color>();
                foreach (int id in new[] { 1, 3, 8, 2, 9, 16, 17 })
                {
                    view.SetSkill(Skill(id));
                    Transform first = Named(view.Root, "Keyword 1");
                    Color background = first.GetComponent<Image>().color;
                    Color ink = Label(view.Root, "Keyword 1 Text").color;
                    Assert.That(backgrounds.Add(background), Is.True, "Different meanings need distinct badge backgrounds: skill " + id);
                    Assert.That(inks.Add(ink), Is.True, "Different meanings need distinct badge ink colors: skill " + id);
                    AssertReadableBadge(first, ink, id);
                    Transform second = Named(view.Root, "Keyword 2", true);
                    if (second.gameObject.activeInHierarchy)
                        AssertReadableBadge(second, Label(view.Root, "Keyword 2 Text").color, id);
                    AssertNoDamageRouting(view.Root);
                }
                view.Root.SetActive(false);
                Object.Destroy(view.Root);

                fixture.Hud.ShowTab(LobbyTab.Loadout);
                fixture.Click("Loadout Slot W 1");
                GameObject loadout = Named(fixture.Hud.Root, "Loadout Selected Detail").gameObject;
                Assert.That(Label(loadout, "Keyword 1 Text").text, Does.Contain("고화력"));
                AssertNoDamageRouting(loadout);
                AssertReadableBadge(Named(loadout, "Keyword 1"), Label(loadout, "Keyword 1 Text").color, 3);
                fixture.Hud.ShowTab(LobbyTab.Curriculum);
                fixture.Click("Curriculum Node one-stroke");
                GameObject curriculum = Named(fixture.Hud.Root, "Curriculum Selected Detail").gameObject;
                Assert.That(Label(curriculum, "Keyword 1 Text").text, Does.Contain("위력 편차"));
                AssertNoDamageRouting(curriculum);
                AssertReadableBadge(Named(curriculum, "Keyword 1"), Label(curriculum, "Keyword 1 Text").color, 16);

                using (var combat = new LegacyCombatHud(fixture.Parent.transform, new LegacyDuelArt(), null, null, null))
                {
                    combat.ShowExplanation(Skill(1), false);
                    GameObject popup = Named(combat.Root, "Skill Explain").gameObject;
                    AssertNoDamageRouting(popup);
                    Assert.That(Label(popup, "Effect").text, Does.Contain("다음 턴 ACT 회복 +1"),
                        "Removing a general rule must not remove the skill's actual effect.");
                    AssertReadableBadge(Named(popup, "Keyword 1"), Label(popup, "Keyword 1 Text").color, 1);
                    combat.ShowExplanation(Skill(8), true);
                    popup = Named(combat.Root, "Enemy Skill Explain").gameObject;
                    AssertNoDamageRouting(popup);
                    Assert.That(Label(popup, "Effect").text, Does.Contain("25%"));
                }
            }
            yield return null;
        }

        private static void AssertReady(RectTransform page, CanvasGroup group, Vector3 position, float alpha)
        {
            Assert.That(Vector3.Distance(page.localPosition, position), Is.LessThan(.001f));
            Assert.That(group.alpha, Is.EqualTo(alpha).Within(.001f));
            Assert.That(group.interactable, Is.True);
            Assert.That(group.blocksRaycasts, Is.True);
        }

        private static void AssertPageReady(RectTransform page)
        {
            Assert.That(page.anchoredPosition, Is.EqualTo(new Vector2(0f, -50f)));
            CanvasGroup group = page.GetComponent<CanvasGroup>();
            if (group == null) return;
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.interactable, Is.True);
            Assert.That(group.blocksRaycasts, Is.True);
        }

        private static void AssertReadableBadge(Transform badge, Color ink, int id)
        {
            Color background = badge.GetComponent<Image>().color;
            Assert.That(background.a, Is.EqualTo(1f), "Keyword colors must not vary with the background underneath: skill " + id);
            Assert.That(Contrast(ink, background), Is.GreaterThanOrEqualTo(4.5f), "Readable keyword contrast: skill " + id);
            SkillInfoGlyph glyph = badge.GetComponentInChildren<SkillInfoGlyph>(true);
            Assert.That(glyph, Is.Not.Null);
            Assert.That(glyph.color, Is.EqualTo(ink), "The pictogram and caption must carry the same meaning color.");
            Assert.That(glyph.raycastTarget, Is.False);
            Assert.That(badge.GetComponent<Image>().raycastTarget, Is.False);
        }

        private static float Contrast(Color first, Color second)
        {
            float a = Luminance(first), b = Luminance(second);
            return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
        }

        private static float Luminance(Color color)
            => .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);

        private static float Linear(float value)
            => value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);

        private static void AssertNoDamageRouting(GameObject root)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                Assert.That(node.name, Is.Not.EqualTo("Damage Routing"));
            foreach (Text text in root.GetComponentsInChildren<Text>())
            {
                Assert.That(text.text, Does.Not.Contain("공격과 대결 → 저항"));
                Assert.That(text.text, Does.Not.Contain("방어·빈칸 → 체력"));
            }
        }

        private static CampaignRun FirstStageClearedLobby()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            return run;
        }

        private static int[] SlotIds(CampaignRun run)
        {
            var result = new int[9];
            for (int lane = 0; lane < 3; lane++)
                for (int slot = 0; slot < 3; slot++) result[lane * 3 + slot] = run.GetLoadoutSlot(lane, slot)?.SkillId ?? 0;
            return result;
        }

        private static LegacySkill Skill(int id)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
            throw new InvalidOperationException("Missing skill " + id);
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();

        private static Transform Named(GameObject root, string name, bool includeInactive = false)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(includeInactive))
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing active lobby screen node: " + name);
            return null;
        }
    }
}
