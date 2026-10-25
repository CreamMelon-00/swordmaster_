using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.LegacyCombat
{
    // Jealousy and Fixed snapshots display the boss resolver's own counters; they are not collection modifiers.
    public enum LegacyStatusKind { PowerUp, Protection, Vulnerability, Bleeding, Jealousy, Fixed, Tension }

    /// <summary>A read-only description of one applied status. Counts survive turn boundaries.</summary>
    public readonly struct LegacyStatusSnapshot
    {
        public LegacyStatusSnapshot(LegacyStatusKind kind, int percent, int remainingCount)
        {
            Kind = kind;
            Percent = percent;
            RemainingCount = remainingCount;
        }
        public LegacyStatusKind Kind { get; }
        public int Percent { get; }
        public int RemainingCount { get; }
    }

    /// <summary>Fighter-owned combat statuses. Power lasts for actual techniques; protection and vulnerability last
    /// for incoming attack techniques, including a fully guarded attack, but excluding a successful dodge. A multi-hit
    /// technique consumes one count. The resolver snapshots modifiers before granting this slot's new statuses.</summary>
    public sealed class LegacyStatusCollection
    {
        public const int BleedingDamagePerHit = 2;
        public const int TensionHitReduction = 1;
        private sealed class Entry
        {
            internal LegacyStatusKind Kind;
            internal int Percent, Count, Serial;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<LegacyStatusSnapshot> snapshots = new List<LegacyStatusSnapshot>();
        private readonly IReadOnlyList<LegacyStatusSnapshot> view;
        private int serial;

        public LegacyStatusCollection() => view = snapshots.AsReadOnly();
        public IReadOnlyList<LegacyStatusSnapshot> Statuses => view;
        /// <summary>Capture before applying a slot's effects, then consume incoming statuses only through this serial.
        /// A freshly granted status cannot expire on the same attack that accompanied its grant.</summary>
        internal int GrantSerial => serial;
        public int PowerPercent => Sum(LegacyStatusKind.PowerUp);
        /// <summary>Remaining actual attack techniques. Each executed hit deals fixed bleeding damage, but a whole
        /// attack technique spends only one count after its last executed hit.</summary>
        public int BleedingCount
        {
            get
            {
                foreach (Entry entry in entries) if (entry.Kind == LegacyStatusKind.Bleeding) return entry.Count;
                return 0;
            }
        }
        /// <summary>Remaining actual attack techniques. Single-hit attacks also spend one count.</summary>
        public int TensionCount
        {
            get
            {
                foreach (Entry entry in entries) if (entry.Kind == LegacyStatusKind.Tension) return entry.Count;
                return 0;
            }
        }
        /// <summary>Actual protection entries, even when vulnerability cancels their net damage reduction.</summary>
        public bool HasProtection
        {
            get
            {
                foreach (Entry entry in entries) if (entry.Kind == LegacyStatusKind.Protection) return true;
                return false;
            }
        }
        // Net vulnerability before capping protection: excess protection still offsets vulnerability.
        public int ProtectionPercent => Math.Min(100, Sum(LegacyStatusKind.Protection) - Sum(LegacyStatusKind.Vulnerability));
        public double ReceivedMultiplier => Math.Max(0, 100 - ProtectionPercent) / 100d;

        public void Grant(LegacyStatusKind kind, int percent, int count)
        {
            if (kind == LegacyStatusKind.Bleeding) percent = BleedingDamagePerHit;
            if (kind == LegacyStatusKind.Tension) percent = TensionHitReduction;
            if (count <= 0 || percent == 0) return;
            // Vulnerability stacks duration, never its intensity. Other strengths remain independent, preserving
            // additive power/protection buffs. Different vulnerability strengths likewise remain separate.
            if (kind == LegacyStatusKind.Vulnerability || kind == LegacyStatusKind.Bleeding || kind == LegacyStatusKind.Tension)
                foreach (Entry existing in entries)
                    if (existing.Kind == kind && existing.Percent == percent)
                    {
                        existing.Count = checked(existing.Count + count);
                        Refresh();
                        return;
                    }
            entries.Add(new Entry { Kind = kind, Percent = percent, Count = count, Serial = checked(++serial) });
            Refresh();
        }

        internal void Grant(LegacySkillEffect effect)
        {
            if (effect == null || !effect.HasBuff) return;
            Grant(LegacyStatusKind.PowerUp, effect.BuffPowerPercent, effect.BuffSlots);
            int protection = effect.BuffProtectionPercent;
            Grant(protection < 0 ? LegacyStatusKind.Vulnerability : LegacyStatusKind.Protection,
                Math.Abs(protection), effect.BuffSlots);
        }

        internal void ConsumeTechnique() => Consume(true, serial);
        internal void ConsumeIncomingAttack(int grantedThrough) => Consume(false, grantedThrough);

        public void GrantBleeding(int count) => Grant(LegacyStatusKind.Bleeding, BleedingDamagePerHit, count);

        /// <summary>Stanch bleeding at slot start, before either fighter grants this slot's new bleeding.</summary>
        public int ReduceBleeding(int count)
        {
            if (count <= 0) return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Kind != LegacyStatusKind.Bleeding) continue;
                int removed = Math.Min(count, entry.Count);
                entry.Count -= removed;
                if (entry.Count == 0) entries.RemoveAt(i);
                Refresh();
                return removed;
            }
            return 0;
        }

        /// <summary>Only bleeding present before the slot's initial effects can hurt this attack. Stanching all old
        /// bleeding and granting a new stack creates a later serial, which cannot activate in the same slot.</summary>
        public int BleedingDamageThrough(int grantedThrough)
        {
            foreach (Entry entry in entries)
                if (entry.Kind == LegacyStatusKind.Bleeding && entry.Serial <= grantedThrough)
                    return BleedingDamagePerHit;
            return 0;
        }

        /// <summary>Called once after an actual attack's executed hits, never per hit or for a guard/breath/canceled
        /// attack. Newly granted counts survive; when merged into an old stack, only that old attack count is spent.</summary>
        public void ConsumeBleedingAttack(int grantedThrough)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Kind != LegacyStatusKind.Bleeding || entry.Serial > grantedThrough) continue;
                if (--entry.Count == 0) entries.RemoveAt(i);
                Refresh();
                return;
            }
        }

        public void GrantTension(int count) => Grant(LegacyStatusKind.Tension, TensionHitReduction, count);

        /// <summary>Calm the existing tension at technique start, before either fighter grants fresh tension.</summary>
        public int ReduceTension(int count)
        {
            if (count <= 0) return 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Kind != LegacyStatusKind.Tension) continue;
                int removed = Math.Min(count, entry.Count);
                entry.Count -= removed;
                if (entry.Count == 0) entries.RemoveAt(i);
                Refresh();
                return removed;
            }
            return 0;
        }

        /// <summary>Only tension present before this technique's start effects can change its execution hit count.
        /// Fully cleansing and then regranting tension cannot affect an attack that has already begun.</summary>
        public bool TensionActiveThrough(int grantedThrough)
        {
            foreach (Entry entry in entries)
                if (entry.Kind == LegacyStatusKind.Tension && entry.Serial <= grantedThrough) return true;
            return false;
        }

        /// <summary>Spend once after an actual attack, including a single-hit attack or counter. Guards, breaths, empty
        /// actions and cancelled attacks never call this. Freshly granted counts remain for later attacks.</summary>
        public void ConsumeTensionAttack(int grantedThrough)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Kind != LegacyStatusKind.Tension || entry.Serial > grantedThrough) continue;
                if (--entry.Count == 0) entries.RemoveAt(i);
                Refresh();
                return;
            }
        }

        /// <summary>Spend every protection entry, leaving power and vulnerability untouched. True only when something
        /// was removed; callers must do this before taking the current attack's power and received-damage snapshots.</summary>
        public bool ConsumeProtection()
        {
            bool removed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].Kind == LegacyStatusKind.Protection)
                {
                    entries.RemoveAt(i);
                    removed = true;
                }
            if (removed) Refresh();
            return removed;
        }

        internal int SpendProtectionForPower(LegacySkill skill)
        {
            if (skill == null || skill.Kind != LegacySkillKind.Attack) return 0;
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            return effect?.HasProtectionSpend == true && ConsumeProtection() ? effect.ConsumeProtectionPowerBonus : 0;
        }

        private void Consume(bool power, int grantedThrough)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                if (entry.Kind == LegacyStatusKind.Bleeding || entry.Kind == LegacyStatusKind.Tension) continue;
                if ((entry.Kind == LegacyStatusKind.PowerUp) != power || entry.Serial > grantedThrough) continue;
                if (--entry.Count <= 0) entries.RemoveAt(i);
            }
            Refresh();
        }

        public void Clear()
        {
            entries.Clear();
            snapshots.Clear();
            serial = 0;
        }

        public LegacyStatusCollection Copy()
        {
            var copy = new LegacyStatusCollection { serial = serial };
            foreach (Entry entry in entries)
                copy.entries.Add(new Entry { Kind = entry.Kind, Percent = entry.Percent, Count = entry.Count, Serial = entry.Serial });
            copy.Refresh();
            return copy;
        }

        private int Sum(LegacyStatusKind kind)
        {
            int total = 0;
            foreach (Entry entry in entries) if (entry.Kind == kind) total = checked(total + entry.Percent);
            return total;
        }

        private void Refresh()
        {
            snapshots.Clear();
            foreach (Entry entry in entries) snapshots.Add(new LegacyStatusSnapshot(entry.Kind, entry.Percent, entry.Count));
        }
    }
}
