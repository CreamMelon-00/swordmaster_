using TurnLimbo.Runtime.LegacyCombat;
using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPresentationSettings
    {
        [Header("연타 끝타 강조 — 다음 타격부터 적용")]
        [SerializeField, Tooltip("연타의 중간 타격은 검광·충돌음·카메라 흔들림을 절제하고 끝타는 기존 세기로 남깁니다. 붕괴·큰 체력 피해·마무리 일격은 중간 타격이어도 기존 강조를 유지합니다. 피해·타격 시간·횟수는 바뀌지 않습니다.")]
        private bool multiHitEmphasisEnabled = true;
        [SerializeField, Range(.35f, 1f), Tooltip("연타 중간 타격의 연출 세기. 1이면 끝타와 같으며, 단타와 결정적인 타격에는 적용하지 않습니다.")]
        private float multiHitMiddleStrength = LegacyMultiHitEmphasis.DefaultMiddleStrength;

        public bool MultiHitEmphasisEnabled => multiHitEmphasisEnabled;
        public float MultiHitMiddleStrength => Safe(multiHitMiddleStrength, .35f, 1f,
            LegacyMultiHitEmphasis.DefaultMiddleStrength);
    }
}
