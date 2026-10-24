using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private DuelWeaponTrail weaponTrails;
        private bool? weaponTrailOverride;
        private float slotWeaponTrailStrength, slotWeaponTrailSeconds;

        public Transform WeaponTrailRoot => weaponTrails?.Root;
        public int ActiveWeaponTrailCount => weaponTrails?.ActiveCount ?? 0;
        public int WeaponTrailPoolSize => weaponTrails?.PoolSize ?? 0;
        public int WeaponTrailEmissionCount => weaponTrails?.TotalEmissionCount ?? 0;

        /// <summary>Applies to the next slot in this arena only; null restores the shared asset's setting.</summary>
        public void SetWeaponTrailOverride(bool? enabled) => weaponTrailOverride = enabled;

        private void InitializeWeaponTrail() => weaponTrails = new DuelWeaponTrail(arenaRoot.transform, ArenaLayer);

        private void ConfigureWeaponTrail()
        {
            slotWeaponTrailStrength = (weaponTrailOverride ?? settings.WeaponTrailEnabled)
                ? settings.WeaponTrailStrength : 0f;
            slotWeaponTrailSeconds = settings.WeaponTrailSeconds;
        }

        private void TickWeaponTrail(float realDelta, bool animationPlaying)
        {
            if (disposed || suspension != null || !animationPlaying || !(realDelta > 0f) ||
                float.IsInfinity(realDelta) || slotWeaponTrailStrength <= 0f) return;
            EmitWeaponTrail(player);
            EmitWeaponTrail(enemy);
        }

        private void EmitWeaponTrail(Actor actor)
        {
            if (!resolving || approaching || returning || actor.ReactionTime > 0f ||
                actor == enemy && EnemyIsDummy || actor.Skill?.Kind != LegacySkillKind.Attack ||
                !TryGetSkillFrame(actor, out float clipTime) || clipTime / OriginalClipDuration + .00001f < .44f) return;
            int hit = Mathf.FloorToInt(actor.AnimationTime / (OriginalClipDuration / slotAnimationSpeed + slotAttackInterval));
            EmitWeaponTrail(actor, hit, actor.AttackVisualOffset);
        }

        // A fast frame may cross the entire lead-in before the next trail tick. Capture the actual strike before
        // mutual hurt/block poses replace the weapons; the same hit mask prevents a second light at bind release.
        internal void PresentWeaponTrailContact(int hit, bool playerAttacked, bool enemyAttacked)
        {
            if (disposed || suspension != null || slotWeaponTrailStrength <= 0f || !resolving) return;
            if (playerAttacked) EmitWeaponTrailContact(player, hit);
            if (enemyAttacked && !EnemyIsDummy) EmitWeaponTrailContact(enemy, hit);
        }

        private void EmitWeaponTrailContact(Actor actor, int hit)
        {
            if (actor.Skill?.Kind != LegacySkillKind.Attack) return;
            Vector3 offset = attackSpriteMaterial == null ? Vector3.zero : arenaRoot.transform.TransformVector(
                Vector3.right * (actor == player ? AttackLungeDistanceFor(actor) : -AttackLungeDistanceFor(actor)));
            EmitWeaponTrail(actor, hit, offset);
        }

        private void EmitWeaponTrail(Actor actor, int hit, Vector3 visibleOffset)
        {
            // Raw hit identity survives contact holds, rewinds and cutscene restoration. Drawing never rolls new RNG.
            if (!actor.WeaponTrailHits.Add(hit)) return;
            if (actor == player && UseFinalPlayerArt && finalPlayerAnimations.IsReady)
            {
                // Blade sweeps are now authored in the actual player cels. Measured guard deliberately has no
                // big swing. Never substitute the old slash mesh or stack it over the authored trail.
                return;
            }
            weaponTrails.Emit(actor.Renderer.transform.position + visibleOffset, actor == player,
                actor.Skill.Property, AttackVariant(actor, hit), actor == enemy && !EnemyIsElisa,
                slotWeaponTrailStrength, slotWeaponTrailSeconds);
        }
    }
}
