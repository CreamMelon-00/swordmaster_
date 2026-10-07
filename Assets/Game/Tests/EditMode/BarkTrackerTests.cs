using System.Collections.Generic;
using NUnit.Framework;
using TurnLimbo.Runtime.Barks;

namespace TurnLimbo.Core.Tests
{
    /// <summary>When battle barks are said (presentation only): once-a-battle triggers, the heavy-hit cooldown, the
    /// first drop to low health, which line wins one fighter's bubble, and how long a line stays (real time).</summary>
    public sealed class BarkTrackerTests
    {
        private const int MaxHealth = 100;

        private static BarkTracker Tracker(string source, Queue<int> picks = null)
            => new BarkTracker(BarkScriptParser.Parse("Barks/test", source),
                picks == null ? (System.Func<int, int>)(count => 0) : count => picks.Dequeue());

        // A fighter's part in a moment: health after it, the health this hit took, whether resistance broke now.
        private static BarkBlow Took(int health, int damage = 0, bool broke = false) => new BarkBlow(broke, damage, health, MaxHealth);

        private static readonly BarkBlow Untouched = new BarkBlow(false, 0, MaxHealth, MaxHealth);

        private static string Says(BarkTracker tracker, BarkSpeaker speaker = BarkSpeaker.Enemy) => tracker.Current(speaker)?.Text;

        [Test]
        public void Start_AndSutun_FireOnceABattle()
        {
            BarkTracker tracker = Tracker("@on start\n덤벼라.\n@on sutun\n이것이 수훈이다.");
            Assert.That(tracker.IsShowingAny, Is.False);
            Assert.That(tracker.BattleStarted(), Is.EqualTo(1));
            Assert.That(Says(tracker), Is.EqualTo("덤벼라."));
            Assert.That(tracker.Current(BarkSpeaker.Enemy).Trigger, Is.EqualTo(BarkTrigger.Start));
            Assert.That(tracker.IsShowing(BarkSpeaker.Player), Is.False);
            tracker.Hide();
            Assert.That(tracker.BattleStarted(), Is.Zero, "Later planning turns say nothing new.");
            Assert.That(tracker.IsShowingAny, Is.False);

            Assert.That(tracker.Empowered(), Is.EqualTo(1));
            Assert.That(Says(tracker), Is.EqualTo("이것이 수훈이다."));
            tracker.Hide();
            Assert.That(tracker.Empowered(), Is.Zero);
        }

        [Test]
        public void ABreak_IsSaidOnceABattle_ForEachSide()
        {
            BarkTracker tracker = Tracker("@on enemy-broken\n큭, 자세가…\n@on player-broken\n빈틈이다!");
            Assert.That(tracker.Hit(Took(100, broke: true), Untouched), Is.EqualTo(1));
            Assert.That(Says(tracker), Is.EqualTo("큭, 자세가…"));
            tracker.Hide();
            Assert.That(tracker.Hit(Took(100, broke: true), Untouched), Is.Zero, "The enemy's next break says nothing.");
            Assert.That(tracker.Hit(Untouched, Took(100, broke: true)), Is.EqualTo(1), "The player's break is its own trigger.");
            Assert.That(Says(tracker), Is.EqualTo("빈틈이다!"), "The enemy speaks unless the block says player.");
        }

        [Test]
        public void AHeavyHit_UsesTheDecisiveShare_AndMayComeAgainAfterItsCooldown()
        {
            BarkTracker tracker = Tracker("@on enemy-hurt\n크윽…\n@on player-hurt player\n(아파…!)");
            tracker.HurtCooldownSeconds = 5f;
            tracker.HeavyHealthPercent = 25;
            Assert.That(tracker.Hit(Took(76, 24), Untouched), Is.Zero, "24 of 100 health is short of 25%.");
            Assert.That(tracker.Hit(Took(51, 25), Untouched), Is.EqualTo(1));
            Assert.That(Says(tracker), Is.EqualTo("크윽…"));
            tracker.Hide();
            Assert.That(tracker.Hit(Took(26, 25), Untouched), Is.Zero, "Not again during its cooldown…");
            tracker.Advance(4.9f);
            Assert.That(tracker.Hit(Took(70, 30), Untouched), Is.Zero);
            tracker.Advance(.2f);
            Assert.That(tracker.Hit(Took(40, 30), Untouched), Is.EqualTo(1), "…but after it, real seconds counted.");

            Assert.That(tracker.Hit(Untouched, Took(60, 40)), Is.EqualTo(1));
            Assert.That(Says(tracker, BarkSpeaker.Player), Is.EqualTo("(아파…!)"));
            Assert.That(tracker.Current(BarkSpeaker.Player).IsMonologue, Is.True);
            Assert.That(tracker.Current(BarkSpeaker.Enemy).IsMonologue, Is.False);

            tracker.HeavyHealthPercent = 0;
            tracker.Advance(10f);
            Assert.That(tracker.Hit(Took(10, 90), Untouched), Is.Zero, "A share of 0 switches heavy hits off.");
        }

