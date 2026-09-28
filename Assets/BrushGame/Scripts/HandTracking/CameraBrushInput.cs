using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   카메라 손 추적 결과를 BrushDetector에 넘기고 IBrushInput으로 노출한다.
  /// </summary>
  public class CameraBrushInput : MonoBehaviour, IBrushInput
  {
    [SerializeField] private BrushHandLandmarkerRunner _runner;
    [SerializeField] private BrushDetectorSettings _settings = new BrushDetectorSettings();

    private BrushDetector _detector;
    private HandPoints[] _hands;
    private int _lastSequence;

    public BrushDetector Detector => _detector;

    public bool IsBrushing => _detector != null && _detector.IsBrushing;
    public bool IsTooFast => _detector != null && _detector.IsTooFast;
    public float StrokeRate => _detector != null ? _detector.StrokeRate : 0f;
    public bool HandVisible => _detector != null && _detector.HandVisible;

    private void Update()
    {
      var buffer = _runner != null ? _runner.Buffer : null;
      if (buffer == null)
      {
        return;
      }
      if (_detector == null)
      {
        _hands = new HandPoints[buffer.Capacity];
        // 손이 잠깐 엇갈려 새 추적이 생겨도 자리가 남도록 여유를 둔다
        _detector = new BrushDetector(_settings, buffer.Capacity + 2);
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
