using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>The transparent, one-shot shield drawn and exported in PixelLab. No actor pixels are changed.</summary>
    internal sealed class DuelBreakShieldClip : IDisposable
    {
        public const float FramesPerSecond = 24f;
        public const float WorldWidth = 3.55f;
        private readonly Sprite[] frames;
        public bool IsReady => frames.Length > 0;
        public float Duration => frames.Length / FramesPerSecond;

        public DuelBreakShieldClip()
        {
            var texture = Resources.Load<Texture2D>("DuelVFX/ShieldBreak/shield-shatter");
            var data = Resources.Load<TextAsset>("DuelVFX/ShieldBreak/shield-shatter-frames");
            var manifest = data != null ? JsonUtility.FromJson<Manifest>(data.text) : null;
            if (texture == null || manifest?.frames == null || manifest.frames.Length != 14)
            {
                frames = Array.Empty<Sprite>();
                Debug.LogWarning("PixelLab shield-break sheet or its 14-frame manifest is missing.");
                return;
            }
            frames = new Sprite[manifest.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                FrameRect rect = manifest.frames[i].frame;
                frames[i] = Sprite.Create(texture,
                    new Rect(rect.x, texture.height - rect.y - rect.h, rect.w, rect.h),
                    Vector2.one * .5f, 36f, 0, SpriteMeshType.FullRect);
                frames[i].name = "shield-shatter-" + i.ToString("00");
            }
        }

        public Sprite Sample(float elapsed) => IsReady
            ? frames[Mathf.Clamp(Mathf.FloorToInt(elapsed * FramesPerSecond), 0, frames.Length - 1)] : null;

        public void Dispose()
        {
            foreach (Sprite frame in frames)
                if (Application.isPlaying) Object.Destroy(frame);
                else Object.DestroyImmediate(frame);
        }

        [Serializable] private sealed class Manifest { public Frame[] frames; }
        [Serializable] private sealed class Frame { public FrameRect frame; }
        [Serializable] private sealed class FrameRect { public int x, y, w, h; }
    }
}
