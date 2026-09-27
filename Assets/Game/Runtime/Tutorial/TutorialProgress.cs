namespace TurnLimbo.Runtime.Tutorial
{
    public enum TutorialStep
    {
        Welcome,
        QueueAttack,
        QueueFollowup,
        QueueGuard,
        InspectEnemy,
        CommitQueue,
        WatchClash,
        TurnRecovery,
        FreeBattle,
        Complete,
    }

    /// <summary>Guidance follows successful player actions; combat stays authoritative in LegacyQueuedDuel.</summary>
    public sealed class TutorialProgress
    {
        public TutorialStep Step { get; private set; } = TutorialStep.Welcome;
        public int StepCount => 8;
        public bool CanAdvance => Step == TutorialStep.Welcome || Step == TutorialStep.TurnRecovery;
        public bool AllowsCommit => Step == TutorialStep.CommitQueue || Step == TutorialStep.FreeBattle;

        public int StepNumber
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.Welcome: return 1;
                    case TutorialStep.QueueAttack: return 2;
                    case TutorialStep.QueueFollowup: return 3;
                    case TutorialStep.QueueGuard: return 4;
                    case TutorialStep.InspectEnemy: return 5;
                    case TutorialStep.CommitQueue: return 6;
                    case TutorialStep.WatchClash: return 7;
                    default: return 8;
                }
            }
        }

        public int ExpectedLane => Step == TutorialStep.QueueAttack || Step == TutorialStep.QueueGuard ? 0
            : Step == TutorialStep.QueueFollowup ? 1 : -1;

        public string Title
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.Welcome: return "첫 결투에 오신 것을 환영합니다";
                    case TutorialStep.QueueAttack: return "공격을 예약해보세요";
                    case TutorialStep.QueueFollowup: return "다음 순서에 기술을 더하세요";
                    case TutorialStep.QueueGuard: return "돌아온 기술열에서 방어를 고르세요";
                    case TutorialStep.InspectEnemy: return "상대의 기술을 살펴보세요";
                    case TutorialStep.CommitQueue: return "준비한 순서를 확정하세요";
                    case TutorialStep.WatchClash: return "두 기술 큐가 맞붙습니다";
                    case TutorialStep.TurnRecovery: return "다음 턴의 ACT가 회복됐습니다";
                    case TutorialStep.FreeBattle: return "이제 직접 결투해보세요";
                    default: return "튜토리얼을 마쳤습니다";
                }
            }
        }

        public string Description
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.Welcome:
                        return "기술을 즉시 쓰는 대신, 먼저 순서대로 큐에 예약합니다. 연습 중에는 편성 제한 시간이 흐르지 않습니다. 결과와 진행은 본 여정에 영향을 주지 않습니다.";
                    case TutorialStep.QueueAttack:
                        return "Q를 짧게 누르거나 베기 카드를 클릭하세요. ACT 1을 쓰고 첫 순서에 예약합니다. 아직 공격은 시작되지 않습니다.";
                    case TutorialStep.QueueFollowup:
                        return "W로 찌르기를 예약하세요. 머리 위 큐에 두 번째로 들어갑니다. 찌르기는 자기 다음 슬롯의 기술 위력을 강화합니다.";
                    case TutorialStep.QueueGuard:
                        return "예약한 기술은 해당 기술열의 맨 뒤로 돌아갑니다. Q의 다음 기술인 막기를 예약하세요. 같은 세 번째 순번의 상대 내려치기를 받아낼 준비입니다.";
                    case TutorialStep.InspectEnemy:
                        return "상대 머리 위에도 이번 턴의 기술 큐가 보입니다. Tab을 누르고 있으면 상세 설명이 열립니다. 좌우 방향키로 순번을 바꿀 수 있습니다.";
                    case TutorialStep.CommitQueue:
                        return "ACT 3을 모두 사용했습니다. Space 또는 Enter, 확정 버튼으로 전투를 시작하세요. 큐는 앞에서부터 같은 순번끼리 대결하며, 확정 후에는 바꿀 수 없습니다.";
                    case TutorialStep.WatchClash:
                        return "공격끼리 대결하면 먼저 저항이 줄어듭니다. 상대가 방어하거나 같은 순번에 기술이 없으면 체력 피해를 줍니다. 방어는 자신의 위력만큼 피해를 줄입니다.";
                    case TutorialStep.TurnRecovery:
                        return "ACT가 6으로 회복됐습니다: 기본 3 + 베기의 회복 1 + 내려치기를 막은 막기의 조건부 회복 2. 남은 ACT도 다음 턴에 이월되며 최대 10까지 모입니다.";
                    case TutorialStep.FreeBattle:
                        return "Q / W / E로 원하는 기술을 예약하고 Space로 확정하세요. 저항이 무너지면 체력 피해가 2배가 되며, 다음 한 턴이 지난 뒤 저항이 회복됩니다. L로 전투 기록, Shift를 누르면 재생 중 느리게 볼 수 있습니다.";
                    default:
                        return "큐와 ACT, 기술열 회전, 방어와 저항을 직접 경험했습니다. 로비에서 편성과 상점을 둘러본 뒤 첫 스테이지에 도전해보세요.";
                }
            }
        }

        public string InputHint
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.Welcome: return "시작 버튼 · Escape로 로비";
                    case TutorialStep.QueueAttack: return "Q 짧게 누르기 / 베기 카드 클릭";
                    case TutorialStep.QueueFollowup: return "W 짧게 누르기 / 찌르기 카드 클릭";
                    case TutorialStep.QueueGuard: return "Q 짧게 누르기 / 막기 카드 클릭";
                    case TutorialStep.InspectEnemy: return "Tab 누르고 있기 · ← / → 순번 확인";
                    case TutorialStep.CommitQueue: return "Space / Enter / 확정 버튼";
                    case TutorialStep.WatchClash: return "전투를 지켜보세요 · Shift로 느리게 보기";
                    case TutorialStep.TurnRecovery: return "계속 버튼";
                    case TutorialStep.FreeBattle: return "Q / W / E 예약 · Space 확정 · L 기록 · Escape 로비";
                    default: return "결과 창에서 로비로 돌아가세요";
                }
            }
        }

        public bool AllowsQueue(int lane)
            => lane >= 0 && lane <= 2 && (Step == TutorialStep.FreeBattle || ExpectedLane == lane);

        public bool TryAdvance()
        {
            if (Step == TutorialStep.Welcome) Step = TutorialStep.QueueAttack;
            else if (Step == TutorialStep.TurnRecovery) Step = TutorialStep.FreeBattle;
            else return false;
            return true;
        }

        public void NotifyQueued(int lane)
        {
            if (!AllowsQueue(lane)) return;
            switch (Step)
            {
                case TutorialStep.QueueAttack: Step = TutorialStep.QueueFollowup; break;
                case TutorialStep.QueueFollowup: Step = TutorialStep.QueueGuard; break;
                case TutorialStep.QueueGuard: Step = TutorialStep.InspectEnemy; break;
            }
        }

        public void NotifyInspected()
        {
            if (Step == TutorialStep.InspectEnemy) Step = TutorialStep.CommitQueue;
        }

        public void NotifyCommitted()
        {
            if (Step == TutorialStep.CommitQueue) Step = TutorialStep.WatchClash;
        }

        public void NotifyTurnBegan(int round)
        {
            if (Step == TutorialStep.WatchClash && round > 1) Step = TutorialStep.TurnRecovery;
        }

        public void Finish() => Step = TutorialStep.Complete;
    }
}
