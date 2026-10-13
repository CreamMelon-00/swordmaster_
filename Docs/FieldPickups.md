# 필드 습득물 (임시 테스트)

2026-10-13. **임시 기능이다.** 학원 필드에 탐색의 재미를 줄 상호작용 요소를 넣기 전에, "필드에 기술이 떨어져 있고 다가가 상호작용하면 얻는다"는 구조만 시험한다. 그림은 기존 기술 아이콘과 코드로 만든 빛으로 대신했고, 진짜 상호작용 요소가 생기면 바꾸거나 지운다. 코드에서는 `TEMPORARY field pickup test` 주석이 붙은 부분이 이 기능이다.

## 게임에서

- 놓인 곳: 지금은 네 개 모두 **바깥 숲** 길에 있다(복도 오른쪽 문으로 나간다). 복도에는 없다.
- 모양: 기술의 아이콘(시트의 `그림` 칸)이 바닥 위로 떠서 천천히 오르내리고, 옅은 놋쇠빛 빛무리와 바닥의 빛 웅덩이, 작은 그림자가 받친다. 놋쇠색 그대로는 숲 길과 같은 색이라 묻히므로 같은 색조를 밝게 썼다. 위에 기술 이름이 뜬다. 가장 가까운 습득물은 빛이 조금 더 밝다.
- 줍기: 필드의 다른 상호작용과 같다. 가까이(2.8 이내) 가면 아래 안내가 `Space · 꿰뚫기 습득`이 되고, **Space** 또는 아이콘 **클릭**으로 줍는다. 새 키는 없다.
- 주우면
  - 기술을 보유한다. 자동으로 편성되지 않는다(커리큘럼 기술과 같다).
  - 습득물은 빛을 내며 떠올라 사라지고, 전투에서 기술을 큐에 넣을 때 나는 효과음(`LegacyDuel/Audio/add_skill_1~3`, 전투는 셋 중 무작위, 습득은 기술의 열 번호)이 난다.
  - 오른쪽 위 안내에 `새 기술 · 꿰뚫기(W 강공)` / `Esc 메뉴의 편성에서 장착할 수 있습니다.`가 뜬다. 이름 뒤 괄호는 결과 창의 커리큘럼 기술 표기와 같다.
  - 그 기술의 열이 아직 닫혀 있으면 열 이름을 말하지 않는다: `새 기술 · 꿰뚫기` / `이 기술의 열이 열리면 편성할 수 있습니다.`(`StoryUnlocks.md`의 '잠긴 것은 이름도 자리도 보여 주지 않는다'). 서막을 마친 직후에는 Q열만 열려 있으므로, W·E 기술인 지금의 네 개는 그때 모두 이 문구다.
  - 이미 보유한 기술이면(정상 진행에서는 생기지 않는다) 습득물만 사라지고 `꿰뚫기 · 이미 익힌 기술입니다.`가 뜬다.
  - 타이틀의 이어하기·새 게임으로 시작한 게임이면 바로 자동 저장한다(`SaveSystem.md`의 `field-pickups`).
- 주운 습득물은 다시 나타나지 않는다. 이어하기로 불러와도 그 자리에 없다. 여정 초기화·새 게임이면 다시 놓인다.

## 습득물 넣기·옮기기

1. 기술: 시트(`Resources/Skills/skills.csv`)에 `구분` `획득` 행이 있어야 한다(`SkillSheet.md`). `시작`·`적` 행은 시트 검사가 거부한다.
2. 자리: `Assets/Game/Runtime/Campaign/CampaignFieldPickups.cs`의 `pickups` 배열에 한 줄을 넣거나 숫자를 고친다.

   ```csharp
   new CampaignFieldPickup("forest-pierce", 47, CampaignFieldArea.Forest, -14f, -1.2f),
   ```

   | 칸 | 뜻 |
   |---|---|
   | 습득물 ID | 저장에 남는 이름. 소문자 단어를 `-`로 잇고, **한 번 내보내면 바꾸지 않는다**(바꾸면 그 ID를 저장한 게임을 불러오지 못한다). |
   | 기술 ID | 줄 기술. 다른 습득물·커리큘럼 과정·스테이지 보상과 겹치지 않게 한다. |
   | 맵 | `Corridor`(복도) 또는 `Forest`(바깥 숲). |
   | x, y | 엘리사가 서는 위치와 같은 기준(상호작용 거리도 이 점에서 잰다). 아이콘은 이 점의 발밑에서 1.3 위에 뜬다. |

   - x: 복도 약 -33.6~33.6, 숲 약 -33.6~28.3(오른쪽 철책 앞). y: 걷는 띠 -2.3(앞)~-0.55(뒤).
   - 문 앞 도착 지점에서 2.8 넘게 떨어뜨린다. 숲 문 앞 도착은 x 약 -26.8, 복도 문 앞 도착은 x 약 26.4다.
   - 복도는 x -26(게시판), -21(허수아비), -14~16(전투 상대, 매번 다시 섞임) 근처가 붐빈다. 복도에 둔다면 x 17~23쯤이 비어 있다.
   - 숲의 앞쪽 덤불(`forest-near`)은 모든 것 위에 그려지므로, y를 걷는 띠 밖으로 빼지 않는다.
