using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SkillInfoViewPlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly GameObject CanvasRoot;
            public readonly SkillInfoView View;

            public Fixture()
            {
                CanvasRoot = new GameObject("Skill Information Test Canvas", typeof(RectTransform), typeof(Canvas));
                CanvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasRoot.GetComponent<Canvas>().sortingOrder = 600;
                View = new SkillInfoView(CanvasRoot.transform, new LegacyDuelArt().UIFont, Vector2.zero, 334f, "Test");
            }

            public void Dispose() => Object.Destroy(CanvasRoot);
        }

        [UnityTest]
        public IEnumerator EveryImplementedSkill_ShowsItsActualCostPowerAndHitCount()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                // Authored expectations, not values copied from the view model.
                int[] ids = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 15, 16, 17, 19, 21, 32, 42 };
                int[] costs = { 1, 1, 2, 3, 1, 2, 1, 3, 1, 1, 1, 1, 1, 5, 2, 2, 3, 2, 1 };
                string[] powers = { "4–5", "8–9", "11–14", "13–16", "4–6", "8–11", "5–8", "12–15", "4–6", "2–3", "4–8", "6–12", "7–10", "3–20", "9–12", "7–11", "12–17", "8–12", "4–8" };
                int[] hits = { 1, 2, 1, 1, 2, 3, 1, 1, 1, 1, 2, 1, 2, 1, 1, 1, 1, 1, 1 };
                for (int index = 0; index < ids.Length; index++)
                {
                    LegacySkill skill = Skill(ids[index]);
                    fixture.View.SetSkill(skill);
                    Assert.That(fixture.View.Root.activeInHierarchy, Is.True, skill.Name);
                    Assert.That(Label(fixture.View.Root, "ACT Value").text, Is.EqualTo(costs[index].ToString()), skill.Name);
                    Assert.That(fixture.View.PowerText.text, Is.EqualTo(powers[index]), skill.Name);
                    if (skill.Kind == LegacySkillKind.Attack)
                    {
                        Assert.That(Label(fixture.View.Root, "Power Label").text, Is.EqualTo("위력"), skill.Name);
                        Assert.That(Label(fixture.View.Root, "Hits Value").text, Is.EqualTo(hits[index] + "회"), skill.Name);
                        if (hits[index] > 1)
                        {
                            AssertNoPowerSplittingExplanation(fixture.View.EffectText.text, skill.Name + " skill card");
                            AssertNoPowerSplittingExplanation(CampaignSkillText.Effect(skill), skill.Name + " lobby effect");
                            if (ids[index] == 2 || ids[index] == 15)
                                Assert.That(fixture.View.EffectText.text, Is.Empty,
                                    "An ordinary multi-hit skill needs no calculation paragraph: " + skill.Name);
                        }
                    }
                    else
                    {
                        Assert.That(Label(fixture.View.Root, "Power Label").text, Does.Contain("방어"),
                            "Defense power must not be presented as an attack's total power.");
                        Assert.That(Label(fixture.View.Root, "Hits Value").text, Is.EqualTo("같은 칸"), skill.Name);
                        Assert.That(fixture.View.DamageText.text, Is.Empty, skill.Name);
                    }
                    Assert.That(fixture.View.DamageText.text, Is.Empty,
                        "Shared damage-routing rules belong to the tutorial, not an individual skill card: " + skill.Name);
                    Assert.That(fixture.View.DamageText.gameObject.activeInHierarchy, Is.False, skill.Name);
                    foreach (Transform node in fixture.View.Root.GetComponentsInChildren<Transform>(true))
                        Assert.That(node.name, Is.Not.EqualTo("Damage Routing"), skill.Name);
                    Assert.That(VisibleText(fixture.View.Root), Does.Not.Contain("공격과 대결 → 저항"), skill.Name);
                    Assert.That(VisibleText(fixture.View.Root), Does.Not.Contain("방어·빈칸 → 체력"), skill.Name);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeywordsAndConditions_ExplainTheStartingSchoolsWithoutChangingRules()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.View.SetSkill(Skill(2));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("분할 연타"));
                Assert.That(Label(fixture.View.Root, "ACT Value").text, Is.EqualTo("1"));
                AssertNoPowerSplittingExplanation(fixture.View.EffectText.text, "연속 베기 skill card");

                fixture.View.SetSkill(Skill(3));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("고화력").And.Contain("단타"));
                Assert.That(Label(fixture.View.Root, "ACT Value").text, Is.EqualTo("2"));
                Assert.That(fixture.View.EffectText.text, Does.Contain("1회 공격"));
                Assert.That(VisibleText(fixture.View.Root), Does.Not.Contain("10%"),
                    "The first W skill no longer promises a following-slot buff.");

                fixture.View.SetSkill(Skill(5));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("저항 -5").And.Contain("공격 대응"));
                Assert.That(fixture.View.EffectText.text, Does.Contain("상대가 공격이면").And.Contain("저항 5 감소"));

                fixture.View.SetSkill(Skill(6));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("저항 -8").And.Contain("방어 대응"));
                Assert.That(fixture.View.EffectText.text, Does.Contain("상대가 방어이면").And.Contain("저항 8 감소"));

                fixture.View.SetSkill(Skill(7));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("ACT").And.Contain("2"));
                Assert.That(fixture.View.EffectText.text, Does.Contain("타격").And.Contain("다음 턴"));

                fixture.View.SetSkill(Skill(8));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("25%").And.Contain("다음 1칸"));
                Assert.That(Label(fixture.View.Root, "ACT Value").text, Is.EqualTo("3"));
                Assert.That(fixture.View.EffectText.text, Does.Contain("이번 턴").And.Contain("다음 1칸"));

                fixture.View.SetSkill(Skill(9));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("20%").And.Contain("뒤 2칸"));
                Assert.That(fixture.View.EffectText.text,
                    Does.Contain("공격").And.Contain("방어").And.Contain("이번 턴").And.Contain("2"));

                fixture.View.SetSkill(Skill(7), true);
                Assert.That(VisibleText(fixture.View.Root), Does.Contain("플레이어 전용"),
                    "An enemy's icon must not promise ACT recovery that the runtime only grants to the player.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AcquiredSkills_DoNotInheritUnimplementedUtilityFromArtworkOrNames()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                foreach (int skillId in new[] { 14, 17, 32 })
                {
                    fixture.View.SetSkill(Skill(skillId));
                    string details = Keywords(fixture.View.Root) + "\n" + fixture.View.EffectText.text;
                    Assert.That(details, Does.Not.Contain("ACT 회복"), "Skill " + skillId);
                    Assert.That(details, Does.Not.Contain("후속 위력"), "Skill " + skillId);
                    Assert.That(details, Does.Not.Contain("피해 감소"), "Skill " + skillId);
                    Assert.That(details, Does.Not.Contain("회피"), "Skill " + skillId);
                    Assert.That(details, Does.Not.Contain("방어 무시"), "Skill " + skillId);
                    Assert.That(details, Is.Not.Empty);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AddedUtilitySkills_ShowTheirOwnConditionsAndEnemyRecoveryLimit()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.View.SetSkill(Skill(10));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("30%").And.Contain("다음 1칸"));
                Assert.That(fixture.View.EffectText.text,
                    Does.Contain("이번 턴").And.Contain("공격·방어").And.Contain("30%"));
                Assert.That(CampaignSkillText.Effect(Skill(10)), Does.Contain("턴을 넘겨 유지되지"));

                fixture.View.SetSkill(Skill(12));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("ACT +3").And.Contain("배율 +50%"));
                Assert.That(fixture.View.EffectText.text,
                    Does.Contain("다음 턴 ACT 회복 +3").And.Contain("이번 턴 · 다음 1칸 받는 피해 배율 +50%"));
                Assert.That(CampaignSkillText.Effect(Skill(12)), Does.Contain("받는 피해").And.Contain("50%"));

                fixture.View.SetSkill(Skill(19));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("저항 회복").And.Contain("10%"));
                Assert.That(fixture.View.EffectText.text,
                    Does.Contain("기술 시작 시 최대 저항").And.Contain("최대치 제한"));
                Assert.That(Keywords(fixture.View.Root), Does.Not.Contain("피해 -30%"));
                Assert.That(CampaignSkillText.Effect(Skill(19)), Does.Contain("최대 저항").And.Contain("최대치를 넘지"));

                fixture.View.SetSkill(Skill(42));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("저항 -20").And.Contain("ACT +3"));
                Assert.That(fixture.View.EffectText.text,
                    Does.Contain("기술 시작 시 같은 칸 상대가 방어이면").And.Contain("체력 피해 없음"));
                Assert.That(CampaignSkillText.Effect(Skill(42)), Does.Contain("체력 피해로 이어지지"));

                foreach (int id in new[] { 12, 42 })
                {
                    fixture.View.SetSkill(Skill(id), true);
                    Assert.That(VisibleText(fixture.View.Root), Does.Contain("플레이어 전용"));
                    Assert.That(Keywords(fixture.View.Root), Does.Contain("플레이어 ACT"));
                }
                foreach (int id in new[] { 10, 19 })
                {
                    fixture.View.SetSkill(Skill(id), true);
                    Assert.That(VisibleText(fixture.View.Root), Does.Not.Contain("플레이어 전용"));
                }
                fixture.View.SetSkill(Skill(17));
                Assert.That(Keywords(fixture.View.Root), Does.Not.Contain("저항 회복"));
                Assert.That(CampaignSkillText.Effect(Skill(17)), Does.Not.Contain("회복"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearOrNull_NeverLeavesVisibleStaleValues()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.View.SetSkill(Skill(1));
                Assert.That(fixture.View.PowerText.text, Does.Contain("4–5"));
                Assert.That(Keywords(fixture.View.Root), Is.Not.Empty);

                fixture.View.Clear();
                AssertNoVisibleValues(fixture.View);
                fixture.View.SetSkill(Skill(8));
                Assert.That(fixture.View.Root.activeInHierarchy, Is.True);
                fixture.View.SetSkill(null);
                AssertNoVisibleValues(fixture.View);
                fixture.View.SetSkill(Skill(3));
                Assert.That(fixture.View.PowerText.text, Is.EqualTo("11–14"));
                Assert.That(Keywords(fixture.View.Root), Does.Contain("고화력"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OwnedSkill_ShowsCompactProgressOnlyOnThePlayersCard()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                var run = new CampaignRun();
                CampaignOwnedSkill owned = run.OwnedSkills[0];
                float height = fixture.View.Height;
                fixture.View.SetSkill(owned.Skill, owned: owned);
                Canvas.ForceUpdateCanvases();
                var progress = Named(fixture.View.Root, "Skill Experience").GetComponent<RectTransform>();
                var fill = Named(fixture.View.Root, "Skill Experience Fill").GetComponent<RectTransform>();
                Assert.That(progress.gameObject.activeInHierarchy, Is.True);
                Assert.That(Label(fixture.View.Root, "Skill Experience Level").text,
                    Is.EqualTo("숙련 " + owned.Level + "/" + CampaignOwnedSkill.MaxLevel));
                Assert.That(Label(fixture.View.Root, "Skill Experience Value").text,
                    Is.EqualTo(owned.ExperienceThisLevel + "/" + owned.ExperienceRequired));
                Assert.That(fill.rect.width, Is.Zero.Within(.1f));
                Assert.That(fixture.View.Height, Is.EqualTo(height).Within(.1f));
                RectTransform effect = fixture.View.EffectText.rectTransform;
                Assert.That(effect.anchoredPosition.y + effect.rect.yMin,
                    Is.GreaterThan(progress.anchoredPosition.y + progress.rect.yMax),
                    "The fixed card reserves a separate line for experience below its effect text.");
                Assert.That(fixture.View.EffectText.preferredHeight, Is.LessThanOrEqualTo(effect.rect.height + 1f));

                for (int use = 0; use < 5; use++) owned.GainClashExperience();
                fixture.View.SetSkill(owned.Skill, owned: owned);
                Assert.That(Label(fixture.View.Root, "Skill Experience Value").text, Is.EqualTo("5/10"),
                    "The card must refresh even when the skill object itself has not changed.");
                Assert.That(fill.rect.width, Is.EqualTo(progress.rect.width / 2f).Within(.1f));
                for (int use = 5; use < 30; use++) owned.GainClashExperience();
                fixture.View.SetSkill(owned.Skill, owned: owned);
                Assert.That(Label(fixture.View.Root, "Skill Experience Level").text, Is.EqualTo("숙련 3/3"));
                Assert.That(Label(fixture.View.Root, "Skill Experience Value").text, Is.EqualTo("MAX"));
                Assert.That(fill.rect.width, Is.EqualTo(progress.rect.width).Within(.1f));
                Assert.That(fixture.View.PowerText.text, Is.EqualTo("10–11"));
                Assert.That(fixture.View.Height, Is.EqualTo(height).Within(.1f));

                fixture.View.SetSkill(owned.Skill, true, owned: owned);
                Assert.That(progress.gameObject.activeSelf, Is.False, "An enemy has no player skill experience.");
                fixture.View.SetSkill(owned.Skill);
                Assert.That(progress.gameObject.activeSelf, Is.False, "An unowned preview has no experience.");
                fixture.View.SetSkill(owned.Skill, owned: owned);
                fixture.View.Clear();
                Assert.That(progress.gameObject.activeSelf, Is.False);
                Assert.That(Label(fixture.View.Root, "Skill Experience Value").text, Is.Empty);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AdditionalCurriculumReward_UsesTheSameCardAndClearsWhenSelectionChanges()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                float height = fixture.View.Height;
                LegacySkill skill = Skill(5);
                fixture.View.SetSkill(skill, false, "능력치 보상: 최대 체력 +5 · 매 턴 ACT 회복 +1");
                Assert.That(fixture.View.EffectText.text, Does.Contain("저항 5 감소").And.Contain("최대 체력 +5"));
                Assert.That(fixture.View.Height, Is.EqualTo(height).Within(.1f));
                fixture.View.SetSkill(skill);
                Assert.That(fixture.View.EffectText.text, Does.Not.Contain("최대 체력 +5"));
                Assert.That(fixture.View.Height, Is.EqualTo(height).Within(.1f));
                fixture.View.SetEmptyMessage("최대 저항 +4 · 일반 스테이지 선택 시간 +3초");
                Assert.That(fixture.View.EffectText.text, Does.Contain("선택 시간 +3초"));
                Assert.That(fixture.View.Height, Is.EqualTo(height).Within(.1f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedSelection_ReusesNodesAndDecorativeGlyphsNeverCaptureInput()
        {
            yield return null;
            using (var fixture = new Fixture())
            {
                fixture.View.SetSkill(Skill(1));
                int originalNodeCount = fixture.View.Root.GetComponentsInChildren<Transform>(true).Length;
                var originalGlyphs = fixture.View.Root.GetComponentsInChildren<SkillInfoGlyph>(true);
                Assert.That(originalGlyphs, Is.Not.Empty, "The information card should use semantic pictograms, not only text.");
                for (int iteration = 0; iteration < 40; iteration++)
                {
                    fixture.View.SetSkill(Skill(iteration % 2 == 0 ? 3 : 8));
                    if (iteration % 4 == 0) fixture.View.Clear();
                }
                fixture.View.SetSkill(Skill(7));
                Assert.That(fixture.View.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(originalNodeCount));
                CollectionAssert.AreEquivalent(originalGlyphs,
                    fixture.View.Root.GetComponentsInChildren<SkillInfoGlyph>(true));
                foreach (SkillInfoGlyph glyph in originalGlyphs)
                {
                    Assert.That(glyph.GetComponent<CanvasRenderer>(), Is.Not.Null, glyph.name);
                    Assert.That(glyph.raycastTarget, Is.False, glyph.name + " is a decorative symbol, not an input target.");
                }
                foreach (Text text in fixture.View.Root.GetComponentsInChildren<Text>(true))
                    Assert.That(text.raycastTarget, Is.False, text.name);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualLoadoutAndCurriculumSelection_RefreshTheSameInformationCardWithoutProgress()
        {
            yield return null;
            var parent = new GameObject("Integrated Skill Information Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run);
                lobby.ShowTab(LobbyTab.Loadout);
                yield return null;
                Click(lobby.Root, "Loadout Slot W 1");
                GameObject detail = Named(lobby.Root, "Loadout Selected Detail").gameObject;
                Assert.That(Label(detail, "Loadout Detail Name").text, Is.EqualTo("깊은 찌르기"));
                Assert.That(Label(detail, "Loadout Detail Values").text, Is.EqualTo("11–14"));
                Assert.That(Label(detail, "ACT Value").text, Is.EqualTo("2"));
                Assert.That(Keywords(detail), Does.Contain("고화력"));
                Click(lobby.Root, "Loadout Slot Q 3");
                Assert.That(Label(detail, "Loadout Detail Name").text, Is.EqualTo("막기"));
                Assert.That(Keywords(detail), Does.Contain("ACT").And.Contain("2"));
                Assert.That(run.HasLoadoutChanges, Is.False, "Choosing information must not move or replace a skill.");

                lobby.ShowTab(LobbyTab.Curriculum);
                yield return null;
                Click(lobby.Root, "Curriculum Node one-stroke");
                detail = Named(lobby.Root, "Curriculum Selected Detail").gameObject;
                Assert.That(Label(detail, "Curriculum Detail Name").text, Is.EqualTo("알티바호"));
                Assert.That(Label(detail, "ACT Value").text, Does.Contain("5"));
                Assert.That(Label(detail, "Curriculum Detail Values").text, Does.Contain("3–20"));
                Assert.That(Keywords(detail), Does.Contain("위력 편차"));
                // Each node shows the skill it grants: 준비 10, 플레슈 12, 르프리즈 19, 쿠페 42.
                foreach (string id in new[] { "preparation", "advance", "fighting-spirit", "quick-draw" })
                {
                    CurriculumNode node = run.Curriculum.Tree.Find(id);
                    LegacySkill skill = Skill(node.SkillIds[0]);
                    Click(lobby.Root, "Curriculum Node " + id);
                    Assert.That(Label(detail, "Curriculum Detail Name").text, Is.EqualTo(node.Title));
                    Assert.That(Label(detail, "ACT Value").text, Is.EqualTo(skill.Cost.ToString()));
                    Assert.That(Label(detail, "Curriculum Detail Values").text, Is.EqualTo(CampaignSkillText.Power(skill)));
                    Assert.That(Label(detail, "Curriculum Detail Effect").text, Is.Not.Empty);
                }
                Click(lobby.Root, "Curriculum Node suppleness");
                Assert.That(Label(detail, "Curriculum Detail Name").text, Is.EqualTo("유연함"));
                Assert.That(Label(detail, "Power Label").text, Does.Contain("방어"));
                Assert.That(Keywords(detail), Does.Contain("수치 방어"));
                Assert.That(run.Curriculum.Active, Is.Null, "Choosing information must not start a curriculum node.");
                Assert.That(run.Curriculum.CompletedCount, Is.Zero);
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(run.HasLoadoutChanges, Is.False);
            }
            finally
            {
                lobby?.Dispose();
                Object.Destroy(parent);
            }
            yield return null;
        }

        private static LegacySkill Skill(int id)
        {
            foreach (LegacySkill skill in LegacyInitialSkills.All) if (skill.Id == id) return skill;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == id) return skill;
            throw new InvalidOperationException("Missing authored skill " + id);
        }

        private static void AssertNoVisibleValues(SkillInfoView view)
        {
            if (!view.Root.activeInHierarchy) return;
            Assert.That(view.PowerText.text, Is.Empty, "An empty selection must not retain the old skill power.");
            Assert.That(Label(view.Root, "ACT Value").text, Is.Empty);
            Assert.That(Label(view.Root, "Hits Value").text, Is.Empty);
            Assert.That(Keywords(view.Root), Is.Empty);
        }

        private static void AssertNoPowerSplittingExplanation(string description, string context)
        {
            foreach (string fragment in new[] { "나눠", "나누", "나눕", "분할", "소수점", "버림", "최소 1", "2배" })
                Assert.That(description, Does.Not.Contain(fragment), context + " still explains hit-power splitting: " + fragment);
        }

        private static string Keywords(GameObject root)
            => (Label(root, "Keyword 1 Text").text + "\n" + Label(root, "Keyword 2 Text").text).Trim();

        private static string VisibleText(GameObject root)
        {
            var lines = new List<string>();
            foreach (Text text in root.GetComponentsInChildren<Text>()) lines.Add(text.text);
            return string.Join("\n", lines);
        }

        private static void Click(GameObject root, string name) => Named(root, name).GetComponent<Button>().onClick.Invoke();
        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();

        private static Transform Named(GameObject root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform;
            Assert.Fail("Missing skill information node: " + name);
            return null;
        }
    }
}
