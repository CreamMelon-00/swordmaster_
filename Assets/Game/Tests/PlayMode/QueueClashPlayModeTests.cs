using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation.Tests
{
    /// <summary>The queue panels' pairing cues and clash motion (2026-10-15, <c>LegacyCombatHud.Clash.cs</c>,
    /// <see cref="LegacyClashPose"/>): while planning, the ring on the opponent card the next reservation meets follows every
    /// reservation (an automatic 숨고르기 and a 반환 included) with its socket mirrored on the player's panel, and hides without
    /// an opponent card there or while the player cannot reserve; a reservation into a held slot sparks at both cards in the
    /// slot forecast's look, and none without an opponent card. While resolving, the pair leans in, meets, pushes and is
    /// knocked apart by the classification, a guard braces, a break shakes, a slot completed mid-meeting hands its cards to
    /// leaving copies once the next slot is under way while the turn's last slot and a finishing blow play on their own cards
    /// (never on a copy as well), and nothing of it is left after the meeting, a reset or a new turn; versus mirrors it for the guest; and
    /// the resolution keeps its own time with the motion on, off or long.</summary>
    public sealed class QueueClashPlayModeTests : InputTestFixture
    {
        private const float Frame = 1f / 60f, Step = .02f;

        [UnityTest]
        public IEnumerator TheNextOpponentRing_FollowsReservations_AnAutomaticBreathAndARemoval()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacySkill guard = LegacySkillDefinitions.Skill(17), returner = LegacySkillDefinitions.Skill(56);
                LegacyQueuedDuel duel = KeywordDuel(guard, returner);
                fixture.Planning(duel);
                Assert.That(duel.EnemyQueue.Count, Is.EqualTo(4));
                AssertRing(hud, duel, 0);

                Assert.That(duel.TryQueueLane(guard.LaneIndex), Is.True, "숨고르기 +1 brings a breath along");
                fixture.Planning(duel);
                Assert.That(duel.PlayerQueue.Count, Is.EqualTo(2));
                AssertRing(hud, duel, 2);

                Assert.That(duel.TryQueueBreath(), Is.True, "A breath reserved by hand takes a slot too.");
                fixture.Planning(duel);
                AssertRing(hud, duel, 3);

                int before = duel.PlayerQueue.Count;
                Assert.That(duel.TryQueueLane(returner.LaneIndex), Is.True, "반환 takes reservations back");
                fixture.Planning(duel);
                Assert.That(duel.PlayerQueue.Count, Is.LessThanOrEqualTo(before), "…so the queue does not grow by it.");
                AssertRing(hud, duel, duel.PlayerQueue.Count < duel.EnemyQueue.Count ? duel.PlayerQueue.Count : -1);

                while (duel.PlayerQueue.Count < duel.EnemyQueue.Count && duel.TryQueueBreath()) fixture.Planning(duel);
                Assume.That(duel.PlayerQueue.Count, Is.GreaterThanOrEqualTo(duel.EnemyQueue.Count));
                fixture.Planning(duel);
                AssertRing(hud, duel, -1);
            }
        }

        [UnityTest]
        public IEnumerator TheRing_HidesWithoutAnOpponentCard_OrWhileThePlayerCannotReserve()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 12), Attack(961, 4), 1);
                fixture.Planning(duel);
                AssertRing(hud, duel, 0);

                hud.SetInputsHeld(true);
                fixture.Planning(duel);
                Assert.That(hud.NextOpponentRingSlot, Is.EqualTo(-1), "Held inputs (versus: the opponent's turn) show no ring.");
                Assert.That(hud.NextOpponentRing.gameObject.activeSelf, Is.False);
                Assert.That(hud.NextReservationSocket.gameObject.activeSelf, Is.False);
                hud.SetInputsHeld(false);
                fixture.Planning(duel);
                AssertRing(hud, duel, 0);

                hud.SetInspectedSlot(0);
                fixture.Planning(duel);
                Assert.That(hud.NextOpponentRingSlot, Is.EqualTo(-1), "Tab reads the opponent's queue: no reservation then.");
                hud.HideExplanation();
                fixture.Planning(duel);
                AssertRing(hud, duel, 0);

                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel);
                AssertRing(hud, duel, -1);
                Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(-1), "The opponent has no second card.");

                duel.Commit();
                fixture.Refresh(duel, true, -1);
                Assert.That(hud.NextOpponentRing.gameObject.activeSelf, Is.False, "Only while planning.");
            }

            using (var off = new HudFixture("{\"queuePairCuesEnabled\":false}"))
            {
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 12), Attack(961, 4), 1);
                off.Planning(duel);
                Assert.That(off.Hud.NextOpponentRingSlot, Is.EqualTo(-1), "Switched off.");
                Assert.That(off.Hud.NextReservationSocket.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator AReservationIntoAHeldSlot_SparksAtBothCards_InTheForecastsLook_AndNoneWithoutAnOpponentCard()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 12), Attack(961, 4), 1);
                fixture.Planning(duel);
                Assert.That(duel.TryQueueLane(0), Is.True);
                hud.PlayReservationSpark(duel.LastQueuedSlotIndex);
                Assert.That(hud.IsReservationSparkPlaying, Is.False, "It waits for the frame that reads the new queue.");
                fixture.Planning(duel);
                Assert.That(hud.IsReservationSparkPlaying, Is.True);
                Assert.That(hud.ReservationSparkSlot, Is.Zero);
                Assert.That(hud.ReservationSparkVerdict, Is.EqualTo(hud.GetForecastVerdict(0)), "The slot forecast's verdict.");

                // At both cards at once, each on its edge facing the seam: no line across the screen.
                RectTransform mine = hud.GetQueuedSkillAnchor(true, 0), theirs = hud.GetQueuedSkillAnchor(false, 0);
                DuelMeshSparks mySpark = hud.GetReservationSpark(true), theirSpark = hud.GetReservationSpark(false);
                Assert.That(mySpark, Is.Not.Null);
                Assert.That(theirSpark, Is.Not.Null);
                float seam = hud.QueueSeam.position.x;
                Assert.That(mySpark.transform.position.x, Is.InRange(mine.position.x, seam));
                Assert.That(theirSpark.transform.position.x, Is.InRange(seam, theirs.position.x));
                Assert.That(mySpark.transform.position.y, Is.EqualTo(mine.position.y).Within(.01f));
                Assert.That(theirSpark.transform.position.y, Is.EqualTo(theirs.position.y).Within(.01f));

                // Its ink: 유리 warm, 불리 cold, otherwise the even brass; the tail is the ink, the alpha its strength.
                LegacyForecastVerdict? verdict = hud.ReservationSparkVerdict;
                Color ink = verdict == LegacyForecastVerdict.Favourable ? fixture.Settings.ReservationSparkFavourableInk
                    : verdict == LegacyForecastVerdict.Unfavourable ? fixture.Settings.ReservationSparkUnfavourableInk
                    : fixture.Settings.ReservationSparkEvenInk;
                foreach (DuelMeshSparks spark in new[] { mySpark, theirSpark })
                {
                    Assert.That(new Vector3(spark.TailInk.r, spark.TailInk.g, spark.TailInk.b), Is.EqualTo(new Vector3(ink.r, ink.g, ink.b)));
                    Assert.That(spark.color.a, Is.EqualTo(ink.a).Within(1e-4f));
                    Assert.That(spark.Count, Is.GreaterThan(0));
                }
                Assert.That(mySpark.Reach, Is.EqualTo(theirSpark.Reach), "Both cards burst alike.");

                fixture.Planning(duel, fixture.Settings.ReservationSparkSeconds + .01f);
                Assert.That(hud.IsReservationSparkPlaying, Is.False, "A brief spark.");

                // A slot the opponent leaves empty: nothing.
                Assert.That(duel.TryQueueLane(0), Is.True);
                hud.PlayReservationSpark(duel.LastQueuedSlotIndex);
                fixture.Planning(duel);
                Assert.That(hud.IsReservationSparkPlaying, Is.False, "No opponent card at slot 2: no spark.");
                Assert.That(hud.ReservationSparkSlot, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator TheResolvingPair_LeansIn_Meets_AndIsKnockedApartByThePowerGap_ThenRestsAgain()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyClashTuning tuning = fixture.Settings.ClashTuning;
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 12), Attack(961, 4), 3);
                Assert.That(duel.TryQueueLane(0) && duel.TryQueueLane(0) && duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel);
                duel.Commit();
                fixture.Refresh(duel, true, -1, 1f);
                float playerRest = hud.GetQueuedSkillAnchor(true, 0).anchoredPosition.x;
                float enemyRest = hud.GetQueuedSkillAnchor(false, 0).anchoredPosition.x;
                Assert.That(playerRest, Is.EqualTo(-enemyRest).Within(.01f));
                Assert.That(hud.IsClashPlaying, Is.False, "Nothing moves before a slot resolves.");

                LegacyCurrentSlot slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                for (int frame = 0; frame < 8; frame++) fixture.Refresh(duel, true, 0, Step);
                Assert.That(hud.ShownClash.HasValue, Is.True);
                LegacyClash clash = hud.ShownClash.Value;
                Assert.That((clash.Kind, clash.Winner), Is.EqualTo((LegacyClashKind.Clash, LegacyClashSide.Player)));
                Assert.That(hud.IsClashMeeting, Is.False, "Before the first hit the cards only lean in…");
                Assert.That(hud.GetQueuedSkillAnchor(true, 0).anchoredPosition.x, Is.GreaterThan(playerRest + 1f), "…toward the seam…");
                Assert.That(hud.GetQueuedSkillAnchor(false, 0).anchoredPosition.x, Is.LessThan(enemyRest - 1f), "…both of them.");
                Assert.That(hud.GetClashPose(true).Forward, Is.LessThanOrEqualTo(tuning.ApproachDistance + 1e-3f));

                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                Assert.That(hud.IsClashMeeting, Is.True, "The first hit decides the meeting.");
                float pushed = float.MinValue, knocked = float.MaxValue;
                bool sparked = false;
                for (float elapsed = 0f; elapsed < tuning.ImpactSeconds * LegacyClashPose.HoldEnd; elapsed += Step)
                {
                    fixture.Refresh(duel, true, 0, Step);
                    pushed = Mathf.Max(pushed, hud.GetClashPose(true).Forward);
                    knocked = Mathf.Min(knocked, hud.GetClashPose(false).Forward);
                    sparked |= hud.ClashSpark != null;
                }
                Assert.That(pushed, Is.GreaterThan(-playerRest - hud.QueueCardWidth * LegacyCombatHud.QueueActiveCardMaxScale * .5f),
                    "The stronger card drives over the seam…");
                Assert.That(knocked, Is.LessThan(0f), "…the weaker is knocked back behind its place…");
                Assert.That(sparked, Is.True, "…with one spark where they meet.");
                Assert.That(hud.GetQueuedSkillAnchor(false, 0).localEulerAngles.z, Is.Not.EqualTo(0f), "It tilts away as it goes.");

                for (float elapsed = 0f; elapsed < tuning.ImpactSeconds; elapsed += Step) fixture.Refresh(duel, true, 0, Step);
                Assert.That(hud.GetClashPose(true).IsRest && hud.GetClashPose(false).IsRest, Is.True, "Back at rest…");
                AssertAtRest(hud.GetQueuedSkillAnchor(true, 0), playerRest);
                AssertAtRest(hud.GetQueuedSkillAnchor(false, 0), enemyRest);

                // The next slot (not the turn's last) completes while its pair is still meeting: the controllers move on to
                // the slot after it, so its cards leave at once, as before, and copies under the rows play the rest of the
                // meeting out and fade.
                while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                fixture.Refresh(duel, true, 1, Step);
                slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                fixture.Refresh(duel, true, 1, Step);
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                fixture.Refresh(duel, true, 1, Step);
                Assert.That(hud.ClashSlot, Is.EqualTo(1));
                while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                fixture.Refresh(duel, true, 2, 0f);
                Assert.That(hud.GetQueuedSkillAnchor(true, 1), Is.Null, "The completed slot's card goes at once…");
                Assert.That(hud.LeavingClashCardCount, Is.EqualTo(2), "…and a copy of each card plays out the meeting…");
                Assert.That(hud.HasLeavingClashCopy(1), Is.True);
                Assert.That(hud.ClashSlot, Is.EqualTo(-1));
                Assert.That(hud.TryGetPosedQueueCard(true, out _, out _), Is.False, "…which no card left in the rows plays…");
                foreach (Transform child in hud.QueueCues)
                    if (child.name == LegacyCombatHud.ClashCardName && child.gameObject.activeSelf)
                        Assert.That(child.GetComponent<CanvasGroup>().blocksRaycasts, Is.False, "…taking no input…");
                fixture.Refresh(duel, true, 2, tuning.ImpactSeconds);
                Assert.That(hud.LeavingClashCardCount, Is.Zero, "…and is gone when it ends.");
                fixture.Refresh(duel, true, 2, 1f);
                Assert.That(hud.IsClashPlaying, Is.False);

                // The turn's last slot completes mid-meeting too, but the controllers keep it the active slot until the next
                // turn, so its cards stay shown: the meeting plays out on them, never on a copy drawn under them as well.
                slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                fixture.Refresh(duel, true, 2, Step);
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                fixture.Refresh(duel, true, 2, Step);
                Assert.That(hud.ClashSlot, Is.EqualTo(2));
                while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
                Assert.That(duel.IsTurnResolved, Is.True);
                AssertLastSlotPlaysOnItsOwnCards(fixture, duel, 2, tuning);
                AssertAtRest(hud.GetQueuedSkillAnchor(true, 2), playerRest);
                AssertAtRest(hud.GetQueuedSkillAnchor(false, 2), enemyRest);
            }
        }

        [UnityTest]
        public IEnumerator AFinishingBlow_PlaysItsMeetingOnItsOwnCards_NeverOnACopyToo()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                var duel = new LegacyQueuedDuel(1000, 50, 5, 50, new[] { Attack(960, 12) }, new[] { Guard(962, 4) }, new[] { 1 }, 1,
                    meshPercent: 0);
                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel);
                duel.Commit();
                fixture.Refresh(duel, true, -1, 1f);
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                fixture.Refresh(duel, true, 0, Step);
                // As the controllers do: the deciding hit, the HUD told at once, the slot completed in the same frame, and the
                // active slot left where it was for the finale (AfterTurn).
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                duel.CompleteCurrentSlot();
                Assert.That(duel.IsFinished, Is.True, "The 8 that pierce a guard of 4 fell an enemy of 5 health.");
                Assert.That(hud.IsClashMeeting, Is.True, "The hit was seen before its slot completed.");
                AssertLastSlotPlaysOnItsOwnCards(fixture, duel, 0, fixture.Settings.ClashTuning);
            }
        }

        // The turn's last slot has completed while its pair meets; the controllers keep it active (AfterTurn, Settling): its
        // cards stay shown and carry the meeting to its end, with no copy of either drawn under them, and then rest.
        private static void AssertLastSlotPlaysOnItsOwnCards(HudFixture fixture, ILegacyDuelView duel, int last,
            LegacyClashTuning tuning)
        {
            LegacyCombatHud hud = fixture.Hud;
            bool posed = false;
            for (float elapsed = 0f; elapsed < tuning.ImpactSeconds; elapsed += Step)
            {
                fixture.Refresh(duel, true, last, Step);
                Assert.That(hud.GetQueuedSkillAnchor(true, last), Is.Not.Null, "The last slot's cards stay shown…");
                Assert.That(hud.GetQueuedSkillAnchor(false, last), Is.Not.Null);
                Assert.That(hud.LeavingClashCardCount, Is.Zero, "…with no copy drawn under them…");
                Assert.That(hud.HasLeavingClashCopy(last), Is.False);
                if (hud.TryGetPosedQueueCard(true, out int posedSlot, out Vector2 offset))
                {
                    Assert.That(posedSlot, Is.EqualTo(last));
                    posed |= offset.sqrMagnitude > .01f;
                }
            }
            Assert.That(posed, Is.True, "…and they carry the rest of the meeting themselves…");
            fixture.Refresh(duel, true, last, 1f);
            Assert.That(hud.IsClashPlaying, Is.False, "…to its end…");
            Assert.That(hud.TryGetPosedQueueCard(true, out _, out _) || hud.TryGetPosedQueueCard(false, out _, out _), Is.False,
                "…and rest.");
            foreach (bool playerSide in new[] { true, false })
                Assert.That(Quaternion.Angle(hud.GetQueuedSkillAnchor(playerSide, last).localRotation, Quaternion.identity),
                    Is.LessThan(.05f), "The settled card is upright within Unity's quaternion comparison tolerance.");
        }

        [UnityTest]
        public IEnumerator AGuard_Braces_OnlyTheAttackLeansIn_AndAPierceKnocksTheGuardBack()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 10), Guard(962, 4), 1);
                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel);
                duel.Commit();
                fixture.Refresh(duel, true, -1, 1f);
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                for (int frame = 0; frame < 8; frame++) fixture.Refresh(duel, true, 0, Step);
                Assert.That(hud.ShownClash.Value.Kind, Is.EqualTo(LegacyClashKind.GuardPierced));
                Assert.That(hud.GetClashPose(true).Forward, Is.GreaterThan(0f), "The attack leans in…");
                Assert.That(hud.GetClashPose(false).IsRest, Is.True, "…the guard waits for it.");
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                float knocked = float.MaxValue;
                for (int frame = 0; frame < 20; frame++)
                {
                    fixture.Refresh(duel, true, 0, Step);
                    knocked = Mathf.Min(knocked, hud.GetClashPose(false).Forward);
                }
                Assert.That(knocked, Is.LessThan(0f), "Damage got through: the guard is knocked back.");
            }
        }

        [UnityTest]
        public IEnumerator ABreakingHit_ShakesTheBrokenSidesCard_AndAResetLeavesNothingBehind()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 12), Attack(961, 4), 1, enemyResistance: 5);
                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel);
                duel.Commit();
                fixture.Refresh(duel, true, -1, 1f);
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                fixture.Refresh(duel, true, 0, Step);
                Assert.That(hud.IsClashShaking(false), Is.False);
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                fixture.Refresh(duel, true, 0, Step);
                Assert.That(duel.Enemy.IsResistanceBroken, Is.True, "12 breaks a resistance of 5.");
                Assert.That(hud.IsClashShaking(false), Is.True, "The broken side's card shakes…");
                Assert.That(hud.IsClashShaking(true), Is.False, "…the other's does not.");
                fixture.Refresh(duel, true, 0, fixture.Settings.ClashBreakShakeSeconds + Step);
                Assert.That(hud.IsClashShaking(false), Is.False, "…and settles.");

                // A reset in the middle of a meeting: no pair, no copy, no spark, and every card upright where its row puts it.
                fixture.Refresh(duel, true, 0, -1f);
                hud.Reset();
                Assert.That(hud.IsClashPlaying, Is.False);
                Assert.That(hud.ShownClash.HasValue, Is.False);
                Assert.That(hud.LeavingClashCardCount, Is.Zero);
                Assert.That(hud.ClashSpark, Is.Null);
                foreach (Transform child in hud.PlayerQueueRow)
                    Assert.That(child.localRotation, Is.EqualTo(Quaternion.identity));
            }
        }

        [UnityTest]
        public IEnumerator ANewTurn_LeavesNoMotion_AndPlanningNeverMovesTheCards()
        {
            yield return null;
            using (var fixture = new HudFixture())
            {
                LegacyCombatHud hud = fixture.Hud;
                LegacyQueuedDuel duel = ClashDuel(Attack(960, 12), Attack(961, 4), 1);
                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel);
                duel.Commit();
                LegacyCurrentSlot slot = duel.BeginNextSlot();
                hud.SetCurrentSkills(slot.PlayerSkill, slot.EnemySkill, .6f);
                fixture.Refresh(duel, true, 0, Step);
                duel.ResolveNextHit();
                hud.ObserveClash(duel);
                fixture.Refresh(duel, true, 0, Step);
                Assert.That(hud.IsClashPlaying, Is.True);
                duel.CompleteCurrentSlot();
                duel.BeginNextTurn();
                hud.BeginTurn();
                Assert.That(hud.IsClashPlaying, Is.False, "The turn's start clears whatever was still meeting.");
                Assert.That(duel.TryQueueLane(0), Is.True);
                fixture.Planning(duel, .5f);
                Assert.That(hud.IsClashPlaying, Is.False);
                Assert.That(hud.GetQueuedSkillAnchor(true, 0).localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(hud.GetQueuedSkillAnchor(true, 0).anchoredPosition.x,
                    Is.EqualTo(-hud.GetQueuedSkillAnchor(false, 0).anchoredPosition.x).Within(.01f), "At rest, mirrored.");
            }
        }

        [UnityTest]
        public IEnumerator Versus_TheGuestSeesTheHostsMeetingMirrored()
        {
            yield return null;
            using (var host = new HudFixture())
            using (var guest = new HudFixture())
            {
                var match = new LocalVersusMatch(new[] { Attack(970, 12) }, new[] { Attack(971, 8) }, 100, 50, 100, 50, randomSeed: 17);
                ILegacyDuelView hostView = new LocalVersusPerspective(match, 0), guestView = new LocalVersusPerspective(match, 1);
                Assert.That(match.TryQueueLane(0, 0), Is.True);
                Assert.That(match.TryCommit(0), Is.True);
                Assert.That(match.TryQueueLane(1, 0), Is.True);
                Assert.That(match.TryCommit(1), Is.True);
                match.BeginNextSlot();
                for (int frame = 0; frame < 6; frame++)
                {
                    host.Refresh(hostView, true, 0, Step);
                    guest.Refresh(guestView, true, 0, Step);
                }
                Assert.That(host.Hud.ShownClash.Value.Winner, Is.EqualTo(LegacyClashSide.Player));
                Assert.That(guest.Hud.ShownClash.Value, Is.EqualTo(host.Hud.ShownClash.Value.Mirrored()));
                match.ResolveNextHit();
                host.Hud.ObserveClash(hostView);
                guest.Hud.ObserveClash(guestView);
                for (int frame = 0; frame < 10; frame++)
                {
                    host.Refresh(hostView, true, 0, Step);
                    guest.Refresh(guestView, true, 0, Step);
                    // The host's own (left) card moves as the guest's opponent (right) card does, and back.
                    AssertSamePose(host.Hud.GetClashPose(true), guest.Hud.GetClashPose(false));
                    AssertSamePose(host.Hud.GetClashPose(false), guest.Hud.GetClashPose(true));
                }
                Assert.That(host.Hud.GetClashPose(true).Forward, Is.GreaterThan(host.Hud.GetClashPose(false).Forward),
                    "The host's stronger card drives on; the guest sees its own card knocked back.");
            }
        }

        [UnityTest]
        public IEnumerator InTheCampaign_TheCuesFollowTheReservations_AndTheMotionNeverHoldsTheResolution()
        {
            yield return null;
            using (var scope = new ClashScope())
            {
                LegacyCombatHud hud = scope.Controller.Hud;
                scope.Advance(Frame);
                Assert.That(hud.NextOpponentRingSlot, Is.Zero, "The enemy's first card faces the first reservation.");
                Assert.That(scope.Controller.QueueLane(0), Is.True);
                Assert.That(hud.ReservationSparkSlot, Is.Zero, "Reserving into a held slot sparks at once.");
                Assert.That(hud.IsReservationSparkPlaying, Is.True);
                scope.Advance(Frame);
                Assert.That(hud.NextOpponentRingSlot, Is.EqualTo(1));
            }

            // The same turn, frame by frame, with no motion, the default and the longest meeting: every slot starts and the turn
            // ends on the same frame, and the pair met while it resolved.
            List<int> still = ResolveTurnRecordingSlotFrames("\"clashMotionEnabled\":false", out bool stillMet);
            List<int> moving = ResolveTurnRecordingSlotFrames(null, out bool met);
            List<int> longest = ResolveTurnRecordingSlotFrames("\"clashImpactSeconds\":1", out _);
            Assert.That(stillMet, Is.False, "Switched off, nothing meets.");
            Assert.That(met, Is.True, "The pair met at the seam.");
            Assert.That(still.Count, Is.GreaterThanOrEqualTo(4), "Three slots and the turn's end.");
            Assert.That(moving, Is.EqualTo(still), "The motion is drawn only; the resolution keeps its own time.");
            Assert.That(longest, Is.EqualTo(still));
        }

        private static List<int> ResolveTurnRecordingSlotFrames(string tuning, out bool met)
        {
            var frames = new List<int>();
            met = false;
            using (var scope = new ClashScope(tuning))
            {
                DuelPrototypeController controller = scope.Controller;
                LegacyCombatHud hud = controller.Hud;
                for (int lane = 0; lane < 3; lane++) Assert.That(controller.QueueLane(lane), Is.True);
                controller.CommitTurn();
                int lastSlot = controller.Session.LastResolvedSlot;
                for (int frame = 0; frame < 60 * 40 && controller.IsResolving; frame++)
                {
                    scope.Advance(Frame);
                    met |= hud.IsClashMeeting;
                    // The turn's last slot stays active (AfterTurn, Settling): its meeting never plays on a copy under it too.
                    for (int slot = 0; slot < 8; slot++)
                        if (hud.HasLeavingClashCopy(slot))
                            Assert.That(hud.GetQueuedSkillAnchor(true, slot) == null && hud.GetQueuedSkillAnchor(false, slot) == null,
                                Is.True, "Slot " + slot + "'s card is drawn once: in its row or as a copy, never both.");
                    if (controller.Session.LastResolvedSlot != lastSlot)
                    {
                        lastSlot = controller.Session.LastResolvedSlot;
                        frames.Add(frame);
                    }
                }
                Assert.That(controller.IsResolving, Is.False, "The turn ends.");
                frames.Add(-1);
                scope.Advance(Frame);
                Assert.That(hud.IsClashPlaying, Is.False, "Nothing of the motion outlives the turn.");
                foreach (bool playerSide in new[] { true, false })
                    for (int slot = 0; slot < 8; slot++)
                    {
                        RectTransform card = hud.GetQueuedSkillAnchor(playerSide, slot);
                        if (card != null) Assert.That(card.localRotation, Is.EqualTo(Quaternion.identity), "Every card stands upright.");
                    }
            }
            return frames;
        }

        private static void AssertRing(LegacyCombatHud hud, ILegacyDuelView duel, int slot)
        {
            Assert.That(hud.NextOpponentRingSlot, Is.EqualTo(slot));
            Assert.That(LegacyClashMotion.NextOpponentSlot(duel), Is.EqualTo(slot), "The forecast's index: the player's queue length.");
            Assert.That(hud.NextOpponentRing.gameObject.activeSelf, Is.EqualTo(slot >= 0));
            Assert.That(hud.NextReservationSocket.gameObject.activeSelf, Is.EqualTo(slot >= 0));
            if (slot < 0) return;
            Canvas.ForceUpdateCanvases();
            RectTransform card = hud.GetQueuedSkillAnchor(false, slot);
            Assert.That(card, Is.Not.Null);
            Assert.That(Vector3.Distance(hud.NextOpponentRing.position, card.position), Is.LessThan(.01f), "Round that card.");
            float seam = hud.QueueSeam.position.x;
            Assert.That(hud.NextReservationSocket.position.x - seam, Is.EqualTo(-(card.position.x - seam)).Within(.01f),
                "The socket mirrors it on the player's panel…");
            Assert.That(hud.NextReservationSocket.position.y, Is.EqualTo(card.position.y).Within(.01f));
            Assert.That(hud.GetQueuedSkillAnchor(true, slot), Is.Null, "…where no card stands yet.");
        }

        private static void AssertAtRest(RectTransform card, float restX)
        {
            Assert.That(card, Is.Not.Null);
            Assert.That(card.anchoredPosition.x, Is.EqualTo(restX).Within(.01f));
            Assert.That(card.anchoredPosition.y, Is.EqualTo(0f).Within(.01f));
            Assert.That(Quaternion.Angle(card.localRotation, Quaternion.identity), Is.LessThan(.05f),
                "The resting card is upright within Unity's quaternion comparison tolerance.");
        }

        private static void AssertSamePose(LegacyCardPose a, LegacyCardPose b)
        {
            Assert.That(a.Forward, Is.EqualTo(b.Forward).Within(1e-3f));
            Assert.That(a.Up, Is.EqualTo(b.Up).Within(1e-3f));
            Assert.That(a.Tilt, Is.EqualTo(b.Tilt).Within(1e-3f));
            Assert.That(a.Squash, Is.EqualTo(b.Squash).Within(1e-4f));
        }

        // A free duel of fixed powers: the player's one skill in lane Q against the enemy's skills, enemySlots a turn.
        private static LegacyQueuedDuel ClashDuel(LegacySkill player, LegacySkill enemy, int enemySlots, int enemyResistance = 50)
            => new LegacyQueuedDuel(1000, 50, 1000, enemyResistance, new[] { player }, new[] { enemy }, new[] { enemySlots }, 1,
                meshPercent: 0);

        // Sheet skills (예약 효과 숨고르기 +1, 반환) against four plain enemy attacks a turn, with ACT to spare.
        private static LegacyQueuedDuel KeywordDuel(params LegacySkill[] skills)
        {
            var enemy = new LegacySkill[4];
            for (int index = 0; index < enemy.Length; index++) enemy[index] = Attack(980 + index, 3);
            return new LegacyQueuedDuel(1000, 50, 1000, 100, skills, enemy, new[] { 4 }, 1, playerActGainBonus: 7, meshPercent: 0);
        }

        private static LegacySkill Attack(int id, int power, int hits = 1)
            => new LegacySkill(id, "합 공격", 0, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits, 0, "검증");

        private static LegacySkill Guard(int id, int power)
            => new LegacySkill(id, "합 방어", 0, power, power, LegacySkillKind.Defence, LegacySkillProperty.Defence, 1, 0, "검증");

        /// <summary>The duel HUD alone, on its own settings (with <c>tuning</c>, JSON fields, over the defaults) and with a
        /// 숨고르기 button, so the player can always reserve while planning.</summary>
        private sealed class HudFixture : IDisposable
        {
            private readonly GameObject parent;
            public DuelPresentationSettings Settings { get; }
            public LegacyCombatHud Hud { get; }

            public HudFixture(string tuning = null)
            {
                parent = new GameObject("Queue Clash HUD Test");
                Settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                if (!string.IsNullOrEmpty(tuning)) JsonUtility.FromJsonOverwrite(tuning, Settings);
                Hud = new LegacyCombatHud(parent.transform, new LegacyDuelArt(), _ => { }, () => { }, () => { }, Settings,
                    () => { }, () => { });
                Canvas.ForceUpdateCanvases();
            }

            public void Planning(ILegacyDuelView duel, float delta = 0f) => Refresh(duel, false, -1, delta);

            public void Refresh(ILegacyDuelView duel, bool resolving, int slot, float delta = 0f)
            {
                Hud.Refresh(duel, 10f, resolving, slot, null, null, null, delta, delta);
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                Hud.Dispose();
                Object.Destroy(parent);
                Object.Destroy(Settings);
            }
        }

        /// <summary>The real controller, driven frame by frame with chosen real seconds, on a free three-lane duel (zero-cost
        /// skills, three enemy skills a turn, health enough for a whole turn); the presentation settings are a copy of the
        /// project's without the hit stop, with <paramref name="tuning"/> (JSON fields) over it.</summary>
        private sealed class ClashScope : IDisposable
        {
            private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
            private readonly DuelPresentationSettings originalSettings, originalArenaSettings, originalHudSettings;
            private readonly LegacyQueuedDuel originalSession;
            private readonly bool originallyEnabled;
            private readonly FieldInfo arenaSettings, hudSettings;
            private readonly Action<float, Keyboard> advance;
            public DuelPrototypeController Controller { get; }
            public DuelPresentationSettings Settings { get; }

            public ClashScope(string tuning = null)
            {
                Controller = Object.FindAnyObjectByType<DuelPrototypeController>();
                Assert.That(Controller, Is.Not.Null);
                Assert.That(Controller.AutoSaveEnabled, Is.False, "Tests never write the player's save.");
                originallyEnabled = Controller.enabled;
                Controller.enabled = false;
                originalSettings = Controller.PresentationSettings;
                originalSession = Controller.Session;
                arenaSettings = typeof(LegacyArenaView).GetField("settings", PrivateInstance);
                originalArenaSettings = (DuelPresentationSettings)arenaSettings.GetValue(Controller.ArenaView);
                hudSettings = typeof(LegacyCombatHud).GetField("presentationSettings", PrivateInstance);
                originalHudSettings = (DuelPresentationSettings)hudSettings.GetValue(Controller.Hud);
                Settings = Object.Instantiate(originalSettings);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"hitStopDuration\":0" + (string.IsNullOrEmpty(tuning) ? "" : "," + tuning) + "}",
                        Settings);
                    Set("presentationSettings", Settings);
                    arenaSettings.SetValue(Controller.ArenaView, Settings);
                    // The HUD keeps the settings supplied when it was built. Tune the same copy on all three readers.
                    hudSettings.SetValue(Controller.Hud, Settings);
                    advance = (Action<float, Keyboard>)Delegate.CreateDelegate(typeof(Action<float, Keyboard>), Controller,
                        typeof(DuelPrototypeController).GetMethod("AdvancePresentation", PrivateInstance));
                    Set("mission", null);
                    Set("guide", null);
                    var free = new List<LegacySkill>();
                    foreach (LegacySkill source in LegacyInitialSkills.All)
                        free.Add(new LegacySkill(source.Id, source.Name, 0, source.MinPower, source.MaxPower, source.Kind,
                            source.Property, source.AttackCount, source.LaneIndex, source.Description, source.AnimationName));
                    Set("session", new LegacyQueuedDuel(1000, 1000, 1000, 1000, free, LegacyInitialSkills.All, new[] { 3 }, 1));
                    typeof(DuelPrototypeController).GetMethod("ResetBattlePresentation", PrivateInstance)
                        .Invoke(Controller, new object[] { null, null });
                    Advance(0f);
                    Assert.That(Controller.CanChoose, Is.True);
                }
                catch
                {
                    // A failed setup never reaches Dispose: restore the shared controller here.
                    try { Dispose(); } catch { Controller.enabled = originallyEnabled; }
                    throw;
                }
            }

            public void Advance(float delta, Keyboard keyboard = null) => advance(delta, keyboard);

            private void Set(string name, object value)
                => typeof(DuelPrototypeController).GetField(name, PrivateInstance).SetValue(Controller, value);

            public void Dispose()
            {
                Set("presentationSettings", originalSettings);
                arenaSettings.SetValue(Controller.ArenaView, originalArenaSettings);
                hudSettings.SetValue(Controller.Hud, originalHudSettings);
                Set("session", originalSession);
                // Leave a fresh arc and lobby behind, as the other flow tests do.
                Controller.StartNewGame();
                Controller.RestartJourney();
                Controller.enabled = originallyEnabled;
                Object.Destroy(Settings);
            }
        }
    }
}
