using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   손 하나에서 판정에 필요한 점들.
  ///   좌표는 이미지 높이 = 1 기준 (x에 가로세로비를 곱해 두 축의 단위를 맞춤).
  /// </summary>
  public struct HandPoints
  {
    public Vector2 wrist;      // 랜드마크 0
    public Vector2 middleMcp;  // 랜드마크 9
    public Vector2 palmCenter; // 랜드마크 0, 5, 9, 13, 17 평균. 칫솔 쥔 주먹의 움직임을 손목보다 잘 따라간다

    /// <summary>손 크기 s = |점0 - 점9|</summary>
    public float Size => Vector2.Distance(wrist, middleMcp);
  }

  /// <summary>
  ///   MediaPipe 결과 콜백(다른 스레드일 수 있음)과 Update()(메인 스레드) 사이의 좌표 전달용 버퍼.
  ///   가장 최근 프레임 하나만 보관하며, 쓰기/읽기 모두 할당이 없다.
  /// </summary>
  public sealed class HandFrameBuffer
  {
    private readonly object _lock = new object();
    private readonly HandPoints[] _hands;
    private int _count;
    private long _timestampMs;
    private int _sequence;

    public HandFrameBuffer(int capacity)
    {
      _hands = new HandPoints[capacity];
    }

    public int Capacity => _hands.Length;

    /// <summary>콜백 쪽에서 호출. src의 앞 count개를 복사해 둔다.</summary>
    public void Publish(HandPoints[] src, int count, long timestampMs)
    {
      lock (_lock)
      {
        _count = Mathf.Min(count, _hands.Length);
        System.Array.Copy(src, _hands, _count);
        _timestampMs = timestampMs;
        _sequence++;
      }
    }

    /// <summary>
    ///   메인 스레드에서 호출. lastSequence 이후 새 프레임이 있으면 dst에 복사하고 true.
    /// </summary>
    public bool TryRead(ref int lastSequence, HandPoints[] dst, out int count, out long timestampMs)
    {
      lock (_lock)
      {
        if (_sequence == lastSequence)
        {
          count = 0;
          timestampMs = 0;
          return false;
        }
        count = Mathf.Min(_count, dst.Length);
        System.Array.Copy(_hands, dst, count);
        timestampMs = _timestampMs;
        lastSequence = _sequence;
        return true;
      }
    }
  }
}
