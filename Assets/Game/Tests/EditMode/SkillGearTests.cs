using System;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The battle dock's skill gears (<see cref="LegacySkillGear"/>): slots 60° apart with the current skill at the
    /// top, the next at the upper left and the used one at the upper right; the lane's order repeated round the rim; packed
    /// lanes and the idlers that mesh between them; the turn curves; and how far a lane turned between two looks.</summary>
    public sealed class SkillGearTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void Slots_SitAtTheTopUpperLeftAndUpperRight_AndATurnMovesThemClockwise()
        {
            Assert.That(LegacySkillGear.SlotDegrees, Is.EqualTo(60f), "Six slots, 60° apart.");
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.CurrentSlot, 0f), Is.EqualTo(90f), "The current skill at the top…");
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.NextSlot, 0f), Is.EqualTo(150f), "…the next at the upper left…");
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.UsedSlot, 0f), Is.EqualTo(30f), "…the used one at the upper right…");
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.SpentSlot, 0f), Is.EqualTo(-30f), "…and the one before it below.");
            // At a turn's start (a slot still to go) each slot is drawn where the slot before it rested: nothing jumps.
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.CurrentSlot, 60f),
                Is.EqualTo(LegacySkillGear.SlotAngle(LegacySkillGear.NextSlot, 0f)));
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.UsedSlot, 60f),
                Is.EqualTo(LegacySkillGear.SlotAngle(LegacySkillGear.CurrentSlot, 0f)));
            Assert.That(LegacySkillGear.SlotAngle(LegacySkillGear.UsedSlot, 30f),
                Is.LessThan(LegacySkillGear.SlotAngle(LegacySkillGear.UsedSlot, 60f)), "As it turns, angles fall: clockwise.");

            const float radius = 68f;
            Assert.That(LegacySkillGear.SlotX(90f, radius), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(LegacySkillGear.SlotY(90f, radius), Is.EqualTo(radius).Within(Tolerance));
            Assert.That(LegacySkillGear.SlotX(150f, radius), Is.LessThan(0f), "Left.");
            Assert.That(LegacySkillGear.SlotX(30f, radius), Is.GreaterThan(0f), "Right.");
            Assert.That(LegacySkillGear.SlotY(150f, radius), Is.EqualTo(radius * .5f).Within(Tolerance));
            Assert.That(LegacySkillGear.SlotRadius(84f), Is.EqualTo(84f - LegacySkillGear.SocketInset));
        }

        [Test]
        public void TheUpperArc_ShowsPreviousCurrentAndNext_AndTheDockHidesTheRest()
        {
            const float radius = 68f, half = 26f;
            foreach (int slot in new[] { LegacySkillGear.UsedSlot, LegacySkillGear.CurrentSlot, LegacySkillGear.NextSlot })
            {
                float angle = LegacySkillGear.SlotAngle(slot, 0f);
                float size = half * LegacySkillGear.SlotScale(angle);
                Assert.That(LegacySkillGear.Showing(angle, radius, size), Is.EqualTo(1f), "Slot " + slot + " shows whole.");
            }
            foreach (int slot in new[] { LegacySkillGear.SpentSlot, 2, 3 })
            {
                float angle = LegacySkillGear.SlotAngle(slot, 0f);
                Assert.That(LegacySkillGear.Showing(angle, radius, half * LegacySkillGear.SlotScale(angle)), Is.Zero,
                    "Slot " + slot + " is below the dock's edge.");
            }
            Assert.That(LegacySkillGear.Showing(0f, radius, half), Is.EqualTo(.5f).Within(Tolerance), "Half out at the edge.");

            float top = 90f, next = 150f, used = 30f;
            Assert.That(LegacySkillGear.SlotScale(top), Is.EqualTo(1f), "The current skill is the largest…");
            Assert.That(LegacySkillGear.SlotScale(next), Is.EqualTo(LegacySkillGear.NextScale).Within(Tolerance));
            Assert.That(LegacySkillGear.SlotScale(used), Is.EqualTo(LegacySkillGear.UsedScale).Within(Tolerance));
            Assert.That(LegacySkillGear.SlotScale(next), Is.GreaterThan(LegacySkillGear.SlotScale(used)), "…the next larger than the used one…");
            Assert.That(LegacySkillGear.SlotAlpha(top), Is.EqualTo(1f));
            Assert.That(LegacySkillGear.SlotAlpha(next), Is.GreaterThan(LegacySkillGear.SlotAlpha(used)), "…and brighter: the used one is dimmed.");
            Assert.That(LegacySkillGear.SlotAlpha(used), Is.LessThan(.75f));
            Assert.That(LegacySkillGear.SlotScale(-30f), Is.LessThan(LegacySkillGear.SlotScale(used)), "Smaller again on the way out.");
            Assert.That(LegacySkillGear.SlotScale(450f), Is.EqualTo(1f).Within(Tolerance), "Any turn of the angle.");
            for (float angle = 30f; angle <= 150f; angle += 5f)
            {
                Assert.That(LegacySkillGear.SlotScale(angle), Is.InRange(LegacySkillGear.UsedScale - Tolerance, 1f + Tolerance));
                Assert.That(LegacySkillGear.SlotAlpha(angle), Is.InRange(LegacySkillGear.UsedAlpha - Tolerance, 1f + Tolerance));
            }
        }

        [Test]
        public void TheLaneOrder_RepeatsRoundTheRim_AndATurnBringsTheNextSlotsSkillToTheTop()
        {
            Assert.That(Enumerable.Range(0, 6).Select(slot => LegacySkillGear.SkillIndex(slot, 3)), Is.EqualTo(new[] { 0, 1, 2, 0, 1, 2 }),
                "Three skills on six slots: the order twice round.");
            Assert.That(LegacySkillGear.SkillIndex(LegacySkillGear.UsedSlot, 3), Is.EqualTo(2), "The used slot shows the back of the lane…");
            Assert.That(LegacySkillGear.SkillIndex(LegacySkillGear.NextSlot, 3), Is.EqualTo(1), "…the next slot its second.");
            Assert.That(LegacySkillGear.SkillIndex(LegacySkillGear.NextSlot, 1), Is.Zero, "A lone skill is its own next and used.");
            Assert.That(LegacySkillGear.SkillIndex(LegacySkillGear.UsedSlot, 4), Is.EqualTo(3));
            Assert.That(LegacySkillGear.SkillIndex(LegacySkillGear.SpentSlot, 4), Is.EqualTo(2));
            Assert.That(LegacySkillGear.SkillIndex(0, 0), Is.EqualTo(-1), "An empty lane shows nothing.");
            // Any lane length: what a slot shows after a turn is what the slot behind it showed before.
            for (int count = 1; count <= 7; count++)
            {
                int[] before = Enumerable.Range(100, count).ToArray();
                int[] after = before.Skip(1).Concat(before.Take(1)).ToArray();
                for (int slot = -3; slot <= 3; slot++)
                    Assert.That(after[LegacySkillGear.SkillIndex(slot, count)], Is.EqualTo(before[LegacySkillGear.SkillIndex(slot + 1, count)]),
                        count + " skills, slot " + slot);
            }
        }

        [Test]
        public void OpenLanes_PackCentred_AndShrinkTheirPitchRatherThanReachTheButtons()
        {
            Assert.That(LegacySkillGear.LaneX(0, 1, 190f), Is.Zero, "One lane in the middle.");
            Assert.That(new[] { LegacySkillGear.LaneX(0, 2, 190f), LegacySkillGear.LaneX(1, 2, 190f) }, Is.EqualTo(new[] { -95f, 95f }),
                "Two either side of it.");
            Assert.That(new[] { LegacySkillGear.LaneX(0, 3, 190f), LegacySkillGear.LaneX(1, 3, 190f), LegacySkillGear.LaneX(2, 3, 190f) },
                Is.EqualTo(new[] { -190f, 0f, 190f }), "Three at a pitch each.");
            Assert.That(LegacySkillGear.FitPitch(3, 190f, 84f, 284f), Is.EqualTo(190f), "The default gears fit.");
            Assert.That(LegacySkillGear.FitPitch(3, 230f, 84f, 284f), Is.EqualTo(200f), "A wide pitch closes up…");
            Assert.That(LegacySkillGear.FitPitch(3, 230f, 84f, 284f) + 84f, Is.LessThanOrEqualTo(284f), "…to keep clear of the buttons.");
            Assert.That(LegacySkillGear.FitPitch(2, 230f, 84f, 284f), Is.EqualTo(230f), "Two lanes have room.");
            Assert.That(LegacySkillGear.FitPitch(1, 230f, 84f, 284f), Is.EqualTo(230f));
        }

        [Test]
        public void Idlers_MeshBetweenNeighbours_WithTeethTheSameSize_AndTurnTheOtherWayByWholeTeeth()
        {
            const float pitch = 190f, gear = 84f, idler = 26f, depth = 10f;
            float height = LegacySkillGear.IdlerHeight(pitch, gear, idler, depth);
            Assert.That(height, Is.EqualTo((float)Math.Sqrt(100.0 * 100.0 - 95.0 * 95.0)).Within(Tolerance));
            float reach = (float)Math.Sqrt(pitch * pitch * .25f + height * height);
            Assert.That(reach, Is.EqualTo(gear + idler - depth).Within(Tolerance), "Its pitch circle touches both gears'.");
            Assert.That(LegacySkillGear.IdlerHeight(pitch, gear, 5f, depth), Is.Zero, "Too small to reach: it rests on the centre line.");

            int gearTeeth = LegacySkillGear.GearTeeth(4);
            Assert.That(gearTeeth, Is.EqualTo(24));
            Assert.That(gearTeeth % LegacySkillGear.SlotCount, Is.Zero, "A slot's turn is whole teeth, so it settles meshed.");
            Assert.That(LegacySkillGear.GearTeeth(0), Is.EqualTo(6), "At least a tooth a slot.");
            int idlerTeeth = LegacySkillGear.IdlerTeeth(gearTeeth, gear, idler, depth);
            Assert.That(idlerTeeth, Is.EqualTo(6));
            Assert.That(LegacySkillGear.IdlerTeeth(gearTeeth, gear, 6f, depth), Is.EqualTo(3), "Never fewer than three.");
            float idlerTurn = LegacySkillGear.IdlerTurnPerSlot(gearTeeth, idlerTeeth);
            Assert.That(idlerTurn, Is.EqualTo(240f).Within(Tolerance));
            float teethTurned = idlerTurn / (360f / idlerTeeth);
            Assert.That(teethTurned, Is.EqualTo((float)Math.Round(teethTurned)).Within(Tolerance), "The idler settles meshed too.");
        }

        [Test]
        public void ThePitch_StopsWhereAnIdlerStillMeshesWhollyAboveTheEdge()
        {
            const float gear = 84f, idler = 26f, depth = 10f;
            float most = LegacySkillGear.MeshedPitch(gear, idler, depth);
            Assert.That(most, Is.EqualTo(2f * (float)Math.Sqrt(100.0 * 100.0 - 26.0 * 26.0)).Within(Tolerance), "About 193.");
            Assert.That(most, Is.GreaterThan(LegacySkillGear.DefaultPitch), "The default pitch stands as tuned.");
            Assert.That(LegacySkillGear.IdlerHeight(most, gear, idler, depth), Is.EqualTo(idler).Within(Tolerance),
                "At the widest the idler's bottom just meets the dock's edge…");
            Assert.That(LegacySkillGear.IdlerHeight(LegacySkillGear.DefaultPitch, gear, idler, depth), Is.GreaterThan(idler),
                "…and at the default it stands clear of it.");
            Assert.That(LegacySkillGear.IdlerHeight(200f, gear, idler, depth), Is.Zero,
                "Wider, it would rest on the line, short of both gears: the HUD closes up instead.");
            Assert.That(LegacySkillGear.MeshedPitch(gear, 0f, depth), Is.Zero, "No idlers, no limit.");
            Assert.That(LegacySkillGear.MeshedPitch(8f, idler, depth), Is.Zero, "An idler that cannot reach: none.");
            Assert.That(LegacySkillGear.MeshedPitch(92f, 40f, 4f), Is.GreaterThan(LegacySkillGear.MeshedPitch(82f, idler, 18f)),
                "Larger gears and idlers may stand wider.");
        }

        [Test]
        public void MeshPhase_PutsAGapWhereTheOtherGearsToothMeetsIt_AndRollsTheOtherWay()
        {
            const int teethA = 24, teethB = 6;
            float pitchA = 360f / teethA, pitchB = 360f / teethB;
            foreach (float contact in new[] { 18.2f, 0f, -18.2f, 160f })
            {
                // A tooth of A faces B: B's gap faces back.
                float phaseA = contact - .5f * pitchA + 3f * pitchA;
                float phaseB = LegacySkillGear.MeshPhase(contact, phaseA, teethA, teethB);
                float gaps = (contact + 180f - phaseB) / pitchB;
                Assert.That(Math.Abs(gaps - Math.Round(gaps)), Is.LessThan(Tolerance), "A gap of B at the contact, " + contact);
                // Drawn: A's tooth covers its side of the contact, B's gap leaves it open.
                Assert.That(Cover(contact - phaseA, .9f, teethA), Is.EqualTo(1f), "A's tooth, " + contact);
                Assert.That(Cover(contact + 180f - phaseB, .9f, teethB), Is.Zero, "B's gap, " + contact);
                // Turning A one way turns B the other, by the teeth's ratio.
                float turned = LegacySkillGear.MeshPhase(contact, phaseA - 2f, teethA, teethB);
                float rolled = LegacySkillGear.Wrap(turned - phaseB + 180f) - 180f;
                Assert.That(rolled, Is.EqualTo(2f * teethA / teethB).Within(Tolerance), "B rolls back, " + contact);
            }
        }

        [Test]
        public void ATurn_EasesIntoItsSlot_AndNeedsNoTime_WhenItHasNone()
        {
            Assert.That(LegacySkillGear.TurnShare(0f, .3f), Is.Zero);
            Assert.That(LegacySkillGear.TurnShare(.3f, .3f), Is.EqualTo(1f));
            Assert.That(LegacySkillGear.TurnShare(.15f, .3f), Is.GreaterThan(.8f), "Quick at first, settling.");
            Assert.That(LegacySkillGear.TurnShare(5f, .3f), Is.EqualTo(1f));
            Assert.That(LegacySkillGear.TurnShare(0f, 0f), Is.EqualTo(1f), "No length: already there.");
            Assert.That(LegacySkillGear.TurnShare(0f, float.NaN), Is.EqualTo(1f));
            float previous = 0f;
            for (float t = 0f; t <= .3f; t += .01f)
            {
                float share = LegacySkillGear.TurnShare(t, .3f);
                Assert.That(share, Is.GreaterThanOrEqualTo(previous - Tolerance), "A queue's turn never turns back.");
                Assert.That(share, Is.LessThanOrEqualTo(1f + Tolerance), "…nor past its slot.");
                previous = share;
            }
        }

        [Test]
        public void Shift_RunsPastTheSlotAndSpringsBack_LikeARatchet_AndEndsExactlyOnIt()
        {
            const float seconds = .4f, bounce = 6f;
            Assert.That(LegacySkillGear.ShiftShare(0f, seconds, bounce), Is.Zero);
            float most = 0f, least = 2f;
            for (float t = 0f; t <= seconds; t += .002f)
            {
                float share = LegacySkillGear.ShiftShare(t, seconds, bounce);
                most = Math.Max(most, share);
                if (t > seconds * LegacySkillGear.RatchetTurnShare) least = Math.Min(least, share);
            }
            Assert.That(most, Is.EqualTo(1f + bounce / 60f).Within(Tolerance), "It runs past the slot by the bounce…");
            Assert.That(least, Is.LessThan(1f), "…springs back short of it…");
            Assert.That(LegacySkillGear.ShiftShare(seconds, seconds, bounce), Is.EqualTo(1f).Within(1e-6f), "…and lands exactly on it.");
            Assert.That(LegacySkillGear.ShiftShare(seconds * 2f, seconds, bounce), Is.EqualTo(1f).Within(1e-6f));
            for (float t = 0f; t <= seconds; t += .01f)
                Assert.That(LegacySkillGear.ShiftShare(t, seconds, 0f), Is.LessThanOrEqualTo(1f + 1e-5f), "No bounce: never past.");
            Assert.That(LegacySkillGear.ShiftShare(.1f, 0f, bounce), Is.EqualTo(1f), "No length: already there.");
        }

        [Test]
        public void TurnsBetween_ReadsHowFarALaneTurned_TrustingAnAnnouncedTurnOnlyWhenTheOrderAgrees()
        {
            int[] order = { 1, 2, 3 };
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 2, 3, 1 }, 0), Is.EqualTo(1), "A skill used or sent back.");
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 3, 1, 2 }, 0), Is.EqualTo(2), "Two at once.");
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 1, 2, 3 }, 0), Is.Zero, "Unchanged.");
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 1, 2, 3 }, 1), Is.Zero, "Announced, but the order did not turn.");
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 2, 3, 1 }, 1), Is.EqualTo(1));
            Assert.That(LegacySkillGear.TurnsBetween(new[] { 5 }, new[] { 5 }, 1), Is.EqualTo(1), "A lone skill comes round again…");
            Assert.That(LegacySkillGear.TurnsBetween(new[] { 5 }, new[] { 5 }, 0), Is.Zero, "…when the controller says it turned.");
            Assert.That(LegacySkillGear.TurnsBetween(new[] { 7, 8, 7, 8 }, new[] { 8, 7, 8, 7 }, 1), Is.EqualTo(1));
            Assert.That(LegacySkillGear.TurnsBetween(new[] { 7, 8, 7, 8 }, new[] { 7, 8, 7, 8 }, 0), Is.Zero,
                "An order that repeats itself is not mistaken for a turn.");
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 4, 5, 6 }, 1), Is.Zero, "Another duel: just show it.");
            Assert.That(LegacySkillGear.TurnsBetween(order, new[] { 2, 3 }, 1), Is.Zero);
            Assert.That(LegacySkillGear.TurnsBetween(new int[0], new[] { 1 }, 1), Is.Zero, "The first look.");
            Assert.That(LegacySkillGear.TurnsBetween(null, order, 0), Is.Zero);

            var duel = new LegacyQueuedDuel(100, 50, 1000, 1000, LegacyInitialSkills.All,
                new[] { LegacySkillDefinitions.Skill(1) }, new[] { 1 }, 3);
            int[] q = duel.GetLane(0).Select(skill => skill.Id).ToArray(), w = duel.GetLane(1).Select(skill => skill.Id).ToArray();
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(LegacySkillGear.TurnsBetween(q, duel.GetLane(0).Select(skill => skill.Id).ToArray(), 1), Is.EqualTo(1),
                "Queueing turns its lane one slot…");
            Assert.That(LegacySkillGear.TurnsBetween(w, duel.GetLane(1).Select(skill => skill.Id).ToArray(), 0), Is.Zero, "…and no other.");
            Assert.That(duel.TryCycleLanes(), Is.True);
            Assert.That(LegacySkillGear.TurnsBetween(w, duel.GetLane(1).Select(skill => skill.Id).ToArray(), 1), Is.EqualTo(1),
                "넘기기 turns every lane one slot.");
        }

        [Test]
        public void GearDrawings_TheShimmersGearIsUnchanged_AndAnIdlerIsSolidRoundItsAxle()
        {
            const float pixel = 2f / 128f;
            for (float x = -1.1f; x <= 1.1f; x += .09f)
                for (float y = -1.1f; y <= 1.1f; y += .09f)
                    Assert.That(LegacyGearShimmer.GearCoverage(x, y, pixel, LegacyGearShimmer.Teeth, LegacyGearShimmer.TipRadius,
                            LegacyGearShimmer.RootRadius, LegacyGearShimmer.RimInnerRadius, LegacyGearShimmer.HubRadius,
                            LegacyGearShimmer.AxleRadius, LegacyGearShimmer.Spokes),
                        Is.EqualTo(LegacyGearShimmer.GearCoverage(x, y, pixel)));
            // A solid pinion: no spokes, its rim reaching in past the edge of its hub, so no seam shows between them.
            for (float radius = .16f; radius <= .58f; radius += .01f)
                Assert.That(LegacyGearShimmer.GearCoverage(radius * .6f, radius * -.8f, pixel, 6, .96f, .6f, .3f, .36f, .14f, 0),
                    Is.EqualTo(1f), "Solid at " + radius);
            Assert.That(LegacyGearShimmer.GearCoverage(.02f, .02f, pixel, 6, .96f, .6f, .3f, .36f, .14f, 0), Is.Zero, "Its axle hole.");
            Assert.That(LegacySkillGear.Wrap(-30f), Is.EqualTo(330f));
            Assert.That(LegacySkillGear.Wrap(725f), Is.EqualTo(5f).Within(Tolerance));
        }

        // How much of a gear of `teeth` teeth (shaped like the lane gears) covers the point `radius` out at `angle`
        // degrees in its own frame.
        private static float Cover(float angle, float radius, int teeth)
        {
            double radians = angle * Math.PI / 180.0;
            return LegacyGearShimmer.GearCoverage(radius * (float)Math.Cos(radians), radius * (float)Math.Sin(radians), 2f / 256f,
                teeth, LegacySkillGear.TipShare, .8f, .6f, .26f, .11f, 5);
        }
    }
}
