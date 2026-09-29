using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>School-uniform enemy cels on the arena's authoritative combat clock.</summary>
    public sealed class EnemyStudentAnimationSet : IDisposable
    {
        public const string ResourceRoot = "EnemyStudent/Animations/";
        public const int RequiredSpriteCount = 118;
        public const int AttackVariationCount = 3;
        private static readonly int[] IdleDurations = { 180, 140, 160, 140, 160, 140, 160, 180 };
        private static readonly int[] AttackDurations = { 140, 100, 120, 60, 70, 90, 70, 100, 90, 90, 130, 200 };
        private readonly Dictionary<string, Sprite[]> clips = new Dictionary<string, Sprite[]>();
        private readonly List<Sprite> owned = new List<Sprite>();
        private readonly List<string> missing = new List<string>();
        private readonly Sprite guard;
        private readonly Sprite hurt;
        private bool disposed;
        public bool HasRequiredAssets => !disposed && missing.Count == 0 && owned.Count == RequiredSpriteCount;
        public IReadOnlyList<string> MissingResources => missing;
        public int LoadedSpriteCount => owned.Count;

        public EnemyStudentAnimationSet()
        {
            foreach (string key in new[] { "idle", "slash", "slash-2", "slash-3", "pierce", "pierce-2", "pierce-3", "blunt", "blunt-2", "blunt-3" })
            {
                var frames = new Sprite[key == "idle" ? 8 : 12];
                for (int i = 0; i < frames.Length; i++) frames[i] = Load(key + "/frame-" + (i + 1).ToString("00"));
                clips.Add(key, frames);
            }
            guard = Load("poses/block");
            hurt = Load("poses/hurt");
        }

        public Sprite GetGuard() => disposed ? null : guard;
        public Sprite GetHurt() => disposed ? null : hurt;
        public Sprite GetIdle(float time) => disposed ? null : Sample(clips["idle"], IdleDurations, Mathf.Max(0f, time) % 1.26f);
        public Sprite GetAttack(LegacySkillProperty property, float phase, int variantIndex = 0)
        {
            if (disposed) return null;
            string key = AttackKey(property, variantIndex);
            if (key == null) return null;
            phase = Mathf.Clamp01(phase);
            // Contact cel 5 at authored 420 ms must coincide with the existing half-cycle hit.
            float time = phase <= .5f ? phase * 2f * .42f : .42f + (phase - .5f) * 2f * .84f;
            return Sample(clips[key], AttackDurations, time);
        }

        public static string AttackKey(LegacySkillProperty property, int variantIndex)
        {
            string key = property == LegacySkillProperty.Slash ? "slash" :
                property == LegacySkillProperty.Penetrate ? "pierce" : property == LegacySkillProperty.Hit ? "blunt" : null;
            if (key == null) return null;
            int variant = Mathf.Max(0, variantIndex) % AttackVariationCount;
            return variant == 0 ? key : key + "-" + (variant + 1);
        }

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

        private Sprite Load(string path)
        {
            Sprite source = Resources.Load<Sprite>(ResourceRoot + path);
            if (source == null) { missing.Add(ResourceRoot + path); return null; }
            Vector2 pivot = source.pivot;
            pivot.y -= MobStudentAnimationSet.GroundOffset * source.pixelsPerUnit;
            Sprite sprite = Sprite.Create(source.texture, source.rect,
                new Vector2(pivot.x / source.rect.width, pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "enemy-" + path.Replace('/', '-');
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
            owned.Clear(); clips.Clear();
        }
    }
}
