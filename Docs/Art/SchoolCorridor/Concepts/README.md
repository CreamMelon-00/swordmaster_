# 노란 석조 학교 복도 시안

`yellow-stone-corridor-v2.png`은 서막 이후 학교 복도의 승인된 디자인 시안이다. 적용판은 창밖 원경을 40px, 근경을 40px 더 높인 `yellow-stone-corridor-v3.png`에서 제작했다. 이전 `v1`의 문과 넓은 바닥을 없애고, 벽이 가까운 좁은 복도·큰 창 다섯 개·노란 석재와 옅은 대리석·절제된 가스등으로 바꿨다. 창밖은 먼 하늘·교정과 가까운 수목을 따로 그려 실내에서 싸우는 깊이를 만든다. 학교 문장·종교 장식·증기 기계는 정하지 않았다. 실제 게임용 파일과 제작 규격은 `../Applied/README.md`를 참고한다.

## v2: 창밖 분리 시안

| 파일 | 역할 |
| --- | --- |
| `yellow-stone-corridor-v2.png` | 세 층을 합성한 2172×724 검토용 화면 |
| `yellow-stone-corridor-v2.aseprite` | 편집 가능한 Aseprite 원본, 먼 풍경·가까운 수목·실내 3층 |
| `Layers/exterior-far.png` | 옅은 금빛 하늘과 먼 교정의 불투명 원경 |
| `Layers/exterior-near.png` | 투명 배경의 가까운 교정 수목 |
| `Layers/interior-windows.png` | 석조 벽·창틀·가스등·바닥의 고정층, 유리 안쪽은 투명 |

`build-layered-concept.lua`는 ImageGen 시안과 투명 창 가이드를 Aseprite에서 결합해 층을 저장한다. 창틀의 색은 원래 시안에서 가져와 투명 창 가이드의 가장자리 색 번짐을 피했다. 이 원본을 다시 열어 2172×724 PNG로 내보내는 검사는 통과했다. **아직 실제 전투용 반복 경계·4×4 도트 정리·카메라 시차·캐릭터 배치는 확정하지 않았다.**

### v2 실내 구도 프롬프트

입력 이미지: `yellow-stone-corridor-v1.png`.

```text
Use case: precise-object-edit. EDIT the supplied 3:1 pixel-art side-view school corridor concept into a NEW layout for an indoor sword-fighting backdrop. Remove BOTH wooden doors completely and remove their doorways; replace those bays with the SAME large windows as the other bays. Do not show any door beside a window anywhere. Make the corridor visibly NARROWER and the rear wall CLOSER to the fighters: raise the floor-to-wall seam from about the middle to roughly 64% down the canvas, reduce the visible floor depth to the lower 36%, and enlarge the rear wall and windows so the windows dominate the upper two-thirds. Use four or five very large, evenly spaced arched windows with thick honey-yellow limestone jambs and dark walnut mullions, seen straight side-on; shallow pilasters between them. Through the glass show an exterior with clear PARALLAX DEPTH: a very pale golden sky and faint distant school-ground roofline on the furthest layer, soft blue-green distant tree silhouettes in front of that, and richer leafy green branches at a closer layer just outside the window. Keep these outside layers limited to the window openings and visibly separated by value and softness; interior stone mullions and sills overlap them in front. The exterior must feel much farther away than the nearby interior wall. Keep sunlight entering diagonally through the big windows and making a few broad yellow patches on a matte pale marble floor, but leave the central playable floor clear and quiet. Interior palette warm ivory/yellow stone, ochre and muted brass, walnut trim; exterior a subdued cool green/gold contrast. Two or three small gas sconces between windows at most; restrained late-19th-century private academy, no overt steampunk. Match existing crisp enlarged square pixel-art clusters; no doors, no hallway vanishing point, no people, no furniture, no text, no invented crest or cross, no gears, no big machinery, no photorealism, no blurred painting. Full bleed 3:1 horizontal, opaque concept preview.
```

### v2 투명 창 프롬프트

입력 이미지: `Layers/corridor-source.png`.

```text
Use case: precise-object-edit / background-extraction. Asset type: transparent FOREGROUND INTERIOR LAYER for the supplied five-window 3:1 pixel-art academy corridor scene. Keep the existing honey limestone wall, columns, arched stone surrounds, dark wood window frames and mullions, dark wood lower panels, two small wall gas lamps, sunlight on wall, and narrow pale-marble fighting floor EXACTLY in their current positions and colors. Change ONLY the contents inside every GLASS PANE of the five large windows: remove the sky, trees, distant buildings, and glass coloration completely and make those pane interiors TRUE TRANSPARENT ALPHA so independent outdoor parallax layers can be visible behind the windows. Preserve the thick dark frame outlines, vertical and horizontal mullions, arched top edges and stone sills opaque in front. Do not put checkerboard or black fill in the panes. The rest of the image must remain opaque, including the entire floor and wall. No doors, people, text, machines, newly invented ornaments, or composition changes. Crisp pixel edges. Output one full 3:1 PNG with transparency ONLY through the five window pane interiors.
```

### v2 먼 풍경 프롬프트

