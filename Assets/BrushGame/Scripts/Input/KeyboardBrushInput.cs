using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   개발용 입력. 카메라 없이 게임 전체를 끝까지 플레이할 수 있게 한다.
  ///   Space = 정상 양치, Space + Shift = 과속, H(누르는 동안) = 손이 안 보임 (다른 키보다 우선).
  /// </summary>
  public class KeyboardBrushInput : MonoBehaviour, IBrushInput
  {
    [SerializeField] private KeyCode _brushKey = KeyCode.Space;
    [SerializeField] private KeyCode _fastKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode _fastKeyAlt = KeyCode.RightShift;
    [SerializeField] private KeyCode _hideHandKey = KeyCode.H;

    [Tooltip("디버그 표시용 가짜 StrokeRate")]
    [SerializeField] private float _normalRate = 5f;
    [SerializeField] private float _fastRate = 12f;

    public bool HandVisible => !Input.GetKey(_hideHandKey);
    public bool IsBrushing => HandVisible && Input.GetKey(_brushKey);
    public bool IsTooFast => IsBrushing && (Input.GetKey(_fastKey) || Input.GetKey(_fastKeyAlt));
    public float StrokeRate => IsTooFast ? _fastRate : IsBrushing ? _normalRate : 0f;
  }
}
