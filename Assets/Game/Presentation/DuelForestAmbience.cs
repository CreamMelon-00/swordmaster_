using System;
using TurnLimbo.Runtime.Cutscene;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>The forest's ambience bed (<c>Sfx/forest-ambience-loop</c>, a generated placeholder: soft wind, leaves,
    /// sparse far birds): one quiet 2D loop under every battle and every scene on the forest arena, faded in and out on
    /// real time, and stopped once it has faded out on the screens away from the forest (title, lobby, briefing). A black
    /// screen silences it with the picture, and the story's own loop (a scene's <c>@ambience</c>, the 수훈 hum) takes
    /// precedence, the bed ducking under it (<see cref="LegacyAmbienceMix"/>). Each start picks a new place in the loop,
    /// so it never opens on the same bird. Its volume is kept here, not read back from the source, so it holds where
    /// audio is disabled (batch runs). A missing clip leaves it silent.</summary>
    public sealed class DuelForestAmbience : IDisposable
    {
        public const string ClipResource = CutsceneStep.SoundFolder + "forest-ambience-loop";

        private readonly Func<DuelPresentationSettings> settings;
        private readonly GameObject root;
        private readonly AudioSource source;
        private readonly AudioClip clip;
        private readonly System.Random random = new System.Random();
        private bool playing, disposed;

        /// <param name="settings">Read when used, so live tuning (and a test's clone) applies at once.</param>
        public DuelForestAmbience(Transform parent, Func<DuelPresentationSettings> settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            root = new GameObject("Forest Ambience");
            if (parent != null) root.transform.SetParent(parent, false);
            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            clip = Resources.Load<AudioClip>(ClipResource);
        }

        /// <summary>The bed's clip, or null when Resources has none (then it stays silent).</summary>
        public AudioClip Clip => clip;
        /// <summary>Whether the bed is playing (or fading out).</summary>
        public bool IsPlaying => !disposed && playing;
        /// <summary>The bed's volume now.</summary>
        public float Volume { get; private set; }
        /// <summary>The volume it is fading toward.</summary>
        public float TargetVolume { get; private set; }

        /// <summary>A frame of real time, whatever is on screen.</summary>
        /// <param name="isForest">The forest is the scene: a battle, its result, a scene on the forest arena.</param>
        /// <param name="visibility">How much of the picture shows, 0 (black) to 1.</param>
        /// <param name="storyLoop">How loud the story's own loop is (a scene's @ambience, the 수훈 hum), 0 to 1.</param>
        public void Tick(float realDelta, bool isForest, float visibility, float storyLoop)
        {
            if (disposed) return;
            DuelPresentationSettings tuning = settings();
            float volume = tuning.ForestAmbienceVolume;
            TargetVolume = clip == null ? 0f
                : LegacyAmbienceMix.Target(isForest, volume, visibility, storyLoop, tuning.ForestAmbienceSceneDuck);
            Volume = LegacyAmbienceMix.Step(Volume, TargetVolume, realDelta, tuning.ForestAmbienceFadeSeconds, volume);
            if (!playing && Volume > 0f) Start();
            source.volume = Volume;
            if (playing && Volume <= 0f && TargetVolume <= 0f) Stop();
        }

        /// <summary>Silent at once (the controller is going away).</summary>
        public void Stop()
        {
            if (disposed) return;
            playing = false;
            Volume = 0f;
            source.Stop();
            source.clip = null;
            source.volume = 0f;
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

        private void Start()
        {
            playing = true;
            source.clip = clip;
            source.Play();
            // Somewhere new in the loop each time it comes back.
            if (clip.length > 0f) source.time = (float)random.NextDouble() * clip.length * .95f;
        }
    }
}
