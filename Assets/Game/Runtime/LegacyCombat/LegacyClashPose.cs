using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>A queue card's offset from its place in its row as the clash motion moves it (<see cref="LegacyClashPose"/>):
    /// forward toward the other side's card (the centre seam), up, a tilt in degrees (positive leans the card back, away from
    /// the seam) and a squash (its scale less 1, never above 0: a card is never drawn larger than it rests). Rest is all
    /// zero. HUD units at the 1920x1080 reference.</summary>
    public readonly struct LegacyCardPose : IEquatable<LegacyCardPose>
    {
        public LegacyCardPose(float forward, float up, float tilt = 0f, float squash = 0f)
        {
            Forward = Finite(forward);
            Up = Finite(up);
            Tilt = Finite(tilt);
            squash = Finite(squash);
            Squash = squash > 0f ? 0f : squash < -1f ? -1f : squash;
        }

        /// <summary>The card where its row lays it out.</summary>
        public static LegacyCardPose Rest => default;

        public float Forward { get; }
        public float Up { get; }
        public float Tilt { get; }
        public float Squash { get; }

        /// <summary>Whether the card stands where its row lays it out (within a thousandth of a unit).</summary>
        public bool IsRest => Math.Abs(Forward) < 1e-3f && Math.Abs(Up) < 1e-3f && Math.Abs(Tilt) < 1e-3f && Math.Abs(Squash) < 1e-4f;

        /// <summary>The same pose shaken <paramref name="shake"/> units along the row, tilting with it.</summary>
        public LegacyCardPose Shaken(float shake)
            => shake == 0f ? this : new LegacyCardPose(Forward + shake, Up, Tilt + shake * LegacyClashPose.ShakeTiltPerUnit, Squash);

        /// <summary>Between <paramref name="from"/> (0) and <paramref name="to"/> (1).</summary>
        public static LegacyCardPose Lerp(LegacyCardPose from, LegacyCardPose to, float t)
        {
            t = float.IsNaN(t) ? 0f : t < 0f ? 0f : t > 1f ? 1f : t;
            return new LegacyCardPose(from.Forward + (to.Forward - from.Forward) * t, from.Up + (to.Up - from.Up) * t,
                from.Tilt + (to.Tilt - from.Tilt) * t, from.Squash + (to.Squash - from.Squash) * t);
        }

        public bool Equals(LegacyCardPose other)
            => Forward.Equals(other.Forward) && Up.Equals(other.Up) && Tilt.Equals(other.Tilt) && Squash.Equals(other.Squash);

        public override bool Equals(object obj) => obj is LegacyCardPose other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Forward.GetHashCode();
                hash = hash * 31 + Up.GetHashCode();
                hash = hash * 31 + Tilt.GetHashCode();
                return hash * 31 + Squash.GetHashCode();
            }
        }

        public static bool operator ==(LegacyCardPose left, LegacyCardPose right) => left.Equals(right);
        public static bool operator !=(LegacyCardPose left, LegacyCardPose right) => !left.Equals(right);

        public override string ToString()
            => "forward " + Forward.ToString("0.##") + ", up " + Up.ToString("0.##") + ", tilt " + Tilt.ToString("0.##") +
                ", squash " + Squash.ToString("0.###");

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    /// <summary>The clash motion's numbers (<see cref="LegacyClashPose"/>): distances in HUD units, times in battle seconds
    /// (they follow bullet time, the decisive slow motion and the hit stop). The presentation settings hold them.</summary>
    public readonly struct LegacyClashTuning
    {
        /// <param name="settleDip">How far a settling card dips (HUD units, at least 0).</param>
        /// <param name="braceSquash">A bracing guard's squash: its scale less 1, from -0.5 to 0 (never larger than at rest).</param>
        public LegacyClashTuning(float approachSeconds, float approachDistance, float impactSeconds, float reach,
            float pushDistance, float knockbackDistance, float sideStepDistance, float tiltDegrees,
            float breakShake, float breakShakeSeconds, float settleDip = LegacyClashPose.SettleDip,
            float braceSquash = LegacyClashPose.BraceSquash)
        {
            ApproachSeconds = NonNegative(approachSeconds);
            ApproachDistance = NonNegative(approachDistance);
            ImpactSeconds = Math.Max(LegacyClashPose.MinimumImpactSeconds, NonNegative(impactSeconds));
            Reach = NonNegative(reach);
            PushDistance = NonNegative(pushDistance);
            KnockbackDistance = NonNegative(knockbackDistance);
            SideStepDistance = NonNegative(sideStepDistance);
            TiltDegrees = NonNegative(tiltDegrees);
            BreakShake = NonNegative(breakShake);
            BreakShakeSeconds = NonNegative(breakShakeSeconds);
            SettleDip = NonNegative(settleDip);
            BraceSquash = float.IsNaN(braceSquash) || braceSquash > 0f ? 0f : braceSquash < -.5f ? -.5f : braceSquash;
        }

        /// <summary>The defaults (<see cref="LegacyClashPose"/>'s constants).</summary>
        public static LegacyClashTuning Default => new LegacyClashTuning(LegacyClashPose.DefaultApproachSeconds,
            LegacyClashPose.DefaultApproachDistance, LegacyClashPose.DefaultImpactSeconds, LegacyClashPose.DefaultReach,
            LegacyClashPose.DefaultPushDistance, LegacyClashPose.DefaultKnockbackDistance, LegacyClashPose.DefaultSideStepDistance,
            LegacyClashPose.DefaultTiltDegrees, LegacyClashPose.DefaultBreakShake, LegacyClashPose.DefaultBreakShakeSeconds);

        /// <summary>How quickly the cards lean in as the slot starts (they reach about 95% in this time).</summary>
        public float ApproachSeconds { get; }
        /// <summary>How far an attacking card leans toward the seam before the slot's first hit.</summary>
        public float ApproachDistance { get; }
        /// <summary>The whole meeting from the slot's first hit: strike, push and knock-back, a short hold, back to rest.</summary>
        public float ImpactSeconds { get; }
        /// <summary>The farthest a card travels to meet the other: wider gaps (the coach's seam) are not closed.</summary>
        public float Reach { get; }
        /// <summary>How far the stronger card drives on past the meeting at full strength (a pierce, a 합's winner), and the
        /// scale of the smaller moves (a bounce, a rebound, a whiff's overshoot).</summary>
        public float PushDistance { get; }
        /// <summary>How far behind its place a knocked-back card goes at full strength.</summary>
        public float KnockbackDistance { get; }
        /// <summary>How far a dodging card steps up out of the attack's way.</summary>
        public float SideStepDistance { get; }
        /// <summary>The largest tilt a knocked-back card takes (degrees).</summary>
        public float TiltDegrees { get; }
        /// <summary>A break's shake: its largest swing along the row and how long it lasts (0 for none).</summary>
        public float BreakShake { get; }
        public float BreakShakeSeconds { get; }
        /// <summary>How far a card with no attack to meet (<see cref="LegacyClashKind.Settle"/>) dips as the slot plays.</summary>
        public float SettleDip { get; }
        /// <summary>How much a guard squashes as the attack lands on it (its scale less 1, -0.5 to 0); a pierced guard and a
        /// settling card squash half as much, a bracing guard keeps 40% of it at its peak.</summary>
        public float BraceSquash { get; }

        private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }

    /// <summary>How the resolving pair of queue cards moves at the centre seam (presentation only; the rules never read it):
    /// the cards of a classified slot (<see cref="LegacyClashMotion.Classify"/>) lean in as it starts, and from its first hit
    /// meet, push or are knocked back by the classification's strength, hold a moment and go back to rest. Pure: a pose is a
    /// function of the move, its strength, where the card started, how far it may travel and the time.</summary>
    public static class LegacyClashPose
    {
        public const float DefaultApproachSeconds = .1f, DefaultApproachDistance = 8f, DefaultImpactSeconds = .44f,
            DefaultReach = 64f, DefaultPushDistance = 12f, DefaultKnockbackDistance = 22f, DefaultSideStepDistance = 24f,
            DefaultTiltDegrees = 8f, DefaultBreakShake = 5f, DefaultBreakShakeSeconds = .32f;
        /// <summary>The shortest meeting the tuning allows (battle seconds).</summary>
        public const float MinimumImpactSeconds = .05f;
        /// <summary>The meeting's beats as shares of <see cref="LegacyClashTuning.ImpactSeconds"/>: the cards meet at
        /// <see cref="StrikeShare"/> (the contact spark), drive to the push or knock-back by <see cref="DriveEnd"/>, hold until
        /// <see cref="HoldEnd"/> and are back at rest at 1.</summary>
        public const float StrikeShare = .14f, DriveEnd = .46f, HoldEnd = .56f;
        /// <summary>A knocked-back or pushing card's least share of its full distance: even the narrowest win reads.</summary>
        public const float MinimumWeight = .3f;
        /// <summary>The defaults of <see cref="LegacyClashTuning.BraceSquash"/> (a guard taking the blow squashes this much)
        /// and <see cref="LegacyClashTuning.SettleDip"/> (a settle dips this far), and a settle's squash (half the brace's).</summary>
        public const float BraceSquash = -.06f, SettleDip = 3f, SettleSquash = BraceSquash * .5f;
        /// <summary>A break's shake: its swings per second, and the tilt (degrees) each unit of swing gives.</summary>
        public const float ShakeHertz = 22f, ShakeTiltPerUnit = .6f;
        /// <summary>A card that has left its row (its slot was completed mid-meeting) fades from here to the meeting's end.</summary>
        public const float GhostFadeStart = HoldEnd;
        // The approach follows its target this fast: about 95% of the way in ApproachSeconds.
        private const float FollowRate = 3f;

        /// <summary>Whether the card travels to meet the other: an attack (pushing, recoiling or bouncing in a 합, thrusting at
        /// or rebounding off a guard, lunging, whiffing). A pierced guard, a brace, a dodge, a settle and an empty side stay.</summary>
        public static bool Advances(LegacyClashKind kind, LegacyClashMove move)
        {
            switch (move)
            {
                case LegacyClashMove.Push:
                case LegacyClashMove.Bounce:
                case LegacyClashMove.Thrust:
                case LegacyClashMove.Rebound:
                case LegacyClashMove.Lunge:
                case LegacyClashMove.Whiff:
                    return true;
                case LegacyClashMove.Recoil:
                    return kind == LegacyClashKind.Clash;
                default:
                    return false;
            }
        }

        /// <summary>How far a card travels to its meeting, never past <paramref name="reach"/>: a 합's two cards meet at the seam
        /// (each crosses its own gap, <paramref name="ownGap"/>: from its front edge to the seam), an attack on a guard and a
        /// whiff go on to where the other card's front stands (both gaps), a lunge into nothing stops at the seam. 0 for a
        /// card that does not advance.</summary>
        public static float ContactTravel(LegacyClashKind kind, LegacyClashMove move, float ownGap, float otherGap, float reach)
        {
            if (!Advances(kind, move)) return 0f;
            float own = NonNegative(ownGap), other = NonNegative(otherGap);
            float travel = move == LegacyClashMove.Thrust || move == LegacyClashMove.Rebound || move == LegacyClashMove.Whiff
                ? own + other : own;
            return Math.Min(travel, NonNegative(reach));
        }

        /// <summary>Where a card leans before the slot's first hit: an advancing card toward the seam (never past its meeting,
        /// <paramref name="contact"/>), a dodger already stepping up; anything else rests.</summary>
        public static LegacyCardPose ApproachTarget(LegacyClashKind kind, LegacyClashMove move, float contact, LegacyClashTuning tuning)
        {
            if (move == LegacyClashMove.SideStep) return new LegacyCardPose(0f, tuning.SideStepDistance);
            if (!Advances(kind, move)) return LegacyCardPose.Rest;
            return new LegacyCardPose(Math.Min(tuning.ApproachDistance, NonNegative(contact)), 0f);
        }

        /// <summary><paramref name="current"/> moved toward <paramref name="target"/> over <paramref name="delta"/> battle
        /// seconds, easing in so a target that changes (a dodge, a 반격 settling) never jumps: about 95% of the way in
        /// <paramref name="seconds"/>, at once when that is 0. It never overshoots.</summary>
        public static LegacyCardPose Follow(LegacyCardPose current, LegacyCardPose target, float delta, float seconds)
        {
            if (!(delta > 0f)) return current;
            if (!(seconds > 0f) || float.IsInfinity(delta)) return target;
            float share = 1f - (float)Math.Exp(-delta * FollowRate / seconds);
            return LegacyCardPose.Lerp(current, target, share);
        }

        /// <summary>Where the card stands as the two meet (the meeting's <see cref="StrikeShare"/>): an advancing card at its
        /// <paramref name="contact"/>, a guard squashed under the blow, a dodger stepped up, a settling card dipped.</summary>
        public static LegacyCardPose Meet(LegacyClashKind kind, LegacyClashMove move, float contact, LegacyClashTuning tuning)
        {
            if (Advances(kind, move)) return new LegacyCardPose(NonNegative(contact), 0f);
            switch (move)
            {
                case LegacyClashMove.Recoil: return new LegacyCardPose(0f, 0f, 0f, tuning.BraceSquash * .5f);
                case LegacyClashMove.Brace: return new LegacyCardPose(0f, 0f, 0f, tuning.BraceSquash);
                case LegacyClashMove.SideStep: return new LegacyCardPose(0f, tuning.SideStepDistance);
                case LegacyClashMove.Settle: return new LegacyCardPose(0f, -tuning.SettleDip, 0f, tuning.BraceSquash * .5f);
                default: return LegacyCardPose.Rest;
            }
        }

        /// <summary>Where the move takes the card after the meeting (the meeting's <see cref="DriveEnd"/> to
        /// <see cref="HoldEnd"/>), by its <paramref name="strength"/> (0 to 1, <see cref="LegacyClash.PlayerStrength"/>): the
        /// winner of a 합 and a piercing attack drive on past the meeting, a 합's loser and a pierced guard are knocked back
        /// behind their place and tilt away, an even 합 bounces both back a little, a held attack rebounds by the guard's
        /// margin while the guard budges less the stronger it held, a lunge holds at the seam, a whiff overshoots, a dodger
        /// stays up, a settle comes back up.</summary>
        public static LegacyCardPose Peak(LegacyClashMove move, float strength, float contact, LegacyClashTuning tuning)
        {
            float s = Clamp01(strength), weight = MinimumWeight + (1f - MinimumWeight) * s;
            contact = NonNegative(contact);
            switch (move)
            {
                case LegacyClashMove.Push:
                case LegacyClashMove.Thrust:
                    return new LegacyCardPose(contact + tuning.PushDistance * weight, 0f, -tuning.TiltDegrees * .25f * weight);
                case LegacyClashMove.Recoil:
                    return new LegacyCardPose(-tuning.KnockbackDistance * weight, 0f, tuning.TiltDegrees * weight);
                case LegacyClashMove.Bounce:
                    return new LegacyCardPose(-tuning.PushDistance * .5f, 0f, tuning.TiltDegrees * .25f);
                case LegacyClashMove.Rebound:
                    return new LegacyCardPose(-tuning.PushDistance * (.4f + .6f * s), 0f, tuning.TiltDegrees * (.15f + .35f * s));
                case LegacyClashMove.Brace:
                    return new LegacyCardPose(-tuning.PushDistance * .3f * (1f - s), 0f, 0f, tuning.BraceSquash * .4f);
                case LegacyClashMove.Lunge:
                    return new LegacyCardPose(contact, 0f, -tuning.TiltDegrees * .15f);
                case LegacyClashMove.Whiff:
                    return new LegacyCardPose(contact + tuning.PushDistance * .8f, 0f, -tuning.TiltDegrees * .3f);
                case LegacyClashMove.SideStep:
                    return new LegacyCardPose(-tuning.PushDistance * .2f, tuning.SideStepDistance);
                case LegacyClashMove.Settle:
                    return new LegacyCardPose(0f, -tuning.SettleDip * .3f);
                default:
                    return LegacyCardPose.Rest;
            }
        }

        /// <summary>The card <paramref name="t"/> (0 to 1) through its meeting, from where its approach left it
        /// (<paramref name="start"/>): it accelerates into the meeting (<see cref="StrikeShare"/>), drives out to its
        /// <see cref="Peak"/> (<see cref="DriveEnd"/>), holds (<see cref="HoldEnd"/>) and eases back to rest, exactly at rest
        /// from 1 on.</summary>
        public static LegacyCardPose Impact(LegacyClashKind kind, LegacyClashMove move, float strength, LegacyCardPose start,
            float contact, float t, LegacyClashTuning tuning)
        {
            t = float.IsNaN(t) ? 0f : t;
            if (t >= 1f) return LegacyCardPose.Rest;
            if (t <= 0f) return start;
            LegacyCardPose meet = Meet(kind, move, contact, tuning);
            if (t < StrikeShare) return LegacyCardPose.Lerp(start, meet, InQuad(t / StrikeShare));
            LegacyCardPose peak = Peak(move, strength, contact, tuning);
            if (t < DriveEnd) return LegacyCardPose.Lerp(meet, peak, OutCubic((t - StrikeShare) / (DriveEnd - StrikeShare)));
            if (t < HoldEnd) return peak;
            return LegacyCardPose.Lerp(peak, LegacyCardPose.Rest, InOutQuad((t - HoldEnd) / (1f - HoldEnd)));
        }

        /// <summary>Where the meeting is, 0 to 1, <paramref name="elapsed"/> battle seconds after the slot's first hit.</summary>
        public static float Progress(float elapsed, float seconds)
            => seconds > 0f && !float.IsInfinity(seconds) ? Clamp01(elapsed / seconds) : 1f;

        /// <summary>Whether the cards meet between <paramref name="before"/> and <paramref name="after"/> seconds into the
        /// meeting: the moment of the contact spark (never once the meeting is over).</summary>
        public static bool ReachesContact(float before, float after, float seconds)
        {
            if (!(seconds > 0f)) return false;
            float moment = StrikeShare * seconds;
            return before < moment && after >= moment && before < seconds;
        }

        /// <summary>The contact spark's size against its tuned size: larger the more one side came out ahead (a 합's gap, a
        /// pierce, a block's margin); 0 for a slot whose cards never touch.</summary>
        public static float SparkScale(LegacyClash clash)
            => clash.Contact ? .8f + .4f * Math.Max(clash.PlayerStrength, clash.EnemyStrength) : 0f;

        /// <summary>A break's shake along the row <paramref name="elapsed"/> seconds after it: swinging fast and dying away
        /// over <paramref name="seconds"/>, never wider than <paramref name="amplitude"/>; 0 before and after.</summary>
        public static float Shake(float elapsed, float seconds, float amplitude)
        {
            if (!(seconds > 0f) || !(amplitude > 0f) || !(elapsed >= 0f) || elapsed >= seconds) return 0f;
            float left = 1f - elapsed / seconds;
            return amplitude * left * left * (float)Math.Sin(elapsed * ShakeHertz * 2.0 * Math.PI);
        }

        /// <summary>The opacity of a card that has left its row while its meeting still plays: whole until
        /// <see cref="GhostFadeStart"/>, gone at the meeting's end.</summary>
        public static float GhostAlpha(float t)
        {
            t = Clamp01(t);
            if (t <= GhostFadeStart) return 1f;
            if (t >= 1f) return 0f;
            float share = (t - GhostFadeStart) / (1f - GhostFadeStart);
            return 1f - share * share * (3f - 2f * share);
        }

        private static float InQuad(float t)
        {
            t = Clamp01(t);
            return t * t;
        }

        private static float OutCubic(float t)
        {
            t = 1f - Clamp01(t);
            return 1f - t * t * t;
        }

        private static float InOutQuad(float t)
        {
            t = Clamp01(t);
            return t < .5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
        }

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;

        private static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }
}
