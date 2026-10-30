using BrushGame.HandTracking;
using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   게임 화면들이 보는 양치 입력. 평소엔 카메라 손 인식, 개발 중엔 키보드로 바꿔 테스트한다.
  ///   아이 측정값 적용·측정 기록은 카메라에만 있다 (키보드는 잴 것이 없어서 측정은 늘 버려진다).
  /// </summary>
  public class BrushInputHub : MonoBehaviour, IBrushInput
  {
    [SerializeField] private CameraBrushInput _camera;
    [SerializeField] private KeyboardBrushInput _keyboard;
    [Tooltip("켜면 카메라 대신 키보드 입력을 쓴다 (Space 닦기, Shift 과속, H 손 숨김)")]
    [SerializeField] private bool _useKeyboard;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F2;
    [Tooltip("아이별 양치 기준 (측정값으로 판정 기준 조정 + 양치할 때마다 측정). 끄면 기본 판정 기준만 쓰고 측정도 하지 않는다. " +
             "BrushGame/Restore/Brush Story - Off 메뉴가 동화와 함께 끈다")]
    [SerializeField] private bool _calibrationEnabled = true;

    private IBrushInput Current => UsingKeyboard ? _keyboard : _camera;

    public bool UsingKeyboard => _useKeyboard || _camera == null;

    public bool IsBrushing => Current.IsBrushing;
    public bool IsTooFast => Current.IsTooFast;
    public float StrokeRate => Current.StrokeRate;
    public bool HandVisible => Current.HandVisible;
    public int StrokeCount => Current.StrokeCount;

    /// <summary>아이 측정값을 판정 기준에 반영한다 (null이면 기본 기준)</summary>
    public void ApplyCalibration(BrushCalibration calibration)
    {
      if (_camera != null)
      {
        _camera.ApplyCalibration(_calibrationEnabled ? calibration : null);
      }
    }

    /// <summary>동화 화면에서만 켠다: 조금만 움직여도 닦는 것으로 본다</summary>
    public void SetLenient(bool lenient)
    {
      if (_camera != null)
      {
        _camera.SetLenient(lenient);
      }
    }

    public void BeginMeasure()
    {
      if (_camera != null && _calibrationEnabled)
      {
        _camera.BeginMeasure();
      }
    }

    public void CancelMeasure()
    {
      if (_camera != null)
      {
        _camera.CancelMeasure();
      }
    }

    /// <summary>
    ///   기록을 끝내고 쓸 만하면 calibration에 반영한다. 반영했으면 true.
    ///   fresh = 동화·다시 재기처럼 새로 잰 값 (그대로 쓴다), false = 양치 한 판 (조금씩 섞는다).
    ///   키보드로 했으면 카메라 기록은 손 움직임이 아니므로 버린다.
    /// </summary>
    public bool EndMeasure(BrushCalibration calibration, bool fresh)
    {
      if (_camera == null)
      {
        return false;
      }
      if (UsingKeyboard || !_calibrationEnabled)
      {
        _camera.CancelMeasure();
        return false;
      }
      return _camera.EndMeasure(calibration, fresh);
    }

    private void Update()
    {
      if (Input.GetKeyDown(_toggleKey))
      {
        _useKeyboard = !_useKeyboard;
        Debug.Log($"[BrushInput] {(_useKeyboard ? "키보드" : "카메라")} 입력 사용");
      }
    }
  }
}
