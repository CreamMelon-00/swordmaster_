using System.Collections;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class LegacyAnimationPlayModeTests
    {
        [UnityTest]
        public IEnumerator Idle_AdvancesNewPlayerLoop_AndPreservesAllOriginalEnemyFrames()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            controller.enabled = false;
            try
            {
                var arena = controller.ArenaView;
                Assert.That(arena.HasMobStudentAnimations, Is.True);
                for (var frame = 0; frame <= 8; frame++)
                {
                    if (frame > 0) arena.Tick(1f / 12f + .00001f, 1f / 12f);
                    AssertFrame(arena, "pa_player_idle-Sheet", "pa_enemy_1_i-Sheet", frame % 8);
                }
            }
            finally { controller.RestartMatch(); controller.enabled = true; }
        }

        [UnityTest]
        public IEnumerator EveryOriginalSkill_ChangesPoseAtImpact_AndRepeatsEveryHitForBothActors()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            controller.enabled = false;
            try
            {
                var arena = controller.ArenaView;
                foreach (var skill in LegacyInitialSkills.All)
                {
                    arena.Reset();
                    arena.BeginSlot(skill, skill);
                    var sheets = SheetsFor(skill.Property);
                    for (var hit = 0; hit < skill.AttackCount; hit++)
                    {
                        AssertFrame(arena, sheets[0], sheets[1], 0, hit);
                        arena.Tick(LegacyArenaView.OriginalImpactTime + .00001f, 0);
                        AssertFrame(arena, sheets[0], sheets[1], 1, hit);
                        arena.Tick(LegacyArenaView.OriginalImpactTime + .00001f, 0);
                    }
                    Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-upper-"));
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("pa_enemy_1_i-Sheet_"));
                }
            }
            finally { controller.RestartMatch(); controller.enabled = true; }
        }

        [UnityTest]
        public IEnumerator Animation_UsesScaledClock_AndResetRestoresIdleInsteadOfLeavingAttackPose()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            controller.enabled = false;
            try
            {
                var arena = controller.ArenaView;
                arena.BeginSlot(LegacyInitialSkills.All[0], null);
                AssertFrame(arena, "pa_player_slash-Sheet", "pa_enemy_1_i-Sheet", 0);
                arena.Tick(0, .5f);
                AssertFrame(arena, "pa_player_slash-Sheet", "pa_enemy_1_i-Sheet", 0);
                arena.Tick(LegacyArenaView.OriginalImpactTime + .00001f, .5f);
                AssertFrame(arena, "pa_player_slash-Sheet", "pa_enemy_1_i-Sheet", 1);
                arena.Reset();
                AssertFrame(arena, "pa_player_idle-Sheet", "pa_enemy_1_i-Sheet", 0);
                arena.BeginSlot(LegacyInitialSkills.All[0], null);
                arena.EndTurn();
                arena.Tick(0, 0);
                AssertFrame(arena, "pa_player_idle-Sheet", "pa_enemy_1_i-Sheet", 0);
            }
            finally { controller.RestartMatch(); controller.enabled = true; }
        }

        [UnityTest]
        public IEnumerator UnequalAttackCounts_ReturnEachActorToIdleIndependently()
        {
            yield return null;
            var controller = FindPrototype();
            controller.RestartMatch();
            controller.enabled = false;
            try
            {
                var arena = controller.ArenaView;
                arena.BeginSlot(LegacyInitialSkills.All[0], LegacyInitialSkills.All[2]);
                arena.Tick(LegacyArenaView.OriginalImpactTime - .001f, 0);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("slash-1-upper-2"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("pa_enemy_1_st-Sheet_0"));
                arena.Tick(.002f, 0);
                AssertFrame(arena, "pa_player_slash-Sheet", "pa_enemy_1_st-Sheet", 1);
                arena.Tick(LegacyArenaView.OriginalImpactTime, 0);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-upper-"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("pa_enemy_1_st-Sheet_0"));
                arena.Tick(LegacyArenaView.OriginalImpactTime, 0);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-upper-"));
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("pa_enemy_1_st-Sheet_1"));
                arena.Tick(.26f, 0);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("pa_enemy_1_i-Sheet_"));
            }
            finally { controller.RestartMatch(); controller.enabled = true; }
        }

        private static string[] SheetsFor(LegacySkillProperty property)
        {
            switch (property)
            {
                case LegacySkillProperty.Slash: return new[] { "pa_player_slash-Sheet", "pa_enemy_1_s-Sheet" };
                case LegacySkillProperty.Penetrate: return new[] { "pa_player_sting1-Sheet", "pa_enemy_1_st-Sheet" };
                case LegacySkillProperty.Hit: return new[] { "pa_player_nike-Sheet", "pa_enemy_1_n-Sheet" };
                default: return new[] { "pa_player_g-Sheet", "pa_enemy_1_g-Sheet" };
            }
        }

        private static void AssertFrame(LegacyArenaView arena, string playerSheet, string enemySheet, int frame, int hitIndex = 0)
        {
            Assert.That(arena.PlayerRenderer.sprite, Is.Not.Null);
            Assert.That(arena.EnemyRenderer.sprite, Is.Not.Null);
            if (playerSheet == "pa_player_idle-Sheet")
                Assert.That(arena.PlayerRenderer.sprite.name,
                    Is.EqualTo("idle-upper-" + (Mathf.FloorToInt(frame / 12f / MobStudentAnimationSet.IdleFrameDuration) % 4 + 1)));
            else if (playerSheet == "pa_player_g-Sheet")
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-upper-"));
            else
            {
                string type = playerSheet == "pa_player_slash-Sheet" ? "slash"
                    : playerSheet == "pa_player_sting1-Sheet" ? "pierce" : "blunt";
                Assert.That(arena.PlayerRenderer.sprite.name,
                    Is.EqualTo(type + "-" + (hitIndex % 3 + 1) + "-upper-" + (frame == 0 ? 1 : 3)));
            }
            Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo(enemySheet + "_" + frame));
        }

        private static DuelPrototypeController FindPrototype()
        {
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }
    }
}
