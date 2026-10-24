using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPresentationSettings
    {
        [Header("재접근 속도선 — 실행 중 즉시 적용")]
        [SerializeField, Tooltip("첫 충돌 이후 벌어진 간격을 빠르게 좁힐 때만 배경에 짧은 속도선을 표시합니다.")]
        private bool reengageSpeedLinesEnabled = true;
        [SerializeField, Range(0f, 1f), Tooltip("재접근 속도선의 진하기. 0이면 표시하지 않습니다.")]
        private float reengageSpeedLinesStrength = .65f;

        public bool ReengageSpeedLinesEnabled => reengageSpeedLinesEnabled;
        public float ReengageSpeedLinesStrength => Safe(reengageSpeedLinesStrength, 0f, 1f, .65f);
    }
}
