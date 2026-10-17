using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class EnemyRosterArenaPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly EnemyAppearance[] Crew = { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student };

        [UnityTest]
        public IEnumerator ThreeBodies_KeepIndependentArtworkAnchorsFloorGuidesAndDepthOrder_AcrossReset()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.ConfigureEnemyRoster(Crew);
                Assert.That(arena.EnemyCount, Is.EqualTo(3));
                Assert.That(arena.EnemyActorTransforms.Count, Is.EqualTo(3));
                Assert.That(arena.GetEnemyRenderer(0).sprite, Is.Not.SameAs(arena.GetEnemyRenderer(1).sprite));
                Assert.That(arena.GetEnemyRenderer(1).sprite, Is.Not.SameAs(arena.GetEnemyRenderer(2).sprite));
                var bodies = new Transform[3];
                var circles = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    bodies[i] = arena.GetEnemyRenderer(i).transform;
                    circles[i] = arena.GetEnemyGroundCircle(i);
                    Assert.That(arena.EnemyActorTransforms[i], Is.SameAs(bodies[i]));
                    Assert.That(bodies[i].gameObject.activeInHierarchy, Is.True);
                }
                arena.SetDepthPresentationVisible(true);
                var orders = new HashSet<int> { arena.PlayerDepthSortingOrder };
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(Vector3.Distance(circles[i].position, arena.GetEnemyGroundPosition(i)), Is.LessThan(.0001f));
                    Assert.That(circles[i].GetComponent<MeshRenderer>().enabled, Is.True);
                    Assert.That(orders.Add(arena.GetEnemyDepthSortingOrder(i)), Is.True, "Whole figures receive unique depth ranks.");
                    Assert.That(bodies[i].GetComponent<SortingGroup>().sortingOrder, Is.InRange(-3, 0));
                }
                Assert.That(arena.GetEnemyDepthSortingOrder(2), Is.GreaterThan(arena.GetEnemyDepthSortingOrder(1)),
                    "The physically lower feet draw in front of the higher feet.");
                arena.SelectActiveEnemy(2);
                arena.Reset();
                arena.ConfigureEnemyRoster(Crew);
                Assert.That(arena.ActiveEnemyIndex, Is.Zero);
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(arena.GetEnemyRenderer(i).transform, Is.SameAs(bodies[i]));
                    Assert.That(arena.GetEnemyGroundCircle(i), Is.SameAs(circles[i]));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BatonHandoff_PreservesBothBodiesThenApproachesFromTheirActualPositions_AndOutgoingEnemyStops()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.ConfigureEnemyRoster(Crew);
                Approach(arena);
                arena.BeginSlot(Attack(), Attack());
                Vector3 playerAtHandoff = arena.PlayerRenderer.transform.localPosition;
                Vector3 outgoing = arena.GetEnemyRenderer(0).transform.localPosition;
                Vector3 incoming = arena.GetEnemyRenderer(1).transform.localPosition;
                arena.SelectActiveEnemy(1);
                Assert.That(arena.EnemyRenderer, Is.SameAs(arena.GetEnemyRenderer(1)));
                Assert.That(arena.GetEnemyRenderer(0).transform.localPosition, Is.EqualTo(outgoing));
                Assert.That(arena.EnemyRenderer.transform.localPosition, Is.EqualTo(incoming), "Selecting an owner never teleports its body to the contact point.");
                arena.BeginApproach();
                arena.Tick(.01f, .01f);
                Assert.That(Vector3.Distance(arena.EnemyRenderer.transform.localPosition, incoming), Is.InRange(.0001f, .5f));
                Assert.That(arena.ApproachComplete, Is.False);
                for (int i = 0; i < 600 && !arena.ApproachComplete; i++) arena.Tick(.01f, .01f);
                Assert.That(arena.ApproachComplete, Is.True);
                Assert.That(arena.IsInRange, Is.True);
                Assert.That(Vector3.Distance(arena.PlayerRenderer.transform.localPosition, playerAtHandoff), Is.LessThan(.181f),
                    "Elisa plants her feet for the relay; the incoming opponent covers the actual approach distance.");
                for (int i = 0; i < 100; i++) arena.Tick(.01f, .01f);
                Vector3 held = arena.GetEnemyRenderer(0).transform.localPosition;
                Assert.That(Vector3.Distance(held, outgoing), Is.GreaterThan(.3f), "The outgoing fighter clears the incoming attack line with a finite step.");
                Assert.That(Mathf.Abs(held.y - outgoing.y), Is.GreaterThan(.2f),
                    "A safety step along X must preserve the outgoing fighter's sideways clearance of the approach line.");
                for (int i = 0; i < 100; i++) arena.Tick(.01f, .01f);
                Assert.That(Vector3.Distance(arena.GetEnemyRenderer(0).transform.localPosition, held), Is.LessThan(.001f),
                    "A waiting opponent holds its reached position instead of wandering between lanes.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PassingAWaitingEnemy_RecoversTheSignedGapThroughFootwork_WithoutTeleportOrDeadlock()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.ConfigureEnemyRoster(Crew);
                arena.PlayerRenderer.transform.localPosition = new Vector3(12f, arena.ActorGroundY, 0f);
                arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(7f, arena.ActorGroundY + .5f, 0f);
                Vector3 player = arena.PlayerRenderer.transform.localPosition, incoming = arena.GetEnemyRenderer(1).transform.localPosition;
                arena.SelectActiveEnemy(1);
                Assert.That(arena.Separation, Is.LessThan(0f));
                Assert.That(arena.PlayerRenderer.transform.localPosition, Is.EqualTo(player));
                Assert.That(arena.EnemyRenderer.transform.localPosition, Is.EqualTo(incoming));
                arena.BeginApproach();
                arena.Tick(.01f, .01f);
                Assert.That(Vector3.Distance(arena.PlayerRenderer.transform.localPosition, player), Is.InRange(.0001f, .5f));
                Assert.That(Vector3.Distance(arena.EnemyRenderer.transform.localPosition, incoming), Is.InRange(.0001f, .5f));
                for (int i = 0; i < 1000 && !arena.ApproachComplete; i++) arena.Tick(.01f, .01f);
                Assert.That(arena.ApproachComplete, Is.True, "A signed separation below zero must not wait forever for a positive closing error.");
                Assert.That(arena.IsInRange, Is.True);
                Assert.That(arena.Separation, Is.GreaterThanOrEqualTo(LegacyArenaView.ContactDistance - .011f));
                for (int i = 0; i < 200; i++) arena.Tick(.01f, .01f);
                for (int i = 0; i < 3; i++)
                    Assert.That(arena.GetEnemyRenderer(i).transform.localPosition.x, Is.GreaterThan(arena.PlayerRenderer.transform.localPosition.x),
                        "Waiting enemies use finite safety steps to remain on the opponent's side.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrokenAndDefeatedCues_AffectOnlyTheirBody_UseCombatTime_AndResetRevivesTheSameBody()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.ConfigureEnemyRoster(Crew);
                arena.SetDepthPresentationVisible(true);
                arena.SetEnemyResistanceBroken(0, true);
                arena.SetEnemyResistanceBroken(1, false);
                arena.SelectActiveEnemy(0);
                Assert.That(arena.EnemyBreakAura.IsBroken, Is.True);
                arena.SelectActiveEnemy(1);
                Assert.That(arena.EnemyBreakAura.IsBroken, Is.False);
                arena.SelectActiveEnemy(0);
                Assert.That(arena.EnemyBreakAura.IsBroken, Is.True);
                SpriteRenderer body = arena.GetEnemyRenderer(0);
                Vector3 atDefeat = body.transform.localPosition;
                arena.SetEnemyDefeated(0, true);
                arena.Tick(0f, .5f);
                Assert.That(body.color.a, Is.EqualTo(1f).Within(.0001f), "Hit stop holds the death cue on the combat clock.");
                Assert.That(body.transform.localPosition, Is.EqualTo(atDefeat));
                Assert.That(arena.GetEnemyGroundCircle(0).GetComponent<MeshRenderer>().enabled, Is.False);
                arena.SelectActiveEnemy(1);
                for (int i = 0; i < 40; i++) arena.Tick(.02f, .02f);
                Assert.That(body.gameObject.activeSelf, Is.False);
                Assert.That(arena.GetEnemyRenderer(1).gameObject.activeSelf, Is.True);
                Assert.That(arena.GetEnemyRenderer(2).gameObject.activeSelf, Is.True);
                arena.Reset();
                Assert.That(arena.GetEnemyRenderer(0), Is.SameAs(body));
                Assert.That(body.gameObject.activeSelf, Is.True);
                Assert.That(body.color.a, Is.EqualTo(1f));
                Assert.That(body.transform.localPosition.x, Is.EqualTo(5f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CutsceneBorrowingHidesOnlyExtraBodies_ThenRestoresThem_AndSingleDuelDisposesTheExtras()
        {
            yield return null;
            SpriteRenderer removedOne, removedTwo;
            Transform removedCircle;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                arena.ConfigureEnemyRoster(Crew);
                arena.SetDepthPresentationVisible(true);
                Vector3 second = arena.GetEnemyRenderer(1).transform.localPosition;
                typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(arena, null);
                Assert.That(arena.GetEnemyRenderer(1).gameObject.activeSelf, Is.False);
                Assert.That(arena.GetEnemyRenderer(2).gameObject.activeSelf, Is.False);
                typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(arena, null);
                Assert.That(arena.GetEnemyRenderer(1).gameObject.activeSelf, Is.True);
                Assert.That(arena.GetEnemyRenderer(1).transform.localPosition, Is.EqualTo(second));
                removedOne = arena.GetEnemyRenderer(1); removedTwo = arena.GetEnemyRenderer(2);
                removedCircle = arena.GetEnemyGroundCircle(1);
                arena.ConfigureEnemyRoster(null);
                Assert.That(arena.EnemyCount, Is.EqualTo(1));
                Assert.That(arena.ActiveEnemyIndex, Is.Zero);
                Assert.That(arena.EnemyRenderer, Is.SameAs(arena.GetEnemyRenderer(0)));
                Assert.That(arena.EnemyActorTransforms.Count, Is.EqualTo(1));
                yield return null;
                Assert.That(removedOne == null && removedTwo == null && removedCircle == null, Is.True,
                    "Returning to a duel releases the extra figure and floor-guide resources.");
                arena.SetEnemyAppearance(EnemyAppearance.Elisa);
                arena.Reset();
                Approach(arena);
                Assert.That(arena.IsInRange, Is.True, "The original two-body arena still resolves after a roster encounter.");
            }
            yield return null;
        }

        private static void Approach(LegacyArenaView arena)
        {
            arena.BeginApproach();
            for (int i = 0; i < 1000 && !arena.ApproachComplete; i++) arena.Tick(.01f, .01f);
            Assert.That(arena.ApproachComplete, Is.True);
            Assert.That(arena.IsInRange, Is.True);
        }

        private static LegacySkill Attack() => new LegacySkill(88321, "교대 검증", 0, 8, 8, LegacySkillKind.Attack,
            LegacySkillProperty.Slash, 1, 0, string.Empty, iconId: 1);

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Enemy Roster Arena Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public LegacyArenaView Arena { get; }
            public ArenaScope()
            {
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                Arena.Reset();
                Arena.ArenaCamera.aspect = 16f / 9f;
            }
            public void Dispose()
            {
                Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
            }
        }
    }
}
