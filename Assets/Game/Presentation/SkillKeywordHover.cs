using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Delayed explanations for common skill terms. The body catches pointer hits only on term glyphs.</summary>
    public sealed class SkillKeywordHover : MonoBehaviour
    {
        public const float DelaySeconds = 1.2f;
        private static readonly string[] Terms = { "순환", "취소", "반환", "붕괴", "맞물림" };

        private RectTransform card, canvasRect, popup, divider;
        private Canvas canvas;
        private Image firstBadge, secondBadge;
        private SkillKeywordHitGraphic bodyHit;
        private SkillKeywordTextTint bodyTint;
        private Text heading, description;
        private float scale;
        private string firstTerm, secondTerm, pendingTerm;
        private SkillKeywordHoverTarget pendingSource;
        private Vector2 pointerPosition;
        private Camera pointerCamera;
        private float pendingSince;
        private readonly Vector3[] cardCorners = new Vector3[4];

        public static SkillKeywordHover Attach(RectTransform card, Image firstBadge, Text firstLabel,
            Image secondBadge, Text secondLabel, Text body, Font font, float scale)
        {
            if (card == null || firstBadge == null || secondBadge == null || body == null)
                throw new ArgumentNullException(nameof(card), "A skill card, both keyword badges and its body are required.");
            SkillKeywordHover hover = card.gameObject.AddComponent<SkillKeywordHover>();
            hover.card = card;
            hover.canvas = card.GetComponentInParent<Canvas>();
            hover.canvasRect = hover.canvas != null ? hover.canvas.rootCanvas.transform as RectTransform : null;
            hover.firstBadge = firstBadge;
            hover.secondBadge = secondBadge;
            hover.scale = Mathf.Max(0.5f, scale);

            ConfigureBadge(firstBadge, firstLabel, hover, 0);
            ConfigureBadge(secondBadge, secondLabel, hover, 1);

            // Keep the body Text nonblocking. This graphic draws underlines and only raycasts over term glyphs.
            var hitObject = new GameObject("Keyword Word Hits", typeof(RectTransform), typeof(CanvasRenderer));
            hitObject.layer = body.gameObject.layer;
            var hitRect = (RectTransform)hitObject.transform;
            hitRect.SetParent(body.rectTransform, false);
            hitRect.anchorMin = Vector2.zero;
            hitRect.anchorMax = Vector2.one;
            hitRect.offsetMin = hitRect.offsetMax = Vector2.zero;
            hover.bodyHit = hitObject.AddComponent<SkillKeywordHitGraphic>();
            hover.bodyHit.SetSource(body);
            hover.bodyTint = body.gameObject.AddComponent<SkillKeywordTextTint>();
            hitObject.AddComponent<SkillKeywordHoverTarget>().Configure(hover, 2);
            body.raycastTarget = false;

            hover.CreatePopup(font != null ? font : body.font);
            hover.Clear();
            return hover;
        }

        /// <summary>Call after setting the two badge labels and the plain effect text.</summary>
        public void SetTerms(string firstBadgeText, string secondBadgeText, string bodyText)
        {
            Dismiss();
            firstTerm = BadgeTerm(firstBadgeText);
            secondTerm = BadgeTerm(secondBadgeText);
            firstBadge.raycastTarget = firstTerm != null;
            secondBadge.raycastTarget = secondTerm != null;
            bodyHit.SetTerms(bodyText);
            bodyTint.SetTerms(bodyText);
        }

        public void Clear()
        {
            Dismiss();
            firstTerm = secondTerm = null;
            if (firstBadge != null) firstBadge.raycastTarget = false;
            if (secondBadge != null) secondBadge.raycastTarget = false;
            if (bodyHit != null) bodyHit.SetTerms(null);
            if (bodyTint != null) bodyTint.SetTerms(null);
        }

        /// <summary>Common definitions only; each skill keeps its numeric effects in its own description.</summary>
        public static bool TryExplain(string term, out string explanation)
        {
            switch (term)
            {
                case "순환":
                    explanation = "같은 전투에서 이 기술을 시전할 때마다 횟수가 1씩 증가합니다.";
                    return true;
                case "취소":
                    explanation = "예약된 기술이 대기열에서 빠집니다. 그 기술에 쓴 ACT는 돌려받지 못합니다.";
                    return true;
                case "반환":
                    explanation = "예약된 기술이 대기열에서 빠지고, 그 기술에 쓴 ACT를 돌려받습니다.";
                    return true;
                case "붕괴":
                    explanation = "저항이 0인 상태입니다. 회복되기 전까지 받는 체력 피해가 2배가 됩니다.";
                    return true;
                case "맞물림":
                    explanation = "서로 다른 검술의 기술을 연달아 예약하면, 연결된 기술 수 × 10%만큼 위력이 증가합니다.";
                    return true;
                default:
                    explanation = null;
                    return false;
            }
        }

        internal static string[] KnownTerms => Terms;

        // TextGenerator does not emit a four-vertex glyph for whitespace, although those characters
        // remain in Text.text. Every keyword lookup starts from the plain-copy index.
        internal static int RenderedGlyphIndex(string plainText, int characterIndex)
        {
            if (string.IsNullOrEmpty(plainText) || characterIndex < 0 ||
                characterIndex >= plainText.Length || char.IsWhiteSpace(plainText[characterIndex]))
                return -1;
            int glyph = 0;
            for (int i = 0; i < characterIndex; i++)
                if (!char.IsWhiteSpace(plainText[i])) glyph++;
            return glyph;
        }

        // Korean particles can follow a term, but a term embedded inside a preceding word (e.g. 미반환)
        // must not gain the opposite meaning's tooltip.
        internal static bool BeginsKeyword(string text, int start)
        {
            return start == 0 || !(char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_');
        }

        internal void PointerOver(SkillKeywordHoverTarget source, PointerEventData eventData)
        {
            string term;
            switch (source.Kind)
            {
                case 0: term = firstTerm; break;
                case 1: term = secondTerm; break;
                default:
                    if (!bodyHit.TryTermAtScreen(eventData.position, eventData.enterEventCamera, out term))
                        term = null;
                    break;
            }
            if (term == null)
            {
                if (pendingSource == source) Dismiss();
                return;
            }
            pointerPosition = eventData.position;
            pointerCamera = eventData.enterEventCamera;
            if (pendingSource == source && pendingTerm == term) return;
            Dismiss();
            pendingSource = source;
            pendingTerm = term;
            pendingSince = Time.unscaledTime;
        }

        internal void PointerLeft(SkillKeywordHoverTarget source)
        {
            if (pendingSource == source) Dismiss();
        }

        private void Update()
        {
            if (pendingTerm != null && popup != null && !popup.gameObject.activeSelf &&
                Time.unscaledTime - pendingSince >= DelaySeconds)
                Show(pendingTerm);
        }

        private void OnDisable() => Dismiss();

        private void OnDestroy()
        {
            if (popup != null) Destroy(popup.gameObject);
        }

        private void Dismiss()
        {
            pendingTerm = null;
            pendingSource = null;
            if (popup != null) popup.gameObject.SetActive(false);
        }

        private static string BadgeTerm(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;
            string match = null;
            int first = int.MaxValue;
            foreach (string term in Terms)
            {
                int from = 0, index;
                while ((index = label.IndexOf(term, from, StringComparison.Ordinal)) >= 0)
                {
                    if (BeginsKeyword(label, index) && index < first)
                    {
                        first = index;
                        match = term;
                    }
                    from = index + term.Length;
                }
            }
            return match;
        }

        private static void ConfigureBadge(Image badge, Text label, SkillKeywordHover owner, int kind)
        {
            badge.raycastTarget = false;
            if (label != null) label.raycastTarget = false;
            badge.gameObject.AddComponent<SkillKeywordHoverTarget>().Configure(owner, kind);
        }

        private void CreatePopup(Font font)
        {
            if (canvasRect == null) return;
            var panel = new GameObject("Skill Keyword Explanation", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            panel.layer = card.gameObject.layer;
            popup = (RectTransform)panel.transform;
            popup.SetParent(canvasRect, false);
            popup.anchorMin = popup.anchorMax = new Vector2(.5f, .5f);
            popup.pivot = new Vector2(0f, 1f);
            popup.sizeDelta = new Vector2(300f, 142f) * scale;
            Image paper = panel.GetComponent<Image>();
            paper.color = DuelVisualTheme.Paper;
            paper.raycastTarget = false;
            DuelVisualTheme.Frame(paper, true);
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            heading = PopupText("Keyword Title", font, new Vector2(15f, -13f) * scale,
                new Vector2(270f, 28f) * scale, Mathf.RoundToInt(22f * scale));
            heading.fontStyle = FontStyle.Bold;
            heading.color = new Color32(34, 83, 82, 255);
            var rule = new GameObject("Keyword Rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rule.layer = panel.layer;
            divider = (RectTransform)rule.transform;
            divider.SetParent(popup, false);
            divider.anchorMin = divider.anchorMax = new Vector2(0f, 1f);
            divider.pivot = new Vector2(0f, 1f);
            divider.anchoredPosition = new Vector2(15f, -48f) * scale;
            divider.sizeDelta = new Vector2(270f, 1f) * scale;
            Image line = rule.GetComponent<Image>();
            line.color = DuelVisualTheme.Border;
            line.raycastTarget = false;
            description = PopupText("Keyword Meaning", font, new Vector2(15f, -56f) * scale,
                new Vector2(270f, 70f) * scale, Mathf.RoundToInt(16f * scale));
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = Mathf.RoundToInt(14f * scale);
            description.resizeTextMaxSize = Mathf.RoundToInt(16f * scale);
            panel.SetActive(false);
        }

        private Text PopupText(string name, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = popup.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(popup, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = DuelVisualTheme.Ink;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private void Show(string term)
        {
            if (popup == null || !TryExplain(term, out string meaning)) return;
            heading.text = term;
            description.text = meaning;
            popup.SetAsLastSibling();
            popup.gameObject.SetActive(true);
            PlacePopup();
        }

        private void PlacePopup()
        {
            card.GetWorldCorners(cardCorners);
            Vector3 lower = canvasRect.InverseTransformPoint(cardCorners[0]);
            Vector3 upper = canvasRect.InverseTransformPoint(cardCorners[2]);
            Rect screen = canvasRect.rect;
            float width = Mathf.Min(300f * scale, Mathf.Max(80f, screen.width - 16f));
            float height = Mathf.Min(142f * scale, Mathf.Max(80f, screen.height - 16f));
            popup.sizeDelta = new Vector2(width, height);
            float textWidth = Mathf.Max(60f, width - 30f * scale);
            heading.rectTransform.sizeDelta = new Vector2(textWidth, heading.rectTransform.sizeDelta.y);
            description.rectTransform.sizeDelta = new Vector2(textWidth, description.rectTransform.sizeDelta.y);
            divider.sizeDelta = new Vector2(textWidth, divider.sizeDelta.y);

            float x = upper.x + 14f * scale;
            if (x + width > screen.xMax - 8f) x = lower.x - width - 14f * scale;
            x = Mathf.Clamp(x, screen.xMin + 8f, screen.xMax - width - 8f);
            Camera camera = pointerCamera != null ? pointerCamera : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, pointerPosition, camera, out Vector2 pointer);
            float y = Mathf.Clamp(pointer.y + height * .48f, screen.yMin + height + 8f, screen.yMax - 8f);
            popup.anchoredPosition = new Vector2(x - screen.center.x, y - screen.center.y);
        }
    }

    /// <summary>Dispatches pointer movement without consuming click or drag events from a containing card.</summary>
    public sealed class SkillKeywordHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private SkillKeywordHover owner;
        internal int Kind { get; private set; }

        internal void Configure(SkillKeywordHover hover, int kind)
        {
            owner = hover;
            Kind = kind;
        }

        public void OnPointerEnter(PointerEventData eventData) => owner?.PointerOver(this, eventData);
        public void OnPointerMove(PointerEventData eventData) => owner?.PointerOver(this, eventData);
        public void OnPointerExit(PointerEventData eventData) => owner?.PointerLeft(this);
    }


    /// <summary>Colours term glyphs without rich-text markup, preserving the plain copy and its wrapping.</summary>
    public sealed class SkillKeywordTextTint : BaseMeshEffect
    {
        private readonly List<int> glyphIndices = new List<int>(12);

        internal void SetTerms(string plainText)
        {
            glyphIndices.Clear();
            if (!string.IsNullOrEmpty(plainText))
            {
                foreach (string term in SkillKeywordHover.KnownTerms)
                {
                    int from = 0, index;
                    while ((index = plainText.IndexOf(term, from, StringComparison.Ordinal)) >= 0)
                    {
                        if (SkillKeywordHover.BeginsKeyword(plainText, index))
                            for (int i = 0; i < term.Length; i++)
                            {
                                int glyph = SkillKeywordHover.RenderedGlyphIndex(plainText, index + i);
                                if (glyph >= 0) glyphIndices.Add(glyph);
                            }
                        from = index + term.Length;
                    }
                }
            }
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            Color32 ink = new Color32(34, 83, 82, 255);
            foreach (int glyph in glyphIndices)
            {
                int first = glyph * 4;
                if (first + 3 >= vh.currentVertCount) continue;
                for (int i = 0; i < 4; i++)
                {
                    UIVertex vertex = default;
                    vh.PopulateUIVertex(ref vertex, first + i);
                    vertex.color = ink;
                    vh.SetUIVertex(vertex, first + i);
                }
            }
        }
    }

    /// <summary>Underlines known terms in plain uGUI Text and raycasts only their rendered glyphs.</summary>
    public sealed class SkillKeywordHitGraphic : MaskableGraphic
    {
        private readonly List<Word> words = new List<Word>(4);
        private Text source;

        internal void SetSource(Text label)
        {
            source = label;
            color = new Color32(34, 83, 82, 255);
            raycastTarget = false;
        }

        internal void SetTerms(string plainText)
        {
            words.Clear();
            if (!string.IsNullOrEmpty(plainText))
            {
                foreach (string term in SkillKeywordHover.KnownTerms)
                {
                    int from = 0, index;
                    while ((index = plainText.IndexOf(term, from, StringComparison.Ordinal)) >= 0)
                    {
                        if (SkillKeywordHover.BeginsKeyword(plainText, index)) words.Add(new Word(index, term));
                        from = index + term.Length;
                    }
                }
            }
            raycastTarget = words.Count > 0;
            SetVerticesDirty();
        }

        internal bool TryTermAtScreen(Vector2 screenPoint, Camera camera, out string term)
        {
            term = null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, camera,
                out Vector2 local)) return false;
            foreach (Word word in words)
            {
                for (int i = word.Start; i < word.Start + word.Term.Length; i++)
                {
                    if (!TryGlyphBounds(i, out Rect glyph)) continue;
                    glyph.xMin -= 2f;
                    glyph.xMax += 2f;
                    glyph.yMin -= 3f;
                    glyph.yMax += 3f;
                    if (!glyph.Contains(local)) continue;
                    term = word.Term;
                    return true;
                }
            }
            return false;
        }

        public override bool Raycast(Vector2 sp, Camera eventCamera)
        {
            return base.Raycast(sp, eventCamera) && TryTermAtScreen(sp, eventCamera, out _);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Word word in words)
            {
                for (int i = word.Start; i < word.Start + word.Term.Length; i++)
                {
                    if (!TryGlyphBounds(i, out Rect glyph)) continue;
                    float y = glyph.yMin - 2f;
                    int start = vh.currentVertCount;
                    Color32 ink = color;
                    vh.AddVert(new Vector3(glyph.xMin, y), ink, Vector2.zero);
                    vh.AddVert(new Vector3(glyph.xMin, y - 1.2f), ink, Vector2.zero);
                    vh.AddVert(new Vector3(glyph.xMax, y - 1.2f), ink, Vector2.zero);
                    vh.AddVert(new Vector3(glyph.xMax, y), ink, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }

        private bool TryGlyphBounds(int index, out Rect bounds)
        {
            bounds = default;
            if (source == null || index < 0 || index >= source.text.Length) return false;
            int glyph = SkillKeywordHover.RenderedGlyphIndex(source.text, index);
            if (glyph < 0) return false;
            IList<UIVertex> vertices = source.cachedTextGenerator.verts;
            int start = glyph * 4;
            if (vertices.Count < start + 4) return false;
            float inverse = 1f / Mathf.Max(.001f, source.pixelsPerUnit);
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int n = 0; n < 4; n++)
            {
                Vector3 point = vertices[start + n].position * inverse;
                xMin = Mathf.Min(xMin, point.x); xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y); yMax = Mathf.Max(yMax, point.y);
            }
            if (xMax - xMin < .5f || yMax - yMin < .5f) return false;
            bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        private readonly struct Word
        {
            internal readonly int Start;
            internal readonly string Term;
            internal Word(int start, string term) { Start = start; Term = term; }
        }
    }
}

