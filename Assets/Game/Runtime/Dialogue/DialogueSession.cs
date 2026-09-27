using System;

namespace TurnLimbo.Runtime.Dialogue
{
    public sealed class DialogueSession
    {
        private int currentIndex;

        public DialogueSession(DialogueScript script)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
        }

        public DialogueScript Script { get; }
        public DialogueLine Current => IsComplete ? null : Script.Lines[currentIndex];
        public int CurrentIndex => currentIndex;
        public int Count => Script.Lines.Count;
        public bool IsComplete => currentIndex >= Count;

        public bool MoveNext()
        {
            if (IsComplete) return false;
            currentIndex++;
            return !IsComplete;
        }

        public void Restart() => currentIndex = 0;

        public void SkipToEnd() => currentIndex = Count;
    }
}
