using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Presentation;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class CombatFlowMovementPlayModeTests
    {
        [UnityTest]
        public IEnumerator ShorterTransitions_SnapshotEachStart_AndStillWaitForRangeAndPendingPush()
        {
            using (var fixture = new ArenaFixture())
            {
                fixture.SetSettles(.18f, .18f);
                fixture.PrepareCombat();
                fixture.Arena.BeginApproach();
                fixture.Arena.Tick(.03f, 0f);
                fixture.SetSettles(.04f, .04f);
                fixture.Arena.Tick(.02f, 0f);
                Assert.That(fixture.Arena.IsInRange, Is.True);
                Assert.That(fixture.Arena.ApproachComplete, Is.False, "Changing tuning cannot cut an approach already in progress.");
                fixture.Arena.Tick(.131f, 0f);
                Assert.That(fixture.Arena.ApproachComplete, Is.True);

                fixture.Arena.BeginApproach();
                fixture.Arena.Tick(.039f, 0f);
                Assert.That(fixture.Arena.ApproachComplete, Is.False);
                fixture.Arena.Tick(.002f, 0f);
                Assert.That(fixture.Arena.ApproachComplete, Is.True, "The next approach uses the shorter settle.");

                fixture.SetSettles(.18f, .18f);
                fixture.Arena.EndTurn();
                fixture.Arena.Tick(.03f, 0f);
                fixture.SetSettles(.04f, .04f);
                fixture.Arena.Tick(.02f, 0f);
                Assert.That(fixture.Arena.ReturnComplete, Is.False, "The active return keeps its original settle too.");
                fixture.Arena.Tick(.131f, 0f);
                Assert.That(fixture.Arena.ReturnComplete, Is.True);
                fixture.Arena.EndTurn();
                fixture.Arena.Tick(.039f, 0f);
                Assert.That(fixture.Arena.ReturnComplete, Is.False);
                fixture.Arena.Tick(.002f, 0f);
                Assert.That(fixture.Arena.ReturnComplete, Is.True);

                fixture.Arena.Reset();
                fixture.Arena.BeginApproach();
                fixture.Arena.Tick(.041f, 0f);
                Assert.That(fixture.Arena.IsInRange, Is.False);
                Assert.That(fixture.Arena.ApproachComplete, Is.False, "A shorter minimum cannot skip the actual travel to contact.");
                fixture.Arena.Tick(.1f, 0f);
                Assert.That(fixture.Arena.IsInRange, Is.True);
                Assert.That(fixture.Arena.ApproachComplete, Is.True);

                fixture.PrepareCombat();
                fixture.Arena.PresentHit(true, 10, 0, false, false, 10);
                fixture.Arena.BeginApproach();
                fixture.Arena.Tick(.041f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.True);
                Assert.That(fixture.Arena.ApproachComplete, Is.False, "Contact cannot begin while the previous push is unfinished.");
                fixture.Arena.Tick(.06f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                // Finishing the push releases pursuit; accelerating back into reach can take longer than the push.
                for (int frame = 0; frame < 100 && !fixture.Arena.ApproachComplete; frame++) fixture.Arena.Tick(.01f, 0f);
                Assert.That(fixture.Arena.ApproachComplete, Is.True);
                Assert.That(fixture.Arena.IsInRange, Is.True, "A completed approach has both blades within reach.");

                fixture.PrepareCombat();
                fixture.Arena.PresentHit(true, 10, 0, false, false, 10);
                fixture.Arena.EndTurn();
                fixture.Arena.Tick(.041f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.True);
                Assert.That(fixture.Arena.ReturnComplete, Is.False, "Turn cleanup still displays the rest of a pending push.");
                fixture.Arena.Tick(.06f, 0f);
                Assert.That(fixture.Arena.HasPendingPush, Is.False);
                Assert.That(fixture.Arena.ReturnComplete, Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompactBladeRecoil_LeavesBodyHitsAndExistingPushesIntact_AndSharesTheBindRelease()
        {
            using (var original = new ArenaFixture())
            using (var compact = new ArenaFixture())
            {
                original.SetRecoil(1f);
                compact.SetRecoil(.35f);
                float oldBlade = original.MeasureEnemyPush(0, 10, false, false, LegacyArenaView.HitExchange.MutualClash);
                float newBlade = compact.MeasureEnemyPush(0, 10, false, false, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(newBlade, Is.EqualTo(oldBlade * .35f).Within(.001f));
                Assert.That(compact.MeasureEnemyPush(0, 10, true, false),
                    Is.EqualTo(original.MeasureEnemyPush(0, 10, true, false) * .35f).Within(.001f),
                    "A guard that spends resistance also stays close.");
                Assert.That(compact.MeasureEnemyPush(0, 0, true, false),
                    Is.EqualTo(original.MeasureEnemyPush(0, 0, true, false) * .35f).Within(.001f),
                    "A fully absorbed guard keeps only its small contact recoil.");
                Assert.That(compact.MeasureEnemyPush(10, 0, false, false),
                    Is.EqualTo(original.MeasureEnemyPush(10, 0, false, false)).Within(.001f), "HP hits keep their weight.");
                Assert.That(compact.MeasureEnemyPush(10, 0, true, false),
                    Is.EqualTo(original.MeasureEnemyPush(10, 0, true, false)).Within(.001f), "HP overflow through a guard is unchanged.");
                Assert.That(compact.MeasureEnemyPush(0, 10, false, true, LegacyArenaView.HitExchange.MutualClash),
                    Is.EqualTo(original.MeasureEnemyPush(0, 10, false, true, LegacyArenaView.HitExchange.MutualClash)).Within(.001f),
                    "A broken or heavy contact keeps its full recoil.");
                Assert.That(compact.MeasureEnemyPush(10, 0, false, true),
                    Is.EqualTo(original.MeasureEnemyPush(10, 0, false, true)).Within(.001f), "A finishing body blow is unchanged.");

                compact.PrepareCombat();
                float enemyAt = compact.EnemyX;
                compact.Arena.PresentHit(true, 5, 0, false, false, 5);
                float existing = compact.Arena.EnemyKnockbackTarget.x - enemyAt;
                compact.Arena.PresentHit(true, 0, 10, false, false, 10, LegacyArenaView.HitExchange.MutualClash);
                Assert.That(compact.Arena.EnemyKnockbackTarget.x - enemyAt,
                    Is.EqualTo(existing + newBlade).Within(.001f), "Scale only the new contact, not the push already queued.");

                original.PrepareCombat();
                compact.PrepareCombat();
                foreach (ArenaFixture fixture in new[] { original, compact })
                {
                    fixture.Arena.PresentHit(true, 0, 10, false, false, 10, LegacyArenaView.HitExchange.MutualClash);
                    fixture.Arena.PresentHit(false, 0, 5, false, false, 5, LegacyArenaView.HitExchange.MutualClash);
                    fixture.Arena.BeginBladeBind(.1f, 0f, 0f, .2f, 0);
                }
                float originalPlayerAt = original.PlayerX, originalEnemyAt = original.EnemyX;
                float compactPlayerAt = compact.PlayerX, compactEnemyAt = compact.EnemyX;
                compact.SetRecoil(1f);
                original.Arena.FinishBladeBind();
                compact.Arena.FinishBladeBind();
                float oldRelease = original.Arena.EnemyKnockbackTarget.x - originalEnemyAt;
                float newRelease = compact.Arena.EnemyKnockbackTarget.x - compactEnemyAt;
                Assert.That(oldRelease, Is.GreaterThan(0f));
                Assert.That(originalPlayerAt - original.Arena.PlayerKnockbackTarget.x, Is.EqualTo(oldRelease).Within(.001f));
                Assert.That(compactPlayerAt - compact.Arena.PlayerKnockbackTarget.x, Is.EqualTo(newRelease).Within(.001f));
                Assert.That(newRelease, Is.EqualTo(oldRelease * .35f).Within(.001f),
                    "Share the reduced pending pushes and keep the extra spring snapshotted at the bind's start.");
                Assert.That(compact.PlayerX, Is.EqualTo(compactPlayerAt));
                Assert.That(compact.EnemyX, Is.EqualTo(compactEnemyAt), "Release queues movement instead of teleporting either fighter.");
            }
            yield return null;
        }

        private sealed class ArenaFixture : IDisposable
        {
            private readonly GameObject host;
            private readonly LegacyDuelArt art;
            private readonly DuelPresentationSettings settings;
            public LegacyArenaView Arena { get; }
            public float PlayerX => Arena.PlayerRenderer.transform.localPosition.x;
            public float EnemyX => Arena.EnemyRenderer.transform.localPosition.x;

            public ArenaFixture()
            {
                host = new GameObject("Combat Flow Movement Test");
                art = new LegacyDuelArt();
                settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                JsonUtility.FromJsonOverwrite("{\"movementDistanceMultiplier\":1.4,\"movementSpeedMultiplier\":1.2," +
                    "\"approachSettleSeconds\":0.04,\"returnSettleSeconds\":0.04,\"bladeRecoilScale\":0.35," +
                    "\"attackRhythmEnabled\":false,\"weaponTrailEnabled\":false}", settings);
                Arena = LegacyArenaView.Create(host.transform, art, settings);
                Arena.ArenaCamera.enabled = false;
            }

            public void SetSettles(float approach, float returning)
            {
                Set("approachSettleSeconds", approach);
                Set("returnSettleSeconds", returning);
            }
            public void SetRecoil(float scale) => Set("bladeRecoilScale", scale);
            private void Set(string name, float value)
            {
                FieldInfo field = typeof(DuelPresentationSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, "Missing combat-flow tuning: " + name);
                field.SetValue(settings, value);
            }
            public void PrepareCombat()
            {
                Arena.Reset();
                Arena.CloseDistance(1f);
                Arena.BeginSlot(LegacySkillDefinitions.Skill(1), LegacySkillDefinitions.Skill(1));
            }
            public float MeasureEnemyPush(int hp, int resistance, bool guarded, bool fatal,
                LegacyArenaView.HitExchange exchange = LegacyArenaView.HitExchange.None)
            {
                PrepareCombat();
                float start = EnemyX;
                Arena.PresentHit(true, hp, resistance, guarded, fatal, 10, exchange);
                return Arena.EnemyKnockbackTarget.x - start;
            }
            public void Dispose()
            {
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
        }
    }
}
