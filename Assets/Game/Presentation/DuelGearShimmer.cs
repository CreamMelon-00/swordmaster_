using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TurnLimbo.Presentation
{
    /// <summary>엘리사's eye: each time the enemy's queue comes up at the start of a planning turn, a brass gear behind its
    /// first card turns a notch and fades while a soft glint sweeps across the cards, slot 1 outward
    /// (<see cref="LegacyGearShimmer"/> has the curves and the gear's shape). It shows no text and never touches the
    /// coach. Both are drawn in code in the duel HUD beside the enemy's queue row, not in it (the gear just under the row,
    /// the glint just over it, clipped to the row, so the row's own children stay its cards); they follow the row as it is
    /// laid out, hide with the HUD, and play on real time whatever the battle's clock does.
    /// <see cref="DuelPresentationSettings.GearShimmerSeconds"/> 0 switches it off.</summary>
    public sealed class DuelGearShimmer : IDisposable
    {
        /// <summary>The gear's size, HUD units: half again a card's, so its rim and teeth ring the first card.</summary>
        public const float GearSize = 96f;
        /// <summary>How opaque the gear and the glint are at their height, before the strength setting.</summary>
        public const float GearOpacity = .85f, GlintOpacity = .55f;
        private const float GlintWidth = 30f, GlintHeight = 112f, GlintTilt = -18f, GlintOverhang = 24f;
        private const int GearTexels = 128, GlintTexels = 32;
        private static readonly Color Brass = DuelVisualTheme.Accent;
        private static readonly Color GlintColor = new Color(1f, .95f, .8f, 1f);

        private readonly LegacyCombatHud hud;
        private readonly Func<DuelPresentationSettings> settings;
        private readonly Image gear, glint;
        private readonly RectTransform glintMask;
        private readonly Texture2D gearTexture, glintTexture;
        private readonly Sprite gearSprite, glintSprite;
        private float seconds, elapsed;
        private bool disposed;

        /// <param name="settings">Read when used, so live tuning (and a test's clone) applies at once.</param>
        public DuelGearShimmer(LegacyCombatHud hud, Func<DuelPresentationSettings> settings)
        {
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            hud.TryGetEnemyQueueRow(out RectTransform row, out _);
            if (row == null) throw new ArgumentException("The duel HUD has no enemy queue row.", nameof(hud));
            gearTexture = CreateGearTexture();
            glintTexture = CreateGlintTexture();
            gearSprite = CreateSprite(gearTexture);
            glintSprite = CreateSprite(glintTexture);
            gear = CreateImage("Enemy Queue Gear", row.parent, gearSprite, new Vector2(GearSize, GearSize));
            glintMask = new GameObject("Enemy Queue Glint", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            glintMask.gameObject.layer = row.gameObject.layer;
            glintMask.SetParent(row.parent, false);
            glintMask.anchorMin = glintMask.anchorMax = glintMask.pivot = Vector2.one * .5f;
            glint = CreateImage("Glint", glintMask, glintSprite, new Vector2(GlintWidth, GlintHeight));
            glint.rectTransform.localRotation = Quaternion.Euler(0f, 0f, GlintTilt);
            KeepAround(row);
            Hide();
        }

        /// <summary>Whether the shimmer is playing (it may be waiting off screen while the row shows no card).</summary>
        public bool IsPlaying => !disposed && seconds > 0f;
        /// <summary>Where the shimmer is, 0 to 1 (0 when it is not playing).</summary>
        public float Progress => IsPlaying ? LegacyGearShimmer.Progress(elapsed, seconds) : 0f;
        /// <summary>The gear and the glint, for tests and tools.</summary>
        public Image Gear => gear;
        public Image Glint => glint;
        public RectTransform GlintArea => glintMask;
        public Sprite GearSprite => gearSprite;

        /// <summary>The enemy's queue has just come up (a planning turn begins): the shimmer plays from its start.</summary>
        public void Reveal()
        {
            if (disposed) return;
            seconds = settings().GearShimmerSeconds;
            elapsed = 0f;
            if (seconds <= 0f) Clear();
        }

        /// <summary>A frame, after the duel HUD's layout: the shimmer moves on by <paramref name="realDelta"/> and follows
        /// the row as it now stands.</summary>
        public void Tick(float realDelta)
        {
            if (!IsPlaying) return;
            elapsed += Mathf.Max(0f, realDelta);
            if (elapsed >= seconds)
            {
                Clear();
                return;
            }
            Apply();
        }

        /// <summary>Gone at once (the battle is over, restarted or left).</summary>
        public void Clear()
        {
            seconds = elapsed = 0f;
            Hide();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            seconds = elapsed = 0f;
            if (gear != null) Release(gear.gameObject);
            if (glintMask != null) Release(glintMask.gameObject);
            Release(gearSprite);
            Release(glintSprite);
            Release(gearTexture);
            Release(glintTexture);
        }

        private void Apply()
        {
            if (!hud.TryGetEnemyQueueRow(out RectTransform row, out Vector2 span))
            {
                Hide();
                return;
            }
            float t = LegacyGearShimmer.Progress(elapsed, seconds);
            float strength = settings().GearShimmerStrength;
            KeepAround(row);
            gear.gameObject.SetActive(true);
            glintMask.gameObject.SetActive(true);
            // The gear centred on slot 1, the glint's window over the row; both where the row now stands.
            Vector2 origin = row.anchoredPosition;
            RectTransform gearRect = gear.rectTransform;
            gearRect.anchoredPosition = origin + new Vector2(span.x + LegacyCombatHud.QueueCardSize * .5f, 0f);
            gearRect.localRotation = Quaternion.Euler(0f, 0f, -LegacyGearShimmer.GearTurn(t));
            gearRect.localScale = Vector3.one * LegacyGearShimmer.GearScale(t);
            gear.color = new Color(Brass.r, Brass.g, Brass.b, GearOpacity * strength * LegacyGearShimmer.GearAlpha(t));
            float width = Mathf.Max(0f, span.y - span.x);
            glintMask.anchoredPosition = origin + new Vector2((span.x + span.y) * .5f, 0f);
            glintMask.sizeDelta = new Vector2(width, LegacyCombatHud.QueueCardSize);
            float travel = width * .5f + GlintOverhang;
            glint.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, LegacyGearShimmer.GlintPosition(t)), 0f);
            glint.color = new Color(GlintColor.r, GlintColor.g, GlintColor.b,
                GlintOpacity * strength * LegacyGearShimmer.GlintAlpha(t));
        }

        private void Hide()
        {
            if (disposed) return;
            gear.gameObject.SetActive(false);
            glintMask.gameObject.SetActive(false);
        }

        // The gear draws just before the row (under its cards) and the glint just after it (over them), whatever the HUD
        // has added or moved since.
        private void KeepAround(RectTransform row)
        {
            Transform gearNode = gear.transform;
            int at = row.GetSiblingIndex();
            if (gearNode.GetSiblingIndex() != at - 1) gearNode.SetSiblingIndex(gearNode.GetSiblingIndex() > at ? at : at - 1);
            at = row.GetSiblingIndex();
            if (glintMask.GetSiblingIndex() != at + 1) glintMask.SetSiblingIndex(glintMask.GetSiblingIndex() > at ? at + 1 : at);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.gameObject.layer = parent.gameObject.layer;
            RectTransform rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        // The gear in white, its edges softened over a texel (LegacyGearShimmer.GearCoverage); the Image tints it brass.
        private static Texture2D CreateGearTexture()
        {
            var pixels = new Color32[GearTexels * GearTexels];
            float texel = 2f / GearTexels;
            for (int y = 0; y < GearTexels; y++)
                for (int x = 0; x < GearTexels; x++)
                {
                    float coverage = LegacyGearShimmer.GearCoverage((x + .5f) * texel - 1f, (y + .5f) * texel - 1f, texel);
                    pixels[y * GearTexels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            return CreateTexture("Gear Shimmer Gear", GearTexels, pixels);
        }

        // A soft vertical band: brightest down its middle, fading to the sides and toward both ends.
        private static Texture2D CreateGlintTexture()
        {
            var pixels = new Color32[GlintTexels * GlintTexels];
            float centre = (GlintTexels - 1) * .5f;
            for (int y = 0; y < GlintTexels; y++)
                for (int x = 0; x < GlintTexels; x++)
                {
                    float across = (x - centre) / (GlintTexels * .5f);
                    float along = Mathf.Abs(y - centre) / (GlintTexels * .5f);
                    float alpha = Mathf.Exp(-across * across * 6f) * Mathf.Clamp01((1f - along) / .3f);
                    pixels[y * GlintTexels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            return CreateTexture("Gear Shimmer Glint", GlintTexels, pixels);
        }

        private static Texture2D CreateTexture(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * .5f,
                100f, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
