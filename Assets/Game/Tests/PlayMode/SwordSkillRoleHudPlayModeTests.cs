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
                LegacySkill actRecovery = LegacyInitialSkills.All[0];
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

                LegacySkill conditionalRecovery = LegacyInitialSkills.All[6];
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

                hud.ShowTab(LobbyTab.Shop);
                yield return null;
                AssertPureRole(hud.Root.transform, 14, "단타");
                AssertPureRole(hud.Root.transform, 17, "방어");
                AssertPureRole(hud.Root.transform, 32, "방어");
            }
            finally
            {
                hud?.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator UpgradeKeepsRoleAndIconIdentity_UnknownLookupsStayEmpty()
        {
            yield return null;
            var run = new CampaignRun();
            CampaignOwnedSkill owned = run.OwnedSkills[0];
            int iconId = owned.Skill.IconId;
            string role = LegacySkillRoles.GetShortLabel(owned.Skill);
            Sprite icon = new LegacyDuelArt().GetSkillIcon(iconId);

            Assert.That(run.TryStartStage(1), Is.True);
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            Assert.That(run.ReturnToLobby(), Is.True);
            Assert.That(run.TryUpgradeSkill(owned.SkillId), Is.True);
            Assert.That(owned.Skill.IconId, Is.EqualTo(iconId));
            Assert.That(new LegacyDuelArt().GetSkillIcon(owned.Skill.IconId), Is.SameAs(icon));
            Assert.That(LegacySkillRoles.GetShortLabel(owned.Skill), Is.EqualTo(role));

            Assert.That(new LegacyDuelArt().GetSkillIcon(16), Is.Null);
            Assert.That(LegacySkillRoles.Get(null), Is.EqualTo(LegacySkillRole.None));
            Assert.That(LegacySkillRoles.GetShortLabel(null), Is.Empty);
            Assert.That(LegacySkillRoles.GetDetail(null), Is.Empty);
        }

        private static void AssertPureRole(Transform root, int skillId, string role)
        {
            FindButton(root, "Shop Skill " + skillId).onClick.Invoke();
            string purpose = FindTextUnder(root, "Shop Selected Detail", "Shop Detail Purpose").text;
            string effect = FindTextUnder(root, "Shop Selected Detail", "Shop Detail Effect").text;
            string detail = purpose + "\n" + effect;
            Assert.That(purpose, Does.Contain(role));
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
