using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   카메라 손 추적 결과를 BrushDetector에 넘기고 IBrushInput으로 노출한다.
  ///   _settings는 기본 판정 기준이고, 실제 판정은 아이 측정값(BrushCalibration)과 동화용 느슨함을 적용한 값으로 한다.
  /// </summary>
  public class CameraBrushInput : MonoBehaviour, IBrushInput
  {
    [SerializeField] private BrushHandLandmarkerRunner _runner;
    [Tooltip("기본 판정 기준. 아이 측정값이 있으면 BrushCalibrationRules가 이보다 느슨하게 바꿔 쓴다")]
    [SerializeField] private BrushDetectorSettings _settings = new BrushDetectorSettings();
    [SerializeField] private BrushCalibrationRules _calibrationRules = new BrushCalibrationRules();

    private readonly BrushDetectorSettings _active = new BrushDetectorSettings();
    private BrushCalibration _calibration;
    private bool _lenient;
    private bool _measurePending;   // 카메라가 아직 준비 안 됐을 때 Begin을 부르면 준비되는 대로 시작

    private BrushDetector _detector;
    private HandPoints[] _hands;
    private int _lastSequence;

    public BrushDetector Detector => _detector;

    /// <summary>지금 실제로 쓰는 판정 기준 (디버그 표시용)</summary>
    public BrushDetectorSettings ActiveSettings => _active;
    public BrushCalibrationRules CalibrationRules => _calibrationRules;

    public bool IsBrushing => _detector != null && _detector.IsBrushing;
    public bool IsTooFast => _detector != null && _detector.IsTooFast;
    public float StrokeRate => _detector != null ? _detector.StrokeRate : 0f;
    public bool HandVisible => _detector != null && _detector.HandVisible;
    public int StrokeCount => _detector != null ? _detector.StrokeCount : 0;

    /// <summary>아이 측정값을 판정 기준에 반영한다 (null이면 기본 기준)</summary>
    public void ApplyCalibration(BrushCalibration calibration)
    {
      _calibration = calibration;
      RefreshSettings();
    }

    /// <summary>동화 화면: 조금만 움직여도 닦는 것으로 본다 (페이지가 막히지 않게)</summary>
    public void SetLenient(bool lenient)
    {
      _lenient = lenient;
      RefreshSettings();
    }

    public void BeginMeasure()
    {
      var detector = EnsureDetector();
      _measurePending = detector == null;
      detector?.Recorder.Begin();
    }

    public void CancelMeasure()
    {
      _measurePending = false;
      _detector?.Recorder.Cancel();
    }

    /// <summary>기록을 끝내고 쓸 만하면 calibration에 반영한다. 반영했으면 true</summary>
    public bool EndMeasure(BrushCalibration calibration, bool fresh)
    {
      _measurePending = false;
      if (_detector == null || !_detector.Recorder.IsRecording)
      {
        return false;
      }
      var m = _detector.Recorder.End();
      var recorded = _calibrationRules.Record(calibration, m, fresh);
      Debug.Log($"[BrushInput] 측정 {(recorded ? "반영" : "버림")}: 왕복 폭 {m.swingMedian:F3}, 초당 {m.rateMedian:F1}번, 왕복 {m.swingCount}번, 움직인 시간 {m.movingSec:F1}초");
      return recorded;
    }

    private void Awake()
    {
      RefreshSettings();
    }

    private void RefreshSettings()
    {
      _calibrationRules.Apply(_settings, _calibration, _lenient, _active);
    }

    private BrushDetector EnsureDetector()
    {
      var buffer = _runner != null ? _runner.Buffer : null;
      if (_detector == null && buffer != null)
      {
        _hands = new HandPoints[buffer.Capacity];
        // 손이 잠깐 엇갈려 새 추적이 생겨도 자리가 남도록 여유를 둔다
        _detector = new BrushDetector(_active, buffer.Capacity + 2);
        if (_measurePending)
        {
          _measurePending = false;
          _detector.Recorder.Begin();
        }
      }
      return _detector;
    }

    private void Update()
    {
      var buffer = _runner != null ? _runner.Buffer : null;
      if (buffer == null || EnsureDetector() == null)
      {
        return;
      }

      var now = Time.unscaledTime;
      if (buffer.TryRead(ref _lastSequence, _hands, out var count, out _))
      {
        _detector.ProcessFrame(_hands, count, now);
      }
      _detector.Tick(now);
    }
  }
}
