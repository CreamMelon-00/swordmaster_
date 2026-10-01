# 새벽 숲 픽셀 정리 도구

`Build-Layer.ps1`은 `Candidates/<이름>.png`를 Aseprite 1.2.34 Lua로 처리해 `Processed/<이름>.aseprite`와 `Processed/<이름>.png`를 만든다. 원본 후보나 게임의 `Assets/Game` 파일은 수정하지 않는다.

## 실행

프로젝트 루트에서 PowerShell로 실행한다. 설치 위치가 바뀌면 `-AsepritePath`를 지정한다.

```powershell
& .\Docs\Art\DawnForest\Tools\Build-Layer.ps1 -Name forest-far-mist -Opaque
& .\Docs\Art\DawnForest\Tools\Build-Layer.ps1 -Name forest-far-trees
& .\Docs\Art\DawnForest\Tools\Build-Layer.ps1 -Name forest-belt-mid
& .\Docs\Art\DawnForest\Tools\Build-Layer.ps1 -Name forest-near
& .\Docs\Art\DawnForest\Tools\Build-Layer.ps1 -Name forest-far -Opaque
```

다른 후보를 처리할 때는 `-Name`과 필요하면 `-InputPath`, `-Colors 64`, `-OutputDirectory`를 지정한다. 완전히 불투명해야 하는 배경은 `-Opaque`를 붙인다. `forest-far.png` 후보는 승인된 전체 시안의 프로젝트 내 사본이다.

## 처리 규칙

- 2172×724 입력의 각 4×4 픽셀을 알파 가중 평균으로 543×181 작업 격자 한 픽셀에 모은다.
- 가중 알파가 절반 이상인 블록만 불투명하게 만든다. 출력 알파는 0 또는 255만 쓴다.
- 5비트 RGB 히스토그램에서 가중 중앙 분할로 최대 64색 팔레트를 만들고, 색을 가장 가까운 팔레트 값으로 바꾼다. 디더링은 쓰지 않는다.
- Aseprite 원본에는 543×181 RGB 레이어와 팔레트를 저장한다. PNG는 각 작업 픽셀을 정확히 4×4 같은 색 블록으로 복제한다.
- 저장된 PNG를 다시 열어 1,572,528픽셀 전부가 작업 격자의 4배 블록과 일치하는지 검사한다. 각 결과의 `.verify.txt`에 픽셀 수, 팔레트 수, 알파 및 레이어별 세로 범위를 기록한다.

## 현재 결과

| 이름 | 팔레트 | 투명 작업 픽셀 | 세로 형태 확인 |
| --- | ---: | ---: | --- |
| `forest-far-mist` | 64색 | 0 | 전체 불투명 |
| `forest-far-trees` | 64색 | 52,688 | 마지막 나무 행 121/180 |
| `forest-belt-mid` | 46색 | 20,698 | 93/180행부터 바닥이 아래까지 불투명 |
| `forest-near` | 52색 | 82,939 | 첫 근경 행 144/180 |
| `forest-far` | 64색 | 0 | 전체 불투명 |

모든 PNG는 2172×724이며 저장 후 검사에 통과했다. `.aseprite` 원본은 각 레이어의 향후 수정을 위한 기준 파일이다.

적용 후에도 `& .\Docs\Art\DawnForest\Tools\verify-forest.ps1 -Directory 'Assets\Game\Resources\ForestArena'`로 게임 리소스 5장의 크기·4×4 격자·색상 수·알파와 레이어 영역을 다시 확인할 수 있다. Aseprite에서 원본 5장을 각각 다시 PNG로 내보내 543×181 작업 픽셀 전부가 최종 PNG의 4배 격자와 일치하는 것도 확인했다.
