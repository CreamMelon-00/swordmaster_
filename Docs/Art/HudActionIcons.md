# HUD 동작 아이콘: 기록 / 확정

## 범위

기본 기술 9개와 별도로 전투 기록과 턴 확정의 버튼 PNG 2개를 제작했다.
기존 키캡형 기술 아이콘의 graphite/navy 외곽, ivory 테두리, 픽셀 문양을 공통 스타일로 사용한다.
책은 기록, 체크 표시는 편성한 큐 전체 확정을 의미한다. 재생 삼각형으로 표현하지 않는다.

| 리소스 | 문양 | UI 이름 / 하단 키 |
| --- | --- | --- |
| `Assets/Game/Resources/HudActions/combat-log.png` | 펼친 기록 책 | 기록 / L |
| `Assets/Game/Resources/HudActions/confirm-turn.png` | 팔각형 인장 위 굵은 체크 | 확정 / A |

이미지에 텍스트·단축키·ACT를 넣지 않는다. 이름과 키는 HUD Text로 표시하며 실제 입력과 일치한다.
원본 기술·큐·ACT·전투 판정 및 캐릭터 리소스는 바꾸지 않았다.

## 생성과 가져오기

- 생성 모드: 내장 이미지 생성/편집 도구 `image_gen` (CLI 아님).
- 공통 스타일 및 편집 대상: `Assets/Game/Resources/SkillButtons/skill1.png`.
- 출처: 이번 작업에서 새로 생성한 PNG. 외부 이미지 다운로드 없음.
- 원본 PNG 1254×1254. corner / center / face 표본 alpha 모두 255 확인.
- Sprite Single, 중앙 pivot, full rect, maximum 256px, Bilinear, Clamp, mipmap 없음, 압축 없음.
- 파일별 고유 GUID와 Sprite ID, 폴더 metadata 포함.
- Resources Sprite는 공유 자산으로 사용하고 HUD에서 파괴하지 않는다.
- 버튼 패널 96×112, Icon 64×64 (중심 Y=18), 이름 88×18 (Y=-23), 키 36×18 (Y=-44).
- 아이콘과 텍스트는 raycast를 받지 않는다. 부모 버튼만 입력을 받는다.

## 실제 생성 프롬프트

### 기록

```text
Use case: precise-object-edit. Image 1 is an edit target and common STYLE MASTER: one pixel-art mechanical keycap button. Keep EXACTLY its square graphite navy housing, slate upper-left bevel, dark lower-right bevel, clipped corners, thin ivory rim, size, orientation, placement and padding. Make one production UI button icon for a pixel-art sword duel. Replace ONLY the central sword and amber arc with the following new pictogram; remove all old glyph pixels. New glyph is chunky ivory with restrained pale cyan accent, bold and simple, legible at 64px. Same pixel-art shading, no fine texture or realistic rendering. Entire tile fully opaque, no transparent areas/holes. No text, letters, numbers, costs, keyboard keys, scene, watermark, brand mark. Keep centered inside the same inner glyph footprint. COMBAT LOG / HISTORY pictogram: a small opened logbook, two broad pages with exactly three thick horizontal record strokes on each page, a clear central binding line. One simple open-book silhouette. No sword, no quill, no clock or tiny letters. Pages should have ample dark gaps to read at small size.
```

### 확정

```text
Use case: precise-object-edit. Image 1 is an edit target and common STYLE MASTER: one pixel-art mechanical keycap button. Keep EXACTLY its square graphite navy housing, slate upper-left bevel, dark lower-right bevel, clipped corners, thin ivory rim, size, orientation, placement and padding. Make one production UI button icon for a pixel-art sword duel. Replace ONLY the central sword and amber arc with the following new pictogram; remove all old glyph pixels. New glyph is chunky ivory with restrained pale cyan accent, bold and simple, legible at 64px. Same pixel-art shading, no fine texture or realistic rendering. Entire tile fully opaque, no transparent areas/holes. No text, letters, numbers, costs, keyboard keys, scene, watermark, brand mark. Keep centered inside the same inner glyph footprint. CONFIRM / LOCK IN TURN pictogram: one bold CHECK MARK centered on a simple squat octagonal seal, ivory thick tick with subtle pale cyan lower-right shading. Make the check mark very obvious: rising from lower left toward upper right. No book, no sword, no arrow, no text. Do not resemble a play triangle; this commits a planned queue, it is not a playback button.
```

첫 확정 출력은 눈으로는 올바르게 보였지만 반투명 영역이 있어 채택하지 않았다.
해당 생성 이미지를 편집 대상으로 전달하여 아래 프롬프트로 불투명도만 보정한 출력을 최종 채택했다.
UI 배경이 버튼 면을 통해 비치지 않는 것이 의도다.

```text
Use case: precise-object-edit. Image 1 is the EDIT TARGET. Keep every visible detail unchanged: the check-mark/octagonal confirmation glyph, palette, exact square pixel-art keycap housing, dimensions, frame position, equal margins. Fix ONLY image transparency: make the ENTIRE canvas fully opaque with alpha 255 everywhere. Composite the existing content on a uniform very dark graphite navy #101820 backdrop. In all semi-transparent keycap face areas use the matching opaque graphite navy face, without mottling or holes. The button face and outside margin must never let a game scene show through. No new details, no text, no lettering, no glow, no watermark. Do not redesign the glyph or border.
```

## 검증

새 Sprite 로드, 아이콘/이름/키의 패널 내 배치와 비겹침, 아이콘 위치를 클릭했을 때 부모 버튼 입력,
L 기록 토글 / A 확정 / 전투 중 기록 차단을 PlayMode에서 확인한다.
실제 검증 결과와 Game View 캡처는 `../Validation.md`의 최신 기록을 따른다.

