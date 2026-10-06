using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TurnLimbo.Runtime.Sheets;

namespace TurnLimbo.Runtime.LegacyCombat
{
    /// <summary>Every problem a skill sheet has, found in one pass. Each problem names its spreadsheet row
    /// (the header is row 1, a cell with line breaks still counts as one row) and, for a cell, its column.</summary>
    public sealed class SkillSheetException : FormatException
    {
        public SkillSheetException(string sheetId, IReadOnlyList<string> problems)
            : base($"기술 시트 '{sheetId}': 문제 {problems?.Count ?? 0}개\n" + string.Join("\n", problems ?? new string[0]))
        {
            SheetId = sheetId;
            Problems = problems ?? new string[0];
        }

        public string SheetId { get; }
        public IReadOnlyList<string> Problems { get; }
    }

    /// <summary>A sheet row's 구분: who uses the technique.</summary>
    public enum LegacySkillGroup
    {
        /// <summary>시작: the player has it from the start; three per lane are the default loadout.</summary>
        Starting,
        /// <summary>획득: a curriculum node or a stage's first clear grants it.</summary>
        Acquisition,
        /// <summary>적: only an enemy uses it (a story enemy's script names it by id). It never reaches the loadout,
        /// the curriculum or the starting set.</summary>
        Enemy,
    }

    /// <summary>The techniques one sheet defines: every row in sheet order, split into the starting rows (시작), the
    /// rows the curriculum grants (획득) and the enemy-only rows (적).</summary>
    public sealed class LegacySkillTable
    {
        private readonly Dictionary<int, LegacySkillDefinition> byId = new Dictionary<int, LegacySkillDefinition>();
        private readonly Dictionary<int, LegacySkillGroup> groupById = new Dictionary<int, LegacySkillGroup>();

        internal LegacySkillTable(string sheetId, IReadOnlyList<LegacySkillDefinition> definitions, IReadOnlyList<LegacySkillGroup> groups)
        {
            SheetId = sheetId;
            var initial = new List<LegacySkill>();
            var acquisition = new List<LegacySkill>();
            var enemy = new List<LegacySkill>();
            for (int index = 0; index < definitions.Count; index++)
            {
                LegacySkill skill = definitions[index].Skill;
                byId.Add(skill.Id, definitions[index]);
                groupById.Add(skill.Id, groups[index]);
                (groups[index] == LegacySkillGroup.Starting ? initial
                    : groups[index] == LegacySkillGroup.Acquisition ? acquisition : enemy).Add(skill);
            }
            All = Array.AsReadOnly(definitions.ToArray());
            InitialSkills = initial.AsReadOnly();
            AcquisitionSkills = acquisition.AsReadOnly();
            EnemySkills = enemy.AsReadOnly();
        }

        public string SheetId { get; }
        /// <summary>Every technique in sheet row order.</summary>
        public IReadOnlyList<LegacySkillDefinition> All { get; }
        /// <summary>The 시작 rows in sheet order; within a lane this is the default loadout order.</summary>
        public IReadOnlyList<LegacySkill> InitialSkills { get; }
        /// <summary>The 획득 rows in sheet order.</summary>
        public IReadOnlyList<LegacySkill> AcquisitionSkills { get; }
        /// <summary>The 적 rows in sheet order: enemy-only techniques, never the player's.</summary>
        public IReadOnlyList<LegacySkill> EnemySkills { get; }

        public LegacySkillDefinition Find(int id) => byId.TryGetValue(id, out LegacySkillDefinition definition) ? definition : null;

        public bool IsStarting(LegacySkillDefinition definition) => Is(definition, LegacySkillGroup.Starting);

        public bool IsEnemy(LegacySkillDefinition definition) => Is(definition, LegacySkillGroup.Enemy);

        /// <summary>The 구분 of this table's row with the definition's id; throws for an id the table lacks.</summary>
        public LegacySkillGroup GroupOf(LegacySkillDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return groupById.TryGetValue(definition.Skill.Id, out LegacySkillGroup group) ? group
                : throw new ArgumentException($"Sheet '{SheetId}' has no technique {definition.Skill.Id}.", nameof(definition));
        }

        private bool Is(LegacySkillDefinition definition, LegacySkillGroup group)
            => definition != null && groupById.TryGetValue(definition.Skill.Id, out LegacySkillGroup found) && found == group;
    }

