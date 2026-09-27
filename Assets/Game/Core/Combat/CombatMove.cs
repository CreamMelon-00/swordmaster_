using System;

namespace TurnLimbo.Core.Combat
{
    public sealed class CombatMove
    {
        private CombatMove(string id, CombatMoveType type, DamageType damageType, int power)
        {
            Id = id;
            Type = type;
            DamageType = damageType;
            Power = power;
        }

        public string Id { get; }

        public CombatMoveType Type { get; }

        public DamageType DamageType { get; }

        public int Power { get; }

        public static CombatMove Attack(string id, DamageType damageType, int power)
        {
            ValidateId(id);

            if (damageType == DamageType.None)
            {
                throw new ArgumentException("An attack must declare a damage type.", nameof(damageType));
            }

            ValidatePositivePower(power);
            return new CombatMove(id, CombatMoveType.Attack, damageType, power);
        }

        public static CombatMove Guard(string id, int power)
        {
            ValidateId(id);
            ValidatePositivePower(power);
            return new CombatMove(id, CombatMoveType.Guard, DamageType.None, power);
        }

        public static CombatMove Wait(string id = "wait")
        {
            ValidateId(id);
            return new CombatMove(id, CombatMoveType.Wait, DamageType.None, 0);
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A move id is required.", nameof(id));
            }
        }

        private static void ValidatePositivePower(int power)
        {
            if (power <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(power), power, "Power must be greater than zero.");
            }
        }
    }
}
