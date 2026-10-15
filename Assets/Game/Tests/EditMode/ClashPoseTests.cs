using System;
using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>How the resolving pair of queue cards moves at the centre seam (<see cref="LegacyClashPose"/>): the lean in
    /// before the first hit, the meeting's beats, the push and knock-back growing with the classification's strength, the
    /// guard's brace and the dodger's step, the break's shake, and that every move ends exactly at rest.</summary>
    public sealed class ClashPoseTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly LegacyClashTuning Tuning = LegacyClashTuning.Default;
        private static readonly LegacyClashMove[] Moves = (LegacyClashMove[])Enum.GetValues(typeof(LegacyClashMove));
        private static readonly LegacyClashKind[] Kinds = (LegacyClashKind[])Enum.GetValues(typeof(LegacyClashKind));

        [Test]
        public void EveryMove_StartsWhereItsApproachLeftIt_AndEndsExactlyAtRest()
        {
            var start = new LegacyCardPose(5f, 2f, 1f, -.02f);
            foreach (LegacyClashKind kind in Kinds)
                foreach (LegacyClashMove move in Moves)
                    foreach (float strength in new[] { 0f, .4f, 1f })
                    {
                        string label = kind + " " + move + " " + strength;
                        Assert.That(LegacyClashPose.Impact(kind, move, strength, start, 30f, 0f, Tuning), Is.EqualTo(start), label);
                        Assert.That(LegacyClashPose.Impact(kind, move, strength, start, 30f, 1f, Tuning).IsRest, Is.True, label);
                        Assert.That(LegacyClashPose.Impact(kind, move, strength, start, 30f, 1.7f, Tuning).IsRest, Is.True, label);
                        for (float t = 0f; t <= 1f; t += .05f)
                            Assert.That(LegacyClashPose.Impact(kind, move, strength, start, 30f, t, Tuning).Squash,
                                Is.LessThanOrEqualTo(0f), label + ": a card is never drawn larger than it rests.");
                    }
        }

        [Test]
        public void TheMeeting_ComesAtTheStrikeShare_ThenThePeakHolds_ThenTheCardEasesHome()
        {
            const float contact = 28f;
            LegacyCardPose lean = new LegacyCardPose(8f, 0f);
            LegacyCardPose meet = LegacyClashPose.Impact(LegacyClashKind.Clash, LegacyClashMove.Push, .5f, lean, contact,
                LegacyClashPose.StrikeShare, Tuning);
            Assert.That(meet.Forward, Is.EqualTo(contact).Within(Tolerance), "Both cards reach the seam as they meet.");
            LegacyCardPose peak = LegacyClashPose.Peak(LegacyClashMove.Push, .5f, contact, Tuning);
            Assert.That(LegacyClashPose.Impact(LegacyClashKind.Clash, LegacyClashMove.Push, .5f, lean, contact,
                LegacyClashPose.DriveEnd, Tuning), Is.EqualTo(peak));
            Assert.That(LegacyClashPose.Impact(LegacyClashKind.Clash, LegacyClashMove.Push, .5f, lean, contact,
                (LegacyClashPose.DriveEnd + LegacyClashPose.HoldEnd) * .5f, Tuning), Is.EqualTo(peak), "A short hold for weight.");
            float before = float.MaxValue;
            for (float t = LegacyClashPose.HoldEnd; t < 1f; t += .04f)
            {
                float forward = LegacyClashPose.Impact(LegacyClashKind.Clash, LegacyClashMove.Push, .5f, lean, contact, t, Tuning).Forward;
                Assert.That(forward, Is.LessThanOrEqualTo(before + Tolerance), "It eases home without overshooting.");
                Assert.That(forward, Is.GreaterThanOrEqualTo(-Tolerance));
                before = forward;
            }
            // The strike accelerates into the meeting: slower at its start than at its end.
            float early = LegacyClashPose.Impact(LegacyClashKind.Clash, LegacyClashMove.Push, .5f, lean, contact,
                LegacyClashPose.StrikeShare * .25f, Tuning).Forward - lean.Forward;
            float late = contact - LegacyClashPose.Impact(LegacyClashKind.Clash, LegacyClashMove.Push, .5f, lean, contact,
                LegacyClashPose.StrikeShare * .75f, Tuning).Forward;
            Assert.That(early, Is.LessThan(late));
        }

        [Test]
        public void AClash_TheWinnerDrivesPastTheMeeting_TheLoserIsKnockedBehindItsPlace_MoreTheWiderTheGap()
        {
            float lastPush = float.MinValue, lastKnock = float.MaxValue, lastTilt = float.MinValue;
            for (float strength = 0f; strength <= 1f; strength += .1f)
            {
                LegacyCardPose push = LegacyClashPose.Peak(LegacyClashMove.Push, strength, 20f, Tuning);
                LegacyCardPose recoil = LegacyClashPose.Peak(LegacyClashMove.Recoil, strength, 20f, Tuning);
                Assert.That(push.Forward, Is.GreaterThan(20f), "The winner pushes on past the seam.");
                Assert.That(recoil.Forward, Is.LessThan(0f), "The loser goes back behind its place.");
                Assert.That(recoil.Tilt, Is.GreaterThan(0f), "…leaning away from the seam.");
                Assert.That(push.Forward, Is.GreaterThan(lastPush), "Pushes harder the wider the gap.");
                Assert.That(recoil.Forward, Is.LessThan(lastKnock), "Knocked further the wider the gap.");
                Assert.That(recoil.Tilt, Is.GreaterThan(lastTilt));
                lastPush = push.Forward;
                lastKnock = recoil.Forward;
                lastTilt = recoil.Tilt;
            }
            Assert.That(LegacyClashPose.Peak(LegacyClashMove.Recoil, 1f, 20f, Tuning).Forward,
                Is.EqualTo(-Tuning.KnockbackDistance).Within(Tolerance), "Full strength is the tuned knock-back.");
            Assert.That(LegacyClashPose.Peak(LegacyClashMove.Recoil, 0f, 20f, Tuning).Forward,
                Is.EqualTo(-Tuning.KnockbackDistance * LegacyClashPose.MinimumWeight).Within(Tolerance), "Even the narrowest loss reads.");
        }

        [Test]
        public void AnEvenClash_BouncesBothCardsAlike()
        {
            // An even 합 classifies both sides as Bounce with strength 0: the same pose on both, mirrored by the screen.
            LegacyClash even = LegacyClashMotion.Classify(new LegacyClashInput(0, LegacyClashAction.Attack, 9, 1,
                LegacyClashAction.Attack, 9, 1));
            Assert.That((even.PlayerMove, even.EnemyMove), Is.EqualTo((LegacyClashMove.Bounce, LegacyClashMove.Bounce)));
            for (float t = 0f; t <= 1f; t += .05f)
                Assert.That(LegacyClashPose.Impact(even.Kind, even.PlayerMove, even.PlayerStrength, LegacyCardPose.Rest, 25f, t, Tuning),
                    Is.EqualTo(LegacyClashPose.Impact(even.Kind, even.EnemyMove, even.EnemyStrength, LegacyCardPose.Rest, 25f, t, Tuning)));
            LegacyCardPose peak = LegacyClashPose.Peak(LegacyClashMove.Bounce, 0f, 25f, Tuning);
            Assert.That(peak.Forward, Is.LessThan(0f), "Both bounce back from the seam.");
            Assert.That(peak.Forward, Is.GreaterThan(-Tuning.KnockbackDistance * LegacyClashPose.MinimumWeight),
                "…less than any knock-back.");
        }

        [Test]
        public void AGuard_OnlyTheAttackTravels_APierceKnocksTheGuardBack_AFullBlockReboundsTheAttack()
        {
            const float gap = 27f;
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.GuardPierced, LegacyClashMove.Thrust, gap, gap, 200f),
                Is.EqualTo(2f * gap).Within(Tolerance), "The attack crosses to the guard's face.");
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.GuardPierced, LegacyClashMove.Recoil, gap, gap, 200f), Is.Zero,
                "The guard does not come to meet it.");
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.GuardHeld, LegacyClashMove.Brace, gap, gap, 200f), Is.Zero);
            Assert.That(LegacyClashPose.Meet(LegacyClashKind.GuardHeld, LegacyClashMove.Brace, 0f, Tuning).Squash,
                Is.EqualTo(LegacyClashPose.BraceSquash).Within(Tolerance), "The guard braces under the blow.");

            float lastKnock = float.MaxValue;
            for (float pierce = .1f; pierce <= 1f; pierce += .3f)
            {
                LegacyCardPose guard = LegacyClashPose.Peak(LegacyClashMove.Recoil, pierce, 0f, Tuning);
                Assert.That(guard.Forward, Is.LessThan(lastKnock), "Knocked back further the more got through.");
                lastKnock = guard.Forward;
                Assert.That(LegacyClashPose.Peak(LegacyClashMove.Thrust, pierce, 2f * gap, Tuning).Forward, Is.GreaterThan(2f * gap));
            }

            float lastRebound = float.MaxValue, lastBudge = float.MinValue;
            for (float margin = 0f; margin <= 1f; margin += .25f)
            {
                LegacyCardPose attack = LegacyClashPose.Peak(LegacyClashMove.Rebound, margin, 2f * gap, Tuning);
                LegacyCardPose brace = LegacyClashPose.Peak(LegacyClashMove.Brace, margin, 0f, Tuning);
                Assert.That(attack.Forward, Is.LessThan(0f), "A blocked attack rebounds behind its place…");
                Assert.That(attack.Forward, Is.LessThan(lastRebound), "…further the more the guard held over it.");
                Assert.That(brace.Forward, Is.LessThanOrEqualTo(0f));
                Assert.That(brace.Forward, Is.GreaterThan(lastBudge), "A guard that held more budges less.");
                lastRebound = attack.Forward;
                lastBudge = brace.Forward;
            }
            Assert.That(LegacyClashPose.Peak(LegacyClashMove.Brace, 1f, 0f, Tuning).Forward, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ContactTravel_NeverPassesTheReach_AndOnlyAdvancingCardsTravel()
        {
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Clash, LegacyClashMove.Push, 27f, 27f, 64f), Is.EqualTo(27f),
                "A 합's cards meet at the seam: each crosses its own gap.");
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Clash, LegacyClashMove.Recoil, 27f, 27f, 64f), Is.EqualTo(27f),
                "A 합's loser comes to meet it too.");
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Clash, LegacyClashMove.Push, 460f, 460f, 64f), Is.EqualTo(64f),
                "A seam opened round the coach's card is not crossed.");
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Unopposed, LegacyClashMove.Lunge, 27f, 27f, 64f), Is.EqualTo(27f),
                "A lunge into nothing stops at the seam.");
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Dodged, LegacyClashMove.Whiff, 27f, 27f, 64f), Is.EqualTo(54f));
            foreach (LegacyClashMove still in new[] { LegacyClashMove.Rest, LegacyClashMove.Settle, LegacyClashMove.Brace,
                LegacyClashMove.SideStep })
                Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Clash, still, 27f, 27f, 64f), Is.Zero, still.ToString());
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Clash, LegacyClashMove.Push, -5f, float.NaN, 64f), Is.Zero);
            Assert.That(LegacyClashPose.ContactTravel(LegacyClashKind.Clash, LegacyClashMove.Push, 27f, 27f, 0f), Is.Zero);
        }

        [Test]
        public void TheApproach_LeansAttacksIn_StepsADodgerUp_AndNeverPassesTheMeeting()
        {
            Assert.That(LegacyClashPose.ApproachTarget(LegacyClashKind.Clash, LegacyClashMove.Recoil, 27f, Tuning).Forward,
                Is.EqualTo(Tuning.ApproachDistance), "Both attacks of a 합 lean in, the weaker too.");
            Assert.That(LegacyClashPose.ApproachTarget(LegacyClashKind.Clash, LegacyClashMove.Push, 3f, Tuning).Forward,
                Is.EqualTo(3f), "Never past where the cards would meet.");
            Assert.That(LegacyClashPose.ApproachTarget(LegacyClashKind.GuardHeld, LegacyClashMove.Brace, 0f, Tuning).IsRest, Is.True);
            Assert.That(LegacyClashPose.ApproachTarget(LegacyClashKind.Settle, LegacyClashMove.Settle, 0f, Tuning).IsRest, Is.True);
            Assert.That(LegacyClashPose.ApproachTarget(LegacyClashKind.Dodged, LegacyClashMove.SideStep, 0f, Tuning).Up,
                Is.EqualTo(Tuning.SideStepDistance), "자동 회피 is known as the slot starts: the card is already stepping aside.");

            // Following a target eases in and never overshoots; a changed target (a dodge) is followed without a jump.
            LegacyCardPose pose = LegacyCardPose.Rest, target = new LegacyCardPose(8f, 0f);
            float last = 0f;
            for (int frame = 0; frame < 30; frame++)
            {
                pose = LegacyClashPose.Follow(pose, target, 1f / 60f, Tuning.ApproachSeconds);
                Assert.That(pose.Forward, Is.InRange(last, 8f));
                last = pose.Forward;
            }
            Assert.That(last, Is.EqualTo(8f).Within(.05f), "Settled within half a second.");
            LegacyCardPose stepping = LegacyClashPose.Follow(pose, new LegacyCardPose(0f, 24f), 1f / 60f, Tuning.ApproachSeconds);
            Assert.That(stepping.Up, Is.InRange(0.01f, 12f), "A dodge eases the card up rather than snapping it.");
            Assert.That(LegacyClashPose.Follow(pose, target, 0f, Tuning.ApproachSeconds), Is.EqualTo(pose), "A stopped clock holds it.");
            Assert.That(LegacyClashPose.Follow(pose, target, .1f, 0f), Is.EqualTo(target), "No approach time: at once.");
        }

        [Test]
        public void ADodge_TheDodgerStaysUpThroughTheMeeting_TheAttackWhiffsPastTheSeam()
        {
            LegacyClash dodged = LegacyClashMotion.Classify(new LegacyClashInput(0, LegacyClashAction.Guard, 5, 1,
                LegacyClashAction.Attack, 12, 1, playerDodged: true));
            Assert.That((dodged.PlayerMove, dodged.EnemyMove), Is.EqualTo((LegacyClashMove.SideStep, LegacyClashMove.Whiff)));
            var up = new LegacyCardPose(0f, Tuning.SideStepDistance);
            for (float t = LegacyClashPose.StrikeShare; t < LegacyClashPose.HoldEnd; t += .05f)
                Assert.That(LegacyClashPose.Impact(dodged.Kind, LegacyClashMove.SideStep, 1f, up, 0f, t, Tuning).Up,
                    Is.EqualTo(Tuning.SideStepDistance).Within(Tolerance), "Out of the way while the attack passes.");
            float contact = LegacyClashPose.ContactTravel(dodged.Kind, LegacyClashMove.Whiff, 27f, 27f, Tuning.Reach);
            Assert.That(LegacyClashPose.Peak(LegacyClashMove.Whiff, 1f, contact, Tuning).Forward, Is.GreaterThan(contact),
                "It overshoots where the dodger stood.");
            Assert.That(LegacyClashPose.Peak(LegacyClashMove.Lunge, 1f, 27f, Tuning).Forward, Is.EqualTo(27f),
                "An unopposed lunge holds at the seam.");
        }

        [Test]
        public void ASettle_IsATinyDip_AndRestStaysPut()
        {
            for (float t = 0f; t <= 1f; t += .05f)
            {
                LegacyCardPose settle = LegacyClashPose.Impact(LegacyClashKind.Settle, LegacyClashMove.Settle, 0f, LegacyCardPose.Rest,
                    0f, t, Tuning);
                Assert.That(settle.Forward, Is.Zero);
                Assert.That(settle.Up, Is.InRange(-LegacyClashPose.SettleDip, 0f));
                Assert.That(LegacyClashPose.Impact(LegacyClashKind.None, LegacyClashMove.Rest, 0f, LegacyCardPose.Rest, 0f, t, Tuning).IsRest,
                    Is.True);
            }
        }

        [Test]
        public void TheContactSpark_FiresOnceAsTheCardsMeet_LargerTheMoreOneSideCameAhead()
        {
            float seconds = Tuning.ImpactSeconds, moment = LegacyClashPose.StrikeShare * seconds;
            Assert.That(LegacyClashPose.ReachesContact(0f, moment * .5f, seconds), Is.False);
            Assert.That(LegacyClashPose.ReachesContact(moment * .5f, moment, seconds), Is.True);
            Assert.That(LegacyClashPose.ReachesContact(moment, moment * 2f, seconds), Is.False, "Once.");
            Assert.That(LegacyClashPose.ReachesContact(0f, seconds * 3f, seconds), Is.True, "A long frame still meets.");
            Assert.That(LegacyClashPose.ReachesContact(seconds, seconds * 2f, seconds), Is.False, "Never after the meeting.");

            LegacyClash close = LegacyClashMotion.Classify(new LegacyClashInput(0, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Attack, 9, 1));
            LegacyClash wide = LegacyClashMotion.Classify(new LegacyClashInput(0, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Attack, 2, 1));
            LegacyClash lunge = LegacyClashMotion.Classify(new LegacyClashInput(0, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Empty, 0, 1));
            Assert.That(LegacyClashPose.SparkScale(wide), Is.GreaterThan(LegacyClashPose.SparkScale(close)));
            Assert.That(LegacyClashPose.SparkScale(close), Is.GreaterThan(0f));
            Assert.That(LegacyClashPose.SparkScale(lunge), Is.Zero, "Nothing to touch: no spark.");
        }

        [Test]
        public void ABreak_ShakesWithinItsAmplitude_AndDiesAway()
        {
            float amplitude = Tuning.BreakShake, seconds = Tuning.BreakShakeSeconds;
            bool moved = false;
            for (float elapsed = 0f; elapsed < seconds; elapsed += .005f)
            {
                float shake = LegacyClashPose.Shake(elapsed, seconds, amplitude);
                Assert.That(Math.Abs(shake), Is.LessThanOrEqualTo(amplitude * (1f - elapsed / seconds) * (1f - elapsed / seconds) + Tolerance));
                moved |= Math.Abs(shake) > amplitude * .3f;
            }
            Assert.That(moved, Is.True, "It visibly shakes.");
            Assert.That(LegacyClashPose.Shake(seconds, seconds, amplitude), Is.Zero);
            Assert.That(LegacyClashPose.Shake(-.1f, seconds, amplitude), Is.Zero);
            Assert.That(LegacyClashPose.Shake(.1f, 0f, amplitude), Is.Zero);
            LegacyCardPose shaken = new LegacyCardPose(3f, 1f).Shaken(2f);
            Assert.That((shaken.Forward, shaken.Up), Is.EqualTo((5f, 1f)));
            Assert.That(shaken.Tilt, Is.EqualTo(2f * LegacyClashPose.ShakeTiltPerUnit).Within(Tolerance), "It rattles as it shakes.");
        }

        [Test]
        public void ALeavingCard_StaysWholeThroughTheHold_AndIsGoneAtTheEnd()
        {
            Assert.That(LegacyClashPose.GhostAlpha(0f), Is.EqualTo(1f));
            Assert.That(LegacyClashPose.GhostAlpha(LegacyClashPose.HoldEnd), Is.EqualTo(1f));
            Assert.That(LegacyClashPose.GhostAlpha(1f), Is.Zero);
            float last = 1f;
            for (float t = LegacyClashPose.GhostFadeStart; t <= 1f; t += .02f)
            {
                float alpha = LegacyClashPose.GhostAlpha(t);
                Assert.That(alpha, Is.LessThanOrEqualTo(last + Tolerance));
                last = alpha;
            }
        }

        [Test]
        public void ThePoses_AreMirrorFree_TheSameMoveReadsTheSameOnEitherSide()
        {
            // The classification mirrors (LegacyClashMotion); the pose depends only on the move, so a mirrored slot gives each
            // card the pose the other had.
            var inputs = new List<LegacyClashInput>
            {
                new LegacyClashInput(0, LegacyClashAction.Attack, 12, 1, LegacyClashAction.Attack, 7, 1),
                new LegacyClashInput(0, LegacyClashAction.Attack, 10, 1, LegacyClashAction.Guard, 6, 1),
                new LegacyClashInput(0, LegacyClashAction.Attack, 4, 1, LegacyClashAction.Guard, 9, 1),
                new LegacyClashInput(0, LegacyClashAction.Breath, 0, 1, LegacyClashAction.Attack, 7, 2),
            };
            foreach (LegacyClashInput input in inputs)
            {
                LegacyClash seen = LegacyClashMotion.Classify(input), mirrored = LegacyClashMotion.Classify(input.Mirrored());
                for (float t = 0f; t <= 1f; t += .1f)
                    Assert.That(LegacyClashPose.Impact(seen.Kind, seen.PlayerMove, seen.PlayerStrength, LegacyCardPose.Rest, 27f, t, Tuning),
                        Is.EqualTo(LegacyClashPose.Impact(mirrored.Kind, mirrored.EnemyMove, mirrored.EnemyStrength, LegacyCardPose.Rest,
                            27f, t, Tuning)), input.ToString());
            }
        }

        [Test]
        public void TheTuning_KeepsItsNumbersSane()
        {
            var odd = new LegacyClashTuning(-1f, float.NaN, 0f, float.PositiveInfinity, -3f, 5f, 6f, 7f, -2f, .2f);
            Assert.That(odd.ApproachSeconds, Is.Zero);
            Assert.That(odd.ApproachDistance, Is.Zero);
            Assert.That(odd.ImpactSeconds, Is.EqualTo(LegacyClashPose.MinimumImpactSeconds));
            Assert.That(odd.Reach, Is.Zero);
            Assert.That(odd.PushDistance, Is.Zero);
            Assert.That(odd.BreakShake, Is.Zero);
            Assert.That((odd.SettleDip, odd.BraceSquash), Is.EqualTo((LegacyClashPose.SettleDip, LegacyClashPose.BraceSquash)),
                "Left out, the settle's dip and the brace's squash keep their defaults.");
            var wild = new LegacyClashTuning(.1f, 8f, .44f, 64f, 12f, 22f, 24f, 8f, 5f, .32f, float.NaN, .3f);
            Assert.That((wild.SettleDip, wild.BraceSquash), Is.EqualTo((0f, 0f)), "Never a rise, never a swell.");
            Assert.That(new LegacyClashTuning(.1f, 8f, .44f, 64f, 12f, 22f, 24f, 8f, 5f, .32f, 2f, -3f).BraceSquash, Is.EqualTo(-.5f));
            // Both are the motion's own numbers: a settle dips by the tuned distance, a guard braces by the tuned squash.
            var tuned = new LegacyClashTuning(.1f, 8f, .44f, 64f, 12f, 22f, 24f, 8f, 5f, .32f, 7f, -.1f);
            Assert.That(LegacyClashPose.Meet(LegacyClashKind.Settle, LegacyClashMove.Settle, 0f, tuned).Up, Is.EqualTo(-7f));
            Assert.That(LegacyClashPose.Meet(LegacyClashKind.Settle, LegacyClashMove.Settle, 0f, tuned).Squash, Is.EqualTo(-.05f).Within(Tolerance));
            Assert.That(LegacyClashPose.Meet(LegacyClashKind.GuardHeld, LegacyClashMove.Brace, 0f, tuned).Squash, Is.EqualTo(-.1f).Within(Tolerance));
            Assert.That(LegacyClashPose.Peak(LegacyClashMove.Settle, 0f, 0f, tuned).Up, Is.EqualTo(-7f * .3f).Within(Tolerance));
            Assert.That(LegacyClashPose.Meet(LegacyClashKind.Settle, LegacyClashMove.Settle, 0f, Tuning).Squash,
                Is.EqualTo(LegacyClashPose.SettleSquash).Within(Tolerance), "The defaults draw as before.");
            Assert.That(new LegacyCardPose(float.NaN, 1f, 2f, .5f), Is.EqualTo(new LegacyCardPose(0f, 1f, 2f, 0f)),
                "Never larger than at rest; nothing that is not a number.");
            Assert.That(LegacyCardPose.Rest.IsRest, Is.True);
            Assert.That(LegacyClashPose.Progress(.22f, .44f), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(LegacyClashPose.Progress(1f, 0f), Is.EqualTo(1f));
        }
    }
}