    /// <summary>What a new sheet changes against the current one, for the editor's confirmation.</summary>
    public sealed class LegacySkillSheetChanges
    {
        internal LegacySkillSheetChanges(IReadOnlyList<string> lines, int added, int removed, int changed, bool reordered, int total)
        {
            Lines = lines;
            Added = added;
            Removed = removed;
            Changed = changed;
            Reordered = reordered;
            Summary = HasChanges
                ? $"추가 {added} · 삭제 {removed} · 변경 {changed}{(reordered ? " · 순서 변경" : string.Empty)} (기술 {total}개)"
                : $"바뀐 내용이 없습니다 (기술 {total}개).";
        }

        /// <summary>One line per added, removed or changed technique (<c>추가: 12 플레슈</c>,
        /// <c>변경: 16 알티바호 — 이름, ACT</c>), then one line if the row order moved.</summary>
        public IReadOnlyList<string> Lines { get; }
        /// <summary>The count line.</summary>
        public string Summary { get; }
        public int Added { get; }
        public int Removed { get; }
        public int Changed { get; }
        public bool Reordered { get; }
        public bool HasChanges => Added + Removed + Changed > 0 || Reordered;
    }

    /// <summary>The skill sheet (CSV, edited in Excel or Google Sheets): one header row, then one technique per row.
    /// Columns are found by their header text, so their order is free and any other header is a memo column.
    /// <see cref="Parse"/> checks everything the combat and the skill texts rely on; <see cref="Write"/> produces a
    /// sheet <see cref="Parse"/> reads back to the same definitions.</summary>
    public static class LegacySkillSheet
    {
        public static class Column
        {
            public const string Id = "ID";
            public const string Name = "이름";
            public const string Group = "구분";
            public const string Lane = "열";
            public const string Kind = "종류";
            public const string Property = "속성";
            public const string Cost = "ACT";
            public const string MinPower = "최소 위력";
            public const string MaxPower = "최대 위력";
            public const string Hits = "타수";
            public const string Icon = "그림";
            public const string Animation = "애니메이션";
            public const string Description = "설명";
            public const string ActGain = "ACT 회복";
            public const string PowerBuff = "위력 버프";
            public const string ProtectionBuff = "보호 버프";
            public const string BuffSlots = "버프 칸";
            public const string ResistanceRecovery = "저항 회복";
            public const string ResistanceReduction = "저항 감소";
            public const string OpponentProperty = "상대 속성 조건";
            public const string OpponentKind = "상대 종류 조건";
            public const string HighPower = "고화력";
            public const string VariablePower = "위력 편차";
            public const string ShortLabel = "짧은 이름";
            public const string Detail = "전투 상세";
            public const string Purpose = "로비 목적";
            public const string Effect = "로비 효과";
            public const string InfoMain = "배지 주 문구";
            public const string InfoEnemyMain = "배지 주 문구(적)";
            public const string InfoMainSymbol = "배지 주 기호";
            public const string InfoMainTone = "배지 주 색";
            public const string InfoSecondary = "배지 보조 문구";
            public const string InfoEnemySecondary = "배지 보조 문구(적)";
            public const string InfoSecondarySymbol = "배지 보조 기호";
            public const string InfoSecondaryTone = "배지 보조 색";
            public const string InfoDescription = "배지 요약";
            public const string BrokenTargetDamage = "붕괴 대상 추가 피해";
            public const string OpponentBreak = "상대 붕괴";
        }

        public const string StartingGroup = "시작";
        public const string AcquisitionGroup = "획득";
        public const string EnemyGroup = "적";
        /// <summary>Ids from here up belong to techniques written in code, the 서막's practice skills (1001-1003). They are
        /// looked up by id like sheet rows, so a sheet row with one of these ids would take them over; the sheet may not use them.</summary>
        public const int ReservedIdStart = 1000;
        private const string LaneLetters = "QWE";
        // What decoding puts in place of bytes that are not UTF-8, such as a CSV that Excel saved in the Korean code page.
        private const char ReplacementCharacter = (char)0xFFFD;
        private const string TrueCell = "O";
        private static readonly string[] TrueValues = { "O", "○", "TRUE", "1", "예", "Y" };
        private static readonly string[] FalseValues = { "X", "FALSE", "0", "아니오", "N" };
        private static readonly LegacySkillProperty[] SheetProperties =
            { LegacySkillProperty.Slash, LegacySkillProperty.Hit, LegacySkillProperty.Penetrate, LegacySkillProperty.Defence };
        private static readonly LegacySkillKind[] SheetKinds = { LegacySkillKind.Attack, LegacySkillKind.Defence };
        private static readonly string[] InfoColumns =
        {
            Column.InfoMain, Column.InfoEnemyMain, Column.InfoMainSymbol, Column.InfoMainTone, Column.InfoSecondary,
            Column.InfoEnemySecondary, Column.InfoSecondarySymbol, Column.InfoSecondaryTone, Column.InfoDescription,
        };

