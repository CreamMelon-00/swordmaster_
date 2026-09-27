# Mob student static pixel design v3: grip and hand orientation

Created 2026-09-27 using the built-in image generation tool. Static enlarged design preview only; no animation, source sprite replacement, or runtime import.

Final image: `mob-student-pixel-design-v3-grip.png`

## Design decisions

- Retain the mob's compact body, eye-covering blonde bob, brown/gold uniform, and steel sword.
- Rework hilt length, hand spacing, elbow support, and hand/hilt contact.
- For this specific viewpoint, show palm/curled finger-pad side of the guard-side front hand and back/knuckle side of the pommel-side rear hand. This is a chosen viewpoint, not a universal rule for every grip.
- Use a short visible hilt section between hands for pixel readability.
- The preview is enlarged generated art, not a native-resolution production sprite.

## References inspected

- [Higgins Armory: Introduction to Historic Combat](https://www.annarborsword.com/PDF/Higgins_Museum_longsword_handout.pdf), PDF pp. 10–11: main hand at crossguard, second toward pommel, adaptable grip.
- [Wasatch HEMA Beginner's Guide](https://img1.wsimg.com/blobby/go/56282a54-c147-441f-a861-f9ec2c0bfc22/downloads/Study%20Guide%20for%20Beginner_s%20V1.3.pdf?ver=1674073413909), PDF p. 4: natural wrist alignment and handshake grip.
- [Art of War: Animating Realistic Sword Combat](https://www.gamedeveloper.com/art/art-of-war-animating-realistic-sword-combat): authored sword-combat instruction with photo reference. The lower photo of Figure 1 was the selected grip reference.
- [The Last Blade 2 official images](https://www.snk-corp.co.jp/official/akeakaneogeo/lastblade2/): viewed official ss1–ss4; ss2 used as a secondary game silhouette reference.
- [Samurai Shodown II official images](https://www.snk-corp.co.jp/us/games/acaneogeo/samusho2/): viewed official ss3–ss4 for comparative pixel combat silhouettes; not used as a direct sword grip template.

Downloaded research images are under workspace `.validation/mob-grip-references/`. They are reference material only, not incorporated as game artwork.

## Prompt 1: grip mechanics

Use case: precise-object-edit
Asset type: ONE static pixel-art combat character design, a targeted hands-and-hilt correction.
Primary request: Correct the unnatural sword grip of the mob student in image 1 using the real longsword grip photo in image 2. Preserve this specific character, compact body proportions, costume, and coarse pixel technique. Do not produce animations.
Input images:
- Image 1 is the ONLY edit target: the existing brown-uniform mob holding a steel longsword.
- Image 2 is an anatomical grip reference with TWO photographs. Use ONLY THE LOWER PHOTOGRAPH as the chosen grip: front hand just under the crossguard, rear hand toward/on the pommel, more separation and natural wrists. The top photo is NOT the chosen grip. The photo's sword points LEFT; mirror its mechanics conceptually to match the student's sword pointing RIGHT.
- Image 3 (The Last Blade 2 official screen) is secondary reference for compact side-facing game arm silhouettes and pixel readable weapon handling ONLY. Do not copy its characters, clothing, text, palette, sword type, UI or proportions.
Anatomical correction, highest priority:
Build one continuous physical sword: straight steel blade -> small brass crossguard -> long dark leather hilt -> small pommel. Blade, hilt and pommel share ONE continuous straight longitudinal axis.
The student's dominant RIGHT hand is the forward hand immediately behind/below the crossguard. Her LEFT hand is the rear hand on the end of the hilt, closing naturally around the pommel area. The hands occupy two distinct positions along that SAME grip axis, never side-by-side on unrelated axes.
Both palms and curled fingers genuinely encircle the hilt. Show compact knuckle arcs, curled finger pads under/around the grip, and a short bent thumb closing against the side of the grip near the index finger. The wooden/leather handle must disappear behind each closed hand and reappear between them, making contact and occlusion unambiguous. The hands must NOT look like two empty fists placed in front of a sword. No flat mittens, spread fingers, fists floating beside the hilt, reversed thumbs, or extra fingers.
Lengthen the hilt minimally if needed to fit two closed hands in separated positions. Leave a short visible dark grip section between the hands (about 1–2 logical pixels, not a huge gap). Place the pommel at the rear hand/end, never clipping through the wrist.
Keep each wrist naturally continuous with its forearm; neither sharply kinked nor folded sideways. Angle and slightly reposition the forearms/elbows to suit the grip. Softly bent elbows, relaxed shoulders, separate readable arm silhouettes, no crossed impossible elbows. Do NOT force blade and forearm perfectly collinear; use a natural oblique handshake grip.
Combat pose: quiet three-quarter guard facing RIGHT, hilt held near waist/lower chest, blade projects forward and only moderately upward (roughly 15–25 degrees above horizontal). Make minor elbow/sword-angle changes to support the corrected grip. Keep balanced staggered planted feet and softly bent knees. No attack swing or flourish.
Preserve: beige-blonde bob, completely eye-covering bangs with NO visible eyes, brown hairband, small neutral mouth, brown blazer and pleated skirt, muted gold lapel/cuff/hem trim and buttons, gold neck ribbon, white shirt collar/cuffs, dark knee socks and brown shoes. Keep the existing approximately 2.8–3-head compact game proportions and head size. Preserve the simple steel blade and brass crossguard; no new armor or weapon embellishments.
Pixel craft: crisp consistent coarse square pixel grid, stepped silhouette, thin dark outlines and restrained hard-edge colour clusters. Hands use a FEW purposeful small pixel clusters for thumb, knuckles and curling fingers, at the SAME logical resolution as the face and clothing. No realistic high-resolution hands pasted onto a coarse sprite, no gradients, blur, anti-aliasing or painterly details.
Composition/output: ONE full-body sword-holding mob only, all shoes and sword tip visible with transparent margin. Genuinely transparent background. Enlarged design preview, no frame sheet, no alternate poses, no annotations, no close-up inset, no text, no source photos in the result, no ground shadow. Correct the hands, hilt and supporting arm mechanics; do not redesign the character.

## Prompt 2: front thumb correction

Use case: precise-object-edit
Image 1 is the pixel-art character edit target. Image 2 is a real grip reference; use the LOWER photograph ONLY, mirrored conceptually so blade points right.
Make a SMALL LOCAL CORRECTION to ONLY the FRONT HAND immediately beside the brass crossguard (the hand on screen RIGHT). Do not redraw or reposition the character, arms, rear hand, hilt, guard, blade, feet, head, hair or costume.
Redraw this front hand into a physically clear oblique handshake grip around the dark brown handle: the thumb is attached to the palm and visibly bends diagonally over the upper/near edge of the handle, just as in the LOWER reference photo. Show a short dark contact/separation line below that thumb. The curled index/middle/ring/little fingers wrap around and occlude the LOWER edge of that same handle; small knuckle/finger curves can be grouped for low resolution. There must be an unmistakable wrap around the hilt, not a pink mitten or four downward hanging fingers placed beside it. A small dark brown handle segment should be visible at the thumb/finger opening and continue into the visible grip between both hands. The front hand remains immediately behind the crossguard, behind the blade, wrist naturally connected to the current forearm. Exactly one normal hand, one bent thumb and four curled fingers, without extra appendages.
Match the EXISTING coarse logical pixel resolution: purposeful square clusters, crisp stepped outline, 2–3 pixel shapes for the thumb/knuckle separation, same skin palette and thin dark outline. No realistic high-resolution hand, no smooth shading, no anti-aliasing, no outlines too fine for this sprite.
Preserve EVERYTHING ELSE in image 1, including exact compact body/head proportions, eye-covering beige bob, brown/gold school uniform, planted combat guard and entire sword silhouette. One full-body figure on true transparent background, same framing and scale. No extra image panels, close-up inset, labels, text, animation or new pose.

## Final prompt: palm versus back of hand

Use case: precise-object-edit
Asset type: single static pixel-art sword fighter design.
Image 1 is the edit target. Image 2 is a true sword grip photo: use the LOWER PHOTO ONLY; mirror its hand relationships mentally to a sword pointing RIGHT.
Make one focused correction to the TWO HANDS in image 1. The user specifically wants the different sides of the two hands to read correctly:
FRONT hand at the CROSSGUARD, screen-right: PALM SIDE visible to the viewer, with the fleshy thumb-base pad and compact inner finger pads. A normal CLOSED grip, not a C-shaped claw. The brown leather hilt is seated inside the palm under a bent thumb. That thumb closes obliquely across/against the curled index finger, over the same hilt; the four fingers are SHORT and tightly curled around the lower/far edge of the handle. Contact is explicit: finger pads and thumb press against the grip, and no large black hole/air gap remains between thumb and fingers. Show just one short interior palm/thenar crease with 1–2 purposeful dark pixels. Palm-side skin clusters, not dorsal knuckle/nail details. Do not draw four long hanging exposed fingers.
REAR hand near the POMMEL, screen-left: BACK OF HAND visible to the viewer. A broad smooth dorsal skin plane, 2–3 compact knuckle steps, with the fingers curled around the far/under side of the grip. The thumb closes mainly on the far side, mostly hidden. No palm crease or prominent exposed finger pads on this rear hand. It must look clearly different in orientation from the front hand.
Maintain one continuous straight sword axis, guard -> hilt through the closed front hand -> short visible brown hilt between hands -> rear grip -> pommel. Both hands genuinely grasp this single handle. Main hand remains guard-side, offhand remains pommel-side. Permit only tiny natural forearm rotations to explain palm-vs-back visibility. Keep wrists aligned naturally with forearms; no disconnected or bent-back wrists, duplicated hand, reversed thumb, or extra fingers.
Preserve all other parts of image 1: face/hair with eyes completely hidden, compact body/head proportions, brown and muted gold uniform, bow, socks/shoes, balanced guard stance, arms/elbows overall placement, blade and brass crossguard design, sword direction/length, framing and transparent margin.
Match the same coarse logical pixel size as face and outfit, hard square clusters, stepped contour, limited skin palette, thin dark outlines. Hands should be readable low-resolution shapes, NOT realistic hands with extra fine detail. Genuine transparent background. ONE full-body figure only. No text, diagrams, labels, close-up inset, source photo, alternate pose, animation or sheet.

