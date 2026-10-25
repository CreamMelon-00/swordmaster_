using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.LegacyCombat;
using TurnLimbo.Runtime.Sheets;
using Column = TurnLimbo.Runtime.LegacyCombat.LegacySkillSheet.Column;

namespace TurnLimbo.Core.Tests
{
    public sealed class IiaSkillSheetTests
    {
        private static List<string[]> Rows()
            => CsvTable.Read(LegacySkillSheet.Write(LegacySkillDefinitions.Table))
                .Select(record => record.Fields.ToArray()).ToList();

        private static int Index(string column) => LegacySkillSheet.Headers.ToList().IndexOf(column);
        private static void Set(List<string[]> rows, int id, string column, string value)
            => rows.Single(row => row[0] == id.ToString())[Index(column)] = value;
        private static LegacySkillTable Parse(List<string[]> rows)
            => LegacySkillSheet.Parse("iia authoring", CsvTable.Write(rows));

        [TestCase(503, "중간 베기", 1, 6, 8, 2, LegacySkillKind.Attack, LegacySkillProperty.Slash)]
        [TestCase(504, "너의 검술이야", 2, 0, 0, 1, LegacySkillKind.Attack, LegacySkillProperty.Slash)]
        [TestCase(505, "따라붙기", 2, 4, 5, 1, LegacySkillKind.Defence, LegacySkillProperty.Defence)]
        [TestCase(506, "나도 할 수 있어…", 3, 21, 37, 3, LegacySkillKind.Attack, LegacySkillProperty.Slash)]
        public void ForestBossRows_KeepTheAuthoredCostsPowersAndHitCounts(int id, string name,
            int cost, int min, int max, int hits, LegacySkillKind kind, LegacySkillProperty property)
        {
            LegacySkillDefinition definition = LegacySkillDefinitions.Find(id);
            LegacySkill skill = definition.Skill;
            Assert.That((skill.Name, skill.Cost, skill.MinPower, skill.MaxPower, skill.AttackCount, skill.Kind, skill.Property),
                Is.EqualTo((name, cost, min, max, hits, kind, property)));
            Assert.That(LegacySkillDefinitions.Table.IsEnemy(definition), Is.True);
            Assert.That(skill.IconId, Is.InRange(1, 39), "The boss reuses imported artwork.");
            Assert.That(definition.Text.Info, Is.Not.Null);
            Assert.That(definition.Text.Info.Description, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ForestBossEffects_AreTypedAndSurviveTheExistingSheetRoundTrip()
        {
            LegacySkillTable parsed = LegacySkillSheet.Parse("iia round trip",
                LegacySkillSheet.Write(LegacySkillDefinitions.Table));
            Assert.That(parsed.All.Count, Is.EqualTo(62));
            Assert.That(LegacySkillSheet.Headers.Count, Is.EqualTo(48), "Boss effects stay in the phrase columns; the optional duration column serves prototype skills.");
            Assert.That(parsed.Find(503).Effect.ActGain, Is.EqualTo(1));
            Assert.That(parsed.Find(504).Effect.MirrorOpponentMaxPower, Is.True);
            Assert.That(parsed.Find(505).Effect.BuffPowerFromPreviousOpponentUses, Is.True);
            Assert.That(parsed.Find(505).Effect.Roles & LegacySkillRole.FollowupPower, Is.Not.EqualTo(LegacySkillRole.None));
            Assert.That(parsed.Find(506).Effect.ConsumeJealousyOnReserve, Is.True);
            Assert.That(parsed.Find(506).Effect.InflictFixedTurns, Is.EqualTo(1));
            Assert.That(parsed.Find(506).Effect.HasReserveEffect && parsed.Find(506).Effect.HasUseEffect, Is.True);
            Assert.That(LegacySkillLabels.ReserveEffect(parsed.Find(506).Effect), Is.EqualTo("질투 모두 소모"));
            Assert.That(LegacySkillLabels.UseEffect(parsed.Find(506).Effect), Is.EqualTo("적중 시 상대에게 고정 1 부여"));
            foreach (int id in new[] { 503, 504, 505, 506 })
                Assert.That(LegacySkillSheetTests.Dump(parsed.Find(id)),
                    Is.EqualTo(LegacySkillSheetTests.Dump(LegacySkillDefinitions.Find(id))), "row " + id);
        }

        [Test]
        public void ForestBossRows_AreNeverGrantedOrEquippedByThePlayer()
        {
            var campaign = new CampaignRun();
            var versus = new VersusLoadout();
            foreach (int id in new[] { 503, 504, 505, 506 })
            {
                Assert.That(campaign.TryEquipSkill(id), Is.False);
                Assert.That(campaign.OwnedSkills.Any(owned => owned.SkillId == id), Is.False);
                Assert.That(versus.TryPlaceSkill(id, 0, 0), Is.False);
                Assert.That(LegacySkillDefinitions.InitialSkills.Concat(LegacySkillDefinitions.InitialCounterSkills)
                    .Concat(LegacySkillDefinitions.AcquisitionSkills).Concat(LegacySkillDefinitions.VersusSkills)
                    .Concat(LegacySkillDefinitions.TemporarySkills).Any(skill => skill.Id == id), Is.False);
            }
        }

        [TestCase("상대 기술 최대 위력 사용", "상대기술최대위력사용")]
        [TestCase("직전 턴 상대 사용 기술마다 강화 50% 1회", "직전턴상대사용기술마다강화50%1회")]
        [TestCase("적중 시 상대에게 고정 1 부여", "적중시상대에게고정1부여")]
        public void BossUsePhrases_AllowSpacingAndWriteTheCanonicalWords(string canonical, string compact)
        {
            var rows = Rows();
            Set(rows, 503, Column.UseEffect, compact);
            Assert.That(LegacySkillLabels.UseEffect(Parse(rows).Find(503).Effect), Is.EqualTo(canonical));
        }

        [Test]
        public void BossReservePhrase_AllowsSpacingAndWritesTheCanonicalWords()
        {
            var rows = Rows();
            Set(rows, 506, Column.ReserveEffect, "질투모두소모");
            Assert.That(LegacySkillLabels.ReserveEffect(Parse(rows).Find(506).Effect), Is.EqualTo("질투 모두 소모"));
        }

        [TestCase(1)]
        [TestCase(10)]
        public void FixedDuration_IsAuthoredAndIncludedInSheetChanges(int turns)
        {
            var rows = Rows();
            Set(rows, 506, Column.UseEffect, "적중 시 상대에게 고정 " + turns + " 부여");
            LegacySkillTable table = Parse(rows);
            Assert.That(table.Find(506).Effect.InflictFixedTurns, Is.EqualTo(turns));
            Assert.That(LegacySkillLabels.UseEffect(table.Find(506).Effect),
                Is.EqualTo("적중 시 상대에게 고정 " + turns + " 부여"));
            Assert.That(LegacySkillSheet.Describe(LegacySkillDefinitions.Table, table).Changed,
                Is.EqualTo(turns == 1 ? 0 : 1));
        }

        [TestCase("적중 시 상대에게 고정 0 부여")]
        [TestCase("적중 시 상대에게 고정 11 부여")]
        [TestCase("적중 시 상대에게 고정 -1 부여")]
        [TestCase("상대 기술 최대 위력 사용; 상대 기술 최대 위력 사용")]
        [TestCase("직전 턴 상대 사용 기술마다 강화 50% 1회; 직전 턴 상대 사용 기술마다 강화 50% 1회")]
        [TestCase("적중 시 상대에게 고정 1 부여; 적중 시 상대에게 고정 2 부여")]
        public void BossUsePhrases_RejectInvalidDurationsAndDuplicateEffects(string phrase)
        {
            var rows = Rows();
            Set(rows, 503, Column.UseEffect, phrase);
            var error = Assert.Throws<SkillSheetException>(() => Parse(rows));
            Assert.That(error.Message, Does.Contain("'사용 효과'"));
        }

        [TestCase(LegacySkillSheet.StartingGroup)]
        [TestCase(LegacySkillSheet.AcquisitionGroup)]
        [TestCase(LegacySkillSheet.TemporaryGroup)]
        [TestCase(LegacySkillSheet.VersusGroup)]
        public void BossEffects_AreRejectedOutsideEnemyRows(string group)
        {
            foreach (int id in new[] { 504, 505, 506 })
            {
                var rows = Rows();
                Set(rows, id, Column.Group, group);
                var error = Assert.Throws<SkillSheetException>(() => Parse(rows), "row " + id);
                Assert.That(error.Message, Does.Contain("적 전용 효과입니다"), "row " + id);
            }
        }

        [TestCase("상대 기술 최대 위력 사용")]
        [TestCase("적중 시 상대에게 고정 1 부여")]
        public void AttackOnlyBossEffects_RejectDefenceRows(string phrase)
        {
            var rows = Rows();
            Set(rows, 505, Column.UseEffect, phrase);
            Assert.That(Assert.Throws<SkillSheetException>(() => Parse(rows)).Message,
                Does.Contain("공격 기술에만 쓸 수 있습니다"));
        }

        [Test]
        public void BossEffects_DoNotOpenThePlayersQueueEffectsToEnemies()
        {
            var rows = Rows();
            Set(rows, 504, Column.UseEffect, "상대 기술 최대 위력 사용; 앞 숨고르기마다 위력 +4 (최대 4)");
            Assert.That(Assert.Throws<SkillSheetException>(() => Parse(rows)).Message,
                Does.Contain("적의 기술은 앞 기술을 세거나 저절로 피하지 않습니다"));
            rows = Rows();
            Set(rows, 506, Column.ReserveEffect, "숨고르기 +1");
            Assert.That(Assert.Throws<SkillSheetException>(() => Parse(rows)).Message,
                Does.Contain("적은 기술을 대기열에 직접 예약하지 않습니다"));
        }
    }
}
