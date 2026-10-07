using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>The battle dock's skill gears (presentation only; the combat rules never read it). Each open lane is a
    /// brass gear whose skills sit in sockets 60° apart on its rim, the lane's order repeated round it, and only its upper
    /// part shows above the dock's bottom edge: the current skill at the top (in a fixed window), the next at the upper
    /// left and the one just used or sent back at the upper right. Queueing a skill turns its lane's gear one slot
    /// clockwise (items go left, top, right); 넘기기 turns every open gear together, the small idlers between them turning
    /// the other way, and ends on a ratchet's bounce. These are its geometry (slot angles, how each slot looks, packing,
    /// where an idler meshes and how wide the gears may stand for it), its turn curves and how far a lane's order turned between two looks. No text: the gear is a
    /// motif of the picture (<c>Docs/Narrative.md</c>).</summary>
    public static class LegacySkillGear
    {
        /// <summary>Sockets round a gear's rim, and the angle between two (one slot, one turn).</summary>
        public const int SlotCount = 6;
        public const float SlotDegrees = 360f / SlotCount;
        /// <summary>Where the current skill sits: the top of the gear (degrees, counter-clockwise from the right).</summary>
        public const float TopAngle = 90f;
        /// <summary>Slots counted from the top: the current skill, the next (one slot back, upper left), the one just used
        /// or sent back (one slot on, upper right) and the one before it, on its way down out of sight.</summary>
        public const int CurrentSlot = 0, NextSlot = 1, UsedSlot = -1, SpentSlot = -2;

        /// <summary>How big and how opaque a skill shows at the next and used slots, against the current one; below the
        /// arc's ends it shrinks and fades a little more on its way out of sight (the dock's edge hides it anyway).</summary>
        public const float NextScale = .8f, UsedScale = .68f, HiddenScale = .6f;
        public const float NextAlpha = .95f, UsedAlpha = .5f, HiddenAlpha = .3f;

        /// <summary>A gear's tip radius as a share of its picture's half-size (as <see cref="LegacyGearShimmer.TipRadius"/>):
        /// the rest is room for the soft edge.</summary>
        public const float TipShare = .96f;
        /// <summary>How far inside the tooth tips the sockets' centres sit (HUD units).</summary>
        public const float SocketInset = 16f;

        // The tunables' defaults (DuelPresentationSettings): sizes in HUD units at the 1920x1080 reference, times in real
        // seconds, the bounce in degrees.
        public const float DefaultRadius = 84f, DefaultPitch = 190f, DefaultToothDepth = 10f, DefaultIdlerRadius = 26f;
        public const int DefaultTeethPerSlot = 4;
        public const float DefaultTurnSeconds = .3f, DefaultShiftSeconds = .4f, DefaultRatchetBounce = 6f;
        public const float DefaultTickVolume = .5f, DefaultRatchetVolume = .6f;

        /// <summary>The share of a 넘기기 turn spent turning (past the slot by the bounce); the rest settles back.</summary>
        public const float RatchetTurnShare = .7f;

        /// <summary>Where slot <paramref name="slot"/> (counted from the top, <see cref="NextSlot"/> back and
        /// <see cref="UsedSlot"/> on) is, in degrees counter-clockwise from the right, while the gear still has
        /// <paramref name="remaining"/> degrees of a turn to go (0 at rest).</summary>
        public static float SlotAngle(int slot, float remaining) => TopAngle + slot * SlotDegrees + remaining;

        /// <summary>A point on a rim of <paramref name="radius"/> at <paramref name="angle"/> degrees, from the gear's centre
        /// (y up).</summary>
        public static float SlotX(float angle, float radius) => radius * (float)Math.Cos(angle * Math.PI / 180.0);
        public static float SlotY(float angle, float radius) => radius * (float)Math.Sin(angle * Math.PI / 180.0);

        /// <summary>The socket's radius on a gear of tip radius <paramref name="gearRadius"/>.</summary>
        public static float SlotRadius(float gearRadius) => gearRadius - SocketInset;

        /// <summary>Which of a lane's <paramref name="laneCount"/> skills (its order, front first) a slot shows: the order
        /// repeated round the rim, so the top shows the front, the next slot the second and the used slot the last (the
        /// one just sent to the back). -1 for an empty lane.</summary>
        public static int SkillIndex(int slot, int laneCount)
        {
            if (laneCount <= 0) return -1;
            int index = slot % laneCount;
            return index < 0 ? index + laneCount : index;
        }

        /// <summary>A skill's size at <paramref name="angle"/>, against the current one's: the largest at the top, the next
        /// slot larger than the used one, smaller still past the arc's ends.</summary>
        public static float SlotScale(float angle) => ArcStyle(angle, 1f, NextScale, UsedScale, HiddenScale);

        /// <summary>A skill's opacity at <paramref name="angle"/>: whole at the top, the next nearly so, the used one dimmed.</summary>
        public static float SlotAlpha(float angle) => ArcStyle(angle, 1f, NextAlpha, UsedAlpha, HiddenAlpha);

        /// <summary>How much of an icon <paramref name="half"/> units from its centre to its edge shows at
        /// <paramref name="angle"/> on a rim of <paramref name="radius"/>, above the line through the gear's centre (where the
        /// dock hides the rest): 0 hidden to 1 whole.</summary>
        public static float Showing(float angle, float radius, float half)
        {
            float y = SlotY(angle, radius);
            if (!(half > 0f)) return y > 0f ? 1f : 0f;
            return Clamp01((y + half) / (2f * half));
        }

        /// <summary>The x of the lane at <paramref name="index"/> among <paramref name="count"/> open lanes packed
        /// <paramref name="pitch"/> apart and centred: one at 0, two either side of it, three at -pitch, 0, +pitch.</summary>
        public static float LaneX(int index, int count, float pitch) => (index - (count - 1) * .5f) * pitch;

        /// <summary>The pitch to pack <paramref name="count"/> lanes at: <paramref name="pitch"/>, or less when the outer
        /// lanes (<paramref name="laneHalfWidth"/> either side of their centres) would pass <paramref name="room"/> from the
        /// middle (the buttons beside the gears).</summary>
        public static float FitPitch(int count, float pitch, float laneHalfWidth, float room)
        {
            if (count < 2) return pitch;
            float most = (room - laneHalfWidth) / ((count - 1) * .5f);
            return Math.Max(0f, Math.Min(pitch, most));
        }

        /// <summary>A lane gear's teeth: the same number between every two sockets, so a turn of one slot leaves its teeth
        /// where they were and it still meshes once it settles.</summary>
        public static int GearTeeth(int teethPerSlot) => SlotCount * Math.Max(1, Math.Min(12, teethPerSlot));

        /// <summary>An idler's teeth, for teeth the same size as the lane gears' (their pitch circles in proportion).</summary>
        public static int IdlerTeeth(int gearTeeth, float gearRadius, float idlerRadius, float toothDepth)
        {
            float gearPitch = gearRadius - toothDepth * .5f, idlerPitch = idlerRadius - toothDepth * .5f;
            if (!(gearPitch > 0f) || !(idlerPitch > 0f)) return 3;
            return Math.Max(3, (int)Math.Round(gearTeeth * idlerPitch / gearPitch, MidpointRounding.AwayFromZero));
        }

        /// <summary>How far an idler turns (degrees, the other way) while the lane gears turn one slot.</summary>
        public static float IdlerTurnPerSlot(int gearTeeth, int idlerTeeth) => SlotDegrees * gearTeeth / Math.Max(1, idlerTeeth);

        /// <summary>How high above the gears' centres an idler sits midway between two lane gears
        /// <paramref name="pitch"/> apart so it meshes with both (their pitch circles touching): 0 when it cannot reach them,
        /// and then it rests on the centre line.</summary>
        public static float IdlerHeight(float pitch, float gearRadius, float idlerRadius, float toothDepth)
        {
            float reach = gearRadius + idlerRadius - toothDepth, half = pitch * .5f;
            return reach > half ? (float)Math.Sqrt(reach * reach - half * half) : 0f;
        }

        /// <summary>The widest pitch at which an idler between two lane gears still meshes with both and shows whole above
        /// their centre line (the dock's edge): its centre at least its own radius above it. Wider, it would sink to the line
        /// and then fall short of them (<see cref="IdlerHeight"/>). 0 when an idler that size cannot reach them at all.</summary>
        public static float MeshedPitch(float gearRadius, float idlerRadius, float toothDepth)
        {
            float reach = gearRadius + idlerRadius - toothDepth;
            if (!(idlerRadius > 0f) || !(reach > idlerRadius)) return 0f;
            return 2f * (float)Math.Sqrt(reach * reach - idlerRadius * idlerRadius);
        }

        /// <summary>The rotation (degrees, counter-clockwise) gear B needs for its teeth to fall between gear A's where they
        /// meet: A turned <paramref name="phaseA"/> with <paramref name="teethA"/> teeth, B with <paramref name="teethB"/>,
        /// B's centre seen from A's at <paramref name="contactAngle"/> degrees. Both are drawn with a tooth half a pitch past
        /// their zero angle (<see cref="LegacyGearShimmer.GearCoverage(float, float, float, int, float, float, float, float, float, int)"/>).</summary>
        public static float MeshPhase(float contactAngle, float phaseA, int teethA, int teethB)
        {
            float pitchA = 360f / Math.Max(1, teethA), pitchB = 360f / Math.Max(1, teethB);
            // How far the contact line is past A's nearest tooth, in A's pitches (-.5 to .5). B's gap sits as far past the
            // contact on B's side: the two rims run opposite ways where they touch.
            float offset = (contactAngle - phaseA) / pitchA - .5f;
            offset -= (float)Math.Floor(offset + .5f);
            return Wrap(contactAngle + 180f + offset * pitchB);
        }

        /// <summary>A queue's turn: how much of its slot the gear has turned after <paramref name="elapsed"/> of
        /// <paramref name="seconds"/>, 0 to 1, quick at first and settling (1 when it has no length).</summary>
        public static float TurnShare(float elapsed, float seconds)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) return 1f;
            float u = 1f - Clamp01(elapsed / seconds);
            return 1f - u * u * u;
        }

        /// <summary>넘기기's turn: like <see cref="TurnShare"/> but it runs <paramref name="bounceDegrees"/> past the slot and
        /// springs back, a ratchet's catch, settling on exactly 1 at the end.</summary>
        public static float ShiftShare(float elapsed, float seconds, float bounceDegrees)
        {
            if (!(seconds > 0f) || float.IsInfinity(seconds)) return 1f;
            float t = Clamp01(elapsed / seconds);
            float bounce = bounceDegrees > 0f && !float.IsInfinity(bounceDegrees) ? bounceDegrees / SlotDegrees : 0f;
            if (t < RatchetTurnShare)
            {
                float u = 1f - t / RatchetTurnShare;
                return (1f + bounce) * (1f - u * u * u);
            }
            float v = (t - RatchetTurnShare) / (1f - RatchetTurnShare);
            return 1f + bounce * (1f - v) * (float)Math.Cos(2.0 * Math.PI * v);
        }

        /// <summary>How many slots a lane's gear turned between two looks at its order (skill ids, front first). An
        /// <paramref name="announced"/> count (the controller turned it: a queue or 넘기기) is taken when the orders agree
        /// with it, which is how a one-skill lane, or one whose turn looks the same, still turns. Otherwise the smallest
        /// turn that makes the old order the new one; 0 when it did not turn, or when the new order is not the old one
        /// turned at all (a new duel or loadout: the gear just shows it).</summary>
        public static int TurnsBetween(IReadOnlyList<int> previous, IReadOnlyList<int> current, int announced)
        {
            if (previous == null || current == null || previous.Count == 0 || previous.Count != current.Count) return 0;
            int count = current.Count;
            if (announced > 0 && Turned(previous, current, announced % count)) return announced;
            if (Turned(previous, current, 0)) return 0;
            for (int turns = 1; turns < count; turns++)
                if (Turned(previous, current, turns)) return turns;
            return 0;
        }

        /// <summary>An angle in degrees brought into 0 (inclusive) to 360.</summary>
        public static float Wrap(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0f;
            float wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        // Whether `current` is `previous` with its front `turns` skills sent to the back.
        private static bool Turned(IReadOnlyList<int> previous, IReadOnlyList<int> current, int turns)
        {
            int count = current.Count;
            for (int index = 0; index < count; index++)
                if (current[index] != previous[(index + turns) % count]) return false;
            return true;
        }

        // Piecewise along the visible arc: the top, then the next slot (upper left) and the used slot (upper right), then
        // the arc's ends one slot further down.
        private static float ArcStyle(float angle, float top, float next, float used, float hidden)
        {
            float a = Wrap(angle - TopAngle + 180f) - 180f;   // -180 to 180 from the top, positive toward the next slot
            if (a >= 0f)
                return a <= SlotDegrees ? Lerp(top, next, a / SlotDegrees)
                    : Lerp(next, hidden, Clamp01((a - SlotDegrees) / SlotDegrees));
            a = -a;
            return a <= SlotDegrees ? Lerp(top, used, a / SlotDegrees)
                : Lerp(used, hidden, Clamp01((a - SlotDegrees) / SlotDegrees));
        }

        private static float Lerp(float from, float to, float t) => from + (to - from) * t;

        private static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
