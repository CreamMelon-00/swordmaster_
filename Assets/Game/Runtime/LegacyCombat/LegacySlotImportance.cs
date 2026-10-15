using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>How much a resolving slot matters to the presentation director (<see cref="LegacySlotImportance.Rate"/>): a
    /// <see cref="Minor"/> slot may play faster, a <see cref="Major"/> one earns the closer camera and the longer hit stop,
    /// and <see cref="Climax"/> is the finishing blow's finale. Presentation only: the rules never read it.</summary>
    public enum LegacySlotTier
    {
        /// <summary>No attack on either side: guard against guard, a breath, an empty slot (the cards settle at most).</summary>
        Minor,
        /// <summary>An attack, and nothing that lifts the slot further.</summary>
        Normal,
        /// <summary>A big moment: a break, a heavy hit, a large 합 gap, a deep pierce, a big unopposed attack, a dodge, a
        /// player's 간파, a signature move (<see cref="LegacySlotHighlight"/>).</summary>
        Major,
        /// <summary>A fighter falls in this slot: the finishing blow.</summary>
        Climax
    }

    /// <summary>간파 (a read paying off) in a resolving slot, for one side (<see cref="LegacySlotImportance.PlayerReads"/>).
    /// The screen cues only the player's own; each one lifts the slot to at least <see cref="LegacySlotTier.Major"/>.</summary>
    [Flags]
    public enum LegacyReadEvent
    {
        None = 0,
        /// <summary>The side's skill has a 상대 속성 조건 or 상대 종류 조건 and the opposing skill in this slot meets it
        /// (막기 against 타격, 기세 꺾기 against an attack, 받아치기 against 관통, 비껴서기 against 참격, 쿠페 and 허점 노리기
        /// against a guard), whether or not its effect then changed anything.</summary>
        OpponentCondition = 1,
        /// <summary>The side's skill has a 상대 상태 조건 and it held against the other fighter when the rules checked it this
        /// slot (르프리즈 while the other is broken, 끝내기 at 체력 30% 이하), even if the effect had nothing to do (a
        /// recovery at full resistance, a multiplier on a hit the resistance absorbed).</summary>
        OpponentState = 2,
        /// <summary>The side broke the other's resistance in this slot (by a hit or an effect).</summary>
        Break = 4,
        /// <summary>The side's skill sits at least <see cref="LegacySlotImportanceTuning.MeshChainMinimum"/> deep in a 맞물림
        /// chain (by default from the chain's second skill on). Only the campaign's player meshes.</summary>
        MeshChain = 8,
    }

    /// <summary>What a slot's rating saw (<see cref="LegacySlotRank.Highlights"/>). Which of them lift the slot to
    /// <see cref="LegacySlotTier.Major"/> is told on each; none of them says which side came out ahead, since both sides of
    /// a 합 deal damage.</summary>
    [Flags]
    public enum LegacySlotHighlight
    {
        None = 0,
        /// <summary>Someone attacks: the slot is at least <see cref="LegacySlotTier.Normal"/>.</summary>
        Attack = 1,
        /// <summary>A 합 whose relative power gap reaches <see cref="LegacySlotImportanceTuning.LargeClashGap"/>. Major.</summary>
        LargeClashGap = 2,
        /// <summary>An attack piercing a guard by at least <see cref="LegacySlotImportanceTuning.DeepPierce"/> of itself. Major.</summary>
        DeepPierce = 4,
        /// <summary>An attack against nothing (an empty slot, a breath) whose whole power reaches
        /// <see cref="LegacySlotImportanceTuning.BigUnopposedPercent"/> of the target's maximum health. Major.</summary>
        BigUnopposed = 8,
        /// <summary>An attack was dodged (a timed dodge or 자동 회피). Major while
        /// <see cref="LegacySlotImportanceTuning.DodgeRaisesToMajor"/>.</summary>
        Dodge = 16,
        /// <summary>A fighter's resistance broke in this slot. Major.</summary>
        Break = 32,
        /// <summary>One hit took at least <see cref="LegacySlotImportanceTuning.HeavyHitPercent"/> of its target's maximum
        /// health (the decisive close-up's threshold, <see cref="LegacyDecisiveHit"/>). Major.</summary>
        HeavyHit = 64,
        /// <summary>The player's 간파 (<see cref="LegacySlotRank.PlayerReads"/>). Major.</summary>
        PlayerRead = 128,
        /// <summary>The enemy's 간파 (<see cref="LegacySlotRank.EnemyReads"/>). Major only while
        /// <see cref="LegacySlotImportanceTuning.EnemyReadsRaiseTier"/>.</summary>
        EnemyRead = 256,
        /// <summary>A signature move (<see cref="LegacySlotImportanceTuning.SignatureSkillIds"/>) on either side. Major.</summary>
        Signature = 512,
        /// <summary>A fighter fell in this slot. Climax.</summary>
        FinishingBlow = 1024,
        /// <summary>An even 합: attack against attack with a relative gap of at most
        /// <see cref="LegacySlotImportanceTuning.BladeBindGap"/>, held as a mutual struggle (팽팽한 합). It lifts nothing.</summary>
        BladeBind = 2048,
    }

    /// <summary>What the director reads of a resolving slot, from the player's side: the clash (<see cref="LegacyClashInput"/>:
    /// actions, rolled powers, a dodge, a break), the skills' ids, both fighters' maximum health and what each lost so far,
    /// its largest single hit, a fall, each side's met conditions and the player's 맞물림 depth. Read it from
    /// <see cref="ILegacyDuelView.CurrentSlotFacts"/> (a versus guest's is mirrored); it changes as the slot plays, so read
    /// it again after each hit. Reading never changes the duel.</summary>
    public readonly struct LegacySlotFacts : IEquatable<LegacySlotFacts>
    {
        /// <summary>No slot resolving.</summary>
        public static LegacySlotFacts None => new LegacySlotFacts(LegacyClashInput.None);

        /// <param name="clash">The slot as the clash motion reads it.</param>
        /// <param name="playerSkillId">The player's skill's id in this slot (a filled 반격 included), 0 for none.</param>
        /// <param name="playerMaxHealth">The player's maximum health; 0 leaves the health-share rules out.</param>
        /// <param name="playerHealthLost">The health the player has lost since the slot began.</param>
        /// <param name="playerLargestHitTaken">The most health the player lost to a single hit of this slot.</param>
        /// <param name="playerDefeated">The player's health went from above zero to zero in this slot.</param>
        /// <param name="playerConditionMatched">The player's skill's 상대 속성/종류 조건 is met by the enemy's skill.</param>
        /// <param name="playerStateConditionHeld">The player's skill's 상대 상태 조건 held against the enemy when the rules
        /// checked it this slot (the recovery's gate, the slot's start for a damage multiplier, or one of its hits).</param>
        /// <param name="playerMeshChainPosition">Where the player's skill sits in its 맞물림 chain, from 1; 0 unmeshed.</param>
        public LegacySlotFacts(LegacyClashInput clash, int playerSkillId = 0, int enemySkillId = 0,
            int playerMaxHealth = 0, int enemyMaxHealth = 0, int playerHealthLost = 0, int enemyHealthLost = 0,
            int playerLargestHitTaken = 0, int enemyLargestHitTaken = 0, bool playerDefeated = false, bool enemyDefeated = false,
            bool playerConditionMatched = false, bool enemyConditionMatched = false,
            bool playerStateConditionHeld = false, bool enemyStateConditionHeld = false,
            int playerMeshChainPosition = 0, int enemyMeshChainPosition = 0)
        {
            if (playerMaxHealth < 0) throw new ArgumentOutOfRangeException(nameof(playerMaxHealth));
            if (enemyMaxHealth < 0) throw new ArgumentOutOfRangeException(nameof(enemyMaxHealth));
            if (playerHealthLost < 0) throw new ArgumentOutOfRangeException(nameof(playerHealthLost));
            if (enemyHealthLost < 0) throw new ArgumentOutOfRangeException(nameof(enemyHealthLost));
            if (playerLargestHitTaken < 0) throw new ArgumentOutOfRangeException(nameof(playerLargestHitTaken));
            if (enemyLargestHitTaken < 0) throw new ArgumentOutOfRangeException(nameof(enemyLargestHitTaken));
            if (playerMeshChainPosition < 0) throw new ArgumentOutOfRangeException(nameof(playerMeshChainPosition));
            if (enemyMeshChainPosition < 0) throw new ArgumentOutOfRangeException(nameof(enemyMeshChainPosition));
            Clash = clash;
            PlayerSkillId = playerSkillId;
            EnemySkillId = enemySkillId;
            PlayerMaxHealth = playerMaxHealth;
            EnemyMaxHealth = enemyMaxHealth;
            PlayerHealthLost = playerHealthLost;
            EnemyHealthLost = enemyHealthLost;
            PlayerLargestHitTaken = playerLargestHitTaken;
            EnemyLargestHitTaken = enemyLargestHitTaken;
            PlayerDefeated = playerDefeated;
            EnemyDefeated = enemyDefeated;
            PlayerConditionMatched = playerConditionMatched;
            EnemyConditionMatched = enemyConditionMatched;
            PlayerStateConditionHeld = playerStateConditionHeld;
            EnemyStateConditionHeld = enemyStateConditionHeld;
            PlayerMeshChainPosition = playerMeshChainPosition;
            EnemyMeshChainPosition = enemyMeshChainPosition;
        }

        public LegacyClashInput Clash { get; }
        public int SlotIndex => Clash.SlotIndex;
        public bool HasSlot => Clash.HasSlot;
        /// <summary>The player's skill's id in this slot, 0 for none (a breath keeps its own id).</summary>
        public int PlayerSkillId { get; }
        public int EnemySkillId { get; }
        public int PlayerMaxHealth { get; }
        public int EnemyMaxHealth { get; }
        /// <summary>The health the player has lost since the slot began (pressure, a broken target's doubling included).</summary>
        public int PlayerHealthLost { get; }
        public int EnemyHealthLost { get; }
        /// <summary>The most health the player lost to one hit of this slot (the decisive close-up judges hits alone).</summary>
        public int PlayerLargestHitTaken { get; }
        public int EnemyLargestHitTaken { get; }
        /// <summary>The player fell in this slot (the enemy's finishing blow).</summary>
        public bool PlayerDefeated { get; }
        public bool EnemyDefeated { get; }
        /// <summary>The player's skill's 상대 속성 조건 or 상대 종류 조건 is met by the enemy's skill in this slot.</summary>
        public bool PlayerConditionMatched { get; }
        public bool EnemyConditionMatched { get; }
        /// <summary>The player's skill's 상대 상태 조건 held against the enemy when the rules checked it this slot.</summary>
        public bool PlayerStateConditionHeld { get; }
        public bool EnemyStateConditionHeld { get; }
        /// <summary>Where the player's skill sits in its 맞물림 chain, counting from 1; 0 when it is not meshed.</summary>
        public int PlayerMeshChainPosition { get; }
        public int EnemyMeshChainPosition { get; }

        /// <summary>The same slot seen from the other side: every player value becomes the enemy's and back.</summary>
        public LegacySlotFacts Mirrored()
            => new LegacySlotFacts(Clash.Mirrored(), EnemySkillId, PlayerSkillId, EnemyMaxHealth, PlayerMaxHealth,
                EnemyHealthLost, PlayerHealthLost, EnemyLargestHitTaken, PlayerLargestHitTaken, EnemyDefeated, PlayerDefeated,
                EnemyConditionMatched, PlayerConditionMatched, EnemyStateConditionHeld, PlayerStateConditionHeld,
                EnemyMeshChainPosition, PlayerMeshChainPosition);

        public bool Equals(LegacySlotFacts other)
            => Clash == other.Clash && PlayerSkillId == other.PlayerSkillId && EnemySkillId == other.EnemySkillId &&
                PlayerMaxHealth == other.PlayerMaxHealth && EnemyMaxHealth == other.EnemyMaxHealth &&
                PlayerHealthLost == other.PlayerHealthLost && EnemyHealthLost == other.EnemyHealthLost &&
                PlayerLargestHitTaken == other.PlayerLargestHitTaken && EnemyLargestHitTaken == other.EnemyLargestHitTaken &&
                PlayerDefeated == other.PlayerDefeated && EnemyDefeated == other.EnemyDefeated &&
                PlayerConditionMatched == other.PlayerConditionMatched && EnemyConditionMatched == other.EnemyConditionMatched &&
                PlayerStateConditionHeld == other.PlayerStateConditionHeld &&
                EnemyStateConditionHeld == other.EnemyStateConditionHeld &&
                PlayerMeshChainPosition == other.PlayerMeshChainPosition && EnemyMeshChainPosition == other.EnemyMeshChainPosition;

        public override bool Equals(object obj) => obj is LegacySlotFacts other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Clash.GetHashCode();
                hash = hash * 31 + PlayerSkillId;
                hash = hash * 31 + EnemySkillId;
                hash = hash * 31 + PlayerMaxHealth;
                hash = hash * 31 + EnemyMaxHealth;
                hash = hash * 31 + PlayerHealthLost;
                hash = hash * 31 + EnemyHealthLost;
                hash = hash * 31 + PlayerLargestHitTaken;
                hash = hash * 31 + EnemyLargestHitTaken;
                hash = hash * 31 + PlayerMeshChainPosition;
                hash = hash * 31 + EnemyMeshChainPosition;
                hash = hash * 31 + (PlayerDefeated ? 1 : 0) + (EnemyDefeated ? 2 : 0) + (PlayerConditionMatched ? 4 : 0) +
                    (EnemyConditionMatched ? 8 : 0) + (PlayerStateConditionHeld ? 16 : 0) + (EnemyStateConditionHeld ? 32 : 0);
                return hash;
            }
        }

        public static bool operator ==(LegacySlotFacts left, LegacySlotFacts right) => left.Equals(right);
        public static bool operator !=(LegacySlotFacts left, LegacySlotFacts right) => !left.Equals(right);

        public override string ToString()
            => Clash + "; player #" + PlayerSkillId + Side(PlayerHealthLost, PlayerMaxHealth, PlayerLargestHitTaken,
                    PlayerDefeated, PlayerConditionMatched, PlayerStateConditionHeld, PlayerMeshChainPosition) +
                "; enemy #" + EnemySkillId + Side(EnemyHealthLost, EnemyMaxHealth, EnemyLargestHitTaken,
                    EnemyDefeated, EnemyConditionMatched, EnemyStateConditionHeld, EnemyMeshChainPosition);

        private static string Side(int lost, int max, int largest, bool defeated, bool matched, bool state, int mesh)
            => " lost " + lost + "/" + max + " (largest " + largest + ")" + (defeated ? " defeated" : string.Empty) +
                (matched ? " condition" : string.Empty) + (state ? " state" : string.Empty) +
                (mesh > 0 ? " mesh " + mesh : string.Empty);
    }

    /// <summary>The director's thresholds and switches (<see cref="LegacySlotImportance.Rate"/>). A threshold of zero or less
    /// switches its rule off, so <c>default</c> lifts only breaks, falls and the player's 간파 (맞물림 aside), binds a 합 only
    /// on an exact tie and has no signature moves; use <see cref="Default"/>. The presentation settings hold the numbers.</summary>
    public readonly struct LegacySlotImportanceTuning
    {
        public const float DefaultBladeBindGap = .15f, DefaultLargeClashGap = .6f, DefaultDeepPierce = .7f;
        public const int DefaultBigUnopposedPercent = 25, DefaultMeshChainMinimum = 2;

        /// <param name="bladeBindGap">A 합 whose relative power gap is at most this is a blade bind (0 only an exact tie,
        /// below 0 none).</param>
        /// <param name="largeClashGap">A 합 whose relative power gap reaches this is Major (0 or less: never).</param>
        /// <param name="deepPierce">An attack that pierces a guard by at least this share of itself is Major (0 or less:
        /// never).</param>
        /// <param name="bigUnopposedPercent">An unopposed attack whose whole power reaches this percent of the target's
        /// maximum health is Major (0 or less: never).</param>
        /// <param name="heavyHitPercent">One hit taking this percent of its target's maximum health is Major (0 or less:
        /// never); the decisive close-up's threshold (<see cref="LegacyDecisiveHit.DefaultHealthDamagePercent"/>).</param>
        /// <param name="meshChainMinimum">A 맞물림 chain counts as the player's 간파 from this skill of the chain on (1 every
        /// meshed skill, 2 from the second; 0 or less: never).</param>
        /// <param name="dodgeRaisesToMajor">A dodged attack is Major.</param>
        /// <param name="enemyReadsRaiseTier">The enemy's 간파 lifts the slot to Major as the player's does (otherwise it only
        /// adds to the score).</param>
        /// <param name="signatureSkillIds">The signature moves' skill ids (Major for either side's use); null for none.</param>
        public LegacySlotImportanceTuning(float bladeBindGap, float largeClashGap, float deepPierce, int bigUnopposedPercent,
            int heavyHitPercent, int meshChainMinimum, bool dodgeRaisesToMajor, bool enemyReadsRaiseTier,
            IReadOnlyList<int> signatureSkillIds)
        {
            BladeBindGap = float.IsNaN(bladeBindGap) ? -1f : Math.Min(1f, bladeBindGap);
            LargeClashGap = Share(largeClashGap);
            DeepPierce = Share(deepPierce);
            BigUnopposedPercent = bigUnopposedPercent;
            HeavyHitPercent = heavyHitPercent;
            MeshChainMinimum = meshChainMinimum;
            DodgeRaisesToMajor = dodgeRaisesToMajor;
            EnemyReadsRaiseTier = enemyReadsRaiseTier;
            SignatureSkillIds = signatureSkillIds;
        }

        /// <summary>The defaults: blade bind up to a gap of 0.15, a 합 gap of 0.6 or a pierce of 0.7 is Major, an unopposed
        /// attack of 25% of the target's maximum health or one hit taking 25% is Major, a 맞물림 chain reads from its second
        /// skill, a dodge is Major, the enemy's 간파 only scores, and the capstones 에스토카다, 카덴스 and 두블레 are the
        /// signature moves (<see cref="LegacySlotImportance.DefaultSignatureSkillIds"/>).</summary>
        public static LegacySlotImportanceTuning Default => new LegacySlotImportanceTuning(DefaultBladeBindGap,
            DefaultLargeClashGap, DefaultDeepPierce, DefaultBigUnopposedPercent, LegacyDecisiveHit.DefaultHealthDamagePercent,
            DefaultMeshChainMinimum, true, false, LegacySlotImportance.DefaultSignatureSkillIds);

        public float BladeBindGap { get; }
        public float LargeClashGap { get; }
        public float DeepPierce { get; }
        public int BigUnopposedPercent { get; }
        public int HeavyHitPercent { get; }
        public int MeshChainMinimum { get; }
        public bool DodgeRaisesToMajor { get; }
        public bool EnemyReadsRaiseTier { get; }
        /// <summary>The signature moves' skill ids, or null for none.</summary>
        public IReadOnlyList<int> SignatureSkillIds { get; }

        /// <summary>The same tuning with another signature list (null for none).</summary>
        public LegacySlotImportanceTuning WithSignatures(IReadOnlyList<int> signatureSkillIds)
            => new LegacySlotImportanceTuning(BladeBindGap, LargeClashGap, DeepPierce, BigUnopposedPercent, HeavyHitPercent,
                MeshChainMinimum, DodgeRaisesToMajor, EnemyReadsRaiseTier, signatureSkillIds);

        // A share above 1 can never be reached; NaN and anything not above 0 switch the rule off.
        private static float Share(float value) => float.IsNaN(value) || value <= 0f ? 0f : value;
    }

    /// <summary>A resolving slot's rating (<see cref="LegacySlotImportance.Rate"/>): its tier, a score that orders slots of
    /// one tier, what it saw, each side's 간파 and signature move, and whether it is a blade bind. There is no winner in
    /// it: both sides of a 합 deal damage, so nothing here says who won or lost a slot.</summary>
    public readonly struct LegacySlotRank : IEquatable<LegacySlotRank>
    {
        public LegacySlotRank(int slotIndex, LegacySlotTier tier, int score, LegacySlotHighlight highlights,
            LegacyReadEvent playerReads, LegacyReadEvent enemyReads, bool playerSignature, bool enemySignature,
            LegacyClashKind kind)
        {
            SlotIndex = slotIndex;
            Tier = tier;
            Score = Math.Max(0, score);
            Highlights = highlights;
            PlayerReads = playerReads;
            EnemyReads = enemyReads;
            PlayerSignature = playerSignature;
            EnemySignature = enemySignature;
            Kind = kind;
        }

        /// <summary>No slot: Minor, score 0, slot -1. Every real slot of a higher tier or a positive score outranks it.</summary>
        public static LegacySlotRank None => new LegacySlotRank(-1, LegacySlotTier.Minor, 0, LegacySlotHighlight.None,
            LegacyReadEvent.None, LegacyReadEvent.None, false, false, LegacyClashKind.None);

        public int SlotIndex { get; }
        public LegacySlotTier Tier { get; }
        /// <summary>Whole points (<see cref="LegacySlotImportance"/>'s constants) that order slots of one tier: the bigger
        /// the gap, the pierce, the health lost, the more reads, the higher. Never negative.</summary>
        public int Score { get; }
        public LegacySlotHighlight Highlights { get; }
        /// <summary>The player's 간파 in this slot: the screen's 간파 cue shows these, and only these.</summary>
        public LegacyReadEvent PlayerReads { get; }
        public LegacyReadEvent EnemyReads { get; }
        /// <summary>The player's skill is a signature move: its windup, strike and impact get the signature treatment.</summary>
        public bool PlayerSignature { get; }
        public bool EnemySignature { get; }
        /// <summary>How the queue cards meet (<see cref="LegacyClashMotion.Classify"/>).</summary>
        public LegacyClashKind Kind { get; }
        public bool HasPlayerRead => PlayerReads != LegacyReadEvent.None;
        /// <summary>An even 합 held as a mutual struggle (<see cref="LegacySlotHighlight.BladeBind"/>).</summary>
        public bool IsBladeBind => Has(LegacySlotHighlight.BladeBind);

        public bool Has(LegacySlotHighlight highlight) => highlight != LegacySlotHighlight.None && (Highlights & highlight) == highlight;

        /// <summary>Whether this slot is more important than <paramref name="other"/>: a higher tier, or the same tier and a
        /// higher score. An exact tie is not, so whoever keeps the most important slot so far keeps the earlier one.</summary>
        public bool Outranks(LegacySlotRank other) => Tier > other.Tier || Tier == other.Tier && Score > other.Score;

        public bool Equals(LegacySlotRank other)
            => SlotIndex == other.SlotIndex && Tier == other.Tier && Score == other.Score && Highlights == other.Highlights &&
                PlayerReads == other.PlayerReads && EnemyReads == other.EnemyReads &&
                PlayerSignature == other.PlayerSignature && EnemySignature == other.EnemySignature && Kind == other.Kind;

        public override bool Equals(object obj) => obj is LegacySlotRank other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SlotIndex;
                hash = hash * 31 + (int)Tier;
                hash = hash * 31 + Score;
                hash = hash * 31 + (int)Highlights;
                hash = hash * 31 + (int)PlayerReads;
                hash = hash * 31 + (int)EnemyReads;
                hash = hash * 31 + (PlayerSignature ? 1 : 0) + (EnemySignature ? 2 : 0);
                return hash * 31 + (int)Kind;
            }
        }

        public static bool operator ==(LegacySlotRank left, LegacySlotRank right) => left.Equals(right);
        public static bool operator !=(LegacySlotRank left, LegacySlotRank right) => !left.Equals(right);

        public override string ToString()
            => "slot " + SlotIndex + ": " + Tier + " " + Score + " (" + Kind + "; " + Highlights + ")" +
                (PlayerReads != LegacyReadEvent.None ? " player reads " + PlayerReads : string.Empty) +
                (EnemyReads != LegacyReadEvent.None ? " enemy reads " + EnemyReads : string.Empty) +
                (PlayerSignature ? " player signature" : string.Empty) + (EnemySignature ? " enemy signature" : string.Empty);
    }

    /// <summary>The presentation director's scorer and the 간파 detection (presentation only; the rules never read either).
    /// <see cref="Rate(LegacySlotFacts, LegacySlotImportanceTuning)"/> is pure: the same facts always rate the same, and it
    /// treats both sides alike except that only the player's 간파 lifts the tier by default.
    /// <list type="bullet">
    /// <item><see cref="LegacySlotTier.Climax"/>: a fighter fell in the slot.</item>
    /// <item><see cref="LegacySlotTier.Major"/>: a break; one hit taking <see cref="LegacySlotImportanceTuning.HeavyHitPercent"/>
    /// of its target's maximum health; a 합 gap of <see cref="LegacySlotImportanceTuning.LargeClashGap"/>; a pierce of
    /// <see cref="LegacySlotImportanceTuning.DeepPierce"/>; an unopposed attack of
    /// <see cref="LegacySlotImportanceTuning.BigUnopposedPercent"/> of the target's maximum health; a dodge; the player's 간파
    /// (<see cref="PlayerReads"/>); a signature move on either side.</item>
    /// <item><see cref="LegacySlotTier.Normal"/>: any other slot with an attack.</item>
    /// <item><see cref="LegacySlotTier.Minor"/>: no attack on either side.</item>
    /// </list>
    /// The score adds whole points for each thing the slot saw (the constants below), so slots of one tier compare
    /// (<see cref="LegacySlotRank.Outranks"/>): the decisive-moment replay keeps the highest.</summary>
    public static class LegacySlotImportance
    {
        /// <summary>Points for any attack.</summary>
        public const int AttackPoints = 100;
        /// <summary>Points for a 합's whole relative gap (scaled by the gap), and for a blade bind on top.</summary>
        public const int ClashGapPoints = 200, BladeBindPoints = 50;
        /// <summary>Points for a whole pierce (scaled by its share), and for a whole block's margin.</summary>
        public const int PiercePoints = 200, HeldPoints = 100;
        /// <summary>Points for an unopposed attack whose whole power matches the target's maximum health (scaled, capped).</summary>
        public const int UnopposedPoints = 200;
        public const int DodgePoints = 200;
        /// <summary>Points per broken side.</summary>
        public const int BreakPoints = 300;
        /// <summary>Points per side for losing its whole maximum health in the slot (scaled, capped), and per heavy hit taken.</summary>
        public const int HealthPoints = 400, HeavyHitPoints = 100;
        /// <summary>Points per 간파 event: the player's count double the enemy's.</summary>
        public const int PlayerReadPoints = 200, EnemyReadPoints = 100;
        /// <summary>Points per side using a signature move.</summary>
        public const int SignaturePoints = 200;
        /// <summary>Points per fallen side.</summary>
        public const int FinishingBlowPoints = 1000;

        /// <summary>The capstones 에스토카다 (51), 카덴스 (53) and 두블레 (56): the default signature moves.</summary>
        public static IReadOnlyList<int> DefaultSignatureSkillIds { get; } = Array.AsReadOnly(new[] { 51, 53, 56 });

        /// <summary>Rates the slot with <see cref="LegacySlotImportanceTuning.Default"/>.</summary>
        public static LegacySlotRank Rate(LegacySlotFacts facts) => Rate(facts, LegacySlotImportanceTuning.Default);

        /// <summary>Rates the slot as it stands (see the class summary). Before the first hit it reads what is known at the
        /// slot's start (the kinds, the rolled powers, the signature moves, the met conditions); read it again after each hit
        /// for the breaks, the health lost and a fall. No slot rates as <see cref="LegacySlotRank.None"/>.</summary>
        public static LegacySlotRank Rate(LegacySlotFacts facts, LegacySlotImportanceTuning tuning)
        {
            if (!facts.HasSlot) return LegacySlotRank.None;
            LegacyClashInput input = facts.Clash;
            LegacyClash clash = LegacyClashMotion.Classify(input);
            // Both cards of every kind carry the same graded strength (a 합's gap, a pierce's share, a block's margin), so
            // reading the larger keeps the rating blind to which side it favours.
            float strength = Math.Max(clash.PlayerStrength, clash.EnemyStrength);
            var highlights = LegacySlotHighlight.None;
            int score = 0;
            bool attack = input.PlayerAction == LegacyClashAction.Attack || input.EnemyAction == LegacyClashAction.Attack;
            if (attack)
            {
                highlights |= LegacySlotHighlight.Attack;
                score += AttackPoints;
            }
            switch (clash.Kind)
            {
                case LegacyClashKind.Clash:
                    score += Points(ClashGapPoints, strength);
                    if (tuning.LargeClashGap > 0f && strength >= tuning.LargeClashGap) highlights |= LegacySlotHighlight.LargeClashGap;
                    if (tuning.BladeBindGap >= 0f && strength <= tuning.BladeBindGap)
                    {
                        highlights |= LegacySlotHighlight.BladeBind;
                        score += BladeBindPoints;
                    }
                    break;
                case LegacyClashKind.GuardPierced:
                    score += Points(PiercePoints, strength);
                    if (tuning.DeepPierce > 0f && strength >= tuning.DeepPierce) highlights |= LegacySlotHighlight.DeepPierce;
                    break;
                case LegacyClashKind.GuardHeld:
                    score += Points(HeldPoints, strength);
                    break;
                case LegacyClashKind.Unopposed:
                {
                    bool playerAttacks = input.PlayerAction == LegacyClashAction.Attack;
                    int power = playerAttacks ? input.PlayerSkillPower : input.EnemySkillPower;
                    int targetMaxHealth = playerAttacks ? facts.EnemyMaxHealth : facts.PlayerMaxHealth;
                    if (targetMaxHealth > 0) score += Points(UnopposedPoints, Share(power, targetMaxHealth));
                    if (Reaches(power, targetMaxHealth, tuning.BigUnopposedPercent)) highlights |= LegacySlotHighlight.BigUnopposed;
                    break;
                }
                case LegacyClashKind.Dodged:
                    highlights |= LegacySlotHighlight.Dodge;
                    score += DodgePoints;
                    break;
            }
            if (input.PlayerBroken || input.EnemyBroken) highlights |= LegacySlotHighlight.Break;
            score += (input.PlayerBroken ? BreakPoints : 0) + (input.EnemyBroken ? BreakPoints : 0);
            score += HealthScore(facts.PlayerHealthLost, facts.PlayerMaxHealth) + HealthScore(facts.EnemyHealthLost, facts.EnemyMaxHealth);
            bool playerHeavy = Heavy(facts.PlayerLargestHitTaken, facts.PlayerMaxHealth, tuning.HeavyHitPercent);
            bool enemyHeavy = Heavy(facts.EnemyLargestHitTaken, facts.EnemyMaxHealth, tuning.HeavyHitPercent);
            if (playerHeavy || enemyHeavy) highlights |= LegacySlotHighlight.HeavyHit;
            score += (playerHeavy ? HeavyHitPoints : 0) + (enemyHeavy ? HeavyHitPoints : 0);
            LegacyReadEvent playerReads = PlayerReads(facts, tuning.MeshChainMinimum);
            LegacyReadEvent enemyReads = EnemyReads(facts, tuning.MeshChainMinimum);
            if (playerReads != LegacyReadEvent.None) highlights |= LegacySlotHighlight.PlayerRead;
            if (enemyReads != LegacyReadEvent.None) highlights |= LegacySlotHighlight.EnemyRead;
            score += Count(playerReads) * PlayerReadPoints + Count(enemyReads) * EnemyReadPoints;
            bool playerSignature = IsSignature(input.PlayerAction, facts.PlayerSkillId, tuning.SignatureSkillIds);
            bool enemySignature = IsSignature(input.EnemyAction, facts.EnemySkillId, tuning.SignatureSkillIds);
            if (playerSignature || enemySignature) highlights |= LegacySlotHighlight.Signature;
            score += (playerSignature ? SignaturePoints : 0) + (enemySignature ? SignaturePoints : 0);
            if (facts.PlayerDefeated || facts.EnemyDefeated) highlights |= LegacySlotHighlight.FinishingBlow;
            score += (facts.PlayerDefeated ? FinishingBlowPoints : 0) + (facts.EnemyDefeated ? FinishingBlowPoints : 0);

            return new LegacySlotRank(facts.SlotIndex, TierOf(highlights, tuning), score, highlights, playerReads, enemyReads,
                playerSignature, enemySignature, clash.Kind);
        }

        /// <summary>The tier a slot that saw <paramref name="highlights"/> has (the rule of
        /// <see cref="Rate(LegacySlotFacts, LegacySlotImportanceTuning)"/>): Climax with a fall; Major with any highlight that
        /// lifts (a dodge and the enemy's 간파 only while the tuning says so); Normal with an attack; Minor otherwise.</summary>
        public static LegacySlotTier TierOf(LegacySlotHighlight highlights, LegacySlotImportanceTuning tuning)
        {
            LegacySlotHighlight lifting = LegacySlotHighlight.LargeClashGap | LegacySlotHighlight.DeepPierce |
                LegacySlotHighlight.BigUnopposed | LegacySlotHighlight.Break | LegacySlotHighlight.HeavyHit |
                LegacySlotHighlight.PlayerRead | LegacySlotHighlight.Signature |
                (tuning.DodgeRaisesToMajor ? LegacySlotHighlight.Dodge : LegacySlotHighlight.None) |
                (tuning.EnemyReadsRaiseTier ? LegacySlotHighlight.EnemyRead : LegacySlotHighlight.None);
            return (highlights & LegacySlotHighlight.FinishingBlow) != 0 ? LegacySlotTier.Climax
                : (highlights & lifting) != 0 ? LegacySlotTier.Major
                : (highlights & LegacySlotHighlight.Attack) != 0 ? LegacySlotTier.Normal : LegacySlotTier.Minor;
        }

        /// <summary>The player's 간파 in the slot (<see cref="LegacyReadEvent"/>): its skill's 상대 속성/종류 조건 met, its
        /// 상대 상태 조건 held, a break of the enemy, its skill <paramref name="meshChainMinimum"/> or more deep in a 맞물림
        /// chain. A condition counts only for a skill that is there (an attack or a guard). Nothing for no slot.</summary>
        public static LegacyReadEvent PlayerReads(LegacySlotFacts facts,
            int meshChainMinimum = LegacySlotImportanceTuning.DefaultMeshChainMinimum)
        {
            if (!facts.HasSlot) return LegacyReadEvent.None;
            LegacyClashAction action = facts.Clash.PlayerAction;
            bool skill = action == LegacyClashAction.Attack || action == LegacyClashAction.Guard;
            var reads = LegacyReadEvent.None;
            if (skill && facts.PlayerConditionMatched) reads |= LegacyReadEvent.OpponentCondition;
            if (skill && facts.PlayerStateConditionHeld) reads |= LegacyReadEvent.OpponentState;
            if (facts.Clash.EnemyBroken) reads |= LegacyReadEvent.Break;
            if (meshChainMinimum > 0 && facts.PlayerMeshChainPosition >= meshChainMinimum) reads |= LegacyReadEvent.MeshChain;
            return reads;
        }

        /// <summary>The enemy's 간파 in the slot: <see cref="PlayerReads"/> of the slot seen from the enemy's side.</summary>
        public static LegacyReadEvent EnemyReads(LegacySlotFacts facts,
            int meshChainMinimum = LegacySlotImportanceTuning.DefaultMeshChainMinimum)
            => PlayerReads(facts.Mirrored(), meshChainMinimum);

        /// <summary>Whether a side's skill is a signature move: there is a skill (an attack or a guard) and its id is listed.</summary>
        public static bool IsSignature(LegacyClashAction action, int skillId, IReadOnlyList<int> signatureSkillIds)
        {
            if (signatureSkillIds == null || skillId == 0 ||
                (action != LegacyClashAction.Attack && action != LegacyClashAction.Guard)) return false;
            for (int i = 0; i < signatureSkillIds.Count; i++)
                if (signatureSkillIds[i] == skillId) return true;
            return false;
        }

        /// <summary>Whether a skill is one of <paramref name="signatureSkillIds"/> (null skill or list: no).</summary>
        public static bool IsSignature(LegacySkill skill, IReadOnlyList<int> signatureSkillIds)
            => skill != null && IsSignature(LegacyClashMotion.ActionOf(skill), skill.Id, signatureSkillIds);

        private static int HealthScore(int lost, int maxHealth) => maxHealth > 0 ? Points(HealthPoints, Share(lost, maxHealth)) : 0;

        private static bool Heavy(int largestHit, int maxHealth, int percent) => LegacyDecisiveHit.IsHeavy(largestHit, maxHealth, percent);

        // Exact, in whole numbers: value × 100 ≥ percent × whole.
        private static bool Reaches(int value, int whole, int percent)
            => percent > 0 && value > 0 && whole > 0 && (long)value * 100 >= (long)percent * whole;

        private static double Share(int part, int whole) => whole > 0 ? Math.Min(1d, Math.Max(0d, (double)part / whole)) : 0d;

        private static int Points(int points, double share)
            => (int)Math.Round(points * (double.IsNaN(share) ? 0d : Math.Min(1d, Math.Max(0d, share))), MidpointRounding.AwayFromZero);

        /// <summary>How many 간파 events <paramref name="reads"/> holds (each scores on its own).</summary>
        internal static int Count(LegacyReadEvent reads)
        {
            int count = 0;
            for (int bits = (int)reads; bits != 0; bits &= bits - 1) count++;
            return count;
        }
    }
}
