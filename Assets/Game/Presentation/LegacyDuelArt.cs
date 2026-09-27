using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    /// <summary>Loads inherited duel art and the redesigned skill-button icons.</summary>
    public sealed class LegacyDuelArt : IDisposable
    {
        public const int SkillIconCount = 15;
        private const string ResourceRoot = "LegacyDuel/";
        private const string SkillIconAtlas = "SkillRoles/skill-role-atlas";
        private const float OriginalFramesPerSecond = 12f;
        private readonly Texture2D background;
        private readonly Sprite backgroundSprite;
        private readonly Dictionary<string, Sprite[]> playerFrames;
        private readonly Dictionary<string, Sprite[]> enemyFrames;
        private readonly Sprite[] skillIcons = new Sprite[SkillIconCount + 1];
        private readonly AudioClip[] selectionSounds = new AudioClip[3];
        private readonly AudioClip hitSound;
        private readonly AudioClip secondHitSound;
        private readonly AudioClip parrySound;
        private Texture2D breathTexture;
        private Sprite breathIcon;
        private bool disposed;

        public Font UIFont { get; }
        public Sprite BackgroundSprite => backgroundSprite;

        public bool HasRequiredAssets
        {
            get
            {
                if (background == null || UIFont == null || !HasActorAssets(playerFrames) ||
                    !HasActorAssets(enemyFrames) || hitSound == null || secondHitSound == null || parrySound == null)
                    return false;
                for (int i = 1; i < skillIcons.Length; i++)
                    if (skillIcons[i] == null) return false;
                for (int i = 0; i < selectionSounds.Length; i++)
                    if (selectionSounds[i] == null) return false;
                return true;
            }
        }

        public LegacyDuelArt()
        {
            background = Resources.Load<Texture2D>(ResourceRoot + "Background/pa_background_-_school_in_game");
            backgroundSprite = Resources.Load<Sprite>(ResourceRoot + "Background/pa_background_-_school_in_game");
            UIFont = Resources.Load<Font>(ResourceRoot + "Font/neodgm");
            playerFrames = LoadActor("Player", "pa_player_idle-Sheet", "pa_player_slash-Sheet",
                "pa_player_sting1-Sheet", "pa_player_nike-Sheet", "pa_player_g-Sheet");
            enemyFrames = LoadActor("Enemy0", "pa_enemy_1_i-Sheet", "pa_enemy_1_s-Sheet",
                "pa_enemy_1_st-Sheet", "pa_enemy_1_n-Sheet", "pa_enemy_1_g-Sheet");

            var roleIcons = Resources.LoadAll<Sprite>(SkillIconAtlas);
            for (var skillId = 1; skillId <= SkillIconCount; skillId++)
                foreach (var icon in roleIcons)
                    if (icon.name == "role" + skillId) skillIcons[skillId] = icon;

            for (var lane = 0; lane < selectionSounds.Length; lane++)
                selectionSounds[lane] = Resources.Load<AudioClip>(ResourceRoot + "Audio/add_skill_" + (lane + 1));

            hitSound = Resources.Load<AudioClip>(ResourceRoot + "Audio/hit1");
            secondHitSound = Resources.Load<AudioClip>(ResourceRoot + "Audio/hit2");
            parrySound = Resources.Load<AudioClip>(ResourceRoot + "Audio/Parry");

            if (!HasRequiredAssets)
                Debug.LogWarning("Duel art or skill-button icons have not finished importing, or a required resource is missing.");
        }

        public void DrawBackground(Rect rect)
        {
            if (background != null)
                GUI.DrawTexture(rect, background, ScaleMode.ScaleAndCrop, true);
        }

        public Sprite GetPlayerSprite(float time, string action = "idle") => GetSprite(playerFrames, time, action);

        public Sprite GetEnemySprite(float time, string action = "idle") => GetSprite(enemyFrames, time, action);

        public Sprite GetSkillIcon(int skillId)
        {
            if (skillId == LegacyCommonActions.Breathe.IconId)
                return disposed ? null : breathIcon != null ? breathIcon : CreateBreathIcon();
            return skillId > 0 && skillId < skillIcons.Length ? skillIcons[skillId] : null;
        }

        private Sprite CreateBreathIcon()
        {
            // A pause mark means one idle queue slot, not a global game pause.
            // It shares the club's brass/paper palette without adding an atlas entry.
            const int size = 64;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= 3 && x <= 60 && y >= 3 && y <= 60 &&
                        x + y >= 12 && x + 63 - y >= 12 && 63 - x + y >= 12 && 126 - x - y >= 12;
                    if (!inside) continue;
                    bool rim = x < 7 || x > 56 || y < 7 || y > 56 ||
                        x + y < 18 || x + 63 - y < 18 || 63 - x + y < 18 || 126 - x - y < 18;
                    bool pause = y >= 18 && y <= 45 && ((x >= 20 && x <= 27) || (x >= 36 && x <= 43));
                    pixels[y * size + x] = pause ? DuelVisualTheme.Ink
                        : rim ? DuelVisualTheme.Accent : DuelVisualTheme.Paper;
                }
            }
            breathTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Breathing Queue Emblem",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            breathTexture.SetPixels(pixels);
            breathTexture.Apply(false, true);
            breathIcon = Sprite.Create(breathTexture, new Rect(0, 0, size, size), Vector2.one * .5f,
                size, 0, SpriteMeshType.FullRect);
            breathIcon.name = "Breathing Queue Emblem";
            breathIcon.hideFlags = HideFlags.HideAndDontSave;
            return breathIcon;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DestroyGenerated(breathIcon);
            DestroyGenerated(breathTexture);
            breathIcon = null;
            breathTexture = null;
        }

        private static void DestroyGenerated(UnityEngine.Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(generated);
            else UnityEngine.Object.DestroyImmediate(generated);
        }

        private static Sprite GetSprite(Dictionary<string, Sprite[]> animations, float time, string action)
        {
            var clipName = NormalizeAction(action);
            if (!animations.TryGetValue(clipName, out var frames) || frames.Length == 0)
                frames = animations["idle"];
            if (frames.Length == 0) return null;
            var frame = Mathf.FloorToInt(Mathf.Max(0f, time) * OriginalFramesPerSecond);
            return frames[clipName == "idle" ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1)];
        }

        /// <param name="time">Seconds elapsed in the current animation. Idle loops; other clips hold their last frame.</param>
        public void DrawPlayer(Rect rect, float time, string action = "idle")
        {
            DrawActor(rect, time, action, playerFrames, 144f);
        }

        /// <param name="time">Seconds elapsed in the current animation. Idle loops; other clips hold their last frame.</param>
        public void DrawEnemy(Rect rect, float time, string action = "idle")
        {
            DrawActor(rect, time, action, enemyFrames, 160f);
        }

        public void DrawSkillIcon(Rect rect, int skillId)
        {
            var sprite = GetSkillIcon(skillId);
            if (sprite == null) return;
            var scale = Mathf.Min(rect.width / sprite.rect.width, rect.height / sprite.rect.height);
            var size = sprite.rect.size * scale;
            DrawSprite(new Rect(rect.center - size * 0.5f, size), sprite);
        }

        public void PlaySelection(AudioSource source, int lane)
        {
            if (source != null && lane >= 0 && lane < selectionSounds.Length && selectionSounds[lane] != null)
                source.PlayOneShot(selectionSounds[lane], 0.45f);
        }

        public void PlayClash(AudioSource source, bool parried = false, bool alternateHit = false)
        {
            var clip = parried ? parrySound : alternateHit ? secondHitSound : hitSound;
            if (source != null && clip != null)
                source.PlayOneShot(clip, 0.55f);
        }

        private static Dictionary<string, Sprite[]> LoadActor(string actor, string idleSheet, string slashSheet,
            string thrustSheet, string hitSheet, string defenceSheet)
        {
            return new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "idle", LoadFrames(actor, idleSheet, 8) },
                { "slash", LoadFrames(actor, slashSheet, 2) },
                { "thrust", LoadFrames(actor, thrustSheet, 2) },
                { "hit", LoadFrames(actor, hitSheet, 2) },
                { "defence", LoadFrames(actor, defenceSheet, 2) }
            };
        }

        private static bool HasActorAssets(Dictionary<string, Sprite[]> animations)
        {
            foreach (var animation in animations)
                if (animation.Value.Length != (animation.Key == "idle" ? 8 : 2)) return false;
            return true;
        }

        private static Sprite[] LoadFrames(string actor, string sheet, int expectedCount)
        {
            var imported = Resources.LoadAll<Sprite>(ResourceRoot + actor + "/" + sheet);
            var ordered = new Sprite[expectedCount];
            // The original .anim files reference _0, _1, ... at 1/12-second intervals.
            // Import order is not guaranteed, so match the original frame names explicitly.
            for (var frame = 0; frame < expectedCount; frame++)
            {
                var expectedName = sheet + "_" + frame;
                for (var index = 0; index < imported.Length; index++)
                {
                    if (imported[index].name == expectedName)
                    {
                        ordered[frame] = imported[index];
                        break;
                    }
                }

                if (ordered[frame] == null)
                    return Array.Empty<Sprite>();
            }

            return ordered;
        }

        private static void DrawActor(Rect rect, float time, string action,
            Dictionary<string, Sprite[]> animations, float referenceWidth)
        {
            var clipName = NormalizeAction(action);
            var hurt = string.Equals(action, "hurt", StringComparison.OrdinalIgnoreCase);
            var dead = string.Equals(action, "death", StringComparison.OrdinalIgnoreCase);
            if (!animations.TryGetValue(clipName, out var frames) || frames.Length == 0)
                frames = animations["idle"];

            if (frames.Length == 0)
                return;

            var frame = Mathf.FloorToInt(Mathf.Max(0f, time) * OriginalFramesPerSecond);
            frame = clipName == "idle" ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1);
            var sprite = frames[frame];
            var scale = Mathf.Min(rect.width / referenceWidth, rect.height / 80f);
            // Preserve the original pivot: Enemy0 idle uses x=.65; attack frames use x=.5.
            var destination = new Rect(rect.center.x - sprite.pivot.x * scale,
                rect.yMax - sprite.rect.height * scale, sprite.rect.width * scale, sprite.rect.height * scale);
            var previousColor = GUI.color;
            if (hurt)
                GUI.color = previousColor * new Color(1f, 0.55f, 0.55f, 1f);
            else if (dead)
                GUI.color = previousColor * new Color(0.55f, 0.55f, 0.55f, 0.8f);

            DrawSprite(destination, sprite);
            GUI.color = previousColor;
        }

        private static string NormalizeAction(string action)
        {
            if (string.IsNullOrEmpty(action))
                return "idle";
            if (string.Equals(action, "Penetrate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "thrust", StringComparison.OrdinalIgnoreCase))
                return "thrust";
            if (string.Equals(action, "Defense", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "guard", StringComparison.OrdinalIgnoreCase))
                return "defence";
            if (string.Equals(action, "attack", StringComparison.OrdinalIgnoreCase))
                return "slash";
            if (string.Equals(action, "hurt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "death", StringComparison.OrdinalIgnoreCase))
                return "idle";
            return action;
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            var texture = sprite.texture;
            var source = sprite.rect;
            var coordinates = new Rect(source.x / texture.width, source.y / texture.height,
                source.width / texture.width, source.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, coordinates, true);
        }
    }
}
