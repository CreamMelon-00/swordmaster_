using System;
using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CompactSkillCardPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly SkillInfoView View;

            public Fixture(float width)
            {
                CanvasRoot = new GameObject("Compact Skill Card Test Canvas", typeof(RectTransform), typeof(Canvas));
                CanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                View = new SkillInfoView(CanvasRoot.transform, new LegacyDuelArt().UIFont, Vector2.zero, width, "Test");
            }

            public void Dispose() => Object.Destroy(CanvasRoot);
        }

        [UnityTest]
        public IEnumerator EverySkillAndSide_FitsMeasuredTextWithoutDroppingFactsOrKeywordColours()
        {
            yield return null;
            foreach (float width in new[] { 334f, 416f })
            {
                using (var fixture = new Fixture(width))
                {
                    foreach (LegacySkill skill in AllSkills())
                    {
                        foreach (bool enemy in new[] { false, true })
                        {
                            fixture.View.SetSkill(skill, enemy);
                            fixture.View.PlaceTop(0f);
                            Canvas.ForceUpdateCanvases();
                            RectTransform body = fixture.View.Root.GetComponent<RectTransform>();
                            Assert.That(body.rect.width, Is.EqualTo(width).Within(.1f));
                            Assert.That(body.rect.height, Is.EqualTo(fixture.View.Height).Within(.1f));
                            Assert.That(fixture.View.Height, Is.InRange(72f, 200f), skill.Name + " must not retain the old 224px body.");
                            Assert.That(Label(body.gameObject, "ACT Value").text, Is.EqualTo(skill.Cost.ToString()));
                            Assert.That(fixture.View.PowerText.text, Is.EqualTo(CampaignSkillText.Power(skill)));
                            Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo(Type(skill.Property)));
                            Assert.That(Label(body.gameObject, "Hits Value").text,
                                Is.EqualTo(skill.Kind == LegacySkillKind.Defence ? "같은 칸" : skill.AttackCount + "회"));
                            Assert.That(fixture.View.EffectText.text, Is.Not.Empty);
                            if (enemy && (LegacySkillRoles.Get(skill) & LegacySkillRole.ActRecovery) != 0)
                                Assert.That(fixture.View.EffectText.text, Does.Contain("플레이어 전용"));
                            AssertBodyFits(body, fixture.View.EffectText);
                            foreach (Graphic decoration in body.GetComponentsInChildren<Graphic>(true))
                                Assert.That(decoration.raycastTarget, Is.False, decoration.name);
                        }
                    }
                    fixture.View.SetSkill(Skill(1));
                    Assert.That(Label(fixture.View.Root, "Keyword 1 Text").color,
                        Is.EqualTo((Color)new Color32(36, 89, 75, 255)), "ACT recovery keeps its established green ink.");
                    Assert.That(Named(fixture.View.Root, "Keyword 1").GetComponent<Image>().color,
                        Is.EqualTo((Color)new Color32(197, 212, 182, 255)));
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ShortLongSingleKeywordAndEmptySelection_ReflowAndKeepTheTopAnchored()
        {
            yield return null;
            using (var fixture = new Fixture(334f))
            {
                var body = fixture.View.Root.GetComponent<RectTransform>();
                fixture.View.SetSkill(Skill(14));
                fixture.View.PlaceTop(91f);
                float shortHeight = fixture.View.Height;
                int count = body.GetComponentsInChildren<Transform>(true).Length;
                RectTransform first = Named(body.gameObject, "Keyword 1").GetComponent<RectTransform>();
                RectTransform row = Named(body.gameObject, "Skill Keywords").GetComponent<RectTransform>();
                Assert.That(Named(body.gameObject, "Keyword 2").gameObject.activeInHierarchy, Is.False);
                Assert.That(first.rect.width, Is.EqualTo(row.rect.width).Within(.1f), "A single keyword uses the available body width.");
                Assert.That(first.anchoredPosition.x, Is.EqualTo(0f).Within(.1f));
                Assert.That(Bounds(body, body.parent).yMax, Is.EqualTo(91f).Within(.1f));

                fixture.View.SetSkill(Skill(3));
                fixture.View.PlaceTop(91f);
                Assert.That(fixture.View.Height, Is.GreaterThan(shortHeight + 10f), "Three readable lines need more height than one.");
                Assert.That(Bounds(body, body.parent).yMax, Is.EqualTo(91f).Within(.1f));
                Assert.That(Named(body.gameObject, "Keyword 2").gameObject.activeInHierarchy, Is.True);
                Assert.That(first.rect.width, Is.LessThan(row.rect.width));
                AssertBodyFits(body, fixture.View.EffectText);

                fixture.View.SetEmptyMessage("기술을 선택하세요.\n카드를 끌어 편성할 수 있습니다.");
                fixture.View.PlaceTop(91f);
                Assert.That(fixture.View.EffectText.text, Does.Contain("기술을 선택"));
                Assert.That(Named(body.gameObject, "Skill Attachments").gameObject.activeInHierarchy, Is.False);
                Assert.That(Named(body.gameObject, "Skill Keywords").gameObject.activeInHierarchy, Is.False);
                Assert.That(fixture.View.AttackTypeText.text, Is.Empty);
                Assert.That(fixture.View.PowerText.text, Is.Empty);
                AssertThatTextFits(fixture.View.EffectText);
                AssertInside(fixture.View.EffectText.rectTransform, body);

                fixture.View.SetSkill(Skill(14));
                fixture.View.PlaceTop(91f);
                Assert.That(fixture.View.Height, Is.EqualTo(shortHeight).Within(.1f));
                Assert.That(body.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count), "Reflow does not rebuild the view.");
                Assert.That(Named(body.gameObject, "Skill Attachments").gameObject.activeInHierarchy, Is.True);
                fixture.View.Clear();
                Assert.That(fixture.View.EffectText.text, Is.Empty);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualCombatPopups_ShrinkForShortDescriptionsAndKeepHeaderBodyAndHintSeparate()
        {
            yield return null;
            var parent = new GameObject("Compact Skill Popup HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                foreach (bool enemy in new[] { false, true })
                {
                    hud.ShowExplanation(Skill(14), enemy);
                    Canvas.ForceUpdateCanvases();
                    GameObject popup = Named(hud.Root, enemy ? "Enemy Skill Explain" : "Skill Explain").gameObject;
                    float shortHeight = popup.GetComponent<RectTransform>().rect.height;
                    Assert.That(shortHeight, Is.LessThan(320f), "One-line facts should not leave a 368px popup.");
                    foreach (LegacySkill skill in AllSkills())
                    {
                        hud.ShowExplanation(skill, enemy);
                        Canvas.ForceUpdateCanvases();
                        RectTransform paper = popup.GetComponent<RectTransform>();
                        var body = Named(popup, "Skill Summary").GetComponent<RectTransform>();
                        Text description = Label(popup, "Effect");
                        AssertBodyFits(body, description);
                        AssertInside(body, paper);
                        AssertInside(Label(popup, "Name").rectTransform, paper);
                        RectTransform hint = Label(popup, "Explanation Hint").rectTransform;
                        AssertInside(hint, paper);
                        AssertNoOverlap(body, hint, paper);
                        AssertHeaderClear(popup, paper, "Name", enemy ? "Power Property" : "Power Cost Property", "Explanation Skill Icon");
                        foreach (SkillInfoAttachment badge in popup.GetComponentsInChildren<SkillInfoAttachment>()) AssertOnScreen(badge.rectTransform);
                    }
                    hud.ShowExplanation(Skill(3), enemy);
                    Assert.That(popup.GetComponent<RectTransform>().rect.height, Is.GreaterThan(shortHeight));
                    hud.ShowExplanation(Skill(14), enemy);
                    Assert.That(popup.GetComponent<RectTransform>().rect.height, Is.EqualTo(shortHeight).Within(.1f));
                }
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualLoadoutDetail_ShrinksWithoutMovingItsTopOrHidingTheRemoveButton()
        {
            yield return null;
            var parent = new GameObject("Compact Loadout Skill Detail Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run); lobby.ShowTab(LobbyTab.Loadout);
                yield return null;
                Click(lobby.Root, "Loadout Slot Q 1");
                GameObject detail = Named(lobby.Root, "Loadout Selected Detail").gameObject;
                RectTransform border = detail.GetComponent<RectTransform>();
                float shortHeight = border.rect.height, originalTop = Bounds(border, border.parent).yMax;
                Assert.That(shortHeight, Is.LessThan(350f), "One-line details should not retain the old 396px paper.");
                foreach (string slot in new[] { "Loadout Slot W 1", "Loadout Slot Q 3", "Loadout Slot E 1", "Loadout Slot Q 1" })
                {
                    Click(lobby.Root, slot);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(Bounds(border, border.parent).yMax, Is.EqualTo(originalTop).Within(.1f), "Only the lower card edge moves during selection.");
                    RectTransform paper = Named(detail, "Detail Surface").GetComponent<RectTransform>();
                    RectTransform body = Named(detail, "Skill Summary").GetComponent<RectTransform>();
                    RectTransform remove = Named(detail, "Loadout Remove Selected").GetComponent<RectTransform>();
                    AssertInside(body, paper); AssertInside(remove, paper); AssertNoOverlap(body, remove, paper);
                    AssertBodyFits(body, Label(detail, "Loadout Detail Effect"));
                    AssertHeaderClear(detail, paper, "Loadout Detail Name", "Loadout Detail Role", "Loadout Detail Icon");
                    Assert.That(Label(detail, "Detail Heading").text, Does.Contain("강화 0/3"));
                    Assert.That(run.HasLoadoutChanges, Is.False);
                }
                Click(lobby.Root, "Loadout Slot W 1");
                Assert.That(border.rect.height, Is.GreaterThan(shortHeight));
                Assert.That(run.Currency, Is.Zero);
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
            }
            finally { lobby?.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualShopDetail_HidesPurchasePreviewAndReflowsUpgradeControlsWithoutTransactions()
        {
            yield return null;
            var parent = new GameObject("Compact Shop Skill Detail Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run); lobby.ShowTab(LobbyTab.Shop);
                yield return null;
                Click(lobby.Root, "Shop Skill 14");
                Canvas.ForceUpdateCanvases();
                GameObject detail = Named(lobby.Root, "Shop Selected Detail").gameObject;
                RectTransform border = detail.GetComponent<RectTransform>();
                float purchaseHeight = border.rect.height, top = Bounds(border, border.parent).yMax;
                Assert.That(purchaseHeight, Is.LessThan(580f), "Acquisition should not reserve the old 644px card or an absent upgrade preview.");
                Assert.That(Named(detail, "Shop Upgrade Preview").gameObject.activeInHierarchy, Is.False);
                AssertShopSections(detail);
                foreach (int id in new[] { 10, 12, 19, 42 })
                {
                    Click(lobby.Root, "Shop Skill " + id);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(Label(detail, "Shop Detail Name").text, Is.EqualTo(Skill(id).Name));
                    Assert.That(Label(detail, "ACT Value").text, Is.EqualTo(Skill(id).Cost.ToString()));
                    Assert.That(Label(detail, "Shop Detail Values").text, Is.EqualTo(CampaignSkillText.Power(Skill(id))));
                    Assert.That(Bounds(border, border.parent).yMax, Is.EqualTo(top).Within(.1f));
                    Assert.That(Named(detail, "Shop Upgrade Preview").gameObject.activeInHierarchy, Is.False);
                    AssertShopSections(detail);
                }
                Click(lobby.Root, "Shop Category Upgrade");
                Click(lobby.Root, "Shop Skill 1");
                Canvas.ForceUpdateCanvases();
                Assert.That(Named(detail, "Shop Upgrade Preview").gameObject.activeInHierarchy, Is.True);
                Assert.That(Label(detail, "Shop Upgrade Preview").text, Does.Contain("4–5").And.Contain("6–7"));
                Assert.That(border.rect.height, Is.GreaterThan(purchaseHeight), "Upgrade reserves readable space only when preview is present.");
                Assert.That(Bounds(border, border.parent).yMax, Is.EqualTo(top).Within(.1f));
                AssertShopSections(detail);
                Click(lobby.Root, "Shop Skill 3");
                AssertShopSections(detail);
                Click(lobby.Root, "Shop Category Purchase");
                Click(lobby.Root, "Shop Skill 14");
                Assert.That(Label(detail, "Shop Detail Name").text, Is.EqualTo("가로베기"));
                Canvas.ForceUpdateCanvases();
                Assert.That(border.rect.height, Is.EqualTo(purchaseHeight).Within(.1f));
                Assert.That(Named(detail, "Shop Upgrade Preview").gameObject.activeInHierarchy, Is.False);
                Assert.That(run.Currency, Is.Zero); Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(run.HasLoadoutChanges, Is.False);
                Assert.That(run.OwnedSkills[0].Level, Is.Zero);
            }
            finally { lobby?.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        private static void AssertShopSections(GameObject detail)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform paper = Named(detail, "Shop Detail Surface").GetComponent<RectTransform>();
            RectTransform body = Named(detail, "Skill Summary").GetComponent<RectTransform>();
            AssertBodyFits(body, Label(detail, "Shop Detail Effect"));
            AssertInside(body, paper);
            AssertHeaderClear(detail, paper, "Shop Detail Name", "Shop Detail Purpose", "Shop Detail Icon");
            string[] names = { "Shop Detail Rule", "Shop Upgrade Preview", "Shop Price", "Shop Wallet", "Shop Availability", "Shop Primary Action" };
            foreach (string name in names)
            {
                RectTransform item = Named(detail, name).GetComponent<RectTransform>();
                if (!item.gameObject.activeInHierarchy) continue;
                AssertInside(item, paper); AssertNoOverlap(body, item, paper);
                foreach (string other in names)
                {
                    RectTransform next = Named(detail, other).GetComponent<RectTransform>();
                    if (item != next && next.gameObject.activeInHierarchy) AssertNoOverlap(item, next, paper);
                }
            }
            AssertThatTextFits(Label(detail, "Shop Availability"));
        }

        private static void AssertBodyFits(RectTransform body, Text effect)
        {
            AssertThatTextFits(effect); AssertInside(effect.rectTransform, body);
            Transform row = Named(body.gameObject, "Skill Keywords");
            if (!row.gameObject.activeInHierarchy) return;
            AssertInside(row.GetComponent<RectTransform>(), body);
            AssertNoOverlap(row.GetComponent<RectTransform>(), effect.rectTransform, body);
            foreach (Text text in row.GetComponentsInChildren<Text>())
            { AssertThatTextFits(text); AssertInside(text.rectTransform, text.transform.parent.GetComponent<RectTransform>()); }
        }

        private static void AssertHeaderClear(GameObject card, RectTransform paper, string name, string detail, string icon)
        {
            RectTransform body = Named(card, "Skill Summary").GetComponent<RectTransform>();
            RectTransform image = Named(card, icon).GetComponent<RectTransform>();
            AssertInside(image, paper); AssertNoOverlap(image, body, paper);
            foreach (string label in new[] { name, detail })
            {
                Text text = Label(card, label);
                AssertInside(text.rectTransform, paper);
                AssertNoOverlap(text.rectTransform, body, paper);
                foreach (string badge in new[] { "ACT Attachment", "Type Attachment" })
                {
                    Rect badgeBounds = Bounds(Named(card, badge).GetComponent<RectTransform>(), paper);
                    Assert.That(InkBounds(text, paper).Overlaps(badgeBounds), Is.False, label + " must not be covered by " + badge);
                    Assert.That(Bounds(image, paper).Overlaps(badgeBounds), Is.False, icon + " must not be covered by " + badge);
                }
            }
        }

        private static void AssertThatTextFits(Text text)
            => Assert.That(text.rectTransform.rect.height + 1f, Is.GreaterThanOrEqualTo(text.preferredHeight), text.name + " should show all measured text lines.");

        private static Rect InkBounds(Text text, Transform relativeTo)
        {
            Rect bounds = Bounds(text.rectTransform, relativeTo);
            float width = Mathf.Min(bounds.width, text.preferredWidth);
            bool right = text.alignment == TextAnchor.UpperRight || text.alignment == TextAnchor.MiddleRight || text.alignment == TextAnchor.LowerRight;
            bool left = text.alignment == TextAnchor.UpperLeft || text.alignment == TextAnchor.MiddleLeft || text.alignment == TextAnchor.LowerLeft;
            return new Rect(right ? bounds.xMax - width : left ? bounds.xMin : bounds.center.x - width * .5f, bounds.yMin, width, bounds.height);
        }

        private static void AssertInside(RectTransform child, RectTransform parent)
        {
            Rect c = Bounds(child, parent), p = parent.rect;
            Assert.That(c.xMin, Is.GreaterThanOrEqualTo(p.xMin - 1f), child.name);
            Assert.That(c.xMax, Is.LessThanOrEqualTo(p.xMax + 1f), child.name);
            Assert.That(c.yMin, Is.GreaterThanOrEqualTo(p.yMin - 1f), child.name);
            Assert.That(c.yMax, Is.LessThanOrEqualTo(p.yMax + 1f), child.name);
        }

        private static void AssertNoOverlap(RectTransform a, RectTransform b, Transform relativeTo)
        {
            Rect first = Bounds(a, relativeTo), second = Bounds(b, relativeTo);
            // A shared border is not an overlap; allow tiny canvas-rounding differences.
            first = Rect.MinMaxRect(first.xMin + .2f, first.yMin + .2f, first.xMax - .2f, first.yMax - .2f);
            Assert.That(first.Overlaps(second), Is.False, a.name + " must not overlap " + b.name);
        }

        private static void AssertOnScreen(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                Assert.That(screen.x, Is.InRange(-1f, Screen.width + 1f), rect.name);
                Assert.That(screen.y, Is.InRange(-1f, Screen.height + 1f), rect.name);
            }
        }

        private static Rect Bounds(RectTransform rect, Transform relativeTo)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            { Vector2 local = relativeTo.InverseTransformPoint(corner); min = Vector2.Min(min, local); max = Vector2.Max(max, local); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static LegacySkill[] AllSkills()
        {
            var skills = new LegacySkill[LegacyInitialSkills.All.Count + CampaignSkillCatalog.AcquisitionSkills.Count];
            int index = 0;
            foreach (LegacySkill skill in LegacyInitialSkills.All) skills[index++] = skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) skills[index++] = skill;
            return skills;
        }

        private static LegacySkill Skill(int id)
        { foreach (LegacySkill skill in AllSkills()) if (skill.Id == id) return skill; throw new InvalidOperationException("Missing skill " + id); }
        private static string Type(LegacySkillProperty property)
            => property == LegacySkillProperty.Slash ? "참격" : property == LegacySkillProperty.Hit ? "타격" : property == LegacySkillProperty.Penetrate ? "관통" : "방어";
        private static void Click(GameObject root, string name)
        {
            // Rebuilt cards are disabled before deferred destruction. Select only
            // the current visible button, rather than a same-named retired card.
            foreach (Button button in root.GetComponentsInChildren<Button>())
            {
                if (button.name != name) continue;
                button.onClick.Invoke();
                return;
            }
            Assert.Fail("Missing active compact card button: " + name);
        }
        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node;
            Assert.Fail("Missing compact skill card node: " + name); return null;
        }
    }
}
