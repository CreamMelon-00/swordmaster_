using System;
using System.Collections.Generic;
using TurnLimbo.Runtime.LegacyCombat;

namespace TurnLimbo.Runtime.Campaign
{
    /// <summary>How a stage's enemy spends its turns (2026-10-02). Internal only: no name is shown on screen, and the
    /// enemy's queue for the turn is still shown before the player plans. Each rhythm asks a different question of
    /// the 'save ACT, then fire the strong skills' habit.</summary>
    public enum CampaignEnemyRhythm
    {
        /// <summary>The original cycle: the six basic skills in order, 2→3→2→1 actions a turn.</summary>
        Basic,
        /// <summary>Do not fire into its guard: two light turns, a four-guard turn (more than three free 숨고르기 can
        /// step past), then one heavy blow with open slots.</summary>
        Gatekeeper,
        /// <summary>Strong from turn 1, then a little lighter: two heavy opening turns once, then a loop of about two
        /// thirds of that, every looping turn still attacking at least twice.</summary>
        Opener,
        /// <summary>Saving gets you hit: three and four actions every turn, nearly all attacks.</summary>
        Onslaught,
        /// <summary>Save for its blow: two guarding turns, then two heavy attacks.</summary>
        Charge,
    }

    public static class CampaignEnemyRhythms
    {
        // Stages 1-2 keep the original cycle while the lobby missions arrive; then each rhythm in turn, with the two
        // timing rhythms (Opener, Charge) again at the end where the stats and the enemy counters are highest.
        private static readonly CampaignEnemyRhythm[] stages =
        {
            CampaignEnemyRhythm.Basic, CampaignEnemyRhythm.Basic, CampaignEnemyRhythm.Gatekeeper, CampaignEnemyRhythm.Opener,
            CampaignEnemyRhythm.Onslaught, CampaignEnemyRhythm.Charge, CampaignEnemyRhythm.Opener, CampaignEnemyRhythm.Charge,
        };

        public static CampaignEnemyRhythm ForStage(int stageNumber)
            => stageNumber >= 1 && stageNumber <= stages.Length ? stages[stageNumber - 1] : CampaignEnemyRhythm.Basic;

        /// <summary>The turn-by-turn script for a rhythm, or null for <see cref="CampaignEnemyRhythm.Basic"/> (the cycle).</summary>
        public static EnemyScript Script(CampaignEnemyRhythm rhythm)
        {
            switch (rhythm)
            {
                case CampaignEnemyRhythm.Gatekeeper:
                    return new EnemyScript(Turns(
                        T(Slash, DeepThrust), T(BreakMomentum, Slash, DeepThrust), T(Guard, FlowGuard, Guard, Deflect), T(PreciseThrust)));
                case CampaignEnemyRhythm.Opener:
                    // Once the opening is spent it eases off a little, catching its breath behind a guard first.
                    return new EnemyScript(
                        Turns(T(DeepThrust, BreakMomentum, Guard), T(QuickSlash, BreakMomentum, PreciseThrust)),
                        Turns(T(PreciseThrust, DeepThrust, QuickSlash), T(PreciseThrust, FindOpening, DeepThrust)));
                case CampaignEnemyRhythm.Onslaught:
                    // One guard keeps the curriculum's defence-reading skills useful here too.
                    return new EnemyScript(Turns(
                        T(Slash, DeepThrust, Slash, DeepThrust), T(Slash, BreakMomentum, Guard), T(DeepThrust, Slash, DeepThrust, Slash)));
                case CampaignEnemyRhythm.Charge:
                    return new EnemyScript(Turns(T(Guard, Deflect), T(Guard), T(DeepThrust, PreciseThrust)));
                default:
                    return null;
            }
        }

        // The starting techniques by id, read from the sheet each time a script is built.
        private static LegacySkill Slash => LegacySkillDefinitions.Skill(1);
        private static LegacySkill QuickSlash => LegacySkillDefinitions.Skill(2);
        private static LegacySkill DeepThrust => LegacySkillDefinitions.Skill(3);
        private static LegacySkill PreciseThrust => LegacySkillDefinitions.Skill(4);
        private static LegacySkill BreakMomentum => LegacySkillDefinitions.Skill(5);
        private static LegacySkill FindOpening => LegacySkillDefinitions.Skill(6);
        private static LegacySkill Guard => LegacySkillDefinitions.Skill(7);
        private static LegacySkill FlowGuard => LegacySkillDefinitions.Skill(8);
        private static LegacySkill Deflect => LegacySkillDefinitions.Skill(9);

        private static IReadOnlyList<LegacySkill> T(params LegacySkill[] actions) => actions;

        private static IReadOnlyList<IReadOnlyList<LegacySkill>> Turns(params IReadOnlyList<LegacySkill>[] turns) => turns;
    }
}
