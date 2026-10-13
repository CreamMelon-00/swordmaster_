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
    public sealed class CampaignCurriculumHudPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly RectTransform Panel;
            public readonly CampaignRun Run;
            public readonly LegacyDuelArt Art = new LegacyDuelArt();
            public readonly CampaignCurriculumHud.ViewState State = new CampaignCurriculumHud.ViewState();
            private readonly GameObject eventSystem;
            private readonly bool callbacks;
            public CampaignCurriculumHud Hud;
            public int SelectCalls, ResetCalls;

            public Fixture(bool callbacks = true, CurriculumTree tree = null)
            {
                this.callbacks = callbacks;
                Run = tree == null ? new CampaignRun() : new CampaignRun(tree);
                if (EventSystem.current == null)
                    eventSystem = new GameObject("Curriculum View Test Event System", typeof(EventSystem));
                CanvasRoot = new GameObject("Curriculum View Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
                var canvas = CanvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 600;
                var scaler = CanvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = .5f;
                Panel = new GameObject("Curriculum Test Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                Panel.SetParent(CanvasRoot.transform, false);
                Panel.anchorMin = Panel.anchorMax = Panel.pivot = Vector2.one * .5f;
                Panel.sizeDelta = new Vector2(1300f, 850f);
                Build();
            }

            public GameObject Root => Hud.Root;

            public void Build()
            {
                Hud?.Dispose();
                Action<string> select = null;
                Action reset = null;
                if (callbacks)
                {
                    select = id => { SelectCalls++; Assert.That(Run.TrySelectCurriculumNode(id), Is.True); };
                    reset = () => { ResetCalls++; Assert.That(Run.TryResetCurriculum(), Is.True); };
                }
                Hud = new CampaignCurriculumHud(Panel, Art, Run, select, reset, State);
                Canvas.ForceUpdateCanvases();
            }

            /// <summary>Makes the node the one in progress (unless it already is) and finishes one battle with it.</summary>
            public void Complete(string id, DuelMatchOutcome outcome = DuelMatchOutcome.PlayerVictory)
            {
                if (Run.Curriculum.Active?.Id != id) Assert.That(Run.TrySelectCurriculumNode(id), Is.True, "Select " + id);
                Assert.That(Run.TryStartStage(1), Is.True);
                Assert.That(Run.TryCompleteBattle(outcome), Is.True);
                Assert.That(Run.ReturnToLobby(), Is.True);
                Assert.That(Run.Curriculum.IsCompleted(id), Is.True, "Complete " + id);
                Hud.Refresh();
            }

            public void CompleteAll(params string[] ids)
            {
                foreach (string id in ids) Complete(id, DuelMatchOutcome.EnemyVictory);
            }

            public void Dispose()
            {
                Hud?.Dispose();
                Object.Destroy(CanvasRoot);
                if (eventSystem != null) Object.Destroy(eventSystem);
                Art.Dispose();
            }
        }

        // A small tree with a 택1 pair, for the rules the default tree no longer uses.
        private static CurriculumTree ExclusiveTree() => new CurriculumTree(new[]
        {
            new CurriculumNode("root", null, CurriculumBranch.Foundation, 1f, 0, new[] { 14 }),
            new CurriculumNode("left", null, CurriculumBranch.Collapse, 0f, 1, new[] { 42 }, requiresAll: new[] { "root" },
                exclusiveWith: new[] { "right" }),
            new CurriculumNode("right", null, CurriculumBranch.Retraction, 2f, 1, new[] { 32 }, requiresAll: new[] { "root" }),
            new CurriculumNode("after", null, CurriculumBranch.Collapse, 0f, 2, new[] { 19 }, requiresAny: new[] { "left", "right" }),
        });

        [UnityTest]
        public IEnumerator StatRewards_ShowWithoutSkillIconsAndDoNotResizeTheDetail()
        {
            yield return null;
            var tree = new CurriculumTree(new[]
            {
                new CurriculumNode("skill", "가로베기", CurriculumBranch.Circulation, 0f, 0, new[] { 14 }),
                new CurriculumNode("stats", "기초 체력", CurriculumBranch.Retraction, 1f, 0, Array.Empty<int>(),
                    statReward: new CurriculumStatReward(health: 5, resistance: 4, actGain: 1,
                        actCapacity: 2, planningSeconds: 3)),
                new CurriculumNode("mixed", "종합 훈련", CurriculumBranch.Retraction, 2f, 0, new[] { 15 },
                    statReward: new CurriculumStatReward(resistance: 2)),
            });
            using (var fixture = new Fixture(tree: tree))
            {
                GameObject root = fixture.Root;
                var panel = (RectTransform)Named(root, "Curriculum Selected Detail");
                float fixedHeight = panel.rect.height;
                fixture.Hud.SelectNode("stats");
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.rect.height, Is.EqualTo(fixedHeight).Within(.1f));
                Assert.That(Named(root, "Curriculum Detail Icon").GetComponent<Image>().enabled, Is.False);
                Assert.That(Named(Named(root, "Curriculum Node stats").gameObject, "Node Icon")
                    .GetComponent<Image>().enabled, Is.False);
                Assert.That(Label(Named(root, "Curriculum Node stats").gameObject, "Node Lane").text,
                    Is.EqualTo("능력치"));
                Assert.That(Label(root, "Curriculum Detail Heading").text, Is.EqualTo("능력치 보상"));
                string reward = Label(root, "Curriculum Detail Effect").text;
                Assert.That(reward, Does.Contain("최대 체력 +5").And.Contain("최대 저항 +4")
                    .And.Contain("매 턴 ACT 회복 +1").And.Contain("ACT 상한 +2")
                    .And.Contain("일반 스테이지 선택 시간 +3초"));
                Assert.That(Label(root, "Curriculum Detail Effect").rectTransform.rect.height,
                    Is.GreaterThanOrEqualTo(Label(root, "Curriculum Detail Effect").preferredHeight - 1f));

                fixture.Hud.SelectNode("mixed");
                Assert.That(panel.rect.height, Is.EqualTo(fixedHeight).Within(.1f));
                Assert.That(Label(root, "Curriculum Detail Effect").text,
                    Does.Contain("능력치 보상: 최대 저항 +2"));
                fixture.Hud.SelectNode("stats");
                Assert.That(fixture.Run.TrySelectCurriculumNode("stats"), Is.True);
                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                Assert.That(fixture.Run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                fixture.Hud.Refresh();
                Assert.That(Label(root, "Curriculum Availability").text, Does.Contain("능력치 보상이 적용됐습니다"));
                Assert.That(panel.rect.height, Is.EqualTo(fixedHeight).Within(.1f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NodeCards_ShowLockedAvailableActiveAndCompletedWithTheirColorsAndStatus()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                int total = fixture.Run.Curriculum.Tree.Nodes.Count;
                AssertCard(root, "horizontal-cut", DuelVisualTheme.Card, "전투 1회", DuelVisualTheme.Muted, false);
                AssertCard(root, "preparation", DuelVisualTheme.Track, "잠김", DuelVisualTheme.Muted, true);
                AssertCard(root, "advance", DuelVisualTheme.Track, "잠김", DuelVisualTheme.Muted, true);
                AssertCard(root, "quick-draw", DuelVisualTheme.Track, "잠김", DuelVisualTheme.Muted, true);
                Assert.That(Label(root, "Curriculum Status").text, Is.EqualTo("진행 중인 과정 없음 · 과정을 고르세요"));
                Assert.That(Label(root, "Curriculum Status").color, Is.EqualTo(DuelVisualTheme.Danger));
                Assert.That(Label(root, "Curriculum Completed Count").text, Is.EqualTo("완료 0 / " + total));

                Assert.That(fixture.Run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
                fixture.Hud.Refresh();
                AssertCard(root, "horizontal-cut", DuelVisualTheme.Selected, "진행 중 0/1", DuelVisualTheme.Accent, false);
                Assert.That(Label(root, "Curriculum Status").text, Is.EqualTo("진행 중  가로베기  0/1"));
                Assert.That(Label(root, "Curriculum Status").color, Is.EqualTo(DuelVisualTheme.Accent));

                // Every finished battle counts, whatever its outcome.
                fixture.Complete("horizontal-cut", DuelMatchOutcome.EnemyVictory);
                fixture.Complete("preparation", DuelMatchOutcome.Draw);
                fixture.Complete("advance");
                AssertCard(root, "horizontal-cut", DuelVisualTheme.RaisedSurface, "완료", DuelVisualTheme.Accent, false);
                AssertCard(root, "preparation", DuelVisualTheme.RaisedSurface, "완료", DuelVisualTheme.Accent, false);
                AssertCard(root, "advance", DuelVisualTheme.RaisedSurface, "완료", DuelVisualTheme.Accent, false);
                foreach (string school in new[] { "quick-draw", "diagonal-cut", "suppleness" })
                    AssertCard(root, school, DuelVisualTheme.Card, "전투 1회", DuelVisualTheme.Muted, false);
                AssertCard(root, "vital-thrust", DuelVisualTheme.Track, "잠김", DuelVisualTheme.Muted, true);
                Assert.That(Label(root, "Curriculum Completed Count").text, Is.EqualTo("완료 3 / " + total));
                Assert.That(Label(root, "Curriculum Status").text, Is.EqualTo("진행 중인 과정 없음 · 과정을 고르세요"));
            }
        }

        [UnityTest]
        public IEnumerator ExclusivePairs_StillTagAndCloseTheirPartner_InATreeThatHasThem()
        {
            yield return null;
            using (var fixture = new Fixture(tree: ExclusiveTree()))
            {
                GameObject root = fixture.Root;
                var paired = new HashSet<string> { "left", "right" };
                foreach (CurriculumNode node in fixture.Run.Curriculum.Tree.Nodes)
                {
                    Transform tag = TryNamed(Button(root, "Curriculum Node " + node.Id).gameObject, "Node Exclusive");
                    Assert.That(tag != null, Is.EqualTo(paired.Contains(node.Id)), "Only paired choices carry the tag: " + node.Id);
                    if (tag != null) Assert.That(tag.GetComponent<Text>().text, Is.EqualTo("택1"));
                }
                Text exclusive = Label(root, "Curriculum Exclusive");
                fixture.Hud.SelectNode("left");
                Assert.That(exclusive.gameObject.activeSelf, Is.True);
                Assert.That(exclusive.text, Is.EqualTo("택1  " + KoreanParticle.Attach(fixture.Run.Curriculum.Tree.Find("right").Title, "과")
                    + " 함께 고를 수 없음"));
                fixture.Complete("root");
                fixture.Complete("right");
                AssertCard(root, "left", DuelVisualTheme.Track, "닫힘", DuelVisualTheme.Danger, true);
                Button primary = Button(root, "Curriculum Primary Action");
                Button(root, "Curriculum Node left").onClick.Invoke();
                AssertPrimary(primary, "닫힌 과정", false);
                Assert.That(Label(root, "Curriculum Availability").text,
                    Is.EqualTo("함께 고를 수 없는 과정을 이미 마쳤습니다. 초기화하면 다시 고를 수 있습니다."));
                primary.onClick.Invoke();
                Assert.That(fixture.Run.Curriculum.Active, Is.Null, "An excluded node cannot be started.");
            }
        }

        [UnityTest]
        public IEnumerator Requirements_NameAllOrAnyPrerequisites_AndTheDefaultTreeHasNoPairedChoice()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                foreach (CurriculumNode node in fixture.Run.Curriculum.Tree.Nodes)
                    Assert.That(TryNamed(Button(root, "Curriculum Node " + node.Id).gameObject, "Node Exclusive"), Is.Null, node.Id);
                Text requirement = Label(root, "Curriculum Requirement");
                Text exclusive = Label(root, "Curriculum Exclusive");
                fixture.Hud.SelectNode("horizontal-cut");
                Assert.That(requirement.text, Is.EqualTo("선행 과정 없음"));
                Assert.That(exclusive.gameObject.activeSelf, Is.False);
                fixture.Hud.SelectNode("diagonal-cut");
                Assert.That(requirement.text, Is.EqualTo("선행  플레슈"));
                fixture.Hud.SelectNode("estocada");
                Assert.That(requirement.text, Is.EqualTo("선행  르프리즈 또는 에스키브"),
                    "Either twist opens the capstone, so the text says 'or'.");
                fixture.Hud.SelectNode("battement");
                Assert.That(requirement.text, Is.EqualTo("선행  에스키브, 급소 찌르기"), "A bridge needs both.");
                Assert.That(exclusive.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator AnyConnectors_NeverCrossASolidLink_AndSolidLinksDrawOverSharedStems()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Transform content = Named(fixture.Root, "Curriculum Tree Content");
                var muted = new List<RectTransform>();
                var solid = new List<RectTransform>();
                foreach (Transform child in content)
                {
                    if (!child.name.StartsWith("Connector ", StringComparison.Ordinal)) continue;
                    Color color = child.GetComponent<Image>().color;
                    if (color == DuelVisualTheme.Muted) muted.Add((RectTransform)child);
                    else if (color == DuelVisualTheme.Border) solid.Add((RectTransform)child);
                }
                Assert.That(muted, Is.Not.Empty, "The default tree has 'any' links.");
                int lastMuted = 0, firstSolid = int.MaxValue;
                foreach (RectTransform piece in muted) lastMuted = Mathf.Max(lastMuted, piece.GetSiblingIndex());
                foreach (RectTransform piece in solid) firstSolid = Mathf.Min(firstSolid, piece.GetSiblingIndex());
                Assert.That(firstSolid, Is.GreaterThan(lastMuted), "A stem shared with an 'all' link shows the solid colour.");
                foreach (RectTransform across in muted)
                {
                    if (!across.name.EndsWith(" Across", StringComparison.Ordinal)) continue;
                    Rect bus = Bounds(across);
                    foreach (RectTransform piece in solid)
                    {
                        if (piece.name.EndsWith(" Down", StringComparison.Ordinal)) continue;
                        Assert.That(bus.Overlaps(Bounds(piece)), Is.False,
                            across.name + " must not cross " + piece.name + ", or it reads as part of that link.");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Connectors_RunFromEveryPrerequisitesBottomToItsNodesTopBehindTheCards()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Transform content = Named(fixture.Root, "Curriculum Tree Content");
                CurriculumTree source = fixture.Run.Curriculum.Tree;
                int firstCard = int.MaxValue;
                foreach (CurriculumNode node in source.Nodes)
                    firstCard = Mathf.Min(firstCard, Named(content.gameObject, "Curriculum Node " + node.Id).GetSiblingIndex());

                int expected = 0, links = 0;
                foreach (CurriculumNode node in source.Nodes)
                {
                    foreach (string id in node.RequiresAll)
                    {
                        expected += AssertConnector(content, source.Find(id), node, DuelVisualTheme.Border, firstCard);
                        links++;
                    }
                    foreach (string id in node.RequiresAny)
                    {
                        expected += AssertConnector(content, source.Find(id), node, DuelVisualTheme.Muted, firstCard);
                        links++;
                    }
                }
                int pieces = 0;
                foreach (Transform child in content)
                    if (child.name.StartsWith("Connector ", StringComparison.Ordinal)) pieces++;
                Assert.That(links, Is.EqualTo(21), "Fifteen 'all' links and six 'any' links in the 17-node tree.");
                Assert.That(expected, Is.EqualTo(60), "Three of them are straight (two pieces); the rest turn (three).");
                Assert.That(pieces, Is.EqualTo(expected), "Only prerequisites draw connectors.");
            }
        }

        [UnityTest]
        public IEnumerator Detail_ShowsEveryNodesGrantedSkillThroughTheSharedSkillInfoView()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                Image icon = Named(root, "Curriculum Detail Icon").GetComponent<Image>();
                CurriculumTree tree = fixture.Run.Curriculum.Tree;
                foreach (CurriculumNode node in tree.Nodes)
                {
                    Assert.That(node.SkillIds.Count, Is.EqualTo(1), node.Id);
                    LegacySkill skill = CatalogSkill(node.SkillIds[0]);
                    Button card = Button(root, "Curriculum Node " + node.Id);
                    card.onClick.Invoke();
                    Assert.That(fixture.State.SelectedNodeId, Is.EqualTo(node.Id));
                    Assert.That(Label(root, "Curriculum Detail Name").text, Is.EqualTo(node.Title));
                    Assert.That(Label(root, "Curriculum Detail Purpose").text, Is.EqualTo(CampaignCurriculumHud.PurposeText(tree, node)));
                    Assert.That(Label(root, "Curriculum Detail Purpose").text, Does.StartWith(CampaignCurriculumHud.BranchName(node.Branch) + " 과정"));
                    Assert.That(Label(root, "Curriculum Detail Heading").text, Is.EqualTo(SkillLaneStyle.FullName(skill.LaneIndex)));
                    Assert.That(Label(root, "Curriculum Detail Values").text, Is.EqualTo(CampaignSkillText.Power(skill)), node.Id);
                    string effect = SkillInfoContent.For(skill, false).Description;
                    Assert.That(Label(root, "Curriculum Detail Effect").text,
                        Is.EqualTo(string.IsNullOrWhiteSpace(effect) ? "추가 효과 없음" : effect), node.Id);
                    Assert.That(Named(root, "Skill Keywords").gameObject.activeSelf, Is.True, node.Id);
                    Assert.That(icon.enabled, Is.True, node.Id);
                    Assert.That(icon.sprite, Is.Not.Null, node.Id);
                    Assert.That(icon.sprite, Is.SameAs(fixture.Art.GetSkillIcon(skill.IconId)), node.Id);
                    Assert.That(Label(card.gameObject, "Node Title").text, Is.EqualTo(node.Title));
                    Assert.That(Label(card.gameObject, "Node Lane").text, Is.EqualTo(SkillLaneStyle.Key(skill.LaneIndex)));
                    Assert.That(Named(card.gameObject, "Node Icon").GetComponent<Image>().sprite, Is.SameAs(icon.sprite));
                }
                Assert.That(CampaignCurriculumHud.PurposeText(tree, tree.Find("battement")),
                    Is.EqualTo("다리 과정  ·  붕괴 × 순환  ·  전투 1회"));
                Assert.That(CampaignCurriculumHud.PurposeText(tree, tree.Find("whetstone")),
                    Is.EqualTo("다리 과정  ·  순환 × 판짜기  ·  전투 1회"));
                Assert.That(CampaignCurriculumHud.PurposeText(tree, tree.Find("quick-steps")), Is.EqualTo("순환 과정  ·  전투 1회"));
                Assert.That(CampaignCurriculumHud.PurposeText(tree, tree.Find("cadence")), Is.EqualTo("순환 과정 정수  ·  전투 1회"));
                Assert.That(CampaignCurriculumHud.PurposeText(tree, tree.Find("vital-thrust")), Is.EqualTo("순환 과정  ·  전투 1회"));
                Assert.That(Label(root, "Curriculum Damage Hint").text, Is.Empty);
                Assert.That(fixture.SelectCalls, Is.Zero, "Browsing details must not start any node.");
                Assert.That(fixture.Run.Curriculum.Active, Is.Null);
                Assert.That(fixture.Run.OwnedSkills.Count, Is.EqualTo(9));
            }
        }

        [UnityTest]
        public IEnumerator CompletedSkillNode_ShowsOwnedLevelAndExperience_WhileFutureRewardsStayAtBasePower()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.Complete("horizontal-cut");
                CampaignOwnedSkill owned = fixture.Run.GetOwnedSkill(14);
                for (int clash = 0; clash < owned.ExperienceRequired; clash++)
                    Assert.That(fixture.Run.TryGainClashExperience(owned.Skill, LegacySkillDefinitions.Skill(1)), Is.True);
                fixture.Hud.Refresh();
                fixture.Hud.SelectNode("horizontal-cut");

                Assert.That(Label(fixture.Root, "Curriculum Detail Name").text, Is.EqualTo("가로베기+"));
                Assert.That(Label(fixture.Root, "Curriculum Detail Values").text, Is.EqualTo("7–11"));
                Assert.That(Label(fixture.Root, "Skill Experience Level").text, Is.EqualTo("숙련 1/3"));
                Assert.That(Named(fixture.Root, "Skill Experience").gameObject.activeInHierarchy, Is.True);
                Assert.That(Label(Named(fixture.Root, "Curriculum Node horizontal-cut").gameObject, "Node Title").text,
                    Is.EqualTo("가로베기+"));

                fixture.Hud.SelectNode("diagonal-cut");
                Assert.That(Label(fixture.Root, "Curriculum Detail Name").text, Is.EqualTo("사선베기"));
                Assert.That(Named(fixture.Root, "Skill Experience").gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator PrimaryAction_FollowsTheSelectedNodesStateAndStartsItOnlyThroughOneExplicitClick()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                Button primary = Button(root, "Curriculum Primary Action");
                Text availability = Label(root, "Curriculum Availability");
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("horizontal-cut"), "The first available node is shown by default.");
                AssertPrimary(primary, "이 과정 진행", true);
                Assert.That(availability.text, Does.StartWith("진행하면 다음 전투부터 반영됩니다."));

                Button(root, "Curriculum Node diagonal-cut").onClick.Invoke();
                AssertPrimary(primary, "잠긴 과정", false);
                Assert.That(availability.text, Is.EqualTo("선행 과정을 먼저 마치세요."));
                primary.onClick.Invoke();
                Assert.That(fixture.SelectCalls, Is.Zero, "A locked node cannot be started.");

                fixture.CompleteAll("horizontal-cut", "preparation", "advance");
                Button(root, "Curriculum Node quick-draw").onClick.Invoke();
                Assert.That(fixture.SelectCalls, Is.Zero, "Clicking a node only selects it for viewing.");
                Assert.That(fixture.Run.Curriculum.Active, Is.Null);
                AssertPrimary(primary, "이 과정 진행", true);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primary.gameObject);
                primary.onClick.Invoke();
                if (EventSystem.current != null) Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                Assert.That(fixture.SelectCalls, Is.EqualTo(1));
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("quick-draw"));
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("quick-draw"));
                AssertPrimary(primary, "진행 중", false);
                Assert.That(availability.text, Is.EqualTo("진행 중 · 전투 0/1. 전투를 마치면 기술을 얻습니다."));
                primary.onClick.Invoke();
                Assert.That(fixture.SelectCalls, Is.EqualTo(1));

                Button(root, "Curriculum Node diagonal-cut").onClick.Invoke();
                AssertPrimary(primary, "이 과정 진행", true);
                Assert.That(availability.text, Is.EqualTo("진행 중인 쿠페 대신 이 과정을 진행합니다."),
                    "Before a battle counts, the choice can still change.");
                primary.onClick.Invoke();
                Assert.That(fixture.SelectCalls, Is.EqualTo(2));
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("diagonal-cut"));
                Assert.That(fixture.Run.Curriculum.GetState("quick-draw"), Is.EqualTo(CurriculumNodeState.Available));

                fixture.Complete("diagonal-cut", DuelMatchOutcome.EnemyVictory);
                Button(root, "Curriculum Node diagonal-cut").onClick.Invoke();
                AssertPrimary(primary, "완료한 과정", false);
                Assert.That(availability.text, Is.EqualTo("완료했습니다. 얻은 기술은 편성에서 장착하세요."));
                Assert.That(fixture.Run.GetOwnedSkill(15), Is.Not.Null);
                Assert.That(fixture.Run.GetOwnedSkill(43), Is.Null,
                    "A lost stage battle advances curriculum but does not grant the stage-clear skill.");
                Assert.That(fixture.Run.IsSkillInLoadout(15), Is.False, "A granted skill is not placed automatically.");
                Assert.That(fixture.Run.IsSkillEquipped(15), Is.False);
                Assert.That(fixture.Run.HasLoadoutChanges, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Actions_WithoutCallbacksUseTheRunAndLockOutsideTheLobbyAndMaintenance()
        {
            yield return null;
            using (var fixture = new Fixture(false))
            {
                GameObject root = fixture.Root;
                Button primary = Button(root, "Curriculum Primary Action");
                Button reset = Button(root, "Curriculum Reset");
                primary.onClick.Invoke();
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("horizontal-cut"),
                    "Without a callback the page asks the run directly.");
                AssertPrimary(primary, "진행 중", false);

                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                fixture.Hud.Refresh();
                AssertLocked(root, primary, reset);
                Button(root, "Curriculum Node preparation").onClick.Invoke();
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("preparation"), "Nodes stay viewable outside the lobby.");
                AssertLocked(root, primary, reset);
                primary.onClick.Invoke();
                reset.onClick.Invoke();
                reset.onClick.Invoke();
                Assert.That(fixture.Hud.IsResetArmed, Is.False);
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("horizontal-cut"));

                Assert.That(fixture.Run.TryCompleteBattle(DuelMatchOutcome.EnemyVictory), Is.True);
                Assert.That(fixture.Run.Phase, Is.EqualTo(CampaignPhase.Failed));
                fixture.Hud.Refresh();
                AssertLocked(root, primary, reset);
                Assert.That(fixture.Run.Curriculum.IsCompleted("horizontal-cut"), Is.True, "A lost battle still counts.");

                Assert.That(fixture.Run.ReturnToLobby(), Is.True);
                fixture.Hud.Refresh();
                AssertPrimary(primary, "이 과정 진행", true);
                primary.onClick.Invoke();
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("preparation"));

                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                Assert.That(fixture.Run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
                Assert.That(fixture.Run.Phase, Is.EqualTo(CampaignPhase.Maintenance));
                fixture.Hud.Refresh();
                Button(root, "Curriculum Node advance").onClick.Invoke();
                AssertPrimary(primary, "이 과정 진행", true);
                primary.onClick.Invoke();
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("advance"), "Maintenance still allows a choice.");

                Assert.That(reset.interactable, Is.True);
                reset.onClick.Invoke();
                Assert.That(fixture.Hud.IsResetArmed, Is.True);
                Assert.That(fixture.Run.Curriculum.CompletedCount, Is.EqualTo(2));
                reset.onClick.Invoke();
                Assert.That(fixture.Hud.IsResetArmed, Is.False);
                Assert.That(fixture.Run.Curriculum.CompletedCount, Is.Zero);
                Assert.That(fixture.Run.Curriculum.Active, Is.Null);
                Assert.That(fixture.Run.OwnedSkills.Count, Is.EqualTo(10),
                    "Reset removes curriculum skills but keeps the first stage reward.");
            }
        }

        [UnityTest]
        public IEnumerator Reset_NeedsTwoClicksDisarmsOnPhaseChangeAndRemovesGrantedSkills()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                Button reset = Button(root, "Curriculum Reset");
                Text caption = reset.GetComponentInChildren<Text>();
                Assert.That(reset.interactable, Is.False, "A fresh curriculum has nothing to reset.");
                Assert.That(caption.text, Is.EqualTo("커리큘럼 초기화"));
                reset.onClick.Invoke();
                Assert.That(fixture.Hud.IsResetArmed, Is.False, "A disabled reset cannot be armed.");
                Assert.That(fixture.ResetCalls, Is.Zero);

                fixture.Complete("horizontal-cut");
                fixture.Complete("preparation");
                Assert.That(fixture.Run.TryPlaceLoadoutSkill(14, 0, 0), Is.True);
                Assert.That(fixture.Run.TrySaveLoadout(), Is.True);
                Assert.That(fixture.Run.IsSkillEquipped(14), Is.True);
                Assert.That(fixture.Run.TrySelectCurriculumNode("advance"), Is.True);
                fixture.Hud.Refresh();
                Assert.That(reset.interactable, Is.True);
                Assert.That(caption.color, Is.EqualTo(DuelVisualTheme.Foreground));

                reset.onClick.Invoke();
                Assert.That(fixture.Hud.IsResetArmed, Is.True);
                Assert.That(caption.text, Is.EqualTo("한 번 더 누르면 초기화"));
                Assert.That(fixture.ResetCalls, Is.Zero, "The first click only arms the reset.");
                Assert.That(fixture.Run.Curriculum.CompletedCount, Is.EqualTo(2));

                Assert.That(fixture.Run.TryStartStage(1), Is.True);
                fixture.Hud.Refresh();
                Assert.That(fixture.Hud.IsResetArmed, Is.False, "Leaving the lobby disarms the reset.");
                Assert.That(reset.interactable, Is.False);
                Assert.That(caption.color, Is.EqualTo(DuelVisualTheme.Muted));
                Assert.That(fixture.Run.TryAbandonBattle(), Is.True);
                fixture.Hud.Refresh();
                Assert.That(caption.text, Is.EqualTo("커리큘럼 초기화"));
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("advance"));
                Assert.That(fixture.Run.Curriculum.ActiveBattles, Is.Zero, "An abandoned battle does not count.");

                reset.onClick.Invoke();
                reset.onClick.Invoke();
                Assert.That(fixture.ResetCalls, Is.EqualTo(1));
                Assert.That(fixture.Hud.IsResetArmed, Is.False);
                Assert.That(caption.text, Is.EqualTo("커리큘럼 초기화"));
                Assert.That(reset.interactable, Is.False);
                Assert.That(fixture.Run.Curriculum.CompletedCount, Is.Zero);
                Assert.That(fixture.Run.Curriculum.Active, Is.Null);
                Assert.That(fixture.Run.OwnedSkills.Count, Is.EqualTo(10),
                    "Reset keeps the first stage reward even after curriculum skills are removed.");
                Assert.That(fixture.Run.IsSkillEquipped(14), Is.False);
                Assert.That(fixture.Run.EquippedSkillCount, Is.EqualTo(9), "The emptied lane is refilled with a starting skill.");
                Assert.That(fixture.Run.CanStartStage(1), Is.True);
                Assert.That(Label(root, "Curriculum Completed Count").text,
                    Is.EqualTo("완료 0 / " + fixture.Run.Curriculum.Tree.Nodes.Count));
                AssertCard(root, "horizontal-cut", DuelVisualTheme.Card, "전투 1회", DuelVisualTheme.Muted, false);
                AssertCard(root, "advance", DuelVisualTheme.Track, "잠김", DuelVisualTheme.Muted, true);
            }
        }

        [UnityTest]
        public IEnumerator TreeLayout_KeepsEveryNodeCardInsideTheContentWithoutOverlaps_AndOnlySchoolsHaveBands()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var content = (RectTransform)Named(fixture.Root, "Curriculum Tree Content");
                CurriculumTree tree = fixture.Run.Curriculum.Tree;
                Assert.That(content.sizeDelta, Is.EqualTo(new Vector2(CampaignCurriculumHud.ContentWidth, 1080f)),
                    "Six rows and the fork's gap: 208 + 168 × 5 + 32.");
                Assert.That(CampaignCurriculumHud.ContentHeightFor(tree), Is.EqualTo(1080f));
                var frame = new Rect(0f, -content.sizeDelta.y, content.sizeDelta.x, content.sizeDelta.y);
                var cards = new List<KeyValuePair<string, Rect>>();
                foreach (CurriculumNode node in tree.Nodes)
                {
                    var card = (RectTransform)Named(content.gameObject, "Curriculum Node " + node.Id);
                    Assert.That(card.parent, Is.SameAs(content.transform), node.Id);
                    Assert.That(card.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), node.Id + " is placed from the content's upper left.");
                    Assert.That(card.anchoredPosition, Is.EqualTo(CampaignCurriculumHud.NodePosition(node)), node.Id);
                    Assert.That(card.sizeDelta,
                        Is.EqualTo(new Vector2(CampaignCurriculumHud.NodeWidth, CampaignCurriculumHud.NodeHeight)), node.Id);
                    Rect bounds = Bounds(card);
                    Assert.That(Inside(frame, bounds), Is.True, $"{node.Id} {bounds} must lie inside the content {frame}.");
                    foreach (KeyValuePair<string, Rect> other in cards)
                        Assert.That(bounds.Overlaps(other.Value), Is.False, node.Id + " overlaps " + other.Key);
                    cards.Add(new KeyValuePair<string, Rect>(node.Id, bounds));
                }
                Assert.That(CampaignCurriculumHud.NodePosition(tree.Find("advance")).x, Is.EqualTo(377f), "The trunk runs down the middle.");
                Assert.That(CampaignCurriculumHud.NodePosition(tree.Find("quick-draw")).y -
                    CampaignCurriculumHud.NodePosition(tree.Find("advance")).y, Is.EqualTo(-168f - CampaignCurriculumHud.ForkGap),
                    "The schools sit a fork's gap lower.");

                foreach (CurriculumBranch branch in Enum.GetValues(typeof(CurriculumBranch)))
                {
                    bool school = branch != CurriculumBranch.Foundation && branch != CurriculumBranch.Bridge;
                    Assert.That(TryNamed(content.gameObject, "Band " + branch) != null, Is.EqualTo(school), "Band " + branch);
                    Transform found = TryNamed(content.gameObject, "Branch " + branch);
                    Assert.That(found != null, Is.EqualTo(school), "Branch " + branch);
                    if (!school) continue;
                    var heading = (RectTransform)found;
                    Assert.That(heading.GetComponent<Text>().text, Is.EqualTo(CampaignCurriculumHud.BranchName(branch)));
                    Assert.That(heading.GetComponent<Text>().alignment, Is.EqualTo(TextAnchor.MiddleLeft), "Names sit at the band's left.");
                    Rect bounds = Bounds(heading);
                    Assert.That(Inside(frame, bounds), Is.True, heading.name + " must lie inside the content.");
                    Rect band = Bounds((RectTransform)Named(content.gameObject, "Band " + branch));
                    Assert.That(Inside(band, bounds), Is.True, heading.name + " sits on its band.");
                    Assert.That(Named(content.gameObject, "Band " + branch).GetComponent<Image>().raycastTarget, Is.False);
                    foreach (KeyValuePair<string, Rect> card in cards)
                        Assert.That(bounds.Overlaps(card.Value), Is.False, heading.name + " overlaps " + card.Key);
                    foreach (Transform child in content)
                        if (child.name.StartsWith("Connector ", StringComparison.Ordinal))
                            Assert.That(bounds.Overlaps(Bounds((RectTransform)child)), Is.False, heading.name + " crosses " + child.name);
                }

                foreach (Transform child in content)
                {
                    if (!child.name.StartsWith("Connector ", StringComparison.Ordinal)) continue;
                    Rect bounds = Bounds((RectTransform)child);
                    Assert.That(Inside(frame, bounds), Is.True, child.name + " must lie inside the content.");
                    // Connectors end exactly on card edges; only a real crossing counts.
                    Rect inner = new Rect(bounds.xMin + .01f, bounds.yMin + .01f, bounds.width - .02f, bounds.height - .02f);
                    foreach (KeyValuePair<string, Rect> card in cards)
                        Assert.That(inner.Overlaps(card.Value), Is.False, child.name + " crosses " + card.Key);
                }

                var treeFrame = (RectTransform)Named(fixture.Root, "Curriculum Tree");
                var detail = (RectTransform)Named(fixture.Root, "Curriculum Selected Detail");
                Assert.That(FrameBounds(treeFrame).Overlaps(FrameBounds(detail)), Is.False, "The detail panel sits beside the tree.");
            }
        }

        [UnityTest]
        public IEnumerator Scroll_TheTreeScrollsVerticallyInAClippedWindow_WithTheWheelAndAThinBar()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var treeFrame = Named(fixture.Root, "Curriculum Tree");
                ScrollRect scroll = treeFrame.GetComponent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null);
                Assert.That(scroll, Is.SameAs(fixture.Hud.TreeScroll));
                Assert.That((scroll.vertical, scroll.horizontal, scroll.inertia), Is.EqualTo((true, false, false)));
                Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(scroll.scrollSensitivity, Is.EqualTo(40f));
                var viewport = (RectTransform)Named(fixture.Root, "Curriculum Viewport");
                Assert.That(scroll.viewport, Is.SameAs(viewport));
                Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null, "The window clips the cards scrolled out of it.");
                Assert.That(viewport.GetComponent<Image>().raycastTarget, Is.True, "The window takes the wheel between cards.");
                Assert.That(scroll.content, Is.SameAs(Named(fixture.Root, "Curriculum Tree Content")));
                Assert.That(scroll.content.parent, Is.SameAs(viewport));
                Assert.That(scroll.verticalScrollbar, Is.Not.Null);
                Assert.That(scroll.verticalScrollbar.name, Is.EqualTo("Curriculum Scrollbar"));
                Assert.That(fixture.Hud.MaximumScroll, Is.EqualTo(1080f - viewport.rect.height).Within(.01f));
                Assert.That(fixture.Hud.ScrollOffset, Is.Zero, "A fresh curriculum opens on its first node at the top.");

                // The wheel (scrolled down one notch) moves the content up by the sensitivity.
                scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1f) });
                Assert.That(fixture.Hud.ScrollOffset, Is.EqualTo(40f).Within(.01f));
                for (int notch = 0; notch < 40; notch++)
                    scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, -1f) });
                Assert.That(fixture.Hud.ScrollOffset, Is.EqualTo(fixture.Hud.MaximumScroll).Within(.01f), "It stops at the tree's foot.");

                // A drag on a card is the scroll view's: no card takes a drag itself.
                Button card = Button(fixture.Root, "Curriculum Node doublet");
                Assert.That(card.GetComponent<IDragHandler>(), Is.Null);
                Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(card.gameObject), Is.SameAs(treeFrame.gameObject));
                Assert.That(ExecuteEvents.GetEventHandler<IScrollHandler>(card.gameObject), Is.SameAs(treeFrame.gameObject));
            }
        }

        [UnityTest]
        public IEnumerator Scroll_SelectingACardOutOfViewBringsItIn_AndARebuildKeepsTheOffset()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                Assert.That(fixture.Hud.IsNodeInView("horizontal-cut"), Is.True);
                Assert.That(fixture.Hud.IsNodeInView("doublet"), Is.False, "The capstone row starts below the window.");
                fixture.Hud.SelectNode("doublet");
                Assert.That(fixture.Hud.IsNodeInView("doublet"), Is.True);
                float offset = fixture.Hud.ScrollOffset;
                Assert.That(offset, Is.GreaterThan(0f));
                fixture.Hud.SelectNode("cadence");
                Assert.That(fixture.Hud.ScrollOffset, Is.EqualTo(offset), "A card already in view does not move the tree.");

                fixture.Build();
                Assert.That(fixture.Hud.ScrollOffset, Is.EqualTo(offset).Within(.01f), "A rebuild (진행, 초기화) stays where it was.");
                Assert.That(fixture.State.TreeScroll.HasValue, Is.True);
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("cadence"));

                fixture.State.Reopen();
                fixture.Build();
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("horizontal-cut"), "Opened afresh, it focuses again.");
                Assert.That(fixture.Hud.ScrollOffset, Is.Zero);
                Assert.That(fixture.Hud.IsNodeInView("horizontal-cut"), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator Focus_OpensOnTheNodeInProgressThenANewOneThenTheFirstAvailable()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.CompleteAll("horizontal-cut", "preparation", "advance", "quick-draw", "fighting-spirit");
                fixture.State.Reopen();
                fixture.Build();
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("estocada"), "The node the last completion opened comes first.");
                Assert.That(fixture.Hud.IsNodeInView("estocada"), Is.True);
                Assert.That(fixture.Hud.ScrollOffset, Is.GreaterThan(0f));

                Assert.That(fixture.Run.TrySelectCurriculumNode("suppleness"), Is.True);
                fixture.State.Reopen();
                fixture.Build();
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("suppleness"), "The node in progress comes before a new one.");
                Assert.That(fixture.Hud.IsNodeInView("suppleness"), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator NewTag_MarksWhatTheLastCompletionOpened_UntilEachIsLookedAt()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                foreach (CurriculumNode node in fixture.Run.Curriculum.Tree.Nodes)
                    Assert.That(fixture.Hud.IsNew(node.Id), Is.False, "Nothing is new before a completion: " + node.Id);
                fixture.CompleteAll("horizontal-cut", "preparation", "advance");
                foreach (string school in new[] { "quick-draw", "diagonal-cut", "suppleness" })
                {
                    Assert.That(fixture.Hud.IsNew(school), Is.True, school);
                    Assert.That(Label(Named(root, "Curriculum Node " + school).gameObject, "Node Tag").text,
                        Is.EqualTo(CampaignCurriculumHud.NewTag), school);
                }
                Assert.That(fixture.Hud.IsNew("preparation"), Is.False, "Only the last completion's nodes are new.");

                Button(root, "Curriculum Node diagonal-cut").onClick.Invoke();
                Assert.That(fixture.Hud.IsNew("diagonal-cut"), Is.False, "Looking at it clears the tag.");
                Assert.That(Label(Named(root, "Curriculum Node diagonal-cut").gameObject, "Node Tag").text, Is.Empty);
                Assert.That(fixture.Hud.IsNew("quick-draw"), Is.True, "The others keep theirs.");
                fixture.Build();
                Assert.That(fixture.Hud.IsNew("diagonal-cut"), Is.False, "A rebuild remembers what was looked at.");
                Assert.That(fixture.Hud.IsNew("suppleness"), Is.True);

                fixture.Complete("quick-draw", DuelMatchOutcome.EnemyVictory);
                Assert.That(fixture.Hud.IsNew("suppleness"), Is.False, "A new completion brings its own new nodes.");
                Assert.That(fixture.Hud.IsNew("fighting-spirit") && fixture.Hud.IsNew("esquive"), Is.True);
                Assert.That(fixture.Run.TrySelectCurriculumNode("esquive"), Is.True);
                fixture.Hud.Refresh();
                Assert.That(fixture.Hud.IsNew("esquive"), Is.False, "A node in progress is no longer new.");
            }
        }

        [UnityTest]
        public IEnumerator BridgeAndCapstoneCards_CarryTheirTags_AndAFinishedSchoolTakesItsKeywordColour()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                GameObject root = fixture.Root;
                Transform content = Named(root, "Curriculum Tree Content");
                Assert.That(Label(Named(root, "Curriculum Node battement").gameObject, "Node Tag").text,
                    Is.EqualTo(CampaignCurriculumHud.BridgeTag));
                Transform plate = Named(content.gameObject, "Bridge Plate battement");
                Assert.That(plate.GetSiblingIndex(), Is.LessThan(Named(content.gameObject, "Curriculum Node battement").GetSiblingIndex()),
                    "The plate is under its card.");
                Color left = Named(plate.gameObject, "Bridge Plate Left").GetComponent<Image>().color;
                Color right = Named(plate.gameObject, "Bridge Plate Right").GetComponent<Image>().color;
                Assert.That(new Color(left.r, left.g, left.b), Is.EqualTo(Opaque(SkillToneStyle.Paper(LegacySkillTone.HighPower))),
                    "붕괴 on the left.");
                Assert.That(new Color(right.r, right.g, right.b), Is.EqualTo(Opaque(SkillToneStyle.Paper(LegacySkillTone.Cycle))),
                    "순환 on the right.");
                Assert.That(CampaignCurriculumHud.TryGetBridgedBranches(fixture.Run.Curriculum.Tree,
                    fixture.Run.Curriculum.Tree.Find("battement"), out CurriculumBranch from, out CurriculumBranch to), Is.True);
                Assert.That((from, to), Is.EqualTo((CurriculumBranch.Collapse, CurriculumBranch.Circulation)));
                // 숫돌 bridges 순환 (left) and 판짜기 (right) the same way.
                Assert.That(Label(Named(root, "Curriculum Node whetstone").gameObject, "Node Tag").text,
                    Is.EqualTo(CampaignCurriculumHud.BridgeTag));
                Transform whetstone = Named(content.gameObject, "Bridge Plate whetstone");
                Color cycle = Named(whetstone.gameObject, "Bridge Plate Left").GetComponent<Image>().color;
                Color retraction = Named(whetstone.gameObject, "Bridge Plate Right").GetComponent<Image>().color;
                Assert.That(new Color(cycle.r, cycle.g, cycle.b), Is.EqualTo(Opaque(SkillToneStyle.Paper(LegacySkillTone.Cycle))));
                Assert.That(new Color(retraction.r, retraction.g, retraction.b),
                    Is.EqualTo(Opaque(SkillToneStyle.Paper(LegacySkillTone.Followup))));

                foreach (string capstone in new[] { "estocada", "cadence", "doublet" })
                {
                    Transform card = Named(root, "Curriculum Node " + capstone);
                    Assert.That(Label(card.gameObject, "Node Tag").text, Is.EqualTo(CampaignCurriculumHud.CapstoneTag), capstone);
                    Assert.That(Named(card.gameObject, "Capstone Seal").gameObject.activeSelf, Is.False, capstone);
                }
                Assert.That(TryNamed(Named(root, "Curriculum Node quick-draw").gameObject, "Capstone Seal"), Is.Null);
                Assert.That(Label(Named(root, "Curriculum Node quick-draw").gameObject, "Node Tag").text, Is.Empty);
                Text collapse = Label(content.gameObject, "Branch Collapse");
                Assert.That(collapse.color, Is.EqualTo(DuelVisualTheme.Muted));

                fixture.CompleteAll("horizontal-cut", "preparation", "advance", "quick-draw", "esquive", "estocada");
                Assert.That(Named(Named(root, "Curriculum Node estocada").gameObject, "Capstone Seal").gameObject.activeSelf, Is.True,
                    "A completed capstone keeps its gold border.");
                Assert.That(collapse.color, Is.EqualTo(SkillToneStyle.Paper(LegacySkillTone.HighPower)), "붕괴 is finished.");
                Assert.That(Label(content.gameObject, "Branch Circulation").color, Is.EqualTo(DuelVisualTheme.Muted));
            }
        }

        [UnityTest]
        public IEnumerator Lifecycle_RebuildKeepsSelectionRefreshReusesNodesAndDisposeIsIdempotent()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                AssertMarker(fixture.Root, "horizontal-cut");
                fixture.Hud.SelectNode("vital-thrust");
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("vital-thrust"));
                AssertMarker(fixture.Root, "vital-thrust");
                fixture.Hud.SelectNode("missing");
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("vital-thrust"), "An unknown node id is ignored.");

                fixture.Build();
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("vital-thrust"));
                Assert.That(Label(fixture.Root, "Curriculum Detail Name").text, Is.EqualTo("급소 찌르기"));
                AssertMarker(fixture.Root, "vital-thrust");
                foreach (Button button in fixture.Root.GetComponentsInChildren<Button>(true))
                    Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None), button.name);
                foreach (Graphic graphic in fixture.Root.GetComponentsInChildren<Graphic>(true))
                {
                    // The scroll window and its bar take the wheel and drags; everything else under a card is decoration.
                    if (graphic.GetComponent<Button>() != null || graphic.name == "Curriculum Viewport" ||
                        graphic.GetComponentInParent<Scrollbar>() != null) continue;
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name + " is decoration and must not block node clicks.");
                }

                int nodes = fixture.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 10; i++) fixture.Hud.Refresh();
                fixture.Hud.SelectNode("advance");
                fixture.Hud.SelectNode("doublet");
                Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes),
                    "Refreshing reuses the node cards instead of adding more.");

                fixture.State.Reset();
                Assert.That(fixture.State.SelectedNodeId, Is.Null);
                Assert.That(fixture.State.TreeScroll.HasValue, Is.False);
                Assert.That(fixture.Run.TrySelectCurriculumNode("horizontal-cut"), Is.True);
                fixture.Hud.Refresh();
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("horizontal-cut"), "Without a selection the node in progress is shown.");

                Button primary = Button(fixture.Root, "Curriculum Primary Action");
                Button node = Button(fixture.Root, "Curriculum Node breathing");
                Button reset = Button(fixture.Root, "Curriculum Reset");
                GameObject disposedRoot = fixture.Root;
                fixture.Hud.Dispose();
                fixture.Hud.Dispose();
                Assert.That(disposedRoot.activeSelf, Is.False);
                Assert.That(fixture.Hud.IsResetArmed, Is.False);
                node.onClick.Invoke();
                primary.onClick.Invoke();
                reset.onClick.Invoke();
                fixture.Hud.Refresh();
                fixture.Hud.SelectNode("breathing");
                Assert.That(fixture.State.SelectedNodeId, Is.EqualTo("horizontal-cut"), "A disposed page ignores selection.");
                Assert.That(fixture.SelectCalls, Is.Zero);
                Assert.That(fixture.ResetCalls, Is.Zero);
                Assert.That(fixture.Run.Curriculum.Active.Id, Is.EqualTo("horizontal-cut"));
                yield return null;
                Assert.That(disposedRoot == null, Is.True, "Dispose releases the page's objects.");
            }
        }

        [Test]
        public void FieldNotice_NamesTheCompletionOrTheCourse_AndTheNewSkill()
        {
            CurriculumTree tree = CampaignCurriculum.Default;
            LegacySkill estocada = LegacySkillDefinitions.Skill(51);
            string notice = AcademyFieldView.CurriculumNotice(tree.Find("estocada"), estocada, true, true);
            Assert.That(notice, Does.StartWith("커리큘럼 · 붕괴 과정 수료 · 새 기술 " + estocada.Name + "(" +
                SkillLaneStyle.Key(estocada.LaneIndex)));
            Assert.That(notice, Does.EndWith("Esc 메뉴의 편성에서 장착할 수 있습니다."));
            string closed = AcademyFieldView.CurriculumNotice(tree.Find("horizontal-cut"), LegacySkillDefinitions.Skill(14), false, false);
            Assert.That(closed, Does.StartWith("커리큘럼 · 가로베기 완료 · 새 기술 가로베기").And.Contain("이 기술의 열이 열리면"));
            var stats = new CurriculumNode("stats", "기초 체력", CurriculumBranch.Retraction, 0f, 0, Array.Empty<int>(),
                statReward: new CurriculumStatReward(health: 5));
            Assert.That(AcademyFieldView.CurriculumNotice(stats, null, false, false), Does.Contain("능력치가 올랐습니다"));
        }

        private static Color Opaque(Color color) => new Color(color.r, color.g, color.b);

        private static void AssertCard(GameObject root, string id, Color background, string status, Color statusColor, bool dim)
        {
            Button card = Button(root, "Curriculum Node " + id);
            Assert.That(card.interactable, Is.True, "Every node can be selected for viewing: " + id);
            Assert.That(card.GetComponent<Image>().color, Is.EqualTo(background), id);
            Text statusLabel = Label(card.gameObject, "Node Status");
            Assert.That(statusLabel.text, Is.EqualTo(status), id);
            Assert.That(statusLabel.color, Is.EqualTo(statusColor), id);
            Assert.That(Label(card.gameObject, "Node Title").color,
                Is.EqualTo(dim ? DuelVisualTheme.Muted : DuelVisualTheme.Foreground), id);
            Assert.That(Named(card.gameObject, "Node Icon").GetComponent<Image>().color.a,
                Is.EqualTo(dim ? .4f : 1f).Within(.001f), id);
        }

        private static void AssertPrimary(Button primary, string caption, bool interactable)
        {
            Text label = primary.GetComponentInChildren<Text>();
            Assert.That(label.text, Is.EqualTo(caption));
            Assert.That(primary.interactable, Is.EqualTo(interactable), caption);
            Assert.That(label.color, Is.EqualTo(interactable ? DuelVisualTheme.Ink : DuelVisualTheme.Foreground), caption);
        }

        private static void AssertLocked(GameObject root, Button primary, Button reset)
        {
            Assert.That(Label(root, "Curriculum Availability").text, Is.EqualTo("로비에서 이용할 수 있습니다."));
            Assert.That(primary.interactable, Is.False);
            Assert.That(reset.interactable, Is.False);
        }

        private static void AssertMarker(GameObject root, string selectedId)
        {
            foreach (Button card in root.GetComponentsInChildren<Button>(true))
            {
                if (!card.name.StartsWith("Curriculum Node ", StringComparison.Ordinal)) continue;
                bool selected = card.name == "Curriculum Node " + selectedId;
                Assert.That(Named(card.gameObject, "Selected Marker").gameObject.activeSelf, Is.EqualTo(selected), card.name);
            }
        }

        /// <summary>Checks one prerequisite link and returns how many pieces it is drawn with.</summary>
        private static int AssertConnector(Transform content, CurriculumNode parent, CurriculumNode child, Color color, int firstCard)
        {
            string name = $"Connector {parent.Id} {child.Id}";
            Vector2 from = CampaignCurriculumHud.NodePosition(parent) - new Vector2(0f, CampaignCurriculumHud.NodeHeight * .5f);
            Vector2 to = CampaignCurriculumHud.NodePosition(child) + new Vector2(0f, CampaignCurriculumHud.NodeHeight * .5f);
            RectTransform down = Piece(content, name + " Down", color, firstCard);
            RectTransform into = Piece(content, name + " Into", color, firstCard);
            Rect downBounds = Bounds(down), intoBounds = Bounds(into);
            Assert.That(down.anchoredPosition.x, Is.EqualTo(from.x).Within(.01f), name);
            Assert.That(downBounds.yMax, Is.EqualTo(from.y).Within(.01f), name + " starts at the prerequisite's bottom edge.");
            Assert.That(into.anchoredPosition.x, Is.EqualTo(to.x).Within(.01f), name);
            Assert.That(intoBounds.yMin, Is.EqualTo(to.y).Within(.01f), name + " ends at the node's top edge.");
            Assert.That(downBounds.yMin, Is.EqualTo(intoBounds.yMax).Within(.01f), name + " halves meet at one height.");

            Transform across = content.Find(name + " Across");
            if (Mathf.Approximately(from.x, to.x))
            {
                Assert.That(across, Is.Null, name + " is a straight line.");
                return 2;
            }
            Assert.That(across, Is.Not.Null, name + " needs a horizontal piece.");
            Rect acrossBounds = Bounds(Piece(content, across.name, color, firstCard));
            Assert.That(acrossBounds.center.y, Is.EqualTo(downBounds.yMin).Within(.01f), name);
            Assert.That(acrossBounds.xMin, Is.LessThanOrEqualTo(Mathf.Min(from.x, to.x)), name);
            Assert.That(acrossBounds.xMax, Is.GreaterThanOrEqualTo(Mathf.Max(from.x, to.x)), name);
            return 3;
        }

        private static RectTransform Piece(Transform content, string name, Color color, int firstCard)
        {
            Transform piece = content.Find(name);
            Assert.That(piece, Is.Not.Null, "Missing connector piece: " + name);
            Image image = piece.GetComponent<Image>();
            Assert.That(image.color, Is.EqualTo(color), name + " uses the colour of its prerequisite kind.");
            Assert.That(image.raycastTarget, Is.False, name);
            Assert.That(piece.GetSiblingIndex(), Is.LessThan(firstCard), name + " is drawn behind the node cards.");
            return (RectTransform)piece;
        }

        // A content child's rect from its centre (they are all placed by their centres from the content's upper left).
        private static Rect Bounds(RectTransform rect)
            => new Rect(rect.anchoredPosition - rect.sizeDelta * .5f, rect.sizeDelta);

        // A page child's rect (anchored at the page's centre).
        private static Rect FrameBounds(RectTransform rect)
            => new Rect(rect.anchoredPosition - rect.sizeDelta * .5f, rect.sizeDelta);

        private static bool Inside(Rect frame, Rect bounds)
            => bounds.xMin >= frame.xMin && bounds.xMax <= frame.xMax && bounds.yMin >= frame.yMin && bounds.yMax <= frame.yMax;

        private static LegacySkill CatalogSkill(int id)
        {
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
            throw new InvalidOperationException("Missing catalog skill " + id);
        }

        private static Transform TryNamed(GameObject root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true)) if (candidate.name == name) return candidate;
            return null;
        }

        private static Transform Named(GameObject root, string name)
        {
            Transform result = TryNamed(root, name);
            Assert.That(result, Is.Not.Null, "Missing curriculum node: " + name);
            return result;
        }

        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Button Button(GameObject root, string name) => Named(root, name).GetComponent<Button>();
    }
}
