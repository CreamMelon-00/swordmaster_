using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TurnLimbo.Presentation
{
    /// <summary>The curriculum page: a HOI4 focus-style tree on the left, the selected node on the right.
    /// Clicking a node only selects it; one explicit button makes it the node in progress. CampaignRun owns the rules.
    /// The tree scrolls vertically inside its frame (<c>Curriculum Viewport</c>, a <see cref="ScrollRect"/> taking the wheel,
    /// a drag anywhere on it, cards included, and a thin bar): the trunk on top, then the three schools, each on a band of its
    /// keyword's colour with its name at the band's upper left (the trunk and the bridges have neither). A bridge card sits
    /// on the line between two bands over a plate of both colours and carries a <c>다리</c> tag; a school's capstone carries
    /// <c>정수</c> and an ornate rim, and a gold border once completed, when its school's name turns its keyword's colour. The
    /// nodes the last completion opened carry <c>새</c> until they are looked at. A page opened with an empty view state (the
    /// first time, or after the lobby's view was reset) scrolls to the node in progress, else a new node, else the first one
    /// available, and shows it; a page rebuilt with the state of the last one keeps its selection and where it was
    /// (<see cref="ViewState.TreeScroll"/>, across tabs and battles too). Selecting a card scrolls it into view.</summary>
    public sealed class CampaignCurriculumHud : IDisposable
    {
        public sealed class ViewState
        {
            public string SelectedNodeId { get; internal set; }
            /// <summary>The tree's scroll position (its content's anchored position) as the page last left it, so a rebuild
            /// (진행, 초기화) stays put. Null until a page has been built, and after <see cref="Reset"/> or
            /// <see cref="Reopen"/>: the next page then scrolls to its focus.</summary>
            public Vector2? TreeScroll { get; internal set; }
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
                Openings++;
            }
        }

        private sealed class NodeCard
        {
            public CurriculumNode Node;
            public Button Button;
            public Image Background, Icon, Marker;
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
        private const float TreeWidth = 760f, TreeHeight = 544f, ColumnWidth = 123f, RowHeight = 168f;
        private const float FirstRowCentre = 124f, ContentFoot = 34f, BandLeft = 8f, BandHeadRoom = 40f, BandFoot = 12f;
        private const float BandAlpha = .13f, BridgeAlpha = .85f, BridgePad = 4f, SealWidth = 3f;
        private static readonly Vector2 TreeCenter = new Vector2(-216f, -53f);
        private readonly RectTransform root, tree, viewport, content, detailFrame, detailSurface, detailRule;
        private readonly ScrollRect scroll;
        private readonly Dictionary<CurriculumBranch, Text> headings = new Dictionary<CurriculumBranch, Text>();
        private readonly HashSet<string> newNodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly LegacyDuelArt art;
        private readonly CampaignRun run;
        private readonly Action<string> select;
        private readonly Action reset;
        private readonly Text status, completedCount, detailName, purpose, requirement, exclusive, availability, actionCaption, resetCaption;
        private readonly SkillLaneBadge detailStyle;
        private readonly SkillInfoView detailInfo;
        private readonly Image detailIcon;
        private readonly Button primaryAction, resetButton;
        private readonly List<NodeCard> cards = new List<NodeCard>();
        private readonly int opening;
        private bool disposed, resetArmed;
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

        /// <summary>Whether the node's whole card shows in the tree's window now.</summary>
        public bool IsNodeInView(string id)
        {
            CurriculumNode node = run.Curriculum.Tree.Find(id);
            if (disposed || node == null) return false;
            float centre = -NodePosition(node).y - ScrollOffset;
            return centre - NodeHeight * .5f >= -.5f && centre + NodeHeight * .5f <= viewport.rect.height + .5f;
        }

        public CampaignCurriculumHud(RectTransform parent, LegacyDuelArt art, CampaignRun run,
            Action<string> select, Action reset, ViewState state = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.select = select;
            this.reset = reset;
            State = state ?? new ViewState();
            opening = State.Openings;
            root = Rect("Curriculum Content", parent, Vector2.zero, Vector2.zero);
            Stretch(root);

            resetButton = ActionButton("Curriculum Reset", root, "커리큘럼 초기화", new Vector2(-487f, 246f),
                new Vector2(220f, 42f), ResetClicked);
            resetCaption = resetButton.GetComponentInChildren<Text>();
            status = Label("Curriculum Status", root, new Vector2(-180f, 246f), new Vector2(360f, 30f), 18, Accent);
            completedCount = Label("Curriculum Completed Count", root, new Vector2(56f, 246f), new Vector2(216f, 30f),
                17, Muted, TextAnchor.MiddleRight);

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
            primaryAction = ActionButton("Curriculum Primary Action", detail.transform, "이 과정 진행", Vector2.zero,
                new Vector2(334f, 50f), PerformSelect, true);
            actionCaption = primaryAction.GetComponentInChildren<Text>();
            Refresh();
            // A rebuild stays where the page was; a page opened afresh scrolls to what matters now.
            if (State.TreeScroll.HasValue) ScrollTo(State.TreeScroll.Value.y);
            else Focus(FocusNodeId() ?? State.SelectedNodeId, true);
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

        /// <summary>Where a page opened afresh looks first: the node in progress, else the first new one, else the first
        /// available one; null when there is none.</summary>
        private string FocusNodeId()
        {
            CurriculumProgress curriculum = run.Curriculum;
            if (curriculum.Active != null) return curriculum.Active.Id;
            foreach (CurriculumNode node in curriculum.Tree.Nodes) if (newNodes.Contains(node.Id)) return node.Id;
            return FirstAvailable();
        }

        public void Refresh()
        {
            if (disposed) return;
            CurriculumProgress curriculum = run.Curriculum;
            UpdateNewNodes(curriculum);
            if (curriculum.Tree.Find(State.SelectedNodeId) == null)
                State.SelectedNodeId = FocusNodeId() ?? curriculum.Tree.Nodes[0].Id;
            status.text = curriculum.Active != null
                ? $"진행 중  {curriculum.Active.Title}  {curriculum.ActiveBattles}/{curriculum.Active.Battles}"
                : curriculum.IsFinished ? "모든 과정을 마쳤습니다 · 초기화하면 다른 길을 고를 수 있습니다"
                : "진행 중인 과정 없음 · 과정을 고르세요";
            status.color = curriculum.Active != null ? Accent : curriculum.IsFinished ? Muted : DuelVisualTheme.Danger;
            completedCount.text = $"완료 {curriculum.CompletedCount} / {curriculum.Tree.Nodes.Count}";
            bool canReset = Editing && (curriculum.CompletedCount > 0 || curriculum.Active != null);
            if (!canReset) resetArmed = false;
            resetButton.interactable = canReset;
            resetCaption.text = resetArmed ? "한 번 더 누르면 초기화" : "커리큘럼 초기화";
            resetCaption.color = canReset ? Foreground : Muted;
            foreach (NodeCard card in cards) RefreshCard(card, curriculum);
            // A finished school's name takes its keyword's colour.
            foreach (KeyValuePair<CurriculumBranch, Text> heading in headings)
                heading.Value.color = IsSchoolFinished(heading.Key) ? SkillToneStyle.Paper(BranchTone(heading.Key)) : Muted;
            RefreshDetail(curriculum);
        }

        private bool Editing => run.Phase == CampaignPhase.Lobby || run.Phase == CampaignPhase.Maintenance;

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
                    SkillLaneStyle.Edge(skill.LaneIndex), TextAnchor.MiddleCenter).text = SkillLaneStyle.Key(skill.LaneIndex);
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
            card.Background.color = state == CurriculumNodeState.Active ? DuelVisualTheme.Selected
                : state == CurriculumNodeState.Completed ? DuelVisualTheme.RaisedSurface
                : state == CurriculumNodeState.Available ? DuelVisualTheme.Card
                : DuelVisualTheme.Track;
            card.Marker.gameObject.SetActive(selected);
            bool dim = state == CurriculumNodeState.Locked || state == CurriculumNodeState.Excluded;
            card.Title.color = dim ? Muted : Foreground;
            card.Icon.color = dim ? new Color(1f, 1f, 1f, .4f) : Color.white;
            card.Role?.SetAlpha(dim ? .4f : 1f);
            card.Status.text = StateText(state, card.Node, curriculum);
            card.Status.color = state == CurriculumNodeState.Active || state == CurriculumNodeState.Completed ? Accent
                : state == CurriculumNodeState.Excluded ? DuelVisualTheme.Danger : Muted;
            bool isNew = newNodes.Contains(card.Node.Id);
            card.Tag.text = isNew ? NewTag : card.Bridge ? BridgeTag : card.Capstone ? CapstoneTag : string.Empty;
            card.Tag.color = isNew ? Accent : card.Bridge ? Foreground : DuelVisualTheme.Paper;
            card.Tag.fontStyle = isNew ? FontStyle.Bold : FontStyle.Normal;
            card.Seal?.SetActive(state == CurriculumNodeState.Completed);
        }

        private static string StateText(CurriculumNodeState state, CurriculumNode node, CurriculumProgress curriculum)
        {
            switch (state)
            {
                case CurriculumNodeState.Completed: return "완료";
                case CurriculumNodeState.Active: return $"진행 중 {curriculum.ActiveBattles}/{node.Battles}";
                case CurriculumNodeState.Available: return $"전투 {node.Battles}회";
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
            if (skill != null && !run.IsLaneOpen(skill.LaneIndex))
                requirement.text += $"  ·  {SkillLaneStyle.Key(skill.LaneIndex)}열은 임무로 열림";
            exclusive.text = ExclusiveText(node, curriculum.Tree);
            bool fixedByProgress = curriculum.Active != null && curriculum.ActiveBattles > 0;
            availability.text = !Editing ? "로비에서 이용할 수 있습니다."
                : state == CurriculumNodeState.Completed ? skill == null ? "완료했습니다. 능력치 보상이 적용됐습니다."
                    : rewardText.Length > 0 ? "완료했습니다. 기술은 편성에서 장착하고 능력치 보상은 적용됐습니다."
                    : "완료했습니다. 얻은 기술은 편성에서 장착하세요."
                : state == CurriculumNodeState.Active ? $"진행 중 · 전투 {curriculum.ActiveBattles}/{node.Battles}. 전투를 마치면 "
                    + (skill == null ? "능력치 보상을 얻습니다." : rewardText.Length > 0 ? "기술과 능력치 보상을 얻습니다." : "기술을 얻습니다.")
                : state == CurriculumNodeState.Excluded ? "함께 고를 수 없는 과정을 이미 마쳤습니다. 초기화하면 다시 고를 수 있습니다."
                : state == CurriculumNodeState.Locked ? "선행 과정을 먼저 마치세요."
                : fixedByProgress ? "진행 중인 과정을 마친 뒤에 고를 수 있습니다."
                : curriculum.Active != null ? $"진행 중인 {curriculum.Active.Title} 대신 이 과정을 진행합니다."
                : "진행하면 다음 전투부터 반영됩니다. 승패와 관계없이 전투를 마치면 한 번으로 셉니다.";
            bool canSelect = Editing && curriculum.CanSelect(node.Id);
            primaryAction.interactable = canSelect;
            actionCaption.text = state == CurriculumNodeState.Completed ? "완료한 과정"
                : state == CurriculumNodeState.Active ? "진행 중"
                : state == CurriculumNodeState.Excluded ? "닫힌 과정"
                : state == CurriculumNodeState.Locked ? "잠긴 과정" : "이 과정 진행";
            actionCaption.color = canSelect ? DuelVisualTheme.Ink : Foreground;
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

        /// <summary>The detail's purpose line: <c>순환 과정  ·  전투 1회</c>, a capstone's <c>순환 과정 정수  ·  전투 1회</c>, a
        /// bridge's <c>다리 과정  ·  붕괴 × 순환  ·  전투 1회</c>.</summary>
        public static string PurposeText(CurriculumTree source, CurriculumNode node)
        {
            string battles = $"전투 {node.Battles}회";
            if (TryGetBridgedBranches(source, node, out CurriculumBranch left, out CurriculumBranch right))
                return $"{BranchName(node.Branch)} 과정  ·  {BranchName(left)} × {BranchName(right)}  ·  {battles}";
            return source != null && source.IsCapstone(node.Id)
                ? $"{BranchName(node.Branch)} 과정 {CapstoneTag}  ·  {battles}"
                : $"{BranchName(node.Branch)} 과정  ·  {battles}";
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
            if (reward.PlanningSeconds > 0) parts.Add("일반 스테이지 선택 시간 +" + reward.PlanningSeconds + "초");
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

        private void PerformSelect()
        {
            if (disposed || !Editing) return;
            string id = State.SelectedNodeId;
            if (!run.Curriculum.CanSelect(id)) return;
            if (select != null) select.Invoke(id);
            else run.TrySelectCurriculumNode(id);
            if (!disposed) Refresh();
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
}
