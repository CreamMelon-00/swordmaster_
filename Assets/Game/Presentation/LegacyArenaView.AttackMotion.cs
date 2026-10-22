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
        private bool playerGuardsSlot, enemyGuardsSlot;
        private bool slotRequiresContact = true;

        /// <summary>World-space displacement of the drawn body only; combat transforms and range stay authoritative.</summary>
        public Vector3 PlayerAttackVisualOffset => player.AttackVisualOffset;
        public Vector3 EnemyAttackVisualOffset => enemy.AttackVisualOffset;
        public int ActiveAttackEchoCount => attackEchoes?.ActiveCount ?? 0;
        public int AttackEchoPoolSize => attackEchoes?.PoolSize ?? 0;

        /// <summary>A slot without attacks can finish its stances where the fighters stand.</summary>
        public bool SlotRequiresContact => slotRequiresContact;
        public bool IsReadyForSlot => !SlotRequiresContact || IsInRange;

        /// <summary>Prepares travel before the next slot starts, without advancing its skills or animation.</summary>
        public void PrepareSlotMovement(LegacySkill playerSkill, LegacySkill enemySkill)
        {
            playerGuardsSlot = playerSkill?.Kind == LegacySkillKind.Defence;
            enemyGuardsSlot = enemySkill?.Kind == LegacySkillKind.Defence;
            slotRequiresContact = playerSkill?.Kind == LegacySkillKind.Attack || enemySkill?.Kind == LegacySkillKind.Attack;
            player.Chasing = enemy.Chasing = false;
        }

        public void PrepareSlotMovement(ILegacyDuelView duel, int slotIndex)
        {
            if (duel == null) throw new System.ArgumentNullException(nameof(duel));
            if (slotIndex < 0) throw new System.ArgumentOutOfRangeException(nameof(slotIndex));
            bool playerCanceled = duel.PlayerBreakCanceledFromSlotIndex >= 0 &&
                slotIndex >= duel.PlayerBreakCanceledFromSlotIndex;
            bool enemyCanceled = duel is ILegacyEnemyRosterView roster && roster.Enemies.Count > 1
                ? roster.IsEnemySlotCanceled(slotIndex)
                : duel.EnemyBreakCanceledFromSlotIndex >= 0 && slotIndex >= duel.EnemyBreakCanceledFromSlotIndex;
            LegacySkill playerSkill = !playerCanceled && slotIndex < duel.PlayerQueue.Count ? duel.PlayerQueue[slotIndex] : null;
            LegacySkill enemySkill = !enemyCanceled && slotIndex < duel.EnemyQueue.Count ? duel.EnemyQueue[slotIndex] : null;
            PrepareSlotMovement(playerSkill, enemySkill);
        }

        private void ClearSlotMovement()
        {
            playerGuardsSlot = enemyGuardsSlot = false;
            slotRequiresContact = true;
        }

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
                    float distance = LegacyAttackRhythm.LungeShare(clipTime / OriginalClipDuration) * AttackLungeDistanceFor(actor);
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
                float maximumOffset = Mathf.Max(AttackLungeDistanceFor(actor), Mathf.Abs(worldOffset.x));
                bounds.Expand(new Vector3(2f * Mathf.Max(maximumOffset, 1f) / scale, 0f, 0f));
                actor.Renderer.localBounds = bounds;
            }
            EnemyFigure figure = actor == player ? null : FigureFor(actor);
            DuelPowerAura aura = actor == player ? playerPowerAura : figure?.PowerAura ?? enemyPowerAura;
            if (aura != null) aura.Root.localPosition = local;
            (actor == player ? playerBreakAura : figure?.BreakAura ?? enemyBreakAura)?.RefreshPose();
        }

        // Called after the controller's impact/hold, so a long frame never leaves ghosts of an overshot pose.
        internal void TickAttackMotionTrail(float realDelta, bool animationPlaying)
        {
            if (IsResistanceImpactPlaying) return;
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
            // Consume the stroke moments without layering extra player silhouettes over the cinematic trail.
            if (actor == player && IsCinematicSlowMotion) return;
            // Two discrete stroke moments; holds and rewinds cannot keep spawning the same ghost.
            Color ink = AttackEchoInk;
            ink.a *= slotAttackRhythmStrength;
            attackEchoes.Emit(actor.Renderer, ink, AttackEchoLifetime, actor.Renderer.sortingOrder - 1);
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
