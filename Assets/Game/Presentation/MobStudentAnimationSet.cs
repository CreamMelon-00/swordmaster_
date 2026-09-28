using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Full-body SwordGirl cels, sampled on the existing authoritative combat clock.
    /// The historical class/API name is retained for arena callers; no separate lower body is rendered.</summary>
    public sealed class MobStudentAnimationSet : IDisposable
    {
        public const float PixelsPerUnit = 40f;
        public const float GroundOffset = -2.23f;
        public const int IdleFrameCount = 8;
        public const int AttackFrameCount = 12;
        public const int SlashVariationCount = 3;
        public const int PierceVariationCount = 3;
        public const int BluntVariationCount = 3;
        public const int ReactionVariationCount = 2;
        public const int RequiredSpriteCount = 120;
        // Retained for the inactive legacy lower-body travel clock in LegacyArenaView.
        public const float MoveFrameDuration = .14f;
        public const string ResourceRoot = "SwordGirl/Animations/";
        private readonly Dictionary<string, Sequence> clips = new Dictionary<string, Sequence>();
        private readonly List<Sprite> ownedSprites = new List<Sprite>(RequiredSpriteCount);
        private readonly List<string> missingResources = new List<string>();
        private bool disposed;
        private readonly Sprite[] blockPoses = new Sprite[ReactionVariationCount];
        private readonly Sprite[] hurtPoses = new Sprite[ReactionVariationCount];

        public bool UsesFullBodyFrames => true;
        public bool HasRequiredAssets => !disposed && clips.Count == 10 &&
            ownedSprites.Count == RequiredSpriteCount && missingResources.Count == 0;
        public int LoadedSpriteCount => ownedSprites.Count;
        public IReadOnlyList<string> MissingResources => missingResources;

        public MobStudentAnimationSet()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceRoot + "timing");
            if (asset == null) { missingResources.Add(ResourceRoot + "timing"); return; }
            Manifest manifest;
            try { manifest = JsonUtility.FromJson<Manifest>(asset.text); }
            catch (Exception error) { missingResources.Add("Invalid SwordGirl timing: " + error.Message); return; }
            if (manifest?.clips == null) { missingResources.Add("SwordGirl timing has no clips"); return; }
            foreach (string key in new[] { "idle", "slash", "slash-2", "slash-3", "pierce", "pierce-2", "pierce-3", "blunt", "blunt-2", "blunt-3" })
            {
                ClipDefinition definition = Array.Find(manifest.clips, item => item != null && item.key == key);
                int expected = key == "idle" ? IdleFrameCount : AttackFrameCount;
                if (definition?.durationsMs == null || definition.durationsMs.Length != expected ||
                    Array.Exists(definition.durationsMs, duration => duration <= 0) ||
                    (key != "idle" && (definition.impactFrame < 2 || definition.impactFrame >= expected)))
                { missingResources.Add("Invalid SwordGirl clip: " + key); continue; }
                var sequence = new Sequence(definition);
                for (int i = 0; i < expected; i++) sequence.Frames[i] = Load(key + "/frame-" + (i + 1).ToString("00"));
                clips.Add(key, sequence);
            }
            blockPoses[0] = Load("poses/block");
            blockPoses[1] = Load("poses/block-2");
            hurtPoses[0] = Load("poses/hurt");
            hurtPoses[1] = Load("poses/hurt-2");
        }

        public Sprite GetBlockPose(int variant = 0) => disposed ? null : blockPoses[Mathf.Clamp(variant, 0, ReactionVariationCount - 1)];
        public Sprite GetHurtPose(int variant = 0) => disposed ? null : hurtPoses[Mathf.Clamp(variant, 0, ReactionVariationCount - 1)];

        // Historical upper-body API now returns the complete character.
        public Sprite GetIdleUpper(float elapsed)
        {
            if (disposed || !clips.TryGetValue("idle", out var clip)) return null;
            return clip.Sample(Mathf.Max(0f, elapsed) % clip.Duration);
        }

        public Sprite GetLower(float elapsed, bool moving, bool retreating = false) => null;

        public Sprite GetAttackUpper(LegacySkillProperty property, int variantIndex, float normalizedTime)
        {
            string key = AttackKey(property, variantIndex);
            if (disposed || key == null || !clips.TryGetValue(key, out var clip)) return null;
            float phase = Mathf.Clamp01(normalizedTime);
            // Map the authored contact (frame 5 at 420 ms) to the game's half-cycle strike.
            // This preserves hit stop, combo gaps, playback multipliers, and authoritative damage timing.
            float elapsed = phase <= .5f ? phase * 2f * clip.ImpactTime
                : clip.ImpactTime + (phase - .5f) * 2f * (clip.Duration - clip.ImpactTime);
            return clip.Sample(elapsed);
        }

        public static int AttackVariationCount(LegacySkillProperty property) =>
            property == LegacySkillProperty.Slash ? SlashVariationCount :
            property == LegacySkillProperty.Penetrate ? PierceVariationCount :
            property == LegacySkillProperty.Hit ? BluntVariationCount : 0;

        public static string AttackKey(LegacySkillProperty property, int variantIndex)
        {
            switch (property)
            {
                case LegacySkillProperty.Slash:
                    int variant = Mathf.Max(0, variantIndex) % SlashVariationCount;
                    return variant == 0 ? "slash" : "slash-" + (variant + 1);
                case LegacySkillProperty.Penetrate:
                    int pierceVariant = Mathf.Max(0, variantIndex) % PierceVariationCount;
                    return pierceVariant == 0 ? "pierce" : "pierce-" + (pierceVariant + 1);
                case LegacySkillProperty.Hit:
                    int bluntVariant = Mathf.Max(0, variantIndex) % BluntVariationCount;
                    return bluntVariant == 0 ? "blunt" : "blunt-" + (bluntVariant + 1);
                default: return null;
            }
        }

        private Sprite Load(string relativePath)
        {
            string path = ResourceRoot + relativePath;
            Sprite source = Resources.Load<Sprite>(path);
            if (source == null) { missingResources.Add(path); return null; }
            Vector2 pivot = source.pivot;
            pivot.y -= GroundOffset * source.pixelsPerUnit;
            var sprite = Sprite.Create(source.texture, source.rect,
                new Vector2(pivot.x / source.rect.width, pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = relativePath.Replace('/', '-');
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ownedSprites.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Sprite sprite in ownedSprites)
            {
                if (Application.isPlaying) Object.Destroy(sprite);
                else Object.DestroyImmediate(sprite);
            }
            ownedSprites.Clear();
            clips.Clear();
        }

        [Serializable] private sealed class Manifest { public ClipDefinition[] clips; }
        [Serializable] private sealed class ClipDefinition
        {
            public string key;
            public int impactFrame;
            public int[] durationsMs;
        }
        private sealed class Sequence
        {
            public readonly Sprite[] Frames;
            public readonly float Duration;
            public readonly float ImpactTime;
            private readonly float[] ends;
            public Sequence(ClipDefinition definition)
            {
                Frames = new Sprite[definition.durationsMs.Length];
                ends = new float[Frames.Length];
                float time = 0f;
                for (int i = 0; i < Frames.Length; i++)
                {
                    if (i == definition.impactFrame - 1) ImpactTime = time;
                    time += definition.durationsMs[i] / 1000f;
                    ends[i] = time;
                }
                Duration = time;
            }
            public Sprite Sample(float time)
            {
                // Tolerate a few float ULPs at exact multi-hit boundaries, not a visible time span.
                for (int i = 0; i < ends.Length - 1; i++)
                    if (time + .000001f < ends[i]) return Frames[i];
                return Frames[Frames.Length - 1];
            }
        }
    }
}
