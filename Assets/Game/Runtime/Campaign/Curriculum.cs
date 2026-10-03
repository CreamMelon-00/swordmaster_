using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>The school a curriculum node belongs to; used for grouping and labels.</summary>
    public enum CurriculumBranch { Slash, Pierce, Guard }

    public enum CurriculumNodeState
    {
        /// <summary>Its prerequisites are not complete yet.</summary>
        Locked,
        /// <summary>It can be chosen as the node in progress.</summary>
        Available,
        /// <summary>It is the node in progress.</summary>
        Active,
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

    /// <summary>One curriculum node, like a HOI4 national focus: it takes <see cref="Battles"/> finished battles
    /// while it is the node in progress, and completing it grants its skills and permanent stat bonuses.</summary>
    public sealed class CurriculumNode
    {
        private readonly SkillNameText title, description;

        /// <param name="title">The shown title, which may hold <see cref="LegacySkillNames"/> tokens. Null for a node that
        /// grants exactly one skill: it is then titled with that skill's current sheet name.</param>
        /// <param name="description">May hold <see cref="LegacySkillNames"/> tokens, formatted when read.</param>
        public CurriculumNode(string id, string title, CurriculumBranch branch, float column, int row,
            IEnumerable<int> skillIds, IEnumerable<string> requiresAll = null, IEnumerable<string> requiresAny = null,
            IEnumerable<string> exclusiveWith = null, int battles = 1, string description = null,
            CurriculumStatReward statReward = default)
        {
            if (string.IsNullOrWhiteSpace(id) || HasWhiteSpace(id))
                throw new ArgumentException("A curriculum node id must be one word.", nameof(id));
            if (title != null && string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A curriculum node needs a title.", nameof(title));
            if (battles < 1) throw new ArgumentOutOfRangeException(nameof(battles), "A node takes at least one battle.");
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
            Battles = battles;
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
        /// <summary>Finished battles needed while this is the node in progress. Data, so the pace can change later.</summary>
        public int Battles { get; }
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

        /// <summary>The node that grants the skill, or null for starting skills.</summary>
        public CurriculumNode FindGranting(int skillId)
        {
            foreach (CurriculumNode node in nodes)
                foreach (int granted in node.SkillIds)
                    if (granted == skillId) return node;
            return null;
        }

        private static IEnumerable<string> Concat(IEnumerable<string> a, IEnumerable<string> b)
        {
            foreach (string value in a) yield return value;
            foreach (string value in b) yield return value;
        }
    }

    /// <summary>Progress through a curriculum: completed nodes in order, the one node in progress and the battles
    /// it has absorbed. The owning <see cref="CampaignRun"/> performs every change so granted skills stay in step.</summary>
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
        public CurriculumNode Active { get; private set; }
        /// <summary>Finished battles counted toward <see cref="Active"/>.</summary>
        public int ActiveBattles { get; private set; }
        public int CompletedCount => completed.Count;

        public bool IsCompleted(string id) => id != null && completedSet.Contains(id);

        /// <summary>Whether some node can still be started. False once every node is completed or closed.</summary>
        public bool HasSelectableNode
        {
            get
            {
                foreach (CurriculumNode node in Tree.Nodes)
                    if (GetState(node.Id) == CurriculumNodeState.Available) return true;
                return false;
            }
        }

        /// <summary>No node in progress and none left to start: the curriculum is done until a reset.</summary>
        public bool IsFinished => Active == null && !HasSelectableNode;

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
            if (Active != null && Active.Id == id) return CurriculumNodeState.Active;
            if (IsExcluded(id)) return CurriculumNodeState.Excluded;
            return ArePrerequisitesMet(id) ? CurriculumNodeState.Available : CurriculumNodeState.Locked;
        }

        /// <summary>Whether the node can become the one in progress. Once a battle has counted toward the node in
        /// progress it is fixed until it completes, as a started national focus is.</summary>
        public bool CanSelect(string id)
            => Tree.Find(id) != null && GetState(id) == CurriculumNodeState.Available && (Active == null || ActiveBattles == 0);

        internal bool TrySelect(string id)
        {
            if (!CanSelect(id)) return false;
            Active = Tree.Find(id);
            ActiveBattles = 0;
            return true;
        }

        /// <summary>The node <see cref="RecordBattle"/> would complete now, or null. Nothing changes.</summary>
        internal CurriculumNode CompletesNext => Active != null && ActiveBattles + 1 >= Active.Battles ? Active : null;

        /// <summary>Counts one finished battle. Returns the node it completed, or null (also when nothing is in progress).</summary>
        internal CurriculumNode RecordBattle()
        {
            if (Active == null) return null;
            ActiveBattles++;
            if (ActiveBattles < Active.Battles) return null;
            CurriculumNode done = Active;
            completed.Add(done.Id);
            completedSet.Add(done.Id);
            Active = null;
            ActiveBattles = 0;
            return done;
        }

        internal void Reset()
        {
            completed.Clear();
            completedSet.Clear();
            Active = null;
            ActiveBattles = 0;
        }

        /// <summary>Checks saved progress against the tree without changing anything. Returns null when it is valid.</summary>
        internal string Validate(IReadOnlyList<string> completedIds, string activeId, int activeBattles)
        {
            var probe = new CurriculumProgress(Tree);
            foreach (string id in completedIds)
            {
                CurriculumNode node = Tree.Find(id);
                if (node == null) return $"알 수 없는 커리큘럼 '{id}'이(가) 완료되어 있습니다.";
                if (probe.IsCompleted(id)) return $"커리큘럼 '{id}'이(가) 두 번 완료되어 있습니다.";
                if (!probe.ArePrerequisitesMet(id)) return $"커리큘럼 '{id}'의 선행 과정이 먼저 완료되지 않았습니다.";
                if (probe.IsExcluded(id)) return $"커리큘럼 '{id}'은(는) 이미 완료한 과정과 함께 고를 수 없습니다.";
                probe.completed.Add(id);
                probe.completedSet.Add(id);
            }
            if (activeId == null) return activeBattles == 0 ? null : "진행 중인 커리큘럼 없이 전투 수가 남아 있습니다.";
            CurriculumNode active = Tree.Find(activeId);
            if (active == null) return $"알 수 없는 커리큘럼 '{activeId}'이(가) 진행 중입니다.";
            if (probe.GetState(activeId) != CurriculumNodeState.Available)
                return $"커리큘럼 '{activeId}'은(는) 지금 진행할 수 없는 과정입니다.";
            if (activeBattles < 0 || activeBattles >= active.Battles)
                return $"커리큘럼 '{activeId}'의 진행 전투 수 {activeBattles}은(는) 0~{active.Battles - 1} 범위 밖입니다.";
            return null;
        }

        internal void Restore(IReadOnlyList<string> completedIds, string activeId, int activeBattles)
        {
            Reset();
            foreach (string id in completedIds)
            {
                completed.Add(id);
                completedSet.Add(id);
            }
            Active = Tree.Find(activeId);
            ActiveBattles = Active == null ? 0 : activeBattles;
        }
    }
}
