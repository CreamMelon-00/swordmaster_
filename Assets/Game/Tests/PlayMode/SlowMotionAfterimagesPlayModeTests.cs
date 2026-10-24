using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class SlowMotionAfterimagesPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator SuccessfulStepsAndDecisiveFocus_UseOneStrongPlayerTrail_WithoutStackedBodyCopies()
        {
            yield return null;
            using (var scope = new TrailScope())
            foreach (string cue in new[] { "dodge", "pressure", "decisive", "finish" })
            {
                scope.Begin();
                scope.Cue(cue);
                scope.Arena.Tick(0f, .181f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.EqualTo(1), cue);
                SpriteRenderer ghost = scope.Ghosts[0];
                Assert.That(ghost.sprite, Is.Not.Null);
                Assert.That(ghost.sortingOrder, Is.LessThan(scope.Arena.PlayerDepthSortingOrder));
                Assert.That(scope.Arena.ActiveAuraAfterimageCount, Is.Zero);
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.Zero);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero);
                Assert.That(scope.Arena.ActiveStepAfterimageCount, Is.Zero,
                    "The cinematic body copy replaces the ordinary step snapshots instead of stacking with them.");
                bool strongFreshGhost = false;
                foreach (SpriteRenderer pose in scope.Ghosts)
                    if (pose.gameObject.activeInHierarchy && pose.sprite != null && DuelStepAfterimages.DrawnColor(pose).a >= .65f)
                        strongFreshGhost = true;
                Assert.That(strongFreshGhost, Is.True, "At least one fresh copy retains the stronger opacity. " + cue);
            }
        }

        [UnityTest]
        public IEnumerator SlowMotion_LeavesDistinctSnapshotsAtASparserRealTimeCadence()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                scope.Begin(); scope.Cue("decisive");
                int births = 0;
                int lastBirthFrame = -1;
                int maximumVisible = 0;
                for (int frame = 1; frame <= 60; frame++)
                {
                    scope.Arena.Tick(0f, .01f);
                    int visible = scope.Arena.ActiveSlowAfterimageCount;
                    maximumVisible = Mathf.Max(maximumVisible, visible);
                    Assert.That(visible, Is.InRange(0, 2), "Even overlapping snapshot lifetimes cannot leave more than two player copies.");
                    int fresh = 0;
                    foreach (SpriteRenderer ghost in scope.Ghosts)
                        if (ghost.gameObject.activeInHierarchy && ghost.transform.parent == scope.TrailRoot &&
                            DuelStepAfterimages.DrawnColor(ghost).a > .699f) fresh++;
                    Assert.That(fresh, Is.InRange(0, 1), "A rendered frame cannot invent duplicate snapshots.");
                    if (fresh == 0) continue;
                    if (lastBirthFrame >= 0)
                        Assert.That(frame - lastBirthFrame, Is.InRange(18, 19),
                            "Fresh poses are spaced apart even when the simulation animation clock is held.");
                    lastBirthFrame = frame;
                    births++;
                }
                Assert.That(births, Is.EqualTo(3), "A 0.60-second slow shot leaves only three spaced poses.");
                Assert.That(maximumVisible, Is.EqualTo(2), "The previous pose overlaps the next only briefly before its 0.22-second life expires.");
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.EqualTo(1), "Older copies fade before the third snapshot is shown.");
            }
        }

        [UnityTest]
        public IEnumerator CinematicSlow_ClearsAndSuppressesPlayerSecondaryCopies_ThenOrdinaryTrailsResume()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                scope.Begin();
                scope.Arena.PerformStep(LegacyStepAction.Pressure, false);
                scope.Arena.Tick(0f, scope.Arena.StepGestureDuration);
                Invoke(scope.Arena, "ConfigureSlotTiming", .6f, .1f);
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .46f);
                Invoke(scope.Arena, "TickPressureAttackTrail", .04f, true);
                Assert.That(scope.Arena.ActiveStepAfterimageCount, Is.GreaterThan(0), "The ordinary pressure path still has its own trail.");
                scope.Arena.BeginSignature(true, Color.cyan, 0f, false, .4f, .5f);
                Invoke(scope.Arena, "TickSignatureTrails", .04f, true);
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.EqualTo(1));
                object player = Actor(scope.Arena, "player");
                Invoke(scope.Arena, "EmitAttackEcho", player);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(1), "The ordinary stroke echo remains available.");

                scope.Cue("decisive");
                Assert.That(scope.Arena.ActiveStepAfterimageCount, Is.Zero, "Entering the close-up removes already visible step/pressure copies.");
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.Zero, "Entering the close-up removes already visible player signature copies.");
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero, "Entering the close-up removes already visible player stroke copies.");
                Assert.That(scope.Arena.IsPressureAttackTrailActive, Is.True, "Suppression keeps the ordinary pressure cue armed.");
                Assert.That(scope.Arena.IsSignatureTrailArmed(true), Is.True);
                scope.Arena.PerformStep(LegacyStepAction.Dodge, false);
                scope.Arena.PerformStep(LegacyStepAction.Pressure, true, cinematic: false);
                Assert.That(scope.Arena.ActiveStepAfterimageCount, Is.Zero,
                    "Failed or automatic gestures during an existing slow shot cannot add a second body trail.");
                for (int frame = 0; frame < 20; frame++)
                {
                    scope.Arena.Tick(0f, .02f);
                    Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .49f);
                    Invoke(scope.Arena, "TickPressureAttackTrail", .02f, true);
                    Invoke(scope.Arena, "TickSignatureTrails", .02f, true);
                    Invoke(scope.Arena, "EmitAttackEcho", player);
                    Assert.That(scope.Arena.ActiveStepAfterimageCount, Is.Zero);
                    Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.Zero);
                    Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero);
                    int allCopies = scope.Arena.ActiveSlowAfterimageCount + scope.Arena.ActiveStepAfterimageCount +
                        scope.Arena.ActiveSignatureAfterimageCount + scope.Arena.ActiveAttackEchoCount;
                    Assert.That(allCopies, Is.InRange(0, 2), "Several armed effects cannot accumulate extra copies between the dedicated slow snapshots.");
                }

                scope.Arena.Tick(0f, 1f);
                Invoke(scope.Arena, "TickPressureAttackTrail", .04f, true);
                Invoke(scope.Arena, "TickSignatureTrails", .04f, true);
                Assert.That(scope.Arena.ActiveStepAfterimageCount, Is.EqualTo(1), "Normal pressure cadence resumes after slow motion ends.");
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.EqualTo(1), "Normal signature cadence resumes after slow motion ends.");
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero);
                Invoke(scope.Arena, "EmitAttackEcho", player);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.Zero,
                    "A stroke suppressed during slow motion is consumed and cannot appear late after the close-up ends.");
            }
        }

        [UnityTest]
        public IEnumerator CinematicSlow_RetainsEnemyCopiesAndContinuesEnemyTrails_InSharedPools()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                scope.Begin();
                Invoke(scope.Arena, "ConfigureSlotTiming", .6f, .1f);
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .46f);
                scope.Arena.BeginSignature(true, Color.cyan, 0f, false, .4f, .5f);
                scope.Arena.BeginSignature(false, Color.red, 0f, false, .4f, .5f);
                Invoke(scope.Arena, "TickSignatureTrails", .04f, true);
                Invoke(scope.Arena, "EmitAttackEcho", Actor(scope.Arena, "player"));
                Invoke(scope.Arena, "EmitAttackEcho", Actor(scope.Arena, "enemy"));
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.EqualTo(2));
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(2));

                scope.Cue("decisive");
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.EqualTo(1), "Only the player's existing signature copy is replaced by the slow trail.");
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(1), "The enemy's existing stroke copy survives the player close-up.");
                Invoke(scope.Arena, "TickSignatureTrails", .04f, true);
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.EqualTo(1), "The existing enemy trail keeps its own slow-shot cadence.");
                Invoke(scope.Arena, "TickSignatureTrails", .046f, true);
                Assert.That(scope.Arena.ActiveSignatureAfterimageCount, Is.EqualTo(2), "An enemy signature can still emit while player signature copies are suppressed.");
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .49f);
                Invoke(scope.Arena, "TickAttackMotionTrail", .04f, true);
                Assert.That(scope.Arena.ActiveAttackEchoCount, Is.EqualTo(2), "The next enemy stroke adds one echo, while the player's stroke stays suppressed.");
            }
        }

        [UnityTest]
        public IEnumerator PlanningMissesAndFrozenTime_DoNotEmit_AndLifecycleBoundariesClearTheTrail()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                for (int frame = 0; frame < 20; frame++)
                {
                    scope.Arena.SetPlanningState(5f, false, true);
                    scope.Arena.Tick(.001f, .02f);
                }
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero, "Planning's automatic slow motion has no success trail.");
                scope.Begin();
                scope.Arena.PerformStep(LegacyStepAction.Dodge, false);
                scope.Advance(.3f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero, "An unsuccessful step is not a reward cue.");
                scope.Begin(); scope.Cue("decisive");
                scope.Advance(.24f);
                int count = scope.Arena.ActiveSlowAfterimageCount;
                Vector3 position = scope.Ghosts[0].transform.position;
                Color color = DuelStepAfterimages.DrawnColor(scope.Ghosts[0]);
                for (int frame = 0; frame < 12; frame++) scope.Arena.Tick(0f, 0f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.EqualTo(count));
                Assert.That(scope.Ghosts[0].transform.position, Is.EqualTo(position));
                Assert.That(DuelStepAfterimages.DrawnColor(scope.Ghosts[0]), Is.EqualTo(color), "Pause/finishing freeze hold the exact picture.");
                Invoke(scope.Arena, "SuspendForCutscene");
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero);
                Assert.That(scope.TrailRoot.gameObject.activeSelf, Is.False);
                scope.Arena.Tick(0f, .04f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero, "A scene cannot emit borrowed combat poses.");
                Invoke(scope.Arena, "ResumeAfterCutscene");
                Assert.That(scope.TrailRoot.gameObject.activeSelf, Is.True);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero, "The restored battle starts with no displaced scene ghosts.");
                scope.Advance(.20f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.GreaterThan(0));
                scope.Arena.EndTurn();
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero);
                scope.Begin(); scope.Cue("decisive"); scope.Advance(.20f);
                scope.Arena.BeginTurn();
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero);
                scope.Begin(); scope.Cue("decisive"); scope.Advance(.20f);
                scope.Arena.ClearSlowMotionAfterimages();
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero, "Results and lobby exits clear the frozen finale's trail.");
                scope.Arena.Reset(); scope.Advance(.12f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator SnapshotsBakeTheDrawnLunge_FadeOnRealTime_AndRemainBoundedBehindAllFighters()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                Assert.That(scope.TrailRoot.childCount, Is.EqualTo(2), "The dedicated pool prewarms only the two copies that may be visible.");
                int objects = scope.TrailRoot.GetComponentsInChildren<Transform>(true).Length;
                float globalScale = Time.timeScale;
                try
                {
                    Time.timeScale = 0f;
                    scope.Begin();
                    scope.Arena.SetAttackRhythmOverride(true);
                    Invoke(scope.Arena, "ConfigureSlotTiming", .6f, .1f);
                    Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .4f);
                    scope.Cue("decisive");
                    scope.Arena.Tick(0f, .181f);
                    Assert.That(scope.Arena.PlayerAttackVisualOffset.sqrMagnitude, Is.GreaterThan(.001f));
                    Vector3 drawnPosition = scope.Arena.PlayerRenderer.transform.position + scope.Arena.PlayerAttackVisualOffset;
                    Assert.That(Vector3.Distance(scope.Ghosts[0].transform.position, drawnPosition), Is.LessThan(.0001f),
                        "The world-space ghost bakes shader displacement instead of snapping back to the logical actor anchor.");
                    Vector3 originalGhostPosition = scope.Ghosts[0].transform.position;
                    Color originalGhostColor = DuelStepAfterimages.DrawnColor(scope.Ghosts[0]);
                    scope.Arena.Tick(0f, .04f);
                    Assert.That(DuelStepAfterimages.DrawnColor(scope.Ghosts[0]).a, Is.LessThan(originalGhostColor.a));
                    Assert.That(scope.Ghosts[0].transform.position, Is.EqualTo(originalGhostPosition),
                        "A slow-motion afterimage stays at the captured world position while only its opacity fades.");
                    for (int hitch = 0; hitch < 16; hitch++)
                    {
                        scope.Arena.HoldFinishingFocus(false, true, 10f);
                        int beforeHitch = scope.Arena.ActiveSlowAfterimageCount;
                        scope.Arena.Tick(0f, .37f);
                        Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.LessThanOrEqualTo(beforeHitch + 1));
                        Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.LessThanOrEqualTo(2));
                        int fresh = 0;
                        foreach (SpriteRenderer ghost in scope.Ghosts)
                            if (ghost.gameObject.activeInHierarchy && ghost.transform.parent == scope.TrailRoot &&
                                DuelStepAfterimages.DrawnColor(ghost).a > .699f) fresh++;
                        Assert.That(fresh, Is.EqualTo(1),
                            "A long frame emits one snapshot, never a whole backlog at one identical pose.");
                    }
                    Assert.That(scope.TrailRoot.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                    scope.Arena.ConfigureEnemyRoster(new[] { EnemyAppearance.CadetA, EnemyAppearance.CadetB, EnemyAppearance.Student });
                    scope.Begin(); scope.Cue("decisive"); scope.Advance(.20f);
                    foreach (SpriteRenderer ghost in scope.Ghosts)
                        if (ghost.sprite != null) Assert.That(ghost.sortingOrder, Is.LessThanOrEqualTo(-4));
                    scope.Arena.ConfigureEnemyRoster(null);
                    scope.Begin(); scope.Cue("decisive"); scope.Advance(.20f);
                    foreach (SpriteRenderer ghost in scope.Ghosts)
                        if (ghost.sprite != null) Assert.That(ghost.sortingOrder, Is.LessThanOrEqualTo(-2));
                    scope.Arena.Tick(0f, 2f);
                    Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero, "When focus and the real-time fade end, no snapshots remain.");
                    scope.Arena.Dispose();
                    yield return null;
                    Assert.That(scope.TrailRoot == null, Is.True, "The arena owns and destroys its prewarmed pool.");
                }
                finally { Time.timeScale = globalScale; }
            }
        }

        [UnityTest]
        public IEnumerator DeferredControllerSampling_UsesOnlyTheFinalHeldPose_AndDoesNotFadeTwice()
        {
            yield return null;
            using (var scope = new TrailScope())
            {
                scope.Begin(); scope.Cue("decisive");
                scope.Arena.SetAttackRhythmOverride(true);
                Invoke(scope.Arena, "ConfigureSlotTiming", .6f, .1f);
                Invoke(scope.Arena, "Tick", .32f, .181f, true);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.Zero,
                    "The controller's provisional advanced animation is never copied before its impact/hold clamps.");
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .4f);
                Sprite heldSprite = scope.Arena.PlayerRenderer.sprite;
                Vector3 heldPosition = scope.Arena.PlayerRenderer.transform.position + scope.Arena.PlayerAttackVisualOffset;
                Invoke(scope.Arena, "SampleSlowAfterimages", .181f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.EqualTo(1));
                SpriteRenderer first = scope.Ghosts[0];
                Assert.That(first.sprite, Is.SameAs(heldSprite));
                Assert.That(Vector3.Distance(first.transform.position, heldPosition), Is.LessThan(.0001f));
                float firstAlpha = DuelStepAfterimages.DrawnColor(first).a;
                Invoke(scope.Arena, "Tick", 0f, .04f, true);
                float afterTickAlpha = DuelStepAfterimages.DrawnColor(first).a;
                Assert.That(afterTickAlpha, Is.EqualTo(firstAlpha * (1f - .04f / .22f)).Within(.001f),
                    "A deferred frame still ages the existing pool once on real time.");
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .4f);
                Invoke(scope.Arena, "SampleSlowAfterimages", .04f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.EqualTo(1), "The shorter intervening frame cannot emit early.");
                Assert.That(DuelStepAfterimages.DrawnColor(first).a, Is.EqualTo(afterTickAlpha).Within(.0001f),
                    "The post-hold emission cannot age existing snapshots a second time in the same rendered frame.");
                Invoke(scope.Arena, "Tick", 0f, .14f, true);
                float nextFrameAlpha = DuelStepAfterimages.DrawnColor(first).a;
                Invoke(scope.Arena, "HoldSlotAtTime", LegacyArenaView.OriginalClipDuration / .6f * .4f);
                Invoke(scope.Arena, "SampleSlowAfterimages", .14f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.EqualTo(2));
                Assert.That(DuelStepAfterimages.DrawnColor(first).a, Is.EqualTo(nextFrameAlpha).Within(.0001f),
                    "Reaching the next snapshot also leaves the pool's once-per-frame fade unchanged.");
            }
        }

        [UnityTest]
        public IEnumerator NativeSlowMotionCaptures_LeaveTheTrailInPlaceWhenTheBodyMoves_AtBothAspects()
        {
            yield return null;
            using (var scope = new TrailScope())
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
            foreach (string cue in new[] { "dodge", "pressure", "decisive", "finish" })
            {
                scope.Resize(size);
                scope.Begin();
                scope.Advance(.3f);
                scope.Cue(cue);
                scope.Advance(.28f);
                Assert.That(scope.Arena.ActiveSlowAfterimageCount, Is.GreaterThan(0));
                // A held body now covers its coincident snapshot. Moving the body after capture must reveal the
                // original world-space copy, in either travel direction, without launching the ghost anywhere.
                scope.Arena.PlayerRenderer.transform.position += Vector3.right * (cue == "dodge" ? -.8f : .8f);
                string label = size.x + "x" + size.y + "-" + cue;
                Color32[] withTrail = scope.Capture(label + "-trail");
                scope.TrailRoot.gameObject.SetActive(false);
                Color32[] withoutTrail = scope.Capture(label + "-without");
                scope.TrailRoot.gameObject.SetActive(true);
                int changed = 0;
                for (int index = 0; index < withTrail.Length; index++)
                    if (Math.Abs(withTrail[index].r - withoutTrail[index].r) + Math.Abs(withTrail[index].g - withoutTrail[index].g) +
                        Math.Abs(withTrail[index].b - withoutTrail[index].b) > 12) changed++;
                Assert.That(changed, Is.GreaterThan(150), "Native render must reveal the fixed snapshot left where the moving protagonist was. " + label);
            }
        }

        private static void Invoke(LegacyArenaView arena, string name, params object[] arguments)
            => typeof(LegacyArenaView).GetMethod(name, Private).Invoke(arena, arguments);

        private static object Actor(LegacyArenaView arena, string name)
            => typeof(LegacyArenaView).GetField(name, Private).GetValue(arena);

        private sealed class TrailScope : IDisposable
        {
            private readonly GameObject root = new GameObject("Slow Motion Afterimages Test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly DuelPrototypeController scene;
            private readonly bool sceneEnabled;
            private RenderTexture target;
            public LegacyArenaView Arena { get; }
            public Transform TrailRoot { get; }
            public SpriteRenderer[] Ghosts { get; }

            public TrailScope()
            {
                scene = Object.FindAnyObjectByType<DuelPrototypeController>();
                if (scene != null) { sceneEnabled = scene.enabled; scene.enabled = false; }
                JsonUtility.FromJsonOverwrite("{\"battleDriftSpeed\":0,\"impactCameraShake\":0}", settings);
                Arena = LegacyArenaView.Create(root.transform, art, settings);
                Arena.SetBackdrop(ArenaBackdropKind.SchoolCorridor);
                TrailRoot = root.transform.Find("Inherited Duel Arena/Duel Slow Motion Afterimages");
                if (TrailRoot == null)
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                        if (node.name == "Duel Slow Motion Afterimages") { TrailRoot = node; break; }
                Assert.That(TrailRoot, Is.Not.Null);
                Ghosts = TrailRoot.GetComponentsInChildren<SpriteRenderer>(true);
            }

            public void Begin()
            {
                Arena.Reset();
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY - .2f, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY + .2f, 0f);
                var attack = new LegacySkill(98793, "잔상 검증", 0, 5, 5, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);
                Arena.BeginSlot(attack, attack);
            }

            public void Cue(string cue)
            {
                if (cue == "dodge" || cue == "pressure")
                    Arena.PerformStep(cue == "dodge" ? LegacyStepAction.Dodge : LegacyStepAction.Pressure, true);
                else
                {
                    Arena.PresentDecisiveFocus(true);
                    if (cue == "finish") Arena.HoldFinishingFocus(false, true, settings.FinishingSlowMotionSeconds);
                }
            }

            public void Advance(float seconds)
            {
                int frames = Mathf.Max(1, Mathf.CeilToInt(seconds / .02f));
                for (int frame = 0; frame < frames; frame++)
                {
                    Arena.SetHudFloor(.31f);
                    Arena.SetCombatShot(.5f, true, true, false);
                    Arena.Tick(0f, seconds / frames);
                }
            }

            public void Resize(Vector2Int size)
            {
                Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                Camera camera = Arena.ArenaCamera;
                camera.targetTexture = target; camera.aspect = size.x / (float)size.y;
            }

            public Color32[] Capture(string name)
            {
                Volume volume = root.GetComponentInChildren<Volume>(true);
                bool volumeEnabled = volume != null && volume.enabled;
                if (volume != null) volume.enabled = false;
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
                Camera camera = Arena.ArenaCamera; camera.cullingMask = 1 << 31;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
                if (volume != null) volume.enabled = volumeEnabled;
                camera.Render();
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                    string output = Environment.GetEnvironmentVariable("SWORDMASTER_SLOW_TRAIL_CAPTURE_DIR");
                    if (!string.IsNullOrEmpty(output))
                    { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); }
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }

            public void Dispose()
            {
                if (Arena.ArenaCamera != null) Arena.ArenaCamera.targetTexture = null;
                if (target != null) { target.Release(); Object.Destroy(target); }
                Arena.Dispose(); art.Dispose(); Object.Destroy(settings); Object.Destroy(root);
                if (scene != null) scene.enabled = sceneEnabled;
            }
        }
    }
}
