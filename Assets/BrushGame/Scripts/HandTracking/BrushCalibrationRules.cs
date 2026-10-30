using System;
using System.Globalization;
using UnityEngine;

namespace BrushGame.HandTracking
{
  /// <summary>
  ///   아이 측정값(BrushCalibration)으로 판정 기준을 정하는 규칙. 수치는 전부 임시값이며 팀 측정 후 확정한다.
  ///   기준은 기본값(씬의 BrushDetectorSettings)보다 느슨해지기만 한다. 과속 기준도 기본값(10)을 넘지 않는다
  ///   (원래 빨리 문지르는 아이에게 과속을 봐주지 않으려고).
  /// </summary>
  [Serializable]
  public class BrushCalibrationRules
  {
    [Header("측정값 → 판정 기준 (각 기준의 위 한계는 기본값)")]
    [Tooltip("최소 폭 = 아이 왕복 폭 × 이 값")]
    [Min(0f)] public float swingFactor = 0.4f;
    [Tooltip("최소 폭의 아래 한계 (손 떨림까지 세지 않게)")]
    [Min(0f)] public float minAmplitudeFloor = 0.03f;
    [Tooltip("닦는 중 기준 = 아이 속도 × 이 값")]
    [Min(0f)] public float rateFactor = 0.5f;
    [Tooltip("닦는 중 기준의 아래 한계")]
    [Min(0f)] public float minRateFloor = 0.8f;
    [Tooltip("과속 기준 = 아이 속도 × 이 값 (측정과 같은 카운터로 비교)")]
    [Min(0f)] public float fastFactor = 1.5f;
    [Tooltip("과속 기준의 아래 한계 (너무 자주 과속 알림이 뜨지 않게)")]
    [Min(0f)] public float maxRateFloor = 6f;

    [Header("동화 화면 (페이지 넘기기용 느슨한 판정)")]
    [Min(0f)] public float lenientAmplitude = 0.025f;
    [Min(0f)] public float lenientRate = 0.5f;

    [Header("측정 결과 받아들이기")]
    [Tooltip("이만큼 왕복이 모여야 측정값으로 쓴다 (느리게 닦는 아이도 동화 측정 7초 안에 모이게 작게)")]
    [Min(1)] public int minSwings = 4;
    [Tooltip("이만큼 움직인 시간이 모여야 측정값으로 쓴다 (초)")]
    [Min(0f)] public float minMovingSec = 4f;
    [Tooltip("양치 한 판이 끝날 때마다 그 판 값을 이 비율만큼 섞는다 (0 = 갱신 안 함)")]
    [Range(0f, 1f)] public float sessionBlend = 0.2f;

    /// <summary>기본 기준(baseSettings)에 아이 측정값과 동화용 느슨함을 적용해 target에 쓴다</summary>
    public void Apply(BrushDetectorSettings baseSettings, BrushCalibration calibration, bool lenient, BrushDetectorSettings target)
    {
      target.CopyFrom(baseSettings);
      if (calibration != null && calibration.measured)
      {
        target.minAmplitude = Mathf.Clamp(calibration.swing * swingFactor, Mathf.Min(minAmplitudeFloor, baseSettings.minAmplitude), baseSettings.minAmplitude);
        target.minRate = Mathf.Clamp(calibration.rate * rateFactor, Mathf.Min(minRateFloor, baseSettings.minRate), baseSettings.minRate);
        target.maxRate = Mathf.Clamp(calibration.rate * fastFactor, Mathf.Min(maxRateFloor, baseSettings.maxRate), baseSettings.maxRate);
        target.tooFastByMeasureRate = true;
      }
      if (lenient)
      {
        target.minAmplitude = Mathf.Min(target.minAmplitude, lenientAmplitude);
        target.minRate = Mathf.Min(target.minRate, lenientRate);
      }
    }

    public bool IsUsable(BrushMeasurement m) => m.swingCount >= minSwings && m.movingSec >= minMovingSec;

    /// <summary>
    ///   측정 결과를 저장값에 반영한다. 쓸 만한 결과가 아니면 아무것도 바꾸지 않고 false.
    ///   동화 측정(fresh)이거나 아직 잰 적이 없으면 그대로 쓰고, 양치 한 판 결과는 조금씩 섞는다.
    /// </summary>
    public bool Record(BrushCalibration calibration, BrushMeasurement m, bool fresh)
    {
      if (calibration == null || !IsUsable(m))
      {
        return false;
      }
      if (fresh || !calibration.measured)
      {
        calibration.swing = m.swingMedian;
        calibration.rate = m.rateMedian;
        calibration.measured = true;
        calibration.updates = 0;
        calibration.measuredAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return true;
      }
      if (sessionBlend <= 0f)
      {
        return false;
      }
      calibration.swing = Mathf.Lerp(calibration.swing, m.swingMedian, sessionBlend);
      calibration.rate = Mathf.Lerp(calibration.rate, m.rateMedian, sessionBlend);
      calibration.updates++;
      return true;
    }
  }
}
