using System;
using UnityEngine;
using UnityEngine.UI;

namespace BrushGame
{
  /// <summary>
  ///   4구역 양치 루프 (좌상 → 우상 → 좌하 → 우하). 한 번에 한 구역만 다룬다.
  ///   구역마다: 입 전체 보기 → 그 구역으로 줌인 → 친구와 충치균 전투(칫솔질 한 번 = 한 대) → 충치균 퇴치 → 줌아웃.
  ///   손 인식으로는 어느 구역을 닦는지 알 수 없으므로, 안내한 구역을 닦는다고 보고 닦은 시간만큼 진행한다.
  ///   구역 타이머는 닦는 동안에만 줄어든다. 멈추면 진행만 멈추고 실패 처리는 하지 않는다.
  ///   입력 해석 우선순위: 손 안 보임 > 과속 > 양치 > 멈춤.
  /// </summary>
  public class BrushingScreen : ScreenBase
  {
    private static readonly string[] ZoneNames = { "왼쪽 위", "오른쪽 위", "왼쪽 아래", "오른쪽 아래" };

    private enum Phase { Overview, ZoomIn, Brushing, Defeat, ZoomOut, Finished }

    private enum LineMode { None, Zone, Cheer, Idle, TooFast }

    [SerializeField] private BrushInputHub _input;
    [Tooltip("좌상, 우상, 좌하, 우하 순서")]
    [SerializeField] private BrushZoneView[] _zones;
    [SerializeField] private MouthZoom _zoom;
    [SerializeField] private BattleView _battle;

    [Header("환자 (그림이 없으면 임시 도형 그대로)")]
    [Tooltip("환자 얼굴(입 벌린 모습) 배경, 1080x1920 기준. 입과 함께 확대된다")]
    [SerializeField] private Image _face;
    [Tooltip("얼굴 그림이 없을 때만 보이는 임시 안내")]
    [SerializeField] private GameObject _facePlaceholder;
    [Tooltip("윗잇몸 그림 자리 (1080x640, 투명 배경 그림을 통째로 넣는다)")]
    [SerializeField] private Image _mouthTop;
    [Tooltip("아랫잇몸 그림 자리 (1080x560)")]
    [SerializeField] private Image _mouthBottom;
    [Tooltip("잇몸 그림이 없을 때만 보이는 임시 잇몸")]
    [SerializeField] private GameObject _mouthTopPlaceholder;
    [SerializeField] private GameObject _mouthBottomPlaceholder;

    [Header("카메라 작은 창")]
    [Tooltip("모든 화면이 같이 쓰는 카메라 영상. 양치 중에만 작은 창으로 옮긴다")]
    [SerializeField] private RectTransform _cameraFeed;
    [SerializeField] private RectTransform _cameraWindow;

    [Header("HUD")]
    [Tooltip("지금 구역의 남은 양치 시간 (닦는 동안에만 줄어든다)")]
    [SerializeField] private Text _timer;
    [SerializeField] private Text _zoneCount;
    [SerializeField] private RectTransform[] _zoneDots;
    [Tooltip("구역 완료 시 켜지는 점 채움")]
    [SerializeField] private GameObject[] _zoneDotFills;
    [SerializeField] private Button _pauseButton;
    [SerializeField] private Button _soundButton;
    [SerializeField] private Text _soundLabel;
    [Tooltip("소리 버튼 그림. 켬/끔 그림이 있으면 글자 대신 그림을 바꾼다")]
    [SerializeField] private Image _soundIcon;
    [SerializeField] private Sprite _soundOnSprite;
    [SerializeField] private Sprite _soundOffSprite;

