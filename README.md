# 외계인 치과 양치 게임 (brushing-game)

4~8세 아이를 위한 양치 게임 프로토타입입니다. 아이는 우주선 치과의 외계인 치과의사가 되어, 찾아온 외계인 환자의 이를 **실제 양치 동작**으로 닦아 줍니다.

- 스마트폰을 **세로로 세워 두고**, 전면 카메라 앞에서 실제 칫솔로 양치하면서 플레이합니다.
- 입력은 **칫솔을 쥔 손의 흔들림 하나뿐**입니다. 얼굴 인식이나 닦는 위치(위·아래·좌·우) 판정은 하지 않습니다.
- 손 인식은 [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin)의 Hand Landmark Detection을 사용합니다.

## 게임 흐름

1. 외계인 환자가 통통 튀며 들어와 "아~" 하고 입을 벌립니다.
2. 양치를 하면 입속에 거품이 생기고, 치태·음식 찌꺼기·세균이 하나씩 사라집니다.
   - 치태: 거품에 덮였다가 걷히며 깨끗한 이가 드러남
   - 음식 찌꺼기: 흔들리다 톡 떨어짐
   - 세균: 거품 방울에 갇혀 날아감
3. 너무 빠르게 문지르면 환자가 "아야야~ 살살!" 하며 아파하고, 치료 속도가 느려집니다.
4. 손이 1.5초 이상 안 보이면 진행이 멈추고 "칫솔이 안 보여!"라고 알려 줍니다. 벌점은 없습니다.
5. 다 닦으면 이 전체가 반짝이고, 환자가 기뻐하며 손을 흔들고 나갑니다. 이어서 다음 환자가 들어옵니다.

오염물이 사라지는 순서와 간격은 환자마다 무작위로 섞입니다. 화면에서 특정 이가 먼저 깨끗해지는 것은 진행 상황을 보여 주는 연출이며, 실제로 닦는 위치와는 관계없습니다.

## 실행 방법

### 요구 사항

- Unity **6000.0.58f2** (Unity 6)
- 카메라 (PC 웹캠 또는 안드로이드 기기의 전면 카메라)

### 에디터에서 실행

1. 이 저장소를 클론하고 Unity Hub에서 프로젝트 폴더를 엽니다. 첫 실행 때 패키지 가져오기에 시간이 걸립니다.
2. `Assets/BrushGame/Scenes/BrushPrototype_v2.unity` 씬을 엽니다.
3. Game 창 해상도를 **1080x1920(세로)** 으로 설정합니다. 메뉴 `BrushGame → Fix Portrait View`를 누르면 자동으로 설정됩니다.
4. 플레이합니다. Windows 에디터에서는 CPU로 손을 인식합니다.

### 키보드로 테스트

카메라 없이 테스트하려면 `Game` 오브젝트의 `SessionManager`에서 입력(`Brush Input Source`)을 같은 오브젝트의 `KeyboardBrushInput`으로 바꿉니다.

| 키 | 동작 |
|---|---|
| Space (누르고 있기) | 정상 양치 |
| Space + Shift | 과속 양치 |
| H (누르고 있기) | 손이 안 보이는 상태 (다른 키보다 우선) |
| F1 | 개발용 표시 켜기/끄기 (카메라 미리보기, 판정 수치) |

### 안드로이드 빌드

- 빌드 씬 목록의 0번이 `BrushPrototype_v2`인지 확인합니다.
- 화면 방향은 세로(Portrait)로 고정되어 있습니다. 9:16보다 넓은 화면에서는 양옆에 여백이 생깁니다.
- 처음 실행할 때 카메라 권한을 허용해야 합니다. 전면 카메라가 자동으로 선택됩니다.
- 실행 직후 `DllNotFoundException`으로 앱이 종료되면 `libstdc++_shared.so`를 APK에 넣어야 합니다. 자세한 방법은 [MediaPipeUnityPlugin-README.md](MediaPipeUnityPlugin-README.md)의 Android 항목을 참고하세요.
- 폰을 50~70cm 정도 떨어뜨리고 가슴 높이에 두어야, 입 근처에서 칫솔을 쥔 손이 화면에 들어옵니다.

## 코드 구조

게임 코드는 전부 `Assets/BrushGame/` 아래에 있습니다. MediaPipe 샘플 원본은 수정하지 않았습니다.

```
[MediaPipe 결과 콜백]  (메인 스레드가 아닐 수 있음. 좌표만 복사)
  → HandFrameBuffer     스레드 안전 좌표 버퍼
  → BrushDetector       순수 C# 흔들림 판정 (방향 반전 횟수 → StrokeRate)
  → IBrushInput         CameraBrushInput / KeyboardBrushInput 교체 가능
  → SessionManager      환자 흐름과 치료 진행도의 유일한 기준
      → Patient / MouthView            환자 표정, 입속 연출
      → PatientReactionController      어떤 대사를 띄울지, 쿨타임
          → ReactionToastUI            상단 알림 (등장 0.5초, 유지 3초, 퇴장 0.6초)
```

| 폴더 | 내용 |
|---|---|
| `Scripts/HandTracking` | MediaPipe Runner, 좌표 버퍼, 흔들림 판정, 디버그 표시 |
| `Scripts/Input` | `IBrushInput`, 키보드 입력 |
| `Scripts/Game` | 세션 흐름, 환자, 입속 연출, 치료 일정(`TreatmentPlan`), 대사 |
| `Scripts/UI` | 상단 알림, 세로 화면 카메라, safe area |
| `Editor` | 씬 생성 메뉴, 세로 화면 설정 복구 메뉴 |

### 주요 조정 값

인스펙터에서 바꿀 수 있습니다.

| 위치 | 값 | 현재 |
|---|---|---|
| `SessionManager` | 치료 완료까지 정상 양치 시간 | 약 12초 |
| `SessionManager` | 과속 중 진행 속도 배율 | 0.4 |
| `CameraBrushInput` | 닦는 중 기준 / 과속 기준 (초당 방향 반전 수) | 1.5 / 10 |
| `CameraBrushInput` | 이보다 작은 손 무시 (`minHandSize`) | 0.08 |
| `BrushHandLandmarkerRunner` | 동시에 인식할 손 개수 (부모가 도와주는 경우 포함) | 2 |

판정 값은 어른 한 명이 측정한 임시 값입니다. 아이들로 다시 측정한 뒤 확정해야 합니다.

## 현재 상태와 한계

- 그래픽은 모두 임시 도형입니다 (`Assets/BrushGame/Art/Placeholder`).
- 칫솔을 손등이 옆을 향하게 쥐고 손목만 까딱이며 닦으면 움직임이 작게 잡혀 판정이 약해질 수 있습니다.
- 매우 빠르게 문지르면 영상이 흐려져 손을 잠깐씩 놓칩니다. 0.3초까지는 보이는 것으로 처리합니다.
- `Assets/BrushGame/Scenes/BrushPrototype.unity`는 이전 버전 씬이며 현재 코드와 맞지 않습니다.

## 크레딧 / 라이선스

- 이 프로젝트는 [homuler/MediaPipeUnityPlugin](https://github.com/homuler/MediaPipeUnityPlugin) v0.16.3 (MIT License)을 기반으로 합니다. 플러그인의 라이선스는 [LICENSE](LICENSE), 서드파티 고지는 [Third Party Notices.md](Third%20Party%20Notices.md), 원본 설명서는 [MediaPipeUnityPlugin-README.md](MediaPipeUnityPlugin-README.md)에 있습니다.
- 손 인식 모델: Google MediaPipe Hand Landmarker
