# EnemyStudent eight-frame walk

Frame order: left contact, left down, right pass, right low swing, right
contact, right down, left pass, left low swing. All frames are 256×224 with
120 ms duration and y=201 support baseline.

| Final frame | Source |
| --- | --- |
| 01–03 | `../Candidate-4-v4/frame-01.png` through `frame-03.png` |
| 04 | `../../EnemyOpposite/right-low-swing-v1.png` |
| 05 | `../../EnemyOpposite/opposite-contact-v2.png` |
| 06 | `../opposite-down-v1.png` from `../compose-down.lua` |
| 07 | `../../EnemyOpposite/left-passing-v1.png` |
| 08 | `../Candidate-4-v4/frame-04.png` |

The first four original candidate poses came from image generation and native
Aseprite cleanup; their exact prompts are recorded in
`../Candidate-4-v4/IMAGEGEN_PROMPTS.md`. The independent opposite-contact,
low-swing, and pass poses have source details in
`../../EnemyOpposite/SOURCE.md`.

Frame 06 used the edited image at
`C:\Users\User\.codex\generated_images\01a0ffaa-756f-7810-a554-2a07374d186f\exec-3d6e09c4-ce2d-43e7-acfc-62b25daab0be.png`
with `../../EnemyOpposite/opposite-contact-v2.png` as its single reference.
Its exact prompt was:

> Edit this EXACT existing full-body pixel-art sprite into the NEXT walk-cycle frame, the opposite-foot DOWN/WEIGHT phase. Keep the same anatomical leg overlap: the right thigh crosses in front beneath the skirt and the screen-left boot is the newly planted support. Bend that planted front knee a little, let hips/skirt sink only about 1–2 native pixels, and let the screen-right rear boot lift its heel slightly while staying low. Move the planted front boot only a few pixels rearward, from screen x about 90 to about 100, flat on one unchanged ground line. Keep the same face, hair, green bow, brown uniform, hands, exactly one low violet sword, left-facing scale, original crisp pixel-art style and transparent background. Both thighs, knees, stockings and boots must remain a seamless complete body. Do not revert to the reference's earlier uncrossed leg order. No duplicate character, no text, no frame grid.

`../compose-down.lua` mapped the generated lower-body pixels to the shipped
idle palette, restored the original upper body and sword, and aligned the
grounded sole to y=201. `../build-eight-final.lua` assembled the editable
Aseprite source, individual PNGs, and review GIFs.
