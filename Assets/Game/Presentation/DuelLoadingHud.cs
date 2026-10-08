using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>A short full-screen bridge between the academy and battle. Its gear drives a brass bar and vents at progress
    /// stops; it is deliberately presentation-only because the current prototype changes modes synchronously.</summary>
    public sealed class DuelLoadingHud : MonoBehaviour, IDisposable
    {
        public const float DefaultDuration = .75f;
        private const int GearTexels = 160, SteamTexels = 64, PuffCount = 16;
        private static readonly Color Backdrop = new Color(.035f, .045f, .05f, .96f);
        private static readonly Color Steam = new Color(.96f, .93f, .84f, .60f);

        private RectTransform root, gear;
        private Image fill;
        private Text label, percent;
        private Texture2D gearTexture, steamTexture;
        private Sprite gearSprite, steamSprite;
        private readonly Puff[] puffs = new Puff[PuffCount];
        private float elapsed, duration, rotation;
        private int nextStop, puffCursor, sequence;
        private bool disposed;

        public bool IsVisible => !disposed && root != null && root.gameObject.activeSelf;
        public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        public RectTransform Root => root;

        public static DuelLoadingHud Create(Transform parent, Font font)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var host = new GameObject("Transition Loading HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(DuelLoadingHud));
            host.layer = 5;
            host.transform.SetParent(parent, false);
            DuelLoadingHud hud = host.GetComponent<DuelLoadingHud>();
            hud.Build(font);
            return hud;
        }

        public void Play(string message, float seconds = DefaultDuration)
        {
            if (disposed) return;
            elapsed = 0f;
            duration = Mathf.Max(.1f, seconds);
            rotation = 0f;
            nextStop = 1;
            label.text = string.IsNullOrWhiteSpace(message) ? "다음 장면을 준비하는 중" : message.Trim();
            fill.fillAmount = 0f;
            percent.text = "0%";
            gear.localRotation = Quaternion.identity;
            ClearPuffs();
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
        }

        public void Finish()
        {
            if (root != null) root.gameObject.SetActive(false);
            ClearPuffs();
        }

        private void Build(Font font)
        {
            root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;

            Panel("Loading Backdrop", root, Vector2.zero, Vector2.zero, Backdrop, true);
            RectTransform assembly = Rect("Loading Assembly", root, new Vector2(0f, -14f), new Vector2(720f, 190f));
            Text title = MakeText("Loading Title", assembly, font, new Vector2(0f, 67f), new Vector2(620f, 42f), 27,
                TextAnchor.MiddleCenter, DuelVisualTheme.Foreground);
            title.text = "기사학교 기계식 전송 장치";
            label = MakeText("Loading Message", assembly, font, new Vector2(18f, -57f), new Vector2(540f, 30f), 18,
                TextAnchor.MiddleCenter, DuelVisualTheme.Muted);

            Image track = Panel("Loading Track", assembly, new Vector2(38f, 4f), new Vector2(520f, 12f),
                DuelVisualTheme.Track, false);
            fill = Panel("Loading Fill", assembly, new Vector2(38f, 4f), new Vector2(520f, 12f),
                DuelVisualTheme.Accent, false);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            percent = MakeText("Loading Percent", assembly, font, new Vector2(337f, 4f), new Vector2(72f, 30f), 18,
                TextAnchor.MiddleRight, DuelVisualTheme.Foreground);

            gearTexture = CreateGearTexture();
            gearSprite = DuelGearShimmer.CreateSprite(gearTexture);
            Image wheel = Image("Loading Gear", assembly, gearSprite, new Vector2(-266f, 4f), Vector2.one * 86f,
                Color.Lerp(DuelVisualTheme.Border, DuelVisualTheme.Accent, .45f));
            gear = wheel.rectTransform;
            Image hub = Panel("Loading Gear Hub", assembly, new Vector2(-266f, 4f), Vector2.one * 21f,
                DuelVisualTheme.Surface, false);
            hub.transform.SetAsLastSibling();

            steamTexture = CreateSteamTexture();
            steamSprite = DuelGearShimmer.CreateSprite(steamTexture);
            for (int index = 0; index < puffs.Length; index++)
            {
                Image puff = Image("Loading Steam " + (index + 1), assembly, steamSprite, Vector2.zero,
                    Vector2.one * 36f, Color.clear);
                puff.gameObject.SetActive(false);
                puffs[index] = new Puff(puff);
            }
            // Keep the information over steam.
            title.transform.SetAsLastSibling();
            label.transform.SetAsLastSibling();
            percent.transform.SetAsLastSibling();
            Finish();
        }

        private void Update()
        {
            if (!IsVisible) return;
            float delta = Mathf.Max(0f, Time.unscaledDeltaTime);
            elapsed += delta;
            float progress = Progress;
            fill.fillAmount = progress;
            percent.text = Mathf.RoundToInt(progress * 100f) + "%";
            rotation = LegacySkillGear.Wrap(rotation - Mathf.Lerp(120f, 330f, progress) * delta);
            gear.localRotation = Quaternion.Euler(0f, 0f, rotation);
            while (nextStop <= 4 && progress >= nextStop * .25f)
            {
                Emit(nextStop == 4 ? 8 : 4);
                nextStop++;
            }
            TickPuffs(delta);
            // Let the completion puff read for one short beat without delaying the underlying synchronous transition.
            if (elapsed >= duration + .16f) Finish();
        }

        private void Emit(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Puff puff = puffs[puffCursor++ % puffs.Length];
                float phase = (sequence++ * .6180339f) % 1f;
                float side = phase * 2f - 1f;
                puff.Age = 0f;
                puff.Duration = .58f + phase * .24f;
                puff.Start = new Vector2(-266f + side * 8f, 34f + phase * 6f);
                puff.Velocity = new Vector2(side * 48f, 72f + phase * 28f);
                puff.StartSize = 34f + phase * 14f;
                puff.EndSize = puff.StartSize * 2.15f;
                puff.Image.gameObject.SetActive(true);
            }
        }

        private void TickPuffs(float delta)
        {
            foreach (Puff puff in puffs)
            {
                if (puff == null || !(puff.Age < puff.Duration)) continue;
                puff.Age = Mathf.Min(puff.Duration, puff.Age + delta);
                float t = puff.Duration > 0f ? puff.Age / puff.Duration : 1f;
                float eased = 1f - (1f - t) * (1f - t);
                puff.Image.rectTransform.anchoredPosition = puff.Start + puff.Velocity * (puff.Duration * eased);
                puff.Image.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(puff.StartSize, puff.EndSize, eased);
                Color color = Steam;
                color.a *= Mathf.Sin(Mathf.PI * t);
                puff.Image.color = color;
                if (puff.Age >= puff.Duration) puff.Image.gameObject.SetActive(false);
            }
        }

        private void ClearPuffs()
        {
            foreach (Puff puff in puffs)
            {
                if (puff == null) continue;
                puff.Age = puff.Duration;
                puff.Image.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DuelGearShimmer.Release(gearSprite);
            DuelGearShimmer.Release(gearTexture);
            DuelGearShimmer.Release(steamSprite);
            DuelGearShimmer.Release(steamTexture);
            if (gameObject != null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private static Texture2D CreateGearTexture()
        {
            var pixels = new Color32[GearTexels * GearTexels];
            float texel = 2f / GearTexels;
            for (int y = 0; y < GearTexels; y++)
                for (int x = 0; x < GearTexels; x++)
                {
                    float coverage = LegacyGearShimmer.GearCoverage((x + .5f) * texel - 1f,
                        (y + .5f) * texel - 1f, texel, 16, .96f, .79f, .56f, .25f, .1f, 6);
                    pixels[y * GearTexels + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(coverage * 255f));
                }
            return DuelGearShimmer.CreateTexture("Transition Loading Gear", GearTexels, pixels);
        }

        private static Texture2D CreateSteamTexture()
        {
            var pixels = new Color32[SteamTexels * SteamTexels];
            for (int y = 0; y < SteamTexels; y++)
                for (int x = 0; x < SteamTexels; x++)
                {
                    float u = ((x + .5f) / SteamTexels - .5f) * 2f;
                    float v = ((y + .5f) / SteamTexels - .5f) * 2f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Clamp01(1f - radius);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * SteamTexels + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            return DuelGearShimmer.CreateTexture("Transition Loading Steam", SteamTexels, pixels);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool stretch)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            if (stretch)
            {
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.sizeDelta = Vector2.zero;
            }
            image.color = color;
            image.raycastTarget = stretch;
            return image;
        }

        private static Image Image(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            Image image = Panel(name, parent, position, size, color, false);
            image.sprite = sprite;
            return image;
        }

        private static Text MakeText(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize,
            TextAnchor alignment, Color color)
        {
            Text text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private sealed class Puff
        {
            public readonly Image Image;
            public Vector2 Start, Velocity;
            public float Age = 1f, Duration = 1f, StartSize, EndSize;
            public Puff(Image image) => Image = image;
        }
    }
}
