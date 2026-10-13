# 먼 학교의 도트 가독성 보강

Built-in imagegen으로 `distant-school-entrance-v2.png`의 학교만 보강했다. 앞쪽 숲 입구와 길의 구도를 유지하면서 학교 지붕과 창문이 게임 도트 크기에서도 읽히도록 했다. 최종 제작 입력은 `distant-school-entrance-v3.png`다.

## Prompt

Use case: precise-object-edit. Edit ONLY the distant school building in this pixel-art forest entrance; preserve all trees, bushes, gas lamps, gate pillars, winding path, flat foreground lane, lighting, colors and overall composition exactly. The small school currently becomes an indistinct yellow blob when the image is reduced to a 340x181 logical-pixel game canvas. Make the school a little larger/wider, approximately 20% of the image width, still far away behind trees in the same upper-left location. Give it a clearly readable low slate blue-gray roof, a modest triangular central pediment, honey limestone facade, two rows of distinct dark arched windows, and a small recessed dark entrance. Strong intentional pixel clusters should make the architecture legible at a tiny logical resolution. Keep soft atmospheric distance and partial foliage overlap; do not bring it forward or make it a close-up facade. It remains several dozen meters behind the forest gate. No extra architecture anywhere else. Preserve the rest of the scene. Landscape ratio and canvas same as input. No text, no people, no UI.
