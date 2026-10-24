using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>PixelLab full-body clips on the arena's authoritative per-hit clock.</summary>
    public sealed class FinalPlayerAnimationSet : IDisposable
    {
        public const string ResourceRoot = "SwordGirl/FinalPlayer/";
        private readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        private readonly List<Sprite> owned = new List<Sprite>();
        public bool IsReady { get; private set; }
        public int LoadedFrameCount => owned.Count;

        public FinalPlayerAnimationSet()
        {
            var manifest = Resources.Load<TextAsset>(ResourceRoot + "timing");
            if (manifest == null) return;
            var data = JsonUtility.FromJson<Manifest>(manifest.text);
            if (data?.clips == null) return;
            foreach (var definition in data.clips)
            {
                if (definition.durationsMs == null || definition.durationsMs.Length == 0 ||
                    Array.Exists(definition.durationsMs, ms => ms <= 0) ||
                    definition.impactIndex < 0 || definition.impactIndex >= definition.durationsMs.Length) return;
                var clip = new Clip(definition);
                for (int i = 0; i < clip.frames.Length; i++)
                {
                    var source = Resources.Load<Sprite>(ResourceRoot + definition.key + "/frame-" + i.ToString("00"));
                    if (source == null) return;
                    Vector2 pivot = source.pivot;
                    pivot.y -= MobStudentAnimationSet.GroundOffset * source.pixelsPerUnit;
                    var sprite = Sprite.Create(source.texture, source.rect,
                        new Vector2(pivot.x / source.rect.width, pivot.y / source.rect.height),
                        source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = "final-" + definition.key + "-frame-" + i.ToString("00");
                    sprite.hideFlags = HideFlags.HideAndDontSave;
                    owned.Add(sprite);
                    clip.frames[i] = sprite;
                }
                clips.Add(definition.key, clip);
            }
            IsReady = clips.Count == 7 && clips.ContainsKey("idle") && clips.ContainsKey("slash") &&
                clips.ContainsKey("pierce") && clips.ContainsKey("beat") && clips.ContainsKey("loaded") &&
                clips.ContainsKey("measured") && clips.ContainsKey("evade");
        }

        public static string AttackKey(LegacySkillProperty property, int skillId)
        {
            if (property == LegacySkillProperty.Slash) return "slash";
            if (property == LegacySkillProperty.Penetrate) return "pierce";
            if (property != LegacySkillProperty.Hit) return null;
            switch (skillId)
            {
                case 6: case 55: case 68: return "loaded";
                case 10: case 53: return "measured";
                case 54: return "evade";
                default: return "beat";
            }
        }

        public Sprite GetIdle(float elapsed) => IsReady ? clips["idle"].Sample(Mathf.Max(0, elapsed) % clips["idle"].duration) : null;

        public Sprite GetAttack(LegacySkillProperty property, int skillId, float normalizedTime)
        {
            string key = AttackKey(property, skillId);
            if (!IsReady || key == null) return null;
            var clip = clips[key];
            float phase = Mathf.Clamp01(normalizedTime);
            // Contact cel begins exactly at the existing half-cycle damage event.
            float time = phase <= .5f ? phase * 2f * clip.contact
                : clip.contact + (phase - .5f) * 2f * (clip.duration - clip.contact);
            return clip.Sample(time);
        }

        public void Dispose()
        {
            IsReady = false;
            foreach (var sprite in owned)
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            owned.Clear();
            clips.Clear();
        }

        [Serializable] private sealed class Manifest { public Definition[] clips; }
        [Serializable] private sealed class Definition
        {
            public string key;
            public int[] durationsMs;
            public int impactIndex;
        }
        private sealed class Clip
        {
            public readonly Sprite[] frames;
            public readonly float duration, contact;
            private readonly float[] ends;
            public Clip(Definition definition)
            {
                frames = new Sprite[definition.durationsMs.Length];
                ends = new float[frames.Length];
                float sum = 0;
                for (int i = 0; i < ends.Length; i++)
                {
                    if (i == definition.impactIndex) contact = sum;
                    sum += definition.durationsMs[i] / 1000f;
                    ends[i] = sum;
                }
                duration = sum;
            }
            public Sprite Sample(float time)
            {
                for (int i = 0; i < ends.Length - 1; i++)
                    if (time + .000001f < ends[i]) return frames[i];
                return frames[frames.Length - 1];
            }
        }
    }
}