    [Header("안내")]
    [SerializeField] private Image _guidePortrait;
    [SerializeField] private Text _guideText;
    [SerializeField] private GameObject _handLostOverlay;
    [Tooltip("양치를 멈췄을 때 돌아가며 나오는 문구")]
    [SerializeField, TextArea] private string[] _idleLines =
    {
      "양치를 멈추면 친구가 아파…",
      "충치균이 다시 힘을 내고 있어!",
      "칫솔을 움직여서 친구를 도와줘!",
      "충치균이 웃고 있어! 슥삭슥삭 해줘!",
    };
    [Tooltip("닦는 동안 돌아가며 나오는 응원 문구")]
    [SerializeField, TextArea] private string[] _cheerLines =
    {
      "좋아! 충치균이 약해지고 있어!",
      "슥삭슥삭! 계속 공격해!",
      "잘하고 있어! 조금만 더!",
    };
    [SerializeField] private string _tooFastLine = "조금만 천천히~ 살살 닦아줘!";
    [Tooltip("같은 종류 문구를 바꿔 보여주는 간격 (초)")]
    [SerializeField, Min(0.5f)] private float _lineSec = 2.5f;
    [Tooltip("구역 시작 안내를 보여주는 시간 (초)")]
    [SerializeField, Min(0f)] private float _zoneLineSec = 2f;
    [Tooltip("이만큼 안 닦으면 멈춤 문구를 보여준다 (초)")]
    [SerializeField, Min(0f)] private float _idleLineDelay = 1f;

    [Header("일시정지")]
    [SerializeField] private GameObject _pauseOverlay;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _quitButton;

    [Header("진행")]
    [Tooltip("과속 중 진행 속도 배율. 오검출이어도 완전히 멈추지 않게")]
    [SerializeField, Range(0f, 1f)] private float _tooFastMultiplier = 0.4f;
    [Tooltip("손이 이만큼 안 보이면 안내한다 (초)")]
    [SerializeField, Min(0f)] private float _handLostDelay = 1.5f;
    [Tooltip("요정의 마법 한 바퀴(반짝이 한 번)에 필요한 칫솔질 방향 전환 횟수. 4 = 왕복 2번")]
    [SerializeField, Min(1)] private int _strokesPerBurst = 4;

    [Header("줌 연출")]
    [SerializeField, Min(1f)] private float _zoomScale = 2.3f;
    [Tooltip("확대한 구역 가운데가 올 높이. 위 구역은 +, 아래 구역은 - 로 쓴다")]
    [SerializeField] private float _focusY = 260f;
    [Tooltip("확대한 구역 가운데에서 전투 무대까지 거리 (입 안쪽, 잇몸이 없는 쪽으로)")]
    [SerializeField] private float _battleOffset = 390f;
    [SerializeField, Min(0f)] private float _overviewSec = 1f;
    [SerializeField, Min(0.01f)] private float _zoomSec = 0.7f;
    [Tooltip("충치균 퇴치 + 구역 반짝임을 보여주는 시간 (초)")]
    [SerializeField, Min(0f)] private float _defeatSec = 1.5f;
    [Tooltip("마지막 구역 후 줌아웃하고 완료 화면으로 가기 전 시간 (초)")]
    [SerializeField, Min(0f)] private float _finishHoldSec = 1.2f;

    private readonly float[] _progress = new float[4];
    private int _zoneSeconds = 25;
    private int _zone;
    private Phase _phase;
    private float _phaseT;
    private bool _paused;
    private float _handLostT;
    private float _idleT;
    private int _lastStrokes;
    private int _zoneStrokes;
    private LineMode _lineMode;
    private float _nextLineTime;
    private int _idleLineIndex;
    private int _cheerLineIndex;

    private ArtSwap _faceArt, _mouthTopArt, _mouthBottomArt;
    private Transform _feedParent;
    private int _feedIndex;
    private Vector2 _feedAnchorMin, _feedAnchorMax, _feedOffsetMin, _feedOffsetMax;

    public event Action Completed;
    public event Action QuitRequested;

    private string ZoneLine => $"{ZoneNames[_zone]}를 닦아줘!";
    private bool IsUpper(int zone) => zone < 2;

