using System.Collections.Generic;
using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   우주 항해 지도의 배치 계산 (화면과 상관없는 순수 계산).
  ///   행성은 아래에서 위로 좌우를 번갈아 놓고, 행성 사이는 3차 베지어 S자 곡선으로 잇는다.
  ///   발판은 곡선의 길이(호 길이)를 같은 간격으로 나눠 놓는다. t값을 같은 간격으로 나누면 곡선이 휘는 곳에서 간격이 들쭉날쭉해진다.
  ///   좌표는 지도 아래 가운데가 (0, 0), 위가 +y.
  /// </summary>
  public class MapPath
  {
    public struct Placement
    {
      public Vector2 position;
      /// <summary>곡선이 나아가는 방향 (위가 0도, 반시계 +)</summary>
      public float angle;
    }

    public struct Settings
    {
      public int planetCount;
      public int padsPerPlanet;
      public float segmentHeight;
      public float horizontalOffset;
      public float curveStrength;
      public float dotSpacing;
      public float bottomMargin;
      public float topMargin;
      /// <summary>행성 중심에서 이만큼은 발판과 점을 놓지 않는다 (행성 그림과 겹치지 않게)</summary>
      public float planetClearance;
    }

    private const int Samples = 256;

    public readonly List<Vector2> planets = new List<Vector2>();
    /// <summary>전체 발판. i번째 구간(행성 i → i+1)의 발판이 padsPerPlanet개씩 순서대로</summary>
    public readonly List<Placement> pads = new List<Placement>();
    public readonly List<Placement> dots = new List<Placement>();
    public float Height { get; private set; }

    public MapPath(Settings s)
    {
      var count = Mathf.Max(1, s.planetCount);
      for (var i = 0; i < count; i++)
      {
        var x = (i % 2 == 0 ? -1f : 1f) * s.horizontalOffset;
        planets.Add(new Vector2(x, s.bottomMargin + i * s.segmentHeight));
      }
      Height = planets[count - 1].y + s.topMargin;

      for (var i = 0; i + 1 < count; i++)
      {
        var p0 = planets[i];
        var p3 = planets[i + 1];
        var dx = p3.x - p0.x;
        var h = p3.y - p0.y;
        // 처음에는 바깥쪽으로 나갔다가 가운데를 가로질러 다음 행성 바깥쪽에서 들어온다 (S자)
        var p1 = new Vector2(p0.x - dx * s.curveStrength, p0.y + h * 0.25f);
        var p2 = new Vector2(p3.x + dx * s.curveStrength, p3.y - h * 0.25f);
        AddSegment(p0, p1, p2, p3, s);
      }
    }

    private void AddSegment(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Settings s)
    {
      // 호 길이 표: 곡선을 잘게 나눠 누적 길이를 구한다
      var points = new Vector2[Samples + 1];
      var lengths = new float[Samples + 1];
      for (var i = 0; i <= Samples; i++)
      {
        points[i] = Bezier(p0, p1, p2, p3, i / (float)Samples);
        lengths[i] = i == 0 ? 0f : lengths[i - 1] + Vector2.Distance(points[i - 1], points[i]);
      }
      var total = lengths[Samples];
      var start = Mathf.Min(s.planetClearance, total * 0.3f);
      var end = total - start;

      var n = Mathf.Max(1, s.padsPerPlanet);
      var step = (end - start) / n;
      for (var k = 0; k < n; k++)
      {
        pads.Add(At(points, lengths, start + step * (k + 0.5f)));
      }
      var spacing = Mathf.Max(1f, s.dotSpacing);
      for (var d = start; d <= end; d += spacing)
      {
        dots.Add(At(points, lengths, d));
      }
    }

    /// <summary>곡선 처음부터 distance만큼 간 자리와 방향</summary>
    private static Placement At(Vector2[] points, float[] lengths, float distance)
    {
      var i = System.Array.BinarySearch(lengths, distance);
      if (i < 0)
      {
        i = ~i;
      }
      i = Mathf.Clamp(i, 1, lengths.Length - 1);
      var span = lengths[i] - lengths[i - 1];
      var k = span > 0f ? (distance - lengths[i - 1]) / span : 0f;
      var dir = points[i] - points[i - 1];
      return new Placement
      {
        position = Vector2.Lerp(points[i - 1], points[i], k),
        angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f,
      };
    }

    private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
      var u = 1f - t;
      return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }
  }
}
