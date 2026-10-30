using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   개발용 판정 상태 표시. 게임 빌드에서는 끈다.
  /// </summary>
  public class BrushDebugOverlay : MonoBehaviour
  {
    [SerializeField] private CameraBrushInput _input;
    [SerializeField, Min(0.02f)] private float _refreshInterval = 0.1f;
    [SerializeField] private int _fontSize = 28;

    // 한두 프레임 놓친 건 표시하지 않는다
    private const float LostDisplaySec = 0.2f;

    private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(256);
    private string _text = "";
    private float _nextRefresh;
    private GUIStyle _style;
    private readonly GUIContent _content = new GUIContent();

    private void Update()
    {
      if (Time.unscaledTime < _nextRefresh)
      {
        return;
      }
      _nextRefresh = Time.unscaledTime + _refreshInterval;

      var detector = _input != null ? _input.Detector : null;
      if (detector == null)
      {
        _text = "BrushInput: waiting...";
        return;
      }

      _sb.Clear();
      _sb.Append("Hands: ").Append(detector.VisibleHandCount).Append('\n');
      for (var i = 0; i < detector.TrackCapacity; i++)
      {
        if (!detector.TryGetTrack(i, out var size, out var rate, out var unseenSec))
        {
          continue;
        }
        _sb.Append("  #").Append(i)
          .Append(" s=").Append(size.ToString("F3"))
          .Append(" rate=").Append(rate.ToString("F1"))
          .Append(unseenSec > LostDisplaySec ? " (lost)" : "").Append('\n');
      }
      _sb.Append("StrokeRate: ").Append(detector.StrokeRate.ToString("F1")).Append('\n')
        .Append("IsBrushing: ").Append(detector.IsBrushing ? "YES" : "no").Append('\n')
        .Append("IsTooFast: ").Append(detector.IsTooFast ? "YES" : "no").Append('\n')
        .Append("HandVisible: ").Append(detector.HandVisible ? "YES" : "no").Append('\n');

      // 실제로 쓰는 판정 기준 (아이 측정값이 있으면 기본값보다 느슨하다)
      var active = _input.ActiveSettings;
      _sb.Append("기준: 폭 ").Append(active.minAmplitude.ToString("F3"))
        .Append(" / 닦기 ").Append(active.minRate.ToString("F1"))
        .Append(" / 과속 ").Append(active.maxRate.ToString("F1")).Append('\n')
        .Append("측정 속도: ").Append(detector.MeasureRate.ToString("F1"));
      var recorder = detector.Recorder;
      if (recorder.IsRecording)
      {
        var m = recorder.Current();
        _sb.Append('\n').Append("측정 중: 폭 ").Append(m.swingMedian.ToString("F3"))
          .Append(" / 속도 ").Append(m.rateMedian.ToString("F1"))
          .Append(" / 왕복 ").Append(m.swingCount)
          .Append(" / ").Append(m.movingSec.ToString("F1")).Append("초");
      }
      _text = _sb.ToString();
    }

    private void OnGUI()
    {
      if (_style == null)
      {
        _style = new GUIStyle(GUI.skin.box)
        {
          fontSize = _fontSize,
          alignment = TextAnchor.UpperLeft,
          padding = new RectOffset(12, 12, 8, 8),
        };
        _style.normal.textColor = Color.white;
      }
      _content.text = _text;
      var size = _style.CalcSize(_content);
      GUI.Box(new Rect(10, 10, size.x, size.y), _content, _style);
    }
  }
}
