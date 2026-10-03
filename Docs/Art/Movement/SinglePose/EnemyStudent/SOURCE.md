# EnemyStudent single travel pose

The final 256×224 pose is `EnemyStudent-travel.png`. It was edited from the
existing idle sprite in Aseprite. The head and torso lean toward screen-left;
the boots stay on the original floor line. Every opaque pixel is from the
shipped idle sprite palette. The editable source is
`EnemyStudent-travel.aseprite`, and `EnemyStudent-travel-comparison.png` shows
idle beside travel at 4× nearest-neighbor scale. `build-pose.lua` rebuilds
and validates the asset.

An image-generation edit was used as a visual pose study. No generated pixels
were copied into the final asset. Built-in ImageGen edit mode used the shipped
`Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png` as its only
reference. Its exact prompt was:

> Use case: precise-object-edit. Asset type: one full-body pixel-art movement sprite for an existing 2D side-view sword game. Edit the provided EnemyStudent idle sprite as the exact identity and pixel style reference. Make ONE complete left-facing travel pose, as though she is deliberately moving toward screen-left: lean head, shoulders, and torso forward toward screen-left by a small but unmistakable amount, about 8–12 degrees from upright. Hips follow naturally; one front boot reaches low toward screen-left and is planted on the same ground line, trailing boot stays close to the floor behind, both legs attached cleanly beneath the skirt. She is travelling, not sprinting, jumping, crouching, attacking, or kicking. Retain the exact recognizable brown-haired schoolgirl face, green bow, brown pleated uniform, stockings, brown boots, and exactly ONE long violet sword held low behind to screen-right. Keep the same full-body scale and position on a transparent 256×224-style canvas, original limited palette and crisp 1-pixel pixel-art edges. Preserve clean continuous silhouette from skirt through thighs, knees, stockings and boots; preserve hands attached to arms and one sword attached to the grip. No background, text, frame grid, shadow, motion streaks, detached parts, duplicate limbs or extra swords.

The study file remains under Codex generated images at
`C:\Users\User\.codex\generated_images\01a0ffde-91fe-72b0-a0d6-d54a6ac03ed8\exec-ad35427e-414a-4bfb-a305-b316e5472b46.png`.
