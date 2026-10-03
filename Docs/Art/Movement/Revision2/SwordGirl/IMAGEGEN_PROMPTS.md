# SwordGirl 8프레임 보행 포즈 시안

내장 imagegen의 **편집 모드**를 사용했다. 생성 결과는 이 폴더의 `concept-*.png`로 보관했고, Aseprite에서 전체 크기 정렬, 원본 64색 팔레트 매핑, 알파 정리와 256×224 프레임 제작을 거쳤다. 아래 이미지들은 다른 게임 리소스를 복사하지 않고 프로젝트의 SwordGirl 원본에서 파생했다.

`concept-contact-a.png`는 직전 2프레임 작업의 낮은 뒤발 포즈이다. 원본 및 정확한 편집 프롬프트는 [../../IMAGEGEN_PROMPTS.md](../../IMAGEGEN_PROMPTS.md)의 SwordGirl 1, 2, 4번을 참고한다. `concept-swing-b.png`는 같은 작업의 낮은 앞발 포즈(그 문서의 1, 3번)를 아래 마지막 프롬프트로 다시 수정했다.

## `concept-pass-a.png`

참조: 직전 2프레임 작업의 낮은 두 포즈(`concept-contact-a.png`와 수정 전 `concept-swing-b.png`).

> Use case: precise-object-edit. Create ONE in-between PASSING POSITION for this exact pixel-art swordswoman's grounded walk cycle, midway between the two reference contact poses. Preserve her identical blonde bob and covered eyes, face, gold-trimmed brown uniform and pleated skirt, dark boots, upright diagonal silver sword, proportions, warm 64-color-like pixel style, and right-facing 3/4 orientation. This is a normal walk, not a run or march: both thighs descend naturally from beneath the skirt, the screen-left support leg is nearly straight with its boot planted flat on the baseline directly beneath her hips, and the screen-right swing leg passes close beside it with its bent knee modestly forward and its boot toe only a few pixels above the same baseline. The feet should be close together around the body center, with knee movement visible. Torso, pelvis, skirt, shoulders and sword hand shift gently as a connected full-body drawing; head remains at essentially the same height as references. No horizontal seam at skirt. Crisp square pixels, transparent background, single complete character, no duplicate, no text.

## `concept-pass-b.png`

참조: 같은 낮은 두 포즈.

> Use case: precise-object-edit. Draw ONE different PASSING POSE for the SAME blonde pixel-art swordswoman as these references, for a six-frame normal walking loop. Preserve her identical covered-eye blonde bob, profile face, gold-trimmed brown coat and short pleated skirt, dark boots, thin diagonal silver sword, warm pixel palette and same right-facing angle. This is the OPPOSITE support leg from a prior passing pose: the screen-RIGHT boot is planted FLAT on the shared ground line directly under the hips, supporting her weight; the screen-LEFT leg has passed beneath the skirt and its bent knee begins to swing gently forward, with the swing boot's toe just 3–6 final sprite pixels above the ground. Both boots remain near each other, not wide apart. Anatomically continuous thighs, knees, shins, boots; hips/skirt respond slightly, torso inclines subtly and head height stays consistent. A quiet ordinary walk, no skipping, high-knee marching, running, or detached limbs. One character on true transparent background, crisp pixel art, no text.

## `concept-swing-a.png`

참조: `concept-pass-a.png`.

> Use case: precise-object-edit. Create the NEXT frame after this swordswoman's passing pose in a quiet ordinary walk toward screen-right. Preserve the exact same character design and image style: blonde bob over eyes, brown gold-trimmed coat and pleated skirt, stocking, dark knee-high boots, upright diagonal silver sword, same size and angle. Evolve the legs naturally from the reference: the boot that currently supports weight near center now moves back to screen-left under the hip while staying PLANTED flat on the ground; the other boot swings forward to screen-right in a low step, its toe hovering only about 25 image pixels above the same ground line. Give the forward thigh a slight bend at the knee, but not a high knee. The back leg must stay straight and continuous from skirt to boot. Shift pelvis/skirt/torso subtly forward over the support foot and keep head height near unchanged. This should read as the swing before an opposite-foot forward contact, not as marching, running, or reversing direction. Pixel art with crisp square pixels, true transparent background, one character, no text.

## `concept-contact-b.png`

참조: `concept-swing-a.png`.

> Use case: precise-object-edit. Create the NEXT contact pose of the exact same pixel-art blonde swordswoman after this low forward leg swing. Preserve every design detail, pixel style, face/hair, brown gold-trimmed uniform and skirt, hand and silver sword, orientation, scale and framing. Change the full-body weight placement naturally: LOWER the forward screen-RIGHT boot so its heel and sole plant FLAT on the exact same ground line as the other boot, taking body weight; gently rotate pelvis/skirt over that new support leg and let the screen-LEFT old support heel rise just slightly as it prepares to trail. Keep both legs long and continuous under skirt, knee bend natural and small, not a marching step. Stable head height, tiny shoulder/hand counterbalance. The pose must be a grounded rightward WALK contact, not idle, run, or high-knee. Transparent background, crisp pixel art, one character, no text.

## `concept-down-a.png`

첫 참조: `concept-contact-a.png`.

