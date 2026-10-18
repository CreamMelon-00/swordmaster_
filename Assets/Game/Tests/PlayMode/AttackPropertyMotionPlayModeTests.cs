using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class AttackPropertyMotionPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator IncomingAttack_OnlyBluntPushes_OnEitherSide()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (bool playerAttacks in new[] { true, false })
            foreach (LegacySkillProperty property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
            {
                scope.Begin(playerAttacks ? Attack(property) : Attack(LegacySkillProperty.Slash),
                    playerAttacks ? Attack(LegacySkillProperty.Slash) : Attack(property));
                Transform target = scope.Target(playerAttacks);
                Vector3 before = target.localPosition;
                scope.Arena.PresentHit(playerAttacks, 10, 0, false, false, 10);
                Vector3 endpoint = scope.Endpoint(playerAttacks);
                if (property == LegacySkillProperty.Hit)
                {
                    Assert.That((endpoint.x - before.x) * (playerAttacks ? 1f : -1f), Is.GreaterThan(.1f));
                    Assert.That(scope.Arena.HasPendingPush, Is.True);
                }
                else
                {
                    Assert.That(endpoint, Is.EqualTo(before), property + " keeps the struck body at its ground position.");
                    Assert.That(scope.Arena.HasPendingPush, Is.False);
                }
                scope.Arena.Tick(LegacyArenaView.PushDuration + .01f, 0f);
                Assert.That(target.localPosition.x, Is.EqualTo(endpoint.x).Within(.001f));
            }
        }

        [UnityTest]
        public IEnumerator MissingAttackAndDefence_DoNotInventAHitPush()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (LegacySkill attacker in new[] { null, Guard() })
            foreach (bool playerAttacks in new[] { true, false })
            {
                scope.Begin(playerAttacks ? attacker : null, playerAttacks ? null : attacker);
                Vector3 before = scope.Target(playerAttacks).localPosition;
                scope.Arena.PresentHit(playerAttacks, 10, 0, false, false, 10);
                Assert.That(scope.Endpoint(playerAttacks), Is.EqualTo(before));
                Assert.That(scope.Arena.HasPendingPush, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Guard_RemainsPlanted_WithOnlyASmallBluntRecoil()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (bool playerAttacks in new[] { true, false })
            {
                scope.Begin(Attack(LegacySkillProperty.Hit), Attack(LegacySkillProperty.Hit));
                float before = scope.Target(playerAttacks).localPosition.x;
                scope.Arena.PresentHit(playerAttacks, 10, 0, false, false, 10);
                float full = Mathf.Abs(scope.Endpoint(playerAttacks).x - before);
                foreach (LegacySkillProperty property in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
                foreach (bool overflow in new[] { false, true })
                {
                    scope.Begin(playerAttacks ? Attack(property) : Guard(), playerAttacks ? Guard() : Attack(property));
                    before = scope.Target(playerAttacks).localPosition.x;
                    scope.Arena.PresentHit(playerAttacks, overflow ? 10 : 0, 0, true, false, 10);
                    float recoil = Mathf.Abs(scope.Endpoint(playerAttacks).x - before);
                    if (property == LegacySkillProperty.Hit)
                        Assert.That(recoil, Is.GreaterThan(0f).And.LessThanOrEqualTo(full * .15f));
                    else
                        Assert.That(recoil, Is.Zero, property + " cannot launch a guarding opponent.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Penetrate_CommitsFartherAtContact_WithoutChangingLogicalPositionsOrGuardPose()
        {
            yield return null;
            using (var scope = new MotionScope())
            {
                scope.Begin(Attack(LegacySkillProperty.Slash), Attack(LegacySkillProperty.Slash));
                scope.HoldContact();
                float playerSlash = scope.Arena.PlayerAttackVisualOffset.x;
                float enemySlash = -scope.Arena.EnemyAttackVisualOffset.x;
                Assert.That(playerSlash, Is.GreaterThan(.1f));
                Assert.That(enemySlash, Is.GreaterThan(.1f));
                scope.Begin(Attack(LegacySkillProperty.Penetrate), Attack(LegacySkillProperty.Penetrate));
                Vector3 playerAt = scope.Arena.PlayerRenderer.transform.localPosition;
                Vector3 enemyAt = scope.Arena.EnemyRenderer.transform.localPosition;
                scope.HoldContact();
                Assert.That(scope.Arena.PlayerAttackVisualOffset.x, Is.GreaterThan(playerSlash * 1.2f));
                Assert.That(-scope.Arena.EnemyAttackVisualOffset.x, Is.GreaterThan(enemySlash * 1.2f));
                Assert.That(scope.Arena.PlayerRenderer.transform.localPosition, Is.EqualTo(playerAt));
                Assert.That(scope.Arena.EnemyRenderer.transform.localPosition, Is.EqualTo(enemyAt));
                scope.Begin(Guard(), Guard());
                scope.HoldContact();
                Assert.That(scope.Arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                Assert.That(scope.Arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));
            }
        }

        [UnityTest]
        public IEnumerator DefensiveSlots_ResolveOutOfRange_AndTheNextAttackerAloneClosesTheGap()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (bool playerGuards in new[] { true, false })
            {
                scope.Begin(Guard(), Guard());
                scope.PlaceApart();
                Vector3 playerAt = scope.Arena.PlayerRenderer.transform.localPosition;
                Vector3 enemyAt = scope.Arena.EnemyRenderer.transform.localPosition;
                scope.Arena.PrepareSlotMovement(Guard(), Guard());
                Assert.That(scope.Arena.IsInRange, Is.False);
                Assert.That(scope.Arena.IsReadyForSlot, Is.True, "Two guards can resolve without travelling into attack range.");
                scope.Arena.CloseDistance(.1f);
                Assert.That(scope.Arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(playerAt.x));
                Assert.That(scope.Arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(enemyAt.x));

                scope.Arena.PrepareSlotMovement(null, null);
                Assert.That(scope.Arena.IsReadyForSlot, Is.True, "An empty or breath slot does not need contact either.");
                LegacySkill playerSkill = playerGuards ? Guard() : Attack(LegacySkillProperty.Slash);
                LegacySkill enemySkill = playerGuards ? Attack(LegacySkillProperty.Slash) : Guard();
                scope.Arena.PrepareSlotMovement(playerSkill, enemySkill);
                Assert.That(scope.Arena.IsReadyForSlot, Is.False);
                for (int frame = 0; frame < 100 && !scope.Arena.IsReadyForSlot; frame++) scope.Arena.CloseDistance(.01f);
                Assert.That(scope.Arena.IsReadyForSlot, Is.True, "A new attack replaces the previous defensive movement intent.");
                Transform guard = playerGuards ? scope.Arena.PlayerRenderer.transform : scope.Arena.EnemyRenderer.transform;
                Transform attacker = playerGuards ? scope.Arena.EnemyRenderer.transform : scope.Arena.PlayerRenderer.transform;
                Assert.That(guard.localPosition.x, Is.EqualTo(playerGuards ? playerAt.x : enemyAt.x).Within(.001f));
                Assert.That(Mathf.Abs(attacker.localPosition.x - (playerGuards ? enemyAt.x : playerAt.x)), Is.GreaterThan(1f));
            }
        }

        [UnityTest]
        public IEnumerator GuardingEnemyHandoff_LetsThePlayerApproach_WhileBothEnemiesKeepTheirPositions()
        {
            yield return null;
            using (var scope = new MotionScope())
            {
                scope.Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB });
                scope.Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, scope.Arena.ActorGroundY, 0f);
                scope.Arena.GetEnemyRenderer(0).transform.localPosition = new Vector3(2f, scope.Arena.ActorGroundY, 0f);
                scope.Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(8f, scope.Arena.ActorGroundY + .3f, 0f);
                scope.Arena.BeginSlot(Attack(LegacySkillProperty.Slash), Attack(LegacySkillProperty.Slash));
                Vector3 outgoingAt = scope.Arena.GetEnemyGroundPosition(0);
                Vector3 guardAt = scope.Arena.GetEnemyGroundPosition(1);
                float playerAt = scope.Arena.PlayerGroundPosition.x;
                scope.Arena.SelectActiveEnemy(1);
                scope.Arena.PrepareSlotMovement(Attack(LegacySkillProperty.Slash), Guard());
                Assert.That(scope.Arena.IsReadyForSlot, Is.False);
                for (int frame = 0; frame < 200 && !scope.Arena.IsReadyForSlot; frame++) scope.Arena.CloseDistance(.01f);
                Assert.That(scope.Arena.IsReadyForSlot, Is.True, "A guarding handoff cannot leave both sides waiting outside attack range.");
                Assert.That(scope.Arena.PlayerGroundPosition.x, Is.GreaterThan(playerAt + 1f));
                Assert.That(scope.Arena.GetEnemyGroundPosition(1), Is.EqualTo(guardAt), "The incoming guard stays planted.");
                Assert.That(scope.Arena.GetEnemyGroundPosition(0), Is.EqualTo(outgoingAt));
            }
        }

        [UnityTest]
        public IEnumerator MixedBladeBind_PreservesEachTargetsOwnBluntPush_InsteadOfSharingItAcrossBothBodies()
        {
            yield return null;
            using (var scope = new MotionScope())
            foreach (LegacySkillProperty playerProperty in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
            foreach (LegacySkillProperty enemyProperty in new[] { LegacySkillProperty.Slash, LegacySkillProperty.Penetrate, LegacySkillProperty.Hit })
            {
                scope.Begin(Attack(playerProperty), Attack(enemyProperty));
                float playerAt = scope.Arena.PlayerRenderer.transform.localPosition.x;
                float enemyAt = scope.Arena.EnemyRenderer.transform.localPosition.x;
                scope.Arena.PresentHit(true, 0, 10, false, false, 10, LegacyArenaView.HitExchange.MutualClash);
                scope.Arena.PresentHit(false, 0, 5, false, false, 5, LegacyArenaView.HitExchange.MutualClash);
                float playerPending = playerAt - scope.Arena.PlayerKnockbackTarget.x;
                float enemyPending = scope.Arena.EnemyKnockbackTarget.x - enemyAt;
                scope.Arena.BeginBladeBind(.1f, 0f, 0f, .2f, 0);
                scope.Arena.FinishBladeBind();
                float playerRelease = playerAt - scope.Arena.PlayerKnockbackTarget.x;
                float enemyRelease = scope.Arena.EnemyKnockbackTarget.x - enemyAt;
                Assert.That(playerRelease, Is.EqualTo(playerPending + (enemyProperty == LegacySkillProperty.Hit ? .07f : 0f)).Within(.001f));
                Assert.That(enemyRelease, Is.EqualTo(enemyPending + (playerProperty == LegacySkillProperty.Hit ? .07f : 0f)).Within(.001f));
                Assert.That(scope.Arena.PlayerRenderer.transform.localPosition.x, Is.EqualTo(playerAt));
                Assert.That(scope.Arena.EnemyRenderer.transform.localPosition.x, Is.EqualTo(enemyAt), "Release queues the push without teleporting either fighter.");
            }
        }

        private static LegacySkill Attack(LegacySkillProperty property)
            => new LegacySkill(88701, "속성 이동 검증", 0, 10, 10, LegacySkillKind.Attack, property, 1, 0, string.Empty);
        private static LegacySkill Guard()
            => new LegacySkill(88702, "방어 이동 검증", 0, 10, 10, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, string.Empty);

        private sealed class MotionScope : IDisposable
        {
            private readonly GameObject root;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            public LegacyArenaView Arena { get; }

            public MotionScope()
            {
                root = new GameObject("Attack Property Motion Test");
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{\"movementDistanceMultiplier\":1,\"movementSpeedMultiplier\":1," +
                    "\"bladeRecoilScale\":0.35,\"attackRhythmEnabled\":true,\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.ArenaCamera.enabled = false;
            }

            public void Begin(LegacySkill player, LegacySkill enemy)
            {
                Arena.Reset();
                Arena.CloseDistance(1f);
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(Arena, new object[] { .6f, .1f });
                Arena.BeginSlot(player, enemy);
            }

            public void PlaceApart()
            {
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-5f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(5f, Arena.ActorGroundY, 0f);
            }
            public Transform Target(bool playerAttacks) => playerAttacks ? Arena.EnemyRenderer.transform : Arena.PlayerRenderer.transform;
            public Vector3 Endpoint(bool playerAttacks) => playerAttacks ? Arena.EnemyKnockbackTarget : Arena.PlayerKnockbackTarget;
            public void HoldContact() => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private)
                .Invoke(Arena, new object[] { LegacyArenaView.OriginalClipDuration / .6f * .5f });
            public void Dispose()
            {
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(root);
            }
        }
    }
}
