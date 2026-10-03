# 이동 자세 제작 기록

현재 게임에 적용한 새 전신 달리기 자세의 제작 기록과 정확한 프롬프트는 [SwordGirl](RunPose/SwordGirl/SOURCE.md)과 [EnemyStudent](RunPose/EnemyStudent/SOURCE.md)에 있다. 두 캐릭터 모두 내장 ImageGen **편집 모드**에서 기존 게임 캐릭터를 참조해 새로운 달리기 포즈를 만들고, Aseprite에서 256×224 원본 팔레트 픽셀로 마무리했다. 기존 셀을 기울이거나 행별로 밀어 만든 그림은 사용하지 않는다.

아래는 채택하지 않은 이전 2프레임과 8프레임 걷기 시안의 프롬프트 보관 기록이다. 이전의 단순 기울이기 자세는 `SinglePose`에 제작 기록으로만 남겼다.

## SwordGirl

원본: `Assets/Game/Resources/SwordGirl/Animations/idle/frame-01.png`

1. 첫 번째 전신 포즈:

   > Use case: precise-object-edit. Draw ONE full-body alternate walking frame of the exact pixel-art swordswoman in the reference sprite. The edit MUST change the entire connected figure pose, not just move boots: her pelvis and brown skirt swing forward with the step, upper body leans slightly opposite for balance, blonde hair and sword hand shift naturally. She faces screen-right. Make the leg movement DRAMATICALLY different from the reference: left leg is newly planted almost VERTICAL directly under the hip, the right knee is lifted and bent FORWARD toward screen-right, with the right boot clearly OFF THE GROUND by about one boot height. The right boot must not overlap the left boot. Show the entire foot sole and all limbs with strong continuous contours from waist through thighs/knees to boots. Preserve her distinctive blonde bob covering eyes, dark brown gold-trimmed coat/skirt, pale stockings, knee-high dark brown boots, and upward diagonal silver sword. Transparent background. Crisp pixel art with limited warm palette, no smeared pixels, no disconnected or cut-off limbs, no text, no duplicate pose or extra character.

2. 반대 접지 포즈:

   > Use case: precise-object-edit. Turn this existing single pixel-art swordswoman walking sprite into the OPPOSITE CONTACT FRAME of her two-frame walk cycle. Preserve the exact same character design, same blonde bob, same facial features, dark brown gold-trimmed coat and skirt, boots, colors, brush/pixel style, size, framing, rightward facing direction and rising silver sword. Change only the whole-body walking pose with natural articulated motion: now the RIGHT/front boot is planted flat on the ground at screen-right, right knee slightly flexed and connected all the way up to the moving hip; the LEFT/rear boot is lifted off the ground behind her at screen-left, left knee bent in trailing recovery. Skirt and pelvis swing coherently toward the planted foot, torso slightly counter-leans, hair and sword arm respond subtly. Every body part connected; no cut seams at skirt or hip, no cropped limbs, no duplicate character, no text. Crisp low-resolution pixel art, limited original palette, genuine transparent background.

3. 첫 번째 포즈의 낮은 발동작:

   > Use case: precise-object-edit. This is the second frame of a low, grounded two-frame walking loop. Preserve the exact same blonde pixel-art swordswoman, face, costume, sword, palette, proportions, overall composition and pose. Change ONLY the screen-right FORWARD lifted leg below its knee: lower that right boot close to the planted left boot's ground line. Its toe and sole should float only about 30 image pixels (approximately 5 final sprite pixels) above the level of the planted boot; there must be a SMALL ground clearance, not a high-knee march. Let the right knee bend modestly and keep continuous anatomy from hip under skirt through thigh, knee, shin and boot. Keep left boot planted flat. The torso/hip/skirt remain coherent and complete. Crisp pixel art and truly transparent background. No extra objects, no text.

