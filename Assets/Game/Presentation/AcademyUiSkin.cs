using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>PixelLab materials decorate the existing functional UI. Text, state colours, input and layout
    /// remain owned by each HUD. Missing art falls back to the original procedural trim.</summary>
    public static class AcademyUiSkin
    {
        public const string ResourceFolder = "AcademyUI/";
        private static Sprite panel, button, crest, titleBackground;
        public static Sprite GearWheel => Resources.Load<Sprite>(ResourceFolder + "gear-wheel");
        public static Sprite GearPinion => Resources.Load<Sprite>(ResourceFolder + "gear-pinion");
        public static Sprite GearClock => Resources.Load<Sprite>(ResourceFolder + "gear-clock");
        public static Sprite GaugeBezel => Resources.Load<Sprite>(ResourceFolder + "gauge-bezel");
        public static Sprite GaugeFill => Resources.Load<Sprite>(ResourceFolder + "gauge-fill");
        public static Sprite MenuPlaque => Resources.Load<Sprite>(ResourceFolder + "menu-plaque");
        public static Sprite ValvePart(string name)
        {
            foreach (Sprite sprite in Resources.LoadAll<Sprite>(ResourceFolder + "breath-valve"))
                if (sprite.name == name) return sprite;
            return null;
        }
        public static Sprite Panel => panel != null ? panel : panel = Resources.Load<Sprite>(ResourceFolder + "panel-frame");
        public static Sprite Button => button != null ? button : button = Resources.Load<Sprite>(ResourceFolder + "button-frame");
        public static Sprite Crest => crest != null ? crest : crest = Resources.Load<Sprite>(ResourceFolder + "academy-crest");
        public static Sprite TitleBackground => titleBackground != null ? titleBackground :
            titleBackground = Resources.Load<Sprite>(ResourceFolder + "title-academy");
        public static void Reload() { panel = button = crest = titleBackground = null; }

        public static bool ApplyFrame(Image surface, bool isButton = false)
        {
            if (surface == null) return false;
            Rect bounds = surface.rectTransform.rect;
            // Compact slots and mechanical parts keep their thin functional borders.
            if (bounds.width < 80f || bounds.height < 36f) return false;
            Sprite sprite = isButton ? Button : Panel;
            if (sprite == null) return false;
            Transform oldTrim = surface.transform.Find("Brass Trim");
            if (oldTrim != null) oldTrim.gameObject.SetActive(false);
            Transform existing = surface.transform.Find("PixelLab Frame");
            Image frame;
            if (existing != null) frame = existing.GetComponent<Image>();
            else
            {
                var node = new GameObject("PixelLab Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                node.layer = surface.gameObject.layer;
                var rect = (RectTransform)node.transform;
                rect.SetParent(surface.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = rect.anchoredPosition = Vector2.zero;
                rect.SetAsFirstSibling();
                frame = node.GetComponent<Image>();
            }
            frame.sprite = sprite;
            // Compensate only for the generated PNG's transparent outer padding. Slicing keeps the actual contour
            // on the surface edge, rather than leaving a second inset box around the label.
            frame.rectTransform.offsetMin = isButton ? new Vector2(-4f, 0f) : new Vector2(-9f, -9f);
            frame.rectTransform.offsetMax = isButton ? new Vector2(4f, 0f) : new Vector2(10f, 10f);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = 1f;
            frame.color = Color.white;
            frame.raycastTarget = false;
            return true;
        }

        public static Image AddCrest(Transform parent, Vector2 position, float size)
        {
            if (parent == null || Crest == null) return null;
            var node = new GameObject("Academy Crest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.layer = 5;
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * size;
            Image image = node.GetComponent<Image>();
            image.sprite = Crest;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }
    }

    /// <summary>A single stepped command instrument housing. The centre carries gauges and gears;
    /// the lower wings carry distinct actuators, instead of repeating a frame around every button.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AcademyCommandHousing : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float sx = r.width / 1240f, sy = r.height / 248f;
            Vector2[] outline = {
                new Vector2(-620, -104), new Vector2(-602, -122), new Vector2(602, -122),
                new Vector2(620, -104), new Vector2(620, 74), new Vector2(602, 92),
                new Vector2(304, 92), new Vector2(284, 124), new Vector2(-284, 124),
                new Vector2(-304, 92), new Vector2(-602, 92), new Vector2(-620, 74)
            };
            Shape(mesh, outline, sx, sy, 0f, -4f, new Color32(12, 11, 9, 245));
            Shape(mesh, outline, sx, sy, 0f, 0f, DuelVisualTheme.Border);
            Shape(mesh, outline, sx, sy, 2f, 0f, DuelVisualTheme.Track);
            Shape(mesh, outline, sx, sy, 5f, 0f, DuelVisualTheme.Surface);
            // The imported bezel owns the instrument contour; only the wheel bed belongs to this body.
            Quad(mesh, -282 * sx, -118 * sy, 564 * sx, 20 * sy, DuelVisualTheme.RaisedSurface);
            // A single perimeter avoids duplicating trim under the keys and the new bezel.
            Quad(mesh, -476 * sx, -72 * sy, 1f, 126 * sy, DuelVisualTheme.Border * new Color(1, 1, 1, .4f));
            Quad(mesh, 476 * sx, -72 * sy, 1f, 126 * sy, DuelVisualTheme.Border * new Color(1, 1, 1, .4f));
        }

        private static void Shape(VertexHelper mesh, Vector2[] points, float sx, float sy, float inset,
            float offsetY, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(0, offsetY), color, Vector2.zero);
            foreach (Vector2 p in points)
                mesh.AddVert(new Vector3(p.x * sx - Mathf.Sign(p.x) * inset,
                    p.y * sy - Mathf.Sign(p.y) * inset + offsetY), color, Vector2.zero);
            for (int i = 0; i < points.Length; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Length);
        }

        private static void Quad(VertexHelper mesh, float x, float y, float w, float h, Color color)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x, y), color, Vector2.zero);
            mesh.AddVert(new Vector3(x, y + h), color, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y + h), color, Vector2.zero);
            mesh.AddVert(new Vector3(x + w, y), color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }

    /// <summary>Rigid imported hardware turns around a registered pivot; the mounting remains fixed.
    /// No generated frame swapping, texture creation or per-frame sprite allocation.</summary>
    public sealed class AcademyControlAnimation
    {
        private readonly Image image;
        private readonly Image moving;
        private readonly bool lever;
        private readonly float duration;
        private float elapsed, startAngle;
        public bool IsPlaying { get; private set; }

        public AcademyControlAnimation(Image image, string folder, float duration)
        {
            this.image = image;
            lever = folder == "Lever";
            string prefix = lever ? "lever-" : "valve-";
            Sprite fixedSprite = lever ? Resources.Load<Sprite>(AcademyUiSkin.ResourceFolder + prefix + "base") : AcademyUiSkin.ValvePart("Valve Pipe");
            Sprite movingSprite = lever ? Resources.Load<Sprite>(AcademyUiSkin.ResourceFolder + prefix + "arm") : AcademyUiSkin.ValvePart("Valve Wheel");
            if (fixedSprite == null || movingSprite == null) return;
            image.sprite = fixedSprite;
            if (!lever)
            {
                // These are two imported sprite rectangles from the original PNG, not masked canvases.
                // Only the complete wheel (including its cap) turns. The pipe keeps its original pixels.
                image.rectTransform.sizeDelta = fixedSprite.rect.size;
                image.rectTransform.anchoredPosition += fixedSprite.rect.center - Vector2.one * 48f;
                image.preserveAspect = false;
            }
            var host = new GameObject("Moving Part", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            host.layer = image.gameObject.layer;
            var rect = (RectTransform)host.transform;
            rect.SetParent(image.transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
            Vector2 pivotPixel = new Vector2(44f, 62f);
            rect.pivot = lever ? new Vector2(pivotPixel.x / 96f, 1f - pivotPixel.y / 96f) : Vector2.one * .5f;
            rect.sizeDelta = lever ? Vector2.one * 96f : movingSprite.rect.size;
            rect.anchoredPosition = lever ? new Vector2(pivotPixel.x - 48f, 48f - pivotPixel.y)
                : movingSprite.rect.center - fixedSprite.rect.center;
            moving = host.GetComponent<Image>();
            moving.sprite = movingSprite;
            moving.raycastTarget = false;
            Button button = image.GetComponentInParent<Button>();
            if (button != null) button.targetGraphic = moving;
            this.duration = Mathf.Max(.1f, duration);
            CopyTint();
        }

        public void Play()
        {
            if (moving == null) return;
            startAngle = Mathf.DeltaAngle(0f, moving.rectTransform.localEulerAngles.z);
            elapsed = 0;
            IsPlaying = true;
        }

        public void Advance(float unscaledDelta)
        {
            CopyTint();
            if (!IsPlaying || moving == null) return;
            elapsed += Mathf.Max(0, unscaledDelta);
            if (elapsed >= duration) { Reset(); return; }
            float t = elapsed / duration;
            float peak = lever ? -32f : -45f;
            float turnShare = lever ? .32f : .45f;
            float angle = t < turnShare
                ? Mathf.Lerp(startAngle, peak, Mathf.SmoothStep(0f, 1f, t / turnShare))
                : Mathf.Lerp(peak, 0f, Mathf.SmoothStep(0f, 1f, (t - turnShare) / (1f - turnShare)));
            moving.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void Reset()
        {
            IsPlaying = false;
            elapsed = 0;
            if (moving != null) moving.rectTransform.localRotation = Quaternion.identity;
            CopyTint();
        }

        private void CopyTint()
        {
            if (moving != null) image.canvasRenderer.SetColor(moving.canvasRenderer.GetColor());
        }
    }

    /// <summary>The coach highlights the command seal with a ring, leaving the control's centre uncovered.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AcademySealFocus : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 from = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 to = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int start = mesh.currentVertCount;
                mesh.AddVert(from * radius, DuelVisualTheme.Accent, Vector2.zero);
                mesh.AddVert(from * (radius - 2f), DuelVisualTheme.Accent, Vector2.zero);
                mesh.AddVert(to * (radius - 2f), DuelVisualTheme.Accent, Vector2.zero);
                mesh.AddVert(to * radius, DuelVisualTheme.Accent, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }

    /// <summary>A left-weighted veil keeps the menu readable while leaving the academy artwork visible.</summary>
    public sealed class AcademyTitleVeil : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float[] stops = { 0f, .24f, .48f, .70f, 1f };
            float[] alpha = { .93f, .85f, .48f, .10f, .02f };
            for (int i = 0; i < stops.Length - 1; i++)
            {
                int offset = mesh.currentVertCount;
                float left = Mathf.Lerp(rect.xMin, rect.xMax, stops[i]);
                float right = Mathf.Lerp(rect.xMin, rect.xMax, stops[i + 1]);
                Color a = new Color32(14, 24, 30, 255), b = a;
                a.a = alpha[i]; b.a = alpha[i + 1];
                mesh.AddVert(new Vector3(left, rect.yMin), a, Vector2.zero);
                mesh.AddVert(new Vector3(left, rect.yMax), a, Vector2.zero);
                mesh.AddVert(new Vector3(right, rect.yMax), b, Vector2.zero);
                mesh.AddVert(new Vector3(right, rect.yMin), b, Vector2.zero);
                mesh.AddTriangle(offset, offset + 1, offset + 2);
                mesh.AddTriangle(offset, offset + 2, offset + 3);
            }
        }
    }
}
