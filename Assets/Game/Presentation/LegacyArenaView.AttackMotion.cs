using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const int AttackEchoCapacity = 8;
        private const float AttackEchoLifetime = .10f;
        private static readonly int AttackOffsetId = Shader.PropertyToID("_AttackOffset");
        private static readonly Color AttackEchoInk = new Color(.95f, .86f, .70f, .26f);
        private Material attackSpriteMaterial;
        private DuelStepAfterimages attackEchoes;
        private float slotAttackLungeDistance;

        /// <summary>World-space displacement of the drawn body only; combat transforms and range stay authoritative.</summary>
        public Vector3 PlayerAttackVisualOffset => player.AttackVisualOffset;
        public Vector3 EnemyAttackVisualOffset => enemy.AttackVisualOffset;
        public int ActiveAttackEchoCount => attackEchoes?.ActiveCount ?? 0;
        public int AttackEchoPoolSize => attackEchoes?.PoolSize ?? 0;

        private void InitializeAttackMotionMaterial()
        {
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline ?? QualitySettings.renderPipeline ??
                UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            if (!(pipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset)) return;
            Shader shader = Resources.Load<Shader>("DuelVFX/AttackSprite");
            if (shader != null && shader.isSupported)
                attackSpriteMaterial = new Material(shader) { name = "Duel Attack Sprites (Runtime)" };
        }

        private void InitializeAttackMotion()
        {
            if (attackSpriteMaterial != null)
            {
                player.Renderer.sharedMaterial = enemy.Renderer.sharedMaterial = attackSpriteMaterial;
            }
            attackEchoes = new DuelStepAfterimages(arenaRoot.transform, spriteMaterial, ArenaLayer, false,
                AttackEchoCapacity, "Strike");
        }

        private void ApplyAttackMotion(Actor actor)
        {
            Vector3 offset = Vector3.zero;
            if (suspension == null && attackSpriteMaterial != null && slotAttackLungeDistance > 0f &&
                resolving && !approaching && !returning && !(actor == enemy && EnemyIsDummy))
            {
                if (actor.ReactionTime > 0f)
                    offset = actor.AttackRecoilOffset * Mathf.Clamp01(actor.ReactionTime / ReactionPoseDuration);
                else if (actor.Skill?.Kind == LegacySkillKind.Attack && TryGetSkillFrame(actor, out float clipTime))
                {
                    float distance = LegacyAttackRhythm.LungeShare(clipTime / OriginalClipDuration) * slotAttackLungeDistance;
                    offset = arenaRoot.transform.TransformVector(Vector3.right * (actor == player ? distance : -distance));
                    actor.AttackRecoilOffset = offset;
                }
                else actor.AttackRecoilOffset = Vector3.zero;
            }
            SetAttackDrawOffset(actor, offset);
        }

        private void SetAttackDrawOffset(Actor actor, Vector3 worldOffset)
        {
            actor.AttackVisualOffset = worldOffset;
            Vector3 local = actor.Renderer.transform.InverseTransformVector(worldOffset);
            actor.Renderer.GetPropertyBlock(actor.AttackDrawProperties);
            actor.AttackDrawProperties.SetVector(AttackOffsetId, new Vector4(local.x, local.y, local.z, 0f));
            actor.Renderer.SetPropertyBlock(actor.AttackDrawProperties);
            // Shader displacement is outside SpriteRenderer's generated bounds. Expand only x for camera culling.
            if (actor.Renderer.sprite != null && attackSpriteMaterial != null)
            {
                Bounds bounds = actor.Renderer.sprite.bounds;
                Vector3 center = bounds.center;
                if (actor.Renderer.flipX) center.x = -center.x;
                if (actor.Renderer.flipY) center.y = -center.y;
                bounds.center = center;
                float scale = Mathf.Max(.01f, Mathf.Abs(actor.Renderer.transform.lossyScale.x));
                bounds.Expand(new Vector3(2f * Mathf.Max(slotAttackLungeDistance, 1f) / scale, 0f, 0f));
                actor.Renderer.localBounds = bounds;
            }
            DuelPowerAura aura = actor == player ? playerPowerAura : enemyPowerAura;
            if (aura != null) aura.Root.localPosition = local;
            (actor == player ? playerBreakAura : enemyBreakAura)?.RefreshPose();
        }

        // Called after the controller's impact/hold, so a long frame never leaves ghosts of an overshot pose.
        internal void TickAttackMotionTrail(float realDelta, bool animationPlaying)
        {
            TickWeaponTrail(realDelta, animationPlaying);
            if (disposed || !animationPlaying || !(realDelta > 0f) || float.IsInfinity(realDelta) ||
                slotAttackLungeDistance <= 0f || attackSpriteMaterial == null || suspension != null) return;
            EmitAttackEcho(player);
            EmitAttackEcho(enemy);
        }

        private void EmitAttackEcho(Actor actor)
        {
            if (!resolving || approaching || returning || actor.ReactionTime > 0f ||
                actor == enemy && EnemyIsDummy || actor.Skill?.Kind != LegacySkillKind.Attack ||
                !TryGetSkillFrame(actor, out float clipTime)) return;
            float phase = clipTime / OriginalClipDuration;
            // Roundoff at a manually held or multiplied clip boundary must still count as that stroke moment.
            int due = (phase + .00001f >= .40f ? 1 : 0) | (phase + .00001f >= .48f ? 2 : 0);
            int hit = Mathf.FloorToInt(actor.AnimationTime / (OriginalClipDuration / slotAnimationSpeed + slotAttackInterval));
            actor.AttackEchoMasks.TryGetValue(hit, out int shown);
            if ((due & ~shown) == 0) return;
            actor.AttackEchoMasks[hit] = shown | due;
            // Two discrete stroke moments; holds and rewinds cannot keep spawning the same ghost.
            Vector3 drift = arenaRoot.transform.TransformVector(Vector3.right * (actor == player ? -.20f : .20f));
            Color ink = AttackEchoInk;
            ink.a *= slotAttackRhythmStrength;
            attackEchoes.Emit(actor.Renderer, ink, AttackEchoLifetime, drift, 0f, 0f,
                actor.Renderer.sortingOrder - 1);
        }

        private void ClearAttackMotion(bool forgetHits = true)
        {
            attackEchoes?.Reset();
            weaponTrails?.Reset();
            ClearAttackMotion(player, forgetHits);
            ClearAttackMotion(enemy, forgetHits);
        }

        private void ClearAttackMotion(Actor actor, bool forgetHits)
        {
            if (actor == null) return;
            actor.AttackRecoilOffset = Vector3.zero;
            if (forgetHits) actor.AttackEchoMasks.Clear();
            if (forgetHits) actor.WeaponTrailHits.Clear();
            SetAttackDrawOffset(actor, Vector3.zero);
        }

        private void DisposeAttackMotion()
        {
            attackEchoes?.Dispose();
            if (attackSpriteMaterial != null) Object.Destroy(attackSpriteMaterial);
        }
    }
}
