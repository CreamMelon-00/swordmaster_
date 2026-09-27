# Mob student static pixel design v1

Created 2026-09-27 using the built-in image generation tool. Design preview only; no animation or runtime asset replacement.

References:
- Character identity: `Assets/Game/Resources/Dialogue/mob-student-brown-muted-cel-v5.png`
- Proportions / pixel rendering only: `Assets/Game/Resources/LegacyDuel/Player/pa_player_idle-Sheet.png`, `Assets/Game/Resources/LegacyDuel/Enemy0/pa_enemy_1_i-Sheet.png`
- Lower legs were not visible in the illustration; plain dark socks and brown loafers are a provisional interpretation.

Final image: `mob-student-pixel-design-v1.png` (transparent enlarged design preview, not a native 144x80 sprite).

## Initial generation prompt

Use case: stylized-concept
Asset type: ONE static pixel-art character design preview for an existing 2D sword-duel game, not animation and not a sprite sheet.
Primary request: Translate the mob student in reference image 1 into the body proportions and pixel rendering technique of references 2 and 3.
Input images: Image 1 is the ONLY character identity/costume/hair/palette reference. Images 2 and 3 are ONLY body-proportion and pixel-craft references; each is an old sprite sheet. Do not reproduce their outfits, hairstyles, headwear, weapons, colours, or animation layout.
Subject and identity: a reserved student with pale beige-blonde bob hair, a dark brown hairband, thick straight bangs COMPLETELY obscuring both eyes, a tiny neutral mouth. Chocolate brown fitted blazer with warm gold lapel edging and gold buttons, white pointed shirt collar and white cuffs, a large muted gold neck ribbon, chocolate brown pleated skirt with gold hem stripe. Faithfully simplify the identity and costume from image 1. Never reveal eyes through bangs.
Body proportions: match the old game sprites, approximately 3 to 3.5 heads tall, large head, very compact torso, short substantial limbs, small hands and simple feet. Do not use the tall anime illustration proportions of image 1. The underlying imagined sprite should be approximately 55–60 source pixels tall on an 80-pixel-tall canvas, like the old idle sprites. Its head should be about 17–19 source pixels high.
Style/medium: authentic low-resolution hand-crafted 2D game pixel art. Use a consistent coarse pixel lattice, crisp square pixels, stepped silhouettes, thin dark brown pixel outlines, restrained hard-edged shadow/highlight clusters, about 20–24 colours across the figure. Match the visual pixel density and compact silhouette of references 2 and 3. Present as a large clean integer nearest-neighbour enlarged preview so pixel blocks are clearly visible. No anti-aliasing, no smooth vectors, no painted textures, no gradients, no blurred or mixed-size pseudo-pixels, no high-resolution illustrated face.
Composition/framing: ONE full-body sprite only, centered with ample transparent margin, standing in a quiet neutral pose with feet slightly apart, hands together loosely in front as in image 1. Slight three-quarter view facing toward the right, suitable for this side-view game, but retain the shy student design. Entire hair and both shoes visible. No ground shadow. Genuine transparent background.
Lower-leg design: image 1 ends at the upper thighs; complete conservatively with plain dark brown knee socks and simple dark brown loafers, with no new decorative motifs.
Constraints: no animation frames, no alternate poses, no turnarounds, no comparison sheet, no text, no UI, no labels, no watermark, no props, no sword, no hat, no cape, no orange adventurer outfit. Preserve only the proportion and pixel technique from the old sprites; the student reference determines all character design.
Output intent: one static design proposal, enlarged pixel preview with real alpha transparency; do not treat it as an imported runtime asset.

## Final proportion correction prompt

Use case: precise-object-edit
Asset type: one static pixel-art character design preview.
Image 1 is the edit target: the blonde mob student pixel concept. Image 2 is a BODY PROPORTION AND PIXEL DENSITY reference ONLY: the old player's sprite sheet.
Make ONE targeted correction to image 1: reduce the head AND complete bob hairstyle together by approximately 15% in both width and height relative to the body, reconnect naturally to the neck, and very slightly lengthen the torso and legs (about 5%). Target the compact but less head-heavy old game proportions visible in image 2, approximately 2.8 to 3 heads high. Keep the neutral standing pose, both hands together, feet apart, entire figure visible.
Preserve the SAME character identity and ALL outfit design: beige-blonde bob, eyes entirely hidden behind straight bangs, dark brown headband, brown fitted blazer and pleated skirt, warm gold piping/buttons/hem stripe and gold bow, white collar/cuffs, dark knee socks and simple brown loafers. Never add eyes, weapon, props, headwear, or different clothing. Image 2 is not a source for colours, costume, sword, or animation poses.
Preserve coarse low-resolution pixel rendering, clean stepped outlines and limited hard-edge shade clusters. Rebuild edges after the proportion correction on a consistent crisp square pixel lattice without anti-aliasing or smoothed scaling. The result is ONE centered full-body static sprite on a genuinely transparent background, enlarged for design review, with ample margin. No multiple poses, no sprite sheet, no animation, no lettering, no captions, no ground shadow, no gradient, no background. Do not redesign anything else.