```text
Use case: stylized-concept. Asset type: FAR OUTDOOR PARALLAX LAYER seen only through large windows of a 19th-century stone academy corridor in a side-view 2D pixel-art duel game. Create one very wide 3:1 horizontal OPAQUE landscape strip, full bleed. Pale gold morning sky, a few quiet soft clouds, very distant low school-ground roofs and tree-covered hills along the LOWER THIRD, softly muted by warm haze. Architectural silhouettes should be small and sparse, no grand castle or church spires, no central landmark, no characters. Deliberate crisp square pixel-art clusters at the same scale as a detailed pixel-art game backdrop; restrained cream, straw yellow, pale blue-grey, muted olive. Low contrast because it is far behind the interior wall. Horizontally continuous calm panorama with no obvious seams. No windows, wall, floor, frames, gas lamps, text, UI, gears, steam or modern structures. This is only the far exterior view; it will sit behind a separate transparent tree layer and a separate window frame layer.
```

### v2 가까운 수목 프롬프트

```text
Use case: stylized-concept. Asset type: NEAR OUTDOOR PARALLAX LAYER seen through large windows of a 19th-century stone academy corridor in a side-view 2D pixel-art duel game. Create one very wide 3:1 horizontal PNG with a TRUE TRANSPARENT background. ONLY an uneven line of ordinary leafy courtyard trees and modest shrubs, scattered across the width, mainly in the LOWER HALF of the canvas; a few rounded canopies rise to around the middle, leaving open transparent gaps and ALL OF THE TOP QUARTER fully transparent. Warm yellow-green leaf highlights and muted blue-green shadow, calm dawn forest palette. Strong chunky pixel-art clusters and simple readable tree silhouettes, no vines, no tropical foliage, no jungle. This near tree layer will move separately in front of a pale distant sky/roof background and behind transparent window openings. No sky, no painted solid background, no floor, no building, no windows, no wall, no frame, no text, no characters, no checkerboard, no cast shadow beyond tree silhouettes. Full 3:1 horizontal composition, true alpha outside foliage.
```

## v1 제작 기록

내장 ImageGen에서 처음 생성한 `yellow-stone-corridor-initial.png`을 편집해 성당처럼 보이는 뾰족한 아치와 웅장한 장식을 줄였다. 승인 후 실제 전투 배경으로 제작할 때 Aseprite 픽셀 정리, 좌우 반복 경계, 레이어 분리 및 게임 연결이 필요하다.

## 생성 프롬프트

```text
Use case: stylized-concept.
Asset type: FIRST DESIGN PREVIEW for a future 2D side-view sword-duel game's academy corridor background, not yet a game asset.
Primary request: an interior corridor of a small late-19th-century stone academy, mainly WARM YELLOW in feeling: honey limestone and pale yellow marble, amber sunlight, aged brass details. Restrained historic design; only a few discreet wall-mounted gas lamps hint at the era. No obvious steampunk mechanisms.
Composition: VERY WIDE 3:1 horizontal canvas, full bleed. View the corridor SIDE-ON like a long theatrical fighting stage, with shallow depth, NOT looking down the length of a corridor. Several rhythmically spaced stone arches and pilasters extend left and right, with tall arched windows and two dark walnut classroom doors set into the BACK WALL. A continuous level floor-to-wall seam runs horizontally at about 47% of canvas height. The lower half is a broad, unobstructed pale marble walking/fighting floor with subtle ochre veining and quiet rectangular stone joints; leave its middle open for two character sprites. Keep the brightest sunlight and architectural features on the back wall and edges so sprites remain readable. Architectural rhythm should feel able to continue horizontally beyond both edges; no central monumental doorway or vanishing point.
Style/medium: polished handcrafted 2D PIXEL ART consistent with a warm dawn forest battle background: deliberately low-resolution square pixel clusters enlarged cleanly, 3-4 shade steps per material, strong silhouettes, restrained texture, no soft painterly blur, no photorealism, no 3D render. The result should be attractive as a concept but practical for eventual Aseprite pixel cleanup and parallax splitting.
Palette: creamy ivory stone, warm honey yellow, muted ochre and brass, deep walnut brown in doors, small soot-charcoal shadows. Rich yellow ambience without neon or full orange saturation. Gentle late-morning daylight enters from high windows; a few lit brass gas sconces give quiet accents.
Constraints: no characters, no people, no swords or weapon racks, no school crest or invented religious symbol, no readable signs or text, no modern lockers, fluorescent lighting, concrete, exposed gears, large boilers, pipes, machines, smoke, furniture in playable floor, user interface, frame, watermark. Opaque background.
```

## 최종 편집 프롬프트

입력 이미지: `yellow-stone-corridor-initial.png`.

```text
Use case: precise-object-edit. This is an EDIT of the supplied 3:1 side-view pixel-art academy corridor design preview. Keep the exact very wide side-on fighting-stage composition, warm honey-yellow limestone and pale marble palette, continuous horizontal wall/floor seam, broad clear walking floor, two walnut doors, gentle amber window light, and discreet gas sconces. Make the architecture feel like a MODEST late-19th-century PRIVATE SCHOOL corridor rather than a cathedral or palace: replace pointed Gothic window arches with restrained shallow round or segmental arches; shorten the towering windows slightly; simplify the upper vaulting and columns; remove hanging banners, heraldic-looking ornaments, castle/cathedral spires outside, and decorative floor reflections. Through windows show only soft yellow morning sky and indistinct leafy school grounds, no monumental townscape. Keep a believable historical stone building with simple carved moldings and dark walnut lower wall panels. Reduce the shiny palace-marble effect to matte worn pale stone with a few subtle ochre veins and flat, level joint lines. Brightest yellow light remains on the rear wall; floor stays quieter and open for character silhouettes. Deliberate crisp low-resolution pixel clusters, no photorealism, no blur, no characters, no UI, no text, no visible machinery, no steam effects. Maintain full-bleed 3:1 scene and opaque pixels.
```
