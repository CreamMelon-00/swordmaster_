using System;
using TurnLimbo.Runtime.Combat;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>The finishing blow, in every battle (missions, stages and training, won or lost): the hit that ends the
    /// duel plays on in a long slow motion with the decisive close-up held on the fallen fighter while letterbox bars slide
    /// in, then a freeze frame; the 서막's forced loss then drains to black and white (the flashback grade) while the bars
    /// slide out. The order and timing are <see cref="LegacyFinishingBlow"/>'s; this draws them (the bars, the grey, the
    /// overlays fading with the colour) and starts the arena's close-up. The controller runs the battle's clock at
    /// <see cref="CombatSpeed"/>, stops everything while <see cref="IsFrozen"/>, and moves on to the result or the outro
    /// once <see cref="IsComplete"/>. It changes no global time; <see cref="Clear"/> takes the bars and the grey away and
    /// brings the overlays back (the battle is left, restarted or handed over).</summary>
    public sealed class DuelFinale : IDisposable
    {
        /// <summary>The close-up is held this much longer than the slow motion, so it is still on when the freeze stops the
        /// picture (the freeze never shows the margin).</summary>
        public const float FocusMargin = .1f;
        private readonly LegacyFinishingBlow timeline = new LegacyFinishingBlow();
        private readonly LegacyArenaView arena;
        private readonly DuelLetterbox letterbox;
        private readonly Action<float> fadeOverlays;
        // Whether this finale has drained the arena's colour, so clearing it gives the colour (and the overlays) back.
        private bool greyShown;
        private bool disposed;

        /// <param name="fadeOverlays">Fades what the arena's grade never reaches (the duel HUD, an overlay) as the fall
        /// drains the colour: 1 shows it, 0 hides it; called with 1 when the colour comes back. Null for none.</param>
        public DuelFinale(Transform parent, LegacyArenaView arena, Action<float> fadeOverlays = null)
        {
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.fadeOverlays = fadeOverlays;
            letterbox = new DuelLetterbox(parent);
        }

        public LegacyFinishingBlow Timeline => timeline;
        public DuelLetterbox Letterbox => letterbox;
        public bool IsRunning => timeline.IsRunning;
        public bool IsFrozen => timeline.IsFrozen;
        public bool IsComplete => timeline.IsComplete;
        /// <summary>Whether it played out through the fall: what follows takes over a black and white picture.</summary>
        public bool EndsInGrey => timeline.EndsInGrey;
        public float CombatSpeed => timeline.CombatSpeed;

        /// <summary>Starts at the finishing hit, after its decisive presentation.</summary>
        /// <param name="fall">The 서막's forced loss: after the freeze the picture drains to black and white.</param>
        /// <param name="hitStop">The real seconds of hit stop still to come before the slow motion starts, so the close-up
        /// lasts until the freeze.</param>
        public void Begin(DuelPresentationSettings settings, DuelMatchOutcome outcome, bool fall, float hitStop = 0f)
        {
            if (disposed) return;
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ClearGrey();
            timeline.Begin(settings.FinishingSlowMotionSeconds, settings.FinishingSlowMotionScale,
                settings.FinishingBarSeconds, settings.FinishingFreezeSeconds, fall ? settings.FinalFallGreySeconds : 0f);
            if (timeline.Current == LegacyFinishingBlow.Stage.SlowMotion)
                arena.HoldFinishingFocus(outcome == DuelMatchOutcome.EnemyVictory || outcome == DuelMatchOutcome.Draw,
                    outcome == DuelMatchOutcome.PlayerVictory || outcome == DuelMatchOutcome.Draw,
                    settings.FinishingSlowMotionSeconds + Mathf.Max(0f, hitStop) + FocusMargin);
            Apply();
        }

        /// <summary>Lets <paramref name="realDelta"/> pass: the battle's live time during the slow motion (hit stop
        /// excluded), real time once frozen.</summary>
        public void Tick(float realDelta)
        {
            if (disposed || !timeline.IsRunning) return;
            timeline.Advance(realDelta);
            Apply();
        }

        /// <summary>Back to idle at once: no bars, and the arena's colour (and the overlays) back if the fall drained it.</summary>
        public void Clear()
        {
            if (disposed) return;
            timeline.Clear();
            letterbox.SetAmount(0f);
            ClearGrey();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            letterbox.Dispose();
        }

        private void Apply()
        {
            letterbox.SetAmount(timeline.BarsAmount);
            float grey = timeline.FallAmount;
            if (grey <= 0f && !greyShown) return;
            arena.SetFlashback(grey);
            fadeOverlays?.Invoke(1f - grey);
            greyShown = grey > 0f;
        }

        private void ClearGrey()
        {
            if (!greyShown) return;
            greyShown = false;
            arena.SetFlashback(0f);
            fadeOverlays?.Invoke(1f);
        }
    }
}
