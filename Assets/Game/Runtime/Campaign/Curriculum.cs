using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The part of the tree a node belongs to; used for grouping and labels. <see cref="Foundation"/> is the
    /// trunk every school opens from; <see cref="Circulation"/>, <see cref="Collapse"/> and <see cref="Retraction"/> are
    /// the three keyword schools (순환, 붕괴, 판짜기); a <see cref="Bridge"/> node joins two neighbouring schools.</summary>
    public enum CurriculumBranch { Foundation, Circulation, Collapse, Retraction, Bridge, Bleeding }

    /// <summary>A node's place in the tree. Whether an available node can be paid for now is not a state: it follows
    /// from the 수련 점수 balance (<see cref="CurriculumProgress.IsAffordable"/>).</summary>
    public enum CurriculumNodeState
    {
        /// <summary>Its prerequisites are not complete yet.</summary>
        Locked,
        /// <summary>Its prerequisites are complete, so it can be learned once the balance covers its cost.</summary>
        Available,
        Completed,
        /// <summary>A mutually exclusive node was completed, so this one is closed until a reset.</summary>
        Excluded,
    }

    /// <summary>Permanent bonuses granted by a completed curriculum node. All values are increases over the
    /// campaign's normal combat values; the planning time bonus applies to stage battles, not story missions.</summary>
    public readonly struct CurriculumStatReward
    {
        public CurriculumStatReward(int health = 0, int resistance = 0, int actGain = 0,
            int actCapacity = 0, int planningSeconds = 0)
        {
            if (health < 0) throw new ArgumentOutOfRangeException(nameof(health));
            if (resistance < 0) throw new ArgumentOutOfRangeException(nameof(resistance));
            if (actGain < 0) throw new ArgumentOutOfRangeException(nameof(actGain));
            if (actCapacity < 0) throw new ArgumentOutOfRangeException(nameof(actCapacity));
            if (planningSeconds < 0) throw new ArgumentOutOfRangeException(nameof(planningSeconds));
            Health = health;
            Resistance = resistance;
            ActGain = actGain;
            ActCapacity = actCapacity;
            PlanningSeconds = planningSeconds;
        }

        public int Health { get; }
        public int Resistance { get; }
        public int ActGain { get; }
        public int ActCapacity { get; }
        public int PlanningSeconds { get; }
        public bool IsEmpty => Health == 0 && Resistance == 0 && ActGain == 0 && ActCapacity == 0 && PlanningSeconds == 0;

        public static CurriculumStatReward operator +(CurriculumStatReward left, CurriculumStatReward right)
            => new CurriculumStatReward(checked(left.Health + right.Health),
                checked(left.Resistance + right.Resistance), checked(left.ActGain + right.ActGain),
                checked(left.ActCapacity + right.ActCapacity), checked(left.PlanningSeconds + right.PlanningSeconds));
    }

    /// <summary>One curriculum node. It is learned at once by spending <see cref="Cost"/> 수련 점수 (earned by battles and
    /// story missions, <see cref="CurriculumPointRules"/>), which grants its skills and permanent stat bonuses.</summary>
    public sealed class CurriculumNode
    {
        private readonly SkillNameText title, description;

        /// <param name="title">The shown title, which may hold <see cref="LegacySkillNames"/> tokens. Null for a node that
        /// grants exactly one skill: it is then titled with that skill's current sheet name.</param>
        /// <param name="description">May hold <see cref="LegacySkillNames"/> tokens, formatted when read.</param>
        public CurriculumNode(string id, string title, CurriculumBranch branch, float column, int row,
            IEnumerable<int> skillIds, IEnumerable<string> requiresAll = null, IEnumerable<string> requiresAny = null,
            IEnumerable<string> exclusiveWith = null, int cost = 1, string description = null,
            CurriculumStatReward statReward = default)
        {
            if (string.IsNullOrWhiteSpace(id) || HasWhiteSpace(id))
                throw new ArgumentException("A curriculum node id must be one word.", nameof(id));
            if (title != null && string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A curriculum node needs a title.", nameof(title));
            if (cost < 1) throw new ArgumentOutOfRangeException(nameof(cost), "A node costs at least one point.");
            Id = id;
            Branch = branch;
            Column = column;
            Row = row;
            SkillIds = new List<int>(skillIds ?? throw new ArgumentNullException(nameof(skillIds))).AsReadOnly();
            if (SkillIds.Count == 0 && statReward.IsEmpty)
                throw new ArgumentException("A curriculum node must grant a skill or a stat bonus.", nameof(skillIds));
            if (title == null && SkillIds.Count != 1)
                throw new ArgumentException("Only a node that grants one skill can take its title from the skill.", nameof(title));
            StatReward = statReward;
            RequiresAll = new List<string>(requiresAll ?? Array.Empty<string>()).AsReadOnly();
            RequiresAny = new List<string>(requiresAny ?? Array.Empty<string>()).AsReadOnly();
            ExclusiveWith = new List<string>(exclusiveWith ?? Array.Empty<string>()).AsReadOnly();
            Cost = cost;
            this.title = new SkillNameText(title ?? LegacySkillNames.Token(SkillIds[0]));
            this.description = new SkillNameText(description);
        }

        public string Id { get; }
        /// <summary>Read from the skill sheet now when the node was built without its own title.</summary>
        public string Title => title.Value;
        public CurriculumBranch Branch { get; }

        // The save codec splits on every char.IsWhiteSpace character, so ids may contain none of them.
        private static bool HasWhiteSpace(string value)
        {
            foreach (char character in value)
                if (char.IsWhiteSpace(character)) return true;
            return false;
        }
        /// <summary>Layout position in the tree view: fractional columns centre a node between two below it.</summary>
        public float Column { get; }
        public int Row { get; }
        /// <summary>Skills granted on completion.</summary>
        public IReadOnlyList<int> SkillIds { get; }
        /// <summary>Permanent stat bonuses granted on completion; empty for a skill-only node.</summary>
        public CurriculumStatReward StatReward { get; }
        /// <summary>Every one of these must be completed first.</summary>
        public IReadOnlyList<string> RequiresAll { get; }
        /// <summary>When not empty, at least one of these must be completed first.</summary>
        public IReadOnlyList<string> RequiresAny { get; }
        /// <summary>Completing any of these closes this node (and the reverse; the tree makes it symmetric).</summary>
        public IReadOnlyList<string> ExclusiveWith { get; }
        /// <summary>The 수련 점수 learning this node spends (1 or more). Data on each node, so the author tunes it per node
        /// (<see cref="CampaignCurriculum.Default"/>); a curriculum reset gives it back.</summary>
        public int Cost { get; }
        public string Description => description.Value;
    }

    /// <summary>A validated set of nodes. Prerequisites must name earlier nodes, so the tree has no cycles.</summary>
    public sealed class CurriculumTree
    {
        private readonly List<CurriculumNode> nodes;
        private readonly Dictionary<string, CurriculumNode> byId = new Dictionary<string, CurriculumNode>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> exclusive = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public CurriculumTree(IEnumerable<CurriculumNode> source)
        {
            nodes = new List<CurriculumNode>(source ?? throw new ArgumentNullException(nameof(source)));
            if (nodes.Count == 0) throw new ArgumentException("A curriculum needs nodes.", nameof(source));
            var skills = new HashSet<int>();
            foreach (CurriculumNode node in nodes)
            {
                if (node == null) throw new ArgumentException("A curriculum node is missing.", nameof(source));
                foreach (string required in Concat(node.RequiresAll, node.RequiresAny))
                    if (!byId.ContainsKey(required))
                        throw new ArgumentException($"Node '{node.Id}' requires '{required}', which is not an earlier node.");
                if (byId.ContainsKey(node.Id)) throw new ArgumentException($"Duplicate curriculum node '{node.Id}'.");
                foreach (int skill in node.SkillIds)
                    if (!skills.Add(skill)) throw new ArgumentException($"Skill {skill} is granted by two nodes.");
                byId.Add(node.Id, node);
                exclusive.Add(node.Id, new HashSet<string>(StringComparer.Ordinal));
            }
            foreach (CurriculumNode node in nodes)
                foreach (string other in node.ExclusiveWith)
                {
                    if (!byId.ContainsKey(other)) throw new ArgumentException($"Node '{node.Id}' excludes unknown '{other}'.");
                    if (other == node.Id) throw new ArgumentException($"Node '{node.Id}' cannot exclude itself.");
                    exclusive[node.Id].Add(other);
                    exclusive[other].Add(node.Id);
                }
            Nodes = nodes.AsReadOnly();
        }

        public IReadOnlyList<CurriculumNode> Nodes { get; }

        public CurriculumNode Find(string id) => id != null && byId.TryGetValue(id, out CurriculumNode node) ? node : null;

        public bool AreExclusive(string a, string b) => a != null && exclusive.TryGetValue(a, out HashSet<string> set) && set.Contains(b);

        /// <summary>Whether the node closes its part of the tree: a trunk or school node (not a bridge) that no other node
        /// of its own branch requires. The trunk's last step and each school's capstone (정수) end their branches.</summary>
        public bool EndsBranch(string id)
        {
            CurriculumNode node = Find(id);
            if (node == null || node.Branch == CurriculumBranch.Bridge) return false;
            foreach (CurriculumNode other in nodes)
                if (other.Branch == node.Branch && !ReferenceEquals(other, node) &&
                    (Contains(other.RequiresAll, id) || Contains(other.RequiresAny, id))) return false;
            return true;
        }

        /// <summary>Whether the node is a school's capstone (정수): the end of one of the three schools.</summary>
        public bool IsCapstone(string id)
        {
            CurriculumNode node = Find(id);
            return node != null && node.Branch != CurriculumBranch.Foundation && EndsBranch(id);
        }

        /// <summary>The node that grants the skill, or null for starting skills.</summary>
        public CurriculumNode FindGranting(int skillId)
        {
            foreach (CurriculumNode node in nodes)
                foreach (int granted in node.SkillIds)
                    if (granted == skillId) return node;
            return null;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            foreach (string candidate in ids)
                if (string.Equals(candidate, id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static IEnumerable<string> Concat(IEnumerable<string> a, IEnumerable<string> b)
        {
            foreach (string value in a) yield return value;
            foreach (string value in b) yield return value;
        }
    }

    /// <summary>Progress through a curriculum: the completed nodes in the order they were learned. The 수련 점수 balance
    /// that pays for them belongs to the owning <see cref="CampaignRun"/>, which performs every change so granted skills
    /// stay in step; the affordability helpers here take a balance as an argument and change nothing.</summary>
    public sealed class CurriculumProgress
    {
        private readonly List<string> completed = new List<string>();
        private readonly HashSet<string> completedSet = new HashSet<string>(StringComparer.Ordinal);

        internal CurriculumProgress(CurriculumTree tree)
        {
            Tree = tree ?? throw new ArgumentNullException(nameof(tree));
            Completed = completed.AsReadOnly();
        }

        public CurriculumTree Tree { get; }
        /// <summary>Completed node ids in completion order.</summary>
        public IReadOnlyList<string> Completed { get; }
        public int CompletedCount => completed.Count;

        public bool IsCompleted(string id) => id != null && completedSet.Contains(id);

        /// <summary>Whether some node can still be learned, whatever the balance. False once every node is completed or
        /// closed.</summary>
        public bool HasAvailableNode
        {
            get
            {
                foreach (CurriculumNode node in Tree.Nodes)
                    if (GetState(node.Id) == CurriculumNodeState.Available) return true;
                return false;
            }
        }

        /// <summary>No node is left to learn: the curriculum is done until a reset.</summary>
        public bool IsFinished => !HasAvailableNode;

        /// <summary>The 수련 점수 spent on the completed nodes: what a curriculum reset gives back.</summary>
        public int SpentPoints
        {
            get
            {
                long total = 0;
                foreach (string id in completed) total += Tree.Find(id)?.Cost ?? 0;
                return total > int.MaxValue ? int.MaxValue : (int)total;
            }
        }

        /// <summary>Whether the node is available and <paramref name="points"/> cover its cost.</summary>
        public bool IsAffordable(string id, int points)
        {
            CurriculumNode node = Tree.Find(id);
            return node != null && points >= node.Cost && GetState(id) == CurriculumNodeState.Available;
        }

        /// <summary>How many available nodes <paramref name="points"/> cover, each on its own (익힐 수 있는 과정 k개).</summary>
        public int AffordableCount(int points)
        {
            int count = 0;
            foreach (CurriculumNode node in Tree.Nodes)
                if (IsAffordable(node.Id, points)) count++;
            return count;
        }

        /// <summary>The points still missing for the cheapest available node (다음 과정까지 m점). Zero when some available
        /// node is affordable already, and when none is available (<see cref="IsFinished"/>).</summary>
        public int PointsToNext(int points)
        {
            int cheapest = int.MaxValue;
            foreach (CurriculumNode node in Tree.Nodes)
                if (node.Cost < cheapest && GetState(node.Id) == CurriculumNodeState.Available) cheapest = node.Cost;
            return cheapest == int.MaxValue || points >= cheapest ? 0 : cheapest - points;
        }

        /// <summary>Whether going from <paramref name="before"/> to <paramref name="after"/> points made some available
        /// node affordable that was not before: its cost is above the old balance and within the new one.</summary>
        public bool BecameAffordable(int before, int after)
        {
            foreach (CurriculumNode node in Tree.Nodes)
                if (node.Cost > before && node.Cost <= after && GetState(node.Id) == CurriculumNodeState.Available) return true;
            return false;
        }

        /// <summary>The nodes that completing <paramref name="id"/> opened: those available now whose prerequisites hold
        /// only thanks to it, every "all" one naming it or no other "any" one completed. Empty when <paramref name="id"/>
        /// is not completed. The tree's "새" tag and the learning moment's <c>새로 열린 과정</c> read it for the last
        /// completed node, so nothing is stored.</summary>
        public IReadOnlyList<CurriculumNode> OpenedBy(string id)
        {
            var opened = new List<CurriculumNode>();
            if (!IsCompleted(id)) return opened;
            foreach (CurriculumNode node in Tree.Nodes)
            {
                if (GetState(node.Id) != CurriculumNodeState.Available) continue;
                bool needsAll = false, anyNamed = false, anyOther = false;
                foreach (string required in node.RequiresAll)
                    if (string.Equals(required, id, StringComparison.Ordinal)) needsAll = true;
                foreach (string any in node.RequiresAny)
                {
                    if (string.Equals(any, id, StringComparison.Ordinal)) anyNamed = true;
                    else if (IsCompleted(any)) anyOther = true;
                }
                if (needsAll || anyNamed && !anyOther) opened.Add(node);
            }
            return opened;
        }

        /// <summary>The last completed node, or null.</summary>
        public CurriculumNode LastCompleted => completed.Count == 0 ? null : Tree.Find(completed[completed.Count - 1]);

        public bool IsExcluded(string id)
        {
            if (id == null || IsCompleted(id)) return false;
            foreach (string done in completed)
                if (Tree.AreExclusive(id, done)) return true;
            return false;
        }

        public bool ArePrerequisitesMet(string id)
        {
            CurriculumNode node = Tree.Find(id);
            if (node == null) return false;
            foreach (string required in node.RequiresAll)
                if (!IsCompleted(required)) return false;
            if (node.RequiresAny.Count == 0) return true;
            foreach (string any in node.RequiresAny)
                if (IsCompleted(any)) return true;
            return false;
        }

        public CurriculumNodeState GetState(string id)
        {
            if (Tree.Find(id) == null) throw new ArgumentException($"Unknown curriculum node '{id}'.", nameof(id));
            if (IsCompleted(id)) return CurriculumNodeState.Completed;
            if (IsExcluded(id)) return CurriculumNodeState.Excluded;
            return ArePrerequisitesMet(id) ? CurriculumNodeState.Available : CurriculumNodeState.Locked;
        }

        /// <summary>Completes a node; the caller has checked that it is available and taken its cost.</summary>
        internal void Complete(string id)
        {
            completed.Add(id);
            completedSet.Add(id);
        }

        internal void Reset()
        {
            completed.Clear();
            completedSet.Clear();
        }

        /// <summary>Checks saved progress against the tree without changing anything. Returns null when it is valid.</summary>
        internal string Validate(IReadOnlyList<string> completedIds)
        {
            var probe = new CurriculumProgress(Tree);
            foreach (string id in completedIds)
            {
                if (Tree.Find(id) == null) return $"알 수 없는 커리큘럼 '{id}'이(가) 완료되어 있습니다.";
                if (probe.IsCompleted(id)) return $"커리큘럼 '{id}'이(가) 두 번 완료되어 있습니다.";
                if (!probe.ArePrerequisitesMet(id)) return $"커리큘럼 '{id}'의 선행 과정이 먼저 완료되지 않았습니다.";
                if (probe.IsExcluded(id)) return $"커리큘럼 '{id}'은(는) 이미 완료한 과정과 함께 고를 수 없습니다.";
                probe.Complete(id);
            }
            return null;
        }

        internal void Restore(IReadOnlyList<string> completedIds)
        {
            Reset();
            foreach (string id in completedIds) Complete(id);
        }
    }
}
