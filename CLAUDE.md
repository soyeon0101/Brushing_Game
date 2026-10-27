# 양치 게임 개발 지침서

이 파일은 Unity 프로젝트 루트에 두고 작업 기준으로 삼는다. (Claude Code는 이 파일을 세션 시작 시 자동으로 읽는다.)

## 1. 프로젝트 개요

- 대상: 4~8세 아동용 양치 게임
- 설정: 외계인 치과의사가 양치로 환자를 치료하는 타이쿤풍 진행
- 입력: **칫솔을 쥔 손의 흔들림 하나뿐**. 얼굴 인식은 쓰지 않는다.
- 판정하는 것: (1) 지금 닦고 있는가 (2) 너무 빠르게 문지르는가
- 판정하지 않는 것: 닦는 위치(위/아래/좌/우), 꼼꼼함. 위치 판정·위치 안내 UI는 만들지 않는다.

## 2. 개발 환경

- Unity 6 (6000.0.58f2)
- homuler/MediaPipeUnityPlugin v0.16.3 — `MediaPipeUnityPlugin-all.zip`을 풀어서 **그 폴더 자체를 Unity 프로젝트로 연다**
- VS Code + MCP for Unity
- Windows 에디터는 **CPU 추론만 가능** (GPU 모드는 Windows/macOS 미지원)
- 에디터 테스트: Asset Loader Type = `Local` / 기기 빌드: `StreamingAssets`로 변경

## 3. 사용할 솔루션

| 샘플 | 사용 여부 | 이유 |
|---|---|---|
| Hand Landmark Detection | **사용** | 손 21개 점 제공. 손목(0번)과 손 크기 계산에 필요한 점이 다 있음 |
| Pose Landmark Detection | 미사용 | 몸 전체 단위라 손 움직임이 거칠게 잡힘 |
| Holistic | 미사용 | 얼굴·몸·손 모델을 전부 돌려서 무거움. 레거시 솔루션(지원 종료) |

- **Num Hands 기본값은 1**이다. 샘플에서 한 손만 잡히는 건 고정이 아니라 기본값 때문. **2로 바꾼다.** (샘플 설정 패널 또는 인스펙터에서 확인)
- 2로 두는 이유: 부모가 대신 닦아 주거나 아이 손을 잡고 같이 닦는 경우도 인정하기 위해.

## 4. AI 작업 규칙 (중요)

- **플러그인 API를 추측해서 쓰지 말 것.** 이 플러그인은 버전마다 API가 크게 바뀌었다. 반드시 `Assets/MediaPipeUnity/Samples/Scenes/` 안 Hand Landmark Detection 샘플의 실제 스크립트(Runner 계열)를 먼저 읽고, 결과 콜백과 결과 타입을 그대로 따른다.
- 샘플 원본 파일은 직접 수정하지 말고 복제해서 쓴다.
- 판정 로직(`BrushDetector`)은 플러그인에 의존하지 않는 순수 C#으로 작성한다. 플러그인과 맞닿는 코드는 좌표를 넘겨주는 얇은 층으로만 둔다.
- LiveStream 모드 결과 콜백은 메인 스레드가 아닐 수 있다. 콜백 안에서 Unity 오브젝트를 건드리지 말고, 좌표만 복사해 버퍼에 넣은 뒤 `Update()`에서 처리한다.
- Windows 에디터는 네이티브 오류 시 통째로 크래시할 수 있다. 자주 저장한다.

## 5. 구조

```
[MediaPipe 결과 콜백] → HandFrameBuffer (스레드 안전 복사)
        ↓ Update()
   BrushDetector (순수 C#)
        ↓
   IBrushInput  ←── KeyboardBrushInput (개발용: Space=닦기, Shift=과속)
        ↓
   게임 로직 (SessionManager / Patient)
```

```csharp
public interface IBrushInput
{
    bool IsBrushing { get; }   // 지금 닦는 중인가
    bool IsTooFast  { get; }   // 과속 상태인가
    float StrokeRate { get; }  // 초당 왕복 반전 횟수 (디버그용)
    bool HandVisible { get; }  // 손이 하나라도 보이는가
}
```

