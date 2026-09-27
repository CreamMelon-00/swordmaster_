using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>Two sprite layers share one canvas; combat remains authoritative over attack timing.</summary>
    public sealed class MobStudentAnimationSet : IDisposable
    {
        public const float PixelsPerUnit = 150f;
        public const float IdleFrameDuration = .14f;
        public const float MoveFrameDuration = .14f;
        public const int AttackFrameCount = 4;
        public const int AttackVariationCount = 3;
        public const int RequiredSpriteCount = 49;
        public const float GroundOffset = -2.23f;
        private const string ResourceRoot = "MobStudent/Animations/";
        private static readonly string[,] AttackKeys =
        {
            { "slash-1", "slash-2", "slash-3" },
            { "pierce-1", "pierce-2", "pierce-3" },
            { "blunt-1", "blunt-2", "blunt-3" }
        };
        private readonly Sprite[] idleUpper = new Sprite[4];
        private readonly Sprite[] movingLower = new Sprite[8];
        private readonly Dictionary<string, Sprite[]> attacks = new Dictionary<string, Sprite[]>(9);
        private readonly List<Sprite> ownedSprites = new List<Sprite>(RequiredSpriteCount);
        private readonly List<string> missingResources = new List<string>();
        private readonly Sprite idleLower;
        private bool disposed;

        public bool HasRequiredAssets => !disposed && ownedSprites.Count == RequiredSpriteCount && missingResources.Count == 0;
        public int LoadedSpriteCount => ownedSprites.Count;
        public IReadOnlyList<string> MissingResources => missingResources;

        public MobStudentAnimationSet()
        {
            for (int frame = 0; frame < idleUpper.Length; frame++)
                idleUpper[frame] = Load("idle/upper-" + (frame + 1));
            idleLower = Load("idle/lower-1");
            for (int frame = 0; frame < movingLower.Length; frame++)
                movingLower[frame] = Load("move/lower-" + (frame + 1));
            foreach (string type in new[] { "slash", "pierce", "blunt" })
            {
                for (int hit = 1; hit <= AttackVariationCount; hit++)
                {
                    string key = type + "-" + hit;
                    var frames = new Sprite[AttackFrameCount];
                    for (int frame = 0; frame < frames.Length; frame++)
                        frames[frame] = Load(key + "/upper-" + (frame + 1));
                    attacks.Add(key, frames);
                }
            }
        }

        public Sprite GetIdleUpper(float elapsed) => disposed ? null
            : idleUpper[LoopFrame(elapsed, IdleFrameDuration, idleUpper.Length)];

        public Sprite GetLower(float elapsed, bool moving, bool retreating = false)
        {
            if (disposed) return null;
            if (!moving) return idleLower;
            int frame = LoopFrame(elapsed, MoveFrameDuration, movingLower.Length);
            // Reverse the footwork cycle for retreat; the torso and both shoes keep facing the opponent.
            if (retreating) frame = (movingLower.Length - frame) % movingLower.Length;
            return movingLower[frame];
        }

        public Sprite GetAttackUpper(LegacySkillProperty property, int hitIndex, float normalizedTime)
        {
            if (disposed) return null;
            string key = AttackKey(property, hitIndex);
            if (key == null || !attacks.TryGetValue(key, out var frames)) return null;
            // Modulo on a later combo hit can put an exact contact a few float ULPs below .5.
            int frame = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(normalizedTime) * frames.Length + .00001f), 0, frames.Length - 1);
            return frames[frame];
        }

        public static string AttackKey(LegacySkillProperty property, int hitIndex)
        {
            int type;
            switch (property)
            {
                case LegacySkillProperty.Slash: type = 0; break;
                case LegacySkillProperty.Penetrate: type = 1; break;
                case LegacySkillProperty.Hit: type = 2; break;
                default: return null;
            }
            return AttackKeys[type, Mathf.Max(0, hitIndex) % AttackVariationCount];
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
            // Imported art retains its foot pivot. Adapt only the render origin to the legacy actor's
            // centre and ground shadow, keeping combat/HUD transforms and the shared source texture intact.
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

        private static int LoopFrame(float elapsed, float frameDuration, int frameCount) =>
            Mathf.FloorToInt(Mathf.Max(0f, elapsed) / frameDuration) % frameCount;

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
        }
    }
}
