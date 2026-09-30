# 19세기 검술 클럽 비주얼

2026-09-27. 현대적인 푸른 방/청록 대시보드/네온 아이콘을 **검술 클럽의 장부와 금속 배지**라는 한 가지 재질 언어로 통일한다. 세계관은 약한 스팀펑크다. 산업 장비와 톱니 장식을 화면마다 붙이지 않고 가스등·황동 배관·압력계 정도로 시대를 암시한다.

## 디자인 계약

- 짙은 호두나무/먹색 `#29231c`, 가죽 카드 `#30291f`, 선택 `#51412b`, 황동 테두리 `#957449`, 황동 강조 `#c8a365`.
- 본문 `#e9debf`, 보조 `#b7aa8e`. 선택 상세는 낡은 종이 `#d8c8a4`와 잉크 `#332c23`. 상태 의미색은 차분한 녹색 HP, 강철색 저항, 적동색 위험이다.
- 브리핑·로비·편성·상점·전투·전투 결과·이전 정비 화면은 `DuelVisualTheme`의 같은 색을 사용한다. 프레임은 이미지 위에 페인팅한 버튼이 아니라 코드 기반의 얇은 황동 테두리/모서리 핀이다. 비차단 단일 uGUI mesh이고 메인 Image의 색·Sprite·치수와 기존 버튼/drag/drop 이벤트는 바꾸지 않는다.
- 스킬의 검 전용 문양, 베기 마름모/찌르기 육각형/타격 홈 사각형/방어 방패 구분을 유지한다. 구리·강철·황동·올리브 법랑으로 채도를 낮춘다. ACT 순환 표식은 실제 회복 효과가 있는1/7에만 남긴다. 추가14/17/32에 없는 유틸 효과를 약속하지 않는다. 효과 의미는 `../SkillRoleLanguage.md`를 따른다.
- 캐릭터/클립/파티클과 네 배경의 크기·Y·패럴랙스/튜닝은 그대로다. 숲길의 앞뒤 폭과 비어 있는 중앙을 유지하고 뒤쪽에 작은 가스등/철제 난간을 추가한다. 원경 안개·나무와 전경 식물은 재사용한다.
- 기존 도트 폰트/한국어, 현재 레이아웃과 노드 이름·공개 API·클릭 설명 선택/드래그 배치/각 열3개 저장/명시적 상점 거래는 유지한다. 서체 교체나 새로운 세계관 대사/캐릭터 디자인은 이번 범위가 아니다.

## 적용 자산과 보존

내장 이미지 생성 도구의 **기존 그림 편집**으로5종을 제작했다. 생성 PNG를 아래 프로젝트 경로로 그대로 복사했으며 후처리로 그림/alpha를 재작성하지 않았다.

- `Assets/Game/Resources/LobbyRoom/room.png`:1672×941, 불투명. 가스등이 켜진 검술 클럽 준비실. 기존 GUID/Resource 경로/room Sprite 이름과 EnvelopeParent 배치를 유지한다.
- `Assets/Game/Resources/SkillRoles/skill-role-atlas.png`:971×1619, 실제 투명 alpha,15개. Sprite names `role1..role15`, GUID·SpriteID·internalID·중앙pivot·Point/Clamp/무압축/무Mip 유지. 새 그림의15개 연결된 불투명 배지 경계에2px 여백을 더해 Sprite rect만 갱신했다. 균등격자로 자르면 다음 행이 섞이는 기존 구조를 피했고 현재 rect 비율은0.8~1.2 안이다.
- `Assets/Game/Resources/ForestArena/forest-belt-mid.png`:2172×724, 실제 투명 하늘 틈. 기존 GUID·Point/Clamp·Sprite 배치/3:1/크기·Y를 유지한다. 원경2층/전경은 그대로다. 좌우는 기존 절대 타일 인덱스의 교대 반전으로 이어진다.
- `Assets/Game/Resources/HudActions/combat-log.png`, `confirm-turn.png`:각1254², 불투명. 장부와 체크 봉인 배지. GUID/Resource 경로·하단L/A 힌트·64px 버튼 아이콘·기존max256 임포트는 유지하고 필터만Point로 맞췄다.

