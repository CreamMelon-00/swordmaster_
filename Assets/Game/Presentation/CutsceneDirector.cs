using System;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Stages a cutscene on the forest arena while the duel is not ticking: it places and poses the two
    /// figures, moves the camera, and drives the <see cref="CutsceneHud"/> layers and the dialogue box. The order of
    /// steps belongs to <see cref="CutscenePlayback"/>. <see cref="Dispose"/> gives the arena and the screens back
    /// as it found them (the controller then resets the arena).</summary>
    public sealed class CutsceneDirector : ICutsceneStage, IDisposable
    {
        private const float GroundY = -.5f;
        private const float CameraY = -1.5f;
        // Zooming in lowers the camera so the figures' feet stay near the same screen height.
        private const float CameraDropPerSize = .2f;

        private sealed class Figure
        {
            public SpriteRenderer Renderer;
            public bool FacesRightByDefault;
            public bool Visible;
            public bool FacesRight;
            public float X, MoveFrom, MoveTo, MoveElapsed, MoveSeconds;
            public bool Moving;
            public CutscenePose Pose;
            public float PoseClock;
            public bool Attacking;
            public CutsceneAttack Attack;
            public float AttackElapsed;
        }

        private readonly LegacyArenaView arena;
        private readonly CutsceneHud hud;
        private readonly DialogueHud dialogueHud;
        private readonly DialoguePortraitCatalog portraits;
        private readonly Figure elise, other;
        private Tween fade, bars, cameraX, cameraSize, imageAmount;
        private Sprite imageSprite;
        private bool imageLeaving;
        private bool otherIsDummy;
        private bool disposed;

        private struct Tween
        {
            public float From, To, Elapsed, Seconds;

            public static Tween At(float value) => new Tween { From = value, To = value };
            public float Value => Seconds <= 0f ? To : Mathf.Lerp(From, To, Mathf.SmoothStep(0f, 1f, Elapsed / Seconds));
            public bool Done => Elapsed >= Seconds;

            public Tween Toward(float target, float seconds)
                => new Tween { From = Value, To = target, Seconds = Mathf.Max(0f, seconds) };

            public void Advance(float delta) => Elapsed = Mathf.Min(Seconds, Elapsed + delta);
        }

        public CutsceneDirector(CutsceneScript script, LegacyArenaView arena, CutsceneHud hud, DialogueHud dialogueHud,
            DialoguePortraitCatalog portraits)
        {
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.dialogueHud = dialogueHud ?? throw new ArgumentNullException(nameof(dialogueHud));
            this.portraits = portraits;
            Playback = new CutscenePlayback(script, this);
            elise = new Figure { Renderer = arena.PlayerRenderer, FacesRightByDefault = true, FacesRight = true, X = -5f };
            other = new Figure { Renderer = arena.EnemyRenderer, FacesRightByDefault = false, FacesRight = false, X = 5f };
        }

        public CutscenePlayback Playback { get; }
        public bool IsComplete => Playback.IsComplete;

        public bool IsVisible(CutsceneActor actor) => Slot(actor).Visible && (actor == CutsceneActor.Elise ||
            (actor == CutsceneActor.Dummy) == otherIsDummy);

        /// <summary>Clears the stage (both figures off, camera at the duel framing, no image or bars) and runs the
        /// cutscene up to its first hold.</summary>
        public void Start()
        {
            if (disposed || Playback.IsStarted) return;
            fade = bars = imageAmount = Tween.At(0f);
            cameraX = Tween.At(0f);
            cameraSize = Tween.At(CutsceneStep.DefaultCameraSize);
            arena.SetEnemyAppearance(EnemyAppearance.Student);
            elise.Visible = other.Visible = false;
            hud.Show();
            dialogueHud.Hide();
            dialogueHud.SetCinematic(true);
            Playback.Start();
            Apply(0f);
        }

        public bool Advance()
        {
            if (disposed || !Playback.Advance()) return false;
            Apply(0f);
            return true;
        }

        public void Tick(float realDelta)
        {
            if (disposed || !Playback.IsStarted) return;
            float delta = Mathf.Max(0f, realDelta);
            // Running motion first, so steps the playback starts below begin from their first frame.
            fade.Advance(delta);
            bars.Advance(delta);
            cameraX.Advance(delta);
            cameraSize.Advance(delta);
            imageAmount.Advance(delta);
            AdvanceFigure(elise, delta);
            AdvanceFigure(other, delta);
            if (imageLeaving && imageAmount.Done)
            {
                imageLeaving = false;
                imageSprite = null;
            }
            Playback.Tick(delta);
            Apply(delta);
        }

        void ICutsceneStage.Run(CutsceneStep step)
        {
            switch (step.Kind)
            {
                case CutsceneStepKind.Fade:
                    fade = fade.Toward(step.ToBlack ? 1f : 0f, step.Seconds);
                    break;
                case CutsceneStepKind.Bars:
                    bars = bars.Toward(step.BarsOn ? 1f : 0f, step.Seconds);
                    break;
                case CutsceneStepKind.Camera:
                    cameraX = cameraX.Toward(step.X, step.Seconds);
                    cameraSize = cameraSize.Toward(step.CameraSize, step.Seconds);
                    break;
                case CutsceneStepKind.Image:
                    RunImage(step);
                    break;
                case CutsceneStepKind.Actor:
                    RunActor(step);
                    break;
            }
        }

        void ICutsceneStage.ShowLine(DialogueLine line, int lineIndex, int lineCount)
        {
            Sprite left = portraits != null ? portraits.FindPortrait(line.Stage.Left?.SpeakerName) : null;
            Sprite right = portraits != null ? portraits.FindPortrait(line.Stage.Right?.SpeakerName) : null;
            dialogueHud.Show(line, lineIndex, lineCount, left, right);
        }

        void ICutsceneStage.HideLine() => dialogueHud.Hide();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            RestoreFigure(elise);
            RestoreFigure(other);
            imageSprite = null;
            hud.Hide();
            dialogueHud.Hide();
            dialogueHud.SetCinematic(false);
        }

        private void RunImage(CutsceneStep step)
        {
            if (step.Resource == null)
            {
                imageAmount = imageAmount.Toward(0f, step.Seconds);
                imageLeaving = true;
                if (step.Seconds <= 0f)
                {
                    imageLeaving = false;
                    imageSprite = null;
                }
                return;
            }
            Sprite sprite = Resources.Load<Sprite>(step.Resource);
            if (sprite == null)
            {
                Debug.LogWarning($"Cutscene '{Playback.Script.Id}', line {step.SourceLineNumber}: no sprite at Resources/{step.Resource}.");
                return;
            }
            imageLeaving = false;
            // A new picture fades in from nothing; the same one keeps its current opacity.
            if (sprite != imageSprite) imageAmount = Tween.At(0f);
            imageSprite = sprite;
            imageAmount = imageAmount.Toward(1f, step.Seconds);
        }

        private void RunActor(CutsceneStep step)
        {
            Figure figure = Slot(step.Actor);
            switch (step.Action)
            {
                case CutsceneActorAction.Place:
                    if (!figure.Visible)
                    {
                        // A figure stepping on stage starts fresh: idle, facing its usual way. The knight and the dummy
                        // share this figure, so nothing carries over from the one who stood here before.
                        if (figure == other)
                        {
                            otherIsDummy = step.Actor == CutsceneActor.Dummy;
                            arena.SetEnemyAppearance(otherIsDummy ? EnemyAppearance.TrainingDummy : EnemyAppearance.Student);
                        }
                        figure.Pose = CutscenePose.Idle;
                        figure.PoseClock = 0f;
                        figure.Attacking = false;
                        figure.FacesRight = figure.FacesRightByDefault;
                    }
                    figure.Visible = true;
                    figure.X = step.X;
                    figure.Moving = false;
                    if (step.Facing != CutsceneFacing.Unchanged) figure.FacesRight = step.Facing == CutsceneFacing.Right;
                    break;
                case CutsceneActorAction.Hide:
                    figure.Visible = false;
                    figure.Moving = false;
                    figure.Attacking = false;
                    break;
                case CutsceneActorAction.Move:
                    figure.MoveFrom = figure.X;
                    figure.MoveTo = step.X;
                    figure.MoveElapsed = 0f;
                    figure.MoveSeconds = step.Seconds;
                    figure.Moving = step.Seconds > 0f;
                    if (!figure.Moving) figure.X = step.X;
                    break;
                case CutsceneActorAction.Face:
                    figure.FacesRight = step.Facing == CutsceneFacing.Right;
                    break;
                case CutsceneActorAction.Pose:
                    figure.Pose = step.Pose;
                    figure.PoseClock = 0f;
                    figure.Attacking = false;
                    break;
                case CutsceneActorAction.Attack:
                    figure.Attacking = true;
                    figure.Attack = step.Attack;
                    figure.AttackElapsed = 0f;
                    break;
            }
        }

        private Figure Slot(CutsceneActor actor) => actor == CutsceneActor.Elise ? elise : other;

        private void AdvanceFigure(Figure figure, float delta)
        {
            figure.PoseClock += delta;
            if (figure.Moving)
            {
                figure.MoveElapsed = Mathf.Min(figure.MoveSeconds, figure.MoveElapsed + delta);
                figure.X = Mathf.Lerp(figure.MoveFrom, figure.MoveTo, Mathf.SmoothStep(0f, 1f, figure.MoveElapsed / figure.MoveSeconds));
                if (figure.MoveElapsed >= figure.MoveSeconds) figure.Moving = false;
            }
            if (figure.Attacking)
            {
                figure.AttackElapsed += delta;
                if (figure.AttackElapsed >= CutsceneStep.AttackSeconds)
                {
                    figure.Attacking = false;
                    figure.Pose = CutscenePose.Idle;
                    figure.PoseClock = 0f;
                }
            }
            // The dummy's hit reaction plays once and settles back into its sway.
            if (figure == other && otherIsDummy && figure.Pose == CutscenePose.Hurt &&
                figure.PoseClock >= TrainingDummyAnimationSet.HurtDuration)
            {
                figure.Pose = CutscenePose.Idle;
                figure.PoseClock = 0f;
            }
        }

        private void Apply(float delta)
        {
            hud.SetFade(fade.Value);
            hud.SetBars(bars.Value);
            hud.SetImage(imageSprite, imageAmount.Value);
            ApplyFigure(elise);
            ApplyFigure(other);
            Camera camera = arena.ArenaCamera;
            float size = cameraSize.Value;
            camera.orthographicSize = size;
            camera.transform.localPosition = new Vector3(cameraX.Value,
                CameraY - (CutsceneStep.DefaultCameraSize - size) * CameraDropPerSize, -10f);
            camera.transform.localRotation = Quaternion.identity;
            arena.ForestBackdrop.Tick(camera, false, delta);
        }

        private void ApplyFigure(Figure figure)
        {
            SpriteRenderer renderer = figure.Renderer;
            if (renderer.gameObject.activeSelf != figure.Visible) renderer.gameObject.SetActive(figure.Visible);
            if (!figure.Visible) return;
            renderer.transform.localPosition = new Vector3(figure.X, GroundY, 0f);
            renderer.transform.localScale = Vector3.one;
            renderer.color = Color.white;
            renderer.flipX = figure.FacesRight != figure.FacesRightByDefault;
            Sprite sprite = Sample(figure);
            if (sprite != null) renderer.sprite = sprite;
        }

        private Sprite Sample(Figure figure)
        {
            if (figure == elise)
            {
                MobStudentAnimationSet set = arena.PlayerAnimations;
                if (figure.Attacking) return set.GetAttackUpper(Property(figure.Attack), 0, AttackPhase(figure.AttackElapsed));
                return figure.Pose == CutscenePose.Hurt ? set.GetHurtPose() : figure.Pose == CutscenePose.Block
                    ? set.GetBlockPose() : set.GetIdleUpper(figure.PoseClock);
            }
            if (otherIsDummy)
            {
                TrainingDummyAnimationSet dummy = arena.DummyAnimations;
                return figure.Pose == CutscenePose.Hurt ? dummy.GetHurt(figure.PoseClock) : dummy.GetIdle(figure.PoseClock);
            }
            EnemyStudentAnimationSet knight = arena.EnemyAnimations;
            if (figure.Attacking) return knight.GetAttack(Property(figure.Attack), AttackPhase(figure.AttackElapsed));
            return figure.Pose == CutscenePose.Hurt ? knight.GetHurt() : figure.Pose == CutscenePose.Block
                ? knight.GetGuard() : knight.GetIdle(figure.PoseClock);
        }

        // The animation sets take the duel's half-cycle phase (the blade connects at 0.5); undo that mapping so the
        // stroke plays at its authored speed and connects at AttackImpactSeconds.
        private static float AttackPhase(float elapsed)
        {
            const float impact = CutsceneStep.AttackImpactSeconds, total = CutsceneStep.AttackSeconds;
            return elapsed <= impact ? .5f * elapsed / impact : .5f + .5f * Mathf.Clamp01((elapsed - impact) / (total - impact));
        }

        private static LegacySkillProperty Property(CutsceneAttack attack)
            => attack == CutsceneAttack.Pierce ? LegacySkillProperty.Penetrate
                : attack == CutsceneAttack.Blunt ? LegacySkillProperty.Hit : LegacySkillProperty.Slash;

        private static void RestoreFigure(Figure figure)
        {
            SpriteRenderer renderer = figure.Renderer;
            if (renderer == null) return;
            renderer.flipX = false;
            renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one;
            if (!renderer.gameObject.activeSelf) renderer.gameObject.SetActive(true);
        }
    }
}
