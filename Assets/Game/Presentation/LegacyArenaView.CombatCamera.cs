using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private bool combatShotRequested, combatShotSeen;
        private bool combatShotPlayerAttacks, combatShotUnopposed;
        private float combatShotProgress, combatShotAmount, combatLetterboxAccent;

        /// <summary>The current exchange's camera engagement, eased on real time and multiplied by its authoring strength.</summary>
        public float CombatShotAmount => combatShotAmount;
        /// <summary>The existing battle letterbox's accent, 0..1. It never creates another overlay or advances combat.</summary>
        public float CombatLetterboxAccent => combatLetterboxAccent;

        /// <summary>Request a camera gesture for this frame's whole skill. Preparation occupies 0..0.2, then the
        /// strike and recovery travel toward 1. Tick consumes the request; omitting it lets the shot settle.</summary>
        public void SetCombatShot(float progress, bool playerAttacks, bool enemyAttacks, bool unopposed)
        {
            if (disposed || suspension != null) return;
            combatShotRequested = playerAttacks || enemyAttacks;
            if (!combatShotRequested) return;
            combatShotProgress = float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : Mathf.Clamp01(progress);
            combatShotPlayerAttacks = playerAttacks;
            combatShotUnopposed = unopposed && playerAttacks != enemyAttacks;
            combatShotSeen = true;
        }

        private void ClearCombatShot()
        {
            combatShotRequested = combatShotSeen = false;
            combatShotPlayerAttacks = combatShotUnopposed = false;
            combatShotProgress = combatShotAmount = combatLetterboxAccent = 0f;
        }

        private float CombatShotMotionWeight => IsFatalFocus || cutInAmount > .001f || StepFocusAmount > .001f ||
            suspension != null ? 0f : combatShotAmount;

        private void TickCombatShot(float realDelta)
        {
            bool requested = combatShotRequested;
            combatShotRequested = false;
            if (!resolving || returning || suspension != null || !settings.CombatCameraMotionEnabled ||
                settings.CombatCameraMotionStrength <= 0f)
            {
                ClearCombatShot();
                return;
            }
            float strength = settings.CombatCameraMotionStrength;
            float target = requested ? strength : 0f;
            float rate = requested ? 22f : 14f;
            float blend = 1f - Mathf.Exp(-rate * realDelta);
            combatShotAmount = Mathf.Lerp(combatShotAmount, target, blend);
            if (!requested && combatShotAmount < .001f) combatShotAmount = 0f;

            // These accents only belong to exchanges requested by a controller. Standalone arenas and cutscenes
            // retain their existing camera and letterbox behavior without enrolling in the new shot system.
            float accent = 0f;
            if (combatShotSeen)
            {
                if (IsFatalFocus) accent = 1f;
                else if (IsBladeBinding) accent = .8f;
                else if (requested && cutInAmount <= .001f && StepFocusAmount <= .001f)
                {
                    float strike = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, .5f, combatShotProgress));
                    accent = .32f + .16f * strike + (combatShotUnopposed ? .12f : 0f);
                }
            }
            float letterboxBlend = 1f - Mathf.Exp(-18f * realDelta);
            combatLetterboxAccent = Mathf.Lerp(combatLetterboxAccent, accent * strength, letterboxBlend);
            if (accent <= 0f && combatLetterboxAccent < .001f) combatLetterboxAccent = 0f;
        }

        private float ApplyCombatShot(ref Vector3 pivot, ref float size)
        {
            float weight = CombatShotMotionWeight;
            if (weight <= 0f) return 0f;
            float preparation = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .2f, combatShotProgress));
            float strike = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, .58f, combatShotProgress));
            float recovery = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.68f, 1f, combatShotProgress));
            float push = Mathf.Lerp(-.035f * preparation, combatShotUnopposed ? .18f : .12f, strike);
            push *= 1f - recovery * .25f;
            if (IsBladeBinding) push = Mathf.Max(push, .16f);
            // A Major/signature already requests a push: choose the stronger framing instead of multiplying two zooms.
            float combinedPush = push >= 0f ? Mathf.Max(directorPushAmount, push * weight)
                : directorPushAmount + push * weight;
            size *= (1f - combinedPush) / Mathf.Max(.5f, 1f - directorPushAmount);

            float direction = combatShotPlayerAttacks ? 1f : -1f;
            float travel = Mathf.Lerp(-.3f * preparation, combatShotUnopposed ? .9f : .68f, strike);
            travel *= 1f - recovery * .2f;
            if (IsBladeBinding) travel *= .2f;
            pivot.x += direction * travel * weight;
            pivot.y += Mathf.Sin(strike * Mathf.PI) * .1f * weight;
            FitCombatShot(ref pivot, ref size);
            return direction * Mathf.Sin(strike * Mathf.PI) * (combatShotUnopposed ? 1.2f : .85f) * weight;
        }

        private void FitPlayerSlowMotionShot(ref Vector3 pivot, ref float size, float focus)
        {
            if (focus <= 0f || suspension != null) return;
            // The protagonist owns slow-motion close-ups. A distant or lower inactive opponent cannot widen them.
            // Fixed body margins keep sprite poses from pumping the zoom; the player's own ground stays over the HUD.
            const float sideMargin = 2.05f, topInset = .08f;
            float aspect = Mathf.Max(.5f, ArenaCamera.aspect);
            Vector3 position = player.Renderer.transform.localPosition;
            float top = position.y + 1.75f;
            float bottom = PlayerGroundPosition.y - .34f;
            if (player.GroundShadow != null && player.GroundShadow.enabled)
                bottom = Mathf.Min(bottom, player.GroundShadow.bounds.min.y);
            float bottomInset = Mathf.Max(.06f, hudFloor + .025f);
            size = Mathf.Max(size, Mathf.Max(sideMargin / aspect,
                (top - bottom) / (2f * Mathf.Max(.2f, 1f - topInset - bottomInset))));
            float halfWidth = size * aspect;
            pivot.x = Mathf.Clamp(pivot.x, position.x + sideMargin - halfWidth, position.x - sideMargin + halfWidth);
            float low = top - size * (1f - 2f * topInset);
            float high = bottom + size * (1f - 2f * bottomInset);
            pivot.y = Mathf.Clamp(pivot.y, low, Mathf.Max(low, high));
        }

        private void FitCombatShot(ref Vector3 pivot, ref float size)
        {
            if (CombatShotMotionWeight <= 0f) return;
            // Authored margins enclose bodies, feet and the sword exchange. Sprite-frame bounds would make a
            // different crop zoom the camera on every cel. At narrow aspects we give up some push before cropping.
            const float sideMargin = 2.05f, topInset = .10f;
            float aspect = Mathf.Max(.5f, ArenaCamera.aspect);
            float left = Mathf.Min(player.Renderer.transform.localPosition.x, enemy.Renderer.transform.localPosition.x) - sideMargin;
            float right = Mathf.Max(player.Renderer.transform.localPosition.x, enemy.Renderer.transform.localPosition.x) + sideMargin;
            float top = Mathf.Max(player.Renderer.transform.localPosition.y, enemy.Renderer.transform.localPosition.y) + 1.75f;
            float bottom = depthPresentationVisible ? LowestGroundVisualY
                : Mathf.Min(PlayerGroundPosition.y, GroundPosition(enemy).y) - .1f;
            float bottomInset = Mathf.Max(.035f, hudFloor + .015f);
            size = Mathf.Max(size, Mathf.Max((right - left) / (2f * aspect),
                (top - bottom) / (2f * Mathf.Max(.2f, 1f - topInset - bottomInset))));
            float halfWidth = size * aspect;
            pivot.x = Mathf.Clamp(pivot.x, right - halfWidth, left + halfWidth);
            float lowestCenter = top - size * (1f - 2f * topInset);
            float highestCenter = bottom + size * (1f - 2f * bottomInset);
            pivot.y = Mathf.Clamp(pivot.y, lowestCenter, Mathf.Max(lowestCenter, highestCenter));
        }
    }
}
