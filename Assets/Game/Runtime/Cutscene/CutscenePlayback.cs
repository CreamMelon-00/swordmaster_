using System;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Runtime.Cutscene
{
    /// <summary>What a cutscene drives: the scene (Presentation) and its dialogue box.</summary>
    public interface ICutsceneStage
    {
        /// <summary>Starts a staging step; timed ones play out over <see cref="CutsceneStep.Seconds"/>.</summary>
        void Run(CutsceneStep step);
        void ShowLine(DialogueLine line, int lineIndex, int lineCount);
        void HideLine();
    }

    /// <summary>Plays a cutscene's steps in order. Instant steps run at once; a timed step holds for its seconds unless
    /// written with <c>&amp;</c>; a line holds until <see cref="Advance"/>. The dialogue box stays up between lines and
    /// goes away once a step that holds for time begins, so the scene is seen without it.</summary>
    public sealed class CutscenePlayback
    {
        private readonly ICutsceneStage stage;
        private int nextStep;
        private int nextLine;
        private bool lineShown;

        public CutscenePlayback(CutsceneScript script, ICutsceneStage stage)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
        }

        public CutsceneScript Script { get; }
        public bool IsStarted { get; private set; }
        public bool IsComplete { get; private set; }
        /// <summary>The line waiting for the player, or null.</summary>
        public DialogueLine CurrentLine { get; private set; }
        public CutsceneStep CurrentChoice { get; private set; }
        public bool PlayFullVoiceMemories { get; set; } = true;
        /// <summary>How long the current timed step still holds.</summary>
        public float HoldRemaining { get; private set; }
        /// <summary>Steps run so far.</summary>
        public int StepsRun => nextStep;

        public void Start()
        {
            if (IsStarted) return;
            IsStarted = true;
            Continue();
        }

        /// <summary>Lets time pass. A finished hold starts the next steps on the same call; any time beyond it is not
        /// carried over, so the scene and the steps start together.</summary>
        public void Tick(float seconds)
        {
            if (!IsStarted || IsComplete || CurrentLine != null || HoldRemaining <= 0f) return;
            HoldRemaining -= Math.Max(0f, seconds);
            if (HoldRemaining > 0f) return;
            HoldRemaining = 0f;
            Continue();
        }

        /// <summary>Moves past the waiting line. Returns false when no line is waiting (a timed step is playing).</summary>
        public bool Advance()
        {
            if (CurrentLine == null || CurrentChoice != null) return false;
            CurrentLine = null;
            Continue();
            return true;
        }

        public bool JumpTo(string label)
        {
            if (!IsStarted || IsComplete || !Script.TryFindMark(label, out int target)) return false;
            if (lineShown) HideLine();
            CurrentLine = null;
            CurrentChoice = null;
            HoldRemaining = 0f;
            nextStep = target;
            Continue();
            return true;
        }

        public bool Choose(int index)
        {
            if (CurrentChoice == null || (index != 0 && index != 1)) return false;
            string label = index == 0 ? CurrentChoice.TargetA : CurrentChoice.TargetB;
            CurrentChoice = null;
            return JumpTo(label);
        }

        private void Continue()
        {
            int stepsThisPass = 0;
            while (nextStep < Script.Steps.Count)
            {
                if (++stepsThisPass > Script.Steps.Count * 2)
                    throw new InvalidOperationException($"Cutscene '{Script.Id}' loops without a waiting step.");
                CutsceneStep step = Script.Steps[nextStep++];
                if (step.Kind == CutsceneStepKind.Mark || step.Kind == CutsceneStepKind.EndIf) continue;
                if (step.Kind == CutsceneStepKind.Jump)
                {
                    Script.TryFindMark(step.Label, out nextStep);
                    continue;
                }
                if (step.Kind == CutsceneStepKind.IfFull)
                {
                    if (!PlayFullVoiceMemories) SkipCondition();
                    continue;
                }
                if (step.Kind == CutsceneStepKind.Choice)
                {
                    CurrentChoice = step;
                    return;
                }
                if (step.Kind == CutsceneStepKind.Line)
                {
                    CurrentLine = step.Line;
                    lineShown = true;
                    stage.ShowLine(step.Line, nextLine++, Script.LineCount);
                    return;
                }
                if (lineShown && step.HoldSeconds > 0f) HideLine();
                stage.Run(step);
                if (step.HoldSeconds > 0f)
                {
                    HoldRemaining = step.HoldSeconds;
                    return;
                }
            }
            if (lineShown) HideLine();
            IsComplete = true;
        }

        private void SkipCondition()
        {
            int depth = 1;
            while (nextStep < Script.Steps.Count && depth > 0)
            {
                CutsceneStep step = Script.Steps[nextStep++];
                if (step.Kind == CutsceneStepKind.IfFull) depth++;
                else if (step.Kind == CutsceneStepKind.EndIf) depth--;
            }
        }

        private void HideLine()
        {
            lineShown = false;
            stage.HideLine();
        }
    }
}
