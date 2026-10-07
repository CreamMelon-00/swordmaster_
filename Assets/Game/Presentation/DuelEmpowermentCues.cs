using System;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Prologue;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>The 서막's 수훈 phase in battle (mission 4, from the empowerment until the battle ends). A warm golden grade
    /// and a deeper vignette ease in over the arena and the aura's hum (<c>aura-loop</c>) loops quietly under the fight.
    /// Each of the empowered enemy's own techniques (the enemy-only rows her empowered turns use,
    /// <see cref="MissionEmpowerment.IsSignatureSkill"/>) opens with a short cut-in: the battle holds while the camera eases
    /// toward her and back and her aura flares. Every hit of a several-hit one (라우다레) shakes the camera a little. No
    /// skill name is shown. <see cref="Clear"/> takes it all away (battle end, retry, exit); the arena sets the grade
    /// aside while a cutscene plays in the middle of the battle, and <see cref="SetPaused"/> quiets the hum meanwhile.</summary>
    public sealed class DuelEmpowermentCues : IDisposable
    {
        public const string AuraLoopResource = CutsceneStep.SoundFolder + "aura-loop";
        /// <summary>How long the hum takes to fade out when the phase ends or the battle pauses (real seconds).</summary>
        public const float LoopFadeOutSeconds = .4f;
        /// <summary>The share of a cut-in's hold its flare spends gathering; it flares out over the rest.</summary>
        public const float CutInChargeShare = .5f;

        private readonly LegacyArenaView arena;
        private readonly Func<DuelPresentationSettings> settings;
        private readonly GameObject root;
        private readonly AudioSource loop;
        private readonly AudioClip loopClip;
        private float cutInSeconds, cutInElapsed;
        private bool paused, disposed;

        /// <param name="settings">Read when used, so live tuning (and a test's clone) applies at once.</param>
        public DuelEmpowermentCues(Transform parent, LegacyArenaView arena, Func<DuelPresentationSettings> settings)
        {
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            root = new GameObject("Empowerment Aura Loop");
            if (parent != null) root.transform.SetParent(parent, false);
            loop = root.AddComponent<AudioSource>();
            loop.playOnAwake = false;
            loop.loop = true;
            loop.spatialBlend = 0f;
            loop.volume = 0f;
            loopClip = Resources.Load<AudioClip>(AuraLoopResource);
        }

        /// <summary>Whether the 수훈 phase is on.</summary>
        public bool IsActive { get; private set; }
        /// <summary>How far the atmosphere has eased in, 0 to 1 (what the arena shows while the battle runs).</summary>
        public float Atmosphere { get; private set; }
        /// <summary>Whether a cut-in is holding the battle now.</summary>
        public bool IsCuttingIn => cutInElapsed < cutInSeconds;
        /// <summary>The real seconds the cut-in in progress still holds the battle.</summary>
        public float CutInRemaining => IsCuttingIn ? cutInSeconds - cutInElapsed : 0f;
        /// <summary>The hum's clip, or null when Resources has none (then the phase is silent).</summary>
        public AudioClip LoopClip => loopClip;
        /// <summary>Whether the hum is playing (or fading out).</summary>
        public bool IsLoopPlaying => !disposed && loop.clip != null;
        public float LoopVolume => disposed ? 0f : loop.volume;

        /// <summary>The empowerment is applied (its scene is over or skipped): the atmosphere and the hum ease in from
        /// here, on real time while the battle runs.</summary>
        public void Begin()
        {
            if (disposed || IsActive) return;
            IsActive = true;
            paused = false;
            if (loopClip == null || loop.clip == loopClip) return;
            loop.clip = loopClip;
            loop.volume = 0f;
            loop.Play();
        }

        /// <summary>A battle frame: eases the atmosphere in (or out) and hands it to the arena.</summary>
        public void Tick(float realDelta)
        {
            if (disposed) return;
            float seconds = settings().EmpowermentAtmosphereSeconds;
            float target = IsActive ? 1f : 0f;
            Atmosphere = seconds > 0f ? Mathf.MoveTowards(Atmosphere, target, Mathf.Max(0f, realDelta) / seconds) : target;
            if (IsActive || arena.EmpowermentAmount > 0f) arena.SetEmpowerment(Atmosphere);
        }

        /// <summary>Every frame, whatever is on screen: the hum follows the atmosphere up and fades out once the phase ends
        /// or the battle pauses, then stops.</summary>
        public void TickAudio(float realDelta)
        {
            if (disposed || loop.clip == null) return;
            float volume = settings().EmpowermentAuraLoopVolume;
            float target = IsActive && !paused ? volume * Atmosphere : 0f;
            loop.volume = target >= loop.volume ? target
                : Mathf.MoveTowards(loop.volume, target, Mathf.Max(0f, realDelta) * Mathf.Max(volume, loop.volume) / LoopFadeOutSeconds);
            if (IsActive || loop.volume > 0f) return;
            loop.Stop();
            loop.clip = null;
        }

        /// <summary>The battle pauses for a scene (true) or resumes (false): the hum fades out and back.</summary>
        public void SetPaused(bool value) => paused = value;

        /// <summary>A slot is starting: when the enemy's action there is one of her 수훈 techniques, the cut-in begins (her
        /// aura starts gathering for its flare). Returns whether it did.</summary>
        public bool TryBeginCutIn(MissionEmpowerment empowerment, LegacySkill enemySkill)
        {
            if (disposed || !IsActive || empowerment == null) return false;
            float seconds = settings().EmpowermentCutInSeconds;
            if (seconds <= 0f || !empowerment.IsSignatureSkill(enemySkill)) return false;
            cutInSeconds = seconds;
            cutInElapsed = 0f;
            arena.EnemyPowerAura.Charge(seconds * CutInChargeShare);
            return true;
        }

        /// <summary>Of <paramref name="available"/> real seconds of this battle frame (what the hit stop left), takes what
        /// the cut-in in progress still holds and returns it: the battle's clock does not move for that time. The camera's
        /// ease and the enemy's flare run on it meanwhile.</summary>
        public float HoldBattle(float available)
        {
            if (disposed || !IsCuttingIn) return 0f;
            float held = Mathf.Min(Mathf.Max(0f, available), cutInSeconds - cutInElapsed);
            cutInElapsed += held;
            // Her aura (and the afterimages it leaves) normally runs on the battle's clock, which the hold stops.
            arena.EnemyPowerAura.Tick(held);
            arena.SetCutIn(LegacyCutIn.CameraAmount(cutInElapsed, cutInSeconds));
            if (!IsCuttingIn) EndCutIn();
            return held;
        }

        /// <summary>The enemy's hit landed: a hit of a several-hit 수훈 technique (라우다레) shakes the camera a little.</summary>
        public void NotifyEnemyHit(MissionEmpowerment empowerment, LegacySkill enemySkill)
        {
            if (disposed || !IsActive || empowerment == null || !empowerment.IsSignatureBarrage(enemySkill)) return;
            DuelPresentationSettings tuning = settings();
            arena.AddCameraShake(tuning.LaudareShakeStrength, tuning.LaudareShakeSeconds);
        }

        /// <summary>The phase ends (battle over, retry, exit): the grade and the cut-in go at once, the hum fades out.</summary>
        public void Clear()
        {
            if (disposed) return;
            IsActive = paused = false;
            Atmosphere = 0f;
            arena.SetEmpowerment(0f);
            EndCutIn();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            loop.Stop();
            loop.clip = null;
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }

        private void EndCutIn()
        {
            cutInSeconds = cutInElapsed = 0f;
            arena.SetCutIn(0f);
        }
    }
}
