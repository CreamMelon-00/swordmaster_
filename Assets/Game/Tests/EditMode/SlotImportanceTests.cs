using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Core.Tests
{
    /// <summary>The presentation director's slot scorer and the 간파 detection (<see cref="LegacySlotImportance"/>): every tier
    /// rule, the reads, the signature moves, the blade bind, tie-breaking, the mirror (no side wins a 합), and the facts both
    /// resolvers report through the duel view.</summary>
    public sealed class SlotImportanceTests
    {
        private const LegacyClashAction Empty = LegacyClashAction.Empty, Breath = LegacyClashAction.Breath,
            Guard = LegacyClashAction.Guard, Attack = LegacyClashAction.Attack;

        [Test]
        public void NoAttackOnEitherSide_IsMinor()
        {
            foreach (LegacyClashInput input in new[]
            {
                In(Guard, 6, 1, Guard, 9, 1), In(Breath, 0, 1, Breath, 0, 1), In(Guard, 6, 1, Empty, 0, 1),
                In(Breath, 0, 1, Guard, 4, 1), In(Empty, 0, 1, Empty, 0, 1),
            })
            {
                LegacySlotRank rank = Rate(input);
                Assert.That(rank.Tier, Is.EqualTo(LegacySlotTier.Minor), input.ToString());
                Assert.That(rank.Score, Is.Zero, input.ToString());
                Assert.That(rank.Highlights, Is.EqualTo(LegacySlotHighlight.None), input.ToString());
                Assert.That(rank.IsBladeBind, Is.False);
            }
            Assert.That(LegacySlotImportance.Rate(LegacySlotFacts.None), Is.EqualTo(LegacySlotRank.None));
            Assert.That(LegacySlotRank.None.SlotIndex, Is.EqualTo(-1));
        }

        [Test]
        public void AnAttack_IsNormal_WhenNothingLiftsIt()
        {
            foreach (LegacyClashInput input in new[]
            {
                In(Attack, 10, 1, Attack, 8, 1), In(Attack, 5, 1, Guard, 8, 1), In(Attack, 9, 1, Guard, 6, 1),
                In(Attack, 10, 1, Empty, 0, 1), In(Breath, 0, 1, Attack, 7, 1),
            })
            {
                LegacySlotRank rank = Rate(input);
                Assert.That(rank.Tier, Is.EqualTo(LegacySlotTier.Normal), input.ToString());
                Assert.That(rank.Has(LegacySlotHighlight.Attack), Is.True);
                Assert.That(rank.Score, Is.GreaterThanOrEqualTo(LegacySlotImportance.AttackPoints));
            }
        }

        [Test]
        public void ALargeClashGap_IsMajor_WhicheverSideIsStronger()
        {
            LegacySlotRank strong = Rate(In(Attack, 10, 1, Attack, 4, 1));
            Assert.That(strong.Tier, Is.EqualTo(LegacySlotTier.Major), "|10 - 4| / 10 = 0.6");
            Assert.That(strong.Has(LegacySlotHighlight.LargeClashGap), Is.True);
            LegacySlotRank weak = Rate(In(Attack, 4, 1, Attack, 10, 1));
            Assert.That((weak.Tier, weak.Score, weak.Highlights), Is.EqualTo((strong.Tier, strong.Score, strong.Highlights)),
                "The gap counts, not who had it.");
            Assert.That(Rate(In(Attack, 10, 1, Attack, 5, 1)).Tier, Is.EqualTo(LegacySlotTier.Normal), "0.5 stays Normal.");
            Assert.That(Rate(In(Attack, 3, 4, Attack, 30, 1)).Tier, Is.EqualTo(LegacySlotTier.Major), "Whole skills: 12 against 30.");
        }

        [Test]
        public void ADeepPierce_IsMajor_AShallowOneOrAHeldGuardIsNot()
        {
            LegacySlotRank deep = Rate(In(Attack, 10, 1, Guard, 3, 1));
            Assert.That((deep.Kind, deep.Tier), Is.EqualTo((LegacyClashKind.GuardPierced, LegacySlotTier.Major)), "(10 - 3) / 10 = 0.7");
            Assert.That(deep.Has(LegacySlotHighlight.DeepPierce), Is.True);
            Assert.That(Rate(In(Guard, 3, 1, Attack, 10, 1)).Tier, Is.EqualTo(LegacySlotTier.Major), "The enemy piercing the player too.");
            Assert.That(Rate(In(Attack, 10, 1, Guard, 4, 1)).Tier, Is.EqualTo(LegacySlotTier.Normal), "0.6");
            LegacySlotRank held = Rate(In(Attack, 5, 1, Guard, 9, 1));
            Assert.That((held.Kind, held.Tier), Is.EqualTo((LegacyClashKind.GuardHeld, LegacySlotTier.Normal)));
            LegacySlotRank pressed = Rate(new LegacyClashInput(0, Attack, 3, 2, Guard, 9, 1, playerPressured: true));
            Assert.That((pressed.Kind, pressed.Tier), Is.EqualTo((LegacyClashKind.GuardPierced, LegacySlotTier.Normal)),
                "A pressed attack pierces by half: not deep.");
        }

        [TestCase(25, 1, 100, true)]
        [TestCase(24, 1, 100, false)]
        [TestCase(5, 5, 100, true)]
        [TestCase(22, 1, 90, false)]
        [TestCase(23, 1, 90, true)]
        [TestCase(80, 1, 0, false)]
        public void AnUnopposedAttack_IsMajor_FromAQuarterOfTheTargetsMaximumHealth(int perHit, int hits, int maxHealth, bool major)
        {
            foreach (LegacyClashAction target in new[] { Empty, Breath })
            {
                LegacySlotRank rank = LegacySlotImportance.Rate(new LegacySlotFacts(In(Attack, perHit, hits, target, 0, 1),
                    playerMaxHealth: 100, enemyMaxHealth: maxHealth));
                Assert.That(rank.Kind, Is.EqualTo(LegacyClashKind.Unopposed));
                Assert.That(rank.Has(LegacySlotHighlight.BigUnopposed), Is.EqualTo(major));
                Assert.That(rank.Tier, Is.EqualTo(major ? LegacySlotTier.Major : LegacySlotTier.Normal));
                LegacySlotRank mirrored = LegacySlotImportance.Rate(new LegacySlotFacts(In(target, 0, 1, Attack, perHit, hits),
                    playerMaxHealth: maxHealth, enemyMaxHealth: 100));
                Assert.That((mirrored.Tier, mirrored.Score), Is.EqualTo((rank.Tier, rank.Score)), "The enemy's attack on the player.");
            }
        }

        [Test]
        public void ADodge_IsMajor_UnlessSwitchedOff()
        {
            LegacySlotFacts facts = Facts(In(Guard, 5, 1, Attack, 12, 1, playerDodged: true));
            LegacySlotRank rank = LegacySlotImportance.Rate(facts);
            Assert.That((rank.Kind, rank.Tier), Is.EqualTo((LegacyClashKind.Dodged, LegacySlotTier.Major)));
            Assert.That(rank.Has(LegacySlotHighlight.Dodge), Is.True);
            LegacySlotRank off = LegacySlotImportance.Rate(facts, Tuning(dodge: false));
            Assert.That((off.Tier, off.Has(LegacySlotHighlight.Dodge)), Is.EqualTo((LegacySlotTier.Normal, true)));
            Assert.That(off.Score, Is.EqualTo(rank.Score), "The switch moves the tier only.");
        }

        [Test]
        public void ABreak_IsMajor_OnEitherSide_EvenWithoutAnAttack()
        {
            LegacySlotRank effectBreak = Rate(new LegacyClashInput(0, Guard, 6, 1, Guard, 6, 1, enemyBroken: true));
            Assert.That((effectBreak.Kind, effectBreak.Tier), Is.EqualTo((LegacyClashKind.Settle, LegacySlotTier.Major)));
            Assert.That(effectBreak.Has(LegacySlotHighlight.Break), Is.True);
            Assert.That(effectBreak.PlayerReads, Is.EqualTo(LegacyReadEvent.Break), "The player broke the enemy: 간파.");
            LegacySlotRank broken = Rate(new LegacyClashInput(0, Attack, 10, 1, Attack, 9, 1, playerBroken: true));
            Assert.That(broken.Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That((broken.PlayerReads, broken.EnemyReads), Is.EqualTo((LegacyReadEvent.None, LegacyReadEvent.Break)));
            LegacySlotRank both = Rate(new LegacyClashInput(0, Attack, 10, 1, Attack, 9, 1, playerBroken: true, enemyBroken: true));
            Assert.That(both.Score - broken.Score, Is.EqualTo(LegacySlotImportance.BreakPoints + LegacySlotImportance.PlayerReadPoints));
        }

        [Test]
        public void AHeavyHit_IsMajor_JudgedHitByHit()
        {
            LegacyClashInput clash = In(Attack, 10, 1, Attack, 9, 1);
            LegacySlotRank heavy = LegacySlotImportance.Rate(Facts(clash, enemyLost: 25, enemyLargest: 25));
            Assert.That((heavy.Tier, heavy.Has(LegacySlotHighlight.HeavyHit)), Is.EqualTo((LegacySlotTier.Major, true)));
            LegacySlotRank many = LegacySlotImportance.Rate(Facts(clash, enemyLost: 48, enemyLargest: 24));
            Assert.That((many.Tier, many.Has(LegacySlotHighlight.HeavyHit)), Is.EqualTo((LegacySlotTier.Normal, false)),
                "Two hits of 24 do not make one heavy hit.");
            Assert.That(many.Score, Is.GreaterThan(heavy.Score - LegacySlotImportance.HeavyHitPoints),
                "The health lost still counts toward the score.");
            Assert.That(LegacySlotImportance.Rate(Facts(clash, playerLost: 30, playerLargest: 30)).Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That(LegacySlotImportance.Rate(Facts(clash, enemyLost: 25, enemyLargest: 25), Tuning(heavyPercent: 0)).Tier,
                Is.EqualTo(LegacySlotTier.Normal), "0 switches the rule off.");
            Assert.That(LegacySlotImportance.Rate(Facts(clash, enemyLost: 20, enemyLargest: 20), Tuning(heavyPercent: 20)).Tier,
                Is.EqualTo(LegacySlotTier.Major));
        }

        [Test]
        public void AFinishingBlow_IsClimax_ForEitherFighter()
        {
            LegacyClashInput clash = In(Attack, 30, 1, Breath, 0, 1);
            LegacySlotRank enemyFalls = LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerMaxHealth: 100, enemyMaxHealth: 100,
                enemyHealthLost: 30, enemyLargestHitTaken: 30, enemyDefeated: true));
            Assert.That(enemyFalls.Tier, Is.EqualTo(LegacySlotTier.Climax));
            Assert.That(enemyFalls.Has(LegacySlotHighlight.FinishingBlow), Is.True);
            LegacySlotRank playerFalls = LegacySlotImportance.Rate(new LegacySlotFacts(In(Guard, 3, 1, Attack, 6, 1),
                playerMaxHealth: 100, enemyMaxHealth: 100, playerHealthLost: 3, playerLargestHitTaken: 3, playerDefeated: true));
            Assert.That(playerFalls.Tier, Is.EqualTo(LegacySlotTier.Climax), "A small last hit is still the finale.");
            LegacySlotRank both = LegacySlotImportance.Rate(new LegacySlotFacts(In(Attack, 9, 1, Attack, 9, 1),
                playerMaxHealth: 100, enemyMaxHealth: 100, playerDefeated: true, enemyDefeated: true));
            Assert.That(both.Tier, Is.EqualTo(LegacySlotTier.Climax));
            Assert.That(both.Score, Is.GreaterThan(LegacySlotImportance.FinishingBlowPoints * 2 - 1));
        }

        [Test]
        public void EveryPlayerRead_LiftsTheSlotToMajor()
        {
            LegacyClashInput held = In(Guard, 6, 1, Attack, 4, 1);
            Assert.That(LegacySlotImportance.Rate(Facts(held)).Tier, Is.EqualTo(LegacySlotTier.Normal), "Without a read.");
            AssertRead(new LegacySlotFacts(held, playerConditionMatched: true), LegacyReadEvent.OpponentCondition);
            AssertRead(new LegacySlotFacts(held, playerStateConditionHeld: true), LegacyReadEvent.OpponentState);
            AssertRead(new LegacySlotFacts(new LegacyClashInput(0, Guard, 6, 1, Attack, 4, 1, enemyBroken: true)),
                LegacyReadEvent.Break);
            AssertRead(new LegacySlotFacts(In(Attack, 6, 1, Breath, 0, 1), playerMeshChainPosition: 2), LegacyReadEvent.MeshChain);
            AssertRead(new LegacySlotFacts(In(Attack, 6, 1, Breath, 0, 1), playerMeshChainPosition: 4), LegacyReadEvent.MeshChain);
            // A read without a hit: a guard whose condition the other guard meets.
            AssertRead(new LegacySlotFacts(In(Guard, 6, 1, Guard, 4, 1), playerConditionMatched: true),
                LegacyReadEvent.OpponentCondition);
            LegacySlotRank all = LegacySlotImportance.Rate(new LegacySlotFacts(
                new LegacyClashInput(0, Attack, 6, 1, Attack, 5, 1, enemyBroken: true), playerConditionMatched: true,
                playerStateConditionHeld: true, playerMeshChainPosition: 3));
            Assert.That(all.PlayerReads, Is.EqualTo(LegacyReadEvent.OpponentCondition | LegacyReadEvent.OpponentState |
                LegacyReadEvent.Break | LegacyReadEvent.MeshChain));
        }

        [Test]
        public void TheFirstSkillOfAChain_AndConditionsWithoutASkill_AreNoRead()
        {
            Assert.That(LegacySlotImportance.PlayerReads(new LegacySlotFacts(In(Attack, 6, 1, Breath, 0, 1),
                playerMeshChainPosition: 1)), Is.EqualTo(LegacyReadEvent.None), "The chain has not reached two yet.");
            foreach (LegacyClashAction none in new[] { Empty, Breath })
                Assert.That(LegacySlotImportance.PlayerReads(new LegacySlotFacts(In(none, 0, 1, Attack, 6, 1),
                    playerConditionMatched: true, playerStateConditionHeld: true)), Is.EqualTo(LegacyReadEvent.None));
            Assert.That(LegacySlotImportance.PlayerReads(LegacySlotFacts.None), Is.EqualTo(LegacyReadEvent.None));
        }

        [TestCase(1, 1, true)]
        [TestCase(2, 1, false)]
        [TestCase(2, 2, true)]
        [TestCase(3, 2, false)]
        [TestCase(3, 3, true)]
        [TestCase(0, 5, false)]
        public void TheChainDepthThatReads_IsTunable(int minimum, int position, bool reads)
        {
            var facts = new LegacySlotFacts(In(Attack, 6, 1, Breath, 0, 1), playerMeshChainPosition: position);
            Assert.That(LegacySlotImportance.PlayerReads(facts, minimum) == LegacyReadEvent.MeshChain, Is.EqualTo(reads));
            Assert.That(LegacySlotImportance.Rate(facts, Tuning(meshMinimum: minimum)).HasPlayerRead, Is.EqualTo(reads));
        }

        [Test]
        public void TheEnemysReads_AreTheMirror_AndOnlyScoreUnlessSwitchedOn()
        {
            var facts = new LegacySlotFacts(In(Attack, 6, 1, Guard, 5, 1), enemyConditionMatched: true, enemyStateConditionHeld: true);
            Assert.That(LegacySlotImportance.EnemyReads(facts), Is.EqualTo(LegacyReadEvent.OpponentCondition | LegacyReadEvent.OpponentState));
            Assert.That(LegacySlotImportance.EnemyReads(facts), Is.EqualTo(LegacySlotImportance.PlayerReads(facts.Mirrored())));
            LegacySlotRank rank = LegacySlotImportance.Rate(facts);
            Assert.That((rank.PlayerReads, rank.HasPlayerRead), Is.EqualTo((LegacyReadEvent.None, false)), "No 간파 cue for the player.");
            Assert.That((rank.Tier, rank.Has(LegacySlotHighlight.EnemyRead)), Is.EqualTo((LegacySlotTier.Normal, true)));
            LegacySlotRank plain = LegacySlotImportance.Rate(new LegacySlotFacts(In(Attack, 6, 1, Guard, 5, 1)));
            Assert.That(rank.Score - plain.Score, Is.EqualTo(2 * LegacySlotImportance.EnemyReadPoints));
            Assert.That(LegacySlotImportance.Rate(facts, Tuning(enemyReads: true)).Tier, Is.EqualTo(LegacySlotTier.Major));
            LegacySlotRank playerRead = LegacySlotImportance.Rate(facts.Mirrored());
            Assert.That(playerRead.Score, Is.GreaterThan(rank.Score), "The player's own reads weigh more.");
        }

        [Test]
        public void SignatureMoves_LiftEitherSide_AndTheListIsTunable()
        {
            Assert.That(LegacySlotImportance.DefaultSignatureSkillIds.ToArray(), Is.EqualTo(new[] { 51, 53, 56 }));
            LegacyClashInput clash = In(Attack, 9, 1, Attack, 8, 1);
            LegacySlotRank mine = LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerSkillId: 51, enemySkillId: 900));
            Assert.That((mine.Tier, mine.PlayerSignature, mine.EnemySignature), Is.EqualTo((LegacySlotTier.Major, true, false)));
            Assert.That(mine.Has(LegacySlotHighlight.Signature), Is.True);
            LegacySlotRank theirs = LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerSkillId: 900, enemySkillId: 56));
            Assert.That((theirs.Tier, theirs.PlayerSignature, theirs.EnemySignature), Is.EqualTo((LegacySlotTier.Major, false, true)));
            Assert.That(LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerSkillId: 53)).PlayerSignature, Is.True);
            Assert.That(LegacySlotImportance.Rate(new LegacySlotFacts(In(Empty, 0, 1, Attack, 8, 1), playerSkillId: 51)).PlayerSignature,
                Is.False, "No skill there, whatever the id says.");
            LegacySlotImportanceTuning none = LegacySlotImportanceTuning.Default.WithSignatures(null);
            Assert.That(LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerSkillId: 51), none).Tier, Is.EqualTo(LegacySlotTier.Normal));
            LegacySlotImportanceTuning custom = LegacySlotImportanceTuning.Default.WithSignatures(new[] { 14 });
            Assert.That(LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerSkillId: 14), custom).PlayerSignature, Is.True);
            Assert.That(LegacySlotImportance.Rate(new LegacySlotFacts(clash, playerSkillId: 51), custom).PlayerSignature, Is.False);
            Assert.That(LegacySlotImportance.IsSignature(AttackSkill(53, 5), LegacySlotImportance.DefaultSignatureSkillIds), Is.True);
            Assert.That(LegacySlotImportance.IsSignature(LegacyCommonActions.Breathe, new[] { -1 }), Is.False, "A breath never is.");
            Assert.That(LegacySlotImportance.IsSignature((LegacySkill)null, LegacySlotImportance.DefaultSignatureSkillIds), Is.False);
        }

        [Test]
        public void ABladeBind_IsAnEven합_ReadAsMutual()
        {
            foreach (LegacyClashInput even in new[] { In(Attack, 9, 1, Attack, 9, 1), In(Attack, 20, 1, Attack, 18, 1),
                In(Attack, 18, 1, Attack, 20, 1), In(Attack, 5, 2, Attack, 11, 1) })
            {
                LegacySlotRank rank = Rate(even);
                Assert.That((rank.IsBladeBind, rank.Tier), Is.EqualTo((true, LegacySlotTier.Normal)), even.ToString());
                Assert.That(Rate(even.Mirrored()).Score, Is.EqualTo(rank.Score));
            }
            Assert.That(Rate(In(Attack, 20, 1, Attack, 16, 1)).IsBladeBind, Is.False, "0.2 is no bind.");
            Assert.That(Rate(In(Attack, 9, 1, Guard, 9, 1)).IsBladeBind, Is.False, "Only attack against attack.");
            Assert.That(Rate(In(Attack, 9, 1, Attack, 9, 1, playerDodged: true)).IsBladeBind, Is.False, "A dodge is no bind.");
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 20, 1, Attack, 18, 1)), Tuning(bladeBind: 0f)).IsBladeBind, Is.False);
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 9, 1, Attack, 9, 1)), Tuning(bladeBind: 0f)).IsBladeBind, Is.True,
                "0: an exact tie only.");
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 9, 1, Attack, 9, 1)), Tuning(bladeBind: -1f)).IsBladeBind, Is.False);
        }

        [Test]
        public void NoSideWinsA합_TheRatingIsTheSameFromEitherSide()
        {
            foreach (LegacySlotFacts facts in Cases())
            {
                LegacySlotRank rank = LegacySlotImportance.Rate(facts), mirrored = LegacySlotImportance.Rate(facts.Mirrored());
                Assert.That((mirrored.PlayerReads, mirrored.EnemyReads), Is.EqualTo((rank.EnemyReads, rank.PlayerReads)), facts.ToString());
                Assert.That((mirrored.PlayerSignature, mirrored.EnemySignature), Is.EqualTo((rank.EnemySignature, rank.PlayerSignature)));
                Assert.That(mirrored.Kind, Is.EqualTo(rank.Kind));
                Assert.That(Swap(mirrored.Highlights), Is.EqualTo(rank.Highlights), facts.ToString());
                if (rank.PlayerReads == LegacyReadEvent.None && rank.EnemyReads == LegacyReadEvent.None)
                    Assert.That((mirrored.Tier, mirrored.Score), Is.EqualTo((rank.Tier, rank.Score)), facts.ToString());
                LegacySlotImportanceTuning symmetric = Tuning(enemyReads: true);
                Assert.That(LegacySlotImportance.Rate(facts.Mirrored(), symmetric).Tier,
                    Is.EqualTo(LegacySlotImportance.Rate(facts, symmetric).Tier), "Both sides' reads lift alike: " + facts);
            }
            string[] sided = { "Win", "Los", "Advantage", "Victory", "Defeat" };
            foreach (PropertyInfo property in typeof(LegacySlotRank).GetProperties())
                Assert.That(sided.Any(word => property.Name.Contains(word)), Is.False, "A rank names no winner: " + property.Name);
            foreach (string name in Enum.GetNames(typeof(LegacySlotHighlight)))
                Assert.That(sided.Any(word => name.Contains(word)), Is.False, name);
        }

        [Test]
        public void Outranks_HigherTierFirst_ThenScore_AndATieKeepsTheEarlier()
        {
            LegacySlotRank climax = Rank(LegacySlotTier.Climax, 100), major = Rank(LegacySlotTier.Major, 2000),
                majorLow = Rank(LegacySlotTier.Major, 400), majorSame = Rank(LegacySlotTier.Major, 400, slot: 3),
                normal = Rank(LegacySlotTier.Normal, 5000), minor = Rank(LegacySlotTier.Minor, 0);
            Assert.That(climax.Outranks(major), Is.True, "The tier first, whatever the score.");
            Assert.That(major.Outranks(normal), Is.True);
            Assert.That(normal.Outranks(major), Is.False);
            Assert.That(major.Outranks(majorLow), Is.True, "Then the score.");
            Assert.That(majorLow.Outranks(major), Is.False);
            Assert.That(majorSame.Outranks(majorLow) || majorLow.Outranks(majorSame), Is.False, "An exact tie keeps the earlier slot.");
            Assert.That(minor.Outranks(LegacySlotRank.None), Is.False, "A plain settle never replaces nothing.");
            Assert.That(normal.Outranks(LegacySlotRank.None), Is.True);
            // Kept as a replay would: the most important slot so far.
            LegacySlotRank kept = LegacySlotRank.None;
            foreach (LegacySlotRank next in new[] { normal, majorLow, majorSame, minor, major, normal })
                if (next.Outranks(kept)) kept = next;
            Assert.That(kept, Is.EqualTo(major));
        }

        [Test]
        public void TheScore_GrowsWithTheGapThePierceAndTheHealthLost()
        {
            int previous = -1;
            for (int weaker = 8; weaker >= 1; weaker--)
            {
                int score = Rate(In(Attack, 10, 1, Attack, weaker, 1)).Score;
                Assert.That(score, Is.GreaterThan(previous), "gap at " + weaker);
                previous = score;
            }
            previous = -1;
            for (int power = 3; power <= 15; power++)
            {
                int score = Rate(In(Attack, power, 1, Guard, 2, 1)).Score;
                Assert.That(score, Is.GreaterThanOrEqualTo(previous), "pierce at " + power);
                previous = score;
            }
            Assert.That(previous, Is.GreaterThan(Rate(In(Attack, 3, 1, Guard, 2, 1)).Score));
            previous = -1;
            for (int lost = 0; lost <= 120; lost += 5)
            {
                int score = LegacySlotImportance.Rate(Facts(In(Attack, 10, 1, Breath, 0, 1), enemyLost: lost)).Score;
                Assert.That(score, Is.GreaterThanOrEqualTo(previous), "health lost " + lost);
                previous = score;
            }
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 10, 1, Breath, 0, 1), enemyLost: 100)).Score,
                Is.EqualTo(LegacySlotImportance.Rate(Facts(In(Attack, 10, 1, Breath, 0, 1), enemyLost: 150)).Score),
                "Capped at the whole maximum health.");
        }

        [Test]
        public void Facts_MirrorBack_AndRefuseNonsense()
        {
            foreach (LegacySlotFacts facts in Cases())
            {
                Assert.That(facts.Mirrored().Mirrored(), Is.EqualTo(facts));
                Assert.That(facts.Mirrored().Mirrored().GetHashCode(), Is.EqualTo(facts.GetHashCode()));
            }
            Assert.That(LegacySlotFacts.None.HasSlot, Is.False);
            Assert.That(LegacySlotFacts.None.SlotIndex, Is.EqualTo(-1));
            LegacyClashInput clash = In(Attack, 5, 1, Attack, 5, 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacySlotFacts(clash, playerMaxHealth: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacySlotFacts(clash, enemyHealthLost: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacySlotFacts(clash, playerLargestHitTaken: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LegacySlotFacts(clash, enemyMeshChainPosition: -1));
        }

        [Test]
        public void TheTuning_DefaultsAndOffSwitches()
        {
            LegacySlotImportanceTuning tuning = LegacySlotImportanceTuning.Default;
            Assert.That((tuning.BladeBindGap, tuning.LargeClashGap, tuning.DeepPierce), Is.EqualTo((.15f, .6f, .7f)));
            Assert.That((tuning.BigUnopposedPercent, tuning.HeavyHitPercent, tuning.MeshChainMinimum),
                Is.EqualTo((25, LegacyDecisiveHit.DefaultHealthDamagePercent, 2)));
            Assert.That((tuning.DodgeRaisesToMajor, tuning.EnemyReadsRaiseTier), Is.EqualTo((true, false)));
            Assert.That(tuning.SignatureSkillIds, Is.SameAs(LegacySlotImportance.DefaultSignatureSkillIds));
            // default: every threshold off, so only breaks, falls and the player's 간파 lift a slot.
            var off = default(LegacySlotImportanceTuning);
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 30, 1, Attack, 1, 1)), off).Tier, Is.EqualTo(LegacySlotTier.Normal));
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 30, 1, Guard, 1, 1)), off).Tier, Is.EqualTo(LegacySlotTier.Normal));
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 90, 1, Empty, 0, 1)), off).Tier, Is.EqualTo(LegacySlotTier.Normal));
            Assert.That(LegacySlotImportance.Rate(Facts(In(Attack, 9, 1, Empty, 0, 1), enemyLost: 90, enemyLargest: 90), off).Tier,
                Is.EqualTo(LegacySlotTier.Normal));
            Assert.That(LegacySlotImportance.Rate(Facts(In(Guard, 5, 1, Attack, 9, 1, playerDodged: true)), off).Tier,
                Is.EqualTo(LegacySlotTier.Normal));
            Assert.That(LegacySlotImportance.Rate(Facts(new LegacyClashInput(0, Attack, 9, 1, Attack, 3, 1, enemyBroken: true)), off).Tier,
                Is.EqualTo(LegacySlotTier.Major));
            Assert.That(new LegacySlotImportanceTuning(float.NaN, float.NaN, -1f, 0, 0, 0, false, false, null).BladeBindGap, Is.LessThan(0f));
        }

        // ---- Through the resolvers (the duel view): campaign and versus report the facts the scorer reads. ----

        [TestCase(7, 5, true)]
        [TestCase(7, 1, false)]
        [TestCase(5, 1, true)]
        [TestCase(5, 9, false)]
        [TestCase(48, 3, true)]
        [TestCase(48, 1, false)]
        [TestCase(49, 1, true)]
        [TestCase(49, 3, false)]
        [TestCase(42, 9, true)]
        [TestCase(42, 1, false)]
        [TestCase(6, 8, true)]
        public void CampaignDuel_ASkillsOpponentCondition_ReadsAgainstTheRightOpponent(int playerSkillId, int enemySkillId, bool reads)
        {
            LegacySkill player = LegacySkillDefinitions.Skill(playerSkillId), enemy = LegacySkillDefinitions.Skill(enemySkillId);
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { player }, new[] { enemy }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(player.LaneIndex), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacySlotFacts facts = ((ILegacyDuelView)duel).CurrentSlotFacts;
            Assert.That((facts.PlayerSkillId, facts.EnemySkillId), Is.EqualTo((playerSkillId, enemySkillId)));
            Assert.That(facts.PlayerConditionMatched, Is.EqualTo(reads));
            Assert.That(facts.PlayerConditionMatched, Is.EqualTo(duel.CurrentSlot.PlayerFeedback.ConditionMet),
                "The read is the rules' own match.");
            LegacySlotRank rank = LegacySlotImportance.Rate(facts);
            Assert.That((rank.PlayerReads & LegacyReadEvent.OpponentCondition) != 0, Is.EqualTo(reads));
            if (reads) Assert.That(rank.Tier, Is.GreaterThanOrEqualTo(LegacySlotTier.Major));
        }

        [Test]
        public void CampaignDuel_TheEnemysCondition_IsTheEnemysRead()
        {
            // The enemy's 기세 꺾기 meets the player's attack: the enemy read it, not the player.
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(940, 5) },
                new[] { LegacySkillDefinitions.Skill(5) }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacySlotFacts facts = duel.CurrentSlotFacts;
            Assert.That((facts.PlayerConditionMatched, facts.EnemyConditionMatched), Is.EqualTo((false, true)));
            LegacySlotRank rank = LegacySlotImportance.Rate(facts);
            Assert.That((rank.PlayerReads, rank.EnemyReads), Is.EqualTo((LegacyReadEvent.None, LegacyReadEvent.OpponentCondition)));
        }

        [Test]
        public void CampaignDuel_끝내기_ReadsAnEnemyAtAThirdOfItsHealth_FromTheSlotsStart()
        {
            LegacySkill finisher = LegacySkillDefinitions.Skill(50);
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(941, 75), finisher },
                new[] { LegacyCommonActions.Breathe }, new[] { 2 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            Assert.That(duel.TryQueueLane(finisher.LaneIndex), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.CurrentSlotFacts.PlayerStateConditionHeld, Is.False, "Full health: no.");
            duel.ResolveNextHit();
            duel.CompleteCurrentSlot();
            Assert.That(duel.Enemy.Health, Is.EqualTo(25), "75 unopposed.");
            duel.BeginNextSlot();
            LegacySlotFacts facts = duel.CurrentSlotFacts;
            Assert.That((facts.PlayerSkillId, facts.PlayerStateConditionHeld), Is.EqualTo((50, true)), "25 of 100 is at most 30%.");
            Assert.That(LegacySlotImportance.Rate(facts).PlayerReads, Is.EqualTo(LegacyReadEvent.OpponentState));
            duel.ResolveNextHit();
            Assert.That(duel.CurrentSlotFacts.PlayerStateConditionHeld, Is.True);

            var healthy = new LegacyQueuedDuel(100, 50, 100, 50, new[] { finisher }, new[] { LegacyCommonActions.Breathe },
                new[] { 1 }, meshPercent: 0);
            Assert.That(healthy.TryQueueLane(finisher.LaneIndex), Is.True);
            healthy.Commit();
            healthy.BeginNextSlot();
            healthy.ResolveNextHit();
            Assert.That(healthy.CurrentSlotFacts.PlayerStateConditionHeld, Is.False);
        }

        [Test]
        public void CampaignDuel_르프리즈_ReadsABrokenEnemy_EvenWithNothingToRestore()
        {
            LegacySkill reprise = LegacySkillDefinitions.Skill(19);
            var duel = new LegacyQueuedDuel(100, 50, 100, 0, new[] { reprise }, new[] { AttackSkill(942, 3) }, new[] { 1 },
                meshPercent: 0);
            Assert.That(duel.TryQueueLane(reprise.LaneIndex), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(duel.CurrentSlot.PlayerFeedback.ResistanceRestored, Is.Zero, "Full resistance: nothing restored.");
            LegacySlotFacts facts = duel.CurrentSlotFacts;
            Assert.That(facts.PlayerStateConditionHeld, Is.True, "The enemy is broken: the read held.");
            Assert.That(LegacySlotImportance.Rate(facts).PlayerReads, Is.EqualTo(LegacyReadEvent.OpponentState));

            var whole = new LegacyQueuedDuel(100, 50, 100, 15, new[] { reprise }, new[] { AttackSkill(942, 3) }, new[] { 1 },
                meshPercent: 0);
            Assert.That(whole.TryQueueLane(reprise.LaneIndex), Is.True);
            whole.Commit();
            whole.BeginNextSlot();
            Assert.That(whole.CurrentSlotFacts.PlayerStateConditionHeld, Is.False);
        }

        [Test]
        public void CampaignDuel_ABreakingHeavyHit_ShowsInTheFacts_AsItLands()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 5, new[] { AttackSkill(943, 30) }, new[] { AttackSkill(944, 4) },
                new[] { 1 }, meshPercent: 0);
            ILegacyDuelView view = duel;
            Assert.That(view.CurrentSlotFacts, Is.EqualTo(LegacySlotFacts.None), "Nothing resolves while planning.");
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            LegacySlotFacts start = view.CurrentSlotFacts;
            Assert.That(start.Clash, Is.EqualTo(view.CurrentClash));
            Assert.That((start.PlayerMaxHealth, start.EnemyMaxHealth, start.EnemyHealthLost), Is.EqualTo((100, 100, 0)));
            Assert.That(LegacySlotImportance.Rate(start).Has(LegacySlotHighlight.LargeClashGap), Is.True, "(30 - 4) / 30");
            duel.ResolveNextHit();
            LegacySlotFacts after = view.CurrentSlotFacts;
            Assert.That((after.EnemyHealthLost, after.EnemyLargestHitTaken), Is.EqualTo((25, 25)), "30 against 5 resistance: 25 over.");
            Assert.That((after.PlayerHealthLost, after.Clash.EnemyBroken, after.EnemyDefeated), Is.EqualTo((0, true, false)));
            LegacySlotRank rank = LegacySlotImportance.Rate(after);
            Assert.That(rank.Tier, Is.EqualTo(LegacySlotTier.Major));
            Assert.That(rank.Has(LegacySlotHighlight.Break) && rank.Has(LegacySlotHighlight.HeavyHit), Is.True);
            Assert.That(rank.PlayerReads, Is.EqualTo(LegacyReadEvent.Break));
            Assert.That(view.CurrentSlotFacts, Is.EqualTo(after), "Reading changes nothing.");
            Assert.That(duel.Enemy.Health, Is.EqualTo(75));
            duel.CompleteCurrentSlot();
            Assert.That(view.CurrentSlotFacts, Is.EqualTo(LegacySlotFacts.None));
        }

        [Test]
        public void CampaignDuel_AMultiHitSlot_KeepsItsLargestHitApartFromItsTotal()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(945, 30, hits: 3) },
                new[] { LegacyCommonActions.Breathe }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
            LegacySlotFacts facts = duel.CurrentSlotFacts;
            Assert.That((facts.EnemyHealthLost, facts.EnemyLargestHitTaken), Is.EqualTo((30, 10)));
            LegacySlotRank rank = LegacySlotImportance.Rate(facts);
            Assert.That(rank.Has(LegacySlotHighlight.HeavyHit), Is.False, "Three hits of 10, none of them heavy.");
            Assert.That((rank.Has(LegacySlotHighlight.BigUnopposed), rank.Tier), Is.EqualTo((true, LegacySlotTier.Major)),
                "The whole skill, 30, is big against 100.");
        }

        [Test]
        public void CampaignDuel_TheFinishingBlow_IsClimax_UntilTheSlotCompletes()
        {
            var duel = new LegacyQueuedDuel(100, 50, 40, 50, new[] { AttackSkill(946, 50) },
                new[] { LegacyCommonActions.Breathe }, new[] { 1 }, meshPercent: 0);
            Assert.That(duel.TryQueueLane(0), Is.True);
            duel.Commit();
            duel.BeginNextSlot();
            Assert.That(LegacySlotImportance.Rate(duel.CurrentSlotFacts).Tier, Is.EqualTo(LegacySlotTier.Major), "Before the blow.");
            duel.ResolveNextHit();
            LegacySlotFacts facts = duel.CurrentSlotFacts;
            Assert.That((facts.EnemyDefeated, facts.PlayerDefeated), Is.EqualTo((true, false)));
            Assert.That(LegacySlotImportance.Rate(facts).Tier, Is.EqualTo(LegacySlotTier.Climax));
            duel.CompleteCurrentSlot();
            Assert.That(duel.IsFinished, Is.True);
            Assert.That(duel.CurrentSlotFacts.HasSlot, Is.False);
        }

        [Test]
        public void CampaignDuel_AMeshingChain_ReadsFromItsSecondSkill()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50,
                new[] { AttackSkill(947, 5, lane: 0), AttackSkill(948, 5, lane: 1), AttackSkill(949, 5, lane: 2) },
                new[] { LegacyCommonActions.Breathe }, new[] { 3 });
            for (int lane = 0; lane < 3; lane++) Assert.That(duel.TryQueueLane(lane), Is.True);
            duel.Commit();
            for (int slot = 0; slot < 3; slot++)
            {
                duel.BeginNextSlot();
                LegacySlotFacts facts = duel.CurrentSlotFacts;
                Assert.That(facts.PlayerMeshChainPosition, Is.EqualTo(slot + 1));
                Assert.That(facts.EnemyMeshChainPosition, Is.Zero, "The enemy never meshes.");
                bool reads = (LegacySlotImportance.Rate(facts).PlayerReads & LegacyReadEvent.MeshChain) != 0;
                Assert.That(reads, Is.EqualTo(slot >= 1), "slot " + slot);
                while (!duel.IsCurrentSlotResolved) duel.ResolveNextHit();
                duel.CompleteCurrentSlot();
            }
        }

        [Test]
        public void CampaignDuel_APendingCounter_HasNoSkillUntilItStrikes()
        {
            var duel = new LegacyQueuedDuel(100, 50, 100, 50, new[] { AttackSkill(950, 10) }, new[] { AttackSkill(951, 6) },
                new[] { 1 }, playerCounter: new LegacyCounter(AttackSkill(952, 9)), meshPercent: 0);
            duel.Commit();
            duel.BeginNextSlot();
            LegacySlotFacts start = duel.CurrentSlotFacts;
            Assert.That((start.PlayerSkillId, start.Clash.PlayerAction), Is.EqualTo((0, Empty)));
            duel.ResolveNextHit();
            LegacySlotFacts struck = duel.CurrentSlotFacts;
            Assert.That((struck.PlayerSkillId, struck.Clash.PlayerCountered), Is.EqualTo((952, true)));
            Assert.That(LegacySlotImportance.Rate(struck).Kind, Is.EqualTo(LegacyClashKind.Clash));
        }

        [Test]
        public void Versus_TheGuestReadsTheHostsFactsMirrored()
        {
            var match = new LocalVersusMatch(new[] { AttackSkill(953, 30) }, new[] { AttackSkill(954, 4) }, 100, 50, 100, 5,
                randomSeed: 17);
            ILegacyDuelView host = new LocalVersusPerspective(match, 0), guest = new LocalVersusPerspective(match, 1);
            Assert.That(host.CurrentSlotFacts, Is.EqualTo(LegacySlotFacts.None));
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, 0), Is.True);
            Commit(match);
            match.BeginNextSlot();
            Assert.That(guest.CurrentSlotFacts, Is.EqualTo(host.CurrentSlotFacts.Mirrored()));
            match.ResolveNextHit();
            LegacySlotFacts hostFacts = host.CurrentSlotFacts, guestFacts = guest.CurrentSlotFacts;
            Assert.That(guestFacts, Is.EqualTo(hostFacts.Mirrored()));
            Assert.That((hostFacts.EnemyHealthLost, hostFacts.EnemyLargestHitTaken, hostFacts.Clash.EnemyBroken),
                Is.EqualTo((25, 25, true)));
            Assert.That((guestFacts.PlayerHealthLost, guestFacts.Clash.PlayerBroken), Is.EqualTo((25, true)));
            Assert.That((hostFacts.PlayerMeshChainPosition, hostFacts.EnemyMeshChainPosition), Is.EqualTo((0, 0)), "Versus never meshes.");
            LegacySlotRank hostRank = LegacySlotImportance.Rate(hostFacts), guestRank = LegacySlotImportance.Rate(guestFacts);
            Assert.That((hostRank.PlayerReads, guestRank.EnemyReads), Is.EqualTo((LegacyReadEvent.Break, LegacyReadEvent.Break)));
            Assert.That(guestRank.PlayerReads, Is.EqualTo(LegacyReadEvent.None), "The guest was broken: no 간파 cue on its screen.");
            Assert.That((hostRank.Tier, guestRank.Tier), Is.EqualTo((LegacySlotTier.Major, LegacySlotTier.Major)));
            Assert.Throws<ArgumentOutOfRangeException>(() => match.CurrentSlotFacts(2));
        }

        [Test]
        public void Versus_ConditionsAndStates_ReadForEachViewer()
        {
            var match = new LocalVersusMatch(new[] { LegacySkillDefinitions.Skill(7) }, new[] { LegacySkillDefinitions.Skill(5) },
                randomSeed: 17);
            var host = new LocalVersusPerspective(match, 0);
            var guest = new LocalVersusPerspective(match, 1);
            Assert.That(match.TryQueueLane(0, LegacySkillDefinitions.Skill(7).LaneIndex), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryQueueLane(1, LegacySkillDefinitions.Skill(5).LaneIndex), Is.True);
            Commit(match);
            match.BeginNextSlot();
            Assert.That((host.CurrentSlotFacts.PlayerConditionMatched, host.CurrentSlotFacts.EnemyConditionMatched),
                Is.EqualTo((true, false)), "막기 meets 기세 꺾기's 타격; 기세 꺾기 wants an attack and meets a guard.");
            Assert.That(LegacySlotImportance.Rate(host.CurrentSlotFacts).PlayerReads, Is.EqualTo(LegacyReadEvent.OpponentCondition));
            Assert.That(LegacySlotImportance.Rate(guest.CurrentSlotFacts).PlayerReads, Is.EqualTo(LegacyReadEvent.None));
            Assert.That(LegacySlotImportance.Rate(guest.CurrentSlotFacts).EnemyReads, Is.EqualTo(LegacyReadEvent.OpponentCondition));

            var broken = new LocalVersusMatch(new[] { LegacySkillDefinitions.Skill(19) }, new[] { AttackSkill(955, 3) },
                100, 50, 100, 0, randomSeed: 17);
            Assert.That(broken.TryQueueLane(0, LegacySkillDefinitions.Skill(19).LaneIndex), Is.True);
            Assert.That(broken.TryCommit(0), Is.True);
            Assert.That(broken.TryQueueLane(1, 0), Is.True);
            Commit(broken);
            broken.BeginNextSlot();
            Assert.That(broken.CurrentSlotFacts(0).PlayerStateConditionHeld, Is.True, "르프리즈 against a broken right side.");
            Assert.That(broken.CurrentSlotFacts(1).EnemyStateConditionHeld, Is.True);
            Assert.That(broken.CurrentSlotFacts(1).PlayerStateConditionHeld, Is.False);
        }

        [Test]
        public void Versus_AFall_IsClimaxOnBothScreens()
        {
            var match = new LocalVersusMatch(new[] { AttackSkill(956, 60) }, new[] { AttackSkill(957, 5) }, 100, 50, 50, 50,
                randomSeed: 17);
            Assert.That(match.TryQueueLane(0, 0), Is.True);
            Assert.That(match.TryCommit(0), Is.True);
            Assert.That(match.TryCommit(1), Is.True);
            match.BeginNextSlot();
            match.ResolveNextHit();
            LegacySlotFacts host = match.CurrentSlotFacts(0), guest = match.CurrentSlotFacts(1);
            Assert.That((host.EnemyDefeated, guest.PlayerDefeated), Is.EqualTo((true, true)));
            Assert.That((LegacySlotImportance.Rate(host).Tier, LegacySlotImportance.Rate(guest).Tier),
                Is.EqualTo((LegacySlotTier.Climax, LegacySlotTier.Climax)));
        }

        private static void AssertRead(LegacySlotFacts facts, LegacyReadEvent read)
        {
            LegacySlotRank rank = LegacySlotImportance.Rate(facts);
            Assert.That(rank.PlayerReads, Is.EqualTo(read), facts.ToString());
            Assert.That((rank.HasPlayerRead, rank.Has(LegacySlotHighlight.PlayerRead)), Is.EqualTo((true, true)));
            Assert.That(rank.Tier, Is.EqualTo(LegacySlotTier.Major), facts.ToString());
        }

        // One case per rule, from both sides where it differs.
        private static IEnumerable<LegacySlotFacts> Cases()
        {
            yield return Facts(In(Guard, 6, 1, Guard, 9, 1));
            yield return Facts(In(Attack, 12, 1, Attack, 8, 1));
            yield return Facts(In(Attack, 9, 1, Attack, 9, 1));
            yield return Facts(In(Attack, 30, 1, Attack, 3, 1));
            yield return Facts(In(Attack, 10, 1, Guard, 3, 1));
            yield return Facts(In(Attack, 5, 1, Guard, 8, 1));
            yield return Facts(new LegacyClashInput(0, Attack, 3, 2, Guard, 9, 1, playerPressured: true));
            yield return Facts(In(Attack, 30, 1, Empty, 0, 1));
            yield return Facts(In(Breath, 0, 1, Attack, 7, 2));
            yield return Facts(In(Guard, 5, 1, Attack, 12, 1, playerDodged: true));
            yield return Facts(new LegacyClashInput(0, Attack, 10, 1, Attack, 9, 1, enemyBroken: true));
            yield return Facts(In(Attack, 10, 1, Attack, 9, 1), enemyLost: 30, enemyLargest: 30);
            yield return Facts(In(Attack, 10, 1, Attack, 9, 1), playerLost: 12, playerLargest: 6);
            yield return new LegacySlotFacts(In(Guard, 6, 1, Attack, 4, 1), playerSkillId: 7, enemySkillId: 5,
                playerMaxHealth: 100, enemyMaxHealth: 80, playerConditionMatched: true);
            yield return new LegacySlotFacts(In(Attack, 8, 1, Breath, 0, 1), playerMaxHealth: 100, enemyMaxHealth: 100,
                playerStateConditionHeld: true, playerMeshChainPosition: 2);
            yield return new LegacySlotFacts(In(Attack, 8, 1, Attack, 7, 1), playerSkillId: 56, enemySkillId: 3,
                playerMaxHealth: 100, enemyMaxHealth: 100);
            yield return new LegacySlotFacts(In(Attack, 8, 1, Breath, 0, 1), playerMaxHealth: 100, enemyMaxHealth: 100,
                enemyHealthLost: 8, enemyLargestHitTaken: 8, enemyDefeated: true);
        }

        private static LegacySlotHighlight Swap(LegacySlotHighlight highlights)
        {
            bool player = (highlights & LegacySlotHighlight.PlayerRead) != 0, enemy = (highlights & LegacySlotHighlight.EnemyRead) != 0;
            highlights &= ~(LegacySlotHighlight.PlayerRead | LegacySlotHighlight.EnemyRead);
            return highlights | (player ? LegacySlotHighlight.EnemyRead : 0) | (enemy ? LegacySlotHighlight.PlayerRead : 0);
        }

        private static LegacySlotRank Rank(LegacySlotTier tier, int score, int slot = 0)
            => new LegacySlotRank(slot, tier, score, LegacySlotHighlight.None, LegacyReadEvent.None, LegacyReadEvent.None, false,
                false, LegacyClashKind.Clash);

        private static LegacySlotRank Rate(LegacyClashInput input) => LegacySlotImportance.Rate(Facts(input));

        private static LegacySlotFacts Facts(LegacyClashInput clash, int playerLost = 0, int playerLargest = 0,
            int enemyLost = 0, int enemyLargest = 0)
            => new LegacySlotFacts(clash, playerMaxHealth: 100, enemyMaxHealth: 100, playerHealthLost: playerLost,
                enemyHealthLost: enemyLost, playerLargestHitTaken: playerLargest, enemyLargestHitTaken: enemyLargest);

        private static LegacySlotImportanceTuning Tuning(float bladeBind = LegacySlotImportanceTuning.DefaultBladeBindGap,
            int heavyPercent = LegacyDecisiveHit.DefaultHealthDamagePercent,
            int meshMinimum = LegacySlotImportanceTuning.DefaultMeshChainMinimum, bool dodge = true, bool enemyReads = false)
            => new LegacySlotImportanceTuning(bladeBind, LegacySlotImportanceTuning.DefaultLargeClashGap,
                LegacySlotImportanceTuning.DefaultDeepPierce, LegacySlotImportanceTuning.DefaultBigUnopposedPercent, heavyPercent,
                meshMinimum, dodge, enemyReads, LegacySlotImportance.DefaultSignatureSkillIds);

        private static LegacyClashInput In(LegacyClashAction player, int playerPower, int playerHits,
            LegacyClashAction enemy, int enemyPower, int enemyHits, bool playerDodged = false)
            => new LegacyClashInput(0, player, playerPower, playerHits, enemy, enemyPower, enemyHits, playerDodged);

        private static LegacySkill AttackSkill(int id, int power, int hits = 1, int lane = 0)
            => new LegacySkill(id, "importance attack", 1, power, power, LegacySkillKind.Attack, LegacySkillProperty.Slash, hits,
                lane, "");

        private static void Commit(LocalVersusMatch match)
        {
            while (match.Phase == LegacyDuelPhase.Planning)
                Assert.That(match.TryCommit(match.CurrentPlanner), Is.True);
        }
    }
}
