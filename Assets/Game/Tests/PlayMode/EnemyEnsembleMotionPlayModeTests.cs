using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class EnemyEnsembleMotionPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly EnemyAppearance[] Crew = { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student };
        private const string PlanningTuning = "{\"battleDriftSpeed\":2,\"battleStagingSeparation\":7,\"battleDriftMinimumSeparation\":5}";

        [UnityTest]
        public IEnumerator Planning_AllThreeApproachAtTheirOwnSpeed_AndTuningChangesOnlyTheChosenFighter()
        {
            yield return null;
            using (var normal = new ArenaScope())
            using (var tuned = new ArenaScope())
            {
                normal.PlacePlanning(); tuned.PlacePlanning();
                JsonUtility.FromJsonOverwrite("{\"enemyPlanningSpeedFactors\":{\"x\":1.6,\"y\":1,\"z\":1.2}}", tuned.Settings);
                Vector3 player = normal.Arena.PlayerGroundPosition;
                Vector3[] start = EnemyPositions(normal.Arena);
                PlanningFor(normal.Arena, .8f); PlanningFor(tuned.Arena, .8f);
                float[] travel = new float[3];
                for (int i = 0; i < 3; i++)
                {
                    Vector3 reached = normal.Arena.GetEnemyGroundPosition(i);
                    travel[i] = start[i].x - reached.x;
                    Assert.That(travel[i], Is.GreaterThan(.1f), "Every opponent must approach during the same planning frames.");
                    Assert.That(reached.y, Is.EqualTo(start[i].y).Within(.0001f), "Planning does not invent a depth destination.");
                    Assert.That(reached.x - normal.Arena.PlayerGroundPosition.x, Is.GreaterThanOrEqualTo(LegacyArenaView.ContactDistance));
                }
                Assert.That(travel[0], Is.LessThan(travel[1]));
                Assert.That(travel[1], Is.LessThan(travel[2]));
                Assert.That(normal.Arena.PlayerGroundPosition.x, Is.GreaterThan(player.x), "Elisa retains the ordinary planning approach.");
                Assert.That(normal.Arena.PlayerGroundPosition.y, Is.EqualTo(player.y).Within(.0001f));
                Assert.That(start[0].x - tuned.Arena.GetEnemyGroundPosition(0).x, Is.GreaterThan(travel[0] * 1.4f));
                for (int i = 1; i < 3; i++)
                    Assert.That(tuned.Arena.GetEnemyGroundPosition(i).x, Is.EqualTo(normal.Arena.GetEnemyGroundPosition(i).x).Within(.0001f),
                        "Changing one fighter's factor must not retime the other two.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Planning_UsesTheSameSlowerClockForAllThree_HoldsAtZero_AndCycleReleaseAcceleratesAllThree()
        {
            yield return null;
            using (var normal = new ArenaScope())
            using (var tab = new ArenaScope())
            using (var reading = new ArenaScope())
            {
                normal.PlacePlanning(); tab.PlacePlanning(); reading.PlacePlanning();
                Vector3[] start = EnemyPositions(normal.Arena);
                PlanningFor(normal.Arena, .8f); PlanningFor(tab.Arena, .8f, .2f); PlanningFor(reading.Arena, .8f, .3f);
                for (int i = 0; i < 3; i++)
                {
                    float ordinaryTravel = start[i].x - normal.Arena.GetEnemyGroundPosition(i).x;
                    float tabTravel = start[i].x - tab.Arena.GetEnemyGroundPosition(i).x;
                    float readingTravel = start[i].x - reading.Arena.GetEnemyGroundPosition(i).x;
                    Assert.That(tabTravel, Is.GreaterThan(0f).And.LessThan(ordinaryTravel * .5f));
                    Assert.That(readingTravel, Is.GreaterThan(tabTravel).And.LessThan(ordinaryTravel * .65f));
                    Vector3 position = tab.Arena.GetEnemyGroundPosition(i);
                    Vector2 velocity = EnemyVelocity(tab.Arena, i);
                    for (int frame = 0; frame < 5; frame++)
                    {
                        tab.Arena.SetPlanningState(10f, true, true); tab.Arena.Tick(0f, .02f);
                    }
                    Assert.That(tab.Arena.GetEnemyGroundPosition(i), Is.EqualTo(position));
                    Assert.That(EnemyVelocity(tab.Arena, i), Is.EqualTo(velocity), "A stopped planning clock preserves its velocity as well as position.");
                }
                // Compare from the same established slow-approach state. The release uses real time for its window,
                // while both ordinary and released travel keep consuming the supplied planning clock.
                normal.Arena.Reset(); normal.Arena.ConfigureEnemyRoster(Crew); normal.PlacePlanning();
                tab.Arena.Reset(); tab.Arena.ConfigureEnemyRoster(Crew); tab.PlacePlanning();
                PlanningFor(normal.Arena, .25f); PlanningFor(tab.Arena, .25f);
                Vector3[] beforeNormal = EnemyPositions(normal.Arena), beforeRelease = EnemyPositions(tab.Arena);
                tab.Arena.BreakBulletTime();
                Assert.That(tab.Arena.IsBulletTimeReleased, Is.True);
                PlanningFor(normal.Arena, .2f); PlanningFor(tab.Arena, .2f);
                for (int i = 0; i < 3; i++)
                    Assert.That(beforeRelease[i].x - tab.Arena.GetEnemyGroundPosition(i).x,
                        Is.GreaterThan(beforeNormal[i].x - normal.Arena.GetEnemyGroundPosition(i).x),
                        "Cycling must release all three approaches, not just the currently selected enemy.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClosePlanning_SeparatesEveryOpponentSafely_WithoutSnappingOrChangingDepth()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                scope.PlacePlanning(0f, 3f);
                Vector3[] start = EnemyPositions(arena);
                arena.SetPlanningState(10f, false, true); arena.Tick(.02f, .02f);
                for (int i = 0; i < 3; i++)
                    Assert.That(Vector3.Distance(arena.GetEnemyGroundPosition(i), start[i]), Is.LessThan(.6f),
                        "The staging recovery must be a finite step, not a teleport of the waiting bodies.");
                for (int frame = 0; frame < 250; frame++)
                {
                    arena.SetPlanningState(10f, false, true); arena.Tick(.02f, .02f);
                    for (int i = 0; i < 3; i++)
                        Assert.That(arena.GetEnemyGroundPosition(i).y, Is.EqualTo(start[i].y).Within(.0001f));
                }
                for (int i = 0; i < 3; i++)
                    Assert.That(arena.GetEnemyGroundPosition(i).x - arena.PlayerGroundPosition.x,
                        Is.GreaterThanOrEqualTo(5f - .001f), "Every enemy respects the planning floor, including inactive owners.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpcomingEnemy_PreparesOutsideContact_WithoutChangingTheCurrentOwner_ThenPromotesWithoutTeleport()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                LegacySkill current = Attack(), next = Attack();
                scope.PlaceRelay(); arena.BeginSlot(current, current);
                Vector3 incoming = arena.GetEnemyGroundPosition(1), third = arena.GetEnemyGroundPosition(2);
                SpriteRenderer owner = arena.EnemyRenderer;
                arena.SetUpcomingEnemy(1, next);
                for (int frame = 0; frame < 200; frame++)
                {
                    arena.Tick(.01f, .01f);
                    Assert.That(arena.ActiveEnemyIndex, Is.Zero);
                    Assert.That(arena.EnemyRenderer, Is.SameAs(owner), "Preparing another body cannot redirect the current hit target.");
                    Assert.That(Field(EnemyActor(arena, 0), "Skill"), Is.SameAs(current));
                    Assert.That(Field(EnemyActor(arena, 1), "Skill"), Is.Null, "A readied enemy has no active attack to resolve early.");
                    Assert.That(arena.GetEnemyGroundPosition(1).x - arena.PlayerGroundPosition.x,
                        Is.GreaterThan(LegacyArenaView.ContactDistance), "The readied enemy waits outside the current exchange's contact surface.");
                    Assert.That(arena.GetEnemyGroundPosition(1).y, Is.EqualTo(incoming.y).Within(.0001f),
                        "Preparation closes the horizontal gap from the incoming fighter's own floor depth.");
                }
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(1));
                Assert.That(incoming.x - arena.GetEnemyGroundPosition(1).x, Is.GreaterThan(1f));
                Assert.That(arena.GetEnemyGroundPosition(2), Is.EqualTo(third), "The uninvolved third fighter does not gain an idle wandering target.");
                var animations = (EnemyStudentAnimationSet)Field(EnemyActor(arena, 1), "AnimationSet");
                Assert.That(arena.GetEnemyRenderer(1).sprite, Is.SameAs(animations.GetAttack(next.Property, 0f, 0)),
                    "Once planted, the next attacker visibly holds its opening attack pose.");
                Vector3 planted = arena.GetEnemyGroundPosition(1);
                for (int frame = 0; frame < 40; frame++) arena.Tick(.01f, .01f);
                Assert.That(Vector3.Distance(planted, arena.GetEnemyGroundPosition(1)), Is.LessThan(.001f));
                arena.SelectActiveEnemy(1);
                Assert.That(arena.GetEnemyGroundPosition(1), Is.EqualTo(planted));
                Assert.That(arena.ActiveEnemyIndex, Is.EqualTo(1));
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                arena.BeginApproach();
                for (int frame = 0; frame < 500 && !arena.ApproachComplete; frame++) arena.Tick(.01f, .01f);
                Assert.That(arena.ApproachComplete, Is.True); Assert.That(arena.IsInRange, Is.True);
                Assert.That(arena.GetEnemyGroundPosition(1).y, Is.EqualTo(incoming.y).Within(.0001f),
                    "Promoting the next owner does not add a depth-alignment detour.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FasterHandoff_ShortensHorizontalPreparationAndContact_WithoutDepthConvergenceOrEarlySelection()
        {
            yield return null;
            using (var normal = new ArenaScope())
            using (var faster = new ArenaScope())
            {
                // Keep the slower overall combat movement in both profiles; only the relay tuning changes.
                JsonUtility.FromJsonOverwrite("{\"movementSpeedMultiplier\":0.84,\"enemyHandoffSpeedMultiplier\":1}", normal.Settings);
                JsonUtility.FromJsonOverwrite("{\"movementSpeedMultiplier\":0.84,\"enemyHandoffSpeedMultiplier\":1.35}", faster.Settings);
                ArenaScope[] profiles = { normal, faster };
                var preparationSeconds = new float[2];
                var closureSeconds = new float[2];
                var earlyAdvance = new float[2];
                var earlyContactAdvance = new float[2];
                var plantedPositions = new Vector3[2];
                LegacySkill current = Attack(), next = Attack();
                for (int i = 0; i < profiles.Length; i++)
                {
                    ArenaScope scope = profiles[i];
                    LegacyArenaView arena = scope.Arena;
                    scope.PlaceRelay(); arena.BeginSlot(current, current);
                    Vector3 player = arena.PlayerGroundPosition, incoming = arena.GetEnemyGroundPosition(1);
                    SpriteRenderer currentOwner = arena.EnemyRenderer;
                    arena.SetUpcomingEnemy(1, next);
                    for (int frame = 0; frame < 150; frame++)
                    {
                        arena.Tick(.01f, .01f); preparationSeconds[i] += .01f;
                        Assert.That(arena.ActiveEnemyIndex, Is.Zero);
                        Assert.That(arena.EnemyRenderer, Is.SameAs(currentOwner));
                        Assert.That(Field(EnemyActor(arena, 0), "Skill"), Is.SameAs(current));
                        Assert.That(Field(EnemyActor(arena, 1), "Skill"), Is.Null,
                            "Faster footwork must not begin the reserved skill early.");
                        Assert.That(arena.PlayerGroundPosition, Is.EqualTo(player));
                        Assert.That(arena.GetEnemyGroundPosition(1).x - player.x,
                            Is.GreaterThan(LegacyArenaView.ContactDistance));
                        Assert.That(arena.GetEnemyGroundPosition(1).y, Is.EqualTo(incoming.y).Within(.0001f));
                        if (frame == 14) earlyAdvance[i] = incoming.x - arena.GetEnemyGroundPosition(1).x;
                        if ((bool)Field(arena, "upcomingEnemyReady")) break;
                    }
                    Assert.That((bool)Field(arena, "upcomingEnemyReady"), Is.True);
                    plantedPositions[i] = arena.GetEnemyGroundPosition(1);
                    Assert.That(plantedPositions[i].y, Is.EqualTo(incoming.y).Within(.0001f),
                        "Readiness retains the incoming fighter's distinct floor depth.");
                    arena.SelectActiveEnemy(1);
                    Assert.That(arena.GetEnemyGroundPosition(1), Is.EqualTo(plantedPositions[i]),
                        "Promoting the pre-positioned body must not teleport it into contact.");
                    Assert.That(arena.ActiveEnemyIndex, Is.EqualTo(1));
                    arena.BeginApproach();
                    for (int frame = 0; frame < 150 && !arena.ApproachComplete; frame++)
                    {
                        arena.Tick(.01f, .01f); closureSeconds[i] += .01f;
                        Assert.That(arena.PlayerGroundPosition, Is.EqualTo(player),
                            "The incoming opponent covers the relay distance while Elisa holds her floor.");
                        Assert.That(arena.GetEnemyGroundPosition(1).y, Is.EqualTo(incoming.y).Within(.0001f),
                            "The final contact approach never spends time converging on Elisa's floor depth.");
                        // Both profiles are still approaching at this common early sample. With no depth detour,
                        // the faster handoff can already finish before the former .12-second sample.
                        if (frame == 3)
                            earlyContactAdvance[i] = plantedPositions[i].x - arena.GetEnemyGroundPosition(1).x;
                    }
                    Assert.That(arena.ApproachComplete, Is.True);
                    Assert.That(arena.IsInRange, Is.True);
                    Assert.That(arena.ActiveEnemyIndex, Is.EqualTo(1));
                }
                Assert.That(earlyAdvance[1], Is.GreaterThan(earlyAdvance[0] + .15f),
                    "The next attacker must visibly cover more ground during the same preparation interval.");
                Assert.That(Vector3.Distance(plantedPositions[0], plantedPositions[1]), Is.LessThan(.001f),
                    "The speed tuning changes arrival time, not the ready position.");
                Assert.That(earlyContactAdvance[1], Is.GreaterThan(earlyContactAdvance[0] + .02f),
                    "The faster handoff also advances the final horizontal approach sooner.");
                Assert.That(closureSeconds[1], Is.LessThan(closureSeconds[0] * .9f));
                Assert.That(preparationSeconds[1] + closureSeconds[1],
                    Is.LessThan((preparationSeconds[0] + closureSeconds[0]) * .9f),
                    "The complete relay should become meaningfully shorter while the overall combat tempo stays slowed.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpcomingIntent_ClearsOnCancellationDefeatTurnResetAndCutscene_WithoutMovingTheBody()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                LegacyArenaView arena = scope.Arena;
                scope.PlaceRelay(); arena.BeginSlot(Attack(), Attack());
                arena.SetUpcomingEnemy(1, Attack()); arena.Tick(.02f, .02f);
                Vector3 before = arena.GetEnemyGroundPosition(1);
                arena.SetUpcomingEnemy(-1, null);
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                Assert.That(arena.GetEnemyGroundPosition(1), Is.EqualTo(before));
                arena.SetUpcomingEnemy(1, Attack()); arena.SetEnemyDefeated(1, true);
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                Vector3 defeated = arena.GetEnemyGroundPosition(1);
                for (int frame = 0; frame < 40; frame++) arena.Tick(.02f, .02f);
                Assert.That(arena.GetEnemyGroundPosition(1), Is.EqualTo(defeated));
                arena.SetUpcomingEnemy(2, Attack()); arena.EndTurn();
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                arena.Reset(); scope.PlaceRelay(); arena.BeginSlot(Attack(), Attack()); arena.SetUpcomingEnemy(2, Attack());
                typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(arena, null);
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(arena, null);
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                arena.SetUpcomingEnemy(1, Attack()); arena.Reset();
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
                Assert.That(arena.ActiveEnemyIndex, Is.Zero);
                arena.ConfigureEnemyRoster(null);
                Assert.That(arena.EnemyCount, Is.EqualTo(1));
                Assert.That(arena.UpcomingEnemyIndex, Is.EqualTo(-1));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualPlanningAndRelay_CanBeCapturedWithIndependentPositionsAndOwners()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                string output = Environment.GetEnvironmentVariable("SWORDMASTER_ENSEMBLE_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(output)) Directory.CreateDirectory(output);
                var trace = new MotionTrace();
                foreach (string sequence in new[] { "planning", "relay" })
                {
                    scope.Arena.Reset(); scope.Arena.ConfigureEnemyRoster(Crew);
                    float[] samples;
                    if (sequence == "planning")
                    {
                        scope.PlacePlanning(); samples = new[] { 0f, .25f, .5f, .75f, 1f, 1.5f, 2f };
                    }
                    else
                    {
                        scope.PlaceRelay(); scope.Arena.BeginSlot(Attack(), Attack()); scope.Arena.SetUpcomingEnemy(1, Attack());
                        samples = new[] { 0f, .1f, .2f, .35f, .55f, .8f, 1.2f };
                    }
                    float time = 0f;
                    foreach (float sample in samples)
                    {
                        while (time + .00001f < sample)
                        {
                            float delta = Mathf.Min(.01f, sample - time);
                            if (sequence == "planning") scope.Arena.SetPlanningState(10f - time, false, true);
                            scope.Arena.Tick(delta, delta); time += delta;
                        }
                        trace.frames.Add(new MotionFrame
                        {
                            sequence = sequence, time = time, player = scope.Arena.PlayerGroundPosition,
                            enemies = EnemyPositions(scope.Arena), activeEnemy = scope.Arena.ActiveEnemyIndex,
                            upcomingEnemy = scope.Arena.UpcomingEnemyIndex,
                            enemyVelocities = new[] { EnemyVelocity(scope.Arena, 0), EnemyVelocity(scope.Arena, 1), EnemyVelocity(scope.Arena, 2) }
                        });
                        yield return null;
                        if (!string.IsNullOrEmpty(output)) scope.Capture(Path.Combine(output, sequence + "-" + Mathf.RoundToInt(time * 1000f).ToString("0000") + "ms.png"));
                    }
                }
                Assert.That(trace.frames.Count, Is.EqualTo(14));
                Assert.That(trace.frames[6].enemies[0].x, Is.LessThan(trace.frames[0].enemies[0].x));
                Assert.That(trace.frames[6].enemies[1].x, Is.LessThan(trace.frames[0].enemies[1].x));
                Assert.That(trace.frames[6].enemies[2].x, Is.LessThan(trace.frames[0].enemies[2].x));
                Assert.That(trace.frames[13].activeEnemy, Is.Zero);
                Assert.That(trace.frames[13].upcomingEnemy, Is.EqualTo(1));
                if (!string.IsNullOrEmpty(output)) File.WriteAllText(Path.Combine(output, "ensemble-motion-trace.json"), JsonUtility.ToJson(trace, true));
            }
            yield return null;
        }

        private static void PlanningFor(LegacyArenaView arena, float seconds, float scale = 1f)
        {
            int frames = Mathf.CeilToInt(seconds / .02f);
            float delta = seconds / frames;
            for (int frame = 0; frame < frames; frame++)
            {
                arena.SetPlanningState(10f, scale < 1f, true); arena.Tick(delta * scale, delta);
            }
        }

        private static Vector3[] EnemyPositions(LegacyArenaView arena) => new[]
            { arena.GetEnemyGroundPosition(0), arena.GetEnemyGroundPosition(1), arena.GetEnemyGroundPosition(2) };
        private static object Field(object owner, string name) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(owner);
        private static object EnemyActor(LegacyArenaView arena, int index)
        {
            var figures = (IList)typeof(LegacyArenaView).GetField("enemyFigures", Private).GetValue(arena);
            return Field(figures[index], "Actor");
        }
        private static Vector2 EnemyVelocity(LegacyArenaView arena, int index) => (Vector2)Field(EnemyActor(arena, index), "GroundVelocity");
        private static LegacySkill Attack() => new LegacySkill(88341, "교대 준비 검증", 0, 8, 8, LegacySkillKind.Attack,
            LegacySkillProperty.Slash, 9, 0, string.Empty, iconId: 1);
        [Serializable] private sealed class MotionTrace { public List<MotionFrame> frames = new List<MotionFrame>(); }
        [Serializable] private sealed class MotionFrame
        {
            public string sequence;
            public float time;
            public Vector3 player;
            public Vector3[] enemies;
            public Vector2[] enemyVelocities;
            public int activeEnemy, upcomingEnemy;
        }

        private sealed class ArenaScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Enemy Ensemble Motion Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly RenderTexture target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            public DuelPresentationSettings Settings { get; } = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            public LegacyArenaView Arena { get; }
            public ArenaScope()
            {
                JsonUtility.FromJsonOverwrite(PlanningTuning, Settings);
                Arena = LegacyArenaView.Create(root.transform, art, Settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor); Arena.Reset(); Arena.ConfigureEnemyRoster(Crew);
                Arena.ArenaCamera.targetTexture = target; Arena.ArenaCamera.aspect = 960f / 540f;
            }
            public void PlacePlanning(float playerX = -5f, float enemyX = 6f)
            {
                Arena.PlayerRenderer.transform.localPosition = new Vector3(playerX, Arena.ActorGroundY, 0f);
                float[] depths = { 0f, .82f, -.72f };
                for (int i = 0; i < 3; i++)
                    Arena.GetEnemyRenderer(i).transform.localPosition = new Vector3(enemyX, Arena.ActorGroundY + depths[i], 0f);
                Arena.Tick(0f, 0f);
            }
            public void PlaceRelay()
            {
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.GetEnemyRenderer(0).transform.localPosition = new Vector3(2f, Arena.ActorGroundY, 0f);
                Arena.GetEnemyRenderer(1).transform.localPosition = new Vector3(7f, Arena.ActorGroundY + .82f, 0f);
                Arena.GetEnemyRenderer(2).transform.localPosition = new Vector3(10f, Arena.ActorGroundY - .72f, 0f);
                Arena.Tick(0f, 0f);
            }
            public void Capture(string path)
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Arena.ArenaCamera.cullingMask = 1 << 31;
                Arena.ArenaCamera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                Arena.ArenaCamera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture old = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { RenderTexture.active = old; Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null; target.Release(); Object.Destroy(target);
                Arena.Dispose(); art.Dispose(); Object.Destroy(Settings); Object.Destroy(root);
            }
        }
    }
}
