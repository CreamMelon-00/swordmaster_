using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPresentationSettings
    {
        [Header("강한 충돌 — 환경음")]
        [SerializeField, Tooltip("강한 검 충돌 때 숲 환경음을 짧게 낮춥니다. 타격음·안내음·이야기의 소리는 그대로 재생합니다.")]
        private bool clashAmbienceDuckEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("충돌 순간 줄이는 숲 환경음의 비율. 기본값은 70%를 줄여 원래 음량의 30%를 남깁니다.")]
        private float clashAmbienceDuckStrength = LegacyClashAmbienceDuck.DefaultStrength;

        public bool ClashAmbienceDuckEnabled => clashAmbienceDuckEnabled;
        public float ClashAmbienceDuckStrength => Safe(clashAmbienceDuckStrength, 0f, 1f,
            LegacyClashAmbienceDuck.DefaultStrength);
    }
}
