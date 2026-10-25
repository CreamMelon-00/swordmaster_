# 플레이어 피격 정지 자세 3종

- 기존 `hurt/frame-00.png` 유지, PixelLab에서 새로 만든 `frame-01.png`, `frame-02.png` 추가.
- 기존 Idle/피격을 외형 참고로 사용. 앞머리로 눈을 가리고, 원본 피격의 34색 팔레트에 PixelLab map_colors로 보정.
- 256×224, PPU 36 유지. 자세별 발 중심 pivot으로 접지 높이를 맞춤. 이미지 확대/축소나 신체 파츠 변형 없음.
- 실제 피격 반응 이벤트마다 3개 중 하나를 독립 랜덤 선택. 같은 자세가 연속 선택될 수 있으며, 반응 도중 다시 뽑지 않음.
- 저항도 붕괴 시 기존 붕괴 자세가 우선. 가드 및 적 캐릭터의 선택 규칙 유지.
- `unity-three-poses.png`: Unity 스프라이트와 실제 머티리얼로 렌더링한 비교. 왼쪽부터 기존 / 추가 1 / 추가 2.
- `generation.json`: PixelLab 생성 작업 및 RGB 보정 기록. `import.json`: 원본 PNG 해시와 가져오기 기준.
- 원본 추가 리소스: `C:/Users/user/Documents/SwordMasterStory/Aseprite_Character/Player_Final_AnimationCanvas_20261023/Hurt_Variants_PixelLab_20261025`.

검증: 관련 PlayMode 25개 통과. 타격별 1회 선택, 반응 중 자세 고정, 붕괴 자세 우선, 로컬 대전 및 기존 공격 연결 검사 포함. 검사 중 공격 카탈로그의 오래된 기대 개수 42를 현행 46으로 갱신.

`Tools/validate_hurt_variants.py`로 기존 124개 PNG의 HEAD 대비 바이트 동일성, 추가 PNG의 정확한 팔레트, 생성 원본 대비 알파 보존을 확인. 수동 전투 플레이 결과와 구분하여 자동 PlayMode 및 렌더 검사 결과를 기록함.
