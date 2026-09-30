using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Runtime.Save
{
    /// <summary>Everything the temporary auto-save keeps: how far the opening arc has gone and the campaign's
    /// persistent state. A battle in progress is never saved; loading resumes at a briefing or the lobby.</summary>
    public sealed class GameSave
    {
        public const int CurrentVersion = 1;

        public GameSave(int prologueCleared, CampaignSave campaign)
        {
            PrologueCleared = prologueCleared;
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        }

        /// <summary>Opening-arc missions won in order.</summary>
        public int PrologueCleared { get; }
        public CampaignSave Campaign { get; }

        public static GameSave Capture(PrologueRun prologue, CampaignRun campaign)
        {
            if (prologue == null) throw new ArgumentNullException(nameof(prologue));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            return new GameSave(prologue.ClearedCount, campaign.CaptureSave());
        }

        /// <summary>Loads this save into both runs. Both parts are checked before either changes, so an invalid
        /// save leaves the runs as they were and <paramref name="error"/> says why.</summary>
        public bool TryApply(PrologueRun prologue, CampaignRun campaign, out string error)
        {
            if (prologue == null) throw new ArgumentNullException(nameof(prologue));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (PrologueCleared < 0 || PrologueCleared > prologue.MissionCount)
            {
                error = $"서막 진행 {PrologueCleared}은(는) 0~{prologue.MissionCount} 범위 밖입니다.";
                return false;
            }
            if (!campaign.TryRestore(Campaign, out error)) return false;
            prologue.TryRestore(PrologueCleared);
            return true;
        }

        /// <summary>Checks this save against fresh runs without touching any live state.</summary>
        public bool Validate(out string error) => TryApply(new PrologueRun(), new CampaignRun(), out error);
    }
}
