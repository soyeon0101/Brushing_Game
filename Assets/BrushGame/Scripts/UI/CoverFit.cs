using UnityEngine;

namespace BrushGame
{
  /// <summary>
  ///   고정 크기(예: 1080x1920) 판을 부모 영역을 꽉 채우도록 통째로 확대한다. 넘치는 쪽은 잘린다.
  ///   판 안의 그림들은 기준 크기 좌표로 배치하면 화면 비율이 달라도 서로 어긋나지 않는다.
  /// </summary>
  [ExecuteAlways]
  [RequireComponent(typeof(RectTransform))]
  public class CoverFit : MonoBehaviour
  {
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
      if (size.x <= 0f || size.y <= 0f)
      {
        return;
      }
      var scale = Mathf.Max(box.x / size.x, box.y / size.y);
      rt.localScale = new Vector3(scale, scale, 1f);
    }
  }
}
