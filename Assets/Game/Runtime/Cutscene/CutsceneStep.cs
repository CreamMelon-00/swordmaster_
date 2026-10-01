using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Dialogue;

namespace TurnLimbo.Runtime.Cutscene
{
    public enum CutsceneStepKind
    {
        /// <summary>A dialogue line; the cutscene waits until the player advances it.</summary>
        Line,
        Wait,
        /// <summary>To black (<see cref="CutsceneStep.ToBlack"/>) or back from it.</summary>
        Fade,
        /// <summary>The letterbox bars in or out.</summary>
        Bars,
        Camera,
        /// <summary>A full-screen image over the scene, or none (<see cref="CutsceneStep.Resource"/> null).</summary>
        Image,
        Actor,
    }

    /// <summary>Who stands in the scene. Elise is the player's figure; the knight and the dummy share the other one,
    /// so only one of them can be on stage at a time.</summary>
    public enum CutsceneActor
    {
        Elise,
        Knight,
        Dummy,
    }

    public enum CutsceneActorAction
    {
        /// <summary>Appears (or jumps) at <see cref="CutsceneStep.X"/>, optionally facing a side.</summary>
        Place,
        Hide,
        /// <summary>Slides to <see cref="CutsceneStep.X"/> over the step's seconds.</summary>
        Move,
        Face,
        Pose,
        /// <summary>Plays one sword stroke, then returns to idle.</summary>
        Attack,
    }

    public enum CutsceneFacing
    {
        /// <summary>Keeps the current facing.</summary>
        Unchanged,
        Left,
        Right,
    }

    public enum CutscenePose
    {
        /// <summary>The breathing loop.</summary>
        Idle,
        /// <summary>The hit reaction: held for Elise and the knight, played once for the dummy.</summary>
        Hurt,
        /// <summary>The guard reaction, held (not for the dummy).</summary>
        Block,
    }

    public enum CutsceneAttack
    {
        Slash,
        Pierce,
        Blunt,
    }

    /// <summary>One instruction of a cutscene, in source order. Timed steps last <see cref="Seconds"/> and hold the
    /// cutscene until they end unless they were written with a trailing <c>&amp;</c> (<see cref="Waits"/> false).</summary>
    public sealed class CutsceneStep
    {
        /// <summary>How long a sword stroke lasts; the blade connects at <see cref="AttackImpactSeconds"/>.</summary>
        public const float AttackSeconds = 1.26f;
        public const float AttackImpactSeconds = .42f;
        public const float DefaultCameraSize = 6f;
        public const float MinimumCameraSize = 2f;
        public const float MaximumCameraSize = 6f;
        public const float MaximumSeconds = 30f;
        public const float MaximumX = 30f;

        private CutsceneStep(CutsceneStepKind kind, int sourceLineNumber, float seconds, bool waits)
        {
            if (sourceLineNumber < 1) throw new ArgumentOutOfRangeException(nameof(sourceLineNumber));
            if (seconds < 0f || seconds > MaximumSeconds || float.IsNaN(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Kind = kind;
            SourceLineNumber = sourceLineNumber;
            Seconds = seconds;
            Waits = waits;
        }

        public CutsceneStepKind Kind { get; }
        public int SourceLineNumber { get; }
        public float Seconds { get; }
        /// <summary>Whether the cutscene holds until this step ends (false when written with <c>&amp;</c>).</summary>
        public bool Waits { get; }
        /// <summary>How long the cutscene holds on this step before the next one starts.</summary>
        public float HoldSeconds => Waits ? Seconds : 0f;

        public DialogueLine Line { get; private set; }
        public bool ToBlack { get; private set; }
        public bool BarsOn { get; private set; }
        public float X { get; private set; }
        public float CameraSize { get; private set; }
        /// <summary>The image's Resources path, or null to take the image away.</summary>
        public string Resource { get; private set; }
        public CutsceneActor Actor { get; private set; }
        public CutsceneActorAction Action { get; private set; }
        public CutsceneFacing Facing { get; private set; }
        public CutscenePose Pose { get; private set; }
        public CutsceneAttack Attack { get; private set; }

        public static CutsceneStep ForLine(DialogueLine line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            return new CutsceneStep(CutsceneStepKind.Line, line.SourceLineNumber, 0f, true) { Line = line };
        }

        public static CutsceneStep ForWait(int lineNumber, float seconds)
            => new CutsceneStep(CutsceneStepKind.Wait, lineNumber, seconds, true);

        public static CutsceneStep ForFade(int lineNumber, bool toBlack, float seconds, bool waits)
            => new CutsceneStep(CutsceneStepKind.Fade, lineNumber, seconds, waits) { ToBlack = toBlack };

        public static CutsceneStep ForBars(int lineNumber, bool on, float seconds, bool waits)
            => new CutsceneStep(CutsceneStepKind.Bars, lineNumber, seconds, waits) { BarsOn = on };

        public static CutsceneStep ForCamera(int lineNumber, float x, float size, float seconds, bool waits)
        {
            if (Math.Abs(x) > MaximumX || float.IsNaN(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (size < MinimumCameraSize || size > MaximumCameraSize || float.IsNaN(size))
                throw new ArgumentOutOfRangeException(nameof(size));
            return new CutsceneStep(CutsceneStepKind.Camera, lineNumber, seconds, waits) { X = x, CameraSize = size };
        }

        public static CutsceneStep ForImage(int lineNumber, string resource, float seconds, bool waits)
        {
            if (resource != null && resource.Trim().Length == 0) throw new ArgumentException("An image needs a path.", nameof(resource));
            return new CutsceneStep(CutsceneStepKind.Image, lineNumber, seconds, waits) { Resource = resource?.Trim() };
        }

        public static CutsceneStep ForActor(int lineNumber, CutsceneActor actor, CutsceneActorAction action,
            float x = 0f, CutsceneFacing facing = CutsceneFacing.Unchanged, CutscenePose pose = CutscenePose.Idle,
            CutsceneAttack attack = CutsceneAttack.Slash, float seconds = 0f, bool waits = true)
        {
            if (Math.Abs(x) > MaximumX || float.IsNaN(x)) throw new ArgumentOutOfRangeException(nameof(x));
            if (action == CutsceneActorAction.Attack) seconds = AttackSeconds;
            else if (action != CutsceneActorAction.Move) seconds = 0f;
            return new CutsceneStep(CutsceneStepKind.Actor, lineNumber, seconds, waits)
            {
                Actor = actor, Action = action, X = x, Facing = facing, Pose = pose, Attack = attack,
            };
        }
    }

    /// <summary>A parsed cutscene: its steps in source order.</summary>
    public sealed class CutsceneScript
    {
        private readonly CutsceneStep[] steps;

        public CutsceneScript(string id, IReadOnlyList<CutsceneStep> steps)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A cutscene id is required.", nameof(id));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (steps.Count == 0) throw new ArgumentException("A cutscene needs at least one step.", nameof(steps));
            this.steps = new CutsceneStep[steps.Count];
            for (int index = 0; index < steps.Count; index++)
            {
                this.steps[index] = steps[index] ?? throw new ArgumentException("Cutscene steps cannot be null.", nameof(steps));
                if (this.steps[index].Kind == CutsceneStepKind.Line) LineCount++;
            }
            Id = id.Trim();
        }

        public string Id { get; }
        public IReadOnlyList<CutsceneStep> Steps => steps;
        /// <summary>How many dialogue lines the player advances through.</summary>
        public int LineCount { get; }
    }
}
