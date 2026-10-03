# 탐색·몰아치기 기술 아이콘

| 스킬 | 그림 ID | 게임 리소스 | Aseprite 원본 |
| --- | ---: | --- | --- |
| 탐색 | 16 | `Assets/Game/Resources/SkillRoles/role16.png` | `Explore.aseprite` |
| 몰아치기 | 17 | `Assets/Game/Resources/SkillRoles/role17.png` | `Barrage.aseprite` |

기존 `skill-role-atlas.png` 15개는 그대로 둔다. 새 그림은 별도 Sprite로 로드한다. 탐색은 방어 계열의 올리브색 테두리와 눈, 작은 청록색 ACT 회복 표식이다. 몰아치기는 참격 계열의 구리색 마름모 테두리와 검, 연속 베기 궤적이다. 무기와 문양은 기술의 판정이나 수치를 대신하지 않는다.

내장 ImageGen으로 기존 아틀라스를 스타일 참고 이미지로 사용해 각 아이콘을 별도로 생성했다. 생성본을 Aseprite에서 25%로 축소해 313×313 투명 PNG와 편집 가능한 `.aseprite` 원본으로 저장했다. `Explore-56.png`와 `Barrage-56.png`는 실제 버튼 크기 판독용 미리보기다. Unity 임포트는 Point / Clamp / 무압축 / Mipmap 없음 / 중앙 pivot을 사용한다.

## 생성 프롬프트

### 탐색

> Use case: stylized-concept. Asset type: ONE standalone transparent PNG skill icon for a Unity pixel-art sword duel game; the supplied atlas is STYLE REFERENCE ONLY, not an edit target. Create a NEW icon for the Korean skill '탐색' (scouting), without rendering any text. Match the existing atlas icon scale and visual language exactly: centered metal-framed badge, hard stepped pixel clusters, broad readable shapes, ivory highlights, restrained amber/teal accents, small game-button readability. Use the OLIVE GREEN shield-shaped metal BORDER and very dark green leather interior from the defense icons, but NO physical shield prop. In the center draw ONE large unmistakable open EYE, ivory eyelids and bright small teal iris/pupil, watchful rather than menacing. At lower right add a tiny separate turquoise curved return arrow around a diamond glint to hint next-turn ACT recovery, subordinate to the eye. One icon only, front facing, full badge in frame with transparent margin around silhouette, no cast shadow outside silhouette, no UI mockup, no labels, no letters or numbers, no extra icons, no sword. True clean transparent alpha outside border. Keep details bold enough at 48px.

### 몰아치기

> Use case: stylized-concept. Asset type: ONE standalone transparent PNG skill icon for a Unity pixel-art sword duel game; the supplied atlas is STYLE REFERENCE ONLY, not an edit target. Create a NEW icon for the Korean skill '몰아치기' (relentless four-hit sword barrage), without rendering any text. Match the existing atlas icon scale and visual language exactly: centered COPPER-ORANGE diamond metal border like the slash icons, dark brown leather interior, chunky hard-edged pixel-art shading, broad ivory silver blade, warm orange slash trails, restrained palette. Inside put ONE clear diagonal silver SWORD with crossguard and THREE thick staggered orange slash arcs sweeping behind it to convey four rapid hits; a small fractured golden impact spark at the tip suggests a broken enemy. Motion should be energetic yet legible at 48px and distinct from the simpler two-sword role11 badge. One icon only, front facing, full badge in frame with transparent margin around silhouette, no cast shadow outside silhouette, no UI mockup, no labels, no letters or numbers, no other weapons. True clean transparent alpha outside border.
