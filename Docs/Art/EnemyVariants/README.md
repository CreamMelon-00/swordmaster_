# 학교 적 외형과 동작

## 화면

`Concepts/corridor-variants-preview.png`의 왼쪽은 현재 이아, 가운데는 일반 학생 A, 오른쪽은 일반 학생 B다. 복도 합성본 위에 세 도트를 같은 발선과 정수 2배율로 배치한 디자인 비교 화면이며 Unity 실행 캡처는 아니다.

- **이아**: 현재 사용 중인 붉은 갈색 머리·초록 리본 외형. 서막 2~4에서 동일 인물로 드러나므로 고유 표식으로 남긴다.
- **일반 학생 A**: 짧은 흑발, 황동 머리핀, 청회색 스카프, 기존 교복 계열의 치마.
- **일반 학생 B**: 밝은 갈색의 짧은 머리, 올리브 칼라와 견장, 바지 제복.

`Concepts/cadet-a-generated.png`와 `cadet-b-generated.png`는 내장 이미지 생성 도구로 만든 원본 시안이다. Aseprite 스크립트 `Tools/build_concept_preview.lua`가 이를 기존 적 스프라이트와 같은 256×224 캔버스와 발선에 맞춰 축소하고, 색상을 각각 80색·이진 투명도로 제한한다. `*-idle-concept-256x224.png`와 `*-idle-concept.aseprite`는 이 단계의 편집 가능한 대기 자세 시안이다. `Tools/inspect_concepts.lua`는 크기·색 수·투명도·발선과 Aseprite 원본/PNG 일치를 검증한다.

## 적용

두 학생 모두 `Assets/Game/Resources/EnemyVariants`에 기존 적과 같은 121장의 전투 프레임을 둔다. 대기 8장, 이동 1장, 방어·피격 4장, 베기·찌르기·타격의 각 3변주 12장씩이다. 제작본과 재생 가능한 Aseprite 원본, 동작 비교 이미지는 `CadetA` 및 `CadetB` 문서를 참조한다. 각 PNG의 Unity 임포트 설정은 기존 적과 같은 256×224, 단일 스프라이트, 40 PPU, Point 필터, 고정 발선 피벗을 따른다.

`applied-variants-preview.png`는 실제 게임 PNG로 만든 비교 이미지다. 위가 A, 아래가 B이며 왼쪽부터 대기·이동·베기 한 프레임이다. `Tools/build_applied_preview.lua`로 재생성한다.

학교 캠페인은 스테이지 번호가 홀수일 때 A, 짝수일 때 B가 전투와 시작 카드, 로비 미리보기에 동일하게 나온다. 서막 2~4의 이아와 컷신은 기존 외형을 유지한다. 두 학생의 전투 규칙과 스킬은 그대로이며 외형·동작만 구분한다.

## 제작 프롬프트

내장 `image_gen`의 기존 대기 프레임 참조 편집을 두 번 사용했다. 두 요청 모두 `Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png`를 양식·크기·방향·도트 밀도 참조로 전달하고 투명 배경을 요청했다.

### 일반 학생 A

```text
Use case: style-transfer. Asset type: CONCEPT SPRITE for a 2D pixel-art sword-duel game, first of two new ordinary school opponents. Input image is the existing 256x224 left-facing enemy idle sprite and is a STYLE, SCALE, POSE, and PIXEL-CLUSTER reference, not the character identity to copy. Draw one new full-body school cadet in the same left-facing combat-ready standing pose, same approximate head/body proportions and planted foot baseline, holding a slim straight sword angled to the lower right. Distinct identity: short charcoal-black bob hair tucked behind the visible ear, no large ribbon, modest narrow brass hair pin; serious expression. Clothing: restrained 19th-century fencing-school uniform related to the reference's dark brown jacket and pleated skirt, with muted slate-blue neck scarf and narrow pewter trim. Keep the design simple and recognizable at actual game size. Pixel art with crisp manually clustered square pixels, opaque pixels and clean edges, same warm outlines and limited shading as reference. Canvas: exactly one character, generous transparent margins, no scenery, no floor, no shadow, no labels, no text, no extra poses, no sprite sheet, no anti-aliasing, no soft gradients, no 3D render. This is a VISUAL DESIGN PROPOSAL; maintain an original silhouette rather than merely recoloring the reference.
```

### 일반 학생 B

```text
Use case: style-transfer. Asset type: CONCEPT SPRITE for a 2D pixel-art sword-duel game, second ordinary school opponent. Input image is the existing left-facing 256x224 female enemy idle sprite and is a STYLE, SCALE, FOOT-BASELINE, and PIXEL-CLUSTER reference; create a genuinely different character rather than a recolor. Draw exactly one full-body male fencing cadet, facing left in a grounded, slightly crouched 19th-century sword-ready pose. Distinct silhouette: short swept-back sandy brown hair, no ribbon, a cropped dark walnut uniform jacket with muted olive collar and one subtle brass shoulder tab, charcoal fitted trousers and knee-high brown boots. He holds one slim school practice sword aimed down toward the lower right, similar length to reference. Youthful face and proportions matching the game's current chibi-like character sprites. Crisp manually clustered pixel art, limited warm brown/olive/steel palette, hard square edges, clean opaque pixels, no painterly blur. Transparent background with generous blank margin, no scene, no floor, no shadow, no extra people, no labels or text, no sprite sheet, no extra pose, no antialiasing, no soft gradient, no 3D render. This is a VISUAL DESIGN PROPOSAL that will guide later native Aseprite sprite production.
```
