using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The curriculum page: a HOI4 focus-style tree on the left, the selected node on the right, and over them a
    /// row with the reset, the 수련 점수 balance and what it buys. Clicking a node only selects it; one explicit button spends
    /// its 수련 점수 and learns it at once. CampaignRun owns the rules.
    /// The tree scrolls vertically inside its frame (<c>Curriculum Viewport</c>, a <see cref="ScrollRect"/> taking the wheel,
    /// a drag anywhere on it, cards included, and a thin bar): the trunk on top, then the three schools, each on a band of its
    /// keyword's colour with its name at the band's upper left (the trunk and the bridges have neither). A bridge card sits
    /// on the line between two bands over a plate of both colours and carries a <c>다리</c> tag; a school's capstone carries
    /// <c>정수</c> and an ornate rim, and a gold border once completed, when its school's name turns its keyword's colour. The
    /// nodes the last completion opened carry <c>새</c> until they are looked at. A page opened with an empty view state (the
    /// first time, or after the lobby's view was reset) scrolls to a new node, else the first affordable one, else the first
    /// one available, and shows it; a page rebuilt with the state of the last one keeps its selection and where it was
    /// (<see cref="ViewState.TreeScroll"/>, across tabs and battles too). Selecting a card scrolls it into view.
    /// Learning a node is its moment: the card swells under a brass flash (<see cref="CurriculumLearnPulse"/>), the banner
    /// under the tree names the new technique, the course it finished and what it opened, and the page turns to the nodes it
    /// opened (the lobby plays the lane's sound). Without a moment the banner says how 수련 점수 are earned.</summary>
    public sealed class CampaignCurriculumHud : IDisposable
    {
        public sealed class ViewState
        {
            public string SelectedNodeId { get; internal set; }
            /// <summary>The tree's scroll position (its content's anchored position) as the page last left it, so a rebuild
            /// (익히기, 초기화) stays put. Null until a page has been built, and after <see cref="Reset"/> or
            /// <see cref="Reopen"/>: the next page then scrolls to its focus.</summary>
            public Vector2? TreeScroll { get; internal set; }
            /// <summary>A node just learned whose moment the next page plays (the lobby rebuilds its page on every change):
            /// set by the learn button, or by the lobby for a learn it is told of. The page that plays it clears it.</summary>
            internal string PendingLearn { get; set; }
            /// <summary>The completion the "새" tags below belong to (the last completed node's id).</summary>
            internal string NewTagsFrom { get; set; }
            /// <summary>The "새" nodes the player has looked at; they lose the tag.</summary>
            internal HashSet<string> SeenNewTags { get; } = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>How often the page was opened afresh: a page built before the last <see cref="Reopen"/> keeps its
            /// scroll to itself when it goes.</summary>
            internal int Openings { get; private set; }

            public void Reset()
            {
                Reopen();
                NewTagsFrom = null;
                SeenNewTags.Clear();
            }

            /// <summary>Lets the next page open afresh: the selection and the scroll go, so it focuses again, but the "새"
            /// tags already looked at stay cleared. A page still open keeps its scroll to itself when it goes.</summary>
            public void Reopen()
            {
                SelectedNodeId = null;
                TreeScroll = null;
                PendingLearn = null;
                Openings++;
            }
        }

        private sealed class NodeCard
        {
            public CurriculumNode Node;
            public Button Button;
            public Image Background, Icon, Marker;
            /// <summary>The learning moment's brass flash over the card; made the first time it is learned on this page.</summary>
            public Image Flash;
            public Text Title, Status, Tag;
            /// <summary>A completed capstone's gold border; null on other cards.</summary>
            public GameObject Seal;
            public bool Capstone, Bridge;
            /// <summary>The granted skill's core mechanic (<see cref="SkillRolePip"/>); null for a node that grants none.</summary>
            public SkillRolePip Role;
        }

        public const float NodeWidth = 116f, NodeHeight = 100f;
        /// <summary>The scrolling content's width; its height follows the tree's rows (<see cref="ContentHeightFor"/>).</summary>
        public const float ContentWidth = 760f;
        /// <summary>The extra room under the trunk, where the schools fork and their names sit on their bands.</summary>
        public const float ForkGap = 32f;
        /// <summary>The first row of the schools: rows from here on sit <see cref="ForkGap"/> lower.</summary>
        public const int ForkRow = 3;
        /// <summary>How far a selected card is kept from the window's edge when it is scrolled into view.</summary>
        public const float FocusMargin = 16f;
        /// <summary>The window's inset from the frame's top and bottom (the brass rim stays clear of scrolling cards).</summary>
        public const float ViewportInset = 8f;
        /// <summary>The card tags: a node the last completion opened, a bridge between two schools, a school's capstone.</summary>
        public const string NewTag = "새", BridgeTag = "다리", CapstoneTag = "정수";
        /// <summary>The tree frame's height: the window under the top row, with the banner's room left under it.</summary>
        public const float TreeHeight = 476f;
        private const float TreeWidth = 760f, ColumnWidth = 123f, RowHeight = 168f;
        private const float FirstRowCentre = 124f, ContentFoot = 34f, BandLeft = 8f, BandHeadRoom = 40f, BandFoot = 12f;
        private const float BandAlpha = .13f, BridgeAlpha = .85f, BridgePad = 4f, SealWidth = 3f;
        private const float BannerHeight = 64f, BannerGap = 8f, TopRowY = 246f;
        // The frame's top stays under the top row (y 219); the banner sits under its foot.
        private static readonly Vector2 TreeCenter = new Vector2(-216f, 219f - TreeHeight * .5f);
        private static readonly Vector2 BannerCenter = new Vector2(TreeCenter.x, TreeCenter.y - TreeHeight * .5f - BannerGap - BannerHeight * .5f);
        private readonly RectTransform root, tree, viewport, content, detailFrame, detailSurface, detailRule;
        private readonly ScrollRect scroll;
        private readonly Dictionary<CurriculumBranch, Text> headings = new Dictionary<CurriculumBranch, Text>();
        private readonly HashSet<string> newNodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly LegacyDuelArt art;
        private readonly CampaignRun run;
        private readonly Action<string> learn;
        private readonly Action reset;
        private readonly Text points, status, completedCount, detailName, purpose, requirement, exclusive, availability, actionCaption, resetCaption;
        private readonly Text bannerTitle, bannerDetail;
        private readonly Image banner, bannerAccent, bannerIconFrame, bannerIcon;
        private readonly SkillLaneBadge detailStyle;
        private readonly SkillInfoView detailInfo;
        private readonly Image detailIcon;
        private readonly Button primaryAction, resetButton;
        private readonly List<NodeCard> cards = new List<NodeCard>();
        private readonly int opening;
        private bool disposed, resetArmed;
        // The node whose learning moment this page shows in its banner; null for the resting banner.
        private string learnedNodeId;
        private static Color Border => DuelVisualTheme.Border;
        private static Color Accent => DuelVisualTheme.Accent;
        private static Color Foreground => DuelVisualTheme.Foreground;
        private static Color Muted => DuelVisualTheme.Muted;

        public GameObject Root => root.gameObject;
        public ViewState State { get; }
        public bool IsResetArmed => !disposed && resetArmed;
        /// <summary>The tree's scroll view (on its frame): vertical only, clamped, no inertia.</summary>
        public ScrollRect TreeScroll => scroll;
        /// <summary>How far the tree is scrolled down now (0 at its top).</summary>
        public float ScrollOffset => disposed ? 0f : content.anchoredPosition.y;
        /// <summary>The farthest the tree scrolls: its content's height less its window's.</summary>
        public float MaximumScroll => disposed ? 0f : Mathf.Max(0f, content.sizeDelta.y - viewport.rect.height);
        /// <summary>Whether the node carries the "새" tag now.</summary>
        public bool IsNew(string id) => !disposed && newNodes.Contains(id);
        /// <summary>The node whose learning moment the banner shows, or null while it says how points are earned.</summary>
        public string LearnedNodeId => disposed ? null : learnedNodeId;

        /// <summary>Whether the node's whole card shows in the tree's window now.</summary>
        public bool IsNodeInView(string id)
        {
            CurriculumNode node = run.Curriculum.Tree.Find(id);
            if (disposed || node == null) return false;
            float centre = -NodePosition(node).y - ScrollOffset;
            return centre - NodeHeight * .5f >= -.5f && centre + NodeHeight * .5f <= viewport.rect.height + .5f;
        }

        public CampaignCurriculumHud(RectTransform parent, LegacyDuelArt art, CampaignRun run,
            Action<string> learn, Action reset, ViewState state = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.learn = learn;
            this.reset = reset;
            State = state ?? new ViewState();
            opening = State.Openings;
            root = Rect("Curriculum Content", parent, Vector2.zero, Vector2.zero);
            Stretch(root);

            // The top row: the reset, the balance on its plate, what it buys now, and how much is done.
            resetButton = ActionButton("Curriculum Reset", root, "커리큘럼 초기화", new Vector2(-487f, TopRowY),
                new Vector2(220f, 42f), ResetClicked);
            resetCaption = resetButton.GetComponentInChildren<Text>();
            // Armed, it says what the reset gives back on a second line.
            Fit(resetCaption, 11, 20);
            var plate = Panel("Curriculum Points Plate", root, new Vector2(-272f, TopRowY), new Vector2(188f, 44f),
                DuelVisualTheme.RaisedSurface);
            DuelVisualTheme.Frame(plate);
            points = Label("Curriculum Points", plate.transform, Vector2.zero, new Vector2(176f, 40f), 24, Foreground,
                TextAnchor.MiddleCenter);
            points.fontStyle = FontStyle.Bold;
            Fit(points, 16, 24);
            status = Label("Curriculum Status", root, new Vector2(-64f, TopRowY), new Vector2(208f, 44f), 18, Accent);
            Fit(status, 12, 18);
            completedCount = Label("Curriculum Completed Count", root, new Vector2(106f, TopRowY), new Vector2(116f, 30f),
                17, Muted, TextAnchor.MiddleRight);
            Fit(completedCount, 12, 17);

            var treeFrame = Panel("Curriculum Tree", root, TreeCenter, new Vector2(TreeWidth, TreeHeight), DuelVisualTheme.Track);
            DuelVisualTheme.DressPanel(treeFrame);
            tree = treeFrame.rectTransform;
            // The window clips the scrolling content and takes the wheel and drags where no card is; cards pass drags on
            // (a button handles no drag), so dragging on a card scrolls too.
            var window = Panel("Curriculum Viewport", tree, Vector2.zero, Vector2.zero, Color.clear);
            window.raycastTarget = true;
            viewport = window.rectTransform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(0f, ViewportInset);
            viewport.offsetMax = new Vector2(0f, -ViewportInset);
            var mask = window.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, 24);
            content = Rect("Curriculum Tree Content", viewport, Vector2.zero,
                new Vector2(ContentWidth, ContentHeightFor(run.Curriculum.Tree)));
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll = treeFrame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = BuildScrollbar();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            // Back to front: the bands and their names, the bridges' plates, the links, the cards.
            BuildBands();
            BuildBridgePlates();
            BuildConnectors();
            foreach (CurriculumNode node in run.Curriculum.Tree.Nodes) AddCard(node);

            // The banner under the tree: a learning moment's technique on its lane's dye (the 2026-10-13 result row's
            // icon, stripe and wording), or how 수련 점수 are earned.
            banner = Panel("Curriculum Banner", root, BannerCenter, new Vector2(TreeWidth, BannerHeight), DuelVisualTheme.RaisedSurface);
            DuelVisualTheme.Frame(banner);
            bannerAccent = Panel("Curriculum Banner Accent", banner.transform, new Vector2(-TreeWidth * .5f + 2f, 0f),
                new Vector2(4f, BannerHeight), Border);
            bannerIconFrame = Panel("Curriculum Banner Icon Frame", banner.transform, new Vector2(-TreeWidth * .5f + 36f, 0f),
                new Vector2(48f, 48f), Accent);
            Panel("Curriculum Banner Icon Surface", bannerIconFrame.transform, Vector2.zero, new Vector2(44f, 44f), DuelVisualTheme.Surface);
            bannerIcon = Panel("Curriculum Banner Icon", bannerIconFrame.transform, Vector2.zero, new Vector2(40f, 40f), Color.white);
            bannerIcon.preserveAspect = true;
            bannerTitle = Label("Curriculum Banner Title", banner.transform, Vector2.zero, new Vector2(TreeWidth - 92f, 26f), 19,
                Accent);
            Fit(bannerTitle, 13, 19);
            bannerDetail = Label("Curriculum Banner Detail", banner.transform, Vector2.zero, new Vector2(TreeWidth - 92f, 24f), 16,
                Foreground);
            Fit(bannerDetail, 12, 16);
            ShowRestingBanner();

            var detailBorder = Panel("Curriculum Selected Detail", root, new Vector2(423f, -50f), new Vector2(384f, 644f), Border);
            var detail = Panel("Curriculum Detail Surface", detailBorder.transform, Vector2.zero, new Vector2(380f, 640f), DuelVisualTheme.Paper);
            detailFrame = detailBorder.rectTransform;
            detailSurface = detail.rectTransform;
            DuelVisualTheme.Frame(detail, true);
            detailStyle = new SkillLaneBadge(detail.transform, art.UIFont, "Curriculum Detail Heading", Vector2.zero, 250f, 24f, 16);
            detailIcon = Panel("Curriculum Detail Icon", detail.transform, Vector2.zero, Vector2.one * 64f, Color.white);
            detailIcon.preserveAspect = true;
            detailName = Label("Curriculum Detail Name", detail.transform, Vector2.zero, new Vector2(162f, 36f), 24);
            Fit(detailName, 17, 24);
            // Wider than the name: a bridge's line names both its schools.
            purpose = Label("Curriculum Detail Purpose", detail.transform, Vector2.zero, new Vector2(240f, 26f), 17);
            Fit(purpose, 11, 17);
            detailInfo = new SkillInfoView(detail.transform, art.UIFont, new Vector2(0f, 64f), 334f, "Curriculum");
            detailRule = Panel("Curriculum Detail Rule", detail.transform, Vector2.zero, new Vector2(334f, 2f), Border).rectTransform;
            requirement = Label("Curriculum Requirement", detail.transform, Vector2.zero, new Vector2(334f, 26f), 17);
            exclusive = Label("Curriculum Exclusive", detail.transform, Vector2.zero, new Vector2(334f, 26f), 17);
            Fit(requirement, 11, 17);
            Fit(exclusive, 11, 17);
            availability = Label("Curriculum Availability", detail.transform, Vector2.zero, new Vector2(334f, 44f), 17);
            // Longer explanations shrink to stay within the two lines reserved for them.
            Fit(availability, 12, 17);
            detailName.color = purpose.color = requirement.color = exclusive.color = availability.color = DuelVisualTheme.Ink;
            primaryAction = ActionButton("Curriculum Primary Action", detail.transform, "익히기", Vector2.zero,
                new Vector2(334f, 50f), PerformLearn, true);
            actionCaption = primaryAction.GetComponentInChildren<Text>();
            Refresh();
            // A rebuild stays where the page was; a page opened afresh scrolls to what matters now.
            if (State.TreeScroll.HasValue) ScrollTo(State.TreeScroll.Value.y);
            else Focus(FocusNodeId() ?? State.SelectedNodeId, true);
            // A page built for a learn (the lobby rebuilds on every change) plays its moment.
            TakePendingLearn();
            State.TreeScroll = content.anchoredPosition;
        }

        /// <summary>A node card's centre in the scrolling content, from the content's upper left corner (x right, y up):
        /// columns 123 apart, rows 168 apart, and the schools' rows (<see cref="ForkRow"/> on) a further
        /// <see cref="ForkGap"/> down. The trunk's column 2.5 sits in the middle (x 377).</summary>
        public static Vector2 NodePosition(CurriculumNode node)
            => new Vector2(BandLeft + ColumnWidth * .5f + node.Column * ColumnWidth, -RowCentre(node.Row));

        /// <summary>The content height a tree needs: its last row's cards and a foot under them, never less than the window
        /// (the frame less its insets). The six rows of the default tree make 1080.</summary>
        public static float ContentHeightFor(CurriculumTree source)
        {
            int last = 0;
            if (source != null) foreach (CurriculumNode node in source.Nodes) last = Math.Max(last, node.Row);
            return Mathf.Max(TreeHeight - 2f * ViewportInset, RowCentre(last) + NodeHeight * .5f + ContentFoot);
        }

        private static float RowCentre(int row) => FirstRowCentre + RowHeight * row + (row >= ForkRow ? ForkGap : 0f);

        public void SelectNode(string id)
        {
            if (disposed || run.Curriculum.Tree.Find(id) == null) return;
            State.SelectedNodeId = id;
            // Looking at a new node clears its tag.
            if (newNodes.Contains(id)) State.SeenNewTags.Add(id);
            Focus(id, false);
            Refresh();
        }

        /// <summary>Scrolls the tree so the node's card shows: centred (<paramref name="centre"/>), or just inside the window's
        /// nearer edge when part of it lies outside (otherwise nothing moves).</summary>
        private void Focus(string id, bool centre)
        {
            CurriculumNode node = run.Curriculum.Tree.Find(id);
            if (node == null) return;
            float middle = -NodePosition(node).y, view = viewport.rect.height, offset = ScrollOffset;
            if (centre) offset = middle - view * .5f;
            else if (middle - NodeHeight * .5f - FocusMargin < offset) offset = middle - NodeHeight * .5f - FocusMargin;
            else if (middle + NodeHeight * .5f + FocusMargin > offset + view) offset = middle + NodeHeight * .5f + FocusMargin - view;
            ScrollTo(offset);
        }

        private void ScrollTo(float offset)
        {
            scroll.StopMovement();
            content.anchoredPosition = new Vector2(0f, Mathf.Clamp(offset, 0f, MaximumScroll));
        }

        /// <summary>Where a page opened afresh looks first: the first new node, else the first affordable one, else the
        /// first available one; null when there is none.</summary>
        private string FocusNodeId()
        {
            CurriculumProgress curriculum = run.Curriculum;
            foreach (CurriculumNode node in curriculum.Tree.Nodes) if (newNodes.Contains(node.Id)) return node.Id;
            foreach (CurriculumNode node in curriculum.Tree.Nodes) if (run.IsCurriculumNodeAffordable(node.Id)) return node.Id;
            return FirstAvailable();
        }

        public void Refresh()
        {
            if (disposed) return;
            CurriculumProgress curriculum = run.Curriculum;
            UpdateNewNodes(curriculum);
            if (curriculum.Tree.Find(State.SelectedNodeId) == null)
                State.SelectedNodeId = FocusNodeId() ?? curriculum.Tree.Nodes[0].Id;
            // The balance stands out, in brass while it buys something.
            int affordable = run.AffordableCurriculumNodeCount;
            points.text = PointsText(run.CurriculumPoints);
            points.color = affordable > 0 ? Accent : Foreground;
            status.text = StandingText(affordable, curriculum.IsFinished, run.CurriculumPointsToNextNode);
            status.color = affordable > 0 ? Accent : Muted;
            completedCount.text = $"완료 {curriculum.CompletedCount} / {curriculum.Tree.Nodes.Count}";
            bool canReset = Editing && curriculum.CompletedCount > 0;
            if (!canReset) resetArmed = false;
            resetButton.interactable = canReset;
            resetCaption.text = resetArmed ? ArmedResetText(run.CurriculumRefund) : "커리큘럼 초기화";
            resetCaption.color = canReset ? Foreground : Muted;
            // A moment whose node is gone (a reset) gives way to the resting banner.
            if (learnedNodeId != null && !curriculum.IsCompleted(learnedNodeId)) ShowRestingBanner();
            foreach (NodeCard card in cards) RefreshCard(card, curriculum);
            // A finished school's name takes its keyword's colour.
            foreach (KeyValuePair<CurriculumBranch, Text> heading in headings)
                heading.Value.color = IsSchoolFinished(heading.Key) ? SkillToneStyle.Paper(BranchTone(heading.Key)) : Muted;
            RefreshDetail(curriculum);
        }

        private bool Editing => run.Phase == CampaignPhase.Lobby || run.Phase == CampaignPhase.Maintenance;

        /// <summary>The top row's balance: <c>수련 점수 5</c>.</summary>
        public static string PointsText(int balance) => $"수련 점수 {balance}";

        /// <summary>What the balance buys, beside it: <c>익힐 수 있는 과정 2개</c>, or with nothing affordable
        /// <c>다음 과정까지 1점</c>, or once nothing is left to learn the reset's offer.</summary>
        public static string StandingText(int affordable, bool finished, int pointsToNext)
            => affordable > 0 ? $"익힐 수 있는 과정 {affordable}개"
                : finished ? "모든 과정을 익혔습니다 · 초기화하면 점수를 돌려받습니다"
                : $"다음 과정까지 {pointsToNext}점";

        /// <summary>The reset's caption after its first click: a second click resets, and the points it gives back.</summary>
        public static string ArmedResetText(int refund) => $"한 번 더 누르면 초기화\n수련 점수 {refund}점을 돌려받습니다";

        /// <summary>A technique as the learning moment names it, with its school: <c>사선베기(Q 정공)</c>.</summary>
        public static string SkillLabel(LegacySkill skill)
            => skill == null ? string.Empty
                : LegacySkillDefinitions.Find(skill)?.IsCounter == true ? $"{skill.Name}(반격)"
                : $"{skill.Name}({SkillLaneStyle.Key(skill.LaneIndex)} {SkillLaneStyle.Name(skill.LaneIndex)})";

        /// <summary>The learning moment's headline: <c>새 기술 · 사선베기(Q 정공)</c>; a node that grants no technique names
        /// its stats (<c>능력치 · 최대 체력 +5</c>).</summary>
        public static string LearnedHeadline(CurriculumNode node, LegacySkill skill)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            return skill != null ? "새 기술 · " + SkillLabel(skill) : "능력치 · " + StatRewardText(node.StatReward);
        }

        /// <summary>The learning moment's second line: the course it finished (<c>붕괴 과정 수료</c>, for the trunk's end and
        /// each capstone) with a technique node's stats, then what it opened (<c>새로 열린 과정: 쿠페 · 사선베기 · 유연함</c>).
        /// With none of these, where the technique goes.</summary>
        public static string LearnedDetail(CurriculumTree source, CurriculumNode node, LegacySkill skill,
            IReadOnlyList<CurriculumNode> opened, bool laneOpen)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            var parts = new List<string>(3);
            string stats = skill != null ? StatRewardText(node.StatReward) : string.Empty;
            if (source != null && source.EndsBranch(node.Id))
                parts.Add(BranchName(node.Branch) + " 과정 수료" + (stats.Length > 0 ? " · " + stats : string.Empty));
            else if (stats.Length > 0) parts.Add(stats);
            if (opened != null && opened.Count > 0)
            {
                var titles = new string[opened.Count];
                for (int index = 0; index < titles.Length; index++) titles[index] = opened[index].Title;
                parts.Add("새로 열린 과정: " + string.Join(" · ", titles));
            }
            if (parts.Count > 0) return string.Join("  ·  ", parts);
            return skill == null ? "능력치가 올랐습니다."
                : LegacySkillDefinitions.Find(skill)?.IsCounter == true ? "편성의 반격 슬롯에 장착할 수 있습니다."
                : laneOpen ? "편성에서 장착할 수 있습니다." : "이 기술을 쓰는 열을 해금하면 편성할 수 있습니다.";
        }

        /// <summary>The resting banner: how 수련 점수 are earned (<see cref="CurriculumPointRules"/>).</summary>
        public const string RestingBannerTitle = "수련 점수는 전투를 마칠 때마다 쌓입니다.";

        public static string RestingBannerDetail
            => $"전투 +{CurriculumPointRules.Battle} · 승리 +{CurriculumPointRules.Victory} · " +
               $"첫 클리어 +{CurriculumPointRules.FirstClear} · 임무 첫 완료 +{CurriculumPointRules.StoryMission}";

        private string FirstAvailable()
        {
            foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
                if (run.Curriculum.GetState(node.Id) == CurriculumNodeState.Available) return node.Id;
            return null;
        }

        // The nodes the last completion opened that can still be chosen, less those the player has looked at. Nothing is
        // saved: the last completed node says it all.
        private void UpdateNewNodes(CurriculumProgress curriculum)
        {
            newNodes.Clear();
            CurriculumNode last = curriculum.LastCompleted;
            if (State.NewTagsFrom != last?.Id)
            {
                State.NewTagsFrom = last?.Id;
                State.SeenNewTags.Clear();
            }
            if (last == null) return;
            foreach (CurriculumNode node in curriculum.OpenedBy(last.Id))
                if (curriculum.GetState(node.Id) == CurriculumNodeState.Available && !State.SeenNewTags.Contains(node.Id))
                    newNodes.Add(node.Id);
        }

        // A school is finished once its capstone (정수) is.
        private bool IsSchoolFinished(CurriculumBranch branch)
        {
            CurriculumTree source = run.Curriculum.Tree;
            foreach (CurriculumNode node in source.Nodes)
                if (node.Branch == branch && source.IsCapstone(node.Id) && run.Curriculum.IsCompleted(node.Id)) return true;
            return false;
        }

        /// <summary>A school's keyword colour (its band, and its name once finished): 순환's, 붕괴's (the 고화력 tone) and
        /// 판짜기's (the 후속 tone). The trunk and the bridges have none.</summary>
        public static LegacySkillTone BranchTone(CurriculumBranch branch)
        {
            switch (branch)
            {
                case CurriculumBranch.Circulation: return LegacySkillTone.Cycle;
                case CurriculumBranch.Collapse: return LegacySkillTone.HighPower;
                case CurriculumBranch.Retraction: return LegacySkillTone.Followup;
                case CurriculumBranch.Bleeding: return LegacySkillTone.Reduction;
                default: return LegacySkillTone.Neutral;
            }
        }

        private static bool IsSchool(CurriculumBranch branch)
            => branch != CurriculumBranch.Foundation && branch != CurriculumBranch.Bridge;

        /// <summary>Each school's band under its nodes, from the column its first node starts in to the one its last ends in
        /// (a column a later phase fills is drawn already), from just above its first row to just under its last, in its
        /// keyword's colour; and its name at the band's upper left, in the fork's extra room, clear of the links coming in.
        /// The trunk and the bridges get neither: a bridge's card carries its tag and its plate instead.</summary>
        private void BuildBands()
        {
            var spans = new Dictionary<CurriculumBranch, (float left, float right, int top, int bottom)>();
            var order = new List<CurriculumBranch>();
            foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
            {
                if (!IsSchool(node.Branch)) continue;
                if (!spans.TryGetValue(node.Branch, out var span))
                {
                    order.Add(node.Branch);
                    span = (node.Column, node.Column, node.Row, node.Row);
                }
                spans[node.Branch] = (Mathf.Min(span.left, node.Column), Mathf.Max(span.right, node.Column),
                    Math.Min(span.top, node.Row), Math.Max(span.bottom, node.Row));
            }
            foreach (CurriculumBranch branch in order)
            {
                var span = spans[branch];
                BandBounds(span.left, span.right, span.top, span.bottom, out float left, out float right, out float top, out float bottom);
                Color tint = SkillToneStyle.Paper(BranchTone(branch));
                tint.a = BandAlpha;
                Pin(Panel("Band " + branch, content, new Vector2((left + right) * .5f, (top + bottom) * .5f),
                    new Vector2(right - left, top - bottom), tint).rectTransform);
            }
            foreach (CurriculumBranch branch in order)
            {
                var span = spans[branch];
                BandBounds(span.left, span.right, span.top, span.bottom, out float left, out _, out float top, out _);
                Text heading = Label("Branch " + branch, content, new Vector2(left + 8f + 50f, top - 4f - 15f),
                    new Vector2(100f, 30f), 18, Muted);
                Pin(heading.rectTransform);
                heading.text = BranchName(branch);
                headings[branch] = heading;
            }
        }

        private static void BandBounds(float firstColumn, float lastColumn, int firstRow, int lastRow,
            out float left, out float right, out float top, out float bottom)
        {
            left = BandLeft + ColumnWidth * Mathf.Floor(firstColumn);
            right = BandLeft + ColumnWidth * (Mathf.Ceil(lastColumn) + 1f);
            top = -RowCentre(firstRow) + NodeHeight * .5f + BandHeadRoom;
            bottom = -RowCentre(lastRow) - NodeHeight * .5f - BandFoot;
        }

        /// <summary>The two schools a bridge joins, the left one first: its prerequisites' schools by column. False for a
        /// node that is not a bridge (or names no school).</summary>
        public static bool TryGetBridgedBranches(CurriculumTree source, CurriculumNode node, out CurriculumBranch left,
            out CurriculumBranch right)
        {
            left = right = CurriculumBranch.Bridge;
            if (source == null || node == null || node.Branch != CurriculumBranch.Bridge) return false;
            float leftColumn = float.MaxValue, rightColumn = float.MinValue;
            foreach (string id in Concat(node.RequiresAll, node.RequiresAny))
            {
                CurriculumNode parent = source.Find(id);
                if (parent == null || !IsSchool(parent.Branch)) continue;
                if (parent.Column < leftColumn) { leftColumn = parent.Column; left = parent.Branch; }
                if (parent.Column > rightColumn) { rightColumn = parent.Column; right = parent.Branch; }
            }
            return left != CurriculumBranch.Bridge;
        }

        // Under each bridge card, a plate a little larger than the card in its two schools' colours, half and half.
        private void BuildBridgePlates()
        {
            CurriculumTree source = run.Curriculum.Tree;
            float width = NodeWidth + 2f * BridgePad, height = NodeHeight + 2f * BridgePad;
            foreach (CurriculumNode node in source.Nodes)
            {
                if (!TryGetBridgedBranches(source, node, out CurriculumBranch left, out CurriculumBranch right)) continue;
                var plate = Rect("Bridge Plate " + node.Id, content, NodePosition(node), new Vector2(width, height));
                Pin(plate);
                Color leftColor = SkillToneStyle.Paper(BranchTone(left)), rightColor = SkillToneStyle.Paper(BranchTone(right));
                leftColor.a = rightColor.a = BridgeAlpha;
                Panel("Bridge Plate Left", plate, new Vector2(-width * .25f, 0f), new Vector2(width * .5f, height), leftColor);
                Panel("Bridge Plate Right", plate, new Vector2(width * .25f, 0f), new Vector2(width * .5f, height), rightColor);
            }
        }

        // A thin bar on the frame's right edge, inside its rim (the combat log's, narrower).
        private Scrollbar BuildScrollbar()
        {
            var track = Panel("Curriculum Scrollbar", tree, Vector2.zero, Vector2.zero, DuelVisualTheme.RaisedSurface);
            track.raycastTarget = true;
            RectTransform rect = track.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, .5f);
            rect.sizeDelta = new Vector2(6f, -2f * (ViewportInset + 8f));
            rect.anchoredPosition = new Vector2(-7f, 0f);
            var handle = Panel("Handle", rect, Vector2.zero, Vector2.zero, Border);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = Vector2.one;
            handle.rectTransform.sizeDelta = Vector2.zero;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            return bar;
        }

        // Content children are placed from the content's upper left.
        private static void Pin(RectTransform rect) => rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);

        private static IEnumerable<string> Concat(IEnumerable<string> a, IEnumerable<string> b)
        {
            foreach (string value in a) yield return value;
            foreach (string value in b) yield return value;
        }

        /// <summary>Right-angled connectors from each prerequisite's bottom to the node's top, like a focus tree.
        /// "Any of" prerequisites use the muted colour, turn above the midpoint (so their horizontal run stays clear
        /// of the "all" links, which turn at the midpoint and descend into their nodes below it) and are drawn
        /// first, so a stem shared with an "all" link shows the solid colour.</summary>
        private void BuildConnectors()
        {
            CurriculumTree source = run.Curriculum.Tree;
            foreach (CurriculumNode node in source.Nodes)
                foreach (string id in node.RequiresAny) Connect(source.Find(id), node, Muted, AnyBusOffset);
            foreach (CurriculumNode node in source.Nodes)
                foreach (string id in node.RequiresAll) Connect(source.Find(id), node, Border, 0f);
        }

        /// <summary>How far above the midpoint between two rows the "any" links run.</summary>
        public const float AnyBusOffset = 17f;

        private void Connect(CurriculumNode parent, CurriculumNode child, Color color, float busOffset)
        {
            Vector2 from = NodePosition(parent) - new Vector2(0f, NodeHeight * .5f);
            Vector2 to = NodePosition(child) + new Vector2(0f, NodeHeight * .5f);
            float middle = (from.y + to.y) * .5f + busOffset;
            string name = $"Connector {parent.Id} {child.Id}";
            Line(name + " Down", new Vector2(from.x, (from.y + middle) * .5f), new Vector2(3f, from.y - middle), color);
            if (!Mathf.Approximately(from.x, to.x))
                Line(name + " Across", new Vector2((from.x + to.x) * .5f, middle), new Vector2(Mathf.Abs(to.x - from.x) + 3f, 3f), color);
            Line(name + " Into", new Vector2(to.x, (middle + to.y) * .5f), new Vector2(3f, middle - to.y), color);
        }

        private void Line(string name, Vector2 position, Vector2 size, Color color)
            => Pin(Panel(name, content, position, size, color).rectTransform);

        private void AddCard(CurriculumNode node)
        {
            CurriculumTree source = run.Curriculum.Tree;
            var card = new NodeCard
            {
                Node = node,
                Capstone = source.IsCapstone(node.Id),
                Bridge = TryGetBridgedBranches(source, node, out _, out _),
            };
            card.Button = ActionButton("Curriculum Node " + node.Id, content, null, NodePosition(node),
                new Vector2(NodeWidth, NodeHeight), () => SelectNode(node.Id));
            Pin((RectTransform)card.Button.transform);
            card.Background = card.Button.GetComponent<Image>();
            // A capstone wears the ornate rim; completed, a gold border over it too.
            if (card.Capstone)
            {
                DuelVisualTheme.DressPanel(card.Background);
                card.Seal = Rect("Capstone Seal", card.Button.transform, Vector2.zero, new Vector2(NodeWidth, NodeHeight)).gameObject;
                Panel("Seal Top", card.Seal.transform, new Vector2(0f, (NodeHeight - SealWidth) * .5f),
                    new Vector2(NodeWidth, SealWidth), Accent);
                Panel("Seal Bottom", card.Seal.transform, new Vector2(0f, -(NodeHeight - SealWidth) * .5f),
                    new Vector2(NodeWidth, SealWidth), Accent);
                Panel("Seal Left", card.Seal.transform, new Vector2(-(NodeWidth - SealWidth) * .5f, 0f),
                    new Vector2(SealWidth, NodeHeight), Accent);
                Panel("Seal Right", card.Seal.transform, new Vector2((NodeWidth - SealWidth) * .5f, 0f),
                    new Vector2(SealWidth, NodeHeight), Accent);
                card.Seal.SetActive(false);
            }
            card.Marker = Panel("Selected Marker", card.Button.transform, new Vector2(0f, NodeHeight * .5f - 3f),
                new Vector2(NodeWidth, 6f), Accent);
            card.Icon = Panel("Node Icon", card.Button.transform, new Vector2(-30f, 22f), Vector2.one * 40f, Color.white);
            card.Icon.preserveAspect = true;
            LegacySkill skill = GrantedSkill(node);
            card.Icon.sprite = skill != null ? art.GetSkillIcon(skill.IconId) : null;
            card.Icon.enabled = card.Icon.sprite != null;
            if (skill != null)
            {
                // The granted skill's school in its dye, and its core mechanic at the icon's upper right.
                Label("Node Lane", card.Button.transform, new Vector2(22f, 30f), new Vector2(40f, 22f), 15,
                    SkillLaneStyle.Edge(skill.LaneIndex), TextAnchor.MiddleCenter).text =
                        LegacySkillDefinitions.Find(skill)?.IsCounter == true ? "반격" : SkillLaneStyle.Key(skill.LaneIndex);
                card.Role = new SkillRolePip(card.Button.transform, new Vector2(-13f, 36f), 14f);
                card.Role.SetSkill(skill);
            }
            else if (!node.StatReward.IsEmpty)
            {
                Text rewardLabel = Label("Node Lane", card.Button.transform, new Vector2(22f, 30f),
                    new Vector2(52f, 22f), 15, Muted, TextAnchor.MiddleCenter);
                Fit(rewardLabel, 11, 15);
                rewardLabel.text = "능력치";
            }
            bool paired = node.ExclusiveWith.Count > 0 || HasExclusivePartner(node);
            if (paired)
                Label("Node Exclusive", card.Button.transform, new Vector2(38f, 8f), new Vector2(40f, 20f), 13,
                    DuelVisualTheme.Danger, TextAnchor.MiddleCenter).text = "택1";
            // 새, 다리 or 정수, in the 택1 tag's place (beside it on a card that has both).
            card.Tag = Label("Node Tag", card.Button.transform, new Vector2(paired ? -4f : 38f, 8f), new Vector2(40f, 20f), 13,
                Accent, TextAnchor.MiddleCenter);
            card.Title = Label("Node Title", card.Button.transform, new Vector2(0f, -14f), new Vector2(NodeWidth - 10f, 26f), 17,
                Foreground, TextAnchor.MiddleCenter);
            Fit(card.Title, 12, 17);
            card.Title.text = node.Title;
            card.Status = Label("Node Status", card.Button.transform, new Vector2(0f, -37f), new Vector2(NodeWidth - 8f, 20f), 13,
                Muted, TextAnchor.MiddleCenter);
            cards.Add(card);
        }

        private bool HasExclusivePartner(CurriculumNode node)
        {
            foreach (CurriculumNode other in run.Curriculum.Tree.Nodes)
                if (run.Curriculum.Tree.AreExclusive(node.Id, other.Id)) return true;
            return false;
        }

        private void RefreshCard(NodeCard card, CurriculumProgress curriculum)
        {
            CurriculumNodeState state = curriculum.GetState(card.Node.Id);
            card.Title.text = DisplayTitle(card.Node, state);
            bool selected = State.SelectedNodeId == card.Node.Id;
            bool affordable = run.IsCurriculumNodeAffordable(card.Node.Id);
            card.Background.color = state == CurriculumNodeState.Completed ? DuelVisualTheme.RaisedSurface
                : state == CurriculumNodeState.Available ? DuelVisualTheme.Card
                : DuelVisualTheme.Track;
            card.Marker.gameObject.SetActive(selected);
            bool dim = state == CurriculumNodeState.Locked || state == CurriculumNodeState.Excluded;
            card.Title.color = dim ? Muted : Foreground;
            card.Icon.color = dim ? new Color(1f, 1f, 1f, .4f) : Color.white;
            card.Role?.SetAlpha(dim ? .4f : 1f);
            // An available node's cost: brass and bold while the balance covers it, muted until then.
            card.Status.text = StateText(state, card.Node);
            card.Status.color = state == CurriculumNodeState.Completed || affordable ? Accent
                : state == CurriculumNodeState.Excluded ? DuelVisualTheme.Danger : Muted;
            card.Status.fontStyle = affordable ? FontStyle.Bold : FontStyle.Normal;
            bool isNew = newNodes.Contains(card.Node.Id);
            card.Tag.text = isNew ? NewTag : card.Bridge ? BridgeTag : card.Capstone ? CapstoneTag : string.Empty;
            card.Tag.color = isNew ? Accent : card.Bridge ? Foreground : DuelVisualTheme.Paper;
            card.Tag.fontStyle = isNew ? FontStyle.Bold : FontStyle.Normal;
            card.Seal?.SetActive(state == CurriculumNodeState.Completed);
        }

        private static string StateText(CurriculumNodeState state, CurriculumNode node)
        {
            switch (state)
            {
                case CurriculumNodeState.Completed: return "완료";
                case CurriculumNodeState.Available: return $"{node.Cost}점";
                case CurriculumNodeState.Excluded: return "닫힘";
                default: return "잠김";
            }
        }

        private void RefreshDetail(CurriculumProgress curriculum)
        {
            CurriculumNode node = curriculum.Tree.Find(State.SelectedNodeId);
            CurriculumNodeState state = curriculum.GetState(node.Id);
            LegacySkill skill = GrantedSkill(node);
            CampaignOwnedSkill owned = state == CurriculumNodeState.Completed && skill != null
                ? run.GetOwnedSkill(skill.Id) : null;
            LegacySkill shownSkill = owned?.Skill ?? skill;
            string rewardText = StatRewardText(node.StatReward);
            if (skill != null)
            {
                detailStyle.SetLane(shownSkill.LaneIndex);
                detailIcon.enabled = true;
                detailIcon.sprite = art.GetSkillIcon(shownSkill.IconId);
                detailInfo.SetSkill(shownSkill, false, rewardText.Length > 0 ? "능력치 보상: " + rewardText : null,
                    owned);
            }
            else
            {
                detailStyle.Clear("능력치 보상");
                detailIcon.enabled = false;
                detailIcon.sprite = null;
                detailInfo.SetEmptyMessage(rewardText);
            }
            detailName.text = DisplayTitle(node, state);
            purpose.text = PurposeText(curriculum.Tree, node);
            requirement.text = RequirementText(node, curriculum.Tree);
            // Lanes open through story missions; a skill of a closed lane is owned but waits to fight.
            if (skill != null && LegacySkillDefinitions.Find(skill)?.IsCounter != true && !run.IsLaneOpen(skill.LaneIndex))
                requirement.text += $"  ·  {SkillLaneStyle.Key(skill.LaneIndex)}열은 임무로 열림";
            exclusive.text = ExclusiveText(node, curriculum.Tree);
            bool affordable = curriculum.IsAffordable(node.Id, run.CurriculumPoints);
            availability.text = !Editing ? "로비에서 이용할 수 있습니다."
                : state == CurriculumNodeState.Completed ? skill == null ? "능력치가 올랐습니다."
                    : rewardText.Length > 0 ? "능력치가 올랐습니다. 익힌 기술은 편성에서 장착하세요."
                    : "익힌 기술은 편성에서 장착하세요."
                : state == CurriculumNodeState.Excluded ? "다른 택1 과정을 익혔습니다. 초기화하면 다시 고를 수 있습니다."
                : state == CurriculumNodeState.Locked ? "선행 과정을 먼저 익히세요."
                : affordable ? $"수련 점수 {node.Cost}점으로 바로 익힙니다."
                : "전투를 마칠 때마다 수련 점수가 쌓입니다.";
            bool canLearn = Editing && run.CanLearnCurriculumNode(node.Id);
            primaryAction.interactable = canLearn;
            actionCaption.text = state == CurriculumNodeState.Completed ? "완료한 과정"
                : state == CurriculumNodeState.Excluded ? "닫힌 과정"
                : state == CurriculumNodeState.Locked ? "잠긴 과정"
                : affordable ? $"익히기 · {node.Cost}점" : $"점수 부족 · {run.CurriculumPoints}/{node.Cost}";
            actionCaption.color = canLearn ? DuelVisualTheme.Ink : Foreground;
            LayoutDetail();
        }

        private void LayoutDetail()
        {
            bool hasExclusive = !string.IsNullOrEmpty(exclusive.text);
            exclusive.gameObject.SetActive(hasExclusive);
            // The optional exclusivity line keeps its space so selecting nodes cannot resize the panel.
            float height = 104f + detailInfo.Height + 20f + 30f + 30f + 52f + 50f + 20f;
            detailFrame.sizeDelta = new Vector2(384f, height + 4f);
            detailFrame.anchoredPosition = new Vector2(423f, 272f - (height + 4f) / 2f);
            detailSurface.sizeDelta = new Vector2(380f, height);
            float top = height / 2f;
            detailStyle.Root.anchoredPosition = new Vector2(-18f, top - 18f);
            detailIcon.rectTransform.anchoredPosition = new Vector2(-105f, top - 62f);
            detailName.rectTransform.anchoredPosition = new Vector2(20f, top - 49f);
            purpose.rectTransform.anchoredPosition = new Vector2(59f, top - 78f);
            detailInfo.PlaceTop(top - 104f);
            float cursor = top - 104f - detailInfo.Height - 8f;
            detailRule.anchoredPosition = new Vector2(0f, cursor - 1f);
            cursor -= 12f;
            requirement.rectTransform.anchoredPosition = new Vector2(0f, cursor - 13f);
            cursor -= 30f;
            exclusive.rectTransform.anchoredPosition = new Vector2(0f, cursor - 13f);
            cursor -= 30f;
            availability.rectTransform.anchoredPosition = new Vector2(0f, cursor - 22f);
            cursor -= 52f;
            primaryAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, cursor - 25f);
        }

        /// <summary>The detail's purpose line: <c>순환 과정  ·  1점</c>, a capstone's <c>순환 과정 정수  ·  3점</c>, a
        /// bridge's <c>다리 과정  ·  붕괴 × 순환  ·  3점</c>.</summary>
        public static string PurposeText(CurriculumTree source, CurriculumNode node)
        {
            string cost = $"{node.Cost}점";
            if (TryGetBridgedBranches(source, node, out CurriculumBranch left, out CurriculumBranch right))
                return $"{BranchName(node.Branch)} 과정  ·  {BranchName(left)} × {BranchName(right)}  ·  {cost}";
            return source != null && source.IsCapstone(node.Id)
                ? $"{BranchName(node.Branch)} 과정 {CapstoneTag}  ·  {cost}"
                : $"{BranchName(node.Branch)} 과정  ·  {cost}";
        }

        private static string RequirementText(CurriculumNode node, CurriculumTree tree)
        {
            var parts = new List<string>();
            foreach (string id in node.RequiresAll) parts.Add(tree.Find(id).Title);
            string all = string.Join(", ", parts);
            parts.Clear();
            foreach (string id in node.RequiresAny) parts.Add(tree.Find(id).Title);
            string any = string.Join(" 또는 ", parts);
            if (all.Length == 0 && any.Length == 0) return "선행 과정 없음";
            return "선행  " + (all.Length > 0 && any.Length > 0 ? all + " · " + any + " 중 하나" : all + any);
        }

        private static string ExclusiveText(CurriculumNode node, CurriculumTree tree)
        {
            var names = new List<string>();
            foreach (CurriculumNode other in tree.Nodes)
                if (tree.AreExclusive(node.Id, other.Id)) names.Add(other.Title);
            if (names.Count == 0) return string.Empty;
            // The last name picks 과 or 와.
            return "택1  " + KoreanParticle.Attach(string.Join(", ", names), "과") + " 함께 고를 수 없음";
        }

        private static string StatRewardText(CurriculumStatReward reward)
        {
            var parts = new List<string>(5);
            if (reward.Health > 0) parts.Add("최대 체력 +" + reward.Health);
            if (reward.Resistance > 0) parts.Add("최대 저항 +" + reward.Resistance);
            if (reward.ActGain > 0) parts.Add("매 턴 ACT 회복 +" + reward.ActGain);
            if (reward.ActCapacity > 0) parts.Add("ACT 상한 +" + reward.ActCapacity);
            if (reward.PlanningSeconds > 0) parts.Add("일반 스테이지 제한시간 +" + reward.PlanningSeconds + "초");
            return string.Join(" · ", parts);
        }

        public static string BranchName(CurriculumBranch branch)
        {
            switch (branch)
            {
                case CurriculumBranch.Foundation: return "기초";
                case CurriculumBranch.Circulation: return "순환";
                case CurriculumBranch.Collapse: return "붕괴";
                case CurriculumBranch.Retraction: return "판짜기";
                case CurriculumBranch.Bleeding: return "출혈";
                default: return "다리";
            }
        }

        // Any sheet row, as CampaignRun grants it, so a node whose technique became a starting one still shows it.
        private static LegacySkill GrantedSkill(CurriculumNode node)
            => node.SkillIds.Count == 0 ? null : LegacySkillDefinitions.Find(node.SkillIds[0])?.Skill;

        private string DisplayTitle(CurriculumNode node, CurriculumNodeState state)
        {
            LegacySkill skill = GrantedSkill(node);
            if (state != CurriculumNodeState.Completed || skill == null || node.Title != skill.Name)
                return node.Title;
            return run.GetOwnedSkill(skill.Id)?.Skill.Name ?? node.Title;
        }

        private void PerformLearn()
        {
            if (disposed || !Editing) return;
            string id = State.SelectedNodeId;
            if (!run.CanLearnCurriculumNode(id)) return;
            // The page the learn leaves behind plays the moment: a new one when the callback rebuilt the lobby, else this one.
            State.PendingLearn = id;
            if (learn != null) learn.Invoke(id);
            else run.TryLearnCurriculumNode(id);
            if (disposed) return;
            Refresh();
            TakePendingLearn();
        }

        // Plays the moment of a learn the view state holds, if that node was indeed the last one learned; clears it either
        // way, so it plays once.
        private void TakePendingLearn()
        {
            string id = State.PendingLearn;
            State.PendingLearn = null;
            CurriculumNode node = run.Curriculum.LastCompleted;
            if (id == null || node == null || node.Id != id) return;
            PlayLearnMoment(node);
        }

        /// <summary>The learning moment: the card pulses under a brass flash, the banner names the technique, the course it
        /// finished and the nodes it opened, and the page turns to those (the first it can learn now, else the first; looked
        /// at by focus only, so they keep their 새) and scrolls them into view under the learned card.</summary>
        private void PlayLearnMoment(CurriculumNode node)
        {
            CurriculumProgress curriculum = run.Curriculum;
            IReadOnlyList<CurriculumNode> opened = curriculum.OpenedBy(node.Id);
            string next = null;
            foreach (CurriculumNode candidate in opened)
                if (run.IsCurriculumNodeAffordable(candidate.Id)) { next = candidate.Id; break; }
            if (next == null && opened.Count > 0) next = opened[0].Id;
            if (next != null) State.SelectedNodeId = next;
            Refresh();
            Focus(node.Id, false);
            if (next != null) Focus(next, false);
            ShowLearnedBanner(node, opened);
            NodeCard card = cards.Find(candidate => candidate.Node.Id == node.Id);
            if (card == null) return;
            if (card.Flash == null)
            {
                card.Flash = Panel("Learn Flash", card.Button.transform, Vector2.zero, new Vector2(NodeWidth, NodeHeight), Accent);
                card.Flash.gameObject.SetActive(false);
            }
            card.Flash.transform.SetAsLastSibling();
            CurriculumLearnPulse pulse = card.Button.GetComponent<CurriculumLearnPulse>();
            if (pulse == null) pulse = card.Button.gameObject.AddComponent<CurriculumLearnPulse>();
            pulse.Play(card.Flash);
        }

        private void ShowLearnedBanner(CurriculumNode node, IReadOnlyList<CurriculumNode> opened)
        {
            learnedNodeId = node.Id;
            LegacySkill granted = GrantedSkill(node);
            LegacySkill skill = granted == null ? null : run.GetOwnedSkill(granted.Id)?.Skill ?? granted;
            CurriculumTree source = run.Curriculum.Tree;
            bool course = source.EndsBranch(node.Id);
            // A finished course wears the ornate rim, as a capstone card does.
            DuelVisualTheme.Frame(banner, course);
            bannerTitle.text = LearnedHeadline(node, skill);
            bannerTitle.color = Accent;
            bannerDetail.text = LearnedDetail(source, node, skill, opened, skill != null && run.IsLaneOpen(skill.LaneIndex));
            bannerDetail.color = Foreground;
            Color dye = skill != null ? SkillLaneStyle.Edge(skill.LaneIndex) : Accent;
            bannerAccent.color = dye;
            bannerIconFrame.color = dye;
            bannerIcon.sprite = skill != null ? art.GetSkillIcon(skill.IconId) : null;
            bannerIcon.enabled = bannerIcon.sprite != null;
            bannerIconFrame.gameObject.SetActive(skill != null);
            LayoutBanner(skill != null);
        }

        private void ShowRestingBanner()
        {
            learnedNodeId = null;
            DuelVisualTheme.Frame(banner);
            bannerTitle.text = RestingBannerTitle;
            bannerTitle.color = Muted;
            bannerDetail.text = RestingBannerDetail;
            bannerDetail.color = Muted;
            bannerAccent.color = Border;
            bannerIcon.sprite = null;
            bannerIconFrame.gameObject.SetActive(false);
            LayoutBanner(false);
        }

        // The two lines start beside the icon, or at the banner's left without one.
        private void LayoutBanner(bool icon)
        {
            float left = -TreeWidth * .5f + (icon ? 72f : 20f), width = TreeWidth * .5f - 16f - left;
            bannerTitle.rectTransform.sizeDelta = new Vector2(width, 26f);
            bannerTitle.rectTransform.anchoredPosition = new Vector2(left + width * .5f, 13f);
            bannerDetail.rectTransform.sizeDelta = new Vector2(width, 24f);
            bannerDetail.rectTransform.anchoredPosition = new Vector2(left + width * .5f, -14f);
        }

        private void ResetClicked()
        {
            if (disposed || !Editing) return;
            if (!resetArmed)
            {
                resetArmed = true;
                Refresh();
                return;
            }
            resetArmed = false;
            if (reset != null) reset.Invoke();
            else run.TryResetCurriculum();
            if (!disposed) Refresh();
        }

        public void Dispose()
        {
            if (disposed) return;
            // Unless the page was opened afresh since this one was built: the next one focuses again.
            if (State.Openings == opening) State.TreeScroll = content.anchoredPosition;
            disposed = true;
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }

        private Button ActionButton(string name, Transform parent, string caption, Vector2 position, Vector2 size, Action action, bool primary = false)
        {
            var image = Panel(name, parent, position, size, primary ? Accent : DuelVisualTheme.Card);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            DuelVisualTheme.StyleButton(button, primary);
            if (caption != null)
                Label("Button Label", image.transform, Vector2.zero, size - new Vector2(12f, 4f), 20,
                    primary ? DuelVisualTheme.Ink : Foreground, TextAnchor.MiddleCenter).text = caption;
            button.onClick.AddListener(() =>
            {
                if (disposed) return;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                action?.Invoke();
            });
            return button;
        }

        private Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize,
            Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.font = art.UIFont;
            label.fontSize = fontSize;
            label.color = color ?? Foreground;
            label.alignment = alignment;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void Fit(Text label, int min, int max)
        { label.resizeTextForBestFit = true; label.resizeTextMinSize = min; label.resizeTextMaxSize = max; }

        private static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.anchoredPosition = rect.sizeDelta = Vector2.zero; }

        private static void Destroy(GameObject target)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target); }
    }

    /// <summary>The learning moment on a curriculum card (<see cref="CampaignCurriculumHud"/>): the card swells and settles
    /// while a brass flash over it fades, in unscaled time. Stopping it (or the card going) puts the card back as it was.</summary>
    [DisallowMultipleComponent]
    public sealed class CurriculumLearnPulse : MonoBehaviour
    {
        public const float Duration = .6f, Swell = .12f, FlashAlpha = .55f;
        private Image flash;
        private float elapsed = Duration;

        public bool IsPlaying => elapsed < Duration;

        internal void Play(Image overlay)
        {
            flash = overlay;
            elapsed = 0f;
            Apply(0f);
        }

        public void Advance(float unscaledDelta)
        {
            if (!IsPlaying || float.IsNaN(unscaledDelta) || unscaledDelta <= 0f) return;
            elapsed = Mathf.Min(Duration, elapsed + unscaledDelta);
            Apply(elapsed / Duration);
        }

        private void Apply(float progress)
        {
            // Up and back down once, easing at both ends; the flash only fades.
            float scale = 1f + Swell * Mathf.Sin(Mathf.PI * progress);
            transform.localScale = progress >= 1f ? Vector3.one : new Vector3(scale, scale, 1f);
            if (flash == null) return;
            Color color = flash.color;
            color.a = FlashAlpha * (1f - progress);
            flash.color = color;
            flash.gameObject.SetActive(progress < 1f);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void OnDisable()
        {
            if (!IsPlaying) return;
            elapsed = Duration;
            Apply(1f);
        }
    }
}