- 게임 로직은 `IBrushInput`만 본다. 카메라 판정이 완성되기 전에는 `KeyboardBrushInput`으로 게임을 먼저 만든다.

## 6. 흔들림 판정 (BrushDetector)

손마다 아래를 수행하고, **하나라도 닦는 중이면 닦는 중**으로 본다.

1. **손 크기**: `s = |점0(손목) - 점9(가운데 손가락 뿌리)|`
2. **먼 손 무시**: `s < minHandSize`이면 제외 (뒤에서 지나가는 사람 손 차단)
3. **손 짝짓기**: 이전 프레임 손목 위치와 가장 가까운 손을 같은 손으로 본다. 일정 시간 안 보이면 그 손 기록 폐기.
4. **떨림 제거**: 손목 좌표에 지수 평활(EMA) 적용
5. **정규화**: 이동량을 `s`로 나눠 카메라와의 거리 영향 제거
6. **방향 반전 카운트**: x, y 축 각각 속도 부호가 바뀔 때, 직전 반전 이후 이동 폭이 `minAmplitude` 이상이면 반전 1회. 최근 `windowSec` 동안의 반전 횟수 = `StrokeRate` (두 축 중 큰 값)
7. **닦는 중**: `StrokeRate >= minRate`. 조건이 깨져도 `holdSec` 동안은 유지 (잠깐 끊겨도 진행이 안 멈추게)
8. **과속**: `StrokeRate >= maxRate`가 `tooFastSustainSec` 이상 이어질 때만

### 파라미터 (전부 인스펙터 노출, 값은 측정 후 확정)

| 이름 | 의미 | 시작값(임시) |
|---|---|---|
| minHandSize | 이보다 작은 손 무시 | 측정 |
| emaAlpha | 평활 강도 | 0.5 |
| minAmplitude | 반전 인정 최소 폭 (손 크기 배수) | 0.1~0.2 |
| windowSec | 반전 집계 구간 | 1.0 |
| minRate | 닦는 중 기준 | 측정 |
| maxRate | 과속 기준 | 측정 |
| holdSec | 손 끊김 허용 | 0.5~1.0 |
| tooFastSustainSec | 과속 지속 조건 | 1.0 |

## 7. 튜닝 절차

1. 화면 구석에 디버그 텍스트: 손 개수, 손별 `s`, `StrokeRate`, IsBrushing, IsTooFast
2. **실제 칫솔을 쥐고 입에 댄 상태**로 측정 (손바닥 편 상태로 측정 금지)
3. 여러 사람이 "평소처럼 닦기" / "일부러 빨리 문지르기"를 각각 10초씩 하며 `StrokeRate` 기록
4. 평소 값 하한 아래에 `minRate`, 평소와 과속 사이에 `maxRate`를 둔다
5. 가만히 든 손, 칫솔 들고 입으로 가져가는 동작에서 오판정 없는지 확인

## 8. 게임 규칙 (현재안)

- 한 판 = 약 2분, 환자가 순서대로 들어와 치료받고 나간다
- **진행도는 "닦고 있는 시간"으로 차오른다** (왕복 횟수 기준 금지 — 빨리 문지를수록 빨리 끝나는 구조가 되면 안 됨)
- 과속 시: 환자가 "아얏! 너무 아파요!" + 점수 소폭 감점. 대사에는 수 초 쿨타임
- 손이 1~2초 이상 안 보이면: 진행만 멈추고 캐릭터가 "칫솔이 안 보여!" 안내. 실패·벌칙 없음
- 손 인식 뼈대(랜드마크 선)는 게임 화면에 표시하지 않는다

## 9. 작업 순서

1. Hand Landmark 샘플에서 Num Hands = 2 확인, 칫솔 쥔 손이 계속 잡히는지 확인
2. 결과 콜백에서 손목·점9 좌표를 로그로 출력
3. `HandFrameBuffer` + `BrushDetector` + 디버그 표시
4. 7장 절차로 튜닝
5. `KeyboardBrushInput`으로 게임 루프 제작 (3~4와 병행 가능)
6. 입력을 카메라 쪽으로 교체

