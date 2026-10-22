using System;
using System.Collections;
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
    public sealed class ResistanceImpactArenaPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly LegacySkill Attack = new LegacySkill(98901, "Body-check follow-up", 1, 8, 8,
            LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, string.Empty);

        [UnityTest]
        public IEnumerator ACheckBracesThenRushesInEitherDirectionWithoutMovingCombatFeetOrItsClock()
        {
            using (var scope = new ArenaScope())
            {
                foreach (bool playerChecks in new[] { true, false })
                {
                    var arena = scope.Arena;
                    scope.Begin();
                    Sprite playerBefore = arena.PlayerRenderer.sprite, enemyBefore = arena.EnemyRenderer.sprite;
                    Vector3 playerFeet = arena.PlayerRenderer.transform.position, enemyFeet = arena.EnemyRenderer.transform.position;
                    Vector3 playerShadow = scope.PlayerShadow.position, enemyShadow = scope.EnemyShadow.position;
                    Assert.That(arena.BeginResistanceImpact(playerChecks, !playerChecks), Is.True);
                    Assert.That(arena.PlayerRenderer.sprite.name, Does.Contain("block"));
                    Assert.That(arena.EnemyRenderer.sprite.name, Does.Contain("block"));
                    arena.Tick(0f, 10f);
                    Assert.That(arena.IsResistanceImpactPlaying, Is.True, "Only the caller's real-time phase advances the check.");
                    Assert.That(arena.HasResistanceImpactContact, Is.False);
                    Assert.That(arena.AdvanceResistanceImpact(.025f), Is.False);
                    Vector3 windup = playerChecks ? arena.PlayerAttackVisualOffset : arena.EnemyAttackVisualOffset;
                    Assert.That(windup.x * (playerChecks ? 1f : -1f), Is.LessThan(0f), "A short brace anticipates the forward burst.");
                    Assert.That(arena.AdvanceResistanceImpact(.05f), Is.False);
                    Vector3 rush = playerChecks ? arena.PlayerAttackVisualOffset : arena.EnemyAttackVisualOffset;
                    Assert.That(rush.x * (playerChecks ? 1f : -1f), Is.GreaterThan(.5f));
                    Assert.That(arena.AdvanceResistanceImpact(.0251f), Is.True);
                    Assert.That(arena.HasResistanceImpactContact, Is.True);
                    Assert.That(arena.ImpactGlow.ActiveCount, Is.EqualTo(1));
                    Assert.That(arena.ActiveParticleCount, Is.Zero, "The cosmetic check does not fabricate an ordinary hit.");
                    Assert.That(arena.PlayerRenderer.transform.position, Is.EqualTo(playerFeet));
                    Assert.That(arena.EnemyRenderer.transform.position, Is.EqualTo(enemyFeet));
                    Assert.That(arena.PlayerAttackVisualOffset.y, Is.Zero);
                    Assert.That(arena.EnemyAttackVisualOffset.y, Is.Zero);
                    Vector3 drawnPlayerBody = BodyPoint(arena, true) + arena.PlayerAttackVisualOffset;
                    Vector3 drawnEnemyBody = BodyPoint(arena, false) + arena.EnemyAttackVisualOffset;
                    Assert.That(drawnEnemyBody.x - drawnPlayerBody.x, Is.InRange(1.1f, 1.4f),
                        "The shoulder check must actually reach the opposing body, beyond the sprite pivots' empty space.");
                    Assert.That(Vector3.Distance(scope.ContactGlowPosition, (drawnPlayerBody + drawnEnemyBody) * .5f), Is.LessThan(.01f),
                        "The glow belongs between the actual drawn chests, not above their heads.");
                    Assert.That(arena.PlayerGroundCircle.position, Is.EqualTo(arena.PlayerGroundPosition + arena.PlayerAttackVisualOffset));
                    Assert.That(arena.EnemyGroundCircle.position, Is.EqualTo(arena.EnemyGroundPosition + arena.EnemyAttackVisualOffset));
                    Assert.That(scope.PlayerShadow.position, Is.EqualTo(playerShadow + arena.PlayerAttackVisualOffset), "The original floor shadow follows the checking body too.");
                    Assert.That(scope.EnemyShadow.position, Is.EqualTo(enemyShadow + arena.EnemyAttackVisualOffset));
                    Assert.That(scope.Reactions.Calls, Is.Zero, "Bracing consumes no hurt/guard random draws.");
                    Assert.That(arena.AdvanceResistanceImpact(.20f), Is.False);
                    Assert.That(arena.IsResistanceImpactPlaying, Is.False);
                    Assert.That(arena.HasResistanceImpactContact, Is.True, "The contact edge remains observable after completion.");
                    Assert.That(arena.PlayerRenderer.sprite, Is.SameAs(playerBefore));
                    Assert.That(arena.EnemyRenderer.sprite, Is.SameAs(enemyBefore));
                    Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero));
                    Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));
                    Assert.That(arena.PlayerGroundCircle.position, Is.EqualTo(arena.PlayerGroundPosition));
                    Assert.That(arena.EnemyGroundCircle.position, Is.EqualTo(arena.EnemyGroundPosition));
                    Assert.That(scope.PlayerShadow.position, Is.EqualTo(playerShadow));
                    Assert.That(scope.EnemyShadow.position, Is.EqualTo(enemyShadow));
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SimultaneousChecksMirrorEachOtherAndNeverCrossEvenAtAShortGap()
        {
            using (var scope = new ArenaScope())
            {
                scope.Begin();
                var arena = scope.Arena;
                arena.PlayerRenderer.transform.position = new Vector3(-1f, -2f, 0f);
                arena.EnemyRenderer.transform.position = new Vector3(1f, -1.4f, 0f);
                Assert.That(arena.BeginResistanceImpact(true, true), Is.True);
                Assert.That(arena.AdvanceResistanceImpact(LegacyArenaView.ResistanceImpactContactTime), Is.True);
                Assert.That(arena.PlayerAttackVisualOffset.x, Is.GreaterThan(0f),
                    "A close simultaneous check uses only the remaining room rather than a fixed minimum lunge.");
                Assert.That(arena.EnemyAttackVisualOffset.x, Is.EqualTo(-arena.PlayerAttackVisualOffset.x).Within(.00001f));
                Vector3 playerDrawn = BodyPoint(arena, true) + arena.PlayerAttackVisualOffset;
                Vector3 enemyDrawn = BodyPoint(arena, false) + arena.EnemyAttackVisualOffset;
                Assert.That(enemyDrawn.x - playerDrawn.x, Is.InRange(1.2499f, 1.2501f), "Measure the mirrored bodies, whose pivots contain empty space.");
                Assert.That(playerDrawn.y, Is.EqualTo(BodyPoint(arena, true).y));
                Assert.That(enemyDrawn.y, Is.EqualTo(BodyPoint(arena, false).y), "The two fighters retain their own ground depth.");
                Assert.That(arena.ImpactGlow.ActiveCount, Is.EqualTo(1), "A simultaneous body contact is one bounded visual impulse.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContactIsEmittedOnceOnALongFrame_EmptyAndInvalidAdvancesDoNothing()
        {
            using (var scope = new ArenaScope())
            {
                scope.Begin();
                var arena = scope.Arena;
                Assert.That(arena.BeginResistanceImpact(false, false), Is.False);
                Assert.That(arena.AdvanceResistanceImpact(1f), Is.False);
                Assert.That(arena.IsResistanceImpactPlaying, Is.False);
                Assert.That(arena.BeginResistanceImpact(true, false), Is.True);
                foreach (float delta in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    Assert.That(arena.AdvanceResistanceImpact(delta), Is.False);
                Assert.That(arena.HasResistanceImpactContact, Is.False);
                Assert.That(arena.AdvanceResistanceImpact(1f), Is.True);
                Assert.That(arena.IsResistanceImpactPlaying, Is.False);
                Assert.That(arena.HasResistanceImpactContact, Is.True);
                for (int i = 0; i < 3; i++) Assert.That(arena.AdvanceResistanceImpact(1f), Is.False);
                Assert.That(arena.ImpactGlow.ActiveCount, Is.EqualTo(1));
                arena.ClearResistanceImpact();
                Assert.That(arena.HasResistanceImpactContact, Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DummyCanReceiveButNeverInitiate_AndChecksDoNotEmitAttackTrails()
        {
            using (var scope = new ArenaScope())
            {
                scope.Begin();
                var arena = scope.Arena;
                arena.SetEnemyAppearance(EnemyAppearance.TrainingDummy);
                Vector3 dummyFeet = arena.EnemyRenderer.transform.position;
                Assert.That(arena.BeginResistanceImpact(false, true), Is.False);
                Assert.That(arena.BeginResistanceImpact(true, true), Is.True);
                Assert.That(arena.AdvanceResistanceImpact(.12f), Is.True);
                Assert.That(arena.EnemyRenderer.transform.position, Is.EqualTo(dummyFeet));
                Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero));
                Assert.That(arena.IsEnemyHurtPlaying, Is.False);
                Assert.That(arena.EnemyRenderer.sprite.name, Does.Contain("idle"));
                scope.Begin();
                Hold(arena, LegacyArenaView.OriginalClipDuration / .5f * .5f);
                Vector3 heldPlayerOffset = arena.PlayerAttackVisualOffset, heldEnemyOffset = arena.EnemyAttackVisualOffset;
                Assert.That(heldPlayerOffset.x, Is.GreaterThan(.1f));
                Assert.That(arena.BeginResistanceImpact(true, false), Is.True);
                Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(heldPlayerOffset), "A deferred counter keeps its existing drawn contact position.");
                Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(heldEnemyOffset));
                arena.AdvanceResistanceImpact(.10f);
                Trail(arena);
                Assert.That(arena.ActiveAttackEchoCount, Is.Zero);
                Assert.That(arena.WeaponTrailEmissionCount, Is.Zero, "A body check never spends the sword hit's trail identity.");
                arena.PresentDecisiveFocus(true);
                arena.Tick(0f, .04f);
                Assert.That(arena.SlowMotionMovementAmount, Is.Zero,
                    "The break's close-up cannot grow the paused counter lunge underneath its body-check baseline.");
                arena.ClearResistanceImpact();
                Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(heldPlayerOffset), "Clearing the check resumes the same held attack without a jump.");
                Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(heldEnemyOffset));
                Trail(arena);
                Assert.That(arena.WeaponTrailEmissionCount, Is.GreaterThan(0), "The actual follow-up still owns its first blade trail.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhaseBoundariesAndCutscenesClearTheCheckWithoutRestoringItsTemporaryPose()
        {
            using (var scope = new ArenaScope())
            {
                foreach (string boundary in new[] { "Reset", "BeginTurn", "BeginSlot", "EndTurn", "Cutscene", "Clear" })
                {
                    scope.Begin();
                    var arena = scope.Arena;
                    arena.BeginResistanceImpact(true, true);
                    arena.AdvanceResistanceImpact(.1f);
                    switch (boundary)
                    {
                        case "Reset": arena.Reset(); break;
                        case "BeginTurn": arena.BeginTurn(); break;
                        case "BeginSlot": arena.BeginSlot(Attack, Attack); break;
                        case "EndTurn": arena.EndTurn(); break;
                        case "Cutscene":
                            typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(arena, null);
                            typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(arena, null);
                            break;
                        default: arena.ClearResistanceImpact(); break;
                    }
                    Assert.That(arena.IsResistanceImpactPlaying, Is.False, boundary);
                    Assert.That(arena.HasResistanceImpactContact, Is.False, boundary);
                    Assert.That(arena.PlayerAttackVisualOffset, Is.EqualTo(Vector3.zero), boundary);
                    Assert.That(arena.EnemyAttackVisualOffset, Is.EqualTo(Vector3.zero), boundary);
                    Assert.That(arena.AdvanceResistanceImpact(.2f), Is.False, boundary);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NativeRenderedBraceActuallyMovesForward_WhenTheDrawOffsetIsApplied()
        {
            yield return null;
            using (var scope = new ArenaScope())
            {
                scope.Begin();
                var arena = scope.Arena;
                arena.BeginResistanceImpact(true, false);
                scope.MaskPlayerOnly();
                yield return null;
                Color32[] before = scope.ReadPixels();
                float beforeX = BrightCentroid(before);
                float bodyGapBefore = BodyPoint(arena, false).x - BodyPoint(arena, true).x;
                arena.AdvanceResistanceImpact(.1f);
                Assert.That(arena.PlayerRenderer.sprite.name, Does.Contain("block"));
                yield return null;
                Color32[] contact = scope.ReadPixels();
                float pixelsPerWorldUnit = ArenaScope.Height / (2f * arena.ArenaCamera.orthographicSize);
                float observedShift = (BrightCentroid(contact) - beforeX) / pixelsPerWorldUnit;
                Assert.That(bodyGapBefore - observedShift, Is.InRange(1.1f, 1.4f),
                    "Read-back pixels must prove that the body reaches shoulder contact with the opposing torso, rather than checking empty air.");
                float playerTorsoRow = arena.ArenaCamera.WorldToScreenPoint(BodyPoint(arena, true)).y;
                float playerTorsoX = BrightTorsoCentroid(contact, playerTorsoRow, pixelsPerWorldUnit * .25f);
                scope.MaskActorOnly(false);
                yield return null;
                Color32[] enemyContact = scope.ReadPixels();
                float enemyTorsoRow = arena.ArenaCamera.WorldToScreenPoint(BodyPoint(arena, false)).y;
                float enemyTorsoX = BrightTorsoCentroid(enemyContact, enemyTorsoRow, pixelsPerWorldUnit * .25f);
                Assert.That(playerTorsoX, Is.LessThan(enemyTorsoX), "The actual rendered bodies retain their left/right order at impact.");
                Assert.That((enemyTorsoX - playerTorsoX) / pixelsPerWorldUnit, Is.InRange(1.1f, 1.4f),
                    "Independently masked torso pixels prove contact spacing and catch incorrect authored-pose anchors.");
                string capture = Environment.GetEnvironmentVariable("SWORDMASTER_RESISTANCE_IMPACT_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(capture))
                {
                    Directory.CreateDirectory(capture);
                    scope.Save(before, Path.Combine(capture, "body-check-brace.png"));
                    scope.Save(contact, Path.Combine(capture, "body-check-contact.png"));
                    scope.Save(enemyContact, Path.Combine(capture, "body-check-target.png"));
                }
            }
        }

        private static void Hold(LegacyArenaView arena, float time)
            => typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private).Invoke(arena, new object[] { time });
        private static void Trail(LegacyArenaView arena)
            => typeof(LegacyArenaView).GetMethod("TickAttackMotionTrail", Private).Invoke(arena, new object[] { .016f, true });
        private static Vector3 BodyPoint(LegacyArenaView arena, bool player)
        {
            SpriteRenderer actor = player ? arena.PlayerRenderer : arena.EnemyRenderer;
            Vector3 point = new Vector3(player || arena.EnemyAppearance == EnemyAppearance.Elisa ? .25f : 0f, -.25f, 0f);
            if (actor.flipX) point.x = -point.x;
            if (actor.flipY) point.y = -point.y;
            return actor.transform.TransformPoint(point);
        }
        private static float BrightCentroid(Color32[] pixels)
        {
            double sum = 0d;
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r + pixels[i].g + pixels[i].b < 45) continue;
                sum += i % ArenaScope.Width;
                count++;
            }
            Assert.That(count, Is.GreaterThan(100));
            return (float)(sum / count);
        }
        private static float BrightTorsoCentroid(Color32[] pixels, float row, float halfHeight)
        {
            double sum = 0d;
            int count = 0;
            int first = Mathf.Clamp(Mathf.FloorToInt(row - halfHeight), 0, ArenaScope.Height - 1);
            int last = Mathf.Clamp(Mathf.CeilToInt(row + halfHeight), 0, ArenaScope.Height - 1);
            for (int y = first; y <= last; y++)
                for (int x = 0; x < ArenaScope.Width; x++)
                {
                    Color32 pixel = pixels[y * ArenaScope.Width + x];
                    if (pixel.r + pixel.g + pixel.b < 45) continue;
                    sum += x;
                    count++;
                }
            Assert.That(count, Is.GreaterThan(50));
            return (float)(sum / count);
        }

        private sealed class ArenaScope : IDisposable
        {
            public const int Width = 960, Height = 540;
            private const int CaptureLayer = 31;
            private readonly GameObject host = new GameObject("Resistance body-check test");
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            public CountingZero Reactions { get; } = new CountingZero();
            public LegacyArenaView Arena { get; }
            public Transform PlayerShadow => Arena.PlayerRenderer.transform.Find("Original Ground Shadow");
            public Transform EnemyShadow => Arena.EnemyRenderer.transform.Find("Original Ground Shadow");
            public Vector3 ContactGlowPosition
            {
                get
                {
                    foreach (MeshRenderer glow in host.GetComponentsInChildren<MeshRenderer>())
                        if (glow.gameObject.name.StartsWith("Impact Glow ", StringComparison.Ordinal)) return glow.transform.position;
                    Assert.Fail("A live contact glow must be present.");
                    return Vector3.zero;
                }
            }
            public ArenaScope()
            {
                Arena = LegacyArenaView.Create(host.transform, art, settings,
                    reactionPoseRandom: Reactions, enemyReactionPoseRandom: Reactions,
                    attackPoseRandom: new CountingZero(), enemyAttackPoseRandom: new CountingZero());
                JsonUtility.FromJsonOverwrite("{\"weaponTrailEnabled\":true,\"weaponTrailStrength\":1}", settings);
                Arena.ArenaCamera.targetTexture = target;
            }
            public void Begin()
            {
                Arena.SetEnemyAppearance(EnemyAppearance.Elisa);
                Arena.Reset();
                Arena.PlayerRenderer.transform.localPosition = new Vector3(-2f, Arena.ActorGroundY, 0f);
                Arena.EnemyRenderer.transform.localPosition = new Vector3(2f, Arena.ActorGroundY + .3f, 0f);
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(Arena, new object[] { .5f, .1f });
                Arena.BeginSlot(Attack, Attack);
            }
            public void MaskPlayerOnly() => MaskActorOnly(true);
            public void MaskActorOnly(bool player)
            {
                foreach (Transform child in host.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
                (player ? Arena.PlayerRenderer : Arena.EnemyRenderer).gameObject.layer = CaptureLayer;
                Camera camera = Arena.ArenaCamera;
                camera.transform.localPosition = new Vector3(0f, -1.5f, -10f);
                camera.transform.localRotation = Quaternion.identity;
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.aspect = Width / (float)Height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << CaptureLayer;
                camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << CaptureLayer;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            }
            public Color32[] ReadPixels()
            {
                RenderTexture previous = RenderTexture.active;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                try
                {
                    Arena.ArenaCamera.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    texture.Apply();
                    return texture.GetPixels32();
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }
            public void Save(Color32[] pixels, string path)
            {
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
                finally { Object.Destroy(texture); }
            }
            public void Dispose()
            {
                Arena.ArenaCamera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Arena.Dispose();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(host);
            }
        }
        private sealed class CountingZero : System.Random
        {
            public int Calls { get; private set; }
            public override int Next(int maxValue) { Calls++; return 0; }
        }
    }
}
