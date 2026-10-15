using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The presentation director's pacing numbers (<see cref="LegacyShowDirector"/>). Presentation only: the rules
    /// never read them. The presentation settings hold them; <see cref="Default"/> is the code's defaults and
    /// <see cref="Off"/> changes nothing.</summary>
    public readonly struct LegacyShowPacingTuning
    {
        public const float DefaultMinorSpeed = 2f, DefaultMajorHitStopScale = 3f, DefaultTurnExtraCap = .8f,
            DefaultBladeBindMinimumShare = .5f, DefaultSignatureHitStopScale = 4.5f;
        /// <summary>The ranges the numbers are kept in: a Minor slot plays at most this many times faster, a Major hit stop
        /// is at most this many times the usual, a blade bind and a turn's extra time are at most this many seconds.</summary>
        public const float MaximumMinorSpeed = 4f, MaximumMajorHitStopScale = 6f, MaximumBladeBindSeconds = 2f,
            MaximumTurnExtraCap = 5f;

        /// <param name="enabled">Whether the director changes anything at all (off: every slot plays as it always did).</param>
        /// <param name="minorSpeed">A Minor slot's rest after its first hit, and the wait after it, are this many times shorter
        /// (1 to 4; 1 leaves them).</param>
        /// <param name="majorHitStopScale">A Major slot's first hit stop is the usual one times this (1 to 6; 1 leaves it).</param>
        /// <param name="bladeBindSeconds">How long an even 합 holds the two fighters in a blade bind (real seconds, 0 to 2;
        /// 0 switches the bind off).</param>
        /// <param name="turnExtraCap">How many real seconds the director may add to one turn beyond what its Minor slots
        /// saved (0 to 5; 0: emphasis only from what was saved).</param>
        /// <param name="bladeBindMinimumShare">A blade bind plays only when the turn can still afford this share of it (0 to
        /// 1); it then plays as long as the turn can afford, up to its full length.</param>
        /// <param name="signatureHitStopScale">A Major slot with a signature move
        /// (<see cref="LegacySlotImportanceTuning.SignatureSkillIds"/>, either side) stops this many times the usual at its
        /// first presented hit instead (1 to 6, never less than <paramref name="majorHitStopScale"/>; not a number: the same
        /// as a Major slot).</param>
        public LegacyShowPacingTuning(bool enabled, float minorSpeed, float majorHitStopScale, float bladeBindSeconds,
            float turnExtraCap, float bladeBindMinimumShare = DefaultBladeBindMinimumShare,
            float signatureHitStopScale = float.NaN)
        {
            Enabled = enabled;
            MinorSpeed = Clamp(minorSpeed, 1f, MaximumMinorSpeed, 1f);
            MajorHitStopScale = Clamp(majorHitStopScale, 1f, MaximumMajorHitStopScale, 1f);
            BladeBindSeconds = Clamp(bladeBindSeconds, 0f, MaximumBladeBindSeconds, 0f);
            TurnExtraCap = Clamp(turnExtraCap, 0f, MaximumTurnExtraCap, 0f);
            BladeBindMinimumShare = Clamp(bladeBindMinimumShare, 0f, 1f, DefaultBladeBindMinimumShare);
            SignatureHitStopScale = Math.Max(MajorHitStopScale,
                Clamp(signatureHitStopScale, 1f, MaximumMajorHitStopScale, MajorHitStopScale));
        }

        /// <summary>The defaults: a Minor slot's rest twice as fast, a Major hit stop three times as long (a signature
        /// move's four and a half), a 0.45-second blade bind, at most 0.8 seconds more per turn than its Minor slots saved.</summary>
        public static LegacyShowPacingTuning Default => new LegacyShowPacingTuning(true, DefaultMinorSpeed,
            DefaultMajorHitStopScale, LegacyBladeBind.DefaultSeconds, DefaultTurnExtraCap, DefaultBladeBindMinimumShare,
            DefaultSignatureHitStopScale);

        /// <summary>The director switched off: every slot keeps its time, nothing is held.</summary>
        public static LegacyShowPacingTuning Off => new LegacyShowPacingTuning(false, 1f, 1f, 0f, 0f);

        public bool Enabled { get; }
        public float MinorSpeed { get; }
        public float MajorHitStopScale { get; }
        /// <summary>A signature move's Major moment: its stop is the usual one times this (at least
        /// <see cref="MajorHitStopScale"/>).</summary>
        public float SignatureHitStopScale { get; }
        public float BladeBindSeconds { get; }
        public float TurnExtraCap { get; }
        public float BladeBindMinimumShare { get; }

        private static float Clamp(float value, float min, float max, float fallback)
            => float.IsNaN(value) ? fallback : value < min ? min : value > max ? max : value;
    }

    /// <summary>The presentation director (presentation only; the rules never read it): it rates each resolving slot
    /// (<see cref="LegacySlotImportance.Rate(LegacySlotFacts, LegacySlotImportanceTuning)"/>) and turns the tier into pacing
    /// a controller applies, keeping a turn's ledger of the time it added and saved.
    /// <list type="bullet">
    /// <item><see cref="LegacySlotTier.Minor"/> (no attack on either side): the slot's rest after its first hit and the
    /// wait after it are <see cref="LegacyShowPacingTuning.MinorSpeed"/> times shorter; what that saves is credited.</item>
    /// <item><see cref="LegacySlotTier.Normal"/>: unchanged.</item>
    /// <item><see cref="LegacySlotTier.Major"/>: its first presented hit's stop is
    /// <see cref="LegacyShowPacingTuning.MajorHitStopScale"/> times the usual, a signature move's
    /// <see cref="LegacyShowPacingTuning.SignatureHitStopScale"/> times (the camera, the sound and the seam take no
    /// time).</item>
    /// <item><see cref="LegacySlotTier.Climax"/>: nothing; the finishing blow keeps its finale.</item>
    /// <item>An even 합 (<see cref="LegacySlotRank.IsBladeBind"/>) may hold its first hit as a blade bind
    /// (<see cref="BladeBind"/>), whatever its tier but Climax.</item>
    /// <item>The turn's later slots play progressively faster (<see cref="PlaybackSpeed"/>, <see cref="LegacyCombatTempo"/>).
    /// After an attack against an empty side, the gap keeps only <see cref="PlaybackGapScale"/> of its usual length.
    /// When this tempo is off, the legacy unopposed-streak speed is used instead (<see cref="StreakSpeed"/>).
    /// They never multiply each other; their saved time is never banked.</item>
    /// </list>
    /// Nothing is ever spent before a slot's first hit, so the step windows keep their time. Every added second is granted
    /// from the turn's allowance (<see cref="Available"/>: <see cref="LegacyShowPacingTuning.TurnExtraCap"/> plus what Minor
    /// slots saved, less what was granted), so a turn never runs more than the cap longer than it would without the
    /// director. Every amount is computed from the slot's own numbers (never a frame's length), so two screens playing the
    /// same match make the same decisions.</summary>
    public sealed class LegacyShowDirector
    {
        private LegacySlotRank slotRank = LegacySlotRank.None, latestRank = LegacySlotRank.None,
            lastSlotRank = LegacySlotRank.None;
        private LegacySlotFacts latestFacts = LegacySlotFacts.None;
        private bool majorStopGiven, bindGiven, readCueGiven;
        // The turn's unopposed streak before the resolving slot (after the slot completed last), and the playback speed the
        // slot completed last had (the wait after it plays at it).
        private int streakBefore;
        private float lastSlotSpeed = 1f, lastPlaybackSpeed = 1f, lastPlaybackGapScale = 1f;

        public LegacyShowDirector() : this(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default) { }

        public LegacyShowDirector(LegacyShowPacingTuning pacing, LegacySlotImportanceTuning importance)
            : this(pacing, importance, LegacyStreakTuning.Default) { }

        public LegacyShowDirector(LegacyShowPacingTuning pacing, LegacySlotImportanceTuning importance,
            LegacyStreakTuning streak)
        {
            Pacing = pacing;
            Importance = importance;
            Streak = streak;
            Tempo = LegacyCombatTempoTuning.Off;
        }

        /// <summary>The pacing this turn plays with (taken at <see cref="BeginTurn"/>).</summary>
        public LegacyShowPacingTuning Pacing { get; private set; }
        /// <summary>The rating's thresholds this turn plays with (taken at <see cref="BeginTurn"/>).</summary>
        public LegacySlotImportanceTuning Importance { get; private set; }
        /// <summary>The unopposed streak's numbers this turn plays with (taken at
        /// <see cref="BeginTurn(LegacyShowPacingTuning, LegacySlotImportanceTuning, LegacyStreakTuning)"/>; the
        /// two-argument <c>BeginTurn</c> keeps them).</summary>
        public LegacyStreakTuning Streak { get; private set; }
        /// <summary>The resolving turn's progressive speed, taken by the four-argument <see cref="BeginTurn"/>. Older
        /// overloads keep it; legacy constructors start with it off.</summary>
        public LegacyCombatTempoTuning Tempo { get; private set; }
        public bool Enabled => Pacing.Enabled;

        /// <summary>Real seconds the Minor slots of this turn have saved so far.</summary>
        public float Saved { get; private set; }
        /// <summary>Real seconds of emphasis granted this turn so far (longer hit stops, blade binds).</summary>
        public float Spent { get; private set; }
        /// <summary>How much longer this turn has run than it would without the director (negative: shorter). Never above
        /// <see cref="LegacyShowPacingTuning.TurnExtraCap"/>.</summary>
        public float NetExtra => Spent - Saved;
        /// <summary>What the turn can still add: the cap plus what was saved, less what was granted (0 while switched off).</summary>
        public float Available => Enabled ? Math.Max(0f, Pacing.TurnExtraCap + Saved - Spent) : 0f;

        /// <summary>The resolving slot's rating: the highest seen since it began (a later hit never lowers it), with every
        /// highlight, read and signature seen OR'd in. Only a change of kind at the first hit (a 반격 filling the slot, a
        /// dodge) re-rates it: what told how the cards met (a 합's gap, a pierce, a big unopposed attack, a bind) is then
        /// the new kind's alone, so a pending 반격 that becomes an even 합 is rated as that 합.
        /// <see cref="LegacySlotRank.None"/> between slots.</summary>
        public LegacySlotRank SlotRank => slotRank;
        /// <summary>The resolving slot's rating as it stands now (what the last <see cref="Observe"/> read).</summary>
        public LegacySlotRank LatestRank => latestRank;
        /// <summary>The rating of the slot this turn completed last (<see cref="EndSlot"/>), or none.</summary>
        public LegacySlotRank LastSlotRank => lastSlotRank;
        public bool HasSlot => slotRank.SlotIndex >= 0;
        /// <summary>Whether the resolving slot is Major: the controllers push the camera in while it plays.</summary>
        public bool IsMajor => HasSlot && slotRank.Tier == LegacySlotTier.Major;
        /// <summary>Whether a signature move plays in the resolving slot, on either side (its windup glow, trail, push and
        /// longer stop; the same on both versus screens).</summary>
        public bool IsSignature => HasSlot && (slotRank.PlayerSignature || slotRank.EnemySignature);
        /// <summary>Whether this slot's blade bind was granted.</summary>
        public bool BladeBindGiven => bindGiven;

        /// <summary>A turn begins resolving: the ledger and the streak start afresh with these numbers (the streak's
        /// numbers stay as they were).</summary>
        public void BeginTurn(LegacyShowPacingTuning pacing, LegacySlotImportanceTuning importance)
            => BeginTurn(pacing, importance, Streak);

        /// <summary>A turn begins resolving: the ledger and the streak start afresh with these numbers.</summary>
        public void BeginTurn(LegacyShowPacingTuning pacing, LegacySlotImportanceTuning importance, LegacyStreakTuning streak)
            => BeginTurn(pacing, importance, streak, Tempo);

        /// <summary>A turn begins resolving: snapshots its pacing and tempo, and resets the ledger, streak and last gap.</summary>
        public void BeginTurn(LegacyShowPacingTuning pacing, LegacySlotImportanceTuning importance, LegacyStreakTuning streak,
            LegacyCombatTempoTuning tempo)
        {
            Pacing = pacing;
            Importance = importance;
            Streak = streak;
            Tempo = tempo;
            Clear();
        }

        /// <summary>Forgets the turn and its slots (a reset, a new battle, the lobby); the numbers stay.</summary>
        public void Clear()
        {
            Saved = Spent = 0f;
            slotRank = latestRank = lastSlotRank = LegacySlotRank.None;
            latestFacts = LegacySlotFacts.None;
            majorStopGiven = bindGiven = readCueGiven = false;
            streakBefore = 0;
            lastSlotSpeed = lastPlaybackSpeed = lastPlaybackGapScale = 1f;
        }

        /// <summary>A slot begins: rates what is known at its start (the kinds, the rolled powers, the met conditions, the
        /// signature moves). A slot still open is ended first.</summary>
        public LegacySlotRank BeginSlot(LegacySlotFacts facts)
        {
            if (HasSlot) EndSlot();
            majorStopGiven = bindGiven = readCueGiven = false;
            lastSlotSpeed = lastPlaybackSpeed = lastPlaybackGapScale = 1f;
            if (!facts.HasSlot) return slotRank;
            latestFacts = facts;
            latestRank = slotRank = LegacySlotImportance.Rate(facts, Importance);
            return slotRank;
        }

        /// <summary>Rates the resolving slot again (after each hit) and keeps the highest: the tier never drops within a
        /// slot of one kind (see <see cref="SlotRank"/> for a change of kind). Facts of another slot begin that slot; no slot
        /// changes nothing.</summary>
        public LegacySlotRank Observe(LegacySlotFacts facts)
        {
            if (!facts.HasSlot) return slotRank;
            if (!HasSlot || slotRank.SlotIndex != facts.SlotIndex) return BeginSlot(facts);
            latestFacts = facts;
            latestRank = LegacySlotImportance.Rate(facts, Importance);
            slotRank = Keep(slotRank, latestRank);
            return slotRank;
        }

        /// <summary>The slot completed: it becomes <see cref="LastSlotRank"/> (the wait after it reads it), and the streak
        /// counts it as it ended.</summary>
        public void EndSlot()
        {
            if (!HasSlot) return;
            lastSlotSpeed = StreakSpeed;
            lastPlaybackSpeed = PlaybackSpeed;
            lastPlaybackGapScale = LegacyCombatTempo.GapScale(latestFacts, Tempo);
            streakBefore = StreakLength;
            lastSlotRank = slotRank;
            slotRank = latestRank = LegacySlotRank.None;
            latestFacts = LegacySlotFacts.None;
            majorStopGiven = bindGiven = readCueGiven = false;
        }

        /// <summary>Whether the resolving slot plays its rest faster: the director is on and the slot is Minor.</summary>
        public bool ShortensSlot => Enabled && Pacing.MinorSpeed > 1f && HasSlot && slotRank.Tier == LegacySlotTier.Minor;

        /// <summary>The rest of the resolving slot after its first hit (battle seconds): shorter by
        /// <see cref="LegacyShowPacingTuning.MinorSpeed"/> for a Minor slot, credited to the turn; otherwise as given.</summary>
        public float ShortenSlotRest(float seconds) => ShortensSlot ? Shorten(seconds) : Positive(seconds);

        /// <summary>The wait after the slot completed last (battle seconds): shorter after a Minor slot, credited to the
        /// turn; otherwise as given.</summary>
        public float ShortenGap(float seconds)
            => Enabled && Pacing.MinorSpeed > 1f && lastSlotRank.SlotIndex >= 0 && lastSlotRank.Tier == LegacySlotTier.Minor
                ? Shorten(seconds) : Positive(seconds);

        /// <summary>Whether this slot's Major moment has come (<see cref="HitStop"/>): its first presented hit.</summary>
        public bool MajorMomentGiven => majorStopGiven;

        /// <summary>The stop for a hit just presented in the resolving slot, given the usual <paramref name="seconds"/>: the
        /// first presented hit of a Major slot is its Major moment (<see cref="MajorMomentGiven"/>) and stops
        /// <see cref="LegacyShowPacingTuning.MajorHitStopScale"/> times as long (a signature move's
        /// <see cref="LegacyShowPacingTuning.SignatureHitStopScale"/>), as far as the turn can afford; every other hit
        /// (Climax's included) keeps the usual.</summary>
        public float HitStop(float seconds)
        {
            seconds = Positive(seconds);
            if (!Enabled || majorStopGiven || !IsMajor) return seconds;
            majorStopGiven = true;
            float scale = IsSignature ? Pacing.SignatureHitStopScale : Pacing.MajorHitStopScale;
            return seconds + Grant(seconds * (scale - 1f));
        }

        /// <summary>The player's 간파 the screen should cue now (<see cref="LegacySlotRank.PlayerReads"/> of the resolving
        /// slot), once per slot: the controllers ask right after each hit, so a read known at the slot's start is cued at its
        /// first hit (the payoff) and one a later hit brings (a break) at that hit. None once given, between slots, or while
        /// the slot has none. It takes no time and holds nothing; in versus each screen cues its own viewer's reads. Whether
        /// the director is switched on does not matter (the rating goes on).</summary>
        public LegacyReadEvent TakeReadCue()
        {
            if (readCueGiven || !HasSlot || slotRank.PlayerReads == LegacyReadEvent.None) return LegacyReadEvent.None;
            readCueGiven = true;
            return slotRank.PlayerReads;
        }

        /// <summary>Whether this slot's 간파 was cued (<see cref="TakeReadCue"/>).</summary>
        public bool ReadCueGiven => readCueGiven;

        /// <summary>Whether the slot as it stands now is an even 합 the bind may hold: attack against attack with a small
        /// gap (<see cref="LegacySlotHighlight.BladeBind"/>), no fall.</summary>
        public bool IsBladeBindCandidate => HasSlot && latestRank.IsBladeBind && latestRank.Kind == LegacyClashKind.Clash &&
            slotRank.Tier != LegacySlotTier.Climax;

        /// <summary>The blade bind's length for the resolving slot (real seconds), or 0: once per slot, only for a
        /// <see cref="IsBladeBindCandidate"/>, only when the turn can still afford
        /// <see cref="LegacyShowPacingTuning.BladeBindMinimumShare"/> of it; then as long as it can afford, up to its full
        /// length. The controller asks at the slot's first hit, when both blades met and neither reached the body.</summary>
        public float BladeBind()
        {
            float seconds = Pacing.BladeBindSeconds;
            if (!Enabled || bindGiven || !IsBladeBindCandidate || !(seconds > 0f)) return 0f;
            // Floats a hair under the share (the cap less what was spent) still count: binary fractions never quite add up.
            if (Available + 1e-5f < seconds * Pacing.BladeBindMinimumShare) return 0f;
            bindGiven = true;
            return Grant(seconds);
        }

        /// <summary>Whether the unopposed streak may speed anything up this turn: the director and the streak are both on.</summary>
        public bool StreakEnabled => Enabled && Streak.Accelerates;

        /// <summary>The turn's unopposed streak (<see cref="LegacyUnopposedStreak"/>): the slots in a row where exactly one
        /// side attacked into nothing, the resolving slot counted as it now stands (its kind is settled at its first hit: a
        /// 반격 filling it or a dodge starts the streak over). Between slots, the streak so far. It counts whether or not
        /// anything speeds up.</summary>
        public int StreakLength => HasSlot ? LegacyUnopposedStreak.Next(streakBefore, latestRank.Kind) : streakBefore;

        /// <summary>How many times faster the resolving slot plays (its playback speed on top of the director's pacing): an
        /// unopposed slot below Major, <see cref="LegacyUnopposedStreak.Speed"/> of <see cref="StreakLength"/>; 1 for any
        /// other slot, between slots, or while switched off. A slot that turns Major plays at 1 from then on. Before the
        /// slot's first hit it is only what the slot's start tells: the controller holds it back while a step or a 반격
        /// could still change the slot. Nothing is banked: the time it saves only shortens the turn.</summary>
        public float StreakSpeed => StreakEnabled && HasSlot
            ? LegacyUnopposedStreak.SlotSpeed(StreakLength, latestRank.Kind, slotRank.Tier, Streak) : 1f;

        /// <summary>How many times faster the wait after the slot that completed last plays: that slot's
        /// <see cref="StreakSpeed"/> as it ended (1 after any other slot, once the next slot begins, or while switched off).</summary>
        public float GapSpeed => StreakEnabled ? lastSlotSpeed : 1f;

        /// <summary>The current slot's progressive speed, including the extra multiplier if exactly one side is Empty.
        /// Breath and guard count as skills, and Major/Climax never remove this tempo. A pending counter may still read
        /// Empty here; controllers preserve input time before its first hit. If tempo is off, uses <see cref="StreakSpeed"/>
        /// alone. Between slots or with the director off, 1. Its savings never enter the emphasis ledger.</summary>
        public float PlaybackSpeed => !Enabled || !HasSlot ? 1f
            : Tempo.Enabled ? LegacyCombatTempo.Speed(latestFacts, Tempo) : StreakSpeed;

        /// <summary>The completed slot's playback speed for the wait after it. Resets when the next slot or turn begins;
        /// with tempo off, uses the legacy <see cref="GapSpeed"/> alone.</summary>
        public float PlaybackGapSpeed => !Enabled ? 1f : Tempo.Enabled ? lastPlaybackSpeed : GapSpeed;

        /// <summary>The share of the wait after the completed slot that remains: shorter only after an attack against an
        /// empty side. Controllers apply it after <see cref="ShortenGap"/> without crediting its savings to the ledger.
        /// Resets when the next slot or turn begins, or on clear; 1 with the director or progressive tempo off.</summary>
        public float PlaybackGapScale => Enabled && Tempo.Enabled ? lastPlaybackGapScale : 1f;

        // What only adds up as a slot plays (an attack, a dodge, a break, a heavy hit, a fall, a 간파, a signature move). The
        // rest (a 합's gap, a deep pierce, a big unopposed attack, a bind) tells how the cards meet: it belongs to one kind.
        private const LegacySlotHighlight Lasting = LegacySlotHighlight.Attack | LegacySlotHighlight.Dodge |
            LegacySlotHighlight.Break | LegacySlotHighlight.HeavyHit | LegacySlotHighlight.PlayerRead |
            LegacySlotHighlight.EnemyRead | LegacySlotHighlight.Signature | LegacySlotHighlight.FinishingBlow;

        // Of one kind: the highest tier (then score), everything seen stays seen. When the kind changes (a 반격 filling the
        // slot or a dodge, at its first hit), what the old kind told about how the cards met goes with it (a pending 반격's
        // empty side made the enemy's attack look unopposed): the new kind's rating, plus what only adds up, rated afresh.
        private LegacySlotRank Keep(LegacySlotRank kept, LegacySlotRank latest)
        {
            LegacyReadEvent playerReads = kept.PlayerReads | latest.PlayerReads, enemyReads = kept.EnemyReads | latest.EnemyReads;
            bool playerSignature = kept.PlayerSignature || latest.PlayerSignature;
            bool enemySignature = kept.EnemySignature || latest.EnemySignature;
            if (latest.Kind == kept.Kind)
                return new LegacySlotRank(latest.SlotIndex, latest.Tier > kept.Tier ? latest.Tier : kept.Tier,
                    Math.Max(kept.Score, latest.Score), kept.Highlights | latest.Highlights, playerReads, enemyReads,
                    playerSignature, enemySignature, latest.Kind);
            LegacySlotHighlight highlights = latest.Highlights | (kept.Highlights & Lasting);
            // The latest score already counts the health lost, the breaks and a fall; add the reads and signatures only the
            // old kind saw.
            int score = latest.Score +
                LegacySlotImportance.Count(playerReads & ~latest.PlayerReads) * LegacySlotImportance.PlayerReadPoints +
                LegacySlotImportance.Count(enemyReads & ~latest.EnemyReads) * LegacySlotImportance.EnemyReadPoints +
                (playerSignature && !latest.PlayerSignature ? LegacySlotImportance.SignaturePoints : 0) +
                (enemySignature && !latest.EnemySignature ? LegacySlotImportance.SignaturePoints : 0);
            return new LegacySlotRank(latest.SlotIndex, LegacySlotImportance.TierOf(highlights, Importance), score, highlights,
                playerReads, enemyReads, playerSignature, enemySignature, latest.Kind);
        }

        private float Shorten(float seconds)
        {
            seconds = Positive(seconds);
            float shorter = seconds / Pacing.MinorSpeed;
            Saved += seconds - shorter;
            return shorter;
        }

        private float Grant(float seconds)
        {
            float granted = Math.Min(Positive(seconds), Available);
            Spent += granted;
            return granted;
        }

        private static float Positive(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }

    /// <summary>The blade bind's motion (팽팽한 합, presentation only): attack against attack with a small gap holds the two
    /// fighters at their contact frame for a short struggle. Both lean into each other by the same amount, and the locked
    /// pair sways together one way and then the other just as far (neither drives the other off: both dealt damage), with
    /// sparks at the contact; at the end both spring apart by the same distance. Pure: the offsets are a function of the
    /// share of the bind played (0 to 1), and they are 0 at both ends.</summary>
    public static class LegacyBladeBind
    {
        /// <summary>The defaults: the bind's real seconds, the pair's sway and each fighter's lean (world units), the extra
        /// spring apart each fighter gets at the end on top of the hit's own knock-back, shared out evenly (world units; none
        /// by default, so a bind never leaves the two farther apart than the hit did), and the sparks it strikes.</summary>
        public const float DefaultSeconds = .45f, DefaultSway = .16f, DefaultPress = .1f, DefaultSpring = 0f;
        public const int DefaultSparks = 2;
        /// <summary>Full swings of the sway over the bind: whole swings, so the pair goes as far one way as the other.</summary>
        public const int SwayCycles = 2;
        /// <summary>The lean comes in over this first share of the bind and goes out over the same last share.</summary>
        public const float PressShare = .2f;

        /// <summary>The locked pair's shared sway along the line <paramref name="t"/> through the bind (both fighters move by
        /// it, the same way): a few even swings that grow and die away, as far one way as the other (the curve read from the
        /// end is the same swing negated), 0 at both ends.</summary>
        public static float Sway(float t, float amplitude)
        {
            t = Clamp01(t);
            if (t <= 0f || t >= 1f || !(amplitude > 0f)) return 0f;
            return amplitude * (float)(Math.Sin(Math.PI * t) * Math.Sin(2.0 * Math.PI * SwayCycles * t));
        }

        /// <summary>How far each fighter leans into the other <paramref name="t"/> through the bind: the same for both,
        /// easing in over <see cref="PressShare"/>, held, and easing out by the end; 0 at both ends.</summary>
        public static float Press(float t, float amount)
        {
            t = Clamp01(t);
            if (t <= 0f || t >= 1f || !(amount > 0f)) return 0f;
            float edge = Math.Min(t, 1f - t) / PressShare;
            if (edge >= 1f) return amount;
            return amount * edge * edge * (3f - 2f * edge);
        }

        /// <summary>Each fighter's offset along the line (world x, right positive; the player stands on the left)
        /// <paramref name="t"/> through the bind: the shared sway, plus each one's lean toward the other.</summary>
        public static void Offsets(float t, float sway, float press, out float player, out float enemy)
        {
            float shared = Sway(t, sway), lean = Press(t, press);
            player = shared + lean;
            enemy = shared - lean;
        }

        /// <summary>When the <paramref name="index"/>-th of <paramref name="count"/> sparks strikes (a share of the bind):
        /// evenly spread, never at its ends.</summary>
        public static float SparkMoment(int index, int count) => count > 0 ? (index + 1f) / (count + 1f) : 1f;

        /// <summary>How many of <paramref name="count"/> spark moments fall after <paramref name="before"/> and by
        /// <paramref name="after"/> (shares of the bind).</summary>
        public static int SparksBetween(float before, float after, int count)
        {
            int sparks = 0;
            for (int index = 0; index < count; index++)
            {
                float moment = SparkMoment(index, count);
                if (before < moment && after >= moment) sparks++;
            }
            return sparks;
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
