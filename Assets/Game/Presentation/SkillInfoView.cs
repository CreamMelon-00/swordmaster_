using System;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Skill text with edge-mounted facts, shared by selection panels and held explanations.</summary>
    public sealed class SkillInfoView
    {
        public const float AttachmentOverhang = 34f;
        private const float BodyLeftInset = 24f;
        private readonly RectTransform root, stats, keywords, costSeal, powerPlate;
        private readonly Text act, powerLabel, hits, hitsLabel, typeLabel, firstKeyword, secondKeyword;
        private readonly SkillInfoGlyph firstSymbol, secondSymbol, powerSymbol;
        private readonly Image firstBadge, secondBadge;
        private readonly SkillInfoAttachment typeAttachment;
        private LegacySkill shown;
        private bool shownEnemy;

        private enum KeywordTone { Neutral, Recovery, Followup, Reduction, HighPower, MultiHit, Variance, Defence }

        public GameObject Root => root.gameObject;
        public float Height => root.sizeDelta.y;
        public Text PowerText { get; }
        public Text AttackTypeText { get; }
        public Text EffectText { get; }
        public Text DamageText { get; }

        public SkillInfoView(Transform parent, Font font, Vector2 center, float width, string prefix)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = Rect("Skill Summary", parent, center, new Vector2(width, 72f));
            stats = Rect("Skill Attachments", root, Vector2.zero, new Vector2(width + AttachmentOverhang * 2f, 72f));
            float edge = width / 2f + 12f;
            costSeal = Attachment("ACT Attachment", stats, new Vector2(-edge - 6, 74), Vector2.one * 58,
                SkillInfoAttachmentShape.Seal, DuelVisualTheme.Surface);
            var costSymbol = Glyph(costSeal, SkillInfoSymbol.Act, new Vector2(-14, 12), 12);
            costSymbol.color = DuelVisualTheme.Foreground;
            var costLabel = Label("ACT Label", costSeal, font, new Vector2(8, 12), new Vector2(30, 16), 11);
            costLabel.text = "ACT"; costLabel.color = DuelVisualTheme.Foreground;
            act = Label("ACT Value", costSeal, font, new Vector2(0, -9), new Vector2(46, 28), 24);
            act.color = DuelVisualTheme.Foreground;
            powerPlate = Attachment("Power Attachment", stats, new Vector2(-edge - 6, -4), new Vector2(70, 60),
                SkillInfoAttachmentShape.PowerPlate, DuelVisualTheme.Paper);
            powerSymbol = Glyph(powerPlate, SkillInfoSymbol.Sword, new Vector2(-18, 14), 14);
            powerLabel = Label("Power Label", powerPlate, font, new Vector2(10, 14), new Vector2(34, 18), 12);
            PowerText = Label(prefix + " Detail Values", powerPlate, font, new Vector2(0, -9), new Vector2(60, 28), 21);
            PowerText.resizeTextMinSize = 16;
            var typeRibbon = Attachment("Type Attachment", stats, new Vector2(edge - 16, 74), new Vector2(104, 80),
                SkillInfoAttachmentShape.TypeRibbon, DuelVisualTheme.Paper);
            typeAttachment = typeRibbon.GetComponent<SkillInfoAttachment>();
            typeLabel = Label("Type Label", typeRibbon, font, new Vector2(0, 24), new Vector2(88, 16), 12);
            AttackTypeText = Label("Attack Type", typeRibbon, font, new Vector2(0, 3), new Vector2(88, 24), 19);
            hitsLabel = Label("Hits Label", typeRibbon, font, new Vector2(-26, -18), new Vector2(34, 16), 12);
            hits = Label("Hits Value", typeRibbon, font, new Vector2(20, -18), new Vector2(52, 16), 13);

            keywords = Rect("Skill Keywords", root, Vector2.zero, new Vector2(width, 28));
            float badgeWidth = (width - 8) / 2f;
            var first = Tile("Keyword 1", keywords, -(badgeWidth + 8) / 2f, badgeWidth, 28);
            firstBadge = first.GetComponent<Image>();
            firstSymbol = Glyph(first, SkillInfoSymbol.Sword, new Vector2(-badgeWidth / 2f + 17, 0), 19);
            firstKeyword = Label("Keyword 1 Text", first, font, new Vector2(13, 0), new Vector2(badgeWidth - 34, 26), 15);
            var second = Tile("Keyword 2", keywords, (badgeWidth + 8) / 2f, badgeWidth, 28);
            secondBadge = second.GetComponent<Image>();
            secondSymbol = Glyph(second, SkillInfoSymbol.Hits, new Vector2(-badgeWidth / 2f + 17, 0), 19);
            secondKeyword = Label("Keyword 2 Text", second, font, new Vector2(13, 0), new Vector2(badgeWidth - 34, 26), 15);
            EffectText = Label(prefix + " Detail Effect", root, font, new Vector2(BodyLeftInset / 2f, -12), new Vector2(width - BodyLeftInset, 24), 16);
            EffectText.alignment = TextAnchor.UpperLeft;
            EffectText.resizeTextForBestFit = false;
            EffectText.lineSpacing = 1.1f;

            // Older consumers retain this reference; shared combat rules no longer belong in a skill card.
            DamageText = Label(prefix + " Damage Hint", root, font, Vector2.zero, Vector2.zero, 14);
            DamageText.gameObject.SetActive(false);
            Clear();
        }

        public void SetSkill(LegacySkill skill, bool enemy = false)
        {
            if (skill == null) { Clear(); return; }
            if (ReferenceEquals(shown, skill) && shownEnemy == enemy) return;
            shown = skill; shownEnemy = enemy;
            stats.gameObject.SetActive(true);
            keywords.gameObject.SetActive(true);
            bool defence = skill.Kind == LegacySkillKind.Defence;
            SetAttackType(skill.Property, defence);
            act.text = skill.Cost.ToString();
            powerLabel.text = defence ? "방어" : "위력";
            powerSymbol.SetSymbol(defence ? SkillInfoSymbol.Guard : SkillInfoSymbol.Sword);
            PowerText.text = CampaignSkillText.Power(skill);
            hitsLabel.text = defence ? "대응" : "타격";
            hits.text = defence ? "같은 칸" : skill.AttackCount + "회";
            string main, secondary = string.Empty, description;
            SkillInfoSymbol mainIcon, secondaryIcon = SkillInfoSymbol.Hits;
            KeywordTone mainTone = KeywordTone.Neutral, secondaryTone = KeywordTone.Neutral;
            switch (skill.Id)
            {
                case 1:
                    main = enemy ? "플레이어 ACT" : "ACT +1"; mainIcon = SkillInfoSymbol.Recovery;
                    secondary = "다음 턴"; secondaryIcon = SkillInfoSymbol.Act;
                    mainTone = secondaryTone = KeywordTone.Recovery;
                    description = "다음 턴 ACT 회복 +1"; break;
                case 3:
                    main = "후속 +10%"; mainIcon = SkillInfoSymbol.Followup;
                    secondary = "뒤 3칸"; secondaryIcon = SkillInfoSymbol.Hits;
                    mainTone = secondaryTone = KeywordTone.Followup;
                    description = "이번 턴 · 뒤 3칸 위력 +10%\n공격·방어 모두 적용 · 위력 분할\n소수점 버림 · 한 타 최소 1"; break;
                case 7:
                    main = enemy ? "플레이어 ACT" : "조건부 ACT +2"; mainIcon = SkillInfoSymbol.Recovery;
                    secondary = "타격 대응"; secondaryIcon = SkillInfoSymbol.Guard;
                    mainTone = KeywordTone.Recovery; secondaryTone = KeywordTone.Defence;
                    description = "같은 칸 상대가 타격일 때\n다음 턴 ACT 회복 +2"; break;
                case 8:
                    main = "피해 -30%"; mainIcon = SkillInfoSymbol.Guard;
                    secondary = "후속 보호"; secondaryIcon = SkillInfoSymbol.Reduction;
                    mainTone = secondaryTone = KeywordTone.Reduction;
                    description = "이번 턴 · 뒤 최대 10칸\n받는 피해 30% 감소"; break;
                case 9:
                    main = "후속 +3%"; mainIcon = SkillInfoSymbol.Followup;
                    secondary = "위력 지원"; secondaryIcon = SkillInfoSymbol.Sword;
                    mainTone = secondaryTone = KeywordTone.Followup;
                    description = "이번 턴 · 뒤 최대 10칸 위력 +3%\n공격·방어 모두 적용"; break;
                case 10:
                    main = "후속 +30%"; mainIcon = SkillInfoSymbol.Followup;
                    secondary = "다음 1칸"; secondaryIcon = SkillInfoSymbol.Hits;
                    mainTone = secondaryTone = KeywordTone.Followup;
                    description = "이번 턴 · 바로 다음 1칸\n공격·방어 위력 +30%"; break;
                case 12:
                    main = enemy ? "플레이어 ACT" : "ACT +3"; mainIcon = SkillInfoSymbol.Recovery;
                    secondary = "배율 +50%"; secondaryIcon = SkillInfoSymbol.Sword;
                    mainTone = KeywordTone.Recovery; secondaryTone = KeywordTone.HighPower;
                    description = "다음 턴 ACT 회복 +3\n이번 턴 · 다음 1칸 받는 피해 배율 +50%\n위력 2회 분할 · 버림 · 한 타 최소 1"; break;
                case 16:
                    main = "위력 편차"; mainIcon = SkillInfoSymbol.Variance;
                    secondary = "단타"; secondaryIcon = SkillInfoSymbol.Sword;
                    mainTone = KeywordTone.Variance;
                    description = "위력 편차가 큰 1회 공격\n최대 위력이 보장되지는 않습니다."; break;
                case 19:
                    main = "저항 회복"; mainIcon = SkillInfoSymbol.Recovery;
                    secondary = "최대치의 10%"; secondaryIcon = SkillInfoSymbol.Guard;
                    mainTone = KeywordTone.Recovery; secondaryTone = KeywordTone.Defence;
                    description = "기술 시작 시 최대 저항의 10% 회복\n반올림 · 최대치 제한\n같은 칸 상대 공격을 방어"; break;
                case 42:
                    main = "저항 -20"; mainIcon = SkillInfoSymbol.Reduction;
                    secondary = enemy ? "플레이어 ACT" : "ACT +3"; secondaryIcon = SkillInfoSymbol.Recovery;
                    mainTone = KeywordTone.Defence; secondaryTone = KeywordTone.Recovery;
                    description = "기술 시작 시 같은 칸 상대가 방어이면\n저항 직접 -20 · 다음 턴 ACT +3\n초과 저항 감소는 체력 피해 없음"; break;
                default:
                    bool strong = (LegacySkillRoles.Get(skill) & LegacySkillRole.HighPower) != 0;
                    main = defence ? "수치 방어" : strong ? "고화력" : skill.AttackCount > 1 ? "분할 연타" : "단타";
                    mainIcon = defence ? SkillInfoSymbol.Guard : strong ? SkillInfoSymbol.Sword : SkillInfoSymbol.Hits;
                    mainTone = defence ? KeywordTone.Defence : strong ? KeywordTone.HighPower
                        : skill.AttackCount > 1 ? KeywordTone.MultiHit : KeywordTone.Neutral;
                    if (strong)
                    {
                        secondary = skill.AttackCount > 1 ? "분할 연타" : "단타";
                        secondaryTone = skill.AttackCount > 1 ? KeywordTone.MultiHit : KeywordTone.Neutral;
                    }
                    description = defence ? "같은 순서의 상대 공격 피해를\n방어 수치만큼 줄입니다."
                        : skill.AttackCount > 1 ? "위력을 " + skill.AttackCount + "회로 나눠 공격\n소수점 버림 · 한 타 최소 1"
                        : "표시 위력으로 1회 공격";
                    break;
            }
            if (enemy && (LegacySkillRoles.Get(skill) & LegacySkillRole.ActRecovery) != 0)
                description += "\nACT 회복은 플레이어 전용";
            firstKeyword.text = main;
            firstSymbol.SetSymbol(mainIcon);
            StyleKeyword(firstBadge, firstKeyword, firstSymbol, mainTone);
            secondKeyword.text = secondary;
            secondSymbol.SetSymbol(secondaryIcon);
            StyleKeyword(secondBadge, secondKeyword, secondSymbol, secondaryTone);
            secondBadge.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            EffectText.text = description;
            DamageText.text = string.Empty;
            RefreshLayout();
        }

        public void Clear()
        {
            shown = null;
            stats.gameObject.SetActive(false);
            keywords.gameObject.SetActive(false);
            act.text = PowerText.text = hits.text = AttackTypeText.text = typeLabel.text = firstKeyword.text = secondKeyword.text = EffectText.text = DamageText.text = string.Empty;
            RefreshLayout();
        }

        public void SetEmptyMessage(string message)
        {
            Clear();
            EffectText.text = message ?? string.Empty;
            RefreshLayout();
        }

        public void PlaceTop(float y)
        {
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, y - Height / 2f);
        }

        private void RefreshLayout()
        {
            // Content owns its height; no per-frame layout group or reserved blank stat rows.
            float bodyWidth = root.sizeDelta.x - BodyLeftInset;
            bool hasKeywords = keywords.gameObject.activeSelf;
            bool hasSecond = hasKeywords && secondBadge.gameObject.activeSelf;
            float badgeWidth = hasSecond ? (bodyWidth - 8f) / 2f : bodyWidth;
            ArrangeKeyword(firstBadge, firstSymbol, firstKeyword, hasSecond ? -(badgeWidth + 8f) / 2f : 0f, badgeWidth);
            ArrangeKeyword(secondBadge, secondSymbol, secondKeyword, (badgeWidth + 8f) / 2f, badgeWidth);
            EffectText.rectTransform.sizeDelta = new Vector2(bodyWidth, 24f);
            float textHeight = Mathf.Ceil(Mathf.Max(20f, EffectText.preferredHeight)) + 2f;
            float keywordHeight = hasKeywords ? 36f : 0f;
            float height = Mathf.Max(72f, 16f + keywordHeight + textHeight);
            root.sizeDelta = new Vector2(root.sizeDelta.x, height);
            stats.sizeDelta = new Vector2(stats.sizeDelta.x, height);
            keywords.sizeDelta = new Vector2(bodyWidth, 28f);
            keywords.anchoredPosition = new Vector2(BodyLeftInset / 2f, height / 2f - 22f);
            EffectText.rectTransform.sizeDelta = new Vector2(bodyWidth, textHeight);
            EffectText.rectTransform.anchoredPosition = new Vector2(BodyLeftInset / 2f, height / 2f - 8f - keywordHeight - textHeight / 2f);
            costSeal.anchoredPosition = new Vector2(costSeal.anchoredPosition.x, height / 2f + 38f);
            typeAttachment.rectTransform.anchoredPosition = new Vector2(typeAttachment.rectTransform.anchoredPosition.x, height / 2f + 38f);
            powerPlate.anchoredPosition = new Vector2(powerPlate.anchoredPosition.x, -height / 2f + 32f);
        }

        private static void ArrangeKeyword(Image badge, SkillInfoGlyph glyph, Text label, float x, float width)
        {
            badge.rectTransform.anchoredPosition = new Vector2(x, 0f);
            badge.rectTransform.sizeDelta = new Vector2(width, 28f);
            glyph.rectTransform.anchoredPosition = new Vector2(-width / 2f + 17f, 0f);
            label.rectTransform.sizeDelta = new Vector2(width - 34f, 26f);
        }

        private void SetAttackType(LegacySkillProperty property, bool defence)
        {
            Color32 ink, paper;
            switch (property)
            {
                case LegacySkillProperty.Slash:
                    AttackTypeText.text = "참격"; ink = new Color32(116, 61, 45, 255); paper = new Color32(220, 195, 173, 255); break;
                case LegacySkillProperty.Hit:
                    AttackTypeText.text = "타격"; ink = new Color32(102, 75, 32, 255); paper = new Color32(223, 206, 163, 255); break;
                case LegacySkillProperty.Penetrate:
                    AttackTypeText.text = "관통"; ink = new Color32(51, 81, 107, 255); paper = new Color32(198, 211, 217, 255); break;
                default:
                    AttackTypeText.text = "방어"; ink = new Color32(69, 82, 47, 255); paper = new Color32(202, 208, 174, 255); break;
            }
            typeLabel.text = defence ? "기술 타입" : "공격 타입";
            typeAttachment.color = paper;
            AttackTypeText.color = typeLabel.color = hits.color = hitsLabel.color = ink;
        }

        private static RectTransform Attachment(string name, Transform parent, Vector2 center, Vector2 size,
            SkillInfoAttachmentShape shape, Color color)
        {
            var rect = Rect(name, parent, center, size);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var attachment = rect.gameObject.AddComponent<SkillInfoAttachment>();
            attachment.raycastTarget = false;
            attachment.color = color;
            attachment.Configure(shape);
            return rect;
        }

        private static void StyleKeyword(Image badge, Text label, SkillInfoGlyph glyph, KeywordTone tone)
        {
            // Muted dyes fit the paper/brass theme; opaque backgrounds keep ink contrast predictable.
            Color32 ink, paper;
            switch (tone)
            {
                case KeywordTone.Recovery:
                    ink = new Color32(36, 89, 75, 255); paper = new Color32(197, 212, 182, 255); break;
                case KeywordTone.Followup:
                    ink = new Color32(89, 65, 107, 255); paper = new Color32(215, 197, 214, 255); break;
                case KeywordTone.Reduction:
                    ink = new Color32(51, 81, 107, 255); paper = new Color32(198, 211, 217, 255); break;
                case KeywordTone.HighPower:
                    ink = new Color32(116, 61, 45, 255); paper = new Color32(220, 195, 173, 255); break;
                case KeywordTone.MultiHit:
                    ink = new Color32(102, 75, 32, 255); paper = new Color32(223, 206, 163, 255); break;
                case KeywordTone.Variance:
                    ink = new Color32(106, 76, 25, 255); paper = new Color32(224, 208, 166, 255); break;
                case KeywordTone.Defence:
                    ink = new Color32(69, 82, 47, 255); paper = new Color32(202, 208, 174, 255); break;
                default:
                    ink = new Color32(73, 63, 49, 255); paper = new Color32(215, 201, 172, 255); break;
            }
            badge.color = paper;
            label.color = glyph.color = ink;
        }

        private static RectTransform Tile(string name, Transform parent, float x, float width, float height = 52)
        {
            var rect = Rect(name, parent, new Vector2(x, 0), new Vector2(width, height));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.73f, .65f, .48f, .32f);
            image.raycastTarget = false;
            DuelVisualTheme.Frame(image);
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private static Text Label(string name, Transform parent, Font font, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.color = DuelVisualTheme.Ink;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = fontSize - 1; text.resizeTextMaxSize = fontSize;
            return text;
        }

        private static SkillInfoGlyph Glyph(Transform parent, SkillInfoSymbol symbol, Vector2 position, float size)
        {
            var rect = Rect(symbol + " Symbol", parent, position, Vector2.one * size);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var glyph = rect.gameObject.AddComponent<SkillInfoGlyph>();
            glyph.color = DuelVisualTheme.Ink; glyph.raycastTarget = false; glyph.SetSymbol(symbol);
            return glyph;
        }
    }

    public enum SkillInfoSymbol { Act, Sword, Guard, Hits, Recovery, Followup, Reduction, Variance }

    /// <summary>Small engraved pictograms; no texture assets or interactive surfaces.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillInfoGlyph : MaskableGraphic
    {
        private SkillInfoSymbol symbol;
        public void SetSymbol(SkillInfoSymbol value) { if (symbol == value) return; symbol = value; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (symbol)
            {
                case SkillInfoSymbol.Act:
                    Line(vh, -.1f, .45f, -.32f, 0); Line(vh, -.32f, 0, .12f, 0);
                    Line(vh, .12f, 0, -.08f, -.45f); Line(vh, -.08f, -.45f, .32f, .08f); break;
                case SkillInfoSymbol.Sword:
                    Line(vh, -.32f, -.35f, .28f, .3f); Line(vh, -.3f, -.05f, -.05f, -.3f);
                    Line(vh, .15f, .35f, .32f, .35f); Line(vh, .32f, .35f, .32f, .16f); break;
                case SkillInfoSymbol.Guard:
                    Line(vh, -.35f, .32f, .35f, .32f); Line(vh, -.35f, .32f, -.3f, -.12f);
                    Line(vh, -.3f, -.12f, 0, -.4f); Line(vh, 0, -.4f, .3f, -.12f);
                    Line(vh, .3f, -.12f, .35f, .32f); Line(vh, -.2f, 0, .2f, 0); break;
                case SkillInfoSymbol.Hits:
                    Line(vh, -.42f, -.2f, -.14f, .2f); Line(vh, -.14f, -.2f, .14f, .2f);
                    Line(vh, .14f, -.2f, .42f, .2f); break;
                case SkillInfoSymbol.Recovery:
                    Line(vh, -.32f, -.1f, -.32f, .3f); Line(vh, -.32f, .3f, .32f, .3f);
                    Line(vh, .32f, .3f, .32f, -.3f); Line(vh, .32f, -.3f, -.32f, -.3f);
                    Line(vh, -.32f, -.3f, -.12f, -.1f); Line(vh, -.32f, -.3f, -.12f, -.46f); break;
                case SkillInfoSymbol.Followup:
                    Line(vh, -.32f, -.28f, 0, .04f); Line(vh, 0, .04f, .32f, -.28f);
                    Line(vh, -.32f, .04f, 0, .36f); Line(vh, 0, .36f, .32f, .04f); break;
                case SkillInfoSymbol.Reduction:
                    Line(vh, -.32f, .3f, .32f, .3f); Line(vh, 0, .3f, 0, -.25f);
                    Line(vh, -.2f, -.05f, 0, -.28f); Line(vh, 0, -.28f, .2f, -.05f);
                    Line(vh, -.32f, -.42f, .32f, -.42f); break;
                case SkillInfoSymbol.Variance:
                    Line(vh, -.32f, -.3f, -.32f, .05f); Line(vh, 0, -.3f, 0, .35f);
                    Line(vh, .32f, -.3f, .32f, -.1f); break;
            }
        }

        private void Line(VertexHelper vh, float x1, float y1, float x2, float y2)
        {
            Rect r = rectTransform.rect;
            float size = Mathf.Min(r.width, r.height);
            Vector2 a = r.center + new Vector2(x1, y1) * size, b = r.center + new Vector2(x2, y2) * size;
            Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * Mathf.Max(1, size * .055f);
            int index = vh.currentVertCount;
            vh.AddVert(a - side, color, Vector2.zero); vh.AddVert(a + side, color, Vector2.zero);
            vh.AddVert(b + side, color, Vector2.zero); vh.AddVert(b - side, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