적용 전 그림5개와 각각의 원본 importer는 `Sources/Victorian-Before/`에 보존했다(`.meta.txt`는 복구 참고용이며 Unity 자산이 아니다). 이전 레거시 프로젝트를 수정하지 않았다. 제작 원본은 `C:/Users/User/.codex/generated_images/01a0dd3b-1ec7-7262-8afd-d810dd15286a/`의 아래 파일에 있다.

- room: `exec-0f2a8adc-9ef7-4a95-b3ff-c53539622e8a.png`
- skill atlas: `exec-482f66a7-640b-46b1-b661-c99cae82a3d5.png`
- forest middle: `exec-48001de3-2a55-4ad1-8ccd-e3970c9e8ae4.png`
- log: `exec-f63b7a9f-3f14-49b7-be30-8bb91e105ef8.png`
- confirm: `exec-ce54ddb4-6372-48f6-9940-be4d40d683b6.png`

## 실제 제작 프롬프트

각 입력 이미지는 위 동일 경로의 적용 전 PNG였고, 모두 개별 편집 호출이었다.

### Room

```text
Use case: style-transfer. Asset type: production pixel-art lobby background for an existing 2D sword-duel game. Image 1 is the edit target, a modern blue bedroom. Redesign the interior as a restrained late-19th-century fencing club preparation room with subtle steam-era craftsmanship; preserve wide 16:9 framing and room depth, NOT the modern furnishings. Dark walnut wainscot and rafters, worn olive plaster, a broad worktable and closed leather ledger, sword rack, two small amber gas sconces, a tall arched frosted window and discreet brass pressure pipe by the side wall. REMOVE laptop, refrigerator, fluorescent strip, modern bedroom objects and bright cyan palette. Leave the middle third uncluttered and low-contrast for the game's menu overlay; interesting room details live mostly near left/right edges. Warm parchment and antique brass highlights, soot-charcoal shadows, faded olive teal window light. Crisp low-resolution pixel-art clusters matching the input game's 16-bit sprite world; large deliberate pixels, limited 24-colour palette, no painted microtexture, no smooth gradients, no excessive glowing gears, no gear wallpaper, no pseudo writing. Fully opaque background, edge-to-edge finished game scenery. No people, no UI, no letters, no labels, no watermark. Output landscape 2400 by 1350 pixels if possible.
```

### Skill atlas

```text
Use case: style-transfer. Asset type: production transparent pixel-art skill icon atlas for a 19th-century sword duel game with understated steampunk. Image 1 is the existing edit target, precisely fifteen sword-skill icons in THREE COLUMNS and FIVE ROWS. Keep exactly 15 individually isolated badges in the exact same row-major positions and same canvas proportions, all glyphs centred safely inside their cell with generous transparent margins. Preserve each sword silhouette and functional symbol, but redesign the neon rims as engraved aged metal clasps, matte leather insets, ivory steel blades, bronze/gunmetal highlights, clear low-resolution pixel clusters. No smooth 3D shiny renders, no gradients, no glow, no fake text or modern UI. Keep category border shapes: slash diamond badges antique copper, thrust hexagonal badges patinated steel/quiet teal, impact notched square badges aged brass, defence shield badges subdued olive enamel. Enough muted category distinction, not rainbow saturation. Row 1: one curved sword with small ACT circular arrow; two crossing curved swords with one sharp strike; horizontal straight sword with small upward support chevrons. Row 2: stronger horizontal sword with point impact; sword downward impact; strong sword downward burst. Row 3: crossed blocking swords with ACT circular arrow; deflecting swords with small damage-reduction wave; parrying swords with upward support chevrons. Row 4: one horizontal slashing sword NO circular arrow; two diagonal swords/slash trails; single vertical sword with a split-force accent (variable heavy strike, NOT twin swords). Row 5: two swords held in pure guard with NO utility mark; single forceful horizontal thrust; two deflecting swords with NO damage-reduction wave or utility mark. Use swords ONLY as weapons. Preserve genuinely transparent alpha outside each badge, NOT a black or checkerboard canvas. Keep all borders separated with transparent gutters, uniform centred 3x5 cells. Output 960x1600 or input's 971x1619 proportions; no added icons, no labels.
```