        /// <summary>Every column, in the order <see cref="Write"/> puts them.</summary>
        public static IReadOnlyList<string> Headers { get; } = Array.AsReadOnly(new[]
        {
            Column.Id, Column.Name, Column.Group, Column.Lane, Column.Kind, Column.Property, Column.Cost,
            Column.MinPower, Column.MaxPower, Column.Hits, Column.Icon, Column.Animation, Column.Description,
            Column.ActGain, Column.PowerBuff, Column.ProtectionBuff, Column.BuffSlots, Column.ResistanceRecovery,
            Column.ResistanceReduction, Column.OpponentProperty, Column.OpponentKind, Column.HighPower,
            Column.VariablePower, Column.ShortLabel, Column.Detail, Column.Purpose, Column.Effect,
            Column.InfoMain, Column.InfoEnemyMain, Column.InfoMainSymbol, Column.InfoMainTone, Column.InfoSecondary,
            Column.InfoEnemySecondary, Column.InfoSecondarySymbol, Column.InfoSecondaryTone, Column.InfoDescription,
            Column.BrokenTargetDamage, Column.OpponentBreak,
        });

        /// <summary>Reads a sheet. Every problem is collected first and thrown once as a <see cref="SkillSheetException"/>.</summary>
        public static LegacySkillTable Parse(string sheetId, string csv)
        {
            if (string.IsNullOrWhiteSpace(sheetId)) throw new ArgumentException("A sheet id is required.", nameof(sheetId));
            if (csv == null) throw new ArgumentNullException(nameof(csv));
            IReadOnlyList<CsvRecord> records;
            try
            {
                records = CsvTable.Read(csv);
            }
            catch (CsvFormatException exception)
            {
                throw new SkillSheetException(sheetId, new[] { exception.Message });
            }

            var problems = new List<string>();
            Dictionary<string, int> columns = ReadHeader(records, problems);
            if (problems.Count > 0) throw new SkillSheetException(sheetId, problems);

            var rows = new List<ParsedRow>();
            var firstRowById = new Dictionary<int, int>();
            // The lane counts mean something only when every row's 구분 and 열 could be read.
            bool membershipKnown = true;
            var perLane = new int[LaneLetters.Length];
            bool anyAcquisition = false;
            for (int index = 1; index < records.Count; index++)
            {
                var reader = new RowReader(records[index], columns, problems);
                if (reader.IsBlank || reader.Cell(Column.Id).StartsWith("#", StringComparison.Ordinal)) continue;
                ParsedRow row = ReadRow(reader);
                if (!reader.Ok(Column.Group, Column.Lane)) membershipKnown = false;
                else if (row.Group == LegacySkillGroup.Starting) perLane[row.Lane]++;
                else if (row.Group == LegacySkillGroup.Acquisition) anyAcquisition = true;
                if (reader.Ok(Column.Id))
                {
                    if (firstRowById.TryGetValue(row.Id, out int first))
                        reader.RowProblem($"ID가 {first}행과 겹칩니다. ID는 기술마다 달라야 합니다.");
                    else firstRowById.Add(row.Id, reader.Row);
                }
                if (reader.Valid) rows.Add(row);
            }

            // The loadout saves exactly three skills per lane, and the starting set must already be one.
            if (membershipKnown)
            {
                if (perLane.Any(count => count != 3))
                    problems.Add($"'{Column.Group}'이(가) '{StartingGroup}'인 기술은 열마다 3개여야 합니다 " +
                        $"(지금 Q {perLane[0]}개, W {perLane[1]}개, E {perLane[2]}개).");
                if (!anyAcquisition)
                    problems.Add($"'{Column.Group}'이(가) '{AcquisitionGroup}'인 기술이 하나도 없습니다. 커리큘럼이 줄 기술이 하나 이상 있어야 합니다.");
            }
            if (problems.Count > 0) throw new SkillSheetException(sheetId, problems);

            var definitions = new LegacySkillDefinition[rows.Count];
            var groups = new LegacySkillGroup[rows.Count];
            for (int index = 0; index < rows.Count; index++)
            {
                definitions[index] = rows[index].Definition;
                groups[index] = rows[index].Group;
            }
            return new LegacySkillTable(sheetId, definitions, groups);
        }

        /// <summary>The whole sheet as CSV text: UTF-8 byte order mark (so Excel reads Korean), LF line ends,
        /// <see cref="Headers"/> in order, then one row per technique in table order.</summary>
        public static string Write(LegacySkillTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            return Write(table.All, table.GroupOf);
        }