    public void Setup(PatientInfo patient, int zoneSeconds)
    {
      ArtSlot.Apply(_guidePortrait, patient.Portrait);
      _zoneSeconds = Mathf.Clamp(zoneSeconds, GuardianSettings.MinZoneSeconds, GuardianSettings.MaxZoneSeconds);

      // 환자가 바뀌면 그림도 바뀌어야 하므로, 그림이 없는 환자는 임시 도형으로 되돌린다
      _faceArt ??= new ArtSwap(_face);
      _mouthTopArt ??= new ArtSwap(_mouthTop);
      _mouthBottomArt ??= new ArtSwap(_mouthBottom);
      _facePlaceholder.SetActive(!_faceArt.Apply(patient.face));
      _mouthTopPlaceholder.SetActive(!_mouthTopArt.Apply(patient.mouthTop));
      _mouthBottomPlaceholder.SetActive(!_mouthBottomArt.Apply(patient.mouthBottom));
    }

    public override void OnShow()
    {
      for (var i = 0; i < _zones.Length; i++)
      {
        _progress[i] = 0f;
        _zones[i].ResetDirty();
      }
      _paused = false;
      _handLostT = 0f;
      _pauseOverlay.SetActive(false);
      _handLostOverlay.SetActive(false);
      _zoom.ResetView();
      _battle.Hide();
      DockCamera();
      RefreshSoundLabel();
      StartOverview(0);
    }

    public override void OnHide()
    {
      _pauseOverlay.SetActive(false);
      _handLostOverlay.SetActive(false);
      UndockCamera();
    }

    // ---------- 카메라 작은 창 ----------

    private void DockCamera()
    {
      if (_cameraFeed == null || _cameraWindow == null || _feedParent != null)
      {
        return;
      }
      _feedParent = _cameraFeed.parent;
      _feedIndex = _cameraFeed.GetSiblingIndex();
      _feedAnchorMin = _cameraFeed.anchorMin;
      _feedAnchorMax = _cameraFeed.anchorMax;
      _feedOffsetMin = _cameraFeed.offsetMin;
      _feedOffsetMax = _cameraFeed.offsetMax;

      _cameraFeed.SetParent(_cameraWindow, false);
      _cameraFeed.anchorMin = Vector2.zero;
      _cameraFeed.anchorMax = Vector2.one;
      _cameraFeed.offsetMin = Vector2.zero;
      _cameraFeed.offsetMax = Vector2.zero;
    }

    private void UndockCamera()
    {
      if (_feedParent == null)
      {
        return;
      }
      _cameraFeed.SetParent(_feedParent, false);
      _cameraFeed.SetSiblingIndex(_feedIndex);
      _cameraFeed.anchorMin = _feedAnchorMin;
      _cameraFeed.anchorMax = _feedAnchorMax;
      _cameraFeed.offsetMin = _feedOffsetMin;
      _cameraFeed.offsetMax = _feedOffsetMax;
      _feedParent = null;
    }

    private void Awake()
    {
      _pauseButton.onClick.AddListener(() => SetPaused(true));
      _resumeButton.onClick.AddListener(() => SetPaused(false));
      _quitButton.onClick.AddListener(() => QuitRequested?.Invoke());
      _soundButton.onClick.AddListener(ToggleSound);
    }

    private void Update()
    {
      if (_paused || _phase == Phase.Finished)
      {
        return;
      }
      _phaseT += Time.deltaTime;

      switch (_phase)
      {
        case Phase.Overview:
          if (_phaseT >= _overviewSec)
          {
            StartZoomIn();
          }
          break;
        case Phase.ZoomIn:
          if (!_zoom.IsMoving)
          {
            StartBrushing();
          }
          break;
        case Phase.Brushing:
          UpdateBrushing(Time.deltaTime);
          break;
        case Phase.Defeat:
          if (_phaseT >= _defeatSec)
          {
            StartZoomOut();
          }
          break;
        case Phase.ZoomOut:
          if (_zoom.IsMoving)
          {
            break;
          }
          if (_zone + 1 < _zones.Length)
          {
            StartOverview(_zone + 1);
          }
          else if (_phaseT >= _zoomSec + _finishHoldSec)
          {
            _phase = Phase.Finished;
            Completed?.Invoke();
          }
          break;
      }
      RefreshHud();
    }

    // ---------- 단계 ----------

