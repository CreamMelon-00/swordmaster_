using System;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>What one side brings to a slot as the clash motion reads it (<see cref="LegacyClashMotion"/>): nothing (an
    /// empty slot, or a 반격 still pending), a 숨고르기 (any wait), a guard or an attack.</summary>
    public enum LegacyClashAction { Empty, Breath, Guard, Attack }

    /// <summary>A side of the clash, seen from the player's side (<see cref="ILegacyDuelView"/>).</summary>
    public enum LegacyClashSide { None, Player, Enemy }

    /// <summary>How a resolving slot's two queue cards meet (<see cref="LegacyClashMotion.Classify"/>).</summary>
    public enum LegacyClashKind
    {
        /// <summary>Neither side has anything in the slot.</summary>
        None,
        /// <summary>No attack: guard against guard, a guard or a breath against nothing, a breath against a breath. At most a
        /// tiny settle.</summary>
        Settle,
        /// <summary>합: attack against attack. Both cards meet; the stronger skill pushes, the weaker is knocked back, an even
        /// one bounces both.</summary>
        Clash,
        /// <summary>An attack against a guard that lets damage through (or backed by a successful 압박, whose bonus passes any
        /// guard): the attacker thrusts in, the guard is knocked back.</summary>
        GuardPierced,
        /// <summary>An attack the guard fully blocks (no 압박 behind it): the attacker thrusts in and rebounds, the guard braces.</summary>
        GuardHeld,
        /// <summary>An attack against nothing: an empty slot or a breath. The attacker lunges unopposed.</summary>
        Unopposed,
        /// <summary>An attack the other side dodged (a timed dodge or 자동 회피): the dodger side-steps, the attacker whiffs past.
        /// A dodger's own attack still lands, but the dodge is what the cards show.</summary>
        Dodged
    }

    /// <summary>What one queue card does in a clash (<see cref="LegacyClash.PlayerMove"/>). Directions are the card's
    /// own: forward is toward the other side's card.</summary>
    public enum LegacyClashMove
    {
        /// <summary>Stays where it is (an empty side has no card to move).</summary>
        Rest,
        /// <summary>A tiny settle in place (<see cref="LegacyClashKind.Settle"/>).</summary>
        Settle,
        /// <summary>Wins a 합: meets the other card and drives forward.</summary>
        Push,
        /// <summary>Knocked back: loses a 합, or a guard the attack pierced.</summary>
        Recoil,
        /// <summary>An even 합: meets the other card and bounces back as far as it does.</summary>
        Bounce,
        /// <summary>An attack thrusting through a guard it pierced.</summary>
        Thrust,
        /// <summary>An attack thrusting into a guard that held, then rebounding.</summary>
        Rebound,
        /// <summary>A guard holding against an attack it fully blocked.</summary>
        Brace,
        /// <summary>An attack lunging at nothing.</summary>
        Lunge,
        /// <summary>An attack lunging past a dodging card.</summary>
        Whiff,
        /// <summary>A card side-stepping (a vertical offset) out of the attack's way.</summary>
        SideStep
    }

    /// <summary>A resolving slot as the clash motion reads it, from the player's side: what each side brings, the power its
    /// skill rolled (per hit, as the rules use it), its hits, and what has happened in the slot so far (a dodge, a break).
    /// Read it from <see cref="ILegacyDuelView.CurrentClash"/>, which mirrors a versus guest's view; reading never changes the
    /// duel.</summary>
    public readonly struct LegacyClashInput : IEquatable<LegacyClashInput>
    {
        /// <summary>No slot resolving: both sides empty, slot index -1.</summary>
        public static LegacyClashInput None => new LegacyClashInput(-1, LegacyClashAction.Empty, 0, 1, LegacyClashAction.Empty, 0, 1);

        private readonly int playerHits, enemyHits;

        /// <param name="slotIndex">The slot resolving, or -1 for none.</param>
        /// <param name="playerPower">An attack's power per hit (the rules' rolled power, buffs and multipliers in, divided by
        /// its hits), or a guard's guard value; ignored for an empty side or a breath.</param>
        /// <param name="playerHits">An attack's hit count; ignored (1) otherwise.</param>
        /// <param name="playerDodged">The player avoids the enemy's attack this slot (a successful dodge, 자동 회피). It counts
        /// only against an attack.</param>
        /// <param name="playerBroken">The player's resistance broke during this slot (it was whole as the slot started).</param>
        /// <param name="playerCountered">The player's skill is its 반격 filling its empty slot, so the player's queue has no
        /// card at this slot.</param>
        /// <param name="decided">The slot's first hit has resolved: no dodge or pending 반격 can change it any more (a later
        /// hit may still break someone).</param>
        /// <param name="playerPressured">The player's attack is backed by a successful 압박: each hit also deals its share of
        /// the whole rolled power to health, past any guard. Kept only for an attack (the campaign's player; versus has none).</param>
        public LegacyClashInput(int slotIndex, LegacyClashAction playerAction, int playerPower, int playerHits,
            LegacyClashAction enemyAction, int enemyPower, int enemyHits,
            bool playerDodged = false, bool enemyDodged = false, bool playerBroken = false, bool enemyBroken = false,
            bool playerCountered = false, bool enemyCountered = false, bool decided = false,
            bool playerPressured = false, bool enemyPressured = false)
        {
            if (slotIndex < -1) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (playerPower < 0) throw new ArgumentOutOfRangeException(nameof(playerPower));
            if (enemyPower < 0) throw new ArgumentOutOfRangeException(nameof(enemyPower));
            if (playerHits < 1) throw new ArgumentOutOfRangeException(nameof(playerHits));
            if (enemyHits < 1) throw new ArgumentOutOfRangeException(nameof(enemyHits));
            SlotIndex = slotIndex;
            PlayerAction = playerAction;
            EnemyAction = enemyAction;
            PlayerPower = HasPower(playerAction) ? playerPower : 0;
            EnemyPower = HasPower(enemyAction) ? enemyPower : 0;
            this.playerHits = playerAction == LegacyClashAction.Attack ? playerHits : 1;
            this.enemyHits = enemyAction == LegacyClashAction.Attack ? enemyHits : 1;
            PlayerDodged = playerDodged;
            EnemyDodged = enemyDodged;
            PlayerBroken = playerBroken;
            EnemyBroken = enemyBroken;
            PlayerCountered = playerCountered;
            EnemyCountered = enemyCountered;
            Decided = decided;
            PlayerPressured = playerPressured && playerAction == LegacyClashAction.Attack;
            EnemyPressured = enemyPressured && enemyAction == LegacyClashAction.Attack;
        }

        /// <summary>The same from the skills themselves: each side's action (<see cref="LegacyClashMotion.ActionOf"/>) and an
        /// attack's hit count come from its skill; a null skill is an empty side.</summary>
        public static LegacyClashInput FromSkills(int slotIndex, LegacySkill playerSkill, int playerPower,
            LegacySkill enemySkill, int enemyPower,
            bool playerDodged = false, bool enemyDodged = false, bool playerBroken = false, bool enemyBroken = false,
            bool playerCountered = false, bool enemyCountered = false, bool decided = false,
            bool playerPressured = false, bool enemyPressured = false)
            => new LegacyClashInput(slotIndex,
                LegacyClashMotion.ActionOf(playerSkill), Math.Max(0, playerPower), Math.Max(1, playerSkill?.AttackCount ?? 1),
                LegacyClashMotion.ActionOf(enemySkill), Math.Max(0, enemyPower), Math.Max(1, enemySkill?.AttackCount ?? 1),
                playerDodged, enemyDodged, playerBroken, enemyBroken, playerCountered, enemyCountered, decided,
                playerPressured, enemyPressured);

        /// <summary>The slot resolving, or -1 for none.</summary>
        public int SlotIndex { get; }
        public bool HasSlot => SlotIndex >= 0;
        public LegacyClashAction PlayerAction { get; }
        public LegacyClashAction EnemyAction { get; }
        /// <summary>An attack's power per hit or a guard's guard value; 0 for an empty side or a breath.</summary>
        public int PlayerPower { get; }
        public int EnemyPower { get; }
        /// <summary>An attack's hit count; 1 for anything else.</summary>
        public int PlayerHits => Math.Max(1, playerHits);
        public int EnemyHits => Math.Max(1, enemyHits);
        /// <summary>The power the whole skill carries into the clash: an attack's power per hit times its hits, a guard's
        /// guard value, 0 otherwise. Two attacks compare these (one skill is one coin).</summary>
        public int PlayerSkillPower => PlayerAction == LegacyClashAction.Attack ? PlayerPower * PlayerHits : PlayerPower;
        public int EnemySkillPower => EnemyAction == LegacyClashAction.Attack ? EnemyPower * EnemyHits : EnemyPower;
        public bool PlayerDodged { get; }
        public bool EnemyDodged { get; }
        public bool PlayerBroken { get; }
        public bool EnemyBroken { get; }
        public bool PlayerCountered { get; }
        public bool EnemyCountered { get; }
        public bool Decided { get; }
        /// <summary>That side's attack is backed by a successful 압박 (its whole rolled power again, to health past any guard):
        /// damage gets through a guard whatever its value. Only the campaign's player presses; false for anything but an
        /// attack.</summary>
        public bool PlayerPressured { get; }
        public bool EnemyPressured { get; }

        /// <summary>The same slot seen from the other side: every player value becomes the enemy's and back.</summary>
        public LegacyClashInput Mirrored()
            => new LegacyClashInput(SlotIndex, EnemyAction, EnemyPower, EnemyHits, PlayerAction, PlayerPower, PlayerHits,
                EnemyDodged, PlayerDodged, EnemyBroken, PlayerBroken, EnemyCountered, PlayerCountered, Decided,
                EnemyPressured, PlayerPressured);

        private static bool HasPower(LegacyClashAction action)
            => action == LegacyClashAction.Attack || action == LegacyClashAction.Guard;

        public bool Equals(LegacyClashInput other)
            => SlotIndex == other.SlotIndex && PlayerAction == other.PlayerAction && EnemyAction == other.EnemyAction &&
                PlayerPower == other.PlayerPower && EnemyPower == other.EnemyPower &&
                PlayerHits == other.PlayerHits && EnemyHits == other.EnemyHits &&
                PlayerDodged == other.PlayerDodged && EnemyDodged == other.EnemyDodged &&
                PlayerBroken == other.PlayerBroken && EnemyBroken == other.EnemyBroken &&
                PlayerCountered == other.PlayerCountered && EnemyCountered == other.EnemyCountered && Decided == other.Decided &&
                PlayerPressured == other.PlayerPressured && EnemyPressured == other.EnemyPressured;

        public override bool Equals(object obj) => obj is LegacyClashInput other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SlotIndex;
                hash = hash * 31 + (int)PlayerAction;
                hash = hash * 31 + (int)EnemyAction;
                hash = hash * 31 + PlayerPower;
                hash = hash * 31 + EnemyPower;
                hash = hash * 31 + PlayerHits;
                hash = hash * 31 + EnemyHits;
                hash = hash * 31 + (PlayerDodged ? 1 : 0) + (EnemyDodged ? 2 : 0) + (PlayerBroken ? 4 : 0) +
                    (EnemyBroken ? 8 : 0) + (PlayerCountered ? 16 : 0) + (EnemyCountered ? 32 : 0) + (Decided ? 64 : 0) +
                    (PlayerPressured ? 128 : 0) + (EnemyPressured ? 256 : 0);
                return hash;
            }
        }

        public static bool operator ==(LegacyClashInput left, LegacyClashInput right) => left.Equals(right);
        public static bool operator !=(LegacyClashInput left, LegacyClashInput right) => !left.Equals(right);

        public override string ToString()
            => "slot " + SlotIndex + ": " + Side(PlayerAction, PlayerPower, PlayerHits, PlayerDodged, PlayerBroken, PlayerPressured) +
                " vs " + Side(EnemyAction, EnemyPower, EnemyHits, EnemyDodged, EnemyBroken, EnemyPressured) +
                (Decided ? " (decided)" : string.Empty);

        private static string Side(LegacyClashAction action, int power, int hits, bool dodged, bool broken, bool pressured)
            => action + (action == LegacyClashAction.Attack ? " " + power + "x" + hits : action == LegacyClashAction.Guard ? " " + power
                : string.Empty) + (pressured ? " pressed" : string.Empty) + (dodged ? " dodged" : string.Empty) +
                (broken ? " broken" : string.Empty);
    }

    /// <summary>How a resolving slot's two queue cards move (<see cref="LegacyClashMotion.Classify"/>), from the player's
    /// side: the kind of meeting, each card's move and its strength, who came out ahead, and who broke.</summary>
    public readonly struct LegacyClash : IEquatable<LegacyClash>
    {
        public LegacyClash(LegacyClashKind kind, LegacyClashSide winner, float playerAdvantage,
            LegacyClashMove playerMove, float playerStrength, LegacyClashMove enemyMove, float enemyStrength,
            bool playerBroken, bool enemyBroken)
        {
            Kind = kind;
            Winner = winner;
            PlayerAdvantage = Clamp(playerAdvantage, -1f, 1f);
            PlayerMove = playerMove;
            EnemyMove = enemyMove;
            PlayerStrength = Clamp(playerStrength, 0f, 1f);
            EnemyStrength = Clamp(enemyStrength, 0f, 1f);
            PlayerBroken = playerBroken;
            EnemyBroken = enemyBroken;
        }

        public LegacyClashKind Kind { get; }
        /// <summary>The side the slot favours: a 합's stronger skill, a guard's piercer, a guard that held, an unopposed
        /// attacker, a dodger. None for an even 합, a settle or nothing.</summary>
        public LegacyClashSide Winner { get; }
        /// <summary>How far the slot favours the player, from -1 (wholly the enemy's) to 1 (wholly the player's): the 합's
        /// relative power gap, a pierce's share or a block's margin, signed toward <see cref="Winner"/>; ±1 for an unopposed
        /// attack or a dodge; 0 for an even 합, a settle or nothing. A guard holding exactly (margin 0) wins at 0.</summary>
        public float PlayerAdvantage { get; }
        public LegacyClashMove PlayerMove { get; }
        public LegacyClashMove EnemyMove { get; }
        /// <summary>How hard the player's card makes its move, from 0 to 1: the 합's relative power gap
        /// (|P1 − P2| / max(P1, P2)) for <see cref="LegacyClashMove.Push"/> and <see cref="LegacyClashMove.Recoil"/>, the
        /// share of the attack the guard let through (a pressed attack's 압박 bonus counted in, so at least half) for
        /// <see cref="LegacyClashMove.Thrust"/> and a pierced guard's
        /// <see cref="LegacyClashMove.Recoil"/>, the guard's margin over the attack for <see cref="LegacyClashMove.Rebound"/>
        /// and <see cref="LegacyClashMove.Brace"/>; 1 for <see cref="LegacyClashMove.Lunge"/>,
        /// <see cref="LegacyClashMove.Whiff"/> and <see cref="LegacyClashMove.SideStep"/>; 0 for
        /// <see cref="LegacyClashMove.Bounce"/>, <see cref="LegacyClashMove.Settle"/> and <see cref="LegacyClashMove.Rest"/>.
        /// The screen adds its own base distance to every move, so a strength of 0 still moves.</summary>
        public float PlayerStrength { get; }
        public float EnemyStrength { get; }
        /// <summary>The player's resistance broke during this slot: its card gets an extra shake.</summary>
        public bool PlayerBroken { get; }
        public bool EnemyBroken { get; }
        /// <summary>The two cards touch (a 합 or an attack meeting a guard): a spark at the contact point.</summary>
        public bool Contact => Kind == LegacyClashKind.Clash || Kind == LegacyClashKind.GuardPierced || Kind == LegacyClashKind.GuardHeld;

        /// <summary>The same clash seen from the other side.</summary>
        public LegacyClash Mirrored()
            => new LegacyClash(Kind, Winner == LegacyClashSide.Player ? LegacyClashSide.Enemy
                    : Winner == LegacyClashSide.Enemy ? LegacyClashSide.Player : LegacyClashSide.None,
                -PlayerAdvantage, EnemyMove, EnemyStrength, PlayerMove, PlayerStrength, EnemyBroken, PlayerBroken);

        public bool Equals(LegacyClash other)
            => Kind == other.Kind && Winner == other.Winner && PlayerAdvantage.Equals(other.PlayerAdvantage) &&
                PlayerMove == other.PlayerMove && EnemyMove == other.EnemyMove &&
                PlayerStrength.Equals(other.PlayerStrength) && EnemyStrength.Equals(other.EnemyStrength) &&
                PlayerBroken == other.PlayerBroken && EnemyBroken == other.EnemyBroken;

        public override bool Equals(object obj) => obj is LegacyClash other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 31 + (int)Winner;
                hash = hash * 31 + PlayerAdvantage.GetHashCode();
                hash = hash * 31 + (int)PlayerMove;
                hash = hash * 31 + (int)EnemyMove;
                hash = hash * 31 + PlayerStrength.GetHashCode();
                hash = hash * 31 + EnemyStrength.GetHashCode();
                hash = hash * 31 + (PlayerBroken ? 1 : 0) + (EnemyBroken ? 2 : 0);
                return hash;
            }
        }

        public static bool operator ==(LegacyClash left, LegacyClash right) => left.Equals(right);
        public static bool operator !=(LegacyClash left, LegacyClash right) => !left.Equals(right);

        public override string ToString()
            => Kind + " (" + Winner + " " + PlayerAdvantage.ToString("0.###") + "): player " + PlayerMove + " " +
                PlayerStrength.ToString("0.###") + (PlayerBroken ? " broken" : string.Empty) + ", enemy " + EnemyMove + " " +
                EnemyStrength.ToString("0.###") + (EnemyBroken ? " broken" : string.Empty);

        // NaN and -0 read as 0, so a mirrored even clash equals (and hashes as) the original.
        private static float Clamp(float value, float min, float max)
            => float.IsNaN(value) || value == 0f ? 0f : value < min ? min : value > max ? max : value;
    }

    /// <summary>The battle phase's clash motion: as a slot resolves, its two queue cards (one skill is one Limbus coin)
    /// move by how the slot played out. <see cref="Classify"/> is pure: it reads a <see cref="LegacyClashInput"/> and never
    /// touches a duel, so the same slot always moves the same way and the guest's view mirrors the host's.</summary>
    public static class LegacyClashMotion
    {
        /// <summary>What a skill brings to a slot: null is empty, a wait (숨고르기) a breath, then a guard or an attack.</summary>
        public static LegacyClashAction ActionOf(LegacySkill skill)
        {
            if (skill == null) return LegacyClashAction.Empty;
            switch (skill.Kind)
            {
                case LegacySkillKind.Attack: return LegacyClashAction.Attack;
                case LegacySkillKind.Defence: return LegacyClashAction.Guard;
                default: return LegacyClashAction.Breath;
            }
        }

        /// <summary>How the slot's two cards move:
        /// <list type="bullet">
        /// <item>attack against attack (합): the whole skills' powers (<see cref="LegacyClashInput.PlayerSkillPower"/>) compare;
        /// the stronger pushes and the weaker recoils, both by the relative gap |P1 − P2| / max(P1, P2); an even 합 bounces
        /// both;</item>
        /// <item>attack against guard: each hit loses the guard's share (guard / hits, as the rules divide it); what is left
        /// pierces (the attacker thrusts, the guard recoils, by the share of the attack that got through), and nothing left
        /// is a block (the attacker rebounds, the guard braces, by the guard's margin over each hit). An attack backed by a
        /// successful 압박 always pierces: its bonus reaches health past the guard;</item>
        /// <item>attack against an empty slot or a breath: the attacker lunges;</item>
        /// <item>attack against a side that dodged it: the attacker whiffs, the dodger side-steps (a dodge without an attack to
        /// dodge counts for nothing);</item>
        /// <item>no attack: each present card settles (guard against guard, a guard or breath against anything but an
        /// attack); nothing at all is <see cref="LegacyClashKind.None"/>.</item>
        /// </list>
        /// A break this slot shows on its side whatever the kind.</summary>
        public static LegacyClash Classify(LegacyClashInput input)
        {
            bool playerAttacks = input.PlayerAction == LegacyClashAction.Attack;
            bool enemyAttacks = input.EnemyAction == LegacyClashAction.Attack;
            if (!playerAttacks && !enemyAttacks) return Settle(input);
            // A dodge counts only against an attack.
            bool playerDodges = input.PlayerDodged && enemyAttacks;
            bool enemyDodges = input.EnemyDodged && playerAttacks;
            if (playerDodges || enemyDodges) return Dodge(input, playerDodges, enemyDodges);
            if (playerAttacks && enemyAttacks) return Clash(input);
            // One side attacks; the other guards, breathes or is empty. Read it with the attacker as the player.
            bool attackerIsPlayer = playerAttacks;
            LegacyClashInput seen = attackerIsPlayer ? input : input.Mirrored();
            LegacyClash clash = seen.EnemyAction == LegacyClashAction.Guard ? AgainstGuard(seen) : Unopposed(seen);
            return attackerIsPlayer ? clash : clash.Mirrored();
        }

        /// <summary>The enemy queue slot the player's next reservation would meet: the player's queue length (breaths, an
        /// automatic 숨고르기 included, count as slots, as the forecast's index does), or -1 when the enemy has no card there or
        /// the duel is not planning. Whether the player may reserve now (inputs held, a versus opponent's turn) is the
        /// screen's to add.</summary>
        public static int NextOpponentSlot(ILegacyDuelView view)
        {
            if (view == null || view.Phase != LegacyDuelPhase.Planning) return -1;
            return NextOpponentSlot(view.PlayerQueue.Count, view.EnemyQueue.Count);
        }

        /// <summary>The same from the queues' lengths: the player's next slot when the enemy has a card there, else -1.</summary>
        public static int NextOpponentSlot(int playerQueueCount, int enemyQueueCount)
            => playerQueueCount >= 0 && playerQueueCount < enemyQueueCount ? playerQueueCount : -1;

        private static LegacyClash Settle(LegacyClashInput input)
        {
            bool playerCard = input.PlayerAction != LegacyClashAction.Empty, enemyCard = input.EnemyAction != LegacyClashAction.Empty;
            return new LegacyClash(playerCard || enemyCard ? LegacyClashKind.Settle : LegacyClashKind.None, LegacyClashSide.None, 0f,
                playerCard ? LegacyClashMove.Settle : LegacyClashMove.Rest, 0f,
                enemyCard ? LegacyClashMove.Settle : LegacyClashMove.Rest, 0f, input.PlayerBroken, input.EnemyBroken);
        }

        private static LegacyClash Dodge(LegacyClashInput input, bool playerDodges, bool enemyDodges)
        {
            // Both dodging each other never happens in a duel; it reads as two side-steps, neither ahead.
            if (playerDodges && enemyDodges)
                return new LegacyClash(LegacyClashKind.Dodged, LegacyClashSide.None, 0f, LegacyClashMove.SideStep, 1f,
                    LegacyClashMove.SideStep, 1f, input.PlayerBroken, input.EnemyBroken);
            return playerDodges
                ? new LegacyClash(LegacyClashKind.Dodged, LegacyClashSide.Player, 1f, LegacyClashMove.SideStep, 1f,
                    LegacyClashMove.Whiff, 1f, input.PlayerBroken, input.EnemyBroken)
                : new LegacyClash(LegacyClashKind.Dodged, LegacyClashSide.Enemy, -1f, LegacyClashMove.Whiff, 1f,
                    LegacyClashMove.SideStep, 1f, input.PlayerBroken, input.EnemyBroken);
        }

        private static LegacyClash Clash(LegacyClashInput input)
        {
            int player = input.PlayerSkillPower, enemy = input.EnemySkillPower;
            if (player == enemy)
                return new LegacyClash(LegacyClashKind.Clash, LegacyClashSide.None, 0f, LegacyClashMove.Bounce, 0f,
                    LegacyClashMove.Bounce, 0f, input.PlayerBroken, input.EnemyBroken);
            float gap = Ratio(Math.Abs((long)player - enemy), Math.Max(player, enemy));
            bool playerWins = player > enemy;
            return new LegacyClash(LegacyClashKind.Clash, playerWins ? LegacyClashSide.Player : LegacyClashSide.Enemy,
                playerWins ? gap : -gap,
                playerWins ? LegacyClashMove.Push : LegacyClashMove.Recoil, gap,
                playerWins ? LegacyClashMove.Recoil : LegacyClashMove.Push, gap, input.PlayerBroken, input.EnemyBroken);
        }

        // The player attacks the enemy's guard. As the rules: each hit carries its power less the guard's share of it, and a
        // successful 압박 adds the whole rolled power again (about the power per hit on each hit) to health, past the guard,
        // so a pressed attack always gets through: what reached health over all it carried.
        private static LegacyClash AgainstGuard(LegacyClashInput input)
        {
            int perHit = input.PlayerPower, share = input.EnemyPower / input.PlayerHits;
            if (perHit > share || input.PlayerPressured)
            {
                float pierce = input.PlayerPressured
                    ? Ratio(Math.Max(0L, (long)perHit - share) + perHit, 2L * perHit)
                    : Ratio(perHit - share, perHit);
                return new LegacyClash(LegacyClashKind.GuardPierced, LegacyClashSide.Player, pierce,
                    LegacyClashMove.Thrust, pierce, LegacyClashMove.Recoil, pierce, input.PlayerBroken, input.EnemyBroken);
            }
            float margin = share > 0 ? Ratio(share - perHit, share) : 0f;
            return new LegacyClash(LegacyClashKind.GuardHeld, LegacyClashSide.Enemy, -margin,
                LegacyClashMove.Rebound, margin, LegacyClashMove.Brace, margin, input.PlayerBroken, input.EnemyBroken);
        }

        // The player attacks an empty slot or a breath.
        private static LegacyClash Unopposed(LegacyClashInput input)
            => new LegacyClash(LegacyClashKind.Unopposed, LegacyClashSide.Player, 1f, LegacyClashMove.Lunge, 1f,
                LegacyClashMove.Rest, 0f, input.PlayerBroken, input.EnemyBroken);

        private static float Ratio(long part, long whole) => whole > 0 ? (float)((double)part / whole) : 0f;
    }
}