        public static string Write(IReadOnlyList<LegacySkillDefinition> definitions, Func<LegacySkillDefinition, LegacySkillGroup> groupOf)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (groupOf == null) throw new ArgumentNullException(nameof(groupOf));
            var rows = new List<IReadOnlyList<string>>(definitions.Count + 1) { Headers };
            foreach (LegacySkillDefinition definition in definitions) rows.Add(Cells(definition, groupOf(definition)));
            return "\ufeff" + CsvTable.Write(rows);
        }

        /// <summary>Compares two tables by id and by the cells <see cref="Write"/> would give each technique, so a
        /// changed field is named by its column header. A null <paramref name="before"/> counts every row as added.</summary>
        public static LegacySkillSheetChanges Describe(LegacySkillTable before, LegacySkillTable after)
        {
            if (after == null) throw new ArgumentNullException(nameof(after));
            var lines = new List<string>();
            int added = 0, removed = 0, changed = 0;
            foreach (LegacySkillDefinition definition in after.All)
            {
                LegacySkill skill = definition.Skill;
                LegacySkillDefinition previous = before?.Find(skill.Id);
                if (previous == null)
                {
                    lines.Add($"추가: {skill.Id} {skill.Name}");
                    added++;
                    continue;
                }
                string[] oldCells = Cells(previous, before.GroupOf(previous));
                string[] newCells = Cells(definition, after.GroupOf(definition));
                var fields = new List<string>();
                for (int index = 0; index < Headers.Count; index++)
                    if (oldCells[index] != newCells[index]) fields.Add(Headers[index]);
                if (fields.Count == 0) continue;
                lines.Add($"변경: {skill.Id} {skill.Name} — {string.Join(", ", fields)}");
                changed++;
            }
            if (before != null)
                foreach (LegacySkillDefinition definition in before.All)
                    if (after.Find(definition.Skill.Id) == null)
                    {
                        lines.Add($"삭제: {definition.Skill.Id} {definition.Skill.Name}");
                        removed++;
                    }

            bool reordered = false;
            if (before != null)
            {
                int[] oldOrder = before.All.Select(d => d.Skill.Id).Where(id => after.Find(id) != null).ToArray();
                int[] newOrder = after.All.Select(d => d.Skill.Id).Where(id => before.Find(id) != null).ToArray();
                reordered = !oldOrder.SequenceEqual(newOrder);
                if (reordered) lines.Add("순서: 행 순서가 바뀌었습니다. 시작 기술은 열마다 위에서부터 기본 편성 순서가 됩니다.");
            }
            return new LegacySkillSheetChanges(lines.AsReadOnly(), added, removed, changed, reordered, after.All.Count);
        }

        private sealed class ParsedRow
        {
            public int Id, Lane;
            public LegacySkillGroup Group;
            public LegacySkillDefinition Definition;
        }

