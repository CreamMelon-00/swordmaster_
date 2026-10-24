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
                string key = FinalPlayerAnimationSet.AttackKey(actor.Skill.Property, actor.Skill.Id);
                // These two clips already contain their authored trail. Do not stack the old mesh effect over it.
                if (key == "slash" || key == "pierce") return;
                Vector4 blade = key == "pierce" ? new Vector4(1.61f, .05f, 3.61f, .05f)
                    : key == "slash" ? new Vector4(.86f, -.84f, 2.78f, -1.62f)
                    : key == "loaded" ? new Vector4(.47f, -1.06f, 1.5f, -2.23f)
                    : key == "measured" ? new Vector4(.72f, -.42f, 2.28f, .99f)
                    : key == "evade" ? new Vector4(.72f, -.15f, 1.72f, 1.8f)
                    : new Vector4(.58f, -.48f, 1.94f, .99f);
                var visualProperty = key == "pierce" ? LegacySkillProperty.Penetrate
                    : key == "measured" ? LegacySkillProperty.Hit : LegacySkillProperty.Slash;
                weaponTrails.Emit(actor.Renderer.transform.position + visibleOffset, true,
                    visualProperty, 0, false, slotWeaponTrailStrength, slotWeaponTrailSeconds, blade);
                return;
            }
            weaponTrails.Emit(actor.Renderer.transform.position + visibleOffset, actor == player,
                actor.Skill.Property, AttackVariant(actor, hit), actor == enemy && !EnemyIsElisa,
                slotWeaponTrailStrength, slotWeaponTrailSeconds);
        }
    }
}
