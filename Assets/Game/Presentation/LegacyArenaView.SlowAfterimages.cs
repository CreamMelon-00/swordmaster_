using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class LegacyArenaView
    {
        private const int SlowAfterimageCapacity = 2;
        private const float SlowAfterimageInterval = .18f;
        private const float SlowAfterimageLifetime = .22f;
        private static readonly Color SlowAfterimageCool = new Color(.43f, .84f, 1f, .70f);
        private static readonly Color SlowAfterimageWarm = new Color(1f, .71f, .35f, .70f);
        // During a cinematic slow shot this is the player's only body trail, rather than stacking several pools.
        private DuelStepAfterimages slowAfterimages;
        private float slowAfterimageClock;
        private bool slowAfterimageActive;
        private Vector3 slowAfterimagePreviousPosition;
        private bool slowAfterimageHasPosition;

        /// <summary>Player pose snapshots left during a successful step or decisive slow motion.</summary>
        public int ActiveSlowAfterimageCount => slowAfterimages?.ActiveCount ?? 0;

        private void InitializeSlowAfterimages()
        {
            slowAfterimages = new DuelStepAfterimages(arenaRoot.transform, spriteMaterial, ArenaLayer,
                mobAnimations.HasRequiredAssets && !mobAnimations.UsesFullBodyFrames,
                SlowAfterimageCapacity, "Slow Motion");
        }

        /// <summary>Ends the trail immediately when leaving combat, resetting or borrowing its actors for a scene.
        /// Pause and the finishing freeze simply stop ticking it, preserving the captured picture.</summary>
        public void ClearSlowMotionAfterimages()
        {
            slowAfterimages?.Reset();
            slowAfterimageClock = 0f;
            slowAfterimageActive = false;
            slowAfterimageHasPosition = false;
        }

        private void TickSlowAfterimages(float realDelta, bool deferEmission)
        {
            if (suspension != null) return;
            slowAfterimages.Tick(realDelta);
            if (!deferEmission) SampleSlowAfterimages(realDelta);
        }

        private void PrepareSlowAfterimageSampling()
        {
            if (slowAfterimageActive || !IsCinematicSlowMotion) return;
            // Clear only the player's older copies. Opponent silhouettes and sword streaks keep their own cues.
            stepAfterimages.Reset();
            signatureAfterimages?.ClearSource(player.Renderer);
            attackEchoes?.ClearSource(player.Renderer);
            pressureTrailSampleTime = playerSignatureTrail.Clock = 0f;
            slowAfterimageActive = true;
        }

        // Called after the controller's windup, bind, range and hit-time holds. Emission never advances the pool's age:
        // Arena.Tick has already faded it exactly once, whether this final displayed pose is held or moving.
        internal void SampleSlowAfterimages(float realDelta)
        {
            if (disposed || suspension != null || !(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            SpriteRenderer source = player.Renderer;
            Vector3 position = source.transform.position + player.AttackVisualOffset;
            Vector3 movement = slowAfterimageHasPosition ? position - slowAfterimagePreviousPosition : Vector3.zero;
            slowAfterimagePreviousPosition = position;
            slowAfterimageHasPosition = true;
            // Automatic planning/manual slow clocks and a failed gesture do not invent a success trail.
            bool slow = IsCinematicSlowMotion;
            if (!slow || !source.enabled || !source.gameObject.activeInHierarchy || source.sprite == null)
            {
                slowAfterimageClock = 0f;
                slowAfterimageActive = false;
                return;
            }
            PrepareSlowAfterimageSampling();
            slowAfterimageClock += realDelta;
            if (slowAfterimageClock < SlowAfterimageInterval) return;
            // At most one snapshot per rendered tick, even after a hitch. Real time fades the bounded pool as well.
            slowAfterimageClock %= SlowAfterimageInterval;
            Vector3 drift = Vector3.zero;
            if (movement.sqrMagnitude < realDelta * realDelta * 4f)
            {
                // Deep slow motion barely moves the body. Ease its echoes back a little to reveal their edges,
                // instead of hiding all the copies exactly underneath the protagonist's opaque pose.
                Vector3 back = movement.sqrMagnitude > .000001f ? -movement.normalized
                    : Vector3.right * (source.flipX ? 1f : -1f);
                drift = back * .56f;
            }
            Color ink = StepPresentationSpeed < .999f && stepFocusAction == TurnLimbo.Runtime.LegacyCombat.LegacyStepAction.Pressure
                ? SlowAfterimageWarm : SlowAfterimageCool;
            slowAfterimages.Emit(source, ink, SlowAfterimageLifetime, drift, 0f, 0f,
                source.sortingOrder - 1, player.LowerRenderer);
        }
    }
}
