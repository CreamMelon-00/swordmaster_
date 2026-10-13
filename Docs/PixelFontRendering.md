# UI 도트 폰트 선명도

## 확인한 원인 (2026-10-13)

프로젝트의 UI는 모두 `LegacyDuel/Font/neodgm`을 사용한다. NeoDunggeunmo 원본은 16px 격자이고, 임포트 설정은 이미 Dynamic·HintedRaster다. [Unity의 HintedRaster 정의](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/fontrenderingmode)에 따라 글리프에는 앤티앨리어싱을 사용하지 않는다.

문제는 동적 폰트 텍스처의 기본 Bilinear 필터였다. uGUI는 Canvas 배율에 맞춰 글리프를 생성하지만 부모 패널의 `localScale`은 이 배율에 들어가지 않는다. 장부·회상 카드·각종 연출처럼 텍스트의 부모를 확대하면 텍스처를 보간하면서 윤곽이 흐려진다.

Unity 6000.5.9f1 GPU 비교에서 같은 한글·숫자·기호를 1920×1080, 1600×900, 1280×720으로 렌더링했다. 부모 배율 1.0은 두 필터가 동일했고, 1.2는 Bilinear에서 글자 가장자리의 중간색이 생겼다. Point에서는 세 해상도 모두 중간색이 없어졌다. 진단 그림과 수치는 버전 관리에서 제외한 `Logs/FontInvestigation/`에 보관한다.

## 적용

`PixelFontRendering.LoadUiFont()`에서 공통 폰트를 읽고 텍스처에 Point 필터를 적용한다. `Font.textureRebuilt`에서 다시 적용하므로 새 한글, 글자 크기와 스타일로 동적 텍스처가 갱신되어도 선명도가 유지된다. 도메인 재시작을 끈 에디터에서는 SubsystemRegistration 초기화로 정적 이벤트 구독이 중복되지 않게 한다.

`LegacyDuelArt`와 `LobbyStoryLauncher`는 이 로더를 함께 사용한다. 로딩 화면도 다른 HUD처럼 Canvas의 pixelPerfect를 켠다. 폰트 파일·글자 크기·Best Fit 범위·패널 배치는 유지한다.

아주 작은 글씨가 낮은 해상도에서 몇 픽셀로 줄어 읽기 어려운 문제는 별도의 레이아웃 문제다. 이번 변경은 확대된 텍스트의 흐린 윤곽을 제거한다.

## 회귀 검증

Unity 컴파일 오류 0개. 선택한 PlayMode 검사 7개 중 6개가 통과했다. 새 폰트의 공유 로드·실제 동적 텍스처 재생성 2개와 스킬 카드 자동 축소·키워드 팝업·맞물림 수치·로딩 화면 검사다.

전 스킬 카드 높이 검사는 두블레의 플레이어 설명에서 허용 90px 대비 preferredHeight 107px로 실패했다. 원래 Bilinear 설정으로 같은 검사를 실행해 동일한 실패를 확인했으므로 이번 변경의 회귀가 아니다. 결과는 `Logs/FontInvestigation/FinalPlayMode.xml`, 대조 검사는 `BaselineLayoutPlayMode.xml`에 보관한다.
