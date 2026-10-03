# SwordGirl single running pose

- Final sprite: `frame-01.png` (256 × 224 RGBA, transparent, right-facing).
- Editable Aseprite source: `SwordGirl-run.aseprite` (one full-body cel).
- Preview: `preview.png`, idle on the left and new run on the right at 4× nearest-neighbor scale.
- Art method: built-in ImageGen **edit** mode used the original SwordGirl idle sprite as the identity/style reference, then a targeted edit planted the forward boot and lifted the rear leg. The generated illustration is a new anatomical pose. `build-run.lua` sampled it through Aseprite onto the project's pixel grid and snapped every opaque pixel to the existing idle sprite's 64-color palette. No row shifting, shearing, whole-sprite rotation, or warping of an existing game cel was used.
- Selection: `concept-running-final.png` is the selected second concept; `concept-running.png` is the initial study with a far-rear planted boot.
- QA: `qa_run.py` confirms 256 × 224, binary alpha, original palette, one 4-connected silhouette, and a planted front sole at y=202. `validate-run.lua` confirms the Aseprite source flattens pixel-identically to `frame-01.png`. The final visible shape has one sword and two joined legs.

## Exact ImageGen prompt, identity edit

```text
Use case: identity-preserve.
Asset: ONE transparent full-body side-view game sprite for SwordGirl's movement pose, facing screen RIGHT. Image 1 is the identity and pixel-art style reference, not a pose to tilt or trace.
Redraw the same blond bob-haired young swordswoman wearing her dark brown tunic/pleated skirt with gold trim, bare knees, tall dark leather boots, and carrying exactly ONE slender silver sword with golden crossguard. Preserve recognizable proportions, colors, and pixel-art shading from the input.
Create an unmistakably NEW RUNNING pose, frozen at one instant of a forward dash: head and chest naturally thrust roughly 20 degrees toward screen right, waist bends with anatomy, front knee lifted forward in a distinct stride, rear leg stretching backward and down with the rear boot planted in a push-off contact on a common floor line. Both legs attach naturally under the skirt. Hair and skirt stream subtly back. Elbows bend to balance the movement while both hands carry the single sword low and forward, its full blade and hilt connected and visible. The dynamic entire-body silhouette should read "charging forward" even if viewed as a single still frame.
Canvas/composition: isolated single character, transparent background, whole figure including sword visible with comfortable margins, same general rendered figure height as reference, no ground or shadow, screen-right movement. Detailed clean hand-drawn pixel art with intentionally discrete square pixels and crisp one-pixel edges. Preserve the reference palette and outline style.
Critical constraints: NO rigid rotation, shearing, row shift, or warping of the original sprite. Actually redraw the running stance with changed limb angles, changed sword angle, and secondary motion. No motion blur, smooth anti-aliasing, gradients, extra limbs, severed limbs, extra weapons, text, frames, panels, or backdrop.
```

Reference: `Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png`.

## Exact ImageGen prompt, planted front boot revision

```text
Use case: precise-object-edit. Edit the attached pixel-art running swordswoman. Keep her identity, hair, dark-brown gold-trim dress, two-handed single sword held low and forward, palette, pixel-art shading, upper body lean, transparent background, and overall scale/composition nearly identical.
Redraw ONLY the legs and lower skirt into a clearer sprint contact pose for a single held game sprite facing screen RIGHT: the RIGHT/front boot is planted flat on the common floor line directly below or a little ahead of her torso center, bearing her weight; the LEFT/rear leg bends at the knee behind her with its boot visibly lifted off the floor in the recovery phase. Both thighs emerge naturally from beneath the skirt, knees/boots are anatomically connected, and the planted leg visibly supports the leaning torso. This must look like actively running forward, not hopping or sliding on a far-behind foot.
Keep one sword, two hands on grip, all body parts, sharp square-pixel edges. Isolated transparent background, no floor/shadow/text, no extra limbs, no duplicate weapons.
```

Reference: `concept-running.png`.
