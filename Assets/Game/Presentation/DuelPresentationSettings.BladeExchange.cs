using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPresentationSettings
    {
        [Header("검 맞대기 — 다음 맞대기부터 적용")]
        [SerializeField, Tooltip("기존 공격 준비 시간 안에서 한 번 칼을 맞대고 짧은 반동을 거쳐 본공격으로 이어집니다. 별도 정지 시간을 추가하지 않으며 숨고르기·일방 공격·성공한 회피·붕괴 중에는 생략합니다.")]
        private bool bladeExchangeEnabled = true;
        [SerializeField, Range(.04f, .25f), Tooltip("맞대기와 반동을 담는 준비 구간의 최소 길이(전투 시계 초). 첫 타격까지 남은 준비 시간 안에 맞추며, 짧은 준비 시간을 늘리지 않습니다.")]
        private float bladeExchangeMinSeconds = LegacyBladeExchange.DefaultMinSeconds;
        [SerializeField, Range(.04f, .25f), Tooltip("맞대기와 반동을 담는 준비 구간의 최대 길이(전투 시계 초). 칼은 한 번만 맞대며 실제 타격 시점·피해·횟수는 바뀌지 않습니다.")]
        private float bladeExchangeMaxSeconds = LegacyBladeExchange.DefaultMaxSeconds;
        [SerializeField, Range(0f, .6f), Tooltip("칼이 닿는 작은 청록 불꽃의 밝기 배율. 실제 타격의 광원보다 약하고, 몸체 번쩍임·밀림·화면 흔들림은 없습니다.")]
        private float bladeExchangeStrength = .28f;
        [SerializeField, Range(0f, .5f), Tooltip("가벼운 칼 충돌음의 음량. 실제 타격음보다 작게 재생합니다.")]
        private float bladeExchangeVolume = .18f;

        public bool BladeExchangeEnabled => bladeExchangeEnabled;
        public float BladeExchangeMinSeconds => Safe(bladeExchangeMinSeconds, .04f, .25f, LegacyBladeExchange.DefaultMinSeconds);
        public float BladeExchangeMaxSeconds => Mathf.Max(BladeExchangeMinSeconds,
            Safe(bladeExchangeMaxSeconds, .04f, .25f, LegacyBladeExchange.DefaultMaxSeconds));
        public float BladeExchangeStrength => Safe(bladeExchangeStrength, 0f, .6f, .28f);
        public float BladeExchangeVolume => Safe(bladeExchangeVolume, 0f, .5f, .18f);
    }
}
