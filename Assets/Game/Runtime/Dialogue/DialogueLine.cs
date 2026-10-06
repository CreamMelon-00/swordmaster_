using System;
using System.Collections.Generic;

namespace TurnLimbo.Runtime.Dialogue
{
    public enum DialogueSide
    {
        Narrator,
        Left,
        Right,
    }

    public sealed class DialogueStageSlot
    {
        public DialogueStageSlot(string speakerName, string speakerRole)
        {
            if (string.IsNullOrWhiteSpace(speakerName))
                throw new ArgumentException("A stage slot requires a speaker name.", nameof(speakerName));

            SpeakerName = speakerName.Trim();
            SpeakerRole = (speakerRole ?? string.Empty).Trim();
        }

        public string SpeakerName { get; }
        public string SpeakerRole { get; }
    }

    public sealed class DialogueStageSnapshot
    {
        public static readonly DialogueStageSnapshot Empty = new DialogueStageSnapshot(null, null);

        public DialogueStageSnapshot(DialogueStageSlot left, DialogueStageSlot right)
        {
            Left = left;
            Right = right;
        }

        public DialogueStageSlot Left { get; }
        public DialogueStageSlot Right { get; }

        public DialogueStageSlot Get(DialogueSide side)
        {
            switch (side)
            {
                case DialogueSide.Left:
                    return Left;
                case DialogueSide.Right:
                    return Right;
                default:
                    return null;
            }
        }
    }

    public sealed class DialogueLine
    {
        public const int MaxTextLength = 240;

        public DialogueLine(int sourceLineNumber, DialogueSide side, string speakerName,
            string speakerRole, string text)
            : this(sourceLineNumber, side, speakerName, speakerRole, text,
                CreateDefaultStage(side, speakerName, speakerRole))
        {
        }

        public DialogueLine(int sourceLineNumber, DialogueSide side, string speakerName,
            string speakerRole, string text, DialogueStageSnapshot stage)
        {
            if (sourceLineNumber < 1) throw new ArgumentOutOfRangeException(nameof(sourceLineNumber));
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Dialogue text is required.", nameof(text));
            if (side != DialogueSide.Narrator && string.IsNullOrWhiteSpace(speakerName))
                throw new ArgumentException("A left or right dialogue line requires a speaker name.", nameof(speakerName));
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            string normalizedText = text.Trim();
            if (normalizedText.Length > MaxTextLength)
                throw new ArgumentException(
                    $"A dialogue line cannot exceed {MaxTextLength} characters.", nameof(text));

            SourceLineNumber = sourceLineNumber;
            Side = side;
            SpeakerName = side == DialogueSide.Narrator ? string.Empty : speakerName.Trim();
            SpeakerRole = side == DialogueSide.Narrator ? string.Empty : (speakerRole ?? string.Empty).Trim();
            Text = normalizedText;
            Stage = stage;
        }

        public int SourceLineNumber { get; }
        public DialogueSide Side { get; }
        public string SpeakerName { get; }
        public string SpeakerRole { get; }
        public string Text { get; }
        public DialogueStageSnapshot Stage { get; }
        public bool ShowsNameplate => Side != DialogueSide.Narrator;
        /// <summary>A thought rather than speech: the whole line is wrapped in parentheses, like "(여긴…)". It is shown
        /// with its parentheses, in the monologue colour, with or without a speaker.</summary>
        public bool IsMonologue => IsMonologueText(Text);

        public static bool IsMonologueText(string text)
        {
            if (text == null) return false;
            string trimmed = text.Trim();
            return trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[trimmed.Length - 1] == ')';
        }

        private static DialogueStageSnapshot CreateDefaultStage(DialogueSide side, string speakerName,
            string speakerRole)
        {
            if (side == DialogueSide.Narrator || string.IsNullOrWhiteSpace(speakerName))
                return DialogueStageSnapshot.Empty;

            var slot = new DialogueStageSlot(speakerName, speakerRole);
            return side == DialogueSide.Left
                ? new DialogueStageSnapshot(slot, null)
                : new DialogueStageSnapshot(null, slot);
        }
    }

    public sealed class DialogueScript
    {
        public DialogueScript(string id, IReadOnlyList<DialogueLine> lines)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A dialogue id is required.", nameof(id));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (lines.Count == 0) throw new ArgumentException("A dialogue requires at least one line.", nameof(lines));

            var copy = new DialogueLine[lines.Count];
            for (int index = 0; index < lines.Count; index++)
                copy[index] = lines[index] ?? throw new ArgumentException("Dialogue lines cannot contain null.", nameof(lines));
            Id = id.Trim();
            Lines = Array.AsReadOnly(copy);
        }

        public string Id { get; }
        public IReadOnlyList<DialogueLine> Lines { get; }
    }
}
