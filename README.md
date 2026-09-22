# PLANET_SHOT

같은 종류의 행성을 합쳐 더 큰 행성을 만들고 높은 점수에 도전하는 Unity 2D 게임입니다.
프로젝트와 저장소 이름은 **PLANET_SHOT**입니다.

[웹에서 플레이하기](https://jin031009.itch.io/planet-shot)

## 주요 기능

- 마우스로 조준하고 행성을 발사하는 물리 기반 합치기 게임
- 발사대 이동, 블랙홀 능력, 특수 행성과 효과음
- 시작 화면에서 목숨 수와 무한 모드 설정
- 배경음악·효과음 볼륨 조절
- 기기·브라우저별 로컬 점수 기록 (`PlayerPrefs` 사용)

## 실행 방법

1. 이 저장소를 복제하거나 ZIP으로 내려받습니다.
2. Unity Hub에서 저장소 폴더를 프로젝트로 추가합니다.
3. **Unity 2022.3.20f1**로 열고 패키지와 에셋 가져오기가 끝날 때까지 기다립니다.
4. `Assets/Scenes/HomeScene.unity`를 열고 Play를 누릅니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| 마우스 왼쪽 버튼 드래그 후 놓기 | 조준 후 발사 |
| W / A / S / D | 발사대 이동 |
| Q | 필드의 사용 가능한 블랙홀 능력 발동 |
| R | 현재 게임 다시 시작 |
| 게임 종료 후 Esc | 시작 화면으로 돌아가기 |

## 프로젝트 구성

| 경로 | 내용 |
| --- | --- |
| `Assets/Scripts/BowlingSuikaGame.cs` | 게임 진행, UI, 물리 상호작용, 오디오 및 점수 기록 |
| `Assets/Scenes/` | 시작 화면과 게임 장면 |
| `Assets/Resources/` | 실행 중 불러오는 이미지, 음원, 폰트, 프리팹 |
| `Assets/Editor/` | 프리팹 구성 및 WebGL 빌드 도구 |
| `Packages/` | Unity 패키지 의존성 |
| `ProjectSettings/` | Unity 버전과 프로젝트 설정 |

## WebGL 빌드

Unity Hub에서 해당 에디터의 WebGL Build Support를 설치합니다.
Unity의 Build Settings에서 WebGL을 선택하고 `HomeScene`, `SampleScene` 순서로 장면을 포함해 빌드합니다.
자동화용 `WebGLBuild.Build`도 같은 장면들을 사용하며 결과를 `Builds/WebGL`에 만듭니다.

## 로컬 작업 폴더 구분

- `GameRepository/PLANET_SHOT`: 이 저장소에 해당하는 Unity 원본 프로젝트입니다.
- 바탕화면의 `PS`: WebGL 실행 파일과 버전별 배포 ZIP, 일부 작업용 음원이 있는 폴더입니다.
- 바탕화면의 `PS_tool`: 이미지·음원·폰트 등 제작 재료를 모아 둔 폴더입니다. 프로젝트에서 사용하는 파일은 `Assets`에 포함합니다.

`PS`와 `PS_tool`은 이 프로젝트를 여는 데 필요한 별도 경로로 연결되어 있지 않습니다.
Unity가 다시 생성하는 `Library`, 로컬 빌드 결과 `Planet_shot`, 녹화물 `Recordings`, 로그는 Git에서 제외합니다.
