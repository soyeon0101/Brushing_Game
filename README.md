# 닦아줘! 프렌즈 (Brushy Galaxy)

## 구현된 기능

- **첫 실행**: 타이틀, 나이 선택, 캐릭터 선택(공주/왕자), 닉네임 입력
- **로비**: 프로필, 오늘의 스티커, 오늘의 환자
- **치료 흐름**: 오늘의 환자, 카메라 준비(3초 카운트다운), 로딩, 4구역 양치, 치료 완료(전/후 비교)
- **양치**: 구역별 줌인, 요정 vs 충치균 전투, 구역 타이머, 응원/멈춤/과속 안내, 손 인식 실패 안내, 카메라 작은 창, 일시정지, 소리 켜기/끄기
- **스티커판**: 주간 × 아침/점심/저녁, 도장 연출
- **환자 도감**: 치료한 환자 열림
- **보호자 설정**: 소리, 음성 안내, 구역당 양치 시간(20/25/30초), 기록 초기화
- **저장**: 프로필, 설정, 스티커, 도감, 치료 횟수
- **손 인식**: 전면 카메라, 양치/과속 판정
- **키보드 테스트 입력**: F2로 전환 (Space 양치, Shift 과속, H 손 숨김)

## 코드 구조

```
[MediaPipe 결과 콜백]
  → HandFrameBuffer    스레드 안전 좌표 버퍼
  → BrushDetector      흔들림 판정 (방향 반전 → 양치 / 과속 / 칫솔질 횟수)
  → IBrushInput        CameraBrushInput / KeyboardBrushInput
  → BrushInputHub      화면들이 보는 단일 입력 (F2로 전환)
  → BrushingScreen     구역 진행, 안내 문구
      → MouthZoom / BrushZoneView / BattleView   줌, 구역 연출, 전투 연출

GameFlow  화면 전환의 유일한 기준 (화면은 버튼 이벤트만 알림)
SaveStore 저장 파일 읽기/쓰기
```

| 폴더 (`Assets/BrushGame/`) | 내용 |
|---|---|
| `Scripts/Core` | 화면 흐름(`GameFlow`), 콘텐츠 목록(`GameCatalog`), 저장 |
| `Scripts/Screens` | 화면별 스크립트, 보호자 설정 창 |
| `Scripts/Game` | 양치 화면 연출 (줌, 구역, 전투) |
| `Scripts/HandTracking` | MediaPipe Runner, 좌표 버퍼, 양치 판정, 개발용 표시 |
| `Scripts/Input` | 입력 인터페이스, 키보드 입력, 입력 전환 |
| `Scripts/UI` | 그림 자리, 카메라 영상 맞춤, 세로 화면/safe area, 공용 UI |
| `Editor` | 씬 생성, UI 스킨, 폰트, 크로마키 처리, 그림 가져오기 설정 |
| `Data` | `GameCatalog.asset` (캐릭터, 환자 목록) |
| `Art`, `Fonts` | 그림, 폰트 |

## 주요 조정 값

판정 값은 임시 값이며, 아이들로 다시 측정한 뒤 확정해야 합니다.

| 위치 | 값 | 현재 |
|---|---|---|
| 보호자 설정 | 구역당 양치 시간 | 25초 (20/25/30) |
| `BrushingScreen` | 과속 중 진행 속도 배율 | 0.4 |
| `BrushingScreen` | 손 안 보임 안내까지 시간 | 1.5초 |
| `BrushingScreen` | 반짝이 한 번에 필요한 칫솔질(방향 전환) | 4 |
| `BrushDetector` | 양치 기준 / 과속 기준 (초당 방향 반전) | 1.5 / 10 |
| `BrushDetector` | 이보다 작은 손 무시 | 0.08 |
