using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The presentation director's pacing (<see cref="LegacyShowDirector"/>) and the blade bind's motion
    /// (<see cref="LegacyBladeBind"/>): a Minor slot's rest and the wait after it shorter, a Major slot's first hit stop
    /// longer once, the finishing blow untouched, an even 합's bind once and only when affordable, the turn's cap on added
    /// time (what Minor slots saved pays for emphasis), a tier that never drops within a slot of one kind (a 반격 or a dodge
    /// changing the kind re-rates how the cards met), both versus screens making the same decisions, and a bind that sways as far one way as the other and comes back to rest.</summary>
    public sealed class ShowDirectorTests
    {
        private const LegacyClashAction Empty = LegacyClashAction.Empty, Breath = LegacyClashAction.Breath,
            Guard = LegacyClashAction.Guard, Attack = LegacyClashAction.Attack;
        private const float HitStop = .04f, Rest = .1f, Gap = .28f, Tolerance = 1e-5f;

        [Test]
        public void TheDefaults_AreTheDocumentedOnes()
        {
            LegacyShowPacingTuning tuning = LegacyShowPacingTuning.Default;
            Assert.That(tuning.Enabled, Is.True);
            Assert.That(tuning.MinorSpeed, Is.EqualTo(2f));
            Assert.That(tuning.MajorHitStopScale, Is.EqualTo(3f));
            Assert.That(tuning.BladeBindSeconds, Is.EqualTo(.45f).Within(Tolerance));
            Assert.That(tuning.TurnExtraCap, Is.EqualTo(.8f).Within(Tolerance));
            Assert.That(tuning.BladeBindMinimumShare, Is.EqualTo(.5f));
            var director = new LegacyShowDirector();
            Assert.That(director.Enabled, Is.True);
            Assert.That(director.Available, Is.EqualTo(.8f).Within(Tolerance));
            Assert.That(director.HasSlot, Is.False);
            Assert.That(director.SlotRank, Is.EqualTo(LegacySlotRank.None));
        }

        [Test]
        public void TheTuning_KeepsItsNumbersInRange()
        {
            var wild = new LegacyShowPacingTuning(true, float.NaN, -3f, 99f, -1f, 7f);
            Assert.That(wild.MinorSpeed, Is.EqualTo(1f), "Not a number: nothing faster.");
            Assert.That(wild.MajorHitStopScale, Is.EqualTo(1f));
            Assert.That(wild.BladeBindSeconds, Is.EqualTo(LegacyShowPacingTuning.MaximumBladeBindSeconds));
            Assert.That(wild.TurnExtraCap, Is.Zero);
            Assert.That(wild.BladeBindMinimumShare, Is.EqualTo(1f));
            var high = new LegacyShowPacingTuning(true, 99f, 99f, float.NaN, 99f);
            Assert.That(high.MinorSpeed, Is.EqualTo(LegacyShowPacingTuning.MaximumMinorSpeed));
            Assert.That(high.MajorHitStopScale, Is.EqualTo(LegacyShowPacingTuning.MaximumMajorHitStopScale));
            Assert.That(high.BladeBindSeconds, Is.Zero);
            Assert.That(high.TurnExtraCap, Is.EqualTo(LegacyShowPacingTuning.MaximumTurnExtraCap));
        }

        [Test]
        public void SwitchedOff_ChangesNothing()
        {
            var director = Director(LegacyShowPacingTuning.Off);
            foreach (LegacySlotFacts facts in new[]
            {
                Facts(In(Guard, 5, 1, Guard, 5, 1)), Facts(In(Attack, 10, 1, Attack, 4, 1)),
                Facts(In(Attack, 10, 1, Attack, 10, 1)),
            })
            {
                director.BeginSlot(facts);
                Assert.That(director.ShortensSlot, Is.False);
                Assert.That(director.ShortenSlotRest(Rest), Is.EqualTo(Rest));
                Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop));
                Assert.That(director.BladeBind(), Is.Zero);
                director.EndSlot();
                Assert.That(director.ShortenGap(Gap), Is.EqualTo(Gap));
            }
            Assert.That((director.Saved, director.Spent, director.Available), Is.EqualTo((0f, 0f, 0f)));
            Assert.That(director.LastSlotRank.Tier, Is.EqualTo(LegacySlotTier.Normal), "It still rates: only the pacing is off.");
        }

        [Test]
        public void AMinorSlot_PlaysItsRestAndTheWaitAfterIt_Faster_AndBanksTheTime()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            foreach (LegacyClashInput minor in new[]
            {
                In(Guard, 6, 1, Guard, 9, 1), In(Breath, 0, 1, Breath, 0, 1), In(Guard, 6, 1, Empty, 0, 1),
                In(Empty, 0, 1, Breath, 0, 1),
            })
            {
                director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
                Assert.That(director.BeginSlot(Facts(minor)).Tier, Is.EqualTo(LegacySlotTier.Minor), minor.ToString());
                Assert.That(director.ShortensSlot, Is.True);
                Assert.That(director.ShortenSlotRest(Rest), Is.EqualTo(Rest / 2f).Within(Tolerance));
                Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "No attack: nothing to stop on longer.");
                Assert.That(director.BladeBind(), Is.Zero);
                director.EndSlot();
                Assert.That(director.ShortenGap(Gap), Is.EqualTo(Gap / 2f).Within(Tolerance), "The wait after it too.");
                Assert.That(director.Saved, Is.EqualTo((Rest + Gap) / 2f).Within(Tolerance));
                Assert.That(director.Spent, Is.Zero);
                Assert.That(director.NetExtra, Is.EqualTo(-(Rest + Gap) / 2f).Within(Tolerance), "The turn got shorter.");
                Assert.That(director.Available, Is.EqualTo(.8f + (Rest + Gap) / 2f).Within(Tolerance));
            }
        }

        [Test]
        public void ANormalSlot_KeepsItsTime()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            Assert.That(director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 8, 1))).Tier, Is.EqualTo(LegacySlotTier.Normal));
            Assert.That(director.ShortensSlot, Is.False);
            Assert.That(director.ShortenSlotRest(Rest), Is.EqualTo(Rest));
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop));
            Assert.That(director.MajorMomentGiven, Is.False);
            Assert.That(director.IsMajor, Is.False);
            Assert.That(director.BladeBind(), Is.Zero, "A gap of 0.2 is no bind.");
            director.EndSlot();
            Assert.That(director.ShortenGap(Gap), Is.EqualTo(Gap));
            Assert.That((director.Saved, director.Spent), Is.EqualTo((0f, 0f)));
        }

        [Test]
        public void AMajorSlot_StopsLonger_OnceForItsFirstPresentedHit()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            Assert.That(director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 4, 1))).Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That(director.IsMajor, Is.True);
            Assert.That(director.ShortensSlot, Is.False);
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop * 3f).Within(Tolerance));
            Assert.That(director.MajorMomentGiven, Is.True);
            Assert.That(director.Spent, Is.EqualTo(HitStop * 2f).Within(Tolerance), "Only the extra is spent.");
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "Its later hits stop as ever.");
            Assert.That(director.Spent, Is.EqualTo(HitStop * 2f).Within(Tolerance));
            director.EndSlot();
            Assert.That(director.MajorMomentGiven, Is.False);
            Assert.That(director.ShortenGap(Gap), Is.EqualTo(Gap), "The wait after a Major slot keeps its time.");

            // Without a hit stop to lengthen, the moment still comes (its sound) and spends nothing.
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 4, 1)));
            Assert.That(director.HitStop(0f), Is.Zero);
            Assert.That(director.MajorMomentGiven, Is.True);
            Assert.That(director.Spent, Is.Zero);
        }

        [Test]
        public void ASlotTurningMajorOnALaterHit_GetsItsMomentThen()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            LegacyClashInput clash = In(Attack, 4, 2, Attack, 9, 1);
            Assert.That(director.BeginSlot(Facts(clash)).Tier, Is.EqualTo(LegacySlotTier.Normal), "8 against 9.");
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "Its first hit is still Normal.");
            var broke = new LegacyClashInput(0, Attack, 4, 2, Attack, 9, 1, enemyBroken: true, decided: true);
            Assert.That(director.Observe(Facts(broke)).Tier, Is.EqualTo(LegacySlotTier.Major), "A later hit broke the enemy.");
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop * 3f).Within(Tolerance));
        }

        [Test]
        public void TheFinishingBlow_KeepsItsFinale()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            LegacyClashInput even = In(Attack, 10, 1, Attack, 10, 1);
            director.BeginSlot(Facts(even));
            var fall = new LegacySlotFacts(even, playerMaxHealth: 100, enemyMaxHealth: 100, enemyHealthLost: 10,
                enemyLargestHitTaken: 10, enemyDefeated: true);
            Assert.That(director.Observe(fall).Tier, Is.EqualTo(LegacySlotTier.Climax));
            Assert.That(director.IsMajor, Is.False);
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "The finale has the moment.");
            Assert.That(director.IsBladeBindCandidate, Is.False, "Not even an even 합 binds as someone falls.");
            Assert.That(director.BladeBind(), Is.Zero);
            Assert.That(director.Spent, Is.Zero);
        }

        [Test]
        public void AnEvenClash_BindsOnce_AndOnlyAnEvenClash()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            LegacySlotRank rank = director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 9, 1)));
            Assert.That(rank.IsBladeBind && rank.Tier == LegacySlotTier.Normal, Is.True, "Gap 0.1: a bind, still Normal.");
            Assert.That(director.IsBladeBindCandidate, Is.True);
            Assert.That(director.BladeBind(), Is.EqualTo(LegacyBladeBind.DefaultSeconds).Within(Tolerance));
            Assert.That(director.BladeBindGiven, Is.True);
            Assert.That(director.BladeBind(), Is.Zero, "Once per slot.");
            Assert.That(director.Spent, Is.EqualTo(LegacyBladeBind.DefaultSeconds).Within(Tolerance));

            foreach (LegacyClashInput input in new[]
            {
                In(Attack, 10, 1, Attack, 8, 1), In(Attack, 10, 1, Guard, 10, 1), In(Attack, 10, 1, Empty, 0, 1),
                In(Attack, 10, 1, Attack, 10, 1, playerDodged: true),
            })
            {
                director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
                director.BeginSlot(Facts(input));
                Assert.That(director.BladeBind(), Is.Zero, input.ToString());
            }

            // A Major even 합 (a signature move on both sides) both stops longer and binds.
            director.BeginTurn(LegacyShowPacingTuning.Default, LegacySlotImportanceTuning.Default);
            var signature = new LegacySlotFacts(In(Attack, 10, 1, Attack, 10, 1), 51, 53, 100, 100);
            Assert.That(director.BeginSlot(signature).Tier, Is.EqualTo(LegacySlotTier.Major));
            // A signature move's Major moment stops longer than a Major slot's (2026-10-15, signature moves).
            Assert.That(director.HitStop(HitStop),
                Is.EqualTo(HitStop * LegacyShowPacingTuning.DefaultSignatureHitStopScale).Within(Tolerance));
            Assert.That(director.BladeBind(), Is.EqualTo(LegacyBladeBind.DefaultSeconds).Within(Tolerance));
        }

        [Test]
        public void ABind_WaitsForWhatTheSlotIsNow_NotWhatItWas()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.IsBladeBindCandidate, Is.True);
            // The player dodged before the first hit: the cards show the dodge, so no blades lock.
            director.Observe(Facts(new LegacyClashInput(0, Attack, 10, 1, Attack, 10, 1, playerDodged: true, decided: true)));
            Assert.That(director.SlotRank.Kind, Is.EqualTo(LegacyClashKind.Dodged));
            Assert.That(director.SlotRank.IsBladeBind, Is.False, "The bind was the 합's; the slot as it stands is a dodge…");
            Assert.That((director.SlotRank.Tier, director.SlotRank.Has(LegacySlotHighlight.Dodge)),
                Is.EqualTo((LegacySlotTier.Major, true)), "…Major as a dodge.");
            Assert.That(director.IsBladeBindCandidate, Is.False);
            Assert.That(director.BladeBind(), Is.Zero);
        }

        [Test]
        public void TheTier_NeverDropsWithinASlot_UnlessItsKindChanges()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            // Of one kind, what was seen stays seen: a break on the first hit keeps the slot Major after it.
            director.BeginSlot(Facts(In(Attack, 4, 2, Attack, 10, 1)));
            Assert.That(director.SlotRank.Tier, Is.EqualTo(LegacySlotTier.Normal), "8 against 10: a plain 합.");
            director.Observe(Facts(new LegacyClashInput(0, Attack, 4, 2, Attack, 10, 1, enemyBroken: true, decided: true)));
            LegacySlotRank later = director.Observe(Facts(new LegacyClashInput(0, Attack, 4, 2, Attack, 10, 1, decided: true)));
            Assert.That((later.Tier, later.Has(LegacySlotHighlight.Break)), Is.EqualTo((LegacySlotTier.Major, true)));
            Assert.That(director.LatestRank.Tier, Is.EqualTo(LegacySlotTier.Normal));
            director.EndSlot();

            // A pending 반격: the player's side is empty until the first hit, so the enemy's big attack looks unopposed.
            LegacySlotRank start = director.BeginSlot(Facts(new LegacyClashInput(1, Empty, 0, 1, Attack, 30, 1)));
            Assert.That((start.Kind, start.Tier), Is.EqualTo((LegacyClashKind.Unopposed, LegacySlotTier.Major)));
            Assert.That(start.Has(LegacySlotHighlight.BigUnopposed), Is.True);
            // The counter strikes at the first hit: an even 합 now, rated as that 합 (the unopposed guess goes).
            LegacySlotRank met = director.Observe(Facts(new LegacyClashInput(1, Attack, 28, 1, Attack, 30, 1,
                playerCountered: true, decided: true)));
            Assert.That(met, Is.EqualTo(director.LatestRank));
            Assert.That((met.Kind, met.Tier), Is.EqualTo((LegacyClashKind.Clash, LegacySlotTier.Normal)));
            Assert.That(met.Has(LegacySlotHighlight.BigUnopposed), Is.False);
            Assert.That(met.IsBladeBind && director.IsBladeBindCandidate, Is.True);
            Assert.That(director.IsMajor, Is.False);
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "No Major moment for a plain 합.");
            Assert.That(director.MajorMomentGiven, Is.False);
            director.EndSlot();
            Assert.That(director.LastSlotRank.Tier, Is.EqualTo(LegacySlotTier.Normal), "What the replay and the wait read.");

            // What only adds up stays seen across the change (a signature seen at the start, even were a later reading to
            // miss it), and the counter's own read lifts the 합.
            director.BeginSlot(new LegacySlotFacts(new LegacyClashInput(2, Empty, 0, 1, Attack, 10, 1), 0, 51, 100, 100));
            Assert.That(director.SlotRank.EnemySignature, Is.True);
            LegacySlotRank read = director.Observe(new LegacySlotFacts(new LegacyClashInput(2, Attack, 9, 1, Attack, 10, 1,
                playerCountered: true, decided: true), 7, 0, 100, 100, playerConditionMatched: true));
            Assert.That((read.Kind, read.Tier), Is.EqualTo((LegacyClashKind.Clash, LegacySlotTier.Major)));
            Assert.That(read.EnemySignature && read.Has(LegacySlotHighlight.Signature), Is.True, "Kept from the start.");
            Assert.That(read.PlayerReads, Is.EqualTo(LegacyReadEvent.OpponentCondition));
            Assert.That(read.Score, Is.EqualTo(director.LatestRank.Score + LegacySlotImportance.SignaturePoints));
            director.EndSlot();

            // Facts of the next slot begin it; no slot changes nothing.
            LegacySlotRank next = director.Observe(Facts(new LegacyClashInput(3, Guard, 4, 1, Guard, 4, 1)));
            Assert.That((next.SlotIndex, next.Tier), Is.EqualTo((3, LegacySlotTier.Minor)));
            Assert.That(director.LastSlotRank.SlotIndex, Is.EqualTo(2));
            Assert.That(director.Observe(LegacySlotFacts.None), Is.EqualTo(next));
        }

        [Test]
        public void ATurn_NeverRunsLongerThanItsCap()
        {
            var tuning = new LegacyShowPacingTuning(true, 2f, 3f, .45f, .2f);
            var director = Director(tuning);
            // Five Major slots in a row, each asking 0.08 s more, and an even 합 asking 0.45 s.
            float added = 0f;
            for (int slot = 0; slot < 5; slot++)
            {
                director.BeginSlot(Facts(new LegacyClashInput(slot, Attack, 10, 1, Attack, 3, 1)));
                added += director.HitStop(HitStop) - HitStop;
                director.EndSlot();
                Assert.That(director.NetExtra, Is.LessThanOrEqualTo(tuning.TurnExtraCap + Tolerance));
            }
            Assert.That(added, Is.EqualTo(.2f).Within(Tolerance), "0.08 + 0.08 + the 0.04 left, then nothing.");
            director.BeginSlot(Facts(new LegacyClashInput(5, Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.BladeBind(), Is.Zero, "Nothing left for a bind.");
            Assert.That(director.Available, Is.Zero);
            Assert.That(director.Spent, Is.EqualTo(tuning.TurnExtraCap).Within(Tolerance));

            // A new turn starts afresh.
            director.BeginTurn(tuning, LegacySlotImportanceTuning.Default);
            Assert.That((director.Saved, director.Spent, director.Available), Is.EqualTo((0f, 0f, .2f)));
            Assert.That(director.HasSlot || director.LastSlotRank.SlotIndex >= 0, Is.False);
        }

        [Test]
        public void WhatMinorSlotsSaved_PaysForEmphasis_EvenWithNoCap()
        {
            var tuning = new LegacyShowPacingTuning(true, 2f, 3f, .45f, 0f);
            var director = Director(tuning);
            director.BeginSlot(Facts(new LegacyClashInput(0, Attack, 10, 1, Attack, 3, 1)));
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "Nothing saved yet, no cap: no extra.");
            Assert.That(director.MajorMomentGiven, Is.True, "The moment still came (its sound).");
            director.EndSlot();
            director.BeginSlot(Facts(new LegacyClashInput(1, Guard, 4, 1, Breath, 0, 1)));
            director.ShortenSlotRest(Rest);
            director.EndSlot();
            director.ShortenGap(Gap);
            Assert.That(director.Saved, Is.EqualTo(.19f).Within(Tolerance));
            director.BeginSlot(Facts(new LegacyClashInput(2, Attack, 10, 1, Attack, 3, 1)));
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop * 3f).Within(Tolerance), "Paid by the Minor slot.");
            director.EndSlot();
            director.BeginSlot(Facts(new LegacyClashInput(3, Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.Available, Is.EqualTo(.11f).Within(Tolerance));
            Assert.That(director.BladeBind(), Is.Zero, "0.11 s is less than half a 0.45 s bind.");
            Assert.That(director.NetExtra, Is.LessThanOrEqualTo(Tolerance), "Never longer than without the director.");
        }

        [Test]
        public void ABind_PlaysAsLongAsTheTurnCanAfford_FromHalfOfIt()
        {
            var director = Director(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .3f));
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.BladeBind(), Is.EqualTo(.3f).Within(Tolerance), "Cut to what is left.");
            director = Director(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .225f));
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.BladeBind(), Is.EqualTo(.225f).Within(Tolerance), "Exactly half still binds.");
            director = Director(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .22f));
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.BladeBind(), Is.Zero, "Less than half does not.");
            Assert.That(director.BladeBindGiven, Is.False);
            director = Director(new LegacyShowPacingTuning(true, 2f, 3f, 0f, .8f));
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 10, 1)));
            Assert.That(director.BladeBind(), Is.Zero, "A bind of 0 seconds is switched off.");
        }

        [Test]
        public void Clear_ForgetsTheTurn_ButKeepsItsNumbers()
        {
            var tuning = new LegacyShowPacingTuning(true, 3f, 2f, .3f, .5f);
            var director = Director(tuning);
            director.BeginSlot(Facts(In(Guard, 4, 1, Guard, 4, 1)));
            director.ShortenSlotRest(Rest);
            director.Clear();
            Assert.That((director.Saved, director.Spent, director.HasSlot), Is.EqualTo((0f, 0f, false)));
            Assert.That(director.LastSlotRank, Is.EqualTo(LegacySlotRank.None));
            Assert.That(director.Pacing.MinorSpeed, Is.EqualTo(3f));
            Assert.That(director.ShortenGap(Gap), Is.EqualTo(Gap), "No slot completed: the wait keeps its time.");
        }

        [Test]
        public void BothVersusScreens_MakeTheSameDecisions_WhenTheEnemysReadsCountAlike()
        {
            LegacySlotImportanceTuning symmetric = new LegacySlotImportanceTuning(LegacySlotImportanceTuning.DefaultBladeBindGap,
                LegacySlotImportanceTuning.DefaultLargeClashGap, LegacySlotImportanceTuning.DefaultDeepPierce,
                LegacySlotImportanceTuning.DefaultBigUnopposedPercent, LegacyDecisiveHit.DefaultHealthDamagePercent,
                LegacySlotImportanceTuning.DefaultMeshChainMinimum, true, true, LegacySlotImportance.DefaultSignatureSkillIds);
            var host = new LegacyShowDirector(LegacyShowPacingTuning.Default, symmetric);
            var guest = new LegacyShowDirector(LegacyShowPacingTuning.Default, symmetric);
            LegacySlotFacts[] slots =
            {
                Facts(new LegacyClashInput(0, Guard, 5, 1, Breath, 0, 1)),
                // Only the host's skill meets its condition: the guest sees it as the enemy's 간파.
                new LegacySlotFacts(new LegacyClashInput(1, Guard, 8, 1, Attack, 6, 1), playerMaxHealth: 100, enemyMaxHealth: 100,
                    playerConditionMatched: true),
                Facts(new LegacyClashInput(2, Attack, 10, 1, Attack, 9, 1)),
                Facts(new LegacyClashInput(3, Attack, 12, 1, Attack, 4, 1)),
                Facts(new LegacyClashInput(4, Breath, 0, 1, Breath, 0, 1)),
            };
            foreach (LegacySlotFacts facts in slots)
            {
                LegacySlotRank hostRank = host.BeginSlot(facts), guestRank = guest.BeginSlot(facts.Mirrored());
                Assert.That(guestRank.Tier, Is.EqualTo(hostRank.Tier), facts.ToString());
                Assert.That(guest.ShortenSlotRest(Rest), Is.EqualTo(host.ShortenSlotRest(Rest)));
                Assert.That(guest.HitStop(HitStop), Is.EqualTo(host.HitStop(HitStop)));
                Assert.That(guest.BladeBind(), Is.EqualTo(host.BladeBind()));
                host.EndSlot();
                guest.EndSlot();
                Assert.That(guest.ShortenGap(Gap), Is.EqualTo(host.ShortenGap(Gap)));
                Assert.That((guest.Saved, guest.Spent), Is.EqualTo((host.Saved, host.Spent)));
            }
            Assert.That(host.Spent, Is.GreaterThan(0f), "The sequence did spend: a Major stop and a bind.");

            // Counted only for the player, the two screens would rate slot 1 apart (Major on the host's, Normal on the
            // guest's) and play it for different times: versus never rates so.
            var asymmetric = LegacySlotImportanceTuning.Default;
            Assert.That(LegacySlotImportance.Rate(slots[1], asymmetric).Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That(LegacySlotImportance.Rate(slots[1].Mirrored(), asymmetric).Tier, Is.EqualTo(LegacySlotTier.Normal));
        }

        [Test]
        public void TheBladeBind_SwaysAsFarOneWayAsTheOther_AndComesBackToRest()
        {
            const float sway = .16f, press = .1f;
            Assert.That(LegacyBladeBind.Sway(0f, sway), Is.Zero);
            Assert.That(LegacyBladeBind.Sway(1f, sway), Is.Zero);
            Assert.That(LegacyBladeBind.Press(0f, press), Is.Zero);
            Assert.That(LegacyBladeBind.Press(1f, press), Is.Zero);
            Assert.That(LegacyBladeBind.Press(.5f, press), Is.EqualTo(press).Within(Tolerance), "Leaning in, held.");
            float sum = 0f, largest = 0f, positive = 0f, negative = 0f;
            const int samples = 1000;
            for (int index = 0; index <= samples; index++)
            {
                float t = index / (float)samples;
                float now = LegacyBladeBind.Sway(t, sway);
                Assert.That(LegacyBladeBind.Sway(1f - t, sway), Is.EqualTo(-now).Within(1e-4f), "Read from the end: negated.");
                Assert.That(LegacyBladeBind.Press(1f - t, press), Is.EqualTo(LegacyBladeBind.Press(t, press)).Within(1e-4f));
                LegacyBladeBind.Offsets(t, sway, press, out float player, out float enemy);
                Assert.That(player - enemy, Is.EqualTo(2f * LegacyBladeBind.Press(t, press)).Within(1e-5f),
                    "Both lean the same distance toward each other.");
                Assert.That(player + enemy, Is.EqualTo(2f * now).Within(1e-5f), "And sway together.");
                sum += now;
                largest = Math.Max(largest, Math.Abs(now));
                positive = Math.Max(positive, now);
                negative = Math.Min(negative, now);
            }
            Assert.That(sum / samples, Is.EqualTo(0f).Within(1e-4f), "Neither side drives the other off.");
            Assert.That(positive, Is.EqualTo(-negative).Within(1e-3f), "As far one way as the other.");
            Assert.That(largest, Is.LessThanOrEqualTo(sway).And.GreaterThan(sway * .5f));
            Assert.That(LegacyBladeBind.Sway(.3f, 0f), Is.Zero);
            Assert.That(LegacyBladeBind.Press(.5f, -1f), Is.Zero);
        }

        [Test]
        public void TheBindsSparks_FallEvenly_InsideIt()
        {
            Assert.That(LegacyBladeBind.SparkMoment(0, 2), Is.EqualTo(1f / 3f).Within(Tolerance));
            Assert.That(LegacyBladeBind.SparkMoment(1, 2), Is.EqualTo(2f / 3f).Within(Tolerance));
            Assert.That(LegacyBladeBind.SparksBetween(0f, 1f, 2), Is.EqualTo(2));
            Assert.That(LegacyBladeBind.SparksBetween(0f, .5f, 2) + LegacyBladeBind.SparksBetween(.5f, 1f, 2), Is.EqualTo(2),
                "Split across frames, each strikes once.");
            Assert.That(LegacyBladeBind.SparksBetween(0f, 1f, 0), Is.Zero);
            Assert.That(LegacyBladeBind.SparksBetween(.4f, .4f, 3), Is.Zero);
        }

        [Test]
        public void ASignatureMove_StopsLongerThanAMajorSlot_OnEitherSide_FromTheSameAllowance()
        {
            Assert.That(LegacyShowPacingTuning.Default.SignatureHitStopScale, Is.EqualTo(4.5f));
            var director = Director(LegacyShowPacingTuning.Default);
            director.BeginSlot(Facts(In(Attack, 10, 1, Attack, 4, 1)));
            Assert.That(director.IsSignature, Is.False);
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop * 3f).Within(Tolerance), "A Major slot: three times.");
            director.EndSlot();
            director.BeginSlot(new LegacySlotFacts(In(Attack, 10, 1, Attack, 10, 1), 51, 0, 100, 100));
            Assert.That(director.IsSignature && director.IsMajor, Is.True, "에스토카다 is Major whatever its 합.");
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop * 4.5f).Within(Tolerance), "A signature: four and a half.");
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop), "Once per slot.");
            director.EndSlot();
            director.BeginSlot(new LegacySlotFacts(In(Attack, 10, 1, Attack, 10, 1), 0, 53, 100, 100));
            Assert.That(director.IsSignature, Is.True, "The enemy's 카덴스 counts alike (both versus screens agree).");
            Assert.That(director.HitStop(HitStop), Is.EqualTo(HitStop * 4.5f).Within(Tolerance));
            Assert.That(director.Spent, Is.EqualTo(HitStop * 2f + HitStop * 3.5f * 2f).Within(Tolerance));

            var tight = Director(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .05f, .5f, 4.5f));
            tight.BeginSlot(new LegacySlotFacts(In(Attack, 10, 1, Attack, 10, 1), 56, 0, 100, 100));
            Assert.That(tight.HitStop(HitStop), Is.EqualTo(HitStop + .05f).Within(Tolerance), "Within the turn's cap.");
            director.EndSlot();
            var off = Director(LegacyShowPacingTuning.Off);
            off.BeginSlot(new LegacySlotFacts(In(Attack, 10, 1, Attack, 10, 1), 51, 0, 100, 100));
            Assert.That(off.HitStop(HitStop), Is.EqualTo(HitStop), "Switched off: the usual stop.");
            var finishing = Director(LegacyShowPacingTuning.Default);
            finishing.BeginSlot(new LegacySlotFacts(In(Attack, 10, 1, Attack, 10, 1), 51, 0, 100, 100, enemyHealthLost: 10,
                enemyDefeated: true));
            Assert.That(finishing.HitStop(HitStop), Is.EqualTo(HitStop), "A finishing signature keeps the finale's stop.");
        }

        [Test]
        public void TheSignatureScale_IsNeverBelowAMajorSlots()
        {
            Assert.That(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .8f).SignatureHitStopScale, Is.EqualTo(3f),
                "Not given: the same as a Major slot.");
            Assert.That(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .8f, .5f, 2f).SignatureHitStopScale, Is.EqualTo(3f));
            Assert.That(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .8f, .5f, 99f).SignatureHitStopScale,
                Is.EqualTo(LegacyShowPacingTuning.MaximumMajorHitStopScale));
            Assert.That(new LegacyShowPacingTuning(true, 2f, 3f, .45f, .8f, .5f, float.NaN).SignatureHitStopScale, Is.EqualTo(3f));
            Assert.That(LegacyShowPacingTuning.Off.SignatureHitStopScale, Is.EqualTo(1f));
        }

        [Test]
        public void TheReadCue_ComesOncePerSlot_AtTheFirstHitThatKnowsIt_ForThePlayersReadsOnly()
        {
            var director = Director(LegacyShowPacingTuning.Default);
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.None), "No slot: nothing.");
            // 막기 against 타격: the read is known as the slot starts and cued at its first hit.
            director.BeginSlot(new LegacySlotFacts(In(Guard, 8, 1, Attack, 6, 1), playerMaxHealth: 100, enemyMaxHealth: 100,
                playerConditionMatched: true));
            Assert.That(director.ReadCueGiven, Is.False);
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.OpponentCondition));
            Assert.That(director.ReadCueGiven, Is.True);
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.None), "Once per slot.");
            director.EndSlot();
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.None), "Between slots: nothing.");

            // The enemy's read is never the player's cue.
            director.BeginSlot(new LegacySlotFacts(In(Attack, 6, 1, Guard, 8, 1), playerMaxHealth: 100, enemyMaxHealth: 100,
                enemyConditionMatched: true));
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.None));
            director.EndSlot();

            // A break a later hit brings is cued at that hit.
            director.BeginSlot(Facts(In(Attack, 9, 2, Attack, 8, 2)));
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.None), "The first hit: no read yet.");
            director.Observe(Facts(new LegacyClashInput(0, Attack, 9, 2, Attack, 8, 2, enemyBroken: true, decided: true)));
            Assert.That(director.TakeReadCue(), Is.EqualTo(LegacyReadEvent.Break), "The second broke the enemy.");
            director.EndSlot();

            // The cue is a reward, not pacing: the director switched off still cues it.
            var off = Director(LegacyShowPacingTuning.Off);
            off.BeginSlot(new LegacySlotFacts(In(Guard, 8, 1, Attack, 6, 1), playerMaxHealth: 100, enemyMaxHealth: 100,
                playerConditionMatched: true));
            Assert.That(off.TakeReadCue(), Is.EqualTo(LegacyReadEvent.OpponentCondition));
            off.Clear();
            Assert.That(off.ReadCueGiven, Is.False);
        }

        private static LegacyShowDirector Director(LegacyShowPacingTuning pacing)
        {
            var director = new LegacyShowDirector();
            director.BeginTurn(pacing, LegacySlotImportanceTuning.Default);
            return director;
        }

        private static LegacySlotFacts Facts(LegacyClashInput clash)
            => new LegacySlotFacts(clash, playerMaxHealth: 100, enemyMaxHealth: 100);

        private static LegacyClashInput In(LegacyClashAction player, int playerPower, int playerHits,
            LegacyClashAction enemy, int enemyPower, int enemyHits, bool playerDodged = false)
            => new LegacyClashInput(0, player, playerPower, playerHits, enemy, enemyPower, enemyHits, playerDodged);
    }
}