        private static Dictionary<string, int> ReadHeader(IReadOnlyList<CsvRecord> records, List<string> problems)
        {
            var columns = new Dictionary<string, int>();
            if (records.Count == 0)
            {
                problems.Add("1행: 시트가 비어 있습니다. 첫 행에 열 이름을 적으세요.");
                return columns;
            }
            CsvRecord header = records[0];
            // The Korean column names cannot be read at all, so listing them as missing would only mislead.
            if (header.Fields.Any(name => name.IndexOf(ReplacementCharacter) >= 0))
            {
                problems.Add("1행: 파일이 UTF-8이 아닙니다(한글이 깨져 읽힙니다). " +
                    "Excel에서 파일 → 다른 이름으로 저장 → CSV UTF-8(쉼표로 분리)로 다시 저장하세요.");
                return columns;
            }
            var counts = new Dictionary<string, int>();
            for (int index = 0; index < header.Count; index++)
            {
                string name = header[index].Trim();
                if (!Headers.Contains(name)) continue;
                counts[name] = counts.TryGetValue(name, out int count) ? count + 1 : 1;
                if (!columns.ContainsKey(name)) columns.Add(name, index);
            }
            foreach (string name in Headers)
                if (counts.TryGetValue(name, out int count) && count > 1)
                    problems.Add($"1행: '{name}' 열이 {count}번 있습니다. 같은 이름의 열은 하나만 둘 수 있습니다.");
            string[] missing = Headers.Where(name => !columns.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
                problems.Add($"1행: 필요한 열이 없습니다: {Quoted(missing)}. 첫 행의 열 이름은 띄어쓰기까지 똑같이 적어야 합니다 " +
                    $"(순서는 자유, 다른 이름의 열은 메모로 무시합니다). 필요한 열: {Quoted(Headers)}.");
            return columns;
        }

        private static ParsedRow ReadRow(RowReader r)
        {
            int id = r.Integer(Column.Id, 1, required: true);
            if (r.Ok(Column.Id) && id >= ReservedIdStart)
                r.Problem(Column.Id, $"'{r.Cell(Column.Id)}'은(는) 쓸 수 없습니다. {ReservedIdStart} 이상은 코드에 있는 서막 연습 기술" +
                    $"(연습 베기 등)이 쓰는 번호입니다. 1~{ReservedIdStart - 1} 중 쓰지 않는 번호를 적으세요.");
            string name = r.Required(Column.Name);
            LegacySkillGroup group = r.Group();
            int lane = r.Lane();
            LegacySkillKind kind = r.Kind(Column.Kind, required: true) ?? LegacySkillKind.Attack;
            LegacySkillProperty property = r.Property(Column.Property, required: true) ?? LegacySkillProperty.Slash;
            int cost = r.Integer(Column.Cost, 0, required: true);
            int minPower = r.Integer(Column.MinPower, 0, required: true);
            int maxPower = r.Integer(Column.MaxPower, 0, required: true);
            int hits = r.Integer(Column.Hits, 1, required: true);
            int icon = r.Integer(Column.Icon, 1, required: false);
            string animation = r.Text(Column.Animation);
            string description = r.Text(Column.Description) ?? string.Empty;
            var effect = new LegacySkillEffect
            {
                ActGain = r.Integer(Column.ActGain, 0, required: false),
                BuffPowerPercent = r.Integer(Column.PowerBuff, null, required: false, percent: true),
                BuffProtectionPercent = r.Integer(Column.ProtectionBuff, null, required: false, percent: true),
                BuffSlots = r.Integer(Column.BuffSlots, 0, required: false),
                ResistanceRecoveryPercent = r.Integer(Column.ResistanceRecovery, 0, required: false, percent: true),
                OpponentResistanceReduction = r.Integer(Column.ResistanceReduction, 0, required: false),
                BrokenTargetDamagePercent = r.Integer(Column.BrokenTargetDamage, 0, required: false, percent: true),
                BreaksOpponent = r.Boolean(Column.OpponentBreak),
                OpponentProperty = r.Property(Column.OpponentProperty, required: false),
                OpponentKind = r.Kind(Column.OpponentKind, required: false),
            };
            bool highPower = r.Boolean(Column.HighPower);
            bool variablePower = r.Boolean(Column.VariablePower);
            var text = new LegacySkillText
            {
                ShortLabel = r.Required(Column.ShortLabel),
                Detail = r.Required(Column.Detail),
                Purpose = r.Text(Column.Purpose),
                Effect = r.Text(Column.Effect),
            };
            if (InfoColumns.Any(column => r.Cell(column).Length > 0))
                text.Info = new LegacySkillInfo
                {
                    Main = r.Text(Column.InfoMain),
                    EnemyMain = r.Text(Column.InfoEnemyMain),
                    MainSymbol = r.Symbol(Column.InfoMainSymbol) ?? LegacySkillSymbol.Act,
                    MainTone = r.Tone(Column.InfoMainTone) ?? LegacySkillTone.Neutral,
                    Secondary = r.Text(Column.InfoSecondary) ?? string.Empty,
                    EnemySecondary = r.Text(Column.InfoEnemySecondary),
                    SecondarySymbol = r.Symbol(Column.InfoSecondarySymbol) ?? LegacySkillSymbol.Hits,
                    SecondaryTone = r.Tone(Column.InfoSecondaryTone) ?? LegacySkillTone.Neutral,
                    Description = r.Text(Column.InfoDescription),
                };

            if (r.Ok(Column.Id)) r.Id = id;
            // LegacySkill's own rules; cost, hits and lane are already checked cell by cell.
            if (r.Ok(Column.MinPower, Column.MaxPower) && maxPower < minPower)
                r.RowProblem($"'{Column.MaxPower}'({maxPower})이(가) '{Column.MinPower}'({minPower})보다 작습니다.");
            // The rules the effect interpreter and the skill texts rely on.
            if (r.Ok(Column.PowerBuff, Column.ProtectionBuff, Column.BuffSlots) &&
                effect.HasBuff != (effect.BuffPowerPercent != 0 || effect.BuffProtectionPercent != 0))
                r.RowProblem(effect.HasBuff
                    ? $"'{Column.BuffSlots}'이(가) 있으면 '{Column.PowerBuff}'나 '{Column.ProtectionBuff}'도 있어야 합니다."
                    : $"'{Column.PowerBuff}'나 '{Column.ProtectionBuff}'를 쓰려면 '{Column.BuffSlots}'이(가) 1 이상이어야 합니다.");
            if (effect.OpponentProperty.HasValue && effect.OpponentKind.HasValue)
                r.RowProblem($"상대 조건은 '{Column.OpponentProperty}'과(와) '{Column.OpponentKind}' 중 하나만 쓸 수 있습니다.");
            if (effect.HasOpponentCondition && r.Ok(Column.ResistanceRecovery) && effect.ResistanceRecoveryPercent > 0)
                r.RowProblem($"'{Column.ResistanceRecovery}'은(는) 그 자체가 조건이라 상대 조건과 함께 쓸 수 없습니다.");
            if (effect.HasOpponentCondition && r.Ok(Column.ActGain, Column.ResistanceReduction, Column.OpponentBreak) &&
                effect.ActGain == 0 && effect.OpponentResistanceReduction == 0 && !effect.BreaksOpponent)
                r.RowProblem("상대 조건은 ACT 회복, 저항 감소나 상대 붕괴가 있어야 합니다.");
            // The break already takes all of the resistance, so a direct loss beside it would never show.
            if (effect.BreaksOpponent && r.Ok(Column.ResistanceReduction) && effect.OpponentResistanceReduction > 0)
                r.RowProblem($"'{Column.OpponentBreak}'이(가) 있으면 '{Column.ResistanceReduction}'은(는) 쓸 수 없습니다. 붕괴가 저항을 모두 없앱니다.");
            if (effect.BrokenTargetDamagePercent > 0 && r.Ok(Column.Kind, Column.BrokenTargetDamage) &&
                kind != LegacySkillKind.Attack)
                r.RowProblem($"'{Column.BrokenTargetDamage}'은(는) 공격 기술에만 쓸 수 있습니다.");
            // Enemies never recover ACT, so on an enemy-only row the cell could only mislead.
            if (group == LegacySkillGroup.Enemy && r.Ok(Column.Group, Column.ActGain) && effect.ActGain > 0)
                r.RowProblem($"'{Column.Group}'이(가) '{EnemyGroup}'인 기술은 '{Column.ActGain}'을(를) 쓸 수 없습니다. 적은 ACT를 회복하지 않습니다.");
            if ((highPower || variablePower) && r.Ok(Column.Kind) && kind != LegacySkillKind.Attack)
                r.RowProblem($"'{Column.HighPower}'과(와) '{Column.VariablePower}'은(는) 공격에만 붙일 수 있습니다.");
            if (text.Info != null && (text.Info.Main == null || text.Info.Description == null))
                r.RowProblem($"배지 칸을 하나라도 쓰면 '{Column.InfoMain}'과(와) '{Column.InfoDescription}'을(를) 모두 적어야 합니다.");

            var row = new ParsedRow { Id = id, Lane = lane, Group = group };
            if (!r.Valid) return row;
            bool hasEffect = effect.ActGain != 0 || effect.HasBuff || effect.ResistanceRecoveryPercent != 0 ||
                effect.OpponentResistanceReduction != 0 || effect.BrokenTargetDamagePercent != 0 ||
                effect.BreaksOpponent || effect.HasOpponentCondition;
            row.Definition = new LegacySkillDefinition(
                new LegacySkill(id, name, cost, minPower, maxPower, kind, property, hits, lane, description, animation, icon),
                hasEffect ? effect : null, text, highPower, variablePower);
            return row;
        }

        private static string[] Cells(LegacySkillDefinition definition, LegacySkillGroup group)
        {
            LegacySkill skill = definition.Skill;
            LegacySkillEffect effect = definition.Effect;
            LegacySkillText text = definition.Text;
            LegacySkillInfo info = text.Info;
            var cells = new Dictionary<string, string>
            {
                [Column.Id] = Number(skill.Id),
                [Column.Name] = skill.Name,
                [Column.Group] = group == LegacySkillGroup.Starting ? StartingGroup
                    : group == LegacySkillGroup.Acquisition ? AcquisitionGroup : EnemyGroup,
                [Column.Lane] = LaneLetters[skill.LaneIndex].ToString(),
                [Column.Kind] = LegacySkillLabels.Kind(skill.Kind),
                [Column.Property] = LegacySkillLabels.Property(skill.Property),
                [Column.Cost] = Number(skill.Cost),
                [Column.MinPower] = Number(skill.MinPower),
                [Column.MaxPower] = Number(skill.MaxPower),
                [Column.Hits] = Number(skill.AttackCount),
                [Column.Icon] = skill.IconId == skill.Id ? string.Empty : Number(skill.IconId),
                [Column.Animation] = skill.AnimationName == LegacySkill.DefaultAnimationName(skill.Property) ? string.Empty : skill.AnimationName,
                [Column.Description] = skill.Description,
                [Column.ActGain] = Optional(effect.ActGain),
                [Column.PowerBuff] = Percent(effect.BuffPowerPercent),
                [Column.ProtectionBuff] = Percent(effect.BuffProtectionPercent),
                [Column.BuffSlots] = Optional(effect.BuffSlots),
                [Column.ResistanceRecovery] = Percent(effect.ResistanceRecoveryPercent),
                [Column.ResistanceReduction] = Optional(effect.OpponentResistanceReduction),
                [Column.BrokenTargetDamage] = Percent(effect.BrokenTargetDamagePercent),
                [Column.OpponentBreak] = effect.BreaksOpponent ? TrueCell : string.Empty,
                [Column.OpponentProperty] = effect.OpponentProperty.HasValue ? LegacySkillLabels.Property(effect.OpponentProperty.Value) : string.Empty,
                [Column.OpponentKind] = effect.OpponentKind.HasValue ? LegacySkillLabels.Kind(effect.OpponentKind.Value) : string.Empty,
                [Column.HighPower] = definition.HighPower ? TrueCell : string.Empty,
                [Column.VariablePower] = definition.VariablePower ? TrueCell : string.Empty,
                [Column.ShortLabel] = text.ShortLabel,
                [Column.Detail] = text.Detail,
                [Column.Purpose] = text.Purpose,
                [Column.Effect] = text.Effect,
                // A row with badges spells out every symbol and tone, defaults included.
                [Column.InfoMain] = info?.Main,
                [Column.InfoEnemyMain] = info?.EnemyMain,
                [Column.InfoMainSymbol] = info == null ? null : LegacySkillLabels.Symbol(info.MainSymbol),
                [Column.InfoMainTone] = info == null ? null : LegacySkillLabels.Tone(info.MainTone),
                [Column.InfoSecondary] = info?.Secondary,
                [Column.InfoEnemySecondary] = info?.EnemySecondary,
                [Column.InfoSecondarySymbol] = info == null ? null : LegacySkillLabels.Symbol(info.SecondarySymbol),
                [Column.InfoSecondaryTone] = info == null ? null : LegacySkillLabels.Tone(info.SecondaryTone),
                [Column.InfoDescription] = info?.Description,
            };
            return Headers.Select(header => cells[header] ?? string.Empty).ToArray();
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Optional(int value) => value == 0 ? string.Empty : Number(value);
        private static string Percent(int value) => value == 0 ? string.Empty : Number(value) + "%";
        private static string Quoted(IEnumerable<string> values) => string.Join(", ", values.Select(value => "'" + value + "'"));
        private static string Labels<T>(IEnumerable<T> values, Func<T, string> label) => string.Join(", ", values.Select(label));

        /// <summary>Reads one row's cells and records each problem with its row and column.</summary>
        private sealed class RowReader
        {
            private readonly CsvRecord record;
            private readonly Dictionary<string, int> columns;
            private readonly List<string> problems;
            private readonly HashSet<string> failed = new HashSet<string>();

            public RowReader(CsvRecord record, Dictionary<string, int> columns, List<string> problems)
            {
                this.record = record;
                this.columns = columns;
                this.problems = problems;
            }

            public int Row => record.RowNumber;
            /// <summary>The row's id once it has been read, for row problems.</summary>
            public int? Id { get; set; }
            public bool Valid { get; private set; } = true;
            public bool IsBlank => record.Fields.All(field => field.Trim().Length == 0);

            public string Cell(string header) => record[columns[header]].Trim();

            public bool Ok(params string[] headers) => headers.All(header => !failed.Contains(header));

            public void RowProblem(string message)
            {
                problems.Add(Id.HasValue ? $"{Row}행 (ID {Id.Value}) {message}" : $"{Row}행 {message}");
                Valid = false;
            }

            public string Text(string header)
            {
                string cell = Cell(header);
                return cell.Length == 0 ? null : cell;
            }

            public string Required(string header)
            {
                string cell = Text(header);
                if (cell == null) Problem(header, "칸이 비어 있습니다. 꼭 적어야 하는 칸입니다.");
                return cell;
            }

            /// <summary>An integer of at least <paramref name="minimum"/> (null: any sign); blank reads 0 unless required.</summary>
            public int Integer(string header, int? minimum, bool required, bool percent = false)
            {
                string cell = Cell(header);
                if (cell.Length == 0)
                {
                    if (required) Problem(header, "칸이 비어 있습니다. 꼭 적어야 하는 칸입니다.");
                    return 0;
                }
                string digits = percent && cell.EndsWith("%", StringComparison.Ordinal) ? cell.Substring(0, cell.Length - 1).TrimEnd() : cell;
                if (int.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value) &&
                    (!minimum.HasValue || value >= minimum.Value))
                    return value;
                string kind = minimum.HasValue ? $"{minimum.Value} 이상의 정수" : "정수";
                Problem(header, $"'{cell}'은(는) {kind}여야 합니다{(percent ? " (끝에 %를 붙여도 됩니다)" : string.Empty)}.");
                return 0;
            }

            public LegacySkillGroup Group()
            {
                string cell = Cell(Column.Group);
                if (cell == StartingGroup) return LegacySkillGroup.Starting;
                if (cell == AcquisitionGroup) return LegacySkillGroup.Acquisition;
                if (cell == EnemyGroup) return LegacySkillGroup.Enemy;
                Problem(Column.Group, Choice(cell, $"{StartingGroup}, {AcquisitionGroup}, {EnemyGroup}"));
                return LegacySkillGroup.Acquisition;
            }

            public int Lane()
            {
                string cell = Cell(Column.Lane);
                int lane = cell.Length == 1 ? LaneLetters.IndexOf(char.ToUpperInvariant(cell[0])) : -1;
                if (lane >= 0) return lane;
                Problem(Column.Lane, Choice(cell, "Q, W, E"));
                return 0;
            }

            public LegacySkillKind? Kind(string header, bool required)
            {
                string cell = Cell(header);
                string allowed = (required ? string.Empty : "비워 두거나 ") + Labels(SheetKinds, LegacySkillLabels.Kind);
                if (cell.Length == 0)
                {
                    if (required) Problem(header, Choice(cell, allowed));
                    return null;
                }
                if (!LegacySkillLabels.TryParseKind(cell, out LegacySkillKind kind)) Problem(header, Choice(cell, allowed));
                else if (kind == LegacySkillKind.Wait)
                    Problem(header, $"'{cell}'은(는) 숨고르기 같은 공용 행동의 종류라 기술에 쓸 수 없습니다. {allowed} 중 하나를 적으세요.");
                else return kind;
                return null;
            }

            public LegacySkillProperty? Property(string header, bool required)
            {
                string cell = Cell(header);
                string allowed = (required ? string.Empty : "비워 두거나 ") + Labels(SheetProperties, LegacySkillLabels.Property);
                if (cell.Length == 0)
                {
                    if (required) Problem(header, Choice(cell, allowed));
                    return null;
                }
                if (LegacySkillLabels.TryParseProperty(cell, out LegacySkillProperty property) && property != LegacySkillProperty.None)
                    return property;
                Problem(header, Choice(cell, allowed));
                return null;
            }

            public LegacySkillSymbol? Symbol(string header)
            {
                string cell = Cell(header);
                if (cell.Length == 0) return null;
                if (LegacySkillLabels.TryParseSymbol(cell, out LegacySkillSymbol symbol)) return symbol;
                Problem(header, Choice(cell, "비워 두거나 " + Labels((LegacySkillSymbol[])Enum.GetValues(typeof(LegacySkillSymbol)), LegacySkillLabels.Symbol)));
                return null;
            }

            public LegacySkillTone? Tone(string header)
            {
                string cell = Cell(header);
                if (cell.Length == 0) return null;
                if (LegacySkillLabels.TryParseTone(cell, out LegacySkillTone tone)) return tone;
                Problem(header, Choice(cell, "비워 두거나 " + Labels((LegacySkillTone[])Enum.GetValues(typeof(LegacySkillTone)), LegacySkillLabels.Tone)));
                return null;
            }

            public bool Boolean(string header)
            {
                string cell = Cell(header);
                if (cell.Length == 0) return false;
                if (TrueValues.Any(value => string.Equals(value, cell, StringComparison.OrdinalIgnoreCase))) return true;
                if (FalseValues.Any(value => string.Equals(value, cell, StringComparison.OrdinalIgnoreCase))) return false;
                // '중 하나로' keeps the particle off the last value, whose reading would decide it.
                Problem(header, $"'{cell}'은(는) 예/아니오로 읽을 수 없습니다. 예는 {string.Join(", ", TrueValues)} 중 하나, " +
                    $"아니오는 빈칸이나 {string.Join(", ", FalseValues)} 중 하나로 적으세요.");
                return false;
            }

            private static string Choice(string cell, string allowed) => cell.Length == 0
                ? $"칸이 비어 있습니다. {allowed} 중 하나를 적으세요."
                : $"'{cell}'은(는) 쓸 수 없습니다. {allowed} 중 하나를 적으세요.";

            public void Problem(string header, string message)
            {
                problems.Add($"{Row}행 '{header}': {message}");
                failed.Add(header);
                Valid = false;
            }
        }
    }
}
