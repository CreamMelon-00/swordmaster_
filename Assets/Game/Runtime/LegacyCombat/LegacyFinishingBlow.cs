using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The finishing blow's timeline, for every battle (presentation only; the combat rules never read it). When a
    /// hit ends the duel the battle plays on in a long slow motion while letterbox bars slide in, then holds a freeze
    /// frame, then (the 서막's forced loss only) drains to black and white while the bars slide back out, and hands over
    /// to the result or the outro. It runs on real seconds the caller hands it; the battle's own clock runs at
    /// <see cref="CombatSpeed"/> meanwhile. A stage of zero length is skipped, and with nothing to play at all
    /// <see cref="Begin"/> leaves it idle, so the battle ends as it did before.</summary>
    public sealed class LegacyFinishingBlow
    {
        public enum Stage
        {
            /// <summary>No finishing blow is playing.</summary>
            None,
            /// <summary>The battle's clock runs slowly while the bars slide in.</summary>
            SlowMotion,
            /// <summary>Nothing moves: the freeze frame, bars in.</summary>
            Freeze,
            /// <summary>Still frozen, the colour drains to black and white and the bars slide out.</summary>
            Fall,
            /// <summary>Played out: what follows (the result or the outro) may come.</summary>
            Done,
        }

        /// <summary>The slowest the slow motion may run: never a full stop, which is the freeze's.</summary>
        public const float MinimumSlowMotionScale = .01f;

        private float slowSeconds, slowScale = 1f, barSeconds, freezeSeconds, fallSeconds;
        // Real seconds spent in the current stage, and since the blow (the bars slide in across the first stages).
        private float elapsed, sinceBlow;

        public Stage Current { get; private set; }
        public bool IsRunning => Current == Stage.SlowMotion || Current == Stage.Freeze || Current == Stage.Fall;
        /// <summary>The freeze frame or the fall after it: the battle shows one still picture.</summary>
        public bool IsFrozen => Current == Stage.Freeze || Current == Stage.Fall;
        public bool IsComplete => Current == Stage.Done;
        /// <summary>Whether it played out through a fall: the picture handed over is black and white.</summary>
        public bool EndsInGrey => Current == Stage.Done && fallSeconds > 0f;
        /// <summary>The battle clock's speed: the slow motion's scale, 0 while frozen, 1 otherwise.</summary>
        public float CombatSpeed => Current == Stage.SlowMotion ? slowScale : IsFrozen ? 0f : 1f;
        /// <summary>The real seconds the current stage has run.</summary>
        public float StageElapsed => elapsed;

        /// <summary>How far the letterbox bars are in, 0 to 1: sliding in from the blow over the bar time (through the
        /// freeze if the slow motion is shorter), all the way in while frozen, sliding out over the fall, gone after.</summary>
        public float BarsAmount
        {
            get
            {
                if (!IsRunning) return 0f;
                float bars = barSeconds > 0f ? Ease(sinceBlow / barSeconds) : 1f;
                if (Current == Stage.Fall) bars *= 1f - Ease(elapsed / fallSeconds);
                return bars;
            }
        }

        /// <summary>How black and white the picture is, 0 to 1: rising over the fall and staying at 1 once it has played out.</summary>
        public float FallAmount => Current == Stage.Fall ? Ease(elapsed / fallSeconds) : EndsInGrey ? 1f : 0f;

        /// <summary>Starts the timeline at the finishing hit. Lengths are real seconds; zero (or less) skips that stage.</summary>
        /// <param name="slowMotionScale">The battle clock's speed in the slow motion, kept above
        /// <see cref="MinimumSlowMotionScale"/>; 1 plays the stage at normal speed.</param>
        /// <param name="fallSeconds">The fall to black and white after the freeze (the 서막's forced loss); 0 for none.</param>
        public void Begin(float slowMotionSeconds, float slowMotionScale, float barSeconds, float freezeSeconds,
            float fallSeconds = 0f)
        {
            slowSeconds = Length(slowMotionSeconds);
            slowScale = float.IsNaN(slowMotionScale) ? 1f : Math.Max(MinimumSlowMotionScale, Math.Min(1f, slowMotionScale));
            this.barSeconds = Length(barSeconds);
            this.freezeSeconds = Length(freezeSeconds);
            this.fallSeconds = Length(fallSeconds);
            elapsed = sinceBlow = 0f;
            if (slowSeconds <= 0f && this.freezeSeconds <= 0f && this.fallSeconds <= 0f)
            {
                Current = Stage.None;
                return;
            }
            Current = Stage.SlowMotion;
            // Steps past any stage of zero length.
            Advance(0f);
        }

        /// <summary>Moves on by <paramref name="realSeconds"/>; a long frame may cross several stages.</summary>
        public void Advance(float realSeconds)
        {
            if (!IsRunning) return;
            float delta = realSeconds > 0f && !float.IsInfinity(realSeconds) ? realSeconds : 0f;
            while (true)
            {
                float left = Math.Max(0f, StageLength(Current) - elapsed);
                if (delta < left)
                {
                    elapsed += delta;
                    sinceBlow += delta;
                    return;
                }
                delta -= left;
                sinceBlow += left;
                elapsed = 0f;
                Current = Current == Stage.SlowMotion ? Stage.Freeze : Current == Stage.Freeze ? Stage.Fall : Stage.Done;
                if (Current == Stage.Done) return;
            }
        }

        /// <summary>Back to idle at once (the battle is left, restarted or handed over).</summary>
        public void Clear()
        {
            Current = Stage.None;
            slowSeconds = barSeconds = freezeSeconds = fallSeconds = 0f;
            slowScale = 1f;
            elapsed = sinceBlow = 0f;
        }

        private float StageLength(Stage stage) => stage == Stage.SlowMotion ? slowSeconds
            : stage == Stage.Freeze ? freezeSeconds : stage == Stage.Fall ? fallSeconds : 0f;

        private static float Length(float seconds) => seconds > 0f && !float.IsInfinity(seconds) ? seconds : 0f;

        // Eases in and out, clamped to 0..1.
        private static float Ease(float t)
        {
            t = t > 0f ? Math.Min(1f, t) : 0f;
            return t * t * (3f - 2f * t);
        }
    }
}
