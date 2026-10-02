using System.Collections;
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
    public sealed class SwordSkillRoleHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator HeldExplanation_ShowsAuthoritativeActAndConditionalRecoveryDetails()
        {
            yield return null;
            var parent = new GameObject("Skill Role HUD Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                LegacySkill actRecovery = LegacySkillDefinitions.Skill(1);
                hud.ShowExplanation(actRecovery, false);
                Text effect = FindTextUnder(hud.Root.transform, "Skill Explain", "Effect");
                Assert.That(effect.gameObject.activeInHierarchy, Is.True);
                Assert.That(effect.text, Does.Contain("다음 턴 ACT 회복 +1"));
                Text damageHint = FindTextUnder(hud.Root.transform, "Skill Explain", "Player Damage Hint");
                Assert.That(damageHint.text, Is.Empty,
                    "The common attack-versus-attack routing is not this skill's special effect.");
                Assert.That(damageHint.gameObject.activeInHierarchy, Is.False);
                Transform explanation = FindNamed(hud.Root.transform, "Skill Explain");
                foreach (Transform node in explanation.GetComponentsInChildren<Transform>(true))
                    Assert.That(node.name, Is.Not.EqualTo("Damage Routing"));
                foreach (Text text in explanation.GetComponentsInChildren<Text>())
                {
                    Assert.That(text.text, Does.Not.Contain("공격과 대결 → 저항"));
                    Assert.That(text.text, Does.Not.Contain("방어·빈칸 → 체력"));
                }

                LegacySkill conditionalRecovery = LegacySkillDefinitions.Skill(7);
                hud.ShowExplanation(conditionalRecovery, false);
                Assert.That(effect.text, Does.Contain("같은 칸 상대가 타격일 때"));
                Assert.That(effect.text, Does.Contain("ACT 회복 +2"));
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator LobbyLabels_ShowActualRolesWithoutInventingUtilityEffects()
        {
            yield return null;
            var parent = new GameObject("Lobby Role Label Test");
            CampaignLobbyHud hud = null;
            try
            {
                var run = new CampaignRun();
                // The acquired skills reach the loadout through the curriculum.
                foreach (string node in new[] { "horizontal-cut", "breathing", "suppleness" }) Learn(run, node);
                hud = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                hud.Show(run);
                hud.ShowTab(LobbyTab.Loadout);
                yield return null;

                FindButton(hud.Root.transform, "Loadout Owned Skill 1").onClick.Invoke();
                Assert.That(FindTextUnder(hud.Root.transform, "Loadout Selected Detail", "Loadout Detail Role").text,
                    Does.Contain("ACT 회복"));
                FindButton(hud.Root.transform, "Loadout Owned Skill 7").onClick.Invoke();
                Assert.That(FindTextUnder(hud.Root.transform, "Loadout Selected Detail", "Loadout Detail Role").text,
                    Does.Contain("조건부 ACT"));
                string loadoutGuide = FindTextUnder(hud.Root.transform, "Loadout Panel", "Tab Subtitle").text;
                Assert.That(loadoutGuide, Does.Contain("각 열 3개"));
                Assert.That(loadoutGuide, Does.Not.Contain("공격끼리 대결할 때만 저항 피해"));
                AssertPureRole(hud.Root.transform, 14, "단타");
                AssertPureRole(hud.Root.transform, 17, "방어");
                FindButton(hud.Root.transform, "Loadout Lane W").onClick.Invoke();
                AssertPureRole(hud.Root.transform, 32, "방어");

                hud.ShowTab(LobbyTab.Curriculum);
                yield return null;
                foreach (string node in new[] { "horizontal-cut", "breathing", "suppleness" })
                {
                    FindButton(hud.Root.transform, "Curriculum Node " + node).onClick.Invoke();
                    AssertNoInventedUtility(FindTextUnder(hud.Root.transform, "Curriculum Selected Detail", "Curriculum Detail Purpose").text
                        + "\n" + FindTextUnder(hud.Root.transform, "Curriculum Selected Detail", "Curriculum Detail Effect").text);
                }
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator CurriculumSkillKeepsCatalogRoleAndIcon_UnknownLookupsStayEmpty()
        {
            yield return null;
            LegacySkill catalog = null;
            foreach (LegacySkill skill in CampaignSkillCatalog.AcquisitionSkills) if (skill.Id == 14) catalog = skill;
            Assert.That(catalog, Is.Not.Null);
            var run = new CampaignRun();
            Learn(run, "horizontal-cut");
            CampaignOwnedSkill owned = run.OwnedSkills[run.OwnedSkills.Count - 1];
            Assert.That(owned.SkillId, Is.EqualTo(14));
            Assert.That(owned.Skill.IconId, Is.EqualTo(catalog.IconId));
            Assert.That(new LegacyDuelArt().GetSkillIcon(owned.Skill.IconId), Is.SameAs(new LegacyDuelArt().GetSkillIcon(catalog.IconId)));
            Assert.That(LegacySkillRoles.GetShortLabel(owned.Skill), Is.EqualTo(LegacySkillRoles.GetShortLabel(catalog)));
            Assert.That(LegacySkillRoles.GetShortLabel(owned.Skill), Is.EqualTo("단타"));

            Assert.That(new LegacyDuelArt().GetSkillIcon(16), Is.Null);
            Assert.That(LegacySkillRoles.Get(null), Is.EqualTo(LegacySkillRole.None));
            Assert.That(LegacySkillRoles.GetShortLabel(null), Is.Empty);
            Assert.That(LegacySkillRoles.GetDetail(null), Is.Empty);
        }

        private static void Learn(CampaignRun run, string nodeId)
        {
            Assert.That(run.TrySelectCurriculumNode(nodeId), Is.True, nodeId);
            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.Curriculum.IsCompleted(nodeId), Is.True, nodeId);
        }

        private static void AssertPureRole(Transform root, int skillId, string role)
        {
            FindButton(root, "Loadout Owned Skill " + skillId).onClick.Invoke();
            string purpose = FindTextUnder(root, "Loadout Selected Detail", "Loadout Detail Role").text;
            string effect = FindTextUnder(root, "Loadout Selected Detail", "Loadout Detail Effect").text;
            Assert.That(purpose, Does.Contain(role));
            AssertNoInventedUtility(purpose + "\n" + effect);
        }

        private static void AssertNoInventedUtility(string detail)
        {
            Assert.That(detail, Does.Not.Contain("ACT 회복"));
            Assert.That(detail, Does.Not.Contain("조건부 ACT"));
            Assert.That(detail, Does.Not.Contain("후속 위력"));
            Assert.That(detail, Does.Not.Contain("피해 감소"));
        }

        private static Text FindTextUnder(Transform root, string parentName, string textName)
        {
            Transform parent = FindNamed(root, parentName);
            foreach (Text text in parent.GetComponentsInChildren<Text>(true))
                if (text.name == textName) return text;
            Assert.Fail("Missing text " + textName + " under " + parentName + ".");
            return null;
        }

        private static Button FindButton(Transform root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Missing HUD button " + name + ".");
            return null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            Assert.Fail("Missing HUD node " + name + ".");
            return null;
        }
    }
}
