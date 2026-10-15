using System;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The tension lighting's rules (<see cref="LegacyTensionLight"/>, 2026-10-15): tense while either fighter is at
    /// or below the health share (exact in whole numbers, either side alike), easing in and out on real time; a swell when
    /// the player goes from behind to ahead past the margin or breaks the opponent, never on a fall, carried on without a
    /// dip when the flow turns again; the first reading after a clear only sets where things stand; switches, the scene's
    /// set-aside, clamps, and readings from the rules' own fighters (a versus match tense on both screens alike, each screen
    /// swelling for its own player).</summary>
    public sealed class TensionLightTests
    {
        private const float Tolerance = 1e-5f;

        [Test]
        public void TheDefaults_AreTheDocumentedOnes()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            Assert.That(tuning.TensionEnabled, Is.True);
            Assert.That(tuning.HealthPercent, Is.EqualTo(30));
            Assert.That(tuning.EaseInSeconds, Is.EqualTo(.8f).Within(Tolerance));
            Assert.That(tuning.EaseOutSeconds, Is.EqualTo(1.2f).Within(Tolerance));
            Assert.That(tuning.SwellEnabled, Is.True);
            Assert.That(tuning.SwellOnBreak, Is.True);
            Assert.That(tuning.LeadMarginPercent, Is.EqualTo(5));
            Assert.That(tuning.SwellSeconds, Is.EqualTo(.92f).Within(Tolerance));
            LegacyTensionTuning off = LegacyTensionTuning.Off;
            Assert.That(off.TensionEnabled || off.SwellEnabled, Is.False);
            var light = new LegacyTensionLight();
            Assert.That(light.Tension + light.Swell, Is.Zero);
            Assert.That(light.IsTense || light.IsSwelling, Is.False);
            Assert.That(light.Lead, Is.EqualTo(LegacyHealthLead.Even));
        }

        [Test]
        public void TheTuning_KeepsItsNumbersInRange()
        {
            var wild = new LegacyTensionTuning(true, 400, float.NaN, -2f, true, false, -9, -1f, 99f, 0f);
            Assert.That(wild.HealthPercent, Is.EqualTo(LegacyTensionTuning.MaximumHealthPercent));
            Assert.That(wild.EaseInSeconds, Is.EqualTo(LegacyTensionTuning.DefaultEaseInSeconds), "Not a number: the default.");
            Assert.That(wild.EaseOutSeconds, Is.Zero);
            Assert.That(wild.LeadMarginPercent, Is.Zero);
            Assert.That(wild.SwellRiseSeconds, Is.Zero);
            Assert.That(wild.SwellHoldSeconds, Is.EqualTo(LegacyTensionTuning.MaximumSwellSeconds));
            Assert.That(wild.SwellFadeSeconds, Is.EqualTo(LegacyTensionTuning.MinimumSwellFadeSeconds), "A swell always fades.");
            Assert.That(new LegacyTensionTuning(true, -5, 1f, 1f, true, true, 99, 1f, 1f, 1f).HealthPercent, Is.Zero);
            Assert.That(new LegacyTensionTuning(true, 30, 1f, 1f, true, true, 99, 1f, 1f, 1f).LeadMarginPercent,
                Is.EqualTo(LegacyTensionTuning.MaximumLeadMarginPercent));
        }

        [TestCase(30, 100, 100, 100, true)]
        [TestCase(31, 100, 100, 100, false)]
        [TestCase(100, 100, 30, 100, true)]
        [TestCase(100, 100, 31, 100, false)]
        [TestCase(3, 10, 10, 10, true)]
        [TestCase(7, 24, 24, 24, true)]
        [TestCase(8, 24, 24, 24, false)]
        [TestCase(0, 100, 100, 100, true)]
        [TestCase(-4, 100, 100, 100, true)]
        [TestCase(100, 100, 100, 100, false)]
        public void Tense_AtOrBelowTheShare_OnEitherSide_ExactInWholeNumbers(int player, int playerMax, int enemy, int enemyMax,
            bool tense)
        {
            var reading = new LegacyHealthReading(player, playerMax, enemy, enemyMax, false);
            var mirrored = new LegacyHealthReading(enemy, enemyMax, player, playerMax, false);
            Assert.That(LegacyTensionLight.IsTenseReading(reading, 30), Is.EqualTo(tense));
            Assert.That(LegacyTensionLight.IsTenseReading(mirrored, 30), Is.EqualTo(tense), "Either fighter counts alike.");
        }

        [Test]
        public void NoShare_OrNoReading_IsNeverTense()
        {
            Assert.That(LegacyTensionLight.IsTenseReading(new LegacyHealthReading(1, 100, 1, 100, false), 0), Is.False);
            Assert.That(LegacyTensionLight.IsTenseReading(LegacyHealthReading.None, 30), Is.False);
            Assert.That(LegacyTensionLight.IsTenseReading(new LegacyHealthReading(1, 0, 1, 100, false), 99), Is.False);
            Assert.That(LegacyHealthReading.None.IsKnown || LegacyHealthReading.None.AnyoneFell, Is.False);
        }

        [TestCase(60, 100, 50, 100, 5, LegacyHealthLead.Ahead)]
        [TestCase(55, 100, 50, 100, 5, LegacyHealthLead.Even)]
        [TestCase(56, 100, 50, 100, 5, LegacyHealthLead.Ahead)]
        [TestCase(45, 100, 50, 100, 5, LegacyHealthLead.Even)]
        [TestCase(44, 100, 50, 100, 5, LegacyHealthLead.Behind)]
        [TestCase(51, 100, 50, 100, 0, LegacyHealthLead.Ahead)]
        [TestCase(50, 100, 50, 100, 0, LegacyHealthLead.Even)]
        [TestCase(12, 24, 40, 100, 5, LegacyHealthLead.Ahead)]
        [TestCase(30, 100, 9, 24, 5, LegacyHealthLead.Behind)]
        [TestCase(150, 100, 100, 100, 0, LegacyHealthLead.Even)]
        public void TheLead_IsBySharesOfEachOwnMaximum_PastTheMargin_AndMirrors(int player, int playerMax, int enemy,
            int enemyMax, int margin, LegacyHealthLead lead)
        {
            Assert.That(LegacyTensionLight.LeadOf(new LegacyHealthReading(player, playerMax, enemy, enemyMax, false), margin),
                Is.EqualTo(lead));
            LegacyHealthLead mirrored = lead == LegacyHealthLead.Ahead ? LegacyHealthLead.Behind
                : lead == LegacyHealthLead.Behind ? LegacyHealthLead.Ahead : LegacyHealthLead.Even;
            Assert.That(LegacyTensionLight.LeadOf(new LegacyHealthReading(enemy, enemyMax, player, playerMax, false), margin),
                Is.EqualTo(mirrored), "The other screen sees the other lead.");
        }

        [Test]
        public void TheFirstReading_OnlySetsWhereThingsStand()
        {
            var light = new LegacyTensionLight();
            Assert.That(light.Observe(Reading(100, 20, enemyBroken: true), LegacyTensionTuning.Default),
                Is.EqualTo(LegacyFlowTurn.None), "Ahead and the opponent broken from the start is no turn.");
            Assert.That(light.IsSwelling, Is.False);
            Assert.That(light.Lead, Is.EqualTo(LegacyHealthLead.Ahead));
            Assert.That(light.IsTense, Is.True, "…but 20% is tense at once.");
            Assert.That(light.Observe(Reading(100, 20, enemyBroken: true), LegacyTensionTuning.Default),
                Is.EqualTo(LegacyFlowTurn.None));
            Assert.That(light.SwellCount, Is.Zero);
        }

        [Test]
        public void FromBehindToAhead_Swells_OnceAndOnlyThen()
        {
            var light = new LegacyTensionLight();
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            light.Observe(Reading(100, 100), tuning);
            Assert.That(light.Observe(Reading(70, 100), tuning), Is.EqualTo(LegacyFlowTurn.None), "Falling behind is no turn.");
            Assert.That(light.Lead, Is.EqualTo(LegacyHealthLead.Behind));
            Assert.That(light.Observe(Reading(70, 60), tuning), Is.EqualTo(LegacyFlowTurn.TookTheLead));
            Assert.That(light.IsSwelling, Is.True);
            Assert.That(light.SwellCount, Is.EqualTo(1));
            Assert.That(light.LastTurn, Is.EqualTo(LegacyFlowTurn.TookTheLead));
            Assert.That(light.Observe(Reading(70, 40), tuning), Is.EqualTo(LegacyFlowTurn.None), "Staying ahead is no turn.");
            Assert.That(light.SwellCount, Is.EqualTo(1));
        }

        [Test]
        public void AnEvenReadingBetween_IsNeither_SoOnlyAClearComebackSwells()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = new LegacyTensionLight();
            light.Observe(Reading(100, 100), tuning);
            light.Observe(Reading(60, 100), tuning);
            Assert.That(light.Observe(Reading(60, 62), tuning), Is.EqualTo(LegacyFlowTurn.None), "Within the margin: even.");
            Assert.That(light.Observe(Reading(60, 50), tuning), Is.EqualTo(LegacyFlowTurn.TookTheLead),
                "Behind, then even, then ahead still turns the flow.");

            var never = new LegacyTensionLight();
            never.Observe(Reading(100, 100), tuning);
            Assert.That(never.Observe(Reading(100, 80), tuning), Is.EqualTo(LegacyFlowTurn.None), "Even to ahead is no comeback.");
            never.Observe(Reading(80, 80), tuning);
            Assert.That(never.Observe(Reading(80, 60), tuning), Is.EqualTo(LegacyFlowTurn.None), "Nor ahead, even, ahead again.");
            Assert.That(never.SwellCount, Is.Zero);
        }

        [Test]
        public void TheOpponentsBreak_Swells_AsItBreaks_AndAgainOnlyAfterItRecovered()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = new LegacyTensionLight();
            light.Observe(Reading(100, 100), tuning);
            Assert.That(light.Observe(Reading(100, 100, enemyBroken: true), tuning), Is.EqualTo(LegacyFlowTurn.BrokeOpponent));
            Assert.That(light.IsSwelling, Is.True);
            Assert.That(light.Observe(Reading(100, 90, enemyBroken: true), tuning), Is.EqualTo(LegacyFlowTurn.None),
                "Still broken is no new turn.");
            light.Observe(Reading(100, 90), tuning);
            Assert.That(light.Observe(Reading(100, 90, enemyBroken: true), tuning), Is.EqualTo(LegacyFlowTurn.BrokeOpponent));
            Assert.That(light.SwellCount, Is.EqualTo(2));

            var noBreakSwell = new LegacyTensionTuning(true, 30, .8f, 1.2f, true, false, 5, .12f, .1f, .7f);
            var quiet = new LegacyTensionLight();
            quiet.Observe(Reading(100, 100), noBreakSwell);
            Assert.That(quiet.Observe(Reading(100, 100, enemyBroken: true), noBreakSwell), Is.EqualTo(LegacyFlowTurn.BrokeOpponent),
                "The break is still read…");
            Assert.That(quiet.IsSwelling, Is.False, "…but switched off for breaks, it does not swell.");
            Assert.That(quiet.LastTurn, Is.EqualTo(LegacyFlowTurn.BrokeOpponent));
        }

        [Test]
        public void AFall_NeverSwells_TheFinaleHasTheLight()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = new LegacyTensionLight();
            light.Observe(Reading(100, 100), tuning);
            light.Observe(Reading(20, 60), tuning);
            Assert.That(light.Observe(Reading(20, 0, enemyBroken: true), tuning),
                Is.EqualTo(LegacyFlowTurn.TookTheLead | LegacyFlowTurn.BrokeOpponent), "The finishing blow turns the flow…");
            Assert.That(light.IsSwelling, Is.False, "…but the finale lights the fall.");
            Assert.That(light.IsTense, Is.True);
        }

        [Test]
        public void TheTension_EasesInAndOut_OnRealTime_ShownSmoothly()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = new LegacyTensionLight();
            light.Observe(Reading(30, 100), tuning);
            Assert.That(light.Tension, Is.Zero, "Nothing until time passes.");
            light.Advance(.4f);
            Assert.That(light.Tension, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(light.TensionAmount, Is.EqualTo(.5f).Within(Tolerance), "Smoothstep's middle.");
            light.Advance(.2f);
            Assert.That(light.TensionAmount, Is.GreaterThan(light.Tension), "Eased in.");
            light.Advance(.4f);
            Assert.That(light.Tension, Is.EqualTo(1f));
            Assert.That(light.TensionAmount, Is.EqualTo(1f));

            light.Observe(Reading(31, 100), tuning);
            Assert.That(light.IsTense, Is.False, "Healed past the share…");
            light.Advance(.6f);
            Assert.That(light.Tension, Is.EqualTo(.5f).Within(Tolerance), "…it eases back out over 1.2 s.");
            light.Advance(.6f);
            Assert.That(light.Tension, Is.Zero);

            light.Observe(Reading(10, 100), tuning);
            light.Advance(5f);
            light.Release();
            Assert.That(light.IsTense, Is.False, "A frame that reads nothing lets it go…");
            light.Advance(1.2f);
            Assert.That(light.Tension, Is.Zero, "…with the last numbers' ease.");
            light.Advance(float.NaN);
            light.Advance(-1f);
            Assert.That(light.Tension, Is.Zero);
        }

        [Test]
        public void ZeroEases_ShiftAtOnce()
        {
            var instant = new LegacyTensionTuning(true, 30, 0f, 0f, true, true, 5, .12f, .1f, .7f);
            var light = new LegacyTensionLight();
            light.Observe(Reading(10, 100), instant);
            light.Advance(.001f);
            Assert.That(light.Tension, Is.EqualTo(1f));
            light.Observe(Reading(100, 100), instant);
            light.Advance(.001f);
            Assert.That(light.Tension, Is.Zero);
            Assert.That(LegacyTensionLight.Step(.5f, 1f, float.PositiveInfinity, 1f, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void TheSwell_RisesHoldsAndFades_ThenEnds()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            Assert.That(LegacyTensionLight.SwellEnvelope(0f, tuning), Is.Zero);
            Assert.That(LegacyTensionLight.SwellEnvelope(.06f, tuning), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(LegacyTensionLight.SwellEnvelope(.12f, tuning), Is.EqualTo(1f));
            Assert.That(LegacyTensionLight.SwellEnvelope(.2f, tuning), Is.EqualTo(1f));
            Assert.That(LegacyTensionLight.SwellEnvelope(.22f + .35f, tuning), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(LegacyTensionLight.SwellEnvelope(.92f, tuning), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(LegacyTensionLight.SwellEnvelope(-1f, tuning), Is.Zero);

            var light = Swelling(tuning);
            Assert.That(light.Swell, Is.Zero, "It starts from nothing…");
            light.Advance(.12f);
            Assert.That(light.Swell, Is.EqualTo(1f), "…rises…");
            light.Advance(.45f);
            Assert.That(light.Swell, Is.EqualTo(.5f).Within(1e-4f), "…and fades…");
            light.Advance(.4f);
            Assert.That(light.IsSwelling, Is.False, "…and ends.");
            Assert.That(light.Swell, Is.Zero);
        }

        [Test]
        public void AnotherTurnWhileSwelling_CarriesItOnWithoutADip()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = Swelling(tuning);
            light.Advance(.12f + .1f + .35f);
            float before = light.Swell;
            Assert.That(before, Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(light.Observe(Reading(70, 60, enemyBroken: true), tuning), Is.EqualTo(LegacyFlowTurn.BrokeOpponent));
            Assert.That(light.Swell, Is.EqualTo(before).Within(1e-4f), "No dip: it carries on from where it was…");
            light.Advance(.03f);
            Assert.That(light.Swell, Is.GreaterThan(before), "…rising again…");
            light.Advance(.05f);
            Assert.That(light.Swell, Is.EqualTo(1f), "…to its full height.");
            Assert.That(light.SwellCount, Is.EqualTo(2));
        }

        [Test]
        public void SwitchedOff_NothingLights_AndASwellStopsAtOnce()
        {
            var light = new LegacyTensionLight();
            light.Observe(Reading(100, 100), LegacyTensionTuning.Off);
            light.Observe(Reading(10, 100), LegacyTensionTuning.Off);
            light.Advance(5f);
            Assert.That(light.IsTense, Is.False);
            Assert.That(light.Tension, Is.Zero);
            Assert.That(light.Observe(Reading(10, 2, enemyBroken: true), LegacyTensionTuning.Off),
                Is.EqualTo(LegacyFlowTurn.TookTheLead | LegacyFlowTurn.BrokeOpponent));
            Assert.That(light.IsSwelling, Is.False);

            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var swelling = Swelling(tuning);
            swelling.Advance(.1f);
            var noSwells = new LegacyTensionTuning(true, 30, .8f, 1.2f, false, true, 5, .12f, .1f, .7f);
            swelling.Observe(Reading(70, 60), noSwells);
            Assert.That(swelling.IsSwelling, Is.False, "Switched off mid-swell: it ends.");
            Assert.That(swelling.Swell, Is.Zero);
        }

        [Test]
        public void AScene_SetsTheLightAside_AndGivesTheTensionBack()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = new LegacyTensionLight();
            light.Observe(Reading(100, 100), tuning);
            light.Observe(Reading(60, 100), tuning);
            light.Observe(Reading(60, 20, enemyBroken: true), tuning);
            light.Advance(.4f);
            Assert.That(light.IsSwelling, Is.True);
            float tension = light.SetAside();
            Assert.That(tension, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(light.Tension + light.Swell, Is.Zero, "The scene plays under the neutral light.");
            Assert.That(light.IsSwelling, Is.False, "The swell ends with it.");
            light.PutBack(tension);
            Assert.That(light.Tension, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(light.Lead, Is.EqualTo(LegacyHealthLead.Ahead), "Where the flow stood is kept.");
            light.PutBack(float.NaN);
            Assert.That(light.Tension, Is.Zero);
            light.PutBack(3f);
            Assert.That(light.Tension, Is.EqualTo(1f));
        }

        [Test]
        public void Clear_ForgetsEverything_TheNextReadingOnlySetsWhereThingsStand()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = Swelling(tuning);
            light.Observe(Reading(10, 60), tuning);
            light.Advance(.5f);
            light.Clear();
            Assert.That(light.Tension + light.Swell, Is.Zero);
            Assert.That(light.IsTense || light.IsSwelling, Is.False);
            Assert.That(light.SwellCount, Is.Zero);
            Assert.That(light.LastTurn, Is.EqualTo(LegacyFlowTurn.None));
            Assert.That(light.Lead, Is.EqualTo(LegacyHealthLead.Even));
            Assert.That(light.Observe(Reading(90, 10, enemyBroken: true), tuning), Is.EqualTo(LegacyFlowTurn.None),
                "A fresh battle's first reading is no turn.");
        }

        [Test]
        public void AnUnknownReading_LetsTheTensionGo_AndTurnsNothing()
        {
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            var light = new LegacyTensionLight();
            light.Observe(Reading(10, 100), tuning);
            light.Advance(1f);
            Assert.That(light.Observe(LegacyHealthReading.None, tuning), Is.EqualTo(LegacyFlowTurn.None));
            Assert.That(light.IsTense, Is.False);
            light.Advance(1.2f);
            Assert.That(light.Tension, Is.Zero);
        }

        [Test]
        public void ReadingsFromTheRulesOwnFighters()
        {
            var duel = new LegacyQueuedDuel(100, 50, 40, 10, new[] { AttackSkill(990, 30) },
                new[] { LegacyCommonActions.Breathe }, new[] { 1 }, 5);
            LegacyHealthReading reading = LegacyHealthReading.Of(duel.Player, duel.Enemy);
            Assert.That(reading.IsKnown, Is.True);
            Assert.That((reading.PlayerHealth, reading.PlayerMaximumHealth), Is.EqualTo((100, 100)));
            Assert.That((reading.EnemyHealth, reading.EnemyMaximumHealth), Is.EqualTo((40, 40)));
            Assert.That(reading.EnemyBroken, Is.False);
            Assert.That(reading.PlayerShare + reading.EnemyShare, Is.EqualTo(2f));
            Assert.That(LegacyHealthReading.Of(null, duel.Enemy).IsKnown, Is.False);
            Assert.That(LegacyHealthReading.Of(duel.Player, null).IsKnown, Is.False);
        }

        [Test]
        public void Versus_IsTenseOnBothScreensAlike_AndEachScreenSwellsForItsOwnPlayer()
        {
            // 12 against 8: the left side's attack breaks the right side's 5 resistance.
            var match = new LocalVersusMatch(new[] { AttackSkill(970, 12) }, new[] { AttackSkill(971, 8) },
                100, 50, 100, 5, randomSeed: 17);
            ILegacyDuelView host = new LocalVersusPerspective(match, 0), guest = new LocalVersusPerspective(match, 1);
            LegacyTensionTuning tuning = LegacyTensionTuning.Default;
            LegacyTensionLight hostLight = new LegacyTensionLight(), guestLight = new LegacyTensionLight();
            hostLight.Observe(LegacyHealthReading.Of(host.Player, host.Enemy), tuning);
            guestLight.Observe(LegacyHealthReading.Of(guest.Player, guest.Enemy), tuning);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            int first = match.CurrentPlanner;
            Assert.That(match.TryPass(first), Is.True);
            Assert.That(match.TryPass(1 - first), Is.True);
            match.BeginNextSlot();
            match.ResolveNextHit();
            Assert.That(host.Enemy.IsResistanceBroken && guest.Player.IsResistanceBroken, Is.True);
            Assert.That(hostLight.Observe(LegacyHealthReading.Of(host.Player, host.Enemy), tuning),
                Is.EqualTo(LegacyFlowTurn.BrokeOpponent), "The breaker's screen swells…");
            Assert.That(guestLight.Observe(LegacyHealthReading.Of(guest.Player, guest.Enemy), tuning),
                Is.EqualTo(LegacyFlowTurn.None), "…the broken one's does not.");
            Assert.That(hostLight.IsSwelling && !guestLight.IsSwelling, Is.True);

            var hostLow = new LegacyHealthReading(100, 100, 25, 100, false);
            var guestLow = new LegacyHealthReading(25, 100, 100, 100, false);
            Assert.That(LegacyTensionLight.IsTenseReading(hostLow, 30) && LegacyTensionLight.IsTenseReading(guestLow, 30),
                Is.True, "Either fighter low makes both screens tense alike.");
        }

        private static LegacyHealthReading Reading(int player, int enemy, bool enemyBroken = false)
            => new LegacyHealthReading(player, 100, enemy, 100, enemyBroken);

        // A light whose swell has just begun (the player came back from behind to ahead).
        private static LegacyTensionLight Swelling(LegacyTensionTuning tuning)
        {
            var light = new LegacyTensionLight();
            light.Observe(Reading(100, 100), tuning);
            light.Observe(Reading(60, 100), tuning);
            Assert.That(light.Observe(Reading(70, 60), tuning), Is.EqualTo(LegacyFlowTurn.TookTheLead));
            Assert.That(light.IsSwelling, Is.True);
            return light;
        }

        private static LegacySkill AttackSkill(int id, int power)
            => new LegacySkill(id, "tension attack", 1, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, 1, 0, "");
    }
}
