# 원경 두 층 분리

2026-09-27. 기존 `Resources/ForestArena/forest-far.png`를 내장 이미지 편집 도구로 두 평면으로 분리했다. 원본 PNG/meta는 수정하지 않고 보존한다. 기존 중경 숲길·근경 식물과 캐릭터/카메라는 변경하지 않는다.

## 자산과 연결

| 자산 | 용도 | 반복 이동 비율 / 정렬 | 기본 높이 / Y |
|---|---|---|---|
| `Assets/Game/Resources/ForestArena/forest-far-mist.png` | 불투명한 안개·먼 숲, 가장 뒤의 화면 덮개 | 0.10 / -7 | 14.4 / -0.5 |
| `Assets/Game/Resources/ForestArena/forest-far-trees.png` | 투명 배경 위 가까운 원경 나무·수관·덤불 | 0.24 / -6 | 14.4 / -0.5 |

두 PNG는2172×724(3:1), Point/압축 없음/PPU128/FullRect Sprite다. 안개 PNG는RGB이며 완전히 불투명하다. 나무 PNG는RGBA이며12픽셀 간격11041개 alpha 표본 중5413개가 완전 투명,46개가 완전 불투명이다. 나머지 나무 픽셀은 대부분 거의 불투명한 alpha이며 모든 나무 픽셀이255인 결과는 아니다. 아래 여백과 나무 사이에 실제 alpha0을 유지한다. 생성된 검정/체커보드 그림을 투명으로 간주하지 않았다.

기존 원경의 `Far Height / Far Y` 튜닝값은 나무 층이 이어받고, 새 `Far Mist Height / Far Mist Y`를 따로 추가했다. 중경1.0/근경1.35 이동과 크기/Y는 유지했다. 기존 절대 타일 인덱스의 교대 좌우 반전과 재사용 풀을 그대로 사용한다. 이동 비율은 고정이며 이번에는 크기/높이를 계속 Inspector에서 조절할 수 있게 했다.

기존 풍경을 다시 구성한 편집이므로 완벽한 원본 픽셀별 분리나 고정 도트 격자를 보장하지 않는다. 그림이 아니라 두 평면의 투명 합성과 실제 게임 화면으로 결과를 확인한다. 지나치게 작은 안개 높이는 빈 배경을 드러낼 수 있으며 자동 확대하지 않는다.

## 사용한 내장 편집 프롬프트

모드: 내장 `image_gen`, 기존 로컬 원경을 먼저 확인한 후 `referenced_image_paths`로 지정. CLI/API 우회나 별도 수동 이미지 가공을 사용하지 않았다.

### 안개 층

Use case: precise-object-edit. Asset type: opaque farthest parallax background for an existing side-scrolling pixel-art forest duel. Input image 1 is the EDIT TARGET, the existing wide green forest art. Separate out its deepest misty forest plane: remove the large dark closer trunks, nearer dark canopy, near bushes, and the visible foreground path/grass. Fill their areas seamlessly with only pale desaturated green mist, small distant faint tree silhouettes and distant canopy. Preserve the original green palette, same level horizontal view, original 3:1 panoramic aspect, recognizable blocky pixel clusters (not painterly), horizon about 65 percent down, subdued contrast. This is the BACK plane, fully opaque edge-to-edge with no alpha, no close trees or floor/path focal details. Top and bottom continue the distant atmosphere so it covers the entire camera backdrop. The image must horizontally repeat gracefully; no center focal landmark. No text, people, characters, framing, grid or watermark. Produce only one clean panoramic background plane.

### 나무 층

Use case: background-extraction. Asset type: transparent nearer-far-tree plane for an existing pixel-art forest parallax background. Input image1 is the EDIT TARGET. Extract ONLY the closer dark green tree trunks, their connected leafy crowns/canopy, and a narrow fringe of dark bushes at their bases from the original forest panorama. Remove ALL pale sky, all mist, every faint distant tree, and all ground/path/foreground grass. Every space between trunks and canopy holes must be genuinely transparent alpha, not painted light green, white, black, or a checkerboard. Bottom area below the bush bases must also be transparent. Keep the original recognisable tree placements, trunk thickness, canopy proportions, crisp square pixel clusters and muted teal/olive green palette. This is NOT a finished scene; only one irregular dark tree/canopy cutout layer floating on true transparent background. Use the SAME panoramic 3:1 canvas dimensions and tree scale as input (roughly2172x724), preserve the full-width framing and horizon alignment; don't center an isolated tree, don't add a floor, don't redesign the forest or blur/antialias pixels. Edges suitable for horizontally repeated forest. No text, characters, UI, border, watermark, shadow backdrop or faux transparency.

생성 원본은 Codex 기본 generated_images 폴더에 남기고, 위 프로젝트 자산 경로에 복사하여 연결했다. 실제 검증 범위·화면·알려진 Editor 경고는 `../Validation.md`를 따른다.