    /// <summary>입 전체를 보여주며 이번에 닦을 구역을 알려준다</summary>
    private void StartOverview(int zone)
    {
      _zone = zone;
      SetPhase(Phase.Overview);
      for (var i = 0; i < _zones.Length; i++)
      {
        _zones[i].SetHighlight(i == zone);
      }
      ShowGuide(zone == 0 ? $"{ZoneNames[zone]}부터 시작해볼까?" : $"다음은 {ZoneNames[zone]}야!");
      RefreshHud();
    }

    private void StartZoomIn()
    {
      SetPhase(Phase.ZoomIn);
      var focus = new Vector2(0f, IsUpper(_zone) ? _focusY : -_focusY);
      _zoom.Focus((RectTransform)_zones[_zone].transform, focus, _zoomScale, _zoomSec);
    }

    private void StartBrushing()
    {
      SetPhase(Phase.Brushing);
      _handLostT = 0f;
      _idleT = 0f;
      _lastStrokes = _input.StrokeCount;
      _zoneStrokes = 0;
      // 위 구역은 입 안쪽이 아래에, 아래 구역은 위에 있으므로 무대를 그쪽에 둔다
      var upper = IsUpper(_zone);
      var y = upper ? _focusY - _battleOffset : -_focusY + _battleOffset;
      _battle.Enter(new Vector2(0f, y), hpBelow: upper);
      _lineMode = LineMode.None;
      SetLine(LineMode.Zone);
    }

    private void UpdateBrushing(float dt)
    {
      // 손이 안 보이면 양치/과속 판정은 적용하지 않는다
      var visible = _input.HandVisible;
      _handLostT = visible ? 0f : _handLostT + dt;
      _handLostOverlay.SetActive(_handLostT >= _handLostDelay);
      var brushing = visible && _input.IsBrushing;
      var tooFast = brushing && _input.IsTooFast;

      // 칫솔질(방향 전환)이 쌓이는 만큼 요정의 마법 동작이 넘어가고, _strokesPerBurst번마다 반짝이가 터진다
      var strokes = _input.StrokeCount;
      if (strokes != _lastStrokes)
      {
        if (visible && strokes > _lastStrokes)
        {
          _zoneStrokes += strokes - _lastStrokes;
          _battle.SetSwingProgress(_zoneStrokes / (float)_strokesPerBurst);
        }
        _lastStrokes = strokes;
      }

      // 과속이면 느려질 뿐 멈추거나 줄지 않는다
      if (brushing)
      {
        var multiplier = tooFast ? _tooFastMultiplier : 1f;
        _progress[_zone] = Mathf.Min(1f, _progress[_zone] + dt * multiplier / _zoneSeconds);
      }
      _idleT = visible && !brushing ? _idleT + dt : 0f;
      var idle = _idleT >= _idleLineDelay;

      _battle.SetHp(1f - _progress[_zone]);
      _battle.SetIdle(idle);
      _zones[_zone].SetState(_progress[_zone], brushing);
      if (visible)
      {
        UpdateLine(brushing, tooFast, idle);
      }

      if (_progress[_zone] >= 1f)
      {
        StartDefeat();
      }
    }

    private void StartDefeat()
    {
      SetPhase(Phase.Defeat);
      _zones[_zone].PlayComplete();
      _battle.PlayDefeat();
      _handLostOverlay.SetActive(false);
      ShowGuide(_zone + 1 >= _zones.Length ? "충치균을 모두 물리쳤어! 반짝반짝!" : "충치균을 물리쳤어! 반짝반짝!");
    }

    private void StartZoomOut()
    {
      SetPhase(Phase.ZoomOut);
      _battle.Hide();
      _zoom.ZoomOut(_zoomSec);
      if (_zone + 1 >= _zones.Length)
      {
        ShowGuide("다 닦았어! 정말 고마워!");
      }
    }

    private void SetPhase(Phase phase)
    {
      _phase = phase;
      _phaseT = 0f;
    }

    // ---------- 안내 문구 ----------

