using System;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.Dialogue;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Stages a cutscene on the forest arena while the duel is not ticking: it places, poses and trembles the
    /// figures (the two fighters, and the senior knight who only exists in cutscenes), moves and shakes the camera,
    /// drains the colour for flashbacks, runs the figures' power effects and the scene's sounds, flashes up remembered
    /// tutorial screens (<see cref="TutorialRecallAlbum"/>), and drives the <see cref="CutsceneHud"/> layers and the
    /// dialogue box. The order of steps belongs to <see cref="CutscenePlayback"/>.
    /// <see cref="Dispose"/> gives the arena and the screens back as it found them (the controller then resets the
    /// arena), stops every sound, charge and tremble, and removes the senior knight. Only the fighters' power auras stay
    /// as the scene left them, so an aura lit in a battle's event scene carries on into the fight; arena reset clears them.
    /// The knight's aura brings her 수훈 afterimages (<see cref="DuelAuraAfterimages"/>): ticked with it on the scene's real
    /// time, they trail her for as long as it is up.
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
        // A tremble shivers at about this rate (cycles per second), easing in and out over the first and last moments.
        private const float TrembleFrequency = 14f;
        private const float TrembleEaseSeconds = .08f;
        // A recall: the white flash dies away over the first moment while the memory comes up under it; the last part
        // fades it out. Short recalls shrink both to a share of their length.
        private const float RecallFlashSeconds = .25f;
        private const float RecallRiseSeconds = .06f;
        private const float RecallFadeSeconds = .5f;

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
            // A shiver around X, which it never changes.
            public bool Trembling;
            public float TrembleElapsed, TrembleSeconds, TrembleStrength;
        }

        private readonly LegacyArenaView arena;
        private readonly CutsceneHud hud;
        private readonly DialogueHud dialogueHud;
        private readonly DialoguePortraitCatalog portraits;
        private readonly TutorialRecallAlbum recall;
        private readonly Figure elisa, other, senior;
        // The senior knight's own objects; created only for a script that uses him.
        private readonly GameObject seniorRoot;
        private readonly SeniorKnightAnimationSet seniorArt;
        private Tween fade, bars, cameraX, cameraSize, imageAmount, flashback;
        // A staged start's camera height and roll away from the cutscene framing, kept until the first @camera.
        private Tween cameraLift, cameraTilt;
        private float shakeStrength, shakeElapsed, shakeSeconds;
        // The recall on screen, if any (seconds 0 = none).
        private float recallElapsed, recallSeconds;
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
        /// <param name="recall">What <c>@recall</c> brings back (the controller's album, which stays its owner); without
        /// one a recall only warns.</param>
        public CutsceneDirector(CutsceneScript script, LegacyArenaView arena, CutsceneHud hud, DialogueHud dialogueHud,
            DialoguePortraitCatalog portraits, bool resumesBattle = false, TutorialRecallAlbum recall = null)
        {
            if (resumesBattle && script != null && script.OnStage.Count == 0)
                throw new ArgumentException("A scene in the middle of a battle starts with the fighters on stage.", nameof(script));
            ResumesBattle = resumesBattle;
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.dialogueHud = dialogueHud ?? throw new ArgumentNullException(nameof(dialogueHud));
            this.portraits = portraits;
            this.recall = recall;
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
        /// <summary>Whether a <c>@recall</c> is still on screen.</summary>
        public bool IsRecalling => recallSeconds > 0f;
        /// <summary>Set before <see cref="Start"/>: the scene opens black and white and its colour comes back over this many
        /// seconds, as if after <c>@flashback on 0</c> and <c>@flashback off</c> (the 서막's final fall hands its outro over
        /// in grey). 0, the default, opens in colour. A <c>@flashback</c> in the script takes over from wherever it is.</summary>
        public float OpensFromGreySeconds { get; set; }

        public bool IsVisible(CutsceneActor actor)
        {
            Figure figure = Slot(actor);
            return figure != null && figure.Visible && (figure != other || (actor == CutsceneActor.Dummy) == otherIsDummy);
        }

        /// <summary>Whether the actor is shivering (<c>@actor … tremble</c>) right now.</summary>
        public bool IsTrembling(CutsceneActor actor)
        {
            Figure figure = Slot(actor);
            return figure != null && figure.Trembling && IsVisible(actor);
        }

        /// <summary>Sets the stage and runs the cutscene up to its first hold. A bare stage has every figure off and the
        /// camera at the duel framing; a script that starts on stage keeps its figures where the arena has them (idle,
        /// facing as they do) and the camera where it is. Either way: in colour (or regaining it, see
        /// <see cref="OpensFromGreySeconds"/>), no image or bars.</summary>
        public void Start()
        {
            if (disposed || Playback.IsStarted) return;
            fade = bars = imageAmount = Tween.At(0f);
            flashback = OpensFromGreySeconds > 0f ? Tween.At(1f).Toward(0f, OpensFromGreySeconds) : Tween.At(0f);
            shakeStrength = shakeElapsed = shakeSeconds = 0f;
            recallElapsed = recallSeconds = 0f;
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
            if (recallSeconds > 0f)
            {
                recallElapsed = Mathf.Min(recallSeconds, recallElapsed + delta);
                if (recallElapsed >= recallSeconds) EndRecall();
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
                case CutsceneStepKind.Recall:
                    RunRecall(step);
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
            recallSeconds = 0f;
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
                figure.Moving = figure.Attacking = figure.Trembling = false;
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

        /// <summary>A remembered tutorial screen: one the album captured, or else a coached beat redrawn as a card, both
        /// chosen at random by the album. A new recall replaces the one up. With nothing to recall the step still holds
        /// its time (like a missing image), with a warning.</summary>
        private void RunRecall(CutsceneStep step)
        {
            Texture2D screen = null;
            TutorialRecallBeat beat = null;
            if (recall == null || !recall.TryPick(out screen, out beat))
            {
                EndRecall();
                Debug.LogWarning($"Cutscene '{Playback.Script.Id}', line {step.SourceLineNumber}: no tutorial screen or coach beat to recall.");
                return;
            }
            if (screen != null) hud.ShowRecall(screen);
            else hud.ShowRecall(beat);
            recallElapsed = 0f;
            recallSeconds = step.Seconds;
        }

        private void EndRecall()
        {
            recallElapsed = recallSeconds = 0f;
            hud.HideRecall();
        }

        // Up at once under a white flash that dies away; held; faded out over its last part.
        private float RecallAmount
        {
            get
            {
                if (recallSeconds <= 0f) return 0f;
                float rise = Mathf.Min(RecallRiseSeconds, recallSeconds * .1f);
                float fadeOut = Mathf.Min(RecallFadeSeconds, recallSeconds * .4f);
                float shown = rise <= 0f ? 1f : Mathf.Clamp01(recallElapsed / rise);
                float left = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((recallSeconds - recallElapsed) / fadeOut));
                return Mathf.Min(shown, left);
            }
        }

        private float RecallFlash
        {
            get
            {
                if (recallSeconds <= 0f) return 0f;
                float flash = 1f - Mathf.Clamp01(recallElapsed / Mathf.Min(RecallFlashSeconds, recallSeconds * .3f));
                return flash * flash;
            }
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
                        figure.Attacking = figure.Trembling = false;
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
                    figure.Trembling = false;
                    // Leaving the stage takes the figure's power with it (and at once any afterimages it leaves).
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
                case CutsceneActorAction.Tremble:
                    // A new tremble replaces the one in progress; the figure's place is untouched.
                    figure.Trembling = true;
                    figure.TrembleElapsed = 0f;
                    figure.TrembleSeconds = step.Seconds;
                    figure.TrembleStrength = step.Strength;
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
            if (figure.Trembling)
            {
                figure.TrembleElapsed += delta;
                if (figure.TrembleElapsed >= figure.TrembleSeconds) figure.Trembling = false;
            }
            // The dummy's hit reaction plays once and settles back into its sway.
            if (figure == other && otherIsDummy && figure.Pose == CutscenePose.Hurt &&
                figure.PoseClock >= TrainingDummyAnimationSet.HurtDuration)
            {
                figure.Pose = CutscenePose.Idle;
                figure.PoseClock = 0f;
            }
            // Real time in a cutscene (the knight's 수훈 afterimages with her aura); the battle ticks the fighters' auras on
            // its own clock.
            figure.Aura?.Tick(delta);
        }

        private void Apply(float delta)
        {
            hud.SetFade(fade.Value);
            hud.SetBars(bars.Value);
            hud.SetImage(imageSprite, imageAmount.Value);
            if (recallSeconds > 0f) hud.SetRecall(RecallAmount, RecallFlash);
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
            renderer.transform.localPosition = new Vector3(figure.X + TrembleOffset(figure), GroundY, 0f);
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

        /// <summary>A tremble's sideways offset now: two quick waves (at most 1 together) at the tremble's strength, eased
        /// in and out so it neither starts nor stops with a jump. 0 when still.</summary>
        private static float TrembleOffset(Figure figure)
        {
            if (!figure.Trembling) return 0f;
            float time = figure.TrembleElapsed;
            float ease = Mathf.Clamp01(Mathf.Min(time, figure.TrembleSeconds - time) / TrembleEaseSeconds);
            float phase = time * TrembleFrequency * 2f * Mathf.PI;
            float wave = Mathf.Sin(phase) * .65f + Mathf.Sin(phase * 1.7f + 1.1f) * .35f;
            return figure.TrembleStrength * ease * wave;
        }

        private static void RestoreFigure(Figure figure)
        {
            SpriteRenderer renderer = figure.Renderer;
            if (renderer == null) return;
            // A scene cut short in a tremble leaves the figure exactly at its place.
            if (figure.Trembling && figure.Visible) renderer.transform.localPosition = new Vector3(figure.X, GroundY, 0f);
            figure.Trembling = false;
            renderer.flipX = false;
            renderer.color = Color.white;
            renderer.transform.localScale = Vector3.one;
            if (!renderer.gameObject.activeSelf) renderer.gameObject.SetActive(true);
        }
    }
}
