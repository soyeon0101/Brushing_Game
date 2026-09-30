using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   입 전체(윗잇몸 + 아랫잇몸)를 담은 rig를 확대/이동해서 한 구역에 카메라가 다가간 것처럼 보이게 한다.
  /// </summary>
  public class MouthZoom : MonoBehaviour
  {
    [Tooltip("확대/이동할 입 전체. 부모 기준 가운데 pivot")]
    [SerializeField] private RectTransform _rig;

    private Vector2 _fromPos, _toPos;
    private float _fromScale = 1f, _toScale = 1f;
    private float _t, _duration;

    public bool IsMoving => _t < _duration;

    /// <summary>target(구역)의 가운데가 부모 좌표 focus에 오도록 scale배 확대</summary>
    public void Focus(RectTransform target, Vector2 focus, float scale, float seconds)
    {
      // rig 자체의 현재 확대/이동과 상관없는 rig 안쪽 좌표
      Vector2 center = _rig.InverseTransformPoint(target.TransformPoint(target.rect.center));
      MoveTo(focus - center * scale, scale, seconds);
    }

    public void ZoomOut(float seconds)
    {
      MoveTo(Vector2.zero, 1f, seconds);
    }

    public void ResetView()
    {
      _t = _duration = 0f;
      _fromPos = _toPos = Vector2.zero;
      _fromScale = _toScale = 1f;
      Apply(1f);
    }

    private void MoveTo(Vector2 pos, float scale, float seconds)
    {
      _fromPos = _rig.localPosition;
      _fromScale = _rig.localScale.x;
      _toPos = pos;
      _toScale = scale;
      _t = 0f;
      _duration = Mathf.Max(0.0001f, seconds);
    }

    private void Update()
    {
      if (!IsMoving)
      {
        return;
      }
      _t = Mathf.Min(_t + Time.deltaTime, _duration);
      Apply(Mathf.SmoothStep(0f, 1f, _t / _duration));
    }

    private void Apply(float k)
    {
      _rig.localPosition = Vector2.Lerp(_fromPos, _toPos, k);
      _rig.localScale = Vector3.one * Mathf.Lerp(_fromScale, _toScale, k);
    }
  }
}
