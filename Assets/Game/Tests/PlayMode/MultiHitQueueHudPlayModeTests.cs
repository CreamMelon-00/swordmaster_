using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class MultiHitQueueHudPlayModeTests
    {
        [UnityTest]
        public IEnumerator UnfinishedMultiHit_StaysVisibleWhenPresentationWaitsPastItsNominalDuration()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyQueuedDuel duel = CreateDuel();
                duel.Commit();
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                Assert.That(slot.HitCount, Is.EqualTo(3));
                fixture.Hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .71f);
                fixture.Refresh(duel, 0, .05f);
                duel.ResolveNextHit();
                Assert.That(slot.HitsResolved, Is.EqualTo(1));

                // Pursuit may hold the controller's impact clock while HUD time
                // continues. Remaining hits still own this queue card.
                foreach (float delta in new[] { .2f, .2f, .3f, .3f, .5f })
                {
                    fixture.Refresh(duel, 0, delta);
                    Assert.That(slot.IsResolved, Is.False);
                    AssertVisible(fixture.Hud, "Player Requests", 0);
                    AssertVisible(fixture.Hud, "Enemy Requests", 0);
                }

                duel.ResolveNextHit();
                fixture.Refresh(duel, 0, .3f);
                AssertVisible(fixture.Hud, "Player Requests", 0);
                AssertVisible(fixture.Hud, "Enemy Requests", 0);
                duel.ResolveNextHit();
                fixture.Refresh(duel, 0, .3f);
                Assert.That(slot.IsResolved, Is.True);
                AssertVisible(fixture.Hud, "Player Requests", 0);
                AssertVisible(fixture.Hud, "Enemy Requests", 0);
            }
        }

        [UnityTest]
        public IEnumerator NextQueuedSkill_DoesNotDisappearAndReappearDuringTheBetweenSkillsPause()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyQueuedDuel duel = CreateDuel();
                duel.Commit();
                LegacyCurrentSlot first = duel.BeginNextSlot();
                fixture.Hud.SetCurrentSkills(first.PlayerSkill, first.EnemySkill, .71f);
                while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
                fixture.Refresh(duel, 0, 1f);
                duel.CompleteCurrentSlot();

                // PrepareNextSlot advances the highlight before the new slot's
                // animation begins. Its pulse still has the old elapsed time.
                fixture.Refresh(duel, 1, .1f);
                Assert.That(duel.CurrentSlot, Is.Null);
                foreach (string queue in new[] { "Player Requests", "Enemy Requests" })
                {
                    Assert.That(Card(fixture.Hud, queue, 0).gameObject.activeSelf, Is.False,
                        "A completed skill must still leave the visible queue.");
                    AssertVisible(fixture.Hud, queue, 1);
                }

                LegacyCurrentSlot next = duel.BeginNextSlot();
                fixture.Hud.SetCurrentSkills(next.PlayerSkill, next.EnemySkill, .4f);
                fixture.Refresh(duel, 1, 0f);
                AssertVisible(fixture.Hud, "Player Requests", 1);
                AssertVisible(fixture.Hud, "Enemy Requests", 1);
            }
        }

        private static LegacyQueuedDuel CreateDuel()
        {
            var freeSkills = new List<LegacySkill>();
            foreach (LegacySkill source in LegacyInitialSkills.All)
                freeSkills.Add(new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower,
                    source.Kind, source.Property, source.AttackCount, source.LaneIndex,
                    source.Description, source.AnimationName, source.IconId));
            var duel = new LegacyQueuedDuel(1000, 1000, 1000, 1000, freeSkills,
                new[] { freeSkills[2], freeSkills[0] }, new[] { 2 }, 1);
            Assert.That(duel.TryQueueLane(1), Is.True);
            Assert.That(duel.TryQueueLane(0), Is.True);
            return duel;
        }

        private static RectTransform Card(LegacyCombatHud hud, string queueName, int index)
        {
            Transform queue = hud.Root.transform.Find(queueName);
            Assert.That(queue, Is.Not.Null);
            foreach (Transform child in queue)
                if (child.name == "Queued Skill" && index-- == 0)
                    return child.GetComponent<RectTransform>();
            Assert.Fail("Missing queued skill card in " + queueName);
            return null;
        }

        private static void AssertVisible(LegacyCombatHud hud, string queueName, int index)
        {
            RectTransform card = Card(hud, queueName, index);
            Assert.That(card.gameObject.activeInHierarchy, Is.True);
            Image icon = card.Find("Icon").GetComponent<Image>();
            Assert.That(icon.enabled, Is.True);
            Assert.That(icon.sprite, Is.Not.Null);
            Assert.That(card.localScale.x, Is.InRange(1f, 1.2001f),
                "An unresolved or waiting skill must stay at least its normal readable size.");
            Assert.That(card.localScale.y, Is.EqualTo(card.localScale.x));
        }

        private sealed class HudFixture : System.IDisposable
        {
            private readonly GameObject owner = new GameObject("Multi Hit Queue HUD Fixture");
            private readonly Camera camera;
            private readonly Transform player, enemy;
            public readonly LegacyCombatHud Hud;

            public HudFixture()
            {
                camera = new GameObject("Fixture Camera").AddComponent<Camera>();
                camera.transform.SetParent(owner.transform, false);
                camera.transform.localPosition = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 6f;
                camera.enabled = false;
                player = new GameObject("Fixture Player").transform;
                enemy = new GameObject("Fixture Enemy").transform;
                player.SetParent(owner.transform, false);
                enemy.SetParent(owner.transform, false);
                player.localPosition = Vector3.left * 2f;
                enemy.localPosition = Vector3.right * 2f;
                Hud = new LegacyCombatHud(owner.transform, new LegacyDuelArt(), _ => { }, () => { }, () => { });
                Canvas.ForceUpdateCanvases();
            }

            public void Refresh(LegacyQueuedDuel duel, int slot, float elapsed)
            {
                Hud.Refresh(duel, 10f, true, slot, camera, player, enemy, elapsed, elapsed);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(owner);
            }
        }
    }
}
