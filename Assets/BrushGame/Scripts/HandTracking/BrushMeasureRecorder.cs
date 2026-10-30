using System.Collections.Generic;

namespace BrushGame.HandTracking
{
  /// <summary>한 번 잰 결과. 가운데값이라 처음 몇 초 신나서 문지른 값 같은 튀는 값에 덜 흔들린다</summary>
  public struct BrushMeasurement
  {
    /// <summary>한 번 왕복한 폭의 가운데값 (손 크기 배수)</summary>
    public float swingMedian;
    /// <summary>움직이는 동안의 초당 방향 전환 횟수 가운데값</summary>
    public float rateMedian;
    /// <summary>기록된 왕복 수</summary>
    public int swingCount;
    /// <summary>움직이고 있던 시간 (초)</summary>
    public float movingSec;
  }

  /// <summary>
  ///   Begin ~ End 사이의 왕복 폭과 속도를 모은다. BrushDetector가 채우고, 화면은 Begin/End만 부른다.
  ///   속도는 일정 간격으로 표본을 뜨며, 손이 안 보이거나 움직이지 않는 순간은 빼고 모은다.
  /// </summary>
  public sealed class BrushMeasureRecorder
  {
    private const float SampleInterval = 0.2f;

    private readonly List<float> _swings = new List<float>(256);
    private readonly List<float> _rates = new List<float>(512);
    private float _nextSample;

    public bool IsRecording { get; private set; }
    public int SwingCount => _swings.Count;
    public float MovingSec => _rates.Count * SampleInterval;

    public void Begin()
    {
      _swings.Clear();
      _rates.Clear();
      _nextSample = float.NegativeInfinity;
      IsRecording = true;
    }

    /// <summary>기록을 멈추고 결과를 돌려준다. 하나도 없으면 0</summary>
    public BrushMeasurement End()
    {
      IsRecording = false;
      return Current();
    }

    public void Cancel()
    {
      IsRecording = false;
      _swings.Clear();
      _rates.Clear();
    }

    /// <summary>지금까지 모은 값 (기록 중에도 볼 수 있다. 디버그 표시용)</summary>
    public BrushMeasurement Current() => new BrushMeasurement
    {
      swingMedian = Median(_swings),
      rateMedian = Median(_rates),
      swingCount = _swings.Count,
      movingSec = MovingSec,
    };

    internal void AddSwing(float swing)
    {
      if (IsRecording && swing > 0f)
      {
        _swings.Add(swing);
      }
    }

    internal void SampleRate(float rate, float now)
    {
      if (!IsRecording || now < _nextSample)
      {
        return;
      }
      _nextSample = now + SampleInterval;
      if (rate > 0f)
      {
        _rates.Add(rate);
      }
    }

    private static float Median(List<float> values)
    {
      if (values.Count == 0)
      {
        return 0f;
      }
      var sorted = values.ToArray();
      System.Array.Sort(sorted);
      var mid = sorted.Length / 2;
      return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
    }
  }
}
