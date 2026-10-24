using UnityEngine;

namespace TurnLimbo.Presentation
{
    public sealed partial class DuelPresentationSettings
    {
        [Header("기본 시선 — 실행 중 즉시 적용")]
        [SerializeField, Range(0f, .6f), Tooltip("편성과 일반 교전의 시선을 올리는 높이(월드 단위). 발과 하단 안내가 겹치면 필요한 만큼만 화면을 넓힙니다. 수훈·스텝·결정타의 집중 구도에는 더하지 않습니다. 0이면 이전 높이입니다.")]
        private float cameraFramingLift = .3f;

        public float CameraFramingLift => Safe(cameraFramingLift, 0f, .6f, .3f);
    }
}
