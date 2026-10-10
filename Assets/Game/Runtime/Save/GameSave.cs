using System;
using TurnLimbo.Runtime.Campaign;
using TurnLimbo.Runtime.Prologue;

namespace TurnLimbo.Runtime.Save
{
    public enum OpeningVoiceChoice
    {
        Full = 0,
        Leave = 1,
        Essential = 2,
    }

    /// <summary>Everything the temporary auto-save keeps: how far the opening arc has gone and the campaign's
    /// persistent state. A battle in progress is never saved; loading resumes at a briefing or the lobby.</summary>
    public sealed class GameSave
    {
        /// <summary>Version 2 saves include curriculum progress; optional skill experience keeps older v2 files readable.</summary>
        public const int CurrentVersion = 2;

        public GameSave(int prologueCleared, CampaignSave campaign,
            OpeningVoiceChoice openingChoice = OpeningVoiceChoice.Full, bool openingCompleted = true)
        {
            PrologueCleared = prologueCleared;
            Campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
            OpeningChoice = openingChoice;
            OpeningCompleted = openingCompleted;
        }

        /// <summary>Opening-arc missions won in order.</summary>
        public int PrologueCleared { get; }
        public CampaignSave Campaign { get; }
        public OpeningVoiceChoice OpeningChoice { get; }
        public bool OpeningCompleted { get; }

        public static GameSave Capture(PrologueRun prologue, CampaignRun campaign,
            OpeningVoiceChoice openingChoice = OpeningVoiceChoice.Full, bool openingCompleted = true)
        {
            if (prologue == null) throw new ArgumentNullException(nameof(prologue));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            return new GameSave(prologue.ClearedCount, campaign.CaptureSave(), openingChoice, openingCompleted);
        }

        /// <summary>Loads this save into both runs. Both parts are checked before either changes, so an invalid
        /// save leaves the runs as they were and <paramref name="error"/> says why.</summary>
        public bool TryApply(PrologueRun prologue, CampaignRun campaign, out string error)
        {
            if (prologue == null) throw new ArgumentNullException(nameof(prologue));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!Enum.IsDefined(typeof(OpeningVoiceChoice), OpeningChoice))
            {
                error = "프롤로그 선택이 올바르지 않습니다.";
                return false;
            }
            if (PrologueCleared < 0 || PrologueCleared > prologue.MissionCount)
            {
                error = $"서막 진행 {PrologueCleared}은(는) 0~{prologue.MissionCount} 범위 밖입니다.";
                return false;
            }
            if (!OpeningCompleted && PrologueCleared != 0)
            {
                error = "오프닝이 끝나기 전에 서막 임무가 진행된 저장입니다.";
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
