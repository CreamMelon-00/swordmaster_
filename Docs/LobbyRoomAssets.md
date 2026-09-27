# 로비의 방 배경

레거시 `OutGame.unity`에서 실제 사용한 도트 방 그림을 그대로 계승했다. 새로 그리거나 레거시 씬·컴포넌트를 이식하지 않고 배경 PNG만 복사한다.

- 원본: `Turn_Limbo/Assets/Resource/Sprite/pa_background_-_out_game_variation_1-2.png`
- 원본 GUID: `f4d5cd9cb26f8804d8cec09131bf41be`
- 확인 근거: `Turn_Limbo/Assets/Scenes/OutGame.unity`의 `BackGround` GameObject(`827760295`)에 붙은 Image(`827760297`), `m_Sprite` 줄 6752. 당시 표시 크기는 2880 × 1620이었다.
- 실제 이미지: 2400 × 1350(16:9), 침대·책장·노트북·냉장고가 있는 푸른 밤색 방. 이미지 가장자리에 건물 벽·창문이 포함된 완성 배경이며 스프라이트 시트나 분할 레이어가 아니다.
- 새 위치: `Assets/Game/Resources/LobbyRoom/room.png`
- 런타임 키: `Resources.Load<Sprite>("LobbyRoom/room")`
- 새 GUID: `949c1aace5574f0486b7a0dad975d5a8`

Single Sprite / Full Rect / Point / Clamp / sRGB / 원본 알파 / 압축 없음 / Mipmap 없음으로 가져온다. 최대 크기는 4096으로 설정하여 레거시의 2048 설정 때문에 2400픽셀 원본이 축소되는 문제를 피한다. uGUI 배경은 원본의 16:9 종횡비를 유지해 표시한다. 별도 그림 조합이나 수동 Inspector 연결은 필요 없다.

원본 파일과 원본 메타데이터는 수정하지 않았다. 복사한 PNG의 SHA-256은 원본과 같다:

`4083B8DC8637B33D022C21805F0865C7D77D14B9B38CDB030E1E850794DF7004`

이 배경은 기존 프로젝트 리소스를 재사용한 것으로, 이번 작업에서 별도 라이선스 조사나 출시 권리 확인은 수행하지 않았다.