4. 두 번째 포즈의 낮은 발동작:

   > Use case: precise-object-edit. Refine this exact pixel-art swordswoman WALKING pose to a low grounded stride. Keep the same design, face, hair, costume, skirt, sword, colors, art style, orientation and support boot unchanged. ONLY adjust the screen-LEFT rear trailing leg below the skirt: its toe/sole should hover a tiny distance above the planted right boot's ground line, about 20 source-image pixels, like a normal understated walking step, not a run or march. Repose rear knee/shin naturally to connect smoothly from the hip through the thigh and boot while keeping the toe low. Maintain full-body continuity and a gently swinging skirt, exact transparent background, crisp pixel art. No extra character or text.

## EnemyStudent

원본: `Assets/Game/Resources/EnemyStudent/Animations/idle/frame-01.png`

1. 앞발 접지:

   > Use case: precise-object-edit. Asset type: frame 1 of a 2-frame pixel-art walking animation for a Unity side-view sword-fighting game. Edit target: the provided 256×224 transparent pixel sprite of the brown-haired female student swordswoman facing left. Draw her entire body in a natural leftward walking CONTACT pose: left/front boot lands ahead under a bent left knee, right leg trails with heel lifting, and thighs connect visibly to hips under the short brown pleated skirt. Tilt the hips, skirt folds, shoulders, arms, hands, sword, head and hair together by a small coherent amount so this looks like one full-body pose drawn afresh, rather than a fixed torso pasted on altered legs. Preserve exactly the same recognizable face, green hair bow, brown school uniform, brown boots, long purple sword angled down-right, proportions, color palette and crisp 1-pixel hand-dithered style. Keep the overall character scale and foot ground line as in reference, leave transparent space around figure. No horizontal seam or cut-off legs, no extra limbs, no cropping, no background, no blur, no antialiasing.

2. 뒷발 지지:

   > Edit the supplied game character sprite into the second distinct frame of a natural two-frame walk facing left. Right boot supports under the hips while left leg swings forward with bent knee and lifted toe. Repose the full connected body including hips, skirt pleats, small torso sway, hands and low-held sword; keep face, brown hair and green bow, school uniform, boot design, long purple blade, proportions and palette recognizably the same. Crisp hand-drawn retro pixel art, transparent 256x224-like composition, baseline of feet preserved, no segmented body, no cut-off thighs, no background, no blur.

추가 탐색 시안은 최종 프레임 제작에 사용하지 않았다.

## 이전 8프레임 시안

위의 2프레임 시안은 당시 8프레임 시안으로 교체되었다. 그 8프레임도 현재 게임에서는 사용하지 않는다. 당시 내장 ImageGen의 이미지 **편집 모드**와 Aseprite 수작업을 함께 사용했고, 프롬프트·입력 이미지·프레임별 출처는 아래 기록에 남겼다.

- SwordGirl: [`Revision2/SwordGirl/IMAGEGEN_PROMPTS.md`](Revision2/SwordGirl/IMAGEGEN_PROMPTS.md). 이 문서의 초기 SwordGirl 프롬프트 1~4번도 일부 접지·내딛기 포즈의 입력으로 사용했다.
- EnemyStudent 첫 네 자세의 시안: [`Revision2/EnemyStudent/Candidate-4-v4/IMAGEGEN_PROMPTS.md`](Revision2/EnemyStudent/Candidate-4-v4/IMAGEGEN_PROMPTS.md).
- EnemyStudent 반대 다리 접지·내딛기·통과 자세: [`Revision2/EnemyOpposite/SOURCE.md`](Revision2/EnemyOpposite/SOURCE.md).
- EnemyStudent 최종 8장 구성과 체중 이동 자세: [`Revision2/EnemyStudent/Candidate-8-final/SOURCE.md`](Revision2/EnemyStudent/Candidate-8-final/SOURCE.md).

현재 게임 리소스와 수정 가능한 `.aseprite` 원본은 이 폴더의 [`README.md`](README.md)에 적었다. `Revision2` 하위의 8프레임은 게임에서 로드하지 않는다.
