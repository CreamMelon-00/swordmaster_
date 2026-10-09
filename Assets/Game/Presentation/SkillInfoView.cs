using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>Fixed-height skill facts shared by selection panels and held explanations.</summary>
    public sealed class SkillInfoView
    {
        public const float AttachmentOverhang = 0f;
        private const float BodyInset = 16f;
        private const float FixedBodyHeight = 206f;
        private const float ExpandedExperienceExtra = 54f;
        private readonly RectTransform root, stats, keywords, costSeal, powerPlate, experienceRoot, experienceFill, divider;
        private readonly Text act, powerLabel, hits, hitsLabel, typeLabel, firstKeyword, secondKeyword;
        private readonly Text experienceLabel, experienceValue;
        private readonly SkillInfoGlyph firstSymbol, secondSymbol, powerSymbol;
        private readonly Image firstBadge, secondBadge;
        private readonly SkillInfoAttachment typeAttachment;
        private readonly SkillKeywordHover keywordHover;
        // The loadout uses its former action row for a readable experience footer.
        private readonly bool expandedExperience;
        // How much larger than its standard size the card is drawn (the battle's held explanation), fittings and type alike.
        private readonly float scale;
        private LegacySkill shown;
        private CampaignOwnedSkill shownOwned;
        private int shownLevel, shownExperience, shownCycleUses = -1;
        private int? shownEffectiveCost;
        private string shownEffectivePower;
        private bool shownEnemy;
        private string shownAdditionalDescription;

        public GameObject Root => root.gameObject;
        public float Height => root.sizeDelta.y;
        /// <summary>The facts stay inside the card, so the held popup needs no side allowance.</summary>
        public float Overhang => AttachmentOverhang * scale;
        public Text PowerText { get; }
        public Text AttackTypeText { get; }
        public Text EffectText { get; }
        public Text DamageText { get; }

        /// <param name="scale">Draws the card larger than its standard size (its facts, type and fixed body height;
        /// <paramref name="width"/> is the body's width as drawn). 1 is the standard card.</param>
        public SkillInfoView(Transform parent, Font font, Vector2 center, float width, string prefix,
            float scale = 1f, bool expandedExperience = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.scale = scale > 0f && !float.IsInfinity(scale) ? scale : 1f;
            this.expandedExperience = expandedExperience;
            float bodyHeight = S(FixedBodyHeight + (expandedExperience ? ExpandedExperienceExtra : 0f));
            float innerWidth = width - S(BodyInset * 2f);
            root = Rect("Skill Summary", parent, center, new Vector2(width, bodyHeight));
            stats = Rect("Skill Attachments", root, Vector2.zero, new Vector2(width, bodyHeight));

            // The three facts form one compact read across the card, directly below its name and icon.
            float statY = bodyHeight / 2f - S(33f);
            float left = -width / 2f + S(BodyInset);
            costSeal = Attachment("ACT Attachment", stats, new Vector2(left + S(40f), statY),
                new Vector2(S(80f), S(42f)), SkillInfoAttachmentShape.PowerPlate, DuelVisualTheme.Surface);
            var costSymbol = Glyph(costSeal, LegacySkillSymbol.Act, new Vector2(S(-27f), 0f), S(13f));
            costSymbol.color = DuelVisualTheme.Accent;
            // The lightning glyph already identifies ACT; leave the number unobstructed.
            act = Label("ACT Value", costSeal, font, new Vector2(S(11f), 0f),
                new Vector2(S(46f), S(30f)), F(22));
            act.color = DuelVisualTheme.Foreground;

            powerPlate = Attachment("Power Attachment", stats, new Vector2(left + S(145f), statY),
                new Vector2(S(120f), S(42f)), SkillInfoAttachmentShape.PowerPlate, DuelVisualTheme.Surface);
            powerSymbol = Glyph(powerPlate, LegacySkillSymbol.Sword, new Vector2(S(-47f), 0f), S(14f));
            powerSymbol.color = DuelVisualTheme.Accent;
            powerLabel = Label("Power Label", powerPlate, font, new Vector2(S(-25f), 0f),
                new Vector2(S(35f), S(27f)), F(13));
            powerLabel.color = DuelVisualTheme.Foreground;
            PowerText = Label(prefix + " Detail Values", powerPlate, font, new Vector2(S(22f), 0f),
                new Vector2(S(72f), S(30f)), F(18));
            PowerText.color = DuelVisualTheme.Foreground;
            PowerText.resizeTextMinSize = F(13);

            float right = width / 2f - S(BodyInset);
            var typeRibbon = Attachment("Type Attachment", stats, new Vector2(right - S(47f), statY),
                new Vector2(S(94f), S(58f)), SkillInfoAttachmentShape.TypeRibbon, DuelVisualTheme.Paper);
            typeAttachment = typeRibbon.GetComponent<SkillInfoAttachment>();
            // The folded ribbon needs only the property and the number of hits.
            typeLabel = Label("Type Label", typeRibbon, font, new Vector2(0f, S(17f)),
                new Vector2(S(80f), S(15f)), F(11));
            typeLabel.gameObject.SetActive(false);
            AttackTypeText = Label("Attack Type", typeRibbon, font, new Vector2(0f, S(8f)),
                new Vector2(S(80f), S(20f)), F(17));
            hitsLabel = Label("Hits Label", typeRibbon, font, new Vector2(S(-25f), S(-15f)),
                new Vector2(S(32f), S(14f)), F(11));
            hitsLabel.gameObject.SetActive(false);
            hits = Label("Hits Value", typeRibbon, font, new Vector2(0f, S(-11f)),
                new Vector2(S(80f), S(17f)), F(13));

            divider = Rect("Skill Effect Rule", root, Vector2.zero, new Vector2(innerWidth, S(2f)));
            var line = divider.gameObject.AddComponent<Image>();
            line.color = new Color(DuelVisualTheme.Border.r, DuelVisualTheme.Border.g,
                DuelVisualTheme.Border.b, .74f);
            line.raycastTarget = false;

            EffectText = Label(prefix + " Detail Effect", root, font, Vector2.zero,
                new Vector2(innerWidth, S(80f)), F(17));
            EffectText.alignment = TextAnchor.UpperLeft;
            EffectText.resizeTextForBestFit = true;
            EffectText.resizeTextMinSize = F(11);
            EffectText.lineSpacing = 1.08f;

            keywords = Rect("Skill Keywords", root, Vector2.zero, new Vector2(innerWidth, S(28f)));
            float badgeWidth = (innerWidth - S(6f)) / 2f;
            var first = Tile("Keyword 1", keywords, -(badgeWidth + S(6f)) / 2f, badgeWidth, S(28f));
            firstBadge = first.GetComponent<Image>();
            firstSymbol = Glyph(first, LegacySkillSymbol.Sword,
                new Vector2(-badgeWidth / 2f + S(15f), 0f), S(17f));
            firstKeyword = Label("Keyword 1 Text", first, font, new Vector2(S(12f), 0f),
                new Vector2(badgeWidth - S(32f), S(26f)), F(15));
            firstKeyword.resizeTextMinSize = F(10);
            var second = Tile("Keyword 2", keywords, (badgeWidth + S(6f)) / 2f, badgeWidth, S(28f));
            secondBadge = second.GetComponent<Image>();
            secondSymbol = Glyph(second, LegacySkillSymbol.Hits,
                new Vector2(-badgeWidth / 2f + S(15f), 0f), S(17f));
            secondKeyword = Label("Keyword 2 Text", second, font, new Vector2(S(12f), 0f),
                new Vector2(badgeWidth - S(32f), S(26f)), F(15));
            secondKeyword.resizeTextMinSize = F(10);

            // Progress belongs to the player's owned skill. The loadout has room for a larger footer.
            float experienceHeight = expandedExperience ? 68f : 20f;
            float experienceTextY = expandedExperience ? 12f : 5f;
            float experienceTextHeight = expandedExperience ? 24f : 10f;
            int experienceFontSize = expandedExperience ? 18 : 11;
            experienceRoot = Rect("Skill Experience", root, Vector2.zero,
                new Vector2(innerWidth, S(experienceHeight)));
            experienceLabel = Label("Skill Experience Level", experienceRoot, font,
                new Vector2(-innerWidth / 4f, S(experienceTextY)),
                new Vector2(innerWidth / 2f, S(experienceTextHeight)), F(experienceFontSize));
            experienceLabel.alignment = TextAnchor.MiddleLeft;
            experienceValue = Label("Skill Experience Value", experienceRoot, font,
                new Vector2(innerWidth / 4f, S(experienceTextY)),
                new Vector2(innerWidth / 2f, S(experienceTextHeight)), F(experienceFontSize));
            experienceValue.alignment = TextAnchor.MiddleRight;
            var track = Rect("Skill Experience Track", experienceRoot,
                new Vector2(0f, S(expandedExperience ? -19f : -5f)),
                new Vector2(innerWidth, S(expandedExperience ? 8f : 4f)));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = DuelVisualTheme.Border;
            trackImage.raycastTarget = false;
            experienceFill = Rect("Skill Experience Fill", track, Vector2.zero, Vector2.zero);
            experienceFill.anchorMin = new Vector2(0f, 0f);
            experienceFill.anchorMax = new Vector2(0f, 1f);
            experienceFill.pivot = new Vector2(0f, .5f);
            var fillImage = experienceFill.gameObject.AddComponent<Image>();
            fillImage.color = DuelVisualTheme.Ink;
            fillImage.raycastTarget = false;

            // Older consumers retain this reference; shared combat rules no longer belong in a skill card.
            DamageText = Label(prefix + " Damage Hint", root, font, Vector2.zero, Vector2.zero, F(14));
            DamageText.gameObject.SetActive(false);
            keywordHover = SkillKeywordHover.Attach(root, firstBadge, firstKeyword, secondBadge,
                secondKeyword, EffectText, font, this.scale);
            Clear();
        }

        public void SetSkill(LegacySkill skill, bool enemy = false, string additionalDescription = null,
            CampaignOwnedSkill owned = null, int cycleUses = -1, int? effectiveCost = null, string effectivePower = null)
        {
            if (skill == null) { Clear(); return; }
            if (enemy || owned?.SkillId != skill.Id) owned = null;
            if (owned != null) skill = owned.Skill;
            if (enemy) { cycleUses = -1; effectiveCost = null; effectivePower = null; }
            int level = owned?.Level ?? 0, experience = owned?.Experience ?? 0;
            if (ReferenceEquals(shown, skill) && shownEnemy == enemy && shownAdditionalDescription == additionalDescription &&
                ReferenceEquals(shownOwned, owned) && shownLevel == level && shownExperience == experience &&
                shownCycleUses == cycleUses && shownEffectiveCost == effectiveCost && shownEffectivePower == effectivePower) return;
            shown = skill; shownEnemy = enemy; shownAdditionalDescription = additionalDescription;
            shownOwned = owned; shownLevel = level; shownExperience = experience;
            shownCycleUses = cycleUses; shownEffectiveCost = effectiveCost; shownEffectivePower = effectivePower;
            stats.gameObject.SetActive(true);
            keywords.gameObject.SetActive(true);
            bool defence = skill.Kind == LegacySkillKind.Defence;
            SetAttackType(skill.Property, defence);
            act.text = (effectiveCost ?? skill.Cost).ToString();
            // The sword or shield glyph identifies the value without repeating a text label.
            powerLabel.text = string.Empty;
            powerLabel.gameObject.SetActive(false);
            powerSymbol.SetSymbol(defence ? LegacySkillSymbol.Guard : LegacySkillSymbol.Sword);
            PowerText.rectTransform.anchoredPosition = new Vector2(S(8f), 0f);
            PowerText.rectTransform.sizeDelta = new Vector2(S(88f), S(30f));
            PowerText.text = effectivePower ?? CampaignSkillText.Power(skill);
            hitsLabel.text = string.Empty;
            hits.text = defence ? "같은 칸" : skill.AttackCount + "회";
            SkillInfoContent content = SkillInfoContent.For(skill, enemy);
            LegacySkillEffect effect = LegacySkillDefinitions.Find(skill)?.Effect;
            bool showCycleCount = cycleUses >= 0 && effect?.HasCycle == true;
            string cycleLabel = showCycleCount ? "순환 " + cycleUses + "/" + effect.CycleMaxCount : null;
            firstKeyword.text = showCycleCount && content.Main == "순환" ? cycleLabel : content.Main;
            firstSymbol.SetSymbol(content.MainSymbol);
            StyleKeyword(firstBadge, firstKeyword, firstSymbol, content.MainTone);
            secondKeyword.text = showCycleCount && content.Secondary == "순환" ? cycleLabel : content.Secondary;
            secondSymbol.SetSymbol(content.SecondarySymbol);
            StyleKeyword(secondBadge, secondKeyword, secondSymbol, content.SecondaryTone);
            secondBadge.gameObject.SetActive(!string.IsNullOrEmpty(content.Secondary));
            EffectText.text = string.IsNullOrEmpty(additionalDescription) ? content.Description
                : string.IsNullOrEmpty(content.Description) ? additionalDescription
                : content.Description + "\n" + additionalDescription;
            if (string.IsNullOrWhiteSpace(EffectText.text)) EffectText.text = "추가 효과 없음";
            EffectText.color = DuelVisualTheme.Ink;
            experienceRoot.gameObject.SetActive(owned != null);
            if (owned != null)
            {
                experienceLabel.text = "숙련 " + owned.Level + "/" + CampaignOwnedSkill.MaxLevel;
                experienceValue.text = owned.IsMaxLevel ? "MAX" : owned.ExperienceThisLevel + "/" + owned.ExperienceRequired;
                float progress = owned.IsMaxLevel ? 1f : owned.ExperienceRequired > 0
                    ? Mathf.Clamp01((float)owned.ExperienceThisLevel / owned.ExperienceRequired) : 0f;
                experienceFill.sizeDelta = new Vector2(experienceRoot.rect.width * progress, 0f);
            }
            DamageText.text = string.Empty;
            RefreshLayout();
            keywordHover.SetTerms(firstKeyword.text, secondKeyword.text, EffectText.text);
        }

        public void Clear()
        {
            shown = null;
            shownOwned = null;
            shownLevel = shownExperience = 0;
            shownCycleUses = -1;
            shownEffectiveCost = null;
            shownEffectivePower = null;
            shownAdditionalDescription = null;
            stats.gameObject.SetActive(false);
            divider.gameObject.SetActive(false);
            keywords.gameObject.SetActive(false);
            keywordHover.Clear();
            experienceRoot.gameObject.SetActive(false);
            experienceLabel.text = experienceValue.text = string.Empty;
            experienceFill.sizeDelta = Vector2.zero;
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
            // Every skill keeps one silhouette. Longer copy shrinks inside the reserved description area.
            float top = root.sizeDelta.y / 2f, bottom = -top;
            float innerWidth = root.sizeDelta.x - S(BodyInset * 2f);
            bool hasFacts = stats.gameObject.activeSelf;
            bool hasKeywords = keywords.gameObject.activeSelf;
            bool hasExperience = experienceRoot.gameObject.activeSelf;
            divider.gameObject.SetActive(hasFacts);
            divider.anchoredPosition = new Vector2(0f, top - S(72f));

            bool hasSecond = hasKeywords && secondBadge.gameObject.activeSelf;
            float badgeWidth = hasSecond ? (innerWidth - S(6f)) / 2f : innerWidth;
            ArrangeKeyword(firstBadge, firstSymbol, firstKeyword,
                hasSecond ? -(badgeWidth + S(6f)) / 2f : 0f, badgeWidth);
            ArrangeKeyword(secondBadge, secondSymbol, secondKeyword,
                (badgeWidth + S(6f)) / 2f, badgeWidth);

            float keywordY = hasExperience ? bottom + S(expandedExperience ? 91f : 37f)
                : bottom + S(19f);
            keywords.sizeDelta = new Vector2(innerWidth, S(28f));
            keywords.anchoredPosition = new Vector2(0f, keywordY);
            experienceRoot.anchoredPosition = new Vector2(0f, bottom + S(expandedExperience ? 36f : 12f));

            float textTop = hasFacts ? top - S(80f) : top - S(16f);
            float textBottom = hasKeywords ? keywordY + S(18f) : bottom + S(16f);
            EffectText.rectTransform.sizeDelta = new Vector2(innerWidth, textTop - textBottom);
            EffectText.rectTransform.anchoredPosition =
                new Vector2(0f, (textTop + textBottom) / 2f);
        }

        private void ArrangeKeyword(Image badge, SkillInfoGlyph glyph, Text label, float x, float width)
        {
            badge.rectTransform.anchoredPosition = new Vector2(x, 0f);
            badge.rectTransform.sizeDelta = new Vector2(width, S(28f));
            glyph.rectTransform.anchoredPosition = new Vector2(-width / 2f + S(15f), 0f);
            label.rectTransform.sizeDelta = new Vector2(width - S(32f), S(26f));
        }

        // A standard size, and a standard type size, at this card's scale.
        private float S(float size) => size * scale;
        private int F(int points) => Mathf.RoundToInt(points * scale);

        private void SetAttackType(LegacySkillProperty property, bool defence)
        {
            Color32 ink, paper;
            switch (property)
            {
                case LegacySkillProperty.Slash:
                    ink = new Color32(116, 61, 45, 255); paper = new Color32(220, 195, 173, 255); break;
                case LegacySkillProperty.Hit:
                    ink = new Color32(102, 75, 32, 255); paper = new Color32(223, 206, 163, 255); break;
                case LegacySkillProperty.Penetrate:
                    ink = new Color32(51, 81, 107, 255); paper = new Color32(198, 211, 217, 255); break;
                default:
                    ink = new Color32(69, 82, 47, 255); paper = new Color32(202, 208, 174, 255); break;
            }
            // 숨고르기 (no property) wears the guard's label, as it wears its colours.
            AttackTypeText.text = LegacySkillLabels.Property(property == LegacySkillProperty.None ? LegacySkillProperty.Defence : property);
            typeLabel.text = string.Empty;
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

        private static void StyleKeyword(Image badge, Text label, SkillInfoGlyph glyph, LegacySkillTone tone)
        {
            // Muted dyes fit the paper/brass theme; opaque backgrounds keep ink contrast predictable.
            Color32 ink, paper;
            switch (tone)
            {
                case LegacySkillTone.Recovery:
                    ink = new Color32(36, 89, 75, 255); paper = new Color32(197, 212, 182, 255); break;
                case LegacySkillTone.Followup:
                    ink = new Color32(89, 65, 107, 255); paper = new Color32(215, 197, 214, 255); break;
                case LegacySkillTone.Cycle:
                    ink = new Color32(34, 83, 82, 255); paper = new Color32(183, 215, 204, 255); break;
                case LegacySkillTone.Reduction:
                    ink = new Color32(51, 81, 107, 255); paper = new Color32(198, 211, 217, 255); break;
                case LegacySkillTone.HighPower:
                    ink = new Color32(116, 61, 45, 255); paper = new Color32(220, 195, 173, 255); break;
                case LegacySkillTone.MultiHit:
                    ink = new Color32(102, 75, 32, 255); paper = new Color32(223, 206, 163, 255); break;
                case LegacySkillTone.Variance:
                    ink = new Color32(106, 76, 25, 255); paper = new Color32(224, 208, 166, 255); break;
                case LegacySkillTone.Defence:
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

        private static SkillInfoGlyph Glyph(Transform parent, LegacySkillSymbol symbol, Vector2 position, float size)
        {
            var rect = Rect(symbol + " Symbol", parent, position, Vector2.one * size);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var glyph = rect.gameObject.AddComponent<SkillInfoGlyph>();
            glyph.color = DuelVisualTheme.Ink; glyph.raycastTarget = false; glyph.SetSymbol(symbol);
            return glyph;
        }
    }

    /// <summary>Small engraved pictograms; no texture assets or interactive surfaces.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillInfoGlyph : MaskableGraphic
    {
        private LegacySkillSymbol symbol;
        public void SetSymbol(LegacySkillSymbol value) { if (symbol == value) return; symbol = value; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (symbol)
            {
                case LegacySkillSymbol.Act:
                    Line(vh, -.1f, .45f, -.32f, 0); Line(vh, -.32f, 0, .12f, 0);
                    Line(vh, .12f, 0, -.08f, -.45f); Line(vh, -.08f, -.45f, .32f, .08f); break;
                case LegacySkillSymbol.Sword:
                    Line(vh, -.32f, -.35f, .28f, .3f); Line(vh, -.3f, -.05f, -.05f, -.3f);
                    Line(vh, .15f, .35f, .32f, .35f); Line(vh, .32f, .35f, .32f, .16f); break;
                case LegacySkillSymbol.Guard:
                    Line(vh, -.35f, .32f, .35f, .32f); Line(vh, -.35f, .32f, -.3f, -.12f);
                    Line(vh, -.3f, -.12f, 0, -.4f); Line(vh, 0, -.4f, .3f, -.12f);
                    Line(vh, .3f, -.12f, .35f, .32f); Line(vh, -.2f, 0, .2f, 0); break;
                case LegacySkillSymbol.Hits:
                    Line(vh, -.42f, -.2f, -.14f, .2f); Line(vh, -.14f, -.2f, .14f, .2f);
                    Line(vh, .14f, -.2f, .42f, .2f); break;
                case LegacySkillSymbol.Recovery:
                    Line(vh, -.32f, -.1f, -.32f, .3f); Line(vh, -.32f, .3f, .32f, .3f);
                    Line(vh, .32f, .3f, .32f, -.3f); Line(vh, .32f, -.3f, -.32f, -.3f);
                    Line(vh, -.32f, -.3f, -.12f, -.1f); Line(vh, -.32f, -.3f, -.12f, -.46f); break;
                case LegacySkillSymbol.Followup:
                    Line(vh, -.32f, -.28f, 0, .04f); Line(vh, 0, .04f, .32f, -.28f);
                    Line(vh, -.32f, .04f, 0, .36f); Line(vh, 0, .36f, .32f, .04f); break;
                case LegacySkillSymbol.Cycle:
                    Line(vh, -.22f, .29f, .13f, .29f); Line(vh, .13f, .29f, .31f, .10f);
                    Line(vh, .31f, .10f, .31f, -.13f); Line(vh, .31f, -.13f, .15f, -.30f);
                    Line(vh, .15f, -.30f, -.17f, -.30f); Line(vh, -.17f, -.30f, -.32f, -.11f);
                    Line(vh, -.32f, -.11f, -.32f, .13f); Line(vh, -.32f, .13f, -.22f, .29f);
                    Line(vh, -.22f, .29f, -.19f, .08f); Line(vh, -.22f, .29f, -.03f, .19f); break;
                case LegacySkillSymbol.Reduction:
                    Line(vh, -.32f, .3f, .32f, .3f); Line(vh, 0, .3f, 0, -.25f);
                    Line(vh, -.2f, -.05f, 0, -.28f); Line(vh, 0, -.28f, .2f, -.05f);
                    Line(vh, -.32f, -.42f, .32f, -.42f); break;
                case LegacySkillSymbol.Variance:
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
