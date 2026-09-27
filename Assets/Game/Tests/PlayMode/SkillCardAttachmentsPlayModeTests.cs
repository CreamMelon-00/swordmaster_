using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillCardAttachmentsPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly RectTransform Paper;
            public readonly SkillInfoView View;

            public Fixture(float width = 334f)
            {
                CanvasRoot = new GameObject("Skill Attachment Test Canvas", typeof(RectTransform), typeof(Canvas));
                CanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasRoot.GetComponent<Canvas>().sortingOrder = 600;
                Paper = new GameObject("Test Card Paper", typeof(RectTransform)).GetComponent<RectTransform>();
                Paper.SetParent(CanvasRoot.transform, false);
                Paper.anchorMin = Paper.anchorMax = Paper.pivot = Vector2.one * .5f;
                Paper.sizeDelta = new Vector2(width == 334f ? 380f : 460f, 392f);
                View = new SkillInfoView(Paper, new LegacyDuelArt().UIFont, Vector2.zero, width, "Test");
            }

            public void Dispose() => Object.Destroy(CanvasRoot);
        }

        [UnityTest]
        public IEnumerator AllFifteenSkills_ShowTheirLiveAttackTypeAndDistinctOpaqueTypeDyes()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                int[] ids = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 14, 15, 16, 17, 21, 32 };
                string[] types = { "참격", "참격", "관통", "관통", "타격", "타격", "방어", "방어", "방어", "참격", "참격", "참격", "방어", "관통", "방어" };
                var dyeByType = new Dictionary<string, Color>();
                for (int index = 0; index < ids.Length; index++)
                {
                    LegacySkill skill = Skill(ids[index]);
                    fixture.View.SetSkill(skill);
                    Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo(types[index]), skill.Name);
                    Assert.That(fixture.View.AttackTypeText.name, Is.EqualTo("Attack Type"));
                    Assert.That(Label(fixture.View.Root, "Type Label").text,
                        Is.EqualTo(skill.Kind == LegacySkillKind.Defence ? "기술 타입" : "공격 타입"), skill.Name);
                    var ribbon = Named(fixture.View.Root, "Type Attachment").GetComponent<SkillInfoAttachment>();
                    Assert.That(ribbon, Is.Not.Null);
                    Assert.That(ribbon.color.a, Is.EqualTo(1f).Within(.001f), "Type dye must remain opaque behind its text.");
                    if (dyeByType.TryGetValue(types[index], out Color dye)) Assert.That(ribbon.color, Is.EqualTo(dye));
                    else dyeByType.Add(types[index], ribbon.color);
                    fixture.View.SetSkill(skill, true);
                    Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo(types[index]), "Enemy information uses the same live type.");
                }
                Assert.That(new HashSet<Color>(dyeByType.Values).Count, Is.EqualTo(4), "Four attack types need four distinguishable dyes.");

                // The type comes from the active skill, not its icon, id, name, or a static lookup.
                var differentType = new LegacySkill(1, "베기", 1, 4, 5, LegacySkillKind.Attack,
                    LegacySkillProperty.Hit, 1, 0, string.Empty, iconId: 1);
                fixture.View.SetSkill(differentType);
                Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo("타격"));

                var run = new CampaignRun();
                CampaignOwnedSkill owned = run.OwnedSkills[0];
                fixture.View.SetSkill(owned.Skill);
                Assert.That(run.TryStartStage(1), Is.True);
                Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(run.ReturnToLobby(), Is.True);
                Assert.That(run.TryUpgradeSkill(owned.SkillId), Is.True);
                fixture.View.SetSkill(owned.Skill);
                Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo("참격"));
                Assert.That(fixture.View.PowerText.text, Is.EqualTo("6–7"), "The attachment reads the upgraded live copy.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Attachments_ReplaceTheThreeInnerTilesAndOverhangBothCardWidths()
        {
            yield return null;
            foreach (float width in new[] { 334f, 416f })
            {
                using (var fixture = new Fixture(width))
                {
                    fixture.View.SetSkill(Skill(3));
                    Canvas.ForceUpdateCanvases();
                    foreach (string oldTile in new[] { "ACT Tile", "Power Tile", "Hits Tile" })
                        Assert.That(Find(fixture.View.Root, oldTile), Is.Null, "Old divided stat tiles should no longer be created.");
                    Transform group = Named(fixture.View.Root, "Skill Attachments");
                    Assert.That(group.GetComponent<Graphic>(), Is.Null, "The attachments are separate silhouettes, not an inner panel.");
                    var attachments = fixture.View.Root.GetComponentsInChildren<SkillInfoAttachment>(true);
                    Assert.That(attachments.Length, Is.EqualTo(3));
                    Rect paper = LocalBounds(fixture.Paper, fixture.Paper);
                    foreach (string name in new[] { "ACT Attachment", "Power Attachment" })
                    {
                        Rect bounds = LocalBounds(Named(fixture.View.Root, name).GetComponent<RectTransform>(), fixture.Paper);
                        Assert.That(bounds.xMin, Is.LessThan(paper.xMin - 20f), name + " should visibly attach outside the left paper edge.");
                        Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(paper.xMin - SkillInfoView.AttachmentOverhang - 1f));
                    }
                    Rect type = LocalBounds(Named(fixture.View.Root, "Type Attachment").GetComponent<RectTransform>(), fixture.Paper);
                    Assert.That(type.xMax, Is.GreaterThan(paper.xMax + 20f), "The type ribbon should visibly attach outside the right edge.");
                    Assert.That(type.xMax, Is.LessThanOrEqualTo(paper.xMax + SkillInfoView.AttachmentOverhang + 1f));
                    Assert.That(Label(fixture.View.Root, "ACT Value").text, Is.EqualTo("1"));
                    Assert.That(fixture.View.PowerText.text, Is.EqualTo("5"));
                    Assert.That(Label(fixture.View.Root, "Hits Label").text, Is.EqualTo("타격"));
                    Assert.That(Label(fixture.View.Root, "Hits Value").text, Is.EqualTo("3회"));
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator DefenceType_SeparatesDefencePowerAndSameSlotResponseFromAttackDamage()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                foreach (int id in new[] { 7, 8, 9, 17, 32 })
                {
                    LegacySkill skill = Skill(id);
                    fixture.View.SetSkill(skill);
                    Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo("방어"), skill.Name);
                    Assert.That(Label(fixture.View.Root, "Type Label").text, Is.EqualTo("기술 타입"));
                    Assert.That(Label(fixture.View.Root, "Power Label").text, Does.Contain("방어"));
                    Assert.That(fixture.View.PowerText.text, Is.EqualTo(CampaignSkillText.Power(skill)));
                    Assert.That(Label(fixture.View.Root, "Hits Label").text, Is.EqualTo("대응"));
                    Assert.That(Label(fixture.View.Root, "Hits Value").text, Is.EqualTo("같은 칸"));
                    Assert.That(fixture.View.DamageText.text, Is.Empty);
                    Assert.That(fixture.View.DamageText.gameObject.activeInHierarchy, Is.False);
                }
                fixture.View.SetSkill(Skill(5));
                Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo("타격"));
                Assert.That(Label(fixture.View.Root, "Power Label").text, Is.EqualTo("위력"));
                Assert.That(Label(fixture.View.Root, "Hits Value").text, Is.EqualTo("2회"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearAndRepeatedSelection_ReuseAttachmentsAndNeverInterceptInput()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.View.SetSkill(Skill(1));
                var original = fixture.View.Root.GetComponentsInChildren<SkillInfoAttachment>(true);
                int nodes = fixture.View.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int iteration = 0; iteration < 40; iteration++)
                {
                    fixture.View.SetSkill(Skill(iteration % 2 == 0 ? 3 : 8));
                    if (iteration % 4 == 0) fixture.View.Clear();
                }
                fixture.View.SetSkill(Skill(7));
                Assert.That(fixture.View.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                CollectionAssert.AreEquivalent(original, fixture.View.Root.GetComponentsInChildren<SkillInfoAttachment>(true));
                foreach (Graphic graphic in fixture.View.Root.GetComponentsInChildren<Graphic>(true))
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name + " must not block clicks, holds or card drags.");
                foreach (string keyword in new[] { "Keyword 1", "Keyword 2" })
                    Assert.That(Named(fixture.View.Root, keyword).GetComponent<Image>().color.a, Is.EqualTo(1f).Within(.001f));

                fixture.View.Clear();
                AssertCleared(fixture.View);
                fixture.View.SetSkill(Skill(5));
                Assert.That(Named(fixture.View.Root, "Skill Attachments").gameObject.activeInHierarchy, Is.True);
                Assert.That(fixture.View.AttackTypeText.text, Is.EqualTo("타격"));
                fixture.View.SetSkill(null);
                AssertCleared(fixture.View);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualHud_ClampsTheExternalAttachmentsAtEveryScreenCornerForBothSides()
        {
            yield return null;
            var parent = new GameObject("External Skill Explanation HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                Canvas.ForceUpdateCanvases();
                var selected = hud.Root.transform.Find("Input/Keys/Current Q/Skill Image").GetComponent<RectTransform>();
                Vector3 original = selected.position;
                Vector2[] corners =
                {
                    new Vector2(4, 4), new Vector2(Screen.width - 4, 4),
                    new Vector2(4, Screen.height - 4), new Vector2(Screen.width - 4, Screen.height - 4),
                };
                foreach (Vector2 corner in corners)
                {
                    selected.position = new Vector3(corner.x, corner.y, original.z);
                    hud.ShowExplanation(Skill(1), false);
                    Canvas.ForceUpdateCanvases();
                    AssertExplanationBounds(hud.Root.transform.Find("Skill Explain").gameObject, "참격");
                }
                // A compact reference canvas pushes the enemy's fixed inspection anchor toward the top-left edge.
                hud.Root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(900f, 550f);
                yield return null;
                Canvas.ForceUpdateCanvases();
                hud.ShowExplanation(Skill(3), true);
                Canvas.ForceUpdateCanvases();
                AssertExplanationBounds(hud.Root.transform.Find("Enemy Skill Explain").gameObject, "관통");
                hud.HideExplanation();
                Assert.That(hud.Root.transform.Find("Skill Explain").gameObject.activeSelf, Is.False);
                Assert.That(hud.Root.transform.Find("Enemy Skill Explain").gameObject.activeSelf, Is.False);
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualLoadoutAndShop_TypeAndValuesFollowSelectionWithoutChangingTheRun()
        {
            yield return null;
            var parent = new GameObject("Attached Skill Information Lobby Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                int[] originalLoadout = EquippedIds(run);
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run);
                lobby.ShowTab(LobbyTab.Loadout);
                yield return null;
                GameObject detail = Named(lobby.Root, "Loadout Selected Detail").gameObject;
                foreach (var selection in new[]
                {
                    new[] { "Loadout Slot Q 1", "참격", "4–5", "1" },
                    new[] { "Loadout Slot W 1", "관통", "5", "1" },
                    new[] { "Loadout Slot E 1", "타격", "6–9", "1" },
                    new[] { "Loadout Slot Q 3", "방어", "5–8", "1" },
                })
                {
                    Click(lobby.Root, selection[0]);
                    Assert.That(Label(detail, "Attack Type").text, Is.EqualTo(selection[1]));
                    Assert.That(Label(detail, "Loadout Detail Values").text, Is.EqualTo(selection[2]));
                    Assert.That(Label(detail, "ACT Value").text, Is.EqualTo(selection[3]));
                }
                lobby.ShowTab(LobbyTab.Shop);
                yield return null;
                Click(lobby.Root, "Shop Skill 16");
                detail = Named(lobby.Root, "Shop Selected Detail").gameObject;
                Assert.That(Label(detail, "Attack Type").text, Is.EqualTo("참격"));
                Assert.That(Label(detail, "ACT Value").text, Is.EqualTo("5"));
                Assert.That(Label(detail, "Shop Detail Values").text, Is.EqualTo("3–20"));
                Click(lobby.Root, "Shop Category Upgrade");
                Click(lobby.Root, "Shop Skill 8");
                Assert.That(Label(detail, "Attack Type").text, Is.EqualTo("방어"));
                Assert.That(Label(detail, "Shop Detail Values").text, Is.EqualTo("7–11"));
                Assert.That(run.Currency, Is.Zero, "Reading details never buys or upgrades a skill.");
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(run.HasLoadoutChanges, Is.False, "Reading details never moves a loadout slot.");
                CollectionAssert.AreEqual(originalLoadout, EquippedIds(run));
            }
            finally
            {
                lobby?.Dispose();
                Object.Destroy(parent);
            }
            yield return null;
        }

        private static void AssertCleared(SkillInfoView view)
        {
            Assert.That(Named(view.Root, "Skill Attachments").gameObject.activeInHierarchy, Is.False);
            Assert.That(view.AttackTypeText.text, Is.Empty);
            Assert.That(view.PowerText.text, Is.Empty);
            Assert.That(Label(view.Root, "ACT Value").text, Is.Empty);
            Assert.That(Label(view.Root, "Hits Value").text, Is.Empty);
        }

        private static void AssertExplanationBounds(GameObject explanation, string type)
        {
            Assert.That(explanation.activeInHierarchy, Is.True);
            Assert.That(Label(explanation, "Attack Type").text, Is.EqualTo(type));
            AssertOnScreen(explanation.GetComponent<RectTransform>());
            SkillInfoAttachment[] attachments = explanation.GetComponentsInChildren<SkillInfoAttachment>();
            Assert.That(attachments.Length, Is.EqualTo(3));
            foreach (SkillInfoAttachment attachment in attachments) AssertOnScreen(attachment.rectTransform);
        }

        private static void AssertOnScreen(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                Assert.That(screen.x, Is.InRange(-1f, Screen.width + 1f), rect.name + " horizontal clipping");
                Assert.That(screen.y, Is.InRange(-1f, Screen.height + 1f), rect.name + " vertical clipping");
            }
        }

        private static Rect LocalBounds(RectTransform rect, RectTransform relativeTo)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = relativeTo.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static int[] EquippedIds(CampaignRun run)
        {
            var ids = new int[9];
            for (int lane = 0; lane < 3; lane++)
                for (int slot = 0; slot < 3; slot++) ids[lane * 3 + slot] = run.GetEquippedLane(lane)[slot].SkillId;
            return ids;
        }

        private static LegacySkill Skill(int id)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
            throw new InvalidOperationException("Missing authored skill " + id);
        }

        private static void Click(GameObject root, string name) => Named(root, name).GetComponent<Button>().onClick.Invoke();
        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Transform Find(GameObject root, string name)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node;
            return null;
        }

        private static Transform Named(GameObject root, string name)
        {
            Transform node = Find(root, name);
            Assert.That(node, Is.Not.Null, "Missing skill attachment node: " + name);
            return node;
        }
    }
}
