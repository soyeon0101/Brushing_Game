using BrushGame.HandTracking;
using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   게임 화면들이 보는 양치 입력. 평소엔 카메라 손 인식, 개발 중엔 키보드로 바꿔 테스트한다.
  /// </summary>
  public class BrushInputHub : MonoBehaviour, IBrushInput
  {
    [SerializeField] private CameraBrushInput _camera;
    [SerializeField] private KeyboardBrushInput _keyboard;
    [Tooltip("켜면 카메라 대신 키보드 입력을 쓴다 (Space 닦기, Shift 과속, H 손 숨김)")]
    [SerializeField] private bool _useKeyboard;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F2;

    private IBrushInput Current => _useKeyboard || _camera == null ? _keyboard : _camera;

    public bool IsBrushing => Current.IsBrushing;
    public bool IsTooFast => Current.IsTooFast;
    public float StrokeRate => Current.StrokeRate;
    public bool HandVisible => Current.HandVisible;
    public int StrokeCount => Current.StrokeCount;

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
