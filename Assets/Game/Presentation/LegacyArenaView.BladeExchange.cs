using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        /// <summary>Overlays a forward-moving attack pose and a small blade deflection for this rendered frame.
        /// The caller owns the cosmetic time and recoil envelope. Neither fighter's natural animation clock,
        /// ground movement nor pending hit reaction is changed; the next Tick samples its natural pose again.</summary>
        public void PresentBladeExchangePose(float visualAttackTime, float recoilShare)
        {
            if (disposed || suspension != null || !resolving || approaching || returning ||
                float.IsNaN(visualAttackTime) || float.IsInfinity(visualAttackTime)) return;
            float recoil = float.IsNaN(recoilShare) || float.IsInfinity(recoilShare) ? 0f : Mathf.Clamp01(recoilShare);
            PresentBladeExchangePose(player, Mathf.Max(0f, visualAttackTime), recoil);
            PresentBladeExchangePose(enemy, Mathf.Max(0f, visualAttackTime), recoil);
        }

        private void PresentBladeExchangePose(Actor actor, float visualAttackTime, float recoilShare)
        {
            if (actor.Defeated) return;
            float animationTime = actor.AnimationTime;
            Vector3 recoilOffset = actor.AttackRecoilOffset;
            try
            {
                actor.AnimationTime = visualAttackTime;
                SampleActor(actor);
                Vector3 deflection = arenaRoot.transform.TransformDirection(
                    new Vector3(actor == player ? -.10f : .10f, .02f, 0f)) * recoilShare;
                SetAttackDrawOffset(actor, actor.AttackVisualOffset + deflection);
            }
            finally
            {
                actor.AnimationTime = animationTime;
                actor.AttackRecoilOffset = recoilOffset;
            }
        }

        /// <summary>A light, cool contact between the blades before the real strike. Only the pooled glow is emitted:
        /// no hit identity, weapon trail, body reaction, displacement, camera impulse or gameplay is consumed.</summary>
        public void PresentBladeExchangeContact(float strength)
        {
            if (disposed || suspension != null || !resolving || approaching || returning) return;
            impactGlow.Emit(BladeBindContactPosition, true, false, strength);
        }
    }
}
