using System;
using UnityEngine;

namespace BrushGame
{
  /// <summary>진행도 구간. start부터 반응하기 시작해 end에 제거된다.</summary>
  public struct TreatmentWindow
  {
    public float start;
    public float end;

    /// <summary>이 구간 안에서의 진행 (0~1)</summary>
    public float Evaluate(float progress) => Mathf.Clamp01(Mathf.InverseLerp(start, end, progress));
  }

  /// <summary>오염 종류 하나의 일정 설정</summary>
  [Serializable]
  public class TreatmentCategorySettings
  {
    [Tooltip("이 종류의 오염이 제거되는 진행도 범위 (첫 제거 ~ 마지막 제거)")]
    public Vector2 removeRange = new Vector2(0.1f, 0.9f);
    [Tooltip("제거 전에 반응하는 구간 길이 (진행도). 범위 안에서 무작위")]
    public Vector2 windowLength = new Vector2(0.06f, 0.12f);
    [Tooltip("제거 시점을 균등 간격에서 흔드는 정도 (간격 대비 0~0.5)")]
    [Range(0f, 0.5f)] public float jitter = 0.35f;
  }

  /// <summary>
  ///   환자 한 명의 치료 일정. 오염 종류마다 제거 시점을 범위 안에 고르게 흩뿌리고,
  ///   어떤 오염이 몇 번째로 제거될지는 환자마다 섞는다 (매번 같은 순서/간격 반복 방지).
  ///   연출 없이 데이터만 가진다.
  /// </summary>
  public sealed class TreatmentPlan
  {
    public TreatmentWindow[] Plaques { get; }
    public TreatmentWindow[] Foods { get; }
    public TreatmentWindow[] Germs { get; }

    public TreatmentPlan(int plaqueCount, int foodCount, int germCount)
    {
      Plaques = new TreatmentWindow[plaqueCount];
      Foods = new TreatmentWindow[foodCount];
      Germs = new TreatmentWindow[germCount];
    }

    public void Randomize(System.Random rng, TreatmentCategorySettings plaque, TreatmentCategorySettings food, TreatmentCategorySettings germ)
    {
      Fill(Plaques, plaque, rng);
      Fill(Foods, food, rng);
      Fill(Germs, germ, rng);
    }

    private static void Fill(TreatmentWindow[] windows, TreatmentCategorySettings s, System.Random rng)
    {
      var n = windows.Length;
      if (n == 0)
      {
        return;
      }

      // 어떤 대상이 몇 번째 슬롯이 될지 섞기
      var order = new int[n];
      for (var i = 0; i < n; i++)
      {
        order[i] = i;
      }
      for (var i = n - 1; i > 0; i--)
      {
        var j = rng.Next(i + 1);
        (order[i], order[j]) = (order[j], order[i]);
      }

      var step = (s.removeRange.y - s.removeRange.x) / n;
      for (var slot = 0; slot < n; slot++)
      {
        var end = s.removeRange.x + step * (slot + 0.5f);
        end += ((float)rng.NextDouble() * 2f - 1f) * s.jitter * step;
        var length = Mathf.Lerp(s.windowLength.x, s.windowLength.y, (float)rng.NextDouble());
        windows[order[slot]] = new TreatmentWindow
        {
          start = Mathf.Max(0f, end - length),
          end = Mathf.Clamp(end, 0.01f, 0.99f),
        };
      }
    }
  }
}
