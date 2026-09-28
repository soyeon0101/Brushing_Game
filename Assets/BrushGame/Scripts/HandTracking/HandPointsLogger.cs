using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   개발용. 버퍼에 들어온 손목/점9 좌표와 손 크기를 일정 간격으로 콘솔에 출력한다.
  /// </summary>
  public class HandPointsLogger : MonoBehaviour
  {
    [SerializeField] private BrushHandLandmarkerRunner _runner;
    [SerializeField, Min(0.05f)] private float _logInterval = 0.5f;

    private HandPoints[] _hands;
    private int _lastSequence;
    private int _count;
    private long _timestampMs;
    private int _framesSinceLog;
    private float _nextLogTime;
    private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(256);

    private void Update()
    {
      var buffer = _runner != null ? _runner.Buffer : null;
      if (buffer == null)
      {
        return;
      }
      _hands ??= new HandPoints[buffer.Capacity];

      if (buffer.TryRead(ref _lastSequence, _hands, out var count, out var timestampMs))
      {
        _count = count;
        _timestampMs = timestampMs;
        _framesSinceLog++;
      }

      if (Time.unscaledTime < _nextLogTime)
      {
        return;
      }
      _nextLogTime = Time.unscaledTime + _logInterval;

      _sb.Clear();
      _sb.Append("[BrushHand] t=").Append(_timestampMs)
        .Append("ms frames=").Append(_framesSinceLog)
        .Append(" hands=").Append(_count);
      for (var i = 0; i < _count; i++)
      {
        var h = _hands[i];
        _sb.Append(" | #").Append(i)
          .Append(" wrist=(").Append(h.wrist.x.ToString("F3")).Append(", ").Append(h.wrist.y.ToString("F3"))
          .Append(") p9=(").Append(h.middleMcp.x.ToString("F3")).Append(", ").Append(h.middleMcp.y.ToString("F3"))
          .Append(") s=").Append(h.Size.ToString("F3"));
      }
      Debug.Log(_sb.ToString());
      _framesSinceLog = 0;
    }
  }
}
