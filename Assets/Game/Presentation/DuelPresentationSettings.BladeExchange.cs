using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPresentationSettings
    {
        [Header("검 맞대기 — 다음 맞대기부터 적용")]
        [SerializeField, Tooltip("서로 교전하는 기술의 첫 타격 직전에 피해 없는 칼 맞대기를 1~2회 재생합니다. 숨고르기·일방 공격·회피·붕괴 중에는 생략합니다.")]
        private bool bladeExchangeEnabled = true;
        [SerializeField, Range(.04f, .25f), Tooltip("칼을 맞대고 거두는 한 동작의 가장 짧은 시간(실제 초). 횟수와 각 동작의 길이는 기술마다 달라집니다.")]
        private float bladeExchangeMinSeconds = LegacyBladeExchange.DefaultMinSeconds;
        [SerializeField, Range(.04f, .25f), Tooltip("칼을 맞대고 거두는 한 동작의 가장 긴 시간(실제 초). 실제 공격의 속도·피해·횟수는 바뀌지 않습니다.")]
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
