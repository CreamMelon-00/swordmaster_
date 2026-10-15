using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Who is ahead on health, each fighter's health taken as a share of its own maximum, from the player's side
    /// (<see cref="LegacyTensionLight.LeadOf"/>). Presentation only: the rules never read it.</summary>
    public enum LegacyHealthLead
    {
        /// <summary>Neither is ahead by more than the lead margin.</summary>
        Even,
        Behind,
        Ahead,
    }

    /// <summary>What turned in the battle's flow at one reading (<see cref="LegacyTensionLight.Observe"/>), from the player's
    /// side. Presentation only.</summary>
    [Flags]
    public enum LegacyFlowTurn
    {
        None = 0,
        /// <summary>The player went from behind to ahead on health share (past the lead margin both times; an even reading
        /// between them does not count as either).</summary>
        TookTheLead = 1,
        /// <summary>The opponent's resistance broke.</summary>
        BrokeOpponent = 2,
    }

    /// <summary>Both fighters' health as the rules have it now, from the player's side (a versus screen's own player first),
    /// and whether the opponent's resistance is broken: what the tension lighting reads (<see cref="LegacyTensionLight"/>).
    /// <see cref="None"/> (a default reading) is unknown.</summary>
    public readonly struct LegacyHealthReading
    {
        public static readonly LegacyHealthReading None = default;

        public LegacyHealthReading(int playerHealth, int playerMaximumHealth, int enemyHealth, int enemyMaximumHealth,
            bool enemyBroken)
        {
            PlayerHealth = playerHealth;
            PlayerMaximumHealth = playerMaximumHealth;
            EnemyHealth = enemyHealth;
            EnemyMaximumHealth = enemyMaximumHealth;
            EnemyBroken = enemyBroken;
        }

        /// <summary>The reading of two fighters, the player's first; <see cref="None"/> when either is missing.</summary>
        public static LegacyHealthReading Of(LegacyFighterState player, LegacyFighterState enemy)
            => player == null || enemy == null ? None
                : new LegacyHealthReading(player.Health, player.MaxHealth, enemy.Health, enemy.MaxHealth, enemy.IsResistanceBroken);

        public int PlayerHealth { get; }
        public int PlayerMaximumHealth { get; }
        public int EnemyHealth { get; }
        public int EnemyMaximumHealth { get; }
        public bool EnemyBroken { get; }
        /// <summary>Whether both fighters have a maximum health to measure against.</summary>
        public bool IsKnown => PlayerMaximumHealth > 0 && EnemyMaximumHealth > 0;
        /// <summary>Whether either fighter has fallen (no health left); false while unknown.</summary>
        public bool AnyoneFell => IsKnown && (PlayerHealth <= 0 || EnemyHealth <= 0);
        /// <summary>Each fighter's health as a share of its maximum, 0 to 1 (1 while unknown).</summary>
        public float PlayerShare => Share(PlayerHealth, PlayerMaximumHealth);
        public float EnemyShare => Share(EnemyHealth, EnemyMaximumHealth);

        private static float Share(int health, int maximum)
            => maximum <= 0 ? 1f : health <= 0 ? 0f : health >= maximum ? 1f : (float)health / maximum;
    }

    /// <summary>The tension lighting's numbers (<see cref="LegacyTensionLight"/>). Presentation only: the rules never read
    /// them. The presentation settings hold them; <see cref="Default"/> is the code's defaults and <see cref="Off"/> lights
    /// nothing.</summary>
    public readonly struct LegacyTensionTuning
    {
        public const int DefaultHealthPercent = 30, DefaultLeadMarginPercent = 5;
        public const float DefaultEaseInSeconds = .8f, DefaultEaseOutSeconds = 1.2f, DefaultSwellRiseSeconds = .12f,
            DefaultSwellHoldSeconds = .1f, DefaultSwellFadeSeconds = .7f;
        /// <summary>The ranges the numbers are kept in: a health share below 100%, a lead margin of at most 30%, eases and
        /// a swell's parts of at most 3 real seconds (a swell's fade at least 0.05).</summary>
        public const int MaximumHealthPercent = 99, MaximumLeadMarginPercent = 30;
        public const float MaximumEaseSeconds = 3f, MaximumSwellSeconds = 3f, MinimumSwellFadeSeconds = .05f;

        /// <param name="tensionEnabled">Whether low health shifts the light at all.</param>
        /// <param name="healthPercent">Either fighter at or below this share of its maximum health (%, 0 to 99; 0 switches
        /// the shift off) makes the light tense.</param>
        /// <param name="easeInSeconds">Real seconds the shift takes to come in whole (0 to 3; 0 at once).</param>
        /// <param name="easeOutSeconds">Real seconds it takes to go back whole once nobody is that low (0 to 3; 0 at once).</param>
        /// <param name="swellEnabled">Whether a turn of the flow swells the light.</param>
        /// <param name="swellOnBreak">Whether the opponent's break swells it too (taking the lead always does).</param>
        /// <param name="leadMarginPercent">How far apart the two health shares must be (%, 0 to 30) for one to be ahead.</param>
        /// <param name="swellRiseSeconds">The swell's rise, hold at full and fade (real seconds; the fade 0.05 to 3, the
        /// others 0 to 3).</param>
        public LegacyTensionTuning(bool tensionEnabled, int healthPercent, float easeInSeconds, float easeOutSeconds,
            bool swellEnabled, bool swellOnBreak, int leadMarginPercent, float swellRiseSeconds, float swellHoldSeconds,
            float swellFadeSeconds)
        {
            TensionEnabled = tensionEnabled;
            HealthPercent = healthPercent < 0 ? 0 : healthPercent > MaximumHealthPercent ? MaximumHealthPercent : healthPercent;
            EaseInSeconds = Clamp(easeInSeconds, 0f, MaximumEaseSeconds, DefaultEaseInSeconds);
            EaseOutSeconds = Clamp(easeOutSeconds, 0f, MaximumEaseSeconds, DefaultEaseOutSeconds);
            SwellEnabled = swellEnabled;
            SwellOnBreak = swellOnBreak;
            LeadMarginPercent = leadMarginPercent < 0 ? 0
                : leadMarginPercent > MaximumLeadMarginPercent ? MaximumLeadMarginPercent : leadMarginPercent;
            SwellRiseSeconds = Clamp(swellRiseSeconds, 0f, MaximumSwellSeconds, DefaultSwellRiseSeconds);
            SwellHoldSeconds = Clamp(swellHoldSeconds, 0f, MaximumSwellSeconds, DefaultSwellHoldSeconds);
            SwellFadeSeconds = Clamp(swellFadeSeconds, MinimumSwellFadeSeconds, MaximumSwellSeconds, DefaultSwellFadeSeconds);
        }

        /// <summary>The defaults: tense at 30% health or less, in over 0.8 s and back over 1.2 s; a swell when the player
        /// takes a lead of more than 5% from behind or breaks the opponent, 0.12 s up, 0.1 s held, 0.7 s down.</summary>
        public static LegacyTensionTuning Default => new LegacyTensionTuning(true, DefaultHealthPercent, DefaultEaseInSeconds,
            DefaultEaseOutSeconds, true, true, DefaultLeadMarginPercent, DefaultSwellRiseSeconds, DefaultSwellHoldSeconds,
            DefaultSwellFadeSeconds);

        /// <summary>Nothing lights: no tension and no swell.</summary>
        public static LegacyTensionTuning Off => new LegacyTensionTuning(false, 0, DefaultEaseInSeconds, DefaultEaseOutSeconds,
            false, false, DefaultLeadMarginPercent, DefaultSwellRiseSeconds, DefaultSwellHoldSeconds, DefaultSwellFadeSeconds);

        public bool TensionEnabled { get; }
        public int HealthPercent { get; }
        public float EaseInSeconds { get; }
        public float EaseOutSeconds { get; }
        public bool SwellEnabled { get; }
        public bool SwellOnBreak { get; }
        public int LeadMarginPercent { get; }
        public float SwellRiseSeconds { get; }
        public float SwellHoldSeconds { get; }
        public float SwellFadeSeconds { get; }
        /// <summary>A whole swell's real seconds: rise, hold and fade.</summary>
        public float SwellSeconds => SwellRiseSeconds + SwellHoldSeconds + SwellFadeSeconds;

        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) ? fallback : value < min ? min : value > max ? max : value;
    }

    /// <summary>The arena's tension lighting (2026-10-15), presentation only: the rules never read it and it takes no time.
    /// <list type="bullet">
    /// <item><b>Tension:</b> while either fighter's health is at or below <see cref="LegacyTensionTuning.HealthPercent"/>
    /// of its maximum, <see cref="Tension"/> eases toward 1 on real time; once nobody is that low (healed) or nothing is read
    /// (<see cref="Release"/>), back toward 0. The picture shows <see cref="TensionAmount"/>, the same eased in and out.</item>
    /// <item><b>Swell:</b> when the flow turns for the player (<see cref="LegacyFlowTurn"/>: from behind to ahead on health
    /// share, or the opponent's break), a short swell (<see cref="Swell"/>, 0 to 1: rise, hold, fade on real time). A
    /// reading where anyone has fallen never swells (the finale has the light). A turn while one swells carries it on from
    /// where it is, without a dip.</item>
    /// </list>
    /// Readings come from the player's side; the first after <see cref="Clear"/> only sets where things stand. Both
    /// fighters count alike for the tension, so a versus match is tense on both screens alike; each screen swells for its
    /// own player.</summary>
    public sealed class LegacyTensionLight
    {
        private LegacyTensionTuning tuning = LegacyTensionTuning.Default;
        private float target;
        private bool primed, enemyWasBroken, swelling;
        private LegacyHealthLead lead;
        private float swellElapsed;

        /// <summary>How far the tension has come in, 0 to 1 (linear in time).</summary>
        public float Tension { get; private set; }
        /// <summary>The tension as the picture shows it: <see cref="Tension"/> eased in and out (smoothstep).</summary>
        public float TensionAmount => Smooth(Tension);
        /// <summary>Whether the last reading was tense (the tension heads for 1).</summary>
        public bool IsTense => target > 0f;
        /// <summary>The swell now, 0 to 1 (0 while none plays).</summary>
        public float Swell => swelling ? SwellEnvelope(swellElapsed, tuning) : 0f;
        public bool IsSwelling => swelling;
        /// <summary>How many swells began since <see cref="Clear"/>.</summary>
        public int SwellCount { get; private set; }
        /// <summary>The last turn of the flow read since <see cref="Clear"/> (whether or not it swelled).</summary>
        public LegacyFlowTurn LastTurn { get; private set; }
        /// <summary>Who was last ahead past the margin (<see cref="LegacyHealthLead.Even"/> until one was).</summary>
        public LegacyHealthLead Lead => lead;
        /// <summary>The numbers of the last reading.</summary>
        public LegacyTensionTuning Tuning => tuning;

        /// <summary>A frame's reading: sets where the tension heads, and returns the turn of the flow it finds (none on the
        /// first reading after <see cref="Clear"/>). A swell begins on taking the lead, or on the opponent's break when
        /// <see cref="LegacyTensionTuning.SwellOnBreak"/>, unless anyone has fallen or swells are off. An unknown reading
        /// counts as none (<see cref="Release"/>).</summary>
        public LegacyFlowTurn Observe(LegacyHealthReading reading, LegacyTensionTuning tuning)
        {
            this.tuning = tuning;
            if (!tuning.SwellEnabled) StopSwell();
            if (!reading.IsKnown)
            {
                target = 0f;
                return LegacyFlowTurn.None;
            }
            target = tuning.TensionEnabled && IsTenseReading(reading, tuning.HealthPercent) ? 1f : 0f;
            LegacyHealthLead now = LeadOf(reading, tuning.LeadMarginPercent);
            LegacyFlowTurn turn = LegacyFlowTurn.None;
            if (primed)
            {
                if (now == LegacyHealthLead.Ahead && lead == LegacyHealthLead.Behind) turn |= LegacyFlowTurn.TookTheLead;
                if (reading.EnemyBroken && !enemyWasBroken) turn |= LegacyFlowTurn.BrokeOpponent;
            }
            primed = true;
            if (now != LegacyHealthLead.Even) lead = now;
            enemyWasBroken = reading.EnemyBroken;
            if (turn == LegacyFlowTurn.None) return turn;
            LastTurn = turn;
            bool swells = (turn & LegacyFlowTurn.TookTheLead) != 0 ||
                (turn & LegacyFlowTurn.BrokeOpponent) != 0 && tuning.SwellOnBreak;
            if (swells && tuning.SwellEnabled && !reading.AnyoneFell) BeginSwell();
            return turn;
        }

        /// <summary>A frame without a reading (nothing asked for the light): the tension heads back to 0, with the last
        /// numbers; a swell plays on.</summary>
        public void Release() => target = 0f;

        /// <summary>Lets <paramref name="realSeconds"/> pass: the tension eases toward where the last reading set it (in over
        /// <see cref="LegacyTensionTuning.EaseInSeconds"/>, out over <see cref="LegacyTensionTuning.EaseOutSeconds"/>) and a
        /// swell plays on, ending after <see cref="LegacyTensionTuning.SwellSeconds"/>.</summary>
        public void Advance(float realSeconds)
        {
            if (float.IsNaN(realSeconds) || !(realSeconds > 0f)) return;
            Tension = Step(Tension, target, realSeconds, tuning.EaseInSeconds, tuning.EaseOutSeconds);
            if (!swelling) return;
            swellElapsed += float.IsInfinity(realSeconds) ? tuning.SwellSeconds : realSeconds;
            if (swellElapsed >= tuning.SwellSeconds) StopSwell();
        }

        /// <summary>A scene in the middle of the battle sets the light aside: the tension comes off at once (returned, for
        /// <see cref="PutBack"/>) and a swell ends. Where the flow stood is kept.</summary>
        public float SetAside()
        {
            float tension = Tension;
            Tension = 0f;
            StopSwell();
            return tension;
        }

        /// <summary>The battle is back after a scene: the tension is where <see cref="SetAside"/> found it.</summary>
        public void PutBack(float tension) => Tension = float.IsNaN(tension) ? 0f : tension < 0f ? 0f : tension > 1f ? 1f : tension;

        /// <summary>A fresh battle: no tension, no swell, and the next reading only sets where things stand.</summary>
        public void Clear()
        {
            Tension = target = swellElapsed = 0f;
            primed = enemyWasBroken = swelling = false;
            lead = LegacyHealthLead.Even;
            SwellCount = 0;
            LastTurn = LegacyFlowTurn.None;
        }

        /// <summary>Whether either fighter is at or below <paramref name="healthPercent"/> of its maximum health (exact, in
        /// whole numbers: 30 of 100 is, 31 is not). An unknown reading or a share of 0 never is.</summary>
        public static bool IsTenseReading(LegacyHealthReading reading, int healthPercent)
        {
            if (!reading.IsKnown || healthPercent <= 0) return false;
            return (long)reading.PlayerHealth * 100 <= (long)reading.PlayerMaximumHealth * healthPercent ||
                (long)reading.EnemyHealth * 100 <= (long)reading.EnemyMaximumHealth * healthPercent;
        }

        /// <summary>Who is ahead on health share, from the player's side: ahead or behind only by more than
        /// <paramref name="marginPercent"/> percentage points (exact, in whole numbers), otherwise even. Mirrored readings
        /// give mirrored leads.</summary>
        public static LegacyHealthLead LeadOf(LegacyHealthReading reading, int marginPercent)
        {
            if (!reading.IsKnown) return LegacyHealthLead.Even;
            long player = Math.Max(0, Math.Min(reading.PlayerHealth, reading.PlayerMaximumHealth));
            long enemy = Math.Max(0, Math.Min(reading.EnemyHealth, reading.EnemyMaximumHealth));
            long scale = (long)reading.PlayerMaximumHealth * reading.EnemyMaximumHealth;
            // The shares' difference times both maxima, against the margin times both maxima (both times 100).
            long difference = (player * reading.EnemyMaximumHealth - enemy * reading.PlayerMaximumHealth) * 100;
            long margin = scale * Math.Max(0, marginPercent);
            return difference > margin ? LegacyHealthLead.Ahead : difference < -margin ? LegacyHealthLead.Behind
                : LegacyHealthLead.Even;
        }

        /// <summary>A swell's strength <paramref name="elapsed"/> real seconds in: up over its rise (smoothstep), held at 1,
        /// down over its fade (smoothstep), then 0.</summary>
        public static float SwellEnvelope(float elapsed, LegacyTensionTuning tuning)
        {
            if (float.IsNaN(elapsed) || elapsed < 0f) return 0f;
            if (elapsed < tuning.SwellRiseSeconds) return Smooth(elapsed / tuning.SwellRiseSeconds);
            elapsed -= tuning.SwellRiseSeconds;
            if (elapsed < tuning.SwellHoldSeconds) return 1f;
            elapsed -= tuning.SwellHoldSeconds;
            return elapsed < tuning.SwellFadeSeconds ? 1f - Smooth(elapsed / tuning.SwellFadeSeconds) : 0f;
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> over <paramref name="realSeconds"/>:
        /// a whole rise takes <paramref name="easeInSeconds"/>, a whole fall <paramref name="easeOutSeconds"/> (0: at once).</summary>
        public static float Step(float current, float target, float realSeconds, float easeInSeconds, float easeOutSeconds)
        {
            current = Clamp01(current);
            target = Clamp01(target);
            if (float.IsNaN(realSeconds) || !(realSeconds > 0f)) return current;
            if (target >= current)
                return easeInSeconds > 0f && !float.IsInfinity(realSeconds)
                    ? Math.Min(target, current + realSeconds / easeInSeconds) : target;
            return easeOutSeconds > 0f && !float.IsInfinity(realSeconds)
                ? Math.Max(target, current - realSeconds / easeOutSeconds) : target;
        }

        // A turn while a swell plays carries it on from its strength now (rising again from there); otherwise from the start.
        private void BeginSwell()
        {
            float now = Swell;
            swellElapsed = swelling && now > 0f ? tuning.SwellRiseSeconds * InverseSmooth(now) : 0f;
            swelling = true;
            SwellCount++;
        }

        private void StopSwell()
        {
            swelling = false;
            swellElapsed = 0f;
        }

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Where on the smoothstep's rise it reaches value (0 to 1).
        private static float InverseSmooth(float value)
        {
            value = Clamp01(value);
            return Clamp01((float)(.5 - Math.Sin(Math.Asin(1.0 - 2.0 * value) / 3.0)));
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
