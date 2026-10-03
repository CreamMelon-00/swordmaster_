# Candidate-4-v4 source prompts

This staged candidate uses the top four poses from `pose-sheet-study.png`. The
bottom four generated poses were rejected because they repeated the same step.
The native Aseprite processing mapped colors to the shipped idle palette,
aligned the feet to y=201, replaced the generated blade with one original
sword, and removed disconnected purple pixels below the guard.

## Initial sprite sheet

Reference image: `Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png`

> Use case: precise-object-edit. Asset type: ONE 8-frame pixel-art walk-cycle sprite sheet for a 2D side-view sword fighting game. Input image is the exact character identity/style reference: brown-haired schoolgirl swordswoman with green bow, brown pleated uniform, brown boots and long violet sword held low to the right, facing left. Make a clean 4-column by 2-row sprite sheet with EIGHT equally sized, separate full-body frames, read left-to-right across each row. All frames must be the SAME character at the SAME scale, same face, hairstyle, bow, clothing colors and sword length, aligned to one consistent invisible ground baseline. Frame sequence: 1 near-foot contact ahead with other foot behind; 2 down/weight sinks 1 pixel, near foot flat and rear heel lifts; 3 passing, rear leg passes the planted one low to ground; 4 swing, incoming foot moves slightly ahead only 2-3 pixels above ground; 5 opposite-foot contact; 6 opposite down; 7 opposite passing; 8 opposite swing returning seamlessly to frame 1. Short restrained walking stride, no knee kicking, no jumping, one grounded support boot in every frame. Hips, skirt pleats and torso sway subtly with the steps, head almost stationary and both hands/sword gently counterbalance. Readable connected anatomy all frames: skirt to thighs, knees, stockings and boots. Crisp 1-pixel hand-drawn game art, matching the provided native pixel sprite. Transparent background. No panel borders, no labels, no text, no shadows, no extra objects, no extra limbs, no cutoffs. Each individual frame should look like a complete 256x224-like game sprite.

Generated image: `C:\Users\User\.codex\generated_images\01a0ffaa-756f-7810-a554-2a07374d186f\exec-7cf338bf-f598-4de7-bac5-5607f36ab449.png`

## Edited sprite sheet

Reference image: the initial generated sheet above.

> Precisely edit this 4-column × 2-row transparent pixel-art walk-cycle sheet. Keep the complete TOP ROW pixels/composition almost identical. The bottom row currently repeats the top row. Replace ONLY the four BOTTOM-ROW poses so they form the OPPOSITE leg step: in bottom-left frame the other leg is now the leading grounded leg, then it bears weight, the previous leg passes close behind, and finally swings forward low to return to top-left frame. This must be a natural alternate half of the same walking cycle, not a duplicate. Maintain the exact same character face, green bow, brown uniform, sword, height, sword angle, pixel density, centered position, and consistent floor baseline in all eight frames. Subtle 1-2px head bob, one flat shoe grounded in every frame, low swing foot, connected knees/hips, no kicking pose. Preserve a clean 4x2 frame grid, no labels, no background.

Generated image: `C:\Users\User\.codex\generated_images\01a0ffaa-756f-7810-a554-2a07374d186f\exec-76deadad-59e5-4dc0-9f83-e9dac07c936d.png`

Project copy: `Docs/Art/Movement/Revision2/EnemyStudent/pose-sheet-study.png`
