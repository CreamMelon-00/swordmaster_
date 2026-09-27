# 역할형 스킬 아이콘 제작

> 이전 9개 아이콘 제작 기록이다. 최신 검 전용15개와 고유 효과 구분은 `SwordSkillIcons.md`/`../SkillRoleLanguage.md`가 우선한다. 이 문서의 망치/창/방패 문양과 추가 기술의 그림 공유는 현재 사용하지 않는다.

내장 이미지 생성 도구를 사용했다. 생성 결과를 프로젝트 `Assets/Game/Resources/SkillRoles/skill-role-atlas.png`로 복사했고 이전 `SkillButtons` PNG는 보존한다. 새 atlas1254×1254의 3×3 셀을 Unity의 multiple Sprite로 나눈다. 원본 모서리와 셀 사이 alpha 표본은0이다. 순서는 기존 아이콘ID1~9와 같다.

베기2개→관통2개→타격2개→방어3개의 틀을 쓰고, 테두리와 색을 같이 구분한다. 새 획득 기술6종은 `IconId`1/2/2/7/4/8을 재사용하며 이름·수치·역할 텍스트를 함께 표시한다.

## 제작 프롬프트

```text
Use case: stylized-concept. Asset type: production game skill-icon atlas for a pixel-art 1v1 sword duel. Make ONE square image, exact uniform 3 columns by 3 rows, nine separate icons centered in nine equal square cells with generous plain transparent gutters. This is a new unified redesign, not a UI screenshot. Genuine transparent alpha everywhere outside each button silhouette, no checkerboard. Chunky clean pixel-art at a logical 48px icon resolution enlarged nearest-neighbor, flat limited palette, minimal details, bold readable at 48x48 pixels, no text/numbers/watermark, no fuzzy glow/shadow/background. Consistent dark navy inset and light inner pictogram. All button bounds must fit centered inside 80% of their equal cells so Unity can slice exact thirds. The OUTER FRAME must distinguish roles using SHAPE as well as color, with no enclosing common square. Slash: orange DIAMOND with clipped tips. Pierce: cyan TALL VERTICAL HEXAGON with distinct pointed top/bottom. Impact: gold SQUAT NOTCHED SQUARE, broad flat top/sides and four large corner notches. Defence: green SHIELD outline with flat shoulder top and pointed bottom. Nine icons in strict row-major sequence: top-left single diagonal curved sword slash, orange diamond; top-middle double crossed sword slash / two clear curved arcs, orange diamond; top-right single thin horizontal piercing spear/arrow, cyan tall hexagon. Middle-left rapier point passing through a simple target crosshair, cyan tall hexagon; middle-middle one stout hammer and small impact star, gold notched square; middle-right oversized hammer and three impact rays, gold notched square. Bottom-left solid upright shield and horizontal brace for block, green shield frame; bottom-middle tilted shield with one curved deflection arrow for parry, green shield frame; bottom-right two crossed short swords with shield center for counter, green shield frame. Keep contrasting silhouettes, obvious clean frames, standardized icon scale, cell centres at 1/6,3/6,5/6 of width and height. Never merge icons or draw grid lines. Transparency outside shapes, dark fill only INSIDE each frame. Avoid ornament, tiny runes, glitter, gradients, labels, puzzle pieces and keyboard letters.
```

Point 임포트는 축소 보간을 없애지만 AI 생성된 획의 모든 픽셀이 완벽한 논리 격자를 따른다고 보장하지 않는다. 실제 UI 판독을 우선한다. 게임에 넣을 때 이미지를 재편집하지 않았고 Sprite slicing으로만 나눴다.
