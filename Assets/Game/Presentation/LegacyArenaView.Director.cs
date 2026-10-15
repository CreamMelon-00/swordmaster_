using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>The presentation director's arena (2026-10-15, <see cref="LegacyShowDirector"/>): the camera's push for a
    /// Major slot and the blade bind (팽팽한 합) of an even 합; the read rewards' light accent on a fighter (간파,
    /// <see cref="AccentFighter"/>) and a signature move's windup glow and strike trail (<see cref="BeginSignature"/>).
    /// None of them moves the battle's clock; the controller owns the time (the bind runs on the real seconds it hands
    /// over, while the battle's clock stands still).</summary>
    public sealed partial class LegacyArenaView
    {
        // The push eases in and out on real time this fast (about 95% in 0.3 s).
        private const float DirectorPushSharpness = 10f;
        // The locked blades' small shake for the bind's length (world units at the combat framing).
        private const float BladeBindShake = .035f;
        private float directorPushRequest, directorPushAmount;
        private float bindSeconds, bindElapsed, bindSway, bindPress, bindSpring;
        private int bindSparks;
        private float bindPlayerOffset, bindEnemyOffset;
        // A signature move's strike trail: tinted ghosts of the fighter's frames while its attack plays (made on first use).
        private const float SignatureTrailInterval = .035f;
        private const int SignatureAfterimageCapacity = 16;
        private DuelStepAfterimages signatureAfterimages;
        private SignatureTrail playerSignatureTrail, enemySignatureTrail;

        private struct SignatureTrail
        {
            public bool Armed;
            public Color Ink;
            public float Lifetime, Clock;
        }

        /// <summary>How far the director's push has the combat framing in now (a share of its size, <see cref="SetDirectorPush"/>).</summary>
        public float DirectorPushAmount => directorPushAmount;
        /// <summary>Whether a blade bind holds the two fighters now (<see cref="BeginBladeBind"/>).</summary>
        public bool IsBladeBinding => bindSeconds > 0f;
        /// <summary>The share of the blade bind played (0 to 1; 0 while none).</summary>
        public float BladeBindProgress => bindSeconds > 0f ? Mathf.Clamp01(bindElapsed / bindSeconds) : 0f;
        /// <summary>Each fighter's offset along the line the bind has them at now (world x; zero while none).</summary>
        public float PlayerBladeBindOffset => bindPlayerOffset;
        public float EnemyBladeBindOffset => bindEnemyOffset;
        /// <summary>The sparks the current (or last) bind has struck between the blades.</summary>
        public int BladeBindSparksStruck { get; private set; }

        /// <summary>The director's push this frame: the combat framing comes in by this share of its size (0 to 0.5; the
        /// controllers ask for it while a Major slot plays). Asked for frame by frame like the time pressure: the next Tick
        /// consumes it and the push eases toward it on real time, back out once no frame asks. Only while resolving; the
        /// decisive close-up keeps its own framing.</summary>
        public void SetDirectorPush(float share)
        {
            directorPushRequest = float.IsNaN(share) || float.IsInfinity(share) ? 0f : Mathf.Clamp(share, 0f, .5f);
        }

        /// <summary>A blade bind begins (an even 합's first hit, both blades met, neither body reached): for
        /// <paramref name="seconds"/> of the real time the controller hands over (<see cref="AdvanceBladeBind"/>, while the
        /// battle's clock stands still) the two fighters hold their contact frames, lean into each other by
        /// <paramref name="press"/> each and sway together by <paramref name="sway"/> one way and then the other just as far
        /// (<see cref="LegacyBladeBind"/>), striking <paramref name="sparks"/> sparks between the blades; at the end both
        /// spring apart alike: the knock-backs the hit queued are shared out evenly, plus <paramref name="spring"/> each (they
        /// move once the battle's clock runs again). A small shake rumbles meanwhile. Only while resolving; a bind already
        /// holding lets go first.</summary>
        public void BeginBladeBind(float seconds, float sway, float press, float spring, int sparks)
        {
            if (disposed) return;
            ClearBladeBind();
            if (!resolving || approaching || returning || !(seconds > 0f) || float.IsInfinity(seconds)) return;
            bindSeconds = seconds;
            bindElapsed = 0f;
            bindSway = Finite(sway);
            bindPress = Finite(press);
            bindSpring = Finite(spring);
            bindSparks = Mathf.Max(0, sparks);
            BladeBindSparksStruck = 0;
            hitShake.Add(BladeBindShake, seconds);
        }

        /// <summary>Plays <paramref name="realSeconds"/> more of the blade bind. At its end the fighters stand where it
        /// found them, plus the spring apart.</summary>
        public void AdvanceBladeBind(float realSeconds)
        {
            if (disposed || bindSeconds <= 0f) return;
            float before = bindElapsed / bindSeconds;
            bindElapsed = Mathf.Min(bindSeconds, bindElapsed + (realSeconds > 0f && !float.IsInfinity(realSeconds) ? realSeconds : 0f));
            float t = bindElapsed / bindSeconds;
            LegacyBladeBind.Offsets(t, bindSway, bindPress, out float playerOffset, out float enemyOffset);
            ApplyBindOffsets(playerOffset, enemyOffset);
            int sparks = LegacyBladeBind.SparksBetween(before, t, bindSparks);
            for (int spark = 0; spark < sparks; spark++) EmitBindSpark();
            if (bindElapsed >= bindSeconds) FinishBladeBind();
        }

        /// <summary>The blade bind is over (its time has run, or the controller's has): the fighters stand where it found
        /// them, and both spring apart alike. Nothing while none holds.</summary>
        public void FinishBladeBind()
        {
            if (disposed || bindSeconds <= 0f) return;
            int sparks = LegacyBladeBind.SparksBetween(bindElapsed / bindSeconds, 1f, bindSparks);
            for (int spark = 0; spark < sparks; spark++) EmitBindSpark();
            ApplyBindOffsets(0f, 0f);
            // Both took a hit: the knock-backs the hit queued (held through the bind) are shared out evenly, so the two
            // spring apart alike and no farther in all than without the bind, plus the tuned extra spring each.
            float even = (Pending(player, -1f) + Pending(enemy, 1f)) * .5f + bindSpring;
            SpringApart(player, -1f, even);
            SpringApart(enemy, 1f, even);
            bindSeconds = bindElapsed = 0f;
        }

        /// <summary>Lets a blade bind go where it is: its offsets come off (the fighters stand where it found them), with no
        /// spring apart. Nothing while none holds.</summary>
        public void ClearBladeBind()
        {
            if (bindSeconds <= 0f && bindPlayerOffset == 0f && bindEnemyOffset == 0f) return;
            ApplyBindOffsets(0f, 0f);
            bindSeconds = bindElapsed = 0f;
        }

        // A reset placed the fighters afresh: forget the bind without moving them, the push and the signature trails.
        private void ClearDirector()
        {
            bindSeconds = bindElapsed = bindPlayerOffset = bindEnemyOffset = 0f;
            directorPushRequest = directorPushAmount = 0f;
            ClearSignatureTrails(true);
        }

        /// <summary>The read rewards' light accent (간파, 2026-10-15): a brief flare at the fighter's chest in
        /// <paramref name="ink"/>, its power aura's release (<see cref="DuelPowerAura.Flare"/>), fading on the battle's clock.
        /// Nothing while the arena is not resolving.</summary>
        public void AccentFighter(bool playerSide, Color ink)
        {
            if (disposed || !resolving) return;
            (playerSide ? playerPowerAura : enemyPowerAura).Flare(ink);
        }

        /// <summary>A signature move begins on one side (2026-10-15): with <paramref name="glow"/> its power gathers at the
        /// fighter's chest in <paramref name="ink"/> (its school's dye) over <paramref name="windupSeconds"/> of the battle's
        /// clock (the windup to its first strike) and flares as it strikes; with <paramref name="afterimageSeconds"/> above 0
        /// its attack leaves a trail of ghosts in that ink, each fading over that many real seconds from
        /// <paramref name="afterimageAlpha"/> (<see cref="TickSignatureTrails"/>). Only while resolving; the next slot,
        /// the turn's end and <see cref="Reset"/> end the trail.</summary>
        public void BeginSignature(bool playerSide, Color ink, float windupSeconds, bool glow, float afterimageSeconds,
            float afterimageAlpha)
        {
            if (disposed || !resolving) return;
            if (glow) (playerSide ? playerPowerAura : enemyPowerAura).Charge(Finite(windupSeconds), false, ink);
            if (!(afterimageSeconds > 0f) || !(afterimageAlpha > 0f)) return;
            if (signatureAfterimages == null)
                signatureAfterimages = new DuelStepAfterimages(arenaRoot.transform, spriteMaterial, ArenaLayer,
                    mobAnimations.HasRequiredAssets && !mobAnimations.UsesFullBodyFrames, SignatureAfterimageCapacity, "Signature");
            var trail = new SignatureTrail
            {
                Armed = true,
                Ink = new Color(ink.r, ink.g, ink.b, Mathf.Clamp01(afterimageAlpha)),
                Lifetime = Mathf.Min(afterimageSeconds, 2f),
            };
            if (playerSide) playerSignatureTrail = trail;
            else enemySignatureTrail = trail;
        }

        /// <summary>Whether a side's signature trail is armed (its attack still to play or playing).</summary>
        public bool IsSignatureTrailArmed(bool playerSide) => (playerSide ? playerSignatureTrail : enemySignatureTrail).Armed;
        /// <summary>The signature trail's ghosts on screen now.</summary>
        public int ActiveSignatureAfterimageCount => signatureAfterimages?.ActiveCount ?? 0;

        /// <summary>The signature trails' frame, after the controller's windup and contact holds (like the pressure trail):
        /// while the slot animates (<paramref name="animationPlaying"/>) and a side's attack shows a strike frame, a ghost
        /// of it is left every few hundredths of a second (real time); once its attack is over its trail ends.</summary>
        internal void TickSignatureTrails(float realDelta, bool animationPlaying)
        {
            if (disposed) return;
            TickSignatureTrail(player, ref playerSignatureTrail, realDelta, animationPlaying);
            TickSignatureTrail(enemy, ref enemySignatureTrail, realDelta, animationPlaying);
        }

        private void TickSignatureTrail(Actor actor, ref SignatureTrail trail, float realDelta, bool animationPlaying)
        {
            if (!trail.Armed) return;
            if (!resolving || approaching || returning || !HasRemainingAttack(actor))
            {
                trail.Armed = false;
                return;
            }
            if (!animationPlaying || actor == player && stepping || !TryGetSkillFrame(actor, out _))
            {
                trail.Clock = 0f;
                return;
            }
            if (!(realDelta > 0f) || float.IsInfinity(realDelta)) return;
            trail.Clock += realDelta;
            if (trail.Clock < SignatureTrailInterval) return;
            trail.Clock %= SignatureTrailInterval;
            signatureAfterimages?.Emit(actor.Renderer, trail.Ink, trail.Lifetime, Vector3.zero, 0f, 0f,
                actor.Renderer.sortingOrder - 1, actor == player ? player.LowerRenderer : null);
        }

        // Whether the actor's attack still has frames to play this slot.
        private bool HasRemainingAttack(Actor actor)
        {
            LegacySkill skill = actor.Skill;
            return skill != null && skill.Kind == LegacySkillKind.Attack && actor.AnimationTime < AttackEnd(skill);
        }

        // The ghosts fade on real time (as the steps' do).
        private void TickSignatureAfterimages(float realDelta) => signatureAfterimages?.Tick(realDelta);

        // The trails end (a new slot, the turn's end, a scene); ghosts also takes their ghosts away.
        private void ClearSignatureTrails(bool ghosts)
        {
            playerSignatureTrail = enemySignatureTrail = default;
            if (ghosts) signatureAfterimages?.Reset();
        }

        private void DisposeDirector()
        {
            signatureAfterimages?.Dispose();
            signatureAfterimages = null;
        }

        private void TickDirectorPush(float realDelta)
        {
            float target = resolving && !approaching && !returning ? directorPushRequest : 0f;
            directorPushRequest = 0f;
            float blend = 1f - Mathf.Exp(-DirectorPushSharpness * Mathf.Max(0f, realDelta));
            directorPushAmount = Mathf.Lerp(directorPushAmount, target, blend);
            if (Mathf.Abs(directorPushAmount - target) < .0001f) directorPushAmount = target;
        }

        // Moves each fighter by the change of its offset: a pending knock-back moves with it (its next sample keeps the
        // gesture), and the player's legs do not read it as a walk.
        private void ApplyBindOffsets(float playerOffset, float enemyOffset)
        {
            Shift(player, playerOffset - bindPlayerOffset);
            Shift(enemy, enemyOffset - bindEnemyOffset);
            bindPlayerOffset = playerOffset;
            bindEnemyOffset = enemyOffset;
        }

        private static void Shift(Actor actor, float x)
        {
            if (x == 0f) return;
            Vector3 displacement = Vector3.right * x;
            actor.Renderer.transform.localPosition += displacement;
            if (actor.Pushing)
            {
                actor.PushStart += displacement;
                actor.PushEnd += displacement;
            }
            actor.LastVisualPosition += displacement;
        }

        // How far a fighter's pending knock-back still takes it outward (0 while none).
        private static float Pending(Actor actor, float direction)
            => actor.Pushing ? Mathf.Max(0f, (actor.PushEnd.x - actor.Renderer.transform.localPosition.x) * direction) : 0f;

        // The fighter's knock-back from where it stands now: this far outward, never past the pending cap (none: it stands).
        // The dummy never binds.
        private void SpringApart(Actor actor, float direction, float distance)
        {
            if (actor == enemy && EnemyIsDummy) return;
            Vector3 position = actor.Renderer.transform.localPosition;
            float queued = Mathf.Min(MaximumPendingKnockback, Mathf.Max(0f, distance));
            actor.PushStart = position;
            actor.PushEnd = position + Vector3.right * (direction * queued);
            actor.PushTime = 0f;
            actor.PushPlaybackDuration = PushDuration / settings.MovementSpeedMultiplier;
            actor.Pushing = queued > 0f;
        }

        // Between the two blades: the glow's starlight (on real time, so it shows while the battle's clock stands still).
        private void EmitBindSpark()
        {
            BladeBindSparksStruck++;
            Vector3 contact = (player.Renderer.transform.position + enemy.Renderer.transform.position) * .5f;
            impactGlow.Emit(contact, false, false);
        }

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }
}
