using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Runtime.Barks
{
    /// <summary>When a battle bark may be said: the <c>@on &lt;상황&gt;</c> of a bark file (<see cref="BarkScript.TriggerName"/>
    /// gives each one's word). Which hits count, and how often each may fire, is <see cref="BarkTracker"/>'s.</summary>
    public enum BarkTrigger
    {
        /// <summary><c>start</c>: the battle's first planning turn.</summary>
        Start,
        /// <summary><c>enemy-broken</c>: the enemy's resistance breaks.</summary>
        EnemyBroken,
        /// <summary><c>player-broken</c>: 엘리사's resistance breaks.</summary>
        PlayerBroken,
        /// <summary><c>enemy-hurt</c>: one hit takes the decisive share of the enemy's health.</summary>
        EnemyHurt,
        /// <summary><c>player-hurt</c>: one hit takes the decisive share of 엘리사's health.</summary>
        PlayerHurt,
        /// <summary><c>enemy-low</c>: the enemy's health first drops to the low share (30%) or less.</summary>
        EnemyLow,
        /// <summary><c>player-low</c>: 엘리사's health first drops to the low share (30%) or less.</summary>
        PlayerLow,
        /// <summary><c>sutun</c>: right after the 서막's 수훈 resumes the battle (mission 4).</summary>
        Sutun,
    }

    /// <summary>Who says a bark: the enemy (the default) or 엘리사.</summary>
    public enum BarkSpeaker
    {
        Enemy,
        Player,
    }

    /// <summary>One <c>@on</c> block of a bark file: what one fighter may say when one trigger fires (one line is picked at
    /// random each time).</summary>
    public sealed class BarkEntry
    {
        public BarkEntry(BarkTrigger trigger, BarkSpeaker speaker, IReadOnlyList<string> lines, int sourceLineNumber)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (lines.Count == 0) throw new ArgumentException("A bark entry needs at least one line.", nameof(lines));
            if (sourceLineNumber < 1) throw new ArgumentOutOfRangeException(nameof(sourceLineNumber));
            var copy = new string[lines.Count];
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index]?.Trim();
                if (string.IsNullOrEmpty(line)) throw new ArgumentException("Bark lines cannot be empty.", nameof(lines));
                if (line.Length > BarkScript.MaxTextLength)
                    throw new ArgumentException($"A bark cannot exceed {BarkScript.MaxTextLength} characters.", nameof(lines));
                copy[index] = line;
            }
            Trigger = trigger;
            Speaker = speaker;
            Lines = Array.AsReadOnly(copy);
            SourceLineNumber = sourceLineNumber;
        }

        public BarkTrigger Trigger { get; }
        public BarkSpeaker Speaker { get; }
        public IReadOnlyList<string> Lines { get; }
        /// <summary>The line of its <c>@on</c>.</summary>
        public int SourceLineNumber { get; }
    }

    /// <summary>A line on screen: one fighter's speech bubble.</summary>
    public sealed class BarkLine
    {
        public BarkLine(BarkTrigger trigger, BarkSpeaker speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("A bark needs its text.", nameof(text));
            Trigger = trigger;
            Speaker = speaker;
            Text = text.Trim();
        }

        public BarkTrigger Trigger { get; }
        public BarkSpeaker Speaker { get; }
        public string Text { get; }
        /// <summary>A thought in parentheses, like "(이대로는…)": shown with them, in the dialogue's monologue colour.</summary>
        public bool IsMonologue => DialogueLine.IsMonologueText(Text);
    }

    /// <summary>A battle's barks (<c>Resources/Barks/mission-NN.txt</c> or <c>stage-NN.txt</c>, read by
    /// <see cref="BarkScriptParser"/>): for each trigger and speaker, the lines that may be said. A file with only comments
    /// is valid and says nothing (<see cref="IsEmpty"/>).</summary>
    public sealed class BarkScript
    {
        /// <summary>A bubble holds one short line: this many characters at most (about 20 read best).</summary>
        public const int MaxTextLength = 40;
        public const string ResourceFolder = "Barks/";

        private static readonly string[] Names =
        {
            "start", "enemy-broken", "player-broken", "enemy-hurt", "player-hurt", "enemy-low", "player-low", "sutun",
        };

        private readonly BarkEntry[] byKey;

        public BarkScript(string id, IReadOnlyList<BarkEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A bark file id is required.", nameof(id));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var copy = new BarkEntry[entries.Count];
            byKey = new BarkEntry[Names.Length * 2];
            for (int index = 0; index < entries.Count; index++)
            {
                BarkEntry entry = entries[index] ?? throw new ArgumentException("Bark entries cannot contain null.", nameof(entries));
                int key = Key(entry.Trigger, entry.Speaker);
                if (byKey[key] != null)
                    throw new ArgumentException($"{TriggerName(entry.Trigger)} has two entries for one speaker.", nameof(entries));
                byKey[key] = copy[index] = entry;
            }
            Id = id.Trim();
            Entries = Array.AsReadOnly(copy);
        }

        public string Id { get; }
        public IReadOnlyList<BarkEntry> Entries { get; }
        public bool IsEmpty => Entries.Count == 0;

        /// <summary>The lines <paramref name="speaker"/> may say when <paramref name="trigger"/> fires, or null.</summary>
        public BarkEntry Find(BarkTrigger trigger, BarkSpeaker speaker) => byKey[Key(trigger, speaker)];

        /// <summary>The trigger's word in a bark file (<c>start</c>, <c>enemy-broken</c>, …).</summary>
        public static string TriggerName(BarkTrigger trigger) => Names[(int)trigger];

        /// <summary>Every trigger's word, in <see cref="BarkTrigger"/> order.</summary>
        public static IReadOnlyList<string> TriggerNames => Array.AsReadOnly(Names);

        public static bool TryParseTrigger(string value, out BarkTrigger trigger)
        {
            int index = Array.IndexOf(Names, value);
            trigger = index >= 0 ? (BarkTrigger)index : BarkTrigger.Start;
            return index >= 0;
        }

        /// <summary>A story mission's bark file (Resources path, no extension): <c>Barks/mission-04</c>.</summary>
        public static string MissionResource(int missionNumber) => ResourceFolder + "mission-" + missionNumber.ToString("00");

        /// <summary>A stage's bark file (Resources path, no extension): <c>Barks/stage-03</c>.</summary>
        public static string StageResource(int stageNumber) => ResourceFolder + "stage-" + stageNumber.ToString("00");

        internal static int Key(BarkTrigger trigger, BarkSpeaker speaker) => (int)trigger * 2 + (int)speaker;
    }
}
