using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   고정 크기 판을 부모 영역 안에 다 들어가도록 통째로 줄인다 (키우지는 않는다).
  ///   로비 가운데 환자 자리처럼 화면이 짧은 기기에서 위아래가 겹치지 않게 할 때 쓴다.
  /// </summary>
  [ExecuteAlways]
  [RequireComponent(typeof(RectTransform))]
  public class FitInParent : MonoBehaviour
  {
    [Tooltip("이보다 크게 키우지 않는다")]
    [SerializeField] private float _maxScale = 1f;

    private void LateUpdate()
    {
      var rt = (RectTransform)transform;
      var parent = rt.parent as RectTransform;
      if (parent == null)
      {
        return;
      }
      var size = rt.rect.size;
      var box = parent.rect.size;
      if (size.x <= 0f || size.y <= 0f || box.x <= 0f || box.y <= 0f)
      {
        return;
      }
      var scale = Mathf.Min(_maxScale, box.x / size.x, box.y / size.y);
      if (!Mathf.Approximately(rt.localScale.x, scale))
      {
        rt.localScale = new Vector3(scale, scale, 1f);
      }
    }
  }
}