        [Test]
        public void LowHealth_IsSaidOnTheFirstHitThatLeavesTheFighterAtThirtyPercentOrLess()
        {
            BarkTracker tracker = Tracker("@on enemy-low\n아직이다…!");
            Assert.That(tracker.LowHealthPercent, Is.EqualTo(BarkTracker.DefaultLowHealthPercent));
            Assert.That(tracker.Hit(Took(31, 10), Untouched), Is.Zero);
            Assert.That(tracker.Hit(Took(31, 0, broke: true), Untouched), Is.Zero);
            Assert.That(tracker.Hit(Took(30, 1), Untouched), Is.EqualTo(1));
            Assert.That(Says(tracker), Is.EqualTo("아직이다…!"));
            tracker.Hide();
            Assert.That(tracker.Hit(Took(20, 10), Untouched), Is.Zero, "Only the first drop.");

            tracker = Tracker("@on enemy-low\n아직이다…!");
            Assert.That(tracker.Hit(Took(0, 40), Untouched), Is.Zero, "Not from one who fell.");
            Assert.That(tracker.Hit(new BarkBlow(false, 0, 20, MaxHealth), Untouched), Is.Zero, "Only a hit that took health.");
            tracker.LowHealthPercent = 0;
            Assert.That(tracker.Hit(Took(5, 15), Untouched), Is.Zero, "0 switches it off.");
        }

        [Test]
        public void OneMoment_SaysItsStrongestTrigger_ForEachSpeaker()
        {
            BarkTracker tracker = Tracker(string.Join("\n",
                "@on enemy-hurt", "크윽…",
                "@on enemy-broken", "자세가…",
                "@on enemy-low", "아직이다…!",
                "@on enemy-hurt player", "(통했다!)"));
            // One hit breaks the enemy, takes 40 health and leaves 25: low beats broken beats hurt.
            Assert.That(tracker.Hit(Took(25, 40, broke: true), Untouched), Is.EqualTo(2), "Both sides may talk at once.");
            Assert.That(Says(tracker), Is.EqualTo("아직이다…!"));
            Assert.That(Says(tracker, BarkSpeaker.Player), Is.EqualTo("(통했다!)"));
            tracker.Hide();
            // What lost the bubble is not spent: the break and the heavy hit still come at their next chance.
            Assert.That(tracker.Hit(Took(25, 0, broke: true), Untouched), Is.EqualTo(1));
            Assert.That(Says(tracker), Is.EqualTo("자세가…"));
            tracker.Hide();
            Assert.That(tracker.Hit(Took(1, 25), Untouched), Is.EqualTo(1), "The player's line is in its cooldown.");
            Assert.That(Says(tracker), Is.EqualTo("크윽…"));
        }

        [Test]
        public void ALineOnScreen_IsNotCutOffByAWeakerOne_WhichWaitsForItsNextChance()
        {
            BarkTracker tracker = Tracker("@on start\n덤벼라.\n@on enemy-broken\n자세가…\n@on enemy-hurt\n크윽…\n@on enemy-low\n아직…");
            tracker.BattleStarted();
            Assert.That(tracker.Hit(Took(60, 40, broke: true), Untouched), Is.Zero, "The start line keeps the bubble.");
            Assert.That(Says(tracker), Is.EqualTo("덤벼라."));
            tracker.Hide();
            Assert.That(tracker.Hit(Took(60, 0, broke: true), Untouched), Is.EqualTo(1), "The break was not spent.");
            int brokenSequence = tracker.Sequence(BarkSpeaker.Enemy);
            Assert.That(tracker.Hit(Took(40, 30), Untouched), Is.Zero, "A heavy hit does not cut off the break's line.");
            Assert.That(Says(tracker), Is.EqualTo("자세가…"));
            Assert.That(tracker.Hit(Took(20, 20), Untouched), Is.EqualTo(1), "Low is stronger: it takes the bubble.");
            Assert.That(Says(tracker), Is.EqualTo("아직…"));
            Assert.That(tracker.Sequence(BarkSpeaker.Enemy), Is.Not.EqualTo(brokenSequence));
            Assert.That(tracker.Elapsed(BarkSpeaker.Enemy), Is.Zero);
        }

