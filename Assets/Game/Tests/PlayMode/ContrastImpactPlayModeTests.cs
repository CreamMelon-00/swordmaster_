using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    public sealed class ContrastImpactPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly int AttackOffset = Shader.PropertyToID("_AttackOffset");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int Sentinel = Shader.PropertyToID("_ContrastTestSentinel");

        [UnityTest]
        public IEnumerator EveryDecidedOutcomePlaysOncePerBattle_WithoutDependingOnTheAttackOrRhythmSetting()
        {
            RequireShader();
            using (var scope = new ArenaScope())
            {
                foreach (DuelMatchOutcome outcome in DecidedOutcomes())
                foreach (bool withSlot in new[] { false, true })
                {
                    scope.Begin(false, withSlot, LegacySkillDefinitions.Skill(1));
                    foreach (DuelMatchOutcome invalid in new[] { DuelMatchOutcome.InProgress,
                        (DuelMatchOutcome)(-1), (DuelMatchOutcome)int.MaxValue })
                        Assert.That(scope.Arena.PresentBattleEndImpact(invalid), Is.False);
                    AssertQuiet(scope);
                    Assert.That(scope.Arena.PresentBattleEndImpact(outcome), Is.True,
                        "A decided battle plays its frame even without a slot or attack emphasis.");
                    Assert.That(scope.Arena.ContrastImpactActive, Is.True);
                    scope.Arena.ClearContrastImpact();
                    AssertQuiet(scope);
                    Assert.That(scope.Arena.PresentBattleEndImpact(outcome), Is.False,
                        "Clearing the picture does not turn one result into a new battle.");
                    scope.Arena.BeginTurn();
                    Assert.That(scope.Arena.PresentBattleEndImpact(outcome), Is.False,
                        "Starting another turn also cannot rearm the battle's finishing frame.");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrdinaryHitsAndResistanceBreakPresentation_DoNotStartTheBattleEndFrame()
        {
            RequireShader();
            using (var scope = new ArenaScope())
            {
                scope.Begin(true);
                scope.Arena.PresentHit(true, 0, 30, false, true);
                AssertQuiet(scope);
                scope.Arena.PresentHit(false, 40, 0, false, true);
                AssertQuiet(scope);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.EnemyVictory), Is.True,
                    "The battle result, rather than an ordinary decisive hit, selects this frame.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FlatCopiesMatchTheLivePosesAndDrawOffsets_WithoutChangingTheOriginalRenderers()
        {
            RequireShader();
            using (var scope = new ArenaScope())
            {
                scope.Begin(true);
                SpriteRenderer player = scope.Arena.PlayerRenderer, enemy = scope.Arena.EnemyRenderer;
                player.flipX = true;
                enemy.flipY = true;
                player.color = new Color(.2f, .4f, .8f, .7f);
                enemy.color = new Color(.8f, .3f, .1f, .9f);
                var block = new MaterialPropertyBlock();
                player.GetPropertyBlock(block);
                block.SetFloat(Sentinel, 37f);
                player.SetPropertyBlock(block);
                var originalPlayer = new RendererSnapshot(player);
                var originalEnemy = new RendererSnapshot(enemy);
                var grade = new GradeSnapshot(scope.Arena.ArenaProfile);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.PlayerVictory), Is.True);

                Transform pulse = scope.Pulse;
                Assert.That(pulse, Is.Not.Null);
                Assert.That(pulse.GetComponent<SortingGroup>().sortingOrder, Is.EqualTo(32000));
                Assert.That(pulse.childCount, Is.EqualTo(5), "One backdrop and four reusable body layers.");
                SpriteRenderer playerCopy = pulse.Find("Player Silhouette").GetComponent<SpriteRenderer>();
                SpriteRenderer enemyCopy = pulse.Find("Enemy Silhouette").GetComponent<SpriteRenderer>();
                AssertCopy(scope.Arena, player, playerCopy);
                AssertCopy(scope.Arena, enemy, enemyCopy);
                Assert.That(pulse.Find("Player Lower Silhouette"), Is.Not.Null);
                Assert.That(pulse.Find("Enemy Lower Silhouette"), Is.Not.Null);
                SpriteRenderer backdrop = pulse.Find("Impact Backdrop").GetComponent<SpriteRenderer>();
                Assert.That(backdrop.enabled && backdrop.gameObject.activeInHierarchy, Is.True);
                Color paper = InkOf(playerCopy), background = InkOf(backdrop);
                Assert.That(InkOf(enemyCopy), Is.EqualTo(paper), "Both bodies use the same flat paper tint, independent of their source colors.");
                Assert.That(paper.a, Is.GreaterThan(.9f));
                Assert.That(Mathf.Abs(paper.grayscale - background.grayscale), Is.GreaterThan(.6f));
                originalPlayer.AssertUnchanged(player);
                originalEnemy.AssertUnchanged(enemy);

                scope.Arena.AdvanceContrastImpact(.2f);
                Assert.That(scope.Arena.ContrastImpactActive, Is.True, "A large tick in the triggering frame cannot consume the only rendered impact frame.");
                yield return null;
                foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    scope.Arena.AdvanceContrastImpact(invalid);
                Assert.That(scope.Arena.ContrastImpactActive, Is.True);
                scope.Arena.AdvanceContrastImpact(DuelContrastImpact.Duration + .2f);
                AssertQuiet(scope);
                grade.AssertRestored(scope.Arena.ArenaProfile);
                originalPlayer.AssertUnchanged(player);
                originalEnemy.AssertUnchanged(enemy);
            }
        }

        [UnityTest]
        public IEnumerator NewBattlesReuseTheirNodes_AndTurnResetCutsceneAndDisposeCannotLeaveAnOverlay()
        {
            RequireShader();
            using (var scope = new ArenaScope())
            {
                scope.Begin(true);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.PlayerVictory), Is.True);
                Transform pulse = scope.Pulse;
                int nodes = scope.Root.GetComponentsInChildren<Transform>(true).Length;
                for (int repeat = 0; repeat < 20; repeat++)
                {
                    scope.Begin(false, false);
                    Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.EnemyVictory), Is.True);
                    Assert.That(scope.Pulse, Is.SameAs(pulse));
                    Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(nodes));
                }
                scope.Arena.BeginTurn();
                AssertQuiet(scope);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.EnemyVictory), Is.False);
                scope.Begin(false, false);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.Draw), Is.True);
                scope.Arena.Reset();
                AssertQuiet(scope);
                typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(scope.Arena, null);
                AssertQuiet(scope);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.PlayerVictory), Is.False);
                typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(scope.Arena, null);
                AssertQuiet(scope);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.PlayerVictory), Is.True,
                    "A rejected attempt during a cutscene does not consume the new battle's frame.");
                typeof(LegacyArenaView).GetMethod("SuspendForCutscene", Private).Invoke(scope.Arena, null);
                AssertQuiet(scope);
                typeof(LegacyArenaView).GetMethod("ResumeAfterCutscene", Private).Invoke(scope.Arena, null);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.PlayerVictory), Is.False,
                    "A frame already shown before a cutscene cannot play again on resume.");
                scope.Begin(false, false);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.Draw), Is.True);
                scope.DisposeArena();
                Assert.That(scope.Arena.ContrastImpactActive, Is.False);
                Assert.That(scope.Arena.PresentBattleEndImpact(DuelMatchOutcome.Draw), Is.False);
                scope.Arena.AdvanceContrastImpact(.2f);
                scope.Arena.ClearContrastImpact();
                yield return null;
                Assert.That(pulse == null, Is.True, "Disposal destroys the reusable overlay, rather than abandoning a sorting group.");
            }
        }

        [UnityTest]
        public IEnumerator TheSharedFinaleStartsEveryBattleEndFrame_AndKeepsItsExistingClockAndFocus()
        {
            RequireShader();
            using (var scope = new ArenaScope())
            using (var finale = new DuelFinale(scope.Root.transform, scope.Arena))
            {
                scope.Begin(false, false);
                foreach (DuelMatchOutcome invalid in new[] { DuelMatchOutcome.InProgress,
                    (DuelMatchOutcome)(-1), (DuelMatchOutcome)int.MaxValue })
                {
                    finale.Begin(scope.Settings, invalid, false);
                    AssertQuiet(scope);
                    Assert.That(finale.Timeline.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
                    Assert.That(scope.Arena.IsFinishingFocus, Is.False);
                }

                float globalSpeed = Time.timeScale;
                foreach (DuelMatchOutcome outcome in DecidedOutcomes())
                {
                    scope.Begin(false, false);
                    bool fall = outcome == DuelMatchOutcome.EnemyVictory;
                    var expected = new LegacyFinishingBlow();
                    expected.Begin(scope.Settings.FinishingSlowMotionSeconds, scope.Settings.FinishingSlowMotionScale,
                        scope.Settings.FinishingBarSeconds, scope.Settings.FinishingFreezeSeconds,
                        fall ? scope.Settings.FinalFallGreySeconds : 0f);
                    finale.Begin(scope.Settings, outcome, fall, .08f);
                    Assert.That(scope.Arena.ContrastImpactActive, Is.True,
                        "Both controller paths use this finale, so the winner and chosen technique need no special case.");
                    Assert.That(scope.Arena.IsFinishingFocus, Is.True);
                    AssertTimeline(finale, expected);
                    float delta = scope.Settings.FinishingSlowMotionSeconds * .25f;
                    finale.Tick(delta);
                    expected.Advance(delta);
                    AssertTimeline(finale, expected);
                    Assert.That(scope.Arena.IsFinishingFocus, Is.True);
                    Assert.That(Time.timeScale, Is.EqualTo(globalSpeed));
                    finale.Clear();
                    AssertQuiet(scope);
                    Assert.That(finale.Timeline.Current, Is.EqualTo(LegacyFinishingBlow.Stage.None));
                    Assert.That(finale.CombatSpeed, Is.EqualTo(1f));
                    Assert.That(finale.Letterbox.IsVisible, Is.False);
                    Assert.That(scope.Arena.PresentBattleEndImpact(outcome), Is.False,
                        "Leaving the finale clears its frame without replaying the same result.");
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingSilhouetteAssetsNeverStartAnOverlay()
        {
            var root = new GameObject("Unsupported Contrast Impact Test");
            try
            {
                using (var impact = new DuelContrastImpact(root.transform, null, 0))
                {
                    Assert.That(impact.HasRequiredAssets, Is.False);
                    Assert.That(impact.Show(), Is.False);
                    Assert.That(impact.Active, Is.False);
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                        Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy, Is.False);
                }
            }
            finally { Object.Destroy(root); }
            yield return null;
        }

        private static DuelMatchOutcome[] DecidedOutcomes() => new[]
        {
            DuelMatchOutcome.PlayerVictory, DuelMatchOutcome.EnemyVictory, DuelMatchOutcome.Draw,
        };

        private static void AssertTimeline(DuelFinale actual, LegacyFinishingBlow expected)
        {
            Assert.That(actual.Timeline.Current, Is.EqualTo(expected.Current));
            Assert.That(actual.Timeline.StageElapsed, Is.EqualTo(expected.StageElapsed).Within(.0001f));
            Assert.That(actual.CombatSpeed, Is.EqualTo(expected.CombatSpeed).Within(.0001f));
            Assert.That(actual.Letterbox.Amount, Is.EqualTo(expected.BarsAmount).Within(.0001f));
        }

        private static void RequireShader()
        {
            Shader shader = Resources.Load<Shader>("DuelVFX/BreakSilhouette");
            if (shader == null || !shader.isSupported)
                Assert.Ignore("The native break silhouette shader is unavailable on this graphics device.");
        }

        private static void AssertQuiet(ArenaScope scope)
        {
            Assert.That(scope.Arena.ContrastImpactActive, Is.False);
            Transform pulse = scope.Pulse;
            if (pulse == null) return;
            foreach (Renderer renderer in pulse.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy, Is.False);
        }

        private static void AssertCopy(LegacyArenaView arena, SpriteRenderer source, SpriteRenderer copy)
        {
            Assert.That(copy.enabled && copy.gameObject.activeInHierarchy, Is.True);
            Assert.That(copy.sprite, Is.SameAs(source.sprite));
            Assert.That(copy.flipX || copy.flipY, Is.False, "The flat mesh shader receives the sprite flip through geometry scale.");
            Assert.That(copy.sharedMaterial, Is.Not.SameAs(source.sharedMaterial));
            Assert.That(copy.sharedMaterial.shader, Is.SameAs(Resources.Load<Shader>("DuelVFX/BreakSilhouette")));
            Assert.That(copy.color, Is.EqualTo(Color.white), "The flat tint is independent of ordinary damage flashes.");
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            Vector4 offset = properties.GetVector(AttackOffset);
            Vector3 drawn = source.transform.TransformPoint(new Vector3(offset.x, offset.y, offset.z));
            Vector3 expected = arena.ArenaCamera.WorldToViewportPoint(drawn);
            Vector3 actual = arena.ArenaCamera.WorldToViewportPoint(copy.transform.position);
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(.0001f));
            Vector3 expectedScale = source.transform.lossyScale;
            expectedScale.x *= source.flipX ? -1f : 1f;
            expectedScale.y *= source.flipY ? -1f : 1f;
            Assert.That(Vector3.Distance(copy.transform.lossyScale, expectedScale), Is.LessThan(.0001f));
        }

        private static Color InkOf(SpriteRenderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(Tint);
        }

        private readonly struct RendererSnapshot
        {
            private readonly Sprite sprite;
            private readonly Material material;
            private readonly Color color;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            private readonly bool enabled, flipX, flipY;
            private readonly int order, layer;
            private readonly Vector4 attackOffset;
            private readonly float sentinel;

            public RendererSnapshot(SpriteRenderer renderer)
            {
                sprite = renderer.sprite; material = renderer.sharedMaterial; color = renderer.color;
                position = renderer.transform.position; scale = renderer.transform.lossyScale; rotation = renderer.transform.rotation;
                enabled = renderer.enabled; flipX = renderer.flipX; flipY = renderer.flipY;
                order = renderer.sortingOrder; layer = renderer.sortingLayerID;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                attackOffset = block.GetVector(AttackOffset); sentinel = block.GetFloat(Sentinel);
            }

            public void AssertUnchanged(SpriteRenderer renderer)
            {
                Assert.That(renderer.sprite, Is.SameAs(sprite));
                Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(renderer.color, Is.EqualTo(color));
                Assert.That(renderer.transform.position, Is.EqualTo(position));
                Assert.That(renderer.transform.lossyScale, Is.EqualTo(scale));
                Assert.That(renderer.transform.rotation, Is.EqualTo(rotation));
                Assert.That(renderer.enabled, Is.EqualTo(enabled));
                Assert.That(renderer.flipX, Is.EqualTo(flipX)); Assert.That(renderer.flipY, Is.EqualTo(flipY));
                Assert.That(renderer.sortingOrder, Is.EqualTo(order)); Assert.That(renderer.sortingLayerID, Is.EqualTo(layer));
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetVector(AttackOffset), Is.EqualTo(attackOffset));
                Assert.That(block.GetFloat(Sentinel), Is.EqualTo(sentinel));
            }
        }

        private readonly struct GradeSnapshot
        {
            private readonly float exposure, contrast, saturation, bloom;
            private readonly Color filter;
            private readonly bool bloomActive, aberrationActive;

            public GradeSnapshot(VolumeProfile profile)
            {
                Assert.That(profile.TryGet(out ColorAdjustments grade), Is.True);
                Assert.That(profile.TryGet(out Bloom light), Is.True);
                Assert.That(profile.TryGet(out ChromaticAberration aberration), Is.True);
                exposure = grade.postExposure.value; contrast = grade.contrast.value; saturation = grade.saturation.value;
                filter = grade.colorFilter.value; bloom = light.intensity.value;
                bloomActive = light.active; aberrationActive = aberration.active;
            }

            public void AssertRestored(VolumeProfile profile)
            {
                profile.TryGet(out ColorAdjustments grade); profile.TryGet(out Bloom light);
                profile.TryGet(out ChromaticAberration aberration);
                Assert.That(grade.postExposure.value, Is.EqualTo(exposure).Within(.0001f));
                Assert.That(grade.contrast.value, Is.EqualTo(contrast).Within(.0001f));
                Assert.That(grade.saturation.value, Is.EqualTo(saturation).Within(.0001f));
                Assert.That(grade.colorFilter.value, Is.EqualTo(filter));
                Assert.That(light.intensity.value, Is.EqualTo(bloom).Within(.0001f));
                Assert.That(light.active, Is.EqualTo(bloomActive));
                Assert.That(aberration.active, Is.EqualTo(aberrationActive));
            }
        }

        private sealed class ArenaScope : IDisposable
        {
            public readonly GameObject Root = new GameObject("Contrast Impact Arena Test");
            private readonly DuelPresentationSettings settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
            private readonly LegacyDuelArt art = new LegacyDuelArt();
            private bool arenaDisposed;
            public LegacyArenaView Arena { get; }
            public DuelPresentationSettings Settings => settings;
            public Transform Pulse
            {
                get
                {
                    foreach (Transform node in Root.GetComponentsInChildren<Transform>(true))
                        if (node.name == "Duel Contrast Impact") return node;
                    return null;
                }
            }

            public ArenaScope()
            {
                JsonUtility.FromJsonOverwrite("{\"attackRhythmStrength\":1,\"attackLungeDistance\":0.6}", settings);
                Arena = LegacyArenaView.Create(Root.transform, art, settings);
            }

            public void Begin(bool rhythm, bool withSlot = true, LegacySkill skill = null)
            {
                Arena.Reset();
                Arena.SetAttackRhythmOverride(rhythm);
                typeof(LegacyArenaView).GetMethod("ConfigureSlotTiming", Private).Invoke(Arena, new object[] { 1f, .12f });
                if (withSlot)
                {
                    skill = skill ?? LegacySkillDefinitions.Skill(3);
                    Arena.BeginSlot(skill, skill);
                    typeof(LegacyArenaView).GetMethod("HoldSlotAtTime", Private)
                        .Invoke(Arena, new object[] { LegacyArenaView.OriginalClipDuration * .5f });
                }
            }

            public void DisposeArena()
            {
                if (arenaDisposed) return;
                Arena.Dispose();
                arenaDisposed = true;
            }

            public void Dispose()
            {
                DisposeArena();
                art.Dispose();
                Object.Destroy(settings);
                Object.Destroy(Root);
            }
        }
    }
}