### Forest middle

```text
Use case: style-transfer. Asset type: transparent middle parallax layer for a horizontally looping 2.5D belt-scroll sword-duel game. Image 1 is the exact edit target. Preserve VERY WIDE 3:1 landscape, low horizon/rear ground edge at 47% down from top, broad unobstructed walkable belt occupying lower half, camera perspective, ground depth, left/right tile joining, and truly transparent sky gaps between trees. Retain the wooded path but integrate discreet late-19th-century park details: two slender old cast-iron gas lamps behind the rear edge of the path, small low iron rail section partly lost in roots, a small aged-brass maintenance pipe close to one tree. No huge factories or towers, no characters, no modern streets. Match our palette of muted moss/olive foliage, dark walnut trunks, old stone/earth ochre ground, antique brass and tiny warm amber lamp glass. Calm clearly clustered 16-bit pixel art with deliberate large pixels, restricted palette, no AI microtexture, no photoreal/painterly blur, no gradients, no large bloom. Trees frame broad empty centre, lamp silhouettes unobtrusive behind fighters. Keep the foreground ground fully opaque and sky gaps genuinely alpha-transparent, no checkerboard or solid matte. Remove no ground depth. Boundary left/right should both remain tree/earth material for alternating-reflection looping. No text, no UI, no watermark. Output 2172x724 or equivalent exact 3:1 landscape.
```

### Combat log

```text
Use case: style-transfer. Image 1 edit target: a combat-log button icon in a 2D sword-duel game. Redesign the same simple open-book silhouette as a late-19th-century fencing club ledger: ivory parchment pages, walnut leather spine, thin aged brass clasp and corner protectors, strong readable pixel-art silhouette at 64px. Same square composition and generous margin. Replace cyan glow and futuristic square bezel with a quiet brass-rimmed dark leather plate; muted antique gold, warm paper and charcoal only. Deliberate chunky 16-bit pixel clusters, no blurred gradients, no noisy microtexture, no written letters, no gear ornaments, no modern glow. Fully opaque dark leather background, no checkerboard, no transparency needed. No text, no watermark. Square image.
```

### Confirm

```text
Use case: style-transfer. Image 1 edit target: a commit/confirm-turn button icon in a 2D sword-duel game. Preserve the bold checkmark silhouette, but render it as an ivory check pressed into a warm copper sealing-stamp medallion on a dark walnut leather plate with a thin aged-brass rim. Understated late-19th-century fencing club interface, matches a parchment ledger book icon. Same square composition and generous margin, legible at64px. Antique brass, copper wax, ivory paper and charcoal palette only. Crisp deliberate chunky 16-bit pixel clusters, no gradients, no cyan glow, no photoreal shine, no microtexture, no decorative gears. Opaque dark leather background. No letters, no UI text, no watermark. Square image.
```

## 한계

생성 그림은 요청한 화면의 재질 언어를 맞춘 것이며 손으로 그린 고정 도트 격자/정확히24색인 결과를 보장하지 않는다. 방 결과의 실제 해상도는 프롬프트의2400×1350보다 작다. 현재 배경 튜닝과19세기 분위기는 계속 직접 보며 조절할 수 있다. 정적 그림에 실제 가스등 조명/환경 상호작용이나 새로운 전투 규칙을 넣지는 않았다. 실행 검증의 정확한 근거는 `../Validation.md`의 최신 항목에 기록한다.
