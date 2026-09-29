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
        public IEnumerator Idle_AdvancesBothRemadeActorLoops()
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
                    Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith(
                        skill.Kind == LegacySkillKind.Defence ? "poses-block" : "idle-frame-"));
                    // Like the player, an enemy defense stays in its guard after its own frames.
                    Assert.That(arena.EnemyRenderer.sprite.name, skill.Kind == LegacySkillKind.Defence
                        ? Is.EqualTo("enemy-poses-block") : Does.StartWith("enemy-idle-frame-"));
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
        public IEnumerator UnequalAttackCounts_FinishedAttackGuardsUntilTheOpponentFinishes()
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
                Assert.That(arena.PlayerRenderer.sprite.name, Does.Match("^slash(?:-[23])?-frame-04$"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Match("^enemy-pierce(?:-[23])?-frame-04$"));
                arena.Tick(.002f, 0);
                AssertFrame(arena, "pa_player_slash-Sheet", "pa_enemy_1_st-Sheet", 1);
                arena.Tick(LegacyArenaView.OriginalImpactTime, 0);
                // The rules still trade resistance with the finished one-hit attack, so it guards, not idles.
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("poses-block"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Match("^enemy-pierce(?:-[23])?-frame-01$"));
                arena.Tick(LegacyArenaView.OriginalImpactTime, 0);
                Assert.That(arena.PlayerRenderer.sprite.name, Is.EqualTo("poses-block"));
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Match("^enemy-pierce(?:-[23])?-frame-05$"));
                arena.Tick(.26f, 0);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("idle-frame-"),
                    "Once the opponent's strikes end, the exchange is over.");

                arena.BeginSlot(LegacyInitialSkills.All[2], LegacyInitialSkills.All[0]);
                arena.Tick(LegacyArenaView.OriginalClipDuration + .001f, 0);
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"),
                    "The remade enemy holds its guard after a finished attack.");
                arena.Tick(LegacyArenaView.OriginalClipDuration * 2f, 0);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
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
                    Does.StartWith("idle-frame-"));
            else if (playerSheet == "pa_player_g-Sheet")
                Assert.That(arena.PlayerRenderer.sprite.name, Does.StartWith("poses-block"));
            else
            {
                string type = playerSheet == "pa_player_slash-Sheet" ? "slash"
                    : playerSheet == "pa_player_sting1-Sheet" ? "pierce" : "blunt";
                Assert.That(arena.PlayerRenderer.sprite.name,
                    Does.Match("^" + type + "(?:-[23])?-frame-" + (frame == 0 ? "01" : "05") + "$"));
            }
            if (enemySheet == "pa_enemy_1_i-Sheet")
                Assert.That(arena.EnemyRenderer.sprite.name, Does.StartWith("enemy-idle-frame-"));
            else if (enemySheet == "pa_enemy_1_g-Sheet")
                Assert.That(arena.EnemyRenderer.sprite.name, Is.EqualTo("enemy-poses-block"));
            else
            {
                string type = enemySheet == "pa_enemy_1_s-Sheet" ? "slash"
                    : enemySheet == "pa_enemy_1_st-Sheet" ? "pierce" : "blunt";
                Assert.That(arena.EnemyRenderer.sprite.name,
                    Does.Match("^enemy-" + type + "(?:-[23])?-frame-" + (frame == 0 ? "01" : "05") + "$"));
            }
        }

        private static DuelPrototypeController FindPrototype()
        {
            var controller = Object.FindAnyObjectByType<DuelPrototypeController>();
            Assert.That(controller, Is.Not.Null);
            return controller;
        }
    }
}
