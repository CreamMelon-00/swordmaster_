using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class VersusHiddenInformationPlayModeTests
    {
        [UnityTest]
        public IEnumerator InspectionMasksRepeatedHiddenSkills_AndRestoresKnownAndCampaignInformation()
        {
            yield return null;
            foreach (int local in new[] { 0, 1 })
            {
                var parent = new GameObject("Hidden Information Test");
                LegacyCombatHud hud = null;
                try
                {
                    var left = Skill(99161, "왼쪽 비밀 기술");
                    var right = Skill(99162, "오른쪽 비밀 기술");
                    var match = new LocalVersusMatch(new[] { left }, new[] { right },
                        1000, 1000, 1000, 1000, randomSeed: 7);
                    for (int index = 0; index < 3; index++) Assert.That(match.TryQueueLane(0, 0), Is.True);
                    Assert.That(match.TryCommit(0), Is.True);
                    for (int index = 0; index < 3; index++) Assert.That(match.TryQueueLane(1, 0), Is.True);
                    var view = new LocalVersusPerspective(match, local);
                    hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), null, null, null);
                    Refresh(hud, view);
                    LegacySkill enemy = view.EnemyQueue[0], own = view.PlayerQueue[0];

                    hud.SetInspectedSlot(0);
                    hud.ShowQueueComparison(enemy, own);
                    Assert.That(Name(hud).text, Is.EqualTo(enemy.Name));

                    // All three cards share one skill object. The inspected slot decides what may be read.
                    hud.SetInspectedSlot(2);
                    hud.ShowQueueComparison(view.EnemyQueue[2], own);
                    AssertMasked(hud, enemy.Name);
                    Assert.That(hud.ComparisonPlayerRoot.Find("Name").GetComponent<Text>().text, Is.EqualTo(own.Name));
                    Assert.That(hud.EnemyForecastNote, Is.Null);
                    hud.ShowExplanation(enemy, true);
                    AssertMasked(hud, enemy.Name);

                    hud.SetInspectedSlot(0);
                    hud.ShowQueueComparison(enemy, own);
                    Assert.That(Name(hud).text, Is.EqualTo(enemy.Name));
                    Assert.That(hud.ExplanationRoot.Find("Explanation Skill Icon").GetComponent<Image>().enabled, Is.True);

                    var campaign = new LegacyQueuedDuel(1000, 1000, 1000, 1000,
                        new[] { own }, new[] { enemy }, new[] { 3 }, meshPercent: 0);
                    Refresh(hud, campaign);
                    hud.SetInspectedSlot(2);
                    hud.ShowQueueComparison(campaign.EnemyQueue[2], null);
                    Assert.That(Name(hud).text, Is.EqualTo(enemy.Name), "Campaign inspection remains fully revealed.");
                    Assert.That(hud.ExplanationRoot.Find("Explanation Skill Icon").GetComponent<Image>().enabled, Is.True);
                }
                finally { hud?.Dispose(); Object.Destroy(parent); }
            }
        }

        private static LegacySkill Skill(int id, string name)
            => new LegacySkill(id, name, 0, 97, 97, LegacySkillKind.Attack, LegacySkillProperty.Slash,
                7, 0, "숨겨진 고유 설명", iconId: 1);

        private static Text Name(LegacyCombatHud hud) => hud.ExplanationRoot.Find("Name").GetComponent<Text>();

        private static void AssertMasked(LegacyCombatHud hud, string hiddenName)
        {
            Assert.That(hud.ExplanationRoot.gameObject.activeSelf, Is.True);
            Assert.That(Name(hud).text, Is.EqualTo("참격"));
            Image icon = hud.ExplanationRoot.Find("Explanation Skill Icon").GetComponent<Image>();
            Assert.That(icon.enabled, Is.False);
            Assert.That(icon.sprite, Is.Null);
            string visible = string.Join(" ", hud.ExplanationRoot.GetComponentsInChildren<Text>(true)
                .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));
            Assert.That(visible, Does.Not.Contain(hiddenName).And.Not.Contain("97").And.Not.Contain("숨겨진 고유 설명"));
        }

        private static void Refresh(LegacyCombatHud hud, ILegacyDuelView view)
        {
            hud.Refresh(view, 20f, false, -1, null, null, null);
            Canvas.ForceUpdateCanvases();
        }
    }
}
