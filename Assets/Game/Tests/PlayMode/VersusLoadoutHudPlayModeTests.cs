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
    public sealed class VersusLoadoutHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator Preparation_CanSelectReplaceConfirmAndCancelToRestore()
        {
            yield return null;
            var host = new GameObject("Versus Loadout Test Host");
            int confirmed = 0, cancelled = 0, changed = 0;
            var loadout = new VersusLoadout();
            LegacySkill qSkill = LegacySkillDefinitions.AcquisitionSkills
                .First(skill => skill.LaneIndex == 0);
            int originalQ = loadout.GetSkillId(0, 0);
            using (var art = new LegacyDuelArt())
            using (var hud = new VersusLoadoutHud(host.transform, art, loadout,
                () => confirmed++, () => cancelled++, () => changed++))
            {
                hud.Show("1P 기술 편성", "준비 완료");
                Assert.That(hud.IsVisible, Is.True);
                Assert.That(Label(hud.Root, "Versus Loadout Heading").text, Is.EqualTo("1P 기술 편성"));
                Button(hud.Root, "Versus Available Skill " + qSkill.Id).onClick.Invoke();
                Assert.That(Named(hud.Root, "Versus Slot Target Q 1").activeSelf, Is.True);
                Assert.That(Named(hud.Root, "Versus Slot Target W 1").activeSelf, Is.False);
                Assert.That(Label(hud.Root, "Versus Detail Name").text, Is.EqualTo(qSkill.Name));
                Button(hud.Root, "Versus Slot Q 1").onClick.Invoke();
                Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(qSkill.Id));
                Assert.That(changed, Is.EqualTo(1));
                hud.ConfirmButton.onClick.Invoke();
                Assert.That(confirmed, Is.EqualTo(1));
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(loadout.ToSkills()[0].Id, Is.EqualTo(qSkill.Id));

                hud.Show("2P 기술 편성", "준비 완료");
                Button(hud.Root, "Versus Available Skill " + originalQ).onClick.Invoke();
                Button(hud.Root, "Versus Slot Q 1").onClick.Invoke();
                Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(originalQ));
                hud.CancelButton.onClick.Invoke();
                Assert.That(cancelled, Is.EqualTo(1));
                Assert.That(hud.IsVisible, Is.False);
                Assert.That(loadout.GetSkillId(0, 0), Is.EqualTo(qSkill.Id),
                    "Going back should restore the loadout shown when preparation opened.");
            }
            Object.Destroy(host);
        }

        private static GameObject Named(GameObject root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            Assert.Fail("Missing UI object: " + name);
            return null;
        }

        private static Text Label(GameObject root, string name)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Missing UI label: " + name);
            return null;
        }

        private static Button Button(GameObject root, string name)
            => Named(root, name).GetComponent<Button>();
    }
}