        [Test]
        public void ALine_StaysItsSecondsOfRealTime_CountedFromTheFrameAfterItWasSaid()
        {
            BarkTracker tracker = Tracker("@on start\n덤벼라.");
            tracker.ShowSeconds = 2f;
            tracker.BattleStarted();
            int sequence = tracker.Sequence(BarkSpeaker.Enemy);
            Assert.That(sequence, Is.GreaterThan(0));
            tracker.Advance(.5f);
            Assert.That(tracker.Elapsed(BarkSpeaker.Enemy), Is.Zero, "Its own frame had passed when it was said.");
            tracker.Advance(1.5f);
            Assert.That(tracker.Elapsed(BarkSpeaker.Enemy), Is.EqualTo(1.5f).Within(1e-5f));
            tracker.Advance(.49f);
            Assert.That(tracker.IsShowing(BarkSpeaker.Enemy), Is.True);
            Assert.That(tracker.Sequence(BarkSpeaker.Enemy), Is.EqualTo(sequence));
            tracker.Advance(.02f);
            Assert.That(tracker.IsShowingAny, Is.False);
            Assert.That(tracker.Sequence(BarkSpeaker.Enemy), Is.Zero);
            tracker.Advance(float.NaN);
            tracker.Advance(-1f);
        }

        [Test]
        public void NoSeconds_SwitchesBarksOff_WithoutSpendingAnything()
        {
            BarkTracker tracker = Tracker("@on enemy-broken\n자세가…");
            tracker.ShowSeconds = 0f;
            Assert.That(tracker.Hit(Took(100, broke: true), Untouched), Is.Zero);
            Assert.That(tracker.IsShowingAny, Is.False);
            tracker.ShowSeconds = 1f;
            Assert.That(tracker.Hit(Took(100, broke: true), Untouched), Is.EqualTo(1), "Nothing was spent while off.");
        }

        [Test]
        public void ABlockOfSeveralLines_NeverSaysTheSameOneTwiceInARow()
        {
            // The pick is given a count and returns an index below it; the line said last is left out of the draw.
            var picks = new Queue<int>(new[] { 1, 0, 0, 1 });
            BarkTracker tracker = Tracker("@on enemy-hurt\n하나\n둘\n셋", picks);
            tracker.HurtCooldownSeconds = 0f;
            string[] said = new string[4];
            for (int hit = 0; hit < said.Length; hit++)
            {
                tracker.Hide();
                Assert.That(tracker.Hit(Took(90, 30), Untouched), Is.EqualTo(1));
                said[hit] = Says(tracker);
            }
            Assert.That(said, Is.EqualTo(new[] { "둘", "하나", "둘", "셋" }));
            Assert.That(picks, Is.Empty);
        }

        [Test]
        public void AFileWithoutTheTrigger_OrWithOnlyComments_SaysNothing()
        {
            BarkTracker tracker = Tracker("# 아직 없다");
            Assert.That(tracker.Script.IsEmpty, Is.True);
            Assert.That(tracker.BattleStarted() + tracker.Empowered() + tracker.Hit(Took(10, 90, true), Took(10, 90, true)), Is.Zero);
            tracker = Tracker("@on player-low player\n(여기서…)");
            Assert.That(tracker.Hit(Took(10, 90, true), Untouched), Is.Zero, "The enemy's moments do not speak for the player.");
            Assert.That(tracker.Hit(Untouched, Took(10, 20)), Is.EqualTo(1));
            Assert.That(Says(tracker, BarkSpeaker.Player), Is.EqualTo("(여기서…)"));
            Assert.That(tracker.Current(BarkSpeaker.Player).Speaker, Is.EqualTo(BarkSpeaker.Player));
        }
    }
}
