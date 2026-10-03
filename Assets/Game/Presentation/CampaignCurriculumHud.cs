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
    /// Clicking a node only selects it; one explicit button makes it the node in progress. CampaignRun owns the rules.</summary>
    public sealed class CampaignCurriculumHud : IDisposable
    {
        public sealed class ViewState
        {
            public string SelectedNodeId { get; internal set; }
            public void Reset() => SelectedNodeId = null;
        }

        private sealed class NodeCard
        {
            public CurriculumNode Node;
            public Button Button;
            public Image Background, Icon, Marker;
            public Text Title, Status;
        }

        public const float NodeWidth = 116f, NodeHeight = 100f;
        private const float TreeWidth = 760f, TreeHeight = 544f, ColumnWidth = 123f, RowHeight = 168f;
        private static readonly Vector2 TreeCenter = new Vector2(-216f, -53f);
        private readonly RectTransform root, tree, detailFrame, detailSurface, detailRule;
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
        private bool disposed, resetArmed;
        private static Color Border => DuelVisualTheme.Border;
        private static Color Accent => DuelVisualTheme.Accent;
        private static Color Foreground => DuelVisualTheme.Foreground;
        private static Color Muted => DuelVisualTheme.Muted;

        public GameObject Root => root.gameObject;
        public ViewState State { get; }
        public bool IsResetArmed => !disposed && resetArmed;

        public CampaignCurriculumHud(RectTransform parent, LegacyDuelArt art, CampaignRun run,
            Action<string> select, Action reset, ViewState state = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            this.art = art ?? throw new ArgumentNullException(nameof(art));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.select = select;
            this.reset = reset;
            State = state ?? new ViewState();
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
            BuildBranchHeadings();
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
            purpose = Label("Curriculum Detail Purpose", detail.transform, Vector2.zero, new Vector2(162f, 26f), 17);
            Fit(purpose, 13, 17);
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
        }

        /// <summary>Position of a node card inside the tree frame.</summary>
        public static Vector2 NodePosition(CurriculumNode node)
            => new Vector2(-TreeWidth * .5f + ColumnWidth * .5f + 8f + node.Column * ColumnWidth,
                TreeHeight * .5f - 40f - NodeHeight * .5f - 34f - node.Row * RowHeight);

        public void SelectNode(string id)
        {
            if (disposed || run.Curriculum.Tree.Find(id) == null) return;
            State.SelectedNodeId = id;
            Refresh();
        }

        public void Refresh()
        {
            if (disposed) return;
            CurriculumProgress curriculum = run.Curriculum;
            if (curriculum.Tree.Find(State.SelectedNodeId) == null)
                State.SelectedNodeId = curriculum.Active?.Id ?? FirstAvailable() ?? curriculum.Tree.Nodes[0].Id;
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
            RefreshDetail(curriculum);
        }

        private bool Editing => run.Phase == CampaignPhase.Lobby || run.Phase == CampaignPhase.Maintenance;

        private string FirstAvailable()
        {
            foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
                if (run.Curriculum.GetState(node.Id) == CurriculumNodeState.Available) return node.Id;
            return null;
        }

        private void BuildBranchHeadings()
        {
            var columns = new Dictionary<CurriculumBranch, (float min, float max)>();
            foreach (CurriculumNode node in run.Curriculum.Tree.Nodes)
                columns[node.Branch] = columns.TryGetValue(node.Branch, out var range)
                    ? (Mathf.Min(range.min, node.Column), Mathf.Max(range.max, node.Column)) : (node.Column, node.Column);
            foreach (KeyValuePair<CurriculumBranch, (float min, float max)> entry in columns)
            {
                float x = -TreeWidth * .5f + ColumnWidth * .5f + 8f + (entry.Value.min + entry.Value.max) * .5f * ColumnWidth;
                Label("Branch " + entry.Key, tree, new Vector2(x, TreeHeight * .5f - 26f), new Vector2(240f, 30f), 20,
                    DuelVisualTheme.Paper, TextAnchor.MiddleCenter).text = BranchName(entry.Key);
            }
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
            => Panel(name, tree, position, size, color);

        private void AddCard(CurriculumNode node)
        {
            var card = new NodeCard { Node = node };
            card.Button = ActionButton("Curriculum Node " + node.Id, tree, null, NodePosition(node),
                new Vector2(NodeWidth, NodeHeight), () => SelectNode(node.Id));
            card.Background = card.Button.GetComponent<Image>();
            card.Marker = Panel("Selected Marker", card.Button.transform, new Vector2(0f, NodeHeight * .5f - 3f),
                new Vector2(NodeWidth, 6f), Accent);
            card.Icon = Panel("Node Icon", card.Button.transform, new Vector2(-30f, 22f), Vector2.one * 40f, Color.white);
            card.Icon.preserveAspect = true;
            LegacySkill skill = GrantedSkill(node);
            card.Icon.sprite = skill != null ? art.GetSkillIcon(skill.IconId) : null;
            card.Icon.enabled = card.Icon.sprite != null;
            if (skill != null)
                Label("Node Lane", card.Button.transform, new Vector2(22f, 30f), new Vector2(40f, 22f), 15, Muted,
                    TextAnchor.MiddleCenter).text = SkillLaneStyle.Key(skill.LaneIndex);
            else if (!node.StatReward.IsEmpty)
            {
                Text rewardLabel = Label("Node Lane", card.Button.transform, new Vector2(22f, 30f),
                    new Vector2(52f, 22f), 15, Muted, TextAnchor.MiddleCenter);
                Fit(rewardLabel, 11, 15);
                rewardLabel.text = "능력치";
            }
            if (node.ExclusiveWith.Count > 0 || HasExclusivePartner(node))
                Label("Node Exclusive", card.Button.transform, new Vector2(38f, 8f), new Vector2(40f, 20f), 13,
                    DuelVisualTheme.Danger, TextAnchor.MiddleCenter).text = "택1";
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
            bool selected = State.SelectedNodeId == card.Node.Id;
            card.Background.color = state == CurriculumNodeState.Active ? DuelVisualTheme.Selected
                : state == CurriculumNodeState.Completed ? DuelVisualTheme.RaisedSurface
                : state == CurriculumNodeState.Available ? DuelVisualTheme.Card
                : DuelVisualTheme.Track;
            card.Marker.gameObject.SetActive(selected);
            bool dim = state == CurriculumNodeState.Locked || state == CurriculumNodeState.Excluded;
            card.Title.color = dim ? Muted : Foreground;
            card.Icon.color = dim ? new Color(1f, 1f, 1f, .4f) : Color.white;
            card.Status.text = StateText(state, card.Node, curriculum);
            card.Status.color = state == CurriculumNodeState.Active || state == CurriculumNodeState.Completed ? Accent
                : state == CurriculumNodeState.Excluded ? DuelVisualTheme.Danger : Muted;
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
            string rewardText = StatRewardText(node.StatReward);
            if (skill != null)
            {
                detailStyle.SetLane(skill.LaneIndex);
                detailIcon.enabled = true;
                detailIcon.sprite = art.GetSkillIcon(skill.IconId);
                detailInfo.SetSkill(skill, false, rewardText.Length > 0 ? "능력치 보상: " + rewardText : null);
            }
            else
            {
                detailStyle.Clear("능력치 보상");
                detailIcon.enabled = false;
                detailIcon.sprite = null;
                detailInfo.SetEmptyMessage(rewardText);
            }
            detailName.text = node.Title;
            purpose.text = $"{BranchName(node.Branch)} 과정  ·  전투 {node.Battles}회";
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
            purpose.rectTransform.anchoredPosition = new Vector2(20f, top - 78f);
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
            => branch == CurriculumBranch.Slash ? "참격" : branch == CurriculumBranch.Pierce ? "관통" : "수비";

        // Any sheet row, as CampaignRun grants it, so a node whose technique became a starting one still shows it.
        private static LegacySkill GrantedSkill(CurriculumNode node)
            => node.SkillIds.Count == 0 ? null : LegacySkillDefinitions.Find(node.SkillIds[0])?.Skill;

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
