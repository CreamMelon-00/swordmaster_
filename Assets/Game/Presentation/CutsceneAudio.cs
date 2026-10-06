using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>A cutscene's sounds: one 2D voice for one-shots (<c>@sound</c>; they may overlap) and one looping voice
    /// that fades in and out (<c>@ambience</c>). Clips load from Resources; a missing one returns false and plays
    /// nothing. <see cref="Stop"/> silences both at once when the scene ends or is skipped. The state is kept here,
    /// not read back from the sources, so it holds where audio is disabled (batch runs).</summary>
    public sealed class CutsceneAudio : IDisposable
    {
        private readonly GameObject root;
        private float ambienceFrom, ambienceTo, ambienceElapsed, ambienceSeconds;
        private bool disposed;

        public CutsceneAudio(Transform parent)
        {
            root = new GameObject("Cutscene Audio");
            if (parent != null) root.transform.SetParent(parent, false);
            EffectSource = CreateVoice("Cutscene Sound", false);
            AmbienceSource = CreateVoice("Cutscene Ambience", true);
        }

        public AudioSource EffectSource { get; }
        public AudioSource AmbienceSource { get; }
        /// <summary>The last one-shot started, or null.</summary>
        public AudioClip LastSound { get; private set; }
        /// <summary>The loop playing or still fading out, or null.</summary>
        public AudioClip AmbienceClip { get; private set; }
        public float AmbienceVolume => disposed ? 0f : AmbienceSource.volume;

        /// <summary>Plays the clip at <paramref name="resourcePath"/> once. Returns false when there is no such clip.</summary>
        public bool PlaySound(string resourcePath, float volume)
        {
            if (disposed) return false;
            AudioClip clip = Load(resourcePath);
            if (clip == null) return false;
            LastSound = clip;
            EffectSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return true;
        }

        /// <summary>Loops the clip at <paramref name="resourcePath"/>, fading in from silence over
        /// <paramref name="fadeSeconds"/> (0 = at once). The same clip already looping just fades back to full.
        /// Returns false when there is no such clip (whatever was looping carries on).</summary>
        public bool StartAmbience(string resourcePath, float fadeSeconds)
        {
            if (disposed) return false;
            AudioClip clip = Load(resourcePath);
            if (clip == null) return false;
            if (AmbienceClip != clip)
            {
                AmbienceSource.Stop();
                AmbienceSource.clip = clip;
                AmbienceSource.volume = 0f;
                AmbienceSource.Play();
                AmbienceClip = clip;
            }
            FadeAmbience(1f, fadeSeconds);
            return true;
        }

        /// <summary>Fades the loop out over <paramref name="fadeSeconds"/> (0 = at once), then stops it.</summary>
        public void StopAmbience(float fadeSeconds)
        {
            if (disposed || AmbienceClip == null) return;
            FadeAmbience(0f, fadeSeconds);
        }

        /// <summary>Advances the loop's fade on real time.</summary>
        public void Tick(float realDelta)
        {
            if (disposed || AmbienceClip == null) return;
            ambienceElapsed = Mathf.Min(ambienceSeconds, ambienceElapsed + Mathf.Max(0f, realDelta));
            ApplyAmbience();
        }

        /// <summary>Silences everything now.</summary>
        public void Stop()
        {
            if (disposed) return;
            EffectSource.Stop();
            AmbienceSource.Stop();
            AmbienceSource.clip = null;
            AmbienceSource.volume = 0f;
            AmbienceClip = LastSound = null;
            ambienceFrom = ambienceTo = ambienceElapsed = ambienceSeconds = 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }

        private void FadeAmbience(float target, float seconds)
        {
            ambienceFrom = AmbienceSource.volume;
            ambienceTo = target;
            ambienceElapsed = 0f;
            ambienceSeconds = Mathf.Max(0f, seconds);
            ApplyAmbience();
        }

        private void ApplyAmbience()
        {
            float t = ambienceSeconds > 0f ? ambienceElapsed / ambienceSeconds : 1f;
            AmbienceSource.volume = Mathf.Lerp(ambienceFrom, ambienceTo, t);
            if (t < 1f || ambienceTo > 0f) return;
            AmbienceSource.Stop();
            AmbienceSource.clip = null;
            AmbienceClip = null;
        }

        private AudioSource CreateVoice(string name, bool loop)
        {
            var voice = new GameObject(name);
            voice.transform.SetParent(root.transform, false);
            var source = voice.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.volume = loop ? 0f : 1f;
            return source;
        }

        private static AudioClip Load(string resourcePath)
            => string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<AudioClip>(resourcePath);
    }
}
