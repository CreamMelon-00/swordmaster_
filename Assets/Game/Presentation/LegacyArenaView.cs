using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Inherited actors/effects with travelling forest-duel staging. The session owns combat rules.</summary>
    public sealed class LegacyArenaView : IDisposable
    {
        /// <summary>How an incoming hit met the target's own action in the rules.</summary>
        public enum HitExchange
        {
            /// <summary>No attack-versus-attack exchange: the body is hit or a defense receives it.</summary>
            None,
            /// <summary>The target's attack has finished its strikes, so its blade receives this resistance-only hit.</summary>
            BladeBlock,
            /// <summary>Both actors strike on this hit; their contact frames already show the blades meeting.</summary>
            MutualClash,
        }

        public const float ApproachDuration = 0.18f;
        public const float ReturnDuration = 0.18f;
        public const float ContactDistance = 4f;
        public const float ApproachSpeed = 32f;
        public const float PursuitSpeed = 54f;
        public const float PursuitDelay = 0.04f;
        public const float PushDuration = 0.1f;
        public const float MinimumKnockback = 0.7f;
        public const float MaximumKnockback = 3f;
        public const float MaximumPendingKnockback = 6f;
        public const float GuardKnockbackMultiplier = 0.35f;
        public const float OriginalClipDuration = 1f / 6f;
        public const float OriginalImpactTime = 1f / 12f;
        public const float ReactionPoseDuration = 0.16f;
        public const float StepDuration = 0.08f;
        public const float DodgeDistance = 1.4f;
        public const float PressureDistance = 1.1f;
        public const float MinimumStepSeparation = 2.8f;
        private const float PressureTrailSampleInterval = 0.035f;
        // Sample just before a looping clip's end so its final keyframe does not wrap to the first.
        private const float GuardHoldOffset = 0.0001f;
        private const int ArenaLayer = 30;
        private const int MaximumEffects = 24;
        private static readonly Color NormalSkyColor = new Color(0.12f, 0.23f, 0.20f, 1f);
        private readonly GameObject arenaRoot;
        private readonly LegacyDuelArt art;
        private readonly Actor player;
        private readonly Actor enemy;
        private readonly MobStudentAnimationSet mobAnimations;
        private readonly System.Random reactionPoseRandom;
        private readonly System.Random attackPoseRandom;
        private readonly GameObject effectPrefab;
        private readonly List<ImpactEffect> effects = new List<ImpactEffect>(MaximumEffects);
        private readonly Material spriteMaterial;
        private readonly VolumeProfile volumeProfile;
        private readonly ChromaticAberration aberration;
        private readonly ColorAdjustments colorAdjustments;
        private readonly Bloom bloom;
        private readonly DuelPresentationSettings settings;
        private readonly bool ownsSettings;
        private readonly DuelImpactGlow impactGlow;
        private readonly DuelStepAfterimages stepAfterimages;
        private LegacyStepAction stepAction;
        private float stepTime;
        private float stepDistance;
        private float stepPlaybackDuration = StepDuration;
        private float stepApplied;
        private int nextStepSample;
        private bool stepping;
        private bool pressureAttackTrail;
        private float pressureTrailSampleTime;
        private float stepFocusTime;
        private float stepFocusDuration;
        private float stepFocusStrength;
        private float stepSlowTime;
        private float stepSlowScale = 1f;
        private LegacyStepAction stepFocusAction;
        private bool stepFeedbackStartedThisSlot;
        // A successful dodge voids the opponent's remaining hits, so nothing is left to guard.
        private bool dodgedThisSlot;
        private float slotAnimationSpeed = 1f;
        private float slotAttackInterval;
        private float fatalExposure;
        private float impactFlashTime;
        private bool resolving;
        private bool approaching;
        private bool returning;
        private bool inspecting;
        private float movementTime;
        private float approachMovementSpeed = 1f;
        private float pursuitMovementSpeed = 1f;
        private float planningTime = 10f;
        private float fatalTime;
        private Transform fatalTarget;
        private float cameraRotation;
        private int rotationSign;
        private Vector3 cameraJolt;
        private float cameraJoltTime;
        private Vector3 combatCameraPivot;
        private float idleTime;
        private bool disposed;

        public Camera ArenaCamera { get; }
        public ForestParallaxBackdrop ForestBackdrop { get; }
        public VolumeProfile ArenaProfile => volumeProfile;
        public DuelImpactGlow ImpactGlow => impactGlow;
        public int ActiveStepAfterimageCount => stepAfterimages.ActiveCount;
        public bool IsStepping => stepping;
        public bool IsPressureAttackTrailActive => pressureAttackTrail;
        public float StepFocusAmount => stepFocusDuration > 0f
            ? stepFocusStrength * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stepFocusTime / stepFocusDuration)) : 0f;
        // The controller owns a custom combat clock. Never change global time,
        // and ease out on real time even during hit stop or manual slow motion.
        public float StepPresentationSpeed => stepSlowTime > 0f
            ? Mathf.Lerp(1f, stepSlowScale, Mathf.Clamp01(stepSlowTime / 0.06f)) : 1f;
        public SpriteRenderer PlayerRenderer => player.Renderer;
        public SpriteRenderer PlayerLowerRenderer => player.LowerRenderer;
        public bool HasMobStudentAnimations => mobAnimations.HasRequiredAssets;
        public SpriteRenderer EnemyRenderer => enemy.Renderer;
        public bool CameraRotate { get; set; }
        public bool ApproachComplete => !approaching;
        public Vector3 DuelCenter => (player.Renderer.transform.localPosition + enemy.Renderer.transform.localPosition) * 0.5f;
        public float Separation => enemy.Renderer.transform.localPosition.x - player.Renderer.transform.localPosition.x;
        public bool HasPendingPush => player.Pushing || enemy.Pushing;
        public bool IsPursuing => resolving && !approaching && !returning && (player.Chasing || enemy.Chasing);
        public Vector3 PlayerKnockbackTarget => player.Pushing ? player.PushEnd : player.Renderer.transform.localPosition;
        public Vector3 EnemyKnockbackTarget => enemy.Pushing ? enemy.PushEnd : enemy.Renderer.transform.localPosition;
        public bool IsInRange => Separation >= 0f && Separation <= ContactDistance + 0.01f;
        public bool IsReturning => returning;
        public bool ReturnComplete => !returning;
        public bool IsFatalFocus => fatalTime > 0f;
        public bool HasRequiredAssets => art.HasRequiredAssets && ForestBackdrop.HasRequiredAssets && spriteMaterial != null &&
            effectPrefab != null && impactGlow.HasRequiredAssets && mobAnimations.HasRequiredAssets && enemy.Clips.Count == 5;
        public int ActiveParticleCount
        {
            get
            {
                var count = 0;
                foreach (var effect in effects)
                    if (effect.Active) count += effect.Particles.particleCount;
                return count;
            }
        }
        public Vector2 PlayerScreenAnchor => ScreenAnchor(player.Renderer.transform.position + Vector3.up * 2f);
        public Vector2 EnemyScreenAnchor => ScreenAnchor(enemy.Renderer.transform.position + Vector3.up * 2f);

        public static LegacyArenaView Create(Transform parent, LegacyDuelArt art, DuelPresentationSettings settings = null,
            System.Random reactionPoseRandom = null, System.Random attackPoseRandom = null) =>
            new LegacyArenaView(parent, art, settings, reactionPoseRandom, attackPoseRandom);

        private LegacyArenaView(Transform parent, LegacyDuelArt art, DuelPresentationSettings settings,
            System.Random reactionPoseRandom, System.Random attackPoseRandom)
        {
            // Cosmetic rolls must not consume the combat/global Unity random stream.
            this.reactionPoseRandom = reactionPoseRandom ?? new System.Random();
            this.attackPoseRandom = attackPoseRandom ?? new System.Random();
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.settings = settings != null ? settings : Resources.Load<DuelPresentationSettings>("DuelPresentationSettings");
            if (this.settings == null)
            {
                this.settings = ScriptableObject.CreateInstance<DuelPresentationSettings>();
                ownsSettings = true;
            }
            arenaRoot = new GameObject("Legacy Duel Arena");
            arenaRoot.transform.SetParent(parent, false);
            arenaRoot.layer = ArenaLayer;

            var pipeline = GraphicsSettings.currentRenderPipeline ?? QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            var shader = Shader.Find(pipeline == null
                ? "Sprites/Default" : "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null) spriteMaterial = new Material(shader) { name = "Legacy Duel Sprites" };

            var cameraObject = Child("Original Duel Camera", arenaRoot.transform);
            ArenaCamera = cameraObject.AddComponent<Camera>();
            ArenaCamera.orthographic = true;
            ArenaCamera.orthographicSize = 6f;
            ArenaCamera.clearFlags = CameraClearFlags.SolidColor;
            ArenaCamera.backgroundColor = NormalSkyColor;
            ArenaCamera.nearClipPlane = 0.1f;
            ArenaCamera.farClipPlane = 100f;
            ArenaCamera.depth = 100f;
            ArenaCamera.cullingMask = 1 << ArenaLayer;
            ArenaCamera.allowHDR = true;
            ArenaCamera.allowMSAA = false;

            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeProfile.name = "Original Duel Post Processing";
            aberration = volumeProfile.Add<ChromaticAberration>();
            aberration.intensity.Override(0f);
            colorAdjustments = volumeProfile.Add<ColorAdjustments>();
            colorAdjustments.postExposure.Override(0f);
            colorAdjustments.saturation.Override(0f);
            bloom = volumeProfile.Add<Bloom>();
            bloom.intensity.Override(this.settings.BloomIntensity);
            bloom.threshold.Override(this.settings.BloomThreshold);
            bloom.scatter.Override(0.55f);
            // Inherited sparks contain HDR values near 30 (over 100 in linear
            // space). Limit only the bloom input, not the original particle art.
            bloom.clamp.Override(4f);
            var vignette = volumeProfile.Add<Vignette>();
            vignette.color.Override(Color.black);
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.2f);
            var volume = Child("Original Duel Volume", arenaRoot.transform).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = volumeProfile;
            if (pipeline is UniversalRenderPipelineAsset)
            {
                var cameraData = ArenaCamera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.volumeLayerMask = 1 << ArenaLayer;
                cameraData.volumeTrigger = ArenaCamera.transform;
                cameraData.antialiasing = AntialiasingMode.None;
            }

            ForestBackdrop = new ForestParallaxBackdrop(arenaRoot.transform, spriteMaterial, ArenaLayer, this.settings);
            impactGlow = new DuelImpactGlow(arenaRoot.transform, ArenaLayer, this.settings);

            mobAnimations = new MobStudentAnimationSet();
            if (!mobAnimations.HasRequiredAssets)
                Debug.LogWarning("Mob student animation resources are incomplete: " + string.Join(", ", mobAnimations.MissingResources));
            player = CreateActor("Player", mobAnimations.HasRequiredAssets ? mobAnimations.GetIdleUpper(0f) : art.GetPlayerSprite(0f));
            player.LowerRenderer = CreateSprite("Mob Student Lower Body", player.Renderer.transform,
                mobAnimations.GetLower(0f, false), -1);
            player.LowerRenderer.enabled = mobAnimations.HasRequiredAssets && !mobAnimations.UsesFullBodyFrames;
            enemy = CreateActor("Enemy0", art.GetEnemySprite(0f));
            stepAfterimages = new DuelStepAfterimages(arenaRoot.transform, spriteMaterial, ArenaLayer, mobAnimations.HasRequiredAssets && !mobAnimations.UsesFullBodyFrames);
            var shadowSprites = Resources.LoadAll<Sprite>("LegacyArena/Shadow/Circle");
            var shadow = shadowSprites.Length > 0 ? shadowSprites[0] : null;
            CreateShadow(player, shadow, mobAnimations.HasRequiredAssets ? 0f : -0.882f);
            CreateShadow(enemy, shadow, 0f);
            effectPrefab = Resources.Load<GameObject>("LegacyArena/VFX/DefaultParticle");
            Reset();
        }

        public void Reset()
        {
            approaching = returning = resolving = inspecting = false;
            CancelStep();
            stepAfterimages.Reset();
            movementTime = fatalTime = cameraJoltTime = idleTime = fatalExposure = impactFlashTime = 0f;
            approachMovementSpeed = pursuitMovementSpeed = settings.MovementSpeedMultiplier;
            slotAnimationSpeed = 1f;
            slotAttackInterval = 0f;
            cameraJolt = Vector3.zero;
            cameraRotation = 0f;
            rotationSign = 0;
            fatalTarget = null;
            planningTime = 10f;
            aberration.intensity.value = 0f;
            colorAdjustments.postExposure.value = 0f;
            colorAdjustments.saturation.value = 0f;
            ResetActor(player, new Vector3(-5f, -0.5f, 0f));
            ResetActor(enemy, new Vector3(5f, -0.5f, 0f));
            player.LowerAnimationTime = 0f;
            player.LowerTravelActive = player.HasLowerTravelProgress = false;
            player.LastVisualPosition = player.Renderer.transform.localPosition;
            SampleActor(player);
            if (player.LowerRenderer != null) player.LowerRenderer.sprite = mobAnimations.GetLower(0f, false);
            combatCameraPivot = DuelCenter;
            ArenaCamera.transform.localPosition = new Vector3(0f, -1.5f, -10f);
            ArenaCamera.transform.localRotation = Quaternion.identity;
            ArenaCamera.orthographicSize = 6f;
            ForestBackdrop.Reset(ArenaCamera);
            impactGlow.Reset();
            RefreshBloom();
            foreach (var effect in effects)
            {
                effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.Instance.SetActive(false);
                effect.Active = false;
            }
        }

        public void BeginTurn()
        {
            resolving = false;
            approaching = returning = false;
            fatalTime = 0f;
            cameraRotation = 0f;
            cameraJoltTime = 0f;
            cameraJolt = Vector3.zero;
            combatCameraPivot = DuelCenter;
            CancelStep();
            player.Skill = enemy.Skill = null;
            player.AnimationTime = enemy.AnimationTime = 0f;
            player.ReactionTime = enemy.ReactionTime = 0f;
            player.Chasing = enemy.Chasing = false;
        }

        public void SetPlanningState(float timeRemaining, bool isInspecting)
        {
            planningTime = Mathf.Clamp(timeRemaining, 0f, 10f);
            inspecting = isInspecting;
        }

        public void BeginApproach()
        {
            combatCameraPivot = DuelCenter;
            cameraJoltTime = 0f;
            cameraJolt = Vector3.zero;
            resolving = approaching = true;
            inspecting = returning = false;
            movementTime = 0f;
            approachMovementSpeed = settings.MovementSpeedMultiplier;
            player.Chasing = enemy.Chasing = false;
            player.Skill = enemy.Skill = null;
            player.LowerTravelActive = player.HasLowerTravelProgress = false;
        }

        public void CloseDistance(float scaledDelta)
        {
            if (IsInRange || approaching || returning || HasPendingPush || stepping) return;
            MoveFightersCloser(Mathf.Max(0f, scaledDelta), PursuitSpeed * pursuitMovementSpeed, !IsPursuing);
            SamplePlayerLowerBody(scaledDelta, scaledDelta);
        }

        public void BeginSlot(LegacySkill playerSkill, LegacySkill enemySkill)
        {
            if (!resolving) combatCameraPivot = DuelCenter;
            resolving = true;
            stepFeedbackStartedThisSlot = dodgedThisSlot = false;
            ClearPressureAttackTrail();
            player.Skill = playerSkill;
            enemy.Skill = enemySkill;
            player.ReactionTime = enemy.ReactionTime = 0f;
            player.GuardVariant = 0;
            player.AttackVariants.Clear();
            player.AnimationTime = enemy.AnimationTime = 0f;
            player.Chasing = enemy.Chasing = false;
            pursuitMovementSpeed = settings.MovementSpeedMultiplier;
            if (CameraRotate)
            {
                cameraRotation = rotationSign == 0 ? UnityEngine.Random.Range(-2.5f, 2.5f)
                    : -rotationSign * UnityEngine.Random.Range(0.5f, 2.5f);
                rotationSign = cameraRotation >= 0f ? 1 : -1;
            }
            SampleActor(player);
            SampleActor(enemy);
        }

        internal void ConfigureSlotTiming(float animationSpeed, float attackInterval)
        {
            // The controller snapshots both values together so changing Inspector
            // settings cannot move an impact to another animation frame mid-skill.
            slotAnimationSpeed = float.IsNaN(animationSpeed) || float.IsInfinity(animationSpeed)
                ? 1f : Mathf.Clamp(animationSpeed, 0.1f, 2f);
            slotAttackInterval = float.IsNaN(attackInterval) || float.IsInfinity(attackInterval)
                ? 0f : Mathf.Clamp(attackInterval, 0f, 1f);
        }

        public void EndTurn()
        {
            returning = true;
            approaching = false;
            movementTime = 0f;
            cameraRotation = 0f;
            cameraJoltTime = 0f;
            cameraJolt = Vector3.zero;
            CancelStep();
            player.Skill = enemy.Skill = null;
            // Slow playback can outlast the turn's final hit; a reaction never carries into planning.
            player.ReactionTime = enemy.ReactionTime = 0f;
            player.Chasing = enemy.Chasing = false;
        }

        /// <summary>Presentation only: the session/controller owns timing and step effects.</summary>
        public void PerformStep(LegacyStepAction action, bool success = false)
        {
            if (disposed || !resolving || approaching || returning ||
                (action != LegacyStepAction.Dodge && action != LegacyStepAction.Pressure)) return;
            stepAction = action;
            if (action == LegacyStepAction.Dodge && success) dodgedThisSlot = true;
            stepTime = stepApplied = 0f;
            // Snapshot a gesture's distance and duration together: live tuning
            // takes effect on the next input, never changes a travelled endpoint.
            stepDistance = (action == LegacyStepAction.Dodge ? -settings.StepDodgeDistance : settings.StepPressureDistance)
                * settings.MovementDistanceMultiplier;
            stepPlaybackDuration = StepDuration / settings.MovementSpeedMultiplier;
            nextStepSample = 1;
            stepping = true;
            player.LowerTravelActive = player.HasLowerTravelProgress = false;
            stepAfterimages.Emit(player.Renderer, action, player.LowerRenderer);
            if (action == LegacyStepAction.Pressure && HasRemainingPlayerAttack())
            {
                pressureAttackTrail = true;
                pressureTrailSampleTime = 0f;
            }
            PresentStepFeedback(action, success);
        }

        // Called after the controller's windup/contact holds: sample the pose
        // actually shown, not the temporary clock advance before HoldSlotAtTime.
        internal void TickPressureAttackTrail(float realDelta, bool animationPlaying)
        {
            if (disposed || !pressureAttackTrail) return;
            if (!resolving || approaching || returning || !HasRemainingPlayerAttack())
            {
                ClearPressureAttackTrail();
                return;
            }
            // The dash already samples its path. Between hits, retain the armed
            // trail without stamping idle poses; the next attack resumes it.
            if (!animationPlaying || stepping || !TryGetSkillFrame(player, out _))
            {
                pressureTrailSampleTime = 0f;
                return;
            }
            if (realDelta <= 0f || float.IsNaN(realDelta) || float.IsInfinity(realDelta)) return;
            pressureTrailSampleTime += realDelta;
            if (pressureTrailSampleTime < PressureTrailSampleInterval) return;
            pressureTrailSampleTime %= PressureTrailSampleInterval;
            // One snapshot per rendered frame, even after a hitch. Reuse the
            // same bounded pool and capture each current sword/character pose.
            stepAfterimages.Emit(player.Renderer, LegacyStepAction.Pressure, player.LowerRenderer);
        }

        private bool HasRemainingPlayerAttack()
        {
            var skill = player.Skill;
            if (skill == null || skill.Kind != LegacySkillKind.Attack) return false;
            return player.AnimationTime < AttackEnd(skill);
        }

        private float AttackEnd(LegacySkill skill)
        {
            float duration = OriginalClipDuration / slotAnimationSpeed;
            return duration + (skill.AttackCount - 1) * (duration + slotAttackInterval);
        }

        // The rules keep a finished attack in the exchange: the opponent's later hits
        // still only trade resistance. Hold a guard instead of idling until they end.
        private bool IsHoldingExchange(Actor actor)
        {
            var own = actor.Skill;
            var other = (actor == player ? enemy : player).Skill;
            if (actor == player && dodgedThisSlot) return false;
            return own != null && own.Kind == LegacySkillKind.Attack &&
                other != null && other.Kind == LegacySkillKind.Attack && other.AttackCount > own.AttackCount &&
                actor.AnimationTime >= AttackEnd(own) && actor.AnimationTime < AttackEnd(other);
        }

        private void ClearPressureAttackTrail()
        {
            pressureAttackTrail = false;
            pressureTrailSampleTime = 0f;
        }

        private void PresentStepFeedback(LegacyStepAction action, bool success)
        {
            if (success)
            {
                // Both actions can still succeed in the rules. The scene gets
                // one full cinematic pulse per skill, so repeated input cannot
                // freeze combat or continually renew the same successful cue.
                if (stepFeedbackStartedThisSlot) return;
                stepFeedbackStartedThisSlot = true;
                stepSlowTime = settings.StepSlowMotionDuration;
                stepSlowScale = settings.StepSlowMotionScale;
                stepFocusDuration = settings.StepFocusDuration;
                stepFocusStrength = 1f;
            }
            else
            {
                // A miss still feels like a physical gesture, but cannot replace
                // or extend an ongoing success focus or slow-motion window.
                if (stepFocusTime > 0f || stepSlowTime > 0f) return;
                stepFocusDuration = Mathf.Min(0.12f, settings.StepFocusDuration);
                stepFocusStrength = 0.25f;
            }
            stepFocusAction = action;
            stepFocusTime = stepFocusDuration;
            RefreshStepBackdrop();
        }

        public void PresentHit(bool playerAttacks, int hpDamage, int resistanceDamage, bool guarded,
            bool fatal, int pushPower = -1, HitExchange exchange = HitExchange.None)
        {
            var attacker = playerAttacks ? player : enemy;
            var target = playerAttacks ? enemy : player;
            float damage = Mathf.Max(0, hpDamage) + Mathf.Max(0, resistanceDamage);
            // Attack against attack only trades resistance: blades meet, not bodies.
            // HP overflow or a resistance break still lands on the body.
            bool bladeBlock = exchange != HitExchange.None && hpDamage <= 0 && !fatal;
            // When both strike on this hit, neither flinches out of its contact frame.
            bool keepsStrike = bladeBlock && exchange == HitExchange.MutualClash;
            bool blocks = (guarded && hpDamage <= 0 && !fatal) || bladeBlock;
            if (target == player && mobAnimations.HasRequiredAssets && !keepsStrike && (guarded || damage > 0f || fatal))
            {
                // A successful guard can spend resistance. HP penetration or a heavy/broken guard recoils.
                player.ReactionIsBlock = blocks;
                // Draw exactly once per incoming hit/guard, including repeated hits in the same slot.
                // Independent draws intentionally allow the same pose on consecutive impacts.
                player.ReactionVariant = reactionPoseRandom.Next(MobStudentAnimationSet.ReactionVariationCount);
                if (player.ReactionIsBlock) player.GuardVariant = player.ReactionVariant;
                player.ReactionTime = ReactionPoseDuration;
                SampleActor(player);
            }
            else if (target == enemy && !keepsStrike && blocks)
            {
                // The legacy enemy sheet has a single guard frame and no hurt frame,
                // so only blocks react, without a cosmetic draw.
                enemy.ReactionIsBlock = true;
                enemy.ReactionTime = ReactionPoseDuration;
                SampleActor(enemy);
            }
            float rawPower = Mathf.Max(0, pushPower);
            float distance;
            if (guarded && damage <= 0f)
                distance = Mathf.Clamp(0.18f + rawPower * 0.025f, 0.18f, 0.35f);
            else
            {
                float power = damage > 0f ? damage : rawPower;
                distance = power > 0f ? Mathf.Clamp(MinimumKnockback + power * 0.16f,
                    MinimumKnockback, MaximumKnockback) : 0f;
                if (guarded) distance *= GuardKnockbackMultiplier;
            }
            distance *= settings.MovementDistanceMultiplier;
            if (distance > 0f)
            {
                // Actor sides remain stable. Repeated hits add to the unfinished
                // outward displacement instead of cancelling or reversing it.
                float direction = playerAttacks ? 1f : -1f;
                Vector3 position = target.Renderer.transform.localPosition;
                float remaining = target.Pushing ? Mathf.Max(0f, (target.PushEnd.x - position.x) * direction) : 0f;
                float queued = Mathf.Min(MaximumPendingKnockback, remaining + distance);
                target.PushStart = position;
                target.PushEnd = position + Vector3.right * (direction * queued);
                target.PushTime = 0f;
                target.PushPlaybackDuration = PushDuration / settings.MovementSpeedMultiplier;
                target.Pushing = true;
                attacker.Chasing = true;
                attacker.ChaseDelay = PursuitDelay;
            }
            target.FlashTime = 0.25f;
            target.FlashColor = guarded ? Color.cyan : hpDamage > 0 ? Color.red : Color.yellow;
            // One bounded impulse per clash: simultaneous hits replace it rather
            // than teleporting/zooming the camera twice over the contact pose.
            float shake = settings.ImpactCameraShake * (fatal ? 2f : guarded ? 0.5f : 1f);
            cameraJolt = new Vector3(playerAttacks ? 1f : -1f, 0.2f, 0f).normalized * shake;
            cameraJoltTime = 0.1f;
            var impactPosition = attacker.Renderer.transform.position + Vector3.right * (playerAttacks ? 2f : -2f);
            EmitImpact(impactPosition);
            impactGlow.Emit(impactPosition, guarded, fatal);
            if (settings.GlowEnabled && settings.FlashExposure > 0f)
                impactFlashTime = Mathf.Min(0.12f, settings.GlowDuration);
            if (fatal && fatalTime <= 0f)
            {
                fatalTime = 0.75f;
                fatalTarget = target.Renderer.transform;
                cameraRotation = UnityEngine.Random.Range(5f, 10f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
            }
            if (fatal)
            {
                aberration.intensity.value = 1f;
                fatalExposure = 1f;
                colorAdjustments.saturation.value = playerAttacks ? 25f : -80f;
            }
            RefreshExposure();
        }

        public void Tick(float scaledDelta) => Tick(scaledDelta, Time.unscaledDeltaTime);

        public void Tick(float scaledDelta, float realDelta)
        {
            if (disposed) return;
            scaledDelta = Mathf.Max(0f, scaledDelta);
            realDelta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            stepFocusTime = Mathf.Max(0f, stepFocusTime - realDelta);
            stepSlowTime = Mathf.Max(0f, stepSlowTime - realDelta);
            idleTime += scaledDelta * settings.AnimationPlaybackSpeed;
            fatalTime = Mathf.Max(0f, fatalTime - Mathf.Max(0f, realDelta));
            cameraJoltTime = Mathf.Max(0f, cameraJoltTime - realDelta);
            aberration.intensity.value = Mathf.MoveTowards(aberration.intensity.value, 0f, scaledDelta * 0.75f);
            fatalExposure = Mathf.MoveTowards(fatalExposure, 0f, scaledDelta * 0.75f);
            impactFlashTime = Mathf.Max(0f, impactFlashTime - Mathf.Max(0f, realDelta));
            RefreshExposure();
            RefreshBloom();
            colorAdjustments.saturation.value = Mathf.MoveTowards(colorAdjustments.saturation.value, 0f, scaledDelta * 60f);
            TickActor(player, scaledDelta);
            TickActor(enemy, scaledDelta);
            stepAfterimages.Tick(realDelta);
            bool stepMovedThisFrame = stepping;
            TickStep(realDelta);
            if (approaching)
            {
                movementTime += scaledDelta;
                MoveFightersCloser(scaledDelta, ApproachSpeed * approachMovementSpeed, true);
                if (movementTime >= ApproachDuration && IsInRange && !HasPendingPush) approaching = false;
            }
            else if (returning)
            {
                // EndTurn retains the displaced world positions. This short wait
                // only lets the HUD settle; it never interpolates to spawn points.
                movementTime += scaledDelta;
                if (movementTime >= ReturnDuration && !HasPendingPush) returning = resolving = false;
            }
            else if (IsPursuing && !stepMovedThisFrame) MoveFightersCloser(scaledDelta, PursuitSpeed * pursuitMovementSpeed, false);
            SamplePlayerLowerBody(scaledDelta, stepMovedThisFrame ? realDelta : scaledDelta, stepMovedThisFrame);
            foreach (var effect in effects)
            {
                if (!effect.Active) continue;
                // Simulate pauses automatically, keeping VFX on the controller's custom slow-motion clock.
                effect.Particles.Simulate(scaledDelta, true, false, false);
                effect.Time += scaledDelta;
                // Simulate pauses the systems; IsAlive can keep reporting future
                // emission even when these non-looping effects have finished.
                // Use our simulation clock and every descendant's actual count.
                if (effect.Time >= effect.EmissionDuration && effect.HasNoParticles)
                {
                    effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    effect.Active = false;
                    effect.Instance.SetActive(false);
                }
            }
            TickCamera(scaledDelta, realDelta);
            RefreshStepBackdrop(scaledDelta);
            impactGlow.Tick(Mathf.Max(0f, realDelta));
        }

        private void RefreshBloom()
        {
            bloom.active = settings.GlowEnabled && settings.BloomIntensity > 0f;
            bloom.intensity.value = settings.BloomIntensity;
            bloom.threshold.value = settings.BloomThreshold;
        }

        private void TickStep(float realDelta)
        {
            if (!stepping) return;
            float delta = realDelta > 0f && !float.IsInfinity(realDelta) ? realDelta : 0f;
            float nextTime = Mathf.Min(stepPlaybackDuration, stepTime + delta);
            if (nextTime >= stepPlaybackDuration - .000001f) nextTime = stepPlaybackDuration;
            // Sample along the travelled path even when a rendered frame spans
            // more than one trail point; never stack all ghosts at the endpoint.
            while (nextStepSample < 3 && nextTime >= stepPlaybackDuration * nextStepSample / 3f)
            {
                ApplyStepOffset(stepPlaybackDuration * nextStepSample / 3f);
                stepAfterimages.Emit(player.Renderer, stepAction, player.LowerRenderer);
                nextStepSample++;
            }
            ApplyStepOffset(nextTime);
            stepTime = nextTime;
            if (stepTime >= stepPlaybackDuration) stepping = false;
        }

        private void ApplyStepOffset(float elapsed)
        {
            float t = Mathf.Clamp01(elapsed / stepPlaybackDuration);
            float desired = stepDistance * (1f - (1f - t) * (1f - t));
            float offset = desired - stepApplied;
            var transform = player.Renderer.transform;
            if (offset > 0f)
                offset = Mathf.Min(offset, Mathf.Max(0f, enemy.Renderer.transform.localPosition.x -
                    MinimumStepSeparation - transform.localPosition.x));
            Vector3 displacement = Vector3.right * offset;
            transform.localPosition += displacement;
            // Knockback owns a separate interpolation. Translate both endpoints
            // so its next sample preserves this gesture instead of snapping back.
            if (player.Pushing)
            {
                player.PushStart += displacement;
                player.PushEnd += displacement;
            }
            stepApplied = desired;
        }

        private void CancelStep()
        {
            stepping = false;
            ClearPressureAttackTrail();
            stepTime = stepDistance = stepApplied = 0f;
            stepPlaybackDuration = StepDuration;
            nextStepSample = 0;
            bool hadFocus = stepFocusTime > 0f;
            stepFocusTime = stepFocusDuration = stepFocusStrength = stepSlowTime = 0f;
            stepSlowScale = 1f;
            stepFeedbackStartedThisSlot = false;
            if (ForestBackdrop != null)
            {
                ArenaCamera.backgroundColor = NormalSkyColor;
                ForestBackdrop.Tick(ArenaCamera, inspecting, 0f);
            }
            if (hadFocus && !IsFatalFocus)
            {
                // Phase changes cancel the special framing immediately. With
                // no focus in progress, preserve the legacy camera behaviour.
                ArenaCamera.orthographicSize = resolving ? settings.CombatCameraSize : inspecting ? 3.5f : 5f + planningTime / 10f;
                combatCameraPivot = DuelCenter;
                Vector3 pivot = resolving ? DuelCenter : inspecting
                    ? enemy.Renderer.transform.localPosition + new Vector3(0f, 0.5f, 0f)
                    : DuelCenter + new Vector3(0f, -1f, 0f);
                pivot.z = -10f;
                ArenaCamera.transform.localPosition = pivot;
                ArenaCamera.transform.localRotation = Quaternion.Euler(0f, 0f, CameraRotate ? cameraRotation : 0f);
            }
        }

        private void RefreshStepBackdrop(float scaledDelta = 0f)
        {
            float darkness = settings.StepBackdropDarkening * StepFocusAmount;
            ArenaCamera.backgroundColor = new Color(NormalSkyColor.r * (1f - darkness),
                NormalSkyColor.g * (1f - darkness), NormalSkyColor.b * (1f - darkness), 1f);
            ForestBackdrop.Tick(ArenaCamera, inspecting, scaledDelta, darkness);
        }

        private void RefreshExposure()
        {
            float pulse = settings.GlowEnabled
                ? Mathf.Clamp01(impactFlashTime / Mathf.Min(0.12f, settings.GlowDuration)) : 0f;
            colorAdjustments.postExposure.value = Mathf.Max(fatalExposure, settings.FlashExposure * pulse * pulse);
        }

        private void TickActor(Actor actor, float delta)
        {
            actor.AnimationTime += delta;
            // Use combat time so hit stop freezes the reaction and slow playback remains readable.
            actor.ReactionTime = Mathf.Max(0f, actor.ReactionTime - delta * slotAnimationSpeed);
            actor.ChaseDelay = Mathf.Max(0f, actor.ChaseDelay - delta);
            if (actor.Pushing)
            {
                actor.PushTime += delta;
                var t = Mathf.Clamp01(actor.PushTime / actor.PushPlaybackDuration);
                actor.Renderer.transform.localPosition = Vector3.Lerp(actor.PushStart, actor.PushEnd, 1f - (1f - t) * (1f - t));
                if (t >= 1f) actor.Pushing = false;
            }
            actor.FlashTime = Mathf.Max(0f, actor.FlashTime - delta);
            actor.Renderer.color = Color.Lerp(Color.white, actor.FlashColor, actor.FlashTime / 0.25f);
            if (actor.LowerRenderer != null) actor.LowerRenderer.color = actor.Renderer.color;
            SampleActor(actor);
        }

        private void MoveFightersCloser(float delta, float speed, bool bothMayApproach)
        {
            float excess = Separation - ContactDistance;
            if (excess <= 0f || delta <= 0f || stepping) return;
            bool playerMayMove = !player.Pushing && (bothMayApproach || player.Chasing && player.ChaseDelay <= 0f);
            bool enemyMayMove = !enemy.Pushing && (bothMayApproach || enemy.Chasing && enemy.ChaseDelay <= 0f);
            if (!playerMayMove && !enemyMayMove) return;
            float step = Mathf.Min(excess / (playerMayMove && enemyMayMove ? 2f : 1f), speed * delta);
            if (playerMayMove)
            {
                if (!player.LowerTravelActive)
                {
                    player.LowerTravelStartX = player.Renderer.transform.localPosition.x;
                    float targetX = enemy.Pushing ? enemy.PushEnd.x : enemy.Renderer.transform.localPosition.x;
                    player.LowerTravelDistance = Mathf.Max(.00001f,
                        (targetX - player.LowerTravelStartX - ContactDistance) / (enemyMayMove ? 2f : 1f));
                    player.LowerTravelActive = true;
                }
                player.HasLowerTravelProgress = true;
                player.Renderer.transform.localPosition += Vector3.right * step;
            }
            if (enemyMayMove) enemy.Renderer.transform.localPosition -= Vector3.right * step;
        }

        internal void HoldSlotAtTime(float elapsed)
        {
            player.AnimationTime = enemy.AnimationTime = Mathf.Max(0f, elapsed);
            SampleActor(player);
            SampleActor(enemy);
        }

        private void SampleActor(Actor actor)
        {
            bool active = TryGetSkillFrame(actor, out float clipTime);
            if (actor == player && mobAnimations.HasRequiredAssets)
            {
                if (actor.ReactionTime > 0f)
                {
                    actor.Renderer.sprite = actor.ReactionIsBlock ? mobAnimations.GetBlockPose(actor.ReactionVariant)
                        : mobAnimations.GetHurtPose(actor.ReactionVariant);
                    return;
                }
                // Defense is a held stance for the entire slot, including gaps between enemy hits.
                // A finished attack guards the same way while the opponent is still striking.
                if (actor.Skill?.Kind == LegacySkillKind.Defence || IsHoldingExchange(actor))
                {
                    actor.Renderer.sprite = mobAnimations.GetBlockPose(actor.GuardVariant);
                    return;
                }
                float cycle = OriginalClipDuration / slotAnimationSpeed + slotAttackInterval;
                int hitIndex = Mathf.FloorToInt(actor.AnimationTime / cycle);
                Sprite attack = active && actor.Skill.Kind == LegacySkillKind.Attack
                    ? mobAnimations.GetAttackUpper(actor.Skill.Property, AttackVariant(actor, hitIndex), clipTime / OriginalClipDuration) : null;
                actor.Renderer.sprite = attack != null ? attack : mobAnimations.GetIdleUpper(idleTime);
                return;
            }
            // Legacy clips: after its own frames, a defense or a finished attack still facing
            // strikes holds the last guard frame, as does a block reaction.
            bool guards = (actor.ReactionTime > 0f && actor.ReactionIsBlock) ||
                (!active && (actor.Skill?.Kind == LegacySkillKind.Defence || IsHoldingExchange(actor)));
            if (guards && actor.Clips.TryGetValue("Defense", out var defense))
            {
                defense.SampleAnimation(actor.Renderer.gameObject, Mathf.Max(0f, defense.length - GuardHoldOffset));
                return;
            }
            var clipName = active ? actor.Skill.AnimationName : "Idle";
            var time = active ? clipTime : idleTime % (2f / 3f);
            if (actor.Clips.TryGetValue(clipName, out var clip)) clip.SampleAnimation(actor.Renderer.gameObject, time);
        }

        private void SamplePlayerLowerBody(float scaledDelta, float movementDelta, bool stepProgress = false)
        {
            if (!mobAnimations.HasRequiredAssets || mobAnimations.UsesFullBodyFrames) return;
            Vector3 position = player.Renderer.transform.localPosition;
            float displacement = position.x - player.LastVisualPosition.x;
            bool moving = Mathf.Abs(displacement) > .00001f;
            if (moving)
            {
                if (stepProgress)
                    player.LowerAnimationTime = LowerCycleTime(stepTime / stepPlaybackDuration);
                else if (player.HasLowerTravelProgress)
                    player.LowerAnimationTime = LowerCycleTime(Mathf.Abs(position.x - player.LowerTravelStartX) / player.LowerTravelDistance);
                else player.LowerAnimationTime += Mathf.Max(0f, movementDelta);
                player.Retreating = displacement < 0f;
            }
            else if (scaledDelta > 0f)
            {
                player.LowerAnimationTime = 0f;
                player.LowerTravelActive = false;
            }
            // Hit stop holds the displayed legs as well as the sword. Real-time steps may still move them.
            if (moving || scaledDelta > 0f)
                player.LowerRenderer.sprite = mobAnimations.GetLower(player.LowerAnimationTime, moving, player.Retreating);
            player.LastVisualPosition = position;
            player.HasLowerTravelProgress = false;
        }

        private int AttackVariant(Actor actor, int hitIndex)
        {
            int count = MobStudentAnimationSet.AttackVariationCount(actor.Skill.Property);
            if (count <= 1) return 0;
            // Cache by hit, not by rendered frame. Clock holds/rewinds must keep the same motion.
            if (!actor.AttackVariants.TryGetValue(hitIndex, out int variant))
            {
                variant = attackPoseRandom.Next(count);
                actor.AttackVariants.Add(hitIndex, variant);
            }
            return variant;
        }

        private static float LowerCycleTime(float progress) =>
            Mathf.Min(Mathf.Clamp01(progress) * 8f * MobStudentAnimationSet.MoveFrameDuration,
                8f * MobStudentAnimationSet.MoveFrameDuration - .00001f);

        private bool TryGetSkillFrame(Actor actor, out float clipTime)
        {
            float duration = OriginalClipDuration / slotAnimationSpeed;
            float cycle = duration + slotAttackInterval;
            int attackIndex = Mathf.FloorToInt(actor.AnimationTime / cycle);
            float cycleTime = actor.AnimationTime % cycle;
            clipTime = cycleTime * slotAnimationSpeed;
            return actor.Skill != null && !actor.Skill.IsWait &&
                attackIndex < actor.Skill.AttackCount && cycleTime < duration;
        }

        private void TickCamera(float delta, float realDelta)
        {
            float size;
            Vector3 pivot;
            // A stable two-person shot follows sustained travel, not every small
            // recoil or sprite-frame change. Camera easing always uses real time.
            float sharpness = IsFatalFocus ? Mathf.Max(10f, settings.CameraFollowSharpness) : settings.CameraFollowSharpness;
            var blend = 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, realDelta));
            if (resolving || IsFatalFocus)
            {
                Vector3 center = DuelCenter;
                float drift = center.x - combatCameraPivot.x;
                if (Mathf.Abs(drift) > settings.CameraFollowDeadZone)
                    combatCameraPivot.x = center.x - Mathf.Sign(drift) * settings.CameraFollowDeadZone;
                combatCameraPivot.y = center.y;
                size = IsFatalFocus ? 3f : settings.CombatCameraSize;
                pivot = IsFatalFocus && fatalTarget != null ? fatalTarget.localPosition
                    : combatCameraPivot;
            }
            else
            {
                size = inspecting ? 3.5f : 5f + planningTime / 10f;
                pivot = inspecting ? enemy.Renderer.transform.localPosition + new Vector3(0f, 0.5f, 0f)
                    : DuelCenter + new Vector3(0f, -1f, 0f);
            }
            float focus = IsFatalFocus ? 0f : StepFocusAmount;
            float direction = stepFocusAction == LegacyStepAction.Dodge ? -1f : 1f;
            if (focus > 0f)
            {
                Vector3 playerFocus = player.Renderer.transform.localPosition +
                    new Vector3(direction * 0.4f, 0.2f, 0f);
                pivot = Vector3.Lerp(pivot, playerFocus, focus * 0.6f);
                pivot.x += direction * focus * 0.15f;
                size = Mathf.Max(1.5f, size - settings.StepCameraZoom * focus);
                // A quick push-in and slower release make the gesture legible
                // even when the actual step lasts only a few rendered frames.
                blend = 1f - Mathf.Pow(0.72f, Mathf.Max(0f, realDelta) * 60f);
            }
            pivot.z = -10f;
            if (cameraJoltTime > 0f) pivot += cameraJolt * (cameraJoltTime / 0.1f);
            ArenaCamera.orthographicSize = Mathf.Lerp(ArenaCamera.orthographicSize, size, blend);
            ArenaCamera.transform.localPosition = Vector3.Lerp(ArenaCamera.transform.localPosition, pivot, blend);
            var rotationBlend = 1f - Mathf.Pow(IsFatalFocus ? 0.8f : 0.95f, Mathf.Max(0f, realDelta) * 60f);
            float rotation = CameraRotate || IsFatalFocus ? cameraRotation : 0f;
            if (!IsFatalFocus) rotation += direction * focus * 2.5f;
            ArenaCamera.transform.localRotation = Quaternion.Lerp(ArenaCamera.transform.localRotation,
                Quaternion.Euler(0f, 0f, rotation), rotationBlend);
        }

        private void EmitImpact(Vector3 position)
        {
            if (effectPrefab == null) return;
            ImpactEffect effect = null;
            foreach (var candidate in effects)
                if (!candidate.Active) { effect = candidate; break; }
            if (effect == null)
            {
                if (effects.Count >= MaximumEffects) return;
                var instance = Object.Instantiate(effectPrefab, arenaRoot.transform);
                instance.name = "Original Clash Particles";
                foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = ArenaLayer;
                foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
                var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                float emissionDuration = 0f;
                foreach (var system in systems)
                {
                    var main = system.main;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.playOnAwake = false;
                    main.simulationSpeed = 1f;
                    emissionDuration = Mathf.Max(emissionDuration, main.startDelay.constantMax + main.duration);
                }
                effect = new ImpactEffect
                {
                    Instance = instance, Particles = instance.GetComponent<ParticleSystem>(),
                    Systems = systems, EmissionDuration = emissionDuration
                };
                effects.Add(effect);
            }
            effect.Instance.transform.position = position;
            effect.Instance.SetActive(true);
            effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Particles.Simulate(0.0001f, true, true, false);
            effect.Time = 0f;
            effect.Active = true;
        }

        private Actor CreateActor(string name, Sprite sprite)
        {
            var actor = new Actor { Renderer = CreateSprite(name, arenaRoot.transform, sprite, 0) };
            // Non-legacy clips need an Animator to bind their SpriteRenderer object curves.
            // No controller is assigned: SampleActor alone owns playback on the combat clock.
            actor.Renderer.gameObject.AddComponent<Animator>();
            foreach (var clipName in new[] { "Idle", "Slash", "Penetrate", "Hit", "Defense" })
            {
                var clip = Resources.Load<AnimationClip>("LegacyArena/Animation/" + name + "/" + clipName);
                if (clip != null) actor.Clips.Add(clipName, clip);
            }
            return actor;
        }

        private void CreateShadow(Actor actor, Sprite sprite, float x)
        {
            var shadow = CreateSprite("Original Ground Shadow", actor.Renderer.transform, sprite, -2);
            shadow.transform.localPosition = new Vector3(x, -2.23f, 0f);
            shadow.transform.localScale = new Vector3(3f, 0.7f, 1f);
            shadow.color = new Color(0f, 0f, 0f, 0.6117647f);
        }

        private SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var renderer = Child(name, parent).AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
            return renderer;
        }

        private static GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name) { layer = ArenaLayer };
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void ResetActor(Actor actor, Vector3 position)
        {
            actor.Renderer.transform.localPosition = position;
            actor.Renderer.transform.localScale = Vector3.one;
            actor.Renderer.color = Color.white;
            if (actor.LowerRenderer != null) actor.LowerRenderer.color = Color.white;
            actor.Pushing = false;
            actor.Chasing = false;
            actor.ChaseDelay = 0f;
            actor.PushStart = actor.PushEnd = position;
            actor.PushTime = 0f;
            actor.PushPlaybackDuration = PushDuration;
            actor.FlashTime = actor.AnimationTime = 0f;
            actor.ReactionTime = 0f;
            actor.ReactionIsBlock = false;
            actor.ReactionVariant = actor.GuardVariant = 0;
            actor.AttackVariants.Clear();
            actor.Skill = null;
            if (actor.Clips.TryGetValue("Idle", out var idle)) idle.SampleAnimation(actor.Renderer.gameObject, 0f);
        }

        private Vector2 ScreenAnchor(Vector3 position)
        {
            var screen = ArenaCamera.WorldToScreenPoint(position);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelStep();
            stepAfterimages.Dispose();
            mobAnimations.Dispose();
            impactGlow.Dispose();
            Object.Destroy(arenaRoot);
            if (spriteMaterial != null) Object.Destroy(spriteMaterial);
            foreach (var component in volumeProfile.components) Object.Destroy(component);
            Object.Destroy(volumeProfile);
            if (ownsSettings) Object.Destroy(settings);
        }

        private sealed class Actor
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer LowerRenderer;
            public readonly Dictionary<string, AnimationClip> Clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            public LegacySkill Skill;
            public float AnimationTime, FlashTime, PushTime, ChaseDelay;
            public float ReactionTime;
            public bool ReactionIsBlock;
            public int ReactionVariant, GuardVariant;
            public readonly Dictionary<int, int> AttackVariants = new Dictionary<int, int>();
            public float PushPlaybackDuration = PushDuration;
            public Color FlashColor;
            public Vector3 PushStart, PushEnd;
            public bool Pushing, Chasing;
            public float LowerAnimationTime;
            public Vector3 LastVisualPosition;
            public bool Retreating;
            public bool LowerTravelActive, HasLowerTravelProgress;
            public float LowerTravelStartX, LowerTravelDistance;
        }

        private sealed class ImpactEffect
        {
            public GameObject Instance;
            public ParticleSystem Particles;
            public ParticleSystem[] Systems;
            public float EmissionDuration;
            public bool Active;
            public float Time;

            public bool HasNoParticles
            {
                get
                {
                    foreach (var system in Systems)
                        if (system.particleCount > 0) return false;
                    return true;
                }
            }
        }
    }
}
