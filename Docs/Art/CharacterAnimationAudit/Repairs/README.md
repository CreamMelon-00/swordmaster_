# 캐릭터 픽셀 애니메이션 수정 — 2026-10-17

실행 그림 111장: Cadet A/B 각 53장, EnemyStudent 2장, SwordGirl 1장, 상급기사용 MobStudent 2장. 원 검수에서 확인한 서로 다른 102개 파일 모두 수정했다. 추가 9장은 Cadet A/B 수평 베기 05–07, SwordGirl 그립 1장, MobStudent 폼멜 타격의 머리 2장이다.

## 결과 보기

- [학생 A/B 주요 자세 수정 전후](cadet-before-after.png)
- [높은 찌르기 수정 전후 재생](cadet-pierce-2-before-after.gif): 위 A/아래 B, 왼쪽 수정 전/오른쪽 수정 후, 원래 12프레임 시간 사용. 손을 보기 위한 확대 영역이다.
- [막기·피격 실제 게임 카메라](runtime-cadet-reactions.png)
- [다리·그립 실제 게임 카메라](runtime-repaired-leg-grip.png)
- [주인공 그립 비교](swordgirl-grip-comparison.png) · [앞뒤 05–07](swordgirl05-07-after.png) · [동작 재생](swordgirl-slash-3-after.gif)
- [상급기사 시선 비교](senior-head-comparison.png) · [동작 재생](senior-pommel-after.gif)

## 현재 편집 원본

| 캐릭터 | 프로젝트 내 경로 | 반영한 셀 |
| --- | --- | ---: |
| 기본 적 | `Docs/Art/EnemyStudent/Reactions/Enemy_AllAttacks3_Reactions2.aseprite` | 2 |
| 학생 A | `Docs/Art/EnemyVariants/CadetA/CadetA-AllAnimations.aseprite` | 53 |
| 학생 B | `Docs/Art/EnemyVariants/CadetB/CadetB-AllAnimations.aseprite` | 53 |
| 주인공 | `Docs/Art/SwordGirl/BluntExpansion/SwordGirl_AllAttacks3.aseprite` | 1 |
| 상급기사 대용 | `Docs/Art/Animations/MobStudentAttacks/head-turn-v2/blunt-1/upper-2.png`, `upper-3.png` | 2 PNG 레이어 |

각 캐릭터 폴더의 기존 포즈 시트·미리보기도 갱신했다. 이전 단계 Aseprite 파일·생성 원화·과거 게임 캡처는 제작 이력이며 현재 편집 원본과 구분한다.

## 수정 자료와 재생성

- `restore_cadet_attack_foregrounds.lua`: 앞뒤 포즈와 기본 적을 기준으로 손·팔 윤곽을 구분한 Aseprite 픽셀 복원 기록.
- `restore_cadet_reaction_poses.lua` 및 8개 patch/mask: 막기·피격의 손·팔·표정과 불필요한 몸통 잔여 픽셀 수정.
- `cadet_authored_pixels.lua`: 검수한 최종 RGBA 좌표. 투명화 항목도 포함한다. `apply_cadet_authored_pixels.lua`를 통해 기존 A/B 제작 도구의 마지막 단계에서 적용된다. 새 자세에서는 이 좌표도 함께 다시 검수한다.
- `restore_enemy_front_leg.lua` 및 `enemy-slash2-front-leg.patch.png`: 기존 불투명 그림을 덮지 않는 다리 복원. 역사적 적 포즈 추출 과정을 다시 실행했을 때 이 보정도 적용한다.
- `repair_swordgirl_slash3_grip.lua` 및 `swordgirl_slash3_grip_pixels.lua`: 검을 쥔 손·커프스와 검날 수정. 기존 `SlashExpansion/build_slashes.lua`에도 연결했다. 역사적 전체 제작 도구에는 당시 외부 중간 입력 경로가 남아 있으므로 현재 편집은 위 최신 원본을 기준으로 한다.
- `repair_senior_pommel_head.lua`: 기존 오른쪽 머리 자세와 직접 수정한 목 연결을 반영한다.
- `sync_native.lua`: 실행 PNG와 최신 Aseprite 네 개의 캐릭터 셀을 동기화하고 태그 구간을 수정한다. `--script-param dry-run=true`로 비교만 할 수 있다. 저장 전후 시간·레이어·팔레트·셀·태그를 검증한다.

## 검증 기록

| 기록 | 확인 내용 |
| --- | --- |
| [repair-validation.json](repair-validation.json) | 실행 PNG 111개 경로·해시·크기, 최초 102개 파손 수정, 임포터 메타 변경 없음 |
| [cadet-pixel-repairs.json](cadet-pixel-repairs.json) | 학생 106장 변경 전후 해시와 실제 변경 픽셀 수, 재생성 시 기존 색 보존 좌표 |
| [native-sync-validation.json](native-sync-validation.json) | 원본 4개/482프레임 저장·재오픈 검증, 수정 셀 109개, 태그 30개. 재검사 차이 0 |
| [generator-validation.json](generator-validation.json) | A/B 격리 재생성 242장 전 픽셀 일치, 태그별 내보내기 장수 |
| [unity-validation.json](unity-validation.json) | Unity 6000.5.9f1, 24/24 통과, 원본·복사본 551개 해시 일치, 카메라 촬영, 검사 한계 |
| [senior-preview-validation.json](senior-preview-validation.json) | 상급기사 시트·GIF 수정, 기존 프레임 시간·다른 동작·수정 영역 밖 픽셀 보존 |
| [cadet-contact-validation.json](cadet-contact-validation.json) | 학생 포즈 시트의 기존 크기·배치와 현재 PNG 일치 |

Unity 검사는 기존 22개와 격리 복사본 전용 2개를 사용했다. 484장의 임포트·피벗과 수정된 셀의 실제 카메라 렌더링을 확인했다. 플레이어 빌드·전투 전체 수동 검사·다른 기능의 전체 검사는 실행하지 않았다. 256×224 그림 109장은 이진 알파를 유지하며, 구형 1024×768 그림 2장의 기존 반투명 가장자리는 유지했다.
