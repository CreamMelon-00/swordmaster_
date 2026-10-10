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
        /// <summary>The world turns black and white (<see cref="CutsceneStep.FlashbackOn"/>) or back to colour.</summary>
        Flashback,
        /// <summary>The camera shakes by <see cref="CutsceneStep.Strength"/>, dying away over the step's seconds.</summary>
        Shake,
        /// <summary>A sound played once (<see cref="CutsceneStep.Resource"/>) at <see cref="CutsceneStep.Volume"/>.</summary>
        Sound,
        /// <summary>A looping sound fading in (<see cref="CutsceneStep.Resource"/>), or out when the resource is null.</summary>
        Ambience,
        /// <summary>Power gathering on an actor for the step's seconds, or cut off (<see cref="CutsceneStep.ChargeStops"/>).</summary>
        Charge,
        /// <summary>A lasting aura around an actor, fading in or out (<see cref="CutsceneStep.AuraOn"/>).</summary>
        Aura,
        /// <summary>A remembered tutorial screen flashes up over the scene for the step's seconds and fades away
        /// (<see cref="Prologue.TutorialRecall"/>).</summary>
        Recall,
        Mark,
        Jump,
        Choice,
        IfFull,
        EndIf,
    }

    /// <summary>Who stands in the scene. Elisa is the player's figure; the knight and the dummy share the other one,
    /// so only one of them can be on stage at a time. The senior knight (상급기사) has a third figure of his own.</summary>
    public enum CutsceneActor
    {
        Elisa,
        Knight,
        Dummy,
        /// <summary>상급기사. Until he has art of his own he wears the MobStudent set: idle, sliding moves and strokes,
        /// no hurt or guard pose.</summary>
        Senior,
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
        /// <summary>Shivers on the spot for the step's seconds (a small, fast side-to-side jitter of
        /// <see cref="CutsceneStep.Strength"/>), then stands exactly where it was.</summary>
        Tremble,
        /// <summary>A short wavering echo of the senior knight's current upper and lower cels.</summary>
        Shimmer,
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
        /// <summary>The hit reaction: held for Elisa and the knight, played once for the dummy.</summary>
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
        /// <summary>How long a shake lasts when the script gives no seconds.</summary>
        public const float DefaultShakeSeconds = .5f;
        /// <summary>The strongest shake: the camera's largest offset in world units at the duel framing (size 6).</summary>
        public const float MaximumShake = 1.5f;
        /// <summary>How far a tremble takes the figure to either side when the script gives no strength: a slight shiver
        /// (world units; a figure is about 3.5 tall).</summary>
        public const float DefaultTrembleStrength = .06f;
        /// <summary>The strongest tremble, in world units to either side.</summary>
        public const float MaximumTremble = .5f;
        /// <summary>How long a tutorial recall shows when the script gives no seconds.</summary>
        public const float DefaultRecallSeconds = 1.5f;
        /// <summary>Where <c>@sound</c> and <c>@ambience</c> find their clips under Resources.</summary>
        public const string SoundFolder = "Sfx/";

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
        /// <summary>The image's or sound's Resources path, or null to take the image away or end the ambience.</summary>
        public string Resource { get; private set; }
        public CutsceneActor Actor { get; private set; }
        public CutsceneActorAction Action { get; private set; }
        public CutsceneFacing Facing { get; private set; }
        public CutscenePose Pose { get; private set; }
        public CutsceneAttack Attack { get; private set; }
        public bool FlashbackOn { get; private set; }
        /// <summary>A shake's largest camera offset (world units at the duel framing), or a tremble's largest sideways
        /// offset of its figure (world units).</summary>
        public float Strength { get; private set; }
        /// <summary>A sound's volume, 0 to 1.</summary>
        public float Volume { get; private set; }
        /// <summary>A charge that is cut off at once instead of started.</summary>
        public bool ChargeStops { get; private set; }
        /// <summary>A charge that reaches full glow in its seconds and then holds there until <c>@charge … stop</c>
        /// (it never flares on its own).</summary>
        public bool ChargeHolds { get; private set; }
        public bool AuraOn { get; private set; }
        public string Label { get; private set; }
        public string ChoiceA { get; private set; }
        public string ChoiceB { get; private set; }
        public string TargetA { get; private set; }
        public string TargetB { get; private set; }

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
            if (action == CutsceneActorAction.Tremble || action == CutsceneActorAction.Shimmer)
                throw new ArgumentException("Use ForTremble or ForShimmer to create a timed actor effect.", nameof(action));
            if (action == CutsceneActorAction.Attack) seconds = AttackSeconds;
            else if (action != CutsceneActorAction.Move) seconds = 0f;
            return new CutsceneStep(CutsceneStepKind.Actor, lineNumber, seconds, waits)
            {
                Actor = actor, Action = action, X = x, Facing = facing, Pose = pose, Attack = attack,
            };
        }

        /// <summary>The figure shivers in place for <paramref name="seconds"/>, at most <paramref name="strength"/> to
        /// either side, and ends exactly where it stood.</summary>
        public static CutsceneStep ForTremble(int lineNumber, CutsceneActor actor, float seconds,
            float strength = DefaultTrembleStrength, bool waits = true)
        {
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (strength <= 0f || strength > MaximumTremble || float.IsNaN(strength)) throw new ArgumentOutOfRangeException(nameof(strength));
            return new CutsceneStep(CutsceneStepKind.Actor, lineNumber, seconds, waits)
            {
                Actor = actor, Action = CutsceneActorAction.Tremble, Strength = strength,
            };
        }

        public static CutsceneStep ForShimmer(int lineNumber, float seconds, bool waits = true)
        {
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            return new CutsceneStep(CutsceneStepKind.Actor, lineNumber, seconds, waits)
            {
                Actor = CutsceneActor.Senior, Action = CutsceneActorAction.Shimmer,
            };
        }

        public static CutsceneStep ForFlashback(int lineNumber, bool on, float seconds, bool waits)
            => new CutsceneStep(CutsceneStepKind.Flashback, lineNumber, seconds, waits) { FlashbackOn = on };

        public static CutsceneStep ForShake(int lineNumber, float strength, float seconds, bool waits)
        {
            if (strength <= 0f || strength > MaximumShake || float.IsNaN(strength)) throw new ArgumentOutOfRangeException(nameof(strength));
            return new CutsceneStep(CutsceneStepKind.Shake, lineNumber, seconds, waits) { Strength = strength };
        }

        /// <summary>A sound never holds the cutscene: it starts and the next step runs at once.</summary>
        public static CutsceneStep ForSound(int lineNumber, string name, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A sound needs a name.", nameof(name));
            if (volume < 0f || volume > 1f || float.IsNaN(volume)) throw new ArgumentOutOfRangeException(nameof(volume));
            return new CutsceneStep(CutsceneStepKind.Sound, lineNumber, 0f, true) { Resource = SoundFolder + name.Trim(), Volume = volume };
        }

        /// <param name="name">The looping sound to fade in, or null to fade the current one out.</param>
        public static CutsceneStep ForAmbience(int lineNumber, string name, float seconds, bool waits)
        {
            if (name != null && name.Trim().Length == 0) throw new ArgumentException("An ambience needs a name.", nameof(name));
            return new CutsceneStep(CutsceneStepKind.Ambience, lineNumber, seconds, waits)
            {
                Resource = name == null ? null : SoundFolder + name.Trim(), Volume = 1f,
            };
        }

        /// <param name="hold">Hold at full glow after <paramref name="seconds"/> until a stop (<see cref="ChargeHolds"/>).
        /// A waiting step still holds the cutscene only for <paramref name="seconds"/>.</param>
        public static CutsceneStep ForCharge(int lineNumber, CutsceneActor actor, float seconds, bool waits, bool hold = false)
        {
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            return new CutsceneStep(CutsceneStepKind.Charge, lineNumber, seconds, waits) { Actor = actor, ChargeHolds = hold };
        }

        public static CutsceneStep ForChargeStop(int lineNumber, CutsceneActor actor)
            => new CutsceneStep(CutsceneStepKind.Charge, lineNumber, 0f, true) { Actor = actor, ChargeStops = true };

        public static CutsceneStep ForAura(int lineNumber, CutsceneActor actor, bool on, float seconds, bool waits)
            => new CutsceneStep(CutsceneStepKind.Aura, lineNumber, seconds, waits) { Actor = actor, AuraOn = on };

        /// <summary>A tutorial screen the player was shown flashes up for <paramref name="seconds"/> (a white flash in, a
        /// fade out); which one is Presentation's choice.</summary>
        public static CutsceneStep ForRecall(int lineNumber, float seconds = DefaultRecallSeconds, bool waits = true)
        {
            if (seconds <= 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            return new CutsceneStep(CutsceneStepKind.Recall, lineNumber, seconds, waits);
        }

        public static CutsceneStep ForMark(int lineNumber, string label)
            => new CutsceneStep(CutsceneStepKind.Mark, lineNumber, 0f, true) { Label = label };

        public static CutsceneStep ForJump(int lineNumber, string label)
            => new CutsceneStep(CutsceneStepKind.Jump, lineNumber, 0f, true) { Label = label };

        public static CutsceneStep ForChoice(int lineNumber, string choiceA, string targetA, string choiceB, string targetB)
            => new CutsceneStep(CutsceneStepKind.Choice, lineNumber, 0f, true)
            {
                ChoiceA = choiceA, TargetA = targetA, ChoiceB = choiceB, TargetB = targetB,
            };

        public static CutsceneStep ForIfFull(int lineNumber)
            => new CutsceneStep(CutsceneStepKind.IfFull, lineNumber, 0f, true);

        public static CutsceneStep ForEndIf(int lineNumber)
            => new CutsceneStep(CutsceneStepKind.EndIf, lineNumber, 0f, true);
    }

    /// <summary>A parsed cutscene: its steps in source order, and who already stands on stage when it starts.</summary>
    public sealed class CutsceneScript
    {
        private static readonly CutsceneActor[] NoActors = new CutsceneActor[0];
        private readonly CutsceneStep[] steps;
        private readonly CutsceneActor[] onStage;
        private readonly Dictionary<string, int> marks = new Dictionary<string, int>(StringComparer.Ordinal);

        public CutsceneScript(string id, IReadOnlyList<CutsceneStep> steps) : this(id, steps, null) { }

        /// <param name="onStage">The figures standing where the scene finds them (a mission's scenes start on its
        /// battlefield); null or empty for a scene that starts on a bare stage.</param>
        public CutsceneScript(string id, IReadOnlyList<CutsceneStep> steps, IReadOnlyList<CutsceneActor> onStage)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A cutscene id is required.", nameof(id));
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            if (steps.Count == 0) throw new ArgumentException("A cutscene needs at least one step.", nameof(steps));
            this.steps = new CutsceneStep[steps.Count];
            for (int index = 0; index < steps.Count; index++)
            {
                this.steps[index] = steps[index] ?? throw new ArgumentException("Cutscene steps cannot be null.", nameof(steps));
                if (this.steps[index].Kind == CutsceneStepKind.Line) LineCount++;
                if (this.steps[index].Kind == CutsceneStepKind.Mark)
                {
                    if (marks.ContainsKey(this.steps[index].Label))
                        throw new ArgumentException($"Duplicate cutscene mark '{this.steps[index].Label}'.", nameof(steps));
                    marks.Add(this.steps[index].Label, index);
                }
            }
            this.onStage = CheckCast(onStage);
            Id = id.Trim();
        }

        public string Id { get; }
        public IReadOnlyList<CutsceneStep> Steps => steps;
        /// <summary>How many dialogue lines the player advances through.</summary>
        public int LineCount { get; }
        /// <summary>The figures already on stage when the scene starts, where the arena has them; empty for a bare stage.</summary>
        public IReadOnlyList<CutsceneActor> OnStage => onStage;
        public bool StartsOnStage(CutsceneActor actor) => Array.IndexOf(onStage, actor) >= 0;
        public bool TryFindMark(string label, out int index) => marks.TryGetValue(label, out index);

        /// <summary>A copy of a starting cast; throws for a repeated actor, or the knight and the dummy together (they
        /// share one figure).</summary>
        internal static CutsceneActor[] CheckCast(IReadOnlyList<CutsceneActor> cast)
        {
            if (cast == null || cast.Count == 0) return NoActors;
            var copy = new CutsceneActor[cast.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                if (!Enum.IsDefined(typeof(CutsceneActor), cast[index]))
                    throw new ArgumentOutOfRangeException(nameof(cast));
                if (Array.IndexOf(copy, cast[index], 0, index) >= 0)
                    throw new ArgumentException("An actor can only be on stage once.", nameof(cast));
                copy[index] = cast[index];
            }
            if (Array.IndexOf(copy, CutsceneActor.Knight) >= 0 && Array.IndexOf(copy, CutsceneActor.Dummy) >= 0)
                throw new ArgumentException("The knight and the dummy share one figure.", nameof(cast));
            return copy;
        }
    }
}
