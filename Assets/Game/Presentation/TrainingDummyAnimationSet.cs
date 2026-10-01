using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>The straw training dummy (a friend's asset, Docs/Art/TrainingDummy): an 8-cel idle sway and a 10-cel
    /// hurt reaction drawn as being struck from the left. It has no attack or guard cels; the opening mission's dummy
    /// only ever stands or gets hit. Frames keep the authored timing.</summary>
    public sealed class TrainingDummyAnimationSet : IDisposable
    {
        public const string ResourceRoot = "TrainingDummy/Animations/";
        public const int IdleFrameCount = 8, HurtFrameCount = 10;
        public const int RequiredSpriteCount = IdleFrameCount + HurtFrameCount;
        private static readonly int[] IdleDurations = { 180, 180, 180, 180, 180, 180, 180, 180 };
        private static readonly int[] HurtDurations = { 35, 45, 55, 70, 65, 65, 65, 80, 100, 120 };
        /// <summary>The idle loop length (1.44 s).</summary>
        public static readonly float IdleDuration = Total(IdleDurations);
        /// <summary>The hurt reaction length (0.70 s); its last cel matches the first idle cel.</summary>
        public static readonly float HurtDuration = Total(HurtDurations);
        private readonly Sprite[] idle = new Sprite[IdleFrameCount];
        private readonly Sprite[] hurt = new Sprite[HurtFrameCount];
        private readonly List<Sprite> owned = new List<Sprite>();
        private readonly List<string> missing = new List<string>();
        private bool disposed;

        public bool HasRequiredAssets => !disposed && missing.Count == 0 && owned.Count == RequiredSpriteCount;
        public IReadOnlyList<string> MissingResources => missing;

        public TrainingDummyAnimationSet()
        {
            for (int i = 0; i < idle.Length; i++) idle[i] = Load("idle/frame-" + (i + 1).ToString("00"));
            for (int i = 0; i < hurt.Length; i++) hurt[i] = Load("hurt/frame-" + (i + 1).ToString("00"));
        }

        public Sprite GetIdle(float time) => disposed ? null : Sample(idle, IdleDurations, Mathf.Repeat(Mathf.Max(0f, time), IdleDuration));

        /// <summary>The hurt cel <paramref name="elapsed"/> seconds into the reaction; holds the last cel afterwards.</summary>
        public Sprite GetHurt(float elapsed) => disposed ? null : Sample(hurt, HurtDurations, Mathf.Max(0f, elapsed));

        private static Sprite Sample(Sprite[] frames, int[] durations, float time)
        {
            float end = 0f;
            for (int i = 0; i < frames.Length - 1; i++)
            {
                end += durations[i] / 1000f;
                if (time + .000001f < end) return frames[i];
            }
            return frames[frames.Length - 1];
        }

        private static float Total(int[] durations)
        {
            int total = 0;
            foreach (int duration in durations) total += duration;
            return total / 1000f;
        }

        private Sprite Load(string path)
        {
            Sprite source = Resources.Load<Sprite>(ResourceRoot + path);
            if (source == null) { missing.Add(ResourceRoot + path); return null; }
            // The same ground shift as the other duel characters, so the stand's base meets the shadow.
            Vector2 pivot = source.pivot;
            pivot.y -= MobStudentAnimationSet.GroundOffset * source.pixelsPerUnit;
            Sprite sprite = Sprite.Create(source.texture, source.rect,
                new Vector2(pivot.x / source.rect.width, pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "dummy-" + path.Replace('/', '-');
            sprite.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Sprite sprite in owned)
                if (Application.isPlaying) Object.Destroy(sprite); else Object.DestroyImmediate(sprite);
            owned.Clear();
        }
    }
}
