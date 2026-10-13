using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Combat;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>How a forecast slot reads for the player (<see cref="LegacySlotForecast.Judge"/>): 유리, 비슷 or 불리.</summary>
    public enum LegacyForecastVerdict { Even, Favourable, Unfavourable }

    /// <summary>편성 예상: one slot of a planning turn as the duel's own rules would resolve it, seen from the player's side.
    /// The duel resolves its committed queues on a copy of itself (<see cref="LegacyQueuedDuel.ForecastTurn(List{LegacySlotForecast})"/>,
    /// <see cref="LocalVersusMatch.ForecastTurn"/>): every power roll is the range's average (<see cref="AveragePower"/>), no
    /// step is taken, and buffs, 맞물림, counters and every skill effect act as they would. The live duel never changes. A
    /// slot the turn never reaches because someone fell first is <see cref="Reached"/> false. Damage is a slot's net loss
    /// (restoration is negative), as <see cref="LegacySlotResult"/> counts it.</summary>
    public sealed class LegacySlotForecast
    {
        /// <summary>The verdict's default weights (<see cref="Judge"/>): resistance counts half as much as health, a break
        /// is worth 8 health and a swing under 3 health reads as even.</summary>
        public const int DefaultResistanceWeightPercent = 50, DefaultBreakBonus = 8, DefaultMargin = 3;

        public LegacySlotForecast(int slotIndex, LegacySkill playerSkill, LegacySkill enemySkill,
            int playerHealthDamage, int enemyHealthDamage, int playerResistanceDamage, int enemyResistanceDamage,
            bool playerBroken, bool enemyBroken, DuelMatchOutcome outcome, bool reached = true, int nextTurnSeconds = 0)
        {
            if (slotIndex < 0) throw new ArgumentOutOfRangeException(nameof(slotIndex));
            SlotIndex = slotIndex;
            PlayerSkill = playerSkill;
            EnemySkill = enemySkill;
            PlayerHealthDamage = playerHealthDamage;
            EnemyHealthDamage = enemyHealthDamage;
            PlayerResistanceDamage = playerResistanceDamage;
            EnemyResistanceDamage = enemyResistanceDamage;
            PlayerBroken = playerBroken;
            EnemyBroken = enemyBroken;
            Outcome = outcome;
            Reached = reached;
            NextTurnSeconds = Math.Max(0, nextTurnSeconds);
        }

        /// <summary>A slot the turn never reaches: the fight ended in an earlier slot.</summary>
        internal static LegacySlotForecast Unreached(int slotIndex, LegacySkill playerSkill, LegacySkill enemySkill,
            DuelMatchOutcome outcome)
            => new LegacySlotForecast(slotIndex, playerSkill, enemySkill, 0, 0, 0, 0, false, false, outcome, false);

        public int SlotIndex { get; }
        /// <summary>The skills that meet in the slot, a counter filling an empty side included (null for nothing).</summary>
        public LegacySkill PlayerSkill { get; }
        public LegacySkill EnemySkill { get; }
        public int PlayerHealthDamage { get; }
        public int EnemyHealthDamage { get; }
        public int PlayerResistanceDamage { get; }
        public int EnemyResistanceDamage { get; }
        /// <summary>The slot breaks that side's resistance (whole before it, broken after it).</summary>
        public bool PlayerBroken { get; }
        public bool EnemyBroken { get; }
        /// <summary>The duel's outcome after this slot (<see cref="DuelMatchOutcome.InProgress"/> while it goes on).</summary>
        public DuelMatchOutcome Outcome { get; }
        public bool Reached { get; }
        /// <summary>Planning seconds the player's skill in this slot adds to the next turn (사용 효과 앞 X마다 다음 턴 제한시간,
        /// within the turn's cap); 0 for none. A battle whose clock is held gains nothing from it.</summary>
        public int NextTurnSeconds { get; }

        /// <summary>The power a forecast rolls for a range: its average, a half rounded up (3–4 rolls 4, 15–18 rolls 17).</summary>
        public static int AveragePower(int minPower, int maxPower)
        {
            if (minPower < 0 || maxPower < minPower) throw new ArgumentOutOfRangeException(nameof(maxPower));
            return checked(minPower + (maxPower - minPower + 1) / 2);
        }

        /// <summary>The slot's swing for the player in hundredths of a health point: what the enemy loses minus what the player
        /// loses, health at full weight, resistance at <paramref name="resistanceWeightPercent"/>, and a break worth
        /// <paramref name="breakBonus"/> health to the side that breaks the other.</summary>
        public int Score(int resistanceWeightPercent = DefaultResistanceWeightPercent, int breakBonus = DefaultBreakBonus)
        {
            long score = 100L * (EnemyHealthDamage - PlayerHealthDamage) +
                (long)resistanceWeightPercent * (EnemyResistanceDamage - PlayerResistanceDamage) +
                100L * breakBonus * ((EnemyBroken ? 1 : 0) - (PlayerBroken ? 1 : 0));
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, score));
        }

        /// <summary>유리 when the slot wins the fight or its <see cref="Score"/> reaches <paramref name="margin"/> health, 불리 when
        /// it loses the fight or falls that far below, 비슷 otherwise (an unreached slot too).</summary>
        public LegacyForecastVerdict Judge(int resistanceWeightPercent = DefaultResistanceWeightPercent,
            int breakBonus = DefaultBreakBonus, int margin = DefaultMargin)
        {
            if (!Reached) return LegacyForecastVerdict.Even;
            if (Outcome == DuelMatchOutcome.PlayerVictory) return LegacyForecastVerdict.Favourable;
            if (Outcome == DuelMatchOutcome.EnemyVictory) return LegacyForecastVerdict.Unfavourable;
            long score = Score(resistanceWeightPercent, breakBonus), threshold = 100L * Math.Max(0, margin);
            return score > 0 && score >= threshold ? LegacyForecastVerdict.Favourable
                : score < 0 && -score >= threshold ? LegacyForecastVerdict.Unfavourable
                : LegacyForecastVerdict.Even;
        }

        /// <summary>The verdict's word on screen.</summary>
        public static string VerdictLabel(LegacyForecastVerdict verdict)
            => verdict == LegacyForecastVerdict.Favourable ? "유리" : verdict == LegacyForecastVerdict.Unfavourable ? "불리" : "비슷";

        /// <summary>The one-line expectation the duel HUD shows: <c>예상: 상대 저항 -12 · 내 저항 -6</c>. The enemy's losses come
        /// first (health, then resistance with <c>(붕괴)</c> when it breaks), then the player's, then
        /// <c>다음 턴 제한시간 +N초</c> (<see cref="NextTurnSeconds"/>), then the fight's end.</summary>
        public string Summary
        {
            get
            {
                if (!Reached) return UnreachedSummary;
                var parts = new List<string>(5);
                AddSide(parts, "상대", EnemyHealthDamage, EnemyResistanceDamage, EnemyBroken);
                AddSide(parts, "내", PlayerHealthDamage, PlayerResistanceDamage, PlayerBroken);
                if (NextTurnSeconds > 0) parts.Add(NextTurnTimeText(NextTurnSeconds));
                if (Outcome == DuelMatchOutcome.PlayerVictory) parts.Add("승리");
                else if (Outcome == DuelMatchOutcome.EnemyVictory) parts.Add("패배");
                else if (Outcome == DuelMatchOutcome.Draw) parts.Add("무승부");
                return SummaryPrefix + (parts.Count == 0 ? "변화 없음" : string.Join(" · ", parts));
            }
        }

        public const string SummaryPrefix = "예상: ";

        /// <summary>The words for next turn's planning seconds: <c>다음 턴 제한시간 +4초</c>.</summary>
        public static string NextTurnTimeText(int seconds) => "다음 턴 제한시간 +" + seconds + "초";
        /// <summary>The line for a slot the turn never reaches.</summary>
        public const string UnreachedSummary = SummaryPrefix + "앞 칸에서 결판";

        private static void AddSide(List<string> parts, string side, int health, int resistance, bool broken)
        {
            if (health > 0) parts.Add(side + " 체력 -" + health);
            else if (health < 0) parts.Add(side + " 체력 +" + -health);
            if (resistance != 0 || broken)
                parts.Add(side + " 저항 " + (resistance >= 0 ? "-" + resistance : "+" + -resistance) + (broken ? "(붕괴)" : string.Empty));
        }

        public override string ToString() => "slot " + SlotIndex + " " + Summary;
    }
}
