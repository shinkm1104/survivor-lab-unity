# Survivor Lab · Windows

이 폴더 전체를 유지하고 `SurvivorLab.exe`를 실행하세요. Unity Editor 설치는 필요하지 않습니다.

1. 클래스 하나를 선택합니다.
2. PLAY: WASD / 방향키로 이동합니다. 공격은 자동입니다.
3. 파란 경험치를 모으면 레벨이 오릅니다. 보상 3개 중 하나를 마우스 또는 숫자 1·2·3으로 선택하세요.
4. 레벨 7·14에 속성을 선택합니다. 60초마다 두루마리 보상이 나옵니다.
5. 5분 동안 생존하면 성공입니다.

WATCH AI: 이동·레벨업·속성·두루마리를 모두 자동으로 선택합니다. `판단`으로 학습 정책과 규칙 기반 판단을 전환하고, `보상 자동`을 끄면 보상만 직접 선택할 수 있습니다.

Space: 일시 정지 / 재개. Esc: 메뉴. Speed: 1·2·4배속. 다른 창으로 전환하면 일시 정지되므로 돌아온 뒤 Space를 누르세요.

TRAIN +400은 이동 정책을 추가 학습합니다. STOP & SAVE로 중단·저장합니다. 보상 선택 정책은 이 버튼으로 학습하지 않습니다.

사용자 정책: `%USERPROFILE%\AppData\LocalLow\Independent Lab\Survivor Lab\policy-v2.json`.
설정: `SurvivorLab_Data/StreamingAssets/balance.json`.

실행 파일만 따로 옮기면 실행할 수 없습니다. 전체 폴더를 유지하세요.
