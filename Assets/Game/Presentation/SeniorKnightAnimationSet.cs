using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>The 상급기사's temporary look in cutscenes: the original layered MobStudent cels (an upper body over
    /// standing or walking legs, one four-cel stroke per attack type) until he has art of his own. There is no hurt
    /// or guard pose. Drawn facing right, standing on the same ground as the fighters.</summary>
    public sealed class SeniorKnightAnimationSet : IDisposable
    {
        public const float FrameDuration = .14f;
        public const int IdleFrameCount = 4;
        public const int MoveFrameCount = 8;
        public const int AttackFrameCount = 4;
        public const int RequiredSpriteCount = IdleFrameCount + 1 + MoveFrameCount + 3 * AttackFrameCount;
        public const string ResourceRoot = "MobStudent/Animations/";
        private readonly Sprite[] idleUpper = new Sprite[IdleFrameCount];
        private readonly Sprite[] movingLower = new Sprite[MoveFrameCount];
        private readonly Dictionary<LegacySkillProperty, Sprite[]> attacks = new Dictionary<LegacySkillProperty, Sprite[]>(3);
        private readonly List<Sprite> ownedSprites = new List<Sprite>(RequiredSpriteCount);
        private readonly List<string> missingResources = new List<string>();
        private readonly Sprite idleLower;
        private bool disposed;

        public bool HasRequiredAssets => !disposed && ownedSprites.Count == RequiredSpriteCount && missingResources.Count == 0;
        public IReadOnlyList<string> MissingResources => missingResources;

        public SeniorKnightAnimationSet()
        {
            for (int frame = 0; frame < idleUpper.Length; frame++) idleUpper[frame] = Load("idle/upper-" + (frame + 1));
            idleLower = Load("idle/lower-1");
            for (int frame = 0; frame < movingLower.Length; frame++) movingLower[frame] = Load("move/lower-" + (frame + 1));
            foreach (var attack in new[]
            {
                (LegacySkillProperty.Slash, "slash-1"), (LegacySkillProperty.Penetrate, "pierce-1"), (LegacySkillProperty.Hit, "blunt-1"),
            })
            {
                var frames = new Sprite[AttackFrameCount];
                for (int frame = 0; frame < frames.Length; frame++) frames[frame] = Load(attack.Item2 + "/upper-" + (frame + 1));
                attacks.Add(attack.Item1, frames);
            }
        }

        public Sprite GetIdleUpper(float elapsed) => disposed ? null : idleUpper[LoopFrame(elapsed, idleUpper.Length)];

        /// <summary>The legs: standing still, or the walking cycle while he travels.</summary>
        public Sprite GetLower(float elapsed, bool moving)
            => disposed ? null : moving ? movingLower[LoopFrame(elapsed, movingLower.Length)] : idleLower;

        /// <param name="phase">The stroke's progress, 0 to 1; the blade connects at 0.5 (the third cel).</param>
        public Sprite GetAttackUpper(LegacySkillProperty property, float phase)
        {
            if (disposed || !attacks.TryGetValue(property, out Sprite[] frames)) return null;
            int frame = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(phase) * frames.Length + .00001f), 0, frames.Length - 1);
            return frames[frame];
        }

        private Sprite Load(string relativePath)
        {
            string path = ResourceRoot + relativePath;
            Sprite source = Resources.Load<Sprite>(path);
            if (source == null)
            {
                missingResources.Add(path);
                return null;
            }
            // The art keeps its foot pivot; move the origin up to the fighters' centre so his feet meet the ground
            // and their shadows (the same adaptation the fighters' art gets).
            Vector2 pivot = source.pivot;
            pivot.y -= MobStudentAnimationSet.GroundOffset * source.pixelsPerUnit;
            var sprite = Sprite.Create(source.texture, source.rect,
                new Vector2(pivot.x / source.rect.width, pivot.y / source.rect.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = "senior-" + relativePath.Replace('/', '-');
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ownedSprites.Add(sprite);
            return sprite;
        }

        private static int LoopFrame(float elapsed, int frameCount)
            => Mathf.FloorToInt(Mathf.Max(0f, elapsed) / FrameDuration) % frameCount;

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
            attacks.Clear();
        }
    }
}
