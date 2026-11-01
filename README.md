# Survivor Lab

Unity로 만든 싱글 플레이 생존 액션 게임입니다. 적을 피하며 경험치를 모으고, 고유 주문 하나를 강화해 5분 동안 생존하세요. 직접 플레이와 AI 관전을 지원합니다.

![AI 플레이 화면](docs/media/gameplay.png)

[플레이 영상 보기 · MP4](docs/media/gameplay.mp4)

![플레이 미리보기](docs/media/gameplay.gif)

실제 Windows 빌드의 AI 관전 화면입니다. 영상은 게임 속도 2배 설정으로 촬영했으며, 경험치를 주입하지 않았습니다. GitHub에서 MP4가 바로 재생되지 않으면 파일 페이지에서 다운로드해 볼 수 있습니다.

## 실행

Windows x64 빌드는 저장소의 **Releases**에서 ZIP으로 제공합니다. 압축을 모두 푼 뒤 `SurvivorLab.exe`를 실행하세요. 실행 파일과 `SurvivorLab_Data`, `MonoBleedingEdge`, DLL 파일은 같은 폴더에 유지해야 합니다.

소스만 받은 경우 아래 개발 방법으로 먼저 빌드하세요. 빌드가 있으면 `play.cmd`로 실행할 수 있습니다.

| 조작 | 기능 |
|---|---|
| WASD / 방향키 | 이동 · 공격은 자동 |
| 마우스 / 숫자 1·2·3 | 레벨업 보상 선택 |
| Space | 일시 정지 / 재개 |
| Esc | 메뉴 |
| WATCH AI / AI 관전 | 이동과 보상을 AI가 자동 선택 |
| Speed | 1·2·4배속 |

다른 창으로 전환하면 일시 정지됩니다. 돌아와서 Space를 누르면 재개합니다.

## 플레이

- **공명술사**: 주변에 원형 파동을 발생시킵니다.
- **유도술사**: 가까운 적을 조준해 마법탄을 발사합니다.
- **각인술사**: 이동한 자리에 돌을 남깁니다.
- **충격술사**: 마지막 이동 방향으로 전방 충격파를 사용합니다.

파란 경험치를 모으면 레벨이 오릅니다. 레벨업마다 보상 3개 중 하나를 선택하고, 레벨 7·14에는 속성을 추가합니다. 화염·번개·산성·냉기·암흑을 중복 강화하거나 조합할 수 있습니다. 60초마다 두루마리 보상이 제공됩니다.

## AI 관전

메뉴에서 클래스를 고르고 **WATCH AI**를 누르세요. 보상도 자동 선택하므로 관전 중 수동 입력이 필요하지 않습니다.

- 이동: 표 기반 Q-learning 정책.
- 보상 선택: 사전 학습한 REINFORCE 정책. `판단` 버튼으로 규칙 기반 선택과 전환할 수 있습니다.
- `보상 자동`을 끄면 보상을 직접 선택할 수 있습니다.
- `TRAIN +400 episodes`는 이동 정책을 추가 학습합니다. 보상 정책을 재학습하는 버튼은 아닙니다.
- 학습 정책은 모든 클래스의 생존 성공을 보장하지 않습니다.

사용자 이동 정책은 `%USERPROFILE%\AppData\LocalLow\Independent Lab\Survivor Lab\policy-v2.json`에 저장됩니다. 이 파일을 별도로 옮기면 다음 실행에서 번들 초기 정책을 사용합니다.

## 개발 · 빌드

Unity **6000.0.74f1**, Windows x64, 기본 렌더 파이프라인을 사용합니다. Unity Hub에서 해당 Editor와 Windows 빌드 모듈을 설치하고 프로젝트를 추가하세요.

```bat
open-unity.cmd
build.cmd --verify
build.cmd
play.cmd
```

Editor 설치 경로가 다르면 `UNITY_EDITOR` 환경 변수에 `Unity.exe`의 전체 경로를 지정하세요. Unity Hub 기본 설치 위치와 로컬 `Tools` 위치도 자동으로 확인합니다.

기본 빌드는 개발용 진단 기능을 제외한 배포용 빌드입니다. `build.cmd --development`로 만든 빌드에서는 `SurvivorLab.exe -batchmode -nographics --qa` 검증을 실행할 수 있습니다. 빌드 과정에서도 동일한 게임 규칙 검증을 실행하며, 로그는 `Logs/build.log`에 저장됩니다.

| 위치 | 내용 |
|---|---|
| `Assets/Scripts/Simulation.cs` | 전투 · 성장 · 보상 |
| `Assets/Scripts/ElementEffects.cs` | 속성 · 상태 이상 |
| `Assets/Scripts/GameApp.cs` | 입력 · UI · 렌더링 |
| `Assets/Scripts/Learning.cs` | 이동 학습 |
| `Assets/Scripts/RewardLearning.cs` | 보상 선택 정책 |
| `Assets/StreamingAssets/balance.json` | 플레이 수치 설정 |
| `ArtSource/` | 픽셀 아트 생성 스크립트 |

수치 설정을 바꾸면 기존 학습 정책의 성능도 달라질 수 있습니다. 아트 구성과 재생성 방법은 [ART.md](ART.md)를 참고하세요.
