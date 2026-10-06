using System;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Stages a cutscene on the forest arena while the duel is not ticking: it places and poses the figures
    /// (the two fighters, and the senior knight who only exists in cutscenes), moves and shakes the camera, drains
    /// the colour for flashbacks, runs the figures' power effects and the scene's sounds, and drives the
    /// <see cref="CutsceneHud"/> layers and the dialogue box. The order of steps belongs to <see cref="CutscenePlayback"/>.
    /// <see cref="Dispose"/> gives the arena and the screens back as it found them (the controller then resets the
    /// arena), stops every sound and charge, and removes the senior knight. Only the fighters' power auras stay as the
    /// scene left them, so an aura lit in a battle's event scene carries on into the fight; arena reset clears them.
    /// A script may start on a bare stage (the opening) or with figures already standing (<see cref="CutsceneScript.OnStage"/>,
    /// a mission's scenes): those keep the arena's places, facing and camera framing until the script moves them. A scene
    /// that plays in the middle of a battle (<see cref="ResumesBattle"/>) suspends the arena for its length and leaves it
    /// exactly as the battle paused it.</summary>
    public sealed class CutsceneDirector : ICutsceneStage, IDisposable
    {
        private const float GroundY = -.5f;
        private const float CameraY = -1.5f;
        // Zooming in lowers the camera so the figures' feet stay near the same screen height.
        private const float CameraDropPerSize = .2f;
        // How fast the shake wanders (noise cycles per second).
        private const float ShakeFrequency = 22f;
        // A battle's tilted close-up levels out this fast when a scene starts in its middle; the zoom stays.
        private const float LevelSeconds = .3f;

        private sealed class Figure
        {
            public SpriteRenderer Renderer;
            /// <summary>The legs of a layered figure (the senior knight), or null.</summary>
            public SpriteRenderer Lower;
            public DuelPowerAura Aura;
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
        private readonly Figure elisa, other, senior;
        // The senior knight's own objects; created only for a script that uses him.
        private readonly GameObject seniorRoot;
        private readonly SeniorKnightAnimationSet seniorArt;
        private Tween fade, bars, cameraX, cameraSize, imageAmount, flashback;
        // A staged start's camera height and roll away from the cutscene framing, kept until the first @camera.
        private Tween cameraLift, cameraTilt;
        private float shakeStrength, shakeElapsed, shakeSeconds;
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

        /// <param name="resumesBattle">The scene plays in the middle of a battle (it must start on stage): the arena is
        /// suspended at <see cref="Start"/> and given back as the battle left it at <see cref="Dispose"/>.</param>
        public CutsceneDirector(CutsceneScript script, LegacyArenaView arena, CutsceneHud hud, DialogueHud dialogueHud,
            DialoguePortraitCatalog portraits, bool resumesBattle = false)
        {
            if (resumesBattle && script != null && script.OnStage.Count == 0)
                throw new ArgumentException("A scene in the middle of a battle starts with the fighters on stage.", nameof(script));
            ResumesBattle = resumesBattle;
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.dialogueHud = dialogueHud ?? throw new ArgumentNullException(nameof(dialogueHud));
            this.portraits = portraits;
            Playback = new CutscenePlayback(script, this);
            elisa = new Figure
            {
                Renderer = arena.PlayerRenderer, Aura = arena.PlayerPowerAura, FacesRightByDefault = true, FacesRight = true, X = -5f,
            };
            other = new Figure
            {
                Renderer = arena.EnemyRenderer, Aura = arena.EnemyPowerAura, FacesRightByDefault = false, FacesRight = false, X = 5f,
            };
            if (UsesSenior(script)) senior = CreateSenior(out seniorRoot, out seniorArt);
            Audio = new CutsceneAudio(arena.Root);
        }

        public CutscenePlayback Playback { get; }
        public bool IsComplete => Playback.IsComplete;
        /// <summary>Whether the scene plays in the middle of a battle and gives the arena back as the battle left it.</summary>
        public bool ResumesBattle { get; }
        /// <summary>The scene's sounds (<c>@sound</c>, <c>@ambience</c>).</summary>
        public CutsceneAudio Audio { get; }
        /// <summary>The senior knight's body, or null when the script never uses him.</summary>
        public SpriteRenderer SeniorRenderer => senior?.Renderer;
        public SpriteRenderer SeniorLowerRenderer => senior?.Lower;
        public DuelPowerAura SeniorPowerAura => senior?.Aura;
        /// <summary>Whether a <c>@shake</c> is still moving the camera.</summary>
        public bool IsShaking => shakeElapsed < shakeSeconds;

        public bool IsVisible(CutsceneActor actor)
        {
            Figure figure = Slot(actor);
            return figure != null && figure.Visible && (figure != other || (actor == CutsceneActor.Dummy) == otherIsDummy);
        }

        /// <summary>Sets the stage and runs the cutscene up to its first hold. A bare stage has every figure off and the
        /// camera at the duel framing; a script that starts on stage keeps its figures where the arena has them (idle,
        /// facing as they do) and the camera where it is. Either way: in colour, no image or bars.</summary>
        public void Start()
        {
            if (disposed || Playback.IsStarted) return;
            fade = bars = imageAmount = flashback = Tween.At(0f);
            shakeStrength = shakeElapsed = shakeSeconds = 0f;
            if (senior != null) senior.Visible = false;
            if (Playback.Script.OnStage.Count > 0) StartOnStage();
            else
            {
                cameraX = cameraLift = cameraTilt = Tween.At(0f);
                cameraSize = Tween.At(CutsceneStep.DefaultCameraSize);
                arena.SetEnemyAppearance(EnemyAppearance.Student);
                elisa.Visible = other.Visible = false;
            }
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
            cameraLift.Advance(delta);
            cameraTilt.Advance(delta);
            imageAmount.Advance(delta);
            flashback.Advance(delta);
            shakeElapsed = Mathf.Min(shakeSeconds, shakeElapsed + delta);
            AdvanceFigure(elisa, delta);
            AdvanceFigure(other, delta);
            if (senior != null) AdvanceFigure(senior, delta);
            Audio.Tick(delta);
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
                    // The script takes the camera over: a staged start's battle framing eases into the cutscene's.
                    cameraLift = cameraLift.Toward(0f, step.Seconds);
                    cameraTilt = cameraTilt.Toward(0f, step.Seconds);
                    break;
                case CutsceneStepKind.Image:
                    RunImage(step);
                    break;
                case CutsceneStepKind.Actor:
                    RunActor(step);
                    break;
                case CutsceneStepKind.Flashback:
                    flashback = flashback.Toward(step.FlashbackOn ? 1f : 0f, step.Seconds);
                    break;
                case CutsceneStepKind.Shake:
                    // A new shake replaces the one in progress.
                    shakeStrength = step.Strength;
                    shakeSeconds = step.Seconds;
                    shakeElapsed = 0f;
                    break;
                case CutsceneStepKind.Sound:
                    if (!Audio.PlaySound(step.Resource, step.Volume)) WarnMissing(step, "sound");
                    break;
                case CutsceneStepKind.Ambience:
                    if (step.Resource == null) Audio.StopAmbience(step.Seconds);
                    else if (!Audio.StartAmbience(step.Resource, step.Seconds)) WarnMissing(step, "sound");
                    break;
                case CutsceneStepKind.Charge:
                {
                    DuelPowerAura aura = Slot(step.Actor)?.Aura;
                    if (step.ChargeStops) aura?.StopCharge();
                    else aura?.Charge(step.Seconds, step.ChargeHolds);
                    break;
                }
                case CutsceneStepKind.Aura:
                    Slot(step.Actor)?.Aura?.SetAura(step.AuraOn, step.Seconds);
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
            RestoreFigure(elisa);
            RestoreFigure(other);
            elisa.Aura?.StopCharge();
            other.Aura?.StopCharge();
            arena.SetFlashback(0f);
            Audio.Dispose();
            if (senior != null)
            {
                senior.Aura.Dispose();
                seniorArt.Dispose();
                if (Application.isPlaying) Object.Destroy(seniorRoot);
                else Object.DestroyImmediate(seniorRoot);
            }
            imageSprite = null;
            hud.Hide();
            dialogueHud.Hide();
            dialogueHud.SetCinematic(false);
            if (ResumesBattle) arena.ResumeAfterCutscene();
        }

        /// <summary>The script's starting cast stays where the arena has it: each figure at its place and facing, idle; the
        /// knight or the dummy as the cast says. The camera keeps its framing (a battle's tilt levels out). A scene in the
        /// middle of a battle first suspends the arena, so the battle's picture can come back when it ends.</summary>
        private void StartOnStage()
        {
            if (ResumesBattle) arena.SuspendForCutscene();
            CutsceneScript script = Playback.Script;
            elisa.Visible = script.StartsOnStage(CutsceneActor.Elisa);
            bool dummy = script.StartsOnStage(CutsceneActor.Dummy);
            other.Visible = dummy || script.StartsOnStage(CutsceneActor.Knight);
            if (other.Visible)
            {
                otherIsDummy = dummy;
                EnemyAppearance appearance = dummy ? EnemyAppearance.TrainingDummy : EnemyAppearance.Student;
                if (arena.EnemyAppearance != appearance) arena.SetEnemyAppearance(appearance);
            }
            foreach (Figure figure in new[] { elisa, other })
            {
                if (!figure.Visible) continue;
                figure.X = figure.Renderer.transform.localPosition.x;
                figure.FacesRight = figure.FacesRightByDefault != figure.Renderer.flipX;
                figure.Pose = CutscenePose.Idle;
                figure.PoseClock = 0f;
                figure.Moving = figure.Attacking = false;
            }
            Transform camera = arena.ArenaCamera.transform;
            float size = arena.ArenaCamera.orthographicSize;
            cameraX = Tween.At(camera.localPosition.x);
            cameraSize = Tween.At(size);
            cameraLift = Tween.At(camera.localPosition.y - CameraHeight(size));
            cameraTilt = Tween.At(Mathf.DeltaAngle(0f, camera.localEulerAngles.z)).Toward(0f, LevelSeconds);
        }

        // The cutscene framing's camera height for a zoom: lower when closer, so the feet stay near the same screen height.
        private static float CameraHeight(float size) => CameraY - (CutsceneStep.DefaultCameraSize - size) * CameraDropPerSize;

        private static bool UsesSenior(CutsceneScript script)
        {
            if (script == null) return false;
            foreach (CutsceneStep step in script.Steps)
                if (step.Actor == CutsceneActor.Senior && (step.Kind == CutsceneStepKind.Actor ||
                    step.Kind == CutsceneStepKind.Charge || step.Kind == CutsceneStepKind.Aura)) return true;
            return false;
        }

        /// <summary>The senior knight: a layered figure (body over legs) with a ground shadow like the fighters', under
        /// the arena on its layer, hidden until the script places him.</summary>
        private Figure CreateSenior(out GameObject root, out SeniorKnightAnimationSet art)
        {
            art = new SeniorKnightAnimationSet();
            if (!art.HasRequiredAssets)
                Debug.LogWarning("Senior knight (MobStudent) art is incomplete: " + string.Join(", ", art.MissingResources));
            root = new GameObject("Cutscene Senior Knight") { layer = LegacyArenaView.ArenaLayer };
            root.transform.SetParent(arena.Root, false);
            SpriteRenderer upper = root.AddComponent<SpriteRenderer>();
            SpriteRenderer lower = Part("Senior Knight Lower Body", root.transform, -1);
            if (arena.SpriteMaterial != null) upper.sharedMaterial = arena.SpriteMaterial;
            Sprite[] shadows = Resources.LoadAll<Sprite>("LegacyArena/Shadow/Circle");
            SpriteRenderer shadow = Part("Senior Knight Ground Shadow", root.transform, -2);
            shadow.sprite = shadows.Length > 0 ? shadows[0] : null;
            shadow.transform.localPosition = new Vector3(0f, DuelPowerAura.FeetY, 0f);
            shadow.transform.localScale = new Vector3(3f, .7f, 1f);
            shadow.color = new Color(0f, 0f, 0f, .6117647f);
            root.SetActive(false);
            return new Figure
            {
                Renderer = upper, Lower = lower, FacesRightByDefault = true, FacesRight = true, X = -8f,
                Aura = new DuelPowerAura(root.transform, arena.SpriteMaterial, LegacyArenaView.ArenaLayer, 37),
            };
        }

        private SpriteRenderer Part(string name, Transform parent, int sortingOrder)
        {
            var node = new GameObject(name) { layer = LegacyArenaView.ArenaLayer };
            node.transform.SetParent(parent, false);
            var renderer = node.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (arena.SpriteMaterial != null) renderer.sharedMaterial = arena.SpriteMaterial;
            return renderer;
        }

        private void WarnMissing(CutsceneStep step, string what)
            => Debug.LogWarning($"Cutscene '{Playback.Script.Id}', line {step.SourceLineNumber}: no {what} at Resources/{step.Resource}.");

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
                WarnMissing(step, "sprite");
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
            if (figure == null) return;
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
                    // Leaving the stage takes the figure's power with it.
                    figure.Aura?.StopCharge();
                    figure.Aura?.SetAura(false);
                    break;
                case CutsceneActorAction.Move:
                    figure.MoveFrom = figure.X;
                    figure.MoveTo = step.X;
                    figure.MoveElapsed = 0f;
                    figure.MoveSeconds = step.Seconds;
                    figure.Moving = step.Seconds > 0f && !Mathf.Approximately(figure.MoveFrom, figure.MoveTo);
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

        private Figure Slot(CutsceneActor actor)
            => actor == CutsceneActor.Elisa ? elisa : actor == CutsceneActor.Senior ? senior : other;

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
            // Real time in a cutscene; the battle ticks the fighters' auras on its own clock.
            figure.Aura?.Tick(delta);
        }

        private void Apply(float delta)
        {
            hud.SetFade(fade.Value);
            hud.SetBars(bars.Value);
            hud.SetImage(imageSprite, imageAmount.Value);
            arena.SetFlashback(flashback.Value);
            ApplyFigure(elisa);
            ApplyFigure(other);
            if (senior != null) ApplyFigure(senior);
            Camera camera = arena.ArenaCamera;
            float size = cameraSize.Value;
            Vector2 shake = ShakeOffset(size);
            camera.orthographicSize = size;
            camera.transform.localPosition = new Vector3(cameraX.Value + shake.x,
                CameraHeight(size) + cameraLift.Value + shake.y, -10f);
            camera.transform.localRotation = Quaternion.Euler(0f, 0f, cameraTilt.Value);
            arena.ForestBackdrop.Tick(camera, false, delta);
        }

        /// <summary>The shake's camera offset now: noise that dies away (quadratically) over its seconds, scaled with
        /// the zoom so a strength looks the same on screen at any camera size.</summary>
        private Vector2 ShakeOffset(float size)
        {
            if (shakeSeconds <= 0f || shakeElapsed >= shakeSeconds) return Vector2.zero;
            float left = 1f - shakeElapsed / shakeSeconds;
            float amplitude = shakeStrength * left * left * size / CutsceneStep.DefaultCameraSize;
            float time = shakeElapsed * ShakeFrequency;
            return new Vector2(Mathf.PerlinNoise(time, .37f) * 2f - 1f, Mathf.PerlinNoise(.71f, time) * 2f - 1f) * amplitude;
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
            if (figure == senior)
            {
                ApplySenior(figure);
                return;
            }
            Sprite sprite = Sample(figure);
            if (sprite != null) renderer.sprite = sprite;
        }

        // Body over legs: a stroke or the breathing loop on top, the walking cycle below while he travels.
        private void ApplySenior(Figure figure)
        {
            Sprite upper = figure.Attacking
                ? seniorArt.GetAttackUpper(Property(figure.Attack), AttackPhase(figure.AttackElapsed))
                : seniorArt.GetIdleUpper(figure.PoseClock);
            if (upper != null) figure.Renderer.sprite = upper;
            figure.Lower.sprite = seniorArt.GetLower(figure.PoseClock, figure.Moving);
            figure.Lower.flipX = figure.Renderer.flipX;
        }

        private Sprite Sample(Figure figure)
        {
            if (figure == elisa)
            {
                MobStudentAnimationSet set = arena.PlayerAnimations;
                if (figure.Attacking) return set.GetAttackUpper(Property(figure.Attack), 0, AttackPhase(figure.AttackElapsed));
                return figure.Pose == CutscenePose.Hurt ? set.GetHurtPose() : figure.Pose == CutscenePose.Block
                    ? set.GetBlockPose() : figure.Moving ? set.GetMove() : set.GetIdleUpper(figure.PoseClock);
            }
            if (otherIsDummy)
            {
                TrainingDummyAnimationSet dummy = arena.DummyAnimations;
                return figure.Pose == CutscenePose.Hurt ? dummy.GetHurt(figure.PoseClock) : dummy.GetIdle(figure.PoseClock);
            }
            EnemyStudentAnimationSet knight = arena.EnemyAnimations;
            if (figure.Attacking) return knight.GetAttack(Property(figure.Attack), AttackPhase(figure.AttackElapsed));
            return figure.Pose == CutscenePose.Hurt ? knight.GetHurt() : figure.Pose == CutscenePose.Block
                ? knight.GetGuard() : figure.Moving ? knight.GetMove() : knight.GetIdle(figure.PoseClock);
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