> Use case: precise-object-edit. Produce ONE DOWN/WEIGHT-TRANSFER in-between frame immediately after this exact pixel-art blonde swordswoman's forward walking contact, as part of a grounded normal walk toward screen-right. Keep EXACTLY the same recognizable face, bob hair, uniform, gold-trimmed pleated skirt, knee boots, sword, painterly pixel clusters and transparent backdrop. The SCREEN-RIGHT boot remains planted flat on the ground, slightly more under her pelvis as the body advances over it; the rear screen-LEFT toe pushes gently off the ground and the rear knee bends a little, not high. Lower pelvis and head by only about 1–2 final sprite pixels, with the coat and skirt compressing slightly over the planted leg and sword hands counterbalancing. Both legs remain connected to the moving hips under the skirt. This is an understated contact→down pose, not the original identical contact, not a run, jump, march, or sliding torso. One full character, crisp pixels, no text.

최종 수정 참조: 위 첫 출력물.

> Use case: precise-object-edit. This is frame 2 of an eight-frame ordinary grounded walk, the DOWN and toe-off pose after a forward contact. Keep this exact pixel-art swordswoman's blonde covered-eye bob, face, coat/skirt gold trim, sword, proportions, palette, art style, right-facing direction, head size and transparent canvas. Make the pose visibly different from the preceding contact while remaining an adjacent animation frame: the SCREEN-RIGHT planted support boot stays flat on the same ground line and now angles slightly back under the hip as weight settles; the SCREEN-LEFT rear leg bends more at its knee and its boot comes about 30–40 image pixels forward toward the support leg, with rear heel raised and toe hovering about 15 image pixels above the ground. Lower the full torso/hip/head slightly, about 1 final 256px sprite pixel, and let the pleated skirt settle over the bent thigh. Keep continuous natural thigh-to-knee-to-boot outlines; no sliced seam, no high lift, no jumping, no new props, no duplicate character, no text. Crisp pixel art.

## `concept-down-b.png`

첫 참조: `concept-contact-b.png`.

> Use case: precise-object-edit. Create ONE DOWN/WEIGHT-TRANSFER in-between pose immediately AFTER this exact blonde pixel-art swordswoman plants her forward boot in a normal rightward walk. Keep the exact same character, covered-eye blonde bob, face, brown gold-trimmed uniform and pleated skirt, brown boots, thin upright silver sword, original scale, right-facing 3/4 viewpoint, painterly crisp pixels and truly transparent backdrop. The NEW forward screen-RIGHT boot stays FLAT on the same ground line but appears slightly closer to under the hips as the torso advances over it. The OLD trailing screen-LEFT boot's heel lifts only modestly and its knee starts to bend toward the passing phase. Lower the full head/torso/pelvis only around 1–2 native sprite pixels, compress skirt slightly, keep sword hands connected and the head mostly stable. Continuous anatomy, no upper-lower cut. Quiet grounded weight transfer, not a run, jump, high march, or frozen duplicate. One character, no text.

최종 수정 참조: 위 첫 출력물.

> Use case: precise-object-edit. Refine this as frame 6, the OPPOSITE-LEG DOWN/TOE-OFF pose in the same eight-frame grounded walk of this exact blonde pixel-art swordswoman. Keep precisely her covered-eye bob, face, brown gold-trimmed jacket and pleated skirt, dark knee boots, diagonal silver sword, right-facing viewpoint, painterly crisp pixel style, scale and transparent background. Make the step visibly different from the preceding wide contact but only a small animation advance: the screen-RIGHT planted forward boot stays flat at the shared ground line while weight shifts slightly forward over it; the SCREEN-LEFT trailing leg flexes distinctly at its knee and its boot slides forward about 30–40 large-image pixels toward the support boot, with heel off the ground and toe only a tiny amount above the baseline. The whole connected pelvis/skirt/torso settles by about one final native pixel, head stays largely level, sword hand counterbalances. Clean connected leg outlines beneath the skirt, no horizontal cut, no high-knee pose, no run or jump. One character, no text.

## `concept-swing-b.png`

참조: 직전 2프레임 작업의 낮은 앞발 포즈(상단 링크의 SwordGirl 1, 3번).

> Use case: precise-object-edit. This is frame 8, the final LOW SWING pose before frame 1 forward boot contact in an 8-frame pixel-art walk loop. Preserve the exact same blonde swordswoman identity, costume, skirt, sword, torso, head size/height, right-facing angle, original warm pixel style and transparent background. Make two small connected lower-body pose changes ONLY: (1) lower the screen-RIGHT forward swing boot so its entire sole floats only about 25–35 pixels above the planted left boot's ground line in this large source image (roughly 4–6 pixels in a 256x224 sprite), and move its toe about 20 pixels left toward the body; (2) put the planted screen-LEFT boot about 35 pixels farther screen-left as the supporting leg reaches its rear position before the next step. Keep both knees/thighs anatomically connected under the skirt, adjust the skirt/hip subtly so there is no straight waist seam. The left support sole remains flat at the same ground level. It is a modest ordinary walk, not high-knee marching or running. One complete character, crisp square pixels, no text.

최종 프레임 8은 Aseprite에서 앞발 정강이를 3픽셀 더 내려 8→1 접지 전환을 정리했다. 모든 프레임은 120ms이다.
