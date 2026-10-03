# EnemyStudent single running pose

The game-ready sprite is `frame-01.png` (transparent RGBA, 256×224); the editable Aseprite file is `EnemyStudent-run.aseprite`. `idle-vs-run.png` compares the shipped idle to the new running drawing at nearest-neighbor 4× size. The run pose uses a leftward torso lean, one bent boot planted on y201, a raised trailing boot, wind-swept hair and skirt, both hands on one violet sword, and no animation cycle.

## Creation and cleanup

- Used built-in ImageGen **edit mode** with the shipped `Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png` as the identity/style reference. The selected study is saved in this folder as `run-study.png`.
- The generated art depicts a new pose; the previous `move/frame-01.png` and old walk frames supplied no pixels and were not sheared, shifted, rotated, or warped into this asset.
- `build_pose.py` projects the new study to the native canvas and maps its colors to the shipped idle palette. `candidate-box.png` was selected after nearest-neighbor comparison.
- `finish-in-aseprite.lua` performs native-pixel cleanup, removes one stray detached pixel behind the bow, saves the final sprite and Aseprite document, and verifies the Aseprite roundtrip.
- Final QA: 256×224; alpha only 0 or 255; all opaque colors are in the shipped idle palette; one 4-connected opaque component; support boot on y201; source document reopens pixel-identically.

## Exact ImageGen prompt

> Use case: precise-object-edit. Asset type: ONE complete full-body pixel-art RUNNING travel pose for a side-view 2D sword game. Image 1 is the exact identity and visual style reference: a small left-facing brown-haired schoolgirl swordswoman in a brown pleated uniform, green hair bow, stockings and boots, carrying exactly ONE long violet sword. Draw her body and clothing anew in a genuinely different running pose. This must look like a character actively RUNNING toward screen-left, not an existing standing sprite tilted or sheared. Commit to strong running biomechanics: forward-leaning torso around 20 degrees, head carried forward of hips, one bent support leg planted with its boot flat on the same floor line, opposite leg extended clearly backward with knee flexed and boot airborne behind, skirt pleats and hair/bow trailing with momentum. Redraw shoulders and both arms in a natural running sword-carry pose, both hands visibly connected, ONE violet sword carried behind and angled backward without doubling. Preserve recognizable face, hair color, green bow, brown uniform and violet sword and original expressive pixel-art shading and palette. The entire figure and sword must fit within a transparent 256x224-like canvas and be comparable in scale to the input sprite, with crisp 1-pixel edges and no blurred antialiasing. Only one character, one sword, one coherent leg pair, no detached limbs, no motion lines, no background, no text, no shadow.
