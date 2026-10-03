# SwordGirl single travel pose

- Source: `../../Revision2/SwordGirl/frame-05.png` (the grounded contact pose from the previous walk set).
- Edit: advance each pixel row toward screen right in proportion to its height above the foot baseline, with a 22-pixel shift at the top. Add a further 9-pixel advance at the head and shoulders, tapering to zero near the waist (y=160). This keeps the planted boots in place while making the forward torso lean visible without splitting limbs or changing the original colors.
- Final: `frame-01.png` is a single 256 × 224 RGBA frame; `SwordGirl-travel.aseprite` is its editable Aseprite source; `preview.png` compares idle and travel at 2× nearest-neighbor scale.
- Validation: binary alpha, source palette retained, one connected silhouette, lowest opaque pixel at y=202, and Aseprite PNG round-trip pixel match.
- Built-in ImageGen was not used; this was a direct edit of an existing project pixel sprite.
