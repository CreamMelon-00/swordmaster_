# 로비의 방 배경

타이틀과 로비는 같은 임시 오두막을 그린 아침·낮·저녁·밤 배경을 사용한다. 네 PNG는 모두 1920 × 1080(16:9)이며, 편집용 Aseprite 파일과 제작 기록은 [`Art/LobbyRoom/Concepts/README.md`](Art/LobbyRoom/Concepts/README.md)에 있다.

| 시간대 | 게임 리소스 | 런타임 키 |
| --- | --- | --- |
| 아침 | `Assets/Game/Resources/LobbyRoom/morning.png` | `LobbyRoom/morning` |
| 낮 | `Assets/Game/Resources/LobbyRoom/day.png` | `LobbyRoom/day` |
| 저녁 | `Assets/Game/Resources/LobbyRoom/evening.png` | `LobbyRoom/evening` |
| 밤 | `Assets/Game/Resources/LobbyRoom/night.png` | `LobbyRoom/night` |

`DuelPrototypeController`가 시작할 때 `LobbyRoomBackdrop.PickRandom()`으로 네 장 중 하나를 무작위로 한 번 고른다. 고른 스프라이트를 `TitleHud`와 `CampaignLobbyHud`에 함께 전달하므로 같은 실행 중에는 타이틀과 로비가 동일한 시간대를 보여 준다. 로비 탭을 바꾸거나 전투에 갔다 돌아와도 다시 뽑지 않는다. 실제 시각이나 저장 데이터에 시간대를 연결하지는 않았다.

네 장 모두 Unity에서 Single Sprite / Full Rect / Point / Clamp / sRGB / 원본 알파 / 압축 없음 / Mipmap 없음으로 가져온다. 최대 텍스처 크기는 4096이다. uGUI 배경은 원본 종횡비를 유지하며 화면을 채운다. 별도 레이어 조합이나 Inspector에서 수동 스프라이트 연결은 필요 없다.

## 이전 배경

`Assets/Game/Resources/LobbyRoom/room.png`는 이전 방 배경으로 남겨 두었다. 현재 이미지 해상도는 1672 × 941이며, 활성 타이틀·로비 화면에서는 읽지 않는다. 이전 문서에서는 레거시 `OutGame.unity`의 `BackGround` Image가 사용한 `Turn_Limbo/Assets/Resource/Sprite/pa_background_-_out_game_variation_1-2.png`에서 가져온 그림이라고 기록했다. 이력 보존용 리소스이므로 새 네 장의 선택 후보에는 넣지 않았다.
