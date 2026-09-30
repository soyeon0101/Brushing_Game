using System;
using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   BrushDetector 파라미터. 수치는 전부 임시값이며 실제 칫솔 측정 후 확정한다.
  ///   좌표 단위는 이미지 높이 = 1.
  /// </summary>
  [Serializable]
  public class BrushDetectorSettings
  {
    [Tooltip("이보다 작은 손(s = |점0 - 점9|)은 무시한다. 뒤쪽 사람 손 차단용")]
    [Min(0f)] public float minHandSize = 0.08f;

    [Tooltip("손 위치 지수 평활 강도. 클수록 원래 값을 더 따른다 (작은 움직임이 덜 깎임)")]
    [Range(0.05f, 1f)] public float emaAlpha = 0.7f;

    [Tooltip("방향 반전으로 인정할 최소 이동 폭 (손 크기 s의 배수)")]
    [Min(0f)] public float minAmplitude = 0.06f;

    [Tooltip("반전 횟수를 집계하는 최근 구간 (초)")]
    [Min(0.1f)] public float windowSec = 1.5f;

    [Tooltip("닦는 중으로 볼 최소 StrokeRate (초당 반전 횟수)")]
    [Min(0f)] public float minRate = 1.5f;

    [Tooltip("과속으로 볼 StrokeRate")]
    [Min(0f)] public float maxRate = 10f;

    [Tooltip("조건이 깨져도 닦는 중을 유지하는 시간 (초)")]
    [Min(0f)] public float holdSec = 1f;

    [Tooltip("maxRate 이상이 이만큼 이어져야 과속 (초)")]
    [Min(0f)] public float tooFastSustainSec = 1f;

    [Tooltip("이 시간 동안 안 보인 손은 추적 기록을 폐기한다 (초)")]
    [Min(0f)] public float trackLostSec = 1f;

    [Tooltip("이전 손 위치와 이 거리 안이면 같은 손으로 본다. 빠르게 움직여 잠깐 놓쳤다 다시 잡혀도 이어지도록 넉넉하게")]
    [Min(0f)] public float maxMatchDistance = 0.5f;

    [Tooltip("손을 한두 프레임 놓쳐도 이 시간 동안은 보이는 것으로 본다 (빠른 동작 중 깜빡임 방지)")]
    [Min(0f)] public float visibleGraceSec = 0.3f;
  }

  /// <summary>
  ///   손바닥 중심의 왕복 흔들림으로 양치 여부와 과속을 판정한다. 플러그인에 의존하지 않는다.
  ///   새 프레임이 오면 ProcessFrame, 매 Update마다 Tick을 호출한다.
  /// </summary>
  public sealed class BrushDetector
  {
    private readonly BrushDetectorSettings _settings;
    private readonly Track[] _tracks;

    private int _visibleCount;
    private float _lastBrushTime = float.NegativeInfinity;
    private float _fastSince = -1f;
    private float _now;
    private float _lastVisibleTime = float.NegativeInfinity;

    public BrushDetector(BrushDetectorSettings settings, int maxTracks)
    {
      _settings = settings;
      _tracks = new Track[maxTracks];
      for (var i = 0; i < maxTracks; i++)
      {
        _tracks[i] = new Track();
      }
    }

    public bool IsBrushing { get; private set; }
    public bool IsTooFast { get; private set; }
    public float StrokeRate { get; private set; }
    public bool HandVisible { get; private set; }

    /// <summary>지금까지 센 왕복(방향 반전) 횟수. 하나 늘 때마다 칫솔질 한 번으로 본다</summary>
    public int StrokeCount { get; private set; }

    /// <summary>마지막 프레임에서 minHandSize를 넘은 손 개수</summary>
    public int VisibleHandCount => _visibleCount;
    public int TrackCapacity => _tracks.Length;

    /// <summary>디버그 표시용</summary>
    public bool TryGetTrack(int index, out float size, out float rate, out float unseenSec)
    {
      var t = _tracks[index];
      size = t.size;
      rate = t.rate;
      unseenSec = _now - t.lastSeen;
      return t.active;
    }

    public void ProcessFrame(HandPoints[] hands, int count, float now)
    {
      foreach (var t in _tracks)
      {
        t.seenLastFrame = false;
      }
      _visibleCount = 0;

      var alpha = _settings.emaAlpha;
      for (var i = 0; i < count; i++)
      {
        var s = hands[i].Size;
        if (s < _settings.minHandSize)
        {
          continue;
        }
        _visibleCount++;
        _lastVisibleTime = now;

        var center = hands[i].palmCenter;
        var track = FindNearestTrack(center);
        if (track == null)
        {
          track = FindFreeTrack();
          if (track == null)
          {
            continue;
          }
          track.Start(center, s);
        }
        else
        {
          track.pos = Vector2.Lerp(track.pos, center, alpha);
          track.size = Mathf.Lerp(track.size, s, alpha);
        }

        // 이동량을 손 크기로 정규화하는 대신, 반전 기준 폭을 손 크기에 비례시킨다 (같은 효과)
        var amplitude = _settings.minAmplitude * track.size;
        var reversedX = track.x.Add(track.pos.x, amplitude, now);
        var reversedY = track.y.Add(track.pos.y, amplitude, now);
        if (reversedX || reversedY)
        {
          StrokeCount++;
        }
        track.lastSeen = now;
        track.seenLastFrame = true;
      }
    }

    public void Tick(float now)
    {
      _now = now;
      HandVisible = now - _lastVisibleTime <= _settings.visibleGraceSec;
      var maxRate = 0f;
      foreach (var t in _tracks)
      {
        if (!t.active)
        {
          continue;
        }
        if (now - t.lastSeen > _settings.trackLostSec)
        {
          t.active = false;
          t.rate = 0f;
          continue;
        }
        t.rate = Mathf.Max(t.x.Rate(now, _settings.windowSec), t.y.Rate(now, _settings.windowSec));
        maxRate = Mathf.Max(maxRate, t.rate);
      }
      StrokeRate = maxRate;

      if (StrokeRate > 0f && StrokeRate >= _settings.minRate)
      {
        _lastBrushTime = now;
      }
      IsBrushing = now - _lastBrushTime <= _settings.holdSec;

      if (StrokeRate > 0f && StrokeRate >= _settings.maxRate)
      {
        if (_fastSince < 0f)
        {
          _fastSince = now;
        }
        IsTooFast = now - _fastSince >= _settings.tooFastSustainSec;
      }
      else
      {
        _fastSince = -1f;
        IsTooFast = false;
      }
    }

    private Track FindNearestTrack(Vector2 center)
    {
      Track best = null;
      var bestDist = _settings.maxMatchDistance;
      foreach (var t in _tracks)
      {
        if (!t.active || t.seenLastFrame)
        {
          continue;
        }
        var d = Vector2.Distance(t.pos, center);
        if (d <= bestDist)
        {
          best = t;
          bestDist = d;
        }
      }
      return best;
    }

    private Track FindFreeTrack()
    {
      foreach (var t in _tracks)
      {
        if (!t.active)
        {
          return t;
        }
      }
      return null;
    }

    private sealed class Track
    {
      public bool active;
      public bool seenLastFrame;
      public Vector2 pos;   // 평활된 손바닥 중심
      public float size;    // 평활된 손 크기
      public float lastSeen;
      public float rate;
      public readonly AxisReversalCounter x = new AxisReversalCounter();
      public readonly AxisReversalCounter y = new AxisReversalCounter();

      public void Start(Vector2 center, float s)
      {
        active = true;
        pos = center;
        size = s;
        rate = 0f;
        x.Reset();
        y.Reset();
      }
    }

    /// <summary>
    ///   한 축의 방향 반전을 센다. 현재 방향의 극점에서 amplitude 이상 되돌아오면 반전 1회.
    ///   극점 기준이라 작은 떨림은 반전으로 세지 않는다.
    /// </summary>
    private sealed class AxisReversalCounter
    {
      private const int Capacity = 64;

      private readonly float[] _times = new float[Capacity];
      private int _next;
      private int _stored;

      private bool _started;
      private int _dir;       // 0: 아직 모름, 1: 증가 중, -1: 감소 중
      private float _extreme; // 현재 방향의 극점
      private float _min, _max;

      public void Reset()
      {
        _next = 0;
        _stored = 0;
        _started = false;
        _dir = 0;
      }

      /// <summary>이번 값으로 반전이 일어났으면 true</summary>
      public bool Add(float v, float amplitude, float now)
      {
        if (!_started)
        {
          _started = true;
          _min = _max = _extreme = v;
          return false;
        }

        switch (_dir)
        {
          case 0:
            _min = Mathf.Min(_min, v);
            _max = Mathf.Max(_max, v);
            if (_max - v >= amplitude)
            {
              _dir = -1;
              _extreme = v;
            }
            else if (v - _min >= amplitude)
            {
              _dir = 1;
              _extreme = v;
            }
            break;
          case 1:
            if (v > _extreme)
            {
              _extreme = v;
            }
            else if (_extreme - v >= amplitude)
            {
              _dir = -1;
              _extreme = v;
              Push(now);
              return true;
            }
            break;
          default:
            if (v < _extreme)
            {
              _extreme = v;
            }
            else if (v - _extreme >= amplitude)
            {
              _dir = 1;
              _extreme = v;
              Push(now);
              return true;
            }
            break;
        }
        return false;
      }

      public float Rate(float now, float windowSec)
      {
        var from = now - windowSec;
        var n = 0;
        for (var i = 0; i < _stored; i++)
        {
          if (_times[i] >= from)
          {
            n++;
          }
        }
        return n / windowSec;
      }

      private void Push(float now)
      {
        _times[_next] = now;
        _next = (_next + 1) % Capacity;
        if (_stored < Capacity)
        {
          _stored++;
        }
      }
    }
  }
}