    private void UpdateLine(bool brushing, bool tooFast, bool idle)
    {
      LineMode want;
      if (tooFast)
      {
        want = LineMode.TooFast;
      }
      else if (idle)
      {
        want = LineMode.Idle;
      }
      else if (brushing)
      {
        want = _lineMode == LineMode.Zone && _phaseT < _zoneLineSec ? LineMode.Zone : LineMode.Cheer;
      }
      else
      {
        // 잠깐 멈춘 정도면 지금 문구를 유지
        want = _lineMode == LineMode.TooFast ? LineMode.Cheer : _lineMode;
      }

      if (want != _lineMode || (Time.time >= _nextLineTime && (want == LineMode.Idle || want == LineMode.Cheer)))
      {
        SetLine(want);
      }
    }

    private void SetLine(LineMode mode)
    {
      _lineMode = mode;
      _nextLineTime = Time.time + _lineSec;
      switch (mode)
      {
        case LineMode.Zone:
          ShowGuide(ZoneLine);
          break;
        case LineMode.Cheer:
          ShowGuide(Next(_cheerLines, ref _cheerLineIndex));
          break;
        case LineMode.Idle:
          ShowGuide(Next(_idleLines, ref _idleLineIndex));
          break;
        case LineMode.TooFast:
          ShowGuide(_tooFastLine);
          break;
      }
    }

    private string Next(string[] lines, ref int index)
    {
      if (lines == null || lines.Length == 0)
      {
        return ZoneLine;
      }
      var line = lines[index % lines.Length];
      index++;
      return line;
    }

    /// <summary>모든 안내 문구는 여기를 거친다 (나중에 음성 안내를 붙일 자리)</summary>
    private void ShowGuide(string line)
    {
      _guideText.text = line;
    }

    // ---------- HUD ----------

    private void RefreshHud()
    {
      var sec = Mathf.CeilToInt((1f - _progress[_zone]) * _zoneSeconds);
      _timer.text = $"{sec / 60}:{sec % 60:00}";
      _zoneCount.text = $"{_zone + 1}/{_zones.Length}";

      for (var i = 0; i < _zoneDots.Length; i++)
      {
        _zoneDotFills[i].SetActive(_progress[i] >= 1f);
        _zoneDots[i].localScale = Vector3.one * (i == _zone && _phase != Phase.Finished ? 1.3f : 1f);
      }
    }

    private void SetPaused(bool paused)
    {
      _paused = paused;
      _pauseOverlay.SetActive(paused);
    }

    private void ToggleSound()
    {
      var settings = SaveStore.Data.settings;
      settings.sound = !settings.sound;
      settings.ApplyAudio();
      SaveStore.Save();
      RefreshSoundLabel();
    }

    private void RefreshSoundLabel()
    {
      var on = SaveStore.Data.settings.sound;
      var useIcon = _soundIcon != null && _soundOnSprite != null && _soundOffSprite != null;
      _soundLabel.gameObject.SetActive(!useIcon);
      _soundLabel.text = on ? "소리\n켬" : "소리\n끔";
      if (useIcon)
      {
        _soundIcon.sprite = on ? _soundOnSprite : _soundOffSprite;
      }
    }

    /// <summary>환자마다 바뀌는 그림 자리. 그림이 없으면 처음 모습(임시 도형)으로 되돌린다</summary>
    private sealed class ArtSwap
    {
      private readonly Image _image;
      private readonly Sprite _sprite;
      private readonly Color _color;
      private readonly Image.Type _type;

      public ArtSwap(Image image)
      {
        _image = image;
        _sprite = image.sprite;
        _color = image.color;
        _type = image.type;
      }

      /// <summary>그림을 넣었으면 true</summary>
      public bool Apply(Sprite art)
      {
        if (art == null)
        {
          _image.sprite = _sprite;
          _image.color = _color;
          _image.type = _type;
          return false;
        }
        // 그림은 자리와 같은 크기로 그려져 있으므로 그대로 채운다
        _image.sprite = art;
        _image.type = Image.Type.Simple;
        _image.preserveAspect = false;
        _image.color = Color.white;
        return true;
      }
    }
  }
}