## 10. 나중에 (기기 빌드 시)

- Asset Loader Type → `StreamingAssets`
- Android: `libstdc++_shared.so`를 APK에 포함 (없으면 `DllNotFoundException`). 가장 쉬운 방법은 `Assets/Plugins/Android`에 넣기. 자세한 건 저장소 README "For Android"

## 11. 바꾼 화면 되돌리기 (Unity 메뉴 한 번)

팀원이 기존 화면을 다른 기능으로 바꿀 때는 지우지 않고 꺼 두며, 아래 메뉴로 언제든 되돌린다.
MCP for Unity에서는 `execute_menu_item`으로 메뉴 경로를 그대로 실행하면 된다 (Main 씬이 열려 있어야 함, 실행하면 씬이 저장된다).
메뉴는 GameFlow의 "로비 버튼 자리" 스위치와 로비 버튼의 그림·글자를 함께 바꾼다.

| 바꾼 것 | 되돌리기 메뉴 | 다시 바꾸기 메뉴 | 되돌린 뒤 확인 |
|---|---|---|---|
| 로비 **반짝 배지** 버튼 → 우주 항해 지도 | `BrushGame/Restore/Badge Button - Badge Box` | `BrushGame/Lobby/Badge Button - Space Map` | 로비 버튼이 "반짝 배지"(메달 그림), 누르면 배지함 |
| 로비 **스티커판** 버튼 → 파트너 방 (로봇 "내 친구") | `BrushGame/Restore/Sticker Button - Sticker Board` | `BrushGame/Lobby/Sticker Button - Partner Room` | 로비 버튼이 "스티커판", 누르면 칭찬 스티커판 |

- 꺼 둔 화면(배지함, 스티커판)과 그 기록 계산은 그대로 남아 있어서, 되돌리면 그동안의 기록도 보인다.
- 치료 후 흐름(완료 → 스티커판 → 우주 항해 지도 → 로비)은 위 스위치와 상관없이 그대로다.
- 화면을 새로 만들 때: `BrushGame/Add Space Map To Scene`, `BrushGame/Add Partner Room To Scene` (이미 있으면 다시 만든다).

## 12. 보호자 동의·회원가입, 양치 사진 액자

- 첫 실행: 타이틀 → **보호자 동의**([필수] 개인정보, [필수] 카메라) → **회원가입**(보호자 이름·이메일) → 나이 → 캐릭터 → 닉네임. 동의 기록이 없는 예전 저장 파일은 동의·가입만 받고 로비로 간다.
- 서버는 없다. 가입 정보는 `save.json`, 사진은 `persistentDataPath/Photos/{날짜}_{끼니}/brush_1~4.png`에만 저장한다. **사진을 서버로 보내는 코드를 넣지 않는다** (동의 화면에 그렇게 명시함). 보호자 설정의 초기화는 사진까지 지운다.
- 양치 중 구역마다 진행도 50%에서 한 장씩 찍는다 (`PhotoBooth`, 카메라 작은 창에 번쩍임). 카메라 없이 키보드로 하면 안 찍히고 액자 칸은 캐릭터 그림으로 채운다.
- 치료 후: 완료 → **사진 액자** → 스티커판 → 지도. 액자 4종은 사진 배치·모양까지 다르다: 딸기우유(하트 4), 반짝우주(크기 다른 동그라미 지그재그), 민트거품(큰 사진 1 + 방울 3), 무지개(테이프 붙인 폴라로이드 콜라주). 고른 액자는 기억한다.
- 씬에 넣기/다시 만들기: `BrushGame/Add Signup And Photo Frame To Scene` (양치 화면은 다시 만들지 않는다). 액자 추가·배치 수정은 `MainSceneBuilder.FrameThemes` (사진 칸 4개의 위치·크기·기울기·모양).
