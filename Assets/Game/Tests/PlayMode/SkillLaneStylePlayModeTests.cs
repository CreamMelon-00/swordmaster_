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
    public sealed class SkillLaneStylePlayModeTests
    {
        [UnityTest]
        public IEnumerator StyleNamesKeysAndDyes_MapToTheThreeChosenSwordSchools()
        {
            yield return null;
            string[] names = { "정공", "강공", "기교" }, keys = { "Q", "W", "E" };
            var papers = new HashSet<Color>();
            for (int lane = 0; lane < 3; lane++)
            {
                Assert.That(SkillLaneStyle.Name(lane), Is.EqualTo(names[lane]));
                Assert.That(SkillLaneStyle.FullName(lane), Is.EqualTo(names[lane] + " 검술"));
                Assert.That(SkillLaneStyle.Key(lane), Is.EqualTo(keys[lane]));
                Color paper = SkillLaneStyle.Paper(lane);
                Assert.That(paper.a, Is.EqualTo(1f).Within(.001f));
                Assert.That(papers.Add(paper), Is.True, "Each school should have a distinct opaque tag colour.");
            }
        }

        [UnityTest]
        public IEnumerator SchoolBadge_ReusesItsNodesAndClearOrEnemyDisplayCannotLeaveAnInputKey()
        {
            yield return null;
            var canvas = new GameObject("Reusable Skill School Badge Test", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var badge = new SkillLaneBadge(canvas.transform, new LegacyDuelArt().UIFont, "Test School", Vector2.zero);
                int nodes = badge.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int iteration = 0; iteration < 30; iteration++)
                {
                    int lane = iteration % 3;
                    badge.SetLane(lane);
                    Assert.That(badge.Label.text, Is.EqualTo(SkillLaneStyle.FullName(lane)));
                    Assert.That(Label(badge.Root.gameObject, "Style Key").text, Is.EqualTo(SkillLaneStyle.Key(lane)));
                    Assert.That(Label(badge.Root.gameObject, "Style Key").gameObject.activeInHierarchy, Is.True);
                    Assert.That(badge.Root.GetComponent<Image>().color, Is.EqualTo(SkillLaneStyle.Paper(lane)));
                    badge.SetLane(lane, false);
                    Assert.That(Label(badge.Root.gameObject, "Style Key").gameObject.activeInHierarchy, Is.False);
                }
                badge.Clear("기술을 선택하세요");
                Assert.That(badge.Label.text, Is.EqualTo("기술을 선택하세요"));
                Assert.That(Label(badge.Root.gameObject, "Style Key").text, Is.Empty);
                Assert.That(Label(badge.Root.gameObject, "Style Key").gameObject.activeInHierarchy, Is.False);
                badge.SetLane(1);
                Assert.That(badge.Label.text, Is.EqualTo("강공 검술"));
                Assert.That(Label(badge.Root.gameObject, "Style Key").text, Is.EqualTo("W"));
                Assert.That(badge.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                foreach (Graphic decoration in badge.Root.GetComponentsInChildren<Graphic>(true))
                    Assert.That(decoration.raycastTarget, Is.False, decoration.name + " is not an input surface.");
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatInformation_UsesLiveLaneRatherThanAttackTypeAndHidesEnemyInputKeys()
        {
            yield return null;
            var parent = new GameObject("Skill School Combat Information Test");
            LegacyCombatHud hud = null;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                hud.ShowExplanation(LegacyInitialSkills.All[0], false);
                GameObject player = Named(hud.Root, "Skill Explain").gameObject;
                int originalNodes = player.GetComponentsInChildren<Transform>(true).Length;
                foreach (LegacySkill skill in AllSkills())
                {
                    hud.ShowExplanation(skill, false);
                    Assert.That(Label(player, "Power Cost Property").text, Does.Contain(SkillLaneStyle.Name(skill.LaneIndex)), skill.Name);
                    Assert.That(Label(player, "Style Key").text, Is.EqualTo(SkillLaneStyle.Key(skill.LaneIndex)), skill.Name);
                    Assert.That(Label(player, "Style Key").gameObject.activeInHierarchy, Is.True);
                    Assert.That(Label(player, "Attack Type").text, Is.EqualTo(Type(skill.Property)), "Sword school does not replace attack type.");
                    Assert.That(Label(player, "ACT Value").text, Is.EqualTo(skill.Cost.ToString()));
                    Assert.That(Label(player, "Player Detail Values").text, Is.EqualTo(CampaignSkillText.Power(skill)));

                    hud.ShowExplanation(skill, true);
                    GameObject enemy = Named(hud.Root, "Enemy Skill Explain").gameObject;
                    Assert.That(Label(enemy, "Power Property").text, Does.Contain(SkillLaneStyle.Name(skill.LaneIndex)), skill.Name);
                    Transform enemyKey = Find(enemy, "Style Key");
                    Assert.That(enemyKey == null || !enemyKey.gameObject.activeInHierarchy, Is.True,
                        "The opponent's school must not appear to be controlled by a player input key.");
                    Assert.That(Label(enemy, "Attack Type").text, Is.EqualTo(Type(skill.Property)));
                }
                LegacySkill acquiredSlash = Skill(16);
                Assert.That(acquiredSlash.Property, Is.EqualTo(LegacySkillProperty.Slash));
                Assert.That(acquiredSlash.LaneIndex, Is.EqualTo(1));
                hud.ShowExplanation(acquiredSlash, false);
                Assert.That(Label(player, "Power Cost Property").text, Does.Contain("강공"));
                Assert.That(Label(player, "Style Key").text, Is.EqualTo("W"));
                Assert.That(Label(player, "Attack Type").text, Is.EqualTo("참격"));

                var alternateLane = new LegacySkill(1, "베기", 1, 4, 5, LegacySkillKind.Attack,
                    LegacySkillProperty.Slash, 1, 2, string.Empty, iconId: 1);
                hud.ShowExplanation(alternateLane, false);
                Assert.That(Label(player, "Power Cost Property").text, Does.Contain("기교"), "The live LaneIndex is authoritative even when the id and icon are unchanged.");
                Assert.That(Label(player, "Style Key").text, Is.EqualTo("E"));
                Assert.That(player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(originalNodes), "Changing school reuses its tag.");
                foreach (Text text in player.GetComponentsInChildren<Text>(true)) Assert.That(text.raycastTarget, Is.False);

                hud.HideExplanation();
                Assert.That(player.activeSelf, Is.False);
                hud.ShowExplanation(Skill(3), false);
                Assert.That(Label(player, "Style Key").text, Is.EqualTo("W"), "Reopening cannot retain the previous school's key.");
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadoutHeadingsFiltersAndSelectedDetails_ShowSchoolWithoutChangingThreeSlotRules()
        {
            yield return null;
            var parent = new GameObject("Skill School Loadout Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                int[] saved = EquippedIds(run);
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run); lobby.ShowTab(LobbyTab.Loadout);
                yield return null;
                GameObject detail = Named(lobby.Root, "Loadout Selected Detail").gameObject;
                for (int lane = 0; lane < 3; lane++)
                {
                    string key = SkillLaneStyle.Key(lane);
                    Assert.That(Label(lobby.Root, "Loadout Heading " + key).text, Does.Contain(SkillLaneStyle.FullName(lane)));
                    Assert.That(VisibleText(Named(lobby.Root, "Loadout Lane " + key).gameObject), Does.Contain(key + " " + SkillLaneStyle.Name(lane)));
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        string name = "Loadout Slot " + key + " " + slot;
                        GameObject card = Named(lobby.Root, name).gameObject;
                        Assert.That(card.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(3), "The compact slot remains order, name and ACT only.");
                        Click(lobby.Root, name);
                        Assert.That(Label(detail, "Detail Heading").text, Is.EqualTo(SkillLaneStyle.FullName(lane)));
                        Assert.That(Label(detail, "Style Key").text, Is.EqualTo(key));
                        Assert.That(run.HasLoadoutChanges, Is.False, "Reading a school tag does not reorder or replace skills.");
                    }
                }
                Assert.That(Label(lobby.Root, "Loadout Counts").text, Is.EqualTo("Q 3/3  ·  W 3/3  ·  E 3/3"));
                CollectionAssert.AreEqual(saved, EquippedIds(run));
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(Named(lobby.Root, "Loadout Save").GetComponent<Button>().interactable, Is.False);
            }
            finally { lobby?.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CurriculumDetail_ShowsTheGrantedSkillsSchoolWhileKeepingTypeAndPower()
        {
            yield return null;
            var parent = new GameObject("Skill School Curriculum Test");
            CampaignLobbyHud lobby = null;
            try
            {
                var run = new CampaignRun();
                int[] saved = EquippedIds(run);
                lobby = new CampaignLobbyHud(parent.transform, new LegacyDuelArt(), null, null, null, null, null, null, null);
                lobby.Show(run); lobby.ShowTab(LobbyTab.Curriculum);
                yield return null;
                GameObject detail = Named(lobby.Root, "Curriculum Selected Detail").gameObject;
                int nodes = detail.GetComponentsInChildren<Transform>(true).Length;
                var shown = new HashSet<int>();
                foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
                {
                    LegacySkill skill = Skill(node.SkillIds[0]);
                    Click(lobby.Root, "Curriculum Node " + node.Id);
                    Assert.That(Label(detail, "Curriculum Detail Heading").text, Is.EqualTo(SkillLaneStyle.FullName(skill.LaneIndex)), node.Id);
                    Assert.That(Label(detail, "Style Key").text, Is.EqualTo(SkillLaneStyle.Key(skill.LaneIndex)), node.Id);
                    Assert.That(Label(detail, "Attack Type").text, Is.EqualTo(Type(skill.Property)), node.Id);
                    Assert.That(Label(detail, "Curriculum Detail Values").text, Is.EqualTo(CampaignSkillText.Power(skill)), node.Id);
                    shown.Add(skill.Id);
                }
                Assert.That(shown.Count, Is.EqualTo(CampaignSkillCatalog.AcquisitionSkills.Count), "Every acquirable skill has a node.");
                // The school follows the granted skill's lane, not the curriculum branch it is taught in.
                Click(lobby.Root, "Curriculum Node one-stroke");
                Assert.That(Label(detail, "Curriculum Detail Heading").text, Is.EqualTo("강공 검술"));
                Assert.That(Label(detail, "Style Key").text, Is.EqualTo("W"));
                Assert.That(Label(detail, "Attack Type").text, Is.EqualTo("참격"));
                Click(lobby.Root, "Curriculum Node breathing");
                Assert.That(Label(detail, "Curriculum Detail Heading").text, Is.EqualTo("정공 검술"));
                Assert.That(Label(detail, "Style Key").text, Is.EqualTo("Q"));
                Assert.That(Label(detail, "Attack Type").text, Is.EqualTo("방어"));
                Click(lobby.Root, "Curriculum Node advance");
                Assert.That(Label(detail, "Curriculum Detail Heading").text, Is.EqualTo("기교 검술"));
                Assert.That(Label(detail, "Style Key").text, Is.EqualTo("E"));
                Assert.That(Label(detail, "Attack Type").text, Is.EqualTo("관통"));
                Assert.That(detail.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                Assert.That(Label(detail, "Style Key").raycastTarget, Is.False);
                Assert.That(run.Curriculum.Active, Is.Null); Assert.That(run.OwnedSkills.Count, Is.EqualTo(9));
                Assert.That(run.HasLoadoutChanges, Is.False);
                CollectionAssert.AreEqual(saved, EquippedIds(run));
            }
            finally { lobby?.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatInputs_KeepStandaloneQweKeysAndAddNonBlockingSchoolNames()
        {
            yield return null;
            var parent = new GameObject("Skill School Input Label Test");
            LegacyCombatHud hud = null;
            int queuedLane = -1;
            try
            {
                hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), lane => queuedLane = lane, null, null);
                for (int lane = 0; lane < 3; lane++)
                {
                    GameObject current = Named(hud.Root, "Current " + SkillLaneStyle.Key(lane)).gameObject;
                    Text key = Label(current, "Key"), style = Label(current, "Style Name");
                    Assert.That(key.text, Is.EqualTo(SkillLaneStyle.Key(lane)), "The input key remains an independent single-letter label.");
                    Assert.That(style.text, Is.EqualTo(SkillLaneStyle.Name(lane)));
                    Assert.That(key.raycastTarget, Is.False); Assert.That(style.raycastTarget, Is.False);
                    current.GetComponent<Button>().onClick.Invoke();
                    Assert.That(queuedLane, Is.EqualTo(lane), "School decoration does not change the lane button callback.");
                }
            }
            finally { hud?.Dispose(); Object.Destroy(parent); }
            yield return null;
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
        private static int[] EquippedIds(CampaignRun run)
        {
            var result = new int[9];
            for (int lane = 0; lane < 3; lane++) for (int slot = 0; slot < 3; slot++) result[lane * 3 + slot] = run.GetEquippedLane(lane)[slot].SkillId;
            return result;
        }
        private static string VisibleText(GameObject root)
        { string result = string.Empty; foreach (Text text in root.GetComponentsInChildren<Text>()) result += "\n" + text.text; return result; }
        private static void Click(GameObject root, string name) => Named(root, name).GetComponent<Button>().onClick.Invoke();
        private static Text Label(GameObject root, string name) => Named(root, name).GetComponent<Text>();
        private static Transform Find(GameObject root, string name)
        { foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) if (node.name == name) return node; return null; }
        private static Transform Named(GameObject root, string name)
        { Transform node = Find(root, name); Assert.That(node, Is.Not.Null, "Missing skill school node: " + name); return node; }
    }
}
