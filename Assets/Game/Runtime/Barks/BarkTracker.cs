using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Barks
{
    /// <summary>What one fighter took in one moment of a battle: a hit, or a slot's opening effects (which can break
    /// resistance, like 라우다레's 상대 붕괴, but deal no damage).</summary>
    public readonly struct BarkBlow
    {
        /// <param name="resistanceBroke">The fighter's resistance broke in this moment.</param>
        /// <param name="healthDamage">The health it lost to this hit alone, as <see cref="LegacyDecisiveHit.IsHeavy"/> counts it.</param>
        /// <param name="health">Its health after the moment.</param>
        /// <param name="maxHealth">Its maximum health.</param>
        public BarkBlow(bool resistanceBroke, int healthDamage, int health, int maxHealth)
        {
            ResistanceBroke = resistanceBroke;
            HealthDamage = Math.Max(0, healthDamage);
            Health = health;
            MaxHealth = maxHealth;
        }

        public bool ResistanceBroke { get; }
        public int HealthDamage { get; }
        public int Health { get; }
        public int MaxHealth { get; }
    }

    /// <summary>The battle-bark rules (presentation only; the combat rules never read them) and what each fighter's speech
    /// bubble shows. One per battle attempt: a retry starts a new one.
    /// <list type="bullet">
    /// <item><c>start</c> (<see cref="BattleStarted"/>), <c>sutun</c> (<see cref="Empowered"/>), <c>*-broken</c> and
    /// <c>*-low</c> fire at most once a battle; <c>*-hurt</c> may fire again once <see cref="HurtCooldownSeconds"/> have
    /// passed since it was last said.</item>
    /// <item>A hit is heavy (<c>*-hurt</c>) when it takes the decisive share of the target's health alone
    /// (<see cref="LegacyDecisiveHit.IsHeavy"/> with <see cref="HeavyHealthPercent"/>); <c>*-low</c> fires on the first
    /// hit after which the fighter is at <see cref="LowHealthPercent"/>% of its health or less, still standing.</item>
    /// <item>Each fighter has one bubble. When one moment fires several triggers for one speaker, the strongest is said:
    /// <c>start</c>/<c>sutun</c>, then <c>*-low</c>, <c>*-broken</c>, <c>*-hurt</c>. A new line replaces the one on screen
    /// unless that one is stronger; then the new one is not said.</item>
    /// <item>A trigger is spent only when its line shows: one passed over (a stronger line won the bubble) can still fire
    /// at its next chance (the next break, the next heavy hit, the next hit while low).</item>
    /// <item>A line shows for <see cref="ShowSeconds"/> of real time (<see cref="Advance"/>), so bullet time, slow motion
    /// and hit stop never stretch it. Lines wrapped in <c>( )</c> are thoughts (<see cref="BarkLine.IsMonologue"/>).</item>
    /// </list></summary>
    public sealed class BarkTracker
    {
        public const float DefaultShowSeconds = 2.5f;
        public const float DefaultHurtCooldownSeconds = 8f;
        public const int DefaultLowHealthPercent = 30;

        private sealed class Shown
        {
            public Shown(BarkLine line, int sequence)
            {
                Line = line;
                Sequence = sequence;
            }

            public readonly BarkLine Line;
            public readonly int Sequence;
            public float Elapsed;
            // Said after the frame's time had passed: it starts aging with the next Advance.
            public bool Fresh = true;
        }

        private readonly BarkScript script;
        private readonly Func<int, int> pick;
        private readonly bool[] spent;
        private readonly float[] cooldowns;
        private readonly int[] lastPicks;
        private readonly Shown[] shown = new Shown[2];
        private readonly List<BarkTrigger> fired = new List<BarkTrigger>(6);
        private bool started, empowered;
        private int sequence;

        /// <param name="pick">Picks one of a block's lines: given a count, returns an index below it. Random when null.</param>
        public BarkTracker(BarkScript script, Func<int, int> pick = null)
        {
            this.script = script ?? throw new ArgumentNullException(nameof(script));
            if (pick == null)
            {
                var random = new Random(Environment.TickCount);
                pick = random.Next;
            }
            this.pick = pick;
            int keys = BarkScript.TriggerNames.Count * 2;
            spent = new bool[keys];
            cooldowns = new float[keys];
            lastPicks = new int[keys];
            for (int key = 0; key < keys; key++) lastPicks[key] = -1;
        }

        public BarkScript Script => script;
        /// <summary>Real seconds a line stays on screen; 0 switches barks off (nothing is said or spent).</summary>
        public float ShowSeconds { get; set; } = DefaultShowSeconds;
        /// <summary>Real seconds after a <c>*-hurt</c> line before that block may be said again.</summary>
        public float HurtCooldownSeconds { get; set; } = DefaultHurtCooldownSeconds;
        /// <summary>The health share, in percent, at or under which <c>*-low</c> fires; 0 switches it off.</summary>
        public int LowHealthPercent { get; set; } = DefaultLowHealthPercent;
        /// <summary>The share of maximum health one hit must take for <c>*-hurt</c> (the decisive close-up's); 0 switches it
        /// off.</summary>
        public int HeavyHealthPercent { get; set; } = LegacyDecisiveHit.DefaultHealthDamagePercent;

        /// <summary>What <paramref name="speaker"/>'s bubble shows now, or null.</summary>
        public BarkLine Current(BarkSpeaker speaker) => shown[(int)speaker]?.Line;

        public bool IsShowing(BarkSpeaker speaker) => shown[(int)speaker] != null;

        public bool IsShowingAny => shown[0] != null || shown[1] != null;

        /// <summary>Real seconds the line in <paramref name="speaker"/>'s bubble has been on screen (0 when none).</summary>
        public float Elapsed(BarkSpeaker speaker) => shown[(int)speaker]?.Elapsed ?? 0f;

        /// <summary>Changes whenever a new line takes <paramref name="speaker"/>'s bubble, even the same words again
        /// (0 when none shows), so the bubble can pop up anew.</summary>
        public int Sequence(BarkSpeaker speaker) => shown[(int)speaker]?.Sequence ?? 0;

        /// <summary>The battle's first planning turn (<c>start</c>). Only the first call counts. Returns the lines said.</summary>
        public int BattleStarted()
        {
            if (started) return 0;
            started = true;
            return Say(BarkTrigger.Start);
        }

        /// <summary>The 서막's 수훈 has resumed the battle (<c>sutun</c>). Only the first call counts. Returns the lines said.</summary>
        public int Empowered()
        {
            if (empowered) return 0;
            empowered = true;
            return Say(BarkTrigger.Sutun);
        }

        /// <summary>A moment of the battle that the duel goes on after: what each fighter took in it. Returns the lines said.</summary>
        public int Hit(BarkBlow enemy, BarkBlow player)
        {
            fired.Clear();
            Collect(enemy, BarkTrigger.EnemyBroken, BarkTrigger.EnemyHurt, BarkTrigger.EnemyLow);
            Collect(player, BarkTrigger.PlayerBroken, BarkTrigger.PlayerHurt, BarkTrigger.PlayerLow);
            return Say();
        }

        /// <summary>Lets <paramref name="realSeconds"/> pass: lines on screen age (and leave after <see cref="ShowSeconds"/>),
        /// and the <c>*-hurt</c> cooldowns run down. A line said since the last call starts aging now.</summary>
        public void Advance(float realSeconds)
        {
            if (!(realSeconds > 0f)) return;
            for (int index = 0; index < shown.Length; index++)
            {
                Shown line = shown[index];
                if (line == null) continue;
                if (line.Fresh) line.Fresh = false;
                else line.Elapsed = float.IsInfinity(realSeconds) ? float.MaxValue : line.Elapsed + realSeconds;
                if (line.Elapsed >= ShowSeconds) shown[index] = null;
            }
            for (int key = 0; key < cooldowns.Length; key++)
                if (cooldowns[key] > 0f) cooldowns[key] = Math.Max(0f, cooldowns[key] - realSeconds);
        }

        /// <summary>Takes both bubbles away at once (an event scene or the finishing blow takes the moment over). Spent
        /// triggers stay spent.</summary>
        public void Hide()
        {
            shown[0] = shown[1] = null;
        }

        private void Collect(BarkBlow blow, BarkTrigger broken, BarkTrigger hurt, BarkTrigger low)
        {
            if (blow.ResistanceBroke) fired.Add(broken);
            if (LegacyDecisiveHit.IsHeavy(blow.HealthDamage, blow.MaxHealth, HeavyHealthPercent)) fired.Add(hurt);
            if (LowHealthPercent > 0 && blow.HealthDamage > 0 && blow.Health > 0 && blow.MaxHealth > 0 &&
                (long)blow.Health * 100 <= (long)LowHealthPercent * blow.MaxHealth)
                fired.Add(low);
        }

        private int Say(BarkTrigger trigger)
        {
            fired.Clear();
            fired.Add(trigger);
            return Say();
        }

        private int Say()
        {
            if (!(ShowSeconds > 0f) || script.IsEmpty || fired.Count == 0) return 0;
            int said = 0;
            for (int side = 0; side < shown.Length; side++)
            {
                var speaker = (BarkSpeaker)side;
                BarkEntry best = null;
                int bestPriority = -1;
                foreach (BarkTrigger trigger in fired)
                {
                    BarkEntry entry = script.Find(trigger, speaker);
                    if (entry == null || !IsReady(entry)) continue;
                    int priority = Priority(trigger);
                    if (priority > bestPriority)
                    {
                        best = entry;
                        bestPriority = priority;
                    }
                }
                // A stronger line still on screen keeps the bubble; this one waits for its next chance.
                if (best == null || shown[side] != null && Priority(shown[side].Line.Trigger) > bestPriority) continue;
                Show(best);
                said++;
            }
            return said;
        }

        private bool IsReady(BarkEntry entry)
        {
            int key = BarkScript.Key(entry.Trigger, entry.Speaker);
            return Repeats(entry.Trigger) ? cooldowns[key] <= 0f : !spent[key];
        }

        private void Show(BarkEntry entry)
        {
            int key = BarkScript.Key(entry.Trigger, entry.Speaker);
            int count = entry.Lines.Count, last = lastPicks[key], index = 0;
            if (count > 1)
            {
                // Never the same line twice in a row from one block.
                int choices = last >= 0 ? count - 1 : count;
                index = Math.Min(Math.Max(0, pick(choices)), choices - 1);
                if (last >= 0 && index >= last) index++;
            }
            lastPicks[key] = index;
            if (Repeats(entry.Trigger)) cooldowns[key] = Math.Max(0f, HurtCooldownSeconds);
            else spent[key] = true;
            shown[(int)entry.Speaker] = new Shown(new BarkLine(entry.Trigger, entry.Speaker, entry.Lines[index]), ++sequence);
        }

        private static bool Repeats(BarkTrigger trigger) => trigger == BarkTrigger.EnemyHurt || trigger == BarkTrigger.PlayerHurt;

        private static int Priority(BarkTrigger trigger)
        {
            switch (trigger)
            {
                case BarkTrigger.Start:
                case BarkTrigger.Sutun:
                    return 4;
                case BarkTrigger.EnemyLow:
                case BarkTrigger.PlayerLow:
                    return 3;
                case BarkTrigger.EnemyBroken:
                case BarkTrigger.PlayerBroken:
                    return 2;
                default:
                    return 1;
            }
        }
    }
}
