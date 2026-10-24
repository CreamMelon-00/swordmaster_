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
        public const int GuardVariationCount = 3;
        private readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
        private readonly List<Sprite> owned = new List<Sprite>();
        private readonly Dictionary<int, Binding> bindings = new Dictionary<int, Binding>();
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
            var bindingAsset = Resources.Load<TextAsset>(ResourceRoot + "bindings");
            var mapping = bindingAsset != null ? JsonUtility.FromJson<BindingManifest>(bindingAsset.text) : null;
            if (mapping?.bindings == null) return;
            foreach (var entry in mapping.bindings)
            {
                if (entry == null || !CompatibleKey(entry.property, entry.key) || !clips.ContainsKey(entry.key) || bindings.ContainsKey(entry.id)) return;
                bindings.Add(entry.id, entry);
            }
            IsReady = bindings.Count > 0 && clips.Count == 15 && clips.ContainsKey("idle") && clips.ContainsKey("slash") &&
                clips.ContainsKey("pierce") && clips.ContainsKey("beat") && clips.ContainsKey("loaded") &&
                clips.ContainsKey("measured") && clips.ContainsKey("evade") &&
                clips.ContainsKey("guard-01") && clips.ContainsKey("guard-02") && clips.ContainsKey("guard-03") &&
                clips.ContainsKey("hurt") && clips.ContainsKey("break") &&
                clips.ContainsKey("walk") && clips.ContainsKey("run") && clips.ContainsKey("combat-move");
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

        public Sprite GetGuard(int variant) => IsReady
            ? clips["guard-" + (Mathf.Clamp(variant, 0, GuardVariationCount - 1) + 1).ToString("00")].frames[0] : null;

        public Sprite GetHurt() => IsReady ? clips["hurt"].frames[0] : null;
        public Sprite GetBreak() => IsReady ? clips["break"].frames[0] : null;
        public Sprite GetCombatMove() => IsReady ? clips["combat-move"].frames[0] : null;
        public float LocomotionDuration(bool running) => IsReady ? clips[running ? "run" : "walk"].duration : 1f;
        public Sprite GetLocomotion(bool running, float phase) => IsReady
            ? clips[running ? "run" : "walk"].Sample(Mathf.Repeat(phase, 1f) * LocomotionDuration(running)) : null;

        public Sprite GetAttack(LegacySkillProperty property, int skillId, float normalizedTime)
        {
            return SampleAttack(AttackKey(property, skillId), normalizedTime);
        }

        public string ResolveAttackKey(LegacySkill skill)
        {
            if (skill == null || skill.Kind != LegacySkillKind.Attack) return null;
            // Property wins over a stale/mismatched ID or animation label: a Hit can never select Slash.
            if (bindings.TryGetValue(skill.Id, out var binding) && binding.property == skill.Property)
                return binding.key;
            return AttackKey(skill.Property, skill.Id);
        }

        public Sprite GetAttack(LegacySkill skill, float normalizedTime)
            => SampleAttack(ResolveAttackKey(skill), normalizedTime);

        private static bool CompatibleKey(LegacySkillProperty property, string key) =>
            property == LegacySkillProperty.Slash ? key == "slash" :
            property == LegacySkillProperty.Penetrate ? key == "pierce" :
            property == LegacySkillProperty.Hit && (key == "beat" || key == "loaded" || key == "measured" || key == "evade");

        private Sprite SampleAttack(string key, float normalizedTime)
        {
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
            bindings.Clear();
        }

        [Serializable] private sealed class BindingManifest { public Binding[] bindings; }
        [Serializable] private sealed class Binding
        {
            public int id;
            public LegacySkillProperty property;
            public string key;
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
