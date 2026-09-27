using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Combat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CampaignMaintenanceHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator Maintenance_ShowsRewardOwnedSkillsAndInheritedIcons()
        {
            yield return null;
            var parent = new GameObject("Maintenance UI Test");
            var run = StartedRun();
            Assert.That(run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory), Is.True);
            using (var hud = new CampaignMaintenanceHud(parent.transform, new LegacyDuelArt(), null, null, null, null))
            {
                hud.Show(run);
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(hud.HasRequiredAssets, Is.True);
                Assert.That(hud.Root.GetComponent<Canvas>().sortingOrder, Is.EqualTo(200));
                Assert.That(FindText(hud, "Wallet").text, Does.Contain("60"));
                Assert.That(FindText(hud, "Clear Reward").text, Does.Contain("+60"));
                Assert.That(FindButton(hud, "Upgrade Skill 1").interactable, Is.True);
                Assert.That(hud.Root.GetComponentsInChildren<Button>().Length,
                    Is.EqualTo(run.OwnedSkills.Count + run.Offers.Count + 1));
                foreach (var image in hud.Root.GetComponentsInChildren<Image>())
                    if (image.name == "Skill Icon") Assert.That(image.sprite, Is.Not.Null);
                Assert.That(FindText(hud, "Next Stage").text, Does.Contain("적 체력 95"));
                hud.Hide();
                Assert.That(hud.IsVisible, Is.False);
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Upgrade_ClickRequestsTransactionAndRefreshesAffordability()
        {
            yield return null;
            var parent = new GameObject("Maintenance Upgrade Test");
            var run = StartedRun();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            int requested = -1;
            CampaignMaintenanceHud hud = null;
            hud = new CampaignMaintenanceHud(parent.transform, new LegacyDuelArt(), null, id =>
            {
                requested = id;
                Assert.That(run.TryUpgradeSkill(id), Is.True);
                hud.Show(run);
            }, null, null);
            using (hud)
            {
                hud.Show(run);
                FindButton(hud, "Upgrade Skill 1").onClick.Invoke();
                Assert.That(requested, Is.EqualTo(1));
                Assert.That(run.Currency, Is.EqualTo(30));
                Assert.That(FindText(hud, "Wallet").text, Does.Contain("30"));
                Assert.That(FindButton(hud, "Upgrade Skill 1").interactable, Is.False);
                Assert.That(FindButton(hud, "Upgrade Skill 2").interactable, Is.True);
                foreach (var offer in run.Offers)
                    Assert.That(FindButton(hud, "Acquire Skill " + offer.SkillId).interactable,
                        Is.EqualTo(run.Currency >= offer.Price));
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Acquire_ClickKeepsSkillIdentityAndRemovesPurchasedOffer()
        {
            yield return null;
            var parent = new GameObject("Maintenance Acquisition Test");
            var run = StartedRun();
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            var offer = run.Offers[0];
            Assert.That(offer.Price, Is.LessThanOrEqualTo(run.Currency));
            int oldOwned = run.OwnedSkills.Count;
            int oldOffers = run.Offers.Count;
            int requested = -1;
            CampaignMaintenanceHud hud = null;
            hud = new CampaignMaintenanceHud(parent.transform, new LegacyDuelArt(), id =>
            {
                requested = id;
                Assert.That(run.TryAcquireSkill(id), Is.True);
                hud.Show(run);
            }, null, null, null);
            using (hud)
            {
                hud.Show(run);
                FindButton(hud, "Acquire Skill " + offer.SkillId).onClick.Invoke();
                Assert.That(requested, Is.EqualTo(offer.SkillId));
                Assert.That(run.OwnedSkills.Count, Is.EqualTo(oldOwned + 1));
                Assert.That(run.Offers.Count, Is.EqualTo(oldOffers - 1));
                Assert.That(FindButton(hud, "Upgrade Skill " + offer.SkillId), Is.Not.Null);
                foreach (var button in hud.Root.GetComponentsInChildren<Button>())
                    Assert.That(button.name, Is.Not.EqualTo("Acquire Skill " + offer.SkillId));
            }
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Battle_HidesOverlayAndDisposeReleasesIt()
        {
            yield return null;
            var parent = new GameObject("Maintenance Lifecycle Test");
            var run = StartedRun();
            var hud = new CampaignMaintenanceHud(parent.transform, new LegacyDuelArt(), null, null, null, null);
            run.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
            hud.Show(run);
            Assert.That(hud.IsVisible, Is.True);
            run.TryStartNextStage();
            hud.Show(run);
            Assert.That(hud.IsVisible, Is.False);
            var root = hud.Root;
            hud.Dispose();
            hud.Dispose();
            yield return null;
            Assert.That(root == null, Is.True);
            Object.Destroy(parent);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedAndCompleted_ShowCorrectContinuationWithoutPurchases()
        {
            yield return null;
            var parent = new GameObject("Maintenance Result Test");
            int continued = 0, restarted = 0;
            using (var hud = new CampaignMaintenanceHud(parent.transform, new LegacyDuelArt(), null, null,
                () => continued++, () => restarted++))
            {
                var failed = StartedRun();
                failed.TryCompleteBattle(DuelMatchOutcome.EnemyVictory);
                hud.Show(failed);
                Assert.That(FindText(hud, "Result Hint").text, Does.Contain("유지"));
                Assert.That(hud.Root.GetComponentsInChildren<Button>().Length, Is.EqualTo(2));
                FindButton(hud, "Continue Run").onClick.Invoke();
                FindButton(hud, "Restart Run").onClick.Invoke();
                Assert.That(continued, Is.EqualTo(1));
                Assert.That(restarted, Is.EqualTo(1));

                var complete = StartedRun();
                for (int i = 1; i <= complete.StageCount; i++)
                {
                    complete.TryCompleteBattle(DuelMatchOutcome.PlayerVictory);
                    if (i < complete.StageCount) complete.TryStartNextStage();
                }
                hud.Show(complete);
                Assert.That(FindText(hud, "Result Summary").text, Does.Contain("8개"));
                Assert.That(hud.Root.GetComponentsInChildren<Button>().Length, Is.EqualTo(1));
                FindButton(hud, "Continue Run").onClick.Invoke();
                Assert.That(continued, Is.EqualTo(2));
            }
            Object.Destroy(parent);
            yield return null;
        }

        private static CampaignRun StartedRun()
        {
            var run = new CampaignRun();
            Assert.That(run.TryStartStage(1), Is.True);
            return run;
        }

        private static Button FindButton(CampaignMaintenanceHud hud, string name)
        {
            foreach (var button in hud.Root.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            Assert.Fail("Missing visible button: " + name);
            return null;
        }

        private static Text FindText(CampaignMaintenanceHud hud, string name)
        {
            foreach (var text in hud.Root.GetComponentsInChildren<Text>())
                if (text.name == name) return text;
            Assert.Fail("Missing visible label: " + name);
            return null;
        }
    }
}
