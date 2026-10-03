# EnemyStudent right-foot contact key pose

Selected finished key pose: `opposite-contact-v2.png` (256×224, 120 ms). Editable single-frame Aseprite source: `EnemyStudent-right-contact.aseprite`. The visual comparison `comparison-v2.png` shows existing candidate frame 1, existing candidate frame 4, and this right-foot contact pose, left to right.

This is the built-in ImageGen edit mode. The successful edit used these inputs in order:

1. Edit target: `opposite-contact-v1.png`, a rough pixel pose sketch made from candidate frame 4 in Aseprite. It specifies the new crossed topology.
2. Style reference: `../EnemyStudent/Candidate-4-v4/frame-04.png`, the existing sprite with the original character, outfit and sword.

Final ImageGen prompt (verbatim):

> Use case: precise-object-edit. Image 1 is the edit target, a rough hand-drawn opposing-foot contact sprite. Image 2 is the exact character/style/shading reference. Polish and anatomically correct only Image 1's legs, skirt hem and boots. Preserve the NEW crossed walking-leg topology from Image 1: her RIGHT hip at screen-right sends a continuous foreground thigh diagonally to screen-left, then a shin into a flat PLANTED boot at screen-left; her LEFT hip at screen-left sends the background leg diagonally to the trailing boot at screen-right. The foreground right thigh occludes the left thigh only at their crossing, leaving exactly two naturally tapered legs and two detailed brown boots, no horizontal flesh bar, no detached thigh, no hard straight triangle-like strip. Make both boots sit on a shared ground line and the rear heel slightly raised. Restore the original detailed pixel texture, small natural folds and contours from Image 2. Preserve the entire head, green bow, torso, hands, and exactly ONE diagonal purple sword from Image 1/2. Transparent background, crisp non-blurry square pixel art, no extra limbs, no cuts, no duplicate sword, no floor, no text.

The selected generated study is `imagegen-crossed-reference.png`. `scale-generated.lua` scales it to the 256×224 pixel grid. `compose.lua` uses only the crossed lower-body study, maps pixels to the original palette, restores the original upper body, trims the planted boot, and aligns the feet at y201. `validate.lua` verifies binary alpha, original palette, upper-body identity, contact line, one connected silhouette, and Aseprite round trip.

## Right-foot low swing (frame 4 candidate)

Final key pose: `right-low-swing-v1.png`; single-frame source `right-low-swing-v1.aseprite`; 3→4→5 preview `right-low-swing-v1-comparison.png`. The built-in ImageGen edit used a two-pass prompt sequence. First pass inputs: `../EnemyStudent/Candidate-4-v4/frame-03.png` as the edit target, and `opposite-contact-v2.png` as next-pose reference. Its intermediate saved output is `swing-first-study.png`.

First-pass prompt (verbatim):

> Use case: precise-object-edit. Asset: one new 256x224 side-scrolling pixel-art walk frame. Image 1 is the edit target and the PREVIOUS pose; Image 2 is the NEXT pose. Draw one natural full-body IN-BETWEEN for a LEFT-facing girl's alternating walk, approximately halfway from Image 1 to Image 2. At this low swing frame, the support LEFT boot stays flat near screen x100 and ground y201. The free RIGHT leg moves from behind to under the skirt, bending softly at knee; its boot appears at x108–115, toe low around y194–198, preparing to land toward screen-left in Image 2. The right boot must have visibly advanced, not remain behind at x140. Both complete legs connect organically at the hips under the skirt with correct front/back overlap, no sliced thigh, no extra foot. Keep the head, green bow, brown uniform, hands and exactly ONE purple sword nearly identical to Image 1. Match its pixel grid, original 80-color palette, crisp square edges, fully transparent background, 256x224 framing and same scale. No duplicated sword, no ground, no blur, no text.

Second-pass inputs: `swing-first-study.png` as the edit target, and `opposite-contact-v2.png` as next-pose reference. The selected final study is `swing-study.png`.

Second-pass prompt (verbatim):

> Use case: precise-object-edit. Image 1 is the edit target for a left-facing pixel-game walk frame, Image 2 is the NEXT right-foot-plant frame. Change ONLY the lifted FREE RIGHT LEG of Image 1: bring that boot much farther forward, from its present far-right x~140 position to directly beneath the skirt around x~108 on the 256-pixel reference canvas, with the lifted toe around y195, 5-6 pixels above the ground. Bend the knee naturally under the skirt, so its thigh, stocking and shoe form one connected, low forward SWING before it plants at x~90 in Image 2. The LEFT leg remains planted flat at x~100/y201 and supports the body. Match Image 1's face, green bow, torso, skirt, single purple sword, and exact scale/composition. Keep two legs, one sword, transparent background, crisp original pixel art, no blur, no extra objects. This must read as a distinct intermediate: free boot directly under center of body, not trailing behind at screen-right.

## Left-foot passing (frame 7 candidate)

Final key pose: `left-passing-v1.png`; single-frame source `left-passing-v1.aseprite`; 5→7→8 preview `left-passing-v1-comparison.png`. The built-in ImageGen edit inputs were `opposite-contact-v2.png` as the edit target and `../EnemyStudent/Candidate-4-v4/frame-04.png` as next-pose reference. The selected study is `passing-study.png`.

Prompt (verbatim):

> Use case: precise-object-edit. Input Image 1 is the edit target, a RIGHT-foot-forward contact for a left-facing pixel-art game character. Input Image 2 is the NEXT left-foot-forward pose. Draw the distinct IN-BETWEEN PASSING pose after Image 1: the planted RIGHT boot has slid back relative to the body from x~90 to x~118 on the 256px canvas, staying flat at y201; the free LEFT boot has left its trailing x~140 position and passes beneath the skirt at x~110, raised 5-7 pixels off the ground, knee softly bent, on its way to plant at screen-left x~95 in Image 2. Preserve which anatomical leg is supporting: RIGHT leg at screen-right foot x118; LEFT leg is the lifted passing foot x110. The left thigh starts from skirt screen-left, passes naturally beside/in front of the right thigh, with no flesh bar, no severed hip, no extra boot. Keep the exact character identity, green bow, detailed brown uniform, hands, and ONE purple sword diagonal down to screen-right. Maintain original pixel-art scale, crisp square edges, transparent background, 256x224 proportions and shared y201 ground line. No duplicate sword, no third leg, no text, no floor, no blur.

`compose-study.lua` downsamples and maps both studies to the original palette, then preserves the source upper body, sword, and foot line. `validate-study.lua` verifies the alpha channel, original palette, a single connected silhouette, unchanged upper body and sword, y201 foot contact, and Aseprite round trip for both frames.