3. 같은 파일의 순서는 맵, 그다음 왼쪽부터다.
4. 기술을 바꾸거나 새로 넣었으면 `SkillSheet.md`의 '필드 습득물' 표도 고친다.

습득물을 지우면 그 ID를 저장한 게임은 이어할 수 없다(모르는 습득물이라 거부된다). 임시 기능을 걷어낼 때는 저장 호환을 따로 정해야 한다.

## 코드

- Runtime `Campaign/CampaignFieldPickups.cs`: 습득물 목록(`All`, `In(area)`, `Find`, `FindGranting`). 가져가기·저장 규칙은 `CampaignRun.TryCollectFieldPickup`, `IsFieldPickupCollected`, `CanCollectFieldPickup`(로비·전투 사이에만)과 `CampaignSave.FieldPickups`.
- Presentation `AcademyFieldView`
  - 생성자의 마지막 인자 `onPickup`(습득물 ID를 넘긴다)과 `TargetKind.Pickup`. 습득물은 다른 대상과 같은 가장 가까운 대상 찾기·클릭 판정·이름표를 쓴다.
  - 맵에 보이는 것은 현재 맵의 습득물 중 런이 아직 줍지 않은 것뿐이다(`RefreshPickups`, 매 프레임과 `Show`·`Resume`·맵 전환 때).
  - 정렬은 걷는 사람들과 같은 `SortAt(y)`이고, 빛무리는 아이콘보다 한 칸 아래다. 빛 웅덩이는 그림자(5) 바로 위인 6, 떠오르는 효과는 60이다. 숲 앞쪽 덤불(100)은 그대로 모든 것 위에 있다.
  - 빛은 코드로 만든 64px 원형 그라데이션 하나를 옅은 놋쇠빛(`PickupLight`)으로 칠해 쓴다(`CreateGlowSprite`, `Dispose`에서 지운다). 그림자는 배우들과 같은 `LegacyArena/Shadow/Circle`.
  - 시험용 공개 값: `VisiblePickupCount`, `IsPickupVisible`, `PickupPosition`, `InteractionPrompt`, `ArrivalNotice`. 안내 문구는 `PickupNotice(skill, newlyOwned, laneOpen)`.
- Presentation `DuelPrototypeController.CollectFieldPickup(id)`: 필드가 입력을 받을 때만(메뉴·문 전환 중 아님) 런에 넘기고, 자동 저장하고, 효과음과 안내를 낸다.
- 테스트(PlayMode, 이 환경에서는 컴파일만 확인했다)
  - `AcademyFieldViewPlayModeTests.ForestPickups_FloatWithTheirIcons_PromptNearby_TakeOnce_AndStayGoneAfterReload`: 숲에 네 개가 아이콘으로 떠 있고, 가까이 가면 안내가 뜨고, Space로 한 번만 주우며, 저장 문자열로 다시 불러온 런에서는 보이지 않는다.
  - `AcademyFieldViewPlayModeTests.PickupNotice_NamesTheLaneOnlyWhileItIsOpen`.
  - `TitleSavePlayModeTests.FieldPickup_IsSavedAtOnce_KeepsAClosedLaneUnnamed_AndStaysTakenAfterContinue`: 서막 직후(Q열만) 이어하기에서 주우면 바로 저장되고, 닫힌 열을 말하지 않고, 메뉴가 열려 있으면 줍지 않고, 다시 이어해도 주운 상태다.
  - EditMode `CampaignFieldPickupTests`: 런의 가져가기·저장·초기화 규칙.

## 알려진 한계

- Unity를 실행할 수 없는 환경에서 만들었다. 아이콘 크기(1.1), 떠 있는 높이(1.3), 빛의 밝기는 원화 위에 합성한 모형으로만 맞췄고 실제 화면으로 확인하지 않았다(`AcademyFieldView`의 `Pickup…` 상수).
- 필드 위치와 맵은 저장하지 않으므로, 숲에서 주운 뒤 이어하기를 하면 복도 입구에서 시작한다.
